using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed partial class OniStorableTemperatureBoundsContractTests
{
    [Test]
    public async Task BoundSource_WhenInspected_DeclaresOnlyReviewedCurrentOniConstants()
    {
        var sourcePath = await BoundSourcePath();
        await Assert.That(File.Exists(sourcePath)).IsTrue().Because($"The reviewed ONI storable-temperature bound source is missing: {sourcePath}");
        var source = File.ReadAllText(sourcePath);

        await Assert.That(source.StartsWith("#nullable enable", StringComparison.Ordinal)).IsTrue().Because("The linked pure bound source must establish its nullable context explicitly.");
        await Assert.That(source).Matches(InternalStaticClassPattern());
        await Assert.That(source).Matches(MinimumConstantPattern());
        await Assert.That(source).Matches(MaximumConstantPattern());
        await Assert.That(source).Contains("changelist 744825");
        await Assert.That(source).Contains("Sim.MaxTemperature");
        await Assert.That(source).Contains("inclusive");
        await Assert.That(source).Contains(nameof(OniStorableTemperatureBoundsContractTests));
        await Assert.That(source).Contains("preserved configurable floor");
        await Assert.That(source.Contains("System.Reflection", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("Assembly-CSharp", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task BoundSource_WhenComparedWithInstalledOni_MatchesMaximumTemperatureConstant()
    {
        var sourcePath = await BoundSourcePath();
        await Assert.That(File.Exists(sourcePath)).IsTrue().Because($"The reviewed ONI storable-temperature bound source is missing: {sourcePath}");
        var source = File.ReadAllText(sourcePath);
        var sourceMaximum = int.Parse(
            MaximumConstantPattern().Match(source).Groups["value"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                (await RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY")),
                "Assembly-CSharp.dll");
        var installedMaximum = DeliveryTemperatureAssemblyMetadataReader
            .ReadFieldConstant(assemblyPath, "Sim", "MaxTemperature");

        await Assert.That(sourceMaximum).IsEqualTo(Convert.ToSingle(
                installedMaximum,
                System.Globalization.CultureInfo.InvariantCulture)).Because($"The compile-time bound in {sourcePath} must be reviewed whenever ONI's " +
            "Sim.MaxTemperature changes.");
    }

    private static async Task<string> BoundSourcePath() =>
        Path.Combine(
            (await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "TemperatureConstraints",
            "OniStorableTemperatureBounds.cs");

    private static async Task<string> RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse().Because($"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value!;
    }

    [GeneratedRegex(
        @"internal\s+static\s+class\s+OniStorableTemperatureBounds\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex InternalStaticClassPattern();

    [GeneratedRegex(
        @"internal\s+const\s+int\s+MinimumTemperatureKelvin\s*=\s*0\s*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex MinimumConstantPattern();

    [GeneratedRegex(
        @"internal\s+const\s+int\s+MaximumTemperatureKelvin\s*=\s*(?<value>10000)\s*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex MaximumConstantPattern();
}
