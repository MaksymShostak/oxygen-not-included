using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Globbing;

namespace MaksymShostak.OniModPipeline.SourceControl;

/// <summary>Reads static SDK item declarations without evaluating a project or executing its imports.</summary>
internal sealed class SdkProjectInputs
{
    [Flags]
    private enum ItemKind { Compile = 1, Resource = 2, CompileBefore = 4, CompileAfter = 8 }

    private sealed record Rule(ItemKind Kind, bool Add, MSBuildGlob Pattern, MSBuildGlob[] Excludes);

    private readonly List<Rule> rules = [];
    private readonly List<MSBuildGlob> includes = [];
    private MSBuildGlob? defaultCompile;
    private MSBuildGlob? defaultResources;

    internal IReadOnlyList<MSBuildGlob> DeclaredIncludes => includes;

    internal bool Includes(string path, bool useDefaults)
    {
        ItemKind selected = 0;
        if (useDefaults && defaultCompile?.IsMatch(path) == true) selected |= ItemKind.Compile;
        if (useDefaults && defaultResources?.IsMatch(path) == true) selected |= ItemKind.Resource;
        foreach (var rule in rules)
        {
            if (rule.Pattern.IsMatch(path) && !Array.Exists(rule.Excludes, exclude => exclude.IsMatch(path)))
                selected = rule.Add ? selected | rule.Kind : selected & ~rule.Kind;
        }
        return selected != 0;
    }

    internal static SdkProjectInputs Read(string projectFile, string language)
    {
        using var reader = XmlReader.Create(projectFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var project = XDocument.Load(reader).Root ?? throw new InvalidDataException($"Empty project: {projectFile}");
        string root = Path.GetDirectoryName(projectFile)!;
        var result = new SdkProjectInputs();
        bool defaults = Property("EnableDefaultItems", true);
        if (defaults && Property("EnableDefaultCompileItems", language != ".fs"))
            result.defaultCompile = Glob("**/*" + language);
        if (defaults && Property("EnableDefaultEmbeddedResourceItems", true))
            result.defaultResources = Glob("**/*.resx");

        foreach (var group in project.Elements().Where(element => element.Name.LocalName == "ItemGroup"))
        {
            foreach (var item in group.Elements())
            {
                var kind = item.Name.LocalName switch
                {
                    "Compile" => ItemKind.Compile,
                    "EmbeddedResource" => ItemKind.Resource,
                    "CompileBefore" => ItemKind.CompileBefore,
                    "CompileAfter" => ItemKind.CompileAfter,
                    _ => (ItemKind)0
                };
                if (kind == 0) continue;
                var excludes = StaticPatterns(item.Attribute("Exclude")).Select(Glob).ToArray();
                foreach (var include in StaticPatterns(item.Attribute("Include")).Select(Glob))
                {
                    result.includes.Add(include);
                    result.rules.Add(new(kind, true, include, excludes));
                }
                // A conditional removal cannot rule out an input for all configurations.
                if (group.Attribute("Condition") is null && item.Attribute("Condition") is null)
                    foreach (var remove in StaticPatterns(item.Attribute("Remove")).Select(Glob))
                        result.rules.Add(new(kind, false, remove, []));
            }
        }
        return result;

        MSBuildGlob Glob(string pattern)
        {
            var glob = MSBuildGlob.Parse(root, pattern);
            return glob.IsLegal ? glob : throw new InvalidDataException($"Invalid item pattern in '{projectFile}': {pattern}");
        }

        bool Property(string name, bool fallback)
        {
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
