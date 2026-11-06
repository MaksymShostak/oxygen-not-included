using System.Text;
using System.Text.Json;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Readme;
public sealed class InstalledMarkdownCanonicalizerTests
{
    [Test]
    [Arguments("not JSON", 0)]
    [Arguments("{}", 0)]
    [Arguments("[]", 0)]
    [Arguments("{}", 2)]
    public async Task Canonicalize_RejectsMalformedOrRefusedOutputWithoutWritingMarkdown(string output, int exitCode)
    {
        using var fixture = new Fixture();
        fixture.Runner.Output = output;
        fixture.Runner.ExitCode = exitCode;
        await Assert.That(async () => { await fixture.Run(); }).Throws<InvalidDataException>();
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [Test]
    [Arguments("path")]
    [Arguments("requestId")]
    public async Task Canonicalize_RejectsResponseForAnotherDocumentOrRequest(string mismatch)
    {
        using var fixture = new Fixture();
        fixture.Runner.ResultVersion = "3.0.0";
        fixture.Runner.Mismatch = mismatch;
        await Assert.That(async () => { await fixture.Run(); }).Throws<InvalidDataException>();
        await Assert.That(fixture.Runner.CheckerCalls).IsEqualTo(1);
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [Test]
    public async Task Canonicalize_PropagatesCancellationAndCleansTemporaryFile()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Runner.DuringChecker = cancellation.Cancel;
        await Assert.That(async () => { await fixture.Run(cancellation.Token); }).Throws<OperationCanceledException>();
        await Assert.That(fixture.Runner.CheckerCalls).IsEqualTo(1);
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [Test]
    public async Task Canonicalize_AcceptsMatchingPublicDocumentResult()
    {
        using var fixture = new Fixture();
        fixture.Runner.ResultVersion = "3.0.1";
        await Assert.That(await fixture.Run()).IsEquivalentTo(Encoding.UTF8.GetBytes(Fixture.Original), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.Runner.CheckerCalls).IsEqualTo(1);
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [Test]
    public async Task Canonicalize_RejectsMissingBridgeBeforeLaunchingChecker()
    {
        using var fixture = new Fixture();
        fixture.RemoveBridge();
        await Assert.That(async () => { await fixture.Run(); }).Throws<InvalidDataException>();
        await Assert.That(fixture.Runner.CheckerCalls).IsEqualTo(0);
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [Test]
    public async Task Canonicalize_RejectsPublicResultFromDifferentPackage()
    {
        using var fixture = new Fixture();
        fixture.Runner.ResultVersion = "3.0.0";
        fixture.Runner.ResultPackage = "other-package";
        await Assert.That(async () => { await fixture.Run(); }).Throws<InvalidDataException>();
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [Test]
    [Arguments("previewed", false)]
    [Arguments("refused", false)]
    [Arguments("completed", true)]
    public async Task Canonicalize_RejectsPreviewRefusalOrCheckoutWriteClaims(string status, bool wroteFile)
    {
        using var fixture = new Fixture();
        fixture.Runner.ResultVersion = "3.0.0";
        fixture.Runner.OperationStatus = status;
        fixture.Runner.WroteFile = wroteFile;
        await Assert.That(async () => { await fixture.Run(); }).Throws<InvalidDataException>();
        await fixture.AssertOriginalAndNoTemporaryFiles();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        internal const string Original = "# Owner\n<!-- oni-mod-pipeline:workshop-description:start -->\n## Mod\n<!-- oni-mod-pipeline:workshop-description:end -->\n";
        internal Runner Runner { get; } = new();
        private string ReadmePath => directory.GetPath("README.md");

        internal Fixture()
        {
            Directory.CreateDirectory(directory.GetPath("tools", "readme"));
            File.WriteAllText(directory.GetPath("tools", "readme", "canonicalize.mjs"), "// controlled transport boundary");
            File.WriteAllText(ReadmePath, Original);
        }

        internal void RemoveBridge() => File.Delete(directory.GetPath("tools", "readme", "canonicalize.mjs"));

        internal Task<byte[]> Run() => Run(CancellationToken.None);
        internal Task<byte[]> Run(CancellationToken token) => new InstalledMarkdownCanonicalizer(Runner)
            .CanonicalizeAsync(directory.Path, ReadmePath, Encoding.UTF8.GetBytes(Original), token);

        internal async Task AssertOriginalAndNoTemporaryFiles()
        {
            await Assert.That(File.ReadAllText(ReadmePath)).IsEqualTo(Original);
            await Assert.That(Directory.GetFiles(directory.Path, "*.md")).IsEquivalentTo(new[] { ReadmePath }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }

        public void Dispose() => directory.Dispose();
    }

    private sealed class Runner : IExternalProcessRunner
    {
        internal string ResultPackage { get; set; } = "@hadden-industries/markdown-quality";
        internal string OperationStatus { get; set; } = "completed";
        internal bool WroteFile { get; set; }
        internal string? Mismatch { get; set; }
        internal string Output { get; set; } = "{}";
        internal string? ResultVersion { get; set; }
        internal int ExitCode { get; set; }
        internal int CheckerCalls { get; private set; }
        internal Action? DuringChecker { get; set; }

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken token)
        {
            CheckerCalls++;
            DuringChecker?.Invoke();
            token.ThrowIfCancellationRequested();
            if (ResultVersion is not null)
            {
                using var input = JsonDocument.Parse(request.StandardInput!);
                var value = input.RootElement;
                return Task.FromResult(new ProcessResult(0, JsonSerializer.Serialize(new { schemaVersion = 5, operation = "format", operationStatus = OperationStatus, writtenPaths = WroteFile ? new[] { "README.md" } : Array.Empty<string>(),
                    package = new { name = ResultPackage, version = ResultVersion },
                    exitCode = 0, outcome = "findings", document = new {
                        path = Mismatch == "path" ? "other.md" : value.GetProperty("path").GetString(),
                        requestId = Mismatch == "requestId" ? "other-request" : value.GetProperty("requestId").GetString(),
                        contentBase64 = value.GetProperty("contentBase64").GetString()
                    } }), ""));
            }
            return Task.FromResult(new ProcessResult(ExitCode, Output, ""));
        }
    }
}
