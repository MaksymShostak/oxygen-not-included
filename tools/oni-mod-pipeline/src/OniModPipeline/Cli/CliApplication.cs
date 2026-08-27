using System.CommandLine;

namespace MaksymShostak.OniModPipeline.Cli;

internal static class CliApplication
{
    internal static RootCommand CreateRootCommand() =>
        new("Prepare tested ONI mod release candidates for manual Workshop upload.");

    internal static Task<int> InvokeAsync(
        string[] args,
        CancellationToken cancellationToken) =>
        CreateRootCommand().Parse(args).InvokeAsync(cancellationToken: cancellationToken);
}
