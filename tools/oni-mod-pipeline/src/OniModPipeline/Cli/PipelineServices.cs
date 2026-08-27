using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.SourceControl;

namespace MaksymShostak.OniModPipeline.Cli;

internal sealed record PipelineServices(
    ModProfileLocator ProfileLocator,
    ModProfileLoader ProfileLoader,
    ModProfileValidator ProfileValidator,
    OniMetadataReader MetadataReader,
    EnvironmentDiscoveryService EnvironmentDiscovery,
    GitRepositoryInspector GitRepositoryInspector,
    IExternalProcessRunner ProcessRunner);
