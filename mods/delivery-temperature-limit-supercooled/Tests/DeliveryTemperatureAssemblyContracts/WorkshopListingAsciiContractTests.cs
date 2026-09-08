#nullable enable

using System.Globalization;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class WorkshopListingAsciiContractTests
{
    [TestMethod]
    public void ChangeNotes_WhenInspected_IsStrictlyAscii()
    {
        string modRoot = ResolveModRoot();
        string changeNotesPath = Path.Combine(modRoot, "STEAM_CHANGE_NOTES.bbcode");
        Assert.IsTrue(File.Exists(changeNotesPath), "STEAM_CHANGE_NOTES.bbcode must exist.");

        string content = File.ReadAllText(changeNotesPath);
        var violations = FindNonAsciiTextViolations(content, allowIcons: false);

        Assert.AreEqual(
            0,
            violations.Count,
            $"STEAM_CHANGE_NOTES.bbcode contains non-ASCII characters:\n{string.Join("\n", violations)}");
    }

    [TestMethod]
    public void WorkshopDescription_WhenInspected_ContainsOnlyAsciiTextAndAllowedIcons()
    {
        string modRoot = ResolveModRoot();
        string descriptionPath = Path.Combine(modRoot, "STEAM_DESCRIPTION.bbcode");
        Assert.IsTrue(File.Exists(descriptionPath), "STEAM_DESCRIPTION.bbcode must exist.");

        string content = File.ReadAllText(descriptionPath);
        var violations = FindNonAsciiTextViolations(content, allowIcons: true);

        Assert.AreEqual(
            0,
            violations.Count,
            $"STEAM_DESCRIPTION.bbcode contains non-ASCII text characters:\n{string.Join("\n", violations)}");
    }

    [TestMethod]
    public void ExplicitlyAllowedCharacters_WhenInspected_ContainsDegreeSign()
    {
        Assert.IsTrue(
            ExplicitlyAllowedCharacters.Contains(0x00B0),
            "Explicitly allowed characters must contain degree sign (U+00B0).");
    }

    [TestMethod]
    public void IsAllowedSymbolOrEmoji_WhenTested_AcceptsStandardIconsAndRejectsTypography()
    {
        // Standard icons and emojis should be permitted
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x1F3AE)); // 🎮
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x1F680)); // 🚀
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x26A1));  // ⚡
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x1F310)); // 🌐
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x1F41B)); // 🐛
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x2744));  // ❄
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0xFE0F));  // Variation Selector-16
        Assert.IsTrue(IsAllowedSymbolOrEmoji(0x00B0));  // ° (degree sign)

        // Non-ASCII typographic punctuation and letters must be rejected
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x2014)); // — (em dash)
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x2013)); // – (en dash)
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x201C)); // “ (left double quote)
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x201D)); // ” (right double quote)
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x2018)); // ‘ (left single quote)
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x2019)); // ’ (right single quote)
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x00A0)); // non-breaking space
        Assert.IsFalse(IsAllowedSymbolOrEmoji(0x00E9)); // é
    }

    private static readonly HashSet<int> ExplicitlyAllowedCharacters =
    [
        0x00B0, // ° (degree sign)
    ];

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

    private static List<string> FindNonAsciiTextViolations(string text, bool allowIcons)
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

                if (codePoint <= 127)
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
