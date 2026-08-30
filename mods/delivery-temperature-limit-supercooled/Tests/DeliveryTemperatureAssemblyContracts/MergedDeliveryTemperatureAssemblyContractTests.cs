using System.Diagnostics;
using System.Security.Cryptography;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class MergedDeliveryTemperatureAssemblyContractTests
{
    private const string ReleaseVersion = "2026.8.26";
    private const string DeterministicTestSourceCommit =
        "0123456789abcdef0123456789abcdef01234567";
    private const string PublishedBaselineSourceCommit =
        "5f7bf43aa823bbb4771936b058c6d573484b6d91";
    private const string PublishedBaselineSha256 =
        "02A14F2E123F42BDD87847C15AB434DAFC8A4D4BC92B465F9DCD367364BF465E";

    public static IEnumerable<object[]> PublishedAssemblyCases
    {
        get
        {
            yield return
            [
                "published-baseline",
                PublishedBaselineSha256,
                "2026.8.26.0",
                PublishedBaselineSourceCommit
            ];
        }
    }

    [TestMethod]
    [DynamicData(nameof(PublishedAssemblyCases))]
    public async Task PublishedAssembly_WhenContractRowIsEvaluated_HasRecordedProvenanceAndSafeReferences(
        string contractRow,
        string expectedSha256,
        string expectedFileVersion,
        string expectedSourceCommit)
    {
        Assert.AreEqual(
            "published-baseline",
            contractRow,
            "Task 1 intentionally provides only the always-present published-baseline row.");

        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var assemblyPath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "DeliveryTemperatureLimit.dll");
        var observedSha256 = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
        var versionInformation = FileVersionInfo.GetVersionInfo(assemblyPath);
        var observedFileVersion = new Version(
            versionInformation.FileMajorPart,
            versionInformation.FileMinorPart,
            versionInformation.FileBuildPart,
            versionInformation.FilePrivatePart).ToString();

        Assert.AreEqual(
            expectedSha256,
            observedSha256,
            $"Published baseline digest changed at {assemblyPath}.");
        Assert.AreEqual(expectedFileVersion, observedFileVersion);
        await AssertGitObjectExistsAsync(repositoryRoot, expectedSourceCommit);
        AssertKnownFrameworkConflictRootsAreNotReferenced(assemblyPath);
    }

    [TestMethod]
    public async Task Build_WhenPipelinePropertiesAreProvided_DoesNotChangeModInfoBytes()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        var modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        var projectPath = Path.Combine(
            modRoot,
            "Source",
            "DeliveryTemperatureLimit.csproj");
        var modInfoPath = Path.Combine(modRoot, "mod_info.yaml");
        using var output = new PipelineTestTemporaryDirectory();
        var paths = CreateIsolatedBuildPaths(output.Path);
        var before = await File.ReadAllBytesAsync(modInfoPath);

        var restore = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "restore",
                projectPath,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}"
            ]);
        Assert.AreEqual(0, restore.ExitCode, restore.FormatEvidence());

        var mergedAssemblyPath = Path.Combine(
            output.Path,
            "DeliveryTemperatureLimit.dll");
        var build = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "build",
                projectPath,
                "--no-restore",
                "--configuration",
                "Release",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={mergedAssemblyPath}",
                $"-p:BaseOutputPath={paths.BaseOutputPath}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}"
            ]);

        Assert.AreEqual(0, build.ExitCode, build.FormatEvidence());
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(modInfoPath));
        Assert.IsTrue(File.Exists(mergedAssemblyPath));
        AssertKnownFrameworkConflictRootsAreNotReferenced(mergedAssemblyPath);
        IntentionalRuntimeContractTests.AssertMergedAssembly(mergedAssemblyPath);
    }

    [TestMethod]
    public async Task Build_WhenBuiltTwiceFromSameInputs_ProducesSameMergedAssemblyHash()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        using var firstOutput = new PipelineTestTemporaryDirectory();
        using var secondOutput = new PipelineTestTemporaryDirectory();

        var firstAssembly = await BuildAssemblyAsync(
            repositoryRoot,
            managedDirectory,
            firstOutput);
        var secondAssembly = await BuildAssemblyAsync(
            repositoryRoot,
            managedDirectory,
            secondOutput);
        var firstHash = SHA256.HashData(await File.ReadAllBytesAsync(firstAssembly));
        var secondHash = SHA256.HashData(await File.ReadAllBytesAsync(secondAssembly));

        CollectionAssert.AreEqual(firstHash, secondHash);
    }

    private static async Task<string> BuildAssemblyAsync(
        string repositoryRoot,
        string managedDirectory,
        PipelineTestTemporaryDirectory output)
    {
        var projectPath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "DeliveryTemperatureLimit.csproj");
        var mergedAssemblyPath = Path.Combine(
            output.Path,
            "DeliveryTemperatureLimit.dll");
        var paths = CreateIsolatedBuildPaths(output.Path);
        var restore = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "restore",
                projectPath,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}"
            ]);
        Assert.AreEqual(0, restore.ExitCode, restore.FormatEvidence());

        var build = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "build",
                projectPath,
                "--no-restore",
                "--configuration",
                "Release",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={mergedAssemblyPath}",
                $"-p:BaseOutputPath={paths.BaseOutputPath}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}",
                $"-p:Version={ReleaseVersion}",
                $"-p:InformationalVersion={ReleaseVersion}+{DeterministicTestSourceCommit[..12]}",
                "-p:Deterministic=true",
                "-p:ContinuousIntegrationBuild=true",
                $"-p:PathMap=\"{CreateDeterministicPathMap(output.Path, repositoryRoot)}\""
            ]);
        Assert.AreEqual(0, build.ExitCode, build.FormatEvidence());
        Assert.IsTrue(File.Exists(mergedAssemblyPath));
        AssertKnownFrameworkConflictRootsAreNotReferenced(mergedAssemblyPath);
        IntentionalRuntimeContractTests.AssertMergedAssembly(mergedAssemblyPath);
        return mergedAssemblyPath;
    }

    private static (string BaseOutputPath, string BaseIntermediateOutputPath)
        CreateIsolatedBuildPaths(string outputRoot)
    {
        var baseOutputPath = Path.Combine(
                outputRoot,
                "bin",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var baseIntermediateOutputPath = Path.Combine(
                outputRoot,
                "obj",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        return (baseOutputPath, baseIntermediateOutputPath);
    }

    private static string CreateDeterministicPathMap(
        string outputRoot,
        string repositoryRoot) =>
        $"{EscapePathMapComponent(Path.GetFullPath(outputRoot))}=/_build/," +
        $"{EscapePathMapComponent(Path.GetFullPath(repositoryRoot))}=/_/";

    private static string EscapePathMapComponent(string value) =>
        value
            .Replace("=", "==", StringComparison.Ordinal)
            .Replace(",", ",,", StringComparison.Ordinal);

    private static void AssertKnownFrameworkConflictRootsAreNotReferenced(
        string assemblyPath)
    {
        var prohibitedReferences = new[]
        {
            "System.IO.Compression",
            "System.Net.Http"
        };
        var references = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyReferences(assemblyPath)
            .Select(reference => reference.Name)
            .ToArray();

        foreach (var prohibitedReference in prohibitedReferences)
        {
            CollectionAssert.DoesNotContain(
                references,
                prohibitedReference,
                $"{assemblyPath} must not directly reference {prohibitedReference}.");
        }
    }

    private static async Task AssertGitObjectExistsAsync(
        string repositoryRoot,
        string objectId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"{objectId}^{{commit}}");

        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start(), "git cat-file did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"Recorded source commit {objectId} is unavailable. " +
            $"Standard output: {await standardOutput}. Standard error: {await standardError}.");
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }
}
