using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed partial class OniStorableTemperatureBoundsContractTests
{
    [TestMethod]
    public void BoundSource_WhenInspected_DeclaresOnlyReviewedCurrentOniConstants()
    {
        var sourcePath = BoundSourcePath();
        Assert.IsTrue(
            File.Exists(sourcePath),
            $"The reviewed ONI storable-temperature bound source is missing: {sourcePath}");
        var source = File.ReadAllText(sourcePath);

        Assert.IsTrue(
            source.StartsWith("#nullable enable", StringComparison.Ordinal),
            "The linked pure bound source must establish its nullable context explicitly.");
        StringAssert.Matches(source, InternalStaticClassPattern());
        StringAssert.Matches(source, MinimumConstantPattern());
        StringAssert.Matches(source, MaximumConstantPattern());
        StringAssert.Contains(source, "changelist 744825");
        StringAssert.Contains(source, "Sim.MaxTemperature");
        StringAssert.Contains(source, "inclusive");
        StringAssert.Contains(source, nameof(OniStorableTemperatureBoundsContractTests));
        StringAssert.Contains(source, "preserved configurable floor");
        Assert.IsFalse(source.Contains("System.Reflection", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Assembly-CSharp", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BoundSource_WhenComparedWithInstalledOni_MatchesMaximumTemperatureConstant()
    {
        var sourcePath = BoundSourcePath();
        Assert.IsTrue(
            File.Exists(sourcePath),
            $"The reviewed ONI storable-temperature bound source is missing: {sourcePath}");
        var source = File.ReadAllText(sourcePath);
        var sourceMaximum = int.Parse(
            MaximumConstantPattern().Match(source).Groups["value"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var assemblyPath = DeliveryTemperatureAssemblyMetadataReader
            .ResolveManagedAssemblyPath(
                RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY"),
                "Assembly-CSharp.dll");
        var installedMaximum = DeliveryTemperatureAssemblyMetadataReader
            .ReadFieldConstant(assemblyPath, "Sim", "MaxTemperature");

        Assert.AreEqual(
            Convert.ToSingle(
                installedMaximum,
                System.Globalization.CultureInfo.InvariantCulture),
            sourceMaximum,
            $"The compile-time bound in {sourcePath} must be reviewed whenever ONI's " +
            "Sim.MaxTemperature changes.");
    }

    private static string BoundSourcePath() =>
        Path.Combine(
            RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT"),
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "TemperatureConstraints",
            "OniStorableTemperatureBounds.cs");

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
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
