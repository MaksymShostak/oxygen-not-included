using System.Collections.Frozen;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed record WorkshopListingProfile(
    string Description,
    string ChangeNotes,
    string Preview,
    IReadOnlyList<string> ModTypes,
    IReadOnlyList<string> DlcCompatibility,
    int DescriptionByteLimit,
    int ChangeNotesByteLimit);

internal static class WorkshopListingVocabulary
{
    internal static IReadOnlyDictionary<string, string> ModTypeLabels { get; } =
        new Dictionary<string, string>
        {
            ["language"] = "language",
            ["worldgen"] = "worldgen",
            ["new-features"] = "new features",
            ["tweaks"] = "tweaks",
            ["ui"] = "ui"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, string> DlcLabels { get; } =
        new Dictionary<string, string>
        {
            ["base-game"] = "Base Game",
            ["spaced-out"] = "Spaced Out!",
            ["frosty-planet-pack"] = "The Frosty Planet Pack",
            ["bionic-booster-pack"] = "The Bionic Booster Pack",
            ["prehistoric-planet-pack"] = "The Prehistoric Planet Pack",
            ["aquatic-planet-pack"] = "The Aquatic Planet Pack"
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
