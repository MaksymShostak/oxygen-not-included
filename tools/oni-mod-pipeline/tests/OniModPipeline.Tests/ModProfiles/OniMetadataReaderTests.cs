using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;
public sealed class OniMetadataReaderTests
{
    private readonly OniMetadataReader metadataReader = new();

    [Test]
    public async Task Read_WhenYamlContainsAllRequiredScalars_ReturnsTypedMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        WriteValidMetadata(temporaryDirectory);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        await Assert.That(result.IsSuccess).IsTrue();
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.StaticId).IsEqualTo("MaksymShostak.ExampleMod");
        await Assert.That(result.Value.Title).IsEqualTo("Example Mod");
        await Assert.That(result.Value.Description).IsEqualTo("Example description");
        await Assert.That(result.Value.SupportedContent).IsEqualTo("ALL");
        await Assert.That(result.Value.MinimumSupportedBuild).IsEqualTo(596100);
        await Assert.That(result.Value.Version).IsEqualTo("2026.8.27");
        await Assert.That(result.Value.ApiVersion).IsEqualTo(2);
    }

    [Test]
    public async Task Read_WhenYamlContainsAdditionalOniOwnedKey_StillReturnsRequiredMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        WriteValidMetadata(
            temporaryDirectory,
            additionalModInfo: "requiredDlcIds:\n  - EXPANSION1\n");

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.StaticId).IsEqualTo("MaksymShostak.ExampleMod");
    }

    [Test]
    public async Task Read_WhenYamlHasMultipleDocuments_ReturnsOnip1005()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(
            temporaryDirectory.GetPath("mod.yaml"),
            """
            title: Example Mod
            description: Example description
            staticID: MaksymShostak.ExampleMod
            ---
            ignored: second document
            """);
        File.WriteAllText(temporaryDirectory.GetPath("mod_info.yaml"), ValidModInfo);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1005");
    }

    [Test]
    public async Task Read_WhenDeclaredYamlIsMissing_ReturnsOnip1008()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(temporaryDirectory.GetPath("mod.yaml"), ValidModYaml);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1008");
    }

    [Test]
    [Arguments(
        "title: First title\ntitle: Second title\ndescription: Example description\nstaticID: MaksymShostak.ExampleMod\n")]
    [Arguments(
        "title: &title Example Mod\ndescription: *title\nstaticID: MaksymShostak.ExampleMod\n")]
    [Arguments(
        "title: !custom Example Mod\ndescription: Example description\nstaticID: MaksymShostak.ExampleMod\n")]
    [Arguments(
        "title:\n  - Example Mod\ndescription: Example description\nstaticID: MaksymShostak.ExampleMod\n")]
    public async Task Read_WhenYamlUsesUnsupportedStructure_ReturnsOnip1005(string modYaml)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(temporaryDirectory.GetPath("mod.yaml"), modYaml);
        File.WriteAllText(temporaryDirectory.GetPath("mod_info.yaml"), ValidModInfo);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1005");
    }

    [Test]
    public async Task Read_WhenInterpretedIntegerOverflows_ReturnsOnip1005()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(temporaryDirectory.GetPath("mod.yaml"), ValidModYaml);
        File.WriteAllText(
            temporaryDirectory.GetPath("mod_info.yaml"),
            ValidModInfo.Replace(
                "APIVersion: 2",
                "APIVersion: 2147483648",
                StringComparison.Ordinal));

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1005");
    }

    [Test]
    public async Task BuildFloorProjectionChecksDriftAndPreservesOtherBytesOnWrite()
    {
        using var directory = new TemporaryDirectory();
        WriteValidMetadata(directory);
        string path = directory.GetPath("mod_info.yaml");
        var encoding = new System.Text.UTF8Encoding(true);
        string text = ValidModInfo.Replace("596100", "'596100' # keep this comment").ReplaceLineEndings("\r\n") + "\r\n";
        File.WriteAllText(path, text, encoding);
        var profile = CreateProfile(directory);
        byte[] original = File.ReadAllBytes(path);
        await Assert.That(metadataReader.SynchronizeMinimumBuild(profile, 765432, false).IsSuccess).IsFalse();
        await Assert.That(File.ReadAllBytes(path).SequenceEqual(original)).IsTrue();
        await Assert.That(metadataReader.SynchronizeMinimumBuild(profile, 765432, true).IsSuccess).IsTrue();
        byte[] expected = [.. encoding.GetPreamble(), .. encoding.GetBytes(text.Replace("596100", "765432"))];
        await Assert.That(File.ReadAllBytes(path).SequenceEqual(expected)).IsTrue();
        var time = File.GetLastWriteTimeUtc(path);
        await Assert.That(metadataReader.SynchronizeMinimumBuild(profile, 765432, true).IsSuccess).IsTrue();
        await Assert.That(File.GetLastWriteTimeUtc(path)).IsEqualTo(time);
        await Assert.That(metadataReader.SynchronizeMinimumBuild(profile, 765432, false).IsSuccess).IsTrue();
    }

    [Test]
    [Arguments("minimumSupportedBuild: &value 596100")]
    [Arguments("minimumSupportedBuild: 1\nminimumSupportedBuild: 2")]
    [Arguments("!!str minimumSupportedBuild: 1\nminimumSupportedBuild: 765432")]
    [Arguments("minimumSupportedBuild: 596100\ninvalid")]
    public async Task BuildFloorProjectionRefusesAmbiguousInputs(string declaration)
    {
        using var directory = new TemporaryDirectory();
        WriteValidMetadata(directory);
        var path = directory.GetPath("mod_info.yaml");
        string original = ValidModInfo.Replace("minimumSupportedBuild: 596100", declaration);
        File.WriteAllText(path, original);
        await Assert.That(metadataReader.SynchronizeMinimumBuild(CreateProfile(directory), 765432, false).IsSuccess).IsFalse();
        await Assert.That(metadataReader.SynchronizeMinimumBuild(CreateProfile(directory), 765432, true).IsSuccess).IsFalse();
        await Assert.That(File.ReadAllText(path)).IsEqualTo(original);
    }

    [Test]
    public async Task BuildFloorProjectionValidatesOtherMetadataBeforeWriting()
    {
        using var directory = new TemporaryDirectory();
        WriteValidMetadata(directory);
        var path = directory.GetPath("mod_info.yaml");
        string original = ValidModInfo.Replace("APIVersion: 2", "APIVersion: 0");
        File.WriteAllText(path, original);
        await Assert.That(metadataReader.SynchronizeMinimumBuild(CreateProfile(directory), 765432, true).IsSuccess).IsFalse();
        await Assert.That(File.ReadAllText(path)).IsEqualTo(original);
    }

    private static ModProfile CreateProfile(TemporaryDirectory temporaryDirectory) =>
        new(
            1,
            temporaryDirectory.GetPath("oni-mod-pipeline.toml"),
            temporaryDirectory.Path,
            "mod.yaml",
            "mod_info.yaml",
            null,
            [],
            new WorkshopListingProfile("description", "change-notes", "preview", [], [], 8000, 8000),
            new LocalInstallProfile("ExampleMod"),
            [],
            []);

    private static void WriteValidMetadata(
        TemporaryDirectory temporaryDirectory,
        string additionalModInfo = "")
    {
        File.WriteAllText(
            temporaryDirectory.GetPath("mod.yaml"),
            ValidModYaml);
        File.WriteAllText(
            temporaryDirectory.GetPath("mod_info.yaml"),
            $"{ValidModInfo}\n{additionalModInfo}");
    }

    private const string ValidModInfo = """
        supportedContent: ALL
        minimumSupportedBuild: 596100
        version: 2026.8.27
        APIVersion: 2
        """;

    private const string ValidModYaml = """
        title: Example Mod
        description: Example description
        staticID: MaksymShostak.ExampleMod
        """;
}
