using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Text;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopListing;
public sealed class WorkshopListingAssemblerTests
{
    [Test]
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

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(Directory.EnumerateFiles(target).Select(Path.GetFileName).ToArray()).IsEquivalentTo((new[] { "change-notes.bbcode", "description.bbcode", "preview.jpg" }).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Any);
        await Assert.That(await File.ReadAllBytesAsync(Path.Combine(target, "description.bbcode"))).IsEquivalentTo(Encoding.UTF8.GetBytes("Description\r\n"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await File.ReadAllBytesAsync(Path.Combine(target, "preview.jpg"))).IsEquivalentTo(previewBytes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Value?.DescriptionReport.LineEndings).IsEqualTo("crlf");
        await Assert.That(result.Value?.Preview.Format).IsEqualTo("jpeg");
        await Assert.That(result.Value?.Files.Count).IsEqualTo(3);
    }

    [Test]
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

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(stalePath)).IsEqualTo("keep");
        await Assert.That(Directory.EnumerateFiles(target).ToArray()).IsEquivalentTo(new[] { stalePath }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
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

        await Assert.That(validation.IsSuccess).IsFalse();
        await Assert.That(assembly.IsSuccess).IsFalse();
        await Assert.That(assembly.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray()).IsEquivalentTo(validation.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(assembly.Diagnostics.Select(diagnostic => diagnostic.Evidence).ToArray()).IsEquivalentTo(validation.Diagnostics.Select(diagnostic => diagnostic.Evidence).ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Directory.EnumerateFileSystemEntries(target).Count()).IsEqualTo(0);
    }

    [Test]
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

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.ModTypeLabels.ToArray()).IsEquivalentTo(new[] { "language", "worldgen", "new features", "tweaks", "ui" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Value?.DlcLabels.ToArray()).IsEquivalentTo(new[]
            {
                "Base Game",
                "Spaced Out!",
                "The Frosty Planet Pack",
                "The Bionic Booster Pack",
                "The Prehistoric Planet Pack",
                "The Aquatic Planet Pack"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
