using System.Text;

namespace MaksymShostak.OniModPipeline.Readme;

internal static class ReadmeDescriptionBlock
{
    private const string Start = "<!-- oni-mod-pipeline:workshop-description:start -->";
    private const string End = "<!-- oni-mod-pipeline:workshop-description:end -->";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] Replace(byte[] original, string markdown)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(markdown);
        var text = StrictUtf8.GetString(original);
        var start = FindSingleStandaloneMarker(text, Start);
        var end = FindSingleStandaloneMarker(text, End);
        var afterStart = start + Start.Length;
        if (end <= afterStart || afterStart == text.Length ||
            markdown.Contains(Start, StringComparison.Ordinal) || markdown.Contains(End, StringComparison.Ordinal))
        {
            throw new InvalidDataException("README must contain one ordered marker pair; generated content cannot contain markers.");
        }

        var newline = text.AsSpan(afterStart).StartsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        afterStart += newline.Length;
        var content = markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').TrimEnd('\n').Replace("\n", newline, StringComparison.Ordinal);
        if (content.Length > 0)
        {
            content += newline;
        }

        return StrictUtf8.GetBytes(text[..afterStart] + content + text[end..]);
    }

    private static int FindSingleStandaloneMarker(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidDataException("README must contain exactly one of each description marker.");
        }

        var after = index + marker.Length;
        if ((index != 0 && text[index - 1] != '\n') ||
            (after != text.Length && text[after] != '\n' &&
                !text.AsSpan(after).StartsWith("\r\n", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("README description markers must occupy standalone lines.");
        }

        return index;
    }
}
