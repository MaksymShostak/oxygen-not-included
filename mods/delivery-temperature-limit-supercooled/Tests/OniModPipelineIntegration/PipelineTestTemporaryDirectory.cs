namespace DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

internal sealed class PipelineTestTemporaryDirectory : IDisposable
{
    private readonly string temporaryRoot = System.IO.Path.GetFullPath(
        System.IO.Path.GetTempPath());

    internal PipelineTestTemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            temporaryRoot,
            $"oni-mod-pipeline-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        var resolvedPath = System.IO.Path.GetFullPath(Path);
        var relativePath = System.IO.Path.GetRelativePath(
            temporaryRoot,
            resolvedPath);
        var escapesTemporaryRoot = System.IO.Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(
                $"..{System.IO.Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
        if (escapesTemporaryRoot)
        {
            throw new InvalidOperationException(
                $"Refusing to delete temporary path outside {temporaryRoot}: {resolvedPath}");
        }

        if (Directory.Exists(resolvedPath))
        {
            Directory.Delete(resolvedPath, recursive: true);
        }
    }
}
