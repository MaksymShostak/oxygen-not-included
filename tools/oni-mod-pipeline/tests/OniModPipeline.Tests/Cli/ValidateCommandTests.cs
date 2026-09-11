using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.SourceControl;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class ValidateCommandTests
{
    [TestMethod]
    public async Task Validate_ForReleaseRejectsStaleReadmeWithoutWriting()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        File.AppendAllText(Path.Combine(fixture.ModRoot, "oni-mod-pipeline.toml"), "\n[readme]\nrepository-path = \"README.md\"\n");
        File.WriteAllText(Path.Combine(fixture.WorktreeRoot, "README.md"), "<!-- oni-mod-pipeline:workshop-description:start -->\nold\n<!-- oni-mod-pipeline:workshop-description:end -->\n");
        File.WriteAllText(Path.Combine(fixture.WorktreeRoot, "package.json"), "{}");
        File.WriteAllText(Path.Combine(fixture.WorktreeRoot, "package-lock.json"), "{}");
        var package = Path.Combine(fixture.WorktreeRoot, "node_modules", "steam-community-bbcode");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "package.json"), "{\"name\":\"steam-community-bbcode\",\"bin\":\"cli.js\"}");
        File.WriteAllText(Path.Combine(package, "cli.js"), "// controlled process fixture");
        var runner = new ReadmeValidationRunner(fixture.ProcessRunner);
        var services = fixture.Services with { ProcessRunner = runner, GitRepositoryInspector = new GitRepositoryInspector(runner) };
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(services), fixture.CreateArguments("validate", "--for-release"));
        Assert.AreEqual(6, invocation.ExitCode);
        StringAssert.Contains(invocation.StandardError, "stale");
        Assert.AreEqual(0, before.ChangedPathsComparedWith(SourceSnapshot.CaptureTree(fixture.RootPath)).Count);
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
    }

    private sealed class ReadmeValidationRunner(MaksymShostak.OniModPipeline.Processes.IExternalProcessRunner inner) : MaksymShostak.OniModPipeline.Processes.IExternalProcessRunner
    {
        public async Task<MaksymShostak.OniModPipeline.Processes.ProcessResult> RunAsync(MaksymShostak.OniModPipeline.Processes.ProcessRequest request, CancellationToken cancellationToken)
        {
            if (request.FileName == "node")
                return new(0, "{\"value\":\"# Updated\\n\",\"diagnostics\":[]}", "");
            var result = await inner.RunAsync(request, cancellationToken);
            return request.Arguments.SequenceEqual(new[] { "ls-files", "-z" })
                ? result with { StandardOutput = result.StandardOutput + "README.md\0package.json\0package-lock.json\0" }
                : result;
        }
    }
    [TestMethod]
    public async Task Validate_WhenDevelopmentInputIsDirty_SucceedsWithoutForRelease()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: true);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        StringAssert.Contains(invocation.StandardOutput, "Release validation: false");
        StringAssert.Contains(invocation.StandardOutput, "Source clean: false");
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Validate_WhenContributingInputIsDirtyAndForRelease_ReturnsExitCodeSix()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: true);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate", "--for-release"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(6, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardOutput);
        StringAssert.Contains(invocation.StandardError, "ONIP5001");
        StringAssert.Contains(invocation.StandardError, "description.bbcode");
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Validate_WhenJsonRequested_WritesOneJsonDocumentWithoutAnsi()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate", "--format", "json"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        Assert.IsFalse(invocation.StandardOutput.Contains('\u001b'));
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        Assert.AreEqual(
            0,
            document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.IsTrue(document.RootElement
            .GetProperty("value")
            .GetProperty("sourceClean")
            .GetBoolean());
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Validate_WhenFormatIsUnknown_ReturnsParseExitTwoWithoutDiscovery()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["validate", "--format", "xml"]);

        Assert.AreEqual(2, invocation.ExitCode);
        StringAssert.Contains(invocation.StandardError, "human");
        StringAssert.Contains(invocation.StandardError, "json");
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task Validate_WhenGitIsUnavailable_SucceedsWithoutForRelease()
    {
        using var fixture = new CliCommandFixture(
            sourceIsDirty: false,
            gitIsAvailable: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(0, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardError);
        StringAssert.Contains(invocation.StandardOutput, "Source clean: false");
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Validate_WhenGitIsUnavailableAndForRelease_ReturnsExitCodeSix()
    {
        using var fixture = new CliCommandFixture(
            sourceIsDirty: false,
            gitIsAvailable: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate", "--for-release"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(6, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardOutput);
        StringAssert.Contains(invocation.StandardError, "ONIP5001");
        StringAssert.Contains(invocation.StandardError, "git is unavailable");
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }

    [TestMethod]
    public async Task Validate_WhenChangeNotesArePlaceholder_ReturnsOnip1006WithoutWritingArtifacts()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        File.WriteAllText(
            Path.Combine(fixture.ModRoot, "change-notes.bbcode"),
            "TODO\n");
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        Assert.AreEqual(2, invocation.ExitCode);
        Assert.AreEqual(string.Empty, invocation.StandardOutput);
        StringAssert.Contains(invocation.StandardError, "ONIP1006");
        StringAssert.Contains(invocation.StandardError, "placeholder");
        Assert.IsFalse(Directory.Exists(fixture.ArtifactsDirectory));
        Assert.AreEqual(0, before.ChangedPathsComparedWith(after).Count);
    }
}
