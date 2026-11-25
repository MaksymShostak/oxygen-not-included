namespace MaksymShostak.OniModPipeline.SourceControl;

/// <summary>Recovers the on-disk spelling of a path already verified inside this checkout.</summary>
internal sealed class CheckoutPathSpelling(string root)
{
    private readonly Dictionary<string, DirectoryNames> namesByDirectory = new(StringComparer.Ordinal);

    internal string ForExistingPath(string path, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return path;
        string relative = Path.GetRelativePath(root, path);
        if (relative == ".") return root;
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Path '{path}' is outside the checkout.");
        string parent = root;
        foreach (var component in relative.Split(Path.DirectorySeparatorChar))
        {
            token.ThrowIfCancellationRequested();
            if (!namesByDirectory.TryGetValue(parent, out var names))
            {
                names = new DirectoryNames(ReadNames(parent, token));
                namesByDirectory.Add(parent, names);
            }
            parent = Path.Combine(parent, names.Select(component));
        }
        return parent;
    }

    // NTFS case sensitivity is per directory. Exact spellings distinguish real siblings.
    // An alias may be collapsed only when a successful native lookup has a unique spelling.
    internal sealed class DirectoryNames
    {
        private readonly HashSet<string> exact = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> folded = new(StringComparer.OrdinalIgnoreCase);

        internal DirectoryNames(IEnumerable<string> physicalNames)
        {
            foreach (var name in physicalNames)
                if (exact.Add(name) && !folded.TryAdd(name, name)) folded[name] = null;
        }

        internal string Select(string requested)
        {
            if (exact.Contains(requested)) return requested;
            return folded.TryGetValue(requested, out var only) && only is not null ? only
                : throw new InvalidDataException($"The physical spelling of '{requested}' is missing or ambiguous.");
        }
    }

    private static IEnumerable<string> ReadNames(string parent, CancellationToken token)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(parent))
        {
            token.ThrowIfCancellationRequested();
            yield return Path.GetFileName(entry);
        }
    }
}
