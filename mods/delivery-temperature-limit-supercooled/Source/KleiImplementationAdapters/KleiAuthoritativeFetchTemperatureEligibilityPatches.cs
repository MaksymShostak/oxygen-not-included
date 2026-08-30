#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Captures temperature eligibility from Klei's authoritative fetch traversal
    /// and conservatively refines clearable-destination decisions.
    /// </summary>
    /// <remarks>
    /// This adapter deliberately has no Harmony discovery attributes. The
    /// coordinated runtime installer activates it only after all exact targets,
    /// semantic IL anchors, and ownership gates have succeeded. Until then, these
    /// methods are inert compiled contracts rather than a second patch path.
    /// </remarks>
    internal static class KleiAuthoritativeFetchTemperatureEligibilityPatches
    {
        [ThreadStatic]
        private static AuthoritativeFetchEligibilityBuildInvocation?
            currentThreadBuildInvocation;

        [ThreadStatic]
        private static FetchTemperatureEligibilityBuilder?
            reusableThreadBuilder;

        internal static MethodInfo ResolveGlobalChoreProviderAddChoreTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProvider),
                "AddChore",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(Chore) });

        internal static MethodInfo
            ResolveGlobalChoreProviderRemoveChoreTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProvider),
                "RemoveChore",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(Chore) });

        internal static MethodInfo ResolveFetchChoreOnTagsChangedTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchChore),
                "OnTagsChanged",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                new[] { typeof(object) });

        internal static MethodInfo
            ResolveGlobalChoreProviderUpdateStorageFetchableBitsTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProvider),
                "UpdateStorageFetchableBits",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());

        internal static MethodInfo
            ResolveGlobalChoreProviderClearableHasDestinationTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(GlobalChoreProvider),
                "ClearableHasDestination",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                new[] { typeof(Pickupable) });

        internal static void GlobalChoreProviderAddChorePostfix(Chore chore)
        {
            if (chore is FetchChore &&
                DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                // Klei's installed AddChore body unconditionally appends a
                // FetchChore after the original returns successfully. A postfix
                // invocation is therefore proof of one effective map mutation.
                session.FetchRequestTopology.RecordEffectiveChange();
            }
        }

        internal static void GlobalChoreProviderRemoveChorePrefix(
            Chore chore,
            Dictionary<int, List<FetchChore>> ___fetchMap,
            out FetchChoreRemovalObservation __state)
        {
            __state = FetchChoreRemovalObservation.Inactive;
            if (!(chore is FetchChore fetchChore) ||
                ___fetchMap == null ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            // RemoveChore clears provider even when List.Remove finds nothing.
            // Version only the topology when the exact FetchChore was actually in
            // the parent-world list immediately before Klei attempted removal.
            UnityEngine.GameObject fetchChoreGameObject =
                fetchChore.gameObject;
            if (fetchChoreGameObject == null)
            {
                return;
            }

            int parentWorldId =
                fetchChoreGameObject.GetMyParentWorldId();
            if (parentWorldId < 0 ||
                !___fetchMap.TryGetValue(
                    parentWorldId,
                    out var parentWorldFetchChores) ||
                !parentWorldFetchChores.Contains(fetchChore))
            {
                return;
            }

            __state = FetchChoreRemovalObservation.Effective(session);
        }

        internal static void GlobalChoreProviderRemoveChorePostfix(
            FetchChoreRemovalObservation __state)
        {
            if (__state.IsEffective &&
                __state.Session.IsAcceptingPublications)
            {
                __state.Session.FetchRequestTopology.RecordEffectiveChange();
            }
        }

        internal static void FetchChoreOnTagsChangedPrefix(
            FetchChore __instance,
            out FetchChoreTagChangeObservation __state)
        {
            __state = FetchChoreTagChangeObservation.Inactive;
            if (__instance == null ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            // Copy values, not the mutable HashSet reference. The prefix/postfix
            // comparison must detect an in-place mutation and must not version an
            // event whose requested-tag set is semantically identical.
            __state = FetchChoreTagChangeObservation.Active(
                session,
                RequestedTagSetSnapshot.Capture(__instance.tags));
        }

        internal static void FetchChoreOnTagsChangedPostfix(
            FetchChore __instance,
            FetchChoreTagChangeObservation __state)
        {
            if (!__state.IsActive ||
                !__state.Session.IsAcceptingPublications ||
                __state.PriorRequestedTags.Matches(
                    __instance == null ? null : __instance.tags))
            {
                return;
            }

            __state.Session.FetchRequestTopology.RecordEffectiveChange();
        }

        internal static void UpdateStorageFetchableBitsPrefix(
            out AuthoritativeFetchEligibilityBuildInvocation __state)
        {
            if (currentThreadBuildInvocation != null)
            {
                throw new InvalidOperationException(
                    "GlobalChoreProvider.UpdateStorageFetchableBits re-entered " +
                    "temperature eligibility collection on the same thread.");
            }

            __state = AuthoritativeFetchEligibilityBuildInvocation.Inactive;
            if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            ActiveTemperatureConstraintSnapshot activeConstraints =
                session.TemperatureConstraints.CaptureSnapshot();
            if (activeConstraints.EnabledConstraintCount == 0)
            {
                // The common bypass retains no game/session object and allocates no
                // builder or requested-tag array. The transpiled body performs only
                // predictable inactive branches around its optional hook points.
                return;
            }

            FetchRequestTopologyVersion fetchTopologyVersion =
                session.FetchRequestTopology.CaptureVersion();
            WorldParentTopologySnapshot worldTopology =
                session.WorldParentTopology.CaptureSnapshot();
            FetchTemperatureEligibilityBuilder builder =
                reusableThreadBuilder ??
                (reusableThreadBuilder =
                    new FetchTemperatureEligibilityBuilder());
            builder.Begin(
                session.Generation,
                activeConstraints,
                fetchTopologyVersion,
                worldTopology);

            var invocation =
                AuthoritativeFetchEligibilityBuildInvocation.Active(
                    session,
                    builder);
            currentThreadBuildInvocation = invocation;
            __state = invocation;
        }

        internal static IEnumerable<CodeInstruction>
            UpdateStorageFetchableBitsTranspiler(
                IEnumerable<CodeInstruction> instructions,
                ILGenerator generator)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            var sourceInstructions = new List<CodeInstruction>(instructions);
            int[] candidateIndices = CreateCandidateInstructionIndices(
                sourceInstructions.Count);
            FieldInfo fetchMapField = RequireFetchMapField();
            MethodInfo sortedWorldIdGetter = RequireSortedWorldIdGetter();
            MethodInfo fetchMapTryGetValue = RequireFetchMapTryGetValue();
            FieldInfo storageFetchableTagsField =
                RequireStorageFetchableTagsField();
            FieldInfo fetchChoreTagsField = RequireFetchChoreTagsField();
            MethodInfo unionRequestedTags = RequireRequestedTagUnionMethod();

            int parentWorldSectionStartIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesParentWorldFetchMapSectionStart(
                        sourceInstructions,
                        index,
                        fetchMapField,
                        sortedWorldIdGetter,
                        fetchMapTryGetValue),
                    "GlobalChoreProvider.UpdateStorageFetchableBits " +
                    "parent-world fetch-map section start");
            int parentWorldIdentityGetterIndex =
                parentWorldSectionStartIndex + 3;

            int selectedFetchChoreIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesSelectedFetchChoreTraversal(
                        sourceInstructions,
                        index,
                        storageFetchableTagsField,
                        fetchChoreTagsField,
                        unionRequestedTags),
                    "GlobalChoreProvider.UpdateStorageFetchableBits selected " +
                    "FetchChore traversal");
            int selectedFetchChoreLocalIndex =
                sourceInstructions[selectedFetchChoreIndex + 1]
                    .LocalIndex();
            int requestedTagUnionIndex = selectedFetchChoreIndex + 3;

            if (parentWorldIdentityGetterIndex + 1 >=
                    sourceInstructions.Count ||
                requestedTagUnionIndex + 1 >= sourceInstructions.Count)
            {
                throw new HarmonyPatchContractViolationException(
                    "GlobalChoreProvider.UpdateStorageFetchableBits semantic " +
                    "anchors do not have safe continuation instructions.");
            }

            MethodInfo isBuildActiveHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                    nameof(IsAuthoritativeFetchEligibilityBuildActive),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    Array.Empty<Type>());
            MethodInfo beginParentWorldHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                    nameof(BeginParentWorldFetchMapSection),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(void),
                    new[] { typeof(int) });
            MethodInfo recordSelectedFetchChoreHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiAuthoritativeFetchTemperatureEligibilityPatches),
                    nameof(RecordSelectedFetchChore),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(void),
                    new[] { typeof(FetchChore) });

            LocalBuilder isBuildActiveLocal =
                generator.DeclareLocal(typeof(bool));
            Label skipParentWorldHookLabel = generator.DefineLabel();
            Label skipSelectedFetchChoreHookLabel = generator.DefineLabel();
            sourceInstructions[parentWorldIdentityGetterIndex + 1].labels.Add(
                skipParentWorldHookLabel);
            sourceInstructions[requestedTagUnionIndex + 1].labels.Add(
                skipSelectedFetchChoreHookLabel);

            var instrumentedInstructions = new List<CodeInstruction>(
                sourceInstructions.Count + 11);
            instrumentedInstructions.Add(new CodeInstruction(
                OpCodes.Call,
                isBuildActiveHook));
            instrumentedInstructions.Add(CodeInstruction2.StoreLocal(
                isBuildActiveLocal.LocalIndex));

            for (int instructionIndex = 0;
                 instructionIndex < sourceInstructions.Count;
                 instructionIndex++)
            {
                instrumentedInstructions.Add(
                    sourceInstructions[instructionIndex]);

                if (instructionIndex == parentWorldIdentityGetterIndex)
                {
                    // List<int>.get_Item leaves the exact parent-world ID needed by
                    // Klei's TryGetValue on the stack. Duplicate it only on the
                    // active branch so Klei's original stack is identical on both
                    // paths and no second fetchMap lookup is introduced.
                    instrumentedInstructions.Add(
                        CodeInstruction2.LoadLocal(
                            isBuildActiveLocal.LocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Brfalse,
                        skipParentWorldHookLabel));
                    instrumentedInstructions.Add(
                        new CodeInstruction(OpCodes.Dup));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        beginParentWorldHook));
                }

                if (instructionIndex == requestedTagUnionIndex)
                {
                    // Klei has already checked chore type, destination existence,
                    // and reachability before UnionWith. Capturing immediately
                    // afterward reuses that authoritative selection decision.
                    instrumentedInstructions.Add(
                        CodeInstruction2.LoadLocal(
                            isBuildActiveLocal.LocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Brfalse,
                        skipSelectedFetchChoreHookLabel));
                    instrumentedInstructions.Add(
                        CodeInstruction2.LoadLocal(
                            selectedFetchChoreLocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        recordSelectedFetchChoreHook));
                }
            }

            return instrumentedInstructions;
        }

        internal static void UpdateStorageFetchableBitsPostfix(
            AuthoritativeFetchEligibilityBuildInvocation __state)
        {
            if (!__state.IsActive || !__state.IsCandidateValid)
            {
                return;
            }

            RequireCurrentThreadBuildInvocation(__state);
            FetchTemperatureEligibilitySnapshot candidate =
                __state.Builder.Build();
            __state.MarkCandidateBuilt();
            if (__state.Session.TryPublishFetchTemperatureEligibility(candidate))
            {
                __state.MarkPublicationAccepted();
            }
        }

        internal static Exception? UpdateStorageFetchableBitsFinalizer(
            Exception? __exception,
            AuthoritativeFetchEligibilityBuildInvocation __state)
        {
            if (!__state.IsActive)
            {
                return __exception;
            }

            try
            {
                RequireCurrentThreadBuildInvocation(__state);
                if (!__state.IsCandidateBuilt ||
                    !__state.IsPublicationAccepted)
                {
                    // Discard is intentionally safe after Build. A publication can
                    // be rejected only after normalization has completed, whereas
                    // an original/transpiler exception leaves a partial build. One
                    // finalizer path releases both cases without guessing progress.
                    __state.Builder.Discard();
                }
            }
            finally
            {
                currentThreadBuildInvocation = null;
            }

            return __exception;
        }

        internal static void ClearableHasDestinationPostfix(
            Pickupable pickupable,
            ref bool __result)
        {
            bool originalHasDestination = __result;
            if (!originalHasDestination ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            ActiveTemperatureConstraintSnapshot activeConstraints =
                session.TemperatureConstraints.CaptureSnapshot();
            if (activeConstraints.EnabledConstraintCount == 0)
            {
                // Preserve Klei's result without touching PrimaryElement, topology,
                // the combined snapshot, or a decision bucket on the bypass path.
                return;
            }

            PrimaryElement? primaryElement = null;
            if (pickupable != null)
            {
                // Capture the Unity-backed component property exactly once for this
                // decision; all later temperature work uses this local reference.
                primaryElement = pickupable.PrimaryElement;
            }

            if (pickupable == null || primaryElement == null)
            {
                __result = ClearableDestinationSweepEligibility.AllowsClearing(
                    new ClearableDestinationSweepEligibilityInput(
                        originalHasDestination,
                        activeConstraints.EnabledConstraintCount,
                        hasPrimaryElement: false,
                        isParentWorldResolved: false,
                        isEligibilitySnapshotCurrent: false,
                        currentEligibilityAllowsPickup: false));
                return;
            }

            WorldParentTopologySnapshot worldTopology =
                session.WorldParentTopology.CaptureSnapshot();
            int worldId = pickupable.GetMyWorldId();
            int parentWorldId = -1;
            bool isParentWorldResolved =
                worldId >= 0 &&
                worldTopology.TryResolveParentWorld(
                    worldId,
                    out parentWorldId);
            if (!isParentWorldResolved)
            {
                __result = ClearableDestinationSweepEligibility.AllowsClearing(
                    new ClearableDestinationSweepEligibilityInput(
                        originalHasDestination,
                        activeConstraints.EnabledConstraintCount,
                        hasPrimaryElement: true,
                        isParentWorldResolved: false,
                        isEligibilitySnapshotCurrent: false,
                        currentEligibilityAllowsPickup: false));
                return;
            }

            FetchRequestTopologyVersion currentFetchTopologyVersion =
                session.FetchRequestTopology.CaptureVersion();
            FetchTemperatureEligibilitySnapshot? eligibilitySnapshot =
                session.CurrentFetchTemperatureEligibility;
            bool isEligibilitySnapshotCurrent =
                eligibilitySnapshot != null &&
                eligibilitySnapshot.GameSessionGeneration.Equals(
                    session.Generation) &&
                eligibilitySnapshot.ConstraintGeneration.Equals(
                    activeConstraints.Generation) &&
                eligibilitySnapshot.FetchTopologyVersion.Equals(
                    currentFetchTopologyVersion) &&
                eligibilitySnapshot.WorldTopologyVersion.Equals(
                    worldTopology.Version) &&
                worldTopology.GameSessionGeneration.Equals(
                    session.Generation);

            bool currentEligibilityAllowsPickup = false;
            KPrefabID? prefabIdentity = pickupable.KPrefabID;
            if (isEligibilitySnapshotCurrent &&
                prefabIdentity != null &&
                eligibilitySnapshot!.TryGetStorageEligibility(
                    parentWorldId,
                    prefabIdentity.PrefabTag,
                    out var allowedTemperatures))
            {
                TemperatureDecisionBucket temperatureBucket =
                    TemperatureDecisionBucket.FromTemperature(
                        primaryElement.Temperature);
                currentEligibilityAllowsPickup =
                    allowedTemperatures.Allows(temperatureBucket);
            }

            __result = ClearableDestinationSweepEligibility.AllowsClearing(
                new ClearableDestinationSweepEligibilityInput(
                    originalHasDestination,
                    activeConstraints.EnabledConstraintCount,
                    hasPrimaryElement: true,
                    isParentWorldResolved: true,
                    isEligibilitySnapshotCurrent,
                    currentEligibilityAllowsPickup));
        }

        private static bool IsAuthoritativeFetchEligibilityBuildActive() =>
            currentThreadBuildInvocation != null &&
            currentThreadBuildInvocation.IsActive;

        private static void BeginParentWorldFetchMapSection(int parentWorldId)
        {
            AuthoritativeFetchEligibilityBuildInvocation? invocation =
                currentThreadBuildInvocation;
            if (invocation == null || !invocation.IsCandidateValid)
            {
                return;
            }

            if (parentWorldId < 0)
            {
                invocation.RejectCandidate();
                return;
            }

            invocation.BeginParentWorldSection(parentWorldId);
        }

        private static void RecordSelectedFetchChore(FetchChore fetchChore)
        {
            AuthoritativeFetchEligibilityBuildInvocation? invocation =
                currentThreadBuildInvocation;
            if (invocation == null || !invocation.IsCandidateValid)
            {
                return;
            }

            if (fetchChore == null ||
                !invocation.TryGetCurrentParentWorldId(
                    out var parentWorldId) ||
                fetchChore.tags == null ||
                fetchChore.destination == null ||
                fetchChore.destination.gameObject == null)
            {
                // Klei's selected path proved these values moments earlier, but a
                // defensive adapter must reject the whole candidate if that proof
                // cannot be translated. It never turns missing evidence into an
                // unconstrained destination.
                invocation.RejectCandidate();
                return;
            }

            var requestedTags = new Tag[fetchChore.tags.Count];
            fetchChore.tags.CopyTo(requestedTags);
            int destinationGameObjectInstanceId =
                fetchChore.destination.gameObject.GetInstanceID();
            if (invocation.Session.TemperatureLimitComponents.TryGetConstraint(
                    destinationGameObjectInstanceId,
                    out var destinationConstraint,
                    out _) &&
                destinationConstraint.IsEnabled)
            {
                invocation.Builder.AddTemperatureConstrainedFetchRequest(
                    parentWorldId,
                    requestedTags,
                    destinationConstraint);
                return;
            }

            invocation.Builder.AddUnconstrainedFetchRequest(
                parentWorldId,
                requestedTags);
        }

        private static bool MatchesParentWorldFetchMapSectionStart(
            IReadOnlyList<CodeInstruction> instructions,
            int fetchMapFieldIndex,
            FieldInfo fetchMapField,
            MethodInfo sortedWorldIdGetter,
            MethodInfo fetchMapTryGetValue)
        {
            if (fetchMapFieldIndex < 1 ||
                fetchMapFieldIndex + 5 >= instructions.Count ||
                instructions[fetchMapFieldIndex - 1].opcode !=
                    OpCodes.Ldarg_0 ||
                !IsFieldLoad(
                    instructions[fetchMapFieldIndex],
                    fetchMapField) ||
                !IsLoadLocal(instructions[fetchMapFieldIndex + 1]) ||
                !IsLoadLocal(instructions[fetchMapFieldIndex + 2]) ||
                !IsCall(
                    instructions[fetchMapFieldIndex + 3],
                    sortedWorldIdGetter) ||
                !IsLoadLocalAddress(
                    instructions[fetchMapFieldIndex + 4]) ||
                !IsCall(
                    instructions[fetchMapFieldIndex + 5],
                    fetchMapTryGetValue))
            {
                return false;
            }

            return instructions[fetchMapFieldIndex + 1].LocalIndex() !=
                instructions[fetchMapFieldIndex + 2].LocalIndex();
        }

        private static bool MatchesSelectedFetchChoreTraversal(
            IReadOnlyList<CodeInstruction> instructions,
            int storageFetchableTagsFieldIndex,
            FieldInfo storageFetchableTagsField,
            FieldInfo fetchChoreTagsField,
            MethodInfo unionRequestedTags)
        {
            if (storageFetchableTagsFieldIndex < 1 ||
                storageFetchableTagsFieldIndex + 4 >= instructions.Count ||
                instructions[storageFetchableTagsFieldIndex - 1].opcode !=
                    OpCodes.Ldarg_0 ||
                !IsFieldLoad(
                    instructions[storageFetchableTagsFieldIndex],
                    storageFetchableTagsField) ||
                !IsLoadLocal(
                    instructions[storageFetchableTagsFieldIndex + 1]) ||
                !IsFieldLoad(
                    instructions[storageFetchableTagsFieldIndex + 2],
                    fetchChoreTagsField) ||
                !IsCall(
                    instructions[storageFetchableTagsFieldIndex + 3],
                    unionRequestedTags))
            {
                return false;
            }

            // A continuation is required because the inactive branch lands on the
            // untouched instruction immediately after Klei's UnionWith call.
            return storageFetchableTagsFieldIndex + 4 < instructions.Count;
        }

        private static void RequireCurrentThreadBuildInvocation(
            AuthoritativeFetchEligibilityBuildInvocation invocation)
        {
            if (!ReferenceEquals(currentThreadBuildInvocation, invocation))
            {
                throw new InvalidOperationException(
                    "The authoritative fetch eligibility build state no longer " +
                    "matches the invocation being completed.");
            }
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

        private static bool IsLoadLocalAddress(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloca_S ||
            instruction.opcode == OpCodes.Ldloca;

        private static FieldInfo RequireFetchMapField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(GlobalChoreProvider),
                "fetchMap",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(Dictionary<int, List<FetchChore>>));

        private static MethodInfo RequireSortedWorldIdGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(List<int>),
                "get_Item",
                DeclaredMemberVisibility.Public,
                typeof(int),
                new[] { typeof(int) });

        private static MethodInfo RequireFetchMapTryGetValue() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Dictionary<int, List<FetchChore>>),
                "TryGetValue",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                new[]
                {
                    typeof(int),
                    typeof(List<FetchChore>).MakeByRefType()
                });

        private static FieldInfo RequireStorageFetchableTagsField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(GlobalChoreProvider),
                "storageFetchableTags",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(HashSet<Tag>));

        private static FieldInfo RequireFetchChoreTagsField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FetchChore),
                "tags",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(HashSet<Tag>));

        private static MethodInfo RequireRequestedTagUnionMethod() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(HashSet<Tag>),
                "UnionWith",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(IEnumerable<Tag>) });

        /// <summary>
        /// Exact state proving whether Klei was about to remove one map entry.
        /// </summary>
        internal sealed class FetchChoreRemovalObservation
        {
            internal static readonly FetchChoreRemovalObservation Inactive =
                new FetchChoreRemovalObservation();

            private FetchChoreRemovalObservation()
            {
                Session = null!;
            }

            private FetchChoreRemovalObservation(
                DeliveryTemperatureGameSession session)
            {
                Session = session;
            }

            internal bool IsEffective => Session != null;

            internal DeliveryTemperatureGameSession Session { get; }

            internal static FetchChoreRemovalObservation Effective(
                DeliveryTemperatureGameSession session) =>
                new FetchChoreRemovalObservation(session);
        }

        /// <summary>
        /// Prefix evidence used to suppress duplicate tag-change invalidations.
        /// </summary>
        internal sealed class FetchChoreTagChangeObservation
        {
            internal static readonly FetchChoreTagChangeObservation Inactive =
                new FetchChoreTagChangeObservation();

            private FetchChoreTagChangeObservation()
            {
                Session = null!;
                PriorRequestedTags = null!;
            }

            private FetchChoreTagChangeObservation(
                DeliveryTemperatureGameSession session,
                RequestedTagSetSnapshot priorRequestedTags)
            {
                Session = session;
                PriorRequestedTags = priorRequestedTags;
            }

            internal bool IsActive => Session != null;

            internal DeliveryTemperatureGameSession Session { get; }

            internal RequestedTagSetSnapshot PriorRequestedTags { get; }

            internal static FetchChoreTagChangeObservation Active(
                DeliveryTemperatureGameSession session,
                RequestedTagSetSnapshot priorRequestedTags) =>
                new FetchChoreTagChangeObservation(
                    session,
                    priorRequestedTags);
        }

        /// <summary>
        /// Immutable requested-tag set identity captured around one event callback.
        /// </summary>
        internal sealed class RequestedTagSetSnapshot
        {
            private readonly Tag[] requestedTags;

            private RequestedTagSetSnapshot(
                bool wasTagSetAvailable,
                Tag[] requestedTags)
            {
                WasTagSetAvailable = wasTagSetAvailable;
                this.requestedTags = requestedTags;
            }

            private bool WasTagSetAvailable { get; }

            internal static RequestedTagSetSnapshot Capture(
                HashSet<Tag>? sourceRequestedTags)
            {
                if (sourceRequestedTags == null)
                {
                    return new RequestedTagSetSnapshot(
                        wasTagSetAvailable: false,
                        Array.Empty<Tag>());
                }

                var copiedRequestedTags =
                    new Tag[sourceRequestedTags.Count];
                sourceRequestedTags.CopyTo(copiedRequestedTags);
                return new RequestedTagSetSnapshot(
                    wasTagSetAvailable: true,
                    copiedRequestedTags);
            }

            internal bool Matches(HashSet<Tag>? currentRequestedTags)
            {
                if (!WasTagSetAvailable)
                {
                    return currentRequestedTags == null;
                }

                if (currentRequestedTags == null ||
                    currentRequestedTags.Count != requestedTags.Length)
                {
                    return false;
                }

                for (int tagIndex = 0;
                     tagIndex < requestedTags.Length;
                     tagIndex++)
                {
                    if (!currentRequestedTags.Contains(
                            requestedTags[tagIndex]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// One exact prefix-to-finalizer build lifetime on the main thread.
        /// </summary>
        internal sealed class
            AuthoritativeFetchEligibilityBuildInvocation
        {
            internal static readonly
                AuthoritativeFetchEligibilityBuildInvocation Inactive =
                    new AuthoritativeFetchEligibilityBuildInvocation();

            private bool hasCurrentParentWorldId;
            private int currentParentWorldId;

            private AuthoritativeFetchEligibilityBuildInvocation()
            {
                Session = null!;
                Builder = null!;
                IsCandidateValid = false;
            }

            private AuthoritativeFetchEligibilityBuildInvocation(
                DeliveryTemperatureGameSession session,
                FetchTemperatureEligibilityBuilder builder)
            {
                Session = session;
                Builder = builder;
                IsCandidateValid = true;
            }

            internal bool IsActive => Session != null;

            internal DeliveryTemperatureGameSession Session { get; }

            internal FetchTemperatureEligibilityBuilder Builder { get; }

            internal bool IsCandidateValid { get; private set; }

            internal bool IsCandidateBuilt { get; private set; }

            internal bool IsPublicationAccepted { get; private set; }

            internal static AuthoritativeFetchEligibilityBuildInvocation Active(
                DeliveryTemperatureGameSession session,
                FetchTemperatureEligibilityBuilder builder) =>
                new AuthoritativeFetchEligibilityBuildInvocation(
                    session,
                    builder);

            internal void BeginParentWorldSection(int parentWorldId)
            {
                if (!IsActive || !IsCandidateValid || parentWorldId < 0)
                {
                    throw new InvalidOperationException(
                        "Only a valid active fetch eligibility invocation can " +
                        "begin a parent-world section.");
                }

                currentParentWorldId = parentWorldId;
                hasCurrentParentWorldId = true;
            }

            internal bool TryGetCurrentParentWorldId(
                out int parentWorldId)
            {
                parentWorldId = currentParentWorldId;
                return IsActive &&
                    IsCandidateValid &&
                    hasCurrentParentWorldId;
            }

            internal void RejectCandidate()
            {
                if (!IsActive)
                {
                    throw new InvalidOperationException(
                        "An inactive fetch eligibility invocation cannot reject " +
                        "a candidate.");
                }

                IsCandidateValid = false;
                hasCurrentParentWorldId = false;
                currentParentWorldId = 0;
            }

            internal void MarkCandidateBuilt()
            {
                if (!IsActive ||
                    !IsCandidateValid ||
                    IsCandidateBuilt)
                {
                    throw new InvalidOperationException(
                        "A valid active fetch eligibility candidate can be marked " +
                        "built exactly once.");
                }

                IsCandidateBuilt = true;
            }

            internal void MarkPublicationAccepted()
            {
                if (!IsCandidateBuilt || IsPublicationAccepted)
                {
                    throw new InvalidOperationException(
                        "A built fetch eligibility candidate can be marked " +
                        "published exactly once.");
                }

                IsPublicationAccepted = true;
            }
        }

    }
}
