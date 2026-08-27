using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;

[TestClass]
public sealed class OniMetadataReaderTests
{
    private readonly OniMetadataReader metadataReader = new();

    [TestMethod]
    public void Read_WhenYamlContainsAllRequiredScalars_ReturnsTypedMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        WriteValidMetadata(temporaryDirectory);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("MaksymShostak.ExampleMod", result.Value.StaticId);
        Assert.AreEqual("Example Mod", result.Value.Title);
        Assert.AreEqual("Example description", result.Value.Description);
        Assert.AreEqual("ALL", result.Value.SupportedContent);
        Assert.AreEqual(596100, result.Value.MinimumSupportedBuild);
        Assert.AreEqual("2026.8.27", result.Value.Version);
        Assert.AreEqual(2, result.Value.ApiVersion);
    }

    [TestMethod]
    public void Read_WhenYamlContainsAdditionalOniOwnedKey_StillReturnsRequiredMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        WriteValidMetadata(
            temporaryDirectory,
            additionalModInfo: "requiredDlcIds:\n  - EXPANSION1\n");

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("MaksymShostak.ExampleMod", result.Value?.StaticId);
    }

    [TestMethod]
    public void Read_WhenYamlHasMultipleDocuments_ReturnsOnip1005()
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

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1005", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public void Read_WhenDeclaredYamlIsMissing_ReturnsOnip1008()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(temporaryDirectory.GetPath("mod.yaml"), ValidModYaml);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1008", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    [DataRow(
        "title: First title\ntitle: Second title\ndescription: Example description\nstaticID: MaksymShostak.ExampleMod\n")]
    [DataRow(
        "title: &title Example Mod\ndescription: *title\nstaticID: MaksymShostak.ExampleMod\n")]
    [DataRow(
        "title: !custom Example Mod\ndescription: Example description\nstaticID: MaksymShostak.ExampleMod\n")]
    [DataRow(
        "title:\n  - Example Mod\ndescription: Example description\nstaticID: MaksymShostak.ExampleMod\n")]
    public void Read_WhenYamlUsesUnsupportedStructure_ReturnsOnip1005(string modYaml)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        File.WriteAllText(temporaryDirectory.GetPath("mod.yaml"), modYaml);
        File.WriteAllText(temporaryDirectory.GetPath("mod_info.yaml"), ValidModInfo);

        var result = metadataReader.Read(CreateProfile(temporaryDirectory));

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1005", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public void Read_WhenInterpretedIntegerOverflows_ReturnsOnip1005()
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

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1005", result.Diagnostics.Single().Id);
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
