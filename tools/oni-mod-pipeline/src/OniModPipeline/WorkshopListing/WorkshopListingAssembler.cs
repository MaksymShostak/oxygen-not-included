using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.WorkshopListing;

internal sealed record WorkshopListingAssembly(
    string DescriptionPath,
    string ChangeNotesPath,
    string PreviewPath,
    ListingTextReport DescriptionReport,
    ListingTextReport ChangeNotesReport,
    PreviewImageInspection Preview,
    IReadOnlyList<FileDigest> Files,
    IReadOnlyList<string> ModTypeLabels,
    IReadOnlyList<string> DlcLabels);

internal sealed class WorkshopListingAssembler
{
    private readonly WorkshopListingValidator validator;
    private readonly ContentHasher contentHasher;

    internal WorkshopListingAssembler()
        : this(new WorkshopListingValidator(), new ContentHasher())
    {
    }

    internal WorkshopListingAssembler(
        WorkshopListingValidator validator,
        ContentHasher contentHasher)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(contentHasher);
        this.validator = validator;
        this.contentHasher = contentHasher;
    }

    internal async Task<OperationResult<WorkshopListingAssembly>> AssembleAsync(
        ModProfile profile,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var target = Path.GetFullPath(targetDirectory);
        var targetValidation = ValidateEmptyTarget(target);
        if (targetValidation is not null)
        {
            return Failure(targetValidation);
        }

        var validation = await validator
            .ValidateAsync(profile, cancellationToken)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
        {
            return new OperationResult<WorkshopListingAssembly>(
                null,
                validation.Diagnostics,
                validation.ExitCode);
        }

        var source = validation.Value!;
        var descriptionPath = Path.Combine(target, "description.bbcode");
        var changeNotesPath = Path.Combine(target, "change-notes.bbcode");
        var previewPath = Path.Combine(target, $"preview{source.Preview.CandidateExtension}");
        var createdPaths = new List<string>(3);
        try
        {
            await WriteNewAsync(
                descriptionPath,
                source.Description.Bytes,
                cancellationToken).ConfigureAwait(false);
            createdPaths.Add(descriptionPath);
            await WriteNewAsync(
                changeNotesPath,
                source.ChangeNotes.Bytes,
                cancellationToken).ConfigureAwait(false);
            createdPaths.Add(changeNotesPath);
            await CopyNewAsync(
                source.PreviewSourcePath,
                previewPath,
                cancellationToken).ConfigureAwait(false);
            createdPaths.Add(previewPath);

            var sourcePreviewDigest = await contentHasher
                .HashFileAsync(source.PreviewSourcePath, cancellationToken)
                .ConfigureAwait(false);
            var files = new[]
            {
                await contentHasher.HashFileAsync(descriptionPath, cancellationToken)
                    .ConfigureAwait(false),
                await contentHasher.HashFileAsync(changeNotesPath, cancellationToken)
                    .ConfigureAwait(false),
                await contentHasher.HashFileAsync(previewPath, cancellationToken)
                    .ConfigureAwait(false)
            };
            if (files[2].ByteLength != sourcePreviewDigest.ByteLength ||
                !string.Equals(
                    files[2].Sha256,
                    sourcePreviewDigest.Sha256,
                    StringComparison.Ordinal))
            {
                Cleanup(createdPaths);
                return Failure(DiagnosticCatalog.InvalidWorkshopListing(
                    "workshop-listing.preview",
                    "candidate preview bytes do not match the validated source bytes."));
            }

            return new OperationResult<WorkshopListingAssembly>(
                new WorkshopListingAssembly(
                    descriptionPath,
                    changeNotesPath,
                    previewPath,
                    source.Description.Report,
                    source.ChangeNotes.Report,
                    source.Preview,
                    files,
                    source.ModTypeLabels,
                    source.DlcLabels),
                [],
                PipelineExitCode.Success);
        }
        catch
        {
            Cleanup(createdPaths);
            throw;
        }
    }

    private static Diagnostic? ValidateEmptyTarget(string target)
    {
        if (!Directory.Exists(target))
        {
            return DiagnosticCatalog.InvalidWorkshopListing(
                "workshop-listing.target-directory",
                $"target directory '{target}' must already exist and be empty.");
        }

        var info = new DirectoryInfo(target);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            info.LinkTarget is not null)
        {
            return DiagnosticCatalog.InvalidWorkshopListing(
                "workshop-listing.target-directory",
                $"target directory '{target}' must not be a link or reparse point.");
        }

        if (!string.Equals(info.Name, "workshop-listing", StringComparison.Ordinal))
        {
            return DiagnosticCatalog.InvalidWorkshopListing(
                "workshop-listing.target-directory",
                "candidate listing directory must be named exactly 'workshop-listing'.");
        }

        if (Directory.EnumerateFileSystemEntries(target).Any())
        {
            return DiagnosticCatalog.InvalidWorkshopListing(
                "workshop-listing.target-directory",
                $"target directory '{target}' must be empty.");
        }

        return null;
    }

    private static async Task WriteNewAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        var created = false;
        try
        {
            await using var stream = new FileStream(
                path,
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
            if (created && File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }
    }

    private static async Task CopyNewAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var created = false;
        try
        {
            await using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = new FileStream(
                destinationPath,
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

    private static void Cleanup(IEnumerable<string> createdPaths)
    {
        foreach (var path in createdPaths.Reverse())
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static OperationResult<WorkshopListingAssembly> Failure(
        Diagnostic diagnostic) =>
        new(null, [diagnostic], PipelineExitCode.InvalidInput);
}
