using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

[TestClass]
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

    [TestMethod]
    public void Create_WhenCapabilitySelectionIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                capabilitySelection: null!));
    }

    [TestMethod]
    public void Create_WhenKleiBaselinesAreSelected_PreservesCompleteContributionOrder()
    {
        RuntimePatchCapabilitySelection selection = CreateSelection();

        DeliveryTemperatureRuntimePatchPlan plan =
            DeliveryTemperatureRuntimePatchPlan.Create(
                checkTemperatureForStatusItems: true,
                selection);

        AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility");
        Assert.AreEqual(7, plan.SelectedContributions.Count);
        Assert.AreEqual(7, plan.OrderedPatchBindings.Count);
        Assert.AreEqual(7, plan.AuthorityRequirements.Count);
        Assert.IsTrue(plan.SelectedContributions.All(
            contribution => contribution.ImplementationIdentity.IsKleiBaseline));
        Assert.IsNull(plan.StatusCompatibilityDiagnostic);
        Assert.AreSame(
            selection.ExternalModIntegrationOutcomes[0],
            plan.ExternalModIntegrationOutcomes[0]);
    }

    [TestMethod]
    public void Create_WhenFastTrackWorldInventoryIsSelected_UsesItsCompleteContribution()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            worldInventoryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "fast-track-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility");
        AssertSelectedImplementation(
            plan,
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
    }

    [TestMethod]
    public void Create_WhenFastTrackPickupGroupingIsSelected_UsesItsCompleteContribution()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "fast-track-pickup-temperature-grouping",
            "klei-direct-delivery-eligibility");
        AssertSelectedImplementation(
            plan,
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
    }

    [TestMethod]
    public void Create_WhenFastTrackDirectDeliveryIsSelected_UsesItsCompleteContribution()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true,
            directDeliveryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "klei-world-inventory-temperature-publication",
            "temperature-status-availability",
            "klei-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility");
        AssertSelectedImplementation(
            plan,
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
    }

    [TestMethod]
    public void Create_WhenStatusOptionIsDisabled_OmitsWorldPublicationAndStatusContributions()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            worldInventoryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible,
            directDeliveryAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "fast-track-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility");
        Assert.IsFalse(plan.SelectedContributions.Any(contribution =>
            contribution.CapabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication) ||
            contribution.CapabilityId.Equals(
                RuntimeCapabilityId.TemperatureStatusAvailability)));
        Assert.IsNull(plan.StatusCompatibilityDiagnostic);
    }

    [TestMethod]
    public void Create_WhenStatusOptionIsDisabled_DoesNotPrepareOmittedKleiContributions()
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

        Assert.HasCount(0, plan.SelectedContributions);
        Assert.AreEqual(0, worldInventoryPreparationCount);
        Assert.AreEqual(0, statusAvailabilityPreparationCount);
    }

    [TestMethod]
    public void Create_WhenWorldInventoryAuthorityIsIncompatible_OmitsOnlyPairedStatusResponsibilities()
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

        AssertPatchGroupIds(
            plan,
            "game-session-lifecycle",
            "world-parent-topology",
            "klei-authoritative-fetch-temperature-eligibility",
            "fast-track-pickup-temperature-grouping",
            "fast-track-direct-delivery-eligibility");
        Assert.IsNotNull(plan.StatusCompatibilityDiagnostic);
        StringAssert.Contains(
            plan.StatusCompatibilityDiagnostic,
            RuntimeCapabilityId.WorldInventoryTemperaturePublication.Value);
        StringAssert.Contains(
            plan.StatusCompatibilityDiagnostic,
            DiagnosticCode(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication));
        StringAssert.Contains(
            plan.StatusCompatibilityDiagnostic,
            "existing ONI status availability remains unchanged");
        ExternalModIntegrationCapabilityOutcome worldOutcome = selection
            .ExternalModIntegrationOutcomes[0]
            .Capabilities.Single(capability => capability.CapabilityId.Equals(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication));
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            worldOutcome.Disposition);
    }

    [TestMethod]
    public void Select_WhenRequiredPickupAuthorityIsIncompatible_BlocksBeforePlanCreation()
    {
        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                CreateSelection(
                    pickupGroupingAuthority:
                        RuntimeAuthorityObservation.OwnsIncompatible));

        AssertRequiredCapabilityBlocked(
            exception,
            RuntimeCapabilityId.PickupTemperatureGrouping);
    }

    [TestMethod]
    public void Select_WhenRequiredDirectDeliveryAuthorityIsIncompatible_BlocksBeforePlanCreation()
    {
        RuntimeCapabilitySelectionException exception =
            Assert.ThrowsExactly<RuntimeCapabilitySelectionException>(() =>
                CreateSelection(
                    directDeliveryAuthority:
                        RuntimeAuthorityObservation.OwnsIncompatible));

        AssertRequiredCapabilityBlocked(
            exception,
            RuntimeCapabilityId.DirectDeliveryEligibility);
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenKleiTargetHasOnlyNonSkippingObserver_ReturnsNormally()
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

        Assert.AreEqual(0, plan.ExternalModIntegrationOutcomes.Count);
    }

    [TestMethod]
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

    [TestMethod]
    public void VerifySelectedAuthority_WhenSelectedExternalPrefixMethodChanges_ThrowsAffectedGroupDiagnostic()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            pickupGroupingAuthority:
                RuntimeAuthorityObservation.OwnsCompatible);

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPrefixDescriptor(
                        RequireMethod(nameof(PickupGroupingTarget)),
                        RequireMethod(nameof(ChangedFastTrackPickupGroupingPrefix)),
                        FastTrackHarmonyOwner,
                        priority: 800)
                ]));

        StringAssert.Contains(
            exception.Message,
            "fast-track-pickup-temperature-grouping");
        StringAssert.Contains(exception.Message, "FastTrackPickupGroupingPrefix");
        StringAssert.Contains(exception.Message, FastTrackHarmonyOwner);
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenUnknownModSkipsUnselectedTarget_DoesNotInterfereOrCreateOutcome()
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

        Assert.AreEqual(0, plan.ExternalModIntegrationOutcomes.Count);
    }

    [TestMethod]
    public void VerifySelectedAuthority_WhenUndeclaredOwnerSkipsSelectedTarget_RejectsWithoutCreatingOutcome()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: false,
            includeDeclaredIntegrationOutcome: false);

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                plan.VerifySelectedAuthority(
                [
                    new ActiveHarmonyPrefixDescriptor(
                        RequireMethod(nameof(PickupGroupingTarget)),
                        RequireMethod(nameof(UnknownSkippingPrefix)),
                        "Unknown.UndeclaredReplacement",
                        priority: 900)
                ]));

        Assert.AreEqual(0, plan.ExternalModIntegrationOutcomes.Count);
        StringAssert.Contains(
            exception.Message,
            "klei-pickup-temperature-grouping");
        StringAssert.Contains(
            exception.Message,
            "Unknown.UndeclaredReplacement");
    }

    [TestMethod]
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

    [TestMethod]
    public void CreateSupportReportSnapshot_WhenSelectionIsGeneric_PreservesAuditIdsAndSanitizedOutcomeFacts()
    {
        DeliveryTemperatureRuntimePatchPlan plan = CreatePlan(
            checkTemperatureForStatusItems: true);

        SupportRuntimeSnapshot snapshot =
            plan.CreateSupportReportSnapshot("Installed");

        Assert.AreEqual("available", snapshot.State);
        Assert.AreEqual("Installed", snapshot.InstallationState);
        CollectionAssert.AreEqual(
            plan.OrderedPatchGroupIds.Select(group => group.Value).ToArray(),
            snapshot.SelectedPatchGroups.ToArray());
        Assert.HasCount(1, snapshot.ExternalModIntegrations);
        SupportExternalModIntegrationSnapshot fastTrack =
            snapshot.ExternalModIntegrations[0];
        Assert.AreEqual("fast-track", fastTrack.IntegrationId);
        Assert.AreEqual("not-matched", fastTrack.MatchState);
        Assert.AreEqual("unavailable", fastTrack.AssemblyIdentity.State);
        CollectionAssert.AreEqual(
            new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication.Value,
                RuntimeCapabilityId.PickupTemperatureGrouping.Value,
                RuntimeCapabilityId.DirectDeliveryEligibility.Value
            },
            fastTrack.Capabilities
                .Select(capability => capability.CapabilityId)
                .ToArray());
    }

    [TestMethod]
    public void CreateSupportReportSnapshot_WhenStatusOptionDisablesCompatibleExternalWorldCapability_ReportsReadyDisposition()
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

        Assert.AreEqual("ready", worldCapability.Disposition);
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

    private static void AssertPatchGroupIds(
        DeliveryTemperatureRuntimePatchPlan plan,
        params string[] expectedGroupIds) =>
        Assert.AreSequenceEqual(
            expectedGroupIds,
            plan.OrderedPatchGroupIds.Select(group => group.Value));

    private static void AssertSelectedImplementation(
        DeliveryTemperatureRuntimePatchPlan plan,
        RuntimeCapabilityId capabilityId,
        RuntimeAuthorityImplementationKind expectedKind)
    {
        PreparedRuntimeAuthorityContribution contribution =
            plan.SelectedContributions.Single(candidate =>
                candidate.CapabilityId.Equals(capabilityId));
        Assert.AreEqual(expectedKind, contribution.ImplementationIdentity.Kind);
        if (expectedKind ==
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration)
        {
            Assert.AreEqual(
                FastTrackIntegrationId,
                contribution.ImplementationIdentity
                    .DeclaredExternalIntegrationId);
        }
    }

    private static void AssertRequiredCapabilityBlocked(
        RuntimeCapabilitySelectionException exception,
        RuntimeCapabilityId expectedCapabilityId)
    {
        Assert.AreEqual(expectedCapabilityId, exception.CapabilityId);
        Assert.AreEqual(
            "required-runtime-capability-unavailable",
            exception.DiagnosticCode);
        ExternalModIntegrationCapabilityOutcome outcome = exception
            .ExternalModIntegrationOutcomes[0]
            .Capabilities.Single(capability =>
                capability.CapabilityId.Equals(expectedCapabilityId));
        Assert.AreEqual(
            IntegrationCapabilityDisposition.ActivationBlocking,
            outcome.Disposition);
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
