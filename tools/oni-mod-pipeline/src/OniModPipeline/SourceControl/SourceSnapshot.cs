using MaksymShostak.OniModPipeline.ContentIntegrity;
using System.Security.Cryptography;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal sealed record SourceSnapshot(IReadOnlyList<FileDigest> Files)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static SourceSnapshot Capture(IReadOnlyList<string> absolutePaths)
    {
        ArgumentNullException.ThrowIfNull(absolutePaths);

        var files = absolutePaths
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .Where(File.Exists)
            .OrderBy(path => path, PathComparer)
            .Select(CaptureFile)
            .ToArray();
        return new SourceSnapshot(files);
    }

    internal static SourceSnapshot CaptureTree(string absoluteRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteRoot);
        var root = Path.GetFullPath(absoluteRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Source tree root '{root}' does not exist.");
        }

        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
            new DirectoryInfo(root).LinkTarget is not null)
        {
            throw new InvalidOperationException(
                $"Source tree root '{root}' must not be a symbolic link or reparse point.");
        }

        var paths = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);
        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();
            foreach (var entry in Directory
                .EnumerateFileSystemEntries(directory)
                .OrderBy(path => path, PathComparer))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (new DirectoryInfo(entry).LinkTarget is null)
                    {
                        pendingDirectories.Push(entry);
                    }

                    continue;
                }

                if (new FileInfo(entry).LinkTarget is null)
                {
                    paths.Add(Path.GetFullPath(entry));
                }
            }
        }

        return Capture(paths);
    }

    internal IReadOnlyList<string> ChangedPathsComparedWith(SourceSnapshot later)
    {
        ArgumentNullException.ThrowIfNull(later);

        var beforeByPath = Files.ToDictionary(file => file.Path, PathComparer);
        var laterByPath = later.Files.ToDictionary(file => file.Path, PathComparer);
        var allPaths = beforeByPath.Keys
            .Concat(laterByPath.Keys)
            .Distinct(PathComparer)
            .OrderBy(path => path, PathComparer);
        var changedPaths = new List<string>();
        foreach (var path in allPaths)
        {
            if (!beforeByPath.TryGetValue(path, out var before) ||
                !laterByPath.TryGetValue(path, out var after) ||
                before.ByteLength != after.ByteLength ||
                !string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
            {
                changedPaths.Add(path);
            }
        }

        return changedPaths;
    }

    private static FileDigest CaptureFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        var byteLength = stream.Length;
        var digest = SHA256.HashData(stream);
        return new FileDigest(
            Path.GetFullPath(path),
            byteLength,
            Convert.ToHexStringLower(digest));
    }
}
