using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;

[TestClass]
public sealed class ModProfileLoaderTests
{
    [TestMethod]
    public void Load_WhenSchemaVersionIsTwo_ReturnsOnip1001()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace("schema-version = 1", "schema-version = 2", StringComparison.Ordinal));

        var result = ModProfileLoader.Load(manifestPath);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1001", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public void Load_WhenTopLevelKeyIsMisspelled_ReturnsOnip1002()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            $"schema-versoin = 1{Environment.NewLine}{ValidManifest}");

        var result = ModProfileLoader.Load(manifestPath);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1002", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "schema-versoin");
    }

    [TestMethod]
    public void Load_WhenNestedKeyIsUnknown_ReturnsOnip1002WithFullKeyPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace(
                "configuration = \"Release\"",
                "configuration = \"Release\"\nconfiguraton = \"Release\"",
                StringComparison.Ordinal));

        var result = ModProfileLoader.Load(manifestPath);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1002", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "build.configuraton");
    }

    [TestMethod]
    public void Load_WhenManifestIsValid_PreservesDeclaredValuesAndUsesListingLimitDefaults()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(temporaryDirectory, ValidManifest);

        var result = ModProfileLoader.Load(manifestPath);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("./mod.yaml", result.Value.ModYamlPath);
        Assert.AreEqual("Release", result.Value.Build?.Configuration);
        Assert.AreEqual(0, result.Value.Build?.MergeInputs.Count);
        Assert.AreEqual(8000, result.Value.WorkshopListing.DescriptionByteLimit);
        Assert.AreEqual(8000, result.Value.WorkshopListing.ChangeNotesByteLimit);
        Assert.AreEqual(string.Empty, result.Value.AcceptanceChecks.Single().Setup);
    }

    [TestMethod]
    public void Load_WhenBuildTableIsAbsent_ReturnsContentOnlyProfile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace(BuildTable, string.Empty, StringComparison.Ordinal));

        var result = ModProfileLoader.Load(manifestPath);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value?.Build);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(8001)]
    public void Load_WhenDescriptionByteLimitExceedsV1Range_ReturnsInvalidInput(int byteLimit)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace(
                "description = \"STEAM_DESCRIPTION.bbcode\"",
                $"description = \"STEAM_DESCRIPTION.bbcode\"\ndescription-byte-limit = {byteLimit}",
                StringComparison.Ordinal));

        var result = ModProfileLoader.Load(manifestPath);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1002", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "description-byte-limit");
    }

    private static string WriteManifest(TemporaryDirectory temporaryDirectory, string content)
    {
        var manifestPath = temporaryDirectory.GetPath("oni-mod-pipeline.toml");
        File.WriteAllText(manifestPath, content);
        return manifestPath;
    }

    private const string ValidManifest = """
        schema-version = 1

        [mod]
        mod-yaml = "./mod.yaml"
        mod-info-yaml = "mod_info.yaml"

        [build]
        entry-point = "Source/Example.csproj"
        configuration = "Release"
        game-managed-directory-property = "OniManagedAssemblyDirectory"
        primary-output = "{build-output}/Example.dll"

        [[package-files]]
        source = "mod.yaml"
        destination = "mod.yaml"

        [workshop-listing]
        description = "STEAM_DESCRIPTION.bbcode"
        change-notes = "STEAM_CHANGE_NOTES.bbcode"
        preview = "Preview.png"
        mod-types = ["tweaks"]
        dlc-compatibility = ["base-game"]

        [local-install]
        directory-name = "ExampleMod"

        [[test-projects]]
        id = "example-regressions"
        path = "Tests/Example.Tests.csproj"
        required = true

        [[acceptance-checks]]
        id = "example-check"
        title = "Example check"
        required = true
        """;

    private const string BuildTable = """
        [build]
        entry-point = "Source/Example.csproj"
        configuration = "Release"
        game-managed-directory-property = "OniManagedAssemblyDirectory"
        primary-output = "{build-output}/Example.dll"

        """;
}
