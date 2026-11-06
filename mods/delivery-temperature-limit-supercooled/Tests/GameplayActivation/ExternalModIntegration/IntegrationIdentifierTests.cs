namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;
public sealed class IntegrationIdentifierTests
{
    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("fast Track")]
    [Arguments("Fast-track")]
    [Arguments("fast_track")]
    [Arguments("fast.track")]
    [Arguments("-fast-track")]
    [Arguments("fast-track-")]
    [Arguments("fast--track")]
    [Arguments("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm")]
    public async Task Constructors_WhenIdentifierIsNotLowercaseAsciiKebabCase_RejectValue(
        string value)
    {
        await Assert.That(() =>
            new DeclaredModIntegrationId(value)).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new RuntimeCapabilityId(value)).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new RuntimePatchGroupId(value)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Constructors_WhenIdentifierIsNull_RejectValueWithExactParameterName()
    {
        var integrationException = (await Assert.That(() => new DeclaredModIntegrationId(null!)).ThrowsExactly<ArgumentNullException>())!;
        var capabilityException = (await Assert.That(() => new RuntimeCapabilityId(null!)).ThrowsExactly<ArgumentNullException>())!;
        var patchGroupException = (await Assert.That(() => new RuntimePatchGroupId(null!)).ThrowsExactly<ArgumentNullException>())!;

        await Assert.That(integrationException.ParamName).IsEqualTo("value");
        await Assert.That(capabilityException.ParamName).IsEqualTo("value");
        await Assert.That(patchGroupException.ParamName).IsEqualTo("value");
    }

    [Test]
    public async Task Constructors_WhenIdentifierIsValid_PreserveExactValue()
    {
        var singleSegment = new DeclaredModIntegrationId("fasttrack");
        var multipleSegments =
            new RuntimeCapabilityId("pickup-temperature-grouping-2");
        var patchGroup =
            new RuntimePatchGroupId("fast-track-pickup-temperature-grouping");

        await Assert.That(singleSegment.Value).IsEqualTo("fasttrack");
        await Assert.That(singleSegment.ToString()).IsEqualTo("fasttrack");
        await Assert.That(multipleSegments.Value).IsEqualTo("pickup-temperature-grouping-2");
        await Assert.That(patchGroup.Value).IsEqualTo("fast-track-pickup-temperature-grouping");
    }

    [Test]
    public async Task IdentityValues_WhenCompared_UseExactOrdinalValueAndType()
    {
        var integration = new DeclaredModIntegrationId("fast-track");
        var equalIntegration = new DeclaredModIntegrationId("fast-track");
        var differentIntegration =
            new DeclaredModIntegrationId("synthetic-runtime-authority");
        var capability = new RuntimeCapabilityId("fast-track");
        var equalCapability = new RuntimeCapabilityId("fast-track");
        var patchGroup = new RuntimePatchGroupId("fast-track");
        var equalPatchGroup = new RuntimePatchGroupId("fast-track");

        await Assert.That(equalIntegration).IsEqualTo(integration);
        await Assert.That(equalIntegration.GetHashCode()).IsEqualTo(integration.GetHashCode());
        await Assert.That(differentIntegration).IsNotEqualTo(integration);
        await Assert.That(integration.Equals((object)capability)).IsFalse();

        await Assert.That(equalCapability).IsEqualTo(capability);
        await Assert.That(equalCapability.GetHashCode()).IsEqualTo(capability.GetHashCode());
        await Assert.That(capability.Equals((object)patchGroup)).IsFalse();

        await Assert.That(equalPatchGroup).IsEqualTo(patchGroup);
        await Assert.That(equalPatchGroup.GetHashCode()).IsEqualTo(patchGroup.GetHashCode());
        await Assert.That(patchGroup.Equals((object)integration)).IsFalse();
    }

    [Test]
    public async Task RuntimeCapabilityIdentifiers_WhenInspected_UseStableSemanticKeys()
    {
        await Assert.That(RuntimeCapabilityId.WorldInventoryTemperaturePublication.Value).IsEqualTo("world-inventory-temperature-publication");
        await Assert.That(RuntimeCapabilityId.PickupTemperatureGrouping.Value).IsEqualTo("pickup-temperature-grouping");
        await Assert.That(RuntimeCapabilityId.DirectDeliveryEligibility.Value).IsEqualTo("direct-delivery-eligibility");
        await Assert.That(RuntimeCapabilityId.TemperatureStatusAvailability.Value).IsEqualTo("temperature-status-availability");
    }

    [Test]
    public async Task IntegrationStateEnums_WhenInspected_ExposeOnlyApprovedDimensions()
    {
        await Assert.That(Enum.GetNames<DeclaredModMatchState>()).IsEquivalentTo(new[]
            {
                "NotMatched",
                "Matched",
                "Ambiguous",
                "InspectionUnavailable"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Enum.GetNames<RuntimeAuthorityObservation>()).IsEquivalentTo(new[]
            {
                "DoesNotOwn",
                "OwnsCompatible",
                "OwnsIncompatible",
                "OwnershipUnavailable"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Enum.GetNames<IntegrationContractState>()).IsEquivalentTo(new[]
            {
                "NotEvaluated",
                "Compatible",
                "Incompatible",
                "VerificationUnavailable"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Enum.GetNames<IntegrationCapabilityDisposition>()).IsEquivalentTo(new[]
            {
                "NotApplicable",
                "Selected",
                "Ready",
                "Unavailable",
                "ActivationBlocking"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Enum.GetNames<ExternalModIntegrationCategory>()).IsEquivalentTo(new[]
            {
                "ExclusiveRuntimeAuthority",
                "AdditiveInteroperability"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
