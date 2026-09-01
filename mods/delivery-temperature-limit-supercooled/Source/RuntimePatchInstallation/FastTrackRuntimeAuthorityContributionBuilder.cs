#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Resolves and verifies the concrete Temperature Limit patch bindings and
    /// authority requirements for one ready FastTrack capability.
    /// </summary>
    internal sealed class FastTrackRuntimeAuthorityContributionBuilder :
        IFastTrackRuntimeAuthorityContributionBuilder
    {
        private const string FastTrackHarmonyOwner = "PeterHan.FastTrack";

        private static readonly IReadOnlyCollection<string>
            PermittedFastTrackSkippingPrefixOwners =
                new[] { FastTrackHarmonyOwner };

        public PreparedRuntimeAuthorityContribution Build(
            DeclaredModIntegrationId integrationId,
            RuntimeCapabilityId capabilityId,
            FastTrackFeatureCompatibility readyFeature,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            if (readyFeature == null)
            {
                throw new ArgumentNullException(nameof(readyFeature));
            }

            if (activePrefixes == null)
            {
                throw new ArgumentNullException(nameof(activePrefixes));
            }

            if (readyFeature.State != FastTrackFeatureCompatibilityState.Ready)
            {
                throw new ArgumentException(
                    "A FastTrack runtime-authority contribution can be built " +
                    "only from a ready verified feature.",
                    nameof(readyFeature));
            }

            switch (readyFeature.Feature)
            {
                case FastTrackFeature.WorldInventory:
                    RequireCapability(
                        capabilityId,
                        RuntimeCapabilityId
                            .WorldInventoryTemperaturePublication,
                        readyFeature.Feature);
                    return BuildWorldInventoryContribution(
                        integrationId,
                        capabilityId,
                        readyFeature,
                        activePrefixes);

                case FastTrackFeature.PickupGrouping:
                    RequireCapability(
                        capabilityId,
                        RuntimeCapabilityId.PickupTemperatureGrouping,
                        readyFeature.Feature);
                    return BuildPickupGroupingContribution(
                        integrationId,
                        capabilityId,
                        readyFeature,
                        activePrefixes);

                case FastTrackFeature.DirectDeliveryEligibility:
                    RequireCapability(
                        capabilityId,
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        readyFeature.Feature);
                    return BuildDirectDeliveryEligibilityContribution(
                        integrationId,
                        capabilityId,
                        readyFeature,
                        activePrefixes);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(readyFeature),
                        readyFeature.Feature,
                        "Unknown FastTrack feature.");
            }
        }

        private static PreparedRuntimeAuthorityContribution
            BuildWorldInventoryContribution(
                DeclaredModIntegrationId integrationId,
                RuntimeCapabilityId capabilityId,
                FastTrackFeatureCompatibility readyFeature,
                IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            FastTrackWorldInventoryTemperaturePatches
                .BindVerifiedWorldInventoryFeature(readyFeature);
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

            var bindings = new List<HarmonyPatchContractBinding>();
            AddPrefix(
                bindings,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdatePrefix));
            AddTranspiler(
                bindings,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdateTranspiler));
            AddPostfix(
                bindings,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdatePostfix));
            AddFinalizer(
                bindings,
                runUpdateTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventoryRunUpdateFinalizer));
            AddTranspiler(
                bindings,
                sumTotalTarget,
                typeof(FastTrackWorldInventoryTemperaturePatches),
                nameof(FastTrackWorldInventoryTemperaturePatches
                    .BackgroundWorldInventorySumTotalTranspiler));

            return CreateContribution(
                integrationId,
                capabilityId,
                new RuntimePatchGroupId(
                    "fast-track-world-inventory-temperature-publication"),
                readyFeature,
                FastTrackVerifiedMember.WorldInventoryReplacementPrefix,
                activePrefixes,
                HarmonyPatchContractBindingVerifier.VerifyAll(bindings));
        }

        private static PreparedRuntimeAuthorityContribution
            BuildPickupGroupingContribution(
                DeclaredModIntegrationId integrationId,
                RuntimeCapabilityId capabilityId,
                FastTrackFeatureCompatibility readyFeature,
                IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            FastTrackPickupTemperaturePatches
                .BindVerifiedPickupGroupingFeature(readyFeature);
            FastTrackPickupTemperaturePatches
                .VerifyFastTrackPickupTemperaturePatchContracts();
            MethodInfo updateTarget = FastTrackPickupTemperaturePatches
                .ResolveFetchManagerBeforeUpdatePickupsTarget();

            var bindings = new List<HarmonyPatchContractBinding>();
            AddPrefix(
                bindings,
                updateTarget,
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .BeforeUpdatePickupsPrefix));
            AddPostfix(
                bindings,
                updateTarget,
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .BeforeUpdatePickupsPostfix));
            AddFinalizer(
                bindings,
                updateTarget,
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .BeforeUpdatePickupsFinalizer));
            AddTranspiler(
                bindings,
                FastTrackPickupTemperaturePatches
                    .ResolvePickupTagDictionaryAddItemTarget(),
                typeof(FastTrackPickupTemperaturePatches),
                nameof(FastTrackPickupTemperaturePatches
                    .PickupTagDictionaryAddItemTranspiler));

            return CreateContribution(
                integrationId,
                capabilityId,
                new RuntimePatchGroupId(
                    "fast-track-pickup-temperature-grouping"),
                readyFeature,
                FastTrackVerifiedMember
                    .PickupGroupingBeforeUpdatePickupsPrefix,
                activePrefixes,
                HarmonyPatchContractBindingVerifier.VerifyAll(bindings));
        }

        private static PreparedRuntimeAuthorityContribution
            BuildDirectDeliveryEligibilityContribution(
                DeclaredModIntegrationId integrationId,
                RuntimeCapabilityId capabilityId,
                FastTrackFeatureCompatibility readyFeature,
                IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            FastTrackDirectDeliveryEligibilityPatches
                .BindVerifiedDirectDeliveryEligibilityFeature(readyFeature);
            FastTrackDirectDeliveryEligibilityPatches
                .VerifyFastTrackDirectDeliveryEligibilityPatchContracts();

            var bindings = new List<HarmonyPatchContractBinding>();
            AddTranspiler(
                bindings,
                FastTrackDirectDeliveryEligibilityPatches
                    .ResolveChoreComparatorCheckFetchChoreTarget(),
                typeof(FastTrackDirectDeliveryEligibilityPatches),
                nameof(FastTrackDirectDeliveryEligibilityPatches
                    .CheckFetchChoreTranspiler));

            return CreateContribution(
                integrationId,
                capabilityId,
                new RuntimePatchGroupId(
                    "fast-track-direct-delivery-eligibility"),
                readyFeature,
                FastTrackVerifiedMember
                    .DirectDeliveryEligibilityReplacementPrefix,
                activePrefixes,
                HarmonyPatchContractBindingVerifier.VerifyAll(bindings));
        }

        private static PreparedRuntimeAuthorityContribution CreateContribution(
            DeclaredModIntegrationId integrationId,
            RuntimeCapabilityId capabilityId,
            RuntimePatchGroupId patchGroupId,
            FastTrackFeatureCompatibility readyFeature,
            FastTrackVerifiedMember replacementPrefixRole,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes,
            HarmonyPatchContractBindingVerifier.VerifiedBindings bindings)
        {
            MethodInfo replacementPrefix = RequireVerifiedMethod(
                readyFeature,
                replacementPrefixRole);
            ActiveHarmonyPrefixDescriptor selectedAuthority =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    activePrefixes,
                    prefix =>
                        Equals(prefix.PrefixMethod, replacementPrefix) &&
                        string.Equals(
                            prefix.HarmonyOwner,
                            FastTrackHarmonyOwner,
                            StringComparison.Ordinal),
                    "the exact active FastTrack replacement authority for " +
                    capabilityId.Value);
            IReadOnlyList<RuntimeAuthorityRequirement> authorityRequirements =
                CreateAuthorityRequirements(
                    selectedAuthority,
                    replacementPrefix,
                    bindings);

            return new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(integrationId),
                capabilityId,
                new[] { patchGroupId },
                RuntimeAuthorityObservation.OwnsCompatible,
                bindings,
                authorityRequirements,
                diagnosticCode: null,
                diagnosticMessage: null);
        }

        private static IReadOnlyList<RuntimeAuthorityRequirement>
            CreateAuthorityRequirements(
                ActiveHarmonyPrefixDescriptor selectedAuthority,
                MethodInfo replacementPrefix,
                IReadOnlyList<HarmonyPatchContractBinding> bindings)
        {
            var requirements = new List<RuntimeAuthorityRequirement>(
                bindings.Count + 1)
            {
                new RuntimeAuthorityRequirement(
                    selectedAuthority.TargetMethod,
                    RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                    FastTrackHarmonyOwner,
                    replacementPrefix,
                    PermittedFastTrackSkippingPrefixOwners)
            };
            var requiredTargets = new HashSet<MethodBase>
            {
                selectedAuthority.TargetMethod
            };
            for (int bindingIndex = 0;
                 bindingIndex < bindings.Count;
                 bindingIndex++)
            {
                MethodBase targetMethod = bindings[bindingIndex].TargetMethod;
                if (!requiredTargets.Add(targetMethod))
                {
                    continue;
                }

                requirements.Add(
                    new RuntimeAuthorityRequirement(
                        targetMethod,
                        RuntimeAuthorityRequirementKind.KleiOriginal,
                        requiredHarmonyOwner: null,
                        requiredPrefixMethod: null,
                        PermittedFastTrackSkippingPrefixOwners));
            }

            return requirements.AsReadOnly();
        }

        private static MethodInfo RequireVerifiedMethod(
            FastTrackFeatureCompatibility readyFeature,
            FastTrackVerifiedMember verifiedMember)
        {
            MemberInfo member = readyFeature.GetVerifiedMember(verifiedMember);
            var method = member as MethodInfo;
            if (method == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "Verified FastTrack role '" + verifiedMember +
                    "' must be a method, but observed " + member.MemberType +
                    ".");
            }

            return method;
        }

        private static void RequireCapability(
            RuntimeCapabilityId actual,
            RuntimeCapabilityId expected,
            FastTrackFeature feature)
        {
            if (!actual.Equals(expected))
            {
                throw new ArgumentException(
                    "FastTrack feature " + feature +
                    " cannot build runtime capability " + actual.Value + ".",
                    nameof(actual));
            }
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
            ICollection<HarmonyPatchContractBinding> bindings,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddBinding(
                bindings,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Prefix);

        private static void AddPostfix(
            ICollection<HarmonyPatchContractBinding> bindings,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddBinding(
                bindings,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Postfix);

        private static void AddTranspiler(
            ICollection<HarmonyPatchContractBinding> bindings,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddBinding(
                bindings,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Transpiler);

        private static void AddFinalizer(
            ICollection<HarmonyPatchContractBinding> bindings,
            MethodBase targetMethod,
            Type patchDeclaringType,
            string patchMethodName) =>
            AddBinding(
                bindings,
                targetMethod,
                patchDeclaringType,
                patchMethodName,
                HarmonyPatchContractKind.Finalizer);

        private static void AddBinding(
            ICollection<HarmonyPatchContractBinding> bindings,
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
            bindings.Add(
                new HarmonyPatchContractBinding(
                    targetMethod,
                    patchMethod,
                    patchKind));
        }
    }
}
