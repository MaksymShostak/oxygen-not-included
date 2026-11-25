#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Applies canonical delivery-temperature constraints to Klei's direct
    /// pickup decisions and fetch-chore coalescing paths.
    /// </summary>
    /// <remarks>
    /// This adapter deliberately declares no Harmony discovery attributes. Gate D
    /// may activate it only after every target and instruction contract below has
    /// passed against the loaded game. Until then, it is compiled but inert.
    /// </remarks>
    internal static class KleiDirectDeliveryEligibilityPatches
    {
        internal static MethodInfo
            ResolveFetchManagerIsFetchablePickupTarget() =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(FetchManager),
                "IsFetchablePickup",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                new[]
                {
                    typeof(Pickupable),
                    typeof(FetchChore),
                    typeof(Storage)
                });

        internal static MethodInfo
            ResolveClearableManagerEvaluateFetchRangeTarget()
        {
            Type clearableManagerType = ResolveClearableManagerType();
            Type range = HarmonyPatchContractVerifier.RequireNestedType(clearableManagerType,
                "TagFetchRange", DeclaredMemberVisibility.NonPublic);
            Type clearable = HarmonyPatchContractVerifier.RequireNestedType(clearableManagerType,
                "SortedClearable", DeclaredMemberVisibility.NonPublic);
            return HarmonyPatchContractVerifier.RequireInstanceMethod(clearableManagerType,
                "EvaluateFetchRange", DeclaredMemberVisibility.NonPublic, typeof(bool),
                new[] { range, typeof(List<int>), typeof(List<GlobalChoreProvider.Fetch>),
                    typeof(ChoreConsumerState), clearable, typeof(KPrefabID), typeof(ChoreType),
                    typeof(int), typeof(int), typeof(List<Chore.Precondition.Context>) });
        }

        internal static MethodInfo
            ResolveFetchAreaChoreStatesInstanceBeginTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchAreaChore.StatesInstance),
                "Begin",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(Chore.Precondition.Context) });

        internal static MethodInfo
            ResolveFetchAreaChoreCandidateDelegateTarget()
        {
            MethodInfo beginTarget =
                ResolveFetchAreaChoreStatesInstanceBeginTarget();
            ILGenerator beginGenerator;
            List<CodeInstruction> beginInstructions =
                PatchProcessor.GetOriginalInstructions(
                    beginTarget,
                    out beginGenerator);
            _ = beginGenerator;
            int[] candidateInstructionIndices =
                CreateCandidateInstructionIndices(beginInstructions.Count);
            int functionPointerInstructionIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateInstructionIndices,
                    index => IsExactFetchAreaCandidateFunctionPointer(
                        beginInstructions[index]),
                    "FetchAreaChore.StatesInstance.Begin candidate delegate " +
                    "function pointer");

            var observedDelegateTarget =
                (MethodInfo)beginInstructions[functionPointerInstructionIndex]
                    .operand;
            Type? closureType = observedDelegateTarget.DeclaringType;
            if (closureType == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "The FetchAreaChore candidate delegate has no declaring " +
                    "closure type.");
            }

            MethodInfo exactDelegateTarget =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    closureType,
                    observedDelegateTarget.Name,
                    DeclaredMemberVisibility.NonPublic,
                    typeof(Util.IterationInstruction),
                    new[] { typeof(object), typeof(object) });
            if (!Equals(exactDelegateTarget, observedDelegateTarget))
            {
                throw new HarmonyPatchContractViolationException(
                    "FetchAreaChore.StatesInstance.Begin points to a delegate " +
                    "other than the uniquely resolved candidate method.");
            }

            _ = RequireFetchAreaCandidateClosureOwnerField(closureType);
            return exactDelegateTarget;
        }

        /// <summary>
        /// Performs the complete no-patching preflight consumed by coordinated
        /// activation before any of the four Klei methods may be modified.
        /// </summary>
        internal static void VerifyKleiDirectDeliveryEligibilityPatchContracts()
        {
            _ = ResolveFetchManagerIsFetchablePickupTarget();

            MethodInfo clearableTarget =
                ResolveClearableManagerEvaluateFetchRangeTarget();
            ILGenerator clearableGenerator;
            List<CodeInstruction> clearableInstructions =
                PatchProcessor.GetOriginalInstructions(
                    clearableTarget,
                    out clearableGenerator);
            _ = clearableGenerator;
            _ = new List<CodeInstruction>(
                ClearableManagerEvaluateFetchRangeTranspiler(
                    clearableInstructions,
                    clearableTarget));

            MethodInfo beginTarget =
                ResolveFetchAreaChoreStatesInstanceBeginTarget();
            ILGenerator beginGenerator;
            List<CodeInstruction> beginInstructions =
                PatchProcessor.GetOriginalInstructions(
                    beginTarget,
                    out beginGenerator);
            _ = beginGenerator;
            _ = new List<CodeInstruction>(
                FetchAreaChoreBeginTranspiler(
                    beginInstructions,
                    beginTarget));

            MethodInfo candidateDelegateTarget =
                ResolveFetchAreaChoreCandidateDelegateTarget();
            ILGenerator candidateDelegateGenerator;
            List<CodeInstruction> candidateDelegateInstructions =
                PatchProcessor.GetOriginalInstructions(
                    candidateDelegateTarget,
                    out candidateDelegateGenerator);
            _ = candidateDelegateGenerator;
            _ = new List<CodeInstruction>(
                FetchAreaCandidateDelegateTranspiler(
                    candidateDelegateInstructions,
                    candidateDelegateTarget));
        }

        internal static void IsFetchablePickupPostfix(
            Pickupable pickup,
            Storage destination,
            ref bool __result)
        {
            if (!__result)
            {
                // A temperature check may only narrow Klei's decision; it must
                // never resurrect a candidate rejected by the original method.
                return;
            }

            __result = IsPickupAllowedForDestination(
                pickup,
                destination);
        }

        internal static IEnumerable<CodeInstruction>
            ClearableManagerEvaluateFetchRangeTranspiler(
                IEnumerable<CodeInstruction> instructions,
                MethodBase originalMethod)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            RequireExactOriginalMethod(
                originalMethod,
                ResolveClearableManagerEvaluateFetchRangeTarget(),
                "ClearableManager.EvaluateFetchRange");
            var sourceInstructions = new List<CodeInstruction>(instructions);
            int join = RequireSweepCandidateJoin(sourceInstructions, originalMethod);
            MethodInfo gate = HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(KleiDirectDeliveryEligibilityPatches), nameof(IsPickupAllowedForFetchChore),
                DeclaredMemberVisibility.NonPublic, typeof(bool), new[] { typeof(FetchChore), typeof(Pickupable) });

            // Replay the two native, typed loads. Both tag paths now visit the gate on an empty stack.
            // A rejected pair continues the same pickup's range at the native loop increment.
            var fetch = sourceInstructions[join + 1];
            var pickup = sourceInstructions[join + 5];
            var first = new CodeInstruction(fetch.opcode, fetch.operand);
            MoveInstructionLabels(sourceInstructions[join], first);
            sourceInstructions.InsertRange(join, new[] {
                first, new CodeInstruction(OpCodes.Ldfld, sourceInstructions[join + 2].operand),
                new CodeInstruction(pickup.opcode, pickup.operand),
                new CodeInstruction(OpCodes.Ldfld, sourceInstructions[join + 6].operand),
                new CodeInstruction(OpCodes.Call, gate),
                new CodeInstruction(OpCodes.Brfalse, sourceInstructions[join - 1].operand)
            });
            return sourceInstructions;
        }

        internal static IEnumerable<CodeInstruction>
            FetchAreaChoreBeginTranspiler(
                IEnumerable<CodeInstruction> instructions,
                MethodBase originalMethod)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            RequireExactOriginalMethod(
                originalMethod,
                ResolveFetchAreaChoreStatesInstanceBeginTarget(),
                "FetchAreaChore.StatesInstance.Begin");
            var sourceInstructions = new List<CodeInstruction>(instructions);
            FetchChoreContainmentExtensionAnchor anchor =
                RequireFetchChoreContainmentExtensionAnchor(
                    sourceInstructions);
            MethodInfo containmentHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiDirectDeliveryEligibilityPatches),
                    nameof(CanCombineFetchChores),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    new[] { typeof(FetchChore), typeof(FetchChore) });

            CodeInstruction originalContinuationInstruction =
                sourceInstructions[anchor.ExtensionInsertionIndex];
            var firstInjectedInstruction =
                new CodeInstruction(OpCodes.Ldarg_0);
            MoveInstructionLabels(
                originalContinuationInstruction,
                firstInjectedInstruction);
            var containmentInstructions = new List<CodeInstruction>(5)
            {
                firstInjectedInstruction,
                new CodeInstruction(OpCodes.Ldfld, anchor.RootFetchChoreField),
                HarmonyCodeInstructionFactory.LoadLocal(
                    anchor.CandidateFetchChoreLocalIndex),
                new CodeInstruction(OpCodes.Call, containmentHook),
                new CodeInstruction(
                    OpCodes.Brfalse,
                    anchor.RejectedCandidateBranchTarget)
            };
            sourceInstructions.InsertRange(
                anchor.ExtensionInsertionIndex,
                containmentInstructions);
            return sourceInstructions;
        }

        internal static IEnumerable<CodeInstruction>
            FetchAreaCandidateDelegateTranspiler(
                IEnumerable<CodeInstruction> instructions,
                MethodBase originalMethod)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            MethodInfo expectedDelegateTarget =
                ResolveFetchAreaChoreCandidateDelegateTarget();
            RequireExactOriginalMethod(
                originalMethod,
                expectedDelegateTarget,
                "FetchAreaChore candidate delegate");
            var sourceInstructions = new List<CodeInstruction>(instructions);
            FetchAreaCandidateCanReachAnchor anchor =
                RequireFetchAreaCandidateCanReachAnchor(
                    sourceInstructions,
                    expectedDelegateTarget);
            MethodInfo eligibilityHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiDirectDeliveryEligibilityPatches),
                    nameof(CanReachAndPickupTemperatureIsAllowed),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    new[]
                    {
                        typeof(ChoreConsumer),
                        typeof(IApproachable),
                        typeof(FetchChore)
                    });

            CodeInstruction originalCanReachInstruction =
                sourceInstructions[anchor.CanReachCallIndex];
            var firstInjectedInstruction =
                new CodeInstruction(OpCodes.Ldarg_0);
            MoveInstructionLabels(
                originalCanReachInstruction,
                firstInjectedInstruction);
            var rootFetchChoreInstructions = new List<CodeInstruction>(3)
            {
                firstInjectedInstruction,
                new CodeInstruction(
                    OpCodes.Ldfld,
                    anchor.ClosureOwnerField),
                new CodeInstruction(
                    OpCodes.Ldfld,
                    anchor.RootFetchChoreField)
            };
            sourceInstructions.InsertRange(
                anchor.CanReachCallIndex,
                rootFetchChoreInstructions);
            sourceInstructions[anchor.CanReachCallIndex + 3] =
                new CodeInstruction(OpCodes.Call, eligibilityHook);
            return sourceInstructions;
        }

        private static bool IsPickupAllowedForFetchChore(
            FetchChore fetchChore,
            Pickupable pickupable)
        {
            Storage? destination = ReferenceEquals(fetchChore, null)
                ? null
                : fetchChore.destination;
            return IsPickupAllowedForDestination(
                pickupable,
                destination);
        }

        private static bool IsPickupAllowedForDestination(
            Pickupable pickupable,
            Storage? destination)
        {
            if (DeliveryTemperatureGameSessionHost.RuntimeFailure.HasFailed) return true;
            try
            {
                if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                        out var session) ||
                    ReferenceEquals(destination, null))
                {
                    return true;
                }

                int destinationGameObjectInstanceId =
                    destination.gameObject.GetInstanceID();
                if (!session.TemperatureLimitComponents.TryGetConstraint(
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
                    // Preserve Klei's characterized permissive handling of pickup
                    // objects that have no PrimaryElement.
                    return true;
                }

                // Canonical conversion and both bounds are owned by Allows. Read the
                // live game temperature exactly once and do not duplicate that logic.
                float temperatureKelvin = primaryElement.Temperature;
                return constraint.Allows(temperatureKelvin);
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableGameplay(nameof(IsPickupAllowedForDestination), exception);
                return true;
            }
        }

        private static bool CanCombineFetchChores(
            FetchChore rootFetchChore,
            FetchChore candidateFetchChore)
        {
            if (DeliveryTemperatureGameSessionHost.RuntimeFailure.HasFailed) return true;
            try
            {
                if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                        out var session))
                {
                    return true;
                }

                int? rootDestinationInstanceId =
                    ResolveFetchChoreDestinationInstanceId(rootFetchChore);
                int? candidateDestinationInstanceId =
                    ResolveFetchChoreDestinationInstanceId(candidateFetchChore);
                if (rootDestinationInstanceId.HasValue &&
                    candidateDestinationInstanceId.HasValue &&
                    rootDestinationInstanceId.Value ==
                        candidateDestinationInstanceId.Value)
                {
                    // The same destination identity necessarily has the same indexed
                    // immutable constraint. Avoid two concurrent-dictionary probes.
                    return true;
                }

                DeliveryTemperatureConstraint? rootConstraint =
                    ResolveOptionalDestinationConstraint(
                        session,
                        rootDestinationInstanceId);
                DeliveryTemperatureConstraint? candidateConstraint =
                    ResolveOptionalDestinationConstraint(
                        session,
                        candidateDestinationInstanceId);
                return FetchChoreTemperatureConstraintContainment.CanCombine(
                    rootConstraint,
                    candidateConstraint);
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableGameplay(nameof(CanCombineFetchChores), exception);
                return true;
            }
        }

        private static bool CanReachAndPickupTemperatureIsAllowed(
            ChoreConsumer consumer,
            IApproachable approachable,
            FetchChore rootFetchChore)
        {
            if (!consumer.CanReach(approachable))
            {
                // Preserve the exact false result of Klei's original call before
                // performing any destination or component-index work.
                return false;
            }

            var pickupable = approachable as Pickupable;
            return ReferenceEquals(pickupable, null) ||
                IsPickupAllowedForFetchChore(
                    rootFetchChore,
                    pickupable);
        }

        private static int? ResolveFetchChoreDestinationInstanceId(
            FetchChore fetchChore)
        {
            if (ReferenceEquals(fetchChore, null))
            {
                return null;
            }

            Storage destination = fetchChore.destination;
            return ReferenceEquals(destination, null)
                ? (int?)null
                : destination.gameObject.GetInstanceID();
        }

        private static DeliveryTemperatureConstraint?
            ResolveOptionalDestinationConstraint(
                DeliveryTemperatureGameSession session,
                int? destinationGameObjectInstanceId)
        {
            if (!destinationGameObjectInstanceId.HasValue ||
                !session.TemperatureLimitComponents.TryGetConstraint(
                    destinationGameObjectInstanceId.Value,
                    out var constraint,
                    out _))
            {
                return null;
            }

            return constraint;
        }

        internal static Type ResolveClearableManagerType()
        {
            Type[] assemblyTypes = typeof(FetchManager).Assembly.GetTypes();
            return HarmonyPatchContractVerifier.RequireSingleMatch(
                assemblyTypes,
                candidate =>
                    candidate.DeclaringType == null &&
                    !candidate.IsPublic &&
                    string.Equals(
                        candidate.FullName,
                        "ClearableManager",
                        StringComparison.Ordinal),
                "Assembly-CSharp internal top-level ClearableManager type");
        }

        private static bool IsExactFetchAreaCandidateFunctionPointer(
            CodeInstruction instruction)
        {
            if (instruction.opcode != OpCodes.Ldftn ||
                !(instruction.operand is MethodInfo candidateMethod) ||
                candidateMethod.IsStatic ||
                candidateMethod.IsPublic ||
                candidateMethod.ReturnType !=
                    typeof(Util.IterationInstruction))
            {
                return false;
            }

            Type? closureType = candidateMethod.DeclaringType;
            if (closureType == null ||
                closureType.DeclaringType !=
                    typeof(FetchAreaChore.StatesInstance) ||
                !closureType.IsNestedPrivate ||
                !closureType.IsSealed)
            {
                return false;
            }

            ParameterInfo[] parameters = candidateMethod.GetParameters();
            return parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(object) &&
                parameters[1].ParameterType == typeof(object);
        }

        private static FieldInfo RequireFetchAreaCandidateClosureOwnerField(
            Type closureType) =>
            HarmonyPatchContractVerifier.RequireField(
                closureType,
                "<>4__this",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(FetchAreaChore.StatesInstance));

        private static int RequireSweepCandidateJoin(List<CodeInstruction> code, MethodBase original)
        {
            Type context = typeof(Chore.Precondition.Context);
            MethodInfo set = HarmonyPatchContractVerifier.RequireInstanceMethod(context, "Set",
                DeclaredMemberVisibility.Public, typeof(void), new[] { typeof(Chore), typeof(ChoreConsumerState), typeof(bool), typeof(object) });
            MethodInfo hasTag = HarmonyPatchContractVerifier.RequireInstanceMethod(typeof(KPrefabID), "HasTag",
                DeclaredMemberVisibility.Public, typeof(bool), new[] { typeof(Tag) });
            MethodInfo run = HarmonyPatchContractVerifier.RequireInstanceMethod(context, "RunPreconditions",
                DeclaredMemberVisibility.Public, typeof(void), Type.EmptyTypes);
            MethodInfo success = HarmonyPatchContractVerifier.RequireInstanceMethod(context, "IsSuccess",
                DeclaredMemberVisibility.Public, typeof(bool), Type.EmptyTypes);
            FieldInfo fetch = HarmonyPatchContractVerifier.RequireField(typeof(GlobalChoreProvider.Fetch), "chore",
                DeclaredMemberVisibility.Public, FieldStorageKind.Instance, typeof(FetchChore));
            FieldInfo pickup = HarmonyPatchContractVerifier.RequireField(original.GetParameters()[4].ParameterType, "pickupable",
                DeclaredMemberVisibility.Public, FieldStorageKind.Instance, typeof(Pickupable));
            FieldInfo tagsFirst = HarmonyPatchContractVerifier.RequireField(typeof(FetchChore), "tagsFirst",
                DeclaredMemberVisibility.Public, FieldStorageKind.Instance, typeof(Tag));
            int[] indices = CreateCandidateInstructionIndices(code.Count);
            int join = HarmonyPatchContractVerifier.RequireSingleMatch(indices, i => IsCall(code[i], set),
                "ClearableManager.EvaluateFetchRange context initialization") - 7;

            // The non-exact criteria branch joins the successful HasTag path; both failed tag
            // matching and failed native preconditions must advance the same candidate index.
            if (join < 11 || join + 15 >= code.Count ||
                !IsBranchWhenNotEqual(code[join - 7]) || !(code[join - 7].operand is Label accepted) ||
                !code[join].labels.Contains(accepted) || !IsLoadArgument(code[join - 6], 6) ||
                !IsLoadLocal(code[join - 5]) || !IsFieldLoad(code[join - 4], fetch) ||
                !IsFieldLoad(code[join - 3], tagsFirst) ||
                !IsCall(code[join - 2], hasTag) || !IsBranchWhenFalse(code[join - 1]) ||
                !(code[join - 1].operand is Label next) || !IsLoadLocalAddress(code[join]) ||
                !IsLoadLocal(code[join + 1]) || GetLocalIndex(code[join + 1]) != GetLocalIndex(code[join - 5]) ||
                !IsFieldLoad(code[join + 2], fetch) || !IsLoadArgument(code[join + 3], 4) ||
                code[join + 4].opcode != OpCodes.Ldc_I4_0 || !IsLoadArgument(code[join + 5], 5) ||
                !IsFieldLoad(code[join + 6], pickup) || !IsCall(code[join + 12], run) ||
                !IsCall(code[join + 14], success) || !IsBranchWhenFalse(code[join + 15]) ||
                !(code[join + 15].operand is Label preconditionRejected))
                throw new HarmonyPatchContractViolationException("Klei sweep candidate admission or precondition flow changed.");

            int increment = HarmonyPatchContractVerifier.RequireSingleMatch(indices, i => code[i].labels.Contains(next),
                "Klei sweep next candidate");
            for (int i = join + 1; i <= join + 7; i++)
                if (code[i].labels.Count != 0)
                    throw new HarmonyPatchContractViolationException("Another sweep path enters the native context after the temperature gate.");

            Type range = original.GetParameters()[0].ParameterType;
            FieldInfo count = HarmonyPatchContractVerifier.RequireField(range, "count",
                DeclaredMemberVisibility.Public, FieldStorageKind.Instance, typeof(int));
            FieldInfo start = HarmonyPatchContractVerifier.RequireField(range, "start",
                DeclaredMemberVisibility.Public, FieldStorageKind.Instance, typeof(int));
            if (increment <= join + 15 || increment + 9 >= code.Count ||
                !IsCall(code[increment - 3], typeof(List<Chore.Precondition.Context>).GetMethod("Add")!) ||
                code[increment - 2].opcode != OpCodes.Ldc_I4_1 || code[increment - 1].opcode != OpCodes.Ret ||
                !code[increment].labels.Contains(preconditionRejected) || !IsLoadLocal(code[increment]) ||
                code[increment + 1].opcode != OpCodes.Ldc_I4_1 || code[increment + 2].opcode != OpCodes.Add ||
                !IsStoreLocal(code[increment + 3]) || GetLocalIndex(code[increment]) != GetLocalIndex(code[increment + 3]) ||
                !IsLoadLocal(code[increment + 4]) || GetLocalIndex(code[increment]) != GetLocalIndex(code[increment + 4]) ||
                !IsLoadArgument(code[increment + 5], 1) || !IsFieldLoad(code[increment + 6], count) ||
                (code[increment + 7].opcode != OpCodes.Blt && code[increment + 7].opcode != OpCodes.Blt_S) ||
                !(code[increment + 7].operand is Label nextPair) ||
                code[increment + 8].opcode != OpCodes.Ldc_I4_0 || code[increment + 9].opcode != OpCodes.Ret)
                throw new HarmonyPatchContractViolationException("Klei sweep rejection no longer advances the candidate index.");
            int pair = HarmonyPatchContractVerifier.RequireSingleMatch(indices, i => code[i].labels.Contains(nextPair),
                "Klei sweep indexed candidate");
            if (pair + 11 != join - 11 || !IsLoadArgument(code[pair], 2) || !IsLoadArgument(code[pair + 1], 1) ||
                !IsFieldLoad(code[pair + 2], start) || !IsLoadLocal(code[pair + 3]) ||
                GetLocalIndex(code[pair + 3]) != GetLocalIndex(code[increment]) || code[pair + 4].opcode != OpCodes.Add ||
                !IsCall(code[pair + 5], typeof(List<int>).GetMethod("get_Item")!) || !IsStoreLocal(code[pair + 6]) ||
                !IsLoadArgument(code[pair + 7], 3) || !IsLoadLocal(code[pair + 8]) ||
                GetLocalIndex(code[pair + 6]) != GetLocalIndex(code[pair + 8]) ||
                !IsCall(code[pair + 9], typeof(List<GlobalChoreProvider.Fetch>).GetMethod("get_Item")!) ||
                !IsStoreLocal(code[pair + 10]) || GetLocalIndex(code[pair + 10]) != GetLocalIndex(code[join + 1]))
                throw new HarmonyPatchContractViolationException("Klei sweep loop no longer reads the indexed candidate.");
            RequireSafeExtensionBoundary(code, join - 7, join + 15, "Klei sweep candidate");
            return join;
        }

        private static bool IsLoadLocalAddress(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloca || instruction.opcode == OpCodes.Ldloca_S;

        private static FetchChoreContainmentExtensionAnchor
            RequireFetchChoreContainmentExtensionAnchor(
                IReadOnlyList<CodeInstruction> instructions)
        {
            FieldInfo rootFetchChoreField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(FetchAreaChore.StatesInstance),
                    "rootChore",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(FetchChore));
            FieldInfo forbidHashField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(FetchChore),
                    "forbidHash",
                    DeclaredMemberVisibility.Public,
                    FieldStorageKind.Instance,
                    typeof(int));
            int[] candidateInstructionIndices =
                CreateCandidateInstructionIndices(instructions.Count);
            int containmentAnchorIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateInstructionIndices,
                    index => MatchesFetchChoreContainmentWindow(
                        instructions,
                        index,
                        rootFetchChoreField,
                        forbidHashField),
                    "FetchAreaChore.StatesInstance.Begin fetch-chore " +
                    "containment extension");
            CodeInstruction rejectionBranch =
                instructions[containmentAnchorIndex + 5];
            if (!(rejectionBranch.operand is Label rejectionTarget))
            {
                throw new HarmonyPatchContractViolationException(
                    "FetchAreaChore.StatesInstance.Begin containment rejection " +
                    "branch has no typed label target.");
            }

            RequireSafeExtensionBoundary(
                instructions,
                containmentAnchorIndex,
                containmentAnchorIndex + 6,
                "FetchAreaChore.StatesInstance.Begin containment extension");
            return new FetchChoreContainmentExtensionAnchor(
                GetLocalIndex(instructions[containmentAnchorIndex]),
                containmentAnchorIndex + 6,
                rejectionTarget,
                rootFetchChoreField);
        }

        private static bool MatchesFetchChoreContainmentWindow(
            IReadOnlyList<CodeInstruction> instructions,
            int startIndex,
            FieldInfo rootFetchChoreField,
            FieldInfo forbidHashField) =>
            startIndex >= 0 &&
            startIndex + 5 < instructions.Count &&
            IsLoadLocal(instructions[startIndex]) &&
            IsFieldLoad(
                instructions[startIndex + 1],
                forbidHashField) &&
            IsLoadArgument(instructions[startIndex + 2], 0) &&
            IsFieldLoad(
                instructions[startIndex + 3],
                rootFetchChoreField) &&
            IsFieldLoad(
                instructions[startIndex + 4],
                forbidHashField) &&
            IsBranchWhenNotEqual(instructions[startIndex + 5]);

        private static FetchAreaCandidateCanReachAnchor
            RequireFetchAreaCandidateCanReachAnchor(
                IReadOnlyList<CodeInstruction> instructions,
                MethodInfo delegateTarget)
        {
            Type closureType = delegateTarget.DeclaringType!;
            FieldInfo closureOwnerField =
                RequireFetchAreaCandidateClosureOwnerField(closureType);
            FieldInfo rootContextField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(FetchAreaChore.StatesInstance),
                    "rootContext",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(Chore.Precondition.Context));
            FieldInfo consumerStateField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(Chore.Precondition.Context),
                    "consumerState",
                    DeclaredMemberVisibility.Public,
                    FieldStorageKind.Instance,
                    typeof(ChoreConsumerState));
            FieldInfo consumerField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(ChoreConsumerState),
                    "consumer",
                    DeclaredMemberVisibility.Public,
                    FieldStorageKind.Instance,
                    typeof(ChoreConsumer));
            FieldInfo rootFetchChoreField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(FetchAreaChore.StatesInstance),
                    "rootChore",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(FetchChore));
            MethodInfo canReachMethod =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(ChoreConsumer),
                    "CanReach",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(IApproachable) });
            int[] candidateInstructionIndices =
                CreateCandidateInstructionIndices(instructions.Count);
            int canReachAnchorIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateInstructionIndices,
                    index => MatchesFetchAreaCandidateCanReachWindow(
                        instructions,
                        index,
                        closureOwnerField,
                        rootContextField,
                        consumerStateField,
                        consumerField,
                        canReachMethod),
                    "FetchAreaChore candidate delegate direct CanReach result");

            RequireSafeExtensionBoundary(
                instructions,
                canReachAnchorIndex,
                canReachAnchorIndex + 6,
                "FetchAreaChore candidate delegate CanReach replacement");
            return new FetchAreaCandidateCanReachAnchor(
                canReachAnchorIndex + 6,
                closureOwnerField,
                rootFetchChoreField);
        }

        private static bool MatchesFetchAreaCandidateCanReachWindow(
            IReadOnlyList<CodeInstruction> instructions,
            int startIndex,
            FieldInfo closureOwnerField,
            FieldInfo rootContextField,
            FieldInfo consumerStateField,
            FieldInfo consumerField,
            MethodInfo canReachMethod) =>
            startIndex >= 0 &&
            startIndex + 9 < instructions.Count &&
            IsLoadArgument(instructions[startIndex], 0) &&
            IsFieldLoad(
                instructions[startIndex + 1],
                closureOwnerField) &&
            IsFieldAddressLoad(
                instructions[startIndex + 2],
                rootContextField) &&
            IsFieldLoad(
                instructions[startIndex + 3],
                consumerStateField) &&
            IsFieldLoad(
                instructions[startIndex + 4],
                consumerField) &&
            IsLoadLocal(instructions[startIndex + 5]) &&
            IsCall(instructions[startIndex + 6], canReachMethod) &&
            IsBranchWhenTrue(instructions[startIndex + 7]) &&
            instructions[startIndex + 8].opcode == OpCodes.Ldc_I4_0 &&
            instructions[startIndex + 9].opcode == OpCodes.Ret;

        private static void RequireExactOriginalMethod(
            MethodBase originalMethod,
            MethodInfo expectedMethod,
            string contractName)
        {
            if (originalMethod == null)
            {
                throw new ArgumentNullException(nameof(originalMethod));
            }

            if (!Equals(originalMethod, expectedMethod))
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " transpiler was invoked for a method other than its exact " +
                    "preflight target.");
            }
        }

        private static void RequireSafeExtensionBoundary(
            IReadOnlyList<CodeInstruction> instructions,
            int firstInstructionIndex,
            int continuationInstructionIndex,
            string contractName)
        {
            if (continuationInstructionIndex >= instructions.Count)
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " has no original continuation instruction.");
            }

            for (int instructionIndex = firstInstructionIndex;
                 instructionIndex <= continuationInstructionIndex;
                 instructionIndex++)
            {
                if (instructions[instructionIndex].blocks.Count != 0)
                {
                    throw new HarmonyPatchContractViolationException(
                        contractName +
                        " crosses an unreviewed exception-block boundary.");
                }
            }
        }

        private static void MoveInstructionLabels(
            CodeInstruction sourceInstruction,
            CodeInstruction destinationInstruction)
        {
            destinationInstruction.labels.AddRange(sourceInstruction.labels);
            sourceInstruction.labels.Clear();
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

        private static bool IsFieldAddressLoad(
            CodeInstruction instruction,
            FieldInfo expectedField) =>
            instruction.opcode == OpCodes.Ldflda &&
            Equals(instruction.operand, expectedField);

        private static bool IsLoadArgument(
            CodeInstruction instruction,
            int expectedArgumentIndex)
        {
            if (instruction.opcode == OpCodes.Ldarg_0)
            {
                return expectedArgumentIndex == 0;
            }

            if (instruction.opcode == OpCodes.Ldarg_1)
            {
                return expectedArgumentIndex == 1;
            }

            if (instruction.opcode == OpCodes.Ldarg_2)
            {
                return expectedArgumentIndex == 2;
            }

            if (instruction.opcode == OpCodes.Ldarg_3)
            {
                return expectedArgumentIndex == 3;
            }

            return (instruction.opcode == OpCodes.Ldarg ||
                    instruction.opcode == OpCodes.Ldarg_S) &&
                OperandRepresentsIndex(
                    instruction.operand,
                    expectedArgumentIndex);
        }

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

        private static int GetLocalIndex(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldloc_0 ||
                instruction.opcode == OpCodes.Stloc_0)
            {
                return 0;
            }

            if (instruction.opcode == OpCodes.Ldloc_1 ||
                instruction.opcode == OpCodes.Stloc_1)
            {
                return 1;
            }

            if (instruction.opcode == OpCodes.Ldloc_2 ||
                instruction.opcode == OpCodes.Stloc_2)
            {
                return 2;
            }

            if (instruction.opcode == OpCodes.Ldloc_3 ||
                instruction.opcode == OpCodes.Stloc_3)
            {
                return 3;
            }

            object? operand = instruction.operand;
            if (operand is LocalBuilder localBuilder)
            {
                return localBuilder.LocalIndex;
            }

            if (operand is byte byteIndex)
            {
                return byteIndex;
            }

            if (operand is sbyte signedByteIndex && signedByteIndex >= 0)
            {
                return signedByteIndex;
            }

            if (operand is short shortIndex && shortIndex >= 0)
            {
                return shortIndex;
            }

            if (operand is ushort unsignedShortIndex)
            {
                return unsignedShortIndex;
            }

            if (operand is int integerIndex && integerIndex >= 0)
            {
                return integerIndex;
            }

            throw new HarmonyPatchContractViolationException(
                "A structurally matched direct-eligibility instruction does not " +
                "carry a supported nonnegative local-variable identity.");
        }

        private static bool OperandRepresentsIndex(
            object? operand,
            int expectedIndex)
        {
            if (operand is byte byteIndex)
            {
                return byteIndex == expectedIndex;
            }

            if (operand is sbyte signedByteIndex)
            {
                return signedByteIndex == expectedIndex;
            }

            if (operand is short shortIndex)
            {
                return shortIndex == expectedIndex;
            }

            if (operand is ushort unsignedShortIndex)
            {
                return unsignedShortIndex == expectedIndex;
            }

            if (operand is int integerIndex)
            {
                return integerIndex == expectedIndex;
            }

            if (operand is ParameterInfo parameter)
            {
                return parameter.Position + (parameter.Member is MethodBase method && !method.IsStatic ? 1 : 0) == expectedIndex;
            }

            return false;
        }

        private static bool IsBranchWhenFalse(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Brfalse ||
            instruction.opcode == OpCodes.Brfalse_S;

        private static bool IsBranchWhenTrue(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Brtrue ||
            instruction.opcode == OpCodes.Brtrue_S;

        private static bool IsBranchWhenNotEqual(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Bne_Un ||
            instruction.opcode == OpCodes.Bne_Un_S;

        private readonly struct FetchChoreContainmentExtensionAnchor
        {
            internal FetchChoreContainmentExtensionAnchor(
                int candidateFetchChoreLocalIndex,
                int extensionInsertionIndex,
                Label rejectedCandidateBranchTarget,
                FieldInfo rootFetchChoreField)
            {
                CandidateFetchChoreLocalIndex =
                    candidateFetchChoreLocalIndex;
                ExtensionInsertionIndex = extensionInsertionIndex;
                RejectedCandidateBranchTarget =
                    rejectedCandidateBranchTarget;
                RootFetchChoreField = rootFetchChoreField;
            }

            internal int CandidateFetchChoreLocalIndex { get; }

            internal int ExtensionInsertionIndex { get; }

            internal Label RejectedCandidateBranchTarget { get; }

            internal FieldInfo RootFetchChoreField { get; }
        }

        private readonly struct FetchAreaCandidateCanReachAnchor
        {
            internal FetchAreaCandidateCanReachAnchor(
                int canReachCallIndex,
                FieldInfo closureOwnerField,
                FieldInfo rootFetchChoreField)
            {
                CanReachCallIndex = canReachCallIndex;
                ClosureOwnerField = closureOwnerField;
                RootFetchChoreField = rootFetchChoreField;
            }

            internal int CanReachCallIndex { get; }

            internal FieldInfo ClosureOwnerField { get; }

            internal FieldInfo RootFetchChoreField { get; }
        }
    }
}
