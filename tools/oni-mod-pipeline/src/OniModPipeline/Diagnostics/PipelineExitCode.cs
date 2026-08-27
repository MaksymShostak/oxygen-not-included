namespace MaksymShostak.OniModPipeline.Diagnostics;

internal enum PipelineExitCode
{
    Success = 0,
    InvalidInput = 2,
    EnvironmentUnavailable = 3,
    BuildOrTestFailed = 4,
    InstallationFailed = 5,
    ReleaseNotReady = 6,
    InternalFailure = 10
}
