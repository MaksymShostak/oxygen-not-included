namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed record ModProfile(
    int SchemaVersion,
    string ManifestPath,
    string ModRoot,
    string ModYamlPath,
    string ModInfoYamlPath,
    BuildProfile? Build,
    IReadOnlyList<PackageFileMapping> PackageFiles,
    WorkshopListingProfile WorkshopListing,
    LocalInstallProfile LocalInstall,
    IReadOnlyList<TestProjectProfile> TestProjects,
    IReadOnlyList<AcceptanceCheckProfile> AcceptanceChecks);
