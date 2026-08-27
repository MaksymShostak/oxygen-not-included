namespace MaksymShostak.OniModPipeline.Tests.Fixtures;

internal sealed class TemporaryDirectory : IDisposable
{
    private readonly string temporaryRoot;
    private bool disposed;

    internal TemporaryDirectory()
    {
        temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
        Path = System.IO.Path.Combine(
            temporaryRoot,
            $"oni-mod-pipeline-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string GetPath(params string[] segments)
    {
        var path = Path;
        foreach (var segment in segments)
        {
            path = System.IO.Path.Combine(path, segment);
        }

        return path;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        var resolvedPath = System.IO.Path.GetFullPath(Path);
        var relativePath = System.IO.Path.GetRelativePath(temporaryRoot, resolvedPath);
        if (relativePath == "." ||
            System.IO.Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{System.IO.Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to delete temporary path outside '{temporaryRoot}': '{resolvedPath}'.");
        }

        if (Directory.Exists(resolvedPath))
        {
            ClearReadOnlyAttributes(resolvedPath);
            Directory.Delete(resolvedPath, recursive: true);
        }

        disposed = true;
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                }

                if ((attributes & FileAttributes.Directory) != 0 &&
                    (attributes & FileAttributes.ReparsePoint) == 0)
                {
                    pendingDirectories.Push(entry);
                }
            }

            var directoryAttributes = File.GetAttributes(directory);
            if ((directoryAttributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(directory, directoryAttributes & ~FileAttributes.ReadOnly);
            }
        }
    }
}
