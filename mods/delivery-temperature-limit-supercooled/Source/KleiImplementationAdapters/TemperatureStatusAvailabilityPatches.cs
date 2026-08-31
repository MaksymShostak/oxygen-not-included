#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Replaces Klei's status-only fetchable amount when—and only when—the
    /// session catalog proves complete temperature-eligible inventory evidence.
    /// </summary>
    /// <remarks>
    /// Manual runtime installation is intentionally separate. This class carries
    /// no patch-discovery metadata and performs no option or compatibility lookup
    /// in the 200 ms status path.
    /// </remarks>
    internal static class TemperatureStatusAvailabilityPatches
    {
        internal static MethodInfo
            ResolveFetchListStatusItemUpdaterRender200msTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchListStatusItemUpdater),
                "Render200ms",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(float) });

        internal static IEnumerable<CodeInstruction> Render200msTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            var sourceInstructions = new List<CodeInstruction>(instructions);
            int[] candidateIndices = CreateCandidateInstructionIndices(
                sourceInstructions.Count);
            MethodInfo worldContainerEnumeratorCurrentGetter =
                RequireWorldContainerEnumeratorCurrentGetter();
            FieldInfo worldContainerIdField = RequireWorldContainerIdField();
            MethodInfo amountDictionaryGetter = RequireAmountDictionaryGetter();
            MethodInfo minimumMethod = RequireMinimumAmountMethod();
            MethodInfo minimumFloatMethod = RequireMinimumFloatMethod();

            int worldIdentityStartIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesWorldIdentityCapture(
                        sourceInstructions,
                        index,
                        worldContainerEnumeratorCurrentGetter,
                        worldContainerIdField),
                    "FetchListStatusItemUpdater.Render200ms world identity");
            int worldIdLocalIndex =
                sourceInstructions[worldIdentityStartIndex + 2].LocalIndex();

            int minimumAmountCallIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesStatusAvailabilityWindow(
                        sourceInstructions,
                        index,
                        amountDictionaryGetter,
                        minimumFloatMethod,
                        minimumMethod),
                    "FetchListStatusItemUpdater.Render200ms " +
                    "early-insufficient branch and fetchable assignment");

            int destinationFetchListLocalIndex =
                sourceInstructions[minimumAmountCallIndex - 2].LocalIndex();
            int resourceTagLocalIndex =
                sourceInstructions[minimumAmountCallIndex - 1].LocalIndex();
            int remainingAmountLocalIndex =
                sourceInstructions[minimumAmountCallIndex - 9].LocalIndex();
            int fetchableAmountLocalIndex =
                sourceInstructions[minimumAmountCallIndex - 3].LocalIndex();
            int minimumRequiredAmountLocalIndex =
                sourceInstructions[minimumAmountCallIndex + 1].LocalIndex();

            MethodInfo replacementHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(TemperatureStatusAvailabilityPatches),
                    nameof(ReplaceFetchableAmountWhenInventoryIsComplete),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(void),
                    new[]
                    {
                        typeof(float),
                        typeof(FetchList2),
                        typeof(int),
                        typeof(Tag),
                        typeof(float).MakeByRefType(),
                        typeof(float),
                        typeof(float)
                    });

            var instrumentedInstructions = new List<CodeInstruction>(
                sourceInstructions.Count + 8);
            for (int instructionIndex = 0;
                 instructionIndex < sourceInstructions.Count;
                 instructionIndex++)
            {
                instrumentedInstructions.Add(
                    sourceInstructions[instructionIndex]);

                if (instructionIndex == minimumAmountCallIndex + 1)
                {
                    // Klei deliberately keeps the original in-storage amount on
                    // the evaluation stack across this calculation. Duplicate it
                    // for the hook and leave the original untouched for Klei's
                    // immediately following insufficient-material comparison.
                    instrumentedInstructions.Add(
                        new CodeInstruction(OpCodes.Dup));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            destinationFetchListLocalIndex));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(worldIdLocalIndex));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(resourceTagLocalIndex));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            fetchableAmountLocalIndex,
                            loadAddress: true));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            remainingAmountLocalIndex));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            minimumRequiredAmountLocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        replacementHook));
                }
            }

            return instrumentedInstructions;
        }

        private static void ReplaceFetchableAmountWhenInventoryIsComplete(
            float originalStorageAmount,
            FetchList2 destinationFetchList,
            int worldId,
            Tag resourceTag,
            ref float fetchableAmount,
            float remainingAmount,
            float minimumRequiredAmount)
        {
            float originalFetchableAmount = fetchableAmount;
            if (!TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                    originalStorageAmount,
                    originalFetchableAmount,
                    minimumRequiredAmount))
            {
                // Preserve Klei's cheap early-insufficient path before capturing a
                // session, touching a Unity object, or querying either catalog.
                return;
            }

            if (destinationFetchList == null ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            if (session.TemperatureConstraints.CaptureSnapshot()
                .EnabledConstraintCount == 0)
            {
                // Avoid Unity and concurrent-index reads for the overwhelmingly
                // common state in which this optional status feature is installed
                // but no destination currently enables a temperature constraint.
                return;
            }

            Storage destinationStorage = destinationFetchList.Destination;
            if (destinationStorage == null)
            {
                return;
            }

            int destinationGameObjectInstanceId =
                destinationStorage.gameObject.GetInstanceID();
            if (!session.TemperatureLimitComponents.TryGetConstraint(
                    destinationGameObjectInstanceId,
                    out var constraint,
                    out _))
            {
                // No registered component means the destination has no delivery-
                // temperature semantics. Preserve the exact incoming amount.
                return;
            }

            if (!constraint.IsEnabled)
            {
                // Disabled components remain indexed for ownership-safe updates,
                // but they must impose no status-path work beyond the O(1) lookup.
                return;
            }

            WorldParentTopologySnapshot worldTopology =
                session.WorldParentTopology.CaptureSnapshot();
            if (!worldTopology.GameSessionGeneration.Equals(
                    session.Generation) ||
                !worldTopology.TryResolveParentWorld(
                    worldId,
                    out var parentWorldId))
            {
                // Missing or cross-session topology is incomplete evidence, not a
                // zero amount. The original Klei availability remains authoritative.
                return;
            }

            WorldInventoryCollectionGeneration collectionGeneration =
                session.CurrentWorldInventoryCollectionGeneration;
            TemperatureConstrainedAmountAvailability availability =
                session.WorldResourceTemperatureAmounts
                    .GetTemperatureConstrainedAmountAvailability(
                        parentWorldId,
                        resourceTag,
                        constraint,
                        collectionGeneration);

            if (TemperatureStatusAvailabilityDecision
                .TryCalculateReplacementFetchable(
                    availability,
                    remainingAmount,
                    out var replacementFetchableAmount))
            {
                // Assignment is the only mutation. Disabled and incomplete states
                // return false and cannot accidentally overwrite the incoming value
                // with a default or unavailable out amount.
                fetchableAmount = replacementFetchableAmount;
            }
        }

        private static bool MatchesWorldIdentityCapture(
            IReadOnlyList<CodeInstruction> instructions,
            int currentWorldContainerGetterIndex,
            MethodInfo worldContainerEnumeratorCurrentGetter,
            FieldInfo worldContainerIdField)
        {
            if (currentWorldContainerGetterIndex < 0 ||
                currentWorldContainerGetterIndex + 2 >= instructions.Count ||
                !IsCall(
                    instructions[currentWorldContainerGetterIndex],
                    worldContainerEnumeratorCurrentGetter) ||
                !IsFieldLoad(
                    instructions[currentWorldContainerGetterIndex + 1],
                    worldContainerIdField) ||
                !IsStoreLocal(
                    instructions[currentWorldContainerGetterIndex + 2]))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesStatusAvailabilityWindow(
            IReadOnlyList<CodeInstruction> instructions,
            int minimumAmountCallIndex,
            MethodInfo amountDictionaryGetter,
            MethodInfo minimumFloatMethod,
            MethodInfo minimumAmountMethod)
        {
            if (minimumAmountCallIndex < 12 ||
                minimumAmountCallIndex + 6 >= instructions.Count ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 12]) ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 11]) ||
                !IsCall(
                    instructions[minimumAmountCallIndex - 10],
                    amountDictionaryGetter) ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 9]) ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 8]) ||
                !IsCall(
                    instructions[minimumAmountCallIndex - 7],
                    minimumFloatMethod) ||
                !IsStoreLocal(
                    instructions[minimumAmountCallIndex - 6]) ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 5]) ||
                instructions[minimumAmountCallIndex - 6].LocalIndex() !=
                    instructions[minimumAmountCallIndex - 5].LocalIndex() ||
                instructions[minimumAmountCallIndex - 4].opcode !=
                    OpCodes.Add ||
                !IsStoreLocal(
                    instructions[minimumAmountCallIndex - 3]) ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 2]) ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex - 1]) ||
                instructions[minimumAmountCallIndex - 11].LocalIndex() !=
                    instructions[minimumAmountCallIndex - 1].LocalIndex() ||
                !IsCall(
                    instructions[minimumAmountCallIndex],
                    minimumAmountMethod) ||
                !IsStoreLocal(
                    instructions[minimumAmountCallIndex + 1]) ||
                instructions[minimumAmountCallIndex + 2].opcode !=
                    OpCodes.Dup ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex + 3]) ||
                instructions[minimumAmountCallIndex - 3].LocalIndex() !=
                    instructions[minimumAmountCallIndex + 3].LocalIndex() ||
                instructions[minimumAmountCallIndex + 4].opcode !=
                    OpCodes.Add ||
                !IsLoadLocal(
                    instructions[minimumAmountCallIndex + 5]) ||
                instructions[minimumAmountCallIndex + 1].LocalIndex() !=
                    instructions[minimumAmountCallIndex + 5].LocalIndex() ||
                !IsUnsignedGreaterThanOrEqualBranch(
                    instructions[minimumAmountCallIndex + 6]))
            {
                return false;
            }

            return true;
        }

        private static int[] CreateCandidateInstructionIndices(
            int instructionCount)
        {
            var candidateIndices = new int[instructionCount];
            for (int index = 0; index < instructionCount; index++)
            {
                candidateIndices[index] = index;
            }

            return candidateIndices;
        }

        private static bool IsCall(
            CodeInstruction instruction,
            MethodInfo expectedMethod) =>
            (instruction.opcode == OpCodes.Call ||
             instruction.opcode == OpCodes.Callvirt) &&
            Equals(instruction.operand, expectedMethod);

        private static bool IsFieldLoad(
            CodeInstruction instruction,
            FieldInfo expectedField) =>
            instruction.opcode == OpCodes.Ldfld &&
            Equals(instruction.operand, expectedField);

        private static bool IsLoadLocal(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloc_0 ||
            instruction.opcode == OpCodes.Ldloc_1 ||
            instruction.opcode == OpCodes.Ldloc_2 ||
            instruction.opcode == OpCodes.Ldloc_3 ||
            instruction.opcode == OpCodes.Ldloc_S ||
            instruction.opcode == OpCodes.Ldloc;

        private static bool IsStoreLocal(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Stloc_0 ||
            instruction.opcode == OpCodes.Stloc_1 ||
            instruction.opcode == OpCodes.Stloc_2 ||
            instruction.opcode == OpCodes.Stloc_3 ||
            instruction.opcode == OpCodes.Stloc_S ||
            instruction.opcode == OpCodes.Stloc;

        private static bool IsUnsignedGreaterThanOrEqualBranch(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Bge_Un ||
            instruction.opcode == OpCodes.Bge_Un_S;

        private static MethodInfo
            RequireWorldContainerEnumeratorCurrentGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(List<WorldContainer>.Enumerator),
                "get_Current",
                DeclaredMemberVisibility.Public,
                typeof(WorldContainer),
                Array.Empty<Type>());

        private static FieldInfo RequireWorldContainerIdField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(WorldContainer),
                "id",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(int));

        private static MethodInfo RequireAmountDictionaryGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Dictionary<Tag, float>),
                "get_Item",
                DeclaredMemberVisibility.Public,
                typeof(float),
                new[] { typeof(Tag) });

        private static MethodInfo RequireMinimumAmountMethod() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchList2),
                "GetMinimumAmount",
                DeclaredMemberVisibility.Public,
                typeof(float),
                new[] { typeof(Tag) });

        private static MethodInfo RequireMinimumFloatMethod() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(UnityEngine.Mathf),
                "Min",
                DeclaredMemberVisibility.Public,
                typeof(float),
                new[] { typeof(float), typeof(float) });
    }
}
