using System.Text.RegularExpressions;

namespace MaksymShostak.OniModPipeline.WorkshopListing;

internal sealed class BbCodeValidator
{
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[[^\]\r\n]+\]\([^\)\r\n]+\)",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex TagPattern = new(
        @"\[(?<closing>/)?(?<name>\*|[A-Za-z][A-Za-z0-9-]*)(?<attribute>=[^\]\r\n]*)?\]",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly ISet<string> PairedTags = new HashSet<string>(
        ["b", "i", "u", "strike", "spoiler", "h1", "h2", "h3", "url", "list", "quote"],
        StringComparer.Ordinal);

    internal IReadOnlyList<string> Validate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var reasons = new List<string>();
        if (MarkdownLinkPattern.IsMatch(text))
        {
            reasons.Add("Markdown link syntax is not supported; use [url=http-or-https] text [/url].");
        }

        var stack = new Stack<TagFrame>();
        foreach (Match match in TagPattern.Matches(text))
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            if (name == "*")
            {
                if (!stack.Any(frame => frame.Name == "list"))
                {
                    reasons.Add("A [*] list item must be contained within a [list] block.");
                }

                continue;
            }

            if (!PairedTags.Contains(name))
            {
                continue;
            }

            var isClosing = match.Groups["closing"].Success;
            var attribute = match.Groups["attribute"];
            if (isClosing)
            {
                if (attribute.Success)
                {
                    reasons.Add($"Closing BBCode tag '[/{name}]' must not have an attribute.");
                }

                if (!stack.TryPeek(out var frame))
                {
                    reasons.Add($"Closing BBCode tag '[/{name}]' has no matching opening tag.");
                    continue;
                }

                if (frame.Name != name)
                {
                    reasons.Add(
                        $"Closing BBCode tag '[/{name}]' crosses the open '[{frame.Name}]' tag.");
                    continue;
                }

                stack.Pop();
                if (name == "url" && frame.UrlTarget is null)
                {
                    ValidateUrl(text[frame.ContentStart..match.Index].Trim(), reasons);
                }

                continue;
            }

            string? urlTarget = null;
            if (name == "url")
            {
                if (attribute.Success)
                {
                    urlTarget = attribute.Value[1..].Trim();
                    ValidateUrl(urlTarget, reasons);
                }
            }
            else if (attribute.Success)
            {
                reasons.Add($"BBCode tag '[{name}]' does not support attributes.");
            }

            stack.Push(new TagFrame(name, match.Index + match.Length, urlTarget));
        }

        foreach (var frame in stack)
        {
            reasons.Add($"Opening BBCode tag '[{frame.Name}]' is not closed.");
        }

        return reasons;
    }

    private static void ValidateUrl(string value, ICollection<string> reasons)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("BBCode URL targets must use an absolute http or https URL.");
        }
    }

    private sealed record TagFrame(
        string Name,
        int ContentStart,
        string? UrlTarget);
}
