using MaksymShostak.OniModPipeline.Cli;
using MaksymShostak.OniModPipeline.SourceControl;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.Cli;
public sealed class ValidateCommandTests
{
    [Test]
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
        var markdownPackage = Path.Combine(fixture.WorktreeRoot, "node_modules", "@hadden-industries", "markdown-quality");
        Directory.CreateDirectory(markdownPackage);
        File.WriteAllText(Path.Combine(markdownPackage, "package.json"), "{\"name\":\"@hadden-industries/markdown-quality\",\"version\":\"3.0.0\",\"bin\":{\"markdown-quality\":\"cli.js\"}}");
        File.WriteAllText(Path.Combine(markdownPackage, "cli.js"), "// controlled checker fixture");
        Directory.CreateDirectory(Path.Combine(fixture.WorktreeRoot, "tools/readme"));
        foreach (var input in new[] { ".node-version", ".python-version", "tools/readme/canonicalize.mjs", "tools/readme/render-description.mjs", "tools/readme/compatibility-badges.mjs" })
            File.WriteAllText(Path.Combine(fixture.WorktreeRoot, input), "{}");
        File.WriteAllText(Path.Combine(fixture.WorktreeRoot, "package-lock.json"), JsonSerializer.Serialize(new { packages = new Dictionary<string, object> { ["node_modules/@hadden-industries/markdown-quality"] = new { version = "3.0.0" } } }));
        var runner = new ReadmeValidationRunner(fixture.ProcessRunner);
        var services = fixture.Services with { ProcessRunner = runner, GitRepositoryInspector = new GitRepositoryInspector(runner) };
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var invocation = await DiagnoseCommandTests.InvokeAsync(CliApplication.CreateRootCommand(services), fixture.CreateArguments("validate", "--for-release"));
        await Assert.That(invocation.ExitCode).IsEqualTo(6);
        await Assert.That(invocation.StandardError).Contains("stale");
        await Assert.That(before.ChangedPathsComparedWith(SourceSnapshot.CaptureTree(fixture.RootPath)).Count).IsEqualTo(0);
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
    }

    private sealed class ReadmeValidationRunner(MaksymShostak.OniModPipeline.Processes.IExternalProcessRunner inner) : MaksymShostak.OniModPipeline.Processes.IExternalProcessRunner
    {
        public async Task<MaksymShostak.OniModPipeline.Processes.ProcessResult> RunAsync(MaksymShostak.OniModPipeline.Processes.ProcessRequest request, CancellationToken cancellationToken)
        {
            if (request.FileName == "node")
            {
                if (request.Arguments[0].EndsWith("render-description.mjs", StringComparison.Ordinal))
                    return new(0, request.StandardInput!, "");
                if (request.StandardInput is not null)
                {
                    using var input = JsonDocument.Parse(request.StandardInput);
                    var document = input.RootElement;
                    return new(0, JsonSerializer.Serialize(new { schemaVersion = 5, operation = "format", operationStatus = "completed", writtenPaths = Array.Empty<string>(),
                        package = new { name = "@hadden-industries/markdown-quality", version = "3.0.0" },
                        exitCode = 0, document = new {
                            path = document.GetProperty("path").GetString(),
                            requestId = document.GetProperty("requestId").GetString(),
                            contentBase64 = document.GetProperty("contentBase64").GetString()
                        } }), "");
                }
                return new(0, "{\"value\":\"# Updated\\n\",\"diagnostics\":[]}", "");
            }
            var result = await inner.RunAsync(request, cancellationToken);
            return request.Arguments.SequenceEqual(new[] { "ls-files", "-z" })
                ? result with { StandardOutput = result.StandardOutput + "README.md\0package.json\0package-lock.json\0.node-version\0.python-version\0tools/readme/canonicalize.mjs\0tools/readme/render-description.mjs\0tools/readme/compatibility-badges.mjs\0" }
                : result;
        }
    }
    [Test]
    public async Task Validate_WhenDevelopmentInputIsDirty_SucceedsWithoutForRelease()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: true);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardOutput).Contains("Release validation: false");
        await Assert.That(invocation.StandardOutput).Contains("Source clean: false");
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_WhenContributingInputIsDirtyAndForRelease_ReturnsExitCodeSix()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: true);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate", "--for-release"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        await Assert.That(invocation.ExitCode).IsEqualTo(6);
        await Assert.That(invocation.StandardOutput).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardError).Contains("ONIP5001");
        await Assert.That(invocation.StandardError).Contains("description.bbcode");
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_WhenJsonRequested_WritesOneJsonDocumentWithoutAnsi()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var before = SourceSnapshot.CaptureTree(fixture.RootPath);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            fixture.CreateArguments("validate", "--format", "json"));

        var after = SourceSnapshot.CaptureTree(fixture.RootPath);
        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardOutput.Contains('\u001b')).IsFalse();
        using var document = JsonDocument.Parse(invocation.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("exitCode").GetInt32()).IsEqualTo(0);
        await Assert.That(document.RootElement
            .GetProperty("value")
            .GetProperty("sourceClean")
            .GetBoolean()).IsTrue();
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_WhenFormatIsUnknown_ReturnsParseExitTwoWithoutDiscovery()
    {
        using var fixture = new CliCommandFixture(sourceIsDirty: false);
        var command = CliApplication.CreateRootCommand(fixture.Services);

        var invocation = await DiagnoseCommandTests.InvokeAsync(
            command,
            ["validate", "--format", "xml"]);

        await Assert.That(invocation.ExitCode).IsEqualTo(2);
        await Assert.That(invocation.StandardError).Contains("human");
        await Assert.That(invocation.StandardError).Contains("json");
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    [Test]
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
        await Assert.That(invocation.ExitCode).IsEqualTo(0);
        await Assert.That(invocation.StandardError).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardOutput).Contains("Source clean: false");
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
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
        await Assert.That(invocation.ExitCode).IsEqualTo(6);
        await Assert.That(invocation.StandardOutput).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardError).Contains("ONIP5001");
        await Assert.That(invocation.StandardError).Contains("git is unavailable");
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }

    [Test]
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
        await Assert.That(invocation.ExitCode).IsEqualTo(2);
        await Assert.That(invocation.StandardOutput).IsEqualTo(string.Empty);
        await Assert.That(invocation.StandardError).Contains("ONIP1006");
        await Assert.That(invocation.StandardError).Contains("placeholder");
        await Assert.That(Directory.Exists(fixture.ArtifactsDirectory)).IsFalse();
        await Assert.That(before.ChangedPathsComparedWith(after).Count).IsEqualTo(0);
    }
}
