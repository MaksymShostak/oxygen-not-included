using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopContent;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Security.Cryptography;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class ReleaseCandidatePreparerTests
{
    [Test]
    public async Task Prepare_WithUnqualifiedReadme_StopsBeforeCreatingCandidate()
    {
        using var fixture = new PreparationFixture();
        var request = fixture.Request with { Profile = fixture.Request.Profile with { Readme = new ReadmeProfile("README.md") } };
        var result = await fixture.Preparer.PrepareAsync(request, CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "ONIP1009")).IsTrue();
    }

    [Test]
    public async Task Prepare_WithStaleReadme_LeavesSourceAndCandidateUntouched()
    {
        using var fixture = new PreparationFixture();
        var readme = Path.Combine(fixture.WorktreeRoot, "README.md");
        var original = "<!-- oni-mod-pipeline:workshop-description:start -->\nold\n<!-- oni-mod-pipeline:workshop-description:end -->\n";
        File.WriteAllText(readme, original);
        var package = Path.Combine(fixture.WorktreeRoot, "node_modules", "steam-community-bbcode");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "package.json"), "{\"name\":\"steam-community-bbcode\",\"bin\":\"cli.cjs\"}");
        File.WriteAllText(Path.Combine(package, "cli.cjs"), "console.log(JSON.stringify({value: '# Updated description\\n', diagnostics: []}));");
        var rendererDirectory = Path.Combine(fixture.WorktreeRoot, "tools", "readme");
        Directory.CreateDirectory(rendererDirectory);
        File.WriteAllText(Path.Combine(rendererDirectory, "render-description.mjs"), "process.stdin.pipe(process.stdout);");
        var request = fixture.Request with { Profile = fixture.Request.Profile with { Readme = new ReadmeProfile("README.md") } };
        var result = await fixture.Preparer.PrepareAsync(request, CancellationToken.None);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Evidence.Contains("stale", StringComparison.Ordinal))).IsTrue();
        await Assert.That(File.ReadAllText(readme)).IsEqualTo(original);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await Assert.That(fixture.Trace.Count).IsEqualTo(0);
    }
    private const string RunId =
        "20260827T140302.1234567Z-0123456789abcdef";

    [Test]
    public async Task PrepareAsync_WhenEveryGatePasses_PromotesExactAwaitingAcceptanceContract()
    {
        using var fixture = new PreparationFixture();

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue().Because(RenderDiagnostics(result.Diagnostics));
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.CandidateDirectory).IsEqualTo(fixture.Layout.CandidateDirectory);
        await Assert.That(result.Value.State).IsEqualTo(ReleaseCandidateState.AwaitingAcceptance);
        await Assert.That(result.Value.Layout.RunId).IsEqualTo(RunId);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsTrue();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);

        await Assert.That(Directory.EnumerateDirectories(fixture.Layout.CandidateDirectory)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()).IsEquivalentTo((new[] { "release-evidence", "workshop-content", "workshop-listing" }).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(EnumerateRelativeFiles(fixture.Layout.CandidateDirectory)).IsEquivalentTo(new[]
            {
                "release-evidence/acceptance-test-plan.json",
                "release-evidence/automated-test-results/example-regressions.trx",
                "release-evidence/build-provenance.json",
                "release-evidence/release-content-manifest.json",
                "release-evidence/release-readiness-report.json",
                "release-evidence/release-summary.md",
                "release-evidence/uploader-checklist.md",
                "workshop-content/Example.dll",
                "workshop-content/mod.yaml",
                "workshop-content/mod_info.yaml",
                "workshop-listing/change-notes.bbcode",
                "workshop-listing/description.bbcode",
                "workshop-listing/preview.png"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(File.Exists(fixture.Layout.InstallationReceiptPath)).IsFalse();
        await Assert.That(File.Exists(fixture.Layout.AcceptanceTestResultsPath)).IsFalse();

        using var plan = JsonDocument.Parse(
            await File.ReadAllBytesAsync(fixture.Layout.AcceptanceTestPlanPath));
        await Assert.That(plan.RootElement.GetProperty("schemaVersion").GetInt32()).IsEqualTo(1);
        await Assert.That(plan.RootElement.GetProperty("staticId").GetString()).IsEqualTo("Example.Mod");
        await Assert.That(plan.RootElement.GetProperty("version").GetString()).IsEqualTo("1.2.3");
        await Assert.That(plan.RootElement.GetProperty("contentDigest").GetString()).IsEqualTo(result.Value.ContentManifest.ContentDigest);
        var checks = plan.RootElement.GetProperty("checks").EnumerateArray().ToArray();
        await Assert.That(checks.Length).IsEqualTo(2);
        await Assert.That(checks[0].GetProperty("id").GetString()).IsEqualTo("first-check");
        await Assert.That(checks[0].GetProperty("setup").GetString()).IsEqualTo("First setup");
        await Assert.That(checks[1].GetProperty("id").GetString()).IsEqualTo("second-check");
        await Assert.That(plan.RootElement.GetProperty("preparedAtUtc").GetDateTimeOffset()).IsEqualTo(new DateTimeOffset(2026, 8, 27, 14, 3, 2, TimeSpan.Zero).AddTicks(1234567));

        using var provenance = JsonDocument.Parse(
            await File.ReadAllBytesAsync(fixture.Layout.BuildProvenancePath));
        await Assert.That(provenance.RootElement.GetProperty("staticId").GetString()).IsEqualTo("Example.Mod");
        await Assert.That(provenance.RootElement
            .GetProperty("repositoryCommit").GetString()).IsEqualTo(PreparationFixture.Commit);
        await Assert.That(provenance.RootElement.GetProperty("relevantPathsClean").GetBoolean()).IsTrue();
        await Assert.That(provenance.RootElement.GetProperty("targetFramework").GetString()).IsEqualTo("netstandard2.1");
        await Assert.That(provenance.RootElement
                .GetProperty("primaryAssemblyTargetFrameworkName")
                .GetString()).IsEqualTo(".NETStandard,Version=v2.1");
        await Assert.That(provenance.RootElement.GetProperty("configuration").GetString()).IsEqualTo("Release");
        await Assert.That(provenance.RootElement.GetProperty("preparedAtUtc").GetDateTimeOffset()).IsEqualTo(plan.RootElement.GetProperty("preparedAtUtc").GetDateTimeOffset());
        await Assert.That(provenance.RootElement
            .GetProperty("releaseContentDigest").GetString()).IsEqualTo(result.Value.ContentManifest.ContentDigest);
        var primaryOutput = provenance.RootElement.GetProperty("primaryOutput");
        await Assert.That(primaryOutput.GetProperty("sha256").GetString()?.Length).IsEqualTo(64);
        await Assert.That(primaryOutput.GetProperty("path").GetString()!).Contains("${ARTIFACTS}", StringComparison.Ordinal);
        var provenanceJson = provenance.RootElement.GetRawText();
        await Assert.That(provenanceJson).Contains("${WORKTREE}", StringComparison.Ordinal);
        await Assert.That(provenanceJson).Contains("${GAME}", StringComparison.Ordinal);
        await Assert.That(provenanceJson).Contains("${ARTIFACTS}", StringComparison.Ordinal);
        await Assert.That(provenanceJson).DoesNotContain(fixture.WorktreeRoot, StringComparison.OrdinalIgnoreCase);
        await Assert.That(provenanceJson).DoesNotContain(fixture.GameDirectory, StringComparison.OrdinalIgnoreCase);
        await Assert.That(provenanceJson).DoesNotContain(fixture.ArtifactsDirectory, StringComparison.OrdinalIgnoreCase);
        var gameBuildMetadata = provenance.RootElement
            .GetProperty("gameBuildMetadata")
            .GetString()!;
        await Assert.That(gameBuildMetadata).Contains("${WORKTREE}", StringComparison.Ordinal);
        await Assert.That(gameBuildMetadata).Contains("${GAME}", StringComparison.Ordinal);
        await Assert.That(gameBuildMetadata).Contains("${ARTIFACTS}", StringComparison.Ordinal);
        await Assert.That(gameBuildMetadata).DoesNotContain(fixture.WorktreeRoot, StringComparison.OrdinalIgnoreCase);

        using var readiness = JsonDocument.Parse(
            await File.ReadAllBytesAsync(fixture.Layout.ReleaseReadinessReportPath));
        await Assert.That(readiness.RootElement.GetProperty("state").GetString()).IsEqualTo("awaiting-acceptance");
        var blockers = readiness.RootElement.GetProperty("blockingConditions")
            .EnumerateArray()
            .Select(element => element.GetProperty("id").GetString())
            .ToArray();
        await Assert.That(blockers).IsEquivalentTo((new[]
            {
                "acceptance-test-results-missing",
                "installation-receipt-missing"
            }).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var evidencePaths = readiness.RootElement.GetProperty("evidenceIndex")
            .EnumerateArray()
            .Select(element => element.GetProperty("path").GetString())
            .ToArray();
        await Assert.That(evidencePaths).Contains("release-evidence/release-summary.md");
        await Assert.That(evidencePaths).Contains("release-evidence/uploader-checklist.md");
        await Assert.That(evidencePaths).DoesNotContain("release-evidence/release-readiness-report.json");

        var summary = await File.ReadAllTextAsync(fixture.Layout.ReleaseSummaryPath);
        await Assert.That(summary).Contains(fixture.Layout.WorkshopContentDirectory, StringComparison.Ordinal);
        await Assert.That(summary).Contains(fixture.Layout.DescriptionPath, StringComparison.Ordinal);
        await Assert.That(summary).Contains(result.Value.ContentManifest.ContentDigest, StringComparison.Ordinal);
        await Assert.That(summary).Contains("awaiting-acceptance", StringComparison.Ordinal);
        await Assert.That(summary).Contains("tweaks", StringComparison.Ordinal);
        await Assert.That(summary).Contains("Base Game", StringComparison.Ordinal);

        var checklist = await File.ReadAllTextAsync(fixture.Layout.UploaderChecklistPath);
        await Assert.That(checklist).Contains("Publication remains blocked until candidate state is ready-for-upload.", StringComparison.Ordinal);
        await Assert.That(checklist).Contains(fixture.Layout.WorkshopContentDirectory, StringComparison.Ordinal);
        await Assert.That(checklist).Contains(fixture.Layout.DescriptionPath, StringComparison.Ordinal);
        await Assert.That(checklist).Contains(fixture.Layout.ChangeNotesPath, StringComparison.Ordinal);
        await Assert.That(checklist).Contains(Path.Combine(fixture.Layout.WorkshopListingDirectory, "preview.png"), StringComparison.Ordinal);

        await Assert.That(fixture.Trace.Where(step => PreparationFixture.MajorSteps.Contains(step)).ToArray()).IsEquivalentTo(new[]
            {
                "build",
                "tests",
                "content",
                "listing",
                "manifest",
                "source-recheck",
                "delete-work",
                "promote"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.SourceInspector.CallCount).IsEqualTo(1);
        await Assert.That(fixture.Clock.GetUtcNowCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task PrepareAsync_WhenTestsRun_BindsTheBuildThisPreparationProduced()
    {
        using var fixture = new PreparationFixture();

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        Assert.NotNull(fixture.BuiltRunRoot);
        if (fixture.TestRunBoundBuild is null) throw new TUnit.Assertions.Exceptions.AssertionException("Release tests must inspect the build this preparation produced.");
        await Assert.That(fixture.TestRunBoundBuild.BuildResultPath).IsEqualTo(Path.Combine(fixture.BuiltRunRoot, "build-result.json"));
        await Assert.That(fixture.TestRunBoundBuild.ArtifactsDirectory).IsEqualTo(fixture.ArtifactsDirectory);
    }

    [Test]
    [Arguments(PreparationFailure.Restore)]
    [Arguments(PreparationFailure.AutomatedTest)]
    [Arguments(PreparationFailure.Listing)]
    [Arguments(PreparationFailure.Packaging)]
    [Arguments(PreparationFailure.InvalidBuildContract)]
    [Arguments(PreparationFailure.InvalidTestContract)]
    public async Task PrepareAsync_WhenExpectedStageFails_RemovesEveryOwnedTransientDirectory(
        PreparationFailure failure)
    {
        using var fixture = new PreparationFixture(failure);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(fixture.ExpectedPrimaryDiagnosticId);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [Test]
    public async Task PrepareAsync_WhenCompiledBuildOmitsTargetFrameworkMetadata_FailsClosedAndCleans()
    {
        using var fixture = new PreparationFixture(
            PreparationFailure.MissingTargetFrameworkMetadata);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.BuildFailed);
        await Assert.That(result.Diagnostics[0].Evidence).Contains("target-framework metadata", StringComparison.Ordinal);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [Test]
    public async Task PrepareAsync_WhenTargetFrameworkIdentitiesDisagree_FailsClosedAndCleans()
    {
        using var fixture = new PreparationFixture(
            PreparationFailure.MismatchedTargetFrameworkIdentity);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.BuildFailed);
        await Assert.That(result.Diagnostics[0].Evidence).Contains("does not match framework name", StringComparison.Ordinal);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [Test]
    [Arguments(PreparationFailure.Hash)]
    [Arguments(PreparationFailure.EvidenceWrite)]
    public async Task PrepareAsync_WhenInfrastructureStageThrows_ReturnsInternalFailureAndCleans(
        PreparationFailure failure)
    {
        using var fixture = new PreparationFixture(failure);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.UnexpectedFailure);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InternalFailure);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [Test]
    public async Task PrepareAsync_WhenFinalPromotionCollides_ReturnsOnip5007AndCleans()
    {
        using var fixture = new PreparationFixture(PreparationFailure.PromotionCollision);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.CandidateAlreadyExists);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.ReleaseNotReady);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [Test]
    public async Task PrepareAsync_WhenCleanupReportsFailure_PreservesPrimaryAndAppendsOnip9002()
    {
        using var fixture = new PreparationFixture(
            PreparationFailure.Restore,
            cleanupThrowsAfterDelete: true);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray()).IsEquivalentTo(new[] { DiagnosticIds.RestoreFailed, DiagnosticIds.CleanupFailed }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.BuildOrTestFailed);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [Test]
    public async Task PrepareAsync_WhenCandidateAlreadyExists_FailsBeforeCreatingTransientState()
    {
        using var fixture = new PreparationFixture();
        Directory.CreateDirectory(fixture.Layout.CandidateDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Layout.CandidateDirectory, "foreign.txt"),
            "preserve");

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo(DiagnosticIds.CandidateAlreadyExists);
        await Assert.That(await File.ReadAllTextAsync(
                Path.Combine(fixture.Layout.CandidateDirectory, "foreign.txt"))).IsEqualTo("preserve");
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
        await Assert.That(fixture.Builder.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task PrepareAsync_WhenSourceChangesBeforePromotion_FailsClosedAndCleans()
    {
        using var fixture = new PreparationFixture(PreparationFailure.SourceRecheck);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo(DiagnosticIds.DirtyReleaseInput);
        await Assert.That(Directory.Exists(fixture.Layout.CandidateDirectory)).IsFalse();
        await AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    private static string[] EnumerateRelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace((char)92, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static async Task AssertTransientSiblingsAreAbsent(CandidateLayout layout)
    {
        if (!Directory.Exists(layout.VersionDirectory))
        {
            return;
        }

        await Assert.That(Directory.EnumerateFileSystemEntries(layout.VersionDirectory)
            .Any(layout.IsOwnedTransientSibling)).IsFalse();
    }

    private static string RenderDiagnostics(IReadOnlyList<Diagnostic> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.Summary} {diagnostic.Evidence}"));
}

public enum PreparationFailure
{
    None,
    Restore,
    AutomatedTest,
    Listing,
    Packaging,
    Hash,
    EvidenceWrite,
    PromotionCollision,
    SourceRecheck,
    InvalidBuildContract,
    InvalidTestContract,
    MissingTargetFrameworkMetadata,
    MismatchedTargetFrameworkIdentity
}

internal sealed class PreparationFixture : IDisposable
{
    internal const string Commit = "0123456789abcdef0123456789abcdef01234567";
    internal static readonly HashSet<string> MajorSteps =
    [
        "build",
        "tests",
        "content",
        "listing",
        "manifest",
        "source-recheck",
        "delete-work",
        "promote"
    ];

    private static readonly DateTimeOffset PreparedAt =
        new(2026, 8, 27, 14, 3, 2, TimeSpan.Zero);

    private readonly TemporaryDirectory temporaryDirectory = new();
    private readonly PreparationFailure failure;

    internal PreparationFixture(
        PreparationFailure failure = PreparationFailure.None,
        bool cleanupThrowsAfterDelete = false,
        MaksymShostak.OniModPipeline.Catalogs.CatalogRunner? catalogRunner = null)
    {
        this.failure = failure;
        WorktreeRoot = temporaryDirectory.GetPath("repository");
        ModRoot = Path.Combine(WorktreeRoot, "mods", "example");
        GameDirectory = temporaryDirectory.GetPath("game");
        ArtifactsDirectory = temporaryDirectory.GetPath("artifacts");
        var managedDirectory = Path.Combine(
            GameDirectory,
            "OxygenNotIncluded_Data",
            "Managed");
        var userDataDirectory = temporaryDirectory.GetPath("user-data");
        Directory.CreateDirectory(ModRoot);
        Directory.CreateDirectory(managedDirectory);
        Directory.CreateDirectory(userDataDirectory);
        Directory.CreateDirectory(ArtifactsDirectory);
        WriteInputs(managedDirectory);

        Profile = CreateProfile();
        Metadata = new OniMetadata(
            "Example.Mod",
            "Example Mod",
            "Example description",
            "ALL",
            123456,
            "1.2.3",
            2);
        Environment = new PipelineEnvironment(
            GameDirectory,
            managedDirectory,
            userDataDirectory,
            Path.Combine(userDataDirectory, "mods", "Dev"),
            Path.Combine(userDataDirectory, "mods", "Local"),
            ArtifactsDirectory,
            "10.0.400",
            "Windows 11",
            "X64");
        PipelineExecutablePath = Path.Combine(WorktreeRoot, "pipeline", "oni-mod-pipeline.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(PipelineExecutablePath)!);
        File.WriteAllText(PipelineExecutablePath, "pipeline executable");
        var contributingPaths = Directory
            .EnumerateFiles(WorktreeRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(WorktreeRoot, path).Replace((char)92, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        InitialProvenance = new GitProvenance(
            WorktreeRoot,
            Commit,
            contributingPaths,
            []);
        Request = new ReleasePreparationRequest(
            Profile,
            Metadata,
            Environment,
            InitialProvenance,
            PipelineExecutablePath,
            $"build=U54-652372;game={GameDirectory};workspace={WorktreeRoot};artifacts={ArtifactsDirectory}");
        Layout = CandidateLayout.Create(
            ArtifactsDirectory,
            Metadata.StaticId,
            Metadata.Version,
            "20260827T140302.1234567Z-0123456789abcdef");

        Builder = new FixtureReleaseBuilder(this);
        SourceInspector = new FixtureSourceInspector(this);
        var fileSystem = new FixtureCandidateFileSystem(
            Trace,
            failure == PreparationFailure.PromotionCollision,
            cleanupThrowsAfterDelete);
        Clock = new FixedTimeProvider(PreparedAt.AddTicks(1234567));
        Preparer = new ReleaseCandidatePreparer(
            Builder,
            new FixtureTestRunner(this),
            new FixtureContentAssembler(this),
            new FixtureListingAssembler(this),
            new FixtureContentHasher(this),
            new FixtureArtifactWriter(this),
            SourceInspector,
            fileSystem,
            Clock,
            () => Convert.FromHexString("0123456789abcdef"),
            CreateTransientGuid,
            new MaksymShostak.OniModPipeline.Readme.ReadmeReleaseValidator(
                new MaksymShostak.OniModPipeline.Readme.ReadmeSynchronizer(
                    new MaksymShostak.OniModPipeline.Readme.InstalledBbcodeConverter(
                        new MaksymShostak.OniModPipeline.Processes.ExternalProcessRunner()),
                    new PassThroughReadmeCanonicalizer())), catalogRunner);
    }

    internal string WorktreeRoot { get; }
    internal string ModRoot { get; }
    internal string GameDirectory { get; }
    internal string ArtifactsDirectory { get; }
    internal string PipelineExecutablePath { get; }
    internal ModProfile Profile { get; }
    internal OniMetadata Metadata { get; }
    internal PipelineEnvironment Environment { get; }
    internal GitProvenance InitialProvenance { get; }
    internal ReleasePreparationRequest Request { get; }
    internal CandidateLayout Layout { get; }
    internal List<string> Trace { get; } = [];
    internal string? BuiltRunRoot { get; set; }
    internal BoundPipelineBuild? TestRunBoundBuild { get; set; }
    internal FixtureReleaseBuilder Builder { get; }
    internal FixtureSourceInspector SourceInspector { get; }
    internal FixedTimeProvider Clock { get; }
    internal ReleaseCandidatePreparer Preparer { get; }

    internal string ExpectedPrimaryDiagnosticId => failure switch
    {
        PreparationFailure.Restore => DiagnosticIds.RestoreFailed,
        PreparationFailure.AutomatedTest => DiagnosticIds.AutomatedTestFailed,
        PreparationFailure.Listing => DiagnosticIds.InvalidWorkshopListing,
        PreparationFailure.Packaging => DiagnosticIds.CandidateManifestMismatch,
        PreparationFailure.InvalidBuildContract => DiagnosticIds.BuildFailed,
        PreparationFailure.InvalidTestContract => DiagnosticIds.AutomatedTestFailed,
        _ => throw new InvalidOperationException($"No expected diagnostic for {failure}.")
    };

    internal bool FailsAt(PreparationFailure expected) => failure == expected;

    public void Dispose() => temporaryDirectory.Dispose();

    private Guid CreateTransientGuid() => Trace.Count(step => step == "guid") switch
    {
        0 => RecordGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        _ => RecordGuid(Guid.Parse("22222222-2222-2222-2222-222222222222"))
    };

    private Guid RecordGuid(Guid value)
    {
        Trace.Add("guid");
        return value;
    }

    private ModProfile CreateProfile() =>
        new(
            1,
            Path.Combine(ModRoot, "oni-mod-pipeline.toml"),
            ModRoot,
            "mod.yaml",
            "mod_info.yaml",
            new BuildProfile(
                "Source/Example.csproj",
                "Release",
                "OniManagedAssemblyDirectory",
                "{build-output}/Example.dll",
                ["PLib"]),
            [
                new PackageFileMapping("mod.yaml", "mod.yaml"),
                new PackageFileMapping("mod_info.yaml", "mod_info.yaml"),
                new PackageFileMapping(
                    "{build-output}/Example.dll",
                    "Example.dll")
            ],
            new WorkshopListingProfile(
                "description.bbcode",
                "change-notes.bbcode",
                "preview.png",
                ["tweaks"],
                ["base-game"],
                8000,
                8000),
            new LocalInstallProfile("ExampleMod"),
            [new TestProjectProfile(
                "example-regressions",
                "Tests/Example.Tests.csproj",
                true)],
            [
                new AcceptanceCheckProfile(
                    "first-check",
                    "First check",
                    true,
                    "First setup",
                    "First action",
                    "First expected observation"),
                new AcceptanceCheckProfile(
                    "second-check",
                    "Second check",
                    true,
                    "Second setup",
                    "Second action",
                    "Second expected observation")
            ]);

    private void WriteInputs(string managedDirectory)
    {
        Directory.CreateDirectory(Path.Combine(ModRoot, "Source"));
        Directory.CreateDirectory(Path.Combine(ModRoot, "Tests"));
        File.WriteAllText(
            Path.Combine(ModRoot, "oni-mod-pipeline.toml"),
            "schema-version = 1\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "mod.yaml"),
            "title: Example Mod\nstaticID: Example.Mod\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "mod_info.yaml"),
            "version: 1.2.3\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "description.bbcode"),
            "Description\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "change-notes.bbcode"),
            "Changes\n");
        File.WriteAllBytes(
            Path.Combine(ModRoot, "preview.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        File.WriteAllText(
            Path.Combine(ModRoot, "Source", "Example.csproj"),
            "<Project />\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "Source", "packages.lock.json"),
            "{}\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "Tests", "Example.Tests.csproj"),
            "<Project />\n");
        File.WriteAllText(
            Path.Combine(ModRoot, "Tests", "packages.lock.json"),
            "{}\n");
        File.WriteAllText(
            Path.Combine(managedDirectory, "Assembly-CSharp.dll"),
            "game assembly");
        File.WriteAllText(
            Path.Combine(managedDirectory, "0Harmony.dll"),
            "harmony");
    }
}

internal sealed class FixtureReleaseBuilder(PreparationFixture fixture) :
    IReleaseModBuilder
{
    internal int CallCount { get; private set; }

    public async Task<OperationResult<BuildResult>> BuildAsync(
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        fixture.Trace.Add("build");
        fixture.BuiltRunRoot = request.RunRoot;
        if (fixture.FailsAt(PreparationFailure.Restore))
        {
            return new OperationResult<BuildResult>(
                null,
                [DiagnosticCatalog.RestoreFailed(
                    request.Profile.Build!.EntryPoint,
                    "Injected locked restore failure.")],
                PipelineExitCode.BuildOrTestFailed);
        }

        var outputDirectory = Path.Combine(request.RunRoot, "output");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "Example.dll");
        var mergeInputPath = Path.Combine(request.RunRoot, "inputs", "PLib.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(mergeInputPath)!);
        await File.WriteAllTextAsync(
            outputPath,
            "compiled mod bytes",
            cancellationToken);
        await File.WriteAllTextAsync(
            mergeInputPath,
            "plib bytes",
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(request.RunRoot, "build-result.json"),
            "{}\n",
            cancellationToken);
        var hasher = new ContentHasher();
        var output = await hasher.HashFileAsync(outputPath, cancellationToken);
        var mergeInput = await hasher.HashFileAsync(mergeInputPath, cancellationToken);
        var gameReference = await hasher.HashFileAsync(
            Path.Combine(
                request.Environment.OniManagedAssemblyDirectory,
                "Assembly-CSharp.dll"),
            cancellationToken);
        var manifestInput = await hasher.HashFileAsync(
            request.Profile.ManifestPath,
            cancellationToken);
        var buildResult = new BuildResult(
                request.RunRoot,
                outputPath,
                [manifestInput],
                [output],
                [mergeInput],
                [gameReference],
                request.SourceCommit,
                request.ReleaseVersion,
                request.Environment.DotnetSdkVersion,
                [
                    "build",
                    request.Profile.Build!.EntryPoint,
                    $"/p:OniManagedAssemblyDirectory={request.Environment.OniManagedAssemblyDirectory}",
                    $"/p:BaseOutputPath={request.RunRoot}"
                ],
                new AssemblyVersionInfo(
                    "1.2.3.0",
                    "1.2.3.0",
                    $"1.2.3+{request.SourceCommit[..12]}"),
                true,
                "netstandard2.1",
                ".NETStandard,Version=v2.1");
        if (fixture.FailsAt(PreparationFailure.InvalidBuildContract))
        {
            buildResult = buildResult with { SourceBytesUnchanged = false };
        }
        else if (fixture.FailsAt(PreparationFailure.MissingTargetFrameworkMetadata))
        {
            buildResult = buildResult with
            {
                PrimaryAssemblyTargetFrameworkMoniker = null,
                PrimaryAssemblyTargetFrameworkName = null
            };
        }
        else if (fixture.FailsAt(PreparationFailure.MismatchedTargetFrameworkIdentity))
        {
            buildResult = buildResult with
            {
                PrimaryAssemblyTargetFrameworkMoniker = "net48"
            };
        }

        return new OperationResult<BuildResult>(
            buildResult,
            [],
            PipelineExitCode.Success);
    }
}

internal sealed class FixtureTestRunner(PreparationFixture fixture) :
    IReleaseAutomatedTestRunner
{
    public async Task<OperationResult<IReadOnlyList<AutomatedTestResult>>> RunAsync(
        ModProfile profile,
        PipelineEnvironment environment,
        string worktreeRoot,
        string resultsRoot,
        BoundPipelineBuild? boundBuild,
        CancellationToken cancellationToken)
    {
        fixture.Trace.Add("tests");
        fixture.TestRunBoundBuild = boundBuild;
        if (fixture.FailsAt(PreparationFailure.AutomatedTest))
        {
            return new OperationResult<IReadOnlyList<AutomatedTestResult>>(
                null,
                [DiagnosticCatalog.AutomatedTestFailed(
                    "example-regressions",
                    "Injected test failure.")],
                PipelineExitCode.BuildOrTestFailed);
        }

        Directory.CreateDirectory(resultsRoot);
        var trxPath = Path.Combine(resultsRoot, "example-regressions.trx");
        await File.WriteAllTextAsync(
            trxPath,
            """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Completed">
                <Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" />
              </ResultSummary>
            </TestRun>

            """,
            cancellationToken);
        var passed = !fixture.FailsAt(PreparationFailure.InvalidTestContract);
        return new OperationResult<IReadOnlyList<AutomatedTestResult>>(
            [new AutomatedTestResult(
                "example-regressions",
                Path.Combine(profile.ModRoot, "Tests", "Example.Tests.csproj"),
                trxPath,
                0,
                "passed",
                string.Empty,
                passed)],
            [],
            PipelineExitCode.Success);
    }
}

internal sealed class FixtureContentAssembler(PreparationFixture fixture) :
    IReleaseWorkshopContentAssembler
{
    private readonly WorkshopContentAssembler assembler = new();

    public Task<OperationResult<IReadOnlyList<FileDigest>>> AssembleAsync(
        ModProfile profile,
        BuildResult buildResult,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        fixture.Trace.Add("content");
        return fixture.FailsAt(PreparationFailure.Packaging)
            ? Task.FromResult(new OperationResult<IReadOnlyList<FileDigest>>(
                null,
                [DiagnosticCatalog.CandidateManifestMismatch(
                    "Injected packaging failure.")],
                PipelineExitCode.ReleaseNotReady))
            : assembler.AssembleAsync(
                profile,
                buildResult,
                targetDirectory,
                cancellationToken);
    }
}

internal sealed class FixtureListingAssembler(PreparationFixture fixture) :
    IReleaseWorkshopListingAssembler
{
    private readonly WorkshopListingAssembler assembler = new();

    public Task<OperationResult<WorkshopListingAssembly>> AssembleAsync(
        ModProfile profile,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        fixture.Trace.Add("listing");
        return fixture.FailsAt(PreparationFailure.Listing)
            ? Task.FromResult(new OperationResult<WorkshopListingAssembly>(
                null,
                [DiagnosticCatalog.InvalidWorkshopListing(
                    "workshop-listing.description",
                    "Injected listing failure.")],
                PipelineExitCode.InvalidInput))
            : assembler.AssembleAsync(profile, targetDirectory, cancellationToken);
    }
}

internal sealed class FixtureContentHasher(PreparationFixture fixture) :
    IReleaseContentHasher
{
    private readonly ContentHasher hasher = new();
    private int manifestCallCount;

    public Task<FileDigest> HashFileAsync(
        string absolutePath,
        CancellationToken cancellationToken) =>
        hasher.HashFileAsync(absolutePath, cancellationToken);

    public Task<ReleaseContentManifest> CreateManifestAsync(
        string releaseContentRoot,
        IReadOnlyList<(string AbsolutePath, ContentArea Area, ContentRole Role)> files,
        CancellationToken cancellationToken)
    {
        manifestCallCount++;
        fixture.Trace.Add(manifestCallCount == 1 ? "manifest" : "rehash-content");
        return fixture.FailsAt(PreparationFailure.Hash)
            ? Task.FromException<ReleaseContentManifest>(
                new IOException("Injected hash failure."))
            : hasher.CreateManifestAsync(
                releaseContentRoot,
                files,
                cancellationToken);
    }
}

internal sealed class FixtureArtifactWriter(PreparationFixture fixture) :
    IReleaseArtifactWriter
{
    private readonly Utf8ArtifactWriter writer = new();
    private int callCount;

    public Task WriteJsonAsync<T>(
        string destinationPath,
        T value,
        CancellationToken cancellationToken)
    {
        ThrowIfConfigured();
        return writer.WriteJsonAtomicallyAsync(
            destinationPath,
            value,
            cancellationToken);
    }

    public Task WriteLfTextAsync(
        string destinationPath,
        string text,
        CancellationToken cancellationToken)
    {
        ThrowIfConfigured();
        return writer.WriteLfTextAtomicallyAsync(
            destinationPath,
            text,
            cancellationToken);
    }

    private void ThrowIfConfigured()
    {
        callCount++;
        if (fixture.FailsAt(PreparationFailure.EvidenceWrite) && callCount == 1)
        {
            throw new IOException("Injected evidence write failure.");
        }
    }
}

internal sealed class FixtureSourceInspector(PreparationFixture fixture) :
    IReleaseSourceInspector
{
    internal int CallCount { get; private set; }

    public Task<OperationResult<GitProvenance>> InspectAsync(
        ModProfile profile,
        string pipelineExecutablePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        fixture.Trace.Add("source-recheck");
        var value = fixture.FailsAt(PreparationFailure.SourceRecheck)
            ? fixture.InitialProvenance with
            {
                DirtyPaths = [fixture.InitialProvenance.ContributingPaths[0]]
            }
            : fixture.InitialProvenance;
        return Task.FromResult(new OperationResult<GitProvenance>(
            value,
            [],
            PipelineExitCode.Success));
    }
}

internal sealed class FixtureCandidateFileSystem(
    List<string> trace,
    bool promotionCollides,
    bool cleanupThrowsAfterDelete) : IReleaseCandidateFileSystem
{
    private readonly ReleaseCandidateFileSystem inner = new();
    private bool cleanupFailureRaised;

    public bool EntryExists(string path) => inner.EntryExists(path);

    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    public void DeleteTransientDirectory(CandidateLayout layout, string path)
    {
        if (path.Contains(".work-", StringComparison.Ordinal))
        {
            trace.Add("delete-work");
        }

        inner.DeleteTransientDirectory(layout, path);
        if (cleanupThrowsAfterDelete && !cleanupFailureRaised)
        {
            cleanupFailureRaised = true;
            throw new IOException("Injected cleanup reporting failure.");
        }
    }

    public void PromoteStagedCandidate(
        CandidateLayout layout,
        string stagingDirectory)
    {
        trace.Add("promote");
        if (promotionCollides)
        {
            throw new ReleaseCandidateCollisionException(
                layout.CandidateDirectory,
                "Injected final rename collision.");
        }

        inner.PromoteStagedCandidate(layout, stagingDirectory);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    internal int GetUtcNowCallCount { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        GetUtcNowCallCount++;
        return utcNow;
    }
}
