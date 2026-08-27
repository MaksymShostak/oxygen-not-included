using MaksymShostak.OniModPipeline.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed class ModProfileValidator
{
    private const string BuildOutputPrefix = "{build-output}/";

    private static readonly Regex KebabCaseIdPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex StaticIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex ManagedDirectoryPropertyPattern = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex AssemblySimpleNamePattern = new(
        "^[A-Za-z_][A-Za-z0-9_.-]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly ISet<string> SupportedBuildEntryPointExtensions =
        new HashSet<string>(
            [".csproj", ".fsproj", ".vbproj", ".sln", ".slnx"],
            StringComparer.OrdinalIgnoreCase);

    internal OperationResult<ModProfile> Validate(
        ModProfile profile,
        OniMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(metadata);

        var diagnostics = new List<Diagnostic>();

        ValidateMetadata(metadata, diagnostics);
        ValidateDeclaredFiles(profile, diagnostics);
        ValidatePackageMappings(profile, diagnostics);
        ValidateWorkshopListing(profile.WorkshopListing, diagnostics);
        ValidateEvidenceProfiles(profile, diagnostics);
        ValidateBuildProfile(profile, diagnostics);
        ValidateLocalInstall(profile.LocalInstall, diagnostics);

        return diagnostics.Count == 0
            ? new OperationResult<ModProfile>(profile, [], PipelineExitCode.Success)
            : new OperationResult<ModProfile>(null, diagnostics, PipelineExitCode.InvalidInput);
    }

    private static void ValidateMetadata(
        OniMetadata metadata,
        ICollection<Diagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod.yaml",
                "required field 'title' must be nonempty."));
        }

        if (string.IsNullOrWhiteSpace(metadata.Description))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod.yaml",
                "required field 'description' must be nonempty."));
        }

        if (!StaticIdPattern.IsMatch(metadata.StaticId))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod.yaml",
                "required field 'staticID' must match ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$."));
        }

        if (string.IsNullOrWhiteSpace(metadata.SupportedContent))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod_info.yaml",
                "required field 'supportedContent' must be nonempty."));
        }

        if (metadata.MinimumSupportedBuild <= 0)
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod_info.yaml",
                "required field 'minimumSupportedBuild' must be positive."));
        }

        if (metadata.ApiVersion <= 0)
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod_info.yaml",
                "required field 'APIVersion' must be positive."));
        }

        if (!IsSupportedVersion(metadata.Version))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidOniMetadata(
                "mod_info.yaml",
                "required field 'version' must have two through four nonnegative components no greater than 65534."));
        }
    }

    private static bool IsSupportedVersion(string version)
    {
        if (!Version.TryParse(version, out _))
        {
            return false;
        }

        var components = version.Split('.');
        return components.Length is >= 2 and <= 4 &&
            components.All(component =>
                int.TryParse(
                    component,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value) &&
                value is >= 0 and <= 65534);
    }

    private static void ValidateDeclaredFiles(
        ModProfile profile,
        ICollection<Diagnostic> diagnostics)
    {
        AddDiagnostics(
            ContainedPathResolver.ResolveExistingFile(profile.ModRoot, profile.ModYamlPath),
            diagnostics);
        AddDiagnostics(
            ContainedPathResolver.ResolveExistingFile(profile.ModRoot, profile.ModInfoYamlPath),
            diagnostics);

        ValidateListingFile(
            profile.ModRoot,
            profile.WorkshopListing.Description,
            "workshop-listing.description",
            diagnostics);
        ValidateListingFile(
            profile.ModRoot,
            profile.WorkshopListing.ChangeNotes,
            "workshop-listing.change-notes",
            diagnostics);
        ValidateListingFile(
            profile.ModRoot,
            profile.WorkshopListing.Preview,
            "workshop-listing.preview",
            diagnostics);

        foreach (var testProject in profile.TestProjects)
        {
            AddDiagnostics(
                ContainedPathResolver.ResolveExistingFile(profile.ModRoot, testProject.Path),
                diagnostics);
        }
    }

    private static void ValidateListingFile(
        string modRoot,
        string declaredPath,
        string field,
        ICollection<Diagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(declaredPath))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidWorkshopListing(
                field,
                "the required path must be nonempty."));
            return;
        }

        AddDiagnostics(
            ContainedPathResolver.ResolveExistingFile(modRoot, declaredPath),
            diagnostics);
    }

    private static void ValidatePackageMappings(
        ModProfile profile,
        ICollection<Diagnostic> diagnostics)
    {
        if (profile.PackageFiles.Count == 0)
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "package-files",
                "at least one explicit package mapping is required."));
            return;
        }

        var destinationsByPortableKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasModYaml = false;
        var hasModInfoYaml = false;

        foreach (var mapping in profile.PackageFiles)
        {
            ValidatePackageSource(profile, mapping.Source, diagnostics);

            if (!TryNormalizeRelativePath(
                mapping.Destination,
                out var normalizedDestination,
                out var invalidReason))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                    "package-files.destination",
                    invalidReason));
                continue;
            }

            var portableKey = normalizedDestination
                .Normalize(NormalizationForm.FormC)
                .ToUpperInvariant();
            if (destinationsByPortableKey.TryGetValue(portableKey, out var firstDestination))
            {
                diagnostics.Add(DiagnosticCatalog.DuplicatePackageDestination(
                    firstDestination,
                    mapping.Destination,
                    portableKey));
            }
            else
            {
                destinationsByPortableKey.Add(portableKey, mapping.Destination);
            }

            hasModYaml |= string.Equals(portableKey, "MOD.YAML", StringComparison.Ordinal);
            hasModInfoYaml |= string.Equals(portableKey, "MOD_INFO.YAML", StringComparison.Ordinal);
        }

        if (!hasModYaml)
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "package-files.destination",
                "one mapping must place mod.yaml at the package root."));
        }

        if (!hasModInfoYaml)
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "package-files.destination",
                "one mapping must place mod_info.yaml at the package root."));
        }
    }

    private static void ValidatePackageSource(
        ModProfile profile,
        string source,
        ICollection<Diagnostic> diagnostics)
    {
        if (source.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
        {
            if (profile.Build is null)
            {
                diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                    "package-files.source",
                    "{build-output} may be used only when a build profile exists."));
            }
            else if (!TryNormalizeRelativePath(
                source[BuildOutputPrefix.Length..],
                out _,
                out var invalidReason))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                    "package-files.source",
                    invalidReason));
            }

            return;
        }

        var fileResult = ContainedPathResolver.ResolveExistingFile(profile.ModRoot, source);
        if (fileResult.IsSuccess)
        {
            return;
        }

        var directoryResult = ContainedPathResolver.ResolveExistingDirectory(profile.ModRoot, source);
        AddDiagnostics(
            directoryResult.IsSuccess ? directoryResult : fileResult,
            diagnostics);
    }

    private static void ValidateWorkshopListing(
        WorkshopListingProfile listing,
        ICollection<Diagnostic> diagnostics)
    {
        ValidateListingIdentifiers(
            "mod-types",
            listing.ModTypes,
            WorkshopListingVocabulary.ModTypeLabels,
            diagnostics);
        ValidateListingIdentifiers(
            "dlc-compatibility",
            listing.DlcCompatibility,
            WorkshopListingVocabulary.DlcLabels,
            diagnostics);
    }

    private static void ValidateListingIdentifiers(
        string field,
        IReadOnlyList<string> identifiers,
        IReadOnlyDictionary<string, string> allowedIdentifiers,
        ICollection<Diagnostic> diagnostics)
    {
        if (identifiers.Count == 0)
        {
            diagnostics.Add(DiagnosticCatalog.InvalidWorkshopListing(
                field,
                "at least one identifier is required."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identifier in identifiers)
        {
            if (!allowedIdentifiers.ContainsKey(identifier))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidWorkshopListing(
                    field,
                    $"unknown identifier '{identifier}'."));
            }

            if (!seen.Add(identifier))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidWorkshopListing(
                    field,
                    $"identifier '{identifier}' is duplicated."));
            }
        }
    }

    private static void ValidateEvidenceProfiles(
        ModProfile profile,
        ICollection<Diagnostic> diagnostics)
    {
        var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testProject in profile.TestProjects)
        {
            ValidateEvidenceId("test-projects.id", testProject.Id, evidenceIds, diagnostics);
        }

        foreach (var acceptanceCheck in profile.AcceptanceChecks)
        {
            ValidateEvidenceId(
                "acceptance-checks.id",
                acceptanceCheck.Id,
                evidenceIds,
                diagnostics);
            if (string.IsNullOrWhiteSpace(acceptanceCheck.Title))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                    "acceptance-checks.title",
                    $"check '{acceptanceCheck.Id}' must have a nonempty title."));
            }
        }
    }

    private static void ValidateEvidenceId(
        string field,
        string id,
        ISet<string> seen,
        ICollection<Diagnostic> diagnostics)
    {
        if (!KebabCaseIdPattern.IsMatch(id))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                field,
                $"identifier '{id}' must use lower-case kebab-case."));
        }

        if (!seen.Add(id))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                field,
                $"identifier '{id}' is duplicated across test and acceptance evidence."));
        }
    }

    private static void ValidateBuildProfile(
        ModProfile profile,
        ICollection<Diagnostic> diagnostics)
    {
        if (profile.Build is not { } build)
        {
            return;
        }

        AddDiagnostics(
            ContainedPathResolver.ResolveExistingFile(profile.ModRoot, build.EntryPoint),
            diagnostics);
        if (!SupportedBuildEntryPointExtensions.Contains(Path.GetExtension(build.EntryPoint)))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "build.entry-point",
                "the path must identify one MSBuild project or solution."));
        }

        if (string.IsNullOrWhiteSpace(build.Configuration))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "build.configuration",
                "the configuration must be nonempty."));
        }

        if (!ManagedDirectoryPropertyPattern.IsMatch(build.GameManagedDirectoryProperty))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "build.game-managed-directory-property",
                "the property must match ^[A-Za-z_][A-Za-z0-9_]*$."));
        }

        if (!build.PrimaryOutput.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "build.primary-output",
                "the path must begin with '{build-output}/'."));
        }
        else if (!TryNormalizeRelativePath(
            build.PrimaryOutput[BuildOutputPrefix.Length..],
            out _,
            out var invalidPrimaryOutputReason))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "build.primary-output",
                invalidPrimaryOutputReason));
        }

        var mergeInputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mergeInput in build.MergeInputs)
        {
            if (!AssemblySimpleNamePattern.IsMatch(mergeInput) ||
                mergeInput.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                    "build.merge-inputs",
                    $"assembly simple name '{mergeInput}' is invalid or includes a .dll suffix."));
            }

            if (!mergeInputs.Add(mergeInput))
            {
                diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                    "build.merge-inputs",
                    $"assembly simple name '{mergeInput}' is duplicated."));
            }
        }
    }

    private static void ValidateLocalInstall(
        LocalInstallProfile localInstall,
        ICollection<Diagnostic> diagnostics)
    {
        var name = localInstall.DirectoryName;
        if (string.IsNullOrWhiteSpace(name) ||
            name is "." or ".." ||
            name.IndexOfAny(['/', (char)92]) >= 0 ||
            name.Any(character => char.IsControl(character) || "<>:\"|?*".Contains(character)))
        {
            diagnostics.Add(DiagnosticCatalog.InvalidProfileSemantics(
                "local-install.directory-name",
                "the value must be one portable directory name without separators or reserved characters."));
        }
    }

    private static bool TryNormalizeRelativePath(
        string path,
        out string normalizedPath,
        out string invalidReason)
    {
        normalizedPath = string.Empty;
        invalidReason = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            invalidReason = "the path must be nonempty.";
            return false;
        }

        if (path.Contains('\0'))
        {
            invalidReason = "the path contains a NUL character.";
            return false;
        }

        if (path[0] == '/' ||
            path[0] == (char)92 ||
            path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            invalidReason = "the path must be relative.";
            return false;
        }

        normalizedPath = path.Replace((char)92, '/');
        var segments = normalizedPath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            invalidReason = "the path must not contain empty, '.' , or '..' segments.";
            normalizedPath = string.Empty;
            return false;
        }

        return true;
    }

    private static void AddDiagnostics<T>(
        OperationResult<T> result,
        ICollection<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }
    }
}
