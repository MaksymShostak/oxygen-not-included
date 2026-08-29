using System.Text.RegularExpressions;
using System.Xml.Linq;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed partial class KnownOniReferenceConflictContractTests
{
    private static readonly IReadOnlyDictionary<string, (string FrameworkVersion, string OniVersion)>
        ExpectedConflicts = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["System.IO.Compression"] = ("4.1.3.0", "4.2.0.0"),
            ["System.Net.Http"] = ("4.1.2.0", "4.2.0.0")
        };

    [TestMethod]
    public async Task ProductionBuild_WhenCurrentOniAssembliesAreUsed_ReportsOnlyReviewedConflictRoots()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable(
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
        Assert.AreEqual(0, restore.ExitCode, restore.FormatEvidence());

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
        Assert.AreEqual(
            0,
            productionBuild.ExitCode,
            productionBuild.FormatEvidence());

        var combinedOutput = productionBuild.StandardOutput +
            Environment.NewLine +
            productionBuild.StandardError;
        var observedConflictRoots = ConflictRootPattern()
            .Matches(combinedOutput)
            .Select(match => match.Groups["root"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            ExpectedConflicts.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            observedConflictRoots,
            $"The visible MSB3277 inventory changed.{Environment.NewLine}{combinedOutput}");

        foreach (var (assemblyName, versions) in ExpectedConflicts)
        {
            StringAssert.Contains(
                combinedOutput,
                $"{assemblyName}, Version={versions.FrameworkVersion}",
                $"Framework/reference root changed for {assemblyName}.");
            StringAssert.Contains(
                combinedOutput,
                $"{assemblyName}, Version={versions.OniVersion}",
                $"ONI root changed for {assemblyName}.");
        }
    }

    [TestMethod]
    public void ProductionProject_WhenInspected_ContainsNoConflictSuppressionOrReferenceWorkaround()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
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
            Assert.IsFalse(
                project.Descendants(property).Any(),
                $"{property} must not conceal the reviewed ONI reference conflicts.");
        }

        var prohibitedDirectReferences = ExpectedConflicts.Keys.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var directReferences = project.Descendants("Reference")
            .Select(element => ((string?)element.Attribute("Include"))?.Split(',')[0])
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        Assert.IsFalse(
            directReferences.Any(prohibitedDirectReferences.Contains),
            "Do not pin framework conflict roots through direct assembly references.");

        var modRoot = Path.GetDirectoryName(Path.GetDirectoryName(projectPath))!;
        var applicationConfigurationFiles = Directory.EnumerateFiles(
                modRoot,
                "*.config",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path))
            .ToArray();
        Assert.IsEmpty(
            applicationConfigurationFiles,
            "Binding/application configuration files are outside the one-DLL ONI mod contract.");
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

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }

    [GeneratedRegex(
        "warning MSB3277: Found conflicts between different versions of \\\"(?<root>[^\\\"]+)\\\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConflictRootPattern();
}
