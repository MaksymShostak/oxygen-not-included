namespace MaksymShostak.OniModPipeline.Processes;

internal interface IExternalProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken);
}
