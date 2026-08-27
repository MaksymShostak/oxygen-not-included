using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;

[TestClass]
public sealed class ContainedPathResolverTests
{
    [TestMethod]
    [DataRow("../outside.txt")]
    [DataRow("..\\outside.txt")]
    [DataRow("/absolute.txt")]
    [DataRow("C:\\absolute.txt")]
    public void ResolveExistingFile_WhenPathEscapesRoot_ReturnsOnip1003(
        string declaredPath)
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var result = ContainedPathResolver.ResolveExistingFile(
            temporaryDirectory.Path,
            declaredPath);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1003", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public void ResolveExistingFile_WhenAncestorIsSymbolicLink_ReturnsOnip1003()
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

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1003", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public void ResolveExistingFile_WhenContainedRegularFile_ReturnsCanonicalAbsolutePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var containedDirectory = temporaryDirectory.GetPath("inputs");
        Directory.CreateDirectory(containedDirectory);
        var filePath = Path.Combine(containedDirectory, "input.txt");
        File.WriteAllText(filePath, "content");

        var result = ContainedPathResolver.ResolveExistingFile(
            temporaryDirectory.Path,
            Path.Combine("inputs", ".", "input.txt"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Path.GetFullPath(filePath), result.Value);
    }
}
