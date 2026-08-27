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
            Directory.Delete(resolvedPath, recursive: true);
        }

        disposed = true;
    }
}
