using System.Reflection;

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
}
