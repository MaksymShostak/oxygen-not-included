using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;
public sealed class DeliveryTemperatureRuntimePatchPlanTests
{
    private const string FastTrackHarmonyOwner = "PeterHan.FastTrack";
    private const string FastTrackAssemblySha256 =
        "8B7914F7E50D0A53F96779A1D47E875585F6A45607959D4885D722067BE30C86";

    private static readonly DeclaredModIntegrationId FastTrackIntegrationId =
        new("fast-track");
    private static readonly RuntimeCapabilityId GameSessionLifecycleCapabilityId =
        new("game-session-lifecycle");
    private static readonly RuntimeCapabilityId WorldParentTopologyCapabilityId =
        new("world-parent-topology");
    private static readonly RuntimeCapabilityId
        AuthoritativeFetchTemperatureEligibilityCapabilityId =
            new("authoritative-fetch-temperature-eligibility");

    [Test]
    public async Task Create_WhenCapabilitySelectionIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                capabilitySelection: null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Create_WhenKleiBaselinesAreSelected_PreservesCompleteContributionOrder()
    {
        RuntimePatchCapabilitySelection selection = CreateSelection();

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                selection);

        await AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility");
        await Assert.That(plan.SelectedContributions.Count).IsEqualTo(7);
        await Assert.That(plan.OrderedPatchBindings.Count).IsEqualTo(7);
        await Assert.That(plan.AuthorityRequirements.Count).IsEqualTo(7);
        await Assert.That(plan.SelectedContributions.All(
            contribution => contribution.ImplementationIdentity.IsKleiBaseline)).IsTrue();
        await Assert.That(plan.StatusCompatibilityDiagnostic).IsNull();
        await Assert.That(plan.ExternalModIntegrationOutcomes[0]).IsSameReferenceAs(selection.ExternalModIntegrationOutcomes[0]);
    }

    [Test]
    public async Task Create_WhenFastTrackWorldInventoryIsSelected_UsesItsCompleteContribution()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            worldInventoryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        await AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "fast-track-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility");
        await AssertSelectedImplementation(
            plan,
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
    }

    [Test]
    public async Task Create_WhenFastTrackPickupGroupingIsSelected_UsesItsCompleteContribution()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        await AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "fast-track-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility");
        await AssertSelectedImplementation(
            plan,
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
    }

    [Test]
    public async Task Create_WhenFastTrackDirectDeliveryIsSelected_UsesItsCompleteContribution()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            directDeliveryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        await AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility");
        await AssertSelectedImplementation(
            plan,
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
    }

    [Test]
    public async Task Create_WhenStatusOptionIsDisabled_OmitsWorldPublicationAndStatusContributions()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            worldInventoryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible,
            directDeliveryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        await AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "fast-track-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility");
        await Assert.That(plan.SelectedContributions.Any(contribution =>
            contribution.CapabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication) ||
            contribution.CapabilityId.Equals(
                RuntimeCapabilityId.TemperatureStatusAvailability))).IsFalse();
        await Assert.That(plan.StatusCompatibilityDiagnostic).IsNull();
    }

    [Test]
    public async Task Create_WhenStatusOptionIsDisabled_DoesNotPrepareOmittedKleiContributions()
    {
        int worldInventoryPreparationCount = 0;
        int statusAvailabilityPreparationCount = 0;
        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[]
                {
                    new RuntimeCapabilityDefinition(
                        RuntimeCapabilityId
                            .WorldInventoryTemperaturePublication,
                        RuntimeCapabilityCriticality.Optional,
                        () =>
                        {
                            worldInventoryPreparationCount++;
                            return CreateKleiBaselineContribution(
                                RuntimeCapabilityId
                                    .WorldInventoryTemperaturePublication,
                                "klei-world-inventory-temperature-publication",
                                nameof(WorldInventoryTarget));
                        },
                        atomicBundleId: null),
                    new RuntimeCapabilityDefinition(
                        RuntimeCapabilityId.TemperatureStatusAvailability,
                        RuntimeCapabilityCriticality.Optional,
                        () =>
                        {
                            statusAvailabilityPreparationCount++;
                            return CreateKleiBaselineContribution(
                                RuntimeCapabilityId
                                    .TemperatureStatusAvailability,
                                "temperature-status-availability",
                                nameof(TemperatureStatusAvailabilityTarget));
                        },
                        atomicBundleId: null)
                },
                Array.Empty<PreparedRuntimeAuthorityContribution>(),
                Array.Empty<ExternalModIntegrationOutcome>());

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: false,
                selection);

        await Assert.That(plan.SelectedContributions).Count().IsEqualTo(0);
        await Assert.That(worldInventoryPreparationCount).IsEqualTo(0);
        await Assert.That(statusAvailabilityPreparationCount).IsEqualTo(0);
    }

    [Test]
    public async Task Create_WhenWorldInventoryAuthorityIsIncompatible_OmitsOnlyPairedStatusResponsibilities()
    {
        RuntimePatchCapabilitySelection selection = CreateSelection(
            worldInventoryAuthority:
                RuntimeAuthorityObservation.OwnsIncompatible,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible,
            directDeliveryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                selection);

        await AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "fast-track-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility");
        Assert.NotNull(plan.StatusCompatibilityDiagnostic);
        await Assert.That(plan.StatusCompatibilityDiagnostic).Contains(RuntimeCapabilityId.WorldInventoryTemperaturePublication.Value);
        await Assert.That(plan.StatusCompatibilityDiagnostic).Contains(DiagnosticCode(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication));
        await Assert.That(plan.StatusCompatibilityDiagnostic).Contains("existing ONI status availability remains unchanged");
        ExternalModIntegrationCapabilityOutcome worldOutcome = selection
            .ExternalModIntegrationOutcomes[0]
            .Capabilities.Single(capability => capability.CapabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication));
        await Assert.That(worldOutcome.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
    }

    [Test]
    public async Task Select_WhenRequiredPickupAuthorityIsIncompatible_BlocksBeforePlanCreation()
    {
        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
                CreateSelection(
                    pickupGroupingAuthority:
                        RuntimeAuthorityObservation.OwnsIncompatible)).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await AssertRequiredCapabilityBlocked(
            exception,
            RuntimeCapabilityId.PickupTemperatureGrouping);
    }

    [Test]
    public async Task Select_WhenRequiredDirectDeliveryAuthorityIsIncompatible_BlocksBeforePlanCreation()
    {
        RuntimeCapabilitySelectionException exception =
            (await Assert.That(() =>
                CreateSelection(
                    directDeliveryAuthority:
                        RuntimeAuthorityObservation.OwnsIncompatible)).ThrowsExactly<RuntimeCapabilitySelectionException>())!;

        await AssertRequiredCapabilityBlocked(
            exception,
            RuntimeCapabilityId.DirectDeliveryEligibility);
    }

    [Test]
    public async Task VerifySelectedAuthority_WhenKleiTargetHasOnlyNonSkippingObserver_ReturnsNormally()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            includeDeclaredIntegrationOutcome: false);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPrefixDescriptor(
                RequireMethod(nameof(PickupGroupingTarget)),
                RequireMethod(nameof(ObservingPrefix)),
                "Unknown.PickupObserver",
                priority: 400)
        ]);

        await Assert.That(plan.ExternalModIntegrationOutcomes.Count).IsEqualTo(0);
    }

    [Test]
    public void VerifySelectedAuthority_WhenExactExternalReplacementIsUnchanged_ReturnsNormally()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPrefixDescriptor(
                RequireMethod(nameof(PickupGroupingTarget)),
                RequireMethod(nameof(FastTrackPickupGroupingPrefix)),
                FastTrackHarmonyOwner,
                priority: 800)
        ]);
    }

    [Test]
    public async Task VerifySelectedAuthority_WhenSelectedExternalPrefixMethodChanges_ThrowsAffectedGroupDiagnostic()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPrefixDescriptor(
                        RequireMethod(nameof(PickupGroupingTarget)),
                        RequireMethod(nameof(ChangedFastTrackPickupGroupingPrefix)),
                        FastTrackHarmonyOwner,
                        priority: 800)
                ])).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("fast-track-pickup-temperature-grouping");
        await Assert.That(exception.Message).Contains("FastTrackPickupGroupingPrefix");
        await Assert.That(exception.Message).Contains(FastTrackHarmonyOwner);
    }

    [Test]
    public async Task VerifySelectedAuthority_WhenUnknownModSkipsUnselectedTarget_DoesNotInterfereOrCreateOutcome()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            includeDeclaredIntegrationOutcome: false);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPrefixDescriptor(
                RequireMethod(nameof(UnknownModTarget)),
                RequireMethod(nameof(UnknownSkippingPrefix)),
                "Unknown.NoninterferingMod",
                priority: 900)
        ]);

        await Assert.That(plan.ExternalModIntegrationOutcomes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task VerifySelectedAuthority_WhenUndeclaredOwnerSkipsSelectedTarget_RejectsWithoutCreatingOutcome()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            includeDeclaredIntegrationOutcome: false);

        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPrefixDescriptor(
                        RequireMethod(nameof(PickupGroupingTarget)),
                        RequireMethod(nameof(UnknownSkippingPrefix)),
                        "Unknown.UndeclaredReplacement",
                        priority: 900)
                ])).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(plan.ExternalModIntegrationOutcomes.Count).IsEqualTo(0);
        await Assert.That(exception.Message).Contains("klei-pickup-temperature-grouping");
        await Assert.That(exception.Message).Contains("Unknown.UndeclaredReplacement");
    }

    [Test]
    public void VerifySelectedAuthority_WhenStatusIsDisabled_DoesNotInspectWorldAuthority()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false);

        plan.VerifySelectedAuthority(
        [
            new ActiveHarmonyPrefixDescriptor(
                RequireMethod(nameof(WorldInventoryTarget)),
                RequireMethod(nameof(UnknownSkippingPrefix)),
                "Unknown.UnselectedWorldReplacement",
                priority: 900)
        ]);
    }

    [Test]
    public async Task CreateSupportReportSnapshot_WhenSelectionIsGeneric_PreservesAuditIdsAndSanitizedOutcomeFacts()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true);

        SupportRuntimeSnapshot snapshot =
            plan.CreateSupportReportSnapshot("Installed");

        await Assert.That(snapshot.State).IsEqualTo("available");
        await Assert.That(snapshot.InstallationState).IsEqualTo("Installed");
        await Assert.That(snapshot.SelectedPatchGroups.ToArray()).IsEquivalentTo(plan.OrderedPatchGroupIds.Select(group => group.Value).ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(snapshot.ExternalModIntegrations).Count().IsEqualTo(1);
        SupportExternalModIntegrationSnapshot fastTrack =
            snapshot.ExternalModIntegrations[0];
        await Assert.That(fastTrack.IntegrationId).IsEqualTo("fast-track");
        await Assert.That(fastTrack.MatchState).IsEqualTo("not-matched");
        await Assert.That(fastTrack.AssemblyIdentity.State).IsEqualTo("unavailable");
        await Assert.That(fastTrack.Capabilities
                .Select(capability => capability.CapabilityId)
                .ToArray()).IsEquivalentTo(new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication.Value,
                RuntimeCapabilityId.PickupTemperatureGrouping.Value,
                RuntimeCapabilityId.DirectDeliveryEligibility.Value
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CreateSupportReportSnapshot_WhenStatusOptionDisablesCompatibleExternalWorldCapability_ReportsReadyDisposition()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            worldInventoryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        SupportExternalModCapabilitySnapshot worldCapability = plan
            .CreateSupportReportSnapshot("Installed")
            .ExternalModIntegrations[0]
            .Capabilities.Single(capability => string.Equals(
                capability.CapabilityId,
                RuntimeCapabilityId
                    .WorldInventoryTemperaturePublication.Value,
                StringComparison.Ordinal));

        await Assert.That(worldCapability.Disposition).IsEqualTo("ready");
    }

    private static DeliveryTemperatureRuntimePatchPlan CreatePlan(
        bool checkTemperatureForStatusItems,
        RuntimeAuthorityObservation worldInventoryAuthority =
            RuntimeAuthorityObservation.DoesNotOwn,
        RuntimeAuthorityObservation pickupGroupingAuthority =
            RuntimeAuthorityObservation.DoesNotOwn,
        RuntimeAuthorityObservation directDeliveryAuthority =
            RuntimeAuthorityObservation.DoesNotOwn,
        bool includeDeclaredIntegrationOutcome = true) =>
        DeliveryTemperatureRuntimePatchPlan.Create(
            checkTemperatureForStatusItems,
            CreateSelection(
                worldInventoryAuthority,
                pickupGroupingAuthority,
                directDeliveryAuthority,
                includeDeclaredIntegrationOutcome));

    private static RuntimePatchCapabilitySelection CreateSelection(
        RuntimeAuthorityObservation worldInventoryAuthority =
            RuntimeAuthorityObservation.DoesNotOwn,
        RuntimeAuthorityObservation pickupGroupingAuthority =
            RuntimeAuthorityObservation.DoesNotOwn,
        RuntimeAuthorityObservation directDeliveryAuthority =
            RuntimeAuthorityObservation.DoesNotOwn,
        bool includeDeclaredIntegrationOutcome = true)
    {
        PreparedRuntimeAuthorityContribution gameSession =
            CreateKleiBaselineContribution(
                GameSessionLifecycleCapabilityId,
                "game-session-lifecycle",
                nameof(GameSessionLifecycleTarget));
        PreparedRuntimeAuthorityContribution worldParent =
            CreateKleiBaselineContribution(
                WorldParentTopologyCapabilityId,
                "world-parent-topology",
                nameof(WorldParentTopologyTarget));
        PreparedRuntimeAuthorityContribution authoritativeFetch =
            CreateKleiBaselineContribution(
                AuthoritativeFetchTemperatureEligibilityCapabilityId,
                "klei-authoritative-fetch-temperature-eligibility",
                nameof(AuthoritativeFetchTemperatureEligibilityTarget));
        PreparedRuntimeAuthorityContribution worldInventory =
            CreateKleiBaselineContribution(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                "klei-world-inventory-temperature-publication",
                nameof(WorldInventoryTarget));
        PreparedRuntimeAuthorityContribution statusAvailability =
            CreateKleiBaselineContribution(
                RuntimeCapabilityId.TemperatureStatusAvailability,
                "temperature-status-availability",
                nameof(TemperatureStatusAvailabilityTarget));
        PreparedRuntimeAuthorityContribution pickupGrouping =
            CreateKleiBaselineContribution(
                RuntimeCapabilityId.PickupTemperatureGrouping,
                "klei-pickup-temperature-grouping",
                nameof(PickupGroupingTarget));
        PreparedRuntimeAuthorityContribution directDelivery =
            CreateKleiBaselineContribution(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                "klei-direct-delivery-eligibility",
                nameof(DirectDeliveryEligibilityTarget));

        RuntimeCapabilityDefinition[] definitions =
        [
            RequiredDefinition(
                GameSessionLifecycleCapabilityId,
                gameSession),
            RequiredDefinition(
                WorldParentTopologyCapabilityId,
                worldParent),
            RequiredDefinition(
                AuthoritativeFetchTemperatureEligibilityCapabilityId,
                authoritativeFetch),
            OptionalDefinition(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                worldInventory),
            OptionalDefinition(
                RuntimeCapabilityId.TemperatureStatusAvailability,
                statusAvailability),
            RequiredDefinition(
                RuntimeCapabilityId.PickupTemperatureGrouping,
                pickupGrouping),
            RequiredDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                directDelivery)
        ];

        RuntimeAuthorityObservation[] externalObservations =
        [
            worldInventoryAuthority,
            pickupGroupingAuthority,
            directDeliveryAuthority
        ];
        RuntimeCapabilityId[] externalCapabilities =
        [
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeCapabilityId.DirectDeliveryEligibility
        ];
        var externalContributions =
            new List<PreparedRuntimeAuthorityContribution>();
        for (int index = 0; index < externalCapabilities.Length; index++)
        {
            PreparedRuntimeAuthorityContribution? contribution =
                CreateExternalContribution(
                    externalCapabilities[index],
                    externalObservations[index]);
            if (contribution != null)
            {
                externalContributions.Add(contribution);
            }
        }

        IReadOnlyList<ExternalModIntegrationOutcome> outcomes =
            includeDeclaredIntegrationOutcome
                ? new[]
                {
                    CreateFastTrackOutcome(
                        externalCapabilities,
                        externalObservations)
                }
                : Array.Empty<ExternalModIntegrationOutcome>();
        return RuntimePatchCapabilitySelector.Select(
            definitions,
            externalContributions,
            outcomes);
    }

    private static PreparedRuntimeAuthorityContribution
        CreateKleiBaselineContribution(
            RuntimeCapabilityId capabilityId,
            string patchGroupId,
            string targetMethodName)
    {
        MethodInfo targetMethod = RequireMethod(targetMethodName);
        return new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity.KleiBaseline,
            capabilityId,
            new[] { new RuntimePatchGroupId(patchGroupId) },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    targetMethod,
                    RequireMethod(nameof(PreparedPostfix)),
                    HarmonyPatchContractKind.Postfix)
            },
            new[]
            {
                new RuntimeAuthorityRequirement(
                    targetMethod,
                    RuntimeAuthorityRequirementKind.KleiOriginal,
                    requiredHarmonyOwner: null,
                    requiredPrefixMethod: null,
                    Array.Empty<string>())
            },
            diagnosticCode: null,
            diagnosticMessage: null);
    }

    private static PreparedRuntimeAuthorityContribution?
        CreateExternalContribution(
            RuntimeCapabilityId capabilityId,
            RuntimeAuthorityObservation observation)
    {
        switch (observation)
        {
            case RuntimeAuthorityObservation.DoesNotOwn:
                return null;
            case RuntimeAuthorityObservation.OwnsCompatible:
                MethodInfo targetMethod = TargetMethod(capabilityId);
                return new PreparedRuntimeAuthorityContribution(
                    RuntimeAuthorityImplementationIdentity
                        .ForDeclaredExternalIntegration(FastTrackIntegrationId),
                    capabilityId,
                    new[]
                    {
                        new RuntimePatchGroupId(
                            ExternalPatchGroupId(capabilityId))
                    },
                    RuntimeAuthorityObservation.OwnsCompatible,
                    new[]
                    {
                        new HarmonyPatchContractBinding(
                            targetMethod,
                            RequireMethod(nameof(PreparedPostfix)),
                            HarmonyPatchContractKind.Postfix)
                    },
                    new[]
                    {
                        new RuntimeAuthorityRequirement(
                            targetMethod,
                            RuntimeAuthorityRequirementKind
                                .ExactOwnedReplacement,
                            FastTrackHarmonyOwner,
                            ExternalPrefixMethod(capabilityId),
                            new[] { FastTrackHarmonyOwner })
                    },
                    diagnosticCode: null,
                    diagnosticMessage: null);
            case RuntimeAuthorityObservation.OwnsIncompatible:
                return new PreparedRuntimeAuthorityContribution(
                    RuntimeAuthorityImplementationIdentity
                        .ForDeclaredExternalIntegration(FastTrackIntegrationId),
                    capabilityId,
                    Array.Empty<RuntimePatchGroupId>(),
                    RuntimeAuthorityObservation.OwnsIncompatible,
                    Array.Empty<HarmonyPatchContractBinding>(),
                    Array.Empty<RuntimeAuthorityRequirement>(),
                    DiagnosticCode(capabilityId),
                    DiagnosticMessage(capabilityId));
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(observation),
                    observation,
                    "This fixture supports does-not-own, compatible, and " +
                    "incompatible authority observations.");
        }
    }

    private static ExternalModIntegrationOutcome CreateFastTrackOutcome(
        IReadOnlyList<RuntimeCapabilityId> capabilityIds,
        IReadOnlyList<RuntimeAuthorityObservation> observations)
    {
        var capabilityOutcomes =
            new List<ExternalModIntegrationCapabilityOutcome>(
                capabilityIds.Count);
        var diagnostics = new List<ExternalModIntegrationDiagnostic>();
        bool matched = false;
        for (int index = 0; index < capabilityIds.Count; index++)
        {
            RuntimeCapabilityId capabilityId = capabilityIds[index];
            RuntimeAuthorityObservation observation = observations[index];
            matched |= observation != RuntimeAuthorityObservation.DoesNotOwn;
            string? diagnosticCode =
                observation == RuntimeAuthorityObservation.OwnsIncompatible
                    ? DiagnosticCode(capabilityId)
                    : null;
            string? diagnosticMessage =
                observation == RuntimeAuthorityObservation.OwnsIncompatible
                    ? DiagnosticMessage(capabilityId)
                    : null;
            capabilityOutcomes.Add(
                new ExternalModIntegrationCapabilityOutcome(
                    capabilityId,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                    observation,
                    observation == RuntimeAuthorityObservation.DoesNotOwn
                        ? IntegrationContractState.NotEvaluated
                        : observation ==
                            RuntimeAuthorityObservation.OwnsCompatible
                            ? IntegrationContractState.Compatible
                            : IntegrationContractState.Incompatible,
                    observation == RuntimeAuthorityObservation.DoesNotOwn
                        ? IntegrationCapabilityDisposition.NotApplicable
                        : observation ==
                            RuntimeAuthorityObservation.OwnsCompatible
                            ? IntegrationCapabilityDisposition.Ready
                            : IntegrationCapabilityDisposition.Unavailable,
                    diagnosticCode,
                    diagnosticMessage));
            if (diagnosticCode != null && diagnosticMessage != null)
            {
                diagnostics.Add(new ExternalModIntegrationDiagnostic(
                    diagnosticCode,
                    diagnosticMessage));
            }
        }

        return new ExternalModIntegrationOutcome(
            FastTrackIntegrationId,
            "Fast Track",
            new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority
            },
            matched
                ? DeclaredModMatchState.Matched
                : DeclaredModMatchState.NotMatched,
            matched ? "FastTrack, Version=0.18.4.0" : null,
            matched ? "0.18.0.0" : null,
            matched ? "0.18.4.0" : null,
            matched ? FastTrackAssemblySha256 : null,
            capabilityOutcomes,
            diagnostics);
    }

    private static RuntimeCapabilityDefinition RequiredDefinition(
        RuntimeCapabilityId capabilityId,
        PreparedRuntimeAuthorityContribution baseline) =>
        new(
            capabilityId,
            RuntimeCapabilityCriticality.Required,
            () => baseline,
            atomicBundleId: null);

    private static RuntimeCapabilityDefinition OptionalDefinition(
        RuntimeCapabilityId capabilityId,
        PreparedRuntimeAuthorityContribution baseline) =>
        new(
            capabilityId,
            RuntimeCapabilityCriticality.Optional,
            () => baseline,
            atomicBundleId: null);

    private static async Task AssertPatchGroupIds(
        DeliveryTemperatureRuntimePatchPlan plan,
        params string[] expectedGroupIds) =>
        await Assert.That(plan.OrderedPatchGroupIds.Select(group => group.Value)).IsEquivalentTo(expectedGroupIds, TUnit.Assertions.Enums.CollectionOrdering.Matching);

    private static async Task AssertSelectedImplementation(
        DeliveryTemperatureRuntimePatchPlan plan,
        RuntimeCapabilityId capabilityId,
        RuntimeAuthorityImplementationKind expectedKind)
    {
        PreparedRuntimeAuthorityContribution contribution =
            plan.SelectedContributions.Single(candidate =>
                candidate.CapabilityId.Equals(capabilityId));
        await Assert.That(contribution.ImplementationIdentity.Kind).IsEqualTo(expectedKind);
        if (expectedKind ==
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration)
        {
            await Assert.That(contribution.ImplementationIdentity
                    .DeclaredExternalIntegrationId).IsEqualTo(FastTrackIntegrationId);
        }
    }

    private static async Task AssertRequiredCapabilityBlocked(
        RuntimeCapabilitySelectionException exception,
        RuntimeCapabilityId expectedCapabilityId)
    {
        await Assert.That(exception.CapabilityId).IsEqualTo(expectedCapabilityId);
        await Assert.That(exception.DiagnosticCode).IsEqualTo("required-runtime-capability-unavailable");
        ExternalModIntegrationCapabilityOutcome outcome = exception
            .ExternalModIntegrationOutcomes[0]
            .Capabilities.Single(capability =>
                capability.CapabilityId.Equals(expectedCapabilityId));
        await Assert.That(outcome.Disposition).IsEqualTo(IntegrationCapabilityDisposition.ActivationBlocking);
    }

    private static string ExternalPatchGroupId(
        RuntimeCapabilityId capabilityId)
    {
        if (capabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication))
        {
            return "fast-track-world-inventory-temperature-publication";
        }

        if (capabilityId.Equals(RuntimeCapabilityId.PickupTemperatureGrouping))
        {
            return "fast-track-pickup-temperature-grouping";
        }

        if (capabilityId.Equals(RuntimeCapabilityId.DirectDeliveryEligibility))
        {
            return "fast-track-direct-delivery-eligibility";
        }

        throw new ArgumentOutOfRangeException(nameof(capabilityId));
    }

    private static string DiagnosticCode(RuntimeCapabilityId capabilityId) =>
        capabilityId.Value + "-incompatible";

    private static string DiagnosticMessage(RuntimeCapabilityId capabilityId) =>
        "The declared external owner could not verify runtime capability " +
        capabilityId.Value + ".";

    private static MethodInfo TargetMethod(RuntimeCapabilityId capabilityId)
    {
        if (capabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication))
        {
            return RequireMethod(nameof(WorldInventoryTarget));
        }

        if (capabilityId.Equals(RuntimeCapabilityId.PickupTemperatureGrouping))
        {
            return RequireMethod(nameof(PickupGroupingTarget));
        }

        if (capabilityId.Equals(RuntimeCapabilityId.DirectDeliveryEligibility))
        {
            return RequireMethod(nameof(DirectDeliveryEligibilityTarget));
        }

        throw new ArgumentOutOfRangeException(nameof(capabilityId));
    }

    private static MethodInfo ExternalPrefixMethod(
        RuntimeCapabilityId capabilityId)
    {
        if (capabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication))
        {
            return RequireMethod(nameof(FastTrackWorldInventoryPrefix));
        }

        if (capabilityId.Equals(RuntimeCapabilityId.PickupTemperatureGrouping))
        {
            return RequireMethod(nameof(FastTrackPickupGroupingPrefix));
        }

        if (capabilityId.Equals(RuntimeCapabilityId.DirectDeliveryEligibility))
        {
            return RequireMethod(nameof(FastTrackDirectDeliveryPrefix));
        }

        throw new ArgumentOutOfRangeException(nameof(capabilityId));
    }

    private static MethodInfo RequireMethod(string name) =>
        typeof(DeliveryTemperatureRuntimePatchPlanTests).GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Missing test method: " + name);

    private static void PreparedPostfix()
    {
    }

    private static void GameSessionLifecycleTarget()
    {
    }

    private static void WorldParentTopologyTarget()
    {
    }

    private static void AuthoritativeFetchTemperatureEligibilityTarget()
    {
    }

    private static void WorldInventoryTarget()
    {
    }

    private static void TemperatureStatusAvailabilityTarget()
    {
    }

    private static void PickupGroupingTarget()
    {
    }

    private static void DirectDeliveryEligibilityTarget()
    {
    }

    private static void UnknownModTarget()
    {
    }

    private static void ObservingPrefix()
    {
    }

    private static bool UnknownSkippingPrefix() => false;

    private static bool FastTrackWorldInventoryPrefix() => false;

    private static bool FastTrackPickupGroupingPrefix() => false;

    private static bool ChangedFastTrackPickupGroupingPrefix() => false;

    private static bool FastTrackDirectDeliveryPrefix() => false;
}
