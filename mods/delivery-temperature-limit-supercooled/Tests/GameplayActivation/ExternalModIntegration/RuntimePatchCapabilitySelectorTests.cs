using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;

[TestClass]
public sealed class RuntimePatchCapabilitySelectorTests
{
    private static readonly DeclaredModIntegrationId
        KleiTextualIdentityCollisionId =
        new("temperature-limit-klei");
    private static readonly DeclaredModIntegrationId FastTrackId =
        new("fast-track");
    private static readonly DeclaredModIntegrationId SyntheticAuthorityId =
        new("synthetic-runtime-authority");

    [TestMethod]
    public void Select_WhenNoExternalIntegrationClaimsCapability_SelectsKleiBaseline()
    {
        RuntimeCapabilityDefinition definition = Definition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[] { definition },
                Array.Empty<PreparedRuntimeAuthorityContribution>(),
                Array.Empty<ExternalModIntegrationOutcome>());

        Assert.AreEqual(
            RuntimeAuthorityImplementationIdentity.KleiBaseline,
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.DirectDeliveryEligibility)
                .PrepareSelectedContribution()
                .ImplementationIdentity);
    }

    [TestMethod]
    public void Select_WhenOneCompatibleExternalOwnerClaimsCapability_SelectsIt()
    {
        PreparedRuntimeAuthorityContribution fastTrack = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    Definition(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        RuntimeCapabilityCriticality.Required)
                },
                new[] { fastTrack },
                new[] { Outcome(fastTrack) });

        Assert.AreSame(
            fastTrack,
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.DirectDeliveryEligibility)
                .PrepareSelectedContribution());
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Selected,
            selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition);
    }

    [TestMethod]
    public void Select_WhenCompatibleExternalOwnerIsSelected_DoesNotPrepareKleiBaselineAlternative()
    {
        bool kleiBaselineWasPrepared = false;
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required,
            () =>
            {
                kleiBaselineWasPrepared = true;
                return KleiBaselineContribution(
                    RuntimeCapabilityId.DirectDeliveryEligibility);
            },
            atomicBundleId: null);
        PreparedRuntimeAuthorityContribution fastTrack = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[] { definition },
                new[] { fastTrack },
                new[] { Outcome(fastTrack) });

        Assert.AreSame(
            fastTrack,
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.DirectDeliveryEligibility)
                .PrepareSelectedContribution());
        Assert.IsFalse(kleiBaselineWasPrepared);
    }

    [TestMethod]
    [DataRow((int)RuntimeAuthorityObservation.OwnsIncompatible)]
    [DataRow((int)RuntimeAuthorityObservation.OwnershipUnavailable)]
    public void Select_WhenRequiredOwnerCannotProveCompatibleContribution_BlocksActivation(
        int observationValue)
    {
        var observation = (RuntimeAuthorityObservation)observationValue;
        PreparedRuntimeAuthorityContribution claim = UnavailableContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility,
            observation);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { claim },
                    new[] { Outcome(claim) }));

        Assert.AreEqual(
            "required-runtime-capability-unavailable",
            exception.DiagnosticCode);
        Assert.AreEqual(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            exception.CapabilityId);
        Assert.HasCount(1, exception.ExternalModIntegrationOutcomes);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.ActivationBlocking,
            exception.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition);
    }

    [TestMethod]
    public void Select_WhenUnavailableAuthorityOutcomeLacksContribution_RejectsFailClosed()
    {
        PreparedRuntimeAuthorityContribution unavailable =
            UnavailableContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeAuthorityObservation.OwnershipUnavailable);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required)
                    },
                    Array.Empty<PreparedRuntimeAuthorityContribution>(),
                    new[] { Outcome(unavailable) }));

        Assert.AreEqual(
            "missing-runtime-authority-contribution",
            exception.DiagnosticCode);
    }

    [TestMethod]
    [DataRow((int)IntegrationContractState.Incompatible)]
    [DataRow((int)IntegrationContractState.VerificationUnavailable)]
    public void Select_WhenCompatibleContributionReportsNonCompatibleContractState_RejectsContradiction(
        int contractStateValue)
    {
        PreparedRuntimeAuthorityContribution contribution =
            CompatibleContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { contribution },
                    new[]
                    {
                        OutcomeWithContractState(
                            contribution,
                            (IntegrationContractState)contractStateValue)
                    }));

        Assert.AreEqual(
            "contradictory-runtime-authority-contract-state",
            exception.DiagnosticCode);
    }

    [TestMethod]
    public void Select_WhenCompatibleContributionReportsUnavailableDisposition_RejectsContradiction()
    {
        PreparedRuntimeAuthorityContribution contribution =
            CompatibleContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { contribution },
                    new[]
                    {
                        OutcomeWithCapabilityState(
                            contribution,
                            IntegrationContractState.Compatible,
                            IntegrationCapabilityDisposition.Unavailable)
                    }));

        Assert.AreEqual(
            "contradictory-runtime-authority-disposition",
            exception.DiagnosticCode);
    }

    [TestMethod]
    public void Select_WhenOptionalOwnedCapabilityIsIncompatible_OmitsWithoutKleiFallback()
    {
        PreparedRuntimeAuthorityContribution claim = UnavailableContribution(
            FastTrackId,
            RuntimeCapabilityId.TemperatureStatusAvailability,
            RuntimeAuthorityObservation.OwnsIncompatible);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    Definition(
                        RuntimeCapabilityId.TemperatureStatusAvailability,
                        RuntimeCapabilityCriticality.Optional)
                },
                new[] { claim },
                new[] { Outcome(claim) });

        RuntimeCapabilitySelectionEntry omission =
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.TemperatureStatusAvailability);
        Assert.IsFalse(omission.HasSelectedContribution);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            omission.Disposition);
        Assert.AreEqual(
            claim.DiagnosticCode,
            omission.DiagnosticCode);
        Assert.AreEqual(
            claim.DiagnosticMessage,
            omission.DiagnosticMessage);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition);
    }

    [TestMethod]
    public void Select_WhenOptionalCapabilityHasNoImplementation_ReportsExplicitOmission()
    {
        var definition = new RuntimeCapabilityDefinition(
            RuntimeCapabilityId.TemperatureStatusAvailability,
            RuntimeCapabilityCriticality.Optional,
            null,
            null);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[] { definition },
                Array.Empty<PreparedRuntimeAuthorityContribution>(),
                Array.Empty<ExternalModIntegrationOutcome>());

        RuntimeCapabilitySelectionEntry omission =
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.TemperatureStatusAvailability);
        Assert.IsFalse(omission.HasSelectedContribution);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            omission.Disposition);
        Assert.AreEqual(
            "optional-runtime-capability-without-implementation",
            omission.DiagnosticCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(omission.DiagnosticMessage));
    }

    [TestMethod]
    public void Select_WhenTwoCompatibleOwnersClaimCapability_RejectsConflict()
    {
        PreparedRuntimeAuthorityContribution fastTrack = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.PickupTemperatureGrouping);
        PreparedRuntimeAuthorityContribution synthetic = CompatibleContribution(
            SyntheticAuthorityId,
            RuntimeCapabilityId.PickupTemperatureGrouping);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { fastTrack, synthetic },
                    new[] { Outcome(fastTrack), Outcome(synthetic) }));

        Assert.AreEqual("conflicting-runtime-authority-owners", exception.DiagnosticCode);
    }

    [TestMethod]
    public void Select_WhenTwoOwnersClaimAndOneIsIncompatible_StillRejectsConflict()
    {
        PreparedRuntimeAuthorityContribution compatible =
            CompatibleContribution(
                FastTrackId,
                RuntimeCapabilityId.PickupTemperatureGrouping);
        PreparedRuntimeAuthorityContribution incompatible =
            UnavailableContribution(
                SyntheticAuthorityId,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeAuthorityObservation.OwnsIncompatible);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { compatible, incompatible },
                    new[] { Outcome(compatible), Outcome(incompatible) }));

        Assert.AreEqual("conflicting-runtime-authority-owners", exception.DiagnosticCode);
    }

    [TestMethod]
    public void Select_WhenOutcomeNamesUndefinedCapability_RejectsInput()
    {
        ExternalModIntegrationOutcome outcome = Outcome(
            UnavailableContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeAuthorityObservation.DoesNotOwn));

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required)
                    },
                    Array.Empty<PreparedRuntimeAuthorityContribution>(),
                    new[] { outcome }));

        Assert.AreEqual("undeclared-runtime-capability-outcome", exception.DiagnosticCode);
    }

    [TestMethod]
    public void Select_WhenOutcomeContainsAdditiveOnlyCapability_PreservesItWithoutRuntimeDefinition()
    {
        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    Definition(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        RuntimeCapabilityCriticality.Required)
                },
                Array.Empty<PreparedRuntimeAuthorityContribution>(),
                new[]
                {
                    AdditiveOutcome(
                        RuntimeCapabilityId.TemperatureStatusAvailability)
                });

        Assert.HasCount(1, selection.CapabilitySelections);
        Assert.HasCount(1, selection.ExternalModIntegrationOutcomes);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Ready,
            selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition);
    }

    [TestMethod]
    public void Select_WhenAtomicBundleWouldMixKleiAndExternalOwners_RejectsSelection()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");
        PreparedRuntimeAuthorityContribution externalDoesNotOwn =
            UnavailableContribution(
                FastTrackId,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeAuthorityObservation.DoesNotOwn);
        PreparedRuntimeAuthorityContribution external = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required,
                            bundleId),
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required,
                            bundleId)
                    },
                    new[] { externalDoesNotOwn, external },
                    new[] { Outcome(externalDoesNotOwn, external) }));

        Assert.AreEqual("mixed-runtime-capability-bundle", exception.DiagnosticCode);
        Assert.IsTrue(exception.ExternalModIntegrationOutcomes[0]
            .Capabilities
            .All(capability =>
                capability.Disposition ==
                    IntegrationCapabilityDisposition.ActivationBlocking));
        Assert.IsTrue(exception.ExternalModIntegrationOutcomes[0]
            .Capabilities
            .All(capability => string.Equals(
                capability.DiagnosticCode,
                "mixed-runtime-capability-bundle",
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Select_WhenMixedRuntimeBundleSharesCapabilityIdWithAdditiveOutcome_PreservesAdditiveReadiness()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");
        PreparedRuntimeAuthorityContribution external = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required,
                            bundleId),
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required,
                            bundleId)
                    },
                    new[] { external },
                    new[]
                    {
                        Outcome(external),
                        AdditiveOutcome(
                            RuntimeCapabilityId.PickupTemperatureGrouping)
                    }));

        ExternalModIntegrationCapabilityOutcome additiveCapability =
            exception.ExternalModIntegrationOutcomes
                .Single(outcome => outcome.IntegrationId.Equals(
                    new DeclaredModIntegrationId(
                        "synthetic-additive-protocol")))
                .Capabilities
                .Single();
        Assert.AreEqual(
            ExternalModIntegrationCategory.AdditiveInteroperability,
            additiveCapability.Category);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Ready,
            additiveCapability.Disposition);
        Assert.IsNull(additiveCapability.DiagnosticCode);
        Assert.IsNull(additiveCapability.DiagnosticMessage);
    }

    [TestMethod]
    public void Select_WhenBundleOriginsShareTextualId_StillRejectsMixedImplementations()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");
        PreparedRuntimeAuthorityContribution external = CompatibleContribution(
            KleiTextualIdentityCollisionId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required,
                            bundleId),
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required,
                            bundleId)
                    },
                    new[] { external },
                    new[] { Outcome(external) }));

        Assert.AreEqual(
            "mixed-runtime-capability-bundle",
            exception.DiagnosticCode);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.ActivationBlocking,
            exception.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition);
    }

    [TestMethod]
    public void Select_WhenAtomicBundleUsesOnlyKleiOwner_AcceptsSelection()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    Definition(
                        RuntimeCapabilityId.PickupTemperatureGrouping,
                        RuntimeCapabilityCriticality.Required,
                        bundleId),
                    Definition(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        RuntimeCapabilityCriticality.Required,
                        bundleId)
                },
                Array.Empty<PreparedRuntimeAuthorityContribution>(),
                Array.Empty<ExternalModIntegrationOutcome>());

        Assert.IsTrue(selection.CapabilitySelections.All(entry =>
            entry.PrepareSelectedContribution().ImplementationIdentity.Equals(
                RuntimeAuthorityImplementationIdentity.KleiBaseline)));
    }

    [TestMethod]
    public void Select_WhenAtomicBundleUsesOnlyOneExternalOwner_AcceptsSelection()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");
        PreparedRuntimeAuthorityContribution pickup = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.PickupTemperatureGrouping);
        PreparedRuntimeAuthorityContribution direct = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    Definition(
                        RuntimeCapabilityId.PickupTemperatureGrouping,
                        RuntimeCapabilityCriticality.Required,
                        bundleId),
                    Definition(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        RuntimeCapabilityCriticality.Required,
                        bundleId)
                },
                new[] { pickup, direct },
                new[] { Outcome(pickup, direct) });

        Assert.IsTrue(selection.CapabilitySelections.All(entry =>
            entry.PrepareSelectedContribution().ImplementationIdentity
                .DeclaredExternalIntegrationId
                .Equals(FastTrackId)));
    }

    [TestMethod]
    public void Select_WhenSyntheticAuthorityOwnsExistingCapability_UsesGenericPath()
    {
        PreparedRuntimeAuthorityContribution synthetic = CompatibleContribution(
            SyntheticAuthorityId,
            RuntimeCapabilityId.PickupTemperatureGrouping);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    Definition(
                        RuntimeCapabilityId.PickupTemperatureGrouping,
                        RuntimeCapabilityCriticality.Required)
                },
                new[] { synthetic },
                new[] { Outcome(synthetic) });

        Assert.AreEqual(
            SyntheticAuthorityId,
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.PickupTemperatureGrouping)
                .PrepareSelectedContribution()
                .ImplementationIdentity
                .DeclaredExternalIntegrationId);
    }

    private static RuntimeCapabilityDefinition Definition(
        RuntimeCapabilityId capabilityId,
        RuntimeCapabilityCriticality criticality,
        RuntimeCapabilityBundleId? bundleId = null) =>
        new(
            capabilityId,
            criticality,
            () => KleiBaselineContribution(capabilityId),
            bundleId);

    private static PreparedRuntimeAuthorityContribution CompatibleContribution(
        DeclaredModIntegrationId integrationId,
        RuntimeCapabilityId capabilityId) =>
        new(
            RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(integrationId),
            capabilityId,
            new[]
            {
                new RuntimePatchGroupId(
                    integrationId.Value + "-" + capabilityId.Value)
            },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    TargetMethod(),
                    PatchMethod(),
                    HarmonyPatchContractKind.Prefix)
            },
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

    private static PreparedRuntimeAuthorityContribution UnavailableContribution(
        DeclaredModIntegrationId integrationId,
        RuntimeCapabilityId capabilityId,
        RuntimeAuthorityObservation observation) =>
        new(
            RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(integrationId),
            capabilityId,
            Array.Empty<RuntimePatchGroupId>(),
            observation,
            Array.Empty<HarmonyPatchContractBinding>(),
            Array.Empty<RuntimeAuthorityRequirement>(),
            observation == RuntimeAuthorityObservation.DoesNotOwn
                ? null
                : "runtime-authority-unavailable",
            observation == RuntimeAuthorityObservation.DoesNotOwn
                ? null
                : "The declared runtime authority could not supply this capability.");

    private static ExternalModIntegrationOutcome Outcome(
        params PreparedRuntimeAuthorityContribution[] contributions)
    {
        DeclaredModIntegrationId integrationId = contributions[0]
            .ImplementationIdentity
            .DeclaredExternalIntegrationId ??
            throw new InvalidOperationException(
                "An external test outcome requires an external contribution.");
        return new ExternalModIntegrationOutcome(
            integrationId,
            integrationId.Equals(FastTrackId)
                ? "Fast Track"
                : "Synthetic Runtime Authority",
            new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority
            },
            DeclaredModMatchState.Matched,
            null,
            null,
            null,
            null,
            contributions.Select(contribution =>
                new ExternalModIntegrationCapabilityOutcome(
                    contribution.CapabilityId,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                    contribution.AuthorityObservation,
                    ContractState(contribution.AuthorityObservation),
                    InitialDisposition(contribution.AuthorityObservation),
                    contribution.DiagnosticCode,
                    contribution.DiagnosticMessage)),
            Array.Empty<ExternalModIntegrationDiagnostic>());
    }

    private static ExternalModIntegrationOutcome AdditiveOutcome(
        RuntimeCapabilityId capabilityId) =>
        new(
            new DeclaredModIntegrationId("synthetic-additive-protocol"),
            "Synthetic Additive Protocol",
            new[]
            {
                ExternalModIntegrationCategory.AdditiveInteroperability
            },
            DeclaredModMatchState.Matched,
            null,
            null,
            null,
            null,
            new[]
            {
                new ExternalModIntegrationCapabilityOutcome(
                    capabilityId,
                    ExternalModIntegrationCategory.AdditiveInteroperability,
                    RuntimeAuthorityObservation.DoesNotOwn,
                    IntegrationContractState.Compatible,
                    IntegrationCapabilityDisposition.Ready,
                    null,
                    null)
            },
            Array.Empty<ExternalModIntegrationDiagnostic>());

    private static ExternalModIntegrationOutcome OutcomeWithContractState(
        PreparedRuntimeAuthorityContribution contribution,
        IntegrationContractState contractState) =>
        OutcomeWithCapabilityState(
            contribution,
            contractState,
            IntegrationCapabilityDisposition.Unavailable);

    private static ExternalModIntegrationOutcome OutcomeWithCapabilityState(
        PreparedRuntimeAuthorityContribution contribution,
        IntegrationContractState contractState,
        IntegrationCapabilityDisposition disposition) =>
        new(
            contribution.ImplementationIdentity
                .DeclaredExternalIntegrationId ??
                throw new InvalidOperationException(
                    "An external test outcome requires an external identity."),
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
                    contribution.CapabilityId,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                    contribution.AuthorityObservation,
                    contractState,
                    disposition,
                    "runtime-authority-contract-incompatible",
                    "The reported runtime-authority contract is not compatible.")
            },
            Array.Empty<ExternalModIntegrationDiagnostic>());

    private static PreparedRuntimeAuthorityContribution
        KleiBaselineContribution(RuntimeCapabilityId capabilityId) =>
            new(
                RuntimeAuthorityImplementationIdentity.KleiBaseline,
                capabilityId,
                new[]
                {
                    new RuntimePatchGroupId("klei-" + capabilityId.Value)
                },
                RuntimeAuthorityObservation.OwnsCompatible,
                new[]
                {
                    new HarmonyPatchContractBinding(
                        TargetMethod(),
                        PatchMethod(),
                        HarmonyPatchContractKind.Prefix)
                },
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

    private static IntegrationContractState ContractState(
        RuntimeAuthorityObservation observation) =>
        observation switch
        {
            RuntimeAuthorityObservation.DoesNotOwn =>
                IntegrationContractState.NotEvaluated,
            RuntimeAuthorityObservation.OwnsCompatible =>
                IntegrationContractState.Compatible,
            RuntimeAuthorityObservation.OwnsIncompatible =>
                IntegrationContractState.Incompatible,
            RuntimeAuthorityObservation.OwnershipUnavailable =>
                IntegrationContractState.VerificationUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(observation))
        };

    private static IntegrationCapabilityDisposition InitialDisposition(
        RuntimeAuthorityObservation observation) =>
        observation switch
        {
            RuntimeAuthorityObservation.DoesNotOwn =>
                IntegrationCapabilityDisposition.NotApplicable,
            RuntimeAuthorityObservation.OwnsCompatible =>
                IntegrationCapabilityDisposition.Ready,
            RuntimeAuthorityObservation.OwnsIncompatible =>
                IntegrationCapabilityDisposition.Unavailable,
            RuntimeAuthorityObservation.OwnershipUnavailable =>
                IntegrationCapabilityDisposition.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(observation))
        };

    private static MethodInfo TargetMethod() =>
        typeof(SelectorFixture).GetMethod(
            nameof(SelectorFixture.Target),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static MethodInfo PatchMethod() =>
        typeof(SelectorFixture).GetMethod(
            nameof(SelectorFixture.Prefix),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static class SelectorFixture
    {
        internal static void Target()
        {
        }

        internal static void Prefix()
        {
        }
    }
}
