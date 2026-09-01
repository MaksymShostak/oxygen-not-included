#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns the immutable, responsibility-ordered runtime implementation choice
    /// for one loaded game.
    /// </summary>
    /// <remarks>
    /// Content mode is intentionally absent: base-game and Spaced Out content use
    /// the same selection rules. An active incompatible delivery replacement is
    /// never converted into a Klei fallback because FastTrack would still own and
    /// suppress the original game method.
    /// </remarks>
    internal sealed class DeliveryTemperatureRuntimePatchPlan
    {
        private static readonly DeliveryTemperatureRuntimePatchGroup[]
            ContractOrderedPatchGroups =
            {
                DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle,
                DeliveryTemperatureRuntimePatchGroup.WorldParentTopology,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiAuthoritativeFetchTemperatureEligibility,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiWorldInventoryTemperaturePublication,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackWorldInventoryTemperaturePublication,
                DeliveryTemperatureRuntimePatchGroup
                    .TemperatureStatusAvailability,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiPickupTemperatureGrouping,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackPickupTemperatureGrouping,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiDirectDeliveryEligibility,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackDirectDeliveryEligibility
            };

        private DeliveryTemperatureRuntimePatchPlan(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup>
                orderedPatchGroups,
            string? statusCompatibilityDiagnostic,
            FastTrackCompatibilityReport fastTrackCompatibility)
        {
            OrderedPatchGroups = orderedPatchGroups;
            StatusCompatibilityDiagnostic = statusCompatibilityDiagnostic;
            this.fastTrackCompatibility = fastTrackCompatibility;
        }

        private const string FastTrackHarmonyOwner = "PeterHan.FastTrack";

        private static readonly IReadOnlyCollection<string>
            NoPermittedSkippingPrefixOwners = Array.Empty<string>();

        private static readonly IReadOnlyCollection<string>
            FastTrackPermittedSkippingPrefixOwners =
                new[] { FastTrackHarmonyOwner };

        private readonly FastTrackCompatibilityReport fastTrackCompatibility;

        internal IReadOnlyList<DeliveryTemperatureRuntimePatchGroup>
            OrderedPatchGroups { get; }

        /// <summary>
        /// Explains why optional temperature-aware status integration was omitted
        /// while delivery correctness remained coherent. Null means no status-only
        /// compatibility degradation occurred.
        /// </summary>
        internal string? StatusCompatibilityDiagnostic { get; }

        internal SupportRuntimeSnapshot CreateSupportReportSnapshot(
            string installationState)
        {
            var selectedPatchGroups = new List<string>(
                OrderedPatchGroups.Count);
            for (int index = 0; index < OrderedPatchGroups.Count; index++)
            {
                selectedPatchGroups.Add(OrderedPatchGroups[index].ToString());
            }

            var features = new List<SupportFastTrackFeatureSnapshot>(3);
            FastTrackFeatureCompatibility worldInventory =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.WorldInventory);
            FastTrackFeatureCompatibility pickupGrouping =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.PickupGrouping);
            FastTrackFeatureCompatibility directDelivery =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.DirectDeliveryEligibility);
            features.Add(CreateSupportFeatureSnapshot(worldInventory));
            features.Add(CreateSupportFeatureSnapshot(pickupGrouping));
            features.Add(CreateSupportFeatureSnapshot(directDelivery));

            var fastTrack = new SupportFastTrackSnapshot(
                GetFastTrackSupportState(
                    worldInventory.State,
                    pickupGrouping.State,
                    directDelivery.State),
                CreateOptionalSupportFact(
                    fastTrackCompatibility.AssemblyIdentity,
                    "FastTrack assembly identity was not observed."),
                CreateOptionalSupportFact(
                    fastTrackCompatibility.AssemblyVersion,
                    "FastTrack assembly version was not observed."),
                CreateOptionalSupportFact(
                    fastTrackCompatibility.FileVersion,
                    "FastTrack file version was not available (" +
                    fastTrackCompatibility.AssemblyFileIdentityReadState +
                    ")."),
                CreateOptionalSupportFact(
                    fastTrackCompatibility.AssemblySha256,
                    "FastTrack assembly SHA-256 was not available (" +
                    fastTrackCompatibility.AssemblyFileIdentityReadState +
                    ")."),
                features);

            return SupportRuntimeSnapshot.Available(
                installationState,
                selectedPatchGroups,
                StatusCompatibilityDiagnostic == null
                    ? null
                    : CreateSupportStatusCompatibilityDiagnostic(
                        worldInventory),
                fastTrack);
        }

        internal static DeliveryTemperatureRuntimePatchPlan Create(
            bool checkTemperatureForStatusItems,
            FastTrackCompatibilityReport fastTrackCompatibility)
        {
            if (fastTrackCompatibility == null)
            {
                throw new ArgumentNullException(nameof(fastTrackCompatibility));
            }

            FastTrackFeatureCompatibility worldInventory =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.WorldInventory);
            FastTrackFeatureCompatibility pickupGrouping =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.PickupGrouping);
            FastTrackFeatureCompatibility directDeliveryEligibility =
                fastTrackCompatibility.GetFeature(
                    FastTrackFeature.DirectDeliveryEligibility);

            ThrowWhenActiveDeliveryFeatureIsIncompatible(
                pickupGrouping,
                fastTrackCompatibility);
            ThrowWhenActiveDeliveryFeatureIsIncompatible(
                directDeliveryEligibility,
                fastTrackCompatibility);

            string? statusCompatibilityDiagnostic =
                checkTemperatureForStatusItems &&
                worldInventory.State ==
                    FastTrackFeatureCompatibilityState.Incompatible
                ? CreateStatusCompatibilityDiagnostic(
                    worldInventory,
                    fastTrackCompatibility)
                : null;
            var selectedPatchGroups =
                new List<DeliveryTemperatureRuntimePatchGroup>(
                    ContractOrderedPatchGroups.Length);
            for (var groupIndex = 0;
                 groupIndex < ContractOrderedPatchGroups.Length;
                 groupIndex++)
            {
                DeliveryTemperatureRuntimePatchGroup patchGroup =
                    ContractOrderedPatchGroups[groupIndex];
                if (ShouldSelect(
                        patchGroup,
                        checkTemperatureForStatusItems,
                        worldInventory,
                        pickupGrouping,
                        directDeliveryEligibility))
                {
                    selectedPatchGroups.Add(patchGroup);
                }
            }

            ValidateSelectedResponsibilities(
                selectedPatchGroups,
                checkTemperatureForStatusItems,
                worldInventory.State,
                pickupGrouping.State,
                directDeliveryEligibility.State,
                statusCompatibilityDiagnostic);
            return new DeliveryTemperatureRuntimePatchPlan(
                new ReadOnlyCollection<DeliveryTemperatureRuntimePatchGroup>(
                    selectedPatchGroups),
                statusCompatibilityDiagnostic,
                fastTrackCompatibility);
        }

        private static SupportFastTrackFeatureSnapshot
            CreateSupportFeatureSnapshot(
                FastTrackFeatureCompatibility compatibility) =>
            new SupportFastTrackFeatureSnapshot(
                compatibility.Feature.ToString(),
                GetFeatureSupportState(compatibility.State),
                compatibility.FailureCode?.ToString(),
                compatibility.FailureMessage == null
                    ? null
                    : CreateSupportCompatibilityFailureMessage(
                        compatibility));

        private static string CreateSupportStatusCompatibilityDiagnostic(
            FastTrackFeatureCompatibility compatibility) =>
            "Temperature-aware resource-status integration is disabled for " +
            "this loaded game; existing ONI status availability remains " +
            "unchanged. " +
            CreateSupportCompatibilityFailureMessage(compatibility);

        private static string CreateSupportCompatibilityFailureMessage(
            FastTrackFeatureCompatibility compatibility) =>
            "FastTrack " +
            compatibility.Feature +
            " compatibility verification failed (" +
            compatibility.FailureCode +
            ").";

        private static SupportReportFact CreateOptionalSupportFact(
            object? value,
            string unavailableReason) =>
            value == null
                ? SupportReportFact.Unavailable(unavailableReason)
                : SupportReportFact.Available(
                    value.ToString() ??
                    throw new InvalidOperationException(
                        "An observed FastTrack identity value could not be formatted."));

        private static string GetFastTrackSupportState(
            FastTrackFeatureCompatibilityState worldInventory,
            FastTrackFeatureCompatibilityState pickupGrouping,
            FastTrackFeatureCompatibilityState directDelivery)
        {
            if (worldInventory ==
                    FastTrackFeatureCompatibilityState.ModNotLoaded &&
                pickupGrouping ==
                    FastTrackFeatureCompatibilityState.ModNotLoaded &&
                directDelivery ==
                    FastTrackFeatureCompatibilityState.ModNotLoaded)
            {
                return "not-loaded";
            }

            if (worldInventory ==
                    FastTrackFeatureCompatibilityState.Incompatible ||
                pickupGrouping ==
                    FastTrackFeatureCompatibilityState.Incompatible ||
                directDelivery ==
                    FastTrackFeatureCompatibilityState.Incompatible)
            {
                return "incompatible";
            }

            if (worldInventory == FastTrackFeatureCompatibilityState.Ready ||
                pickupGrouping == FastTrackFeatureCompatibilityState.Ready ||
                directDelivery == FastTrackFeatureCompatibilityState.Ready)
            {
                return "ready";
            }

            return "replacement-inactive";
        }

        private static string GetFeatureSupportState(
            FastTrackFeatureCompatibilityState state)
        {
            switch (state)
            {
                case FastTrackFeatureCompatibilityState.ModNotLoaded:
                    return "mod-not-loaded";
                case FastTrackFeatureCompatibilityState.ReplacementInactive:
                    return "replacement-inactive";
                case FastTrackFeatureCompatibilityState.Ready:
                    return "ready";
                case FastTrackFeatureCompatibilityState.Incompatible:
                    return "incompatible";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state),
                        state,
                        "Unknown FastTrack compatibility state.");
            }
        }

        /// <summary>
        /// Revalidates only the authorities selected for this loaded game. It is
        /// intentionally cold: the runtime installer invokes it once at the game
        /// load boundary, never from inventory, pickup, status, or delivery work.
        /// </summary>
        internal void VerifySelectedAuthority(
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches)
        {
            if (activePatches == null)
            {
                throw new ArgumentNullException(nameof(activePatches));
            }

            for (int patchIndex = 0;
                 patchIndex < activePatches.Count;
                 patchIndex++)
            {
                if (activePatches[patchIndex] == null)
                {
                    throw new ArgumentException(
                        "An active Harmony patch descriptor cannot be null.",
                        nameof(activePatches));
                }
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiAuthoritativeFetchTemperatureEligibility))
            {
                VerifyKleiAuthorityForMatchingTargets(
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiAuthoritativeFetchTemperatureEligibility,
                    activePatches,
                    method => HasMethodContract(
                        method,
                        "GlobalChoreProvider",
                        "UpdateStorageFetchableBits",
                        "System.Void",
                        Array.Empty<string>()));
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiWorldInventoryTemperaturePublication))
            {
                VerifyKleiAuthorityForMatchingTargets(
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiWorldInventoryTemperaturePublication,
                    activePatches,
                    IsWorldInventoryUpdateTarget);
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .FastTrackWorldInventoryTemperaturePublication))
            {
                VerifyFastTrackAuthority(
                    DeliveryTemperatureRuntimePatchGroup
                        .FastTrackWorldInventoryTemperaturePublication,
                    fastTrackCompatibility.GetFeature(
                        FastTrackFeature.WorldInventory),
                    FastTrackVerifiedMember.WorldInventoryReplacementPrefix,
                    activePatches,
                    IsWorldInventoryUpdateTarget);
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiPickupTemperatureGrouping))
            {
                VerifyKleiAuthorityForMatchingTargets(
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiPickupTemperatureGrouping,
                    activePatches,
                    IsPickupUpdateTarget);
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .FastTrackPickupTemperatureGrouping))
            {
                VerifyFastTrackAuthority(
                    DeliveryTemperatureRuntimePatchGroup
                        .FastTrackPickupTemperatureGrouping,
                    fastTrackCompatibility.GetFeature(
                        FastTrackFeature.PickupGrouping),
                    FastTrackVerifiedMember
                        .PickupGroupingBeforeUpdatePickupsPrefix,
                    activePatches,
                    IsPickupUpdateTarget);
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiDirectDeliveryEligibility))
            {
                VerifyKleiAuthorityForMatchingTargets(
                    DeliveryTemperatureRuntimePatchGroup
                        .KleiDirectDeliveryEligibility,
                    activePatches,
                    IsGlobalChoreCollectionTarget);
            }

            if (Contains(
                    OrderedPatchGroups,
                    DeliveryTemperatureRuntimePatchGroup
                        .FastTrackDirectDeliveryEligibility))
            {
                VerifyFastTrackAuthority(
                    DeliveryTemperatureRuntimePatchGroup
                        .FastTrackDirectDeliveryEligibility,
                    fastTrackCompatibility.GetFeature(
                        FastTrackFeature.DirectDeliveryEligibility),
                    FastTrackVerifiedMember
                        .DirectDeliveryEligibilityReplacementPrefix,
                    activePatches,
                    IsGlobalChoreCollectionTarget);
            }
        }

        private static void VerifyKleiAuthorityForMatchingTargets(
            DeliveryTemperatureRuntimePatchGroup selectedGroup,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches,
            Func<MethodBase, bool> targetContract)
        {
            var verifiedTargets = new HashSet<MethodBase>();
            for (int patchIndex = 0;
                 patchIndex < activePatches.Count;
                 patchIndex++)
            {
                ActiveHarmonyPatchDescriptor patch = activePatches[patchIndex];
                MethodBase targetMethod = patch.TargetMethod;
                if (!targetContract(targetMethod) ||
                    !verifiedTargets.Add(targetMethod))
                {
                    continue;
                }

                if (HarmonyPatchContractVerifier.VerifyKleiAuthority(
                        targetMethod,
                        activePatches,
                        NoPermittedSkippingPrefixOwners))
                {
                    continue;
                }

                ActiveHarmonyPatchDescriptor conflictingPatch =
                    RequireConflictingSkippingPrefix(
                        targetMethod,
                        activePatches,
                        NoPermittedSkippingPrefixOwners);
                throw CreateChangedAuthorityException(
                    selectedGroup,
                    targetMethod,
                    conflictingPatch,
                    "Klei's original method is no longer the proved authority");
            }
        }

        private static void VerifyFastTrackAuthority(
            DeliveryTemperatureRuntimePatchGroup selectedGroup,
            FastTrackFeatureCompatibility selectedFeature,
            FastTrackVerifiedMember replacementPrefixRole,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches,
            Func<MethodBase, bool> targetContract)
        {
            MemberInfo verifiedMember =
                selectedFeature.GetVerifiedMember(replacementPrefixRole);
            var verifiedReplacementPrefix = verifiedMember as MethodInfo;
            if (verifiedReplacementPrefix == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "Selected runtime group '" +
                    selectedGroup +
                    "' expected verified FastTrack role '" +
                    replacementPrefixRole +
                    "' to be a method, but observed " +
                    verifiedMember.MemberType +
                    ".");
            }

            bool foundExactSelectedAuthority = false;
            var verifiedTargets = new HashSet<MethodBase>();
            for (int patchIndex = 0;
                 patchIndex < activePatches.Count;
                 patchIndex++)
            {
                ActiveHarmonyPatchDescriptor patch = activePatches[patchIndex];
                if (!targetContract(patch.TargetMethod))
                {
                    continue;
                }

                if (Equals(patch.PatchMethod, verifiedReplacementPrefix) &&
                    string.Equals(
                        patch.HarmonyOwner,
                        FastTrackHarmonyOwner,
                        StringComparison.Ordinal))
                {
                    foundExactSelectedAuthority = true;
                }

                if (!verifiedTargets.Add(patch.TargetMethod) ||
                    HarmonyPatchContractVerifier.VerifyKleiAuthority(
                        patch.TargetMethod,
                        activePatches,
                        FastTrackPermittedSkippingPrefixOwners))
                {
                    continue;
                }

                ActiveHarmonyPatchDescriptor conflictingPatch =
                    RequireConflictingSkippingPrefix(
                        patch.TargetMethod,
                        activePatches,
                        FastTrackPermittedSkippingPrefixOwners);
                throw CreateChangedAuthorityException(
                    selectedGroup,
                    patch.TargetMethod,
                    conflictingPatch,
                    "an unverified skipping prefix can supersede the selected " +
                    "FastTrack replacement");
            }

            if (!foundExactSelectedAuthority)
            {
                throw new HarmonyPatchContractViolationException(
                    "Selected runtime group '" +
                    selectedGroup +
                    "' no longer has exact FastTrack authority method '" +
                    GetMethodDisplayName(verifiedReplacementPrefix) +
                    "' under Harmony owner '" +
                    FastTrackHarmonyOwner +
                    "'. No fallback was selected.");
            }
        }

        private static ActiveHarmonyPatchDescriptor
            RequireConflictingSkippingPrefix(
                MethodBase targetMethod,
                IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches,
                IReadOnlyCollection<string> permittedOwners)
        {
            for (int patchIndex = 0;
                 patchIndex < activePatches.Count;
                 patchIndex++)
            {
                ActiveHarmonyPatchDescriptor patch = activePatches[patchIndex];
                if (Equals(patch.TargetMethod, targetMethod) &&
                    patch.PatchMethod.ReturnType == typeof(bool) &&
                    !ContainsExactOwner(
                        permittedOwners,
                        patch.HarmonyOwner))
                {
                    return patch;
                }
            }

            throw new InvalidOperationException(
                "Klei authority verification reported a conflict without an " +
                "identifiable skipping prefix.");
        }

        private static HarmonyPatchContractViolationException
            CreateChangedAuthorityException(
                DeliveryTemperatureRuntimePatchGroup selectedGroup,
                MethodBase targetMethod,
                ActiveHarmonyPatchDescriptor conflictingPatch,
                string reason) =>
            new HarmonyPatchContractViolationException(
                "Selected runtime group '" +
                selectedGroup +
                "' failed its game-load authority check for target '" +
                GetMethodDisplayName(targetMethod) +
                "': " +
                reason +
                ". Conflicting patch '" +
                GetMethodDisplayName(conflictingPatch.PatchMethod) +
                "', Harmony owner '" +
                conflictingPatch.HarmonyOwner +
                "', priority " +
                conflictingPatch.Priority +
                ".");

        private static bool IsWorldInventoryUpdateTarget(MethodBase method) =>
            HasMethodContract(
                method,
                "WorldInventory",
                "Update",
                "System.Void",
                Array.Empty<string>());

        private static bool IsPickupUpdateTarget(MethodBase method) =>
            HasMethodContract(
                method,
                "FetchManager+FetchablesByPrefabId",
                "UpdatePickups",
                "System.Void",
                new[] { "Navigator", "System.Int32" });

        private static bool IsGlobalChoreCollectionTarget(MethodBase method) =>
            HasMethodContract(
                method,
                "GlobalChoreProvider",
                "CollectChores",
                "System.Void",
                new[]
                {
                    "ChoreConsumerState",
                    "System.Collections.Generic.List`1[Chore+Precondition+Context]"
                });

        private static bool HasMethodContract(
            MethodBase method,
            string declaringTypeName,
            string methodName,
            string returnTypeName,
            IReadOnlyList<string> parameterTypeNames)
        {
            var methodInfo = method as MethodInfo;
            Type? declaringType = method.DeclaringType;
            if (methodInfo == null ||
                declaringType == null ||
                !string.Equals(
                    declaringType.FullName,
                    declaringTypeName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    GetStableTypeName(methodInfo.ReturnType),
                    returnTypeName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != parameterTypeNames.Count)
            {
                return false;
            }

            for (int parameterIndex = 0;
                 parameterIndex < parameters.Length;
                 parameterIndex++)
            {
                if (!string.Equals(
                        GetStableTypeName(
                            parameters[parameterIndex].ParameterType),
                        parameterTypeNames[parameterIndex],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetStableTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                Type genericDefinition = type.GetGenericTypeDefinition();
                string genericDefinitionName =
                    genericDefinition.FullName ?? genericDefinition.Name;
                Type[] genericArguments = type.GetGenericArguments();
                var argumentNames = new string[genericArguments.Length];
                for (int argumentIndex = 0;
                     argumentIndex < genericArguments.Length;
                     argumentIndex++)
                {
                    argumentNames[argumentIndex] =
                        GetStableTypeName(genericArguments[argumentIndex]);
                }

                return genericDefinitionName +
                    "[" +
                    string.Join(",", argumentNames) +
                    "]";
            }

            return type.FullName ?? type.Name;
        }

        private static bool ContainsExactOwner(
            IReadOnlyCollection<string> permittedOwners,
            string candidateOwner)
        {
            foreach (string permittedOwner in permittedOwners)
            {
                if (string.Equals(
                        permittedOwner,
                        candidateOwner,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetMethodDisplayName(MethodBase method) =>
            (method.DeclaringType?.FullName ?? "<unknown-type>") +
            "." +
            method.Name;

        private static bool ShouldSelect(
            DeliveryTemperatureRuntimePatchGroup patchGroup,
            bool checkTemperatureForStatusItems,
            FastTrackFeatureCompatibility worldInventory,
            FastTrackFeatureCompatibility pickupGrouping,
            FastTrackFeatureCompatibility directDeliveryEligibility)
        {
            switch (patchGroup)
            {
                case DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle:
                case DeliveryTemperatureRuntimePatchGroup.WorldParentTopology:
                case DeliveryTemperatureRuntimePatchGroup
                    .KleiAuthoritativeFetchTemperatureEligibility:
                    return true;

                case DeliveryTemperatureRuntimePatchGroup
                    .KleiWorldInventoryTemperaturePublication:
                    return checkTemperatureForStatusItems &&
                        UsesKleiImplementation(worldInventory.State);

                case DeliveryTemperatureRuntimePatchGroup
                    .FastTrackWorldInventoryTemperaturePublication:
                    return checkTemperatureForStatusItems &&
                        worldInventory.State ==
                            FastTrackFeatureCompatibilityState.Ready;

                case DeliveryTemperatureRuntimePatchGroup
                    .TemperatureStatusAvailability:
                    return checkTemperatureForStatusItems &&
                        worldInventory.State !=
                            FastTrackFeatureCompatibilityState.Incompatible;

                case DeliveryTemperatureRuntimePatchGroup
                    .KleiPickupTemperatureGrouping:
                    return UsesKleiImplementation(pickupGrouping.State);

                case DeliveryTemperatureRuntimePatchGroup
                    .FastTrackPickupTemperatureGrouping:
                    return pickupGrouping.State ==
                        FastTrackFeatureCompatibilityState.Ready;

                case DeliveryTemperatureRuntimePatchGroup
                    .KleiDirectDeliveryEligibility:
                    return UsesKleiImplementation(
                        directDeliveryEligibility.State);

                case DeliveryTemperatureRuntimePatchGroup
                    .FastTrackDirectDeliveryEligibility:
                    return directDeliveryEligibility.State ==
                        FastTrackFeatureCompatibilityState.Ready;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(patchGroup),
                        patchGroup,
                        "Unknown delivery-temperature runtime patch group.");
            }
        }

        private static bool UsesKleiImplementation(
            FastTrackFeatureCompatibilityState compatibilityState)
        {
            switch (compatibilityState)
            {
                case FastTrackFeatureCompatibilityState.ModNotLoaded:
                case FastTrackFeatureCompatibilityState.ReplacementInactive:
                    return true;
                case FastTrackFeatureCompatibilityState.Ready:
                case FastTrackFeatureCompatibilityState.Incompatible:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(compatibilityState),
                        compatibilityState,
                        "Unknown FastTrack compatibility state.");
            }
        }

        private static void ThrowWhenActiveDeliveryFeatureIsIncompatible(
            FastTrackFeatureCompatibility deliveryFeature,
            FastTrackCompatibilityReport compatibilityReport)
        {
            if (deliveryFeature.State !=
                FastTrackFeatureCompatibilityState.Incompatible)
            {
                return;
            }

            throw new FastTrackDeliveryEligibilityCompatibilityException(
                "Delivery Temperature Limit cannot activate because the active " +
                "FastTrack " +
                deliveryFeature.Feature +
                " replacement is incompatible. " +
                CreateCompatibilityEvidence(
                    deliveryFeature,
                    compatibilityReport),
                compatibilityReport);
        }

        private static string CreateStatusCompatibilityDiagnostic(
            FastTrackFeatureCompatibility worldInventory,
            FastTrackCompatibilityReport compatibilityReport) =>
            "Temperature-aware resource-status integration is disabled for " +
            "this loaded game; existing ONI status availability remains " +
            "unchanged. " +
            CreateCompatibilityEvidence(
                worldInventory,
                compatibilityReport);

        private static string CreateCompatibilityEvidence(
            FastTrackFeatureCompatibility feature,
            FastTrackCompatibilityReport report) =>
            "Feature " +
            feature.Feature +
            "; assembly identity " +
            FormatOptional(report.AssemblyIdentity) +
            "; assembly version " +
            FormatOptional(report.AssemblyVersion) +
            "; file version " +
            FormatOptional(report.FileVersion) +
            "; SHA-256 " +
            FormatOptional(report.AssemblySha256) +
            "; failure code " +
            FormatOptional(feature.FailureCode) +
            "; structural failure: " +
            FormatOptional(CreateRuntimeCompatibilityFailureEvidence(feature)) +
            ". FastTrack compatibility is best-efforts and applies only to an " +
            "explicitly supported exact assembly build and its verified " +
            "member shape.";

        private static string? CreateRuntimeCompatibilityFailureEvidence(
            FastTrackFeatureCompatibility feature) =>
            feature.FailureCode ==
                FastTrackFeatureCompatibilityFailureCode
                    .AssemblyFileIdentityUnavailable
                ? "The FastTrack assembly file identity was unavailable; " +
                  "raw file-system failure text was omitted."
                : feature.FailureMessage;

        private static string FormatOptional(object? value) =>
            value == null
                ? "<unavailable>"
                : value.ToString() ?? "<unavailable>";

        private static void ValidateSelectedResponsibilities(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            bool checkTemperatureForStatusItems,
            FastTrackFeatureCompatibilityState worldInventoryState,
            FastTrackFeatureCompatibilityState pickupGroupingState,
            FastTrackFeatureCompatibilityState directDeliveryState,
            string? statusCompatibilityDiagnostic)
        {
            if (groups.Count < 5 ||
                groups[0] !=
                    DeliveryTemperatureRuntimePatchGroup.GameSessionLifecycle ||
                groups[1] !=
                    DeliveryTemperatureRuntimePatchGroup.WorldParentTopology ||
                groups[2] != DeliveryTemperatureRuntimePatchGroup
                    .KleiAuthoritativeFetchTemperatureEligibility)
            {
                throw new InvalidOperationException(
                    "A runtime patch plan must begin with lifecycle, topology, " +
                    "and authoritative fetch eligibility in contract order.");
            }

            int inventoryGroupCount = CountSelected(
                groups,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiWorldInventoryTemperaturePublication,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackWorldInventoryTemperaturePublication);
            int statusGroupCount = Contains(
                groups,
                DeliveryTemperatureRuntimePatchGroup
                    .TemperatureStatusAvailability)
                ? 1
                : 0;
            bool compatibleStatusWasRequested =
                checkTemperatureForStatusItems &&
                worldInventoryState !=
                    FastTrackFeatureCompatibilityState.Incompatible;
            if (inventoryGroupCount !=
                    (compatibleStatusWasRequested ? 1 : 0) ||
                statusGroupCount !=
                    (compatibleStatusWasRequested ? 1 : 0) ||
                (statusCompatibilityDiagnostic != null) !=
                    (checkTemperatureForStatusItems &&
                     worldInventoryState ==
                        FastTrackFeatureCompatibilityState.Incompatible))
            {
                throw new InvalidOperationException(
                    "Inventory publication, status instrumentation, and its " +
                    "compatibility diagnostic are not coherent.");
            }

            RequireExactlyOneSelectedImplementation(
                groups,
                pickupGroupingState,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiPickupTemperatureGrouping,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackPickupTemperatureGrouping,
                "pickup grouping");
            RequireExactlyOneSelectedImplementation(
                groups,
                directDeliveryState,
                DeliveryTemperatureRuntimePatchGroup
                    .KleiDirectDeliveryEligibility,
                DeliveryTemperatureRuntimePatchGroup
                    .FastTrackDirectDeliveryEligibility,
                "direct-delivery eligibility");
        }

        private static void RequireExactlyOneSelectedImplementation(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            FastTrackFeatureCompatibilityState featureState,
            DeliveryTemperatureRuntimePatchGroup kleiGroup,
            DeliveryTemperatureRuntimePatchGroup fastTrackGroup,
            string responsibility)
        {
            int selectedCount = CountSelected(
                groups,
                kleiGroup,
                fastTrackGroup);
            bool selectedExpectedGroup =
                featureState == FastTrackFeatureCompatibilityState.Ready
                    ? Contains(groups, fastTrackGroup)
                    : UsesKleiImplementation(featureState) &&
                        Contains(groups, kleiGroup);
            if (selectedCount != 1 || !selectedExpectedGroup)
            {
                throw new InvalidOperationException(
                    "A runtime patch plan must select exactly one verified " +
                    responsibility +
                    " implementation.");
            }
        }

        private static int CountSelected(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            DeliveryTemperatureRuntimePatchGroup first,
            DeliveryTemperatureRuntimePatchGroup second) =>
            (Contains(groups, first) ? 1 : 0) +
            (Contains(groups, second) ? 1 : 0);

        private static bool Contains(
            IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> groups,
            DeliveryTemperatureRuntimePatchGroup expected)
        {
            for (var groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                if (groups[groupIndex] == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
