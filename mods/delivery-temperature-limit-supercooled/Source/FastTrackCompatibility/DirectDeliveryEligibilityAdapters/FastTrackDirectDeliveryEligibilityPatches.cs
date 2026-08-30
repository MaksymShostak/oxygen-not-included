#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Narrows only FastTrack's verified successful direct-delivery comparator
    /// result with the canonical destination temperature constraint.
    /// </summary>
    /// <remarks>
    /// The official pinned FastTrack GitHub artifact has no direct-delivery
    /// replacement, so this class normally remains unbound and unpatched. It
    /// exists solely for a loaded same-version binary whose former replacement is
    /// both active and structurally <c>Ready</c>. There is no assembly-presence
    /// fallback and no Harmony discovery attribute.
    /// </remarks>
    internal static class FastTrackDirectDeliveryEligibilityPatches
    {
        private static readonly object BindingSynchronization = new object();

        private static VerifiedDirectDeliveryEligibilityFeatureBinding?
            verifiedDirectDeliveryEligibilityFeatureBinding;

        internal static void BindVerifiedDirectDeliveryEligibilityFeature(
            FastTrackFeatureCompatibility directDeliveryEligibilityFeature)
        {
            if (directDeliveryEligibilityFeature == null)
            {
                throw new ArgumentNullException(
                    nameof(directDeliveryEligibilityFeature));
            }

            if (directDeliveryEligibilityFeature.Feature !=
                    FastTrackFeature.DirectDeliveryEligibility ||
                directDeliveryEligibilityFeature.State !=
                    FastTrackFeatureCompatibilityState.Ready)
            {
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack direct-delivery adapter can bind only a Ready " +
                    "DirectDeliveryEligibility feature contract.");
            }

            VerifiedDirectDeliveryEligibilityFeatureBinding candidateBinding =
                VerifiedDirectDeliveryEligibilityFeatureBinding.Create(
                    directDeliveryEligibilityFeature);
            lock (BindingSynchronization)
            {
                VerifiedDirectDeliveryEligibilityFeatureBinding?
                    currentBinding =
                        verifiedDirectDeliveryEligibilityFeatureBinding;
                if (currentBinding == null)
                {
                    Volatile.Write(
                        ref verifiedDirectDeliveryEligibilityFeatureBinding,
                        candidateBinding);
                    return;
                }

                if (!currentBinding.ReferencesSameMembers(candidateBinding))
                {
                    throw new HarmonyPatchContractViolationException(
                        "The FastTrack direct-delivery adapter is already bound " +
                        "to a different verified member set.");
                }
            }
        }

        internal static MethodInfo
            ResolveChoreComparatorCheckFetchChoreTarget() =>
            RequireVerifiedDirectDeliveryEligibilityFeatureBinding()
                .CheckFetchChore;

        /// <summary>
        /// Runs the exact no-mutation IL preflight consumed by coordinated
        /// activation before Harmony may modify the optional comparator.
        /// </summary>
        internal static void
            VerifyFastTrackDirectDeliveryEligibilityPatchContracts()
        {
            MethodInfo comparatorTarget =
                ResolveChoreComparatorCheckFetchChoreTarget();
            ILGenerator comparatorGenerator;
            List<CodeInstruction> comparatorInstructions =
                PatchProcessor.GetOriginalInstructions(
                    comparatorTarget,
                    out comparatorGenerator);
            _ = comparatorGenerator;
            _ = new List<CodeInstruction>(
                CheckFetchChoreTranspiler(
                    comparatorInstructions,
                    comparatorTarget));
        }

        internal static IEnumerable<CodeInstruction> CheckFetchChoreTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase originalMethod)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            VerifiedDirectDeliveryEligibilityFeatureBinding binding =
                RequireVerifiedDirectDeliveryEligibilityFeatureBinding();
            if (originalMethod == null)
            {
                throw new ArgumentNullException(nameof(originalMethod));
            }

            if (!Equals(originalMethod, binding.CheckFetchChore))
            {
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack direct-delivery transpiler was invoked for a " +
                    "method other than its exact verified comparator target.");
            }

            var sourceInstructions = new List<CodeInstruction>(instructions);
            int successReturnInstructionIndex =
                RequireUniqueOriginalSuccessReturnInstructionIndex(
                    sourceInstructions);
            CodeInstruction originalSuccessReturn =
                sourceInstructions[successReturnInstructionIndex];
            MethodInfo eligibilityHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(FastTrackDirectDeliveryEligibilityPatches),
                    nameof(IsPickupAllowedForFetchChore),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    new[] { typeof(FetchChore), typeof(Pickupable) });

            // Replace only the literal true. Every original false branch and its
            // return remain byte-for-byte represented by the surrounding list, so
            // temperature logic can never resurrect a rejected FastTrack chore.
            var loadFetchChore = new CodeInstruction(OpCodes.Ldarg_2);
            loadFetchChore.labels.AddRange(originalSuccessReturn.labels);
            loadFetchChore.blocks.AddRange(originalSuccessReturn.blocks);
            var narrowedSuccessInstructions = new List<CodeInstruction>(4)
            {
                loadFetchChore,
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(
                    OpCodes.Ldfld,
                    binding.SortedClearablePickupableField),
                new CodeInstruction(OpCodes.Call, eligibilityHook)
            };
            sourceInstructions.RemoveAt(successReturnInstructionIndex);
            sourceInstructions.InsertRange(
                successReturnInstructionIndex,
                narrowedSuccessInstructions);
            return sourceInstructions;
        }

        private static bool IsPickupAllowedForFetchChore(
            FetchChore fetchChore,
            Pickupable pickupable)
        {
            Storage? destination = ReferenceEquals(fetchChore, null)
                ? null
                : fetchChore.destination;
            if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out DeliveryTemperatureGameSession gameSession) ||
                ReferenceEquals(destination, null))
            {
                return true;
            }

            int destinationGameObjectInstanceId =
                destination.gameObject.GetInstanceID();
            if (!gameSession.TemperatureLimitComponents.TryGetConstraint(
                    destinationGameObjectInstanceId,
                    out var constraint,
                    out _) ||
                !constraint.IsEnabled)
            {
                return true;
            }

            if (ReferenceEquals(pickupable, null))
            {
                return true;
            }

            PrimaryElement primaryElement = pickupable.PrimaryElement;
            if (ReferenceEquals(primaryElement, null))
            {
                // Preserve the characterized permissive behavior for unusual
                // pickup objects that do not own a PrimaryElement.
                return true;
            }

            // Allows owns canonical Kelvin normalization and both boundaries.
            // Read the live direct-delivery temperature exactly once.
            float temperatureKelvin = primaryElement.Temperature;
            return constraint.Allows(temperatureKelvin);
        }

        private static int RequireUniqueOriginalSuccessReturnInstructionIndex(
            IReadOnlyList<CodeInstruction> instructions)
        {
            int successReturnInstructionIndex = -1;
            int successReturnCount = 0;
            for (var instructionIndex = 0;
                 instructionIndex + 1 < instructions.Count;
                 instructionIndex++)
            {
                if (instructions[instructionIndex].opcode != OpCodes.Ldc_I4_1 ||
                    instructions[instructionIndex + 1].opcode != OpCodes.Ret)
                {
                    continue;
                }

                successReturnCount++;
                successReturnInstructionIndex = instructionIndex;
            }

            if (successReturnCount != 1)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack ChoreComparator.CheckFetchChore requires exactly " +
                    "one original success return, but found " +
                    successReturnCount +
                    ".");
            }

            return successReturnInstructionIndex;
        }

        private static VerifiedDirectDeliveryEligibilityFeatureBinding
            RequireVerifiedDirectDeliveryEligibilityFeatureBinding()
        {
            VerifiedDirectDeliveryEligibilityFeatureBinding? binding =
                Volatile.Read(
                    ref verifiedDirectDeliveryEligibilityFeatureBinding);
            return binding ??
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack direct-delivery adapter has not been bound " +
                    "to a Ready compatibility report.");
        }

        private sealed class VerifiedDirectDeliveryEligibilityFeatureBinding
        {
            private VerifiedDirectDeliveryEligibilityFeatureBinding(
                MethodInfo checkFetchChore,
                FieldInfo sortedClearablePickupableField)
            {
                CheckFetchChore = checkFetchChore;
                SortedClearablePickupableField =
                    sortedClearablePickupableField;
            }

            internal MethodInfo CheckFetchChore { get; }

            internal FieldInfo SortedClearablePickupableField { get; }

            internal static VerifiedDirectDeliveryEligibilityFeatureBinding
                Create(FastTrackFeatureCompatibility compatibility)
            {
                if (!(compatibility.GetVerifiedMember(
                            FastTrackVerifiedMember
                                .DirectDeliveryEligibilityComparator) is
                        MethodInfo comparator) ||
                    comparator.IsStatic ||
                    comparator.ReturnType != typeof(bool))
                {
                    throw new HarmonyPatchContractViolationException(
                        "The verified FastTrack direct-delivery comparator no " +
                        "longer matches its bound instance-method contract.");
                }

                ParameterInfo[] parameters = comparator.GetParameters();
                if (parameters.Length != 3 ||
                    parameters[0].ParameterType !=
                        typeof(Chore.Precondition.Context).MakeByRefType() ||
                    parameters[1].ParameterType != typeof(FetchChore) ||
                    !parameters[2].ParameterType.IsByRef)
                {
                    throw new HarmonyPatchContractViolationException(
                        "The verified FastTrack direct-delivery comparator no " +
                        "longer has the exact context, chore, and sorted-pickup " +
                        "parameter sequence.");
                }

                Type? sortedClearableType = parameters[2]
                    .ParameterType
                    .GetElementType();
                if (!(compatibility.GetVerifiedMember(
                            FastTrackVerifiedMember
                                .DirectDeliveryEligibilitySortedPickupableField)
                        is FieldInfo pickupableField) ||
                    pickupableField.IsStatic ||
                    !pickupableField.IsPublic ||
                    pickupableField.DeclaringType != sortedClearableType ||
                    pickupableField.FieldType != typeof(Pickupable))
                {
                    throw new HarmonyPatchContractViolationException(
                        "The verified FastTrack sorted-clearable pickupable " +
                        "member no longer matches the comparator's exact " +
                        "by-reference value type.");
                }

                return new VerifiedDirectDeliveryEligibilityFeatureBinding(
                    comparator,
                    pickupableField);
            }

            internal bool ReferencesSameMembers(
                VerifiedDirectDeliveryEligibilityFeatureBinding other) =>
                Equals(CheckFetchChore, other.CheckFetchChore) &&
                Equals(
                    SortedClearablePickupableField,
                    other.SortedClearablePickupableField);
        }
    }
}
