namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;
public sealed class DeclaredModIntegrationCatalogTests
{
    [Test]
    public async Task Constructor_WhenDeclarationsAreValid_PreservesInsertionOrderAndCopiesInput()
    {
        DeclaredModIntegrationDescriptor fastTrack = Descriptor(
            "fast-track",
            "Fast Track",
            "PeterHan.FastTrack",
            "FastTrack",
            RuntimeCapabilityId.DirectDeliveryEligibility);
        DeclaredModIntegrationDescriptor synthetic = Descriptor(
            "synthetic-runtime-authority",
            "Synthetic Runtime Authority",
            "Example.SyntheticRuntimeAuthority",
            "SyntheticRuntimeAuthority",
            RuntimeCapabilityId.PickupTemperatureGrouping);
        var source = new List<DeclaredModIntegrationDescriptor>
        {
            fastTrack,
            synthetic
        };

        var catalog = new DeclaredModIntegrationCatalog(source);
        source.Clear();

        await Assert.That(catalog.Descriptors.ToArray()).IsEquivalentTo(new[] { fastTrack, synthetic }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(catalog.GetRequired(new DeclaredModIntegrationId("fast-track"))).IsSameReferenceAs(fastTrack);
        await Assert.That(catalog.TryGet(
            new DeclaredModIntegrationId("unknown-integration"),
            out _)).IsFalse();
    }

    [Test]
    public async Task Constructor_WhenIntegrationIdentityRepeats_RejectsCatalog()
    {
        await Assert.That(() =>
            new DeclaredModIntegrationCatalog(new[]
            {
                Descriptor(
                    "fast-track",
                    "Fast Track",
                    "PeterHan.FastTrack",
                    "FastTrack",
                    RuntimeCapabilityId.DirectDeliveryEligibility),
                Descriptor(
                    "fast-track",
                    "Another Fast Track Declaration",
                    "Example.AnotherFastTrack",
                    "AnotherFastTrack",
                    RuntimeCapabilityId.PickupTemperatureGrouping)
            })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WhenExactStaticIdRepeats_RejectsAmbiguousCatalog()
    {
        await Assert.That(() =>
            new DeclaredModIntegrationCatalog(new[]
            {
                Descriptor(
                    "fast-track",
                    "Fast Track",
                    "PeterHan.FastTrack",
                    "FastTrack",
                    RuntimeCapabilityId.DirectDeliveryEligibility),
                Descriptor(
                    "other-runtime-authority",
                    "Other Runtime Authority",
                    "PeterHan.FastTrack",
                    "OtherRuntimeAuthority",
                    RuntimeCapabilityId.PickupTemperatureGrouping)
            })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Descriptor_WhenCapabilityDeclarationRepeats_RejectsBeforeCatalogUse()
    {
        await Assert.That(() =>
            new DeclaredModIntegrationDescriptor(
                new DeclaredModIntegrationId("fast-track"),
                "Fast Track",
                new[] { "PeterHan.FastTrack" },
                new[] { "FastTrack" },
                "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
                new[]
                {
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        ExternalModIntegrationCategory.ExclusiveRuntimeAuthority),
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        ExternalModIntegrationCategory.ExclusiveRuntimeAuthority)
                })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task FastTrackOnlyCatalog_WhenConstructed_HasOneExplicitDeclaration()
    {
        DeclaredModIntegrationDescriptor fastTrack = Descriptor(
            "fast-track",
            "Fast Track",
            "PeterHan.FastTrack",
            "FastTrack",
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        var catalog = new DeclaredModIntegrationCatalog(new[] { fastTrack });

        await Assert.That(catalog.Descriptors).Count().IsEqualTo(1);
        await Assert.That(catalog.Descriptors[0].IntegrationId.Value).IsEqualTo("fast-track");
        await Assert.That(catalog.Descriptors[0].DeclaredCapabilityIds.ToArray()).IsEquivalentTo(new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeCapabilityId.DirectDeliveryEligibility
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static DeclaredModIntegrationDescriptor Descriptor(
        string integrationId,
        string displayName,
        string staticId,
        string assemblySimpleName,
        params RuntimeCapabilityId[] capabilities) =>
        new DeclaredModIntegrationDescriptor(
            new DeclaredModIntegrationId(integrationId),
            displayName,
            new[] { staticId },
            new[] { assemblySimpleName },
            "https://example.com/upstream-evidence/" + integrationId,
            capabilities.Select(capabilityId =>
                new DeclaredModIntegrationCapability(
                    capabilityId,
                    ExternalModIntegrationCategory
                        .ExclusiveRuntimeAuthority)));
}
