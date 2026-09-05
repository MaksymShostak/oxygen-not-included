#nullable enable

using System.Globalization;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class WorkshopListingAsciiContractTests
{
    private static readonly HashSet<int> AllowedIconCodePoints = new()
    {
        0x1F3AE, // 🎮 Game controller
        0x1F680, // 🚀 Rocket
        0x26A1,  // ⚡ High voltage
        0x1F393, // 🎓 Graduation cap
        0x1F6E0, // 🛠 Hammer and wrench
        0xFE0F,  // Variation Selector-16
        0x1F41B  // 🐛 Bug
    };

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

                if (allowIcons && AllowedIconCodePoints.Contains(codePoint))
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
