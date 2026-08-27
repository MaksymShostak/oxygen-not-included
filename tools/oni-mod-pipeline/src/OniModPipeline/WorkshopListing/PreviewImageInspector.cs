using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.WorkshopListing;

internal sealed record PreviewImageInspection(
    string Format,
    string CandidateExtension,
    long ByteLength);

internal sealed class PreviewImageInspector
{
    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    internal OperationResult<PreviewImageInspection> Inspect(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var path = Path.GetFullPath(absolutePath);
        if (!File.Exists(path))
        {
            return Failure($"preview file '{path}' does not exist.");
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".gif"))
        {
            return Failure(
                $"extension '{extension}' is unsupported; use PNG, JPEG, GIF87a, or GIF89a.");
        }

        Span<byte> header = stackalloc byte[8];
        int read;
        long byteLength;
        using (var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8,
            FileOptions.SequentialScan))
        {
            read = stream.Read(header);
            byteLength = stream.Length;
        }

        var signature = header[..read];
        var detected = Detect(signature);
        if (detected is null)
        {
            return Failure("file signature is not PNG, JPEG, GIF87a, or GIF89a.");
        }

        var extensionMatches = detected.Value.Format switch
        {
            "png" => extension == ".png",
            "jpeg" => extension is ".jpg" or ".jpeg",
            "gif" => extension == ".gif",
            _ => false
        };
        if (!extensionMatches)
        {
            return Failure(
                $"extension '{extension}' does not agree with the detected {detected.Value.Format} signature.");
        }

        return new OperationResult<PreviewImageInspection>(
            new PreviewImageInspection(
                detected.Value.Format,
                detected.Value.CandidateExtension,
                byteLength),
            [],
            PipelineExitCode.Success);
    }

    private static (string Format, string CandidateExtension)? Detect(
        ReadOnlySpan<byte> signature)
    {
        if (signature.StartsWith(PngSignature))
        {
            return ("png", ".png");
        }

        if (signature.StartsWith(JpegSignature))
        {
            return ("jpeg", ".jpg");
        }

        if (signature.StartsWith("GIF87a"u8) || signature.StartsWith("GIF89a"u8))
        {
            return ("gif", ".gif");
        }

        return null;
    }

    private static OperationResult<PreviewImageInspection> Failure(string reason) =>
        new(
            null,
            [DiagnosticCatalog.InvalidWorkshopListing("workshop-listing.preview", reason)],
            PipelineExitCode.InvalidInput);
}
