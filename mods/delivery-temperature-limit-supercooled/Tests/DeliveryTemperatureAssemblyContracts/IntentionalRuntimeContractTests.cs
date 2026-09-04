using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

/// <summary>
/// Guards the intentionally supported runtime and serialization boundary. These
/// source-level assertions fail before a production assembly can be built; the
/// matching metadata assertions run against every newly merged candidate.
/// </summary>
[TestClass]
public sealed class IntentionalRuntimeContractTests
{
    private static readonly string[] IntentionalPublicTypeNames =
    [
        "DeliveryTemperatureLimit.DeliveryTemperatureLimitMod",
        "DeliveryTemperatureLimit.DeliveryTemperatureLimitOptions",
        "DeliveryTemperatureLimit.TemperatureLimit",
        "STRINGS.TEMPERATURELIMIT"
    ];

    private static readonly string[] IntentionalTemperatureLimitMemberNames =
    [
        "MinValue",
        "MaxValue",
        "IsDisabled",
        "LowLimit",
        "HighLimit",
        "Get",
        "CopySettings",
        "SetLowLimit",
        "SetHighLimit",
        "Disable",
        "AllowedByTemperature",
        "OnPrefabInit",
        "OnSpawn",
        "OnCleanUp"
    ];

    private static readonly string[] IntentionalModMemberNames =
    [
        "OnLoad",
        "OnAllModsLoaded"
    ];

    private static readonly string[] IntentionalPersistedOptionPropertyNames =
    [
        "CheckTemperatureForStatusItems",
        "UnderConstructionLimit",
        "MaxConstructionTemperature",
        "MinConstructionTemperature"
    ];

    private static readonly string[] IntentionalSupportActionPropertyNames =
    [
        "CreateSupportReport",
        "CreateExtendedSupportReport"
    ];

    private static readonly string[] IntentionalOptionPropertyNames =
    [
        .. IntentionalPersistedOptionPropertyNames,
        .. IntentionalSupportActionPropertyNames
    ];

    private static readonly string[] IntentionalOptionMemberNames =
    [
        "CheckTemperatureForStatusItems",
        "UnderConstructionLimit",
        "MaxConstructionTemperature",
        "MinConstructionTemperature",
        "CreateSupportReport",
        "CreateExtendedSupportReport",
        "ToString"
    ];

    private static readonly string[] IntentionalLocalizationFieldNames =
    [
        "LABEL",
        "RANGE_SEPARATOR",
        "TOOLTIP_RANGE",
        "TOOLTIP_NOTSET",
        "SIDESCREEN_TITLE"
    ];

    [TestMethod]
    public void Source_WhenIntentionalRuntimeBoundaryIsInspected_UsesSemanticOwnersWithoutCompatibilityFacades()
    {
        string sourceRoot = ResolveSourceRoot();
        string componentSource = ReadRequiredSource(
            sourceRoot,
            "TemperatureLimitedDeliveryTargets",
            "TemperatureLimit.cs");
        string modSource = ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitMod.cs");
        string optionSource = ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitOptions.cs");
        string stringsSource = ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitStrings.cs");

        StringAssert.Contains(
            componentSource,
            "public class TemperatureLimit : KMonoBehaviour");
        StringAssert.Contains(
            modSource,
            "public sealed class DeliveryTemperatureLimitMod : KMod.UserMod2");
        StringAssert.Contains(
            modSource,
            "typeof(DeliveryTemperatureLimitOptions)");
        Assert.IsFalse(
            modSource.Contains("typeof(Options)", StringComparison.Ordinal),
            "The PLib registration must not retain a renamed-options shim.");
        StringAssert.Contains(
            optionSource,
            "public sealed class DeliveryTemperatureLimitOptions");
        StringAssert.Contains(
            stringsSource,
            "public class TEMPERATURELIMIT");
        Assert.IsFalse(
            componentSource.Contains("TemperatureIndexData", StringComparison.Ordinal));
        Assert.IsFalse(
            componentSource.Contains("getTemperatureIndexData", StringComparison.Ordinal));
        Assert.IsFalse(
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Any(source => source.Contains(
                    "class DeliveryTemperatureLimitStrings",
                    StringComparison.Ordinal)),
            "The Klei localization key must not gain a parallel facade type.");
    }

    [TestMethod]
    public void TemperatureLimitSource_WhenGameSessionOwnershipIsInspected_UsesOneIndexedRegistrationWithoutGlobalFallback()
    {
        string source = ReadRequiredSource(
            ResolveSourceRoot(),
            "TemperatureLimitedDeliveryTargets",
            "TemperatureLimit.cs");

        StringAssert.Contains(source, "TryGetRegisteredComponent(");
        StringAssert.Contains(source, "RegisterTemperatureLimit(");
        StringAssert.Contains(source, "TryReplaceTemperatureConstraint(");
        StringAssert.Contains(source, "RemoveTemperatureLimit(");
        StringAssert.Contains(source, "if (component == null)");
        Assert.IsFalse(
            source.Contains("EnsureGameSession", StringComparison.Ordinal),
            "A component lifecycle callback may capture but never create a session.");
        Assert.IsFalse(
            source.Contains("Dictionary<", StringComparison.Ordinal) ||
            source.Contains("List<", StringComparison.Ordinal),
            "The component must not recreate a process-global lookup collection.");

        int normalizationIndex = RequireIndex(
            source,
            "DeliveryTemperatureConstraint.FromSerializedLimits(",
            startIndex: 0);
        int unchangedComparisonIndex = RequireIndex(
            source,
            "if (lowLimit == canonicalConstraint.MinimumInclusiveKelvin",
            normalizationIndex);
        int fieldAssignmentIndex = RequireIndex(
            source,
            "lowLimit = canonicalConstraint.MinimumInclusiveKelvin",
            unchangedComparisonIndex);
        int replacementPublicationIndex = RequireIndex(
            source,
            "PublishConstraintReplacement(canonicalConstraint)",
            fieldAssignmentIndex);
        Assert.IsTrue(
            normalizationIndex >= 0 &&
            normalizationIndex < unchangedComparisonIndex &&
            unchangedComparisonIndex < fieldAssignmentIndex &&
            fieldAssignmentIndex < replacementPublicationIndex,
            "Setters must normalize, reject an unchanged value, update serialized " +
            "fields, and then publish one exact-owner replacement.");
    }

    [TestMethod]
    public void TemperatureLimitSource_WhenSerializationBoundaryIsInspected_PreservesFieldsAndCanonicalBounds()
    {
        string source = ReadRequiredSource(
            ResolveSourceRoot(),
            "TemperatureLimitedDeliveryTargets",
            "TemperatureLimit.cs");

        AssertSerializedIntegerField(source, "lowLimit");
        AssertSerializedIntegerField(source, "highLimit");
        StringAssert.Contains(source, "public const int MinValue = 0;");
        StringAssert.Contains(
            source,
            "public const int MaxValue = OniStorableTemperatureBounds.MaximumTemperatureKelvin;");
        foreach (string memberName in IntentionalTemperatureLimitMemberNames)
        {
            StringAssert.Contains(
                source,
                memberName,
                $"The intentional TemperatureLimit member {memberName} is absent.");
        }
    }

    [TestMethod]
    public void OptionsSource_WhenSerializationBoundaryIsInspected_PreservesExactOptInPropertiesAndDefaults()
    {
        string source = ReadRequiredSource(
            ResolveSourceRoot(),
            "DeliveryTemperatureLimitOptions.cs");

        StringAssert.Contains(source, "[JsonObject(MemberSerialization.OptIn)]");
        StringAssert.Contains(source, "[ConfigFile(SharedConfigLocation: true)]");
        StringAssert.Contains(source, "[RestartRequired]");
        StringAssert.Contains(
            source,
            "internal static DeliveryTemperatureLimitOptions Instance");
        Assert.IsFalse(
            source.Contains("SingletonOptions<", StringComparison.Ordinal) ||
            Regex.IsMatch(
                source,
                @"\bIOptions\b",
                RegexOptions.CultureInvariant),
            "A public PLib base or interface would force merged PLib " +
            "implementation types back into the assembly's public contract.");
        StringAssert.Contains(source, "CheckTemperatureForStatusItems = true;");
        StringAssert.Contains(source, "UnderConstructionLimit = false;");
        foreach (string propertyName in IntentionalPersistedOptionPropertyNames)
        {
            Assert.AreEqual(
                1,
                Regex.Matches(
                    source,
                    @"\[JsonProperty\]\s+public\s+(?:bool|int)\s+" +
                    Regex.Escape(propertyName) +
                    @"\s*\{\s*get;\s*set;\s*\}",
                    RegexOptions.CultureInvariant).Count,
                $"Option {propertyName} must be one exact public opt-in JSON property.");
        }

        string[] declaredOptionPropertyNames = Regex.Matches(
                source,
                @"public\s+(?:bool|int|System\.Action<object>)\s+([A-Za-z]\w*)\s*(?:\{\s*get;\s*set;\s*\}|=>)",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            IntentionalOptionPropertyNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray(),
            declaredOptionPropertyNames,
            "The options type must expose exactly the four persisted values and " +
            "the two approved non-persisted support actions.");

        foreach (string propertyName in IntentionalSupportActionPropertyNames)
        {
            Assert.AreEqual(
                1,
                Regex.Matches(
                    source,
                    @"\[Option\(\s*""[^""]+"",\s*""[^""]+"",\s*""Support""\)\]\s*" +
                    @"\[JsonIgnore\]\s*public\s+System\.Action<object>\s+" +
                    Regex.Escape(propertyName) +
                    @"\s*=>",
                    RegexOptions.CultureInvariant).Count,
                $"Support action {propertyName} must be one read-only " +
                "System.Action<object>, which PLib maps to a button, " +
                "with [Option] and [JsonIgnore].");
            Assert.AreEqual(
                0,
                Regex.Matches(
                    source,
                    @"\[JsonProperty\][\s\S]{0,200}public\s+System\.Action<object>\s+" +
                    Regex.Escape(propertyName),
                    RegexOptions.CultureInvariant).Count,
                $"Support action {propertyName} must not be persisted.");
        }
    }

    [TestMethod]
    public void LocalizationSource_WhenInspected_PreservesExactKleiLocalizationKeys()
    {
        string source = ReadRequiredSource(
            ResolveSourceRoot(),
            "DeliveryTemperatureLimitStrings.cs");

        foreach (string fieldName in IntentionalLocalizationFieldNames)
        {
            Assert.AreEqual(
                1,
                Regex.Matches(
                    source,
                    @"public\s+static\s+LocString\s+" +
                    Regex.Escape(fieldName) +
                    @"\s*=",
                    RegexOptions.CultureInvariant).Count,
                $"Localization field STRINGS.TEMPERATURELIMIT.{fieldName} changed.");
        }
    }

    internal static void AssertMergedAssembly(string assemblyPath)
    {
        IReadOnlyList<string> publicSurface =
            DeliveryTemperatureAssemblyMetadataReader.ReadPublicSurface(
                assemblyPath);
        string[] declaredPublicTypes = publicSurface
            .Where(contract => contract.StartsWith("type|", StringComparison.Ordinal))
            .Select(contract => contract.Split('|')[1])
            .ToArray();

        string[] unexpectedPublicTypes = declaredPublicTypes
            .Except(IntentionalPublicTypeNames, StringComparer.Ordinal)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToArray();
        string[] missingPublicTypes = IntentionalPublicTypeNames
            .Except(declaredPublicTypes, StringComparer.Ordinal)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToArray();
        Assert.IsEmpty(
            unexpectedPublicTypes,
            $"The merged assembly at {assemblyPath} exposes unintended public " +
            $"types: {string.Join(", ", unexpectedPublicTypes)}");
        Assert.IsEmpty(
            missingPublicTypes,
            $"The merged assembly at {assemblyPath} is missing intentional public " +
            $"types: {string.Join(", ", missingPublicTypes)}");
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "DeliveryTemperatureLimit.TemperatureLimit",
            IntentionalTemperatureLimitMemberNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "DeliveryTemperatureLimit.DeliveryTemperatureLimitMod",
            IntentionalModMemberNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "DeliveryTemperatureLimit.DeliveryTemperatureLimitOptions",
            IntentionalOptionMemberNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.TEMPERATURELIMIT",
            IntentionalLocalizationFieldNames);

        Assert.AreEqual(
            0,
            DeliveryTemperatureAssemblyMetadataReader.ReadFieldConstant(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit",
                "MinValue"));
        Assert.AreEqual(
            10000,
            DeliveryTemperatureAssemblyMetadataReader.ReadFieldConstant(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit",
                "MaxValue"));
        DeliveryTemperatureAssemblyMetadataReader.AssertPrivateSerializedInt32Field(
            assemblyPath,
            "DeliveryTemperatureLimit.TemperatureLimit",
            "lowLimit",
            "KSerialization.Serialize",
            "UnityEngine.SerializeField");
        DeliveryTemperatureAssemblyMetadataReader.AssertPrivateSerializedInt32Field(
            assemblyPath,
            "DeliveryTemperatureLimit.TemperatureLimit",
            "highLimit",
            "KSerialization.Serialize",
            "UnityEngine.SerializeField");
        Assert.IsFalse(
            DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit+TemperatureIndexData"));
        Assert.IsFalse(
            DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit",
                "getTemperatureIndexData"));
        Assert.IsTrue(
            DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "FastTrackRuntimeAuthorityIntegrationInspector"));
        Assert.IsTrue(
            DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "FastTrackRuntimeAuthorityContributionBuilder"));
        Assert.IsTrue(
            DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "FastTrackRuntimeAuthorityContributionBuilder",
                "Build"));
        Assert.IsFalse(
            DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareFastTrackWorldInventoryTemperaturePatches"));
        Assert.IsFalse(
            DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareFastTrackPickupTemperaturePatches"));
        Assert.IsFalse(
            DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareFastTrackDirectDeliveryEligibilityPatches"));
        IReadOnlyList<AssemblyMethodBodyContract> selectionPreparationBodies =
            DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareSelectedRuntimePatches");
        Assert.AreEqual(1, selectionPreparationBodies.Count);
        Assert.IsTrue(
            selectionPreparationBodies[0].Instructions.Any(instruction =>
                instruction.ResolvedOperand?.Contains(
                    "DeliveryTemperatureLimit." +
                    "DeliveryTemperatureRuntimePatchPlan." +
                    "get_OrderedPatchBindings",
                    StringComparison.Ordinal) == true),
            "Selected runtime patch preparation must consume only the plan's " +
            "complete verified binding snapshot.");
        Assert.IsFalse(
            selectionPreparationBodies[0].Instructions.Any(instruction =>
                instruction.ResolvedOperand?.Contains(
                    "FastTrackRuntimeAuthorityContributionBuilder.Build",
                    StringComparison.Ordinal) == true),
            "Provider-specific contribution construction must finish before " +
            "selected runtime patch preparation.");
        Assert.IsTrue(
            DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit.ActiveHarmonyPrefixDescriptor"));
        Assert.IsFalse(
            DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit.ActiveHarmonyPatchDescriptor"));
    }

    private static void AssertPublicMemberNamesAreIntentional(
        IEnumerable<string> publicSurface,
        string declaringType,
        IReadOnlyCollection<string> intentionalNames)
    {
        string[] unexpectedMembers = publicSurface
            .Where(contract => !contract.StartsWith("type|", StringComparison.Ordinal))
            .Where(contract => contract.Split('|')[1] == declaringType)
            .Select(contract => contract.Split('|')[2])
            .Where(name => name is not ".ctor" and not ".cctor")
            .Where(name => !name.StartsWith("get_", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("set_", StringComparison.Ordinal))
            .Where(name => !intentionalNames.Contains(name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(
            unexpectedMembers,
            $"{declaringType} exposes unintended members: {string.Join(", ", unexpectedMembers)}");
    }

    private static void AssertSerializedIntegerField(
        string source,
        string fieldName)
    {
        string pattern =
            @"\[Serialize\]\s*\[SerializeField\][^\r\n]*\s*private\s+int\s+" +
            Regex.Escape(fieldName) +
            @"\s*=";
        Assert.AreEqual(
            1,
            Regex.Matches(
                source,
                pattern,
                RegexOptions.CultureInvariant).Count,
            $"{fieldName} must remain one private int with both serialization attributes.");
    }

    private static string ResolveSourceRoot() => Path.Combine(
        ResolveRepositoryRoot(),
        "mods",
        "delivery-temperature-limit-supercooled",
        "Source");

    private static int RequireIndex(
        string source,
        string value,
        int startIndex)
    {
        int index = source.IndexOf(
            value,
            startIndex,
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(
            0,
            index,
            "Missing exact source contract marker: " + value);
        return index;
    }

    private static string ReadRequiredSource(
        string sourceRoot,
        params string[] relativePathSegments)
    {
        string path = relativePathSegments.Aggregate(
            sourceRoot,
            Path.Combine);
        Assert.IsTrue(File.Exists(path), $"Required semantic source owner is absent: {path}");
        return File.ReadAllText(path);
    }

    private static string ResolveRepositoryRoot()
    {
        string? pipelineRepositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(pipelineRepositoryRoot))
        {
            return pipelineRepositoryRoot;
        }

        DirectoryInfo? candidateDirectory =
            new DirectoryInfo(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            string expectedProject = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Tests",
                "DeliveryTemperatureLimit.Tests.csproj");
            if (File.Exists(expectedProject))
            {
                return candidateDirectory.FullName;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        throw new InvalidOperationException(
            "The repository root was neither supplied by ONI Mod Pipeline nor " +
            $"an ancestor of the test assembly directory {AppContext.BaseDirectory}.");
    }
}
