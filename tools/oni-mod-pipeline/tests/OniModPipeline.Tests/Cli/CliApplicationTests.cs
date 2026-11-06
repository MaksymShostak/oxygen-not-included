using MaksymShostak.OniModPipeline.Cli;
using System.Diagnostics;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class CliApplicationTests
{
    [Test]
    public async Task CreateRootCommand_WhenCalled_DescribesTheLocalPipeline()
    {
        var command = CliApplication.CreateRootCommand();

        await Assert.That(command.Description).IsEqualTo("Prepare tested ONI mod release candidates for manual Workshop upload.");
    }

    [Test]
    public async Task InvokeExecutable_WhenHelpIsRequested_UsesPublicToolName()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
        startInfo.ArgumentList.Add("--help");
        using var process = new Process { StartInfo = startInfo };

        await Assert.That(process.Start()).IsTrue().Because("The production CLI process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        await Assert.That(process.ExitCode).IsEqualTo(0).Because(await standardError);
        await Assert.That(await standardOutput).Contains("oni-mod-pipeline [command] [options]");
    }

    [Test]
    public async Task InvokeExecutable_WhenFormatIsUnknown_ReturnsInvalidInputExitCode()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(typeof(CliApplication).Assembly.Location);
        startInfo.ArgumentList.Add("validate");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("xml");
        using var process = new Process { StartInfo = startInfo };

        await Assert.That(process.Start()).IsTrue().Because("The production CLI process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        await Assert.That(process.ExitCode).IsEqualTo(2).Because(await standardError);
        await Assert.That(await standardOutput).IsEqualTo(string.Empty);
        await Assert.That(await standardError).Contains("human");
        await Assert.That(await standardError).Contains("json");
    }
}
