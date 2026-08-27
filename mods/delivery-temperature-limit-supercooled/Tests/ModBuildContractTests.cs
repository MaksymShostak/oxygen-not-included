using System.Security.Cryptography;

namespace DeliveryTemperatureLimit.Tests;

[TestClass]
public sealed class ModBuildContractTests
{
    private const string ReleaseVersion = "2026.8.26";
    private const string SourceCommit = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public async Task Build_WhenPipelinePropertiesAreProvided_DoesNotChangeModInfoBytes()
    {
        var repositoryRoot = RequiredEnvironmentVariable("ONI_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY");
        var modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        var project = Path.Combine(modRoot, "Source", "DeliveryTemperatureLimit.csproj");
        var modInfo = Path.Combine(modRoot, "mod_info.yaml");
        using var output = new TemporaryDirectory();
        var baseOutputPath = Path.Combine(
                output.Path,
                "bin",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var baseIntermediateOutputPath = Path.Combine(
                output.Path,
                "obj",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var before = await File.ReadAllBytesAsync(modInfo);

        var restore = await DotnetProcess.RunAsync(
            repositoryRoot,
            [
                "restore",
                project,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
            ]);
        Assert.AreEqual(0, restore.ExitCode, ProcessEvidence(restore));

        var result = await DotnetProcess.RunAsync(
            repositoryRoot,
            [
                "build",
                project,
                "--no-restore",
                "--configuration",
                "Release",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={Path.Combine(output.Path, "DeliveryTemperatureLimit.dll")}",
                $"-p:BaseOutputPath={baseOutputPath}",
                $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
            ]);

        Assert.AreEqual(0, result.ExitCode, ProcessEvidence(result));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(modInfo));
        Assert.IsTrue(File.Exists(Path.Combine(
            output.Path,
            "DeliveryTemperatureLimit.dll")));
    }

    [TestMethod]
    public async Task ModernizedBuild_WhenComparedWithTrackedLegacyDll_PreservesPublicSurface()
    {
        var repositoryRoot = RequiredEnvironmentVariable("ONI_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY");
        using var output = new TemporaryDirectory();

        var modernizedAssembly = await BuildAssemblyAsync(
            repositoryRoot,
            managedDirectory,
            output);
        var legacyAssembly = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "DeliveryTemperatureLimit.dll");

        CollectionAssert.AreEqual(
            PublicAssemblySurface.Read(legacyAssembly).ToArray(),
            PublicAssemblySurface.Read(modernizedAssembly).ToArray());
    }

    [TestMethod]
    public async Task ModernizedBuild_WhenBuiltTwiceFromSameInputs_ProducesSameMergedDllHash()
    {
        var repositoryRoot = RequiredEnvironmentVariable("ONI_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY");
        using var firstOutput = new TemporaryDirectory();
        using var secondOutput = new TemporaryDirectory();

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
        TemporaryDirectory output)
    {
        var project = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "DeliveryTemperatureLimit.csproj");
        var mergedOutput = Path.Combine(output.Path, "DeliveryTemperatureLimit.dll");
        var baseOutputPath = Path.Combine(
                output.Path,
                "bin",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var baseIntermediateOutputPath = Path.Combine(
                output.Path,
                "obj",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var restore = await DotnetProcess.RunAsync(
            repositoryRoot,
            [
                "restore",
                project,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
            ]);
        Assert.AreEqual(0, restore.ExitCode, ProcessEvidence(restore));

        var build = await DotnetProcess.RunAsync(
            repositoryRoot,
            [
                "build",
                project,
                "--no-restore",
                "--configuration",
                "Release",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={mergedOutput}",
                $"-p:BaseOutputPath={baseOutputPath}",
                $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}",
                $"-p:Version={ReleaseVersion}",
                $"-p:InformationalVersion={ReleaseVersion}+{SourceCommit[..12]}",
                "-p:Deterministic=true",
                "-p:ContinuousIntegrationBuild=true",
                $"-p:PathMap=\"{CreateDeterministicPathMap(output.Path, repositoryRoot)}\""
            ]);
        Assert.AreEqual(0, build.ExitCode, ProcessEvidence(build));
        Assert.IsTrue(File.Exists(mergedOutput));
        return mergedOutput;
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

    private static string ProcessEvidence(DotnetProcessResult result)
    {
        var standardError = string.IsNullOrWhiteSpace(result.StandardError)
            ? "<empty>"
            : result.StandardError.Trim();
        var standardOutput = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? "<empty>"
            : result.StandardOutput.Trim();
        return $"dotnet exited {result.ExitCode}. Standard error: {standardError}. " +
            $"Standard output: {standardOutput}.";
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
