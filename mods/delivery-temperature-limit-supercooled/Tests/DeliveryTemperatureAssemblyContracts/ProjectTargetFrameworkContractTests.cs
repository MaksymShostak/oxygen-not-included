using System.Text.Json;
using System.Xml.Linq;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class ProjectTargetFrameworkContractTests
{
    [Test]
    public async Task ProductionProject_WhenEvaluated_UsesCurrentOniRuntimeCompilerContract()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var projectPath = ProductionProjectPath(repositoryRoot);

        var properties = await EvaluatePropertiesAsync(
            repositoryRoot,
            projectPath,
            "TargetFramework",
            "LangVersion",
            "Nullable",
            "CopyLocalLockFileAssemblies",
            "TreatWarningsAsErrors");

        await Assert.That(properties["TargetFramework"]).IsEqualTo("netstandard2.1");
        await Assert.That(properties["LangVersion"]).IsEqualTo("8.0").Because("The game-loaded project must use the C# version derived from netstandard2.1.");
        await Assert.That(properties["Nullable"]).IsEqualTo("enable");
        await Assert.That(properties["CopyLocalLockFileAssemblies"]).IsEqualTo("true");
        await Assert.That(properties["TreatWarningsAsErrors"]).IsEqualTo("true");
    }

    [Test]
    public async Task TestProject_WhenEvaluated_UsesModernStaticToolingWithoutChangingProductionLanguage()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var projectPath = TestProjectPath(repositoryRoot);

        var properties = await EvaluatePropertiesAsync(
            repositoryRoot,
            projectPath,
            "TargetFramework",
            "LangVersion",
            "Nullable",
            "TreatWarningsAsErrors");

        await Assert.That(properties["TargetFramework"]).IsEqualTo("net10.0");
        await Assert.That(properties["LangVersion"]).IsEqualTo("14.0");
        await Assert.That(properties["Nullable"]).IsEqualTo("enable");
        await Assert.That(properties["TreatWarningsAsErrors"]).IsEqualTo("true");
    }

    [Test]
    public async Task ProjectFiles_WhenAuthored_DeclareApprovedCoordinatedActivationSettings()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var productionProperties = ReadAuthoredProperties(
            ProductionProjectPath(repositoryRoot));
        var testProperties = ReadAuthoredProperties(TestProjectPath(repositoryRoot));

        await Assert.That(productionProperties["TargetFramework"]).IsEqualTo("netstandard2.1");
        await Assert.That(productionProperties["CopyLocalLockFileAssemblies"]).IsEqualTo("true");
        await Assert.That(productionProperties["TreatWarningsAsErrors"]).IsEqualTo("true");
        await Assert.That(productionProperties.ContainsKey("LangVersion")).IsFalse().Because("Production must derive C# 8 from netstandard2.1; do not override LangVersion.");
        await Assert.That(productionProperties["Nullable"]).IsEqualTo("enable");

        await Assert.That(testProperties["TargetFramework"]).IsEqualTo("net10.0");
        await Assert.That(testProperties["Nullable"]).IsEqualTo("enable");
        await Assert.That(testProperties["TreatWarningsAsErrors"]).IsEqualTo("true");
        await Assert.That(testProperties.ContainsKey("LangVersion")).IsFalse().Because("Test-only C# 14 must remain the net10.0 SDK default.");
    }

    [Test]
    public async Task ProductionLockFile_WhenInspected_ContainsOnlyNetStandardGraphAndPinnedPackages()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var lockFilePath = Path.Combine(
            Path.GetDirectoryName(ProductionProjectPath(repositoryRoot))!,
            "packages.lock.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(lockFilePath));
        var centralVersions = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .ToDictionary(package => package.Attribute("Include")!.Value,
                package => package.Attribute("Version")!.Value, StringComparer.Ordinal);
        var dependencyGraphs = document.RootElement.GetProperty("dependencies");
        var graphNames = dependencyGraphs
            .EnumerateObject()
            .Select(graph => graph.Name)
            .ToArray();
        await Assert.That(graphNames).IsEquivalentTo(new[] { ".NETStandard,Version=v2.1" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var dependencies = dependencyGraphs.GetProperty(graphNames[0]);
        var dependencyNames = dependencies
            .EnumerateObject()
            .Select(dependency => dependency.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        await Assert.That(dependencyNames).IsEquivalentTo(new[]
            {
                "ILRepack.Lib.MSBuild.Task",
                "Lib.Harmony.Ref",
                "PLib",
                "System.Reflection.Emit"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await AssertLockedDependency(
            dependencies,
            "ILRepack.Lib.MSBuild.Task",
            "Direct",
            centralVersions["ILRepack.Lib.MSBuild.Task"]);
        await AssertLockedDependency(
            dependencies,
            "PLib",
            "Direct",
            centralVersions["PLib"]);
        await AssertLockedDependency(
            dependencies,
            "Lib.Harmony.Ref",
            "Transitive",
            "2.4.2");
        await AssertLockedDependency(
            dependencies,
            "System.Reflection.Emit",
            "Transitive",
            "4.7.0");
    }

    private static async Task AssertLockedDependency(
        JsonElement dependencies,
        string packageName,
        string expectedDependencyType,
        string expectedVersion)
    {
        var dependency = dependencies.GetProperty(packageName);
        await Assert.That(dependency.GetProperty("type").GetString()).IsEqualTo(expectedDependencyType).Because($"Locked dependency type changed for {packageName}.");
        await Assert.That(dependency.GetProperty("resolved").GetString()).IsEqualTo(expectedVersion).Because($"Locked dependency version changed for {packageName}.");
    }

    private static async Task<IReadOnlyDictionary<string, string>> EvaluatePropertiesAsync(
        string repositoryRoot,
        string projectPath,
        params string[] propertyNames)
    {
        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "--nologo"
        };
        arguments.AddRange(propertyNames.Select(name => $"-getProperty:{name}"));

        var result = await DotnetCommandRunner.RunAsync(repositoryRoot, arguments);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.FormatEvidence());

        using var document = JsonDocument.Parse(result.StandardOutput);
        var properties = document.RootElement.GetProperty("Properties");
        return propertyNames.ToDictionary(
            name => name,
            name => properties.GetProperty(name).GetString() ?? string.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadAuthoredProperties(
        string projectPath)
    {
        var document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
        return document.Root!
            .Elements("PropertyGroup")
            .Elements()
            .GroupBy(element => element.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value.Trim(),
                StringComparer.Ordinal);
    }

    private static string ProductionProjectPath(string repositoryRoot) =>
        Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "DeliveryTemperatureLimit.csproj");

    private static string TestProjectPath(string repositoryRoot) =>
        Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Tests",
            "DeliveryTemperatureLimit.Tests.csproj");

    private static async Task<string> RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse().Because($"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value!;
    }
}
