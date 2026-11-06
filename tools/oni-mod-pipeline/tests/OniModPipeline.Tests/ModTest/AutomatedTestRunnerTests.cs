using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModTest;
public sealed class AutomatedTestRunnerTests
{
    [Test]
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

        await AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
        await Assert.That(Directory.Exists(fixture.ResultsRoot)).IsFalse();
    }

    [Test]
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

        await AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    [Test]
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

        await AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.NotNull(result.Value);
        var testResult = result.Value.Single();
        await Assert.That(testResult.ExitCode).IsEqualTo(7);
        await Assert.That(testResult.StandardOutput).IsEqualTo("captured standard output");
        await Assert.That(testResult.StandardError).IsEqualTo("captured standard error");
        await Assert.That(testResult.Passed).IsFalse();
    }

    [Test]
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

        await AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.Single().ExitCode).IsEqualTo(0);
        await Assert.That(result.Value.Single().Passed).IsFalse();
    }

    [Test]
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

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success);
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.Count).IsEqualTo(2);
        await Assert.That(result.Value.All(test => test.Passed)).IsTrue();
        await Assert.That(Directory.EnumerateFiles(fixture.ResultsRoot, "*.trx")
                .Select(Path.GetFileName)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray()).IsEquivalentTo((new[] { "first-project.trx", "second-project.trx" }).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(4);

        for (var index = 0; index < result.Value.Count; index++)
        {
            var profile = fixture.Profile.TestProjects[index];
            var projectPath = Path.GetFullPath(Path.Combine(fixture.ModRoot, profile.Path));
            var restore = fixture.ProcessRunner.Requests[index * 2];
            var test = fixture.ProcessRunner.Requests[index * 2 + 1];
            await Assert.That(restore.Arguments.ToArray()).IsEquivalentTo(new[] { "restore", projectPath, "--locked-mode" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(restore.EnvironmentVariables.Count).IsEqualTo(0);
            await Assert.That(test.Arguments.ToArray()).IsEquivalentTo(new[]
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
                }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(test.EnvironmentVariables.Count).IsEqualTo(2);
            await Assert.That(test.EnvironmentVariables["ONI_MANAGED_ASSEMBLY_DIRECTORY"]).IsEqualTo(fixture.ManagedDirectory);
            await Assert.That(test.EnvironmentVariables.TryGetValue(
                "ONI_MOD_PIPELINE_REPOSITORY_ROOT",
                out var repositoryRoot)).IsTrue();
            await Assert.That(repositoryRoot).IsEqualTo(fixture.RepositoryRoot);
            await Assert.That(test.EnvironmentVariables.ContainsKey(
                "ONI_PIPELINE_REPOSITORY_ROOT")).IsFalse();
        }
    }

    [Test]
    public async Task RunAsync_WhenBuildIsBound_ExportsThatExactBuildAndRequiresIt()
    {
        using var fixture = new AutomatedTestFixture(
            new TestProjectProfile("bound-project", "Tests/Bound.csproj", true));
        var artifactsDirectory = Path.Combine(fixture.RepositoryRoot, "artifacts");
        var buildResultPath = Path.Combine(
            artifactsDirectory,
            "release-candidates",
            "work",
            "build-result.json");
        var runner = fixture.CreateRunner();

        var result = await runner.RunAsync(
            fixture.Profile,
            fixture.ResultsRoot,
            new BoundPipelineBuild(buildResultPath, artifactsDirectory),
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(2);
        await Assert.That(fixture.ProcessRunner.Requests[0].EnvironmentVariables.Count).IsEqualTo(0).Because("Restore must not receive build-inspection variables.");
        var environment = fixture.ProcessRunner.Requests[1].EnvironmentVariables;
        await Assert.That(environment.Count).IsEqualTo(5);
        await Assert.That(environment["ONI_MANAGED_ASSEMBLY_DIRECTORY"]).IsEqualTo(fixture.ManagedDirectory);
        await Assert.That(environment["ONI_MOD_PIPELINE_REPOSITORY_ROOT"]).IsEqualTo(fixture.RepositoryRoot);
        await Assert.That(environment["ONI_MOD_PIPELINE_BUILD_RESULT_PATH"]).IsEqualTo(buildResultPath);
        await Assert.That(environment["ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY"]).IsEqualTo(artifactsDirectory);
        await Assert.That(environment["ONI_MOD_PIPELINE_REQUIRE_BUILD_RESULT"]).IsEqualTo("true");
    }

    [Test]
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

        await AssertDiagnostic(result, DiagnosticIds.AutomatedTestFailed);
        await Assert.That(await File.ReadAllTextAsync(marker)).IsEqualTo("keep");
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
    }

    private static async Task AssertDiagnostic(
        OperationResult<IReadOnlyList<AutomatedTestResult>> result,
        string expectedId)
    {
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.BuildOrTestFailed);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == expectedId)).IsTrue().Because($"Expected {expectedId}; received " +
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

        public async Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (request.Arguments[0] == "restore")
            {
                return new ProcessResult(0, "restore output", string.Empty);
            }

            await Assert.That(request.Arguments[0]).IsEqualTo("test");
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

            return new ProcessResult(
                TestExitCode,
                StandardOutput,
                StandardError);
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
