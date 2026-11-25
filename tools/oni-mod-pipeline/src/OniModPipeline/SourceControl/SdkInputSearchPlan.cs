using Microsoft.Build.Globbing;

namespace MaksymShostak.OniModPipeline.SourceControl;

/// <summary>Unique files and disjoint directory walks needed to consider statically declared MSBuild items.</summary>
internal sealed class SdkInputSearchPlan
{
    internal static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly HashSet<string> files = new(PathComparer);
    private readonly Dictionary<string, bool> directories = new(PathComparer);

    internal IReadOnlySet<string> Files => files;
    /// <summary>A true value requests recursive enumeration; false requests only immediate files.</summary>
    internal IReadOnlyDictionary<string, bool> Directories => directories;

    internal SdkInputSearchPlan(IEnumerable<MSBuildGlob> patterns, CancellationToken token)
    {
        var requested = new Dictionary<string, bool>(PathComparer);
        foreach (var glob in patterns)
        {
            token.ThrowIfCancellationRequested();
            string prefix = Path.TrimEndingDirectorySeparator(glob.FixedDirectoryPart);
            bool recursive = glob.WildcardDirectoryPart.Length != 0;
            if (!recursive && glob.FilenamePart.IndexOfAny(['*', '?']) < 0)
                files.Add(Path.Combine(prefix, glob.FilenamePart));
            else
                requested[prefix] = recursive || requested.GetValueOrDefault(prefix);
        }
        foreach (var (directory, recursive) in requested)
        {
            token.ThrowIfCancellationRequested();
            if (!HasRecursiveAncestor(directory, requested, token))
                directories.Add(directory, recursive);
        }
    }

    // Coverage is lexical and works for index/status paths whose files or parent directory were deleted.
    internal bool Covers(string file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (files.Contains(file)) return true;
        if (directories.Count == 0) return false;
        string? parent = Path.GetDirectoryName(file);
        return parent is not null && (directories.ContainsKey(parent) || HasRecursiveAncestor(parent, directories, token));
    }

    private static bool HasRecursiveAncestor(string directory, Dictionary<string, bool> roots, CancellationToken token)
    {
        for (string? parent = Path.GetDirectoryName(directory); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            token.ThrowIfCancellationRequested();
            if (roots.GetValueOrDefault(parent)) return true;
        }
        return false;
    }
}
