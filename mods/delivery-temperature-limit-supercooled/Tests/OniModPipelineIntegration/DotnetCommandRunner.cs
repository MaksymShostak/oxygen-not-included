using System.Diagnostics;

namespace DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

internal sealed record DotnetCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    internal string FormatEvidence()
    {
        var standardError = string.IsNullOrWhiteSpace(StandardError)
            ? "<empty>"
            : StandardError.Trim();
        var standardOutput = string.IsNullOrWhiteSpace(StandardOutput)
            ? "<empty>"
            : StandardOutput.Trim();

        return $"dotnet exited {ExitCode}. Standard error: {standardError}. " +
            $"Standard output: {standardOutput}.";
    }
}

internal static class DotnetCommandRunner
{
    internal static async Task<DotnetCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

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
        Assert.IsTrue(process.Start(), "The requested dotnet command did not start.");
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
        return new(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }
}
