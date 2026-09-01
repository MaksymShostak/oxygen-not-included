using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackCompatibilityInspectorTests
{
    private static readonly Version SupportedFileVersion = new(0, 18, 4, 0);
    private const string SupportedDigest =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";
    private const string OtherAdmittedDigest =
        "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B";
    private const string UnknownDigest =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [TestMethod]
    public void Inspect_WhenFastTrackModIsNotLoaded_ClassifiesEveryFeatureAsModNotLoaded()
    {
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());
        var inspector = new FastTrackCompatibilityInspector(
            identityReader,
            CreateSupportedTestCatalog());
        var inspectionInput = new FastTrackLoadedGameInspectionInput(
            isFastTrackEnabledForLoadedGame: false,
            fastTrackAssembly: null,
            Array.Empty<ActiveHarmonyPrefixDescriptor>());

        FastTrackCompatibilityReport report = inspector.Inspect(inspectionInput);

        AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.ModNotLoaded);
        Assert.IsNull(report.AssemblyIdentity);
        Assert.IsNull(report.AssemblyVersion);
        Assert.IsNull(report.FileVersion);
        Assert.IsNull(report.AssemblySha256);
        Assert.AreEqual(0, identityReader.ReadCallCount);
    }

    [TestMethod]
    public void Inspect_WhenAssemblyIsLoadedButWorldInventoryReplacementIsInactive_ClassifiesWorldInventoryAsReplacementInactive()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.PickupGroupingReplacement,
            fixture.DirectDeliveryEligibilityReplacement);

        AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.Ready);
        AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.Ready);
    }

    [TestMethod]
    public void Inspect_WhenAssemblyIsLoadedButPickupPrefixIsNotActive_ClassifiesPickupGroupingAsReplacementInactive()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.WorldInventoryReplacement,
            fixture.DirectDeliveryEligibilityReplacement);

        AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
    }

    [TestMethod]
    public void Inspect_WhenFeaturesHaveDifferentActivationStates_ClassifiesEachIndependently()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.WorldInventoryReplacement);

        AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.Ready);
        AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
    }

    [TestMethod]
    public void Inspect_WhenActiveBuildHasKnownVersionButDifferentDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var supportedIdentity = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            SupportedDigest);
        var otherSupportedIdentity = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 5, 0),
            OtherAdmittedDigest);
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(
                new Version(0, 18, 4, 0),
                OtherAdmittedDigest));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            new FastTrackSupportedAssemblyBuildCatalog(new[]
            {
                supportedIdentity,
                otherSupportedIdentity
            }),
            fixture.WorldInventoryReplacement,
            fixture.PickupGroupingReplacement,
            fixture.DirectDeliveryEligibilityReplacement);

        AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [TestMethod]
    public void Inspect_WhenActiveBuildHasUnknownVersionButKnownDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(
                new Version(0, 18, 6, 0),
                SupportedDigest));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            fixture.AllReplacements.ToArray());

        AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [TestMethod]
    public void Inspect_WhenActiveBuildHasKnownVersionButUnknownDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(SupportedFileVersion, UnknownDigest));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            fixture.AllReplacements.ToArray());

        AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [TestMethod]
    public void Inspect_WhenActiveBuildDigestUsesLowercase_ProceedsToStructuralVerification()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(
                SupportedFileVersion,
                SupportedDigest.ToLowerInvariant()));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            fixture.AllReplacements.ToArray());

        AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.Ready);
    }

    [TestMethod]
    public void Inspect_WhenActiveBuildDigestIsMalformed_ClassifiesActiveFeaturesAsUnsupportedWithoutThrowing()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(
                SupportedFileVersion,
                "not-a-complete-sha256"));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            fixture.AllReplacements.ToArray());

        AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [TestMethod]
    public void Inspect_WhenUnsupportedBuildHasOneActiveReplacement_RejectsOnlyThatFeature()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(SupportedFileVersion, UnknownDigest));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            fixture.WorldInventoryReplacement);

        Assert.AreEqual(
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild,
            report.GetFeature(FastTrackFeature.WorldInventory).FailureCode);
        AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
    }

    [TestMethod]
    public void Inspect_WhenUnsupportedBuildHasNoActiveReplacement_ClassifiesEveryFeatureAsReplacementInactive()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(SupportedFileVersion, UnknownDigest));

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            Array.Empty<ActiveHarmonyPrefixDescriptor>());

        AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        Assert.AreEqual(1, identityReader.ReadCallCount);
    }

    [TestMethod]
    public void Inspect_WhenAssemblyBuildIsUnsupported_DiagnosticIncludesObservedPairWithoutCatalogEnumeration()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(
                new Version(0, 18, 9, 0),
                UnknownDigest));
        var catalog = new FastTrackSupportedAssemblyBuildCatalog(new[]
        {
            new FastTrackAssemblyBuildIdentity(
                SupportedFileVersion,
                SupportedDigest),
            new FastTrackAssemblyBuildIdentity(
                new Version(0, 18, 5, 0),
                OtherAdmittedDigest)
        });

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            catalog,
            fixture.WorldInventoryReplacement);

        string diagnostic = report
            .GetFeature(FastTrackFeature.WorldInventory)
            .FailureMessage!;
        StringAssert.Contains(diagnostic, "0.18.9.0");
        StringAssert.Contains(diagnostic, UnknownDigest);
        Assert.IsFalse(diagnostic.Contains(
            SupportedDigest,
            StringComparison.Ordinal));
        Assert.IsFalse(diagnostic.Contains(
            OtherAdmittedDigest,
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Inspect_WhenAssemblyIsPresentButDisabledForLoadedGame_PerformsNoFeatureBinding()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());
        var inspector = new FastTrackCompatibilityInspector(
            identityReader,
            CreateSupportedTestCatalog());
        var input = new FastTrackLoadedGameInspectionInput(
            isFastTrackEnabledForLoadedGame: false,
            fixture.Assembly,
            fixture.AllReplacements);

        FastTrackCompatibilityReport report = inspector.Inspect(input);

        AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.ModNotLoaded);
        Assert.AreEqual(0, identityReader.ReadCallCount);
        Assert.IsNull(report.AssemblyIdentity);
    }

    [TestMethod]
    public void GetFeature_WhenFeatureValueIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.WorldInventoryReplacement);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            report.GetFeature((FastTrackFeature)int.MaxValue));
    }

    [TestMethod]
    public void Inspect_WhenWorldInventoryRunUpdateSignatureChanges_ClassifiesOnlyWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateWithRunUpdateSignatureChanged();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateNoLongerHasCompleteAndSingleTagBranches_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateMissingSingleTagBranch();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "complete and single-resource-tag branches");
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateFirstUpdateBranchIsReversed_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateFirstUpdateBranchReversed();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "firstUpdate false branch");
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateCallsBothTotalsBeforeSingleTagBranch_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateTotalsInCompleteBranchOnly();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "opposite sides");
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateResourceTagPublicationAnchorIsMissing_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateResourceTagPublicationAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "resource-tag publication anchor");
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateResourceTagPublicationAnchorIsDuplicated_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateResourceTagPublicationAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "resource-tag publication anchor");
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateInventoryFieldAnchorIsMissing_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateInventoryFieldAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "WorldInventory.Inventory field anchor");
    }

    [TestMethod]
    public void Inspect_WhenRunUpdateInventoryFieldAnchorIsDuplicated_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateInventoryFieldAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "WorldInventory.Inventory field anchor");
    }

    [TestMethod]
    public void Inspect_WhenSumTotalFilteredContributionAnchorIsMissing_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithSumTotalFilteredContributionAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "filtered Pickupable.TotalAmount contribution anchor");
    }

    [TestMethod]
    public void Inspect_WhenSumTotalFilteredContributionAnchorIsDuplicated_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithSumTotalFilteredContributionAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "filtered Pickupable.TotalAmount contribution anchor");
    }

    [TestMethod]
    public void Inspect_WhenRemovedFetchableCanDeleteTagKey_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRemovedFetchableDeletingTagKey();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!,
            "dictionary key");
    }

    [TestMethod]
    public void Inspect_WhenPickupTagKeyEqualityUsesMoreThanAllocatedHash_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithPickupTagKeyEqualityUsingAllocatedIdentity();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.PickupGrouping).FailureMessage!,
            "allocated hash");
    }

    [TestMethod]
    public void Inspect_WhenAddItemConstructorAnchorIsMissing_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithAddItemConstructorAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.PickupGrouping).FailureMessage!,
            "exactly one PickupTagKey constructor anchor");
    }

    [TestMethod]
    public void Inspect_WhenAddItemConstructorAnchorIsDuplicated_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithAddItemConstructorAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
    }

    [TestMethod]
    public void Inspect_WhenPickupTagKeyConstructorArgumentsAreReversed_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithPickupTagKeyConstructorArgumentsReversed();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
    }

    [TestMethod]
    public void Inspect_WhenAddItemSignatureChanges_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithAddItemSignatureChanged();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
    }

    [TestMethod]
    public void Inspect_WhenPickupGroupingContractIsReady_ProvidesConstructorAndPickupableIdentityFieldAnchors()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();

        FastTrackFeatureCompatibility compatibility = InspectAllActive(fixture)
            .GetFeature(FastTrackFeature.PickupGrouping);

        Assert.AreEqual(
            FastTrackFeatureCompatibilityState.Ready,
            compatibility.State);
        Assert.IsInstanceOfType<ConstructorInfo>(
            compatibility.GetVerifiedMember(
                FastTrackVerifiedMember.PickupGroupingKeyConstructor));
        FieldInfo pickupablePrefabIdentityField =
            Assert.IsInstanceOfType<FieldInfo>(
                compatibility.GetVerifiedMember(
                    FastTrackVerifiedMember
                        .PickupGroupingPickupablePrefabIdentityField));
        Assert.AreEqual("Pickupable", pickupablePrefabIdentityField.DeclaringType!.FullName);
        Assert.AreEqual("KPrefabID", pickupablePrefabIdentityField.Name);
    }

    [TestMethod]
    public void Inspect_WhenHarmonyOwnerDoesNotMatchFastTrack_ClassifiesReplacementAsInactiveRatherThanClaimingReady()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        ActiveHarmonyPrefixDescriptor unexpectedOwnerDescriptor =
            fixture.WithHarmonyOwner(
                fixture.WorldInventoryReplacement,
                "another.mod.owner");

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            unexpectedOwnerDescriptor,
            fixture.PickupGroupingReplacement,
            fixture.DirectDeliveryEligibilityReplacement);

        AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.Ready);
        AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.Ready);
    }

    [TestMethod]
    public void Inspect_WhenDirectComparatorContractChanges_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorContractChanged();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityFailureCode.DirectDeliveryEligibilityContractViolation);
    }

    [TestMethod]
    public void Inspect_WhenDirectComparatorHasNoUniqueSuccessReturn_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorSuccessReturnMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityFailureCode.DirectDeliveryEligibilityContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.DirectDeliveryEligibility)
                .FailureMessage!,
            "exactly one success return");
    }

    [TestMethod]
    public void Inspect_WhenDirectComparatorHasTwoSuccessReturns_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorSuccessReturnDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityFailureCode.DirectDeliveryEligibilityContractViolation);
        StringAssert.Contains(
            report.GetFeature(FastTrackFeature.DirectDeliveryEligibility)
                .FailureMessage!,
            "exactly one success return");
    }

    [TestMethod]
    public void Inspect_WhenDirectDeliveryContractIsReady_ProvidesSortedClearablePickupableFieldAnchor()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();

        FastTrackFeatureCompatibility compatibility = InspectAllActive(fixture)
            .GetFeature(FastTrackFeature.DirectDeliveryEligibility);

        Assert.AreEqual(
            FastTrackFeatureCompatibilityState.Ready,
            compatibility.State);
        FieldInfo pickupableField = Assert.IsInstanceOfType<FieldInfo>(
            compatibility.GetVerifiedMember(
                FastTrackVerifiedMember
                    .DirectDeliveryEligibilitySortedPickupableField));
        Assert.AreEqual(
            "ClearableManager+SortedClearable",
            pickupableField.DeclaringType!.FullName);
        Assert.AreEqual("pickupable", pickupableField.Name);
    }

    [TestMethod]
    public void Inspect_WhenActiveAssemblyIdentityCannotBeRead_PropagatesReaderStateOnlyToActiveFeatures()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var failedIdentity = new FastTrackAssemblyFileIdentity(
            FastTrackAssemblyFileIdentityReadState.LocationUnavailable,
            fileVersion: null,
            assemblySha256: null,
            failureMessage: "The loaded assembly has no physical location.");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            failedIdentity);

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            fixture.WorldInventoryReplacement);

        AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.Incompatible);
        Assert.AreEqual(
            FastTrackFeatureCompatibilityFailureCode.AssemblyFileIdentityUnavailable,
            report.GetFeature(FastTrackFeature.WorldInventory).FailureCode);
        AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        Assert.AreEqual(
            FastTrackAssemblyFileIdentityReadState.LocationUnavailable,
            report.AssemblyFileIdentityReadState);
    }

    [TestMethod]
    public void Inspect_WhenEnabledAssemblyIsLoaded_ReadsPhysicalIdentityExactlyOnce()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            fixture.AllReplacements.ToArray());

        Assert.AreEqual(1, identityReader.ReadCallCount);
        Assert.AreSame(fixture.Assembly, identityReader.LastAssembly);
        Assert.AreEqual(fixture.Assembly.FullName, report.AssemblyIdentity);
        Assert.AreEqual(new Version(0, 18, 0, 0), report.AssemblyVersion);
        Assert.AreEqual(SupportedFileVersion, report.FileVersion);
        Assert.AreEqual(
            SupportedDigest,
            report.AssemblySha256);
        Assert.AreEqual(
            FastTrackAssemblyFileIdentityReadState.Success,
            report.AssemblyFileIdentityReadState);
    }

    [TestMethod]
    public void Inspect_WhenFeatureIsReady_ExposesOnlyItsVerifiedReflectedMembersWithoutDiagnostics()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            FastTrackFeatureCompatibility compatibility =
                report.GetFeature(feature);
            Assert.AreEqual(
                FastTrackFeatureCompatibilityState.Ready,
                compatibility.State);
            Assert.IsNotEmpty(compatibility.VerifiedMembers);
            Assert.IsNull(compatibility.FailureCode);
            Assert.IsNull(compatibility.FailureMessage);
            foreach (KeyValuePair<FastTrackVerifiedMember, MemberInfo> verified in
                     compatibility.VerifiedMembers)
            {
                Assert.IsNotNull(verified.Value);
            }
        }
    }

    private static FastTrackCompatibilityReport InspectAllActive(
        FastTrackEmittedAssembly fixture) =>
        Inspect(fixture, fixture.AllReplacements.ToArray());

    private static FastTrackCompatibilityReport Inspect(
        FastTrackEmittedAssembly fixture,
        params ActiveHarmonyPrefixDescriptor[] activePatches) =>
        Inspect(
            fixture,
            new RecordingAssemblyFileIdentityReader(SuccessfulFileIdentity()),
            activePatches);

    private static FastTrackCompatibilityReport Inspect(
        FastTrackEmittedAssembly fixture,
        RecordingAssemblyFileIdentityReader identityReader,
        params ActiveHarmonyPrefixDescriptor[] activePatches)
        => Inspect(
            fixture,
            identityReader,
            CreateSupportedTestCatalog(),
            activePatches);

    private static FastTrackCompatibilityReport Inspect(
        FastTrackEmittedAssembly fixture,
        RecordingAssemblyFileIdentityReader identityReader,
        FastTrackSupportedAssemblyBuildCatalog supportedAssemblyBuildCatalog,
        params ActiveHarmonyPrefixDescriptor[] activePatches)
    {
        var inspector = new FastTrackCompatibilityInspector(
            identityReader,
            supportedAssemblyBuildCatalog);
        var input = new FastTrackLoadedGameInspectionInput(
            isFastTrackEnabledForLoadedGame: true,
            fixture.Assembly,
            activePatches);

        return inspector.Inspect(input);
    }

    private static FastTrackAssemblyFileIdentity SuccessfulFileIdentity(
        Version? fileVersion = null,
        string? assemblySha256 = null) =>
        new(
            FastTrackAssemblyFileIdentityReadState.Success,
            fileVersion ?? SupportedFileVersion,
            assemblySha256 ?? SupportedDigest,
            failureMessage: null);

    private static FastTrackSupportedAssemblyBuildCatalog
        CreateSupportedTestCatalog() =>
        new FastTrackSupportedAssemblyBuildCatalog(new[]
        {
            new FastTrackAssemblyBuildIdentity(
                SupportedFileVersion,
                SupportedDigest)
        });

    private static void AssertEveryFeatureHasState(
        FastTrackCompatibilityReport report,
        FastTrackFeatureCompatibilityState expectedState)
    {
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            AssertFeatureState(report, feature, expectedState);
        }
    }

    private static void AssertEveryFeatureHasFailure(
        FastTrackCompatibilityReport report,
        FastTrackFeatureCompatibilityFailureCode expectedFailureCode)
    {
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            FastTrackFeatureCompatibility compatibility =
                report.GetFeature(feature);
            Assert.AreEqual(
                FastTrackFeatureCompatibilityState.Incompatible,
                compatibility.State);
            Assert.AreEqual(expectedFailureCode, compatibility.FailureCode);
            Assert.IsNotNull(compatibility.FailureMessage);
        }
    }

    private static void AssertOnlyFeatureIsIncompatible(
        FastTrackCompatibilityReport report,
        FastTrackFeature incompatibleFeature,
        FastTrackFeatureCompatibilityFailureCode expectedFailureCode)
    {
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            FastTrackFeatureCompatibility compatibility =
                report.GetFeature(feature);
            if (feature == incompatibleFeature)
            {
                Assert.AreEqual(
                    FastTrackFeatureCompatibilityState.Incompatible,
                    compatibility.State);
                Assert.AreEqual(expectedFailureCode, compatibility.FailureCode);
                Assert.IsNotNull(compatibility.FailureMessage);
            }
            else
            {
                Assert.AreEqual(
                    FastTrackFeatureCompatibilityState.Ready,
                    compatibility.State,
                    $"Mutation for {incompatibleFeature} changed {feature}.");
            }
        }
    }

    private static void AssertFeatureState(
        FastTrackCompatibilityReport report,
        FastTrackFeature feature,
        FastTrackFeatureCompatibilityState expectedState)
    {
        FastTrackFeatureCompatibility compatibility = report.GetFeature(feature);
        Assert.AreEqual(feature, compatibility.Feature);
        Assert.AreEqual(expectedState, compatibility.State);
        if (expectedState == FastTrackFeatureCompatibilityState.Incompatible)
        {
            Assert.IsNotNull(compatibility.FailureCode);
            Assert.IsNotNull(compatibility.FailureMessage);
        }
        else
        {
            Assert.IsNull(compatibility.FailureCode);
            Assert.IsNull(compatibility.FailureMessage);
        }
    }

    private static IEnumerable<FastTrackFeature> EnumerateFeatures()
    {
        yield return FastTrackFeature.WorldInventory;
        yield return FastTrackFeature.PickupGrouping;
        yield return FastTrackFeature.DirectDeliveryEligibility;
    }

    private sealed class RecordingAssemblyFileIdentityReader :
        IFastTrackAssemblyFileIdentityReader
    {
        private readonly FastTrackAssemblyFileIdentity result;

        internal RecordingAssemblyFileIdentityReader(
            FastTrackAssemblyFileIdentity result)
        {
            this.result = result;
        }

        internal int ReadCallCount { get; private set; }

        internal Assembly? LastAssembly { get; private set; }

        public FastTrackAssemblyFileIdentity Read(Assembly fastTrackAssembly)
        {
            ReadCallCount++;
            LastAssembly = fastTrackAssembly;
            return result;
        }
    }
}
