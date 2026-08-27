using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModTest;

[TestClass]
public sealed class AutomatedTestRunnerTests
{
    [TestMethod]
    public async Task RunAsync_WhenRequiredIdsAreDuplicated_ReturnsOnip3005()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("duplicate-id", "Tests/First.csproj", true),
            new TestProjectProfile("duplicate-id", "Tests/Second.csproj", true));
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
        Assert.IsFalse(Directory.Exists(fixture.ResultsRoot));
    }

    [TestMethod]
    public async Task RunAsync_WhenRequiredProjectIsMissing_ReturnsOnip3005()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("missing-project", "Tests/Missing.csproj", true));
        File.Delete(Path.Combine(fixture.ModRoot, "Tests", "Missing.csproj"));
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task RunAsync_WhenRequiredTestProcessFails_CapturesLogsAndReturnsOnip3005()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("failing-project", "Tests/Failing.csproj", true));
        fixture.ProcessRunner.TestExitCode = 7;
        fixture.ProcessRunner.StandardOutput = "captured standard output";
        fixture.ProcessRunner.StandardError = "captured standard error";
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.IsNotNull(result.Value);
        var testResult = result.Value.Single();
        Assert.AreEqual(7, testResult.ExitCode);
        Assert.AreEqual("captured standard output", testResult.StandardOutput);
        Assert.AreEqual("captured standard error", testResult.StandardError);
        Assert.IsFalse(testResult.Passed);
    }

    [TestMethod]
    public async Task RunAsync_WhenRequiredProcessSucceedsWithoutTrx_ReturnsOnip3005()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("missing-evidence", "Tests/MissingEvidence.csproj", true));
        fixture.ProcessRunner.WriteTrx = false;
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(0, result.Value.Single().ExitCode);
        Assert.IsFalse(result.Value.Single().Passed);
    }

    [TestMethod]
    public async Task RunAsync_WhenProjectsPass_WritesExactTrxFilenamesAndChildEnvironment()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("first-project", "Tests/First.csproj", true),
            new TestProjectProfile("second-project", "Tests/Second.csproj", true));
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.Success, result.ExitCode);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(2, result.Value.Count);
        Assert.IsTrue(result.Value.All(test => test.Passed));
        CollectionAssert.AreEqual(
            new[] { "first-project.trx", "second-project.trx" },
            Directory.EnumerateFiles(fixture.ResultsRoot, "*.trx")
                .Select(Path.GetFileName)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
        Assert.AreEqual(4, fixture.ProcessRunner.Requests.Count);

        for (var index = 0; index < result.Value.Count; index++)
        {
            var profile = fixture.Profile.TestProjects[index];
            var projectPath = Path.GetFullPath(Path.Combine(fixture.ModRoot, profile.Path));
            var restore = fixture.ProcessRunner.Requests[index * 2];
            var test = fixture.ProcessRunner.Requests[index * 2 + 1];
            CollectionAssert.AreEqual(
                new[] { "restore", projectPath, "--locked-mode" },
                restore.Arguments.ToArray());
            Assert.AreEqual(0, restore.EnvironmentVariables.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    "test",
                    "--project",
                    projectPath,
                    "--no-restore",
                    "--configuration",
                    "Release",
                    "--results-directory",
                    fixture.ResultsRoot,
                    "--",
                    "--report-trx",
                    "--report-trx-filename",
                    $"{profile.Id}.trx"
                },
                test.Arguments.ToArray());
            Assert.AreEqual(2, test.EnvironmentVariables.Count);
            Assert.AreEqual(
                fixture.ManagedDirectory,
                test.EnvironmentVariables["ONI_MANAGED_ASSEMBLY_DIRECTORY"]);
            Assert.IsTrue(test.EnvironmentVariables.TryGetValue(
                "ONI_MOD_PIPELINE_REPOSITORY_ROOT",
                out var repositoryRoot));
            Assert.AreEqual(fixture.RepositoryRoot, repositoryRoot);
            Assert.IsFalse(test.EnvironmentVariables.ContainsKey(
                "ONI_PIPELINE_REPOSITORY_ROOT"));
        }
    }

    [TestMethod]
    public async Task RunAsync_WhenResultsRootAlreadyExists_ReturnsOnip3005WithoutDeletingIt()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("example-project", "Tests/Example.csproj", true));
        Directory.CreateDirectory(fixture.ResultsRoot);
        var marker = Path.Combine(fixture.ResultsRoot, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep");
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.AreEqual("keep", await File.ReadAllTextAsync(marker));
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
    }

    private static void AssertDiagnostic(
        OperationResult<IReadOnlyList<AutomatedTestResult>> result,
        string expectedId)
    {
        Assert.AreEqual(PipelineExitCode.BuildOrTestFailed, result.ExitCode);
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == expectedId),
            $"Expected {expectedId}; received " +
            string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Id)));
    }

    private sealed class AutomatedTestFixture : IDisposable
    {
        private readonly TemporaryDirectory temporaryDirectory = new();

        internal AutomatedTestFixture(params TestProjectProfile[] testProjects)
        {
            RepositoryRoot = temporaryDirectory.GetPath("repository");
            ModRoot = Path.Combine(RepositoryRoot, "mods", "fixture");
            ManagedDirectory = temporaryDirectory.GetPath("game", "Managed");
            ResultsRoot = temporaryDirectory.GetPath("results", "automated-test-results");
            Directory.CreateDirectory(Path.Combine(ModRoot, "Tests"));
            Directory.CreateDirectory(ManagedDirectory);
            foreach (var testProject in testProjects)
            {
                var projectPath = Path.Combine(
                    ModRoot,
                    testProject.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
                File.WriteAllText(projectPath, "<Project />\n");
            }

            Profile = new ModProfile(
                1,
                Path.Combine(ModRoot, "oni-mod-pipeline.toml"),
                ModRoot,
                "mod.yaml",
                "mod_info.yaml",
                null,
                [],
                new WorkshopListingProfile(
                    "description.bbcode",
                    "change-notes.bbcode",
                    "preview.png",
                    ["tweaks"],
                    ["base-game"],
                    8000,
                    8000),
                new LocalInstallProfile("Fixture"),
                testProjects,
                []);
            ProcessRunner = new RecordingTestProcessRunner();
        }

        internal string RepositoryRoot { get; }

        internal string ModRoot { get; }

        internal string ManagedDirectory { get; }

        internal string ResultsRoot { get; }

        internal ModProfile Profile { get; }

        internal RecordingTestProcessRunner ProcessRunner { get; }

        internal AutomatedTestRunner CreateRunner() =>
            new(ProcessRunner, ManagedDirectory, RepositoryRoot);

        public void Dispose() => temporaryDirectory.Dispose();
    }

    private sealed class RecordingTestProcessRunner : IExternalProcessRunner
    {
        internal List<ProcessRequest> Requests { get; } = [];

        internal int TestExitCode { get; set; }

        internal bool WriteTrx { get; set; } = true;

        internal string StandardOutput { get; set; } = "test output";

        internal string StandardError { get; set; } = string.Empty;

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (request.Arguments[0] == "restore")
            {
                return Task.FromResult(new ProcessResult(0, "restore output", string.Empty));
            }

            Assert.AreEqual("test", request.Arguments[0]);
            if (TestExitCode == 0 && WriteTrx)
            {
                var resultsIndex = request.Arguments.IndexOf("--results-directory");
                var filenameIndex = request.Arguments.IndexOf("--report-trx-filename");
                var trxPath = Path.Combine(
                    request.Arguments[resultsIndex + 1],
                    request.Arguments[filenameIndex + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(trxPath)!);
                File.WriteAllText(trxPath, "<TestRun />\n");
            }

            return Task.FromResult(new ProcessResult(
                TestExitCode,
                StandardOutput,
                StandardError));
        }
    }
}

internal static class ReadOnlyListSearchExtensions
{
    internal static int IndexOf<T>(this IReadOnlyList<T> values, T expected)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], expected))
            {
                return index;
            }
        }

        return -1;
    }
}
