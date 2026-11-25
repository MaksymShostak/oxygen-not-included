using System.Text.Json;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;

public sealed class GameReferenceCatalogTests
{
    [Test]
    public async Task SelectedBuildComesFromCatalogDataEvenWhenTheCheckoutVerifierIsInvalid()
    {
        using var directory = new TemporaryDirectory();
        string ci = directory.GetPath("tools/ci");
        Directory.CreateDirectory(ci);
        File.WriteAllText(Path.Combine(ci, "oni_references.py"), "This is intentionally not valid Python.");
        string digest = new string('a', 64);
        File.WriteAllText(Path.Combine(ci, "oni-references.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, manifestSha256 = digest,
            package = new { owner = "example", id = "MaksymShostak.ONI.GameReferences", version = "765432.0.0", byteLength = 1, sha256 = digest }
        }));
        var result = await GameReferenceCatalog.ReadSelectedBuildAsync(new ExternalProcessRunner(),
            directory.Path, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(765432);
        File.WriteAllText(Path.Combine(ci, "oni-references.json"), "{}");
        var invalid = await GameReferenceCatalog.ReadSelectedBuildAsync(new ExternalProcessRunner(), directory.Path, null, CancellationToken.None);
        await Assert.That(invalid.IsSuccess).IsFalse();
    }
}
