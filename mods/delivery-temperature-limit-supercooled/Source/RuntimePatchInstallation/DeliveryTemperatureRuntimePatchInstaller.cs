#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Verifies the complete selected runtime contract before applying any
    /// selected patch, then owns exact-method rollback and game-load authority.
    /// </summary>
    internal static class DeliveryTemperatureRuntimePatchInstaller
    {
        internal const string HarmonyOwner =
            "MaksymShostak.DeliveryTemperatureLimit";

        private static readonly object InstallationSynchronization = new object();

        private static readonly RuntimeCapabilityId
            GameSessionLifecycleCapabilityId =
                new RuntimeCapabilityId("game-session-lifecycle");
        private static readonly RuntimeCapabilityId
            WorldParentTopologyCapabilityId =
                new RuntimeCapabilityId("world-parent-topology");
        private static readonly RuntimeCapabilityId
            AuthoritativeFetchTemperatureEligibilityCapabilityId =
                new RuntimeCapabilityId(
                    "authoritative-fetch-temperature-eligibility");

        private static readonly RuntimePatchGroupId
            GameSessionLifecyclePatchGroupId =
                new RuntimePatchGroupId("game-session-lifecycle");
        private static readonly RuntimePatchGroupId
            WorldParentTopologyPatchGroupId =
                new RuntimePatchGroupId("world-parent-topology");
        private static readonly RuntimePatchGroupId
            KleiAuthoritativeFetchTemperatureEligibilityPatchGroupId =
                new RuntimePatchGroupId(
                    "klei-authoritative-fetch-temperature-eligibility");
        private static readonly RuntimePatchGroupId
            KleiWorldInventoryTemperaturePublicationPatchGroupId =
                new RuntimePatchGroupId(
                    "klei-world-inventory-temperature-publication");
        private static readonly RuntimePatchGroupId
            TemperatureStatusAvailabilityPatchGroupId =
                new RuntimePatchGroupId("temperature-status-availability");
        private static readonly RuntimePatchGroupId
            KleiPickupTemperatureGroupingPatchGroupId =
                new RuntimePatchGroupId("klei-pickup-temperature-grouping");
        private static readonly RuntimePatchGroupId
            KleiDirectDeliveryEligibilityPatchGroupId =
                new RuntimePatchGroupId("klei-direct-delivery-eligibility");

        private static RuntimePatchInstallerState runtimeInstallerState =
            RuntimePatchInstallerState.NotStarted;
        private static bool topologyIndependentPatchesInstalled;
        private static DeliveryTemperatureRuntimePatchPlan? installedPatchPlan;
        private static Harmony? installedHarmony;
        private static WeakReference<Game>? mostRecentlyEvaluatedGameLoad;
        private static bool mostRecentGameLoadWasAuthorized;

        internal static SupportRuntimeSnapshot CaptureSupportReportSnapshot()
        {
            lock (InstallationSynchronization)
            {
                string installationState = runtimeInstallerState.ToString();
                return installedPatchPlan != null
                    ? installedPatchPlan.CreateSupportReportSnapshot(
                        installationState)
                    : SupportRuntimeSnapshot.Unavailable(
                        installationState,
                        "No verified runtime patch plan was published.");
            }
        }

        internal static void InstallLoadedModTopologyIndependentPatches(
            Harmony harmony)
        {
            ValidateHarmonyOwner(harmony);
            lock (InstallationSynchronization)
            {
                if (topologyIndependentPatchesInstalled)
                {
                    return;
                }

                HarmonyPatchContractBindingVerifier.VerifiedBindings
                    preparedPatches =
                    PrepareLoadedModTopologyIndependentPatches();
                ApplyPreparedPatchesWithExactRollback(
                    harmony,
                    preparedPatches);
                topologyIndependentPatchesInstalled = true;
            }
        }

        internal static void InstallLoadedModTopologyDependentPatches(
            Harmony harmony,
            IReadOnlyList<KMod.Mod> loadedMods)
        {
            ValidateHarmonyOwner(harmony);
            if (loadedMods == null)
            {
                throw new ArgumentNullException(nameof(loadedMods));
            }

            lock (InstallationSynchronization)
            {
                switch (runtimeInstallerState)
                {
                    case RuntimePatchInstallerState.Installed:
                        return;
                    case RuntimePatchInstallerState.Verifying:
                        throw new InvalidOperationException(
                            "Delivery Temperature Limit runtime patch installation " +
                            "re-entered while verification was in progress.");
                    case RuntimePatchInstallerState.Failed:
                        throw new InvalidOperationException(
                            "Delivery Temperature Limit runtime patch installation " +
                            "previously failed and cannot be retried against a " +
                            "partially initialized process.");
                    case RuntimePatchInstallerState.NotStarted:
                        runtimeInstallerState =
                            RuntimePatchInstallerState.Verifying;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(runtimeInstallerState),
                            runtimeInstallerState,
                            "Unknown runtime patch installer state.");
                }

                try
                {
                    IReadOnlyList<ActiveHarmonyPrefixDescriptor>
                        startupActivePrefixes =
                            CollectActiveHarmonyPrefixDescriptors();
                    LoadedModInspectionContext loadedModInspectionContext =
                        CreateLoadedModInspectionContext(
                            loadedMods,
                            startupActivePrefixes);
                    var declaredIntegrationCatalog =
                        new DeclaredModIntegrationCatalog(new[]
                        {
                            FastTrackRuntimeAuthorityIntegrationInspector
                                .DeclaredIntegrationDescriptor
                        });
                    var fastTrackRuntimeAuthorityInspector =
                        new FastTrackRuntimeAuthorityIntegrationInspector(
                            new FastTrackCompatibilityInspector(
                                new FastTrackAssemblyFileIdentityReader(),
                                FastTrackSupportedAssemblyBuildCatalog.Declared),
                            new FastTrackRuntimeAuthorityContributionBuilder());
                    DeclaredIntegrationPreparationResult
                        declaredIntegrationPreparation =
                            DeclaredExternalModIntegrationPreparation.Prepare(
                                declaredIntegrationCatalog,
                                loadedModInspectionContext,
                                new IRuntimeAuthorityIntegrationInspector[]
                                {
                                    fastTrackRuntimeAuthorityInspector
                                },
                                Array.Empty<
                                    IAdditiveInteroperabilityInspector>());
                    IReadOnlyList<RuntimeCapabilityDefinition>
                        runtimeCapabilityDefinitions =
                            CreateRuntimeCapabilityDefinitions();
                    RuntimePatchCapabilitySelection capabilitySelection =
                        RuntimePatchCapabilitySelector.Select(
                            runtimeCapabilityDefinitions,
                            declaredIntegrationPreparation
                                .RuntimeAuthorityContributions,
                            declaredIntegrationPreparation
                                .ExternalModIntegrationOutcomes);
                    DeliveryTemperatureRuntimePatchPlan patchPlan =
                        DeliveryTemperatureRuntimePatchPlan.Create(
                            DeliveryTemperatureLimitOptions.Instance
                                .CheckTemperatureForStatusItems,
                            capabilitySelection);

                    // This is the first authority pass. The plan owns the owner
                    // decision; the installer supplies only immutable descriptors.
                    patchPlan.VerifySelectedAuthority(startupActivePrefixes);
                    HarmonyPatchContractBindingVerifier.VerifiedBindings
                        preparedPatches =
                        PrepareSelectedRuntimePatches(patchPlan);

                    // Every target, member, IL anchor, Harmony argument binding,
                    // and owner has now been verified. Only this point may mutate
                    // Harmony.
                    ApplyPreparedPatchesWithExactRollback(
                        harmony,
                        preparedPatches);
                    installedPatchPlan = patchPlan;
                    installedHarmony = harmony;
                    mostRecentlyEvaluatedGameLoad = null;
                    runtimeInstallerState = RuntimePatchInstallerState.Installed;

                    if (patchPlan.StatusCompatibilityDiagnostic != null)
                    {
                        DeliveryTemperatureSupportReporter.Record(
                            "DTL-STATUS-COMPATIBILITY-DEGRADED",
                            SupportDiagnosticSeverity.Error,
                            "Delivery Temperature Limit: " +
                            patchPlan.StatusCompatibilityDiagnostic);
                    }
                }
                catch
                {
                    installedPatchPlan = null;
                    installedHarmony = null;
                    mostRecentlyEvaluatedGameLoad = null;
                    runtimeInstallerState = RuntimePatchInstallerState.Failed;
                    throw;
                }
            }
        }

        internal static bool TryStartAuthorizedGameSession(Game game)
        {
            if (game == null)
            {
                return false;
            }

            lock (InstallationSynchronization)
            {
                if (mostRecentlyEvaluatedGameLoad != null &&
                    mostRecentlyEvaluatedGameLoad.TryGetTarget(
                        out Game? evaluatedGame) &&
                    ReferenceEquals(evaluatedGame, game))
                {
                    return mostRecentGameLoadWasAuthorized;
                }

                DeliveryTemperatureRuntimePatchPlan patchPlan =
                    installedPatchPlan ??
                    throw new InvalidOperationException(
                        "Game-load authority was requested before the runtime " +
                        "patch plan was installed.");
                if (runtimeInstallerState != RuntimePatchInstallerState.Installed ||
                    installedHarmony == null)
                {
                    throw new InvalidOperationException(
                        "Game-load authority was requested from an incomplete " +
                        "runtime patch installation.");
                }

                IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes =
                    CollectActiveHarmonyPrefixDescriptors();
                try
                {
                    patchPlan.VerifySelectedAuthority(activePrefixes);
                }
                catch (HarmonyPatchContractViolationException exception)
                {
                    CacheGameLoadAuthorityOutcome(game, wasAuthorized: false);
                    DeliveryTemperatureSupportReporter.Record(
                        "DTL-GAME-LOAD-AUTHORITY-REJECTED",
                        SupportDiagnosticSeverity.Error,
                        "Delivery Temperature Limit rejected this game load " +
                        "because selected patch authority changed. No game " +
                        "session or fallback was published.",
                        exception);
                    return false;
                }

                // EnsureGameSession is intentionally reachable only after the
                // selected-owner verification above has returned successfully.
                _ = DeliveryTemperatureGameSessionHost.EnsureGameSession(
                    game.GetInstanceID());
                CacheGameLoadAuthorityOutcome(game, wasAuthorized: true);
                return true;
            }
        }

        internal static IReadOnlyList<ActiveHarmonyPrefixDescriptor>
            CollectActiveHarmonyPrefixDescriptors()
        {
            var descriptors = new List<ActiveHarmonyPrefixDescriptor>();
            foreach (MethodBase targetMethod in Harmony.GetAllPatchedMethods())
            {
                Patches? patchInformation = Harmony.GetPatchInfo(targetMethod);
                if (patchInformation == null)
                {
                    continue;
                }

                foreach (Patch prefix in patchInformation.Prefixes)
                {
                    MethodInfo? patchMethod = prefix.PatchMethod;
                    if (patchMethod == null)
                    {
                        throw new HarmonyPatchContractViolationException(
                            "Harmony reported an active prefix without a patch " +
                            "method for target " +
                            GetMethodDisplayName(targetMethod) +
                            ".");
                    }

                    descriptors.Add(new ActiveHarmonyPrefixDescriptor(
                        targetMethod,
                        patchMethod,
                        prefix.owner,
                        prefix.priority));
                }
            }

            return descriptors.AsReadOnly();
        }

        private static LoadedModInspectionContext
            CreateLoadedModInspectionContext(
                IReadOnlyList<KMod.Mod> loadedMods,
                IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            var loadedModCandidates = new List<LoadedModCandidate>(
                loadedMods.Count);
            for (int modIndex = 0; modIndex < loadedMods.Count; modIndex++)
            {
                KMod.Mod? loadedMod = loadedMods[modIndex];
                if (loadedMod == null ||
                    string.IsNullOrWhiteSpace(loadedMod.staticID))
                {
                    continue;
                }

                var loadedAssemblies = new List<Assembly>();
                if (loadedMod.loaded_mod_data?.dlls != null)
                {
                    foreach (Assembly loadedAssembly in
                             loadedMod.loaded_mod_data.dlls)
                    {
                        loadedAssemblies.Add(loadedAssembly);
                    }
                }

                loadedModCandidates.Add(new LoadedModCandidate(
                    loadedMod.IsActive(),
                    loadedMod.staticID,
                    loadedAssemblies));
            }

            return new LoadedModInspectionContext(
                loadedModCandidates,
                activePrefixes);
        }

        private static IReadOnlyList<RuntimeCapabilityDefinition>
            CreateRuntimeCapabilityDefinitions()
        {
            var definitions = new List<RuntimeCapabilityDefinition>(7)
            {
                CreateBuiltInRuntimeCapabilityDefinition(
                    GameSessionLifecycleCapabilityId,
                    RuntimeCapabilityCriticality.Required,
                    GameSessionLifecyclePatchGroupId,
                    PrepareGameSessionLifecyclePatches),
                CreateBuiltInRuntimeCapabilityDefinition(
                    WorldParentTopologyCapabilityId,
                    RuntimeCapabilityCriticality.Required,
                    WorldParentTopologyPatchGroupId,
                    PrepareWorldParentTopologyPatches),
                CreateBuiltInRuntimeCapabilityDefinition(
                    AuthoritativeFetchTemperatureEligibilityCapabilityId,
                    RuntimeCapabilityCriticality.Required,
                    KleiAuthoritativeFetchTemperatureEligibilityPatchGroupId,
                    PrepareKleiAuthoritativeFetchTemperatureEligibilityPatches),
                CreateBuiltInRuntimeCapabilityDefinition(
                    RuntimeCapabilityId
                        .WorldInventoryTemperaturePublication,
                    RuntimeCapabilityCriticality.Optional,
                    KleiWorldInventoryTemperaturePublicationPatchGroupId,
                    PrepareKleiWorldInventoryTemperaturePatches),
                CreateBuiltInRuntimeCapabilityDefinition(
                    RuntimeCapabilityId.TemperatureStatusAvailability,
                    RuntimeCapabilityCriticality.Optional,
                    TemperatureStatusAvailabilityPatchGroupId,
                    PrepareTemperatureStatusAvailabilityPatches),
                CreateBuiltInRuntimeCapabilityDefinition(
                    RuntimeCapabilityId.PickupTemperatureGrouping,
                    RuntimeCapabilityCriticality.Required,
                    KleiPickupTemperatureGroupingPatchGroupId,
                    PrepareKleiPickupTemperatureGroupingPatches),
                CreateBuiltInRuntimeCapabilityDefinition(
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    RuntimeCapabilityCriticality.Required,
                    KleiDirectDeliveryEligibilityPatchGroupId,
                    PrepareKleiDirectDeliveryEligibilityPatches)
            };
            return definitions.AsReadOnly();
        }

        private static RuntimeCapabilityDefinition
            CreateBuiltInRuntimeCapabilityDefinition(
                RuntimeCapabilityId capabilityId,
                RuntimeCapabilityCriticality criticality,
                RuntimePatchGroupId patchGroupId,
                Action<ICollection<HarmonyPatchContractBinding>>
                    preparePatchBindings) =>
            new RuntimeCapabilityDefinition(
                capabilityId,
                criticality,
                () => PrepareKleiBaselineContribution(
                        capabilityId,
                        patchGroupId,
                        preparePatchBindings),
                atomicBundleId: null);

        private static PreparedRuntimeAuthorityContribution
            PrepareKleiBaselineContribution(
                RuntimeCapabilityId capabilityId,
                RuntimePatchGroupId patchGroupId,
                Action<ICollection<HarmonyPatchContractBinding>>
                    preparePatchBindings)
        {
            if (preparePatchBindings == null)
            {
                throw new ArgumentNullException(nameof(preparePatchBindings));
            }

            var bindings = new List<HarmonyPatchContractBinding>();
            preparePatchBindings(bindings);
            HarmonyPatchContractBindingVerifier.VerifiedBindings
                verifiedBindings =
                    HarmonyPatchContractBindingVerifier.VerifyAll(bindings);
            return new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity.KleiBaseline,
                capabilityId,
                new[] { patchGroupId },
                RuntimeAuthorityObservation.OwnsCompatible,
                verifiedBindings,
                CreateKleiOriginalAuthorityRequirements(verifiedBindings),
                diagnosticCode: null,
                diagnosticMessage: null);
        }

        private static IReadOnlyList<RuntimeAuthorityRequirement>
            CreateKleiOriginalAuthorityRequirements(
                IReadOnlyList<HarmonyPatchContractBinding> bindings)
        {
            var requirements = new List<RuntimeAuthorityRequirement>(
                bindings.Count);
            var requiredTargets = new HashSet<MethodBase>();
            for (int bindingIndex = 0;
                 bindingIndex < bindings.Count;
                 bindingIndex++)
            {
                MethodBase targetMethod = bindings[bindingIndex].TargetMethod;
                if (!requiredTargets.Add(targetMethod))
                {
                    continue;
                }

                requirements.Add(new RuntimeAuthorityRequirement(
                    targetMethod,
                    RuntimeAuthorityRequirementKind.KleiOriginal,
                    requiredHarmonyOwner: null,
                    requiredPrefixMethod: null,
                    Array.Empty<string>()));
            }

            return requirements.AsReadOnly();
        }

        private static HarmonyPatchContractBindingVerifier.VerifiedBindings
            PrepareLoadedModTopologyIndependentPatches()
        {
            var preparedPatches = new List<HarmonyPatchContractBinding>();

            MethodInfo buildingConfigurationTarget =
                TemperatureLimitedDeliveryTargetPrefabConfigurator
                    .ResolveBuildingConfigurationTarget();
            _ = TemperatureLimitedDeliveryTargetPrefabConfigurator
                .ResolveBuildingConfigurationTableField();
            AddPostfix(
                preparedPatches,
                buildingConfigurationTarget,
                typeof(TemperatureLimitedDeliveryTargetPrefabConfigurator),
                nameof(TemperatureLimitedDeliveryTargetPrefabConfigurator
                    .ConfigureTemperatureLimitedDeliveryTargetPrefabsPostfix));

            AddPostfix(
                preparedPatches,
                ConstructionMaterialTemperatureLimit
                    .ResolveMaterialSelectionPanelPrefabInitializationTarget(),
                typeof(ConstructionMaterialTemperatureLimit),
                nameof(ConstructionMaterialTemperatureLimit
                    .MaterialSelectionPanelPrefabInitializationPostfix));
            AddPostfix(
                preparedPatches,
                ConstructionMaterialTemperatureLimit
                    .ResolveMaterialSelectionPanelConfigurationTarget(),
                typeof(ConstructionMaterialTemperatureLimit),
                nameof(ConstructionMaterialTemperatureLimit
                    .MaterialSelectionPanelConfigurationPostfix));
            AddPostfix(
                preparedPatches,
                ConstructionMaterialTemperatureLimit
                    .ResolveBuildingDefinitionInstantiationTarget(),
                typeof(ConstructionMaterialTemperatureLimit),
                nameof(ConstructionMaterialTemperatureLimit
                    .BuildingDefinitionInstantiationPostfix));
            AddPostfix(
                preparedPatches,
                ConstructionMaterialTemperatureLimit
                    .ResolveBuildingDefinitionPostProcessingTarget(),
                typeof(ConstructionMaterialTemperatureLimit),
                nameof(ConstructionMaterialTemperatureLimit
                    .BuildingDefinitionPostProcessingPostfix));
            _ = ConstructionMaterialTemperatureLimit
                .ResolveDetailsScreenMaterialSelectionPanelField();

            AddPostfix(
                preparedPatches,
                TemperatureLimitSideScreenRegistrationPatches
                    .ResolveDetailsScreenPrefabInitializationTarget(),
                typeof(TemperatureLimitSideScreenRegistrationPatches),
                nameof(TemperatureLimitSideScreenRegistrationPatches
                    .DetailsScreenPrefabInitializationPostfix));
            AddPostfix(
                preparedPatches,
                ComplexFabricatorTemperatureLimitLayoutPatches
                    .ResolveComplexFabricatorSideScreenShowTarget(),
                typeof(ComplexFabricatorTemperatureLimitLayoutPatches),
                nameof(ComplexFabricatorTemperatureLimitLayoutPatches
                    .ComplexFabricatorSideScreenShowPostfix));

            return HarmonyPatchContractBindingVerifier.VerifyAll(
                preparedPatches);
        }

        private static HarmonyPatchContractBindingVerifier.VerifiedBindings
            PrepareSelectedRuntimePatches(
                DeliveryTemperatureRuntimePatchPlan patchPlan)
        {
            return patchPlan.OrderedPatchBindings;
        }

        private static void PrepareGameSessionLifecyclePatches(
            ICollection<HarmonyPatchContractBinding> patches)
        {
            AddPrefix(
                patches,
                DeliveryTemperatureGameLoadAuthorityPatches
                    .ResolveGameOnLoadLevelTarget(),
                typeof(DeliveryTemperatureGameLoadAuthorityPatches),
                nameof(DeliveryTemperatureGameLoadAuthorityPatches
                    .GameOnLoadLevelPrefix));

            MethodInfo destroyInstancesTarget =
                DeliveryTemperatureGameSessionShutdownPatches
                    .ResolveGameDestroyInstancesTarget();
            AddPrefix(
                patches,
                destroyInstancesTarget,
                typeof(DeliveryTemperatureGameSessionShutdownPatches),
                nameof(DeliveryTemperatureGameSessionShutdownPatches
                    .GameDestroyInstancesPrefix));
            AddFinalizer(
                patches,
                destroyInstancesTarget,
                typeof(DeliveryTemperatureGameSessionShutdownPatches),
                nameof(DeliveryTemperatureGameSessionShutdownPatches
                    .GameDestroyInstancesFinalizer));
        }

        private static void PrepareWorldParentTopologyPatches(
            ICollection<HarmonyPatchContractBinding> patches)
        {
            AddPostfix(
                patches,
                WorldParentTopologyPatches
                    .ResolveClusterManagerRegisterWorldContainerTarget(),
                typeof(WorldParentTopologyPatches),
                nameof(WorldParentTopologyPatches
                    .RegisterWorldContainerPostfix));
            AddPrefix(
                patches,
                WorldParentTopologyPatches
                    .ResolveClusterManagerUnregisterWorldContainerTarget(),
                typeof(WorldParentTopologyPatches),
                nameof(WorldParentTopologyPatches
                    .UnregisterWorldContainerPrefix));
            AddPostfix(
                patches,
                WorldParentTopologyPatches
                    .ResolveWorldContainerSetParentIdxTarget(),
                typeof(WorldParentTopologyPatches),
                nameof(WorldParentTopologyPatches.SetParentIdxPostfix));
        }

        private static void
            PrepareKleiAuthoritativeFetchTemperatureEligibilityPatches(
                ICollection<HarmonyPatchContractBinding> patches)
        {
            AddPostfix(
                patches,
                KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .ResolveGlobalChoreProviderAddChoreTarget(),
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .GlobalChoreProviderAddChorePostfix));

            MethodInfo removeChoreTarget =
                KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .ResolveGlobalChoreProviderRemoveChoreTarget();
            AddPrefix(
                patches,
                removeChoreTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .GlobalChoreProviderRemoveChorePrefix));
            AddPostfix(
                patches,
                removeChoreTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .GlobalChoreProviderRemoveChorePostfix));

            MethodInfo tagsChangedTarget =
                KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .ResolveFetchChoreOnTagsChangedTarget();
            AddPrefix(
                patches,
                tagsChangedTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .FetchChoreOnTagsChangedPrefix));
            AddPostfix(
                patches,
                tagsChangedTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .FetchChoreOnTagsChangedPostfix));

            MethodInfo updateTarget =
                KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .ResolveGlobalChoreProviderUpdateStorageFetchableBitsTarget();
            VerifyTranspiler(
                updateTarget,
                KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .UpdateStorageFetchableBitsTranspiler);
            AddPrefix(
                patches,
                updateTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .UpdateStorageFetchableBitsPrefix));
            AddTranspiler(
                patches,
                updateTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .UpdateStorageFetchableBitsTranspiler));
            AddPostfix(
                patches,
                updateTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .UpdateStorageFetchableBitsPostfix));
            AddFinalizer(
                patches,
                updateTarget,
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .UpdateStorageFetchableBitsFinalizer));

            AddPostfix(
                patches,
                KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .ResolveGlobalChoreProviderClearableHasDestinationTarget(),
                typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                nameof(KleiAuthoritativeFetchTemperatureEligibilityPatches
                    .ClearableHasDestinationPostfix));
        }

        private static void PrepareKleiWorldInventoryTemperaturePatches(
            ICollection<HarmonyPatchContractBinding> patches)
        {
            MethodInfo target = KleiWorldInventoryTemperaturePatches
                .ResolveWorldInventoryUpdateTarget();
            VerifyTranspiler(
                target,
                KleiWorldInventoryTemperaturePatches
                    .WorldInventoryUpdateTranspiler);
            AddPrefix(
                patches,
                target,
                typeof(KleiWorldInventoryTemperaturePatches),
                nameof(KleiWorldInventoryTemperaturePatches
                    .WorldInventoryUpdatePrefix));
            AddTranspiler(
                patches,
                target,
                typeof(KleiWorldInventoryTemperaturePatches),
                nameof(KleiWorldInventoryTemperaturePatches
                    .WorldInventoryUpdateTranspiler));
            AddPostfix(
                patches,
                target,
                typeof(KleiWorldInventoryTemperaturePatches),
                nameof(KleiWorldInventoryTemperaturePatches
                    .WorldInventoryUpdatePostfix));
            AddFinalizer(
                patches,
                target,
                typeof(KleiWorldInventoryTemperaturePatches),
                nameof(KleiWorldInventoryTemperaturePatches
                    .WorldInventoryUpdateFinalizer));
        }

        private static void PrepareTemperatureStatusAvailabilityPatches(
            ICollection<HarmonyPatchContractBinding> patches)
        {
            MethodInfo target = TemperatureStatusAvailabilityPatches
                .ResolveFetchListStatusItemUpdaterRender200msTarget();
            VerifyTranspiler(
                target,
                TemperatureStatusAvailabilityPatches.Render200msTranspiler);
            AddTranspiler(
                patches,
                target,
                typeof(TemperatureStatusAvailabilityPatches),
                nameof(TemperatureStatusAvailabilityPatches
                    .Render200msTranspiler));
        }

        private static void PrepareKleiPickupTemperatureGroupingPatches(
            ICollection<HarmonyPatchContractBinding> patches)
        {
            KleiPickupTemperatureGroupingPatches
                .VerifyKleiPickupGroupingPatchContracts();
            MethodInfo updateTarget = KleiPickupTemperatureGroupingPatches
                .ResolveFetchablesByPrefabIdUpdatePickupsTarget();
            AddPrefix(
                patches,
                updateTarget,
                typeof(KleiPickupTemperatureGroupingPatches),
                nameof(KleiPickupTemperatureGroupingPatches.UpdatePickupsPrefix));
            AddTranspiler(
                patches,
                updateTarget,
                typeof(KleiPickupTemperatureGroupingPatches),
                nameof(KleiPickupTemperatureGroupingPatches
                    .UpdatePickupsTranspiler));
            AddPostfix(
                patches,
                updateTarget,
                typeof(KleiPickupTemperatureGroupingPatches),
                nameof(KleiPickupTemperatureGroupingPatches.UpdatePickupsPostfix));
            AddFinalizer(
                patches,
                updateTarget,
                typeof(KleiPickupTemperatureGroupingPatches),
                nameof(KleiPickupTemperatureGroupingPatches
                    .UpdatePickupsFinalizer));
            AddTranspiler(
                patches,
                KleiPickupTemperatureGroupingPatches
                    .ResolvePickupComparerIncludingPriorityCompareTarget(),
                typeof(KleiPickupTemperatureGroupingPatches),
                nameof(KleiPickupTemperatureGroupingPatches
                    .PickupComparerTranspiler));
        }

        private static void PrepareKleiDirectDeliveryEligibilityPatches(
            ICollection<HarmonyPatchContractBinding> patches)
        {
            KleiDirectDeliveryEligibilityPatches
                .VerifyKleiDirectDeliveryEligibilityPatchContracts();
            AddPostfix(
                patches,
                KleiDirectDeliveryEligibilityPatches
                    .ResolveFetchManagerIsFetchablePickupTarget(),
                typeof(KleiDirectDeliveryEligibilityPatches),
                nameof(KleiDirectDeliveryEligibilityPatches
                    .IsFetchablePickupPostfix));
            AddTranspiler(
                patches,
                KleiDirectDeliveryEligibilityPatches
                    .ResolveClearableManagerCollectChoresTarget(),
                typeof(KleiDirectDeliveryEligibilityPatches),
                nameof(KleiDirectDeliveryEligibilityPatches
                    .ClearableManagerCollectChoresTranspiler));
            AddTranspiler(
                patches,
                KleiDirectDeliveryEligibilityPatches
                    .ResolveFetchAreaChoreStatesInstanceBeginTarget(),
                typeof(KleiDirectDeliveryEligibilityPatches),
                nameof(KleiDirectDeliveryEligibilityPatches
                    .FetchAreaChoreBeginTranspiler));
            AddTranspiler(
                patches,
                KleiDirectDeliveryEligibilityPatches
                    .ResolveFetchAreaChoreCandidateDelegateTarget(),
                typeof(KleiDirectDeliveryEligibilityPatches),
                nameof(KleiDirectDeliveryEligibilityPatches
                    .FetchAreaCandidateDelegateTranspiler));
        }

        private static void VerifyTranspiler(
            MethodInfo targetMethod,
            Func<IEnumerable<CodeInstruction>,
                System.Reflection.Emit.ILGenerator,
                IEnumerable<CodeInstruction>> transpiler)
        {
            System.Reflection.Emit.ILGenerator generator;
            List<CodeInstruction> instructions =
                PatchProcessor.GetOriginalInstructions(
                    targetMethod,
                    out generator);
            _ = new List<CodeInstruction>(
                transpiler(instructions, generator));
        }

        private static void VerifyTranspiler(
            MethodInfo targetMethod,
            Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>
                transpiler)
        {
            System.Reflection.Emit.ILGenerator generator;
            List<CodeInstruction> instructions =
                PatchProcessor.GetOriginalInstructions(
                    targetMethod,
                    out generator);
            _ = generator;
            _ = new List<CodeInstruction>(transpiler(instructions));
        }

        private static void AddPrefix(
            ICollection<HarmonyPatchContractBinding> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Prefix);

        private static void AddPostfix(
            ICollection<HarmonyPatchContractBinding> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Postfix);

        private static void AddTranspiler(
            ICollection<HarmonyPatchContractBinding> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Transpiler);

        private static void AddFinalizer(
            ICollection<HarmonyPatchContractBinding> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Finalizer);

        private static void AddPreparedPatch(
            ICollection<HarmonyPatchContractBinding> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName,
            HarmonyPatchContractKind patchKind)
        {
            MethodInfo patchMethod = HarmonyPatchContractVerifier
                .RequireSingleMatch(
                    patchDeclaringType.GetMethods(
                        BindingFlags.DeclaredOnly |
                        BindingFlags.Static |
                        BindingFlags.NonPublic),
                    candidate => string.Equals(
                        candidate.Name,
                        patchMethodName,
                        StringComparison.Ordinal),
                    patchDeclaringType.FullName + "." + patchMethodName);
            patches.Add(new HarmonyPatchContractBinding(
                targetMethod,
                patchMethod,
                patchKind));
        }

        private static void ApplyPreparedPatchesWithExactRollback(
            Harmony harmony,
            HarmonyPatchContractBindingVerifier.VerifiedBindings
                preparedPatches)
        {
            var installedPatches = new List<InstalledHarmonyPatch>(
                preparedPatches.Count);
            try
            {
                for (int patchIndex = 0;
                     patchIndex < preparedPatches.Count;
                     patchIndex++)
                {
                    HarmonyPatchContractBinding preparedPatch =
                        preparedPatches[patchIndex];
                    var harmonyMethod = new HarmonyMethod(
                        preparedPatch.PatchMethod);
                    switch (preparedPatch.PatchKind)
                    {
                        case HarmonyPatchContractKind.Prefix:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                prefix: harmonyMethod);
                            break;
                        case HarmonyPatchContractKind.Postfix:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                postfix: harmonyMethod);
                            break;
                        case HarmonyPatchContractKind.Transpiler:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                transpiler: harmonyMethod);
                            break;
                        case HarmonyPatchContractKind.Finalizer:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                finalizer: harmonyMethod);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(preparedPatch.PatchKind),
                                preparedPatch.PatchKind,
                                "Unknown prepared Harmony patch kind.");
                    }

                    installedPatches.Add(new InstalledHarmonyPatch(
                        preparedPatch.TargetMethod,
                        preparedPatch.PatchMethod));
                }
            }
            catch (Exception installationException)
            {
                RollBackExactInstalledMethods(harmony, installedPatches);
                if (installationException is
                    HarmonyPatchContractViolationException)
                {
                    throw;
                }

                throw new HarmonyPatchContractViolationException(
                    "Delivery Temperature Limit patch application failed after " +
                    "verification. Only exact methods installed by this attempt " +
                    "were removed.",
                    installationException);
            }
        }

        private static void RollBackExactInstalledMethods(
            Harmony harmony,
            IReadOnlyList<InstalledHarmonyPatch> installedPatches)
        {
            for (int patchIndex = installedPatches.Count - 1;
                 patchIndex >= 0;
                 patchIndex--)
            {
                InstalledHarmonyPatch installedPatch =
                    installedPatches[patchIndex];
                harmony.Unpatch(
                    installedPatch.TargetMethod,
                    installedPatch.PatchMethod);
            }
        }

        private static void CacheGameLoadAuthorityOutcome(
            Game game,
            bool wasAuthorized)
        {
            mostRecentlyEvaluatedGameLoad = new WeakReference<Game>(game);
            mostRecentGameLoadWasAuthorized = wasAuthorized;
        }

        private static void ValidateHarmonyOwner(Harmony harmony)
        {
            if (harmony == null)
            {
                throw new ArgumentNullException(nameof(harmony));
            }

            if (!string.Equals(
                    harmony.Id,
                    HarmonyOwner,
                    StringComparison.Ordinal))
            {
                throw new HarmonyPatchContractViolationException(
                    "Delivery Temperature Limit requires Harmony owner '" +
                    HarmonyOwner +
                    "', but ONI supplied '" +
                    harmony.Id +
                    "'.");
            }
        }

        private static string GetMethodDisplayName(MethodBase method) =>
            (method.DeclaringType?.FullName ?? "<unknown-type>") +
            "." +
            method.Name;

        private enum RuntimePatchInstallerState
        {
            NotStarted,
            Verifying,
            Installed,
            Failed
        }

        private sealed class InstalledHarmonyPatch
        {
            internal InstalledHarmonyPatch(
                MethodBase targetMethod,
                MethodInfo patchMethod)
            {
                TargetMethod = targetMethod;
                PatchMethod = patchMethod;
            }

            internal MethodBase TargetMethod { get; }

            internal MethodInfo PatchMethod { get; }
        }
    }
}
