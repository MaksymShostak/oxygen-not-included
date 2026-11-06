using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;
public sealed class DeclaredIntegrationModelTests
{
    [Test]
    public async Task Descriptor_WhenSourceCollectionsChange_RetainsExactOrderedDeclaration()
    {
        var declaredCapabilities = new List<DeclaredModIntegrationCapability>
        {
            new(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority),
            new(
                RuntimeCapabilityId.TemperatureStatusAvailability,
                ExternalModIntegrationCategory.AdditiveInteroperability)
        };
        var staticIds = new List<string> { "PeterHan.FastTrack" };
        var assemblyNames = new List<string> { "FastTrack" };
        var descriptor = new DeclaredModIntegrationDescriptor(
            new DeclaredModIntegrationId("fast-track"),
            "Fast Track",
            staticIds,
            assemblyNames,
            "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
            declaredCapabilities);

        declaredCapabilities.Clear();
        staticIds[0] = "Changed.StaticId";
        assemblyNames[0] = "ChangedAssembly";

        await Assert.That(descriptor.IntegrationId.Value).IsEqualTo("fast-track");
        await Assert.That(descriptor.DisplayName).IsEqualTo("Fast Track");
        await Assert.That(descriptor.Categories.ToArray()).IsEquivalentTo(new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                ExternalModIntegrationCategory.AdditiveInteroperability
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.AcceptedStaticIds.ToArray()).IsEquivalentTo(new[] { "PeterHan.FastTrack" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.AcceptedAssemblySimpleNames.ToArray()).IsEquivalentTo(new[] { "FastTrack" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.UpstreamEvidenceReference).IsEqualTo("https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta");
        await Assert.That(descriptor.DeclaredCapabilityIds.ToArray()).IsEquivalentTo(new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityId.TemperatureStatusAvailability
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.GetDeclaredCapabilityIds(
                    ExternalModIntegrationCategory.AdditiveInteroperability)
                .ToArray()).IsEquivalentTo(new[] { RuntimeCapabilityId.TemperatureStatusAvailability }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Descriptor_WhenCapabilityCrossesInspectionBoundaries_RejectsDeclaration()
    {
        await Assert.That(() =>
            CreateDescriptor(declaredCapabilities: new[]
            {
                new DeclaredModIntegrationCapability(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority),
                new DeclaredModIntegrationCapability(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    ExternalModIntegrationCategory.AdditiveInteroperability)
            })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Descriptor_WhenHumanReadableOrUpstreamEvidenceIsInvalid_RejectsIt()
    {
        await Assert.That(() => CreateDescriptor(" ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            CreateDescriptor(new string('x', 129))).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            CreateDescriptor("Fast Track", upstreamEvidenceReference: " ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            CreateDescriptor(
                "Fast Track",
                upstreamEvidenceReference: "relative/evidence")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Descriptor_WhenDeclaredElementsAreNullDuplicateOrNotExact_RejectsThem()
    {
        await Assert.That(() => CreateDescriptor(
            acceptedStaticIds: new[] { "PeterHan.FastTrack", null! })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CreateDescriptor(
            acceptedAssemblySimpleNames: new[] { "FastTrack", null! })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CreateDescriptor(
            acceptedStaticIds:
                new[] { "PeterHan.FastTrack", "PeterHan.FastTrack" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CreateDescriptor(
            acceptedAssemblySimpleNames: new[] { "FastTrack", "FastTrack" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CreateDescriptor(
            acceptedStaticIds: new[] { " PeterHan.FastTrack" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CreateDescriptor(
            acceptedAssemblySimpleNames: new[] { "FastTrack.dll" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => CreateDescriptor(
            declaredCapabilities: new[]
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
    public async Task RuntimeCapabilityDefinition_WhenConstructed_PreservesTypedPolicy()
    {
        var baseline = KleiBaselineContribution(
            RuntimeCapabilityId.DirectDeliveryEligibility);
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");

        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            () => baseline,
            bundleId);

        await Assert.That(definition.Id).IsEqualTo(RuntimeCapabilityId.DirectDeliveryEligibility);
        await Assert.That(definition.Criticality).IsEqualTo(RuntimeCapabilityCriticality.Required);
        await Assert.That(definition.IsRequired).IsTrue();
        await Assert.That(definition.PrepareKleiBaselineContribution()).IsSameReferenceAs(baseline);
        await Assert.That(definition.AtomicBundleId).IsEqualTo(bundleId);
    }

    [Test]
    public async Task RuntimeCapabilityDefinition_WhenBaselineDescribesAnotherCapability_RejectsIt()
    {
        var mismatchedBaseline = KleiBaselineContribution(
            RuntimeCapabilityId.PickupTemperatureGrouping);

        await Assert.That(() =>
        {
            var definition = new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityCriticality.Required,
                () => mismatchedBaseline,
                null);
            _ = definition.PrepareKleiBaselineContribution();
        }).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RuntimeCapabilityDefinition_WhenBaselineRequiresExternalReplacement_RejectsIt()
    {
        PreparedRuntimeAuthorityContribution replacementBackedBaseline =
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity.KleiBaseline,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                new[] { new RuntimePatchGroupId("klei-direct-delivery") },
                RuntimeAuthorityObservation.OwnsCompatible,
                new[] { Binding() },
                new[] { ExactReplacementRequirement() },
                null,
                null);

        await Assert.That(() =>
        {
            var definition = new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityCriticality.Required,
                () => replacementBackedBaseline,
                null);
            _ = definition.PrepareKleiBaselineContribution();
        }).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RuntimeCapabilitySelectionEntry_ForSelectedContribution_PreservesSelectedResult()
    {
        PreparedRuntimeAuthorityContribution baseline =
            KleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility);
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            () => baseline,
            null);

        RuntimeCapabilitySelectionEntry selection =
            RuntimeCapabilitySelectionEntry.ForSelectedContribution(
                definition,
                baseline);

        await Assert.That(selection.Definition).IsSameReferenceAs(definition);
        await Assert.That(selection.PrepareSelectedContribution()).IsSameReferenceAs(baseline);
        await Assert.That(selection.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
        await Assert.That(selection.DiagnosticCode).IsNull();
        await Assert.That(selection.DiagnosticMessage).IsNull();
    }

    [Test]
    public async Task RuntimeCapabilitySelectionEntry_ForSelectedContribution_WhenCapabilityDiffers_RejectsIt()
    {
        PreparedRuntimeAuthorityContribution baseline =
            KleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility);
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            () => baseline,
            null);
        PreparedRuntimeAuthorityContribution mismatchedContribution =
            KleiBaselineContribution(
                RuntimeCapabilityId.PickupTemperatureGrouping);

        await Assert.That(() =>
            RuntimeCapabilitySelectionEntry.ForSelectedContribution(
                definition,
                mismatchedContribution)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RuntimeCapabilitySelectionEntry_ForOptionalOmission_PreservesUnavailableDiagnostic()
    {
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.TemperatureStatusAvailability,
            RuntimeCapabilityCriticality.Optional,
            null,
            null);

        RuntimeCapabilitySelectionEntry omission =
            RuntimeCapabilitySelectionEntry.ForOptionalOmission(
                definition,
                "optional-runtime-capability-without-implementation",
                "No implementation was available for the optional capability.");

        await Assert.That(omission.Definition).IsSameReferenceAs(definition);
        await Assert.That(omission.HasSelectedContribution).IsFalse();
        await Assert.That(omission.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
        await Assert.That(omission.DiagnosticCode).IsEqualTo("optional-runtime-capability-without-implementation");
        await Assert.That(omission.DiagnosticMessage).IsEqualTo("No implementation was available for the optional capability.");
    }

    [Test]
    public async Task RuntimeCapabilitySelectionEntry_ForOptionalOmission_WhenCapabilityIsRequired_RejectsIt()
    {
        PreparedRuntimeAuthorityContribution baseline =
            KleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility);
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            () => baseline,
            null);

        await Assert.That(() =>
            RuntimeCapabilitySelectionEntry.ForOptionalOmission(
                definition,
                "required-runtime-capability-omitted",
                "A required capability cannot be omitted.")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RuntimeAuthorityRequirement_WhenSourceOwnersChange_RetainsExactEvidence()
    {
        var owners = new List<string>
        {
            "PeterHan.FastTrack",
            "MaksymShostak.DeliveryTemperatureLimit",
            "PeterHan.FastTrack"
        };
        MethodInfo target = TargetMethod();
        MethodInfo prefix = PatchMethod();

        var requirement = new RuntimeAuthorityRequirement(
            target,
            RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
            "PeterHan.FastTrack",
            prefix,
            owners);

        owners.Clear();

        await Assert.That(requirement.TargetMethod).IsSameReferenceAs(target);
        await Assert.That(requirement.Kind).IsEqualTo(RuntimeAuthorityRequirementKind.ExactOwnedReplacement);
        await Assert.That(requirement.RequiredHarmonyOwner).IsEqualTo("PeterHan.FastTrack");
        await Assert.That(requirement.RequiredPrefixMethod).IsSameReferenceAs(prefix);
        await Assert.That(requirement.PermittedSkippingPrefixOwners.ToArray()).IsEquivalentTo(new[]
            {
                "PeterHan.FastTrack",
                "MaksymShostak.DeliveryTemperatureLimit"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task RuntimeAuthorityRequirement_WhenEvidenceContradictsKind_RejectsIt()
    {
        MethodInfo target = TargetMethod();
        MethodInfo prefix = PatchMethod();

        await Assert.That(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.KleiOriginal,
                "PeterHan.FastTrack",
                null,
                new[] { "PeterHan.FastTrack" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                null,
                prefix,
                new[] { "PeterHan.FastTrack" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                "PeterHan.FastTrack",
                null,
                new[] { "PeterHan.FastTrack" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                "PeterHan.FastTrack",
                prefix,
                new[] { "Some.Other.Owner" })).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.KleiOriginal,
                null,
                null,
                new[] { " " })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ActivePrefixDescriptor_WhenConstructed_PreservesCopiedAuthorityFacts()
    {
        MethodInfo target = TargetMethod();
        MethodInfo prefix = PatchMethod();

        var descriptor = new ActiveHarmonyPrefixDescriptor(
            target,
            prefix,
            "PeterHan.FastTrack",
            800);

        await Assert.That(descriptor.TargetMethod).IsSameReferenceAs(target);
        await Assert.That(descriptor.PrefixMethod).IsSameReferenceAs(prefix);
        await Assert.That(descriptor.HarmonyOwner).IsEqualTo("PeterHan.FastTrack");
        await Assert.That(descriptor.Priority).IsEqualTo(800);
        await Assert.That(() =>
            new ActiveHarmonyPrefixDescriptor(target, prefix, " ", 0)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task CompatibleContribution_WhenSourceCollectionsChange_RetainsCompleteEvidence()
    {
        var patchGroups = new List<RuntimePatchGroupId>
        {
            new RuntimePatchGroupId("fast-track-direct-delivery")
        };
        var patchBindings = new List<HarmonyPatchContractBinding>
        {
            Binding()
        };
        var requirements = new List<RuntimeAuthorityRequirement>
        {
            ExactReplacementRequirement()
        };

        var contribution = new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(
                    new DeclaredModIntegrationId("fast-track")),
            RuntimeCapabilityId.DirectDeliveryEligibility,
            patchGroups,
            RuntimeAuthorityObservation.OwnsCompatible,
            patchBindings,
            requirements,
            null,
            null);

        patchGroups.Clear();
        patchBindings.Clear();
        requirements.Clear();

        await Assert.That(contribution.PatchGroupIds).Count().IsEqualTo(1);
        await Assert.That(contribution.PatchBindings).Count().IsEqualTo(1);
        await Assert.That(contribution.AuthorityRequirements).Count().IsEqualTo(1);
        await Assert.That(contribution.AuthorityObservation).IsEqualTo(RuntimeAuthorityObservation.OwnsCompatible);
    }

    [Test]
    public async Task Contribution_WhenAuthorityClaimIsIncompleteOrContradictory_RejectsIt()
    {
        await Assert.That(() =>
            new PreparedRuntimeAuthorityContribution(
                default,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                Array.Empty<RuntimePatchGroupId>(),
                RuntimeAuthorityObservation.DoesNotOwn,
                Array.Empty<HarmonyPatchContractBinding>(),
                Array.Empty<RuntimeAuthorityRequirement>(),
                null,
                null)).ThrowsExactly<ArgumentException>();

        await Assert.That(() =>
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(
                        new DeclaredModIntegrationId("fast-track")),
                RuntimeCapabilityId.DirectDeliveryEligibility,
                Array.Empty<RuntimePatchGroupId>(),
                RuntimeAuthorityObservation.DoesNotOwn,
                new[] { Binding() },
                Array.Empty<RuntimeAuthorityRequirement>(),
                null,
                null)).ThrowsExactly<ArgumentException>();

        await Assert.That(() =>
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(
                        new DeclaredModIntegrationId("fast-track")),
                RuntimeCapabilityId.DirectDeliveryEligibility,
                Array.Empty<RuntimePatchGroupId>(),
                RuntimeAuthorityObservation.OwnsCompatible,
                Array.Empty<HarmonyPatchContractBinding>(),
                Array.Empty<RuntimeAuthorityRequirement>(),
                null,
                null)).ThrowsExactly<ArgumentException>();

        HarmonyPatchContractBinding binding = Binding();
        await Assert.That(() =>
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(
                        new DeclaredModIntegrationId("fast-track")),
                RuntimeCapabilityId.DirectDeliveryEligibility,
                new[] { new RuntimePatchGroupId("fast-track-direct-delivery") },
                RuntimeAuthorityObservation.OwnsCompatible,
                new[] { binding, binding },
                new[] { ExactReplacementRequirement() },
                null,
                null)).ThrowsExactly<ArgumentException>();

        await Assert.That(() =>
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(
                        new DeclaredModIntegrationId("fast-track")),
                RuntimeCapabilityId.DirectDeliveryEligibility,
                Array.Empty<RuntimePatchGroupId>(),
                RuntimeAuthorityObservation.OwnsIncompatible,
                Array.Empty<HarmonyPatchContractBinding>(),
                Array.Empty<RuntimeAuthorityRequirement>(),
                null,
                null)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Diagnostics_WhenCodeOrMessageIsUnbounded_RejectsIt()
    {
        await Assert.That(() =>
            new ExternalModIntegrationDiagnostic(" ", "Useful message")).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new ExternalModIntegrationDiagnostic(
                "contract-incompatible",
                new string('x', 2049))).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new ExternalModIntegrationCapabilityOutcome(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                RuntimeAuthorityObservation.OwnsIncompatible,
                IntegrationContractState.Incompatible,
                IntegrationCapabilityDisposition.ActivationBlocking,
                "contract-incompatible",
                null)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task IntegrationOutcome_WhenSourceCollectionsChange_RetainsSanitizedFacts()
    {
        var categories = new List<ExternalModIntegrationCategory>
        {
            ExternalModIntegrationCategory.AdditiveInteroperability,
            ExternalModIntegrationCategory.ExclusiveRuntimeAuthority
        };
        var capabilities = new List<ExternalModIntegrationCapabilityOutcome>
        {
            new ExternalModIntegrationCapabilityOutcome(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                RuntimeAuthorityObservation.OwnsCompatible,
                IntegrationContractState.Compatible,
                IntegrationCapabilityDisposition.Selected,
                null,
                null)
        };
        var diagnostics = new List<ExternalModIntegrationDiagnostic>
        {
            new ExternalModIntegrationDiagnostic(
                "identity-verified",
                "The declared identity was verified.")
        };

        var outcome = new ExternalModIntegrationOutcome(
            new DeclaredModIntegrationId("fast-track"),
            "Fast Track",
            categories,
            DeclaredModMatchState.Matched,
            "FastTrack, Version=0.18.4.0",
            "0.18.4.0",
            "0.18.4.0",
            "8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D",
            capabilities,
            diagnostics);

        categories.Clear();
        capabilities.Clear();
        diagnostics.Clear();

        await Assert.That(outcome.Categories.ToArray()).IsEquivalentTo(new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                ExternalModIntegrationCategory.AdditiveInteroperability
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(outcome.Capabilities).Count().IsEqualTo(1);
        await Assert.That(outcome.Capabilities[0].Category).IsEqualTo(ExternalModIntegrationCategory.ExclusiveRuntimeAuthority);
        await Assert.That(outcome.Diagnostics).Count().IsEqualTo(1);
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.Matched);
        await Assert.That(outcome.AssemblyVersion).IsEqualTo("0.18.4.0");

        Type[] forbiddenRetainedTypes =
        {
            typeof(Assembly),
            typeof(MemberInfo),
            typeof(Exception)
        };
        foreach (PropertyInfo property in typeof(ExternalModIntegrationOutcome)
                     .GetProperties(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
        {
            await Assert.That(forbiddenRetainedTypes.Any(type =>
                    type.IsAssignableFrom(property.PropertyType))).IsFalse().Because($"Outcome property {property.Name} retains runtime object graph type " +
                property.PropertyType.FullName);
        }
    }

    [Test]
    public async Task IntegrationOutcome_WhenCapabilityCategoryIsNotDeclared_RejectsIt()
    {
        await Assert.That(() =>
            new ExternalModIntegrationOutcome(
                new DeclaredModIntegrationId("synthetic-additive-protocol"),
                "Synthetic Additive Protocol",
                new[]
                {
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority
                },
                DeclaredModMatchState.Matched,
                null,
                null,
                null,
                null,
                new[]
                {
                    new ExternalModIntegrationCapabilityOutcome(
                        RuntimeCapabilityId.TemperatureStatusAvailability,
                        ExternalModIntegrationCategory.AdditiveInteroperability,
                        RuntimeAuthorityObservation.DoesNotOwn,
                        IntegrationContractState.Compatible,
                        IntegrationCapabilityDisposition.Ready,
                        null,
                        null)
                },
                Array.Empty<ExternalModIntegrationDiagnostic>())).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task PreparedInspection_WhenSourceContributionsChange_RetainsMatchingOutcome()
    {
        var outcome = Outcome(new DeclaredModIntegrationId("fast-track"));
        var contributions = new List<PreparedRuntimeAuthorityContribution>
        {
            CompatibleContribution(
                new DeclaredModIntegrationId("fast-track"),
                RuntimeCapabilityId.DirectDeliveryEligibility)
        };

        var inspection = new PreparedRuntimeAuthorityInspection(
            outcome,
            contributions);

        contributions.Clear();

        await Assert.That(inspection.Outcome).IsSameReferenceAs(outcome);
        await Assert.That(inspection.Contributions).Count().IsEqualTo(1);
        await Assert.That(() =>
            new PreparedRuntimeAuthorityInspection(
                outcome,
                new[]
                {
                    CompatibleContribution(
                        new DeclaredModIntegrationId("other-authority"),
                        RuntimeCapabilityId.DirectDeliveryEligibility)
                })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task PreparedInspection_WhenOwnedOutcomeLacksContribution_RejectsIt()
    {
        await Assert.That(() =>
            new PreparedRuntimeAuthorityInspection(
                Outcome(new DeclaredModIntegrationId("fast-track")),
                Array.Empty<PreparedRuntimeAuthorityContribution>())).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task PreparedInspection_WhenContributionContradictsOutcomeAuthority_RejectsIt()
    {
        var integrationId = new DeclaredModIntegrationId("fast-track");
        var contradictoryContribution =
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(integrationId),
                RuntimeCapabilityId.DirectDeliveryEligibility,
                Array.Empty<RuntimePatchGroupId>(),
                RuntimeAuthorityObservation.OwnershipUnavailable,
                Array.Empty<HarmonyPatchContractBinding>(),
                Array.Empty<RuntimeAuthorityRequirement>(),
                "runtime-authority-unavailable",
                "The declared runtime authority could not be inspected.");

        await Assert.That(() =>
            new PreparedRuntimeAuthorityInspection(
                Outcome(integrationId),
                new[] { contradictoryContribution })).ThrowsExactly<ArgumentException>();
    }

    private static DeclaredModIntegrationDescriptor CreateDescriptor(
        string displayName = "Fast Track",
        IEnumerable<string>? acceptedStaticIds = null,
        IEnumerable<string>? acceptedAssemblySimpleNames = null,
        string upstreamEvidenceReference =
            "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
        IEnumerable<DeclaredModIntegrationCapability>?
            declaredCapabilities = null) =>
        new DeclaredModIntegrationDescriptor(
            new DeclaredModIntegrationId("fast-track"),
            displayName,
            acceptedStaticIds ?? new[] { "PeterHan.FastTrack" },
            acceptedAssemblySimpleNames ?? new[] { "FastTrack" },
            upstreamEvidenceReference,
            declaredCapabilities ?? new[]
            {
                new DeclaredModIntegrationCapability(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority)
            });

    private static PreparedRuntimeAuthorityContribution CompatibleContribution(
        DeclaredModIntegrationId integrationId,
        RuntimeCapabilityId capabilityId) =>
        new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(integrationId),
            capabilityId,
            new[] { new RuntimePatchGroupId("direct-delivery-eligibility") },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[] { Binding() },
            new[] { ExactReplacementRequirement() },
            null,
            null);

    private static PreparedRuntimeAuthorityContribution
        KleiBaselineContribution(RuntimeCapabilityId capabilityId) =>
            new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity.KleiBaseline,
                capabilityId,
                new[] { new RuntimePatchGroupId("klei-" + capabilityId.Value) },
                RuntimeAuthorityObservation.OwnsCompatible,
                new[] { Binding() },
                new[]
                {
                    new RuntimeAuthorityRequirement(
                        TargetMethod(),
                        RuntimeAuthorityRequirementKind.KleiOriginal,
                        null,
                        null,
                        Array.Empty<string>())
                },
                null,
                null);

    private static ExternalModIntegrationOutcome Outcome(
        DeclaredModIntegrationId integrationId) =>
        new ExternalModIntegrationOutcome(
            integrationId,
            "Fast Track",
            new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority
            },
            DeclaredModMatchState.Matched,
            null,
            null,
            null,
            null,
            new[]
            {
                new ExternalModIntegrationCapabilityOutcome(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                    RuntimeAuthorityObservation.OwnsCompatible,
                    IntegrationContractState.Compatible,
                    IntegrationCapabilityDisposition.Selected,
                    null,
                    null)
            },
            Array.Empty<ExternalModIntegrationDiagnostic>());

    private static HarmonyPatchContractBinding Binding() =>
        new HarmonyPatchContractBinding(
            TargetMethod(),
            PatchMethod(),
            HarmonyPatchContractKind.Prefix);

    private static RuntimeAuthorityRequirement ExactReplacementRequirement() =>
        new RuntimeAuthorityRequirement(
            TargetMethod(),
            RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
            "PeterHan.FastTrack",
            PatchMethod(),
            new[]
            {
                "PeterHan.FastTrack",
                "MaksymShostak.DeliveryTemperatureLimit"
            });

    private static MethodInfo TargetMethod() =>
        typeof(AuthorityFixture).GetMethod(
            nameof(AuthorityFixture.Target),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static MethodInfo PatchMethod() =>
        typeof(AuthorityFixture).GetMethod(
            nameof(AuthorityFixture.Prefix),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static class AuthorityFixture
    {
        internal static void Target()
        {
        }

        internal static void Prefix()
        {
        }
    }
}
