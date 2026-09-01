namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;

[TestClass]
public sealed class IntegrationIdentifierTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("fast Track")]
    [DataRow("Fast-track")]
    [DataRow("fast_track")]
    [DataRow("fast.track")]
    [DataRow("-fast-track")]
    [DataRow("fast-track-")]
    [DataRow("fast--track")]
    [DataRow("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm")]
    public void Constructors_WhenIdentifierIsNotLowercaseAsciiKebabCase_RejectValue(
        string value)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DeclaredModIntegrationId(value));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeCapabilityId(value));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimePatchGroupId(value));
    }

    [TestMethod]
    public void Constructors_WhenIdentifierIsNull_RejectValueWithExactParameterName()
    {
        var integrationException = Assert.ThrowsExactly<ArgumentNullException>(
            () => new DeclaredModIntegrationId(null!));
        var capabilityException = Assert.ThrowsExactly<ArgumentNullException>(
            () => new RuntimeCapabilityId(null!));
        var patchGroupException = Assert.ThrowsExactly<ArgumentNullException>(
            () => new RuntimePatchGroupId(null!));

        Assert.AreEqual("value", integrationException.ParamName);
        Assert.AreEqual("value", capabilityException.ParamName);
        Assert.AreEqual("value", patchGroupException.ParamName);
    }

    [TestMethod]
    public void Constructors_WhenIdentifierIsValid_PreserveExactValue()
    {
        var singleSegment = new DeclaredModIntegrationId("fasttrack");
        var multipleSegments =
            new RuntimeCapabilityId("pickup-temperature-grouping-2");
        var patchGroup =
            new RuntimePatchGroupId("fast-track-pickup-temperature-grouping");

        Assert.AreEqual("fasttrack", singleSegment.Value);
        Assert.AreEqual("fasttrack", singleSegment.ToString());
        Assert.AreEqual("pickup-temperature-grouping-2", multipleSegments.Value);
        Assert.AreEqual(
            "fast-track-pickup-temperature-grouping",
            patchGroup.Value);
    }

    [TestMethod]
    public void IdentityValues_WhenCompared_UseExactOrdinalValueAndType()
    {
        var integration = new DeclaredModIntegrationId("fast-track");
        var equalIntegration = new DeclaredModIntegrationId("fast-track");
        var differentIntegration =
            new DeclaredModIntegrationId("synthetic-runtime-authority");
        var capability = new RuntimeCapabilityId("fast-track");
        var equalCapability = new RuntimeCapabilityId("fast-track");
        var patchGroup = new RuntimePatchGroupId("fast-track");
        var equalPatchGroup = new RuntimePatchGroupId("fast-track");

        Assert.AreEqual(integration, equalIntegration);
        Assert.AreEqual(integration.GetHashCode(), equalIntegration.GetHashCode());
        Assert.AreNotEqual(integration, differentIntegration);
        Assert.IsFalse(integration.Equals((object)capability));

        Assert.AreEqual(capability, equalCapability);
        Assert.AreEqual(capability.GetHashCode(), equalCapability.GetHashCode());
        Assert.IsFalse(capability.Equals((object)patchGroup));

        Assert.AreEqual(patchGroup, equalPatchGroup);
        Assert.AreEqual(patchGroup.GetHashCode(), equalPatchGroup.GetHashCode());
        Assert.IsFalse(patchGroup.Equals((object)integration));
    }

    [TestMethod]
    public void RuntimeCapabilityIdentifiers_WhenInspected_UseStableSemanticKeys()
    {
        Assert.AreEqual(
            "world-inventory-temperature-publication",
            RuntimeCapabilityId.WorldInventoryTemperaturePublication.Value);
        Assert.AreEqual(
            "pickup-temperature-grouping",
            RuntimeCapabilityId.PickupTemperatureGrouping.Value);
        Assert.AreEqual(
            "direct-delivery-eligibility",
            RuntimeCapabilityId.DirectDeliveryEligibility.Value);
        Assert.AreEqual(
            "temperature-status-availability",
            RuntimeCapabilityId.TemperatureStatusAvailability.Value);
    }

    [TestMethod]
    public void IntegrationStateEnums_WhenInspected_ExposeOnlyApprovedDimensions()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "NotMatched",
                "Matched",
                "Ambiguous",
                "InspectionUnavailable"
            },
            Enum.GetNames<DeclaredModMatchState>());
        CollectionAssert.AreEqual(
            new[]
            {
                "DoesNotOwn",
                "OwnsCompatible",
                "OwnsIncompatible",
                "OwnershipUnavailable"
            },
            Enum.GetNames<RuntimeAuthorityObservation>());
        CollectionAssert.AreEqual(
            new[]
            {
                "NotEvaluated",
                "Compatible",
                "Incompatible",
                "VerificationUnavailable"
            },
            Enum.GetNames<IntegrationContractState>());
        CollectionAssert.AreEqual(
            new[]
            {
                "NotApplicable",
                "Selected",
                "Ready",
                "Unavailable",
                "ActivationBlocking"
            },
            Enum.GetNames<IntegrationCapabilityDisposition>());
        CollectionAssert.AreEqual(
            new[]
            {
                "ExclusiveRuntimeAuthority",
                "AdditiveInteroperability"
            },
            Enum.GetNames<ExternalModIntegrationCategory>());
    }
}
