using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;
public sealed class RuntimePatchCapabilitySelectorTests
{
    private static readonly DeclaredModIntegrationId
        KleiTextualIdentityCollisionId =
        new("temperature-limit-klei");
    private static readonly DeclaredModIntegrationId FastTrackId =
        new("fast-track");
    private static readonly DeclaredModIntegrationId SyntheticAuthorityId =
        new("synthetic-runtime-authority");

    [Test]
    public async Task Select_WhenNoExternalIntegrationClaimsCapability_SelectsKleiBaseline()
    {
        RuntimeCapabilityDefinition definition = Definition(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityCriticality.Required);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[] { definition },
                Array.Empty<PreparedRuntimeAuthorityContribution>(),
                Array.Empty<ExternalModIntegrationOutcome>());

        await Assert.That(selection.GetCapabilitySelection(
                RuntimeCapabilityId.DirectDeliveryEligibility)
                .PrepareSelectedContribution()
                .ImplementationIdentity).IsEqualTo(RuntimeAuthorityImplementationIdentity.KleiBaseline);
    }

    [Test]
    public async Task Select_WhenOneCompatibleExternalOwnerClaimsCapability_SelectsIt()
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

        await Assert.That(selection.GetCapabilitySelection(
                RuntimeCapabilityId.DirectDeliveryEligibility)
                .PrepareSelectedContribution()).IsSameReferenceAs(fastTrack);
        await Assert.That(selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
    }

    [Test]
    public async Task Select_WhenCompatibleExternalOwnerIsSelected_DoesNotPrepareKleiBaselineAlternative()
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

        await Assert.That(selection.GetCapabilitySelection(
                RuntimeCapabilityId.DirectDeliveryEligibility)
                .PrepareSelectedContribution()).IsSameReferenceAs(fastTrack);
        await Assert.That(kleiBaselineWasPrepared).IsFalse();
    }

    [Test]
    [Arguments((int)RuntimeAuthorityObservation.OwnsIncompatible)]
    [Arguments((int)RuntimeAuthorityObservation.OwnershipUnavailable)]
    public async Task Select_WhenRequiredOwnerCannotProveCompatibleContribution_BlocksActivation(
        int observationValue)
    {
        var observation = (RuntimeAuthorityObservation)observationValue;
        PreparedRuntimeAuthorityContribution claim = UnavailableContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility,
            observation);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { claim },
                    new[] { Outcome(claim) })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("required-runtime-capability-unavailable");
        await Assert.That(exception.CapabilityId).IsEqualTo(RuntimeCapabilityId.DirectDeliveryEligibility);
        await Assert.That(exception.ExternalModIntegrationOutcomes).Count().IsEqualTo(1);
        await Assert.That(exception.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition).IsEqualTo(IntegrationCapabilityDisposition.ActivationBlocking);
    }

    [Test]
    public async Task Select_WhenUnavailableAuthorityOutcomeLacksContribution_RejectsFailClosed()
    {
        PreparedRuntimeAuthorityContribution unavailable =
            UnavailableContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeAuthorityObservation.OwnershipUnavailable);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.DirectDeliveryEligibility,
                            RuntimeCapabilityCriticality.Required)
                    },
                    Array.Empty<PreparedRuntimeAuthorityContribution>(),
                    new[] { Outcome(unavailable) })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("missing-runtime-authority-contribution");
    }

    [Test]
    [Arguments((int)IntegrationContractState.Incompatible)]
    [Arguments((int)IntegrationContractState.VerificationUnavailable)]
    public async Task Select_WhenCompatibleContributionReportsNonCompatibleContractState_RejectsContradiction(
        int contractStateValue)
    {
        PreparedRuntimeAuthorityContribution contribution =
            CompatibleContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
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
                    })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("contradictory-runtime-authority-contract-state");
    }

    [Test]
    public async Task Select_WhenCompatibleContributionReportsUnavailableDisposition_RejectsContradiction()
    {
        PreparedRuntimeAuthorityContribution contribution =
            CompatibleContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
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
                    })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("contradictory-runtime-authority-disposition");
    }

    [Test]
    public async Task Select_WhenOptionalOwnedCapabilityIsIncompatible_OmitsWithoutKleiFallback()
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
        await Assert.That(omission.HasSelectedContribution).IsFalse();
        await Assert.That(omission.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
        await Assert.That(omission.DiagnosticCode).IsEqualTo(claim.DiagnosticCode);
        await Assert.That(omission.DiagnosticMessage).IsEqualTo(claim.DiagnosticMessage);
        await Assert.That(selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
    }

    [Test]
    public async Task Select_WhenOptionalCapabilityHasNoImplementation_ReportsExplicitOmission()
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
        await Assert.That(omission.HasSelectedContribution).IsFalse();
        await Assert.That(omission.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
        await Assert.That(omission.DiagnosticCode).IsEqualTo("optional-runtime-capability-without-implementation");
        await Assert.That(string.IsNullOrWhiteSpace(omission.DiagnosticMessage)).IsFalse();
    }

    [Test]
    public async Task Select_WhenTwoCompatibleOwnersClaimCapability_RejectsConflict()
    {
        PreparedRuntimeAuthorityContribution fastTrack = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.PickupTemperatureGrouping);
        PreparedRuntimeAuthorityContribution synthetic = CompatibleContribution(
            SyntheticAuthorityId,
            RuntimeCapabilityId.PickupTemperatureGrouping);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { fastTrack, synthetic },
                    new[] { Outcome(fastTrack), Outcome(synthetic) })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("conflicting-runtime-authority-owners");
    }

    [Test]
    public async Task Select_WhenTwoOwnersClaimAndOneIsIncompatible_StillRejectsConflict()
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
            (await Assert.That(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required)
                    },
                    new[] { compatible, incompatible },
                    new[] { Outcome(compatible), Outcome(incompatible) })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("conflicting-runtime-authority-owners");
    }

    [Test]
    public async Task Select_WhenOutcomeNamesUndefinedCapability_RejectsInput()
    {
        ExternalModIntegrationOutcome outcome = Outcome(
            UnavailableContribution(
                FastTrackId,
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeAuthorityObservation.DoesNotOwn));

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
                RuntimePatchCapabilitySelector.Select(
                    new[]
                    {
                        Definition(
                            RuntimeCapabilityId.PickupTemperatureGrouping,
                            RuntimeCapabilityCriticality.Required)
                    },
                    Array.Empty<PreparedRuntimeAuthorityContribution>(),
                    new[] { outcome })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("undeclared-runtime-capability-outcome");
    }

    [Test]
    public async Task Select_WhenOutcomeContainsAdditiveOnlyCapability_PreservesItWithoutRuntimeDefinition()
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

        await Assert.That(selection.CapabilitySelections).Count().IsEqualTo(1);
        await Assert.That(selection.ExternalModIntegrationOutcomes).Count().IsEqualTo(1);
        await Assert.That(selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition).IsEqualTo(IntegrationCapabilityDisposition.Ready);
    }

    [Test]
    public async Task Select_WhenAtomicBundleWouldMixKleiAndExternalOwners_RejectsSelection()
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
            (await Assert.That(() =>
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
                    new[] { Outcome(externalDoesNotOwn, external) })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("mixed-runtime-capability-bundle");
        await Assert.That(exception.ExternalModIntegrationOutcomes[0]
            .Capabilities
            .All(capability =>
                capability.Disposition ==
                    IntegrationCapabilityDisposition.ActivationBlocking)).IsTrue();
        await Assert.That(exception.ExternalModIntegrationOutcomes[0]
            .Capabilities
            .All(capability => string.Equals(
                capability.DiagnosticCode,
                "mixed-runtime-capability-bundle",
                StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Select_WhenMixedRuntimeBundleSharesCapabilityIdWithAdditiveOutcome_PreservesAdditiveReadiness()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");
        PreparedRuntimeAuthorityContribution external = CompatibleContribution(
            FastTrackId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
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
                    })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        ExternalModIntegrationCapabilityOutcome additiveCapability =
            exception.ExternalModIntegrationOutcomes
                .Single(outcome => outcome.IntegrationId.Equals(
                    new DeclaredModIntegrationId(
                        "synthetic-additive-protocol")))
                .Capabilities
                .Single();
        await Assert.That(additiveCapability.Category).IsEqualTo(ExternalModIntegrationCategory.AdditiveInteroperability);
        await Assert.That(additiveCapability.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Ready);
        await Assert.That(additiveCapability.DiagnosticCode).IsNull();
        await Assert.That(additiveCapability.DiagnosticMessage).IsNull();
    }

    [Test]
    public async Task Select_WhenBundleOriginsShareTextualId_StillRejectsMixedImplementations()
    {
        var bundleId = new RuntimeCapabilityBundleId("delivery-correctness");
        PreparedRuntimeAuthorityContribution external = CompatibleContribution(
            KleiTextualIdentityCollisionId,
            RuntimeCapabilityId.DirectDeliveryEligibility);

        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
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
                    new[] { Outcome(external) })).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.DiagnosticCode).IsEqualTo("mixed-runtime-capability-bundle");
        await Assert.That(exception.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition).IsEqualTo(IntegrationCapabilityDisposition.ActivationBlocking);
    }

    [Test]
    public async Task Select_WhenAtomicBundleUsesOnlyKleiOwner_AcceptsSelection()
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

        await Assert.That(selection.CapabilitySelections.All(entry =>
            entry.PrepareSelectedContribution().ImplementationIdentity.Equals(
                RuntimeAuthorityImplementationIdentity.KleiBaseline))).IsTrue();
    }

    [Test]
    public async Task Select_WhenAtomicBundleUsesOnlyOneExternalOwner_AcceptsSelection()
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

        await Assert.That(selection.CapabilitySelections.All(entry =>
            entry.PrepareSelectedContribution().ImplementationIdentity
                .DeclaredExternalIntegrationId
                .Equals(FastTrackId))).IsTrue();
    }

    [Test]
    public async Task Select_WhenSyntheticAuthorityOwnsExistingCapability_UsesGenericPath()
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

        await Assert.That(selection.GetCapabilitySelection(
                RuntimeCapabilityId.PickupTemperatureGrouping)
                .PrepareSelectedContribution()
                .ImplementationIdentity
                .DeclaredExternalIntegrationId).IsEqualTo(SyntheticAuthorityId);
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
