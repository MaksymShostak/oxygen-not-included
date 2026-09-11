using System.Text.Json;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class InstalledBbcodeConverterTests
{
    [TestMethod]
    public async Task Convert_UsesDeclaredPublicBinAndSeparateArguments()
    {
        using var directory = new TemporaryDirectory();
        var package = CreatePackage(directory, "bin with spaces/convert.js");
        var runner = new RecordingRunner(new ProcessResult(0, "{\"value\":\"# Mod\\n\",\"diagnostics\":[]}", ""));
        var input = directory.GetPath("generated description.bbcode");
        File.WriteAllText(input, "[h1]Mod[/h1]\r\n");
        var result = await new InstalledBbcodeConverter(runner).ConvertAsync(package, input, CancellationToken.None);
        Assert.AreEqual("# Mod\n", result.Markdown);
        Assert.IsNotNull(runner.Request);
        Assert.AreEqual("node", runner.Request.FileName);
        CollectionAssert.AreEqual(new[] { Path.Combine(package, "bin with spaces", "convert.js"), "to-gfm", "--format=json", "--profile=workshop-item", "--fail-on=lossy", input }, runner.Request.Arguments.ToArray());
    }

    [TestMethod]
    [DataRow(1, "{\"value\":\"partial\",\"diagnostics\":[]}")]
    [DataRow(0, "not json")]
    [DataRow(0, "{\"value\":42,\"diagnostics\":[]}")]
    [DataRow(0, "{\"value\":\"partial\"}")]
    [DataRow(0, "{\"value\":\"partial\",\"diagnostics\":[{\"fidelity\":\"lossy\"}]}")]
    public async Task Convert_RejectsFailedOrMalformedResults(int exitCode, string output)
    {
        using var directory = new TemporaryDirectory();
        var package = CreatePackage(directory, "cli.js");
        var runner = new RecordingRunner(new ProcessResult(exitCode, output, "converter diagnostic"));
        await Assert.ThrowsAsync<InvalidDataException>(() => new InstalledBbcodeConverter(runner).ConvertAsync(package, directory.GetPath("input.bbcode"), CancellationToken.None));
    }

    [TestMethod]
    public async Task Convert_RejectsBinOutsidePackageBeforeLaunching()
    {
        using var directory = new TemporaryDirectory();
        var package = CreatePackage(directory, "cli.js");
        File.WriteAllText(Path.Combine(package, "package.json"), "{\"name\":\"steam-community-bbcode\",\"bin\":{\"steam-community-bbcode\":\"../escape.js\"}}");
        var runner = new RecordingRunner(new ProcessResult(0, "{}", ""));
        await Assert.ThrowsAsync<InvalidDataException>(() => new InstalledBbcodeConverter(runner).ConvertAsync(package, "input", CancellationToken.None));
        Assert.IsNull(runner.Request);
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
