using System.Text.Json;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Readme;
public sealed class InstalledBbcodeConverterTests
{
    [Test]
    public async Task Convert_UsesDeclaredPublicBinAndSeparateArguments()
    {
        using var directory = new TemporaryDirectory();
        var package = CreatePackage(directory, "bin with spaces/convert.js");
        var runner = new RecordingRunner(new ProcessResult(0, "{\"value\":\"# Mod\\n\",\"diagnostics\":[]}", ""));
        var input = directory.GetPath("generated description.bbcode");
        File.WriteAllText(input, "[h1]Mod[/h1]\r\n");
        var result = await new InstalledBbcodeConverter(runner).ConvertAsync(package, input, CancellationToken.None);
        await Assert.That(result.Markdown).IsEqualTo("# Mod\n");
        Assert.NotNull(runner.Request);
        await Assert.That(runner.Request.FileName).IsEqualTo("node");
        await Assert.That(runner.Request.Arguments.ToArray()).IsEquivalentTo(new[] { Path.Combine(package, "bin with spaces", "convert.js"), "to-gfm", "--format=json", "--profile=workshop-item", "--fail-on=lossy", input }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(1, "{\"value\":\"partial\",\"diagnostics\":[]}")]
    [Arguments(0, "not json")]
    [Arguments(0, "{\"value\":42,\"diagnostics\":[]}")]
    [Arguments(0, "{\"value\":\"partial\"}")]
    [Arguments(0, "{\"value\":\"partial\",\"diagnostics\":[{\"fidelity\":\"lossy\"}]}")]
    public async Task Convert_RejectsFailedOrMalformedResults(int exitCode, string output)
    {
        using var directory = new TemporaryDirectory();
        var package = CreatePackage(directory, "cli.js");
        var runner = new RecordingRunner(new ProcessResult(exitCode, output, "converter diagnostic"));
        await Assert.That(async () => {await new InstalledBbcodeConverter(runner).ConvertAsync(package, directory.GetPath("input.bbcode"), CancellationToken.None);}).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Convert_RejectsBinOutsidePackageBeforeLaunching()
    {
        using var directory = new TemporaryDirectory();
        var package = CreatePackage(directory, "cli.js");
        File.WriteAllText(Path.Combine(package, "package.json"), "{\"name\":\"steam-community-bbcode\",\"bin\":{\"steam-community-bbcode\":\"../escape.js\"}}");
        var runner = new RecordingRunner(new ProcessResult(0, "{}", ""));
        await Assert.That(async () => {await new InstalledBbcodeConverter(runner).ConvertAsync(package, "input", CancellationToken.None);}).Throws<InvalidDataException>();
        await Assert.That(runner.Request).IsNull();
    }

    private static string CreatePackage(TemporaryDirectory directory, string bin)
    {
        var package = directory.GetPath("installed package");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(package, bin))!);
        File.WriteAllText(Path.Combine(package, bin), "// installed public bin");
        File.WriteAllText(Path.Combine(package, "package.json"), JsonSerializer.Serialize(new { name = "steam-community-bbcode", bin = new Dictionary<string, string> { ["steam-community-bbcode"] = bin } }));
        return package;
    }

    private sealed class RecordingRunner(ProcessResult result) : IExternalProcessRunner
    {
        internal ProcessRequest? Request { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
