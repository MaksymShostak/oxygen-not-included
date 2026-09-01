namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;

[TestClass]
public sealed class DeclaredModIntegrationCatalogTests
{
    [TestMethod]
    public void Constructor_WhenDeclarationsAreValid_PreservesInsertionOrderAndCopiesInput()
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

        CollectionAssert.AreEqual(
            new[] { fastTrack, synthetic },
            catalog.Descriptors.ToArray());
        Assert.AreSame(
            fastTrack,
            catalog.GetRequired(new DeclaredModIntegrationId("fast-track")));
        Assert.IsFalse(catalog.TryGet(
            new DeclaredModIntegrationId("unknown-integration"),
            out _));
    }

    [TestMethod]
    public void Constructor_WhenIntegrationIdentityRepeats_RejectsCatalog()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
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
            }));
    }

    [TestMethod]
    public void Constructor_WhenExactStaticIdRepeats_RejectsAmbiguousCatalog()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
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
            }));
    }

    [TestMethod]
    public void Descriptor_WhenCapabilityDeclarationRepeats_RejectsBeforeCatalogUse()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
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
                }));
    }

    [TestMethod]
    public void FastTrackOnlyCatalog_WhenConstructed_HasOneExplicitDeclaration()
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

        Assert.HasCount(1, catalog.Descriptors);
        Assert.AreEqual(
            "fast-track",
            catalog.Descriptors[0].IntegrationId.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeCapabilityId.DirectDeliveryEligibility
            },
            catalog.Descriptors[0].DeclaredCapabilityIds.ToArray());
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
