using MaksymShostak.OniModPipeline.Diagnostics;
using Tomlyn;
using Tomlyn.Model;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed class ModProfileLoader
{
    private const int SupportedSchemaVersion = 1;
    private const int DefaultListingByteLimit = 8000;

    private static readonly IReadOnlyDictionary<string, ISet<string>> AllowedKeys =
        new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
        {
            [""] = new HashSet<string>(
                [
                    "schema-version",
                    "mod",
                    "build",
                    "package-files",
                    "workshop-listing",
                    "local-install",
                    "test-projects",
                    "acceptance-checks"
                ],
                StringComparer.Ordinal),
            ["mod"] = new HashSet<string>(
                ["mod-yaml", "mod-info-yaml"],
                StringComparer.Ordinal),
            ["build"] = new HashSet<string>(
                [
                    "entry-point",
                    "configuration",
                    "game-managed-directory-property",
                    "primary-output",
                    "merge-inputs"
                ],
                StringComparer.Ordinal),
            ["package-files[]"] = new HashSet<string>(
                ["source", "destination"],
                StringComparer.Ordinal),
            ["workshop-listing"] = new HashSet<string>(
                [
                    "description",
                    "change-notes",
                    "preview",
                    "mod-types",
                    "dlc-compatibility",
                    "description-byte-limit",
                    "change-notes-byte-limit"
                ],
                StringComparer.Ordinal),
            ["local-install"] = new HashSet<string>(
                ["directory-name"],
                StringComparer.Ordinal),
            ["test-projects[]"] = new HashSet<string>(
                ["id", "path", "required"],
                StringComparer.Ordinal),
            ["acceptance-checks[]"] = new HashSet<string>(
                ["id", "title", "required", "setup", "action", "expected"],
                StringComparer.Ordinal)
        };

    internal OperationResult<ModProfile> Load(string manifestPath)
    {
        var displayPath = string.IsNullOrWhiteSpace(manifestPath)
            ? "<empty>"
            : manifestPath;

        string resolvedManifestPath;
        try
        {
            resolvedManifestPath = Path.GetFullPath(manifestPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(DiagnosticCatalog.InvalidProfileValue(
                "manifest-path",
                displayPath,
                "the manifest path must be a valid filesystem path."));
        }

        if (!File.Exists(resolvedManifestPath))
        {
            return Failure(DiagnosticCatalog.DeclaredInputMissing(
                manifestPath,
                resolvedManifestPath));
        }

        var manifestDirectory = Path.GetDirectoryName(resolvedManifestPath)!;
        var containedManifest = ContainedPathResolver.ResolveExistingFile(
            manifestDirectory,
            Path.GetFileName(resolvedManifestPath));
        if (!containedManifest.IsSuccess)
        {
            return new OperationResult<ModProfile>(
                null,
                containedManifest.Diagnostics,
                containedManifest.ExitCode);
        }

        resolvedManifestPath = containedManifest.Value!;

        TomlTable root;
        try
        {
            var content = File.ReadAllText(resolvedManifestPath);
            root = TomlSerializer.Deserialize<TomlTable>(content)
                ?? throw new TomlException("The TOML document did not produce a root table.");
        }
        catch (Exception exception) when (
            exception is TomlException or IOException or UnauthorizedAccessException)
        {
            return Failure(DiagnosticCatalog.InvalidProfileValue(
                "document",
                resolvedManifestPath,
                exception.Message));
        }

        try
        {
            ValidateAllowedKeys(root);

            var schemaVersion = ReadRequiredInt(root, "schema-version", "schema-version");
            if (schemaVersion != SupportedSchemaVersion)
            {
                return Failure(DiagnosticCatalog.UnsupportedSchemaVersion(
                    schemaVersion,
                    resolvedManifestPath));
            }

            var modTable = ReadRequiredTable(root, "mod", "mod");
            var build = ReadBuildProfile(root);
            var packageFiles = ReadOptionalTableArray(root, "package-files", "package-files")
                .Select((table, index) => new PackageFileMapping(
                    ReadRequiredString(table, "source", $"package-files[{index}].source"),
                    ReadRequiredString(table, "destination", $"package-files[{index}].destination")))
                .ToArray();
            var workshopListing = ReadWorkshopListing(root);
            var localInstallTable = ReadRequiredTable(root, "local-install", "local-install");
            var testProjects = ReadOptionalTableArray(root, "test-projects", "test-projects")
                .Select((table, index) => new TestProjectProfile(
                    ReadRequiredString(table, "id", $"test-projects[{index}].id"),
                    ReadRequiredString(table, "path", $"test-projects[{index}].path"),
                    ReadRequiredBoolean(table, "required", $"test-projects[{index}].required")))
                .ToArray();
            var acceptanceChecks = ReadOptionalTableArray(
                    root,
                    "acceptance-checks",
                    "acceptance-checks")
                .Select((table, index) => new AcceptanceCheckProfile(
                    ReadRequiredString(table, "id", $"acceptance-checks[{index}].id"),
                    ReadRequiredString(table, "title", $"acceptance-checks[{index}].title"),
                    ReadRequiredBoolean(table, "required", $"acceptance-checks[{index}].required"),
                    ReadOptionalString(table, "setup", $"acceptance-checks[{index}].setup"),
                    ReadOptionalString(table, "action", $"acceptance-checks[{index}].action"),
                    ReadOptionalString(table, "expected", $"acceptance-checks[{index}].expected")))
                .ToArray();

            var profile = new ModProfile(
                schemaVersion,
                resolvedManifestPath,
                Path.GetDirectoryName(resolvedManifestPath)!,
                ReadRequiredString(modTable, "mod-yaml", "mod.mod-yaml"),
                ReadRequiredString(modTable, "mod-info-yaml", "mod.mod-info-yaml"),
                build,
                packageFiles,
                workshopListing,
                new LocalInstallProfile(ReadRequiredString(
                    localInstallTable,
                    "directory-name",
                    "local-install.directory-name")),
                testProjects,
                acceptanceChecks);

            return new OperationResult<ModProfile>(
                profile,
                [],
                PipelineExitCode.Success);
        }
        catch (ProfileSchemaException exception)
        {
            var diagnostic = exception.IsUnknownKey
                ? DiagnosticCatalog.UnknownProfileKey(
                    exception.KeyPath,
                    resolvedManifestPath)
                : DiagnosticCatalog.InvalidProfileValue(
                    exception.KeyPath,
                    resolvedManifestPath,
                    exception.Message);
            return Failure(diagnostic);
        }
    }

    private static BuildProfile? ReadBuildProfile(TomlTable root)
    {
        if (!root.TryGetValue("build", out var value))
        {
            return null;
        }

        if (value is not TomlTable table)
        {
            throw Invalid("build", "the field must be a table.");
        }

        return new BuildProfile(
            ReadRequiredString(table, "entry-point", "build.entry-point"),
            ReadRequiredString(table, "configuration", "build.configuration"),
            ReadRequiredString(
                table,
                "game-managed-directory-property",
                "build.game-managed-directory-property"),
            ReadRequiredString(table, "primary-output", "build.primary-output"),
            ReadOptionalStringArray(table, "merge-inputs", "build.merge-inputs"));
    }

    private static WorkshopListingProfile ReadWorkshopListing(TomlTable root)
    {
        var table = ReadRequiredTable(root, "workshop-listing", "workshop-listing");
        var descriptionByteLimit = ReadOptionalInt(
            table,
            "description-byte-limit",
            "workshop-listing.description-byte-limit",
            DefaultListingByteLimit);
        var changeNotesByteLimit = ReadOptionalInt(
            table,
            "change-notes-byte-limit",
            "workshop-listing.change-notes-byte-limit",
            DefaultListingByteLimit);

        ValidateListingByteLimit(
            descriptionByteLimit,
            "workshop-listing.description-byte-limit");
        ValidateListingByteLimit(
            changeNotesByteLimit,
            "workshop-listing.change-notes-byte-limit");

        return new WorkshopListingProfile(
            ReadRequiredString(table, "description", "workshop-listing.description"),
            ReadRequiredString(table, "change-notes", "workshop-listing.change-notes"),
            ReadRequiredString(table, "preview", "workshop-listing.preview"),
            ReadRequiredStringArray(table, "mod-types", "workshop-listing.mod-types"),
            ReadRequiredStringArray(
                table,
                "dlc-compatibility",
                "workshop-listing.dlc-compatibility"),
            descriptionByteLimit,
            changeNotesByteLimit);
    }

    private static void ValidateListingByteLimit(int byteLimit, string keyPath)
    {
        if (byteLimit is < 1 or > DefaultListingByteLimit)
        {
            throw Invalid(keyPath, "the integer must be between 1 and 8000 inclusive.");
        }
    }

    private static void ValidateAllowedKeys(TomlTable root)
    {
        ValidateTableKeys(root, "", "");
        ValidateNestedTable(root, "mod");
        ValidateNestedTable(root, "build");
        ValidateNestedTable(root, "workshop-listing");
        ValidateNestedTable(root, "local-install");
        ValidateTableArray(root, "package-files");
        ValidateTableArray(root, "test-projects");
        ValidateTableArray(root, "acceptance-checks");
    }

    private static void ValidateNestedTable(TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out var value))
        {
            return;
        }

        if (value is not TomlTable table)
        {
            throw Invalid(key, "the field must be a table.");
        }

        ValidateTableKeys(table, key, key);
    }

    private static void ValidateTableArray(TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out var value))
        {
            return;
        }

        if (value is not TomlTableArray tables)
        {
            throw Invalid(key, "the field must be an array of tables.");
        }

        for (var index = 0; index < tables.Count; index++)
        {
            ValidateTableKeys(tables[index], $"{key}[]", $"{key}[{index}]");
        }
    }

    private static void ValidateTableKeys(
        TomlTable table,
        string allowedKeyPath,
        string displayKeyPath)
    {
        var allowedKeys = AllowedKeys[allowedKeyPath];
        foreach (var key in table.Keys)
        {
            if (allowedKeys.Contains(key))
            {
                continue;
            }

            var fullKeyPath = string.IsNullOrEmpty(displayKeyPath)
                ? key
                : $"{displayKeyPath}.{key}";
            throw new ProfileSchemaException(fullKeyPath, "the key is not defined.", true);
        }
    }

    private static TomlTable ReadRequiredTable(
        TomlTable parent,
        string key,
        string keyPath)
    {
        if (!parent.TryGetValue(key, out var value))
        {
            throw Invalid(keyPath, "the required table is missing.");
        }

        return value as TomlTable
            ?? throw Invalid(keyPath, "the field must be a table.");
    }

    private static IReadOnlyList<TomlTable> ReadOptionalTableArray(
        TomlTable parent,
        string key,
        string keyPath)
    {
        if (!parent.TryGetValue(key, out var value))
        {
            return [];
        }

        if (value is not TomlTableArray tables)
        {
            throw Invalid(keyPath, "the field must be an array of tables.");
        }

        return tables.ToArray();
    }

    private static string ReadRequiredString(
        TomlTable table,
        string key,
        string keyPath)
    {
        if (!table.TryGetValue(key, out var value))
        {
            throw Invalid(keyPath, "the required string is missing.");
        }

        return value as string
            ?? throw Invalid(keyPath, "the field must be a string.");
    }

    private static string ReadOptionalString(
        TomlTable table,
        string key,
        string keyPath)
    {
        if (!table.TryGetValue(key, out var value))
        {
            return string.Empty;
        }

        return value as string
            ?? throw Invalid(keyPath, "the field must be a string.");
    }

    private static bool ReadRequiredBoolean(
        TomlTable table,
        string key,
        string keyPath)
    {
        if (!table.TryGetValue(key, out var value))
        {
            throw Invalid(keyPath, "the required Boolean is missing.");
        }

        return value is bool boolean
            ? boolean
            : throw Invalid(keyPath, "the field must be a Boolean.");
    }

    private static int ReadRequiredInt(TomlTable table, string key, string keyPath)
    {
        if (!table.TryGetValue(key, out var value))
        {
            throw Invalid(keyPath, "the required integer is missing.");
        }

        return ConvertToInt(value, keyPath);
    }

    private static int ReadOptionalInt(
        TomlTable table,
        string key,
        string keyPath,
        int defaultValue)
    {
        return table.TryGetValue(key, out var value)
            ? ConvertToInt(value, keyPath)
            : defaultValue;
    }

    private static int ConvertToInt(object? value, string keyPath) =>
        value switch
        {
            int integer => integer,
            long integer when integer is >= int.MinValue and <= int.MaxValue => (int)integer,
            _ => throw Invalid(keyPath, "the field must be a 32-bit integer.")
        };

    private static IReadOnlyList<string> ReadRequiredStringArray(
        TomlTable table,
        string key,
        string keyPath)
    {
        if (!table.TryGetValue(key, out var value))
        {
            throw Invalid(keyPath, "the required string array is missing.");
        }

        return ConvertToStringArray(value, keyPath);
    }

    private static IReadOnlyList<string> ReadOptionalStringArray(
        TomlTable table,
        string key,
        string keyPath)
    {
        return table.TryGetValue(key, out var value)
            ? ConvertToStringArray(value, keyPath)
            : [];
    }

    private static IReadOnlyList<string> ConvertToStringArray(object? value, string keyPath)
    {
        if (value is not TomlArray array)
        {
            throw Invalid(keyPath, "the field must be an array of strings.");
        }

        var values = new string[array.Count];
        for (var index = 0; index < array.Count; index++)
        {
            values[index] = array[index] as string
                ?? throw Invalid($"{keyPath}[{index}]", "the array item must be a string.");
        }

        return values;
    }

    private static ProfileSchemaException Invalid(string keyPath, string requirement) =>
        new(keyPath, requirement, false);

    private static OperationResult<ModProfile> Failure(Diagnostic diagnostic) =>
        new(null, [diagnostic], PipelineExitCode.InvalidInput);

    private sealed class ProfileSchemaException(
        string keyPath,
        string message,
        bool isUnknownKey) : Exception(message)
    {
        internal string KeyPath { get; } = keyPath;

        internal bool IsUnknownKey { get; } = isUnknownKey;
    }
}
