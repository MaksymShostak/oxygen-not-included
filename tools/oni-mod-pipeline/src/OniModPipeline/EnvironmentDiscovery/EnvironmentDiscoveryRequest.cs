namespace MaksymShostak.OniModPipeline.EnvironmentDiscovery;

internal sealed record EnvironmentDiscoveryRequest(
    string? GameDirectory,
    string? UserDataDirectory,
    string? ArtifactsDirectory);
