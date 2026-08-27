using System.Text.RegularExpressions;

namespace MaksymShostak.OniModPipeline.ModBuild;

internal static partial class MsBuildPropertyArgument
{
    internal static string Create(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);

        if (!PropertyNamePattern().IsMatch(name))
        {
            throw new ArgumentException(
                "MSBuild property names must match ^[A-Za-z_][A-Za-z0-9_]*$.",
                nameof(name));
        }

        if (value.Contains('"') || value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "MSBuild property values must not contain control characters or double quotes.",
                nameof(value));
        }

        var escapedValue = value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace(";", "%3B", StringComparison.Ordinal);
        return $"-p:{name}=\"{escapedValue}\"";
    }

    [GeneratedRegex(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PropertyNamePattern();
}
