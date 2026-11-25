using Microsoft.Build.Globbing;

namespace MaksymShostak.OniModPipeline.SourceControl;

/// <summary>Unique files and disjoint directory walks needed to consider statically declared MSBuild items.</summary>
internal sealed class SdkInputSearchPlan
{
    // Git observations may need a conservative case-insensitive prefilter. Physical paths and
    // classification results always keep distinct on-disk spellings.
    private readonly HashSet<string> declaredFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> declaredDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> directories = new(StringComparer.Ordinal);

    internal IReadOnlySet<string> Files => files;
    /// <summary>A true value requests recursive enumeration; false requests only immediate files.</summary>
    internal IReadOnlyDictionary<string, bool> Directories => directories;

    internal SdkInputSearchPlan(IEnumerable<MSBuildGlob> patterns, Func<string, string?> existingFile,
        Func<string, string?> existingDirectory, CancellationToken token)
    {
        var requested = new Dictionary<string, bool>(StringComparer.Ordinal);
        var candidateFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var glob in patterns)
        {
            token.ThrowIfCancellationRequested();
            string prefix = Path.TrimEndingDirectorySeparator(glob.FixedDirectoryPart);
            bool recursive = glob.WildcardDirectoryPart.Length != 0;
            if (!recursive && glob.FilenamePart.IndexOfAny(['*', '?']) < 0)
                candidateFiles.Add(Path.Combine(prefix, glob.FilenamePart));
            else
                requested[prefix] = recursive || requested.GetValueOrDefault(prefix);
        }
        foreach (var file in candidateFiles)
        {
            token.ThrowIfCancellationRequested();
            declaredFiles.Add(file);
            if (existingFile(file) is { } physical) files.Add(physical);
        }
        var physicalRoots = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (directory, recursive) in requested)
        {
            token.ThrowIfCancellationRequested();
            declaredDirectories[directory] = recursive || declaredDirectories.GetValueOrDefault(directory);
            if (existingDirectory(directory) is { } physical)
                physicalRoots[physical] = recursive || physicalRoots.GetValueOrDefault(physical);
        }
        foreach (var (directory, recursive) in physicalRoots)
        {
            token.ThrowIfCancellationRequested();
            if (!HasRecursiveAncestor(directory, physicalRoots, token))
                directories.Add(directory, recursive);
        }
    }

    // Coverage is lexical and works for index/status paths whose files or parent directory were deleted.
    internal bool Covers(string file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (declaredFiles.Contains(file)) return true;
        if (declaredDirectories.Count == 0) return false;
        string? parent = Path.GetDirectoryName(file);
        return parent is not null && (declaredDirectories.ContainsKey(parent) || HasRecursiveAncestor(parent, declaredDirectories, token));
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
