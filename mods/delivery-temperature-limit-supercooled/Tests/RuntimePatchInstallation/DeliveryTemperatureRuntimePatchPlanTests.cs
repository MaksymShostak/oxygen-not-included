using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

[TestClass]
public sealed class DeliveryTemperatureRuntimePatchPlanTests
{
    private static readonly Version SupportedFastTrackVersion =
        new(0, 18, 4, 0);
    private const string FixtureSha256 =
        "8B7914F7E50D0A53F96779A1D47E875585F6A45607959D4885D722067BE30C86";

    [TestMethod]
    public void Create_WhenFastTrackIsNotLoadedOrDisabledForLoadedGame_OrdersKleiInventoryPickupAndDirectGroups()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.ModNotLoaded,
            FastTrackFeatureCompatibilityState.ModNotLoaded,
            FastTrackFeatureCompatibilityState.ModNotLoaded,
            includeLoadedAssemblyIdentity: false);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .KleiWorldInventoryTemperaturePublication,
            DeliveryTemperatureRuntimePatchGroup
                .TemperatureStatusAvailability,
            DeliveryTemperatureRuntimePatchGroup
                .KleiPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility);
        Assert.IsNull(plan.StatusCompatibilityDiagnostic);
    }

    [TestMethod]
    public void Create_WhenFastTrackReplacementsAreInactive_OrdersKleiInventoryPickupAndDirectGroups()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.ReplacementInactive);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .KleiWorldInventoryTemperaturePublication,
            DeliveryTemperatureRuntimePatchGroup
                .TemperatureStatusAvailability,
            DeliveryTemperatureRuntimePatchGroup
                .KleiPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility);
    }

    [TestMethod]
    public void Create_WhenFastTrackWorldInventoryIsReady_OrdersFastTrackInventoryGroup()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.ReplacementInactive);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackWorldInventoryTemperaturePublication,
            DeliveryTemperatureRuntimePatchGroup
                .TemperatureStatusAvailability,
            DeliveryTemperatureRuntimePatchGroup
                .KleiPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility);
    }

    [TestMethod]
    public void Create_WhenFastTrackPickupGroupingIsReady_OrdersFastTrackPickupGroup()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.ReplacementInactive);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .KleiWorldInventoryTemperaturePublication,
            DeliveryTemperatureRuntimePatchGroup
                .TemperatureStatusAvailability,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility);
    }

    [TestMethod]
    public void Create_WhenFastTrackDirectDeliveryIsReady_OrdersFastTrackDirectGroup()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.Ready);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .KleiWorldInventoryTemperaturePublication,
            DeliveryTemperatureRuntimePatchGroup
                .TemperatureStatusAvailability,
            DeliveryTemperatureRuntimePatchGroup
                .KleiPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackDirectDeliveryEligibility);
    }

    [TestMethod]
    public void Create_WhenStatusOptionIsDisabled_SelectsNoInventoryOrStatusInstrumentation()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.Ready);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackDirectDeliveryEligibility);
        Assert.IsNull(plan.StatusCompatibilityDiagnostic);
    }

    [TestMethod]
    public void Create_WhenStatusOptionIsDisabledAndFastTrackWorldInventoryIsIncompatible_DoesNotBlockUnusedStatusFeature()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.Incompatible,
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.ReplacementInactive);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .KleiPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility);
        Assert.IsNull(plan.StatusCompatibilityDiagnostic);
    }

    [TestMethod]
    public void Create_WhenActivePickupFeatureIsIncompatible_ThrowsFastTrackDeliveryEligibilityCompatibilityException()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.Incompatible,
            FastTrackFeatureCompatibilityState.ReplacementInactive);

        FastTrackDeliveryEligibilityCompatibilityException exception =
            Assert.ThrowsExactly<
                FastTrackDeliveryEligibilityCompatibilityException>(() =>
                DeliveryTemperatureRuntimePatchPlan.Create(
                    checkTemperatureForStatusItems: true,
                    compatibility));

        AssertCompatibilityFailureDetails(
            exception.Message,
            FastTrackFeature.PickupGrouping,
            "The verified PickupGrouping structural anchor changed.");
        Assert.AreSame(compatibility, exception.CompatibilityReport);
    }

    [TestMethod]
    public void Create_WhenActiveDirectDeliveryFeatureIsIncompatible_ThrowsFastTrackDeliveryEligibilityCompatibilityException()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.ReplacementInactive,
            FastTrackFeatureCompatibilityState.Incompatible);

        FastTrackDeliveryEligibilityCompatibilityException exception =
            Assert.ThrowsExactly<
                FastTrackDeliveryEligibilityCompatibilityException>(() =>
                DeliveryTemperatureRuntimePatchPlan.Create(
                    checkTemperatureForStatusItems: true,
                    compatibility));

        AssertCompatibilityFailureDetails(
            exception.Message,
            FastTrackFeature.DirectDeliveryEligibility,
            "The verified DirectDeliveryEligibility structural anchor changed.");
        Assert.AreSame(compatibility, exception.CompatibilityReport);
    }

    [TestMethod]
    public void Create_WhenStatusIsEnabledAndWorldInventoryFeatureIsIncompatible_OmitsOnlyStatusIntegrationAndReturnsDiagnostic()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.Incompatible,
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.Ready);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackDirectDeliveryEligibility);
        Assert.IsNotNull(plan.StatusCompatibilityDiagnostic);
        AssertCompatibilityFailureDetails(
            plan.StatusCompatibilityDiagnostic,
            FastTrackFeature.WorldInventory,
            "The verified WorldInventory structural anchor changed.");
        StringAssert.Contains(
            plan.StatusCompatibilityDiagnostic,
            "existing ONI status availability remains unchanged");
    }

    [TestMethod]
    public void Create_WhenOnlyDirectDeliveryFeatureIsInactive_SelectsKleiDirectGroupWithoutChangingReadyFastTrackGroups()
    {
        FastTrackCompatibilityReport compatibility = CreateReport(
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.Ready,
            FastTrackFeatureCompatibilityState.ReplacementInactive);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                compatibility);

        AssertPatchGroups(
            plan,
            DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
            DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
            DeliveryTemperatureRuntimePatchGroup
                .KleiAuthoritativeFetchTemperatureEligibility,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackWorldInventoryTemperaturePublication,
            DeliveryTemperatureRuntimePatchGroup
                .TemperatureStatusAvailability,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackPickupTemperatureGrouping,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility);
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenSelectedKleiTargetHasOnlyNonSkippingObserver_ReturnsNormally()
    {
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                CreateReport(
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    includeLoadedAssemblyIdentity: false));
        MethodInfo worldInventoryUpdate = CreateEmittedMethod(
            "WorldInventory",
            "Update",
            typeof(void),
            Type.EmptyTypes);
        MethodInfo observingPrefix = CreateEmittedMethod(
            "UnrelatedInventoryObserver",
            "Prefix",
            typeof(void),
            Type.EmptyTypes);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPatchDescriptor(
                worldInventoryUpdate,
                observingPrefix,
                "Unrelated.InventoryObserver",
                priority: 400)
        ]);
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenSelectedKleiPickupTargetGainsSkippingPrefix_ThrowsAffectedAuthorityDiagnostic()
    {
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                CreateReport(
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    includeLoadedAssemblyIdentity: false));
        MethodInfo updatePickups = CreateEmittedMethod(
            "FetchManager+FetchablesByPrefabId",
            "UpdatePickups",
            typeof(void),
            [typeof(Navigator), typeof(int)]);
        MethodInfo skippingPrefix = CreateEmittedMethod(
            "UnexpectedPickupReplacement",
            "Prefix",
            typeof(bool),
            Type.EmptyTypes);

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPatchDescriptor(
                        updatePickups,
                        skippingPrefix,
                        "Unexpected.PickupReplacement",
                        priority: 800)
                ]));

        StringAssert.Contains(
            exception.Message,
            DeliveryTemperatureRuntimePatchGroup
                .KleiPickupTemperatureGrouping.ToString());
        StringAssert.Contains(exception.Message, "UpdatePickups");
        StringAssert.Contains(
            exception.Message,
            "Unexpected.PickupReplacement");
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenSelectedKleiDirectDeliveryTargetGainsSkippingPrefix_ThrowsAffectedAuthorityDiagnostic()
    {
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                CreateReport(
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    includeLoadedAssemblyIdentity: false));
        MethodInfo collectChores =
            CreateEmittedGlobalChoreCollectionMethod();
        MethodInfo skippingPrefix = CreateEmittedMethod(
            "UnexpectedDirectDeliveryReplacement",
            "Prefix",
            typeof(bool),
            Type.EmptyTypes);

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPatchDescriptor(
                        collectChores,
                        skippingPrefix,
                        "Unexpected.DirectDeliveryReplacement",
                        priority: 800)
                ]));

        StringAssert.Contains(
            exception.Message,
            DeliveryTemperatureRuntimePatchGroup
                .KleiDirectDeliveryEligibility.ToString());
        StringAssert.Contains(exception.Message, "CollectChores");
        StringAssert.Contains(
            exception.Message,
            "Unexpected.DirectDeliveryReplacement");
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenSelectedFastTrackPrefixIsUnchanged_ReturnsNormally()
    {
        MethodInfo verifiedFastTrackPrefix = CreateEmittedMethod(
            "FastTrack.GamePatches.FetchManagerFastUpdate",
            "BeforeUpdatePickups",
            typeof(bool),
            Type.EmptyTypes);
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                CreateReportWithReadyPickupReplacement(
                    verifiedFastTrackPrefix));
        MethodInfo updatePickups = CreateEmittedMethod(
            "FetchManager+FetchablesByPrefabId",
            "UpdatePickups",
            typeof(void),
            [typeof(Navigator), typeof(int)]);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPatchDescriptor(
                updatePickups,
                verifiedFastTrackPrefix,
                "PeterHan.FastTrack",
                priority: 800)
        ]);
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenSelectedFastTrackPrefixMethodChanges_ThrowsAffectedAuthorityDiagnostic()
    {
        MethodInfo verifiedFastTrackPrefix = CreateEmittedMethod(
            "FastTrack.GamePatches.FetchManagerFastUpdate",
            "BeforeUpdatePickups",
            typeof(bool),
            Type.EmptyTypes);
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                CreateReportWithReadyPickupReplacement(
                    verifiedFastTrackPrefix));
        MethodInfo changedFastTrackPrefix = CreateEmittedMethod(
            "FastTrack.GamePatches.FetchManagerFastUpdate",
            "ChangedBeforeUpdatePickups",
            typeof(bool),
            Type.EmptyTypes);
        MethodInfo updatePickups = CreateEmittedMethod(
            "FetchManager+FetchablesByPrefabId",
            "UpdatePickups",
            typeof(void),
            [typeof(Navigator), typeof(int)]);

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPatchDescriptor(
                        updatePickups,
                        changedFastTrackPrefix,
                        "PeterHan.FastTrack",
                        priority: 800)
                ]));

        StringAssert.Contains(
            exception.Message,
            DeliveryTemperatureRuntimePatchGroup
                .FastTrackPickupTemperatureGrouping.ToString());
        StringAssert.Contains(exception.Message, "BeforeUpdatePickups");
        StringAssert.Contains(exception.Message, "PeterHan.FastTrack");
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenUnselectedInventoryTargetChanges_DoesNotInspectThatOwner()
    {
        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                CreateReport(
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    FastTrackFeatureCompatibilityState.ModNotLoaded,
                    includeLoadedAssemblyIdentity: false));
        MethodInfo worldInventoryUpdate = CreateEmittedMethod(
            "WorldInventory",
            "Update",
            typeof(void),
            Type.EmptyTypes);
        MethodInfo skippingPrefix = CreateEmittedMethod(
            "UnselectedInventoryReplacement",
            "Prefix",
            typeof(bool),
            Type.EmptyTypes);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPatchDescriptor(
                worldInventoryUpdate,
                skippingPrefix,
                "Unselected.InventoryReplacement",
                priority: 800)
        ]);
    }

    private static void AssertPatchGroups(
        DeliveryTemperatureRuntimePatchPlan plan,
        params DeliveryTemperatureRuntimePatchGroup[] expectedGroups) =>
        Assert.AreSequenceEqual(expectedGroups, plan.OrderedPatchGroups);

    private static void AssertCompatibilityFailureDetails(
        string message,
        FastTrackFeature feature,
        string structuralFailure)
    {
        StringAssert.Contains(message, feature.ToString());
        StringAssert.Contains(message, "FastTrack, Version=0.18.4.0");
        StringAssert.Contains(message, "assembly version 0.18.0.0");
        StringAssert.Contains(message, "file version 0.18.4.0");
        StringAssert.Contains(message, FixtureSha256);
        StringAssert.Contains(message, structuralFailure);
        StringAssert.Contains(
            message,
            "FastTrack file version 0.18.4.0 support is best-efforts");
    }

    private static FastTrackCompatibilityReport CreateReport(
        FastTrackFeatureCompatibilityState worldInventoryState,
        FastTrackFeatureCompatibilityState pickupGroupingState,
        FastTrackFeatureCompatibilityState directDeliveryState,
        bool includeLoadedAssemblyIdentity = true) =>
        new(
            includeLoadedAssemblyIdentity
                ? "FastTrack, Version=0.18.4.0"
                : null,
            includeLoadedAssemblyIdentity
                ? new Version(0, 18, 0, 0)
                : null,
            includeLoadedAssemblyIdentity
                ? FastTrackAssemblyFileIdentityReadState.Success
                : FastTrackAssemblyFileIdentityReadState.NotRead,
            includeLoadedAssemblyIdentity ? SupportedFastTrackVersion : null,
            includeLoadedAssemblyIdentity ? FixtureSha256 : null,
            CreateFeatureCompatibility(
                FastTrackFeature.WorldInventory,
                worldInventoryState),
            CreateFeatureCompatibility(
                FastTrackFeature.PickupGrouping,
                pickupGroupingState),
            CreateFeatureCompatibility(
                FastTrackFeature.DirectDeliveryEligibility,
                directDeliveryState));

    private static FastTrackCompatibilityReport
        CreateReportWithReadyPickupReplacement(
            MethodInfo verifiedFastTrackPrefix) =>
        new(
            "FastTrack, Version=0.18.4.0",
            new Version(0, 18, 0, 0),
            FastTrackAssemblyFileIdentityReadState.Success,
            SupportedFastTrackVersion,
            FixtureSha256,
            FastTrackFeatureCompatibility.ReplacementInactive(
                FastTrackFeature.WorldInventory),
            FastTrackFeatureCompatibility.Ready(
                FastTrackFeature.PickupGrouping,
                new Dictionary<FastTrackVerifiedMember, MemberInfo>
                {
                    {
                        FastTrackVerifiedMember
                            .PickupGroupingBeforeUpdatePickupsPrefix,
                        verifiedFastTrackPrefix
                    }
                }),
            FastTrackFeatureCompatibility.ReplacementInactive(
                FastTrackFeature.DirectDeliveryEligibility));

    private static FastTrackFeatureCompatibility CreateFeatureCompatibility(
        FastTrackFeature feature,
        FastTrackFeatureCompatibilityState state) =>
        state switch
        {
            FastTrackFeatureCompatibilityState.ModNotLoaded =>
                FastTrackFeatureCompatibility.ModNotLoaded(feature),
            FastTrackFeatureCompatibilityState.ReplacementInactive =>
                FastTrackFeatureCompatibility.ReplacementInactive(feature),
            FastTrackFeatureCompatibilityState.Ready =>
                FastTrackFeatureCompatibility.Ready(
                    feature,
                    new Dictionary<FastTrackVerifiedMember, MemberInfo>
                    {
                        {
                            GetRepresentativeVerifiedMemberRole(feature),
                            typeof(DeliveryTemperatureRuntimePatchPlanTests)
                                .GetMethod(
                                    nameof(RepresentativeVerifiedMember),
                                    BindingFlags.Static |
                                    BindingFlags.NonPublic)!
                        }
                    }),
            FastTrackFeatureCompatibilityState.Incompatible =>
                FastTrackFeatureCompatibility.Incompatible(
                    feature,
                    GetFailureCode(feature),
                    "The verified " +
                    feature +
                    " structural anchor changed."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Unknown FastTrack compatibility state.")
        };

    private static FastTrackVerifiedMember GetRepresentativeVerifiedMemberRole(
        FastTrackFeature feature) =>
        feature switch
        {
            FastTrackFeature.WorldInventory =>
                FastTrackVerifiedMember.BackgroundWorldInventoryRunUpdate,
            FastTrackFeature.PickupGrouping =>
                FastTrackVerifiedMember.PickupGroupingAddItem,
            FastTrackFeature.DirectDeliveryEligibility =>
                FastTrackVerifiedMember.DirectDeliveryEligibilityComparator,
            _ => throw new ArgumentOutOfRangeException(
                nameof(feature),
                feature,
                "Unknown FastTrack feature.")
        };

    private static FastTrackFeatureCompatibilityFailureCode GetFailureCode(
        FastTrackFeature feature) =>
        feature switch
        {
            FastTrackFeature.WorldInventory =>
                FastTrackFeatureCompatibilityFailureCode
                    .WorldInventoryContractViolation,
            FastTrackFeature.PickupGrouping =>
                FastTrackFeatureCompatibilityFailureCode
                    .PickupGroupingContractViolation,
            FastTrackFeature.DirectDeliveryEligibility =>
                FastTrackFeatureCompatibilityFailureCode
                    .DirectDeliveryEligibilityContractViolation,
            _ => throw new ArgumentOutOfRangeException(
                nameof(feature),
                feature,
                "Unknown FastTrack feature.")
        };

    private static void RepresentativeVerifiedMember()
    {
    }

    private static MethodInfo CreateEmittedMethod(
        string declaringTypeName,
        string methodName,
        Type returnType,
        Type[] parameterTypes)
    {
        var assemblyName = new AssemblyName(
            "DeliveryTemperatureAuthorityFixture_" + Guid.NewGuid().ToString("N"));
        string emittedAssemblyName = assemblyName.Name ??
            throw new InvalidOperationException(
                "The authority-contract fixture assembly has no name.");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(
            emittedAssemblyName);
        int nestedTypeSeparatorIndex = declaringTypeName.IndexOf(
            '+',
            StringComparison.Ordinal);
        TypeBuilder? outerTypeBuilder = null;
        TypeBuilder typeBuilder;
        if (nestedTypeSeparatorIndex >= 0)
        {
            string outerTypeName = declaringTypeName.Substring(
                0,
                nestedTypeSeparatorIndex);
            string nestedTypeName = declaringTypeName.Substring(
                nestedTypeSeparatorIndex + 1);
            outerTypeBuilder = moduleBuilder.DefineType(
                outerTypeName,
                TypeAttributes.Public | TypeAttributes.Class);
            typeBuilder = outerTypeBuilder.DefineNestedType(
                nestedTypeName,
                TypeAttributes.NestedPublic |
                TypeAttributes.Sealed |
                TypeAttributes.Abstract);
        }
        else
        {
            typeBuilder = moduleBuilder.DefineType(
                declaringTypeName,
                TypeAttributes.Public |
                TypeAttributes.Sealed |
                TypeAttributes.Abstract);
        }

        MethodBuilder methodBuilder = typeBuilder.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static,
            returnType,
            parameterTypes);
        ILGenerator generator = methodBuilder.GetILGenerator();
        if (returnType == typeof(bool))
        {
            generator.Emit(OpCodes.Ldc_I4_1);
        }

        generator.Emit(OpCodes.Ret);
        Type? emittedType;
        if (outerTypeBuilder is null)
        {
            emittedType = typeBuilder.CreateType();
        }
        else
        {
            emittedType = typeBuilder.CreateType();
            _ = outerTypeBuilder.CreateType() ??
                throw new InvalidOperationException(
                    "The authority-contract fixture outer type was not emitted.");
        }

        if (emittedType is null)
        {
            throw new InvalidOperationException(
                "The authority-contract fixture type was not emitted.");
        }

        return emittedType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException(
                "The authority-contract fixture method was not emitted.");
    }

    private static MethodInfo CreateEmittedGlobalChoreCollectionMethod()
    {
        var assemblyName = new AssemblyName(
            "DeliveryTemperatureDirectAuthorityFixture_" +
            Guid.NewGuid().ToString("N"));
        string emittedAssemblyName = assemblyName.Name ??
            throw new InvalidOperationException(
                "The direct-authority fixture assembly has no name.");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(
            emittedAssemblyName);

        Type choreConsumerStateType = moduleBuilder.DefineType(
                "ChoreConsumerState",
                TypeAttributes.Public | TypeAttributes.Class)
            .CreateType() ??
            throw new InvalidOperationException(
                "The ChoreConsumerState contract type was not emitted.");
        TypeBuilder choreTypeBuilder = moduleBuilder.DefineType(
            "Chore",
            TypeAttributes.Public | TypeAttributes.Class);
        TypeBuilder preconditionTypeBuilder = choreTypeBuilder.DefineNestedType(
            "Precondition",
            TypeAttributes.NestedPublic | TypeAttributes.Class);
        TypeBuilder contextTypeBuilder =
            preconditionTypeBuilder.DefineNestedType(
                "Context",
                TypeAttributes.NestedPublic | TypeAttributes.Class);
        Type contextType = contextTypeBuilder.CreateType() ??
            throw new InvalidOperationException(
                "The Chore.Precondition.Context contract type was not emitted.");
        _ = preconditionTypeBuilder.CreateType() ??
            throw new InvalidOperationException(
                "The Chore.Precondition contract type was not emitted.");
        _ = choreTypeBuilder.CreateType() ??
            throw new InvalidOperationException(
                "The Chore contract type was not emitted.");

        TypeBuilder providerTypeBuilder = moduleBuilder.DefineType(
            "GlobalChoreProvider",
            TypeAttributes.Public | TypeAttributes.Class);
        MethodBuilder collectChoresMethod = providerTypeBuilder.DefineMethod(
            "CollectChores",
            MethodAttributes.Public,
            typeof(void),
            [
                choreConsumerStateType,
                typeof(List<>).MakeGenericType(contextType)
            ]);
        collectChoresMethod.GetILGenerator().Emit(OpCodes.Ret);
        Type providerType = providerTypeBuilder.CreateType() ??
            throw new InvalidOperationException(
                "The GlobalChoreProvider contract type was not emitted.");
        return providerType.GetMethod(
                "CollectChores",
                BindingFlags.Public | BindingFlags.Instance) ??
            throw new InvalidOperationException(
                "The GlobalChoreProvider.CollectChores contract method was not emitted.");
    }
}
