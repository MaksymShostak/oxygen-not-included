using MaksymShostak.OniModPipeline.Cli;
using System.Diagnostics;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class CliApplicationTests
{
    [TestMethod]
    public void CreateRootCommand_WhenCalled_DescribesTheLocalPipeline()
    {
        var command = CliApplication.CreateRootCommand();

        Assert.AreEqual(
            "Prepare tested ONI mod release candidates for manual Workshop upload.",
            command.Description);
    }

    [TestMethod]
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

        Assert.IsTrue(process.Start(), "The production CLI process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.AreEqual(0, process.ExitCode, await standardError);
        StringAssert.Contains(await standardOutput, "oni-mod-pipeline [options]");
    }
}
