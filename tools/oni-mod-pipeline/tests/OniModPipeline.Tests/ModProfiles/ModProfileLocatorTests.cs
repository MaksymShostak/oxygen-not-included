using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;

[TestClass]
public sealed class ModProfileLocatorTests
{
    private readonly ModProfileLocator profileLocator = new();

    [TestMethod]
    public void Locate_WhenNoManifestIsReachable_ReturnsMissingProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.GetPath(".git"));
        var descendant = temporaryDirectory.GetPath("mods", "example-mod", "Source");
        Directory.CreateDirectory(descendant);

        var result = profileLocator.Locate(descendant);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1007", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().NextAction, "oni-mod-pipeline.toml");
    }

    [TestMethod]
    public void Locate_WhenStartedBelowOneManifest_ReturnsThatManifest()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.GetPath(".git"));
        var modRoot = temporaryDirectory.GetPath("mods", "example-mod");
        var descendant = Path.Combine(modRoot, "Source", "Nested");
        Directory.CreateDirectory(descendant);
        var manifestPath = Path.Combine(modRoot, "oni-mod-pipeline.toml");
        File.WriteAllText(manifestPath, "schema-version = 1");

        var result = profileLocator.Locate(descendant);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Path.GetFullPath(manifestPath), result.Value);
        Assert.AreEqual(PipelineExitCode.Success, result.ExitCode);
    }

    [TestMethod]
    public void Locate_WhenTwoCandidateManifestsAreReachable_ReturnsAmbiguityDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.GetPath(".git"));
        var firstManifest = temporaryDirectory.GetPath("oni-mod-pipeline.toml");
        File.WriteAllText(firstManifest, "schema-version = 1");
        var nestedRoot = temporaryDirectory.GetPath("mods", "example-mod");
        Directory.CreateDirectory(nestedRoot);
        var secondManifest = Path.Combine(nestedRoot, "oni-mod-pipeline.toml");
        File.WriteAllText(secondManifest, "schema-version = 1");
        var descendant = Path.Combine(nestedRoot, "Source");
        Directory.CreateDirectory(descendant);

        var result = profileLocator.Locate(descendant);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1007", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, firstManifest);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, secondManifest);
    }
}
