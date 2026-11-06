#nullable enable

using System.Globalization;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class WorkshopListingCharacterContractTests
{
    [Test]
    public async Task ChangeNotes_WhenInspected_ContainsOnlyAsciiAndExplicitlyAllowedCharacters()
    {
        string modRoot = ResolveModRoot();
        string changeNotesPath = Path.Combine(modRoot, "STEAM_CHANGE_NOTES.bbcode");
        await Assert.That(File.Exists(changeNotesPath)).IsTrue().Because("STEAM_CHANGE_NOTES.bbcode must exist.");

        string content = File.ReadAllText(changeNotesPath);
        var violations = FindDisallowedCharacters(content, allowIcons: false);

        await Assert.That(violations.Count).IsEqualTo(0).Because($"STEAM_CHANGE_NOTES.bbcode contains disallowed characters:\n{string.Join("\n", violations)}");
    }

    [Test]
    public async Task WorkshopDescription_WhenInspected_ContainsOnlyAsciiAndAllowedCharactersOrIcons()
    {
        string modRoot = ResolveModRoot();
        string descriptionPath = Path.Combine(modRoot, "STEAM_DESCRIPTION.bbcode");
        await Assert.That(File.Exists(descriptionPath)).IsTrue().Because("STEAM_DESCRIPTION.bbcode must exist.");

        string content = File.ReadAllText(descriptionPath);
        var violations = FindDisallowedCharacters(content, allowIcons: true);

        await Assert.That(violations.Count).IsEqualTo(0).Because($"STEAM_DESCRIPTION.bbcode contains disallowed characters:\n{string.Join("\n", violations)}");
    }

    [Test]
    public async Task ExplicitlyAllowedCharacters_WhenInspected_ContainsDegreeSign()
    {
        await Assert.That(ExplicitlyAllowedCharacters.Contains(0x00B0)).IsTrue().Because("Explicitly allowed characters must contain degree sign (U+00B0).");
    }

    [Test]
    public async Task IsAllowedSymbolOrEmoji_WhenTested_AcceptsStandardIconsAndRejectsTypography()
    {
        // Standard icons and emojis should be permitted
        await Assert.That(IsAllowedSymbolOrEmoji(0x1F3AE)).IsTrue(); // 🎮
        await Assert.That(IsAllowedSymbolOrEmoji(0x1F680)).IsTrue(); // 🚀
        await Assert.That(IsAllowedSymbolOrEmoji(0x26A1)).IsTrue();  // ⚡
        await Assert.That(IsAllowedSymbolOrEmoji(0x1F310)).IsTrue(); // 🌐
        await Assert.That(IsAllowedSymbolOrEmoji(0x1F41B)).IsTrue(); // 🐛
        await Assert.That(IsAllowedSymbolOrEmoji(0x2744)).IsTrue();  // ❄
        await Assert.That(IsAllowedSymbolOrEmoji(0xFE0F)).IsTrue();  // Variation Selector-16
        await Assert.That(IsAllowedSymbolOrEmoji(0x00B0)).IsTrue();  // ° (degree sign)

        // Non-ASCII typographic punctuation and letters must be rejected
        await Assert.That(IsAllowedSymbolOrEmoji(0x2014)).IsFalse(); // — (em dash)
        await Assert.That(IsAllowedSymbolOrEmoji(0x2013)).IsFalse(); // – (en dash)
        await Assert.That(IsAllowedSymbolOrEmoji(0x201C)).IsFalse(); // “ (left double quote)
        await Assert.That(IsAllowedSymbolOrEmoji(0x201D)).IsFalse(); // ” (right double quote)
        await Assert.That(IsAllowedSymbolOrEmoji(0x2018)).IsFalse(); // ‘ (left single quote)
        await Assert.That(IsAllowedSymbolOrEmoji(0x2019)).IsFalse(); // ’ (right single quote)
        await Assert.That(IsAllowedSymbolOrEmoji(0x00A0)).IsFalse(); // non-breaking space
        await Assert.That(IsAllowedSymbolOrEmoji(0x00E9)).IsFalse(); // é
    }

    private static readonly HashSet<int> ExplicitlyAllowedCharacters =
    [
        0x00B0, // ° (degree sign)
    ];

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CharacterValidation_AllowsDegreeSignsAndRejectsTypography(bool allowIcons)
    {
        await Assert.That(FindDisallowedCharacters("Temperature: 20\u00B0C / 68\u00B0F", allowIcons)).Count().IsEqualTo(0);
        await Assert.That(FindDisallowedCharacters("\u2014\u00A0\u00E9", allowIcons)).Count().IsEqualTo(3);
        await Assert.That(FindDisallowedCharacters("\u26A1", allowIcons)).Count().IsEqualTo(allowIcons ? 0 : 1);
    }

    private static bool IsAllowedSymbolOrEmoji(int codePoint)
    {
        if (ExplicitlyAllowedCharacters.Contains(codePoint))
        {
            return true;
        }

        // Variation Selectors
        if (codePoint is >= 0xFE00 and <= 0xFE0F)
        {
            return true;
        }

        // Standard Unicode Emoji & Symbol Blocks
        if (codePoint is >= 0x2600 and <= 0x27BF ||   // Misc Symbols & Dingbats (e.g. ⚡, ❄)
            codePoint is >= 0x2B00 and <= 0x2BFF ||   // Misc Symbols and Arrows
            codePoint is >= 0x1F300 and <= 0x1FAFF)   // Pictographs, Emojis, Transport, etc.
        {
            return true;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
        return category is UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol;
    }

    private static List<string> FindDisallowedCharacters(string text, bool allowIcons)
    {
        var violations = new List<string>();
        string[] lines = text.Split('\n');

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            for (int charIndex = 0; charIndex < line.Length; charIndex++)
            {
                int codePoint = char.ConvertToUtf32(line, charIndex);
                if (char.IsSurrogate(line[charIndex]))
                {
                    charIndex++; // Skip the trailing surrogate pair character
                }

                if (codePoint <= 127 || ExplicitlyAllowedCharacters.Contains(codePoint))
                {
                    continue;
                }

                if (allowIcons && IsAllowedSymbolOrEmoji(codePoint))
                {
                    continue;
                }

                violations.Add(
                    $"Line {lineIndex + 1}, Col {charIndex + 1}: Character '{char.ConvertFromUtf32(codePoint)}' (U+{codePoint:X4}) in \"{line.Trim()}\"");
            }
        }

        return violations;
    }

    private static string ResolveModRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(
                directory,
                "mods",
                "delivery-temperature-limit-supercooled");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new DirectoryNotFoundException(
            "Could not resolve the delivery-temperature-limit-supercooled directory.");
    }
}
