#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Adds temperature-aware ordering and duplicate suppression to Klei's
    /// authoritative pickup grouping update.
    /// </summary>
    /// <remarks>
    /// This adapter deliberately has no Harmony discovery attributes. The
    /// coordinated installer may activate it only after every exact reflection,
    /// instruction, worker-read, and Harmony-authority contract has passed.
    /// Until then, this type is compiled but inert.
    /// </remarks>
    internal static class KleiPickupTemperatureGroupingPatches
    {
        private static readonly PickupTemperatureGroupingSession
            .ApplicableRequestedTagResolver<KPrefabID>
                ApplicableRequestedTagResolver =
                    ResolveApplicableRequestedTags;

        [ThreadStatic]
        private static PickupTemperatureGroupingSession?
            reusableThreadGroupingSession;

        internal static MethodInfo
            ResolveFetchablesByPrefabIdUpdatePickupsTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(FetchManager.FetchablesByPrefabId),
                "UpdatePickups",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(Navigator), typeof(int) });

        internal static MethodInfo
            ResolvePickupComparerIncludingPriorityCompareTarget()
        {
            Type comparerType =
                HarmonyPatchContractVerifier.RequireNestedType(
                    typeof(FetchManager),
                    "PickupComparerIncludingPriority",
                    DeclaredMemberVisibility.NonPublic);
            return HarmonyPatchContractVerifier.RequireStaticMethod(
                comparerType,
                "Compare",
                DeclaredMemberVisibility.NonPublic,
                typeof(int),
                new[]
                {
                    typeof(FetchManager.Pickup),
                    typeof(FetchManager.Pickup)
                });
        }

        /// <summary>
        /// Performs the complete no-patching preflight that Gate D consumes before
        /// it is permitted to mutate either Klei method.
        /// </summary>
        internal static void VerifyKleiPickupGroupingPatchContracts()
        {
            VerifyPickupWorkerManagedReadContracts();

            MethodInfo updatePickupsTarget =
                ResolveFetchablesByPrefabIdUpdatePickupsTarget();
            ILGenerator updatePickupsGenerator;
            List<CodeInstruction> updatePickupsInstructions =
                PatchProcessor.GetOriginalInstructions(
                    updatePickupsTarget,
                    out updatePickupsGenerator);
            _ = updatePickupsGenerator;
            _ = new List<CodeInstruction>(
                UpdatePickupsTranspiler(updatePickupsInstructions));

            MethodInfo pickupComparerTarget =
                ResolvePickupComparerIncludingPriorityCompareTarget();
            ILGenerator pickupComparerGenerator;
            List<CodeInstruction> pickupComparerInstructions =
                PatchProcessor.GetOriginalInstructions(
                    pickupComparerTarget,
                    out pickupComparerGenerator);
            _ = new List<CodeInstruction>(
                PickupComparerTranspiler(
                    pickupComparerInstructions,
                    pickupComparerGenerator));
        }

        /// <summary>
        /// Proves that every read permitted from a worker-capable pickup update is
        /// either an exact field access or an exact reviewed managed-only method.
        /// </summary>
        internal static void VerifyPickupWorkerManagedReadContracts()
        {
            FieldInfo navigatorAnchorCellField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(Navigator),
                    "AnchorCell",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(int));
            MethodInfo navigatorAnchorCellGetter =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(Navigator),
                    "GetAnchorCell",
                    DeclaredMemberVisibility.Public,
                    typeof(int),
                    Array.Empty<Type>());
            RequireDirectManagedInstanceFieldGetter(
                navigatorAnchorCellGetter,
                navigatorAnchorCellField,
                "Navigator.GetAnchorCell");

            _ = HarmonyPatchContractVerifier.RequireField(
                typeof(Grid),
                "WorldIdx",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Static,
                typeof(byte[]));
            _ = RequirePickupableField();

            _ = HarmonyPatchContractVerifier.RequireField(
                typeof(Pickupable),
                "KPrefabID",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(KPrefabID));
            _ = HarmonyPatchContractVerifier.RequireField(
                typeof(KPrefabID),
                "InstanceID",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(int));
            FieldInfo prefabTagField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(KPrefabID),
                    "PrefabTag",
                    DeclaredMemberVisibility.Public,
                    FieldStorageKind.Instance,
                    typeof(Tag));
            FieldInfo additionalTagsField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(KPrefabID),
                    "tags",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(HashSet<Tag>));
            MethodInfo hasTagMethod =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(KPrefabID),
                    "HasTag",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(Tag) });
            RequireManagedKPrefabIdHasTagBody(
                hasTagMethod,
                prefabTagField,
                additionalTagsField);

            FieldInfo primaryElementField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(Pickupable),
                    "primaryElement",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(PrimaryElement));
            MethodInfo primaryElementGetter =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(Pickupable),
                    "get_PrimaryElement",
                    DeclaredMemberVisibility.Public,
                    typeof(PrimaryElement),
                    Array.Empty<Type>());
            RequireDirectManagedInstanceFieldGetter(
                primaryElementGetter,
                primaryElementField,
                "Pickupable.PrimaryElement");

            FieldInfo internalTemperatureField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(PrimaryElement),
                    "_Temperature",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(float));
            MethodInfo internalTemperatureGetter =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(PrimaryElement),
                    "get_InternalTemperature",
                    DeclaredMemberVisibility.Public,
                    typeof(float),
                    Array.Empty<Type>());
            RequireDirectManagedInstanceFieldGetter(
                internalTemperatureGetter,
                internalTemperatureField,
                "PrimaryElement.InternalTemperature");
        }

        internal static void UpdatePickupsPrefix(
            Navigator navigator,
            out PickupTemperatureGroupingInvocation __state)
        {
            __state = PickupTemperatureGroupingInvocation.Inactive;
            if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            ActiveTemperatureConstraintSnapshot activeConstraints =
                session.TemperatureConstraints.CaptureSnapshot();
            if (activeConstraints.EnabledConstraintCount == 0)
            {
                // This is the ordinary bypass. It allocates no grouping session,
                // reads no navigator/grid state, and leaves both transpiled hooks
                // equivalent to Klei's original ordering and suppression.
                return;
            }

            WorldParentTopologySnapshot worldTopology =
                session.WorldParentTopology.CaptureSnapshot();
            int? resolvedParentWorldId = ResolveNavigatorParentWorldId(
                navigator,
                worldTopology);
            FetchTemperatureEligibilitySnapshot? eligibilitySnapshot =
                session.CurrentFetchTemperatureEligibility;
            PickupTemperatureGroupingSession groupingSession =
                TakeReusableThreadGroupingSession();

            try
            {
                groupingSession.Begin(
                    session,
                    resolvedParentWorldId,
                    activeConstraints,
                    eligibilitySnapshot,
                    worldTopology);
            }
            catch
            {
                groupingSession.Discard();
                TryRetainReusableThreadGroupingSession(groupingSession);
                throw;
            }

            ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                .SessionScopeToken scopeToken;
            try
            {
                scopeToken = ThreadConfinedSessionSlot<
                    PickupTemperatureGroupingSession>.Enter(
                        session.Generation,
                        groupingSession);
            }
            catch
            {
                groupingSession.Discard();
                TryRetainReusableThreadGroupingSession(groupingSession);
                throw;
            }

            __state = PickupTemperatureGroupingInvocation.Active(
                groupingSession,
                scopeToken);
        }

        internal static IEnumerable<CodeInstruction> UpdatePickupsTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            var sourceInstructions = new List<CodeInstruction>(instructions);
            PickupDuplicateSuppressionAnchor anchor =
                RequirePickupDuplicateSuppressionAnchor(sourceInstructions);
            MethodInfo sameTemperatureClassMethod =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiPickupTemperatureGroupingPatches),
                    nameof(HaveSameTemperatureEligibilityClass),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    new[]
                    {
                        typeof(FetchManager.Pickup),
                        typeof(FetchManager.Pickup)
                    });

            var temperatureSuppressionInstructions =
                new List<CodeInstruction>(4)
                {
                    CodeInstruction2.LoadLocal(
                        anchor.PreviousPickupLocalIndex),
                    CodeInstruction2.LoadLocal(
                        anchor.CurrentPickupLocalIndex),
                    new CodeInstruction(
                        OpCodes.Call,
                        sameTemperatureClassMethod),
                    new CodeInstruction(
                        OpCodes.Brfalse,
                        anchor.NotDuplicateBranchTarget)
                };
            sourceInstructions.InsertRange(
                anchor.ExtensionInsertionIndex,
                temperatureSuppressionInstructions);
            return sourceInstructions;
        }

        internal static void UpdatePickupsPostfix(
            PickupTemperatureGroupingInvocation __state)
        {
            if (__state.IsActive)
            {
                __state.GroupingSession.Complete();
            }
        }

        internal static Exception? UpdatePickupsFinalizer(
            Exception? __exception,
            PickupTemperatureGroupingInvocation __state)
        {
            if (!__state.IsActive)
            {
                return __exception;
            }

            Exception? cleanupException = null;
            bool scopeExited = false;
            try
            {
                // Complete and Discard intentionally share an idempotent release
                // path, so this also covers an exception before the postfix ran.
                __state.GroupingSession.Discard();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            try
            {
                ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                    .Exit(__state.ScopeToken);
                scopeExited = true;
            }
            catch (Exception exception)
            {
                if (cleanupException == null)
                {
                    cleanupException = exception;
                }
            }

            if (scopeExited)
            {
                TryRetainReusableThreadGroupingSession(
                    __state.GroupingSession);
            }

            // Never replace the game's original failure with cleanup diagnostics.
            // With no original failure, a lifecycle violation remains fail-closed.
            return __exception ?? cleanupException;
        }

        internal static IEnumerable<CodeInstruction> PickupComparerTranspiler(
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
            PickupComparatorExtensionAnchor anchor =
                RequirePickupComparatorExtensionAnchor(sourceInstructions);
            MethodInfo compareTemperatureClassesMethod =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiPickupTemperatureGroupingPatches),
                    nameof(CompareTemperatureEligibilityClasses),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(int),
                    new[]
                    {
                        typeof(FetchManager.Pickup),
                        typeof(FetchManager.Pickup)
                    });

            CodeInstruction originalContinuationInstruction =
                sourceInstructions[anchor.ExtensionInsertionIndex];
            var priorContinuationLabels = new List<Label>(
                originalContinuationInstruction.labels);
            originalContinuationInstruction.labels.Clear();
            Label temperatureClassesEqualLabel = generator.DefineLabel();
            originalContinuationInstruction.labels.Add(
                temperatureClassesEqualLabel);

            var firstInjectedInstruction =
                new CodeInstruction(OpCodes.Ldarg_0);
            firstInjectedInstruction.labels.AddRange(
                priorContinuationLabels);
            var temperatureComparatorInstructions =
                new List<CodeInstruction>(8)
                {
                    firstInjectedInstruction,
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(
                        OpCodes.Call,
                        compareTemperatureClassesMethod),
                    CodeInstruction2.StoreLocal(
                        anchor.ComparisonResultLocalIndex),
                    CodeInstruction2.LoadLocal(
                        anchor.ComparisonResultLocalIndex),
                    new CodeInstruction(
                        OpCodes.Brfalse,
                        temperatureClassesEqualLabel),
                    CodeInstruction2.LoadLocal(
                        anchor.ComparisonResultLocalIndex),
                    new CodeInstruction(OpCodes.Ret)
                };
            sourceInstructions.InsertRange(
                anchor.ExtensionInsertionIndex,
                temperatureComparatorInstructions);
            return sourceInstructions;
        }

        private static int CompareTemperatureEligibilityClasses(
            FetchManager.Pickup firstCandidate,
            FetchManager.Pickup secondCandidate)
        {
            if (!ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                .TryGetCurrent(out var groupingSession))
            {
                return 0;
            }

            TemperatureEligibilityClassKey firstTemperatureEligibilityClassKey =
                GetTemperatureEligibilityClassKey(
                    groupingSession,
                    firstCandidate);
            TemperatureEligibilityClassKey secondTemperatureEligibilityClassKey =
                GetTemperatureEligibilityClassKey(
                    groupingSession,
                    secondCandidate);
            return firstTemperatureEligibilityClassKey.CompareTo(
                secondTemperatureEligibilityClassKey);
        }

        private static bool HaveSameTemperatureEligibilityClass(
            FetchManager.Pickup firstCandidate,
            FetchManager.Pickup secondCandidate)
        {
            if (!ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                .TryGetCurrent(out var groupingSession))
            {
                return true;
            }

            TemperatureEligibilityClassKey firstTemperatureEligibilityClassKey =
                GetTemperatureEligibilityClassKey(
                    groupingSession,
                    firstCandidate);
            TemperatureEligibilityClassKey secondTemperatureEligibilityClassKey =
                GetTemperatureEligibilityClassKey(
                    groupingSession,
                    secondCandidate);
            return firstTemperatureEligibilityClassKey.Equals(
                secondTemperatureEligibilityClassKey);
        }

        private static TemperatureEligibilityClassKey
            GetTemperatureEligibilityClassKey(
                PickupTemperatureGroupingSession groupingSession,
                FetchManager.Pickup candidate)
        {
            Pickupable pickupable = candidate.pickupable;
            if (ReferenceEquals(pickupable, null))
            {
                return TemperatureEligibilityClassKey.MissingPrimaryElement();
            }

            // These component references and scalar values are the exact managed
            // reads proven by VerifyPickupWorkerManagedReadContracts. In
            // particular, the cached internal temperature avoids the live
            // callback-backed property that can cross an unsafe boundary.
            KPrefabID kPrefabId = pickupable.KPrefabID;
            PrimaryElement primaryElement = pickupable.PrimaryElement;
            bool hasPrimaryElement = !ReferenceEquals(primaryElement, null);
            float temperatureKelvin = hasPrimaryElement
                ? primaryElement!.InternalTemperature
                : 0.0f;
            if (ReferenceEquals(kPrefabId, null) || kPrefabId.InstanceID < 0)
            {
                return CreateUncachedConservativeTemperatureClass(
                    hasPrimaryElement,
                    temperatureKelvin);
            }

            var tagIdentity = new PickupTagIdentity(
                candidate.tagBitsHash,
                kPrefabId.PrefabTag);
            return groupingSession
                .ClassifyUsingApplicableRequestedTagResolver(
                    kPrefabId.InstanceID,
                    tagIdentity,
                    kPrefabId,
                    ApplicableRequestedTagResolver,
                    hasPrimaryElement,
                    temperatureKelvin);
        }

        private static TemperatureEligibilityClassKey
            CreateUncachedConservativeTemperatureClass(
                bool hasPrimaryElement,
                float temperatureKelvin) =>
            hasPrimaryElement
                ? TemperatureEligibilityClassKey.ExactDecisionBucket(
                    TemperatureDecisionBucket.FromTemperature(
                        temperatureKelvin))
                : TemperatureEligibilityClassKey.MissingPrimaryElement();

        private static IReadOnlyList<Tag> ResolveApplicableRequestedTags(
            KPrefabID kPrefabId,
            IReadOnlyList<Tag> requestedTagsForResolvedParentWorld)
        {
            List<Tag>? applicableRequestedTags = null;
            for (int requestedTagIndex = 0;
                 requestedTagIndex <
                    requestedTagsForResolvedParentWorld.Count;
                 requestedTagIndex++)
            {
                Tag requestedTag =
                    requestedTagsForResolvedParentWorld[requestedTagIndex];
                if (!kPrefabId.HasTag(requestedTag))
                {
                    continue;
                }

                if (applicableRequestedTags == null)
                {
                    applicableRequestedTags = new List<Tag>();
                }

                applicableRequestedTags.Add(requestedTag);
            }

            return applicableRequestedTags == null
                ? (IReadOnlyList<Tag>)Array.Empty<Tag>()
                : applicableRequestedTags;
        }

        private static int? ResolveNavigatorParentWorldId(
            Navigator navigator,
            WorldParentTopologySnapshot worldTopology)
        {
            if (ReferenceEquals(navigator, null))
            {
                return null;
            }

            // Capture the anchor exactly once. Grid.WorldIdx is a direct managed
            // byte-array read; all parent relationships thereafter come from the
            // immutable session topology snapshot.
            int anchorCell = navigator.GetAnchorCell();
            byte[] worldIndices = Grid.WorldIdx;
            if (worldIndices == null ||
                (uint)anchorCell >= (uint)worldIndices.Length)
            {
                return null;
            }

            int rawWorldId = worldIndices[anchorCell];
            return worldTopology.TryResolveParentWorld(
                rawWorldId,
                out var parentWorldId)
                ? parentWorldId
                : (int?)null;
        }

        private static PickupTemperatureGroupingSession
            TakeReusableThreadGroupingSession()
        {
            PickupTemperatureGroupingSession? groupingSession =
                reusableThreadGroupingSession;
            reusableThreadGroupingSession = null;
            return groupingSession ??
                new PickupTemperatureGroupingSession();
        }

        private static void TryRetainReusableThreadGroupingSession(
            PickupTemperatureGroupingSession groupingSession)
        {
            if (reusableThreadGroupingSession != null ||
                ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                    .TryGetCurrent(out _))
            {
                // Nested invocations are rare. Retain only the outermost completed
                // session so a thread cannot accumulate an unbounded object pool.
                return;
            }

            reusableThreadGroupingSession = groupingSession;
        }

        private static PickupComparatorExtensionAnchor
            RequirePickupComparatorExtensionAnchor(
                IReadOnlyList<CodeInstruction> instructions)
        {
            FieldInfo masterPriorityField = RequireMasterPriorityField();
            MethodInfo integerCompareTo =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(int),
                    "CompareTo",
                    DeclaredMemberVisibility.Public,
                    typeof(int),
                    new[] { typeof(int) });
            int[] candidateIndices = CreateCandidateInstructionIndices(
                instructions.Count);
            int anchorStartIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesPickupComparatorPriorityWindow(
                        instructions,
                        index,
                        masterPriorityField,
                        integerCompareTo),
                    "FetchManager.PickupComparerIncludingPriority.Compare " +
                    "post-priority extension anchor");
            int comparisonResultLocalIndex =
                GetLocalIndex(instructions[anchorStartIndex + 5]);
            int extensionInsertionIndex = anchorStartIndex + 10;
            if (extensionInsertionIndex >= instructions.Count)
            {
                throw new HarmonyPatchContractViolationException(
                    "The pickup comparator extension anchor has no safe original " +
                    "continuation instruction.");
            }

            CodeInstruction equalityBranch =
                instructions[anchorStartIndex + 7];
            CodeInstruction continuationInstruction =
                instructions[extensionInsertionIndex];
            if (!(equalityBranch.operand is Label equalityTarget) ||
                !continuationInstruction.labels.Contains(equalityTarget))
            {
                throw new HarmonyPatchContractViolationException(
                    "The pickup comparator equality branch does not target the " +
                    "reviewed post-priority continuation.");
            }

            for (int instructionIndex = anchorStartIndex;
                 instructionIndex <= extensionInsertionIndex;
                 instructionIndex++)
            {
                if (instructions[instructionIndex].blocks.Count != 0)
                {
                    throw new HarmonyPatchContractViolationException(
                        "The pickup comparator extension crosses an unreviewed " +
                        "exception-block boundary.");
                }
            }

            return new PickupComparatorExtensionAnchor(
                comparisonResultLocalIndex,
                extensionInsertionIndex);
        }

        private static bool MatchesPickupComparatorPriorityWindow(
            IReadOnlyList<CodeInstruction> instructions,
            int startIndex,
            FieldInfo masterPriorityField,
            MethodInfo integerCompareTo)
        {
            if (startIndex < 0 || startIndex + 9 >= instructions.Count ||
                !IsLoadArgumentAddress(instructions[startIndex], 1) ||
                !IsFieldAddressLoad(
                    instructions[startIndex + 1],
                    masterPriorityField) ||
                instructions[startIndex + 2].opcode != OpCodes.Ldarg_0 ||
                !IsFieldLoad(
                    instructions[startIndex + 3],
                    masterPriorityField) ||
                !IsCall(
                    instructions[startIndex + 4],
                    integerCompareTo) ||
                !IsStoreLocal(instructions[startIndex + 5]) ||
                !IsLoadLocal(instructions[startIndex + 6]) ||
                !IsBranchWhenFalse(instructions[startIndex + 7]) ||
                !IsLoadLocal(instructions[startIndex + 8]) ||
                instructions[startIndex + 9].opcode != OpCodes.Ret)
            {
                return false;
            }

            int resultLocalIndex =
                GetLocalIndex(instructions[startIndex + 5]);
            return GetLocalIndex(instructions[startIndex + 6]) ==
                    resultLocalIndex &&
                GetLocalIndex(instructions[startIndex + 8]) ==
                    resultLocalIndex;
        }

        private static PickupDuplicateSuppressionAnchor
            RequirePickupDuplicateSuppressionAnchor(
                IReadOnlyList<CodeInstruction> instructions)
        {
            FieldInfo masterPriorityField = RequireMasterPriorityField();
            FieldInfo tagBitsHashField = RequireTagBitsHashField();
            int[] candidateIndices = CreateCandidateInstructionIndices(
                instructions.Count);
            int suppressionStartIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesPickupDuplicateSuppressionWindow(
                        instructions,
                        index,
                        masterPriorityField),
                    "FetchManager.FetchablesByPrefabId.UpdatePickups duplicate " +
                    "suppression extension anchor");

            int previousPickupLocalIndex =
                GetLocalIndex(instructions[suppressionStartIndex]);
            int currentPickupLocalIndex =
                GetLocalIndex(instructions[suppressionStartIndex + 2]);
            int currentTagHashLocalIndex =
                GetLocalIndex(instructions[suppressionStartIndex + 5]);
            int previousTagHashLocalIndex =
                GetLocalIndex(instructions[suppressionStartIndex + 6]);
            if (previousPickupLocalIndex == currentPickupLocalIndex)
            {
                throw new HarmonyPatchContractViolationException(
                    "The pickup duplicate-suppression anchor does not reference " +
                    "two distinct candidate locals.");
            }

            _ = HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesPickupTagHashCapture(
                    instructions,
                    index,
                    previousPickupLocalIndex,
                    previousTagHashLocalIndex,
                    tagBitsHashField),
                "UpdatePickups previous pickup tag-hash capture");
            _ = HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => MatchesPickupTagHashCapture(
                    instructions,
                    index,
                    currentPickupLocalIndex,
                    currentTagHashLocalIndex,
                    tagBitsHashField),
                "UpdatePickups current pickup tag-hash capture");

            CodeInstruction notDuplicateBranch =
                instructions[suppressionStartIndex + 7];
            if (!(notDuplicateBranch.operand is Label notDuplicateTarget))
            {
                throw new HarmonyPatchContractViolationException(
                    "The pickup duplicate-suppression hash inequality branch has " +
                    "no typed label target.");
            }

            return new PickupDuplicateSuppressionAnchor(
                previousPickupLocalIndex,
                currentPickupLocalIndex,
                suppressionStartIndex + 8,
                notDuplicateTarget);
        }

        private static bool MatchesPickupDuplicateSuppressionWindow(
            IReadOnlyList<CodeInstruction> instructions,
            int startIndex,
            FieldInfo masterPriorityField)
        {
            if (startIndex < 0 || startIndex + 9 >= instructions.Count ||
                !IsLoadLocal(instructions[startIndex]) ||
                !IsFieldLoad(
                    instructions[startIndex + 1],
                    masterPriorityField) ||
                !IsLoadLocal(instructions[startIndex + 2]) ||
                !IsFieldLoad(
                    instructions[startIndex + 3],
                    masterPriorityField) ||
                !IsBranchWhenNotEqual(instructions[startIndex + 4]) ||
                !IsLoadLocal(instructions[startIndex + 5]) ||
                !IsLoadLocal(instructions[startIndex + 6]) ||
                !IsBranchWhenNotEqual(instructions[startIndex + 7]) ||
                instructions[startIndex + 8].opcode != OpCodes.Ldc_I4_1 ||
                !IsStoreLocal(instructions[startIndex + 9]))
            {
                return false;
            }

            return GetLocalIndex(instructions[startIndex]) !=
                GetLocalIndex(instructions[startIndex + 2]);
        }

        private static bool MatchesPickupTagHashCapture(
            IReadOnlyList<CodeInstruction> instructions,
            int startIndex,
            int pickupLocalIndex,
            int tagHashLocalIndex,
            FieldInfo tagBitsHashField) =>
            startIndex >= 0 &&
            startIndex + 2 < instructions.Count &&
            IsLoadLocal(instructions[startIndex]) &&
            GetLocalIndex(instructions[startIndex]) == pickupLocalIndex &&
            IsFieldLoad(
                instructions[startIndex + 1],
                tagBitsHashField) &&
            IsStoreLocal(instructions[startIndex + 2]) &&
            GetLocalIndex(instructions[startIndex + 2]) == tagHashLocalIndex;

        private static FieldInfo RequirePickupableField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FetchManager.Pickup),
                "pickupable",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(Pickupable));

        private static FieldInfo RequireMasterPriorityField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FetchManager.Pickup),
                "masterPriority",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(int));

        private static FieldInfo RequireTagBitsHashField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(FetchManager.Pickup),
                "tagBitsHash",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(int));

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
                "A structurally matched pickup instruction does not carry a " +
                "supported nonnegative local-variable identity.");
        }

        private static bool IsLoadArgumentAddress(
            CodeInstruction instruction,
            int expectedArgumentIndex) =>
            (instruction.opcode == OpCodes.Ldarga_S ||
             instruction.opcode == OpCodes.Ldarga) &&
            OperandRepresentsIndex(
                instruction.operand,
                expectedArgumentIndex);

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
                return parameter.Position == expectedIndex;
            }

            return false;
        }

        private static bool IsBranchWhenFalse(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Brfalse ||
            instruction.opcode == OpCodes.Brfalse_S;

        private static bool IsBranchWhenNotEqual(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Bne_Un ||
            instruction.opcode == OpCodes.Bne_Un_S;

        private static void RequireDirectManagedInstanceFieldGetter(
            MethodInfo getter,
            FieldInfo expectedField,
            string contractName)
        {
            byte[]? body = getter.GetMethodBody()?.GetILAsByteArray();
            if (body == null ||
                body.Length != 7 ||
                body[0] != 0x02 ||
                body[1] != 0x7B ||
                body[6] != 0x2A)
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " is no longer an exact managed instance-field getter.");
            }

            FieldInfo? resolvedField;
            try
            {
                resolvedField = getter.Module.ResolveField(
                    BitConverter.ToInt32(body, 2));
            }
            catch (Exception exception)
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " contains an unresolvable field-token operand.",
                    exception);
            }

            if (!Equals(resolvedField, expectedField))
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " reads a field other than the reviewed managed field.");
            }
        }

        private static void RequireManagedKPrefabIdHasTagBody(
            MethodInfo hasTagMethod,
            FieldInfo prefabTagField,
            FieldInfo additionalTagsField)
        {
            MethodInfo tagEqualityMethod =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(Tag),
                    "op_Equality",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(Tag), typeof(Tag) });
            MethodInfo additionalTagContainsMethod =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(HashSet<Tag>),
                    "Contains",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(Tag) });
            byte[]? body = hasTagMethod.GetMethodBody()?.GetILAsByteArray();
            if (body == null ||
                body.Length != 29 ||
                body[0] != 0x02 ||
                body[1] != 0x7B ||
                body[6] != 0x03 ||
                body[7] != 0x28 ||
                body[12] != 0x2D ||
                unchecked((sbyte)body[13]) != 13 ||
                body[14] != 0x02 ||
                body[15] != 0x7B ||
                body[20] != 0x03 ||
                body[21] != 0x6F ||
                body[26] != 0x2A ||
                body[27] != 0x17 ||
                body[28] != 0x2A)
            {
                throw new HarmonyPatchContractViolationException(
                    "KPrefabID.HasTag is no longer the reviewed managed-only " +
                    "prefab/additional-tag membership body.");
            }

            try
            {
                FieldInfo? resolvedPrefabTagField =
                    hasTagMethod.Module.ResolveField(
                        BitConverter.ToInt32(body, 2));
                MethodBase? resolvedTagEqualityMethod =
                    hasTagMethod.Module.ResolveMethod(
                        BitConverter.ToInt32(body, 8));
                FieldInfo? resolvedAdditionalTagsField =
                    hasTagMethod.Module.ResolveField(
                        BitConverter.ToInt32(body, 16));
                MethodBase? resolvedContainsMethod =
                    hasTagMethod.Module.ResolveMethod(
                        BitConverter.ToInt32(body, 22));
                if (!Equals(resolvedPrefabTagField, prefabTagField) ||
                    !Equals(resolvedTagEqualityMethod, tagEqualityMethod) ||
                    !Equals(
                        resolvedAdditionalTagsField,
                        additionalTagsField) ||
                    !Equals(
                        resolvedContainsMethod,
                        additionalTagContainsMethod))
                {
                    throw new HarmonyPatchContractViolationException(
                        "KPrefabID.HasTag no longer reads only the reviewed tag " +
                        "fields and managed membership methods.");
                }
            }
            catch (HarmonyPatchContractViolationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new HarmonyPatchContractViolationException(
                    "KPrefabID.HasTag contains an unresolvable managed-read " +
                    "operand.",
                    exception);
            }
        }

        /// <summary>
        /// Prefix-to-finalizer ownership for one exact UpdatePickups invocation.
        /// </summary>
        internal readonly struct PickupTemperatureGroupingInvocation
        {
            internal static readonly PickupTemperatureGroupingInvocation
                Inactive = default(PickupTemperatureGroupingInvocation);

            private PickupTemperatureGroupingInvocation(
                PickupTemperatureGroupingSession groupingSession,
                ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                    .SessionScopeToken scopeToken)
            {
                GroupingSession = groupingSession;
                ScopeToken = scopeToken;
            }

            internal bool IsActive => GroupingSession != null;

            internal PickupTemperatureGroupingSession GroupingSession
            {
                get;
            }

            internal ThreadConfinedSessionSlot<
                PickupTemperatureGroupingSession>.SessionScopeToken ScopeToken
            {
                get;
            }

            internal static PickupTemperatureGroupingInvocation Active(
                PickupTemperatureGroupingSession groupingSession,
                ThreadConfinedSessionSlot<PickupTemperatureGroupingSession>
                    .SessionScopeToken scopeToken) =>
                new PickupTemperatureGroupingInvocation(
                    groupingSession,
                    scopeToken);
        }

        private readonly struct PickupComparatorExtensionAnchor
        {
            internal PickupComparatorExtensionAnchor(
                int comparisonResultLocalIndex,
                int extensionInsertionIndex)
            {
                ComparisonResultLocalIndex = comparisonResultLocalIndex;
                ExtensionInsertionIndex = extensionInsertionIndex;
            }

            internal int ComparisonResultLocalIndex { get; }

            internal int ExtensionInsertionIndex { get; }
        }

        private readonly struct PickupDuplicateSuppressionAnchor
        {
            internal PickupDuplicateSuppressionAnchor(
                int previousPickupLocalIndex,
                int currentPickupLocalIndex,
                int extensionInsertionIndex,
                Label notDuplicateBranchTarget)
            {
                PreviousPickupLocalIndex = previousPickupLocalIndex;
                CurrentPickupLocalIndex = currentPickupLocalIndex;
                ExtensionInsertionIndex = extensionInsertionIndex;
                NotDuplicateBranchTarget = notDuplicateBranchTarget;
            }

            internal int PreviousPickupLocalIndex { get; }

            internal int CurrentPickupLocalIndex { get; }

            internal int ExtensionInsertionIndex { get; }

            internal Label NotDuplicateBranchTarget { get; }
        }
    }
}
