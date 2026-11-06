using MaksymShostak.OniModPipeline.WorkshopContent;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopContent;
public sealed class WorkshopContentValidatorTests
{
    [Test]
    public async Task ValidateInventory_WhenForbiddenContentIsPresent_RejectsEveryV1ForbiddenPath()
    {
        string[] forbiddenPaths =
        [
            "0Harmony.dll",
            "Assembly-CSharp.dll",
            "Assembly-CSharp-firstpass.dll",
            "UnityEngine.dll",
            "Unity.Custom.dll",
            "FMOD.dll",
            "FMODStudio.dll",
            "Newtonsoft.Json.dll",
            "PLib.dll",
            "Preview.png",
            "STEAM_DESCRIPTION.bbcode",
            "Source/Mod.cs",
            "Source/Mod.csproj",
            "Source/Mod.sln",
            "Source/Mod.slnx",
            "scripts/build.ps1",
            "scripts/build.bat",
            "scripts/package-content.sh",
            "Example.pdb",
            "packages.lock.json",
            "dependencies.lock",
            "build.log",
            "bin/Example.dll",
            "obj/generated.cs",
            "Tests/Example.Tests.dll",
            "release-evidence/report.json",
            "translations/delivery_temperature_limit.pot",
            "docs/guide.md"
        ];
        var validator = new WorkshopContentValidator();

        foreach (var forbiddenPath in forbiddenPaths)
        {
            var result = validator.ValidateInventory(
                ["mod.yaml", "mod_info.yaml", "Example.dll", forbiddenPath],
                "Example.dll");

            await Assert.That(result.IsSuccess).IsFalse().Because(forbiddenPath);
            await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP5002").Because(forbiddenPath);
        }
    }

    [Test]
    public async Task ValidateInventory_WhenRequiredRootMetadataIsMissing_RejectsInventory()
    {
        var validator = new WorkshopContentValidator();

        var missingModYaml = validator.ValidateInventory(
            ["mod_info.yaml", "Example.dll"],
            "Example.dll");
        var missingModInfo = validator.ValidateInventory(
            ["mod.yaml", "Example.dll"],
            "Example.dll");

        await Assert.That(missingModYaml.IsSuccess).IsFalse();
        await Assert.That(missingModInfo.IsSuccess).IsFalse();
        await Assert.That(missingModYaml.Diagnostics.Single().Evidence).Contains("mod.yaml");
        await Assert.That(missingModInfo.Diagnostics.Single().Evidence).Contains("mod_info.yaml");
    }

    [Test]
    public async Task ValidateInventory_WhenRootMetadataUsesDifferentCase_RejectsInventory()
    {
        var result = new WorkshopContentValidator().ValidateInventory(
            ["MOD.YAML", "MOD_INFO.YAML", "Example.dll"],
            "Example.dll");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Evidence).Contains("mod.yaml");
    }

    [Test]
    public async Task ValidateInventory_WhenPrimaryAssemblyIsNested_RejectsInventory()
    {
        var result = new WorkshopContentValidator().ValidateInventory(
            ["mod.yaml", "mod_info.yaml", "assemblies/Example.dll"],
            "assemblies/Example.dll");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Diagnostics.Single().Evidence).Contains("root");
    }

    [Test]
    public async Task ValidateInventory_WhenRuntimeSetIsClosed_AcceptsNormalizedPaths()
    {
        var result = new WorkshopContentValidator().ValidateInventory(
            ["mod.yaml", "mod_info.yaml", "Example.dll", "assets/config.json"],
            "Example.dll");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value?.ToArray()).IsEquivalentTo(new[] { "Example.dll", "assets/config.json", "mod.yaml", "mod_info.yaml" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
