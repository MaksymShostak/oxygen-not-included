using System.Text;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class ReadmeSynchronizerTests
{
    private const string Original = "# Intro\n<!-- oni-mod-pipeline:workshop-description:start -->\nold\n<!-- oni-mod-pipeline:workshop-description:end -->\nSupport  \n";

    [TestMethod]
    public async Task Synchronize_UsesGeneratedBytesChecksWithoutWritingAndRepeatsWithoutChange()
    {
        using var fixture = new Fixture();
        var before = File.ReadAllBytes(fixture.ReadmePath);
        var check = await fixture.Run(true);
        Assert.IsTrue(check.HasDrift);
        Assert.IsFalse(check.Changed);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(fixture.ReadmePath));
        var write = await fixture.Run(false);
        Assert.IsTrue(write.Changed);
        Assert.AreEqual(Original.Replace("\nold\n", "\n# Mod\n", StringComparison.Ordinal), File.ReadAllText(fixture.ReadmePath));
        Assert.IsFalse((await fixture.Run(false)).Changed);
        Assert.IsFalse((await fixture.Run(true)).HasDrift);
        CollectionAssert.AreEqual(fixture.Description.Bytes, fixture.Runner.GeneratedBytes);
    }

    [TestMethod]
    public async Task Synchronize_RejectsConcurrentChangeWithoutOverwritingIt()
    {
        using var fixture = new Fixture();
        fixture.Runner.DuringConversion = () => File.WriteAllText(fixture.ReadmePath, "owner edit");
        await Assert.ThrowsAsync<IOException>(() => fixture.Run(false));
        Assert.AreEqual("owner edit", File.ReadAllText(fixture.ReadmePath));
    }

    [TestMethod]
    public async Task Synchronize_RejectsFailureAndCancellationWithoutWriting()
    {
        using var fixture = new Fixture();
        fixture.Runner.ExitCode = 1;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Run(false));
        Assert.AreEqual(Original, File.ReadAllText(fixture.ReadmePath));
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Run(false, new CancellationToken(true)));
        Assert.AreEqual(Original, File.ReadAllText(fixture.ReadmePath));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        internal string ReadmePath => directory.GetPath("README.md");
        internal RenderedListingText Description { get; } = new ListingTextRenderer().Render("[h1]Mod[/h1]\n");
        internal Runner Runner { get; } = new();
        internal Fixture()
        {
            File.WriteAllText(ReadmePath, Original, new UTF8Encoding(false));
            Directory.CreateDirectory(directory.GetPath("package"));
            File.WriteAllText(directory.GetPath("package", "package.json"), "{\"name\":\"steam-community-bbcode\",\"bin\":\"cli.js\"}");
            File.WriteAllText(directory.GetPath("package", "cli.js"), "// bin");
        }
        internal Task<ReadmeSynchronization> Run(bool check, CancellationToken token = default) =>
            new ReadmeSynchronizer(new InstalledBbcodeConverter(Runner)).SynchronizeAsync(directory.Path, new ReadmeProfile("README.md"), Description, directory.GetPath("package"), check, token);
        public void Dispose() => directory.Dispose();
    }

    private sealed class Runner : IExternalProcessRunner
    {
        internal byte[] GeneratedBytes { get; private set; } = [];
        internal Action? DuringConversion { get; set; }
        internal int ExitCode { get; set; }
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            GeneratedBytes = File.ReadAllBytes(request.Arguments[^1]);
            DuringConversion?.Invoke();
            return Task.FromResult(new ProcessResult(ExitCode, "{\"value\":\"# Mod\\n\",\"diagnostics\":[]}", ""));
        }
    }
}
