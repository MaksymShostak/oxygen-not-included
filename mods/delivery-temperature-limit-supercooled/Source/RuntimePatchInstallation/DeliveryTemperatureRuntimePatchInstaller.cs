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

        private const string FastTrackAssemblySimpleName = "FastTrack";
        private static readonly object InstallationSynchronization = new object();

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

                IReadOnlyList<PreparedHarmonyPatch> preparedPatches =
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
                    IReadOnlyList<ActiveHarmonyPatchDescriptor>
                        startupActivePrefixes =
                            CollectActiveHarmonyPrefixDescriptors();
                    FastTrackLoadedGameInspectionInput inspectionInput =
                        CreateFastTrackInspectionInput(
                            loadedMods,
                            startupActivePrefixes);
                    var compatibilityInspector =
                        new FastTrackCompatibilityInspector(
                            new FastTrackAssemblyFileIdentityReader());
                    FastTrackCompatibilityReport compatibilityReport =
                        compatibilityInspector.Inspect(inspectionInput);
                    DeliveryTemperatureRuntimePatchPlan patchPlan =
                        DeliveryTemperatureRuntimePatchPlan.Create(
                            DeliveryTemperatureLimitOptions.Instance
                                .CheckTemperatureForStatusItems,
                            compatibilityReport);

                    // This is the first authority pass. The plan owns the owner
                    // decision; the installer supplies only immutable descriptors.
                    patchPlan.VerifySelectedAuthority(startupActivePrefixes);
                    IReadOnlyList<PreparedHarmonyPatch> preparedPatches =
                        PrepareSelectedRuntimePatches(
                            patchPlan,
                            compatibilityReport);

                    // Every target, member, IL anchor, optional binding, and owner
                    // has now been verified. Only this point may mutate Harmony.
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

                IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes =
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

        internal static IReadOnlyList<ActiveHarmonyPatchDescriptor>
            CollectActiveHarmonyPrefixDescriptors()
        {
            var descriptors = new List<ActiveHarmonyPatchDescriptor>();
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

                    descriptors.Add(new ActiveHarmonyPatchDescriptor(
                        targetMethod,
                        patchMethod,
                        prefix.owner,
                        prefix.priority));
                }
            }

            return descriptors.AsReadOnly();
        }

        private static FastTrackLoadedGameInspectionInput
            CreateFastTrackInspectionInput(
                IReadOnlyList<KMod.Mod> loadedMods,
                IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes)
        {
            Assembly? activeFastTrackAssembly = null;
            for (int modIndex = 0; modIndex < loadedMods.Count; modIndex++)
            {
                KMod.Mod? loadedMod = loadedMods[modIndex];
                if (loadedMod == null ||
                    !loadedMod.IsActive() ||
                    loadedMod.loaded_mod_data?.dlls == null)
                {
                    continue;
                }

                foreach (Assembly loadedAssembly in
                         loadedMod.loaded_mod_data.dlls)
                {
                    if (!string.Equals(
                            loadedAssembly.GetName().Name,
                            FastTrackAssemblySimpleName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (activeFastTrackAssembly != null &&
                        !ReferenceEquals(
                            activeFastTrackAssembly,
                            loadedAssembly))
                    {
                        throw new HarmonyPatchContractViolationException(
                            "More than one active loaded mod supplied an assembly " +
                            "named FastTrack; compatibility identity is ambiguous.");
                    }

                    activeFastTrackAssembly = loadedAssembly;
                }
            }

            return new FastTrackLoadedGameInspectionInput(
                activeFastTrackAssembly != null,
                activeFastTrackAssembly,
                activePrefixes);
        }

        private static IReadOnlyList<PreparedHarmonyPatch>
            PrepareLoadedModTopologyIndependentPatches()
        {
            var preparedPatches = new List<PreparedHarmonyPatch>();

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

            return preparedPatches.AsReadOnly();
        }

        private static IReadOnlyList<PreparedHarmonyPatch>
            PrepareSelectedRuntimePatches(
                DeliveryTemperatureRuntimePatchPlan patchPlan,
                FastTrackCompatibilityReport compatibilityReport)
        {
            var preparedPatches = new List<PreparedHarmonyPatch>();
            for (int groupIndex = 0;
                 groupIndex < patchPlan.OrderedPatchGroups.Count;
                 groupIndex++)
            {
                DeliveryTemperatureRuntimePatchGroup selectedGroup =
                    patchPlan.OrderedPatchGroups[groupIndex];
                switch (selectedGroup)
                {
                    case DeliveryTemperatureRuntimePatchGroup
                        .GameSessionLifecycle:
                        PrepareGameSessionLifecyclePatches(preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .WorldParentTopology:
                        PrepareWorldParentTopologyPatches(preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .KleiAuthoritativeFetchTemperatureEligibility:
                        PrepareKleiAuthoritativeFetchTemperatureEligibilityPatches(
                            preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .KleiWorldInventoryTemperaturePublication:
                        PrepareKleiWorldInventoryTemperaturePatches(
                            preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .FastTrackWorldInventoryTemperaturePublication:
                        PrepareFastTrackWorldInventoryTemperaturePatches(
                            preparedPatches,
                            compatibilityReport.GetFeature(
                                FastTrackFeature.WorldInventory));
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .TemperatureStatusAvailability:
                        PrepareTemperatureStatusAvailabilityPatches(
                            preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .KleiPickupTemperatureGrouping:
                        PrepareKleiPickupTemperatureGroupingPatches(
                            preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .FastTrackPickupTemperatureGrouping:
                        PrepareFastTrackPickupTemperaturePatches(
                            preparedPatches,
                            compatibilityReport.GetFeature(
                                FastTrackFeature.PickupGrouping));
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .KleiDirectDeliveryEligibility:
                        PrepareKleiDirectDeliveryEligibilityPatches(
                            preparedPatches);
                        break;
                    case DeliveryTemperatureRuntimePatchGroup
                        .FastTrackDirectDeliveryEligibility:
                        PrepareFastTrackDirectDeliveryEligibilityPatches(
                            preparedPatches,
                            compatibilityReport.GetFeature(
                                FastTrackFeature.DirectDeliveryEligibility));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(selectedGroup),
                            selectedGroup,
                            "Unknown selected runtime patch group.");
                }
            }

            return preparedPatches.AsReadOnly();
        }

        private static void PrepareGameSessionLifecyclePatches(
            ICollection<PreparedHarmonyPatch> patches)
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
            ICollection<PreparedHarmonyPatch> patches)
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
                ICollection<PreparedHarmonyPatch> patches)
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
            ICollection<PreparedHarmonyPatch> patches)
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

        private static void PrepareFastTrackWorldInventoryTemperaturePatches(
            ICollection<PreparedHarmonyPatch> patches,
            FastTrackFeatureCompatibility feature)
        {
            FastTrackWorldInventoryTemperaturePatches
                .BindVerifiedWorldInventoryFeature(feature);
            MethodInfo runUpdateTarget =
                FastTrackWorldInventoryTemperaturePatches
                    .ResolveBackgroundWorldInventoryRunUpdateTarget();
            MethodInfo sumTotalTarget =
                FastTrackWorldInventoryTemperaturePatches
                    .ResolveBackgroundWorldInventorySumTotalTarget();
            VerifyTranspiler(
                runUpdateTarget,
                FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdateTranspiler);
            VerifyTranspiler(
                sumTotalTarget,
                FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventorySumTotalTranspiler);

            AddPrefix(
                patches,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdatePrefix));
            AddTranspiler(
                patches,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdateTranspiler));
            AddPostfix(
                patches,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdatePostfix));
            AddFinalizer(
                patches,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdateFinalizer));
            AddTranspiler(
                patches,
                sumTotalTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventorySumTotalTranspiler));
        }

        private static void PrepareTemperatureStatusAvailabilityPatches(
            ICollection<PreparedHarmonyPatch> patches)
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
            ICollection<PreparedHarmonyPatch> patches)
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

        private static void PrepareFastTrackPickupTemperaturePatches(
            ICollection<PreparedHarmonyPatch> patches,
            FastTrackFeatureCompatibility feature)
        {
            FastTrackPickupTemperaturePatches
                .BindVerifiedPickupGroupingFeature(feature);
            FastTrackPickupTemperaturePatches
                .VerifyFastTrackPickupTemperaturePatchContracts();
            MethodInfo updateTarget = FastTrackPickupTemperaturePatches
                .ResolveFetchManagerBeforeUpdatePickupsTarget();
            AddPrefix(
                patches,
                updateTarget,
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .BeforeUpdatePickupsPrefix));
            AddPostfix(
                patches,
                updateTarget,
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .BeforeUpdatePickupsPostfix));
            AddFinalizer(
                patches,
                updateTarget,
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .BeforeUpdatePickupsFinalizer));
            AddTranspiler(
                patches,
                FastTrackPickupTemperaturePatches
                    .ResolvePickupTagDictionaryAddItemTarget(),
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .PickupTagDictionaryAddItemTranspiler));
        }

        private static void PrepareKleiDirectDeliveryEligibilityPatches(
            ICollection<PreparedHarmonyPatch> patches)
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

        private static void PrepareFastTrackDirectDeliveryEligibilityPatches(
            ICollection<PreparedHarmonyPatch> patches,
            FastTrackFeatureCompatibility feature)
        {
            FastTrackDirectDeliveryEligibilityPatches
                .BindVerifiedDirectDeliveryEligibilityFeature(feature);
            FastTrackDirectDeliveryEligibilityPatches
                .VerifyFastTrackDirectDeliveryEligibilityPatchContracts();
            AddTranspiler(
                patches,
                FastTrackDirectDeliveryEligibilityPatches
                    .ResolveChoreComparatorCheckFetchChoreTarget(),
                typeof(FastTrackDirectDeliveryEligibilityPatches),
                nameof(FastTrackDirectDeliveryEligibilityPatches
                    .CheckFetchChoreTranspiler));
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
            ICollection<PreparedHarmonyPatch> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                PreparedHarmonyPatchKind.Prefix);

        private static void AddPostfix(
            ICollection<PreparedHarmonyPatch> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                PreparedHarmonyPatchKind.Postfix);

        private static void AddTranspiler(
            ICollection<PreparedHarmonyPatch> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                PreparedHarmonyPatchKind.Transpiler);

        private static void AddFinalizer(
            ICollection<PreparedHarmonyPatch> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddPreparedPatch(
                patches,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                PreparedHarmonyPatchKind.Finalizer);

        private static void AddPreparedPatch(
            ICollection<PreparedHarmonyPatch> patches,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName,
            PreparedHarmonyPatchKind patchKind)
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
            patches.Add(new PreparedHarmonyPatch(
                targetMethod,
                patchMethod,
                patchKind));
        }

        private static void ApplyPreparedPatchesWithExactRollback(
            Harmony harmony,
            IReadOnlyList<PreparedHarmonyPatch> preparedPatches)
        {
            var installedPatches = new List<InstalledHarmonyPatch>(
                preparedPatches.Count);
            try
            {
                for (int patchIndex = 0;
                     patchIndex < preparedPatches.Count;
                     patchIndex++)
                {
                    PreparedHarmonyPatch preparedPatch =
                        preparedPatches[patchIndex];
                    var harmonyMethod = new HarmonyMethod(
                        preparedPatch.PatchMethod);
                    switch (preparedPatch.PatchKind)
                    {
                        case PreparedHarmonyPatchKind.Prefix:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                prefix: harmonyMethod);
                            break;
                        case PreparedHarmonyPatchKind.Postfix:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                postfix: harmonyMethod);
                            break;
                        case PreparedHarmonyPatchKind.Transpiler:
                            harmony.Patch(
                                preparedPatch.TargetMethod,
                                transpiler: harmonyMethod);
                            break;
                        case PreparedHarmonyPatchKind.Finalizer:
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

        private enum PreparedHarmonyPatchKind
        {
            Prefix,
            Postfix,
            Transpiler,
            Finalizer
        }

        private sealed class PreparedHarmonyPatch
        {
            internal PreparedHarmonyPatch(
                MethodBase targetMethod,
                MethodInfo patchMethod,
                PreparedHarmonyPatchKind patchKind)
            {
                TargetMethod = targetMethod ??
                    throw new ArgumentNullException(nameof(targetMethod));
                PatchMethod = patchMethod ??
                    throw new ArgumentNullException(nameof(patchMethod));
                PatchKind = patchKind;
            }

            internal MethodBase TargetMethod { get; }

            internal MethodInfo PatchMethod { get; }

            internal PreparedHarmonyPatchKind PatchKind { get; }
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
