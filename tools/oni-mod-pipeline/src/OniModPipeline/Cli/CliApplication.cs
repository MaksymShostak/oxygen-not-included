using System.CommandLine;
using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.Cli;

internal static class CliApplication
{
    internal static RootCommand CreateRootCommand() =>
        new("Prepare tested ONI mod release candidates for manual Workshop upload.");

    internal static async Task<int> InvokeAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        try
        {
            var parseResult = CreateRootCommand().Parse(args);
            if (parseResult.Errors.Count > 0)
            {
                foreach (var parseError in parseResult.Errors)
                {
                    Console.Error.Write(parseError.Message);
                    Console.Error.Write('\n');
                }

                return (int)PipelineExitCode.InvalidInput;
            }

            return await parseResult.InvokeAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var result = new OperationResult<object>(
                null,
                [DiagnosticCatalog.UnexpectedFailure(exception)],
                PipelineExitCode.InternalFailure);

            return DiagnosticRenderer.Render(
                result,
                OutputFormat.Human,
                Console.Out,
                Console.Error);
        }
    }
}
