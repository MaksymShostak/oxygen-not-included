namespace MaksymShostak.OniModPipeline.Readme;

/// <summary>Supply domain labels for known compatibility images in the generated description.</summary>
internal static class CompatibilityBadgeLabels
{
    private const string BadgeBase = "https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/";
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["VanillaYes.png"] = "Base game supported",
        ["Dlc1Yes.png"] = "Spaced Out! supported",
        ["Dlc2Yes.png"] = "The Frosty Planet Pack supported",
        ["Dlc3Yes.png"] = "The Bionic Booster Pack supported",
        ["Dlc4Yes.png"] = "The Prehistoric Planet Pack supported",
        ["Dlc5Yes.png"] = "The Aquatic Planet Pack supported"
    };

    internal static string RenderGeneratedDescription(string markdown)
    {
        foreach (var (file, label) in Labels)
            markdown = markdown.Replace($"![]({BadgeBase}{file})", $"![{label}]({BadgeBase}{file})", StringComparison.Ordinal);
        return markdown;
    }
}
