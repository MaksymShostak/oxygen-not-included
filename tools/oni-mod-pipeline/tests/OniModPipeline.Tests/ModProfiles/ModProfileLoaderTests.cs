using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;
public sealed class ModProfileLoaderTests
{
    private readonly ModProfileLoader profileLoader = new();

    [Test]
    public async Task LoadWithCatalogsAcceptsExactDeclaration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var result = profileLoader.Load(WriteManifest(temporaryDirectory, ValidManifest + "\n[catalogs]\ndirectory = \"translations\"\ntemplate = \"example.pot\"\noptions-source = \"Options.cs\"\noptions-context-prefix = \"STRINGS.EXAMPLE.OPTIONS.\"\n"));
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Catalogs).IsEqualTo(new CatalogsProfile("translations", "example.pot", "Options.cs", "STRINGS.EXAMPLE.OPTIONS."));
    }

    [Test]
    [Arguments("[catalogs]")]
    [Arguments("[catalogs]\ndirectory = 42")]
    [Arguments("[catalogs]\nunknown = \"translations\"")]
    public async Task LoadWithInvalidCatalogsRejectsDeclaration(string table)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await Assert.That(profileLoader.Load(WriteManifest(temporaryDirectory, ValidManifest + "\n" + table)).ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
    }

    [Test]
    public async Task Load_WithReadmeTable_AcceptsRepositoryRelativeDeclaration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var result = profileLoader.Load(WriteManifest(temporaryDirectory,
            ValidManifest + "\n[readme]\nrepository-path = \"README.md\"\n"));
        await Assert.That(result.IsSuccess).IsTrue().Because(string.Join("; ", result.Diagnostics));
        await Assert.That(result.Value?.Readme?.RepositoryPath).IsEqualTo("README.md");
    }

    [Test]
    [Arguments("[readme]\nrepository-path = 42")]
    [Arguments("[readme]\npath = \"README.md\"")]
    [Arguments("[readme]")]
    public async Task Load_WithInvalidReadmeTable_RejectsDeclaration(string table)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var result = profileLoader.Load(WriteManifest(temporaryDirectory, ValidManifest + "\n" + table));
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
    }

    [Test]
    public async Task Load_WhenSchemaVersionIsTwo_ReturnsOnip1001()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace("schema-version = 1", "schema-version = 2", StringComparison.Ordinal));

        var result = profileLoader.Load(manifestPath);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1001");
    }

    [Test]
    public async Task Load_WhenTopLevelKeyIsMisspelled_ReturnsOnip1002()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            $"schema-versoin = 1{Environment.NewLine}{ValidManifest}");

        var result = profileLoader.Load(manifestPath);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1002");
        await Assert.That(result.Diagnostics.Single().Evidence).Contains("schema-versoin");
    }

    [Test]
    public async Task Load_WhenNestedKeyIsUnknown_ReturnsOnip1002WithFullKeyPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace(
                "configuration = \"Release\"",
                "configuration = \"Release\"\nconfiguraton = \"Release\"",
                StringComparison.Ordinal));

        var result = profileLoader.Load(manifestPath);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1002");
        await Assert.That(result.Diagnostics.Single().Evidence).Contains("build.configuraton");
    }

    [Test]
    public async Task Load_WhenManifestIsValid_PreservesDeclaredValuesAndUsesListingLimitDefaults()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(temporaryDirectory, ValidManifest);

        var result = profileLoader.Load(manifestPath);

        await Assert.That(result.IsSuccess).IsTrue();
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.ModYamlPath).IsEqualTo("./mod.yaml");
        await Assert.That(result.Value.Readme).IsNull();
        await Assert.That(result.Value.Build?.Configuration).IsEqualTo("Release");
        await Assert.That(result.Value.Build?.MergeInputs.Count).IsEqualTo(0);
        await Assert.That(result.Value.WorkshopListing.DescriptionByteLimit).IsEqualTo(8000);
        await Assert.That(result.Value.WorkshopListing.ChangeNotesByteLimit).IsEqualTo(8000);
        await Assert.That(result.Value.AcceptanceChecks.Single().Setup).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Load_WhenBuildTableIsAbsent_ReturnsContentOnlyProfile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace(BuildTable, string.Empty, StringComparison.Ordinal));

        var result = profileLoader.Load(manifestPath);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.Build).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(8001)]
    public async Task Load_WhenDescriptionByteLimitExceedsV1Range_ReturnsInvalidInput(int byteLimit)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestPath = WriteManifest(
            temporaryDirectory,
            ValidManifest.Replace(
                "description = \"STEAM_DESCRIPTION.bbcode\"",
                $"description = \"STEAM_DESCRIPTION.bbcode\"\ndescription-byte-limit = {byteLimit}",
                StringComparison.Ordinal));

        var result = profileLoader.Load(manifestPath);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1002");
        await Assert.That(result.Diagnostics.Single().Evidence).Contains("description-byte-limit");
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
