using MaksymShostak.OniModPipeline.WorkshopContent;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopContent;

[TestClass]
public sealed class WorkshopContentValidatorTests
{
    [TestMethod]
    public void ValidateInventory_WhenForbiddenContentIsPresent_RejectsEveryV1ForbiddenPath()
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
            "scripts/build.sh",
            "Example.pdb",
            "packages.lock.json",
            "dependencies.lock",
            "build.log",
            "bin/Example.dll",
            "obj/generated.cs",
            "Tests/Example.Tests.dll",
            "release-evidence/report.json"
        ];
        var validator = new WorkshopContentValidator();

        foreach (var forbiddenPath in forbiddenPaths)
        {
            var result = validator.ValidateInventory(
                ["mod.yaml", "mod_info.yaml", "Example.dll", forbiddenPath],
                "Example.dll");

            Assert.IsFalse(result.IsSuccess, forbiddenPath);
            Assert.AreEqual("ONIP5002", result.Diagnostics.Single().Id, forbiddenPath);
        }
    }

    [TestMethod]
    public void ValidateInventory_WhenRequiredRootMetadataIsMissing_RejectsInventory()
    {
        var validator = new WorkshopContentValidator();

        var missingModYaml = validator.ValidateInventory(
            ["mod_info.yaml", "Example.dll"],
            "Example.dll");
        var missingModInfo = validator.ValidateInventory(
            ["mod.yaml", "Example.dll"],
            "Example.dll");

        Assert.IsFalse(missingModYaml.IsSuccess);
        Assert.IsFalse(missingModInfo.IsSuccess);
        StringAssert.Contains(missingModYaml.Diagnostics.Single().Evidence, "mod.yaml");
        StringAssert.Contains(missingModInfo.Diagnostics.Single().Evidence, "mod_info.yaml");
    }

    [TestMethod]
    public void ValidateInventory_WhenRootMetadataUsesDifferentCase_RejectsInventory()
    {
        var result = new WorkshopContentValidator().ValidateInventory(
            ["MOD.YAML", "MOD_INFO.YAML", "Example.dll"],
            "Example.dll");

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "mod.yaml");
    }

    [TestMethod]
    public void ValidateInventory_WhenPrimaryAssemblyIsNested_RejectsInventory()
    {
        var result = new WorkshopContentValidator().ValidateInventory(
            ["mod.yaml", "mod_info.yaml", "assemblies/Example.dll"],
            "assemblies/Example.dll");

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "root");
    }

    [TestMethod]
    public void ValidateInventory_WhenRuntimeSetIsClosed_AcceptsNormalizedPaths()
    {
        var result = new WorkshopContentValidator().ValidateInventory(
            ["mod.yaml", "mod_info.yaml", "Example.dll", "assets/config.json"],
            "Example.dll");

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "Example.dll", "assets/config.json", "mod.yaml", "mod_info.yaml" },
            result.Value?.ToArray());
    }
}
