using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class CurrentOniRuntimeContractTests
{
    private const string ExpectedBuildBranch = "release";
    private const float ExpectedMaximumTemperatureKelvin = 10000f;

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

        var repository = await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        string selected = await SelectedReferenceVersion(repository);
        await Assert.That(selected).IsEqualTo(FormattableString.Invariant($"{changeList}.0.0")).Because(evidence);
        var verify = new ProcessStartInfo(Environment.GetEnvironmentVariable("ONI_MOD_PIPELINE_PYTHON") ??
            (OperatingSystem.IsWindows() ? "python.exe" : "python3"))
        {
            WorkingDirectory = repository, UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true
        };
        verify.ArgumentList.Add("-I");
        verify.ArgumentList.Add("-B");
        verify.ArgumentList.Add(Path.Combine(repository, "tools", "ci", "oni_references.py"));
        verify.ArgumentList.Add("verify-installed");
        verify.ArgumentList.Add("--managed-directory");
        verify.ArgumentList.Add(Path.GetDirectoryName(assemblyPath)!);
        using var process = Process.Start(verify)!;
        var diagnostics = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Assert.That(process.ExitCode).IsEqualTo(0).Because(await diagnostics);
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
        var repository = await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var arguments = new List<string>();
        if (Environment.GetEnvironmentVariable("ONI_MOD_PIPELINE_CLI_ASSEMBLY") is { Length: > 0 } cli)
            arguments.Add(cli);
        else
            arguments.AddRange(["run", "--project", "tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj", "--no-restore", "--"]);
        arguments.AddRange(["sync-build-floor", "--mod", "mods/delivery-temperature-limit-supercooled", "--format", "json"]);
        if (Environment.GetEnvironmentVariable("ONI_MOD_PIPELINE_PYTHON") is { Length: > 0 } python)
            arguments.AddRange(["--python", python]);
        var result = await DotnetCommandRunner.RunAsync(repository, arguments);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.FormatEvidence());
        using var response = JsonDocument.Parse(result.StandardOutput);
        var metadata = response.RootElement.GetProperty("value");
        await Assert.That(metadata.GetProperty("supportedContent").GetString()).IsEqualTo("ALL");
        await Assert.That(metadata.GetProperty("apiVersion").GetInt32()).IsEqualTo(2);
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
        var repository = await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        using var packageLock = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(repository, "mods",
            "delivery-temperature-limit-supercooled", "Source", "packages.lock.json")));
        var resolved = Version.Parse(packageLock.RootElement.GetProperty("dependencies").GetProperty(".NETStandard,Version=v2.1")
            .GetProperty("Lib.Harmony.Ref").GetProperty("resolved").GetString()!);
        var expectedVersion = new Version(resolved.Major, resolved.Minor, resolved.Build, Math.Max(0, resolved.Revision));
        var evidence =
            $"Observed path={assemblyPath}; SHA-256={digest}; " +
            $"assembly version={assemblyVersion}; file version={fileVersion}.";

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

    private static async Task<string> SelectedReferenceVersion(string repository)
    {
        using var catalog = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(repository, "tools", "ci", "oni-references.json")));
        return catalog.RootElement.GetProperty("package").GetProperty("version").GetString()!;
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
