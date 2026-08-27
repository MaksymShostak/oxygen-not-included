namespace MaksymShostak.OniModPipeline.Diagnostics;

internal sealed record OperationResult<T>(
    T? Value,
    IReadOnlyList<Diagnostic> Diagnostics,
    PipelineExitCode ExitCode)
{
    internal bool IsSuccess => ExitCode == PipelineExitCode.Success;
}
