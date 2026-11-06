using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackCompatibilityInspectorTests
{
    private static readonly Version SupportedFileVersion = new(0, 18, 4, 0);
    private const string SupportedDigest =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";
    private const string OtherAdmittedDigest =
        "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B";
    private const string UnknownDigest =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Test]
    public async Task Inspect_WhenFastTrackModIsNotLoaded_ClassifiesEveryFeatureAsModNotLoaded()
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

        await AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.ModNotLoaded);
        await Assert.That(report.AssemblyIdentity).IsNull();
        await Assert.That(report.AssemblyVersion).IsNull();
        await Assert.That(report.FileVersion).IsNull();
        await Assert.That(report.AssemblySha256).IsNull();
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Inspect_WhenAssemblyIsLoadedButWorldInventoryReplacementIsInactive_ClassifiesWorldInventoryAsReplacementInactive()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.PickupGroupingReplacement,
            fixture.DirectDeliveryEligibilityReplacement);

        await AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.Ready);
        await AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.Ready);
    }

    [Test]
    public async Task Inspect_WhenAssemblyIsLoadedButPickupPrefixIsNotActive_ClassifiesPickupGroupingAsReplacementInactive()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.WorldInventoryReplacement,
            fixture.DirectDeliveryEligibilityReplacement);

        await AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
    }

    [Test]
    public async Task Inspect_WhenFeaturesHaveDifferentActivationStates_ClassifiesEachIndependently()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.WorldInventoryReplacement);

        await AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.Ready);
        await AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
    }

    [Test]
    public async Task Inspect_WhenActiveBuildHasKnownVersionButDifferentDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
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

        await AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [Test]
    public async Task Inspect_WhenActiveBuildHasUnknownVersionButKnownDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
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

        await AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [Test]
    public async Task Inspect_WhenActiveBuildHasKnownVersionButUnknownDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
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

        await AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [Test]
    public async Task Inspect_WhenActiveBuildDigestUsesLowercase_ProceedsToStructuralVerification()
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

        await AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.Ready);
    }

    [Test]
    public async Task Inspect_WhenActiveBuildDigestIsMalformed_ClassifiesActiveFeaturesAsUnsupportedWithoutThrowing()
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

        await AssertEveryFeatureHasFailure(
            report,
            FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
    }

    [Test]
    public async Task Inspect_WhenUnsupportedBuildHasOneActiveReplacement_RejectsOnlyThatFeature()
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

        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureCode).IsEqualTo(FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
        await AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
    }

    [Test]
    public async Task Inspect_WhenUnsupportedBuildHasNoActiveReplacement_ClassifiesEveryFeatureAsReplacementInactive()
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

        await AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Inspect_WhenAssemblyBuildIsUnsupported_DiagnosticIncludesObservedPairWithoutCatalogEnumeration()
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
        await Assert.That(diagnostic).Contains("0.18.9.0");
        await Assert.That(diagnostic).Contains(UnknownDigest);
        await Assert.That(diagnostic.Contains(
            SupportedDigest,
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(diagnostic.Contains(
            OtherAdmittedDigest,
            StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Inspect_WhenAssemblyIsPresentButDisabledForLoadedGame_PerformsNoFeatureBinding()
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

        await AssertEveryFeatureHasState(
            report,
            FastTrackFeatureCompatibilityState.ModNotLoaded);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(0);
        await Assert.That(report.AssemblyIdentity).IsNull();
    }

    [Test]
    public async Task GetFeature_WhenFeatureValueIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        FastTrackCompatibilityReport report = Inspect(
            fixture,
            fixture.WorldInventoryReplacement);

        await Assert.That(() =>
            report.GetFeature((FastTrackFeature)int.MaxValue)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Inspect_WhenWorldInventoryRunUpdateSignatureChanges_ClassifiesOnlyWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateWithRunUpdateSignatureChanged();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
    }

    [Test]
    public async Task Inspect_WhenRunUpdateNoLongerHasCompleteAndSingleTagBranches_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateMissingSingleTagBranch();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("complete and single-resource-tag branches");
    }

    [Test]
    public async Task Inspect_WhenRunUpdateFirstUpdateBranchIsReversed_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateFirstUpdateBranchReversed();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("firstUpdate false branch");
    }

    [Test]
    public async Task Inspect_WhenRunUpdateCallsBothTotalsBeforeSingleTagBranch_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateTotalsInCompleteBranchOnly();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("opposite sides");
    }

    [Test]
    public async Task Inspect_WhenRunUpdateResourceTagPublicationAnchorIsMissing_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateResourceTagPublicationAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("resource-tag publication anchor");
    }

    [Test]
    public async Task Inspect_WhenRunUpdateResourceTagPublicationAnchorIsDuplicated_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateResourceTagPublicationAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("resource-tag publication anchor");
    }

    [Test]
    public async Task Inspect_WhenRunUpdateInventoryFieldAnchorIsMissing_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateInventoryFieldAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("WorldInventory.Inventory field anchor");
    }

    [Test]
    public async Task Inspect_WhenRunUpdateInventoryFieldAnchorIsDuplicated_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateInventoryFieldAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("WorldInventory.Inventory field anchor");
    }

    [Test]
    public async Task Inspect_WhenSumTotalFilteredContributionAnchorIsMissing_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithSumTotalFilteredContributionAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("filtered Pickupable.TotalAmount contribution anchor");
    }

    [Test]
    public async Task Inspect_WhenSumTotalFilteredContributionAnchorIsDuplicated_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithSumTotalFilteredContributionAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("filtered Pickupable.TotalAmount contribution anchor");
    }

    [Test]
    public async Task Inspect_WhenRemovedFetchableCanDeleteTagKey_ClassifiesWorldInventoryAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRemovedFetchableDeletingTagKey();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityFailureCode.WorldInventoryContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureMessage!).Contains("dictionary key");
    }

    [Test]
    public async Task Inspect_WhenPickupTagKeyEqualityUsesMoreThanAllocatedHash_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithPickupTagKeyEqualityUsingAllocatedIdentity();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.PickupGrouping).FailureMessage!).Contains("allocated hash");
    }

    [Test]
    public async Task Inspect_WhenAddItemConstructorAnchorIsMissing_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithAddItemConstructorAnchorMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.PickupGrouping).FailureMessage!).Contains("exactly one PickupTagKey constructor anchor");
    }

    [Test]
    public async Task Inspect_WhenAddItemConstructorAnchorIsDuplicated_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithAddItemConstructorAnchorDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
    }

    [Test]
    public async Task Inspect_WhenPickupTagKeyConstructorArgumentsAreReversed_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithPickupTagKeyConstructorArgumentsReversed();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
    }

    [Test]
    public async Task Inspect_WhenAddItemSignatureChanges_ClassifiesPickupGroupingAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithAddItemSignatureChanged();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityFailureCode.PickupGroupingContractViolation);
    }

    [Test]
    public async Task Inspect_WhenPickupGroupingContractIsReady_ProvidesConstructorAndPickupableIdentityFieldAnchors()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();

        FastTrackFeatureCompatibility compatibility = InspectAllActive(fixture)
            .GetFeature(FastTrackFeature.PickupGrouping);

        await Assert.That(compatibility.State).IsEqualTo(FastTrackFeatureCompatibilityState.Ready);
        await Assert.That(compatibility.GetVerifiedMember(
                FastTrackVerifiedMember.PickupGroupingKeyConstructor)).IsAssignableTo<ConstructorInfo>();
        FieldInfo pickupablePrefabIdentityField =
            (await Assert.That(compatibility.GetVerifiedMember(
                    FastTrackVerifiedMember
                        .PickupGroupingPickupablePrefabIdentityField)).IsAssignableTo<FieldInfo>())!;
        await Assert.That(pickupablePrefabIdentityField.DeclaringType!.FullName).IsEqualTo("Pickupable");
        await Assert.That(pickupablePrefabIdentityField.Name).IsEqualTo("KPrefabID");
    }

    [Test]
    public async Task Inspect_WhenHarmonyOwnerDoesNotMatchFastTrack_ClassifiesReplacementAsInactiveRatherThanClaimingReady()
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

        await AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.Ready);
        await AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.Ready);
    }

    [Test]
    public async Task Inspect_WhenDirectComparatorContractChanges_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorContractChanged();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityFailureCode.DirectDeliveryEligibilityContractViolation);
    }

    [Test]
    public async Task Inspect_WhenDirectComparatorHasNoUniqueSuccessReturn_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorSuccessReturnMissing();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityFailureCode.DirectDeliveryEligibilityContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.DirectDeliveryEligibility)
                .FailureMessage!).Contains("exactly one success return");
    }

    [Test]
    public async Task Inspect_WhenDirectComparatorHasTwoSuccessReturns_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorSuccessReturnDuplicated();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        await AssertOnlyFeatureIsIncompatible(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityFailureCode.DirectDeliveryEligibilityContractViolation);
        await Assert.That(report.GetFeature(FastTrackFeature.DirectDeliveryEligibility)
                .FailureMessage!).Contains("exactly one success return");
    }

    [Test]
    public async Task Inspect_WhenDirectDeliveryContractIsReady_ProvidesSortedClearablePickupableFieldAnchor()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();

        FastTrackFeatureCompatibility compatibility = InspectAllActive(fixture)
            .GetFeature(FastTrackFeature.DirectDeliveryEligibility);

        await Assert.That(compatibility.State).IsEqualTo(FastTrackFeatureCompatibilityState.Ready);
        FieldInfo pickupableField = (await Assert.That(compatibility.GetVerifiedMember(
                FastTrackVerifiedMember
                    .DirectDeliveryEligibilitySortedPickupableField)).IsAssignableTo<FieldInfo>())!;
        await Assert.That(pickupableField.DeclaringType!.FullName).IsEqualTo("ClearableManager+SortedClearable");
        await Assert.That(pickupableField.Name).IsEqualTo("pickupable");
    }

    [Test]
    public async Task Inspect_WhenActiveAssemblyIdentityCannotBeRead_PropagatesReaderStateOnlyToActiveFeatures()
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

        await AssertFeatureState(
            report,
            FastTrackFeature.WorldInventory,
            FastTrackFeatureCompatibilityState.Incompatible);
        await Assert.That(report.GetFeature(FastTrackFeature.WorldInventory).FailureCode).IsEqualTo(FastTrackFeatureCompatibilityFailureCode.AssemblyFileIdentityUnavailable);
        await AssertFeatureState(
            report,
            FastTrackFeature.PickupGrouping,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await AssertFeatureState(
            report,
            FastTrackFeature.DirectDeliveryEligibility,
            FastTrackFeatureCompatibilityState.ReplacementInactive);
        await Assert.That(report.AssemblyFileIdentityReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.LocationUnavailable);
    }

    [Test]
    public async Task Inspect_WhenEnabledAssemblyIsLoaded_ReadsPhysicalIdentityExactlyOnce()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());

        FastTrackCompatibilityReport report = Inspect(
            fixture,
            identityReader,
            fixture.AllReplacements.ToArray());

        await Assert.That(identityReader.ReadCallCount).IsEqualTo(1);
        await Assert.That(identityReader.LastAssembly).IsSameReferenceAs(fixture.Assembly);
        await Assert.That(report.AssemblyIdentity).IsEqualTo(fixture.Assembly.FullName);
        await Assert.That(report.AssemblyVersion).IsEqualTo(new Version(0, 18, 0, 0));
        await Assert.That(report.FileVersion).IsEqualTo(SupportedFileVersion);
        await Assert.That(report.AssemblySha256).IsEqualTo(SupportedDigest);
        await Assert.That(report.AssemblyFileIdentityReadState).IsEqualTo(FastTrackAssemblyFileIdentityReadState.Success);
    }

    [Test]
    public async Task Inspect_WhenFeatureIsReady_ExposesOnlyItsVerifiedReflectedMembersWithoutDiagnostics()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract();

        FastTrackCompatibilityReport report = InspectAllActive(fixture);

        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            FastTrackFeatureCompatibility compatibility =
                report.GetFeature(feature);
            await Assert.That(compatibility.State).IsEqualTo(FastTrackFeatureCompatibilityState.Ready);
            await Assert.That(compatibility.VerifiedMembers).IsNotEmpty();
            await Assert.That(compatibility.FailureCode).IsNull();
            await Assert.That(compatibility.FailureMessage).IsNull();
            foreach (KeyValuePair<FastTrackVerifiedMember, MemberInfo> verified in
                     compatibility.VerifiedMembers)
            {
                Assert.NotNull(verified.Value);
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

    private static async Task AssertEveryFeatureHasState(
        FastTrackCompatibilityReport report,
        FastTrackFeatureCompatibilityState expectedState)
    {
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            await AssertFeatureState(report, feature, expectedState);
        }
    }

    private static async Task AssertEveryFeatureHasFailure(
        FastTrackCompatibilityReport report,
        FastTrackFeatureCompatibilityFailureCode expectedFailureCode)
    {
        foreach (FastTrackFeature feature in EnumerateFeatures())
        {
            FastTrackFeatureCompatibility compatibility =
                report.GetFeature(feature);
            await Assert.That(compatibility.State).IsEqualTo(FastTrackFeatureCompatibilityState.Incompatible);
            await Assert.That(compatibility.FailureCode).IsEqualTo(expectedFailureCode);
            Assert.NotNull(compatibility.FailureMessage);
        }
    }

    private static async Task AssertOnlyFeatureIsIncompatible(
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
                await Assert.That(compatibility.State).IsEqualTo(FastTrackFeatureCompatibilityState.Incompatible);
                await Assert.That(compatibility.FailureCode).IsEqualTo(expectedFailureCode);
                Assert.NotNull(compatibility.FailureMessage);
            }
            else
            {
                await Assert.That(compatibility.State).IsEqualTo(FastTrackFeatureCompatibilityState.Ready).Because($"Mutation for {incompatibleFeature} changed {feature}.");
            }
        }
    }

    private static async Task AssertFeatureState(
        FastTrackCompatibilityReport report,
        FastTrackFeature feature,
        FastTrackFeatureCompatibilityState expectedState)
    {
        FastTrackFeatureCompatibility compatibility = report.GetFeature(feature);
        await Assert.That(compatibility.Feature).IsEqualTo(feature);
        await Assert.That(compatibility.State).IsEqualTo(expectedState);
        if (expectedState == FastTrackFeatureCompatibilityState.Incompatible)
        {
            Assert.NotNull(compatibility.FailureCode);
            Assert.NotNull(compatibility.FailureMessage);
        }
        else
        {
            await Assert.That(compatibility.FailureCode).IsNull();
            await Assert.That(compatibility.FailureMessage).IsNull();
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
