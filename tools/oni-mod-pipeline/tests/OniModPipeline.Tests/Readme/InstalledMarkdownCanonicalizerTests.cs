using System.Text;
using System.Text.Json;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Readme;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class InstalledMarkdownCanonicalizerTests
{
    [TestMethod]
    [DataRow("not JSON", 0)]
    [DataRow("{}", 0)]
    [DataRow("[]", 0)]
    [DataRow("{}", 2)]
    public async Task Canonicalize_RejectsMalformedOrRefusedOutputAndRemovesOnlyItsTemporaryFile(string output, int exitCode)
    {
        using var fixture = new Fixture();
        fixture.Runner.Output = output;
        fixture.Runner.ExitCode = exitCode;
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Run);
        fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [TestMethod]
    [DataRow("v24.20.0")]
    [DataRow("v25.0.0")]
    [DataRow("unknown")]
    public async Task Canonicalize_RejectsUnsupportedRuntimeBeforeLaunchingChecker(string version)
    {
        using var fixture = new Fixture();
        fixture.Runner.Version = version;
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Run);
        Assert.AreEqual(0, fixture.Runner.CheckerCalls);
        fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Canonicalize_PropagatesCancellationAndCleansTemporaryFile()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Runner.DuringChecker = cancellation.Cancel;
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Run(cancellation.Token));
        Assert.AreEqual(1, fixture.Runner.CheckerCalls);
        fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Canonicalize_AcceptsLockedVersionAndMatchingPublicResult()
    {
        using var fixture = new Fixture("1.0.4");
        fixture.Runner.ResultVersion = "1.0.4";
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(Fixture.Original), await fixture.Run());
        Assert.AreEqual(3, fixture.Runner.CheckerCalls);
        fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Canonicalize_RejectsInstalledVersionDifferentFromLockBeforeLaunchingChecker()
    {
        using var fixture = new Fixture(lockedVersion: "1.0.4", installedVersion: "1.0.3");
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Run);
        Assert.AreEqual(0, fixture.Runner.CheckerCalls);
        fixture.AssertOriginalAndNoTemporaryFiles();
    }

    [TestMethod]
    public async Task Canonicalize_RejectsPublicResultFromDifferentPackageVersion()
    {
        using var fixture = new Fixture();
        fixture.Runner.ResultVersion = "1.0.4";
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Run);
        fixture.AssertOriginalAndNoTemporaryFiles();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        internal const string Original = "# Owner\n<!-- oni-mod-pipeline:workshop-description:start -->\n## Mod\n<!-- oni-mod-pipeline:workshop-description:end -->\n";
        internal Runner Runner { get; } = new();
        private string ReadmePath => directory.GetPath("README.md");

        internal Fixture(string lockedVersion = "1.0.3", string? installedVersion = null)
        {
            var package = directory.GetPath("tooling", "markdown", "node_modules", "@hadden-industries", "markdown-quality");
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(package, "package.json"), JsonSerializer.Serialize(new { name = "@hadden-industries/markdown-quality", version = installedVersion ?? lockedVersion, bin = new Dictionary<string, string> { ["markdown-quality"] = "cli.js" } }));
            File.WriteAllText(directory.GetPath("tooling", "markdown", "package-lock.json"), JsonSerializer.Serialize(new { packages = new Dictionary<string, object> { ["node_modules/@hadden-industries/markdown-quality"] = new { version = lockedVersion } } }));
            File.WriteAllText(Path.Combine(package, "cli.js"), "// test-only installed bin");
            File.WriteAllText(ReadmePath, Original);
        }

        internal Task<byte[]> Run() => Run(CancellationToken.None);
        internal Task<byte[]> Run(CancellationToken token) => new InstalledMarkdownCanonicalizer(Runner)
            .CanonicalizeAsync(directory.Path, ReadmePath, Encoding.UTF8.GetBytes(Original), token);

        internal void AssertOriginalAndNoTemporaryFiles()
        {
            Assert.AreEqual(Original, File.ReadAllText(ReadmePath));
            CollectionAssert.AreEqual(new[] { ReadmePath }, Directory.GetFiles(directory.Path, "*.md"));
        }

        public void Dispose() => directory.Dispose();
    }

    private sealed class Runner : IExternalProcessRunner
    {
        internal string Version { get; set; } = "v24.21.0";
        internal string Output { get; set; } = "{}";
        internal string? ResultVersion { get; set; }
        internal int ExitCode { get; set; }
        internal int CheckerCalls { get; private set; }
        internal Action? DuringChecker { get; set; }

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken token)
        {
            if (request.Arguments.SequenceEqual(new[] { "--version" }))
                return Task.FromResult(new ProcessResult(0, Version, ""));
            CheckerCalls++;
            DuringChecker?.Invoke();
            token.ThrowIfCancellationRequested();
            if (ResultVersion is not null)
            {
                var index = request.Arguments.ToList().IndexOf("--files-json");
                return Task.FromResult(new ProcessResult(0, JsonSerializer.Serialize(new { schemaVersion = 1, operation = request.Arguments[1],
                    package = new { name = "@hadden-industries/markdown-quality", version = ResultVersion },
                    exitCode = 0, outcome = "clean", errors = Array.Empty<string>(), unprocessed = Array.Empty<string>(),
                    diagnostics = Array.Empty<string>(), selection = new { mode = "explicit", files = JsonSerializer.Deserialize<string[]>(request.Arguments[index + 1]) } }), ""));
            }
            return Task.FromResult(new ProcessResult(ExitCode, Output, ""));
        }
    }
}
