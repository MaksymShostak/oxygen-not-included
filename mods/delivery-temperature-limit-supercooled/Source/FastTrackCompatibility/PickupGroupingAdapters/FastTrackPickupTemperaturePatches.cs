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
    /// Adds canonical temperature classes to FastTrack's verified private pickup
    /// grouping key without replacing FastTrack's grouping implementation.
    /// </summary>
    /// <remarks>
    /// This class intentionally has no Harmony discovery attribute. The
    /// coordinated installer may bind and patch it only when runtime inspection
    /// classifies the active FastTrack pickup replacement as <c>Ready</c>.
    /// Binding stores exact members once; gameplay hooks perform no reflection,
    /// assembly discovery, option lookup, or FastTrack feature detection.
    /// </remarks>
    internal static class FastTrackPickupTemperaturePatches
    {
        private static readonly object BindingSynchronization = new object();
        private static readonly PickupTemperatureGroupingSession
            .ApplicableRequestedTagResolver<KPrefabID>
                ApplicableRequestedTagResolver =
                    ResolveApplicableRequestedTags;

        private static VerifiedPickupGroupingFeatureBinding?
            verifiedPickupGroupingFeatureBinding;

        [ThreadStatic]
        private static FastTrackPickupGroupingUpdateContext?
            reusableThreadPickupGroupingUpdateContext;

        /// <summary>
        /// Binds the immutable reflected member set before any FastTrack method is
        /// patched. A second identical bind is harmless; a different bind fails
        /// closed because installed Harmony methods must never be silently
        /// retargeted during a loaded game.
        /// </summary>
        internal static void BindVerifiedPickupGroupingFeature(
            FastTrackFeatureCompatibility pickupGroupingFeature)
        {
            if (pickupGroupingFeature == null)
            {
                throw new ArgumentNullException(nameof(pickupGroupingFeature));
            }

            if (pickupGroupingFeature.Feature !=
                    FastTrackFeature.PickupGrouping ||
                pickupGroupingFeature.State !=
                    FastTrackFeatureCompatibilityState.Ready)
            {
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack pickup-grouping adapter can bind only a " +
                    "Ready PickupGrouping feature contract.");
            }

            // Both the Klei and FastTrack paths use these worker-capable reads.
            // Prove them here as part of this adapter's own preflight rather than
            // trusting that an unrelated implementation happened to be selected.
            PickupTemperatureGroupingWorkerReadContractVerifier
                .VerifySharedManagedReadContracts();
            VerifiedPickupGroupingFeatureBinding candidateBinding =
                VerifiedPickupGroupingFeatureBinding.Create(
                    pickupGroupingFeature);
            lock (BindingSynchronization)
            {
                VerifiedPickupGroupingFeatureBinding? currentBinding =
                    verifiedPickupGroupingFeatureBinding;
                if (currentBinding == null)
                {
                    Volatile.Write(
                        ref verifiedPickupGroupingFeatureBinding,
                        candidateBinding);
                    return;
                }

                if (!currentBinding.ReferencesSameMembers(candidateBinding))
                {
                    throw new HarmonyPatchContractViolationException(
                        "The FastTrack pickup-grouping adapter is already bound " +
                        "to a different verified member set.");
                }
            }
        }

        internal static MethodInfo
            ResolveFetchManagerBeforeUpdatePickupsTarget() =>
            RequireVerifiedPickupGroupingFeatureBinding()
                .BeforeUpdatePickups;

        internal static MethodInfo
            ResolvePickupTagDictionaryAddItemTarget() =>
            RequireVerifiedPickupGroupingFeatureBinding().AddItem;

        /// <summary>
        /// Executes the complete no-mutation transpiler preflight used by the
        /// coordinated installer after binding and before applying either patch.
        /// </summary>
        internal static void VerifyFastTrackPickupTemperaturePatchContracts()
        {
            _ = ResolveFetchManagerBeforeUpdatePickupsTarget();
            MethodInfo addItemTarget =
                ResolvePickupTagDictionaryAddItemTarget();
            ILGenerator addItemGenerator;
            List<CodeInstruction> addItemInstructions =
                PatchProcessor.GetOriginalInstructions(
                    addItemTarget,
                    out addItemGenerator);
            _ = new List<CodeInstruction>(
                PickupTagDictionaryAddItemTranspiler(
                    addItemInstructions,
                    addItemGenerator));
        }

        internal static void BeforeUpdatePickupsPrefix(
            [HarmonyArgument(1)] Navigator navigator,
            out FastTrackPickupGroupingInvocation __state)
        {
            __state = FastTrackPickupGroupingInvocation.Inactive;
            if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out DeliveryTemperatureGameSession gameSession))
            {
                return;
            }

            ActiveTemperatureConstraintSnapshot activeConstraints =
                gameSession.TemperatureConstraints.CaptureSnapshot();
            if (activeConstraints.EnabledConstraintCount == 0)
            {
                // This is the ordinary bypass: FastTrack keeps its exact original
                // hash, and no grouping context or composite dictionary is begun.
                return;
            }

            WorldParentTopologySnapshot worldTopology =
                gameSession.WorldParentTopology.CaptureSnapshot();
            int? resolvedParentWorldId = ResolveNavigatorParentWorldId(
                navigator,
                worldTopology);
            FetchTemperatureEligibilitySnapshot? eligibilitySnapshot =
                gameSession.CurrentFetchTemperatureEligibility;
            FastTrackPickupGroupingUpdateContext pickupGroupingContext =
                TakeReusableThreadPickupGroupingUpdateContext();
            bool groupingSessionHasBegun = false;
            bool groupingKeyAllocatorHasBegun = false;

            try
            {
                pickupGroupingContext.GroupingSession.Begin(
                    gameSession,
                    resolvedParentWorldId,
                    activeConstraints,
                    eligibilitySnapshot,
                    worldTopology);
                groupingSessionHasBegun = true;
                pickupGroupingContext.GroupingKeyAllocator.Begin(
                    temperatureGroupingIsActive: true);
                groupingKeyAllocatorHasBegun = true;

                ThreadConfinedSessionSlot<
                        FastTrackPickupGroupingUpdateContext>.SessionScopeToken
                    scopeToken = ThreadConfinedSessionSlot<
                        FastTrackPickupGroupingUpdateContext>.Enter(
                            gameSession.Generation,
                            pickupGroupingContext);
                __state = FastTrackPickupGroupingInvocation.Active(
                    pickupGroupingContext,
                    scopeToken);
            }
            catch (Exception originalException)
            {
                Exception? cleanupException = null;
                if (groupingKeyAllocatorHasBegun)
                {
                    cleanupException = TryDiscardAfterFailedPrefix(
                        pickupGroupingContext.GroupingKeyAllocator.Discard,
                        cleanupException);
                }

                if (groupingSessionHasBegun)
                {
                    cleanupException = TryDiscardAfterFailedPrefix(
                        pickupGroupingContext.GroupingSession.Discard,
                        cleanupException);
                }

                if (cleanupException == null)
                {
                    TryRetainReusableThreadPickupGroupingUpdateContext(
                        pickupGroupingContext);
                    throw;
                }

                throw new AggregateException(
                    "FastTrack pickup-grouping setup and its defensive cleanup " +
                    "both failed.",
                    originalException,
                    cleanupException);
            }
        }

        internal static void BeforeUpdatePickupsPostfix(
            FastTrackPickupGroupingInvocation __state)
        {
            if (!__state.IsActive)
            {
                return;
            }

            __state.GroupingSession.Complete();
            __state.GroupingKeyAllocator.Complete();
        }

        internal static Exception? BeforeUpdatePickupsFinalizer(
            Exception? __exception,
            FastTrackPickupGroupingInvocation __state)
        {
            if (!__state.IsActive)
            {
                return __exception;
            }

            Exception? cleanupException = null;
            bool scopeExited = false;
            try
            {
                // Complete and Discard share an idempotent release path. This
                // therefore handles both a successful postfix and any exception
                // that bypassed or interrupted it.
                __state.GroupingSession.Discard();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            try
            {
                __state.GroupingKeyAllocator.Discard();
            }
            catch (Exception exception)
            {
                if (cleanupException == null)
                {
                    cleanupException = exception;
                }
            }

            try
            {
                ThreadConfinedSessionSlot<FastTrackPickupGroupingUpdateContext>
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
                TryRetainReusableThreadPickupGroupingUpdateContext(
                    __state.PickupGroupingContext);
            }

            // The game's/FastTrack's original exception always wins. With no
            // original failure, a lifecycle violation remains fail-closed.
            return __exception ?? cleanupException;
        }

        internal static IEnumerable<CodeInstruction>
            PickupTagDictionaryAddItemTranspiler(
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

            VerifiedPickupGroupingFeatureBinding binding =
                RequireVerifiedPickupGroupingFeatureBinding();
            var sourceInstructions = new List<CodeInstruction>(instructions);
            PickupTagKeyConstructorHashArgumentAnchor constructorAnchor =
                RequirePickupTagKeyConstructorHashArgumentAnchor(
                    sourceInstructions,
                    binding.PickupGroupingKeyConstructor,
                    binding.PickupablePrefabIdentityField);
            MethodInfo allocationHook =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(FastTrackPickupTemperaturePatches),
                    nameof(AllocatePickupGroupingKey),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(int),
                    new[]
                    {
                        typeof(int),
                        typeof(Pickupable),
                        typeof(FastTrackPickupGroupingUpdateContext)
                    });
            MethodInfo currentContextGetter =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(FastTrackPickupTemperaturePatches),
                    nameof(GetCurrentPickupGroupingUpdateContext),
                    DeclaredMemberVisibility.NonPublic,
                    typeof(FastTrackPickupGroupingUpdateContext),
                    Array.Empty<Type>());
            LocalBuilder pickupGroupingContextLocal = generator.DeclareLocal(
                typeof(FastTrackPickupGroupingUpdateContext));

            CodeInstruction originalPickupableLoad =
                sourceInstructions[
                    constructorAnchor.PickupableLoadInstructionIndex];
            var duplicatedPickupableLoad = new CodeInstruction(
                originalPickupableLoad.opcode,
                originalPickupableLoad.operand);
            duplicatedPickupableLoad.labels.AddRange(
                originalPickupableLoad.labels);
            originalPickupableLoad.labels.Clear();
            var allocationInstructions = new List<CodeInstruction>(3)
            {
                duplicatedPickupableLoad,
                CodeInstruction2.LoadLocal(
                    pickupGroupingContextLocal.LocalIndex),
                new CodeInstruction(OpCodes.Call, allocationHook)
            };
            sourceInstructions.InsertRange(
                constructorAnchor.HashArgumentExtensionIndex,
                allocationInstructions);

            var loadCurrentContextInstruction =
                new CodeInstruction(OpCodes.Call, currentContextGetter);
            CodeInstruction originalFirstInstruction = sourceInstructions[0];
            loadCurrentContextInstruction.labels.AddRange(
                originalFirstInstruction.labels);
            originalFirstInstruction.labels.Clear();
            loadCurrentContextInstruction.blocks.AddRange(
                originalFirstInstruction.blocks);
            originalFirstInstruction.blocks.Clear();
            sourceInstructions.InsertRange(
                0,
                new[]
                {
                    loadCurrentContextInstruction,
                    CodeInstruction2.StoreLocal(
                        pickupGroupingContextLocal.LocalIndex)
                });
            return sourceInstructions;
        }

        private static int AllocatePickupGroupingKey(
            int originalTagBitsHash,
            Pickupable pickupable,
            FastTrackPickupGroupingUpdateContext? pickupGroupingContext)
        {
            if (pickupGroupingContext == null ||
                ReferenceEquals(pickupable, null))
            {
                return originalTagBitsHash;
            }

            // These are only the cached managed fields/getters proven during
            // binding. In particular, InternalTemperature avoids a callback-
            // backed live property from FastTrack's worker-capable update.
            KPrefabID kPrefabId = pickupable.KPrefabID;
            PrimaryElement primaryElement = pickupable.PrimaryElement;
            bool hasPrimaryElement = !ReferenceEquals(primaryElement, null);
            float temperatureKelvin = hasPrimaryElement
                // The immediately preceding ReferenceEquals result proves this
                // exact cached component reference is non-null on this branch.
                ? primaryElement!.InternalTemperature
                : 0.0f;

            TemperatureEligibilityClassKey temperatureEligibilityClass;
            if (ReferenceEquals(kPrefabId, null) || kPrefabId.InstanceID < 0)
            {
                temperatureEligibilityClass = hasPrimaryElement
                    ? TemperatureEligibilityClassKey.ExactDecisionBucket(
                        TemperatureDecisionBucket.FromTemperature(
                            temperatureKelvin))
                    : TemperatureEligibilityClassKey.MissingPrimaryElement();
            }
            else
            {
                var tagIdentity = new PickupTagIdentity(
                    originalTagBitsHash,
                    kPrefabId.PrefabTag);
                temperatureEligibilityClass = pickupGroupingContext
                    .GroupingSession
                    .ClassifyUsingApplicableRequestedTagResolver(
                        kPrefabId.InstanceID,
                        tagIdentity,
                        kPrefabId,
                        ApplicableRequestedTagResolver,
                        hasPrimaryElement,
                        temperatureKelvin);
            }

            int allocatedGroupingKey = pickupGroupingContext
                .GroupingKeyAllocator
                .GetOrAllocate(
                    originalTagBitsHash,
                    temperatureEligibilityClass);
            return allocatedGroupingKey;
        }

        private static FastTrackPickupGroupingUpdateContext?
            GetCurrentPickupGroupingUpdateContext() =>
            ThreadConfinedSessionSlot<FastTrackPickupGroupingUpdateContext>
                .TryGetCurrent(out var pickupGroupingContext)
                ? pickupGroupingContext
                : null;

        private static IReadOnlyList<Tag> ResolveApplicableRequestedTags(
            KPrefabID kPrefabId,
            IReadOnlyList<Tag> requestedTagsForResolvedParentWorld)
        {
            List<Tag>? applicableRequestedTags = null;
            for (var requestedTagIndex = 0;
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

        private static Exception? TryDiscardAfterFailedPrefix(
            System.Action discard,
            Exception? existingCleanupException)
        {
            try
            {
                discard();
                return existingCleanupException;
            }
            catch (Exception exception)
            {
                return existingCleanupException ?? exception;
            }
        }

        private static FastTrackPickupGroupingUpdateContext
            TakeReusableThreadPickupGroupingUpdateContext()
        {
            FastTrackPickupGroupingUpdateContext? pickupGroupingContext =
                reusableThreadPickupGroupingUpdateContext;
            reusableThreadPickupGroupingUpdateContext = null;
            return pickupGroupingContext ??
                new FastTrackPickupGroupingUpdateContext();
        }

        private static void TryRetainReusableThreadPickupGroupingUpdateContext(
            FastTrackPickupGroupingUpdateContext pickupGroupingContext)
        {
            if (reusableThreadPickupGroupingUpdateContext != null ||
                ThreadConfinedSessionSlot<FastTrackPickupGroupingUpdateContext>
                    .TryGetCurrent(out _))
            {
                // Retain only the outermost completed context so uncommon nested
                // work cannot turn this single-entry reuse policy into a pool.
                return;
            }

            reusableThreadPickupGroupingUpdateContext = pickupGroupingContext;
        }

        private static PickupTagKeyConstructorHashArgumentAnchor
            RequirePickupTagKeyConstructorHashArgumentAnchor(
                IReadOnlyList<CodeInstruction> instructions,
                ConstructorInfo expectedConstructor,
                FieldInfo expectedPickupablePrefabIdentityField)
        {
            int[] candidateInstructionIndices =
                CreateCandidateInstructionIndices(instructions.Count);
            int anchorStartIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateInstructionIndices,
                    index => MatchesPickupTagKeyConstructorWindow(
                        instructions,
                        index,
                        expectedConstructor,
                        expectedPickupablePrefabIdentityField),
                    "FastTrack PickupTagDict.AddItem PickupTagKey original-hash " +
                    "constructor argument");
            for (var instructionIndex = anchorStartIndex;
                 instructionIndex <= anchorStartIndex + 4;
                 instructionIndex++)
            {
                if (instructions[instructionIndex].blocks.Count != 0)
                {
                    throw new HarmonyPatchContractViolationException(
                        "FastTrack PickupTagDict.AddItem PickupTagKey " +
                        "constructor argument crosses an unreviewed " +
                        "exception-block boundary.");
                }
            }

            return new PickupTagKeyConstructorHashArgumentAnchor(
                pickupableLoadInstructionIndex: anchorStartIndex + 2,
                hashArgumentExtensionIndex: anchorStartIndex + 2);
        }

        private static bool MatchesPickupTagKeyConstructorWindow(
            IReadOnlyList<CodeInstruction> instructions,
            int startIndex,
            ConstructorInfo expectedConstructor,
            FieldInfo expectedPickupablePrefabIdentityField) =>
            startIndex >= 0 &&
            startIndex + 4 < instructions.Count &&
            IsLoadLocalAddress(instructions[startIndex]) &&
            IsLoadLocalValue(instructions[startIndex + 1]) &&
            IsLoadLocalValue(instructions[startIndex + 2]) &&
            instructions[startIndex + 3].opcode == OpCodes.Ldfld &&
            Equals(
                instructions[startIndex + 3].operand,
                expectedPickupablePrefabIdentityField) &&
            instructions[startIndex + 4].opcode == OpCodes.Call &&
            Equals(
                instructions[startIndex + 4].operand,
                expectedConstructor);

        private static bool IsLoadLocalAddress(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloca ||
            instruction.opcode == OpCodes.Ldloca_S;

        private static bool IsLoadLocalValue(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldloc ||
            instruction.opcode == OpCodes.Ldloc_S ||
            instruction.opcode == OpCodes.Ldloc_0 ||
            instruction.opcode == OpCodes.Ldloc_1 ||
            instruction.opcode == OpCodes.Ldloc_2 ||
            instruction.opcode == OpCodes.Ldloc_3;

        private static int[] CreateCandidateInstructionIndices(
            int instructionCount)
        {
            var candidateIndices = new int[instructionCount];
            for (var instructionIndex = 0;
                 instructionIndex < instructionCount;
                 instructionIndex++)
            {
                candidateIndices[instructionIndex] = instructionIndex;
            }

            return candidateIndices;
        }

        private static VerifiedPickupGroupingFeatureBinding
            RequireVerifiedPickupGroupingFeatureBinding()
        {
            VerifiedPickupGroupingFeatureBinding? binding = Volatile.Read(
                ref verifiedPickupGroupingFeatureBinding);
            return binding ??
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack pickup-grouping adapter has not been bound " +
                    "to a Ready compatibility report.");
        }

        internal sealed class FastTrackPickupGroupingUpdateContext
        {
            internal PickupTemperatureGroupingSession GroupingSession
            {
                get;
            } = new PickupTemperatureGroupingSession();

            internal FastTrackPickupGroupingKeyAllocator GroupingKeyAllocator
            {
                get;
            } = new FastTrackPickupGroupingKeyAllocator();
        }

        /// <summary>
        /// Exact prefix-to-finalizer ownership for one FastTrack pickup update.
        /// </summary>
        internal readonly struct FastTrackPickupGroupingInvocation
        {
            internal static readonly FastTrackPickupGroupingInvocation Inactive =
                default(FastTrackPickupGroupingInvocation);

            private FastTrackPickupGroupingInvocation(
                FastTrackPickupGroupingUpdateContext pickupGroupingContext,
                ThreadConfinedSessionSlot<FastTrackPickupGroupingUpdateContext>
                    .SessionScopeToken scopeToken)
            {
                PickupGroupingContext = pickupGroupingContext;
                ScopeToken = scopeToken;
            }

            internal bool IsActive => PickupGroupingContext != null;

            internal FastTrackPickupGroupingUpdateContext PickupGroupingContext
            {
                get;
            }

            internal PickupTemperatureGroupingSession GroupingSession =>
                PickupGroupingContext.GroupingSession;

            internal FastTrackPickupGroupingKeyAllocator GroupingKeyAllocator =>
                PickupGroupingContext.GroupingKeyAllocator;

            internal ThreadConfinedSessionSlot<
                FastTrackPickupGroupingUpdateContext>.SessionScopeToken ScopeToken
            {
                get;
            }

            internal static FastTrackPickupGroupingInvocation Active(
                FastTrackPickupGroupingUpdateContext pickupGroupingContext,
                ThreadConfinedSessionSlot<FastTrackPickupGroupingUpdateContext>
                    .SessionScopeToken scopeToken) =>
                new FastTrackPickupGroupingInvocation(
                    pickupGroupingContext,
                    scopeToken);
        }

        private readonly struct PickupTagKeyConstructorHashArgumentAnchor
        {
            internal PickupTagKeyConstructorHashArgumentAnchor(
                int pickupableLoadInstructionIndex,
                int hashArgumentExtensionIndex)
            {
                PickupableLoadInstructionIndex =
                    pickupableLoadInstructionIndex;
                HashArgumentExtensionIndex = hashArgumentExtensionIndex;
            }

            internal int PickupableLoadInstructionIndex { get; }

            internal int HashArgumentExtensionIndex { get; }
        }

        private sealed class VerifiedPickupGroupingFeatureBinding
        {
            private VerifiedPickupGroupingFeatureBinding(
                MethodInfo beforeUpdatePickups,
                MethodInfo addItem,
                ConstructorInfo pickupGroupingKeyConstructor,
                FieldInfo pickupablePrefabIdentityField)
            {
                BeforeUpdatePickups = beforeUpdatePickups;
                AddItem = addItem;
                PickupGroupingKeyConstructor =
                    pickupGroupingKeyConstructor;
                PickupablePrefabIdentityField =
                    pickupablePrefabIdentityField;
            }

            internal MethodInfo BeforeUpdatePickups { get; }

            internal MethodInfo AddItem { get; }

            internal ConstructorInfo PickupGroupingKeyConstructor { get; }

            internal FieldInfo PickupablePrefabIdentityField { get; }

            internal static VerifiedPickupGroupingFeatureBinding Create(
                FastTrackFeatureCompatibility compatibility)
            {
                MethodInfo beforeUpdatePickups = RequireVerifiedMethod(
                    compatibility,
                    FastTrackVerifiedMember
                        .PickupGroupingBeforeUpdatePickupsPrefix,
                    isStatic: true,
                    typeof(bool),
                    new[]
                    {
                        typeof(FetchManager.FetchablesByPrefabId),
                        typeof(Navigator),
                        typeof(int)
                    });
                MethodInfo addItem = RequireVerifiedMethod(
                    compatibility,
                    FastTrackVerifiedMember.PickupGroupingAddItem,
                    isStatic: false,
                    typeof(void),
                    new[]
                    {
                        typeof(FetchManager.Fetchable).MakeByRefType(),
                        typeof(int)
                    });
                ConstructorInfo pickupGroupingKeyConstructor =
                    RequireVerifiedConstructor(
                        compatibility,
                        FastTrackVerifiedMember
                            .PickupGroupingKeyConstructor,
                        new[] { typeof(int), typeof(KPrefabID) });
                FieldInfo pickupablePrefabIdentityField =
                    RequireVerifiedField(
                        compatibility,
                        FastTrackVerifiedMember
                            .PickupGroupingPickupablePrefabIdentityField,
                        typeof(Pickupable),
                        typeof(KPrefabID));
                FieldInfo currentGamePrefabIdentityField =
                    HarmonyPatchContractVerifier.RequireField(
                        typeof(Pickupable),
                        "KPrefabID",
                        DeclaredMemberVisibility.Public,
                        FieldStorageKind.Instance,
                        typeof(KPrefabID));
                if (!Equals(
                        pickupablePrefabIdentityField,
                        currentGamePrefabIdentityField))
                {
                    throw new HarmonyPatchContractViolationException(
                        "FastTrack's verified PickupTagKey anchor does not read " +
                        "the current game's exact Pickupable.KPrefabID field.");
                }

                Module fastTrackModule = beforeUpdatePickups.Module;
                if (!ReferenceEquals(addItem.Module, fastTrackModule) ||
                    !ReferenceEquals(
                        pickupGroupingKeyConstructor.Module,
                        fastTrackModule))
                {
                    throw new HarmonyPatchContractViolationException(
                        "Verified FastTrack pickup-grouping methods and key " +
                        "constructor do not share one assembly module.");
                }

                return new VerifiedPickupGroupingFeatureBinding(
                    beforeUpdatePickups,
                    addItem,
                    pickupGroupingKeyConstructor,
                    pickupablePrefabIdentityField);
            }

            internal bool ReferencesSameMembers(
                VerifiedPickupGroupingFeatureBinding other) =>
                Equals(BeforeUpdatePickups, other.BeforeUpdatePickups) &&
                Equals(AddItem, other.AddItem) &&
                Equals(
                    PickupGroupingKeyConstructor,
                    other.PickupGroupingKeyConstructor) &&
                Equals(
                    PickupablePrefabIdentityField,
                    other.PickupablePrefabIdentityField);

            private static MethodInfo RequireVerifiedMethod(
                FastTrackFeatureCompatibility compatibility,
                FastTrackVerifiedMember memberRole,
                bool isStatic,
                Type returnType,
                IReadOnlyList<Type> orderedParameterTypes)
            {
                if (!(compatibility.GetVerifiedMember(memberRole) is MethodInfo
                        method) ||
                    method.IsStatic != isStatic ||
                    method.ReturnType != returnType ||
                    !ParametersMatch(
                        method.GetParameters(),
                        orderedParameterTypes))
                {
                    throw new HarmonyPatchContractViolationException(
                        "FastTrack verified member " +
                        memberRole +
                        " no longer matches its bound method contract.");
                }

                return method;
            }

            private static ConstructorInfo RequireVerifiedConstructor(
                FastTrackFeatureCompatibility compatibility,
                FastTrackVerifiedMember memberRole,
                IReadOnlyList<Type> orderedParameterTypes)
            {
                if (!(compatibility.GetVerifiedMember(memberRole) is
                        ConstructorInfo constructor) ||
                    !ParametersMatch(
                        constructor.GetParameters(),
                        orderedParameterTypes))
                {
                    throw new HarmonyPatchContractViolationException(
                        "FastTrack verified member " +
                        memberRole +
                        " no longer matches its bound constructor contract.");
                }

                return constructor;
            }

            private static FieldInfo RequireVerifiedField(
                FastTrackFeatureCompatibility compatibility,
                FastTrackVerifiedMember memberRole,
                Type declaringType,
                Type fieldType)
            {
                if (!(compatibility.GetVerifiedMember(memberRole) is FieldInfo
                        field) ||
                    field.IsStatic ||
                    field.DeclaringType != declaringType ||
                    field.FieldType != fieldType)
                {
                    throw new HarmonyPatchContractViolationException(
                        "FastTrack verified member " +
                        memberRole +
                        " no longer matches its bound instance-field contract.");
                }

                return field;
            }

            private static bool ParametersMatch(
                IReadOnlyList<ParameterInfo> parameters,
                IReadOnlyList<Type> orderedParameterTypes)
            {
                if (parameters.Count != orderedParameterTypes.Count)
                {
                    return false;
                }

                for (var parameterIndex = 0;
                     parameterIndex < parameters.Count;
                     parameterIndex++)
                {
                    if (parameters[parameterIndex].ParameterType !=
                        orderedParameterTypes[parameterIndex])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
