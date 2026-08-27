using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;

[TestClass]
public sealed class WorkshopListingAssemblerTests
{
    [TestMethod]
    public async Task AssembleAsync_WhenSourcesAreValid_WritesExactUploaderArtifactsAndReports()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory, "Preview.jpeg");
        var previewBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x01, 0x02 };
        File.WriteAllBytes(Path.Combine(profile.ModRoot, "Preview.jpeg"), previewBytes);
        var target = temporaryDirectory.GetPath("candidate", "workshop-listing");
        Directory.CreateDirectory(target);
        var assembler = new WorkshopListingAssembler();

        var result = await assembler.AssembleAsync(
            profile,
            target,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEquivalent(
            new[] { "change-notes.bbcode", "description.bbcode", "preview.jpg" },
            Directory.EnumerateFiles(target).Select(Path.GetFileName).ToArray());
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes("Description\r\n"),
            await File.ReadAllBytesAsync(Path.Combine(target, "description.bbcode")));
        CollectionAssert.AreEqual(
            previewBytes,
            await File.ReadAllBytesAsync(Path.Combine(target, "preview.jpg")));
        Assert.AreEqual("crlf", result.Value?.DescriptionReport.LineEndings);
        Assert.AreEqual("jpeg", result.Value?.Preview.Format);
        Assert.AreEqual(3, result.Value?.Files.Count);
    }

    [TestMethod]
    public async Task AssembleAsync_WhenTargetIsNotEmpty_RejectsWithoutChangingIt()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory, "Preview.png");
        File.WriteAllBytes(
            Path.Combine(profile.ModRoot, "Preview.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var target = temporaryDirectory.GetPath("candidate", "workshop-listing");
        Directory.CreateDirectory(target);
        var stalePath = Path.Combine(target, "stale.txt");
        await File.WriteAllTextAsync(stalePath, "keep");

        var result = await new WorkshopListingAssembler().AssembleAsync(
            profile,
            target,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("keep", await File.ReadAllTextAsync(stalePath));
        CollectionAssert.AreEqual(new[] { stalePath }, Directory.EnumerateFiles(target).ToArray());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenSourceIsInvalid_PreservesStandaloneDiagnostics()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory, "Preview.png");
        File.WriteAllBytes(
            Path.Combine(profile.ModRoot, "Preview.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        File.WriteAllText(Path.Combine(profile.ModRoot, "change-notes.bbcode"), "TODO\n");
        var target = temporaryDirectory.GetPath("candidate", "workshop-listing");
        Directory.CreateDirectory(target);
        var validator = new WorkshopListingValidator();
        var validation = await validator.ValidateAsync(profile, CancellationToken.None);

        var assembly = await new WorkshopListingAssembler().AssembleAsync(
            profile,
            target,
            CancellationToken.None);

        Assert.IsFalse(validation.IsSuccess);
        Assert.IsFalse(assembly.IsSuccess);
        CollectionAssert.AreEqual(
            validation.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray(),
            assembly.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray());
        CollectionAssert.AreEqual(
            validation.Diagnostics.Select(diagnostic => diagnostic.Evidence).ToArray(),
            assembly.Diagnostics.Select(diagnostic => diagnostic.Evidence).ToArray());
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(target).Count());
    }

    [TestMethod]
    public async Task ValidateAsync_WhenProfileUsesKnownIdentifiers_ReturnsUploaderLabels()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory, "Preview.png") with
        {
            WorkshopListing = CreateProfile(temporaryDirectory, "Preview.png").WorkshopListing with
            {
                ModTypes = ["language", "worldgen", "new-features", "tweaks", "ui"],
                DlcCompatibility =
                [
                    "base-game",
                    "spaced-out",
                    "frosty-planet-pack",
                    "bionic-booster-pack",
                    "prehistoric-planet-pack",
                    "aquatic-planet-pack"
                ]
            }
        };
        File.WriteAllBytes(
            Path.Combine(profile.ModRoot, "Preview.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var result = await new WorkshopListingValidator().ValidateAsync(
            profile,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "language", "worldgen", "new features", "tweaks", "ui" },
            result.Value?.ModTypeLabels.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Base Game",
                "Spaced Out!",
                "The Frosty Planet Pack",
                "The Bionic Booster Pack",
                "The Prehistoric Planet Pack",
                "The Aquatic Planet Pack"
            },
            result.Value?.DlcLabels.ToArray());
    }

    private static ModProfile CreateProfile(
        TemporaryDirectory temporaryDirectory,
        string previewName)
    {
        var modRoot = temporaryDirectory.GetPath("mod");
        Directory.CreateDirectory(modRoot);
        File.WriteAllText(Path.Combine(modRoot, "description.bbcode"), "Description\n");
        File.WriteAllText(Path.Combine(modRoot, "change-notes.bbcode"), "Changes\n");
        return new ModProfile(
            1,
            Path.Combine(modRoot, "oni-mod-pipeline.toml"),
            modRoot,
            "mod.yaml",
            "mod_info.yaml",
            null,
            [],
            new WorkshopListingProfile(
                "description.bbcode",
                "change-notes.bbcode",
                previewName,
                ["tweaks"],
                ["base-game"],
                8000,
                8000),
            new LocalInstallProfile("ExampleMod"),
            [],
            []);
    }
}
