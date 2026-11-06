using MaksymShostak.OniModPipeline.Cli;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class TestCommandTests
{
    [Test]
    public async Task Test_WhenDeclaredProjectPasses_PrintsExactAutomatedTestResultsDirectory()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("test"));

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        var resultsDirectory = invocation.StandardOutput.TrimEnd('\r', '\n');
        await Assert.That(Path.GetFileName(resultsDirectory)).IsEqualTo("automated-test-results");
        await Assert.That(resultsDirectory.StartsWith(
            Path.Combine(fixture.ArtifactsDirectory, "tests", "Example.Mod") +
            Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(Directory.EnumerateFiles(resultsDirectory, "*.trx")
                .Select(Path.GetFileName)
                .ToArray()).IsEquivalentTo((new[] { "example-regressions.trx" }).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var testRequest = fixture.ProcessRunner.BuildOrTestRequests.Single(request =>
            request.Arguments[0] == "test");
        await Assert.That(testRequest.EnvironmentVariables["ONI_MANAGED_ASSEMBLY_DIRECTORY"]).IsEqualTo(fixture.ManagedDirectory);
        await Assert.That(testRequest.EnvironmentVariables.TryGetValue(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT",
            out var repositoryRoot)).IsTrue();
        await Assert.That(repositoryRoot).IsEqualTo(fixture.WorktreeRoot);
        await Assert.That(testRequest.EnvironmentVariables.ContainsKey(
            "ONI_PIPELINE_REPOSITORY_ROOT")).IsFalse();
    }

    [Test]
    public async Task Test_WhenJsonRequested_ReturnsExactResultsDirectoryAsValue()
    {
        using var fixture = new PipelineCommandFixture(includeTests: true);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("test", "--format", "json"));

        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        var resultsDirectory = document.RootElement.GetProperty("value").GetString();
        Assert.NotNull(resultsDirectory);
        await Assert.That(File.Exists(Path.Combine(
            resultsDirectory,
            "example-regressions.trx"))).IsTrue();
    }
}
