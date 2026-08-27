namespace DeliveryTemperatureLimit.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    private readonly string tempRoot = System.IO.Path.GetFullPath(
        System.IO.Path.GetTempPath());

    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            tempRoot,
            $"oni-mod-pipeline-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        var resolved = System.IO.Path.GetFullPath(Path);
        var relative = System.IO.Path.GetRelativePath(tempRoot, resolved);
        var escapes = System.IO.Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith(
                $"..{System.IO.Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
        if (escapes)
        {
            throw new InvalidOperationException(
                $"Refusing to delete temporary path outside {tempRoot}: {resolved}");
        }

        if (Directory.Exists(resolved))
        {
            Directory.Delete(resolved, recursive: true);
        }
    }
}
