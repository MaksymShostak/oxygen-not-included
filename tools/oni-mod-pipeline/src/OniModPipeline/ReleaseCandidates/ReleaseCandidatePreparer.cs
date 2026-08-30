using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.WorkshopContent;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal sealed record ReleasePreparationRequest(
    ModProfile Profile,
    OniMetadata Metadata,
    PipelineEnvironment Environment,
    GitProvenance InitialProvenance,
    string PipelineExecutablePath,
    string? GameBuildMetadata);

internal interface IReleaseCandidatePreparer
{
    Task<OperationResult<PreparedReleaseCandidate>> PrepareAsync(
        ReleasePreparationRequest request,
        CancellationToken cancellationToken);
}

internal interface IReleaseModBuilder
{
    Task<OperationResult<BuildResult>> BuildAsync(
        BuildRequest request,
        CancellationToken cancellationToken);
}

internal interface IReleaseAutomatedTestRunner
{
    Task<OperationResult<IReadOnlyList<AutomatedTestResult>>> RunAsync(
        ModProfile profile,
        PipelineEnvironment environment,
        string worktreeRoot,
        string resultsRoot,
        CancellationToken cancellationToken);
}

internal interface IReleaseWorkshopContentAssembler
{
    Task<OperationResult<IReadOnlyList<FileDigest>>> AssembleAsync(
        ModProfile profile,
        BuildResult buildResult,
        string targetDirectory,
        CancellationToken cancellationToken);
}

internal interface IReleaseWorkshopListingAssembler
{
    Task<OperationResult<WorkshopListingAssembly>> AssembleAsync(
        ModProfile profile,
        string targetDirectory,
        CancellationToken cancellationToken);
}

internal interface IReleaseContentHasher
{
    Task<FileDigest> HashFileAsync(
        string absolutePath,
        CancellationToken cancellationToken);

    Task<ReleaseContentManifest> CreateManifestAsync(
        string releaseContentRoot,
        IReadOnlyList<(string AbsolutePath, ContentArea Area, ContentRole Role)> files,
        CancellationToken cancellationToken);
}

internal interface IReleaseArtifactWriter
{
    Task WriteJsonAsync<T>(
        string destinationPath,
        T value,
        CancellationToken cancellationToken);

    Task WriteLfTextAsync(
        string destinationPath,
        string text,
        CancellationToken cancellationToken);
}

internal interface IReleaseSourceInspector
{
    Task<OperationResult<GitProvenance>> InspectAsync(
        ModProfile profile,
        string pipelineExecutablePath,
        CancellationToken cancellationToken);
}

internal interface IReleaseCandidateFileSystem
{
    bool EntryExists(string path);

    void CreateDirectory(string path);

    void DeleteTransientDirectory(CandidateLayout layout, string path);

    void PromoteStagedCandidate(CandidateLayout layout, string stagingDirectory);
}

internal sealed class ReleaseCandidateCollisionException(
    string candidatePath,
    string reason) : IOException(reason)
{
    internal string CandidatePath { get; } = candidatePath;
}

internal sealed class ReleaseCandidateFileSystem : IReleaseCandidateFileSystem
{
    public bool EntryExists(string path) => File.Exists(path) || Directory.Exists(path);

    public void CreateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetFullPath(path));
    }

    public void DeleteTransientDirectory(CandidateLayout layout, string path)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = Path.GetFullPath(path);
        if (!layout.IsOwnedTransientSibling(resolved))
        {
            throw new InvalidOperationException(
                $"Refusing to delete unowned release-candidate path '{resolved}'.");
        }

        if (File.Exists(resolved))
        {
            throw new InvalidOperationException(
                $"Owned transient path '{resolved}' is a file, not a directory.");
        }

        if (!Directory.Exists(resolved))
        {
            return;
        }

        EnsureRegularDirectory(layout.VersionDirectory, "candidate version directory");
        EnsureRegularDirectory(resolved, "owned transient directory");
        Directory.Delete(resolved, recursive: true);
    }

    public void PromoteStagedCandidate(
        CandidateLayout layout,
        string stagingDirectory)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        var staging = Path.GetFullPath(stagingDirectory);
        if (!layout.IsOwnedTransientSibling(staging) ||
            !Path.GetFileName(staging).Contains(".staging-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to promote unowned candidate staging path '{staging}'.");
        }

        EnsureRegularDirectory(layout.VersionDirectory, "candidate version directory");
        EnsureRegularDirectory(staging, "candidate staging directory");
        if (EntryExists(layout.CandidateDirectory))
        {
            throw new ReleaseCandidateCollisionException(
                layout.CandidateDirectory,
                "the final candidate path already exists");
        }

        try
        {
            Directory.Move(staging, layout.CandidateDirectory);
        }
        catch (IOException exception) when (EntryExists(layout.CandidateDirectory))
        {
            throw new ReleaseCandidateCollisionException(
                layout.CandidateDirectory,
                $"atomic promotion collided with an existing destination ({exception.Message})");
        }
    }

    private static void EnsureRegularDirectory(string path, string description)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        directory.Refresh();
        if (!directory.Exists ||
            (directory.Attributes & FileAttributes.ReparsePoint) != 0 ||
            directory.LinkTarget is not null)
        {
            throw new InvalidOperationException(
                $"The {description} '{directory.FullName}' must be an existing non-linked directory.");
        }
    }
}

internal sealed class ReleaseCandidatePreparer : IReleaseCandidatePreparer
{
    private const string ReleaseConfiguration = "Release";

    private static readonly StringComparer HostPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly IReleaseModBuilder modBuilder;
    private readonly IReleaseAutomatedTestRunner automatedTestRunner;
    private readonly IReleaseWorkshopContentAssembler workshopContentAssembler;
    private readonly IReleaseWorkshopListingAssembler workshopListingAssembler;
    private readonly IReleaseContentHasher contentHasher;
    private readonly IReleaseArtifactWriter artifactWriter;
    private readonly IReleaseSourceInspector sourceInspector;
    private readonly IReleaseCandidateFileSystem fileSystem;
    private readonly TimeProvider timeProvider;
    private readonly Func<byte[]> entropySource;
    private readonly Func<Guid> transientSuffixFactory;

    internal ReleaseCandidatePreparer(
        IReleaseModBuilder modBuilder,
        IReleaseAutomatedTestRunner automatedTestRunner,
        IReleaseWorkshopContentAssembler workshopContentAssembler,
        IReleaseWorkshopListingAssembler workshopListingAssembler,
        IReleaseContentHasher contentHasher,
        IReleaseArtifactWriter artifactWriter,
        IReleaseSourceInspector sourceInspector,
        IReleaseCandidateFileSystem fileSystem,
        TimeProvider timeProvider,
        Func<byte[]> entropySource,
        Func<Guid> transientSuffixFactory)
    {
        ArgumentNullException.ThrowIfNull(modBuilder);
        ArgumentNullException.ThrowIfNull(automatedTestRunner);
        ArgumentNullException.ThrowIfNull(workshopContentAssembler);
        ArgumentNullException.ThrowIfNull(workshopListingAssembler);
        ArgumentNullException.ThrowIfNull(contentHasher);
        ArgumentNullException.ThrowIfNull(artifactWriter);
        ArgumentNullException.ThrowIfNull(sourceInspector);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(entropySource);
        ArgumentNullException.ThrowIfNull(transientSuffixFactory);

        this.modBuilder = modBuilder;
        this.automatedTestRunner = automatedTestRunner;
        this.workshopContentAssembler = workshopContentAssembler;
        this.workshopListingAssembler = workshopListingAssembler;
        this.contentHasher = contentHasher;
        this.artifactWriter = artifactWriter;
        this.sourceInspector = sourceInspector;
        this.fileSystem = fileSystem;
        this.timeProvider = timeProvider;
        this.entropySource = entropySource;
        this.transientSuffixFactory = transientSuffixFactory;
    }

    internal static ReleaseCandidatePreparer CreateDefault(
        IExternalProcessRunner processRunner,
        GitRepositoryInspector gitRepositoryInspector)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(gitRepositoryInspector);
        var hasher = new ContentHasher();
        var writer = new Utf8ArtifactWriter();
        return new ReleaseCandidatePreparer(
            new ReleaseModBuilderAdapter(new ModBuilder(processRunner, writer)),
            new ReleaseAutomatedTestRunnerAdapter(processRunner),
            new ReleaseWorkshopContentAssemblerAdapter(
                new WorkshopContentAssembler()),
            new ReleaseWorkshopListingAssemblerAdapter(
                new WorkshopListingAssembler()),
            new ReleaseContentHasherAdapter(hasher),
            new ReleaseArtifactWriterAdapter(writer),
            new ReleaseSourceInspectorAdapter(gitRepositoryInspector),
            new ReleaseCandidateFileSystem(),
            TimeProvider.System,
            () => RandomNumberGenerator.GetBytes(8),
            Guid.NewGuid);
    }

    public async Task<OperationResult<PreparedReleaseCandidate>> PrepareAsync(
        ReleasePreparationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.Metadata);
        ArgumentNullException.ThrowIfNull(request.Environment);
        ArgumentNullException.ThrowIfNull(request.InitialProvenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PipelineExecutablePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.InitialProvenance.IsClean)
        {
            return Failure(
                DiagnosticCatalog.DirtyReleaseInput(
                    "The release preparation request was not produced from a clean contributing source set."),
                PipelineExitCode.ReleaseNotReady);
        }

        CandidateLayout layout;
        DateTimeOffset preparedAtUtc;
        try
        {
            preparedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            var runId = RunIdFactory.Create(
                preparedAtUtc,
                entropySource());
            layout = CandidateLayout.Create(
                request.Environment.ArtifactsDirectory,
                request.Metadata.StaticId,
                request.Metadata.Version,
                runId);
        }
        catch (ArgumentException exception)
        {
            return Failure(
                DiagnosticCatalog.InvalidProfileSemantics(
                    "release-candidate.identity",
                    exception.Message),
                PipelineExitCode.InvalidInput);
        }

        if (fileSystem.EntryExists(layout.CandidateDirectory))
        {
            return Failure(
                DiagnosticCatalog.CandidateAlreadyExists(
                    layout.CandidateDirectory,
                    "the final run-ID directory existed before preparation began"),
                PipelineExitCode.ReleaseNotReady);
        }

        var stagingDirectory = layout.CreateTransientSiblingPath(
            "staging",
            transientSuffixFactory());
        var workDirectory = layout.CreateTransientSiblingPath(
            "work",
            transientSuffixFactory());
        if (fileSystem.EntryExists(stagingDirectory) ||
            fileSystem.EntryExists(workDirectory))
        {
            return Failure(
                DiagnosticCatalog.CandidateAlreadyExists(
                    layout.CandidateDirectory,
                    "a unique transient sibling path collided with existing filesystem state"),
                PipelineExitCode.ReleaseNotReady);
        }

        try
        {
            var stagedContentDirectory = layout.GetStagingPath(
                stagingDirectory,
                layout.WorkshopContentDirectory);
            var stagedListingDirectory = layout.GetStagingPath(
                stagingDirectory,
                layout.WorkshopListingDirectory);
            var stagedEvidenceDirectory = layout.GetStagingPath(
                stagingDirectory,
                layout.ReleaseEvidenceDirectory);
            var stagedTestResultsDirectory = layout.GetStagingPath(
                stagingDirectory,
                layout.AutomatedTestResultsDirectory);

            fileSystem.CreateDirectory(stagingDirectory);
            fileSystem.CreateDirectory(stagedContentDirectory);
            fileSystem.CreateDirectory(stagedListingDirectory);
            fileSystem.CreateDirectory(stagedEvidenceDirectory);

            var buildResult = await modBuilder.BuildAsync(
                new BuildRequest(
                    request.Profile,
                    request.Environment,
                    ReleaseConfiguration,
                    workDirectory,
                    request.Metadata.Version,
                    request.InitialProvenance.Commit),
                cancellationToken).ConfigureAwait(false);
            if (!buildResult.IsSuccess)
            {
                return FailAndCleanup(
                    buildResult.Diagnostics,
                    buildResult.ExitCode,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var build = buildResult.Value!;
            var buildContractDiagnostic = ValidateBuildResult(
                request,
                build,
                workDirectory);
            if (buildContractDiagnostic is not null)
            {
                return FailAndCleanup(
                    [buildContractDiagnostic],
                    PipelineExitCode.BuildOrTestFailed,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var testResult = await automatedTestRunner.RunAsync(
                request.Profile,
                request.Environment,
                request.InitialProvenance.WorktreeRoot,
                stagedTestResultsDirectory,
                cancellationToken).ConfigureAwait(false);
            if (!testResult.IsSuccess)
            {
                return FailAndCleanup(
                    testResult.Diagnostics,
                    testResult.ExitCode,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var automatedTests = testResult.Value!;
            var testContractDiagnostic = ValidateAutomatedTestResults(
                request.Profile,
                automatedTests,
                stagedTestResultsDirectory);
            if (testContractDiagnostic is not null)
            {
                return FailAndCleanup(
                    [testContractDiagnostic],
                    PipelineExitCode.BuildOrTestFailed,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var contentResult = await workshopContentAssembler.AssembleAsync(
                request.Profile,
                build,
                stagedContentDirectory,
                cancellationToken).ConfigureAwait(false);
            if (!contentResult.IsSuccess)
            {
                return FailAndCleanup(
                    contentResult.Diagnostics,
                    contentResult.ExitCode,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var listingResult = await workshopListingAssembler.AssembleAsync(
                request.Profile,
                stagedListingDirectory,
                cancellationToken).ConfigureAwait(false);
            if (!listingResult.IsSuccess)
            {
                return FailAndCleanup(
                    listingResult.Diagnostics,
                    listingResult.ExitCode,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var listing = listingResult.Value!;
            var manifestFiles = CreateManifestFileSet(
                contentResult.Value!,
                listing);
            var contentManifest = await contentHasher.CreateManifestAsync(
                stagingDirectory,
                manifestFiles,
                cancellationToken).ConfigureAwait(false);
            var stagedManifestPath = layout.GetStagingPath(
                stagingDirectory,
                layout.ReleaseContentManifestPath);
            await artifactWriter.WriteJsonAsync(
                stagedManifestPath,
                contentManifest,
                cancellationToken).ConfigureAwait(false);

            var acceptancePlan = AcceptanceTestPlan.Create(
                request.Profile,
                request.Metadata,
                contentManifest.ContentDigest,
                preparedAtUtc);
            var stagedAcceptancePlanPath = layout.GetStagingPath(
                stagingDirectory,
                layout.AcceptanceTestPlanPath);
            await artifactWriter.WriteJsonAsync(
                stagedAcceptancePlanPath,
                acceptancePlan,
                cancellationToken).ConfigureAwait(false);
            var acceptancePlanDigest = await contentHasher.HashFileAsync(
                stagedAcceptancePlanPath,
                cancellationToken).ConfigureAwait(false);

            var evidenceMapper = new EvidencePathMapper(
                request.InitialProvenance.WorktreeRoot,
                request.Environment.GameDirectory,
                request.Environment.ArtifactsDirectory);
            var pipelineExecutableDigest = await contentHasher.HashFileAsync(
                request.PipelineExecutablePath,
                cancellationToken).ConfigureAwait(false);
            var lockFiles = await HashLockFilesAsync(
                request.InitialProvenance,
                evidenceMapper,
                cancellationToken).ConfigureAwait(false);
            var provenance = CreateBuildProvenance(
                request,
                build,
                listing,
                preparedAtUtc,
                contentManifest,
                pipelineExecutableDigest,
                lockFiles,
                acceptancePlanDigest,
                evidenceMapper);
            var stagedProvenancePath = layout.GetStagingPath(
                stagingDirectory,
                layout.BuildProvenancePath);
            await artifactWriter.WriteJsonAsync(
                stagedProvenancePath,
                provenance,
                cancellationToken).ConfigureAwait(false);

            var documentContext = new ReleaseDocumentContext(
                request.Metadata,
                layout,
                contentManifest,
                provenance,
                listing,
                automatedTests,
                ReleaseCandidateState.AwaitingAcceptance,
                [],
                AutomatedTestRequirements: request.Profile.TestProjects.ToDictionary(
                    project => project.Id,
                    project => project.Required,
                    StringComparer.Ordinal));
            var stagedSummaryPath = layout.GetStagingPath(
                stagingDirectory,
                layout.ReleaseSummaryPath);
            var stagedChecklistPath = layout.GetStagingPath(
                stagingDirectory,
                layout.UploaderChecklistPath);
            await artifactWriter.WriteLfTextAsync(
                stagedSummaryPath,
                ReleaseSummaryRenderer.Render(documentContext),
                cancellationToken).ConfigureAwait(false);
            await artifactWriter.WriteLfTextAsync(
                stagedChecklistPath,
                UploaderChecklistRenderer.Render(documentContext),
                cancellationToken).ConfigureAwait(false);

            var evidencePaths = new List<string>
            {
                stagedManifestPath,
                stagedProvenancePath,
                stagedAcceptancePlanPath,
                stagedSummaryPath,
                stagedChecklistPath
            };
            evidencePaths.AddRange(automatedTests.Select(test => test.TrxPath));
            var evidenceIndex = await CreateEvidenceIndexAsync(
                stagingDirectory,
                evidencePaths,
                cancellationToken).ConfigureAwait(false);
            var automatedTestEvidence = automatedTests
                .OrderBy(test => test.TestProjectId, StringComparer.Ordinal)
                .Select(test => new AutomatedTestEvidence(
                    test.TestProjectId,
                    request.Profile.TestProjects.Single(project =>
                        string.Equals(
                            project.Id,
                            test.TestProjectId,
                            StringComparison.Ordinal)).Required,
                    evidenceMapper.MapPath(test.ProjectPath),
                    NormalizeCandidateRelativePath(stagingDirectory, test.TrxPath),
                    test.ExitCode,
                    test.Passed))
                .ToArray();
            var readiness = new ReleaseReadinessReport(
                1,
                request.Metadata.StaticId,
                request.Metadata.Version,
                contentManifest.ContentDigest,
                preparedAtUtc,
                ReleaseCandidateState.AwaitingAcceptance,
                BuildSucceeded: true,
                AutomatedTestsPassed: automatedTestEvidence
                    .Where(test => test.Required)
                    .All(test => test.Passed),
                PreparedContentVerified: true,
                RelevantSourcesClean: true,
                automatedTestEvidence,
                evidenceIndex,
                [
                    new ReleaseBlockingCondition(
                        "acceptance-test-results-missing",
                        "Human acceptance results have not been recorded for this content digest."),
                    new ReleaseBlockingCondition(
                        "installation-receipt-missing",
                        "This exact candidate has not been installed and verified for acceptance testing.")
                ],
                IrreversibleInvalidation: null);
            var stagedReadinessPath = layout.GetStagingPath(
                stagingDirectory,
                layout.ReleaseReadinessReportPath);
            await artifactWriter.WriteJsonAsync(
                stagedReadinessPath,
                readiness,
                cancellationToken).ConfigureAwait(false);

            var verificationDiagnostic = await VerifyPreparedStagingAsync(
                layout,
                stagingDirectory,
                manifestFiles,
                contentManifest,
                evidenceIndex,
                automatedTests,
                cancellationToken).ConfigureAwait(false);
            if (verificationDiagnostic is not null)
            {
                return FailAndCleanup(
                    [verificationDiagnostic],
                    PipelineExitCode.ReleaseNotReady,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var sourceResult = await sourceInspector.InspectAsync(
                request.Profile,
                request.PipelineExecutablePath,
                cancellationToken).ConfigureAwait(false);
            if (!sourceResult.IsSuccess)
            {
                return FailAndCleanup(
                    sourceResult.Diagnostics,
                    sourceResult.ExitCode,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            var sourceDiagnostic = ValidateFinalSourceState(
                request.InitialProvenance,
                sourceResult.Value!);
            if (sourceDiagnostic is not null)
            {
                return FailAndCleanup(
                    [sourceDiagnostic],
                    PipelineExitCode.ReleaseNotReady,
                    layout,
                    stagingDirectory,
                    workDirectory);
            }

            fileSystem.DeleteTransientDirectory(layout, workDirectory);
            fileSystem.PromoteStagedCandidate(layout, stagingDirectory);
            return new OperationResult<PreparedReleaseCandidate>(
                new PreparedReleaseCandidate(
                    layout.CandidateDirectory,
                    layout,
                    contentManifest,
                    provenance,
                    ReleaseCandidateState.AwaitingAcceptance),
                [],
                PipelineExitCode.Success);
        }
        catch (OperationCanceledException)
        {
            _ = CleanupTransientDirectories(
                layout,
                workDirectory,
                stagingDirectory);
            throw;
        }
        catch (ReleaseCandidateCollisionException exception)
        {
            return FailAndCleanup(
                [DiagnosticCatalog.CandidateAlreadyExists(
                    exception.CandidatePath,
                    exception.Message)],
                PipelineExitCode.ReleaseNotReady,
                layout,
                stagingDirectory,
                workDirectory);
        }
        catch (Exception exception)
        {
            return FailAndCleanup(
                [DiagnosticCatalog.UnexpectedFailure(exception)],
                PipelineExitCode.InternalFailure,
                layout,
                stagingDirectory,
                workDirectory);
        }
    }

    private static IReadOnlyList<(
        string AbsolutePath,
        ContentArea Area,
        ContentRole Role)> CreateManifestFileSet(
        IReadOnlyList<FileDigest> contentFiles,
        WorkshopListingAssembly listing)
    {
        var files = contentFiles
            .Select(file => (
                file.Path,
                ContentArea.WorkshopContent,
                ContentRole.Runtime))
            .ToList();
        files.Add((
            listing.DescriptionPath,
            ContentArea.WorkshopListing,
            ContentRole.Description));
        files.Add((
            listing.ChangeNotesPath,
            ContentArea.WorkshopListing,
            ContentRole.ChangeNotes));
        files.Add((
            listing.PreviewPath,
            ContentArea.WorkshopListing,
            ContentRole.Preview));
        return files;
    }

    private static Diagnostic? ValidateBuildResult(
        ReleasePreparationRequest request,
        BuildResult build,
        string expectedWorkDirectory)
    {
        var projectPath = request.Profile.Build?.EntryPoint ??
            request.Profile.ManifestPath;
        if (!HostPathComparer.Equals(
            Path.GetFullPath(build.RunRoot),
            Path.GetFullPath(expectedWorkDirectory)))
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                "The build result names a run root other than the unique owned work directory.");
        }

        if (!string.Equals(
                build.SourceCommit,
                request.InitialProvenance.Commit,
                StringComparison.Ordinal) ||
            !string.Equals(
                build.ReleaseVersion,
                request.Metadata.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                build.DotnetSdkVersion,
                request.Environment.DotnetSdkVersion,
                StringComparison.Ordinal))
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                "The build result does not preserve the requested commit, release version, and exact SDK identity.");
        }

        if (!build.SourceBytesUnchanged)
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                "The build result did not prove that contributing source bytes remained unchanged.");
        }

        if (request.Profile.Build is null)
        {
            return build.PrimaryOutputPath is null &&
                   build.Outputs.Count == 0 &&
                   build.PrimaryAssemblyTargetFrameworkMoniker is null &&
                   build.PrimaryAssemblyTargetFrameworkName is null
                ? null
                : DiagnosticCatalog.BuildFailed(
                    projectPath,
                    "A content-only profile returned unexpected compiled outputs or target-framework metadata.");
        }

        if (build.PrimaryOutputPath is null)
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                "A compiled mod build did not identify its primary output.");
        }

        if (string.IsNullOrWhiteSpace(
                build.PrimaryAssemblyTargetFrameworkMoniker) ||
            string.IsNullOrWhiteSpace(
                build.PrimaryAssemblyTargetFrameworkName))
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                "A compiled mod build did not report target-framework metadata from its exact primary output.");
        }

        TargetFrameworkIdentity targetFrameworkIdentity;
        try
        {
            targetFrameworkIdentity = TargetFrameworkIdentity.ParseFrameworkName(
                build.PrimaryAssemblyTargetFrameworkName);
        }
        catch (InvalidDataException exception)
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                exception.Message);
        }

        if (!string.Equals(
                targetFrameworkIdentity.Moniker,
                build.PrimaryAssemblyTargetFrameworkMoniker,
                StringComparison.Ordinal))
        {
            return DiagnosticCatalog.BuildFailed(
                projectPath,
                "Primary assembly target-framework moniker " +
                $"'{build.PrimaryAssemblyTargetFrameworkMoniker}' does not match " +
                $"framework name '{build.PrimaryAssemblyTargetFrameworkName}'.");
        }

        var primaryPath = Path.GetFullPath(build.PrimaryOutputPath);
        var primaryMatches = build.Outputs.Count(output => HostPathComparer.Equals(
            Path.GetFullPath(output.Path),
            primaryPath));
        return primaryMatches == 1
            ? null
            : DiagnosticCatalog.BuildFailed(
                projectPath,
                $"The primary output appears {primaryMatches} times in the hashed build-output inventory; exactly one is required.");
    }

    private static Diagnostic? ValidateAutomatedTestResults(
        ModProfile profile,
        IReadOnlyList<AutomatedTestResult> results,
        string resultsRoot)
    {
        var profileById = profile.TestProjects.ToDictionary(
            project => project.Id,
            StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            if (!profileById.TryGetValue(result.TestProjectId, out var project))
            {
                return DiagnosticCatalog.AutomatedTestFailed(
                    result.TestProjectId,
                    "The test runner returned evidence for an undeclared project ID.");
            }

            if (!seenIds.Add(result.TestProjectId))
            {
                return DiagnosticCatalog.AutomatedTestFailed(
                    result.TestProjectId,
                    "The test runner returned duplicate evidence for one project ID.");
            }

            var expectedTrxPath = Path.GetFullPath(Path.Combine(
                resultsRoot,
                $"{result.TestProjectId}.trx"));
            if (!HostPathComparer.Equals(
                    Path.GetFullPath(result.TrxPath),
                    expectedTrxPath) ||
                !File.Exists(expectedTrxPath))
            {
                return DiagnosticCatalog.AutomatedTestFailed(
                    result.TestProjectId,
                    $"Exact TRX evidence '{expectedTrxPath}' is missing or was reported at another path.");
            }

            if (result.Passed != (result.ExitCode == 0))
            {
                return DiagnosticCatalog.AutomatedTestFailed(
                    result.TestProjectId,
                    "The reported pass state disagrees with the test process exit code.");
            }

            if (project.Required && !result.Passed)
            {
                return DiagnosticCatalog.AutomatedTestFailed(
                    result.TestProjectId,
                    "A required automated test did not pass.");
            }
        }

        var missingRequired = profile.TestProjects
            .Where(project => project.Required && !seenIds.Contains(project.Id))
            .Select(project => project.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return missingRequired.Length == 0
            ? null
            : DiagnosticCatalog.AutomatedTestFailed(
                missingRequired[0],
                $"Required automated-test evidence is missing for: {string.Join(", ", missingRequired)}.");
    }

    private async Task<IReadOnlyList<ProvenanceFileDigest>> HashLockFilesAsync(
        GitProvenance provenance,
        EvidencePathMapper mapper,
        CancellationToken cancellationToken)
    {
        var lockPaths = provenance.ContributingPaths
            .Where(path => string.Equals(
                Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar)),
                "packages.lock.json",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var digests = new List<ProvenanceFileDigest>(lockPaths.Length);
        foreach (var relativePath in lockPaths)
        {
            var absolutePath = ResolveContributingPath(
                provenance.WorktreeRoot,
                relativePath);
            var digest = await contentHasher.HashFileAsync(
                absolutePath,
                cancellationToken).ConfigureAwait(false);
            digests.Add(mapper.MapDigest(digest));
        }

        return digests
            .OrderBy(digest => digest.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static BuildProvenance CreateBuildProvenance(
        ReleasePreparationRequest request,
        BuildResult build,
        WorkshopListingAssembly listing,
        DateTimeOffset preparedAtUtc,
        ReleaseContentManifest contentManifest,
        FileDigest pipelineExecutableDigest,
        IReadOnlyList<ProvenanceFileDigest> lockFiles,
        FileDigest acceptancePlanDigest,
        EvidencePathMapper mapper)
    {
        var mappedLockFiles = lockFiles
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
        return new BuildProvenance(
            1,
            request.Profile.SchemaVersion,
            GetPipelineInformationalVersion(),
            pipelineExecutableDigest.Sha256,
            request.Metadata.StaticId,
            request.Metadata.Title,
            request.Metadata.Version,
            request.Profile.LocalInstall.DirectoryName,
            request.InitialProvenance.Commit,
            RelevantPathsClean: request.InitialProvenance.IsClean,
            request.InitialProvenance.ContributingPaths
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray(),
            preparedAtUtc,
            request.Environment.OperatingSystem,
            request.Environment.Architecture,
            build.DotnetSdkVersion,
            request.Profile.Build is null
                ? "content-only"
                : build.PrimaryAssemblyTargetFrameworkMoniker!,
            request.Profile.Build is null
                ? null
                : build.PrimaryAssemblyTargetFrameworkName,
            ReleaseConfiguration,
            "${WORKTREE}",
            "${GAME}",
            mapper.MapPath(request.Environment.OniManagedAssemblyDirectory),
            "${ARTIFACTS}",
            request.GameBuildMetadata is null
                ? null
                : mapper.MapArgument(request.GameBuildMetadata),
            mappedLockFiles,
            ComputeLockedDependencyClosureSha256(mappedLockFiles),
            MapDigests(build.GameReferences, mapper),
            new BuildInvocation(
                "dotnet",
                build.StructuredBuildArguments
                    .Select(mapper.MapArgument)
                    .ToArray()),
            MapDigests(build.Inputs, mapper),
            MapDigests(build.MergeInputs, mapper),
            MapDigests(build.Outputs, mapper),
            MapPrimaryOutput(build, mapper),
            build.PrimaryAssemblyVersion,
            build.SourceBytesUnchanged,
            new WorkshopListingProvenance(
                listing.DescriptionReport,
                listing.ChangeNotesReport,
                listing.Preview,
                listing.ModTypeLabels.ToArray(),
                listing.DlcLabels.ToArray()),
            request.Profile.AcceptanceChecks.Count,
            acceptancePlanDigest.Sha256,
            contentManifest.ContentDigest);
    }

    private static string GetPipelineInformationalVersion()
    {
        var assembly = typeof(ReleaseCandidatePreparer).Assembly;
        return assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ??
            assembly.GetName().Version?.ToString() ??
            "unknown";
    }

    private static IReadOnlyList<ProvenanceFileDigest> MapDigests(
        IReadOnlyList<FileDigest> digests,
        EvidencePathMapper mapper) =>
        digests
            .Select(mapper.MapDigest)
            .OrderBy(digest => digest.Path, StringComparer.Ordinal)
            .ToArray();

    private static ProvenanceFileDigest? MapPrimaryOutput(
        BuildResult build,
        EvidencePathMapper mapper)
    {
        if (build.PrimaryOutputPath is null)
        {
            return null;
        }

        var primaryPath = Path.GetFullPath(build.PrimaryOutputPath);
        var primary = build.Outputs.Single(output => HostPathComparer.Equals(
            Path.GetFullPath(output.Path),
            primaryPath));
        return mapper.MapDigest(primary);
    }

    private static string ComputeLockedDependencyClosureSha256(
        IReadOnlyList<ProvenanceFileDigest> lockFiles)
    {
        var canonical = new StringBuilder("oni-locked-dependency-closure-v1\n");
        foreach (var file in lockFiles.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            canonical.Append(file.Path);
            canonical.Append('\0');
            canonical.Append(file.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            canonical.Append('\0');
            canonical.Append(file.Sha256);
            canonical.Append('\n');
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private async Task<IReadOnlyList<EvidenceIndexEntry>> CreateEvidenceIndexAsync(
        string stagingDirectory,
        IEnumerable<string> evidencePaths,
        CancellationToken cancellationToken)
    {
        var entries = new List<EvidenceIndexEntry>();
        foreach (var path in evidencePaths
            .Select(Path.GetFullPath)
            .Distinct(HostPathComparer)
            .OrderBy(path => path, HostPathComparer))
        {
            var digest = await contentHasher.HashFileAsync(path, cancellationToken)
                .ConfigureAwait(false);
            entries.Add(new EvidenceIndexEntry(
                NormalizeCandidateRelativePath(stagingDirectory, path),
                digest.ByteLength,
                digest.Sha256));
        }

        return entries
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<Diagnostic?> VerifyPreparedStagingAsync(
        CandidateLayout layout,
        string stagingDirectory,
        IReadOnlyList<(string AbsolutePath, ContentArea Area, ContentRole Role)> manifestFiles,
        ReleaseContentManifest expectedManifest,
        IReadOnlyList<EvidenceIndexEntry> evidenceIndex,
        IReadOnlyList<AutomatedTestResult> automatedTests,
        CancellationToken cancellationToken)
    {
        var actualManifest = await contentHasher.CreateManifestAsync(
            stagingDirectory,
            manifestFiles,
            cancellationToken).ConfigureAwait(false);
        if (!ManifestsEqual(expectedManifest, actualManifest))
        {
            return DiagnosticCatalog.CandidateManifestMismatch(
                "Release content changed after its canonical manifest was written.");
        }

        foreach (var entry in evidenceIndex)
        {
            var path = ResolveCandidateRelativePath(stagingDirectory, entry.Path);
            var digest = await contentHasher.HashFileAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (digest.ByteLength != entry.ByteLength ||
                !string.Equals(digest.Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                return DiagnosticCatalog.CandidateManifestMismatch(
                    $"Prepared evidence '{entry.Path}' changed before promotion.");
            }
        }

        var readinessPath = layout.GetStagingPath(
            stagingDirectory,
            layout.ReleaseReadinessReportPath);
        _ = await contentHasher.HashFileAsync(readinessPath, cancellationToken)
            .ConfigureAwait(false);
        return ValidateExactPreparedTree(
            layout,
            stagingDirectory,
            expectedManifest,
            automatedTests);
    }

    private static Diagnostic? ValidateExactPreparedTree(
        CandidateLayout layout,
        string stagingDirectory,
        ReleaseContentManifest manifest,
        IReadOnlyList<AutomatedTestResult> automatedTests)
    {
        var expectedFiles = new HashSet<string>(HostPathComparer);
        foreach (var entry in manifest.Entries)
        {
            var areaRoot = entry.ContentArea switch
            {
                ContentArea.WorkshopContent => layout.WorkshopContentDirectory,
                ContentArea.WorkshopListing => layout.WorkshopListingDirectory,
                _ => throw new InvalidDataException(
                    "The prepared manifest contains an unknown content area.")
            };
            expectedFiles.Add(layout.GetStagingPath(
                stagingDirectory,
                Path.Combine(
                    areaRoot,
                    entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        foreach (var finalEvidencePath in new[]
        {
            layout.ReleaseReadinessReportPath,
            layout.ReleaseContentManifestPath,
            layout.BuildProvenancePath,
            layout.AcceptanceTestPlanPath,
            layout.ReleaseSummaryPath,
            layout.UploaderChecklistPath
        })
        {
            expectedFiles.Add(layout.GetStagingPath(
                stagingDirectory,
                finalEvidencePath));
        }

        foreach (var test in automatedTests)
        {
            expectedFiles.Add(Path.GetFullPath(test.TrxPath));
        }

        var actualFiles = new HashSet<string>(HostPathComparer);
        var actualDirectories = new HashSet<string>(HostPathComparer)
        {
            Path.GetFullPath(stagingDirectory)
        };
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(stagingDirectory));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
                    ? new DirectoryInfo(entry)
                    : new FileInfo(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    info.LinkTarget is not null)
                {
                    return DiagnosticCatalog.CandidateManifestMismatch(
                        $"Prepared candidate contains linked entry '{entry}'.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    var resolved = Path.GetFullPath(entry);
                    actualDirectories.Add(resolved);
                    pending.Push(resolved);
                }
                else
                {
                    actualFiles.Add(Path.GetFullPath(entry));
                }
            }
        }

        if (!actualFiles.SetEquals(expectedFiles))
        {
            return DiagnosticCatalog.CandidateManifestMismatch(
                "Prepared candidate file inventory is not the exact declared content and evidence contract.");
        }

        var expectedDirectories = new HashSet<string>(HostPathComparer)
        {
            Path.GetFullPath(stagingDirectory),
            layout.GetStagingPath(stagingDirectory, layout.WorkshopContentDirectory),
            layout.GetStagingPath(stagingDirectory, layout.WorkshopListingDirectory),
            layout.GetStagingPath(stagingDirectory, layout.ReleaseEvidenceDirectory),
            layout.GetStagingPath(stagingDirectory, layout.AutomatedTestResultsDirectory)
        };
        foreach (var file in expectedFiles)
        {
            var current = Path.GetDirectoryName(file);
            while (current is not null &&
                !HostPathComparer.Equals(current, stagingDirectory))
            {
                expectedDirectories.Add(Path.GetFullPath(current));
                current = Path.GetDirectoryName(current);
            }
        }

        return actualDirectories.SetEquals(expectedDirectories)
            ? null
            : DiagnosticCatalog.CandidateManifestMismatch(
                "Prepared candidate contains an undeclared directory.");
    }

    private static bool ManifestsEqual(
        ReleaseContentManifest expected,
        ReleaseContentManifest actual) =>
        expected.SchemaVersion == actual.SchemaVersion &&
        string.Equals(
            expected.ContentDigest,
            actual.ContentDigest,
            StringComparison.Ordinal) &&
        expected.Entries.SequenceEqual(actual.Entries);

    private static Diagnostic? ValidateFinalSourceState(
        GitProvenance initial,
        GitProvenance final)
    {
        if (!final.IsClean)
        {
            return DiagnosticCatalog.DirtyReleaseInput(
                $"Contributing source paths became dirty during preparation: {string.Join(", ", final.DirtyPaths.Select(path => $"'{path}'"))}.");
        }

        if (!string.Equals(initial.Commit, final.Commit, StringComparison.Ordinal))
        {
            return DiagnosticCatalog.DirtyReleaseInput(
                $"Repository commit changed during preparation from '{initial.Commit}' to '{final.Commit}'.");
        }

        if (!initial.ContributingPaths.SequenceEqual(
            final.ContributingPaths,
            StringComparer.Ordinal))
        {
            return DiagnosticCatalog.DirtyReleaseInput(
                "The contributing source-set identity changed during preparation.");
        }

        return null;
    }

    private static string ResolveContributingPath(
        string worktreeRoot,
        string relativePath)
    {
        var root = Path.GetFullPath(worktreeRoot);
        var resolved = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, resolved);
        if (!IsStrictDescendant(relative))
        {
            throw new InvalidDataException(
                $"Contributing path '{relativePath}' escapes worktree '{root}'.");
        }

        return resolved;
    }

    private static string NormalizeCandidateRelativePath(
        string stagingDirectory,
        string absolutePath)
    {
        var root = Path.GetFullPath(stagingDirectory);
        var path = Path.GetFullPath(absolutePath);
        var relative = Path.GetRelativePath(root, path);
        if (!IsStrictDescendant(relative))
        {
            throw new InvalidDataException(
                $"Prepared evidence path '{path}' escapes staging directory '{root}'.");
        }

        return relative.Replace((char)92, '/');
    }

    private static string ResolveCandidateRelativePath(
        string stagingDirectory,
        string relativePath)
    {
        var root = Path.GetFullPath(stagingDirectory);
        var path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, path);
        if (!IsStrictDescendant(relative))
        {
            throw new InvalidDataException(
                $"Evidence index path '{relativePath}' escapes staging directory '{root}'.");
        }

        return path;
    }

    private static bool IsStrictDescendant(string relativePath) =>
        relativePath != "." &&
        !Path.IsPathRooted(relativePath) &&
        relativePath != ".." &&
        !relativePath.StartsWith(
            $"..{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal) &&
        !relativePath.StartsWith(
            $"..{Path.AltDirectorySeparatorChar}",
            StringComparison.Ordinal);

    private OperationResult<PreparedReleaseCandidate> FailAndCleanup(
        IReadOnlyList<Diagnostic> primaryDiagnostics,
        PipelineExitCode exitCode,
        CandidateLayout layout,
        string stagingDirectory,
        string workDirectory)
    {
        var diagnostics = primaryDiagnostics.ToList();
        diagnostics.AddRange(CleanupTransientDirectories(
            layout,
            workDirectory,
            stagingDirectory));
        return new OperationResult<PreparedReleaseCandidate>(
            null,
            diagnostics,
            exitCode);
    }

    private IReadOnlyList<Diagnostic> CleanupTransientDirectories(
        CandidateLayout layout,
        params string[] paths)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var path in paths)
        {
            try
            {
                fileSystem.DeleteTransientDirectory(layout, path);
            }
            catch (Exception exception)
            {
                diagnostics.Add(DiagnosticCatalog.CleanupFailed(
                    path,
                    exception.Message));
            }
        }

        return diagnostics;
    }

    private static OperationResult<PreparedReleaseCandidate> Failure(
        Diagnostic diagnostic,
        PipelineExitCode exitCode) =>
        new(null, [diagnostic], exitCode);

    private sealed class ReleaseModBuilderAdapter(ModBuilder builder) :
        IReleaseModBuilder
    {
        public Task<OperationResult<BuildResult>> BuildAsync(
            BuildRequest request,
            CancellationToken cancellationToken) =>
            builder.BuildAsync(request, cancellationToken);
    }

    private sealed class ReleaseAutomatedTestRunnerAdapter(
        IExternalProcessRunner processRunner) : IReleaseAutomatedTestRunner
    {
        public Task<OperationResult<IReadOnlyList<AutomatedTestResult>>> RunAsync(
            ModProfile profile,
            PipelineEnvironment environment,
            string worktreeRoot,
            string resultsRoot,
            CancellationToken cancellationToken) =>
            new AutomatedTestRunner(
                processRunner,
                environment.OniManagedAssemblyDirectory,
                worktreeRoot)
                .RunAsync(profile, resultsRoot, cancellationToken);
    }

    private sealed class ReleaseWorkshopContentAssemblerAdapter(
        WorkshopContentAssembler assembler) : IReleaseWorkshopContentAssembler
    {
        public Task<OperationResult<IReadOnlyList<FileDigest>>> AssembleAsync(
            ModProfile profile,
            BuildResult buildResult,
            string targetDirectory,
            CancellationToken cancellationToken) =>
            assembler.AssembleAsync(
                profile,
                buildResult,
                targetDirectory,
                cancellationToken);
    }

    private sealed class ReleaseWorkshopListingAssemblerAdapter(
        WorkshopListingAssembler assembler) : IReleaseWorkshopListingAssembler
    {
        public Task<OperationResult<WorkshopListingAssembly>> AssembleAsync(
            ModProfile profile,
            string targetDirectory,
            CancellationToken cancellationToken) =>
            assembler.AssembleAsync(profile, targetDirectory, cancellationToken);
    }

    private sealed class ReleaseContentHasherAdapter(ContentHasher hasher) :
        IReleaseContentHasher
    {
        public Task<FileDigest> HashFileAsync(
            string absolutePath,
            CancellationToken cancellationToken) =>
            hasher.HashFileAsync(absolutePath, cancellationToken);

        public Task<ReleaseContentManifest> CreateManifestAsync(
            string releaseContentRoot,
            IReadOnlyList<(
                string AbsolutePath,
                ContentArea Area,
                ContentRole Role)> files,
            CancellationToken cancellationToken) =>
            hasher.CreateManifestAsync(
                releaseContentRoot,
                files,
                cancellationToken);
    }

    private sealed class ReleaseArtifactWriterAdapter(Utf8ArtifactWriter writer) :
        IReleaseArtifactWriter
    {
        public Task WriteJsonAsync<T>(
            string destinationPath,
            T value,
            CancellationToken cancellationToken) =>
            writer.WriteJsonAtomicallyAsync(
                destinationPath,
                value,
                cancellationToken);

        public Task WriteLfTextAsync(
            string destinationPath,
            string text,
            CancellationToken cancellationToken) =>
            writer.WriteLfTextAtomicallyAsync(
                destinationPath,
                text,
                cancellationToken);
    }

    private sealed class ReleaseSourceInspectorAdapter(
        GitRepositoryInspector inspector) : IReleaseSourceInspector
    {
        public Task<OperationResult<GitProvenance>> InspectAsync(
            ModProfile profile,
            string pipelineExecutablePath,
            CancellationToken cancellationToken) =>
            inspector.InspectAsync(
                profile,
                pipelineExecutablePath,
                cancellationToken);
    }
}
