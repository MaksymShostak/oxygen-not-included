namespace MaksymShostak.OniModPipeline.EnvironmentDiscovery;

internal sealed record PipelineEnvironment(
    string GameDirectory,
    string OniManagedAssemblyDirectory,
    string UserDataDirectory,
    string DevelopmentModsDirectory,
    string LocalModsDirectory,
    string ArtifactsDirectory,
    string DotnetSdkVersion,
    string OperatingSystem,
    string Architecture);
