#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Brackets Klei's authoritative <see cref="WorldInventory.Update"/>
    /// enumeration and adds sparse temperature accumulation to that same pass.
    /// </summary>
    /// <remarks>
    /// This adapter deliberately has no Harmony discovery attributes. The runtime
    /// installer activates it only after verifying that Klei still owns the update
    /// and that every semantic IL anchor matches exactly once.
    /// </remarks>
    internal static class KleiWorldInventoryTemperaturePatches
    {
        [ThreadStatic]
        private static WorldInventoryTemperatureCollectionInvocation?
            currentThreadInvocation;

        internal static MethodInfo ResolveWorldInventoryUpdateTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldInventory),
                "Update",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());

        internal static void WorldInventoryUpdatePrefix(
            WorldInventory __instance,
            bool ___firstUpdate,
            out WorldInventoryTemperatureCollectionInvocation __state)
        {
            if (currentThreadInvocation != null)
            {
                throw new InvalidOperationException(
                    "WorldInventory.Update re-entered temperature collection on " +
                    "the same thread. A nested candidate cannot share an open " +
                    "resource-tag builder safely.");
            }

            __state = WorldInventoryTemperatureCollectionInvocation.Inactive;
            if (__instance == null ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var session))
            {
                return;
            }

            ActiveTemperatureConstraintSnapshot activeConstraints =
                session.TemperatureConstraints.CaptureSnapshot();
            if (activeConstraints.EnabledConstraintCount == 0)
            {
                // This is the common bypass after every constraint is disabled.
                // The shared immutable state allocates no builder and retains no
                // inventory object, world identity, or session reference.
                return;
            }

            WorldContainer worldContainer = __instance.WorldContainer;
            if (worldContainer == null || worldContainer.id < 0)
            {
                // An invalid Klei identity cannot be mapped to the session-owned
                // catalog. Leave this invocation explicitly inactive; never guess
                // a world or publish a candidate under a sentinel identity.
                return;
            }

            WorldInventoryCollectionGeneration collectionGeneration =
                session.CurrentWorldInventoryCollectionGeneration;
            if (collectionGeneration.Value <= 0)
            {
                throw new InvalidOperationException(
                    "An enabled temperature constraint exists without a current " +
                    "world-inventory collection generation.");
            }

            WorldInventoryTemperaturePublicationKind publicationKind;
            if (___firstUpdate)
            {
                publicationKind = WorldInventoryTemperaturePublicationKind
                    .CompleteWorldAmounts;
            }
            else
            {
                WorldResourceTagCoverageRequirementState coverageState =
                    session.WorldResourceTemperatureAmounts
                        .GetWorldResourceTagCoverageRequirementState(
                            worldContainer.id,
                            collectionGeneration);
                switch (coverageState)
                {
                    case WorldResourceTagCoverageRequirementState
                        .UnknownWorldOrCollectionGeneration:
                        // World lifecycle registration has not supplied enough
                        // identity evidence. Publishing under a guessed membership
                        // would be worse than temporarily preserving Klei status.
                        return;

                    case WorldResourceTagCoverageRequirementState
                        .CoverageRequired:
                        publicationKind = WorldInventoryTemperaturePublicationKind
                            .ResourceTagCoverageAndTemperatureSeries;
                        break;

                    case WorldResourceTagCoverageRequirementState
                        .CoverageCurrent:
                        publicationKind = WorldInventoryTemperaturePublicationKind
                            .ResourceTemperatureSeries;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(coverageState),
                            coverageState,
                            "Unknown world resource-tag coverage requirement " +
                            "state.");
                }
            }

            var builder =
                new CompleteWorldResourceTemperatureAmountsBuilder();
            builder.BeginWorld(collectionGeneration);
            var activeInvocation =
                WorldInventoryTemperatureCollectionInvocation.Active(
                    session,
                    worldContainer.id,
                    collectionGeneration,
                    publicationKind,
                    builder);

            // Publish thread-confined hook state only after every fallible setup
            // operation has succeeded. Finalizer owns clearing this exact object.
            currentThreadInvocation = activeInvocation;
            __state = activeInvocation;
        }

        internal static IEnumerable<CodeInstruction>
            WorldInventoryUpdateTranspiler(
                IEnumerable<CodeInstruction> instructions,
                ILGenerator generator)
        {
            if (instructions == null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }

            var sourceInstructions = new List<CodeInstruction>(instructions);
            int[] candidateIndices = CreateCandidateInstructionIndices(
                sourceInstructions.Count);

            MethodInfo inventoryEntryGetter =
                RequireInventoryEnumeratorCurrentGetter();
            MethodInfo resourceTagGetter =
                RequireKeyValuePairResourceTagGetter();
            MethodInfo pickupableSetGetter =
                RequireKeyValuePairPickupableSetGetter();
            MethodInfo pickupableSetEnumerator =
                RequirePickupableSetEnumerator();
            FieldInfo pickupablePrefabIdentityField =
                RequirePickupablePrefabIdentityField();
            FieldInfo storedPrivateTagField = RequireStoredPrivateTagField();
            MethodInfo hasTagMethod = RequireHasTagMethod();
            MethodInfo totalAmountGetter = RequirePickupableTotalAmountGetter();
            FieldInfo accessibleAmountsField = RequireAccessibleAmountsField();
            MethodInfo accessibleAmountSetter =
                RequireAccessibleAmountSetter();

            int inventoryEntryCaptureIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesInventoryEntryCapture(
                        sourceInstructions,
                        index,
                        inventoryEntryGetter),
                    "WorldInventory.Update inventory-entry capture");
            int inventoryEntryLocalIndex =
                sourceInstructions[inventoryEntryCaptureIndex + 1]
                    .LocalIndex();

            int resourceTagStartIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesResourceTagStart(
                        sourceInstructions,
                        index,
                        resourceTagGetter,
                        pickupableSetGetter,
                        pickupableSetEnumerator),
                    "WorldInventory.Update resource-tag enumeration start");
            int resourceTagLocalIndex =
                sourceInstructions[resourceTagStartIndex + 1].LocalIndex();
            int accumulatedAmountLocalIndex =
                sourceInstructions[resourceTagStartIndex + 5].LocalIndex();

            int filteredPickupContributionIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesFilteredPickupContribution(
                        sourceInstructions,
                        index,
                        pickupablePrefabIdentityField,
                        storedPrivateTagField,
                        hasTagMethod,
                        totalAmountGetter,
                        accumulatedAmountLocalIndex),
                    "WorldInventory.Update filtered " +
                    "Pickupable.TotalAmount contribution");
            int pickupableTotalAmountGetterIndex =
                filteredPickupContributionIndex + 7;

            int resourceTagCompletionIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index => MatchesResourceTagCompletion(
                        sourceInstructions,
                        index,
                        accessibleAmountsField,
                        accessibleAmountSetter,
                        resourceTagLocalIndex,
                        accumulatedAmountLocalIndex),
                    "WorldInventory.Update resource-tag enumeration completion");

            MethodInfo beginResourceTagHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiWorldInventoryTemperaturePatches),
                    nameof(BeginResourceTagEnumeration),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(void),
                    new[] { typeof(Tag) });
            MethodInfo recordPickupHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiWorldInventoryTemperaturePatches),
                    nameof(RecordFilteredPickupTemperatureAmount),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(float),
                    new[] { typeof(Pickupable), typeof(float) });
            MethodInfo completeResourceTagHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiWorldInventoryTemperaturePatches),
                    nameof(CompleteResourceTagEnumeration),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(void),
                    Array.Empty<Type>());
            MethodInfo shouldObserveCoverageHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiWorldInventoryTemperaturePatches),
                    nameof(ShouldObserveResourceTagCoverage),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    Array.Empty<Type>());
            MethodInfo observeCoverageHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiWorldInventoryTemperaturePatches),
                    nameof(ObserveResourceTagForCoverage),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(void),
                    new[]
                    {
                        typeof(KeyValuePair<Tag, HashSet<Pickupable>>)
                            .MakeByRefType()
                    });
            MethodInfo isCollectionActiveHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(KleiWorldInventoryTemperaturePatches),
                    nameof(IsTemperatureCollectionActive),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(bool),
                    Array.Empty<Type>());

            LocalBuilder isCollectionActiveLocal =
                generator.DeclareLocal(typeof(bool));
            LocalBuilder shouldObserveCoverageLocal =
                generator.DeclareLocal(typeof(bool));
            Label skipCoverageObservationLabel = generator.DefineLabel();
            Label skipResourceTagStartHookLabel = generator.DefineLabel();
            Label originalTotalAmountGetterLabel = generator.DefineLabel();
            Label afterTotalAmountGetterLabel = generator.DefineLabel();
            Label skipResourceTagCompletionHookLabel = generator.DefineLabel();
            sourceInstructions[inventoryEntryCaptureIndex + 2].labels.Add(
                skipCoverageObservationLabel);
            sourceInstructions[resourceTagStartIndex + 2].labels.Add(
                skipResourceTagStartHookLabel);
            sourceInstructions[pickupableTotalAmountGetterIndex].labels.Add(
                originalTotalAmountGetterLabel);
            sourceInstructions[pickupableTotalAmountGetterIndex + 1].labels.Add(
                afterTotalAmountGetterLabel);
            sourceInstructions[resourceTagCompletionIndex + 4].labels.Add(
                skipResourceTagCompletionHookLabel);

            var instrumentedInstructions = new List<CodeInstruction>(
                sourceInstructions.Count + 23);
            instrumentedInstructions.Add(new CodeInstruction(
                OpCodes.Call,
                isCollectionActiveHook));
            instrumentedInstructions.Add(
                HarmonyCodeInstructionFactory.StoreLocal(
                    isCollectionActiveLocal.LocalIndex));
            instrumentedInstructions.Add(new CodeInstruction(
                OpCodes.Call,
                shouldObserveCoverageHook));
            instrumentedInstructions.Add(
                HarmonyCodeInstructionFactory.StoreLocal(
                    shouldObserveCoverageLocal.LocalIndex));
            for (int instructionIndex = 0;
                 instructionIndex < sourceInstructions.Count;
                 instructionIndex++)
            {
                instrumentedInstructions.Add(
                    sourceInstructions[instructionIndex]);

                if (instructionIndex == inventoryEntryCaptureIndex + 1)
                {
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            shouldObserveCoverageLocal.LocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Brfalse,
                        skipCoverageObservationLabel));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            inventoryEntryLocalIndex,
                            loadAddress: true));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        observeCoverageHook));
                }

                if (instructionIndex == resourceTagStartIndex + 1)
                {
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            isCollectionActiveLocal.LocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Brfalse,
                        skipResourceTagStartHookLabel));
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(resourceTagLocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        beginResourceTagHook));
                }

                if (instructionIndex ==
                    pickupableTotalAmountGetterIndex - 1)
                {
                    // The inactive branch lands on Klei's original getter with its
                    // original stack. The active branch duplicates Pickupable,
                    // performs one equivalent getter, records the amount, and skips
                    // the original getter. Both paths therefore call TotalAmount
                    // exactly once and reconverge with [runningTotal, amount].
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            isCollectionActiveLocal.LocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Brfalse,
                        originalTotalAmountGetterLabel));
                    instrumentedInstructions.Add(
                        new CodeInstruction(OpCodes.Dup));
                    instrumentedInstructions.Add(new CodeInstruction(
                        sourceInstructions[pickupableTotalAmountGetterIndex]
                            .opcode,
                        totalAmountGetter));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        recordPickupHook));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Br,
                        afterTotalAmountGetterLabel));
                }

                if (instructionIndex == resourceTagCompletionIndex + 3)
                {
                    instrumentedInstructions.Add(
                        HarmonyCodeInstructionFactory.LoadLocal(
                            isCollectionActiveLocal.LocalIndex));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Brfalse,
                        skipResourceTagCompletionHookLabel));
                    instrumentedInstructions.Add(new CodeInstruction(
                        OpCodes.Call,
                        completeResourceTagHook));
                }
            }

            return instrumentedInstructions;
        }

        internal static void WorldInventoryUpdatePostfix(
            WorldInventoryTemperatureCollectionInvocation __state)
        {
            if (!__state.IsActive)
            {
                return;
            }

            RequireCurrentThreadInvocation(__state);
            CompleteWorldResourceTemperatureAmounts candidate =
                __state.Builder.Build();
            __state.MarkCandidateBuilt();

            switch (__state.PublicationKind)
            {
                case WorldInventoryTemperaturePublicationKind
                    .CompleteWorldAmounts:
                    // Klei's first update visits every resource-tag pickup set. It
                    // is therefore the only invocation that can replace the whole
                    // world map without manufacturing absence for skipped tags.
                    __state.Session.WorldResourceTemperatureAmounts
                        .PublishCompleteWorldResourceAmounts(
                            __state.WorldId,
                            candidate);
                    break;

                case WorldInventoryTemperaturePublicationKind
                    .ResourceTagCoverageAndTemperatureSeries:
                    WorldResourceTagCoverage resourceTagCoverage =
                        WorldResourceTagCoverage.Create(
                            __state.CollectionGeneration,
                            __state.ObservedResourceTags);
                    if (__state.Session.WorldResourceTemperatureAmounts
                        .PublishWorldResourceTagCoverage(
                            __state.WorldId,
                            resourceTagCoverage))
                    {
                        PublishResourceTemperatureSeries(
                            __state,
                            candidate);
                    }

                    break;

                case WorldInventoryTemperaturePublicationKind
                    .ResourceTemperatureSeries:
                    PublishResourceTemperatureSeries(__state, candidate);
                    break;

                case WorldInventoryTemperaturePublicationKind.Inactive:
                    throw new InvalidOperationException(
                        "An inactive WorldInventory.Update invocation reached " +
                        "temperature publication.");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(__state),
                        __state.PublicationKind,
                        "Unknown world-inventory temperature publication kind.");
            }
        }

        internal static Exception? WorldInventoryUpdateFinalizer(
            Exception? __exception,
            WorldInventoryTemperatureCollectionInvocation __state)
        {
            if (!__state.IsActive)
            {
                return __exception;
            }

            try
            {
                RequireCurrentThreadInvocation(__state);
                if (!__state.HasBuiltCandidate)
                {
                    // This covers failures in Klei's body, any injected hook, or
                    // Build itself. Discard releases every partially accumulated
                    // resource tag and makes publication impossible.
                    __state.Builder.Discard();
                }
            }
            finally
            {
                if (ReferenceEquals(currentThreadInvocation, __state))
                {
                    currentThreadInvocation = null;
                }
            }

            return __exception;
        }

        private static void BeginResourceTagEnumeration(Tag resourceTag)
        {
            WorldInventoryTemperatureCollectionInvocation? invocation =
                currentThreadInvocation;
            if (invocation == null)
            {
                return;
            }

            invocation.Builder.BeginResourceTag(resourceTag);
        }

        private static bool ShouldObserveResourceTagCoverage()
        {
            WorldInventoryTemperatureCollectionInvocation? invocation =
                currentThreadInvocation;
            return invocation != null &&
                invocation.PublicationKind ==
                    WorldInventoryTemperaturePublicationKind
                        .ResourceTagCoverageAndTemperatureSeries;
        }

        private static bool IsTemperatureCollectionActive() =>
            currentThreadInvocation != null;

        private static void ObserveResourceTagForCoverage(
            ref KeyValuePair<Tag, HashSet<Pickupable>> inventoryEntry)
        {
            WorldInventoryTemperatureCollectionInvocation? invocation =
                currentThreadInvocation;
            if (invocation == null ||
                invocation.PublicationKind !=
                    WorldInventoryTemperaturePublicationKind
                        .ResourceTagCoverageAndTemperatureSeries)
            {
                throw new InvalidOperationException(
                    "Resource-tag coverage observation ran without the exact " +
                    "incremental coverage-collection invocation.");
            }

            invocation.ObserveResourceTag(inventoryEntry.Key);
        }

        private static float RecordFilteredPickupTemperatureAmount(
            Pickupable pickupable,
            float originalTotalAmount)
        {
            WorldInventoryTemperatureCollectionInvocation? invocation =
                currentThreadInvocation;
            if (invocation != null &&
                pickupable != null &&
                pickupable.PrimaryElement != null)
            {
                invocation.Builder.AddTemperatureAmount(
                    pickupable.PrimaryElement.Temperature,
                    originalTotalAmount);
            }

            return originalTotalAmount;
        }

        private static void CompleteResourceTagEnumeration()
        {
            WorldInventoryTemperatureCollectionInvocation? invocation =
                currentThreadInvocation;
            if (invocation == null)
            {
                return;
            }

            invocation.Builder.CompleteResourceTag();
        }

        private static void PublishResourceTemperatureSeries(
            WorldInventoryTemperatureCollectionInvocation invocation,
            CompleteWorldResourceTemperatureAmounts candidate)
        {
            foreach (Tag resourceTag in candidate.ResourceTags)
            {
                if (!candidate.TryGetSeries(
                        resourceTag,
                        out var temperatureAmounts))
                {
                    throw new InvalidOperationException(
                        "An incremental WorldInventory.Update candidate listed a " +
                        "resource tag without its temperature amount series.");
                }

                invocation.Session.WorldResourceTemperatureAmounts
                    .PublishWorldResourceTemperatureSeries(
                        invocation.WorldId,
                        new WorldResourceTemperatureSeriesPublication(
                            invocation.CollectionGeneration,
                            resourceTag,
                            temperatureAmounts));
            }
        }

        private static bool MatchesResourceTagStart(
            IReadOnlyList<CodeInstruction> instructions,
            int resourceTagGetterIndex,
            MethodInfo resourceTagGetter,
            MethodInfo pickupableSetGetter,
            MethodInfo pickupableSetEnumerator)
        {
            if (resourceTagGetterIndex < 1 ||
                resourceTagGetterIndex + 6 >= instructions.Count ||
                !IsCall(
                    instructions[resourceTagGetterIndex],
                    resourceTagGetter) ||
                !IsStoreLocal(instructions[resourceTagGetterIndex + 1]) ||
                !IsLoadLocalAddress(
                    instructions[resourceTagGetterIndex - 1]) ||
                !IsLoadLocalAddress(
                    instructions[resourceTagGetterIndex + 2]) ||
                instructions[resourceTagGetterIndex - 1].LocalIndex() !=
                    instructions[resourceTagGetterIndex + 2].LocalIndex() ||
                !IsCall(
                    instructions[resourceTagGetterIndex + 3],
                    pickupableSetGetter) ||
                !IsFloatingPointZero(
                    instructions[resourceTagGetterIndex + 4]) ||
                !IsStoreLocal(instructions[resourceTagGetterIndex + 5]) ||
                !IsCall(
                    instructions[resourceTagGetterIndex + 6],
                    pickupableSetEnumerator))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesInventoryEntryCapture(
            IReadOnlyList<CodeInstruction> instructions,
            int inventoryEntryGetterIndex,
            MethodInfo inventoryEntryGetter)
        {
            if (inventoryEntryGetterIndex < 1 ||
                inventoryEntryGetterIndex + 1 >= instructions.Count ||
                !IsLoadLocalAddress(
                    instructions[inventoryEntryGetterIndex - 1]) ||
                !IsCall(
                    instructions[inventoryEntryGetterIndex],
                    inventoryEntryGetter) ||
                !IsStoreLocal(
                    instructions[inventoryEntryGetterIndex + 1]))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesFilteredPickupContribution(
            IReadOnlyList<CodeInstruction> instructions,
            int pickupableFilterStartIndex,
            FieldInfo pickupablePrefabIdentityField,
            FieldInfo storedPrivateTagField,
            MethodInfo hasTagMethod,
            MethodInfo totalAmountGetter,
            int accumulatedAmountLocalIndex)
        {
            if (pickupableFilterStartIndex < 0 ||
                pickupableFilterStartIndex + 9 >= instructions.Count ||
                !IsLoadLocal(instructions[pickupableFilterStartIndex]) ||
                !IsFieldLoad(
                    instructions[pickupableFilterStartIndex + 1],
                    pickupablePrefabIdentityField,
                    isStatic: false) ||
                !IsFieldLoad(
                    instructions[pickupableFilterStartIndex + 2],
                    storedPrivateTagField,
                    isStatic: true) ||
                !IsCall(
                    instructions[pickupableFilterStartIndex + 3],
                    hasTagMethod) ||
                !IsTrueBranch(
                    instructions[pickupableFilterStartIndex + 4]) ||
                !IsLoadLocal(
                    instructions[pickupableFilterStartIndex + 5]) ||
                instructions[pickupableFilterStartIndex + 5].LocalIndex() !=
                    accumulatedAmountLocalIndex ||
                !IsLoadLocal(
                    instructions[pickupableFilterStartIndex + 6]) ||
                instructions[pickupableFilterStartIndex].LocalIndex() !=
                    instructions[pickupableFilterStartIndex + 6]
                        .LocalIndex() ||
                !IsCall(
                    instructions[pickupableFilterStartIndex + 7],
                    totalAmountGetter) ||
                instructions[pickupableFilterStartIndex + 8].opcode !=
                    OpCodes.Add ||
                !IsStoreLocal(
                    instructions[pickupableFilterStartIndex + 9]) ||
                instructions[pickupableFilterStartIndex + 9].LocalIndex() !=
                    accumulatedAmountLocalIndex)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesResourceTagCompletion(
            IReadOnlyList<CodeInstruction> instructions,
            int accessibleAmountsFieldIndex,
            FieldInfo accessibleAmountsField,
            MethodInfo accessibleAmountSetter,
            int resourceTagLocalIndex,
            int accumulatedAmountLocalIndex)
        {
            if (accessibleAmountsFieldIndex < 1 ||
                accessibleAmountsFieldIndex + 3 >= instructions.Count ||
                instructions[accessibleAmountsFieldIndex - 1].opcode !=
                    OpCodes.Ldarg_0 ||
                !IsFieldLoad(
                    instructions[accessibleAmountsFieldIndex],
                    accessibleAmountsField,
                    isStatic: false) ||
                !IsLoadLocal(
                    instructions[accessibleAmountsFieldIndex + 1]) ||
                instructions[accessibleAmountsFieldIndex + 1].LocalIndex() !=
                    resourceTagLocalIndex ||
                !IsLoadLocal(
                    instructions[accessibleAmountsFieldIndex + 2]) ||
                instructions[accessibleAmountsFieldIndex + 2].LocalIndex() !=
                    accumulatedAmountLocalIndex ||
                !IsCall(
                    instructions[accessibleAmountsFieldIndex + 3],
                    accessibleAmountSetter))
            {
                return false;
            }

            return true;
        }

        private static void RequireCurrentThreadInvocation(
            WorldInventoryTemperatureCollectionInvocation invocation)
        {
            if (!ReferenceEquals(currentThreadInvocation, invocation))
            {
                throw new InvalidOperationException(
                    "The WorldInventory.Update temperature-collection state no " +
                    "longer matches the invocation being completed.");
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
            FieldInfo expectedField,
            bool isStatic) =>
            instruction.opcode ==
                (isStatic ? OpCodes.Ldsfld : OpCodes.Ldfld) &&
            Equals(instruction.operand, expectedField);

        private static bool IsLoadLocal(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloc_0 ||
            instruction.opcode == OpCodes.Ldloc_1 ||
            instruction.opcode == OpCodes.Ldloc_2 ||
            instruction.opcode == OpCodes.Ldloc_3 ||
            instruction.opcode == OpCodes.Ldloc_S ||
            instruction.opcode == OpCodes.Ldloc;

        private static bool IsLoadLocalAddress(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloca_S ||
            instruction.opcode == OpCodes.Ldloca;

        private static bool IsStoreLocal(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Stloc_0 ||
            instruction.opcode == OpCodes.Stloc_1 ||
            instruction.opcode == OpCodes.Stloc_2 ||
            instruction.opcode == OpCodes.Stloc_3 ||
            instruction.opcode == OpCodes.Stloc_S ||
            instruction.opcode == OpCodes.Stloc;

        private static bool IsFloatingPointZero(
            CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldc_R4 &&
            instruction.operand is float value &&
            value == 0.0f;

        private static bool IsTrueBranch(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Brtrue ||
            instruction.opcode == OpCodes.Brtrue_S;

        private static MethodInfo RequireKeyValuePairResourceTagGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(KeyValuePair<Tag, HashSet<Pickupable>>),
                "get_Key",
                DeclaredMemberVisibility.Public,
                typeof(Tag),
                Array.Empty<Type>());

        private static MethodInfo RequireInventoryEnumeratorCurrentGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Dictionary<
                    Tag,
                    HashSet<Pickupable>>.Enumerator),
                "get_Current",
                DeclaredMemberVisibility.Public,
                typeof(KeyValuePair<Tag, HashSet<Pickupable>>),
                Array.Empty<Type>());

        private static MethodInfo RequireKeyValuePairPickupableSetGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(KeyValuePair<Tag, HashSet<Pickupable>>),
                "get_Value",
                DeclaredMemberVisibility.Public,
                typeof(HashSet<Pickupable>),
                Array.Empty<Type>());

        private static MethodInfo RequirePickupableSetEnumerator() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(IEnumerable<Pickupable>),
                "GetEnumerator",
                DeclaredMemberVisibility.Public,
                typeof(IEnumerator<Pickupable>),
                Array.Empty<Type>());

        private static FieldInfo RequirePickupablePrefabIdentityField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(Pickupable),
                "KPrefabID",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(KPrefabID));

        private static FieldInfo RequireStoredPrivateTagField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(GameTags),
                "StoredPrivate",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Static,
                typeof(Tag));

        private static MethodInfo RequireHasTagMethod() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(KPrefabID),
                "HasTag",
                DeclaredMemberVisibility.Public,
                typeof(bool),
                new[] { typeof(Tag) });

        private static MethodInfo RequirePickupableTotalAmountGetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Pickupable),
                "get_TotalAmount",
                DeclaredMemberVisibility.Public,
                typeof(float),
                Array.Empty<Type>());

        private static FieldInfo RequireAccessibleAmountsField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(WorldInventory),
                "accessibleAmounts",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(Dictionary<Tag, float>));

        private static MethodInfo RequireAccessibleAmountSetter() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Dictionary<Tag, float>),
                "set_Item",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(Tag), typeof(float) });

        internal enum WorldInventoryTemperaturePublicationKind
        {
            Inactive,
            CompleteWorldAmounts,
            ResourceTagCoverageAndTemperatureSeries,
            ResourceTemperatureSeries
        }

        /// <summary>
        /// Exact one-call state passed by Harmony from prefix through postfix and
        /// finalizer. The inactive singleton is immutable and retains no game data.
        /// </summary>
        internal sealed class WorldInventoryTemperatureCollectionInvocation
        {
            internal static readonly
                WorldInventoryTemperatureCollectionInvocation Inactive =
                    new WorldInventoryTemperatureCollectionInvocation();

            private WorldInventoryTemperatureCollectionInvocation()
            {
                PublicationKind =
                    WorldInventoryTemperaturePublicationKind.Inactive;
                Session = null!;
                Builder = null!;
                ObservedResourceTags = Array.Empty<Tag>();
            }

            private WorldInventoryTemperatureCollectionInvocation(
                DeliveryTemperatureGameSession session,
                int worldId,
                WorldInventoryCollectionGeneration collectionGeneration,
                WorldInventoryTemperaturePublicationKind publicationKind,
                CompleteWorldResourceTemperatureAmountsBuilder builder)
            {
                if (publicationKind ==
                    WorldInventoryTemperaturePublicationKind.Inactive)
                {
                    throw new ArgumentException(
                        "An active invocation requires an active publication kind.",
                        nameof(publicationKind));
                }

                PublicationKind = publicationKind;
                Session = session;
                WorldId = worldId;
                CollectionGeneration = collectionGeneration;
                Builder = builder;
                ObservedResourceTags = publicationKind ==
                    WorldInventoryTemperaturePublicationKind
                        .ResourceTagCoverageAndTemperatureSeries
                    ? new List<Tag>()
                    : (IReadOnlyCollection<Tag>)Array.Empty<Tag>();
            }

            internal bool IsActive =>
                PublicationKind !=
                    WorldInventoryTemperaturePublicationKind.Inactive;

            internal WorldInventoryTemperaturePublicationKind PublicationKind
            {
                get;
            }

            internal DeliveryTemperatureGameSession Session { get; }

            internal int WorldId { get; }

            internal WorldInventoryCollectionGeneration CollectionGeneration
            {
                get;
            }

            internal CompleteWorldResourceTemperatureAmountsBuilder Builder
            {
                get;
            }

            internal bool HasBuiltCandidate { get; private set; }

            internal IReadOnlyCollection<Tag> ObservedResourceTags { get; }

            internal static WorldInventoryTemperatureCollectionInvocation
                Active(
                    DeliveryTemperatureGameSession session,
                    int worldId,
                    WorldInventoryCollectionGeneration collectionGeneration,
                    WorldInventoryTemperaturePublicationKind publicationKind,
                    CompleteWorldResourceTemperatureAmountsBuilder builder) =>
                new WorldInventoryTemperatureCollectionInvocation(
                    session,
                    worldId,
                    collectionGeneration,
                    publicationKind,
                    builder);

            internal void ObserveResourceTag(Tag resourceTag)
            {
                if (PublicationKind !=
                    WorldInventoryTemperaturePublicationKind
                        .ResourceTagCoverageAndTemperatureSeries ||
                    !(ObservedResourceTags is List<Tag> mutableResourceTags))
                {
                    throw new InvalidOperationException(
                        "Only an incremental coverage invocation can observe " +
                        "complete resource-tag coverage.");
                }

                // Dictionary keys are unique by construction. Retain encounter
                // order and avoid an extra membership structure in this one-shot
                // candidate; WorldResourceTagCoverage defensively deduplicates at
                // its immutable publication boundary.
                mutableResourceTags.Add(resourceTag);
            }

            internal void MarkCandidateBuilt()
            {
                if (!IsActive || HasBuiltCandidate)
                {
                    throw new InvalidOperationException(
                        "A WorldInventory.Update temperature candidate may be " +
                        "marked built exactly once on an active invocation.");
                }

                HasBuiltCandidate = true;
            }
        }
    }
}
