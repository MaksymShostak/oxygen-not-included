using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class IntentionalRuntimeContractTests
{
    private static readonly string[] IntentionalPublicTypeNames =
    [
        "DeliveryTemperatureLimit.DeliveryTemperatureLimitMod",
        "DeliveryTemperatureLimit.DeliveryTemperatureLimitOptions",
        "DeliveryTemperatureLimit.TemperatureLimit",
        "STRINGS.DELIVERY_TEMPERATURE_LIMIT",
        "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN",
        "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+STATUS",
        "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+VALIDATION",
        "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+TOOLTIPS",
        "STRINGS.DELIVERY_TEMPERATURE_LIMIT+OPTIONS"
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
        "SchemaVersion",
        "TemperatureUnit",
        "CheckTemperatureForStatusItems",
        "UnderConstructionLimit",
        "MaxConstructionTemperature",
        "MinConstructionTemperature"
    ];

    private static readonly string[] IntentionalOptionPropertyNames =
    [
        .. IntentionalPersistedOptionPropertyNames
    ];

    private static readonly string[] IntentionalOptionMemberNames =
    [
        .. IntentionalPersistedOptionPropertyNames,
        "ToString"
    ];

    private static readonly string[] IntentionalSideScreenFieldNames =
    [
        "TITLE",
        "SECTION_RANGE",
        "LOWER_BOUND",
        "UPPER_BOUND",
        "BUTTON_CLEAR"
    ];

    private static readonly string[] IntentionalStatusFieldNames =
    [
        "DISABLED",
        "LOWER_BOUND_ONLY",
        "UPPER_BOUND_ONLY",
        "INTERVAL"
    ];

    private static readonly string[] IntentionalValidationFieldNames =
    [
        "EMPTY_INTERVAL",
        "BOUNDS_REVERSED",
        "INVALID_NUMBER",
        "OUT_OF_RANGE"
    ];

    private static readonly string[] IntentionalTooltipsFieldNames =
    [
        "LOWER_BOUND",
        "UPPER_BOUND",
        "CLEAR",
        "STATUS"
    ];

    private static readonly string[] IntentionalOptionsFieldNames =
    [
        "DIALOG_TITLE",
        "LABEL_SETTINGS_SCOPE",
        "DIALOG_INTRO",
        "RESTART_NOTICE",
        "PENDING_RESTART_NOTICE",
        "SECTION_CONSTRUCTION",
        "CHECKBOX_LIMIT_CONSTRUCTION",
        "LABEL_DEFAULT_CONSTRUCTION_RANGE",
        "TOOLTIP_DEFAULT_CONSTRUCTION_RANGE",
        "SECTION_RESOURCE_WARNINGS",
        "CHECKBOX_WARN_ON_BLOCKED",
        "TOOLTIP_WARN_ON_BLOCKED",
        "NOTICE_RESOURCE_WARNINGS_UNAVAILABLE",
        "VALIDATION_INTEGER_REQUIRED",
        "VALIDATION_SUPPORTED_RANGE",
        "VALIDATION_EMPTY_INTERVAL",
        "BUTTON_REVERT_RANGE",
        "BANNER_LEGACY_UNIT",
        "BUTTON_INTERPRET_CELSIUS",
        "BUTTON_INTERPRET_FAHRENHEIT",
        "BUTTON_INTERPRET_KELVIN",
        "BUTTON_REPORT_BUG",
        "DIALOG_REPORT_TITLE",
        "BUTTON_BACK_TO_OPTIONS",
        "SECTION_SUPPORT",
        "SECTION_REPORT_DETAILS",
        "SECTION_ADVANCED_TROUBLESHOOTING",
        "BUTTON_OPEN_HOMEPAGE",
        "BUTTON_OPEN_CONFIG_FOLDER",
        "TOOLTIP_CONFIG_FOLDER",
        "TOOLTIP_SUPPORT_REPORT",
        "CHECKBOX_INCLUDE_PLAYER_LOG",
        "TOOLTIP_INCLUDE_PLAYER_LOG",
        "BUTTON_CREATE_REPORT",
        "STATUS_CREATING_REPORT",
        "STATUS_REPORT_CREATED",
        "STATUS_REPORT_FAILED",
        "STATUS_LOG_INCLUDED",
        "STATUS_LOG_NOT_INCLUDED",
        "STATUS_LOG_UNAVAILABLE",
        "BUTTON_OPEN_LAST_REPORT_FOLDER",
        "BUTTON_COPY_REPORT_SUMMARY",
        "STATUS_SUMMARY_COPIED",
        "BUTTON_OPEN_ISSUE_FORM",
        "NOTICE_ISSUE_FORM",
        "STATUS_ISSUE_FORM_OPENED",
        "STATUS_NO_REPORT",
        "STATUS_ACTION_FAILED",
        "BUTTON_RESTORE_DEFAULTS",
        "TOOLTIP_RESTORE_DEFAULTS",
        "BUTTON_CANCEL",
        "BUTTON_SAVE",
        "STATUS_CHANGES_SAVED",
        "BUTTON_DONE",
        "BUTTON_RESTART_NOW",
        "BUTTON_RESTART_LATER",
        "WARNING_RUNNING_COLONY",
        "DIALOG_DISCARD_TITLE",
        "BUTTON_CONFIRM_DISCARD",
        "BUTTON_CANCEL_DISCARD",
        "ERROR_SAVE_FAILED",
        "ERROR_LOAD_FAILED",
        "ERROR_UI_FAILED"
    ];

    [Test]
    public async Task Source_WhenIntentionalRuntimeBoundaryIsInspected_UsesSemanticOwnersWithoutCompatibilityFacades()
    {
        string sourceRoot = ResolveSourceRoot();
        string componentSource = await ReadRequiredSource(
            sourceRoot,
            "TemperatureLimitedDeliveryTargets",
            "TemperatureLimit.cs");
        string modSource = await ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitMod.cs");
        string optionSource = await ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitOptions.cs");
        string stringsSource = await ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitStrings.cs");

        await Assert.That(componentSource).Contains("public class TemperatureLimit : KMonoBehaviour");
        await Assert.That(modSource).Contains("public sealed class DeliveryTemperatureLimitMod : KMod.UserMod2");
        await Assert.That(modSource).Contains("typeof(STRINGS.DELIVERY_TEMPERATURE_LIMIT)");
        await Assert.That(modSource.Contains("typeof(Options)", StringComparison.Ordinal)).IsFalse().Because("The PLib registration must not retain a renamed-options shim.");
        await Assert.That(optionSource).Contains("public sealed class DeliveryTemperatureLimitOptions");
        await Assert.That(stringsSource).Contains("public static class DELIVERY_TEMPERATURE_LIMIT");
        await Assert.That(componentSource.Contains("TemperatureIndexData", StringComparison.Ordinal)).IsFalse();
        await Assert.That(componentSource.Contains("getTemperatureIndexData", StringComparison.Ordinal)).IsFalse();
        await Assert.That(Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Any(source => source.Contains(
                    "class DeliveryTemperatureLimitStrings",
                    StringComparison.Ordinal))).IsFalse().Because("The Klei localization key must not gain a parallel facade type.");
    }

    [Test]
    public async Task TemperatureLimitStrings_WhenInspected_DoNotContainUnrenderedUnicodeGlyphs()
    {
        string sourceRoot = ResolveSourceRoot();
        string stringsSource = await ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitStrings.cs");

        await Assert.That(stringsSource.Contains("\\u26a0", StringComparison.OrdinalIgnoreCase) ||
            stringsSource.Contains("\u26a0", StringComparison.Ordinal)).IsFalse().Because("Localization strings must not contain U+26A0 which renders as an unrendered tofu box in ONI's UI font.");

        await Assert.That(stringsSource.Contains("\\u25b2", StringComparison.OrdinalIgnoreCase) ||
            stringsSource.Contains("\u25b2", StringComparison.Ordinal)).IsTrue().Because("Warning and error strings must use the renderable U+25B2 hazard triangle glyph.");

        await Assert.That(stringsSource.Contains("\\u2265", StringComparison.OrdinalIgnoreCase) ||
            stringsSource.Contains("\u2265", StringComparison.Ordinal)).IsFalse().Because("Localization strings must not contain U+2265 which is absent from ONI's pre-baked TMP font atlas.");

        await Assert.That(stringsSource).Contains("public static LocString INTERVAL = \"Allows deliveries at or above {0} and below {1}\";").Because("Range status string must use clear natural language matching LOWER_BOUND_ONLY and UPPER_BOUND_ONLY.");
    }

    [Test]
    public async Task TemperatureLimitWidget_WhenDraftsReverted_DeactivatesInputFieldsAndClearsSelection()
    {
        string sourceRoot = ResolveSourceRoot();
        string widgetSource = await ReadRequiredSource(
            sourceRoot,
            "TemperatureLimitUserInterface",
            "TemperatureLimitWidget.cs");

        await Assert.That(widgetSource).Contains("lowField.DeactivateInputField()").Because("RevertDrafts must deactivate the low input field to drop focus.");
        await Assert.That(widgetSource).Contains("highField.DeactivateInputField()").Because("RevertDrafts must deactivate the high input field to drop focus.");
        await Assert.That(widgetSource).Contains("UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null)").Because("RevertDrafts must clear the active EventSystem selection.");
    }


    [Test]
    public async Task TemperatureLimitSource_WhenGameSessionOwnershipIsInspected_UsesOneIndexedRegistrationWithoutGlobalFallback()
    {
        string source = await ReadRequiredSource(
            ResolveSourceRoot(),
            "TemperatureLimitedDeliveryTargets",
            "TemperatureLimit.cs");

        await Assert.That(source).Contains("TryGetRegisteredComponent(");
        await Assert.That(source).Contains("RegisterTemperatureLimit(");
        await Assert.That(source).Contains("TryReplaceTemperatureConstraint(");
        await Assert.That(source).Contains("RemoveTemperatureLimit(");
        await Assert.That(source).Contains("if (component == null)");
        await Assert.That(source.Contains("EnsureGameSession", StringComparison.Ordinal)).IsFalse().Because("A component lifecycle callback may capture but never create a session.");
        await Assert.That(source.Contains("Dictionary<", StringComparison.Ordinal) ||
            source.Contains("List<", StringComparison.Ordinal)).IsFalse().Because("The component must not recreate a process-global lookup collection.");

        int normalizationIndex = await RequireIndex(
            source,
            "DeliveryTemperatureConstraint.FromSerializedLimits(",
            startIndex: 0);
        int unchangedComparisonIndex = await RequireIndex(
            source,
            "if (lowLimit == canonicalConstraint.MinimumInclusiveKelvin",
            normalizationIndex);
        int fieldAssignmentIndex = await RequireIndex(
            source,
            "lowLimit = canonicalConstraint.MinimumInclusiveKelvin",
            unchangedComparisonIndex);
        int replacementPublicationIndex = await RequireIndex(
            source,
            "PublishConstraintReplacement(canonicalConstraint)",
            fieldAssignmentIndex);
        await Assert.That(normalizationIndex >= 0 &&
            normalizationIndex < unchangedComparisonIndex &&
            unchangedComparisonIndex < fieldAssignmentIndex &&
            fieldAssignmentIndex < replacementPublicationIndex).IsTrue().Because("Setters must normalize, reject an unchanged value, update serialized " +
            "fields, and then publish one exact-owner replacement.");
    }

    [Test]
    public async Task TemperatureLimitSource_WhenSerializationBoundaryIsInspected_PreservesFieldsAndCanonicalBounds()
    {
        string source = await ReadRequiredSource(
            ResolveSourceRoot(),
            "TemperatureLimitedDeliveryTargets",
            "TemperatureLimit.cs");

        await AssertSerializedIntegerField(source, "lowLimit");
        await AssertSerializedIntegerField(source, "highLimit");
        await Assert.That(source).Contains("public const int MinValue = 0;");
        await Assert.That(source).Contains("public const int MaxValue = OniStorableTemperatureBounds.MaximumTemperatureKelvin;");
        foreach (string memberName in IntentionalTemperatureLimitMemberNames)
        {
            await Assert.That(source).Contains(memberName).Because($"The intentional TemperatureLimit member {memberName} is absent.");
        }
    }

    [Test]
    public async Task OptionsSource_WhenSerializationBoundaryIsInspected_PreservesExactOptInPropertiesAndDefaults()
    {
        string source = await ReadRequiredSource(
            ResolveSourceRoot(),
            "DeliveryTemperatureLimitOptions.cs");

        await Assert.That(source).Contains("[JsonObject(MemberSerialization.OptIn)]");
        await Assert.That(source).Contains("[ConfigFile(SharedConfigLocation: true)]");
        await Assert.That(source).Contains("internal static DeliveryTemperatureLimitOptions Instance => LoadedOptions.Value;");
        await Assert.That(source.Contains("SingletonOptions<", StringComparison.Ordinal) ||
            Regex.IsMatch(
                source,
                @"\bIOptions\b",
                RegexOptions.CultureInvariant)).IsFalse().Because("A public PLib base or interface would force merged PLib " +
            "implementation types back into the assembly's public contract.");
        await Assert.That(source.Contains("[RestartRequired]", StringComparison.Ordinal)).IsFalse().Because("Restart detection is owned by the custom options session, not a static class attribute.");
        await Assert.That(source).Contains("CheckTemperatureForStatusItems { get; set; } = true;");
        await Assert.That(source).Contains("UnderConstructionLimit { get; set; }");
        await Assert.That(source).Contains("MaxConstructionTemperature { get; set; } = 318;");
        await Assert.That(source).Contains("MinConstructionTemperature { get; set; } = 223;");
        await Assert.That(source).Contains("SchemaVersion { get; set; } = 1;");
        await Assert.That(source).Contains("TemperatureUnit { get; set; } = \"kelvin\";");
        foreach (string propertyName in IntentionalPersistedOptionPropertyNames)
        {
            await Assert.That(Regex.Matches(
                    source,
                    @"\[JsonProperty\]\s+public\s+(?:bool|int|string)\s+" +
                    Regex.Escape(propertyName) +
                    @"\s*\{\s*get;\s*set;\s*\}",
                    RegexOptions.CultureInvariant).Count).IsEqualTo(1).Because($"Option {propertyName} must be one exact public opt-in JSON property.");
        }

        string[] declaredOptionPropertyNames = Regex.Matches(
                source,
                @"public\s+(?:bool|int|string)\s+([A-Za-z]\w*)\s*\{\s*get;\s*set;\s*\}",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        await Assert.That(declaredOptionPropertyNames).IsEquivalentTo(IntentionalOptionPropertyNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("The options type must expose exactly the approved persistent options properties.");
    }

    [Test]
    public async Task LocalizationSource_WhenInspected_PreservesExactKleiLocalizationKeys()
    {
        string source = await ReadRequiredSource(
            ResolveSourceRoot(),
            "DeliveryTemperatureLimitStrings.cs");

        await Assert.That(source).Contains("public static class DELIVERY_TEMPERATURE_LIMIT");
        await Assert.That(source).Contains("public static class SIDESCREEN");
        await Assert.That(source).Contains("public static class STATUS");
        await Assert.That(source).Contains("public static class VALIDATION");
        await Assert.That(source).Contains("public static class TOOLTIPS");
        await Assert.That(source).Contains("public static class OPTIONS");

        int totalLocStrings = Regex.Matches(
            source,
            @"public\s+static\s+LocString\s+[A-Za-z0-9_]+\s*=",
            RegexOptions.CultureInvariant).Count;
        await Assert.That(totalLocStrings).IsEqualTo(82).Because("Total LocString declarations include the two approved runtime failure warnings.");
        await Assert.That(source).Contains("public static LocString GAMEPLAY_FAILURE =");
        await Assert.That(source).Contains("public static LocString USER_INTERFACE_FAILURE =");

        await AssertClassContainsLocStrings(source, "SIDESCREEN", IntentionalSideScreenFieldNames);
        await AssertClassContainsLocStrings(source, "STATUS", IntentionalStatusFieldNames);
        await AssertClassContainsLocStrings(source, "VALIDATION", IntentionalValidationFieldNames);
        await AssertClassContainsLocStrings(source, "TOOLTIPS", IntentionalTooltipsFieldNames);
        await AssertClassContainsLocStrings(source, "OPTIONS", IntentionalOptionsFieldNames);

        await Assert.That(Regex.IsMatch(source, @"\b(LABEL|RANGE_SEPARATOR|TOOLTIP_RANGE|TOOLTIP_NOTSET)\b")).IsFalse().Because("Dead legacy localization keys must not be retained.");
    }

    private static async Task AssertClassContainsLocStrings(string source, string className, string[] expectedFields)
    {
        int classIndex = source.IndexOf($"class {className}", StringComparison.Ordinal);
        await Assert.That(classIndex >= 0).IsTrue().Because($"Class {className} must be declared.");
        int openBrace = source.IndexOf('{', classIndex);
        await Assert.That(openBrace >= 0).IsTrue();
        int depth = 0;
        int closeBrace = -1;
        for (int i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    closeBrace = i;
                    break;
                }
            }
        }
        await Assert.That(closeBrace > openBrace).IsTrue();
        string classBody = source.Substring(openBrace, closeBrace - openBrace + 1);
        foreach (string field in expectedFields)
        {
            await Assert.That(Regex.IsMatch(classBody, @"public\s+static\s+LocString\s+" + Regex.Escape(field) + @"\s*=")).IsTrue().Because($"Class {className} must declare LocString {field}.");
        }
    }

    private static async Task AssertLocalizationPotMatchesAssembly(string assemblyPath)
    {
        string sourceRoot = ResolveSourceRoot();
        string potPath = Path.GetFullPath(
            Path.Combine(sourceRoot, "..", "translations", "delivery_temperature_limit.pot"));
        await Assert.That(File.Exists(potPath)).IsTrue().Because($"The POT catalog must exist at {potPath}.");

        string potContent = File.ReadAllText(potPath);
        string[] potContextKeys = Regex.Matches(
                potContent,
                @"^msgctxt\s+""([^""]+)""",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        string[] declaredKeys = DeliveryTemperatureAssemblyMetadataReader
            .ReadLocalizationKeys(assemblyPath).ToArray();
        await Assert.That(declaredKeys).IsNotEmpty();

        await Assert.That(potContextKeys).IsEquivalentTo(declaredKeys, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("Every C# LocString must have an exact matching msgctxt in the POT template, with no orphaned keys.");
    }

    [Test]
    public async Task LocalizationPoCatalogs_WhenComparedWithPot_MatchAllDeclaredKeysExactly()
    {
        string sourceRoot = ResolveSourceRoot();
        string translationsDir = Path.GetFullPath(
            Path.Combine(sourceRoot, "..", "translations"));
        await Assert.That(Directory.Exists(translationsDir)).IsTrue().Because($"Translations directory must exist at {translationsDir}.");

        string potPath = Path.Combine(translationsDir, "delivery_temperature_limit.pot");
        await Assert.That(File.Exists(potPath)).IsTrue().Because($"The POT catalog must exist at {potPath}.");

        string potContent = File.ReadAllText(potPath);
        string[] potContextKeys = Regex.Matches(
                potContent,
                @"^msgctxt\s+""([^""]+)""",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        string[] poFiles = Directory.GetFiles(translationsDir, "*.po");
        await Assert.That(poFiles.Length >= 18).IsTrue().Because($"Expected at least 18 language catalogs (.po), found {poFiles.Length}.");

        string[] expectedLanguageCodes =
        [
            "cs", "de", "el", "es", "fr", "hu", "it", "ja", "ko", "pl", "pt", "pt_BR", "th", "tr", "uk", "vi", "zh", "zh_tw"
        ];
        string[] actualLanguageCodes = poFiles
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        foreach (string expectedCode in expectedLanguageCodes)
        {
            await Assert.That(actualLanguageCodes).Contains(expectedCode).Because($"Expected language catalog for '{expectedCode}' was not found.");
        }

        foreach (string poFile in poFiles)
        {
            string fileName = Path.GetFileName(poFile);
            string poContent = File.ReadAllText(poFile);
            string[] poContextKeys = Regex.Matches(
                    poContent,
                    @"^msgctxt\s+""([^""]+)""",
                    RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            await Assert.That(poContextKeys).IsEquivalentTo(potContextKeys, TUnit.Assertions.Enums.CollectionOrdering.Any).Because($"PO catalog '{fileName}' does not match the declared keys of the POT template.");

            await Assert.That(poContent.Contains("—")).IsFalse().Because($"PO catalog '{fileName}' must not contain em-dashes ('—'); use standard hyphens (' - ').");
        }
    }

    internal static async Task AssertMergedAssembly(string assemblyPath)
    {
        await AssertLocalizationPotMatchesAssembly(assemblyPath);
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
        await Assert.That(unexpectedPublicTypes).IsEmpty().Because($"The merged assembly at {assemblyPath} exposes unintended public " +
            $"types: {string.Join(", ", unexpectedPublicTypes)}");
        await Assert.That(missingPublicTypes).IsEmpty().Because($"The merged assembly at {assemblyPath} is missing intentional public " +
            $"types: {string.Join(", ", missingPublicTypes)}");
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "DeliveryTemperatureLimit.TemperatureLimit",
            IntentionalTemperatureLimitMemberNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "DeliveryTemperatureLimit.DeliveryTemperatureLimitMod",
            IntentionalModMemberNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "DeliveryTemperatureLimit.DeliveryTemperatureLimitOptions",
            IntentionalOptionMemberNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT",
            ["GAMEPLAY_FAILURE", "USER_INTERFACE_FAILURE"]);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN",
            IntentionalSideScreenFieldNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+STATUS",
            IntentionalStatusFieldNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+VALIDATION",
            IntentionalValidationFieldNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+TOOLTIPS",
            IntentionalTooltipsFieldNames);
        await AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+OPTIONS",
            IntentionalOptionsFieldNames);

        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.ReadFieldConstant(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit",
                "MinValue")).IsEqualTo(0);
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.ReadFieldConstant(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit",
                "MaxValue")).IsEqualTo(10000);
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
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit+TemperatureIndexData")).IsFalse();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit.TemperatureLimit",
                "getTemperatureIndexData")).IsFalse();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "FastTrackRuntimeAuthorityIntegrationInspector")).IsTrue();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "FastTrackRuntimeAuthorityContributionBuilder")).IsTrue();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "FastTrackRuntimeAuthorityContributionBuilder",
                "Build")).IsTrue();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareFastTrackWorldInventoryTemperaturePatches")).IsFalse();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareFastTrackPickupTemperaturePatches")).IsFalse();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.MethodExists(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareFastTrackDirectDeliveryEligibilityPatches")).IsFalse();
        IReadOnlyList<AssemblyMethodBodyContract> selectionPreparationBodies =
            DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                assemblyPath,
                "DeliveryTemperatureLimit." +
                "DeliveryTemperatureRuntimePatchInstaller",
                "PrepareSelectedRuntimePatches");
        await Assert.That(selectionPreparationBodies.Count).IsEqualTo(1);
        await Assert.That(selectionPreparationBodies[0].Instructions.Any(instruction =>
                instruction.ResolvedOperand?.Contains(
                    "DeliveryTemperatureLimit." +
                    "DeliveryTemperatureRuntimePatchPlan." +
                    "get_OrderedPatchBindings",
                    StringComparison.Ordinal) == true)).IsTrue().Because("Selected runtime patch preparation must consume only the plan's " +
            "complete verified binding snapshot.");
        await Assert.That(selectionPreparationBodies[0].Instructions.Any(instruction =>
                instruction.ResolvedOperand?.Contains(
                    "FastTrackRuntimeAuthorityContributionBuilder.Build",
                    StringComparison.Ordinal) == true)).IsFalse().Because("Provider-specific contribution construction must finish before " +
            "selected runtime patch preparation.");
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit.ActiveHarmonyPrefixDescriptor")).IsTrue();
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                assemblyPath,
                "DeliveryTemperatureLimit.ActiveHarmonyPatchDescriptor")).IsFalse();
    }

    private static async Task AssertPublicMemberNamesAreIntentional(
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

        await Assert.That(unexpectedMembers).IsEmpty().Because($"{declaringType} exposes unintended members: {string.Join(", ", unexpectedMembers)}");
    }

    private static async Task AssertSerializedIntegerField(
        string source,
        string fieldName)
    {
        string pattern =
            @"\[Serialize\]\s*\[SerializeField\][^\r\n]*\s*private\s+int\s+" +
            Regex.Escape(fieldName) +
            @"\s*=";
        await Assert.That(Regex.Matches(
                source,
                pattern,
                RegexOptions.CultureInvariant).Count).IsEqualTo(1).Because($"{fieldName} must remain one private int with both serialization attributes.");
    }

    private static string ResolveSourceRoot() => Path.Combine(
        ResolveRepositoryRoot(),
        "mods",
        "delivery-temperature-limit-supercooled",
        "Source");

    private static async Task<int> RequireIndex(
        string source,
        string value,
        int startIndex)
    {
        int index = source.IndexOf(
            value,
            startIndex,
            StringComparison.Ordinal);
        await Assert.That(index).IsGreaterThanOrEqualTo(0).Because("Missing exact source contract marker: " + value);
        return index;
    }

    private static async Task<string> ReadRequiredSource(
        string sourceRoot,
        params string[] relativePathSegments)
    {
        string path = relativePathSegments.Aggregate(
            sourceRoot,
            Path.Combine);
        await Assert.That(File.Exists(path)).IsTrue().Because($"Required semantic source owner is absent: {path}");
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
