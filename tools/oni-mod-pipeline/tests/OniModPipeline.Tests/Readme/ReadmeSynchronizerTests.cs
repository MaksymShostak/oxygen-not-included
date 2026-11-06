using System.Text;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Tests.Readme;
public sealed class ReadmeSynchronizerTests
{
    private const string Original = "# Intro\n<!-- oni-mod-pipeline:workshop-description:start -->\nold\n<!-- oni-mod-pipeline:workshop-description:end -->\nSupport  \n";

    [Test]
    public async Task Synchronize_UsesGeneratedBytesChecksWithoutWritingAndRepeatsWithoutChange()
    {
        using var fixture = new Fixture();
        var before = File.ReadAllBytes(fixture.ReadmePath);
        var check = await fixture.Run(true);
        await Assert.That(check.HasDrift).IsTrue();
        await Assert.That(check.Changed).IsFalse();
        await Assert.That(File.ReadAllBytes(fixture.ReadmePath)).IsEquivalentTo(before, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var write = await fixture.Run(false);
        await Assert.That(write.Changed).IsTrue();
        await Assert.That(File.ReadAllText(fixture.ReadmePath)).IsEqualTo(Original.Replace("\nold\n", "\n# Mod\n", StringComparison.Ordinal));
        await Assert.That((await fixture.Run(false)).Changed).IsFalse();
        await Assert.That((await fixture.Run(true)).HasDrift).IsFalse();
        await Assert.That(fixture.Runner.GeneratedBytes).IsEquivalentTo(fixture.Description.Bytes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Synchronize_RejectsConcurrentChangeWithoutOverwritingIt()
    {
        using var fixture = new Fixture();
        fixture.Runner.DuringConversion = () => File.WriteAllText(fixture.ReadmePath, "owner edit");
        await Assert.That(async () => { await fixture.Run(false); }).Throws<IOException>();
        await Assert.That(File.ReadAllText(fixture.ReadmePath)).IsEqualTo("owner edit");
    }

    [Test]
    public async Task Synchronize_RejectsFailureAndCancellationWithoutWriting()
    {
        using var fixture = new Fixture();
        fixture.Runner.ExitCode = 1;
        await Assert.That(async () => { await fixture.Run(false); }).Throws<InvalidDataException>();
        await Assert.That(File.ReadAllText(fixture.ReadmePath)).IsEqualTo(Original);
        await Assert.That(async () => { await fixture.Run(false, new CancellationToken(true)); }).Throws<OperationCanceledException>();
        await Assert.That(File.ReadAllText(fixture.ReadmePath)).IsEqualTo(Original);
    }

    [Test]
    public async Task Synchronize_RejectsFormatterChangesOutsideOwnershipOrRefusal()
    {
        using var fixture = new Fixture();
        fixture.Canonicalizer.Transform = candidate => Encoding.UTF8.GetBytes("owner changed\n" + Encoding.UTF8.GetString(candidate));
        await Assert.That(async () => { await fixture.Run(false); }).Throws<InvalidDataException>();
        await Assert.That(File.ReadAllText(fixture.ReadmePath)).IsEqualTo(Original);
        fixture.Canonicalizer.Transform = _ => throw new InvalidDataException("preservation refused");
        await Assert.That(async () => { await fixture.Run(false); }).Throws<InvalidDataException>();
        await Assert.That(File.ReadAllText(fixture.ReadmePath)).IsEqualTo(Original);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        internal string ReadmePath => directory.GetPath("README.md");
        internal RenderedListingText Description { get; } = new ListingTextRenderer().Render("[h1]Mod[/h1]\n");
        internal Runner Runner { get; } = new();
        internal Canonicalizer Canonicalizer { get; } = new();
        internal Fixture()
        {
            File.WriteAllText(ReadmePath, Original, new UTF8Encoding(false));
            Directory.CreateDirectory(directory.GetPath("package"));
            File.WriteAllText(directory.GetPath("package", "package.json"), "{\"name\":\"steam-community-bbcode\",\"bin\":\"cli.js\"}");
            File.WriteAllText(directory.GetPath("package", "cli.js"), "// bin");
            Directory.CreateDirectory(directory.GetPath("tools", "readme"));
            File.WriteAllText(directory.GetPath("tools", "readme", "render-description.mjs"), "// renderer");
        }
        internal Task<ReadmeSynchronization> Run(bool check, CancellationToken token = default) =>
            new ReadmeSynchronizer(new InstalledBbcodeConverter(Runner), Canonicalizer).SynchronizeAsync(directory.Path, new ReadmeProfile("README.md"), Description, directory.GetPath("package"), check, token);
        public void Dispose() => directory.Dispose();
    }

    private sealed class Canonicalizer : IReadmeCanonicalizer
    {
        internal Func<byte[], byte[]> Transform { get; set; } = candidate => candidate;
        public Task<byte[]> CanonicalizeAsync(string root, string path, byte[] candidate, CancellationToken token) => Task.FromResult(Transform(candidate));
    }

    private sealed class Runner : IExternalProcessRunner
    {
        internal byte[] GeneratedBytes { get; private set; } = [];
        internal Action? DuringConversion { get; set; }
        internal int ExitCode { get; set; }
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            if (request.StandardInput is { } markdown)
                return Task.FromResult(new ProcessResult(0, markdown, ""));
            GeneratedBytes = File.ReadAllBytes(request.Arguments[^1]);
            DuringConversion?.Invoke();
            return Task.FromResult(new ProcessResult(ExitCode, "{\"value\":\"# Mod\\n\",\"diagnostics\":[]}", ""));
        }
    }
}
