using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class CurrentOniRuntimeContractTests
{
    private const uint ExpectedChangeList = 764025;
    private const string ExpectedBuildBranch = "release";
    private const float ExpectedMaximumTemperatureKelvin = 10000f;
    private const string ExpectedAssemblySha256 =
        "96B192E45E044AA591E7143BBE1126D685DD1A195DB7BA1632FDF4ABD8FE3036";
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
        // Intake may test a newer installation before its reference set and release floor qualify.
        var catalogPath = Path.Combine(await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT"),
            "tools", "ci", "oni-references.json");
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
        var selectedVersion = Version.Parse(catalog.RootElement.GetProperty("package").GetProperty("version").GetString()!);
        await Assert.That(values["minimumSupportedBuild"]).IsEqualTo(
            selectedVersion.Major.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        var body = methodBodies.Single(candidate => candidate.Instructions.Any(
            instruction => instruction.Operation == "ldc.r4" &&
                Equals(instruction.Operand, ExpectedMaximumTemperatureKelvin)));
        var instructions = body.Instructions.ToArray();
        int maximum = Array.FindIndex(instructions, instruction =>
            instruction.Operation == "ldc.r4" && Equals(instruction.Operand, ExpectedMaximumTemperatureKelvin));
        bool deserialize = methodIdentity == "PrimaryElement.OnDeserialized";
        int branch = deserialize ? maximum + 2 : maximum + 1;
        string comparison = deserialize ? "bge.un.s" : "ble.un.s";
        string input = deserialize ? "ldloc.0" : "ldarg.2";

        bool AcceptsMaximum(AssemblyInstructionContract[] code)
        {
            // ECMA-335 short branch displacements are relative to the next instruction.
            // Both game methods fall through into repair and jump over it on success.
            if (code[deserialize ? maximum + 1 : maximum - 1].Operation != input ||
                code[branch].Operation != comparison || code[branch].Operand is not sbyte displacement)
                return false;
            int accepted = Array.FindIndex(code, op => op.Offset == code[branch + 1].Offset + displacement);
            if (accepted <= branch + 1 || accepted + 2 >= code.Length) return false;
            return deserialize
                ? code[branch + 1].ResolvedOperand == "DeserializeWarnings.Instance" &&
                    code[accepted].Operation == "ldarg.0" && code[accepted + 1].Operation == input &&
                    code[accepted + 2].ResolvedOperand == "PrimaryElement._Temperature"
                : code[branch + 1].Operation == "ldstr" &&
                    code[branch + 1].ResolvedOperand!.Contains("temp out of bounds", StringComparison.Ordinal) &&
                    code[accepted].Operation == input && code[accepted + 1].Operation == "ldc.r4" &&
                    Equals(code[accepted + 1].Operand, 0f) && code[accepted + 2].Operation == "bne.un.s";
        }

        await Assert.That(AcceptsMaximum(instructions)).IsTrue().Because(body.FormatInstructions());
        var exclusive = (AssemblyInstructionContract[])instructions.Clone();
        exclusive[branch] = exclusive[branch] with { Operation = deserialize ? "bgt.un.s" : "blt.un.s" };
        await Assert.That(AcceptsMaximum(exclusive)).IsFalse();
        var redirected = (AssemblyInstructionContract[])instructions.Clone();
        redirected[branch] = redirected[branch] with { Operand = (sbyte)0 };
        await Assert.That(AcceptsMaximum(redirected)).IsFalse();
    }

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
