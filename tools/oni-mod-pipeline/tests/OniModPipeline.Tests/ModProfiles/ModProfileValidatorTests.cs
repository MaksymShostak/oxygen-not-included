using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.ModProfiles;
public sealed class ModProfileValidatorTests
{
    private readonly ModProfileValidator profileValidator = new();

    [Test]
    [Arguments("")]
    [Arguments("../README.md")]
    [Arguments("/README.md")]
    [Arguments("C:\\README.md")]
    [Arguments("docs//README.md")]
    public async Task Validate_WithUnsafeReadmePath_RejectsProfile(string path)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with { Readme = new ReadmeProfile(path) };
        var result = profileValidator.Validate(profile, ValidMetadata);
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Evidence.Contains("readme.repository-path", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validate_WhenProfileIsPortableAndComplete_ReturnsSuccess()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);

        var result = profileValidator.Validate(profile, ValidMetadata);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsSameReferenceAs(profile);
        await Assert.That(result.Diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("2026.9.8")]
    [Arguments("2027.1.0")]
    public async Task Validate_WhenModReleaseAdvances_PreservesRuntimeCompatibility(string version)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);
        var metadata = ValidMetadata with { Version = version };

        var result = profileValidator.Validate(profile, metadata);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(metadata.MinimumSupportedBuild).IsEqualTo(ValidMetadata.MinimumSupportedBuild);
        await Assert.That(metadata.ApiVersion).IsEqualTo(ValidMetadata.ApiVersion);
    }

    [Test]
    [Arguments("")]
    [Arguments("not-a-version")]
    [Arguments("1.2.65535")]
    public async Task Validate_WhenReleaseVersionIsInvalid_ReturnsOnip1005(string version)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);

        var result = profileValidator.Validate(profile, ValidMetadata with { Version = version });

        await AssertDiagnostic(result, "ONIP1005");
    }

    [Test]
    [Arguments("Assets/config.json", "Assets\\config.json")]
    [Arguments("Assets/Café.txt", "Assets/Café.txt")]
    [Arguments("Assets/File.txt", "assets/file.txt")]
    public async Task Validate_WhenPackageDestinationsCollidePortably_ReturnsOnip1004(
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

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1004");
    }

    [Test]
    public async Task Validate_WhenTestOrCheckIdIsDuplicated_ReturnsInvalidProfileDiagnostic()
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

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1002");
    }

    [Test]
    public async Task Validate_WhenEvidenceIdIsNotKebabCase_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            TestProjects = [new TestProjectProfile("Not_Kebab", "Tests/Example.Tests.csproj", true)]
        };

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1002");
    }

    [Test]
    public async Task Validate_WhenRequiredListingInputIsMissing_ReturnsOnip1008()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            WorkshopListing = CreateValidProfile(temporaryDirectory).WorkshopListing with
            {
                Description = "missing-description.bbcode"
            }
        };

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1008");
    }

    [Test]
    [Arguments("unsupported-type", "base-game")]
    [Arguments("tweaks", "unsupported-dlc")]
    public async Task Validate_WhenListingIdentifierIsUnknown_ReturnsOnip1006(
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

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1006");
    }

    [Test]
    public async Task Validate_WhenPackageMappingsAreEmpty_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with { PackageFiles = [] };

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1002");
    }

    [Test]
    public async Task Validate_WhenRootMetadataDestinationIsMissing_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory) with
        {
            PackageFiles = [new PackageFileMapping("mod.yaml", "mod.yaml")]
        };

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1002");
    }

    [Test]
    public async Task Validate_WhenPrimaryOutputIsNotFromBuildOutput_ReturnsInvalidProfileDiagnostic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateValidProfile(temporaryDirectory);
        profile = profile with
        {
            Build = profile.Build! with { PrimaryOutput = "bin/Example.dll" }
        };

        var result = profileValidator.Validate(profile, ValidMetadata);

        await AssertDiagnostic(result, "ONIP1002");
    }

    [Test]
    [Arguments("title")]
    [Arguments("description")]
    [Arguments("static-id")]
    [Arguments("minimum-build")]
    [Arguments("api-version")]
    [Arguments("version")]
    public async Task Validate_WhenOniMetadataIsInvalid_ReturnsOnip1005(string invalidField)
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
            _ => throw new TUnit.Assertions.Exceptions.AssertionException($"Unknown fixture field '{invalidField}'.")
        };

        var result = profileValidator.Validate(profile, metadata);

        await AssertDiagnostic(result, "ONIP1005");
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

    private static async Task AssertDiagnostic(
        OperationResult<ModProfile> result,
        string diagnosticId)
    {
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.InvalidInput);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId)).IsTrue().Because($"Expected diagnostic {diagnosticId}, but received: " +
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
