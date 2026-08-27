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
