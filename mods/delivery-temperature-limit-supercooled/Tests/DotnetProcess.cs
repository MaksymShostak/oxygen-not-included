using System.Diagnostics;

namespace DeliveryTemperatureLimit.Tests;

internal sealed record DotnetProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class DotnetProcess
{
    internal static async Task<DotnetProcessResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start(), "dotnet process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var registration = cancellationToken.Register(
            () =>
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            });
        await process.WaitForExitAsync(cancellationToken);
        return new(process.ExitCode, await standardOutput, await standardError);
    }
}
