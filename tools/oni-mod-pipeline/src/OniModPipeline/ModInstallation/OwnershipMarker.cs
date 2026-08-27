namespace MaksymShostak.OniModPipeline.ModInstallation;

internal sealed record OwnershipMarker(
    int SchemaVersion,
    string StaticId,
    string ManagedDirectoryName,
    string InstalledContentDigest);
