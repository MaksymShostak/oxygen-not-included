using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.WorkshopContent;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace MaksymShostak.OniModPipeline.ModInstallation;

internal interface IModInstaller
{
    Task<OperationResult<ModInstallationResult>> InstallCandidateAsync(
        string candidateDirectory,
        InstallTarget target,
        PipelineEnvironment environment,
        CancellationToken cancellationToken);

    Task<OperationResult<ModInstallationResult>> InstallBuildAsync(
        ModProfile profile,
        OniMetadata metadata,
        string buildResultPath,
        InstallTarget target,
        PipelineEnvironment environment,
        CancellationToken cancellationToken);
}

internal interface IModInstallationOperations
{
    bool EntryExists(string path);

    void CreateDirectory(string path);

    Task CopyFileNewAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken);

    void MoveDirectory(string sourcePath, string destinationPath);

    void DeleteDirectory(string path);

    Task WriteJsonCreateNewAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken);
}

internal sealed class ModInstallationOperations : IModInstallationOperations
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public bool EntryExists(string path) => File.Exists(path) || Directory.Exists(path);

    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(Path.GetFullPath(path));

    public async Task CopyFileNewAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var created = false;
        try
        {
            await using var source = new FileStream(
                Path.GetFullPath(sourcePath),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = new FileStream(
                Path.GetFullPath(destinationPath),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            created = true;
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (created && File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }

    public void MoveDirectory(string sourcePath, string destinationPath) =>
        Directory.Move(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath));

    public void DeleteDirectory(string path) =>
        Directory.Delete(Path.GetFullPath(path), recursive: true);

    public async Task WriteJsonCreateNewAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var destination = Path.GetFullPath(path);
        var serialized = JsonSerializer.Serialize(value, JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n') + "\n";
        var bytes = Utf8WithoutBom.GetBytes(serialized);
        var created = false;
        try
        {
            await using var stream = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            created = true;
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (created && File.Exists(destination))
            {
                File.Delete(destination);
            }

            throw;
        }
    }
}

internal sealed class ModInstaller : IModInstaller
{
    internal const string OwnershipMarkerFileName =
        ".oni-mod-pipeline-owner.json";

    private const int MaximumEvidenceBytes = 16 * 1024 * 1024;
    private const int MaximumSubscribedMetadataBytes = 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly StringComparer HostPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly HashSet<string> WindowsReservedDeviceNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
            "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly ContentHasher contentHasher;
    private readonly WorkshopContentAssembler workshopContentAssembler;
    private readonly IModInstallationOperations operations;
    private readonly TimeProvider timeProvider;
    private readonly Func<Guid> transientSuffixFactory;

    internal ModInstaller(
        ContentHasher contentHasher,
        WorkshopContentAssembler workshopContentAssembler,
        IModInstallationOperations operations,
        TimeProvider timeProvider,
        Func<Guid> transientSuffixFactory)
    {
        ArgumentNullException.ThrowIfNull(contentHasher);
        ArgumentNullException.ThrowIfNull(workshopContentAssembler);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(transientSuffixFactory);
        this.contentHasher = contentHasher;
        this.workshopContentAssembler = workshopContentAssembler;
        this.operations = operations;
        this.timeProvider = timeProvider;
        this.transientSuffixFactory = transientSuffixFactory;
    }

    internal static ModInstaller CreateDefault() =>
        new(
            new ContentHasher(),
            new WorkshopContentAssembler(),
            new ModInstallationOperations(),
            TimeProvider.System,
            Guid.NewGuid);

    public async Task<OperationResult<ModInstallationResult>> InstallCandidateAsync(
        string candidateDirectory,
        InstallTarget target,
        PipelineEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        cancellationToken.ThrowIfCancellationRequested();

        CandidateInstallSource source;
        try
        {
            source = await LoadCandidateAsync(
                candidateDirectory,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            return InstallationFailure(
                DiagnosticCatalog.InstalledContentMismatch(
                    Path.GetFullPath(candidateDirectory),
                    exception.Message));
        }

        if (operations.EntryExists(source.Layout.InstallationReceiptPath))
        {
            return InstallationFailure(
                DiagnosticCatalog.InstallationReceiptExists(
                    source.Layout.InstallationReceiptPath));
        }

        var layoutResult = ResolveInstallLayout(
            environment,
            target,
            source.Provenance.StaticId,
            source.Provenance.ManagedDirectoryName);
        if (!layoutResult.IsSuccess)
        {
            return ConvertFailure<InstallLayout, ModInstallationResult>(layoutResult);
        }

        return await InstallAsync(
            layoutResult.Value!,
            source.Provenance.StaticId,
            source.Provenance.Version,
            source.ContentDigest,
            source.RuntimeFiles,
            async (stagingDirectory, token) =>
            {
                foreach (var file in source.RuntimeFiles)
                {
                    var destination = ResolveRuntimePath(
                        stagingDirectory,
                        file.RelativePath);
                    CreateParentDirectories(stagingDirectory, destination);
                    await operations.CopyFileNewAsync(
                        file.SourcePath!,
                        destination,
                        token).ConfigureAwait(false);
                }
            },
            new CandidateReceiptRequest(
                source.Layout.InstallationReceiptPath,
                source.Provenance.Version),
            environment,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult<ModInstallationResult>> InstallBuildAsync(
        ModProfile profile,
        OniMetadata metadata,
        string buildResultPath,
        InstallTarget target,
        PipelineEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildResultPath);
        ArgumentNullException.ThrowIfNull(environment);
        cancellationToken.ThrowIfCancellationRequested();

        BuildResult build;
        try
        {
            build = ReadJsonFile<BuildResult>(buildResultPath);
            ValidateExplicitBuildResultPath(buildResultPath, build);
            await VerifyRecordedBuildFilesAsync(build, cancellationToken)
                .ConfigureAwait(false);
            ValidateBuildIdentity(build, metadata, environment);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            return InstallationFailure(
                DiagnosticCatalog.InstalledContentMismatch(
                    Path.GetFullPath(buildResultPath),
                    exception.Message));
        }

        var layoutResult = ResolveInstallLayout(
            environment,
            target,
            metadata.StaticId,
            profile.LocalInstall.DirectoryName);
        if (!layoutResult.IsSuccess)
        {
            return ConvertFailure<InstallLayout, ModInstallationResult>(layoutResult);
        }

        var runtimeFiles = new List<RuntimeFileIdentity>();
        string? contentDigest = null;
        return await InstallAsync(
            layoutResult.Value!,
            metadata.StaticId,
            metadata.Version,
            getContentDigest: () => contentDigest ??
                throw new InvalidOperationException(
                    "Development content identity was not created."),
            getRuntimeFiles: () => runtimeFiles,
            async (stagingDirectory, token) =>
            {
                var packageDirectory = Path.Combine(
                    stagingDirectory,
                    "workshop-content");
                operations.CreateDirectory(packageDirectory);
                var assembly = await workshopContentAssembler.AssembleAsync(
                    profile,
                    build,
                    packageDirectory,
                    token).ConfigureAwait(false);
                if (!assembly.IsSuccess)
                {
                    throw new InstallationSourceException(
                        string.Join(
                            " ",
                            assembly.Diagnostics.Select(diagnostic =>
                                $"{diagnostic.Id}: {diagnostic.Evidence}")));
                }

                var manifest = await contentHasher.CreateManifestAsync(
                    stagingDirectory,
                    assembly.Value!
                        .Select(file => (
                            file.Path,
                            ContentArea.WorkshopContent,
                            ContentRole.Runtime))
                        .ToArray(),
                    token).ConfigureAwait(false);
                runtimeFiles.AddRange(manifest.Entries.Select(entry =>
                    new RuntimeFileIdentity(
                        entry.RelativePath,
                        entry.ByteLength,
                        entry.Sha256,
                        SourcePath: null)));
                contentDigest = manifest.ContentDigest;

                foreach (var file in runtimeFiles)
                {
                    var sourcePath = ResolveRuntimePath(
                        packageDirectory,
                        file.RelativePath);
                    var destinationPath = ResolveRuntimePath(
                        stagingDirectory,
                        file.RelativePath);
                    CreateParentDirectories(stagingDirectory, destinationPath);
                    await operations.CopyFileNewAsync(
                        sourcePath,
                        destinationPath,
                        token).ConfigureAwait(false);
                }

                EnsureOwnedStagingPath(layoutResult.Value!, packageDirectory);
                operations.DeleteDirectory(packageDirectory);
            },
            receiptRequest: null,
            environment,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<OperationResult<ModInstallationResult>> InstallAsync(
        InstallLayout layout,
        string staticId,
        string version,
        string contentDigest,
        IReadOnlyList<RuntimeFileIdentity> runtimeFiles,
        Func<string, CancellationToken, Task> populateStaging,
        CandidateReceiptRequest? receiptRequest,
        PipelineEnvironment environment,
        CancellationToken cancellationToken) =>
        InstallAsync(
            layout,
            staticId,
            version,
            () => contentDigest,
            () => runtimeFiles,
            populateStaging,
            receiptRequest,
            environment,
            cancellationToken);

    private async Task<OperationResult<ModInstallationResult>> InstallAsync(
        InstallLayout layout,
        string staticId,
        string version,
        Func<string> getContentDigest,
        Func<IReadOnlyList<RuntimeFileIdentity>> getRuntimeFiles,
        Func<string, CancellationToken, Task> populateStaging,
        CandidateReceiptRequest? receiptRequest,
        PipelineEnvironment environment,
        CancellationToken cancellationToken)
    {
        var ownershipDiagnostic = ValidateExistingDestination(
            layout,
            staticId);
        if (ownershipDiagnostic is not null)
        {
            return InstallationFailure(ownershipDiagnostic);
        }

        var stagingDirectory = layout.CreateTransientPath(
            "staging",
            transientSuffixFactory());
        var backupDirectory = layout.CreateTransientPath(
            "backup",
            transientSuffixFactory());
        if (operations.EntryExists(stagingDirectory) ||
            operations.EntryExists(backupDirectory))
        {
            return InstallationFailure(DiagnosticCatalog.UnownedInstallDestination(
                layout.Destination,
                "a unique installation staging or backup sibling already exists"));
        }

        var destinationExisted = Directory.Exists(layout.Destination);
        var oldMoved = false;
        var newInstalled = false;
        var receiptWritten = false;
        OwnershipMarker? intendedMarker = null;
        try
        {
            CreateAndValidateTargetRoot(layout);
            operations.CreateDirectory(stagingDirectory);
            EnsureOwnedTransientDirectory(layout, stagingDirectory);
            await populateStaging(stagingDirectory, cancellationToken)
                .ConfigureAwait(false);

            var contentDigest = getContentDigest();
            var runtimeFiles = getRuntimeFiles();
            var marker = new OwnershipMarker(
                1,
                staticId,
                layout.ManagedDirectoryName,
                contentDigest);
            intendedMarker = marker;
            await operations.WriteJsonCreateNewAsync(
                Path.Combine(stagingDirectory, OwnershipMarkerFileName),
                marker,
                cancellationToken).ConfigureAwait(false);
            var stagedDiagnostic = await VerifyInstalledTreeAsync(
                stagingDirectory,
                runtimeFiles,
                marker,
                cancellationToken).ConfigureAwait(false);
            if (stagedDiagnostic is not null)
            {
                return FailWithCleanup(
                    stagedDiagnostic,
                    layout,
                    stagingDirectory,
                    backupDirectory,
                    oldMoved,
                    newInstalled,
                    marker);
            }

            if (destinationExisted)
            {
                RevalidateOwnedDestination(layout, staticId);
                operations.MoveDirectory(layout.Destination, backupDirectory);
                oldMoved = true;
            }

            EnsureDestinationAbsent(layout);
            operations.MoveDirectory(stagingDirectory, layout.Destination);
            newInstalled = true;
            var installedDiagnostic = await VerifyInstalledTreeAsync(
                layout.Destination,
                runtimeFiles,
                marker,
                cancellationToken).ConfigureAwait(false);
            if (installedDiagnostic is not null)
            {
                return FailWithCleanup(
                    installedDiagnostic,
                    layout,
                    stagingDirectory,
                    backupDirectory,
                    oldMoved,
                    newInstalled,
                    marker);
            }

            var installedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            if (receiptRequest is not null)
            {
                if (operations.EntryExists(receiptRequest.Path))
                {
                    return FailWithCleanup(
                        DiagnosticCatalog.InstallationReceiptExists(
                            receiptRequest.Path),
                        layout,
                        stagingDirectory,
                        backupDirectory,
                        oldMoved,
                        newInstalled,
                        marker);
                }

                var receipt = new InstallationReceipt(
                    1,
                    staticId,
                    receiptRequest.Version,
                    contentDigest,
                    layout.Target,
                    layout.Destination,
                    installedAtUtc,
                    InstalledFilesVerified: true);
                await operations.WriteJsonCreateNewAsync(
                    receiptRequest.Path,
                    receipt,
                    cancellationToken).ConfigureAwait(false);
                receiptWritten = true;
            }

            var diagnostics = FindDuplicateSubscribedCopies(
                environment.UserDataDirectory,
                staticId).ToList();
            if (oldMoved && Directory.Exists(backupDirectory))
            {
                try
                {
                    EnsureOwnedBackupDirectory(layout, backupDirectory, staticId);
                    operations.DeleteDirectory(backupDirectory);
                }
                catch (Exception exception) when (IsExpectedDataOrFileException(exception))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticIds.CleanupFailed,
                        DiagnosticSeverity.Warning,
                        "The verified installation succeeded but its owned backup was not removed.",
                        $"Backup '{backupDirectory}' remains: {exception.Message}",
                        "After confirming the live installation is correct, remove only the named hidden backup directory."));
                }
            }

            return new OperationResult<ModInstallationResult>(
                new ModInstallationResult(
                    staticId,
                    version,
                    contentDigest,
                    layout.Target,
                    layout.Destination,
                    installedAtUtc,
                    receiptWritten),
                diagnostics,
                PipelineExitCode.Success);
        }
        catch (OperationCanceledException)
        {
            if (!receiptWritten)
            {
                _ = RollBack(
                    layout,
                    stagingDirectory,
                    backupDirectory,
                    oldMoved,
                    newInstalled,
                    intendedMarker);
            }

            throw;
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            var primary = receiptRequest is not null &&
                operations.EntryExists(receiptRequest.Path) &&
                !receiptWritten
                ? DiagnosticCatalog.InstallationReceiptExists(receiptRequest.Path)
                : DiagnosticCatalog.InstalledContentMismatch(
                    layout.Destination,
                    exception.Message);
            return FailWithCleanup(
                primary,
                layout,
                stagingDirectory,
                backupDirectory,
                oldMoved,
                newInstalled,
                intendedMarker);
        }
    }

    private OperationResult<ModInstallationResult> FailWithCleanup(
        Diagnostic primary,
        InstallLayout layout,
        string stagingDirectory,
        string backupDirectory,
        bool oldMoved,
        bool newInstalled,
        OwnershipMarker? expectedMarker)
    {
        var diagnostics = new List<Diagnostic> { primary };
        diagnostics.AddRange(RollBack(
            layout,
            stagingDirectory,
            backupDirectory,
            oldMoved,
            newInstalled,
            expectedMarker));
        return new OperationResult<ModInstallationResult>(
            null,
            diagnostics,
            PipelineExitCode.InstallationFailed);
    }

    private IReadOnlyList<Diagnostic> RollBack(
        InstallLayout layout,
        string stagingDirectory,
        string backupDirectory,
        bool oldMoved,
        bool newInstalled,
        OwnershipMarker? expectedMarker)
    {
        var diagnostics = new List<Diagnostic>();
        if (newInstalled && Directory.Exists(layout.Destination))
        {
            try
            {
                RevalidateNewDestinationForDeletion(
                    layout,
                    expectedMarker);
                operations.DeleteDirectory(layout.Destination);
            }
            catch (Exception exception) when (IsExpectedDataOrFileException(exception))
            {
                diagnostics.Add(DiagnosticCatalog.CleanupFailed(
                    layout.Destination,
                    exception.Message));
            }
        }

        if (oldMoved && Directory.Exists(backupDirectory) &&
            !operations.EntryExists(layout.Destination))
        {
            try
            {
                EnsureOwnedBackupDirectory(
                    layout,
                    backupDirectory,
                    layout.StaticId);
                operations.MoveDirectory(backupDirectory, layout.Destination);
            }
            catch (Exception exception) when (IsExpectedDataOrFileException(exception))
            {
                diagnostics.Add(DiagnosticCatalog.CleanupFailed(
                    backupDirectory,
                    $"The previous owned installation could not be restored: {exception.Message}"));
            }
        }

        if (Directory.Exists(stagingDirectory))
        {
            try
            {
                EnsureOwnedTransientDirectory(layout, stagingDirectory);
                operations.DeleteDirectory(stagingDirectory);
            }
            catch (Exception exception) when (IsExpectedDataOrFileException(exception))
            {
                diagnostics.Add(DiagnosticCatalog.CleanupFailed(
                    stagingDirectory,
                    exception.Message));
            }
        }

        return diagnostics;
    }

    private async Task<CandidateInstallSource> LoadCandidateAsync(
        string candidateDirectory,
        CancellationToken cancellationToken)
    {
        var layout = CandidateLayout.FromCandidateDirectory(candidateDirectory);
        EnsureRegularDirectory(layout.CandidateDirectory, "release candidate");
        var manifest = ReadJsonFile<ReleaseContentManifest>(
            layout.ReleaseContentManifestPath);
        var provenance = ReadJsonFile<BuildProvenance>(layout.BuildProvenancePath);
        if (manifest.SchemaVersion != 1 || provenance.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                "Candidate content manifest and build provenance must use schema version 1.");
        }

        if (!string.Equals(layout.StaticId, provenance.StaticId, StringComparison.Ordinal) ||
            !string.Equals(layout.Version, provenance.Version, StringComparison.Ordinal) ||
            !string.Equals(
                manifest.ContentDigest,
                provenance.ReleaseContentDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Candidate path, provenance identity, and content digest do not agree.");
        }

        var manifestFiles = EnumerateCandidateManifestFiles(layout);
        var actualManifest = await contentHasher.CreateManifestAsync(
            layout.CandidateDirectory,
            manifestFiles,
            cancellationToken).ConfigureAwait(false);
        if (!ManifestsEqual(manifest, actualManifest))
        {
            throw new InvalidDataException(
                "Current Workshop content or listing bytes differ from release-content-manifest.json.");
        }

        var runtimeFiles = actualManifest.Entries
            .Where(entry => entry.ContentArea == ContentArea.WorkshopContent)
            .Select(entry => new RuntimeFileIdentity(
                entry.RelativePath,
                entry.ByteLength,
                entry.Sha256,
                ResolveRuntimePath(
                    layout.WorkshopContentDirectory,
                    entry.RelativePath)))
            .ToArray();
        if (runtimeFiles.Length == 0)
        {
            throw new InvalidDataException(
                "Candidate Workshop content contains no runtime files.");
        }

        return new CandidateInstallSource(
            layout,
            provenance,
            actualManifest.ContentDigest,
            runtimeFiles);
    }

    private static IReadOnlyList<(
        string AbsolutePath,
        ContentArea Area,
        ContentRole Role)> EnumerateCandidateManifestFiles(CandidateLayout layout)
    {
        EnsureRegularDirectory(layout.WorkshopContentDirectory, "Workshop content");
        EnsureRegularDirectory(layout.WorkshopListingDirectory, "Workshop listing");
        var files = EnumerateRegularFiles(layout.WorkshopContentDirectory)
            .Select(path => (path, ContentArea.WorkshopContent, ContentRole.Runtime))
            .ToList();
        var listingFiles = EnumerateRegularFiles(layout.WorkshopListingDirectory);
        foreach (var path in listingFiles)
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
        if (!listingRoles.SequenceEqual(new[]
        {
            ContentRole.Description,
            ContentRole.ChangeNotes,
            ContentRole.Preview
        }.OrderBy(role => role)))
        {
            throw new InvalidDataException(
                "Workshop listing must contain exactly one description, change-notes, and preview file.");
        }

        return files;
    }

    private async Task VerifyRecordedBuildFilesAsync(
        BuildResult build,
        CancellationToken cancellationToken)
    {
        var recorded = build.Inputs.Concat(build.Outputs).ToArray();
        if (recorded.Length == 0)
        {
            throw new InvalidDataException(
                "The explicit build result contains no recorded input or output files.");
        }

        var seen = new HashSet<string>(HostPathComparer);
        foreach (var expected in recorded)
        {
            var path = Path.GetFullPath(expected.Path);
            if (!seen.Add(path))
            {
                throw new InvalidDataException(
                    $"The explicit build result repeats recorded file '{path}'.");
            }

            var actual = await contentHasher.HashFileAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (actual.ByteLength != expected.ByteLength ||
                !string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Recorded build file '{path}' changed after the build result was written.");
            }
        }
    }

    private static void ValidateExplicitBuildResultPath(
        string buildResultPath,
        BuildResult build)
    {
        var actual = Path.GetFullPath(buildResultPath);
        var expected = Path.GetFullPath(Path.Combine(
            build.RunRoot,
            "build-result.json"));
        if (!HostPathComparer.Equals(actual, expected))
        {
            throw new InvalidDataException(
                $"Explicit build result path '{actual}' does not equal recorded run artifact '{expected}'.");
        }
    }

    private static void ValidateBuildIdentity(
        BuildResult build,
        OniMetadata metadata,
        PipelineEnvironment environment)
    {
        if (!build.SourceBytesUnchanged ||
            !string.Equals(build.ReleaseVersion, metadata.Version, StringComparison.Ordinal) ||
            !string.Equals(
                build.DotnetSdkVersion,
                environment.DotnetSdkVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The explicit build result does not preserve unchanged sources, release version, and current exact SDK identity.");
        }
    }

    private OperationResult<InstallLayout> ResolveInstallLayout(
        PipelineEnvironment environment,
        InstallTarget target,
        string staticId,
        string managedDirectoryName)
    {
        try
        {
            ValidateManagedDirectoryName(managedDirectoryName);
            var userDataRoot = Path.GetFullPath(environment.UserDataDirectory);
            var modsRoot = Path.GetFullPath(Path.Combine(userDataRoot, "mods"));
            var expectedTargetRoot = Path.GetFullPath(Path.Combine(
                modsRoot,
                target.ToDirectoryName()));
            var selectedTargetRoot = Path.GetFullPath(target switch
            {
                InstallTarget.Dev => environment.DevelopmentModsDirectory,
                InstallTarget.Local => environment.LocalModsDirectory,
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            });
            if (!HostPathComparer.Equals(expectedTargetRoot, selectedTargetRoot))
            {
                throw new InvalidDataException(
                    $"Selected {target.ToCanonicalName()} target root '{selectedTargetRoot}' does not equal derived root '{expectedTargetRoot}'.");
            }

            var destination = Path.GetFullPath(Path.Combine(
                selectedTargetRoot,
                managedDirectoryName));
            EnsureStrictDescendant(selectedTargetRoot, destination, "install target root");
            foreach (var protectedPath in GetProtectedPaths(
                userDataRoot,
                modsRoot,
                selectedTargetRoot))
            {
                if (HostPathComparer.Equals(destination, protectedPath) ||
                    IsStrictDescendant(destination, protectedPath))
                {
                    throw new InvalidDataException(
                        $"Destination '{destination}' equals or contains protected path '{protectedPath}'.");
                }
            }

            ValidateExistingDirectoryChain(userDataRoot, selectedTargetRoot);
            return new OperationResult<InstallLayout>(
                new InstallLayout(
                    userDataRoot,
                    modsRoot,
                    selectedTargetRoot,
                    destination,
                    staticId,
                    managedDirectoryName,
                    target),
                [],
                PipelineExitCode.Success);
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            var fallbackPath = Path.GetFullPath(environment.UserDataDirectory);
            return new OperationResult<InstallLayout>(
                null,
                [DiagnosticCatalog.UnownedInstallDestination(
                    fallbackPath,
                    exception.Message)],
                PipelineExitCode.InstallationFailed);
        }
    }

    private Diagnostic? ValidateExistingDestination(
        InstallLayout layout,
        string staticId)
    {
        if (!operations.EntryExists(layout.Destination))
        {
            return null;
        }

        try
        {
            RevalidateOwnedDestination(layout, staticId);
            return null;
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            return DiagnosticCatalog.UnownedInstallDestination(
                layout.Destination,
                exception.Message);
        }
    }

    private static void RevalidateOwnedDestination(
        InstallLayout layout,
        string staticId)
    {
        EnsureRegularDirectory(layout.Destination, "existing install destination");
        var markerPath = Path.Combine(
            layout.Destination,
            OwnershipMarkerFileName);
        var marker = ReadJsonFile<OwnershipMarker>(markerPath);
        if (marker.SchemaVersion != 1 ||
            !string.Equals(marker.StaticId, staticId, StringComparison.Ordinal) ||
            !string.Equals(
                marker.ManagedDirectoryName,
                layout.ManagedDirectoryName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The ownership marker schema, static ID, or managed directory name does not match.");
        }
    }

    private void CreateAndValidateTargetRoot(InstallLayout layout)
    {
        ValidateExistingDirectoryChain(layout.UserDataRoot, layout.TargetRoot);
        operations.CreateDirectory(layout.TargetRoot);
        ValidateExistingDirectoryChain(layout.UserDataRoot, layout.TargetRoot);
        EnsureRegularDirectory(layout.TargetRoot, "ONI mod target root");
    }

    private async Task<Diagnostic?> VerifyInstalledTreeAsync(
        string root,
        IReadOnlyList<RuntimeFileIdentity> runtimeFiles,
        OwnershipMarker expectedMarker,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureRegularDirectory(root, "installed mod root");
            var actualFiles = EnumerateRegularFiles(root);
            var markerPath = Path.Combine(root, OwnershipMarkerFileName);
            var expectedPaths = runtimeFiles
                .Select(file => ResolveRuntimePath(root, file.RelativePath))
                .Append(markerPath)
                .ToHashSet(HostPathComparer);
            if (!expectedPaths.SetEquals(actualFiles))
            {
                throw new InvalidDataException(
                    "Installed inventory contains missing or undeclared files.");
            }

            var marker = ReadJsonFile<OwnershipMarker>(markerPath);
            if (marker != expectedMarker)
            {
                throw new InvalidDataException(
                    "Installed ownership marker does not match the intended pipeline identity.");
            }

            foreach (var expected in runtimeFiles)
            {
                var path = ResolveRuntimePath(root, expected.RelativePath);
                var actual = await contentHasher.HashFileAsync(path, cancellationToken)
                    .ConfigureAwait(false);
                if (actual.ByteLength != expected.ByteLength ||
                    !string.Equals(
                        actual.Sha256,
                        expected.Sha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Installed runtime file '{expected.RelativePath}' differs by length or SHA-256.");
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            return DiagnosticCatalog.InstalledContentMismatch(root, exception.Message);
        }
    }

    private static void RevalidateNewDestinationForDeletion(
        InstallLayout layout,
        OwnershipMarker? expectedMarker)
    {
        EnsureRegularDirectory(layout.Destination, "new install destination");
        EnsureStrictDescendant(layout.TargetRoot, layout.Destination, "install target root");
        if (expectedMarker is null)
        {
            var marker = ReadJsonFile<OwnershipMarker>(Path.Combine(
                layout.Destination,
                OwnershipMarkerFileName));
            if (marker.SchemaVersion != 1 ||
                !string.Equals(
                    marker.StaticId,
                    layout.StaticId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    marker.ManagedDirectoryName,
                    layout.ManagedDirectoryName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "New destination marker no longer proves ownership for rollback.");
            }

            return;
        }

        var actualMarker = ReadJsonFile<OwnershipMarker>(Path.Combine(
            layout.Destination,
            OwnershipMarkerFileName));
        if (actualMarker != expectedMarker)
        {
            throw new InvalidDataException(
                "New destination marker changed before rollback cleanup.");
        }
    }

    private static void EnsureOwnedBackupDirectory(
        InstallLayout layout,
        string backupDirectory,
        string? expectedStaticId)
    {
        if (!layout.IsOwnedTransientPath(backupDirectory, "backup"))
        {
            throw new InvalidDataException(
                $"Backup path '{backupDirectory}' is not an owned sibling.");
        }

        EnsureRegularDirectory(backupDirectory, "owned installation backup");
        var marker = ReadJsonFile<OwnershipMarker>(Path.Combine(
            backupDirectory,
            OwnershipMarkerFileName));
        if (marker.SchemaVersion != 1 ||
            expectedStaticId is not null &&
            !string.Equals(marker.StaticId, expectedStaticId, StringComparison.Ordinal) ||
            !string.Equals(
                marker.ManagedDirectoryName,
                layout.ManagedDirectoryName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Backup marker no longer proves ownership for this managed directory.");
        }
    }

    private static void EnsureOwnedTransientDirectory(
        InstallLayout layout,
        string transientDirectory)
    {
        if (!layout.IsOwnedTransientPath(transientDirectory, "staging"))
        {
            throw new InvalidDataException(
                $"Staging path '{transientDirectory}' is not an owned sibling.");
        }

        EnsureRegularDirectory(transientDirectory, "owned installation staging");
    }

    private static void EnsureOwnedStagingPath(
        InstallLayout layout,
        string nestedPath)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(nestedPath))!;
        if (!layout.IsOwnedTransientPath(parent, "staging"))
        {
            throw new InvalidDataException(
                $"Installation packaging path '{nestedPath}' is outside owned staging.");
        }

        EnsureStrictDescendant(parent, nestedPath, "installation staging");
        EnsureRegularDirectory(nestedPath, "installation packaging directory");
    }

    private static void EnsureDestinationAbsent(InstallLayout layout)
    {
        EnsureStrictDescendant(layout.TargetRoot, layout.Destination, "install target root");
        if (File.Exists(layout.Destination) || Directory.Exists(layout.Destination))
        {
            throw new IOException(
                $"Install destination '{layout.Destination}' appeared during the guarded swap.");
        }
    }

    private static IReadOnlyList<Diagnostic> FindDuplicateSubscribedCopies(
        string userDataDirectory,
        string staticId)
    {
        var steamRoot = Path.Combine(
            Path.GetFullPath(userDataDirectory),
            "mods",
            "Steam");
        if (!Directory.Exists(steamRoot))
        {
            return [];
        }

        var duplicates = new List<string>();
        try
        {
            EnsureRegularDirectory(steamRoot, "subscribed Steam mods root");
            foreach (var directory in Directory
                .EnumerateDirectories(steamRoot)
                .OrderBy(path => path, HostPathComparer))
            {
                try
                {
                    EnsureRegularDirectory(directory, "subscribed Steam mod directory");
                    var metadataPath = Path.Combine(directory, "mod.yaml");
                    if (!File.Exists(metadataPath))
                    {
                        continue;
                    }

                    EnsureRegularFile(metadataPath, "subscribed mod metadata");
                    var info = new FileInfo(metadataPath);
                    if (info.Length > MaximumSubscribedMetadataBytes)
                    {
                        continue;
                    }

                    var yaml = File.ReadAllText(metadataPath);
                    var values = new DeserializerBuilder()
                        .Build()
                        .Deserialize<Dictionary<string, object?>>(yaml);
                    if (values.TryGetValue("staticID", out var value) &&
                        value is not null &&
                        string.Equals(
                            Convert.ToString(
                                value,
                                System.Globalization.CultureInfo.InvariantCulture),
                            staticId,
                            StringComparison.Ordinal))
                    {
                        duplicates.Add(Path.GetFullPath(directory));
                    }
                }
                catch (Exception exception) when (IsExpectedDataOrFileException(exception))
                {
                    // Optional duplicate-risk inspection never mutates or blocks installation.
                }
            }
        }
        catch (Exception exception) when (IsExpectedDataOrFileException(exception))
        {
            return [];
        }

        return duplicates.Count == 0
            ? []
            : [DiagnosticCatalog.DuplicateInstalledMod(staticId, duplicates)];
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
                JsonOptions) ??
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

    private void CreateParentDirectories(string root, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath)!;
        EnsureStrictDescendant(root, destinationPath, "installation staging");
        var relative = Path.GetRelativePath(root, directory);
        if (relative == ".")
        {
            return;
        }

        var current = Path.GetFullPath(root);
        foreach (var segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                operations.CreateDirectory(current);
            }

            EnsureRegularDirectory(current, "installation staging ancestor");
        }
    }

    private static string ResolveRuntimePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"Runtime path '{relativePath}' must be a nonempty relative path.");
        }

        var path = Path.GetFullPath(Path.Combine(
            Path.GetFullPath(root),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureStrictDescendant(root, path, "runtime root");
        return path;
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

    private static void ValidateManagedDirectoryName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            value.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            value.EndsWith(' ') ||
            value.EndsWith('.') ||
            WindowsReservedDeviceNames.Contains(
                value.Split('.', 2, StringSplitOptions.None)[0]) ||
            value.Any(char.IsControl))
        {
            throw new InvalidDataException(
                "Managed directory name must be one portable nonempty filesystem segment.");
        }
    }

    private static IReadOnlyList<string> GetProtectedPaths(
        string userDataRoot,
        string modsRoot,
        string targetRoot)
    {
        var values = new[]
        {
            userDataRoot,
            modsRoot,
            targetRoot,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Path.GetFullPath)
            .Distinct(HostPathComparer)
            .ToArray();
    }

    private static void ValidateExistingDirectoryChain(string root, string path)
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedPath = Path.GetFullPath(path);
        EnsureRegularDirectory(resolvedRoot, "ONI user-data root");
        EnsureStrictDescendant(resolvedRoot, resolvedPath, "ONI user-data root");
        var relative = Path.GetRelativePath(resolvedRoot, resolvedPath);
        var current = resolvedRoot;
        foreach (var segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current))
            {
                throw new InvalidDataException(
                    $"Expected directory path '{current}' is an existing file.");
            }

            if (!Directory.Exists(current))
            {
                break;
            }

            EnsureRegularDirectory(current, "ONI mod target ancestor");
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
        string candidate,
        string description)
    {
        if (!IsStrictDescendant(root, candidate))
        {
            throw new InvalidDataException(
                $"Path '{candidate}' must remain beneath the {description} '{root}'.");
        }
    }

    private static bool IsStrictDescendant(string root, string candidate)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(root),
            Path.GetFullPath(candidate));
        return relative != "." &&
            !Path.IsPathRooted(relative) &&
            relative != ".." &&
            !relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) &&
            !relative.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal);
    }

    private static bool IsExpectedDataOrFileException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or
        InvalidDataException or ArgumentException or JsonException or
        NotSupportedException or YamlDotNet.Core.YamlException or
        InstallationSourceException;

    private static OperationResult<ModInstallationResult> InstallationFailure(
        Diagnostic diagnostic) =>
        new(null, [diagnostic], PipelineExitCode.InstallationFailed);

    private static OperationResult<TOutput> ConvertFailure<TInput, TOutput>(
        OperationResult<TInput> result) =>
        new(default, result.Diagnostics, result.ExitCode);

    private sealed record RuntimeFileIdentity(
        string RelativePath,
        long ByteLength,
        string Sha256,
        string? SourcePath);

    private sealed record CandidateInstallSource(
        CandidateLayout Layout,
        BuildProvenance Provenance,
        string ContentDigest,
        IReadOnlyList<RuntimeFileIdentity> RuntimeFiles);

    private sealed record CandidateReceiptRequest(string Path, string Version);

    private sealed class InstallationSourceException(string message) :
        Exception(message);

    private sealed record InstallLayout(
        string UserDataRoot,
        string ModsRoot,
        string TargetRoot,
        string Destination,
        string StaticId,
        string ManagedDirectoryName,
        InstallTarget Target)
    {
        internal string CreateTransientPath(string kind, Guid suffix)
        {
            if (kind is not ("staging" or "backup"))
            {
                throw new ArgumentException(
                    "Install transient kind must be staging or backup.",
                    nameof(kind));
            }

            var path = Path.GetFullPath(Path.Combine(
                TargetRoot,
                $".{ManagedDirectoryName}.{kind}-{suffix:N}"));
            EnsureStrictDescendant(TargetRoot, path, "install target root");
            return path;
        }

        internal bool IsOwnedTransientPath(string path, string kind)
        {
            var resolved = Path.GetFullPath(path);
            if (!HostPathComparer.Equals(
                Path.GetDirectoryName(resolved),
                TargetRoot))
            {
                return false;
            }

            var prefix = $".{ManagedDirectoryName}.{kind}-";
            var name = Path.GetFileName(resolved);
            return name.StartsWith(prefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(name[prefix.Length..], "N", out _);
        }
    }
}
