using System.Security.Cryptography;
using System.Text;

namespace MaksymShostak.OniModPipeline.WorkshopListing;

internal sealed class ListingTextRenderer
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal RenderedListingText Render(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var segments = ParseLogicalSegments(text);
        while (segments.Count > 0 && segments[^1].Length == 0)
        {
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count == 0)
        {
            segments.Add(string.Empty);
        }

        segments.Add(string.Empty);
        var logicalText = string.Join('\n', segments);
        var artifactText = string.Join("\r\n", segments);
        var logicalBytes = StrictUtf8.GetBytes(logicalText);
        var artifactBytes = StrictUtf8.GetBytes(artifactText);
        VerifyArtifact(artifactBytes);

        var lineBreakCount = segments.Count - 1;
        var blankLineCount = segments
            .Take(segments.Count - 1)
            .Count(segment => segment.Length == 0);
        return new RenderedListingText(
            artifactBytes,
            new ListingTextReport(
                "utf-8",
                false,
                "crlf",
                segments.Count,
                lineBreakCount,
                blankLineCount,
                artifactBytes.LongLength,
                Hash(logicalBytes),
                Hash(artifactBytes)));
    }

    private static List<string> ParseLogicalSegments(string text)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is not ('\r' or '\n'))
            {
                current.Append(character);
                continue;
            }

            if (character == '\r' &&
                index + 1 < text.Length &&
                text[index + 1] == '\n')
            {
                index++;
            }

            segments.Add(current.ToString());
            current.Clear();
        }

        segments.Add(current.ToString());
        return segments;
    }

    private static void VerifyArtifact(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ||
            !bytes.EndsWith("\r\n"u8) ||
            bytes.EndsWith("\r\n\r\n"u8))
        {
            throw new InvalidOperationException(
                "Rendered Workshop text must be BOM-free UTF-8 with exactly one final CRLF.");
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\r' &&
                (index + 1 >= bytes.Length || bytes[index + 1] != (byte)'\n'))
            {
                throw new InvalidOperationException(
                    "Rendered Workshop text contains a lone CR character.");
            }

            if (bytes[index] == (byte)'\n' &&
                (index == 0 || bytes[index - 1] != (byte)'\r'))
            {
                throw new InvalidOperationException(
                    "Rendered Workshop text contains a lone LF character.");
            }
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
