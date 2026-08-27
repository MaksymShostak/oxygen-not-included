using MaksymShostak.OniModPipeline.Cli;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class TestCommandTests
{
    [TestMethod]
    public async Task Test_WhenDeclaredProjectPasses_PrintsExactAutomatedTestResultsDirectory()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("test"));

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        var resultsDirectory = invocation.StandardOutput.TrimEnd('\r', '\n');
        Assert.AreEqual("automated-test-results", Path.GetFileName(resultsDirectory));
        Assert.IsTrue(resultsDirectory.StartsWith(
            Path.Combine(fixture.ArtifactsDirectory, "tests", "Example.Mod") +
            Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
        CollectionAssert.AreEqual(
            new[] { "example-regressions.trx" },
            Directory.EnumerateFiles(resultsDirectory, "*.trx")
                .Select(Path.GetFileName)
                .ToArray());
        var testRequest = fixture.ProcessRunner.BuildOrTestRequests.Single(request =>
            request.Arguments[0] == "test");
        Assert.AreEqual(
            fixture.ManagedDirectory,
            testRequest.EnvironmentVariables["ONI_MANAGED_ASSEMBLY_DIRECTORY"]);
        Assert.AreEqual(
            fixture.WorktreeRoot,
            testRequest.EnvironmentVariables["ONI_PIPELINE_REPOSITORY_ROOT"]);
    }

    [TestMethod]
    public async Task Test_WhenJsonRequested_ReturnsExactResultsDirectoryAsValue()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("test", "--format", "json"));

        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        var resultsDirectory = document.RootElement.GetProperty("value").GetString();
        Assert.IsNotNull(resultsDirectory);
        Assert.IsTrue(File.Exists(Path.Combine(
            resultsDirectory,
            "example-regressions.trx")));
    }
}
