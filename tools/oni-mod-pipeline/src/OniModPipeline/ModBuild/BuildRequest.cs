using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.ModBuild;

internal sealed record BuildRequest(
    ModProfile Profile,
    PipelineEnvironment Environment,
    string Configuration,
    string RunRoot,
    string ReleaseVersion,
    string SourceCommit);
