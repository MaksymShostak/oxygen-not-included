using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class DeliveryTemperaturePackageBoundaryContractTests
{
    [TestMethod]
    public void PipelineProfile_WhenPackageMappingsAreInspected_ContainsOnlyReleaseContractFiles()
    {
        var profile = OniModPipelineProfileContractReader.Read(ProfilePath());
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
