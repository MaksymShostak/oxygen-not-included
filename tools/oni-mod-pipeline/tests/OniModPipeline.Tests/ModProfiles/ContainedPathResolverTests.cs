using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;
public sealed class ContainedPathResolverTests
{
    [Test]
    [Arguments("../outside.txt")]
    [Arguments("..\\outside.txt")]
    [Arguments("/absolute.txt")]
    [Arguments("C:\\absolute.txt")]
    public async Task ResolveExistingFile_WhenPathEscapesRoot_ReturnsOnip1003(
        string declaredPath)
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var result = ContainedPathResolver.ResolveExistingFile(
            temporaryDirectory.Path,
            declaredPath);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1003");
    }

    [Test]
    public async Task ResolveExistingFile_WhenAncestorIsSymbolicLink_ReturnsOnip1003()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var targetDirectory = temporaryDirectory.GetPath("target");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "input.txt"), "content");
        var linkDirectory = temporaryDirectory.GetPath("link");

        OperationResult<string> result;
        try
        {
            Directory.CreateSymbolicLink(linkDirectory, targetDirectory);
            result = ContainedPathResolver.ResolveExistingFile(
                temporaryDirectory.Path,
                Path.Combine("link", "input.txt"));
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            var ordinaryDirectory = temporaryDirectory.GetPath("ordinary");
            Directory.CreateDirectory(ordinaryDirectory);
            File.WriteAllText(Path.Combine(ordinaryDirectory, "input.txt"), "content");
            result = ContainedPathResolver.ResolveExistingFile(
                temporaryDirectory.Path,
                Path.Combine("ordinary", "input.txt"),
                path => string.Equals(path, ordinaryDirectory, StringComparison.OrdinalIgnoreCase)
                    ? File.GetAttributes(path) | FileAttributes.ReparsePoint
                    : File.GetAttributes(path),
                _ => null);
        }

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1003");
    }

    [Test]
    public async Task ResolveExistingFile_WhenContainedRegularFile_ReturnsCanonicalAbsolutePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var containedDirectory = temporaryDirectory.GetPath("inputs");
        Directory.CreateDirectory(containedDirectory);
        var filePath = Path.Combine(containedDirectory, "input.txt");
        File.WriteAllText(filePath, "content");

        var result = ContainedPathResolver.ResolveExistingFile(
            temporaryDirectory.Path,
            Path.Combine("inputs", ".", "input.txt"));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(Path.GetFullPath(filePath));
    }
}
