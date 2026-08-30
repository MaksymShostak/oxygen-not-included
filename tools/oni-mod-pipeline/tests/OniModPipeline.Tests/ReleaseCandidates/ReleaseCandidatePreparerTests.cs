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

[TestClass]
public sealed class ReleaseCandidatePreparerTests
{
    private const string RunId =
        "20260827T140302.1234567Z-0123456789abcdef";

    [TestMethod]
    public async Task PrepareAsync_WhenEveryGatePasses_PromotesExactAwaitingAcceptanceContract()
    {
        using var fixture = new PreparationFixture();

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, RenderDiagnostics(result.Diagnostics));
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(fixture.Layout.CandidateDirectory, result.Value.CandidateDirectory);
        Assert.AreEqual(ReleaseCandidateState.AwaitingAcceptance, result.Value.State);
        Assert.AreEqual(RunId, result.Value.Layout.RunId);
        Assert.IsTrue(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);

        CollectionAssert.AreEqual(
            new[] { "release-evidence", "workshop-content", "workshop-listing" },
            Directory.EnumerateDirectories(fixture.Layout.CandidateDirectory)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        CollectionAssert.AreEqual(
            new[]
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
            },
            EnumerateRelativeFiles(fixture.Layout.CandidateDirectory));
        Assert.IsFalse(File.Exists(fixture.Layout.InstallationReceiptPath));
        Assert.IsFalse(File.Exists(fixture.Layout.AcceptanceTestResultsPath));

        using var plan = JsonDocument.Parse(
            await File.ReadAllBytesAsync(fixture.Layout.AcceptanceTestPlanPath));
        Assert.AreEqual(1, plan.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("Example.Mod", plan.RootElement.GetProperty("staticId").GetString());
        Assert.AreEqual("1.2.3", plan.RootElement.GetProperty("version").GetString());
        Assert.AreEqual(
            result.Value.ContentManifest.ContentDigest,
            plan.RootElement.GetProperty("contentDigest").GetString());
        var checks = plan.RootElement.GetProperty("checks").EnumerateArray().ToArray();
        Assert.AreEqual(2, checks.Length);
        Assert.AreEqual("first-check", checks[0].GetProperty("id").GetString());
        Assert.AreEqual("First setup", checks[0].GetProperty("setup").GetString());
        Assert.AreEqual("second-check", checks[1].GetProperty("id").GetString());
        Assert.AreEqual(
            new DateTimeOffset(2026, 8, 27, 14, 3, 2, TimeSpan.Zero).AddTicks(1234567),
            plan.RootElement.GetProperty("preparedAtUtc").GetDateTimeOffset());

        using var provenance = JsonDocument.Parse(
            await File.ReadAllBytesAsync(fixture.Layout.BuildProvenancePath));
        Assert.AreEqual("Example.Mod", provenance.RootElement.GetProperty("staticId").GetString());
        Assert.AreEqual(PreparationFixture.Commit, provenance.RootElement
            .GetProperty("repositoryCommit").GetString());
        Assert.IsTrue(provenance.RootElement.GetProperty("relevantPathsClean").GetBoolean());
        Assert.AreEqual(
            ".NETStandard,Version=v2.1",
            provenance.RootElement.GetProperty("targetFramework").GetString());
        Assert.AreEqual("Release", provenance.RootElement.GetProperty("configuration").GetString());
        Assert.AreEqual(
            plan.RootElement.GetProperty("preparedAtUtc").GetDateTimeOffset(),
            provenance.RootElement.GetProperty("preparedAtUtc").GetDateTimeOffset());
        Assert.AreEqual(result.Value.ContentManifest.ContentDigest, provenance.RootElement
            .GetProperty("releaseContentDigest").GetString());
        var primaryOutput = provenance.RootElement.GetProperty("primaryOutput");
        Assert.AreEqual(64, primaryOutput.GetProperty("sha256").GetString()?.Length);
        Assert.Contains(
            "${ARTIFACTS}",
            primaryOutput.GetProperty("path").GetString()!,
            StringComparison.Ordinal);
        var provenanceJson = provenance.RootElement.GetRawText();
        Assert.Contains("${WORKTREE}", provenanceJson, StringComparison.Ordinal);
        Assert.Contains("${GAME}", provenanceJson, StringComparison.Ordinal);
        Assert.Contains("${ARTIFACTS}", provenanceJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.WorktreeRoot, provenanceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.GameDirectory, provenanceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.ArtifactsDirectory, provenanceJson, StringComparison.OrdinalIgnoreCase);
        var gameBuildMetadata = provenance.RootElement
            .GetProperty("gameBuildMetadata")
            .GetString()!;
        Assert.Contains("${WORKTREE}", gameBuildMetadata, StringComparison.Ordinal);
        Assert.Contains("${GAME}", gameBuildMetadata, StringComparison.Ordinal);
        Assert.Contains("${ARTIFACTS}", gameBuildMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain(
            fixture.WorktreeRoot,
            gameBuildMetadata,
            StringComparison.OrdinalIgnoreCase);

        using var readiness = JsonDocument.Parse(
            await File.ReadAllBytesAsync(fixture.Layout.ReleaseReadinessReportPath));
        Assert.AreEqual(
            "awaiting-acceptance",
            readiness.RootElement.GetProperty("state").GetString());
        var blockers = readiness.RootElement.GetProperty("blockingConditions")
            .EnumerateArray()
            .Select(element => element.GetProperty("id").GetString())
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "acceptance-test-results-missing",
                "installation-receipt-missing"
            },
            blockers);
        var evidencePaths = readiness.RootElement.GetProperty("evidenceIndex")
            .EnumerateArray()
            .Select(element => element.GetProperty("path").GetString())
            .ToArray();
        CollectionAssert.Contains(evidencePaths, "release-evidence/release-summary.md");
        CollectionAssert.Contains(evidencePaths, "release-evidence/uploader-checklist.md");
        CollectionAssert.DoesNotContain(
            evidencePaths,
            "release-evidence/release-readiness-report.json");

        var summary = await File.ReadAllTextAsync(fixture.Layout.ReleaseSummaryPath);
        Assert.Contains(fixture.Layout.WorkshopContentDirectory, summary, StringComparison.Ordinal);
        Assert.Contains(fixture.Layout.DescriptionPath, summary, StringComparison.Ordinal);
        Assert.Contains(result.Value.ContentManifest.ContentDigest, summary, StringComparison.Ordinal);
        Assert.Contains("awaiting-acceptance", summary, StringComparison.Ordinal);
        Assert.Contains("tweaks", summary, StringComparison.Ordinal);
        Assert.Contains("Base Game", summary, StringComparison.Ordinal);

        var checklist = await File.ReadAllTextAsync(fixture.Layout.UploaderChecklistPath);
        Assert.Contains(
            "Publication remains blocked until candidate state is ready-for-upload.",
            checklist,
            StringComparison.Ordinal);
        Assert.Contains(fixture.Layout.WorkshopContentDirectory, checklist, StringComparison.Ordinal);
        Assert.Contains(fixture.Layout.DescriptionPath, checklist, StringComparison.Ordinal);
        Assert.Contains(fixture.Layout.ChangeNotesPath, checklist, StringComparison.Ordinal);
        Assert.Contains(
            Path.Combine(fixture.Layout.WorkshopListingDirectory, "preview.png"),
            checklist,
            StringComparison.Ordinal);

        CollectionAssert.AreEqual(
            new[]
            {
                "build",
                "tests",
                "content",
                "listing",
                "manifest",
                "source-recheck",
                "delete-work",
                "promote"
            },
            fixture.Trace.Where(step => PreparationFixture.MajorSteps.Contains(step)).ToArray());
        Assert.AreEqual(1, fixture.SourceInspector.CallCount);
        Assert.AreEqual(1, fixture.Clock.GetUtcNowCallCount);
    }

    [TestMethod]
    [DataRow(PreparationFailure.Restore)]
    [DataRow(PreparationFailure.AutomatedTest)]
    [DataRow(PreparationFailure.Listing)]
    [DataRow(PreparationFailure.Packaging)]
    [DataRow(PreparationFailure.InvalidBuildContract)]
    [DataRow(PreparationFailure.InvalidTestContract)]
    public async Task PrepareAsync_WhenExpectedStageFails_RemovesEveryOwnedTransientDirectory(
        PreparationFailure failure)
    {
        using var fixture = new PreparationFixture(failure);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(fixture.ExpectedPrimaryDiagnosticId, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenCompiledBuildOmitsTargetFrameworkMetadata_FailsClosedAndCleans()
    {
        using var fixture = new PreparationFixture(
            PreparationFailure.MissingTargetFrameworkMetadata);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.BuildFailed, result.Diagnostics[0].Id);
        Assert.Contains(
            "target-framework metadata",
            result.Diagnostics[0].Evidence,
            StringComparison.Ordinal);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [TestMethod]
    [DataRow(PreparationFailure.Hash)]
    [DataRow(PreparationFailure.EvidenceWrite)]
    public async Task PrepareAsync_WhenInfrastructureStageThrows_ReturnsInternalFailureAndCleans(
        PreparationFailure failure)
    {
        using var fixture = new PreparationFixture(failure);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.UnexpectedFailure, result.Diagnostics[0].Id);
        Assert.AreEqual(PipelineExitCode.InternalFailure, result.ExitCode);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenFinalPromotionCollides_ReturnsOnip5007AndCleans()
    {
        using var fixture = new PreparationFixture(PreparationFailure.PromotionCollision);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.CandidateAlreadyExists, result.Diagnostics[0].Id);
        Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenCleanupReportsFailure_PreservesPrimaryAndAppendsOnip9002()
    {
        using var fixture = new PreparationFixture(
            PreparationFailure.Restore,
            cleanupThrowsAfterDelete: true);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { DiagnosticIds.RestoreFailed, DiagnosticIds.CleanupFailed },
            result.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray());
        Assert.AreEqual(PipelineExitCode.BuildOrTestFailed, result.ExitCode);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    [TestMethod]
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

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.CandidateAlreadyExists, result.Diagnostics.Single().Id);
        Assert.AreEqual(
            "preserve",
            await File.ReadAllTextAsync(
                Path.Combine(fixture.Layout.CandidateDirectory, "foreign.txt")));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
        Assert.AreEqual(0, fixture.Builder.CallCount);
    }

    [TestMethod]
    public async Task PrepareAsync_WhenSourceChangesBeforePromotion_FailsClosedAndCleans()
    {
        using var fixture = new PreparationFixture(PreparationFailure.SourceRecheck);

        var result = await fixture.Preparer.PrepareAsync(
            fixture.Request,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.DirtyReleaseInput, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.Layout.CandidateDirectory));
        AssertTransientSiblingsAreAbsent(fixture.Layout);
    }

    private static string[] EnumerateRelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace((char)92, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static void AssertTransientSiblingsAreAbsent(CandidateLayout layout)
    {
        if (!Directory.Exists(layout.VersionDirectory))
        {
            return;
        }

        Assert.IsFalse(Directory.EnumerateFileSystemEntries(layout.VersionDirectory)
            .Any(layout.IsOwnedTransientSibling));
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
    MissingTargetFrameworkMetadata
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
        bool cleanupThrowsAfterDelete = false)
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
            CreateTransientGuid);
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
                ".NETStandard,Version=v2.1");
        if (fixture.FailsAt(PreparationFailure.InvalidBuildContract))
        {
            buildResult = buildResult with { SourceBytesUnchanged = false };
        }
        else if (fixture.FailsAt(PreparationFailure.MissingTargetFrameworkMetadata))
        {
            buildResult = buildResult with { PrimaryTargetFrameworkMoniker = null };
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
        CancellationToken cancellationToken)
    {
        fixture.Trace.Add("tests");
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
