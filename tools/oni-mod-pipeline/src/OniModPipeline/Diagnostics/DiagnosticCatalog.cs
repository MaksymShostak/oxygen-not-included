namespace MaksymShostak.OniModPipeline.Diagnostics;

internal static class DiagnosticCatalog
{
    internal static Diagnostic UnsupportedSchemaVersion(
        int schemaVersion,
        string profilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);

        return new Diagnostic(
            DiagnosticIds.UnsupportedSchemaVersion,
            DiagnosticSeverity.Error,
            $"Unsupported profile schema version {schemaVersion}.",
            $"Profile '{profilePath}' declares schema-version = {schemaVersion}; " +
            "oni-mod-pipeline supports schema-version = 1.",
            "Use schema-version = 1.");
    }

    internal static Diagnostic UnexpectedFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new Diagnostic(
            DiagnosticIds.UnexpectedFailure,
            DiagnosticSeverity.Error,
            "The pipeline encountered an unexpected internal failure.",
            $"{exception.GetType().FullName}: {exception.Message}",
            "Report this failure with the diagnostic ID and evidence.");
    }

    internal static Diagnostic UnknownProfileKey(string keyPath, string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        return new Diagnostic(
            DiagnosticIds.UnknownProfileKey,
            DiagnosticSeverity.Error,
            "The mod profile contains an unknown key.",
            $"Profile '{manifestPath}' contains unknown key '{keyPath}'.",
            "Correct the key to a schema-version = 1 field or remove it.");
    }

    internal static Diagnostic InvalidProfileValue(
        string keyPath,
        string manifestPath,
        string requirement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement);

        return new Diagnostic(
            DiagnosticIds.UnknownProfileKey,
            DiagnosticSeverity.Error,
            "The mod profile does not match schema-version = 1.",
            $"Profile '{manifestPath}' has invalid field '{keyPath}': {requirement}",
            "Correct the declared value to satisfy the schema-version = 1 requirement.");
    }

    internal static Diagnostic UnsafeProfilePath(
        string modRoot,
        string declaredPath,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.UnsafeProfilePath,
            DiagnosticSeverity.Error,
            "A declared profile path is not safely contained by the mod root.",
            $"Path '{declaredPath}' under mod root '{modRoot}' is unsafe: {reason}",
            "Use a relative path that resolves entirely beneath the mod root without links.");
    }

    internal static Diagnostic InvalidOniMetadata(string metadataPath, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.InvalidOniMetadata,
            DiagnosticSeverity.Error,
            "ONI metadata is invalid.",
            $"Metadata file '{metadataPath}' is invalid: {reason}",
            "Correct the YAML while preserving ONI's required metadata fields.");
    }

    internal static Diagnostic ProfileNotFound(
        string? startPath,
        IReadOnlyList<string> searchedPaths)
    {
        ArgumentNullException.ThrowIfNull(searchedPaths);

        var renderedStartPath = string.IsNullOrWhiteSpace(startPath)
            ? "<empty>"
            : startPath;
        var searchedEvidence = searchedPaths.Count == 0
            ? "No valid search directory was available."
            : $"Searched: {string.Join(", ", searchedPaths.Select(path => $"'{path}'"))}.";

        return new Diagnostic(
            DiagnosticIds.ProfileNotFoundOrAmbiguous,
            DiagnosticSeverity.Error,
            "No ONI mod profile was found.",
            $"Starting from '{renderedStartPath}'. {searchedEvidence}",
            "Pass --mod with a mod directory or an explicit oni-mod-pipeline.toml path.");
    }

    internal static Diagnostic ProfileAmbiguous(
        string startPath,
        IReadOnlyList<string> candidatePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);
        ArgumentNullException.ThrowIfNull(candidatePaths);

        var orderedCandidates = candidatePaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"'{path}'");

        return new Diagnostic(
            DiagnosticIds.ProfileNotFoundOrAmbiguous,
            DiagnosticSeverity.Error,
            "More than one ONI mod profile is reachable.",
            $"Starting from '{startPath}', found: {string.Join(", ", orderedCandidates)}.",
            "Pass --mod with the intended explicit oni-mod-pipeline.toml path.");
    }

    internal static Diagnostic DeclaredInputMissing(
        string declaredPath,
        string resolvedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedPath);

        return new Diagnostic(
            DiagnosticIds.DeclaredInputMissing,
            DiagnosticSeverity.Error,
            "A declared profile input is missing.",
            $"Declared path '{declaredPath}' resolves to '{resolvedPath}', which does not exist.",
            "Create the declared input or correct its path in oni-mod-pipeline.toml.");
    }

    internal static Diagnostic DeclaredInputWrongKind(
        string declaredPath,
        string resolvedPath,
        string expectedKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedKind);

        return new Diagnostic(
            DiagnosticIds.DeclaredInputMissing,
            DiagnosticSeverity.Error,
            "A declared profile input has the wrong filesystem kind.",
            $"Declared path '{declaredPath}' resolves to '{resolvedPath}', which is not a {expectedKind}.",
            "Correct the declared input kind or its path in oni-mod-pipeline.toml.");
    }

    internal static Diagnostic InvalidProfileSemantics(string field, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.UnknownProfileKey,
            DiagnosticSeverity.Error,
            "The mod profile is not semantically valid.",
            $"Field '{field}' is invalid: {reason}",
            "Correct the profile so it satisfies the schema-version = 1 semantic contract.");
    }

    internal static Diagnostic DuplicatePackageDestination(
        string firstDestination,
        string secondDestination,
        string portableCollisionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstDestination);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondDestination);
        ArgumentException.ThrowIfNullOrWhiteSpace(portableCollisionKey);

        return new Diagnostic(
            DiagnosticIds.DuplicatePackageDestination,
            DiagnosticSeverity.Error,
            "Package destinations collide on a supported filesystem.",
            $"Destinations '{firstDestination}' and '{secondDestination}' share portable key '{portableCollisionKey}'.",
            "Use unique NFC-normalized destinations that remain distinct without case sensitivity.");
    }

    internal static Diagnostic InvalidWorkshopListing(string field, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.InvalidWorkshopListing,
            DiagnosticSeverity.Error,
            "Workshop listing configuration is invalid.",
            $"Listing field '{field}' is invalid: {reason}",
            "Use only the schema-version = 1 Workshop listing fields and identifiers.");
    }

    internal static Diagnostic DirtyReleaseInput(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.DirtyReleaseInput,
            DiagnosticSeverity.Error,
            "Release input provenance is not clean and attributable.",
            reason,
            "Commit every contributing input and remove scoped modifications before preparing a release.");
    }

    internal static Diagnostic MissingDotnetSdk(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.MissingDotnetSdk,
            DiagnosticSeverity.Error,
            "The required .NET SDK is unavailable.",
            reason,
            "Install the pinned stable .NET 10.0.4xx SDK; oni-mod-pipeline never installs SDKs automatically.");
    }

    internal static Diagnostic AmbiguousGameInstallation(
        IReadOnlyList<string> candidatePaths)
    {
        ArgumentNullException.ThrowIfNull(candidatePaths);

        return new Diagnostic(
            DiagnosticIds.AmbiguousGameInstallation,
            DiagnosticSeverity.Error,
            "More than one valid ONI installation was discovered.",
            $"Valid installations: {string.Join(", ", candidatePaths.Select(path => $"'{path}'"))}.",
            "Pass --game-directory with the intended ONI installation root.");
    }

    internal static Diagnostic MissingGameAssembly(
        IReadOnlyList<string> searchedPaths,
        IReadOnlyList<string> requiredAssemblies)
    {
        ArgumentNullException.ThrowIfNull(searchedPaths);
        ArgumentNullException.ThrowIfNull(requiredAssemblies);

        var searched = searchedPaths.Count == 0
            ? "No automatic game-installation candidates were available."
            : $"Searched: {string.Join(", ", searchedPaths.Select(path => $"'{path}'"))}.";
        return new Diagnostic(
            DiagnosticIds.MissingGameAssembly,
            DiagnosticSeverity.Error,
            "A valid ONI managed-assembly directory was not found.",
            $"{searched} Required managed anchors: {string.Join(", ", requiredAssemblies)}.",
            "Pass --game-directory with a game root containing both required managed assemblies.");
    }

    internal static Diagnostic MissingUserDataDirectory(
        IReadOnlyList<string> searchedPaths)
    {
        ArgumentNullException.ThrowIfNull(searchedPaths);

        var searched = searchedPaths.Count == 0
            ? "No automatic ONI user-data candidates were available."
            : $"Searched: {string.Join(", ", searchedPaths.Select(path => $"'{path}'"))}.";
        return new Diagnostic(
            DiagnosticIds.MissingUserDataDirectory,
            DiagnosticSeverity.Error,
            "A valid ONI user-data directory was not found.",
            searched,
            "Pass --user-data-directory with the exact existing ONI per-user data root.");
    }

    internal static Diagnostic RestoreFailed(string projectPath, string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        return new Diagnostic(
            DiagnosticIds.RestoreFailed,
            DiagnosticSeverity.Error,
            "Locked dependency restore failed.",
            $"Project '{projectPath}' did not restore successfully: {evidence}",
            "Restore the reviewed lock file with the pinned SDK and correct the reported dependency failure.");
    }

    internal static Diagnostic BuildFailed(string projectPath, string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        return new Diagnostic(
            DiagnosticIds.BuildFailed,
            DiagnosticSeverity.Error,
            "The isolated mod build failed.",
            $"Project '{projectPath}' did not satisfy the build contract: {evidence}",
            "Correct the project or declared build inputs, then run the isolated build again.");
    }

    internal static Diagnostic SourceChangedDuringBuild(IReadOnlyList<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(changedPaths);

        return new Diagnostic(
            DiagnosticIds.SourceChangedDuringBuild,
            DiagnosticSeverity.Error,
            "Contributing source bytes changed during the build.",
            changedPaths.Count == 0
                ? "The pre-build and post-build source snapshots differ."
                : $"Changed paths: {string.Join(", ", changedPaths.Select(path => $"'{path}'"))}.",
            "Remove source-writing build behavior and keep every intermediate and output beneath the run root.");
    }

    internal static Diagnostic BuildOutputMissing(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return new Diagnostic(
            DiagnosticIds.BuildOutputMissing,
            DiagnosticSeverity.Error,
            "A declared build output is missing.",
            $"The isolated build did not produce declared output '{outputPath}'.",
            "Correct the project output contract so every declared build output is written beneath the run root.");
    }

    internal static Diagnostic AutomatedTestFailed(string testProjectId, string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testProjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        return new Diagnostic(
            DiagnosticIds.AutomatedTestFailed,
            DiagnosticSeverity.Error,
            $"Automated test project '{testProjectId}' did not produce passing evidence.",
            evidence,
            "Correct the test project or its locked dependencies and rerun the declared automated tests.");
    }

    internal static Diagnostic UnsafeArtifactsDirectory(string path, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new Diagnostic(
            DiagnosticIds.UnsafeProfilePath,
            DiagnosticSeverity.Error,
            "The artifact-directory override is unsafe.",
            $"Artifact path '{path}' is invalid: {reason}.",
            "Use an absolute, dedicated artifact directory that is not a protected filesystem or ONI root.");
    }
}

internal static class DiagnosticIds
{
    internal const string UnsupportedSchemaVersion = "ONIP1001";
    internal const string UnknownProfileKey = "ONIP1002";
    internal const string UnsafeProfilePath = "ONIP1003";
    internal const string DuplicatePackageDestination = "ONIP1004";
    internal const string InvalidOniMetadata = "ONIP1005";
    internal const string InvalidWorkshopListing = "ONIP1006";
    internal const string ProfileNotFoundOrAmbiguous = "ONIP1007";
    internal const string DeclaredInputMissing = "ONIP1008";
    internal const string MissingDotnetSdk = "ONIP2001";
    internal const string AmbiguousGameInstallation = "ONIP2002";
    internal const string MissingGameAssembly = "ONIP2003";
    internal const string MissingUserDataDirectory = "ONIP2004";
    internal const string DuplicateInstalledMod = "ONIP2005";
    internal const string RestoreFailed = "ONIP3001";
    internal const string BuildFailed = "ONIP3002";
    internal const string SourceChangedDuringBuild = "ONIP3003";
    internal const string BuildOutputMissing = "ONIP3004";
    internal const string AutomatedTestFailed = "ONIP3005";
    internal const string UnownedInstallDestination = "ONIP4001";
    internal const string InstalledContentMismatch = "ONIP4002";
    internal const string InstallationReceiptExists = "ONIP4003";
    internal const string DirtyReleaseInput = "ONIP5001";
    internal const string CandidateManifestMismatch = "ONIP5002";
    internal const string AcceptanceDigestMismatch = "ONIP5003";
    internal const string RequiredAcceptanceMissing = "ONIP5004";
    internal const string InvalidUploaderRepresentation = "ONIP5005";
    internal const string ReleaseNotReady = "ONIP5006";
    internal const string CandidateAlreadyExists = "ONIP5007";
    internal const string AcceptanceRequiresInteractiveTerminal = "ONIP5008";
    internal const string UnexpectedFailure = "ONIP9001";
    internal const string CleanupFailed = "ONIP9002";
}
