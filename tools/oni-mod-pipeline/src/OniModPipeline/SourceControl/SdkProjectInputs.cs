using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace MaksymShostak.OniModPipeline.SourceControl;

/// <summary>Reads ordinary SDK file declarations as data, without evaluating project code or loading imports.</summary>
internal sealed class SdkProjectInputs
{
    internal sealed record Item(string Include, IReadOnlyList<string> Excludes);

    private readonly Matcher defaultCompile = new(StringComparison.OrdinalIgnoreCase);
    private readonly Matcher defaultResources = new(StringComparison.OrdinalIgnoreCase);
    internal List<Item> DeclaredItems { get; } = [];

    internal bool IncludesDefault(string relativePath)
    {
        string path = relativePath.Replace('\\', '/');
        return defaultCompile.Match(path).HasMatches || defaultResources.Match(path).HasMatches;
    }

    internal static SdkProjectInputs Read(string projectFile, string language)
    {
        using var reader = XmlReader.Create(projectFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var project = XDocument.Load(reader).Root ?? throw new InvalidDataException($"Empty project: {projectFile}");
        var result = new SdkProjectInputs();
        bool defaultsEnabled = Property("EnableDefaultItems", true);
        if (defaultsEnabled && Property("EnableDefaultCompileItems", language != ".fs"))
            result.defaultCompile.AddInclude("**/*" + language);
        if (defaultsEnabled && Property("EnableDefaultEmbeddedResourceItems", true))
            result.defaultResources.AddInclude("**/*.resx");

        foreach (var group in project.Elements().Where(element => element.Name.LocalName == "ItemGroup"))
        {
            foreach (var item in group.Elements().Where(element => element.Name.LocalName is "Compile" or "EmbeddedResource" or "CompileBefore" or "CompileAfter"))
            {
                if (group.Attribute("Condition") is null && item.Attribute("Condition") is null)
                    foreach (var pattern in StaticPatterns(item.Attribute("Remove")))
                        (item.Name.LocalName == "EmbeddedResource" ? result.defaultResources : result.defaultCompile).AddExclude(pattern);
                var excludes = StaticPatterns(item.Attribute("Exclude")).ToArray();
                foreach (var include in StaticPatterns(item.Attribute("Include")))
                    result.DeclaredItems.Add(new(include, excludes));
            }
        }
        return result;

        bool Property(string name, bool fallback)
        {
            // A conditional value may depend on configuration: only an unconditional false can disable the default.
            bool value = fallback;
            foreach (var group in project.Elements().Where(element => element.Name.LocalName == "PropertyGroup"))
                foreach (var property in group.Elements().Where(element => element.Name.LocalName == name))
                    if (bool.TryParse(property.Value.Trim(), out bool enabled) &&
                        (enabled || (group.Attribute("Condition") is null && property.Attribute("Condition") is null)))
                        value = enabled;
            return value;
        }
    }

    private static IEnumerable<string> StaticPatterns(XAttribute? attribute) =>
        (attribute?.Value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pattern => !pattern.Contains("$(", StringComparison.Ordinal) && !pattern.Contains("@(", StringComparison.Ordinal) &&
                !pattern.Contains("%(", StringComparison.Ordinal))
            .Select(pattern => pattern.Replace('\\', '/'));
}
