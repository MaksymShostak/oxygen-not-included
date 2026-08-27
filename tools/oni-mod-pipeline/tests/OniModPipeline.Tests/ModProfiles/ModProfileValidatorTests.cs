using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;

[TestClass]
public sealed class ModProfileValidatorTests
{
    [TestMethod]
    public void Validate_WhenProfileIsPortableAndComplete_ReturnsSuccess()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(profile, result.Value);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    [TestMethod]
    [DataRow("Assets/config.json", "Assets\\config.json")]
    [DataRow("Assets/Café.txt", "Assets/Café.txt")]
    [DataRow("Assets/File.txt", "assets/file.txt")]
    public void Validate_WhenPackageDestinationsCollidePortably_ReturnsOnip1004(
        string firstDestination,
        string secondDestination)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            PackageFiles =
            [
                new PackageFileMapping("mod.yaml", "mod.yaml"),
                new PackageFileMapping("mod_info.yaml", "mod_info.yaml"),
                new PackageFileMapping("asset.txt", firstDestination),
                new PackageFileMapping("asset.txt", secondDestination)
            ]
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1004");
    }

    [TestMethod]
    public void Validate_WhenTestOrCheckIdIsDuplicated_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            AcceptanceChecks =
            [
                new AcceptanceCheckProfile("duplicate-id", "First", true, "", "", ""),
                new AcceptanceCheckProfile("duplicate-id", "Second", true, "", "", "")
            ]
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1002");
    }

    [TestMethod]
    public void Validate_WhenEvidenceIdIsNotKebabCase_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            TestProjects = [new TestProjectProfile("Not_Kebab", "Tests/Example.Tests.csproj", true)]
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1002");
    }

    [TestMethod]
    public void Validate_WhenRequiredListingInputIsMissing_ReturnsOnip1008()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            WorkshopListing = CreateValidProfile(temporaryDirectory).WorkshopListing with
            {
                Description = "missing-description.bbcode"
            }
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1008");
    }

    [TestMethod]
    [DataRow("unsupported-type", "base-game")]
    [DataRow("tweaks", "unsupported-dlc")]
    public void Validate_WhenListingIdentifierIsUnknown_ReturnsOnip1006(
        string modType,
        string dlcCompatibility)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);
        profile = profile with
        {
            WorkshopListing = profile.WorkshopListing with
            {
                ModTypes = [modType],
                DlcCompatibility = [dlcCompatibility]
            }
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1006");
    }

    [TestMethod]
    public void Validate_WhenPackageMappingsAreEmpty_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with { PackageFiles = [] };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1002");
    }

    [TestMethod]
    public void Validate_WhenRootMetadataDestinationIsMissing_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            PackageFiles = [new PackageFileMapping("mod.yaml", "mod.yaml")]
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1002");
    }

    [TestMethod]
    public void Validate_WhenPrimaryOutputIsNotFromBuildOutput_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);
        profile = profile with
        {
            Build = profile.Build! with { PrimaryOutput = "bin/Example.dll" }
        };

        var result = ModProfileValidator.Validate(profile, ValidMetadata);

        AssertDiagnostic(result, "ONIP1002");
    }

    [TestMethod]
    [DataRow("title")]
    [DataRow("description")]
    [DataRow("static-id")]
    [DataRow("minimum-build")]
    [DataRow("api-version")]
    [DataRow("version")]
    public void Validate_WhenOniMetadataIsInvalid_ReturnsOnip1005(string invalidField)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);
        var metadata = invalidField switch
        {
            "title" => ValidMetadata with { Title = "" },
            "description" => ValidMetadata with { Description = "" },
            "static-id" => ValidMetadata with { StaticId = "invalid id" },
            "minimum-build" => ValidMetadata with { MinimumSupportedBuild = 0 },
            "api-version" => ValidMetadata with { ApiVersion = 0 },
            "version" => ValidMetadata with { Version = "1.2.65535" },
            _ => throw new AssertFailedException($"Unknown fixture field '{invalidField}'.")
        };

        var result = ModProfileValidator.Validate(profile, metadata);

        AssertDiagnostic(result, "ONIP1005");
    }

    private static ModProfile CreateValidProfile(TemporaryDirectory temporaryDirectory)
    {
        Directory.CreateDirectory(temporaryDirectory.GetPath("Source"));
        Directory.CreateDirectory(temporaryDirectory.GetPath("Tests"));
        File.WriteAllText(temporaryDirectory.GetPath("mod.yaml"), "metadata");
        File.WriteAllText(temporaryDirectory.GetPath("mod_info.yaml"), "metadata");
        File.WriteAllText(temporaryDirectory.GetPath("asset.txt"), "asset");
        File.WriteAllText(temporaryDirectory.GetPath("description.bbcode"), "description");
        File.WriteAllText(temporaryDirectory.GetPath("change-notes.bbcode"), "notes");
        File.WriteAllBytes(temporaryDirectory.GetPath("preview.png"), [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllText(temporaryDirectory.GetPath("Source", "Example.csproj"), "<Project />");
        File.WriteAllText(
            temporaryDirectory.GetPath("Tests", "Example.Tests.csproj"),
            "<Project />");

        return new ModProfile(
            1,
            temporaryDirectory.GetPath("oni-mod-pipeline.toml"),
            temporaryDirectory.Path,
            "mod.yaml",
            "mod_info.yaml",
            new BuildProfile(
                "Source/Example.csproj",
                "Release",
                "OniManagedAssemblyDirectory",
                "{build-output}/Example.dll",
                ["PLib"]),
            [
                new PackageFileMapping("mod.yaml", "mod.yaml"),
                new PackageFileMapping("mod_info.yaml", "mod_info.yaml"),
                new PackageFileMapping("asset.txt", "asset.txt"),
                new PackageFileMapping("{build-output}/Example.dll", "Example.dll")
            ],
            new WorkshopListingProfile(
                "description.bbcode",
                "change-notes.bbcode",
                "preview.png",
                ["tweaks"],
                ["base-game"],
                8000,
                8000),
            new LocalInstallProfile("ExampleMod"),
            [new TestProjectProfile("example-regressions", "Tests/Example.Tests.csproj", true)],
            [new AcceptanceCheckProfile("example-check", "Example check", true, "", "", "")]);
    }

    private static void AssertDiagnostic(
        OperationResult<ModProfile> result,
        string diagnosticId)
    {
        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            $"Expected diagnostic {diagnosticId}, but received: " +
            string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Id)));
    }

    private static readonly OniMetadata ValidMetadata = new(
        "MaksymShostak.ExampleMod",
        "Example Mod",
        "Example description",
        "ALL",
        596100,
        "2026.8.27",
        2);
}
