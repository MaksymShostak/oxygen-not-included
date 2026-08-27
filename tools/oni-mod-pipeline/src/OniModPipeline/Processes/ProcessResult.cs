namespace MaksymShostak.OniModPipeline.Processes;

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
