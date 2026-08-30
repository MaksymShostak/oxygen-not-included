using System.Reflection;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class DeliveryTemperaturePackageBoundaryContractTests
{
    private static readonly string[] ExactRuntimePackageRelativePaths =
    [
        "mod.yaml",
        "mod_info.yaml",
        "DeliveryTemperatureLimit.dll"
    ];

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ProvenanceBoundPackageCases
    {
        get
        {
            foreach (DeliveryTemperatureArtifactContractCase contractCase in
                     DeliveryTemperatureArtifactContractCaseProvider
                         .ResolveForCurrentEnvironment())
            {
                yield return
                [
                    contractCase.ContractRowName,
                    contractCase.Kind.ToString(),
                    contractCase.AssemblyPath,
                    contractCase.EvidencePath,
                    contractCase.PackageDirectoryPath ?? string.Empty,
                    contractCase.PackageRelativePaths.ToArray()
                ];
            }
        }
    }

    public static string FormatProvenanceBoundPackageCaseName(
        MethodInfo methodInfo,
        object[] data) =>
        $"{methodInfo.Name} ({data[0]})";

    [TestMethod]
    [DynamicData(
        nameof(ProvenanceBoundPackageCases),
        DynamicDataDisplayName = nameof(FormatProvenanceBoundPackageCaseName))]
    public void ProvenanceBoundArtifact_WhenPackageBoundaryIsInspected_ContainsOnlyDeclaredRuntimeFiles(
        string contractRow,
        string artifactKind,
        string assemblyPath,
        string evidencePath,
        string packageDirectoryPath,
        string[] packageRelativePaths)
    {
        TestContext.WriteLine($"Package contract row: {contractRow}");
        Assert.AreEqual(
            artifactKind,
            contractRow,
            "The display name and semantic artifact kind must remain identical.");
        CollectionAssert.AreEqual(
            ExactRuntimePackageRelativePaths,
            packageRelativePaths,
            $"{contractRow} is bound to an unexpected package inventory.");
        Assert.AreEqual(
            "DeliveryTemperatureLimit.dll",
            Path.GetFileName(assemblyPath),
            $"{contractRow} is not bound to the declared merged assembly.");
        Assert.IsTrue(
            File.Exists(assemblyPath),
            $"{contractRow} assembly does not exist at {assemblyPath}.");
        AssertPackageMappingsAreExact(
            OniModPipelineProfileContractReader.Read(ProfilePath()));

        if (string.Equals(
                artifactKind,
                nameof(DeliveryTemperatureArtifactContractKind.ExactPipelineBuild),
                StringComparison.Ordinal))
        {
            Assert.AreEqual(
                "build-result.json",
                Path.GetFileName(evidencePath),
                "ExactPipelineBuild must be bound through build-result.json.");
            Assert.AreEqual(
                string.Empty,
                packageDirectoryPath,
                "A build result describes declared packaging inputs; it must " +
                "not masquerade as a prepared package directory.");
        }
        else if (string.Equals(
                     artifactKind,
                     nameof(DeliveryTemperatureArtifactContractKind.ExactReleaseCandidate),
                     StringComparison.Ordinal))
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(packageDirectoryPath));
            string[] actualPackagePaths = Directory
                .EnumerateFiles(
                    packageDirectoryPath,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(
                        packageDirectoryPath,
                        path)
                    .Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(path => Array.IndexOf(
                    ExactRuntimePackageRelativePaths,
                    path))
                .ToArray();
            CollectionAssert.AreEqual(
                ExactRuntimePackageRelativePaths,
                actualPackagePaths,
                "The immutable release-candidate package contains a missing " +
                "or undeclared runtime file.");
        }
        else
        {
            Assert.AreEqual(
                nameof(DeliveryTemperatureArtifactContractKind.PublishedBaseline),
                artifactKind);
            Assert.AreEqual("tracked-published-baseline", evidencePath);
            Assert.AreEqual(string.Empty, packageDirectoryPath);
        }

        AssertRuntimePackageFileNamesAreSafe(packageRelativePaths);
    }

    [TestMethod]
    public void PipelineProfile_WhenPackageMappingsAreInspected_ContainsOnlyReleaseContractFiles()
    {
        var profile = OniModPipelineProfileContractReader.Read(ProfilePath());
        AssertPackageMappingsAreExact(profile);
    }

    [TestMethod]
    public void PipelineProfile_WhenPackageMappingsAreInspected_CannotShipFrameworkOrConfigurationFiles()
    {
        var profile = OniModPipelineProfileContractReader.Read(ProfilePath());
        var prohibitedFrameworkFiles = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "System.IO.Compression.dll",
            "System.Net.Http.dll"
        };

        foreach (var mapping in profile.PackageFiles)
        {
            var sourceName = Path.GetFileName(mapping.Source);
            var destinationName = Path.GetFileName(mapping.Destination);
            Assert.IsFalse(
                prohibitedFrameworkFiles.Contains(sourceName)
                || prohibitedFrameworkFiles.Contains(destinationName),
                $"Framework assembly mapping is prohibited: {mapping}");
            Assert.AreNotEqual(
                ".config",
                Path.GetExtension(mapping.Source),
                ignoreCase: true,
                $"Application configuration mapping is prohibited: {mapping}");
            Assert.AreNotEqual(
                ".config",
                Path.GetExtension(mapping.Destination),
                ignoreCase: true,
                $"Application configuration mapping is prohibited: {mapping}");
            Assert.IsFalse(
                mapping.Source.Contains('*') || mapping.Source.Contains('?'),
                $"Package sources must be exact files, not patterns: {mapping.Source}");
        }
    }

    private static void AssertPackageMappingsAreExact(
        OniModPipelineProfileContract profile)
    {
        var expectedMappings = new[]
        {
            new OniModPipelinePackageFileContract("mod.yaml", "mod.yaml"),
            new OniModPipelinePackageFileContract("mod_info.yaml", "mod_info.yaml"),
            new OniModPipelinePackageFileContract(
                "{build-output}/DeliveryTemperatureLimit.dll",
                "DeliveryTemperatureLimit.dll")
        };

        CollectionAssert.AreEqual(
            expectedMappings,
            profile.PackageFiles.ToArray());
    }

    private static void AssertRuntimePackageFileNamesAreSafe(
        IEnumerable<string> packageRelativePaths)
    {
        string[] prohibitedExactFileNames =
        [
            "FastTrack.dll",
            "PLib.dll",
            "System.IO.Compression.dll",
            "System.Net.Http.dll",
            "DeliveryTemperatureLimit.Tests.dll"
        ];
        string[] prohibitedExtensions =
        [
            ".config",
            ".pdb"
        ];

        foreach (string relativePath in packageRelativePaths)
        {
            string fileName = Path.GetFileName(relativePath);
            CollectionAssert.DoesNotContain(
                prohibitedExactFileNames,
                fileName,
                $"Prohibited runtime package file {relativePath} was declared.");
            CollectionAssert.DoesNotContain(
                prohibitedExtensions,
                Path.GetExtension(relativePath),
                $"Prohibited package sidecar {relativePath} was declared.");
            Assert.IsFalse(
                relativePath.Contains(
                    "Fixture",
                    StringComparison.OrdinalIgnoreCase),
                $"Test fixture path {relativePath} entered the runtime package.");
        }
    }

    private static string ProfilePath() =>
        Path.Combine(
            RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT"),
            "mods",
            "delivery-temperature-limit-supercooled",
            "oni-mod-pipeline.toml");

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }
}
