using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using System.Globalization;
using System.Text;

namespace MaksymShostak.OniModPipeline.WorkshopListing;

internal sealed record WorkshopListingValidation(
    string DescriptionSourcePath,
    RenderedListingText Description,
    string ChangeNotesSourcePath,
    RenderedListingText ChangeNotes,
    string PreviewSourcePath,
    PreviewImageInspection Preview,
    IReadOnlyList<string> ModTypeLabels,
    IReadOnlyList<string> DlcLabels);

internal sealed class WorkshopListingValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly ISet<string> ReservedWholeFileValues = new HashSet<string>(
        ["TODO", "TBD", "CHANGEME", "ONI_PIPELINE_CHANGE_NOTES_REQUIRED"],
        StringComparer.OrdinalIgnoreCase);

    private readonly ListingTextRenderer renderer;
    private readonly BbCodeValidator bbCodeValidator;
    private readonly PreviewImageInspector previewInspector;

    internal WorkshopListingValidator()
        : this(
            new ListingTextRenderer(),
            new BbCodeValidator(),
            new PreviewImageInspector())
    {
    }

    internal WorkshopListingValidator(
        ListingTextRenderer renderer,
        BbCodeValidator bbCodeValidator,
        PreviewImageInspector previewInspector)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(bbCodeValidator);
        ArgumentNullException.ThrowIfNull(previewInspector);
        this.renderer = renderer;
        this.bbCodeValidator = bbCodeValidator;
        this.previewInspector = previewInspector;
    }

    internal OperationResult<RenderedListingText> ValidateText(
        string field,
        ReadOnlySpan<byte> sourceBytes,
        int byteLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        if (byteLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLimit));
        }

        var diagnostics = new List<Diagnostic>();
        if (sourceBytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            diagnostics.Add(Invalid(field, "source UTF-8 must not contain a BOM."));
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(sourceBytes);
        }
        catch (DecoderFallbackException exception)
        {
            diagnostics.Add(Invalid(field, $"source bytes are not valid UTF-8: {exception.Message}"));
            return Failure<RenderedListingText>(diagnostics);
        }

        if (text.Contains('\r'))
        {
            diagnostics.Add(Invalid(field, "source text must use LF line endings only."));
        }

        if (!text.EndsWith('\n') || text.EndsWith("\n\n", StringComparison.Ordinal))
        {
            diagnostics.Add(Invalid(field, "source text must have exactly one final LF."));
        }

        if (text.Contains('\0'))
        {
            diagnostics.Add(Invalid(field, "source text must not contain NUL characters."));
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            diagnostics.Add(Invalid(field, "source text must not be empty."));
        }
        else if (ReservedWholeFileValues.Contains(trimmed))
        {
            diagnostics.Add(Invalid(field, $"reserved placeholder '{trimmed}' must be replaced."));
        }

        RenderedListingText rendered;
        try
        {
            rendered = renderer.Render(text);
        }
        catch (EncoderFallbackException exception)
        {
            diagnostics.Add(Invalid(field, $"source text is not valid Unicode: {exception.Message}"));
            return Failure<RenderedListingText>(diagnostics);
        }

        if (rendered.Report.Utf8ByteCount > byteLimit)
        {
            diagnostics.Add(Invalid(
                field,
                $"rendered UTF-8 is {rendered.Report.Utf8ByteCount.ToString("N0", CultureInfo.InvariantCulture)} bytes; the limit is {byteLimit.ToString("N0", CultureInfo.InvariantCulture)} bytes."));
        }

        foreach (var reason in bbCodeValidator.Validate(text))
        {
            diagnostics.Add(Invalid(field, reason));
        }

        return diagnostics.Count == 0
            ? new OperationResult<RenderedListingText>(
                rendered,
                [],
                PipelineExitCode.Success)
            : Failure<RenderedListingText>(diagnostics);
    }

    internal async Task<OperationResult<WorkshopListingValidation>> ValidateAsync(
        ModProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<Diagnostic>();
        var descriptionPath = Resolve(
            profile.ModRoot,
            profile.WorkshopListing.Description,
            diagnostics);
        var changeNotesPath = Resolve(
            profile.ModRoot,
            profile.WorkshopListing.ChangeNotes,
            diagnostics);
        var previewPath = Resolve(
            profile.ModRoot,
            profile.WorkshopListing.Preview,
            diagnostics);
        var modTypeLabels = MapIdentifiers(
            "workshop-listing.mod-types",
            profile.WorkshopListing.ModTypes,
            WorkshopListingVocabulary.ModTypeLabels,
            diagnostics);
        var dlcLabels = MapIdentifiers(
            "workshop-listing.dlc-compatibility",
            profile.WorkshopListing.DlcCompatibility,
            WorkshopListingVocabulary.DlcLabels,
            diagnostics);
        if (diagnostics.Count > 0)
        {
            return Failure<WorkshopListingValidation>(diagnostics);
        }

        var descriptionResult = ValidateText(
            "workshop-listing.description",
            await File.ReadAllBytesAsync(descriptionPath!, cancellationToken).ConfigureAwait(false),
            profile.WorkshopListing.DescriptionByteLimit);
        var changeNotesResult = ValidateText(
            "workshop-listing.change-notes",
            await File.ReadAllBytesAsync(changeNotesPath!, cancellationToken).ConfigureAwait(false),
            profile.WorkshopListing.ChangeNotesByteLimit);
        var previewResult = previewInspector.Inspect(previewPath!);
        diagnostics.AddRange(descriptionResult.Diagnostics);
        diagnostics.AddRange(changeNotesResult.Diagnostics);
        diagnostics.AddRange(previewResult.Diagnostics);
        if (diagnostics.Count > 0)
        {
            return Failure<WorkshopListingValidation>(diagnostics);
        }

        return new OperationResult<WorkshopListingValidation>(
            new WorkshopListingValidation(
                descriptionPath!,
                descriptionResult.Value!,
                changeNotesPath!,
                changeNotesResult.Value!,
                previewPath!,
                previewResult.Value!,
                modTypeLabels,
                dlcLabels),
            [],
            PipelineExitCode.Success);
    }

    private static string? Resolve(
        string root,
        string declaredPath,
        ICollection<Diagnostic> diagnostics)
    {
        var result = ContainedPathResolver.ResolveExistingFile(root, declaredPath);
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }

        return result.Value;
    }

    private static IReadOnlyList<string> MapIdentifiers(
        string field,
        IReadOnlyList<string> identifiers,
        IReadOnlyDictionary<string, string> labels,
        ICollection<Diagnostic> diagnostics)
    {
        var mapped = new List<string>(identifiers.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (identifiers.Count == 0)
        {
            diagnostics.Add(Invalid(field, "at least one identifier is required."));
        }

        foreach (var identifier in identifiers)
        {
            if (!labels.TryGetValue(identifier, out var label))
            {
                diagnostics.Add(Invalid(field, $"unknown identifier '{identifier}'."));
                continue;
            }

            if (!seen.Add(identifier))
            {
                diagnostics.Add(Invalid(field, $"identifier '{identifier}' is duplicated."));
                continue;
            }

            mapped.Add(label);
        }

        return mapped;
    }

    private static Diagnostic Invalid(string field, string reason) =>
        DiagnosticCatalog.InvalidWorkshopListing(field, reason);

    private static OperationResult<T> Failure<T>(IReadOnlyList<Diagnostic> diagnostics) =>
        new(default, diagnostics, PipelineExitCode.InvalidInput);
}
