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
    private bool localBinExcluded;
    private bool localObjExcluded;

    internal IReadOnlyList<MSBuildGlob> DeclaredIncludes => includes;
    internal bool HasDefaults => defaultCompile is not null || defaultResources is not null;

    internal bool IsExcludedLocalOutput(string relativePath)
    {
        int end = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        string first = end < 0 ? relativePath : relativePath[..end];
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return (localBinExcluded && first.Equals("bin", comparison)) || (localObjExcluded && first.Equals("obj", comparison));
    }

    private ItemKind removedKinds;
    internal bool Includes(string path, bool useDefaults, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ItemKind selected = 0;
        if (useDefaults && defaultCompile?.IsMatch(path) == true) selected |= ItemKind.Compile;
        if (useDefaults && defaultResources?.IsMatch(path) == true) selected |= ItemKind.Resource;
        if ((selected & ~removedKinds) != 0) return true;
        ItemKind decided = 0;
        // For each item type only its last applicable declaration can affect the answer.
        for (int index = rules.Count - 1; index >= 0; index--)
        {
            token.ThrowIfCancellationRequested();
            var rule = rules[index];
            if ((decided & rule.Kind) != 0 || !rule.Pattern.IsMatch(path) || IsExcluded(rule.Excludes))
                continue;
            if (rule.Add) return true;
            decided |= rule.Kind;
        }
        return (selected & ~decided) != 0;

        bool IsExcluded(MSBuildGlob[] excludes)
        {
            foreach (var exclude in excludes)
            {
                token.ThrowIfCancellationRequested();
                if (exclude.IsMatch(path)) return true;
            }
            return false;
        }
    }

    internal static SdkProjectInputs Read(string projectFile, string language, bool isolatedOutputs, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var reader = XmlReader.Create(projectFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var project = XDocument.Load(reader).Root ?? throw new InvalidDataException($"Empty project: {projectFile}");
        string root = Path.GetDirectoryName(projectFile)!;
        var compiled = new Dictionary<string, MSBuildGlob>(StringComparer.Ordinal);
        var positivePatterns = new HashSet<MSBuildGlob>();
        var lastRules = new Dictionary<(ItemKind, MSBuildGlob, string), (int Order, Rule Rule)>();
        int order = 0;
        var result = new SdkProjectInputs
        {
            localBinExcluded = ExcludesLocalOutput("bin", "BaseOutputPath"),
            localObjExcluded = ExcludesLocalOutput("obj", "BaseIntermediateOutputPath")
        };
        bool defaults = Property("EnableDefaultItems", true);
        if (defaults && Property("EnableDefaultCompileItems", language != ".fs"))
            result.defaultCompile = Glob("**/*" + language);
        if (defaults && Property("EnableDefaultEmbeddedResourceItems", true))
            result.defaultResources = Glob("**/*.resx");

        foreach (var group in project.Elements().Where(element => element.Name.LocalName == "ItemGroup"))
        {
            foreach (var item in group.Elements())
            {
                token.ThrowIfCancellationRequested();
                var kind = item.Name.LocalName switch
                {
                    "Compile" => ItemKind.Compile,
                    "EmbeddedResource" => ItemKind.Resource,
                    "CompileBefore" => ItemKind.CompileBefore,
                    "CompileAfter" => ItemKind.CompileAfter,
                    _ => (ItemKind)0
                };
                if (kind == 0) continue;
                var excludeSpecs = StaticPatterns(item.Attribute("Exclude")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var excludes = excludeSpecs.Select(Glob).ToArray();
                string excludeKey = string.Join('\u0000', excludeSpecs);
                foreach (var include in StaticPatterns(item.Attribute("Include")).Select(Glob))
                {
                    if (positivePatterns.Add(include)) result.includes.Add(include);
                    lastRules[(kind, include, excludeKey)] = (++order, new(kind, true, include, excludes));
                }
                // A conditional removal cannot rule out an input for all configurations.
                if (group.Attribute("Condition") is null && item.Attribute("Condition") is null)
                    foreach (var remove in StaticPatterns(item.Attribute("Remove")).Select(Glob))
                    {
                        result.removedKinds |= kind;
                        lastRules[(kind, remove, string.Empty)] = (++order, new(kind, false, remove, []));
                    }
            }
        }
        result.rules.AddRange(lastRules.Values.OrderBy(value => value.Order).Select(value => value.Rule));
        return result;

        MSBuildGlob Glob(string pattern)
        {
            token.ThrowIfCancellationRequested();
            if (compiled.TryGetValue(pattern, out var glob)) return glob;
            glob = MSBuildGlob.Parse(root, pattern);
            if (!glob.IsLegal) throw new InvalidDataException($"Invalid item pattern in '{projectFile}': {pattern}");
            compiled.Add(pattern, glob);
            return glob;
        }

        bool ExcludesLocalOutput(string name, string outputProperty)
        {
            // The isolated build relocates the SDK's output exclusions. A project-local folder
            // is then excluded only if the project independently retains that exclusion.
            var groups = project.Elements().Where(element => element.Name.LocalName == "PropertyGroup").ToArray();
            if (!isolatedOutputs && !groups.SelectMany(group => group.Elements()).Any(p => p.Name.LocalName == outputProperty))
                return true;
            bool excluded = false;
            foreach (var group in groups)
            {
                foreach (var property in group.Elements().Where(p => p.Name.LocalName == "DefaultItemExcludes"))
                {
                    string[] values = property.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (!values.Contains("$(DefaultItemExcludes)", StringComparer.OrdinalIgnoreCase)) excluded = false;
                    if (group.Attribute("Condition") is not null || property.Attribute("Condition") is not null) continue;
                    foreach (var value in values)
                    {
                        string pattern = value.Replace('\\', '/');
                        const string projectDirectory = "$(MSBuildProjectDirectory)/";
                        const string fileDirectory = "$(MSBuildThisFileDirectory)";
                        if (pattern.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase)) pattern = pattern[projectDirectory.Length..];
                        else if (pattern.StartsWith(fileDirectory, StringComparison.OrdinalIgnoreCase)) pattern = pattern[fileDirectory.Length..];
                        if (pattern.StartsWith("./", StringComparison.Ordinal)) pattern = pattern[2..];
                        if (pattern == name + "/**" || pattern == name + "/**/*") excluded = true;
                    }
                }
            }
            return excluded;
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
