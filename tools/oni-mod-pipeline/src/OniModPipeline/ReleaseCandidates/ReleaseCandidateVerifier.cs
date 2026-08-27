using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal interface IReleaseCandidateVerifier
{
    Task<OperationResult<ReleaseReadinessReport>> VerifyAsync(
        string candidateDirectory,
        CancellationToken cancellationToken);
}

internal sealed partial class ReleaseCandidateVerifier : IReleaseCandidateVerifier
{
    private const int MaximumEvidenceBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly StringComparer HostPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly ContentHasher contentHasher;
    private readonly ListingTextRenderer listingTextRenderer;
    private readonly PreviewImageInspector previewImageInspector;

    internal ReleaseCandidateVerifier(
        ContentHasher contentHasher,
        ListingTextRenderer listingTextRenderer,
        PreviewImageInspector previewImageInspector)
    {
        ArgumentNullException.ThrowIfNull(contentHasher);
        ArgumentNullException.ThrowIfNull(listingTextRenderer);
        ArgumentNullException.ThrowIfNull(previewImageInspector);
        this.contentHasher = contentHasher;
        this.listingTextRenderer = listingTextRenderer;
        this.previewImageInspector = previewImageInspector;
    }

    internal static ReleaseCandidateVerifier CreateDefault() =>
        new(
            new ContentHasher(),
            new ListingTextRenderer(),
            new PreviewImageInspector());

    public async Task<OperationResult<ReleaseReadinessReport>> VerifyAsync(
        string candidateDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        CandidateLayout layout;
        ReleaseReadinessReport prior;
        try
        {
            layout = CandidateLayout.FromCandidateDirectory(candidateDirectory);
            EnsureRegularDirectory(layout.CandidateDirectory, "release candidate");
            EnsureRegularDirectory(layout.ReleaseEvidenceDirectory, "release evidence");
            prior = ReadJsonFile<ReleaseReadinessReport>(
                layout.ReleaseReadinessReportPath);
            ValidatePriorReadinessIdentity(layout, prior);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return new OperationResult<ReleaseReadinessReport>(
                null,
                [DiagnosticCatalog.ReleaseNotReady(
                    $"Candidate readiness evidence could not be loaded: {exception.Message}")],
                PipelineExitCode.ReleaseNotReady);
        }

        if (!string.IsNullOrWhiteSpace(prior.IrreversibleInvalidation))
        {
            var invalidated = prior with
            {
                State = ReleaseCandidateState.VerificationFailed,
                BlockingConditions =
                [
                    new ReleaseBlockingCondition(
                        "irreversible-invalidation",
                        prior.IrreversibleInvalidation)
                ]
            };
            return new OperationResult<ReleaseReadinessReport>(
                invalidated,
                [DiagnosticCatalog.ReleaseNotReady(
                    $"Candidate run ID is irreversibly invalidated: {prior.IrreversibleInvalidation}")],
                PipelineExitCode.ReleaseNotReady);
        }

        VerifiedCandidate verified;
        try
        {
            verified = await InspectCandidateAsync(
                layout,
                prior,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VerificationException exception)
        {
            return await PersistVerificationFailureAsync(
                layout,
                prior,
                exception.Diagnostic,
                exception.Irreversible,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return await PersistVerificationFailureAsync(
                layout,
                prior,
                DiagnosticCatalog.ReleaseNotReady(
                    $"Candidate verification failed closed: {exception.Message}"),
                irreversible: true,
                cancellationToken).ConfigureAwait(false);
        }

        var derivation = DeriveState(verified);
        var context = CreateDocumentContext(verified, derivation.State);
        var summaryBytes = ToLfUtf8(ReleaseSummaryRenderer.Render(context));
        var checklistBytes = ToLfUtf8(UploaderChecklistRenderer.Render(context));
        IReadOnlyList<EvidenceIndexEntry> evidenceIndex;
        try
        {
            evidenceIndex = await CreateFinalEvidenceIndexAsync(
                verified,
                summaryBytes,
                checklistBytes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return await PersistVerificationFailureAsync(
                layout,
                prior,
                DiagnosticCatalog.ReleaseNotReady(
                    $"Final evidence index could not be created: {exception.Message}"),
                irreversible: true,
                cancellationToken).ConfigureAwait(false);
        }

        var report = new ReleaseReadinessReport(
            1,
            verified.Provenance.StaticId,
            verified.Provenance.Version,
            verified.Manifest.ContentDigest,
            verified.Provenance.PreparedAtUtc,
            derivation.State,
            BuildSucceeded: true,
            AutomatedTestsPassed: verified.AutomatedTests
                .Where(test => test.Evidence.Required)
                .All(test => test.Passed),
            PreparedContentVerified: true,
            RelevantSourcesClean: true,
            verified.AutomatedTests
                .Select(test => test.Evidence with { Passed = test.Passed })
                .OrderBy(test => test.Id, StringComparer.Ordinal)
                .ToArray(),
            evidenceIndex,
            derivation.Blockers,
            IrreversibleInvalidation: null,
            verified.Receipt?.InstalledAtUtc,
            verified.AcceptanceResults?.RecordedAtUtc,
            verified.AcceptanceResults?.Tester,
            derivation.RequiredAcceptancePassed);
        var readinessBytes = SerializeJson(report);
        try
        {
            await WriteDerivedEvidenceAsync(
                layout,
                summaryBytes,
                checklistBytes,
                readinessBytes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return new OperationResult<ReleaseReadinessReport>(
                report with
                {
                    State = ReleaseCandidateState.VerificationFailed,
                    BlockingConditions =
                    [
                        new ReleaseBlockingCondition(
                            DiagnosticIds.ReleaseNotReady,
                            "Derived release evidence could not be replaced atomically.")
                    ],
                    IrreversibleInvalidation =
                        $"{DiagnosticIds.ReleaseNotReady}: derived evidence write failed: {exception.Message}"
                },
                [DiagnosticCatalog.ReleaseNotReady(
                    $"Derived release evidence was not fully replaced: {exception.Message}")],
                PipelineExitCode.ReleaseNotReady);
        }

        if (derivation.State == ReleaseCandidateState.ReadyForUpload)
        {
            return new OperationResult<ReleaseReadinessReport>(
                report,
                [],
                PipelineExitCode.Success);
        }

        var diagnostic = derivation.State == ReleaseCandidateState.AcceptanceFailed
            ? DiagnosticCatalog.RequiredAcceptanceMissing(
                string.Join(
                    " ",
                    derivation.Blockers.Select(blocker => blocker.Summary)))
            : DiagnosticCatalog.ReleaseNotReady(
                string.Join(
                    " ",
                    derivation.Blockers.Select(blocker => blocker.Summary)));
        return new OperationResult<ReleaseReadinessReport>(
            report,
            [diagnostic],
            PipelineExitCode.ReleaseNotReady);
    }

    private async Task<VerifiedCandidate> InspectCandidateAsync(
        CandidateLayout layout,
        ReleaseReadinessReport prior,
        CancellationToken cancellationToken)
    {
        ValidateCandidateInventory(layout, prior);
        var manifest = ReadJsonFile<ReleaseContentManifest>(
            layout.ReleaseContentManifestPath);
        var provenance = ReadJsonFile<BuildProvenance>(layout.BuildProvenancePath);
        var plan = ReadJsonFile<AcceptanceTestPlan>(layout.AcceptanceTestPlanPath);
        ValidateCoreIdentity(layout, manifest, provenance, plan);
        ValidateBuildProvenance(provenance);

        var planDigest = await contentHasher.HashFileAsync(
            layout.AcceptanceTestPlanPath,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
            planDigest.Sha256,
            provenance.AcceptanceTestPlanSha256,
            StringComparison.Ordinal))
        {
            throw AcceptanceMismatch(
                layout.AcceptanceTestPlanPath,
                "The immutable acceptance-plan SHA-256 differs from build provenance.");
        }

        var descriptionRepresentation = InspectListingText(
            layout.DescriptionPath,
            provenance.WorkshopListing.Description);
        var changeNotesRepresentation = InspectListingText(
            layout.ChangeNotesPath,
            provenance.WorkshopListing.ChangeNotes);
        var actualManifest = await contentHasher.CreateManifestAsync(
            layout.CandidateDirectory,
            EnumerateCandidateContent(layout),
            cancellationToken).ConfigureAwait(false);
        if (!ManifestsEqual(manifest, actualManifest))
        {
            var representationIssue = descriptionRepresentation.NonCanonicalWithSameLogicalContent
                ? (layout.DescriptionPath, descriptionRepresentation.Reason)
                : changeNotesRepresentation.NonCanonicalWithSameLogicalContent
                    ? (layout.ChangeNotesPath, changeNotesRepresentation.Reason)
                    : default;
            if (representationIssue.Item1 is not null)
            {
                throw UploaderMismatch(
                    representationIssue.Item1,
                    representationIssue.Reason!);
            }

            throw ManifestMismatch(
                "Current Workshop content or listing inventory, lengths, hashes, roles, or canonical digest differ from release-content-manifest.json.");
        }

        ValidateExactListingRepresentation(
            layout.DescriptionPath,
            descriptionRepresentation,
            provenance.WorkshopListing.Description);
        ValidateExactListingRepresentation(
            layout.ChangeNotesPath,
            changeNotesRepresentation,
            provenance.WorkshopListing.ChangeNotes);
        var previewPath = Path.Combine(
            layout.WorkshopListingDirectory,
            $"preview{provenance.WorkshopListing.Preview.CandidateExtension}");
        var previewResult = previewImageInspector.Inspect(previewPath);
        if (!previewResult.IsSuccess ||
            previewResult.Value != provenance.WorkshopListing.Preview)
        {
            var reason = previewResult.IsSuccess
                ? "Preview format or byte length differs from build provenance."
                : string.Join(
                    " ",
                    previewResult.Diagnostics.Select(diagnostic => diagnostic.Evidence));
            throw UploaderMismatch(previewPath, reason);
        }

        var automatedTests = new List<VerifiedAutomatedTest>();
        foreach (var test in prior.AutomatedTests
            .OrderBy(test => test.Id, StringComparer.Ordinal))
        {
            var trxPath = ResolveCandidateRelativePath(
                layout,
                test.TrxPath,
                "automated test evidence");
            EnsureStrictDescendant(
                layout.AutomatedTestResultsDirectory,
                trxPath,
                "automated-test-results directory");
            bool passed;
            try
            {
                passed = ParsePassedTrx(trxPath);
            }
            catch (Exception exception) when (IsExpectedEvidenceException(exception))
            {
                throw AutomatedTestMismatch(test.Id, exception.Message);
            }

            if (test.Required && !passed)
            {
                throw AutomatedTestMismatch(
                    test.Id,
                    "TRX outcome or counters do not prove a completed passing required run.");
            }

            automatedTests.Add(new VerifiedAutomatedTest(test, trxPath, passed));
        }

        InstallationReceipt? receipt = null;
        AcceptanceTestResults? acceptanceResults = null;
        var receiptExists = File.Exists(layout.InstallationReceiptPath);
        var resultsExist = File.Exists(layout.AcceptanceTestResultsPath);
        if (Directory.Exists(layout.InstallationReceiptPath) ||
            Directory.Exists(layout.AcceptanceTestResultsPath))
        {
            throw AcceptanceMismatch(
                layout.ReleaseEvidenceDirectory,
                "Installation receipt and acceptance results must be regular files when present.");
        }

        if (resultsExist && !receiptExists)
        {
            throw AcceptanceMismatch(
                layout.AcceptanceTestResultsPath,
                "Acceptance results exist without the installation receipt required to bind live bytes.");
        }

        if (receiptExists)
        {
            receipt = ReadJsonFile<InstallationReceipt>(layout.InstallationReceiptPath);
            await ValidateReceiptAndInstalledBytesAsync(
                layout,
                manifest,
                provenance,
                receipt,
                cancellationToken).ConfigureAwait(false);
        }

        if (resultsExist)
        {
            acceptanceResults = ReadJsonFile<AcceptanceTestResults>(
                layout.AcceptanceTestResultsPath);
            ValidateAcceptanceResults(
                layout,
                manifest,
                plan,
                provenance,
                acceptanceResults);
        }

        await ValidatePriorEvidenceIndexAsync(
            layout,
            prior,
            cancellationToken).ConfigureAwait(false);
        var listingFiles = actualManifest.Entries
            .Where(entry => entry.ContentArea == ContentArea.WorkshopListing)
            .Select(entry => new FileDigest(
                ResolveAreaPath(
                    layout.WorkshopListingDirectory,
                    entry.RelativePath),
                entry.ByteLength,
                entry.Sha256))
            .ToArray();
        var listing = new WorkshopListingAssembly(
            layout.DescriptionPath,
            layout.ChangeNotesPath,
            previewPath,
            provenance.WorkshopListing.Description,
            provenance.WorkshopListing.ChangeNotes,
            provenance.WorkshopListing.Preview,
            listingFiles,
            provenance.WorkshopListing.ModTypeLabels,
            provenance.WorkshopListing.DlcLabels);
        return new VerifiedCandidate(
            layout,
            manifest,
            provenance,
            plan,
            receipt,
            acceptanceResults,
            listing,
            automatedTests);
    }

    private async Task ValidatePriorEvidenceIndexAsync(
        CandidateLayout layout,
        ReleaseReadinessReport prior,
        CancellationToken cancellationToken)
    {
        var entries = prior.EvidenceIndex.ToArray();
        if (!entries.Select(entry => entry.Path).SequenceEqual(
                entries.Select(entry => entry.Path).OrderBy(path => path, StringComparer.Ordinal)) ||
            entries.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Count() !=
                entries.Length)
        {
            throw ReleaseEvidenceMismatch(
                "The prior readiness evidence index is unsorted or contains duplicate paths.");
        }

        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            CandidateRelativePath(layout, layout.ReleaseContentManifestPath),
            CandidateRelativePath(layout, layout.BuildProvenancePath),
            CandidateRelativePath(layout, layout.AcceptanceTestPlanPath),
            CandidateRelativePath(layout, layout.ReleaseSummaryPath),
            CandidateRelativePath(layout, layout.UploaderChecklistPath)
        };
        foreach (var test in prior.AutomatedTests)
        {
            required.Add(test.TrxPath);
        }

        var allowed = new HashSet<string>(required, StringComparer.Ordinal)
        {
            CandidateRelativePath(layout, layout.InstallationReceiptPath),
            CandidateRelativePath(layout, layout.AcceptanceTestResultsPath)
        };
        foreach (var entry in entries)
        {
            if (!allowed.Contains(entry.Path))
            {
                throw ReleaseEvidenceMismatch(
                    $"Readiness evidence index contains undeclared path '{entry.Path}'.");
            }

            var path = ResolveCandidateRelativePath(
                layout,
                entry.Path,
                "readiness evidence index");
            FileDigest actual;
            try
            {
                actual = await contentHasher.HashFileAsync(path, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IsExpectedEvidenceException(exception))
            {
                throw ClassifyIndexedMismatch(layout, prior, entry.Path, exception.Message);
            }

            if (actual.ByteLength != entry.ByteLength ||
                !string.Equals(actual.Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                throw ClassifyIndexedMismatch(
                    layout,
                    prior,
                    entry.Path,
                    "Recorded byte length or SHA-256 no longer matches.");
            }
        }

        var indexed = entries.Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);
        var missing = required.Where(path => !indexed.Contains(path)).ToArray();
        if (missing.Length > 0)
        {
            throw ReleaseEvidenceMismatch(
                $"Readiness evidence index is missing required paths: {string.Join(", ", missing.Select(path => $"'{path}'"))}.");
        }
    }

    private static VerificationException ClassifyIndexedMismatch(
        CandidateLayout layout,
        ReleaseReadinessReport prior,
        string relativePath,
        string reason)
    {
        if (string.Equals(
            relativePath,
            CandidateRelativePath(layout, layout.BuildProvenancePath),
            StringComparison.Ordinal))
        {
            return new VerificationException(
                DiagnosticCatalog.DirtyReleaseInput(
                    $"Immutable build provenance changed: {reason}"),
                irreversible: true);
        }

        if (string.Equals(
                relativePath,
                CandidateRelativePath(layout, layout.AcceptanceTestPlanPath),
                StringComparison.Ordinal) ||
            string.Equals(
                relativePath,
                CandidateRelativePath(layout, layout.InstallationReceiptPath),
                StringComparison.Ordinal) ||
            string.Equals(
                relativePath,
                CandidateRelativePath(layout, layout.AcceptanceTestResultsPath),
                StringComparison.Ordinal))
        {
            return AcceptanceMismatch(relativePath, reason);
        }

        var test = prior.AutomatedTests.FirstOrDefault(candidate =>
            string.Equals(candidate.TrxPath, relativePath, StringComparison.Ordinal));
        if (test is not null)
        {
            return AutomatedTestMismatch(test.Id, reason);
        }

        if (string.Equals(
            relativePath,
            CandidateRelativePath(layout, layout.ReleaseContentManifestPath),
            StringComparison.Ordinal))
        {
            return ManifestMismatch(
                $"Immutable release-content manifest changed: {reason}");
        }

        return ReleaseEvidenceMismatch(
            $"Derived or indexed evidence '{relativePath}' changed: {reason}");
    }

    private async Task ValidateReceiptAndInstalledBytesAsync(
        CandidateLayout layout,
        ReleaseContentManifest manifest,
        BuildProvenance provenance,
        InstallationReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (receipt.SchemaVersion != 1 ||
            !receipt.InstalledFilesVerified ||
            receipt.InstalledAtUtc.Offset != TimeSpan.Zero ||
            !string.Equals(receipt.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(receipt.Version, provenance.Version, StringComparison.Ordinal) ||
            !string.Equals(
                receipt.ContentDigest,
                manifest.ContentDigest,
                StringComparison.Ordinal))
        {
            throw AcceptanceMismatch(
                layout.InstallationReceiptPath,
                "Receipt schema, identity, UTC time, digest, or verified-files flag is invalid.");
        }

        ValidateReceiptTarget(receipt, provenance);
        var markerPath = Path.Combine(
            receipt.AbsoluteTargetPath,
            ModInstaller.OwnershipMarkerFileName);
        var marker = ReadJsonFile<OwnershipMarker>(markerPath);
        if (marker.SchemaVersion != 1 ||
            !string.Equals(marker.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(
                marker.ManagedDirectoryName,
                provenance.ManagedDirectoryName,
                StringComparison.Ordinal) ||
            !string.Equals(
                marker.InstalledContentDigest,
                manifest.ContentDigest,
                StringComparison.Ordinal))
        {
            throw AcceptanceMismatch(
                markerPath,
                "Live ownership marker does not match the candidate identity.");
        }

        var runtimeEntries = manifest.Entries
            .Where(entry => entry.ContentArea == ContentArea.WorkshopContent)
            .ToArray();
        var actualFiles = EnumerateRegularFiles(receipt.AbsoluteTargetPath);
        var expectedFiles = runtimeEntries
            .Select(entry => ResolveAreaPath(
                receipt.AbsoluteTargetPath,
                entry.RelativePath))
            .Append(markerPath)
            .Select(Path.GetFullPath)
            .ToHashSet(HostPathComparer);
        if (!expectedFiles.SetEquals(actualFiles))
        {
            throw AcceptanceMismatch(
                receipt.AbsoluteTargetPath,
                "Live installation contains missing or undeclared files.");
        }

        foreach (var expected in runtimeEntries)
        {
            var path = ResolveAreaPath(
                receipt.AbsoluteTargetPath,
                expected.RelativePath);
            var actual = await contentHasher.HashFileAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (actual.ByteLength != expected.ByteLength ||
                !string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                throw AcceptanceMismatch(
                    path,
                    "Live installed byte length or SHA-256 differs from Workshop content.");
            }
        }
    }

    private static void ValidateAcceptanceResults(
        CandidateLayout layout,
        ReleaseContentManifest manifest,
        AcceptanceTestPlan plan,
        BuildProvenance provenance,
        AcceptanceTestResults results)
    {
        if (results.SchemaVersion != 1 ||
            results.RecordedAtUtc.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(results.Tester) ||
            !string.Equals(results.Tester, results.Tester.Trim(), StringComparison.Ordinal) ||
            results.Tester.Any(char.IsControl) ||
            !string.Equals(
                results.ContentDigest,
                manifest.ContentDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                results.AcceptancePlanSha256,
                provenance.AcceptanceTestPlanSha256,
                StringComparison.Ordinal) ||
            results.Checks.Count != plan.Checks.Count)
        {
            throw AcceptanceMismatch(
                layout.AcceptanceTestResultsPath,
                "Results schema, tester, UTC time, digest, plan hash, or check count is invalid.");
        }

        for (var index = 0; index < plan.Checks.Count; index++)
        {
            var expected = plan.Checks[index];
            var actual = results.Checks[index];
            if (!string.Equals(actual.Id, expected.Id, StringComparison.Ordinal) ||
                !string.Equals(actual.Title, expected.Title, StringComparison.Ordinal) ||
                !string.Equals(actual.Setup, expected.Setup, StringComparison.Ordinal) ||
                !string.Equals(actual.Action, expected.Action, StringComparison.Ordinal) ||
                !string.Equals(actual.Expected, expected.Expected, StringComparison.Ordinal) ||
                actual.Note is not null &&
                !string.Equals(actual.Note, actual.Note.Trim(), StringComparison.Ordinal))
            {
                throw AcceptanceMismatch(
                    layout.AcceptanceTestResultsPath,
                    $"Recorded check at index {index.ToString(CultureInfo.InvariantCulture)} does not exactly copy the immutable plan.");
            }
        }
    }

    private static StateDerivation DeriveState(VerifiedCandidate candidate)
    {
        var blockers = new List<ReleaseBlockingCondition>();
        if (candidate.Receipt is null)
        {
            blockers.Add(new ReleaseBlockingCondition(
                "installation-receipt-missing",
                "This exact candidate has not been installed and verified for acceptance testing."));
        }

        if (candidate.AcceptanceResults is null)
        {
            blockers.Add(new ReleaseBlockingCondition(
                "acceptance-test-results-missing",
                "Human acceptance results have not been recorded for this content digest."));
        }

        if (blockers.Count > 0)
        {
            return new StateDerivation(
                ReleaseCandidateState.AwaitingAcceptance,
                blockers,
                RequiredAcceptancePassed: null);
        }

        var resultsById = candidate.AcceptanceResults!.Checks.ToDictionary(
            result => result.Id,
            StringComparer.Ordinal);
        var failedRequired = candidate.Plan.Checks
            .Where(check => check.Required &&
                resultsById[check.Id].Outcome != AcceptanceOutcome.Passed)
            .ToArray();
        if (failedRequired.Length > 0)
        {
            return new StateDerivation(
                ReleaseCandidateState.AcceptanceFailed,
                failedRequired.Select(check => new ReleaseBlockingCondition(
                    check.Id,
                    $"Required acceptance check '{check.Title}' failed."))
                    .ToArray(),
                RequiredAcceptancePassed: false);
        }

        return new StateDerivation(
            ReleaseCandidateState.ReadyForUpload,
            [],
            RequiredAcceptancePassed: true);
    }

    private static ReleaseDocumentContext CreateDocumentContext(
        VerifiedCandidate candidate,
        ReleaseCandidateState state)
    {
        var metadata = new OniMetadata(
            candidate.Provenance.StaticId,
            candidate.Provenance.Title,
            Description: string.Empty,
            SupportedContent: string.Empty,
            MinimumSupportedBuild: 0,
            Version: candidate.Provenance.Version,
            ApiVersion: 0);
        var tests = candidate.AutomatedTests.Select(test => new AutomatedTestResult(
            test.Evidence.Id,
            test.Evidence.ProjectPath,
            test.TrxPath,
            test.Evidence.ExitCode,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            test.Passed)).ToArray();
        var warnings = candidate.AcceptanceResults is null
            ? Array.Empty<string>()
            : candidate.Plan.Checks
                .Where(check => !check.Required)
                .Join(
                    candidate.AcceptanceResults.Checks,
                    check => check.Id,
                    result => result.Id,
                    (check, result) => (check, result),
                    StringComparer.Ordinal)
                .Where(pair => pair.result.Outcome == AcceptanceOutcome.Failed)
                .Select(pair =>
                    $"Optional acceptance check '{pair.check.Title}' failed: {pair.result.Note ?? "no note"}.")
                .ToArray();
        return new ReleaseDocumentContext(
            metadata,
            candidate.Layout,
            candidate.Manifest,
            candidate.Provenance,
            candidate.Listing,
            tests,
            state,
            warnings,
            candidate.Plan,
            candidate.AcceptanceResults,
            candidate.AutomatedTests.ToDictionary(
                test => test.Evidence.Id,
                test => test.Evidence.Required,
                StringComparer.Ordinal));
    }

    private async Task<IReadOnlyList<EvidenceIndexEntry>> CreateFinalEvidenceIndexAsync(
        VerifiedCandidate candidate,
        byte[] summaryBytes,
        byte[] checklistBytes,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>
        {
            candidate.Layout.ReleaseContentManifestPath,
            candidate.Layout.BuildProvenancePath,
            candidate.Layout.AcceptanceTestPlanPath
        };
        paths.AddRange(candidate.AutomatedTests.Select(test => test.TrxPath));
        if (candidate.Receipt is not null)
        {
            paths.Add(candidate.Layout.InstallationReceiptPath);
        }

        if (candidate.AcceptanceResults is not null)
        {
            paths.Add(candidate.Layout.AcceptanceTestResultsPath);
        }

        var entries = new List<EvidenceIndexEntry>();
        foreach (var path in paths
            .Select(Path.GetFullPath)
            .Distinct(HostPathComparer)
            .OrderBy(path => path, HostPathComparer))
        {
            var digest = await contentHasher.HashFileAsync(path, cancellationToken)
                .ConfigureAwait(false);
            entries.Add(new EvidenceIndexEntry(
                CandidateRelativePath(candidate.Layout, path),
                digest.ByteLength,
                digest.Sha256));
        }

        entries.Add(CreateMemoryEvidenceEntry(
            candidate.Layout,
            candidate.Layout.ReleaseSummaryPath,
            summaryBytes));
        entries.Add(CreateMemoryEvidenceEntry(
            candidate.Layout,
            candidate.Layout.UploaderChecklistPath,
            checklistBytes));
        return entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
    }

    private static EvidenceIndexEntry CreateMemoryEvidenceEntry(
        CandidateLayout layout,
        string path,
        byte[] bytes) =>
        new(
            CandidateRelativePath(layout, path),
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private async Task<OperationResult<ReleaseReadinessReport>> PersistVerificationFailureAsync(
        CandidateLayout layout,
        ReleaseReadinessReport prior,
        Diagnostic diagnostic,
        bool irreversible,
        CancellationToken cancellationToken)
    {
        var irreversibleReason = irreversible
            ? prior.IrreversibleInvalidation ??
                $"{diagnostic.Id}: {diagnostic.Summary} {diagnostic.Evidence}"
            : prior.IrreversibleInvalidation;
        var report = prior with
        {
            State = ReleaseCandidateState.VerificationFailed,
            PreparedContentVerified = false,
            BlockingConditions =
            [
                new ReleaseBlockingCondition(diagnostic.Id, diagnostic.Summary)
            ],
            IrreversibleInvalidation = irreversibleReason
        };
        var diagnostics = new List<Diagnostic> { diagnostic };
        try
        {
            await WriteSingleDerivedEvidenceAsync(
                layout,
                layout.ReleaseReadinessReportPath,
                SerializeJson(report),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            diagnostics.Add(DiagnosticCatalog.CleanupFailed(
                layout.ReleaseReadinessReportPath,
                $"Irreversible verification state could not be persisted: {exception.Message}"));
        }

        return new OperationResult<ReleaseReadinessReport>(
            report,
            diagnostics,
            PipelineExitCode.ReleaseNotReady);
    }

    private static async Task WriteDerivedEvidenceAsync(
        CandidateLayout layout,
        byte[] summaryBytes,
        byte[] checklistBytes,
        byte[] readinessBytes,
        CancellationToken cancellationToken)
    {
        var writes = new[]
        {
            (layout.ReleaseSummaryPath, summaryBytes),
            (layout.UploaderChecklistPath, checklistBytes),
            (layout.ReleaseReadinessReportPath, readinessBytes)
        };
        var staged = new List<(string Temporary, string Destination)>();
        try
        {
            foreach (var (destination, bytes) in writes)
            {
                EnsureRegularDirectory(layout.ReleaseEvidenceDirectory, "release evidence");
                var temporary = CreateDerivedTemporaryPath(layout, destination);
                await WriteCreateNewAsync(temporary, bytes, cancellationToken)
                    .ConfigureAwait(false);
                staged.Add((temporary, destination));
            }

            foreach (var (temporary, destination) in staged)
            {
                EnsureRegularDirectory(layout.ReleaseEvidenceDirectory, "release evidence");
                File.Move(temporary, destination, overwrite: true);
            }
        }
        finally
        {
            foreach (var (temporary, _) in staged)
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }

    private static async Task WriteSingleDerivedEvidenceAsync(
        CandidateLayout layout,
        string destination,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        EnsureRegularDirectory(layout.ReleaseEvidenceDirectory, "release evidence");
        var temporary = CreateDerivedTemporaryPath(layout, destination);
        try
        {
            await WriteCreateNewAsync(temporary, bytes, cancellationToken)
                .ConfigureAwait(false);
            EnsureRegularDirectory(layout.ReleaseEvidenceDirectory, "release evidence");
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task WriteCreateNewAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CreateDerivedTemporaryPath(
        CandidateLayout layout,
        string destination)
    {
        var path = Path.GetFullPath(Path.Combine(
            layout.ReleaseEvidenceDirectory,
            $".{Path.GetFileName(destination)}.verify-{Guid.NewGuid():N}.tmp"));
        EnsureStrictDescendant(
            layout.ReleaseEvidenceDirectory,
            path,
            "release evidence");
        return path;
    }

    private ListingRepresentation InspectListingText(
        string path,
        ListingTextReport expected)
    {
        EnsureRegularFile(path, "Workshop text artifact");
        var bytes = File.ReadAllBytes(path);
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            return new ListingRepresentation(
                null,
                NonCanonicalWithSameLogicalContent: false,
                $"Text is not strict UTF-8: {exception.Message}");
        }

        var rendered = listingTextRenderer.Render(text);
        var canonical = bytes.AsSpan().SequenceEqual(rendered.Bytes);
        var logicalMatches = string.Equals(
            rendered.Report.LogicalContentSha256,
            expected.LogicalContentSha256,
            StringComparison.Ordinal);
        return new ListingRepresentation(
            rendered,
            NonCanonicalWithSameLogicalContent: !canonical && logicalMatches,
            !canonical
                ? "Bytes are not BOM-free UTF-8 with the exact generated CRLF representation."
                : null);
    }

    private static void ValidateExactListingRepresentation(
        string path,
        ListingRepresentation actual,
        ListingTextReport expected)
    {
        if (actual.Rendered is null ||
            actual.Rendered.Report != expected ||
            actual.Reason is not null)
        {
            throw UploaderMismatch(
                path,
                actual.Reason ??
                "Logical or artifact representation report differs from build provenance.");
        }
    }

    private static bool ParsePassedTrx(string path)
    {
        EnsureRegularFile(path, "TRX automated test evidence");
        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root ??
            throw new InvalidDataException("TRX has no document root.");
        var ns = root.Name.Namespace;
        if (root.Name.LocalName != "TestRun" || ns == XNamespace.None)
        {
            throw new InvalidDataException(
                "TRX root must be a namespaced TestRun element.");
        }

        var summaries = root.Descendants(ns + "ResultSummary").ToArray();
        if (summaries.Length != 1 ||
            !string.Equals(
                (string?)summaries[0].Attribute("outcome"),
                "Completed",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "TRX must contain one completed ResultSummary.");
        }

        var counters = summaries[0].Descendants(ns + "Counters").ToArray();
        if (counters.Length != 1)
        {
            throw new InvalidDataException(
                "TRX ResultSummary must contain one Counters element.");
        }

        var executed = ReadCounter(counters[0], "executed");
        var total = ReadCounter(counters[0], "total");
        var passed = ReadCounter(counters[0], "passed");
        var failed = ReadCounter(counters[0], "failed");
        var error = ReadCounter(counters[0], "error");
        var timeout = ReadCounter(counters[0], "timeout");
        var aborted = ReadCounter(counters[0], "aborted");
        if (executed <= 0 ||
            total < executed ||
            passed != executed ||
            failed != 0 ||
            error != 0 ||
            timeout != 0 ||
            aborted != 0)
        {
            return false;
        }

        return true;
    }

    private static long ReadCounter(XElement counters, string name)
    {
        var value = (string?)counters.Attribute(name);
        if (!long.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsed) ||
            parsed < 0)
        {
            throw new InvalidDataException(
                $"TRX counter '{name}' is missing or invalid.");
        }

        return parsed;
    }

    private static void ValidateCandidateInventory(
        CandidateLayout layout,
        ReleaseReadinessReport prior)
    {
        var topLevel = Directory.EnumerateFileSystemEntries(layout.CandidateDirectory)
            .Select(Path.GetFullPath)
            .ToHashSet(HostPathComparer);
        var expectedTopLevel = new[]
        {
            layout.WorkshopContentDirectory,
            layout.WorkshopListingDirectory,
            layout.ReleaseEvidenceDirectory
        }.Select(Path.GetFullPath).ToHashSet(HostPathComparer);
        if (!topLevel.SetEquals(expectedTopLevel))
        {
            throw ManifestMismatch(
                "Candidate top-level inventory must contain only workshop-content, workshop-listing, and release-evidence directories.");
        }

        EnsureRegularDirectory(layout.WorkshopContentDirectory, "Workshop content");
        EnsureRegularDirectory(layout.WorkshopListingDirectory, "Workshop listing");
        EnsureRegularDirectory(layout.ReleaseEvidenceDirectory, "release evidence");
        EnsureRegularDirectory(
            layout.AutomatedTestResultsDirectory,
            "automated test results");
        var allowedEvidence = new[]
        {
            layout.ReleaseReadinessReportPath,
            layout.ReleaseContentManifestPath,
            layout.BuildProvenancePath,
            layout.AutomatedTestResultsDirectory,
            layout.AcceptanceTestPlanPath,
            layout.ReleaseSummaryPath,
            layout.UploaderChecklistPath,
            layout.InstallationReceiptPath,
            layout.AcceptanceTestResultsPath
        }.Select(Path.GetFullPath).ToHashSet(HostPathComparer);
        var actualEvidence = Directory
            .EnumerateFileSystemEntries(layout.ReleaseEvidenceDirectory)
            .Select(Path.GetFullPath)
            .ToArray();
        var unknownEvidence = actualEvidence
            .Where(path => !allowedEvidence.Contains(path))
            .ToArray();
        if (unknownEvidence.Length > 0)
        {
            throw ReleaseEvidenceMismatch(
                $"Release evidence contains undeclared paths: {string.Join(", ", unknownEvidence.Select(path => $"'{path}'"))}.");
        }

        var expectedTrx = prior.AutomatedTests
            .Select(test => ResolveCandidateRelativePath(
                layout,
                test.TrxPath,
                "automated test evidence"))
            .ToHashSet(HostPathComparer);
        var actualTrx = EnumerateRegularFiles(layout.AutomatedTestResultsDirectory)
            .ToHashSet(HostPathComparer);
        if (!actualTrx.SetEquals(expectedTrx))
        {
            var missingTest = prior.AutomatedTests.FirstOrDefault(test =>
                !actualTrx.Contains(ResolveCandidateRelativePath(
                    layout,
                    test.TrxPath,
                    "automated test evidence")));
            if (missingTest is not null)
            {
                throw AutomatedTestMismatch(
                    missingTest.Id,
                    "Required TRX file is missing from the exact automated-test inventory.");
            }

            throw ReleaseEvidenceMismatch(
                "Automated-test-results contains undeclared files.");
        }
    }

    private static void ValidatePriorReadinessIdentity(
        CandidateLayout layout,
        ReleaseReadinessReport prior)
    {
        if (prior.SchemaVersion != 1 ||
            !string.Equals(prior.StaticId, layout.StaticId, StringComparison.Ordinal) ||
            !string.Equals(prior.Version, layout.Version, StringComparison.Ordinal) ||
            prior.AutomatedTests.Select(test => test.Id)
                .Distinct(StringComparer.Ordinal).Count() != prior.AutomatedTests.Count)
        {
            throw new InvalidDataException(
                "Readiness schema, candidate identity, or automated-test identities are invalid.");
        }
    }

    private static void ValidateCoreIdentity(
        CandidateLayout layout,
        ReleaseContentManifest manifest,
        BuildProvenance provenance,
        AcceptanceTestPlan plan)
    {
        if (manifest.SchemaVersion != 1 ||
            provenance.SchemaVersion != 1 ||
            plan.SchemaVersion != 1 ||
            !string.Equals(layout.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(layout.Version, provenance.Version, StringComparison.Ordinal) ||
            !string.Equals(plan.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(plan.Version, provenance.Version, StringComparison.Ordinal) ||
            !string.Equals(
                manifest.ContentDigest,
                provenance.ReleaseContentDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                plan.ContentDigest,
                manifest.ContentDigest,
                StringComparison.Ordinal) ||
            plan.Checks.Count != provenance.AcceptanceCheckCount)
        {
            throw ManifestMismatch(
                "Candidate path, schema versions, provenance, manifest, and acceptance-plan identities do not agree.");
        }
    }

    private static void ValidateBuildProvenance(BuildProvenance provenance)
    {
        if (!provenance.RelevantPathsClean ||
            !provenance.SourceBytesUnchanged ||
            !CommitPattern().IsMatch(provenance.RepositoryCommit))
        {
            throw new VerificationException(
                DiagnosticCatalog.DirtyReleaseInput(
                    "Build provenance is dirty, source bytes changed, or the scoped commit identity is invalid."),
                irreversible: true);
        }

        ValidateSha256(provenance.PipelineExecutableSha256, "pipeline executable");
        ValidateSha256(provenance.ReleaseContentDigest, "release content");
        ValidateSha256(provenance.AcceptanceTestPlanSha256, "acceptance plan");
        ValidateDigestCollection(provenance.LockFiles, "lock files");
        ValidateDigestCollection(provenance.GameReferences, "game references");
        ValidateDigestCollection(provenance.BuildInputs, "build inputs");
        ValidateDigestCollection(provenance.MergeInputs, "merge inputs");
        ValidateDigestCollection(provenance.BuildOutputs, "build outputs");
        var closure = ComputeLockedDependencyClosureSha256(provenance.LockFiles);
        if (!string.Equals(
            closure,
            provenance.LockedDependencyClosureSha256,
            StringComparison.Ordinal))
        {
            throw new VerificationException(
                DiagnosticCatalog.DirtyReleaseInput(
                    "Locked dependency closure digest does not match its canonical lock-file inventory."),
                irreversible: true);
        }

        if (provenance.PrimaryOutput is not null &&
            !provenance.BuildOutputs.Contains(provenance.PrimaryOutput))
        {
            throw new VerificationException(
                DiagnosticCatalog.DirtyReleaseInput(
                    "Primary build output is not present in the immutable build-output inventory."),
                irreversible: true);
        }
    }

    private static void ValidateDigestCollection(
        IReadOnlyList<ProvenanceFileDigest> files,
        string description)
    {
        if (files.Select(file => file.Path).Distinct(StringComparer.Ordinal).Count() !=
            files.Count)
        {
            throw new InvalidDataException(
                $"Provenance {description} contain duplicate paths.");
        }

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Path) || file.ByteLength < 0)
            {
                throw new InvalidDataException(
                    $"Provenance {description} contain an invalid path or byte length.");
            }

            ValidateSha256(file.Sha256, description);
        }
    }

    private static void ValidateSha256(string value, string description)
    {
        if (!Sha256Pattern().IsMatch(value))
        {
            throw new InvalidDataException(
                $"Provenance {description} SHA-256 is not 64 lowercase hexadecimal characters.");
        }
    }

    private static string ComputeLockedDependencyClosureSha256(
        IReadOnlyList<ProvenanceFileDigest> lockFiles)
    {
        var canonical = new StringBuilder("oni-locked-dependency-closure-v1\n");
        foreach (var file in lockFiles.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            canonical.Append(file.Path);
            canonical.Append('\0');
            canonical.Append(file.ByteLength.ToString(CultureInfo.InvariantCulture));
            canonical.Append('\0');
            canonical.Append(file.Sha256);
            canonical.Append('\n');
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(StrictUtf8.GetBytes(canonical.ToString())));
    }

    private static void ValidateReceiptTarget(
        InstallationReceipt receipt,
        BuildProvenance provenance)
    {
        if (!Path.IsPathFullyQualified(receipt.AbsoluteTargetPath))
        {
            throw new InvalidDataException(
                "Installation receipt target must be absolute.");
        }

        var destination = Path.GetFullPath(receipt.AbsoluteTargetPath);
        var targetRoot = Path.GetDirectoryName(destination);
        var modsRoot = targetRoot is null ? null : Path.GetDirectoryName(targetRoot);
        if (!HostPathComparer.Equals(destination, receipt.AbsoluteTargetPath) ||
            !string.Equals(
                Path.GetFileName(destination),
                provenance.ManagedDirectoryName,
                StringComparison.Ordinal) ||
            targetRoot is null ||
            modsRoot is null ||
            !string.Equals(
                Path.GetFileName(targetRoot),
                receipt.Target.ToDirectoryName(),
                StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(modsRoot), "mods", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Installation receipt target is not the canonical mods/Dev or mods/Local managed path.");
        }

        EnsureRegularDirectory(modsRoot, "installed mods root");
        EnsureRegularDirectory(targetRoot, "installed target root");
        EnsureRegularDirectory(destination, "live installed mod");
    }

    private static IReadOnlyList<(
        string AbsolutePath,
        ContentArea Area,
        ContentRole Role)> EnumerateCandidateContent(CandidateLayout layout)
    {
        var files = EnumerateRegularFiles(layout.WorkshopContentDirectory)
            .Select(path => (path, ContentArea.WorkshopContent, ContentRole.Runtime))
            .ToList();
        foreach (var path in EnumerateRegularFiles(layout.WorkshopListingDirectory))
        {
            var relative = Path.GetRelativePath(
                    layout.WorkshopListingDirectory,
                    path)
                .Replace((char)92, '/');
            var role = relative switch
            {
                "description.bbcode" => ContentRole.Description,
                "change-notes.bbcode" => ContentRole.ChangeNotes,
                "preview.png" or "preview.jpg" or "preview.gif" =>
                    ContentRole.Preview,
                _ => throw ManifestMismatch(
                    $"Workshop listing contains undeclared path '{relative}'.")
            };
            files.Add((path, ContentArea.WorkshopListing, role));
        }

        var listingRoles = files
            .Where(file => file.Item2 == ContentArea.WorkshopListing)
            .Select(file => file.Item3)
            .OrderBy(role => role)
            .ToArray();
        if (!listingRoles.SequenceEqual(new[]
        {
            ContentRole.Description,
            ContentRole.ChangeNotes,
            ContentRole.Preview
        }.OrderBy(role => role)))
        {
            throw ManifestMismatch(
                "Workshop listing must contain exactly one description, change-notes, and preview file.");
        }

        return files;
    }

    private static IReadOnlyList<string> EnumerateRegularFiles(string root)
    {
        var resolvedRoot = Path.GetFullPath(root);
        EnsureRegularDirectory(resolvedRoot, "file inventory root");
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(resolvedRoot);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory
                .EnumerateFileSystemEntries(directory)
                .OrderBy(path => path, HostPathComparer))
            {
                var attributes = File.GetAttributes(entry);
                FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
                    ? new DirectoryInfo(entry)
                    : new FileInfo(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    info.LinkTarget is not null)
                {
                    throw new InvalidDataException(
                        $"Inventory contains linked entry '{entry}'.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(Path.GetFullPath(entry));
                }
                else
                {
                    files.Add(Path.GetFullPath(entry));
                }
            }
        }

        return files.OrderBy(path => path, HostPathComparer).ToArray();
    }

    private static T ReadJsonFile<T>(string path)
    {
        var resolved = Path.GetFullPath(path);
        EnsureRegularFile(resolved, "JSON evidence");
        var info = new FileInfo(resolved);
        if (info.Length == 0 || info.Length > MaximumEvidenceBytes)
        {
            throw new InvalidDataException(
                $"JSON evidence '{resolved}' must be nonempty and no larger than {MaximumEvidenceBytes} bytes.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(
                File.ReadAllBytes(resolved),
                ReadJsonOptions) ??
                throw new InvalidDataException(
                    $"JSON evidence '{resolved}' deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"JSON evidence '{resolved}' is invalid: {exception.Message}",
                exception);
        }
    }

    private static byte[] SerializeJson<T>(T value)
    {
        var text = JsonSerializer.Serialize(value, WriteJsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n') + "\n";
        return StrictUtf8.GetBytes(text);
    }

    private static byte[] ToLfUtf8(string value) =>
        StrictUtf8.GetBytes(
            value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .TrimEnd('\n') + "\n");

    private static string CandidateRelativePath(
        CandidateLayout layout,
        string path)
    {
        var resolved = Path.GetFullPath(path);
        EnsureStrictDescendant(layout.CandidateDirectory, resolved, "candidate root");
        return Path.GetRelativePath(layout.CandidateDirectory, resolved)
            .Replace((char)92, '/');
    }

    private static string ResolveCandidateRelativePath(
        CandidateLayout layout,
        string relativePath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"{description} path must be a nonempty relative path.");
        }

        var resolved = Path.GetFullPath(Path.Combine(
            layout.CandidateDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureStrictDescendant(layout.CandidateDirectory, resolved, "candidate root");
        return resolved;
    }

    private static string ResolveAreaPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                "Content manifest path must be a nonempty relative path.");
        }

        var resolved = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureStrictDescendant(root, resolved, "content area");
        return resolved;
    }

    private static void EnsureRegularDirectory(string path, string description)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        directory.Refresh();
        if (!directory.Exists ||
            (directory.Attributes & FileAttributes.ReparsePoint) != 0 ||
            directory.LinkTarget is not null)
        {
            throw new InvalidDataException(
                $"The {description} '{directory.FullName}' must be an existing non-linked directory.");
        }
    }

    private static void EnsureRegularFile(string path, string description)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        file.Refresh();
        if (!file.Exists ||
            (file.Attributes & FileAttributes.Directory) != 0 ||
            (file.Attributes & FileAttributes.ReparsePoint) != 0 ||
            file.LinkTarget is not null)
        {
            throw new InvalidDataException(
                $"The {description} '{file.FullName}' must be an existing non-linked file.");
        }
    }

    private static void EnsureStrictDescendant(
        string root,
        string path,
        string description)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        if (relative == "." ||
            Path.IsPathRooted(relative) ||
            relative == ".." ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            relative.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Path '{path}' must remain beneath the {description} '{root}'.");
        }
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

    private static bool IsExpectedEvidenceException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or
        InvalidDataException or InvalidOperationException or
        ArgumentException or JsonException or NotSupportedException or
        DecoderFallbackException or System.Xml.XmlException;

    private static VerificationException ManifestMismatch(string reason) =>
        new(DiagnosticCatalog.CandidateManifestMismatch(reason), irreversible: true);

    private static VerificationException AcceptanceMismatch(
        string path,
        string reason) =>
        new(
            DiagnosticCatalog.AcceptanceDigestMismatch(path, reason),
            irreversible: true);

    private static VerificationException UploaderMismatch(
        string path,
        string reason) =>
        new(
            DiagnosticCatalog.InvalidUploaderRepresentation(path, reason),
            irreversible: true);

    private static VerificationException AutomatedTestMismatch(
        string id,
        string reason) =>
        new(
            DiagnosticCatalog.AutomatedTestFailed(id, reason),
            irreversible: true);

    private static VerificationException ReleaseEvidenceMismatch(string reason) =>
        new(DiagnosticCatalog.ReleaseNotReady(reason), irreversible: true);

    [GeneratedRegex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private sealed class VerificationException(
        Diagnostic diagnostic,
        bool irreversible) : Exception(diagnostic.Evidence)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;

        internal bool Irreversible { get; } = irreversible;
    }

    private sealed record ListingRepresentation(
        RenderedListingText? Rendered,
        bool NonCanonicalWithSameLogicalContent,
        string? Reason);

    private sealed record VerifiedAutomatedTest(
        AutomatedTestEvidence Evidence,
        string TrxPath,
        bool Passed);

    private sealed record VerifiedCandidate(
        CandidateLayout Layout,
        ReleaseContentManifest Manifest,
        BuildProvenance Provenance,
        AcceptanceTestPlan Plan,
        InstallationReceipt? Receipt,
        AcceptanceTestResults? AcceptanceResults,
        WorkshopListingAssembly Listing,
        IReadOnlyList<VerifiedAutomatedTest> AutomatedTests);

    private sealed record StateDerivation(
        ReleaseCandidateState State,
        IReadOnlyList<ReleaseBlockingCondition> Blockers,
        bool? RequiredAcceptancePassed);
}
