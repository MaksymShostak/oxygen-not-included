using System.Xml.Linq;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class KnownOniReferenceConflictContractTests
{

    [Test]
    public async Task ProductionBuild_WhenCurrentOniAssembliesAreUsed_ReportsOnlyReviewedConflictRoots()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = await RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        var projectPath = ProductionProjectPath(repositoryRoot);
        using var output = new PipelineTestTemporaryDirectory();
        var baseIntermediateOutputPath = Path.Combine(
                output.Path,
                "obj",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var baseOutputPath = Path.Combine(
                output.Path,
                "bin",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;

        var restore = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "restore",
                projectPath,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
            ]);
        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.FormatEvidence());

        var productionBuild = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "build",
                projectPath,
                "--no-restore",
                "--configuration",
                "Release",
                "--verbosity",
                "minimal",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={Path.Combine(output.Path, "DeliveryTemperatureLimit.dll")}",
                $"-p:BaseOutputPath={baseOutputPath}",
                $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
            ]);
        await Assert.That(productionBuild.ExitCode).IsEqualTo(0).Because(productionBuild.FormatEvidence());

        var combinedOutput = productionBuild.StandardOutput +
            Environment.NewLine +
            productionBuild.StandardError;
        // U60 targets .NET Standard 2.1. Its game references no longer introduce the old Framework conflicts.
        await Assert.That(combinedOutput).DoesNotContain("warning MSB3277");
    }

    [Test]
    public async Task ProductionProject_WhenInspected_ContainsNoConflictSuppressionOrReferenceWorkaround()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var projectPath = ProductionProjectPath(repositoryRoot);
        var project = XDocument.Load(projectPath);
        var prohibitedProperties = new[]
        {
            "NoWarn",
            "AutoUnify",
            "AutoGenerateBindingRedirects",
            "GenerateBindingRedirectsOutputType"
        };
        foreach (var property in prohibitedProperties)
        {
            await Assert.That(project.Descendants(property).Any()).IsFalse().Because($"{property} must not conceal the reviewed ONI reference conflicts.");
        }

        var prohibitedDirectReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "System.IO.Compression", "System.Net.Http" };
        var directReferences = project.Descendants("Reference")
            .Select(element => ((string?)element.Attribute("Include"))?.Split(',')[0])
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        await Assert.That(directReferences.Any(prohibitedDirectReferences.Contains)).IsFalse().Because("Do not pin framework conflict roots through direct assembly references.");

        var modRoot = Path.GetDirectoryName(Path.GetDirectoryName(projectPath))!;
        var applicationConfigurationFiles = Directory.EnumerateFiles(
                modRoot,
                "*.config",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path))
            .ToArray();
        await Assert.That(applicationConfigurationFiles).IsEmpty().Because("Binding/application configuration files are outside the one-DLL ONI mod contract.");
    }

    private static bool IsGeneratedOutput(string path)
    {
        var segments = Path.GetFullPath(path).Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }

    private static string ProductionProjectPath(string repositoryRoot) =>
        Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "DeliveryTemperatureLimit.csproj");

    private static async Task<string> RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse().Because($"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value!;
    }

}
