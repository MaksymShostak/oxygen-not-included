using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class LinkedProductionSourceBoundaryContractTests
{
    private static readonly string[] ApprovedCompileIncludes =
    [
        @"..\Source\Buildings.cs",
        @"..\Source\TemperatureConstraints\**\*.cs",
        @"..\Source\WorldParentTopology\**\*.cs",
        @"..\Source\WorldResourceTemperatureAmounts\**\*.cs",
        @"..\Source\FetchTemperatureEligibility\**\*.cs",
        @"..\Source\DeliveryTemperatureGameSessionLifecycle\**\*.cs",
        @"..\Source\FastTrackCompatibility\FeatureContractVerification\**\*.cs",
        @"..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationKind.cs",
        @"..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationResult.cs",
        @"..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationSession.cs",
        @"..\Source\FastTrackCompatibility\PickupGroupingAdapters\FastTrackPickupGroupingKeyAllocator.cs",
        @"..\Source\HarmonyTranspilerInfrastructure\HarmonyPatchContract*.cs",
        @"..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchGroup.cs",
        @"..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchPlan.cs"
    ];

    private static readonly HashSet<string> ConditionallyLinkedFutureSourceIncludes =
        new(StringComparer.Ordinal)
        {
            @"..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationKind.cs",
            @"..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationResult.cs",
            @"..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationSession.cs",
            @"..\Source\FastTrackCompatibility\PickupGroupingAdapters\FastTrackPickupGroupingKeyAllocator.cs",
            @"..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchGroup.cs",
            @"..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchPlan.cs"
        };

    [TestMethod]
    public void TestProject_WhenCompileLinksAreInspected_UsesExactApprovedProductionBoundary()
    {
        var testProjectPath = TestProjectPath();
        var document = XDocument.Load(testProjectPath, LoadOptions.SetLineInfo);
        var compileItems = document.Root!
            .Elements("ItemGroup")
            .Elements("Compile")
            .Select(element => new
            {
                Include = (string?)element.Attribute("Include"),
                Link = (string?)element.Attribute("Link"),
                Condition = (string?)element.Attribute("Condition")
            })
            .ToArray();

        CollectionAssert.AreEquivalent(
            ApprovedCompileIncludes,
            compileItems.Select(item => item.Include).ToArray());
        Assert.IsTrue(
            compileItems.All(item =>
                !string.IsNullOrWhiteSpace(item.Link)
                && item.Link!.StartsWith(@"Production\", StringComparison.Ordinal)),
            "Every linked production source must have a semantic Production\\ link path.");
        Assert.IsFalse(
            compileItems.Any(item => string.Equals(
                item.Include,
                @"..\Source\FastTrackCompatibility\**\*.cs",
                StringComparison.Ordinal)),
            "Runtime FastTrack adapters must never enter the pure linked-source boundary.");

        foreach (var compileItem in compileItems)
        {
            Assert.IsNotNull(compileItem.Include);
            var shouldBeConditional = ConditionallyLinkedFutureSourceIncludes.Contains(
                compileItem.Include);
            var expectedCondition = shouldBeConditional
                ? $"Exists('{compileItem.Include}')"
                : null;
            Assert.AreEqual(
                expectedCondition,
                compileItem.Condition,
                shouldBeConditional
                    ? $"Future exact link {compileItem.Include} must use a path-identical existence guard."
                    : $"Existing or wildcard link {compileItem.Include} must remain unconditional.");
        }
    }

    [TestMethod]
    public void LinkedProductionSources_WhenInspected_AreProductionCompileInputsWithoutTestForks()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        var sourceRoot = Path.GetFullPath(Path.Combine(modRoot, "Source"));
        var productionProject = XDocument.Load(
            Path.Combine(sourceRoot, "DeliveryTemperatureLimit.csproj"));

        Assert.IsFalse(
            productionProject.Descendants("Compile")
                .Any(element => element.Attribute("Remove") is not null),
            "Production compile exclusions could let a test-linked copy evade the C# 8 build.");
        Assert.AreNotEqual(
            "false",
            productionProject.Descendants("EnableDefaultCompileItems")
                .LastOrDefault()?.Value.Trim(),
            "The production project must retain SDK default compile discovery.");
        Assert.IsFalse(
            Directory.Exists(Path.Combine(modRoot, "Tests", "Production")),
            "Production algorithms must be linked from Source, never copied under Tests.");

        foreach (var include in ApprovedCompileIncludes)
        {
            foreach (var sourcePath in ExpandExistingCompileInclude(modRoot, include))
            {
                var fullPath = Path.GetFullPath(sourcePath);
                Assert.IsTrue(
                    IsDescendantOf(sourceRoot, fullPath),
                    $"Linked file escapes the production source tree: {fullPath}");
                Assert.IsFalse(
                    IsDescendantOf(Path.Combine(sourceRoot, "obj"), fullPath)
                    || IsDescendantOf(Path.Combine(sourceRoot, "bin"), fullPath),
                    $"Generated output must not be linked as production source: {fullPath}");

                if (!string.Equals(
                    fullPath,
                    Path.Combine(sourceRoot, "Buildings.cs"),
                    StringComparison.OrdinalIgnoreCase))
                {
                    AssertPureLinkedSource(fullPath);
                }
            }
        }
    }

    [TestMethod]
    public void OniGameTypeStubs_WhenIdentityAndMembersAreInspected_MatchPureSourceDependenciesExactly()
    {
        var tagType = typeof(global::Tag);
        Assert.AreEqual("Tag", tagType.FullName);
        Assert.IsTrue(tagType.IsValueType);
        Assert.IsTrue(
            tagType.CustomAttributes.Any(attribute =>
                attribute.AttributeType == typeof(IsReadOnlyAttribute)),
            "The global Tag test double must be readonly.");
        Assert.IsEmpty(tagType.GetFields(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly));
        Assert.IsEmpty(tagType.GetProperties(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly));
        Assert.IsEmpty(tagType.GetMethods(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly));

        var temperatureLimitType = typeof(global::DeliveryTemperatureLimit.TemperatureLimit);
        Assert.AreEqual(
            "DeliveryTemperatureLimit.TemperatureLimit",
            temperatureLimitType.FullName);
        Assert.IsNull(
            temperatureLimitType.Assembly.GetType(
                "TemperatureLimit",
                throwOnError: false,
                ignoreCase: false),
            "A global TemperatureLimit alias would conceal a production namespace dependency.");
        Assert.IsEmpty(temperatureLimitType.GetFields(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly));
        Assert.IsEmpty(temperatureLimitType.GetProperties(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly));
        Assert.IsEmpty(temperatureLimitType.GetMethods(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly));

        var constructors = temperatureLimitType.GetConstructors(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly);
        Assert.HasCount(1, constructors);
        Assert.IsTrue(constructors[0].IsPublic);
        Assert.IsEmpty(constructors[0].GetParameters());
    }

    [TestMethod]
    public void OniGameTypeStubs_WhenLocated_HaveSemanticFileIdentityWithoutLegacyAliasFile()
    {
        var testRoot = Path.GetDirectoryName(TestProjectPath())!;

        Assert.IsTrue(File.Exists(Path.Combine(
            testRoot,
            "TestDoubles",
            "OniGameTypeStubs.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(testRoot, "GameStubs.cs")));
    }

    private static IEnumerable<string> ExpandExistingCompileInclude(
        string modRoot,
        string include)
    {
        var absolutePattern = Path.GetFullPath(Path.Combine(
            modRoot,
            "Tests",
            include));
        if (absolutePattern.EndsWith(
            $"{Path.DirectorySeparatorChar}**{Path.DirectorySeparatorChar}*.cs",
            StringComparison.Ordinal))
        {
            var directory = absolutePattern[..^($"{Path.DirectorySeparatorChar}**{Path.DirectorySeparatorChar}*.cs").Length];
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(
                    directory,
                    "*.cs",
                    SearchOption.AllDirectories)
                : [];
        }

        if (absolutePattern.Contains('*'))
        {
            var directory = Path.GetDirectoryName(absolutePattern)!;
            var searchPattern = Path.GetFileName(absolutePattern);
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(
                    directory,
                    searchPattern,
                    SearchOption.TopDirectoryOnly)
                : [];
        }

        return File.Exists(absolutePattern) ? [absolutePattern] : [];
    }

    private static void AssertPureLinkedSource(string sourcePath)
    {
        var source = File.ReadAllText(sourcePath);
        var forbiddenFragments = new[]
        {
            "using HarmonyLib",
            "using UnityEngine",
            "using PeterHan",
            "using KMod",
            "HarmonyLib.",
            "UnityEngine.",
            "PeterHan.",
            "KMod.",
            "#if",
            "#elif",
            "#else"
        };
        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.IsFalse(
                source.Contains(forbiddenFragment, StringComparison.Ordinal),
                $"Linked pure source {sourcePath} contains '{forbiddenFragment}' and crosses " +
                "the game/runtime or conditional-test boundary.");
        }
    }

    private static bool IsDescendantOf(string parentPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(parentPath),
            Path.GetFullPath(candidatePath));
        return !Path.IsPathRooted(relativePath)
            && relativePath != ".."
            && !relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
    }

    private static string TestProjectPath() =>
        Path.Combine(
            RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT"),
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
