using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;
public sealed class ModProfileLocatorTests
{
    private readonly ModProfileLocator profileLocator = new();

    [Test]
    public async Task Locate_WhenNoManifestIsReachable_ReturnsMissingProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.GetPath(".git"));
        var descendant = temporaryDirectory.GetPath("mods", "example-mod", "Source");
        Directory.CreateDirectory(descendant);

        var result = profileLocator.Locate(descendant);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1007");
        await Assert.That(result.Diagnostics.Single().NextAction).Contains("oni-mod-pipeline.toml");
    }

    [Test]
    public async Task Locate_WhenStartedBelowOneManifest_ReturnsThatManifest()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.GetPath(".git"));
        var modRoot = temporaryDirectory.GetPath("mods", "example-mod");
        var descendant = Path.Combine(modRoot, "Source", "Nested");
        Directory.CreateDirectory(descendant);
        var manifestPath = Path.Combine(modRoot, "oni-mod-pipeline.toml");
        File.WriteAllText(manifestPath, "schema-version = 1");

        var result = profileLocator.Locate(descendant);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(Path.GetFullPath(manifestPath));
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success);
    }

    [Test]
    public async Task Locate_WhenTwoCandidateManifestsAreReachable_ReturnsAmbiguityDiagnostic()
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

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1007");
        await Assert.That(result.Diagnostics.Single().Evidence).Contains(firstManifest);
        await Assert.That(result.Diagnostics.Single().Evidence).Contains(secondManifest);
    }
}
