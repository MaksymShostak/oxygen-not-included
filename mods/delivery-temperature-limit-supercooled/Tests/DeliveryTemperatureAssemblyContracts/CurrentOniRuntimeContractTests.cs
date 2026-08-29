using System.Diagnostics;
using System.Security.Cryptography;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class CurrentOniRuntimeContractTests
{
    private const uint ExpectedChangeList = 744825;
    private const string ExpectedBuildBranch = "release";
    private const float ExpectedMaximumTemperatureKelvin = 10000f;
    private const string ExpectedAssemblySha256 =
        "A58E04D0FFDF89B86FB28B71AD900625B3B539DB30D67F8C6269F73A9F5AE599";
    private const string ExpectedHarmonyAssemblySha256 =
        "AEC7446028FE31D00DFECC684E40011C4208BB036E82617B8DE32002A8E55B53";

    [TestMethod]
    public async Task InstalledAssembly_WhenInspected_MatchesCurrentPublicRuntimeContract()
    {
        var assemblyPath = ResolveInstalledAssemblyPath();
        var digest = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
        var assemblyVersion = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyVersion(assemblyPath);
        var changeList = DeliveryTemperatureAssemblyMetadataReader
            .ReadFieldConstant(assemblyPath, "KleiVersion", "ChangeList");
        var buildBranch = DeliveryTemperatureAssemblyMetadataReader
            .ReadFieldConstant(assemblyPath, "KleiVersion", "BuildBranch");
        var maximumTemperature = DeliveryTemperatureAssemblyMetadataReader
            .ReadFieldConstant(assemblyPath, "Sim", "MaxTemperature");
        var evidence =
            $"Observed path={assemblyPath}; SHA-256={digest}; " +
            $"assembly version={assemblyVersion}; " +
            $"KleiVersion.ChangeList={changeList}; " +
            $"KleiVersion.BuildBranch={buildBranch}; " +
            $"Sim.MaxTemperature={maximumTemperature}.";

        Assert.AreEqual(ExpectedAssemblySha256, digest, evidence);
        Assert.AreEqual(ExpectedChangeList, changeList, evidence);
        Assert.AreEqual(ExpectedBuildBranch, buildBranch, evidence);
        Assert.AreEqual(ExpectedMaximumTemperatureKelvin, maximumTemperature, evidence);
    }

    [TestMethod]
    public void InstalledAssembly_WhenTemperatureValidationMethodsAreInspected_UsesInclusiveMaximumBound()
    {
        var assemblyPath = ResolveInstalledAssemblyPath();
        var primaryElementMethods = DeliveryTemperatureAssemblyMetadataReader
            .ReadMethodBodies(assemblyPath, "PrimaryElement", "OnDeserialized");
        var modifyCellMethods = DeliveryTemperatureAssemblyMetadataReader
            .ReadMethodBodies(assemblyPath, "SimMessages", "ModifyCell");

        AssertInclusiveMaximumEvidence(
            "PrimaryElement.OnDeserialized",
            primaryElementMethods);
        AssertInclusiveMaximumEvidence(
            "SimMessages.ModifyCell",
            modifyCellMethods);
    }

    [TestMethod]
    public void ModMetadata_WhenInspected_DeclaresOnlyCurrentPublicOniAsMinimumBuild()
    {
        var metadataPath = Path.Combine(
            RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT"),
            "mods",
            "delivery-temperature-limit-supercooled",
            "mod_info.yaml");
        var values = File.ReadAllLines(metadataPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(':', count: 2))
            .ToDictionary(
                parts => parts[0].Trim(),
                parts => parts[1].Trim(),
                StringComparer.Ordinal);

        Assert.AreEqual("ALL", values["supportedContent"]);
        Assert.AreEqual(ExpectedChangeList.ToString(), values["minimumSupportedBuild"]);
        Assert.AreEqual("2026.8.26", values["version"]);
        Assert.AreEqual("2", values["APIVersion"]);
        Assert.HasCount(4, values);
    }

    [TestMethod]
    public async Task InstalledHarmonyAssembly_WhenInspected_MatchesLockedPlibReferenceContract()
    {
        var assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY"),
                "0Harmony.dll");
        var digest = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
        var assemblyVersion = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyVersion(assemblyPath);
        var fileVersionInformation = FileVersionInfo.GetVersionInfo(assemblyPath);
        var fileVersion = new Version(
            fileVersionInformation.FileMajorPart,
            fileVersionInformation.FileMinorPart,
            fileVersionInformation.FileBuildPart,
            fileVersionInformation.FilePrivatePart);
        var expectedVersion = new Version(2, 4, 2, 0);
        var evidence =
            $"Observed path={assemblyPath}; SHA-256={digest}; " +
            $"assembly version={assemblyVersion}; file version={fileVersion}.";

        Assert.AreEqual(ExpectedHarmonyAssemblySha256, digest, evidence);
        Assert.AreEqual(expectedVersion, assemblyVersion, evidence);
        Assert.AreEqual(expectedVersion, fileVersion, evidence);
    }

    [TestMethod]
    public void ManagedAssemblyResolver_WhenDirectoryIsRelative_RejectsAmbiguousDiscovery()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
                ".",
                "Assembly-CSharp.dll"));
    }

    [TestMethod]
    public void ManagedAssemblyResolver_WhenAssemblyNameEscapesDirectory_RejectsPathTraversal()
    {
        var managedDirectory = RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");

        Assert.ThrowsExactly<ArgumentException>(() =>
            DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
                managedDirectory,
                $"..{Path.DirectorySeparatorChar}Assembly-CSharp.dll"));
    }

    [TestMethod]
    public void ManagedAssemblyResolver_WhenAssemblyIsMissing_ReportsExactCandidate()
    {
        var managedDirectory = RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");

        var exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
            DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
                managedDirectory,
                "Assembly-Definitely-Missing.dll"));

        StringAssert.Contains(
            exception.Message,
            Path.Combine(managedDirectory, "Assembly-Definitely-Missing.dll"));
    }

    private static void AssertInclusiveMaximumEvidence(
        string methodIdentity,
        IReadOnlyList<AssemblyMethodBodyContract> methodBodies)
    {
        var methodsWithMaximum = methodBodies
            .Where(body => body.Instructions.Any(IsExpectedMaximumOperand))
            .ToArray();
        var observedBodies = string.Join(
            Environment.NewLine + Environment.NewLine,
            methodBodies.Select(body =>
                $"{body.DeclaringType}.{body.MethodName} signature {body.Signature}" +
                Environment.NewLine +
                body.FormatInstructions()));

        Assert.IsNotEmpty(
            methodBodies,
            $"No metadata body was found for {methodIdentity}.");
        Assert.IsNotEmpty(
            methodsWithMaximum,
            $"{methodIdentity} does not contain the reviewed 10000 K bound. " +
            $"Observed IL:{Environment.NewLine}{observedBodies}");

        var inclusiveComparisonOperations = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "ble",
            "ble.s",
            "ble.un",
            "ble.un.s",
            "bgt",
            "bgt.s",
            "bgt.un",
            "bgt.un.s",
            "clt",
            "clt.un"
        };
        Assert.IsTrue(
            methodsWithMaximum.Any(body => body.Instructions.Any(instruction =>
                inclusiveComparisonOperations.Contains(instruction.Operation))),
            $"{methodIdentity} no longer has the reviewed inclusive-bound comparison shape. " +
            $"Observed IL:{Environment.NewLine}{observedBodies}");
    }

    private static bool IsExpectedMaximumOperand(
        AssemblyInstructionContract instruction) =>
        instruction.Operand switch
        {
            float value => value.Equals(ExpectedMaximumTemperatureKelvin),
            double value => value.Equals(ExpectedMaximumTemperatureKelvin),
            int value => value == (int)ExpectedMaximumTemperatureKelvin,
            _ => false
        };

    private static string ResolveInstalledAssemblyPath() =>
        DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
            RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY"),
            "Assembly-CSharp.dll");

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }
}
