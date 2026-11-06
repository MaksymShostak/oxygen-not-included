using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class LinkedProductionSourceBoundaryContractTests
{
    private static readonly string[] ApprovedCompileIncludes =
    [
        @"..\Source\GameplayActivation\Core\**\*.cs",
        @"..\Source\SupportReporting\Core\**\*.cs",
        @"..\Source\TemperatureLimitedDeliveryTargets\TemperatureLimitedDeliveryTargetPrefabConfigurator.cs",
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
        @"..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchPlan.cs",
        @"..\Source\Options\ConstructionTemperatureDraft.cs"
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

    [Test]
    public async Task TestProject_WhenCompileLinksAreInspected_UsesExactApprovedProductionBoundary()
    {
        var testProjectPath = await TestProjectPath();
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

        await Assert.That(compileItems.Select(item => item.Include).ToArray()).IsEquivalentTo((ApprovedCompileIncludes).Cast<string?>(), TUnit.Assertions.Enums.CollectionOrdering.Any);
        await Assert.That(compileItems.All(item =>
                !string.IsNullOrWhiteSpace(item.Link)
                && item.Link!.StartsWith(@"Production\", StringComparison.Ordinal))).IsTrue().Because("Every linked production source must have a semantic Production\\ link path.");
        await Assert.That(compileItems.Any(item => string.Equals(
                item.Include,
                @"..\Source\FastTrackCompatibility\**\*.cs",
                StringComparison.Ordinal))).IsFalse().Because("Runtime FastTrack adapters must never enter the pure linked-source boundary.");

        foreach (var compileItem in compileItems)
        {
            Assert.NotNull(compileItem.Include);
            var shouldBeConditional = ConditionallyLinkedFutureSourceIncludes.Contains(
                compileItem.Include);
            var expectedCondition = shouldBeConditional
                ? $"Exists('{compileItem.Include}')"
                : null;
            await Assert.That(compileItem.Condition).IsEqualTo(expectedCondition).Because(shouldBeConditional
                    ? $"Future exact link {compileItem.Include} must use a path-identical existence guard."
                    : $"Existing or wildcard link {compileItem.Include} must remain unconditional.");
        }
    }

    [Test]
    public async Task LinkedProductionSources_WhenInspected_AreProductionCompileInputsWithoutTestForks()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        var sourceRoot = Path.GetFullPath(Path.Combine(modRoot, "Source"));
        var productionProject = XDocument.Load(
            Path.Combine(sourceRoot, "DeliveryTemperatureLimit.csproj"));

        await Assert.That(productionProject.Descendants("Compile")
                .Any(element => element.Attribute("Remove") is not null)).IsFalse().Because("Production compile exclusions could let a test-linked copy evade the C# 8 build.");
        await Assert.That(productionProject.Descendants("EnableDefaultCompileItems")
                .LastOrDefault()?.Value.Trim()).IsNotEqualTo("false").Because("The production project must retain SDK default compile discovery.");
        await Assert.That(Directory.Exists(Path.Combine(modRoot, "Tests", "Production"))).IsFalse().Because("Production algorithms must be linked from Source, never copied under Tests.");

        foreach (var include in ApprovedCompileIncludes)
        {
            foreach (var sourcePath in ExpandExistingCompileInclude(modRoot, include))
            {
                var fullPath = Path.GetFullPath(sourcePath);
                await Assert.That(IsDescendantOf(sourceRoot, fullPath)).IsTrue().Because($"Linked file escapes the production source tree: {fullPath}");
                await Assert.That(IsDescendantOf(Path.Combine(sourceRoot, "obj"), fullPath)
                    || IsDescendantOf(Path.Combine(sourceRoot, "bin"), fullPath)).IsFalse().Because($"Generated output must not be linked as production source: {fullPath}");

                if (!string.Equals(
                    fullPath,
                    Path.Combine(
                        sourceRoot,
                        "TemperatureLimitedDeliveryTargets",
                        "TemperatureLimitedDeliveryTargetPrefabConfigurator.cs"),
                    StringComparison.OrdinalIgnoreCase))
                {
                    await AssertPureLinkedSource(fullPath);
                }
            }
        }
    }

    [Test]
    public async Task DeclaredStaticMethodProtocol_WhenLocated_IsCoveredByThePureCoreWildcard()
    {
        string repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        string protocolPath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "GameplayActivation",
            "Core",
            "ExternalModIntegration",
            "DeclaredStaticMethodProtocol.cs");

        await Assert.That(File.Exists(protocolPath)).IsTrue().Because("The BCL-only declared protocol verifier must remain inside the " +
            "already linked GameplayActivation/Core boundary.");
        await AssertPureLinkedSource(protocolPath);
    }

    [Test]
    public async Task OniGameTypeStubs_WhenIdentityAndMembersAreInspected_MatchPureSourceDependenciesExactly()
    {
        var tagType = typeof(global::Tag);
        await Assert.That(tagType.FullName).IsEqualTo("Tag");
        await Assert.That(tagType.IsValueType).IsTrue();
        await Assert.That(tagType.CustomAttributes.Any(attribute =>
                attribute.AttributeType == typeof(IsReadOnlyAttribute))).IsTrue().Because("The global Tag test double must be readonly.");
        await Assert.That(tagType.GetInterfaces()).IsEquivalentTo(new[] { typeof(IEquatable<global::Tag>) }, TUnit.Assertions.Enums.CollectionOrdering.Any);

        var tagFields = tagType.GetFields(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly);
        await Assert.That(tagFields).Count().IsEqualTo(1);
        await Assert.That(tagFields[0].Name).IsEqualTo("value");
        await Assert.That(tagFields[0].FieldType).IsEqualTo(typeof(string));
        await Assert.That(tagFields[0].IsPrivate).IsTrue();
        await Assert.That(tagFields[0].IsInitOnly).IsTrue();
        await Assert.That(tagFields[0].IsStatic).IsFalse();

        await Assert.That(tagType.GetProperties(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly)).IsEmpty();

        var tagConstructors = tagType.GetConstructors(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly);
        await Assert.That(tagConstructors).Count().IsEqualTo(1);
        await Assert.That(tagConstructors[0].IsPublic).IsTrue();
        await Assert.That(tagConstructors[0]
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray()).IsEquivalentTo(new[] { typeof(string) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var tagMethodSignatures = tagType.GetMethods(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly)
            .Select(MethodSignature)
            .ToArray();
        await Assert.That(tagMethodSignatures).IsEquivalentTo(new[]
            {
                "Equals(System.Object):System.Boolean",
                "Equals(Tag):System.Boolean",
                "GetHashCode():System.Int32"
            }, TUnit.Assertions.Enums.CollectionOrdering.Any);

        var temperatureLimitType = typeof(global::DeliveryTemperatureLimit.TemperatureLimit);
        await Assert.That(temperatureLimitType.FullName).IsEqualTo("DeliveryTemperatureLimit.TemperatureLimit");
        await Assert.That(temperatureLimitType.Assembly.GetType(
                "TemperatureLimit",
                throwOnError: false,
                ignoreCase: false)).IsNull().Because("A global TemperatureLimit alias would conceal a production namespace dependency.");
        await Assert.That(temperatureLimitType.GetFields(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly)).IsEmpty();
        await Assert.That(temperatureLimitType.GetProperties(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly)).IsEmpty();
        await Assert.That(temperatureLimitType.GetMethods(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly)).IsEmpty();

        var constructors = temperatureLimitType.GetConstructors(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly);
        await Assert.That(constructors).Count().IsEqualTo(1);
        await Assert.That(constructors[0].IsPublic).IsTrue();
        await Assert.That(constructors[0].GetParameters()).IsEmpty();
    }

    [Test]
    public async Task OniGameTypeStubs_WhenLocated_HaveSemanticFileIdentityWithoutLegacyAliasFile()
    {
        var testRoot = Path.GetDirectoryName((await TestProjectPath()))!;

        await Assert.That(File.Exists(Path.Combine(
            testRoot,
            "TestDoubles",
            "OniGameTypeStubs.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(testRoot, "GameStubs.cs"))).IsFalse();
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

    private static async Task AssertPureLinkedSource(string sourcePath)
    {
        var source = File.ReadAllText(sourcePath);
        var forbiddenFragments = new[]
        {
            "using HarmonyLib",
            "using UnityEngine",
            "using PeterHan",
            "using KMod",
            "UnityEngine.",
            "KMod.",
            "#if",
            "#elif",
            "#else"
        };
        foreach (var forbiddenFragment in forbiddenFragments)
        {
            await Assert.That(source.Contains(forbiddenFragment, StringComparison.Ordinal)).IsFalse().Because($"Linked pure source {sourcePath} contains '{forbiddenFragment}' and crosses " +
                "the game/runtime or conditional-test boundary.");
        }

        await AssertHarmonyNamespaceReferencesAreReflectionContractLiterals(
            sourcePath,
            source);
        await AssertPeterHanNamespaceReferencesAreReflectionContractLiterals(
            sourcePath,
            source);
    }

    private static async Task AssertHarmonyNamespaceReferencesAreReflectionContractLiterals(
        string sourcePath,
        string source)
    {
        const string thirdPartyNamespacePrefix = "HarmonyLib.";
        if (!source.Contains(
                thirdPartyNamespacePrefix,
                StringComparison.Ordinal))
        {
            return;
        }

        string sourceOutsideRegularStringLiterals =
            System.Text.RegularExpressions.Regex.Replace(
                source,
                "\"(?:\\\\.|[^\"\\\\])*\"",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        await Assert.That(sourceOutsideRegularStringLiterals.Contains(
                thirdPartyNamespacePrefix,
                StringComparison.Ordinal)).IsFalse().Because($"Linked pure source {sourcePath} may name HarmonyLib types only " +
            "inside reflection-contract string literals.");
    }

    private static async Task AssertPeterHanNamespaceReferencesAreReflectionContractLiterals(
        string sourcePath,
        string source)
    {
        const string thirdPartyNamespacePrefix = "PeterHan.";
        var referenceIndex = source.IndexOf(
            thirdPartyNamespacePrefix,
            StringComparison.Ordinal);
        if (referenceIndex < 0)
        {
            return;
        }

        var permittedReflectionContractDirectory = Path.Combine(
            "FastTrackCompatibility",
            "FeatureContractVerification");
        var permittedRuntimePlanPath = Path.Combine(
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchPlan.cs");
        await Assert.That(sourcePath.Contains(
                permittedReflectionContractDirectory,
                StringComparison.Ordinal) ||
            sourcePath.EndsWith(
                permittedRuntimePlanPath,
                StringComparison.Ordinal)).IsTrue().Because($"Linked pure source {sourcePath} may name PeterHan types only in " +
            "the reflection-only feature contracts or as the exact Harmony " +
            "owner literal in the immutable runtime plan.");

        while (referenceIndex >= 0)
        {
            await Assert.That(referenceIndex > 0 && source[referenceIndex - 1] == '"').IsTrue().Because($"Linked pure source {sourcePath} contains a PeterHan namespace " +
                "reference that is not the start of an explicit reflection " +
                "contract string literal.");
            referenceIndex = source.IndexOf(
                thirdPartyNamespacePrefix,
                referenceIndex + thirdPartyNamespacePrefix.Length,
                StringComparison.Ordinal);
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

    private static string MethodSignature(MethodInfo method)
    {
        var parameterTypes = string.Join(
            ",",
            method.GetParameters().Select(parameter =>
                parameter.ParameterType.FullName));
        return $"{method.Name}({parameterTypes}):{method.ReturnType.FullName}";
    }

    private static async Task<string> TestProjectPath() =>
        Path.Combine(
            (await RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")),
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
