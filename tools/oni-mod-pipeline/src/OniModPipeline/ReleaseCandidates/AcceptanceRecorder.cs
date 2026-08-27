using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModInstallation;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal interface IAcceptanceRecorder
{
    Task<OperationResult<AcceptanceRecordingResult>> RecordAsync(
        string candidateDirectory,
        string? tester,
        CancellationToken cancellationToken);
}

internal sealed class AcceptanceRecorder : IAcceptanceRecorder
{
    private const int MaximumEvidenceBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding Utf8WithoutBom = new(
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
    private readonly IAcceptanceConsole console;
    private readonly TimeProvider timeProvider;

    internal AcceptanceRecorder(
        ContentHasher contentHasher,
        IAcceptanceConsole console,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contentHasher);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.contentHasher = contentHasher;
        this.console = console;
        this.timeProvider = timeProvider;
    }

    internal static AcceptanceRecorder CreateDefault(IAcceptanceConsole console) =>
        new(new ContentHasher(), console, TimeProvider.System);

    public async Task<OperationResult<AcceptanceRecordingResult>> RecordAsync(
        string candidateDirectory,
        string? tester,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!console.IsInteractive)
        {
            return Failure(
                DiagnosticCatalog.AcceptanceRequiresInteractiveTerminal(),
                PipelineExitCode.ReleaseNotReady);
        }

        if (tester is not null && string.IsNullOrWhiteSpace(tester))
        {
            return Failure(
                DiagnosticCatalog.RequiredAcceptanceMissing(
                    "The explicitly supplied tester display name is empty."),
                PipelineExitCode.InvalidInput);
        }

        CandidateLayout layout;
        try
        {
            layout = CandidateLayout.FromCandidateDirectory(candidateDirectory);
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return EvidenceFailure(candidateDirectory, exception.Message);
        }

        if (EntryExists(layout.AcceptanceTestResultsPath))
        {
            return Failure(
                DiagnosticCatalog.AcceptanceResultsAlreadyExist(
                    layout.AcceptanceTestResultsPath),
                PipelineExitCode.ReleaseNotReady);
        }

        VerifiedAcceptanceSnapshot initial;
        try
        {
            initial = await VerifyCandidateAndInstallationAsync(
                layout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return EvidenceFailure(layout.CandidateDirectory, exception.Message);
        }

        console.WriteLine($"Candidate static ID: {initial.Plan.StaticId}");
        console.WriteLine($"Candidate version: {initial.Plan.Version}");
        console.WriteLine($"Candidate content digest: {initial.Plan.ContentDigest}");

        var testerName = (tester ?? console.ReadRequired("Tester display name: ")).Trim();
        if (testerName.Length == 0)
        {
            return Failure(
                DiagnosticCatalog.RequiredAcceptanceMissing(
                    "The tester display name is empty."),
                PipelineExitCode.InvalidInput);
        }

        var checkResults = new List<AcceptanceCheckResult>(initial.Plan.Checks.Count);
        for (var index = 0; index < initial.Plan.Checks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var check = initial.Plan.Checks[index];
            console.WriteLine(string.Empty);
            console.WriteLine(
                $"Acceptance check {index + 1}/{initial.Plan.Checks.Count}: {check.Title}");
            console.WriteLine($"Setup: {check.Setup}");
            console.WriteLine($"Action: {check.Action}");
            console.WriteLine($"Expected: {check.Expected}");
            var outcome = console.ReadOutcome("Outcome (passed/failed): ");
            var note = console.ReadOptional("Optional note: ")?.Trim();
            checkResults.Add(new AcceptanceCheckResult(
                check.Id,
                check.Title,
                check.Setup,
                check.Action,
                check.Expected,
                outcome,
                string.IsNullOrWhiteSpace(note) ? null : note));
        }

        VerifiedAcceptanceSnapshot current;
        try
        {
            current = await VerifyCandidateAndInstallationAsync(
                layout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return EvidenceFailure(layout.CandidateDirectory, exception.Message);
        }

        if (!initial.HasSameEvidenceIdentity(current))
        {
            return EvidenceFailure(
                layout.CandidateDirectory,
                "Candidate or installation evidence changed while acceptance responses were being collected.");
        }

        if (EntryExists(layout.AcceptanceTestResultsPath))
        {
            return Failure(
                DiagnosticCatalog.AcceptanceResultsAlreadyExist(
                    layout.AcceptanceTestResultsPath),
                PipelineExitCode.ReleaseNotReady);
        }

        var recordedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var results = new AcceptanceTestResults(
            1,
            testerName,
            recordedAtUtc,
            initial.Manifest.ContentDigest,
            initial.AcceptancePlanSha256,
            checkResults);
        try
        {
            EnsureRegularDirectory(
                layout.ReleaseEvidenceDirectory,
                "release evidence");
            await WriteResultsCreateNewAsync(
                layout.AcceptanceTestResultsPath,
                results,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException) when (EntryExists(layout.AcceptanceTestResultsPath))
        {
            return Failure(
                DiagnosticCatalog.AcceptanceResultsAlreadyExist(
                    layout.AcceptanceTestResultsPath),
                PipelineExitCode.ReleaseNotReady);
        }
        catch (Exception exception) when (IsExpectedEvidenceException(exception))
        {
            return EvidenceFailure(layout.AcceptanceTestResultsPath, exception.Message);
        }

        var allChecksPassed = checkResults.All(
            check => check.Outcome == AcceptanceOutcome.Passed);
        var recording = new AcceptanceRecordingResult(
            layout.AcceptanceTestResultsPath,
            initial.Plan.StaticId,
            initial.Plan.Version,
            initial.Manifest.ContentDigest,
            recordedAtUtc,
            allChecksPassed);
        console.WriteLine(string.Empty);
        console.WriteLine(
            $"Acceptance results recorded: {layout.AcceptanceTestResultsPath}");
        console.WriteLine(
            $"All acceptance checks passed: {allChecksPassed.ToString().ToLowerInvariant()}");
        if (allChecksPassed)
        {
            return new OperationResult<AcceptanceRecordingResult>(
                recording,
                [],
                PipelineExitCode.Success);
        }

        var failedIds = checkResults
            .Where(check => check.Outcome == AcceptanceOutcome.Failed)
            .Select(check => check.Id)
            .ToArray();
        return new OperationResult<AcceptanceRecordingResult>(
            recording,
            [DiagnosticCatalog.RequiredAcceptanceMissing(
                $"Failed acceptance checks: {string.Join(", ", failedIds.Select(id => $"'{id}'"))}.")],
            PipelineExitCode.ReleaseNotReady);
    }

    private async Task<VerifiedAcceptanceSnapshot> VerifyCandidateAndInstallationAsync(
        CandidateLayout layout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRegularDirectory(layout.CandidateDirectory, "release candidate");
        EnsureRegularDirectory(
            layout.ReleaseEvidenceDirectory,
            "release evidence");
        var manifest = ReadJsonFile<ReleaseContentManifest>(
            layout.ReleaseContentManifestPath);
        var provenance = ReadJsonFile<BuildProvenance>(layout.BuildProvenancePath);
        var plan = ReadJsonFile<AcceptanceTestPlan>(layout.AcceptanceTestPlanPath);
        var receipt = ReadJsonFile<InstallationReceipt>(layout.InstallationReceiptPath);
        if (manifest.SchemaVersion != 1 ||
            provenance.SchemaVersion != 1 ||
            plan.SchemaVersion != 1 ||
            receipt.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                "Manifest, provenance, acceptance plan, and installation receipt must all use schema version 1.");
        }

        if (!string.Equals(layout.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(layout.Version, provenance.Version, StringComparison.Ordinal) ||
            !string.Equals(plan.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(plan.Version, provenance.Version, StringComparison.Ordinal) ||
            !string.Equals(receipt.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(receipt.Version, provenance.Version, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Candidate path, provenance, acceptance plan, and receipt identities do not agree.");
        }

        var actualManifest = await contentHasher.CreateManifestAsync(
            layout.CandidateDirectory,
            EnumerateCandidateContent(layout),
            cancellationToken).ConfigureAwait(false);
        if (!ManifestsEqual(manifest, actualManifest) ||
            !string.Equals(
                manifest.ContentDigest,
                provenance.ReleaseContentDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.ContentDigest,
                plan.ContentDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.ContentDigest,
                receipt.ContentDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Current release content, manifest, provenance, acceptance plan, and receipt digests do not agree.");
        }

        if (plan.Checks.Count == 0 ||
            plan.Checks.Count != provenance.AcceptanceCheckCount ||
            plan.Checks.Select(check => check.Id).Distinct(StringComparer.Ordinal).Count() !=
                plan.Checks.Count)
        {
            throw new InvalidDataException(
                "The acceptance plan must contain the provenance-recorded nonempty set of unique checks.");
        }

        var acceptancePlanDigest = await contentHasher.HashFileAsync(
            layout.AcceptanceTestPlanPath,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
            acceptancePlanDigest.Sha256,
            provenance.AcceptanceTestPlanSha256,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The acceptance plan SHA-256 differs from immutable build provenance.");
        }

        ValidateReceiptTarget(receipt, provenance);
        var runtimeEntries = manifest.Entries
            .Where(entry => entry.ContentArea == ContentArea.WorkshopContent)
            .ToArray();
        if (runtimeEntries.Length == 0)
        {
            throw new InvalidDataException(
                "The release candidate contains no runtime files to attest.");
        }

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
            throw new InvalidDataException(
                "The live ownership marker does not match candidate identity and content.");
        }

        await VerifyInstalledRuntimeAsync(
            receipt.AbsoluteTargetPath,
            markerPath,
            runtimeEntries,
            cancellationToken).ConfigureAwait(false);

        var manifestDigest = await contentHasher.HashFileAsync(
            layout.ReleaseContentManifestPath,
            cancellationToken).ConfigureAwait(false);
        var provenanceDigest = await contentHasher.HashFileAsync(
            layout.BuildProvenancePath,
            cancellationToken).ConfigureAwait(false);
        var receiptDigest = await contentHasher.HashFileAsync(
            layout.InstallationReceiptPath,
            cancellationToken).ConfigureAwait(false);
        var markerDigest = await contentHasher.HashFileAsync(
            markerPath,
            cancellationToken).ConfigureAwait(false);
        return new VerifiedAcceptanceSnapshot(
            manifest,
            provenance,
            plan,
            receipt,
            manifestDigest.Sha256,
            provenanceDigest.Sha256,
            acceptancePlanDigest.Sha256,
            receiptDigest.Sha256,
            markerDigest.Sha256);
    }

    private async Task VerifyInstalledRuntimeAsync(
        string installDirectory,
        string markerPath,
        IReadOnlyList<ReleaseContentEntry> runtimeEntries,
        CancellationToken cancellationToken)
    {
        EnsureRegularDirectory(installDirectory, "live installed mod");
        var actualFiles = EnumerateRegularFiles(installDirectory);
        var expectedFiles = runtimeEntries
            .Select(entry => ResolveRelativePath(
                installDirectory,
                entry.RelativePath,
                "live installed mod"))
            .Append(Path.GetFullPath(markerPath))
            .ToHashSet(HostPathComparer);
        if (!expectedFiles.SetEquals(actualFiles))
        {
            throw new InvalidDataException(
                "The live installation contains missing or undeclared paths; only the ownership marker may be ignored as non-runtime content.");
        }

        foreach (var expected in runtimeEntries)
        {
            var installedPath = ResolveRelativePath(
                installDirectory,
                expected.RelativePath,
                "live installed mod");
            var actual = await contentHasher.HashFileAsync(
                installedPath,
                cancellationToken).ConfigureAwait(false);
            if (actual.ByteLength != expected.ByteLength ||
                !string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Installed runtime file '{expected.RelativePath}' differs by length or SHA-256.");
            }
        }
    }

    private static IReadOnlyList<(
        string AbsolutePath,
        ContentArea Area,
        ContentRole Role)> EnumerateCandidateContent(CandidateLayout layout)
    {
        EnsureRegularDirectory(layout.WorkshopContentDirectory, "Workshop content");
        EnsureRegularDirectory(layout.WorkshopListingDirectory, "Workshop listing");
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
                _ => throw new InvalidDataException(
                    $"Workshop listing contains undeclared path '{relative}'.")
            };
            files.Add((path, ContentArea.WorkshopListing, role));
        }

        var listingRoles = files
            .Where(file => file.Item2 == ContentArea.WorkshopListing)
            .Select(file => file.Item3)
            .OrderBy(role => role)
            .ToArray();
        var requiredRoles = new[]
        {
            ContentRole.Description,
            ContentRole.ChangeNotes,
            ContentRole.Preview
        }.OrderBy(role => role);
        if (!listingRoles.SequenceEqual(requiredRoles))
        {
            throw new InvalidDataException(
                "Workshop listing must contain exactly one description, change-notes, and preview file.");
        }

        return files;
    }

    private static void ValidateReceiptTarget(
        InstallationReceipt receipt,
        BuildProvenance provenance)
    {
        if (!receipt.InstalledFilesVerified ||
            !Path.IsPathFullyQualified(receipt.AbsoluteTargetPath))
        {
            throw new InvalidDataException(
                "The installation receipt must record verified files at an absolute target path.");
        }

        var destination = Path.GetFullPath(receipt.AbsoluteTargetPath);
        if (!HostPathComparer.Equals(destination, receipt.AbsoluteTargetPath) ||
            !string.Equals(
                Path.GetFileName(destination),
                provenance.ManagedDirectoryName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The receipt destination is not the canonical managed-directory path.");
        }

        var targetRoot = Path.GetDirectoryName(destination);
        var modsRoot = targetRoot is null ? null : Path.GetDirectoryName(targetRoot);
        if (targetRoot is null ||
            modsRoot is null ||
            !string.Equals(
                Path.GetFileName(targetRoot),
                receipt.Target.ToDirectoryName(),
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetFileName(modsRoot),
                "mods",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The receipt destination must use the exact mods/Dev or mods/Local hierarchy.");
        }

        EnsureRegularDirectory(modsRoot, "installed mods root");
        EnsureRegularDirectory(targetRoot, "installed target root");
        EnsureRegularDirectory(destination, "live installed mod");
    }

    private static async Task WriteResultsCreateNewAsync(
        string path,
        AcceptanceTestResults results,
        CancellationToken cancellationToken)
    {
        var destination = Path.GetFullPath(path);
        var serialized = JsonSerializer.Serialize(results, WriteJsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n') + "\n";
        var bytes = Utf8WithoutBom.GetBytes(serialized);
        await using var stream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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

    private static string ResolveRelativePath(
        string root,
        string relativePath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"Recorded path '{relativePath}' must be a nonempty relative path.");
        }

        var resolvedRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(
            resolvedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(resolvedRoot, path);
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
                $"Recorded path '{relativePath}' escapes the {description}.");
        }

        return path;
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

    private static bool EntryExists(string path) =>
        File.Exists(path) || Directory.Exists(path);

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
        ArgumentException or JsonException or NotSupportedException;

    private static OperationResult<AcceptanceRecordingResult> EvidenceFailure(
        string path,
        string reason) =>
        Failure(
            DiagnosticCatalog.AcceptanceDigestMismatch(
                RenderPath(path),
                reason),
            PipelineExitCode.ReleaseNotReady);

    private static OperationResult<AcceptanceRecordingResult> Failure(
        Diagnostic diagnostic,
        PipelineExitCode exitCode) =>
        new(null, [diagnostic], exitCode);

    private static string RenderPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException)
        {
            return path;
        }
    }

    private sealed record VerifiedAcceptanceSnapshot(
        ReleaseContentManifest Manifest,
        BuildProvenance Provenance,
        AcceptanceTestPlan Plan,
        InstallationReceipt Receipt,
        string ManifestSha256,
        string ProvenanceSha256,
        string AcceptancePlanSha256,
        string ReceiptSha256,
        string OwnershipMarkerSha256)
    {
        internal bool HasSameEvidenceIdentity(VerifiedAcceptanceSnapshot other) =>
            string.Equals(
                ManifestSha256,
                other.ManifestSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                ProvenanceSha256,
                other.ProvenanceSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                AcceptancePlanSha256,
                other.AcceptancePlanSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                ReceiptSha256,
                other.ReceiptSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                OwnershipMarkerSha256,
                other.OwnershipMarkerSha256,
                StringComparison.Ordinal);
    }
}
