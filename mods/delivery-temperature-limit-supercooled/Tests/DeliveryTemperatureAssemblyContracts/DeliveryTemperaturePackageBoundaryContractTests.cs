using System.Reflection;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class DeliveryTemperaturePackageBoundaryContractTests
{
    private static readonly string[] ExactRuntimePackageRelativePaths =
    [
        "mod.yaml",
        "mod_info.yaml",
        "DeliveryTemperatureLimit.dll"
    ];

    public static IEnumerable<(string ContractRow, string ArtifactKind,
        string AssemblyPath, string EvidencePath, string PackageDirectoryPath,
        string[] PackageRelativePaths)> ProvenanceBoundPackageCases
    {
        get
        {
            foreach (DeliveryTemperatureArtifactContractCase contractCase in
                     DeliveryTemperatureArtifactContractCaseProvider
                         .ResolveForCurrentEnvironment())
            {
                yield return
                (
                    contractCase.ContractRowName,
                    contractCase.Kind.ToString(),
                    contractCase.AssemblyPath,
                    contractCase.EvidencePath,
                    contractCase.PackageDirectoryPath ?? string.Empty,
                    contractCase.PackageRelativePaths.ToArray()
                );
            }
        }
    }

    [Test]
    [MethodDataSource(nameof(ProvenanceBoundPackageCases))]
    [DisplayName($"{nameof(ProvenanceBoundArtifact_WhenPackageBoundaryIsInspected_ContainsOnlyDeclaredRuntimeFiles)} ($contractRow)")]
    public async Task ProvenanceBoundArtifact_WhenPackageBoundaryIsInspected_ContainsOnlyDeclaredRuntimeFiles(
        string contractRow,
        string artifactKind,
        string assemblyPath,
        string evidencePath,
        string packageDirectoryPath,
        string[] packageRelativePaths)
    {
        Console.WriteLine($"Package contract row: {contractRow}");
        await Assert.That(contractRow).IsEqualTo(artifactKind).Because("The display name and semantic artifact kind must remain identical.");
        await Assert.That(packageRelativePaths).IsEquivalentTo(ExactRuntimePackageRelativePaths, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because($"{contractRow} is bound to an unexpected package inventory.");
        await Assert.That(Path.GetFileName(assemblyPath)).IsEqualTo("DeliveryTemperatureLimit.dll").Because($"{contractRow} is not bound to the declared merged assembly.");
        await Assert.That(File.Exists(assemblyPath)).IsTrue().Because($"{contractRow} assembly does not exist at {assemblyPath}.");
        await AssertPackageMappingsAreExact(
            OniModPipelineProfileContractReader.Read((await ProfilePath())));

        if (string.Equals(
                artifactKind,
                nameof(DeliveryTemperatureArtifactContractKind.ExactPipelineBuild),
                StringComparison.Ordinal))
        {
            await Assert.That(Path.GetFileName(evidencePath)).IsEqualTo("build-result.json").Because("ExactPipelineBuild must be bound through build-result.json.");
            await Assert.That(packageDirectoryPath).IsEqualTo(string.Empty).Because("A build result describes declared packaging inputs; it must " +
                "not masquerade as a prepared package directory.");
        }
        else if (string.Equals(
                     artifactKind,
                     nameof(DeliveryTemperatureArtifactContractKind.ExactReleaseCandidate),
                     StringComparison.Ordinal))
        {
            await Assert.That(string.IsNullOrWhiteSpace(packageDirectoryPath)).IsFalse();
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
            await Assert.That(actualPackagePaths).IsEquivalentTo(ExactRuntimePackageRelativePaths, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("The immutable release-candidate package contains a missing " +
                "or undeclared runtime file.");
        }
        else
        {
            await Assert.That(artifactKind).IsEqualTo(nameof(DeliveryTemperatureArtifactContractKind.PublishedBaseline));
            await Assert.That(evidencePath).IsEqualTo("tracked-published-baseline");
            await Assert.That(packageDirectoryPath).IsEqualTo(string.Empty);
        }

        await AssertRuntimePackageFileNamesAreSafe(packageRelativePaths);
    }

    [Test]
    public async Task PipelineProfile_WhenPackageMappingsAreInspected_ContainsOnlyReleaseContractFiles()
    {
        var profile = OniModPipelineProfileContractReader.Read((await ProfilePath()));
        await AssertPackageMappingsAreExact(profile);
    }

    [Test]
    public async Task PipelineProfile_WhenPackageMappingsAreInspected_CannotShipFrameworkOrConfigurationFiles()
    {
        var profile = OniModPipelineProfileContractReader.Read((await ProfilePath()));
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
            await Assert.That(prohibitedFrameworkFiles.Contains(sourceName)
                || prohibitedFrameworkFiles.Contains(destinationName)).IsFalse().Because($"Framework assembly mapping is prohibited: {mapping}");
            await Assert.That(string.Equals(Path.GetExtension(mapping.Source), ".config", true ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).IsFalse().Because($"Application configuration mapping is prohibited: {mapping}");
            await Assert.That(string.Equals(Path.GetExtension(mapping.Destination), ".config", true ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).IsFalse().Because($"Application configuration mapping is prohibited: {mapping}");
            await Assert.That(mapping.Source.Contains('*') || mapping.Source.Contains('?')).IsFalse().Because($"Package sources must be exact files, not patterns: {mapping.Source}");
        }
    }

    private static async Task AssertPackageMappingsAreExact(
        OniModPipelineProfileContract profile)
    {
        var expectedMappings = new[]
        {
            new OniModPipelinePackageFileContract("mod.yaml", "mod.yaml"),
            new OniModPipelinePackageFileContract("mod_info.yaml", "mod_info.yaml"),
            new OniModPipelinePackageFileContract(
                "{build-output}/DeliveryTemperatureLimit.dll",
                "DeliveryTemperatureLimit.dll"),
            new OniModPipelinePackageFileContract("translations", "translations")
        };

        await Assert.That(profile.PackageFiles.ToArray()).IsEquivalentTo(expectedMappings, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static async Task AssertRuntimePackageFileNamesAreSafe(
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
            await Assert.That(prohibitedExactFileNames).DoesNotContain(fileName).Because($"Prohibited runtime package file {relativePath} was declared.");
            await Assert.That(prohibitedExtensions).DoesNotContain(Path.GetExtension(relativePath)).Because($"Prohibited package sidecar {relativePath} was declared.");
            await Assert.That(relativePath.Contains(
                    "Fixture",
                    StringComparison.OrdinalIgnoreCase)).IsFalse().Because($"Test fixture path {relativePath} entered the runtime package.");
        }
    }

    private static async Task<string> ProfilePath() =>
        Path.Combine(
            (await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")),
            "mods",
            "delivery-temperature-limit-supercooled",
            "oni-mod-pipeline.toml");

    private static async Task<string> RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse().Because($"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value!;
    }
}
