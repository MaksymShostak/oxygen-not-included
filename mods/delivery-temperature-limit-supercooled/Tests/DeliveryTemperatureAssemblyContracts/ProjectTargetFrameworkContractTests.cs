using System.Text.Json;
using System.Xml.Linq;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class ProjectTargetFrameworkContractTests
{
    [TestMethod]
    public async Task ProductionProject_WhenEvaluated_UsesCurrentOniRuntimeCompilerContract()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var projectPath = ProductionProjectPath(repositoryRoot);

        var properties = await EvaluatePropertiesAsync(
            repositoryRoot,
            projectPath,
            "TargetFramework",
            "LangVersion",
            "CopyLocalLockFileAssemblies",
            "TreatWarningsAsErrors");

        Assert.AreEqual("netstandard2.1", properties["TargetFramework"]);
        Assert.AreEqual(
            "8.0",
            properties["LangVersion"],
            "The game-loaded project must use the C# version derived from netstandard2.1.");
        Assert.AreEqual("true", properties["CopyLocalLockFileAssemblies"]);
        Assert.AreEqual("true", properties["TreatWarningsAsErrors"]);
    }

    [TestMethod]
    public async Task TestProject_WhenEvaluated_UsesModernStaticToolingWithoutChangingProductionLanguage()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var projectPath = TestProjectPath(repositoryRoot);

        var properties = await EvaluatePropertiesAsync(
            repositoryRoot,
            projectPath,
            "TargetFramework",
            "LangVersion",
            "Nullable",
            "TreatWarningsAsErrors");

        Assert.AreEqual("net10.0", properties["TargetFramework"]);
        Assert.AreEqual("14.0", properties["LangVersion"]);
        Assert.AreEqual("annotations", properties["Nullable"]);
        Assert.AreEqual("true", properties["TreatWarningsAsErrors"]);
    }

    [TestMethod]
    public void ProjectFiles_WhenAuthored_DeclareOnlyTheApprovedFirstStageSettings()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var productionProperties = ReadAuthoredProperties(
            ProductionProjectPath(repositoryRoot));
        var testProperties = ReadAuthoredProperties(TestProjectPath(repositoryRoot));

        Assert.AreEqual("netstandard2.1", productionProperties["TargetFramework"]);
        Assert.AreEqual("true", productionProperties["CopyLocalLockFileAssemblies"]);
        Assert.AreEqual("true", productionProperties["TreatWarningsAsErrors"]);
        Assert.IsFalse(
            productionProperties.ContainsKey("LangVersion"),
            "Production must derive C# 8 from netstandard2.1; do not override LangVersion.");
        Assert.IsFalse(
            productionProperties.ContainsKey("Nullable"),
            "Nullable is intentionally deferred until coordinated runtime activation.");

        Assert.AreEqual("net10.0", testProperties["TargetFramework"]);
        Assert.AreEqual("annotations", testProperties["Nullable"]);
        Assert.AreEqual("true", testProperties["TreatWarningsAsErrors"]);
        Assert.IsFalse(
            testProperties.ContainsKey("LangVersion"),
            "Test-only C# 14 must remain the net10.0 SDK default.");
    }

    [TestMethod]
    public void ProductionLockFile_WhenInspected_ContainsOnlyNetStandardGraphAndPinnedPackages()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var lockFilePath = Path.Combine(
            Path.GetDirectoryName(ProductionProjectPath(repositoryRoot))!,
            "packages.lock.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(lockFilePath));
        var dependencyGraphs = document.RootElement.GetProperty("dependencies");
        var graphNames = dependencyGraphs
            .EnumerateObject()
            .Select(graph => graph.Name)
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { ".NETStandard,Version=v2.1" },
            graphNames);

        var dependencies = dependencyGraphs.GetProperty(graphNames[0]);
        var dependencyNames = dependencies
            .EnumerateObject()
            .Select(dependency => dependency.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "ILRepack.Lib.MSBuild.Task",
                "Lib.Harmony.Ref",
                "PLib",
                "System.Reflection.Emit"
            },
            dependencyNames);
        AssertLockedDependency(
            dependencies,
            "ILRepack.Lib.MSBuild.Task",
            "Direct",
            "2.0.34");
        AssertLockedDependency(
            dependencies,
            "PLib",
            "Direct",
            "4.24.0");
        AssertLockedDependency(
            dependencies,
            "Lib.Harmony.Ref",
            "Transitive",
            "2.4.2");
        AssertLockedDependency(
            dependencies,
            "System.Reflection.Emit",
            "Transitive",
            "4.7.0");
    }

    private static void AssertLockedDependency(
        JsonElement dependencies,
        string packageName,
        string expectedDependencyType,
        string expectedVersion)
    {
        var dependency = dependencies.GetProperty(packageName);
        Assert.AreEqual(
            expectedDependencyType,
            dependency.GetProperty("type").GetString(),
            $"Locked dependency type changed for {packageName}.");
        Assert.AreEqual(
            expectedVersion,
            dependency.GetProperty("resolved").GetString(),
            $"Locked dependency version changed for {packageName}.");
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
        Assert.AreEqual(0, result.ExitCode, result.FormatEvidence());

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

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }
}
