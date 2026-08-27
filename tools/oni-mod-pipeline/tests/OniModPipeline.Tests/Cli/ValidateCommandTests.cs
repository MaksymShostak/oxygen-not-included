using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.SourceControl;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class ValidateCommandTests
{
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
