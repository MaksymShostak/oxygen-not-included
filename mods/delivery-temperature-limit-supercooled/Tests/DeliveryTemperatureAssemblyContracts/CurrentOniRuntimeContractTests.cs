using System.Diagnostics;
using System.Security.Cryptography;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class CurrentOniRuntimeContractTests
{
    private const uint ExpectedChangeList = 744825;
    private const string ExpectedBuildBranch = "release";
    private const float ExpectedMaximumTemperatureKelvin = 10000f;
    private const string ExpectedAssemblySha256 =
        "A58E04D0FFDF89B86FB28B71AD900625B3B539DB30D67F8C6269F73A9F5AE599";
    private const string ExpectedHarmonyAssemblySha256 =
        "AEC7446028FE31D00DFECC684E40011C4208BB036E82617B8DE32002A8E55B53";

    [Test]
    public async Task InstalledAssembly_WhenInspected_MatchesCurrentPublicRuntimeContract()
    {
        var assemblyPath = await ResolveInstalledAssemblyPath();
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

        await Assert.That(digest).IsEqualTo(ExpectedAssemblySha256).Because(evidence);
        await Assert.That(changeList).IsEqualTo(ExpectedChangeList).Because(evidence);
        await Assert.That(buildBranch).IsEqualTo(ExpectedBuildBranch).Because(evidence);
        await Assert.That(maximumTemperature).IsEqualTo(ExpectedMaximumTemperatureKelvin).Because(evidence);
    }

    [Test]
    public async Task InstalledAssembly_WhenTemperatureValidationMethodsAreInspected_UsesInclusiveMaximumBound()
    {
        var assemblyPath = await ResolveInstalledAssemblyPath();
        var primaryElementMethods = DeliveryTemperatureAssemblyMetadataReader
            .ReadMethodBodies(assemblyPath, "PrimaryElement", "OnDeserialized");
        var modifyCellMethods = DeliveryTemperatureAssemblyMetadataReader
            .ReadMethodBodies(assemblyPath, "SimMessages", "ModifyCell");

        await AssertInclusiveMaximumEvidence(
            "PrimaryElement.OnDeserialized",
            primaryElementMethods);
        await AssertInclusiveMaximumEvidence(
            "SimMessages.ModifyCell",
            modifyCellMethods);
    }

    [Test]
    public async Task ModMetadata_MatchesSupportedOniRuntimeContract()
    {
        var metadataPath = Path.Combine(
            (await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")),
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

        await Assert.That(values["supportedContent"]).IsEqualTo("ALL");
        await Assert.That(values["minimumSupportedBuild"]).IsEqualTo(ExpectedChangeList.ToString());
        await Assert.That(values["APIVersion"]).IsEqualTo("2");
        // The pipeline owns metadata schema and mod release-version validation.
    }

    [Test]
    public async Task InstalledHarmonyAssembly_WhenInspected_MatchesLockedPlibReferenceContract()
    {
        var assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                (await RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY")),
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

        await Assert.That(digest).IsEqualTo(ExpectedHarmonyAssemblySha256).Because(evidence);
        await Assert.That(assemblyVersion).IsEqualTo(expectedVersion).Because(evidence);
        await Assert.That(fileVersion).IsEqualTo(expectedVersion).Because(evidence);
    }

    [Test]
    public async Task ManagedAssemblyResolver_WhenDirectoryIsRelative_RejectsAmbiguousDiscovery()
    {
        await Assert.That(() =>
            DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
                ".",
                "Assembly-CSharp.dll")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ManagedAssemblyResolver_WhenAssemblyNameEscapesDirectory_RejectsPathTraversal()
    {
        var managedDirectory = await RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");

        await Assert.That(() =>
            DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
                managedDirectory,
                $"..{Path.DirectorySeparatorChar}Assembly-CSharp.dll")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ManagedAssemblyResolver_WhenAssemblyIsMissing_ReportsExactCandidate()
    {
        var managedDirectory = await RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");

        var exception = (await Assert.That(() =>
            DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
                managedDirectory,
                "Assembly-Definitely-Missing.dll")).ThrowsExactly<FileNotFoundException>())!;

        await Assert.That(exception.Message).Contains(Path.Combine(managedDirectory, "Assembly-Definitely-Missing.dll"));
    }

    private static async Task AssertInclusiveMaximumEvidence(
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

        await Assert.That(methodBodies).IsNotEmpty().Because($"No metadata body was found for {methodIdentity}.");
        await Assert.That(methodsWithMaximum).IsNotEmpty().Because($"{methodIdentity} does not contain the reviewed 10000 K bound. " +
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
        await Assert.That(methodsWithMaximum.Any(body => body.Instructions.Any(instruction =>
                inclusiveComparisonOperations.Contains(instruction.Operation)))).IsTrue().Because($"{methodIdentity} no longer has the reviewed inclusive-bound comparison shape. " +
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

    private static async Task<string> ResolveInstalledAssemblyPath() =>
        DeliveryTemperatureAssemblyMetadataReader.ResolveManagedAssemblyPath(
            (await RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY")),
            "Assembly-CSharp.dll");

    private static async Task<string> RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse().Because($"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value!;
    }
}
