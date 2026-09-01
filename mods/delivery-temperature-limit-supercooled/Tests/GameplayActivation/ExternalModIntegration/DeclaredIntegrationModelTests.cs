using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;

[TestClass]
public sealed class DeclaredIntegrationModelTests
{
    [TestMethod]
    public void Descriptor_WhenSourceCollectionsChange_RetainsExactOrderedDeclaration()
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

        Assert.AreEqual("fast-track", descriptor.IntegrationId.Value);
        Assert.AreEqual("Fast Track", descriptor.DisplayName);
        CollectionAssert.AreEqual(
            new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                ExternalModIntegrationCategory.AdditiveInteroperability
            },
            descriptor.Categories.ToArray());
        CollectionAssert.AreEqual(
            new[] { "PeterHan.FastTrack" },
            descriptor.AcceptedStaticIds.ToArray());
        CollectionAssert.AreEqual(
            new[] { "FastTrack" },
            descriptor.AcceptedAssemblySimpleNames.ToArray());
        Assert.AreEqual(
            "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
            descriptor.UpstreamEvidenceReference);
        CollectionAssert.AreEqual(
            new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityId.TemperatureStatusAvailability
            },
            descriptor.DeclaredCapabilityIds.ToArray());
        CollectionAssert.AreEqual(
            new[] { RuntimeCapabilityId.TemperatureStatusAvailability },
            descriptor.GetDeclaredCapabilityIds(
                    ExternalModIntegrationCategory.AdditiveInteroperability)
                .ToArray());
    }

    [TestMethod]
    public void Descriptor_WhenCapabilityCrossesInspectionBoundaries_RejectsDeclaration()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateDescriptor(declaredCapabilities: new[]
            {
                new DeclaredModIntegrationCapability(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority),
                new DeclaredModIntegrationCapability(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    ExternalModIntegrationCategory.AdditiveInteroperability)
            }));
    }

    [TestMethod]
    public void Descriptor_WhenHumanReadableOrUpstreamEvidenceIsInvalid_RejectsIt()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(" "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateDescriptor(new string('x', 129)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateDescriptor("Fast Track", upstreamEvidenceReference: " "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateDescriptor(
                "Fast Track",
                upstreamEvidenceReference: "relative/evidence"));
    }

    [TestMethod]
    public void Descriptor_WhenDeclaredElementsAreNullDuplicateOrNotExact_RejectsThem()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            acceptedStaticIds: new[] { "PeterHan.FastTrack", null! }));
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            acceptedAssemblySimpleNames: new[] { "FastTrack", null! }));
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            acceptedStaticIds:
                new[] { "PeterHan.FastTrack", "PeterHan.FastTrack" }));
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            acceptedAssemblySimpleNames: new[] { "FastTrack", "FastTrack" }));
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            acceptedStaticIds: new[] { " PeterHan.FastTrack" }));
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            acceptedAssemblySimpleNames: new[] { "FastTrack.dll" }));
        Assert.ThrowsExactly<ArgumentException>(() => CreateDescriptor(
            declaredCapabilities: new[]
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
    public void RuntimeCapabilityDefinition_WhenConstructed_PreservesTypedPolicy()
    {
        var baseline = KleiBaselineContribution(
            RuntimeCapabilityId.DirectDeliveryEligibility);
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");

        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            baseline,
            bundleId);

        Assert.AreEqual(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            definition.Id);
        Assert.AreEqual(
            RuntimeCapabilityCriticality.Required,
            definition.Criticality);
        Assert.IsTrue(definition.IsRequired);
        Assert.AreSame(baseline, definition.KleiBaselineContribution);
        Assert.AreEqual(bundleId, definition.AtomicBundleId);
    }

    [TestMethod]
    public void RuntimeCapabilityDefinition_WhenBaselineDescribesAnotherCapability_RejectsIt()
    {
        var mismatchedBaseline = KleiBaselineContribution(
            RuntimeCapabilityId.PickupTemperatureGrouping);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityCriticality.Required,
                mismatchedBaseline,
                null));
    }

    [TestMethod]
    public void RuntimeCapabilityDefinition_WhenBaselineRequiresExternalReplacement_RejectsIt()
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

        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityCriticality.Required,
                replacementBackedBaseline,
                null));
    }

    [TestMethod]
    public void RuntimeCapabilitySelectionEntry_ForSelectedContribution_PreservesSelectedResult()
    {
        PreparedRuntimeAuthorityContribution baseline =
            KleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility);
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            baseline,
            null);

        RuntimeCapabilitySelectionEntry selection =
            RuntimeCapabilitySelectionEntry.ForSelectedContribution(
                definition,
                baseline);

        Assert.AreSame(definition, selection.Definition);
        Assert.AreSame(baseline, selection.SelectedContribution);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Selected,
            selection.Disposition);
        Assert.IsNull(selection.DiagnosticCode);
        Assert.IsNull(selection.DiagnosticMessage);
    }

    [TestMethod]
    public void RuntimeCapabilitySelectionEntry_ForSelectedContribution_WhenCapabilityDiffers_RejectsIt()
    {
        PreparedRuntimeAuthorityContribution baseline =
            KleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility);
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            baseline,
            null);
        PreparedRuntimeAuthorityContribution mismatchedContribution =
            KleiBaselineContribution(
                RuntimeCapabilityId.PickupTemperatureGrouping);

        Assert.ThrowsExactly<ArgumentException>(() =>
            RuntimeCapabilitySelectionEntry.ForSelectedContribution(
                definition,
                mismatchedContribution));
    }

    [TestMethod]
    public void RuntimeCapabilitySelectionEntry_ForOptionalOmission_PreservesUnavailableDiagnostic()
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

        Assert.AreSame(definition, omission.Definition);
        Assert.IsNull(omission.SelectedContribution);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            omission.Disposition);
        Assert.AreEqual(
            "optional-runtime-capability-without-implementation",
            omission.DiagnosticCode);
        Assert.AreEqual(
            "No implementation was available for the optional capability.",
            omission.DiagnosticMessage);
    }

    [TestMethod]
    public void RuntimeCapabilitySelectionEntry_ForOptionalOmission_WhenCapabilityIsRequired_RejectsIt()
    {
        PreparedRuntimeAuthorityContribution baseline =
            KleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility);
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            baseline,
            null);

        Assert.ThrowsExactly<ArgumentException>(() =>
            RuntimeCapabilitySelectionEntry.ForOptionalOmission(
                definition,
                "required-runtime-capability-omitted",
                "A required capability cannot be omitted."));
    }

    [TestMethod]
    public void RuntimeAuthorityRequirement_WhenSourceOwnersChange_RetainsExactEvidence()
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

        Assert.AreSame(target, requirement.TargetMethod);
        Assert.AreEqual(
            RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
            requirement.Kind);
        Assert.AreEqual("PeterHan.FastTrack", requirement.RequiredHarmonyOwner);
        Assert.AreSame(prefix, requirement.RequiredPrefixMethod);
        CollectionAssert.AreEqual(
            new[]
            {
                "PeterHan.FastTrack",
                "MaksymShostak.DeliveryTemperatureLimit"
            },
            requirement.PermittedSkippingPrefixOwners.ToArray());
    }

    [TestMethod]
    public void RuntimeAuthorityRequirement_WhenEvidenceContradictsKind_RejectsIt()
    {
        MethodInfo target = TargetMethod();
        MethodInfo prefix = PatchMethod();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.KleiOriginal,
                "PeterHan.FastTrack",
                null,
                new[] { "PeterHan.FastTrack" }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                null,
                prefix,
                new[] { "PeterHan.FastTrack" }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                "PeterHan.FastTrack",
                null,
                new[] { "PeterHan.FastTrack" }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                "PeterHan.FastTrack",
                prefix,
                new[] { "Some.Other.Owner" }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new RuntimeAuthorityRequirement(
                target,
                RuntimeAuthorityRequirementKind.KleiOriginal,
                null,
                null,
                new[] { " " }));
    }

    [TestMethod]
    public void ActivePrefixDescriptor_WhenConstructed_PreservesCopiedAuthorityFacts()
    {
        MethodInfo target = TargetMethod();
        MethodInfo prefix = PatchMethod();

        var descriptor = new ActiveHarmonyPrefixDescriptor(
            target,
            prefix,
            "PeterHan.FastTrack",
            800);

        Assert.AreSame(target, descriptor.TargetMethod);
        Assert.AreSame(prefix, descriptor.PrefixMethod);
        Assert.AreEqual("PeterHan.FastTrack", descriptor.HarmonyOwner);
        Assert.AreEqual(800, descriptor.Priority);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ActiveHarmonyPrefixDescriptor(target, prefix, " ", 0));
    }

    [TestMethod]
    public void CompatibleContribution_WhenSourceCollectionsChange_RetainsCompleteEvidence()
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

        Assert.HasCount(1, contribution.PatchGroupIds);
        Assert.HasCount(1, contribution.PatchBindings);
        Assert.HasCount(1, contribution.AuthorityRequirements);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnsCompatible,
            contribution.AuthorityObservation);
    }

    [TestMethod]
    public void Contribution_WhenAuthorityClaimIsIncompleteOrContradictory_RejectsIt()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new PreparedRuntimeAuthorityContribution(
                default,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                Array.Empty<RuntimePatchGroupId>(),
                RuntimeAuthorityObservation.DoesNotOwn,
                Array.Empty<HarmonyPatchContractBinding>(),
                Array.Empty<RuntimeAuthorityRequirement>(),
                null,
                null));

        Assert.ThrowsExactly<ArgumentException>(() =>
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
                null));

        Assert.ThrowsExactly<ArgumentException>(() =>
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
                null));

        HarmonyPatchContractBinding binding = Binding();
        Assert.ThrowsExactly<ArgumentException>(() =>
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
                null));

        Assert.ThrowsExactly<ArgumentException>(() =>
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
                null));
    }

    [TestMethod]
    public void Diagnostics_WhenCodeOrMessageIsUnbounded_RejectsIt()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ExternalModIntegrationDiagnostic(" ", "Useful message"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ExternalModIntegrationDiagnostic(
                "contract-incompatible",
                new string('x', 2049)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ExternalModIntegrationCapabilityOutcome(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                RuntimeAuthorityObservation.OwnsIncompatible,
                IntegrationContractState.Incompatible,
                IntegrationCapabilityDisposition.ActivationBlocking,
                "contract-incompatible",
                null));
    }

    [TestMethod]
    public void IntegrationOutcome_WhenSourceCollectionsChange_RetainsSanitizedFacts()
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

        CollectionAssert.AreEqual(
            new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                ExternalModIntegrationCategory.AdditiveInteroperability
            },
            outcome.Categories.ToArray());
        Assert.HasCount(1, outcome.Capabilities);
        Assert.AreEqual(
            ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
            outcome.Capabilities[0].Category);
        Assert.HasCount(1, outcome.Diagnostics);
        Assert.AreEqual(DeclaredModMatchState.Matched, outcome.MatchState);
        Assert.AreEqual("0.18.4.0", outcome.AssemblyVersion);

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
            Assert.IsFalse(
                forbiddenRetainedTypes.Any(type =>
                    type.IsAssignableFrom(property.PropertyType)),
                $"Outcome property {property.Name} retains runtime object graph type " +
                property.PropertyType.FullName);
        }
    }

    [TestMethod]
    public void IntegrationOutcome_WhenCapabilityCategoryIsNotDeclared_RejectsIt()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
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
                Array.Empty<ExternalModIntegrationDiagnostic>()));
    }

    [TestMethod]
    public void PreparedInspection_WhenSourceContributionsChange_RetainsMatchingOutcome()
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

        Assert.AreSame(outcome, inspection.Outcome);
        Assert.HasCount(1, inspection.Contributions);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new PreparedRuntimeAuthorityInspection(
                outcome,
                new[]
                {
                    CompatibleContribution(
                        new DeclaredModIntegrationId("other-authority"),
                        RuntimeCapabilityId.DirectDeliveryEligibility)
                }));
    }

    [TestMethod]
    public void PreparedInspection_WhenOwnedOutcomeLacksContribution_RejectsIt()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new PreparedRuntimeAuthorityInspection(
                Outcome(new DeclaredModIntegrationId("fast-track")),
                Array.Empty<PreparedRuntimeAuthorityContribution>()));
    }

    [TestMethod]
    public void PreparedInspection_WhenContributionContradictsOutcomeAuthority_RejectsIt()
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

        Assert.ThrowsExactly<ArgumentException>(() =>
            new PreparedRuntimeAuthorityInspection(
                Outcome(integrationId),
                new[] { contradictoryContribution }));
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
