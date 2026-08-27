namespace MaksymShostak.OniModPipeline.ModTest;

internal sealed record AutomatedTestResult(
    string TestProjectId,
    string ProjectPath,
    string TrxPath,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Passed);
