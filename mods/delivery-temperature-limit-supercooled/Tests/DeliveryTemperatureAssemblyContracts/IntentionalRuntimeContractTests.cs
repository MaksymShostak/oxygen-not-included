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
        "DIALOG_INTRO",
        "RESTART_NOTICE",
        "PENDING_RESTART_NOTICE",
        "SECTION_CONSTRUCTION",
        "CHECKBOX_LIMIT_CONSTRUCTION",
        "LABEL_DEFAULT_CONSTRUCTION_RANGE",
        "TOOLTIP_DEFAULT_CONSTRUCTION_RANGE",
        "HINT_CONSTRUCTION_DISABLED",
        "SECTION_RESOURCE_WARNINGS",
        "CHECKBOX_WARN_ON_BLOCKED",
        "TOOLTIP_WARN_ON_BLOCKED",
        "NOTICE_RESOURCE_WARNINGS_UNAVAILABLE",
        "VALIDATION_INTEGER_REQUIRED",
        "VALIDATION_SUPPORTED_RANGE",
        "VALIDATION_EMPTY_INTERVAL",
        "VALIDATION_INVALID_PENDING",
        "BUTTON_REVERT_RANGE",
        "BANNER_LEGACY_UNIT",
        "BUTTON_INTERPRET_CELSIUS",
        "BUTTON_INTERPRET_FAHRENHEIT",
        "BUTTON_INTERPRET_KELVIN",
        "BUTTON_EXPAND_HELP",
        "BUTTON_COLLAPSE_HELP",
        "LABEL_INSTALLED_VERSION",
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
        "BUTTON_OPEN_LAST_REPORT_FOLDER",
        "BUTTON_COPY_REPORT_SUMMARY",
        "STATUS_SUMMARY_COPIED",
        "BUTTON_OPEN_ISSUE_FORM",
        "TOOLTIP_ISSUE_FORM",
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
            "typeof(STRINGS.DELIVERY_TEMPERATURE_LIMIT)");
        Assert.IsFalse(
            modSource.Contains("typeof(Options)", StringComparison.Ordinal),
            "The PLib registration must not retain a renamed-options shim.");
        StringAssert.Contains(
            optionSource,
            "public sealed class DeliveryTemperatureLimitOptions");
        StringAssert.Contains(
            stringsSource,
            "public static class DELIVERY_TEMPERATURE_LIMIT");
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
    public void TemperatureLimitStrings_WhenInspected_DoNotContainUnrenderedUnicodeGlyphs()
    {
        string sourceRoot = ResolveSourceRoot();
        string stringsSource = ReadRequiredSource(
            sourceRoot,
            "DeliveryTemperatureLimitStrings.cs");

        Assert.IsFalse(
            stringsSource.Contains("\\u26a0", StringComparison.OrdinalIgnoreCase) ||
            stringsSource.Contains("\u26a0", StringComparison.Ordinal),
            "Localization strings must not contain U+26A0 which renders as an unrendered tofu box in ONI's UI font.");

        Assert.IsTrue(
            stringsSource.Contains("\\u25b2", StringComparison.OrdinalIgnoreCase) ||
            stringsSource.Contains("\u25b2", StringComparison.Ordinal),
            "Warning and error strings must use the renderable U+25B2 hazard triangle glyph.");

        Assert.IsFalse(
            stringsSource.Contains("\\u2265", StringComparison.OrdinalIgnoreCase) ||
            stringsSource.Contains("\u2265", StringComparison.Ordinal),
            "Localization strings must not contain U+2265 which is absent from ONI's pre-baked TMP font atlas.");

        StringAssert.Contains(
            stringsSource,
            "public static LocString INTERVAL = \"Allows deliveries at or above {0} and below {1}\";",
            "Range status string must use clear natural language matching LOWER_BOUND_ONLY and UPPER_BOUND_ONLY.");
    }

    [TestMethod]
    public void TemperatureLimitSideScreen_WhenKeyboardInputInspected_OverridesInputEventHandlersToProtectGameHotkeys()
    {
        string sourceRoot = ResolveSourceRoot();
        string sideScreenSource = ReadRequiredSource(
            sourceRoot,
            "TemperatureLimitUserInterface",
            "TemperatureLimitSideScreen.cs");

        StringAssert.Contains(
            sideScreenSource,
            "public override void OnKeyDown(KButtonEvent e)",
            "Side screen must explicitly override OnKeyDown to prevent KScreen from consuming game hotkeys when unfocused.");
        StringAssert.Contains(
            sideScreenSource,
            "public override void OnKeyUp(KButtonEvent e)",
            "Side screen must explicitly override OnKeyUp to prevent KScreen from consuming game hotkeys when unfocused.");
        StringAssert.Contains(
            sideScreenSource,
            "if (!e.Consumed && isEditing)",
            "Side screen must only consume key events when text input editing is active.");
        Assert.IsFalse(
            sideScreenSource.Contains("base.OnKeyDown", StringComparison.Ordinal),
            "Side screen must not call base.OnKeyDown because KScreen consumes keys or processes child scroll rects.");
        Assert.IsFalse(
            sideScreenSource.Contains("base.OnKeyUp", StringComparison.Ordinal),
            "Side screen must not call base.OnKeyUp because KScreen consumes keys or processes child scroll rects.");
    }

    [TestMethod]
    public void TemperatureLimitWidget_WhenDraftsReverted_DeactivatesInputFieldsAndClearsSelection()
    {
        string sourceRoot = ResolveSourceRoot();
        string widgetSource = ReadRequiredSource(
            sourceRoot,
            "TemperatureLimitUserInterface",
            "TemperatureLimitWidget.cs");

        StringAssert.Contains(
            widgetSource,
            "lowField.DeactivateInputField()",
            "RevertDrafts must deactivate the low input field to drop focus.");
        StringAssert.Contains(
            widgetSource,
            "highField.DeactivateInputField()",
            "RevertDrafts must deactivate the high input field to drop focus.");
        StringAssert.Contains(
            widgetSource,
            "UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null)",
            "RevertDrafts must clear the active EventSystem selection.");
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
        StringAssert.Contains(
            source,
            "internal static DeliveryTemperatureLimitOptions Instance => LoadedOptions.Value;");
        Assert.IsFalse(
            source.Contains("SingletonOptions<", StringComparison.Ordinal) ||
            Regex.IsMatch(
                source,
                @"\bIOptions\b",
                RegexOptions.CultureInvariant),
            "A public PLib base or interface would force merged PLib " +
            "implementation types back into the assembly's public contract.");
        Assert.IsFalse(
            source.Contains("[RestartRequired]", StringComparison.Ordinal),
            "Restart detection is owned by the custom options session, not a static class attribute.");
        StringAssert.Contains(source, "CheckTemperatureForStatusItems { get; set; } = true;");
        StringAssert.Contains(source, "UnderConstructionLimit { get; set; }");
        StringAssert.Contains(source, "MaxConstructionTemperature { get; set; } = 318;");
        StringAssert.Contains(source, "MinConstructionTemperature { get; set; } = 223;");
        StringAssert.Contains(source, "SchemaVersion { get; set; } = 1;");
        StringAssert.Contains(source, "TemperatureUnit { get; set; } = \"kelvin\";");
        foreach (string propertyName in IntentionalPersistedOptionPropertyNames)
        {
            Assert.AreEqual(
                1,
                Regex.Matches(
                    source,
                    @"\[JsonProperty\]\s+public\s+(?:bool|int|string)\s+" +
                    Regex.Escape(propertyName) +
                    @"\s*\{\s*get;\s*set;\s*\}",
                    RegexOptions.CultureInvariant).Count,
                $"Option {propertyName} must be one exact public opt-in JSON property.");
        }

        string[] declaredOptionPropertyNames = Regex.Matches(
                source,
                @"public\s+(?:bool|int|string)\s+([A-Za-z]\w*)\s*\{\s*get;\s*set;\s*\}",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            IntentionalOptionPropertyNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray(),
            declaredOptionPropertyNames,
            "The options type must expose exactly the approved persistent options properties.");
    }

    [TestMethod]
    public void LocalizationSource_WhenInspected_PreservesExactKleiLocalizationKeys()
    {
        string source = ReadRequiredSource(
            ResolveSourceRoot(),
            "DeliveryTemperatureLimitStrings.cs");

        StringAssert.Contains(source, "public static class DELIVERY_TEMPERATURE_LIMIT");
        StringAssert.Contains(source, "public static class SIDESCREEN");
        StringAssert.Contains(source, "public static class STATUS");
        StringAssert.Contains(source, "public static class VALIDATION");
        StringAssert.Contains(source, "public static class TOOLTIPS");
        StringAssert.Contains(source, "public static class OPTIONS");

        int totalLocStrings = Regex.Matches(
            source,
            @"public\s+static\s+LocString\s+[A-Za-z0-9_]+\s*=",
            RegexOptions.CultureInvariant).Count;
        Assert.AreEqual(74, totalLocStrings, "Total LocString declarations in source must equal exactly 74.");

        AssertClassContainsLocStrings(source, "SIDESCREEN", IntentionalSideScreenFieldNames);
        AssertClassContainsLocStrings(source, "STATUS", IntentionalStatusFieldNames);
        AssertClassContainsLocStrings(source, "VALIDATION", IntentionalValidationFieldNames);
        AssertClassContainsLocStrings(source, "TOOLTIPS", IntentionalTooltipsFieldNames);
        AssertClassContainsLocStrings(source, "OPTIONS", IntentionalOptionsFieldNames);

        Assert.IsFalse(
            Regex.IsMatch(source, @"\b(LABEL|RANGE_SEPARATOR|TOOLTIP_RANGE|TOOLTIP_NOTSET)\b"),
            "Dead legacy localization keys must not be retained.");
    }

    private static void AssertClassContainsLocStrings(string source, string className, string[] expectedFields)
    {
        int classIndex = source.IndexOf($"class {className}", StringComparison.Ordinal);
        Assert.IsTrue(classIndex >= 0, $"Class {className} must be declared.");
        int openBrace = source.IndexOf('{', classIndex);
        Assert.IsTrue(openBrace >= 0);
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
        Assert.IsTrue(closeBrace > openBrace);
        string classBody = source.Substring(openBrace, closeBrace - openBrace + 1);
        foreach (string field in expectedFields)
        {
            Assert.IsTrue(
                Regex.IsMatch(classBody, @"public\s+static\s+LocString\s+" + Regex.Escape(field) + @"\s*="),
                $"Class {className} must declare LocString {field}.");
        }
    }

    [TestMethod]
    public void LocalizationPotCatalog_WhenComparedWithSource_MatchesAllDeclaredLocStringsExactly()
    {
        string sourceRoot = ResolveSourceRoot();
        string potPath = Path.GetFullPath(
            Path.Combine(sourceRoot, "..", "translations", "delivery_temperature_limit.pot"));
        Assert.IsTrue(File.Exists(potPath), $"The POT catalog must exist at {potPath}.");

        string potContent = File.ReadAllText(potPath);
        string[] potContextKeys = Regex.Matches(
                potContent,
                @"^msgctxt\s+""([^""]+)""",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        List<string> expectedKeys = [];
        foreach (string field in IntentionalSideScreenFieldNames)
            expectedKeys.Add($"STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.{field}");
        foreach (string field in IntentionalStatusFieldNames)
            expectedKeys.Add($"STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.STATUS.{field}");
        foreach (string field in IntentionalValidationFieldNames)
            expectedKeys.Add($"STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.VALIDATION.{field}");
        foreach (string field in IntentionalTooltipsFieldNames)
            expectedKeys.Add($"STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TOOLTIPS.{field}");
        foreach (string field in IntentionalOptionsFieldNames)
            expectedKeys.Add($"STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.{field}");

        string[] sortedExpected = expectedKeys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEquivalent(
            sortedExpected,
            potContextKeys,
            "Every C# LocString must have an exact matching msgctxt in the POT template, with no orphaned keys.");
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
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT",
            []);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN",
            IntentionalSideScreenFieldNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+STATUS",
            IntentionalStatusFieldNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+VALIDATION",
            IntentionalValidationFieldNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+SIDESCREEN+TOOLTIPS",
            IntentionalTooltipsFieldNames);
        AssertPublicMemberNamesAreIntentional(
            publicSurface,
            "STRINGS.DELIVERY_TEMPERATURE_LIMIT+OPTIONS",
            IntentionalOptionsFieldNames);

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
