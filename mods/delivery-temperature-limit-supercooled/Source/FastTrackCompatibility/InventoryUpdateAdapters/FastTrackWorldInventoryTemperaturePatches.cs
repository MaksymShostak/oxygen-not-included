#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Extends FastTrack's verified incremental world-inventory implementation
    /// with sparse temperature amounts while preserving FastTrack's scheduling.
    /// </summary>
    /// <remarks>
    /// This adapter deliberately has no Harmony discovery attributes. The runtime
    /// installer binds one immutable <see cref="FastTrackFeatureCompatibility"/>
    /// result and activates these methods only after every reflected member and IL
    /// anchor has been verified. Gameplay hooks therefore never rediscover a type,
    /// inspect an option, enumerate assemblies, or read the FastTrack binary.
    /// </remarks>
    internal static class FastTrackWorldInventoryTemperaturePatches
    {
        private const string UnknownCoverageDiagnosticKey =
            "FastTrackWorldInventoryCoverageUnknown";

        private static readonly object BindingSynchronization = new object();
        private static readonly ConditionalWeakTable<
                object,
                FastTrackWorldInventoryPublicationSession>
            PublicationSessionsByBackgroundInventory =
                new ConditionalWeakTable<
                    object,
                    FastTrackWorldInventoryPublicationSession>();

        private static VerifiedWorldInventoryFeatureBinding?
            verifiedWorldInventoryFeatureBinding;

        /// <summary>
        /// Stores the exact verified contract once, before any Harmony patch is
        /// applied. Rebinding to different members is rejected rather than
        /// allowing a later load callback to silently retarget installed patches.
        /// </summary>
        internal static void BindVerifiedWorldInventoryFeature(
            FastTrackFeatureCompatibility worldInventoryFeature)
        {
            if (worldInventoryFeature == null)
            {
                throw new ArgumentNullException(nameof(worldInventoryFeature));
            }

            if (worldInventoryFeature.Feature != FastTrackFeature.WorldInventory ||
                worldInventoryFeature.State !=
                    FastTrackFeatureCompatibilityState.Ready)
            {
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack world-inventory adapter can bind only a Ready " +
                    "WorldInventory feature contract.");
            }

            VerifiedWorldInventoryFeatureBinding candidateBinding =
                VerifiedWorldInventoryFeatureBinding.Create(
                    worldInventoryFeature);
            lock (BindingSynchronization)
            {
                VerifiedWorldInventoryFeatureBinding? currentBinding =
                    verifiedWorldInventoryFeatureBinding;
                if (currentBinding == null)
                {
                    Volatile.Write(
                        ref verifiedWorldInventoryFeatureBinding,
                        candidateBinding);
                    return;
                }

                if (!currentBinding.ReferencesSameMembers(candidateBinding))
                {
                    throw new HarmonyPatchContractViolationException(
                        "The FastTrack world-inventory adapter is already bound " +
                        "to a different verified member set.");
                }
            }
        }

        internal static MethodInfo
            ResolveBackgroundWorldInventoryRunUpdateTarget() =>
            RequireVerifiedWorldInventoryFeatureBinding().RunUpdate;

        internal static MethodInfo
            ResolveBackgroundWorldInventorySumTotalTarget() =>
            RequireVerifiedWorldInventoryFeatureBinding().SumTotal;

        internal static void BackgroundWorldInventoryRunUpdatePrefix(
            object __instance,
            bool ___firstUpdate,
            WorldContainer ___worldContainer,
            WorldInventory ___worldInventory,
            out FastTrackWorldInventoryTemperatureCollectionInvocation __state)
        {
            __state =
                FastTrackWorldInventoryTemperatureCollectionInvocation.Inactive;
            if (__instance == null ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out DeliveryTemperatureGameSession gameSession))
            {
                return;
            }

            ActiveTemperatureConstraintSnapshot activeConstraints =
                gameSession.TemperatureConstraints.CaptureSnapshot();
            if (activeConstraints.EnabledConstraintCount == 0)
            {
                // No session, coverage enumeration, builder, or retained game
                // object is created in the ordinary fully-disabled path.
                return;
            }

            if (___worldContainer == null || ___worldInventory == null)
            {
                // The verified FastTrack fields are authoritative. Never fall
                // back to a global world lookup or guess a sentinel world ID.
                return;
            }

            int worldId = ___worldContainer.id;
            if (worldId < 0)
            {
                return;
            }

            WorldInventoryCollectionGeneration collectionGeneration =
                gameSession.CurrentWorldInventoryCollectionGeneration;
            if (collectionGeneration.Value <= 0)
            {
                throw new InvalidOperationException(
                    "An enabled temperature constraint exists without a current " +
                    "world-inventory collection generation.");
            }

            IDictionary<Tag, HashSet<Pickupable>>? worldInventoryEntries =
                RequireVerifiedWorldInventoryFeatureBinding()
                    .ReadWorldInventoryEntries(___worldInventory);
            if (worldInventoryEntries == null)
            {
                // FastTrack itself skips this update when the backing collection
                // is absent, so the adapter must not manufacture empty coverage.
                return;
            }

            FastTrackWorldInventoryPublicationSession publicationSession =
                PublicationSessionsByBackgroundInventory.GetValue(
                    __instance,
                    CreatePublicationSession);
            bool publicationSessionHasBegun = false;
            try
            {
                if (___firstUpdate)
                {
                    publicationSession.BeginCompleteWorldUpdate(
                        gameSession.Generation,
                        collectionGeneration);
                    publicationSessionHasBegun = true;
                }
                else
                {
                    WorldResourceTagCoverageRequirementState coverageState =
                        gameSession.WorldResourceTemperatureAmounts
                            .GetWorldResourceTagCoverageRequirementState(
                                worldId,
                                collectionGeneration);
                    switch (coverageState)
                    {
                        case WorldResourceTagCoverageRequirementState
                            .UnknownWorldOrCollectionGeneration:
                            EmitUnknownCoverageDiagnosticOnce(
                                gameSession,
                                worldId,
                                collectionGeneration);
                            return;

                        case WorldResourceTagCoverageRequirementState
                            .CoverageRequired:
                            // WorldResourceTagCoverage.Create performs the one
                            // defensive copy. Pickupable sets are never visited.
                            publicationSession
                                .BeginIncrementalResourceTagUpdateRequiringCoverage(
                                    gameSession.Generation,
                                    collectionGeneration,
                                    worldInventoryEntries.Keys);
                            publicationSessionHasBegun = true;
                            break;

                        case WorldResourceTagCoverageRequirementState
                            .CoverageCurrent:
                            if (worldInventoryEntries.Count == 0)
                            {
                                // Coverage already proves an empty inventory and
                                // FastTrack has no selected tag to refresh.
                                return;
                            }

                            publicationSession
                                .BeginIncrementalResourceTagUpdateWithCurrentCoverage(
                                    gameSession.Generation,
                                    collectionGeneration);
                            publicationSessionHasBegun = true;
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(coverageState),
                                coverageState,
                                "Unknown world resource-tag coverage requirement " +
                                "state.");
                    }
                }

                ThreadConfinedSessionSlot<
                        FastTrackWorldInventoryPublicationSession>
                    .SessionScopeToken sessionScopeToken =
                        ThreadConfinedSessionSlot<
                            FastTrackWorldInventoryPublicationSession>.Enter(
                                gameSession.Generation,
                                publicationSession);
                __state =
                    FastTrackWorldInventoryTemperatureCollectionInvocation.Active(
                        gameSession,
                        worldId,
                        publicationSession,
                        sessionScopeToken);
            }
            catch
            {
                if (publicationSessionHasBegun)
                {
                    publicationSession.Discard();
                }

                throw;
            }
        }

        internal static IEnumerable<CodeInstruction>
            BackgroundWorldInventoryRunUpdateTranspiler(
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

            VerifiedWorldInventoryFeatureBinding binding =
                RequireVerifiedWorldInventoryFeatureBinding();
            var sourceInstructions = new List<CodeInstruction>(instructions);
            int[] candidateIndices = CreateCandidateInstructionIndices(
                sourceInstructions.Count);
            int incrementalBranchStartIndex =
                RequireIncrementalBranchStartIndex(
                    sourceInstructions,
                    candidateIndices,
                    binding.FirstUpdateField);
            int completeSumTotalIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index =>
                        index < incrementalBranchStartIndex &&
                        MatchesResourceTagPublicationAnchor(
                            sourceInstructions,
                            index,
                            binding),
                    "FastTrack RunUpdate complete resource-tag publication");
            int incrementalSumTotalIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index =>
                        index >= incrementalBranchStartIndex &&
                        MatchesResourceTagPublicationAnchor(
                            sourceInstructions,
                            index,
                            binding),
                    "FastTrack RunUpdate incremental resource-tag publication");

            MethodInfo currentPublicationSessionHook =
                RequireAdapterStaticMethod(
                    nameof(GetCurrentPublicationSession),
                    typeof(FastTrackWorldInventoryPublicationSession),
                    Array.Empty<Type>());
            MethodInfo beginResourceTagHook =
                RequireAdapterStaticMethod(
                    nameof(BeginResourceTagEnumeration),
                    typeof(void),
                    new[]
                    {
                        typeof(FastTrackWorldInventoryPublicationSession),
                        typeof(Tag)
                    });
            MethodInfo completeResourceTagHook =
                RequireAdapterStaticMethod(
                    nameof(CompleteResourceTagEnumeration),
                    typeof(void),
                    new[]
                    {
                        typeof(FastTrackWorldInventoryPublicationSession)
                    });
            LocalBuilder publicationSessionLocal = generator.DeclareLocal(
                typeof(FastTrackWorldInventoryPublicationSession));
            Label skipCompleteBeginHook = generator.DefineLabel();
            Label skipCompleteEndHook = generator.DefineLabel();
            Label skipIncrementalBeginHook = generator.DefineLabel();
            Label skipIncrementalEndHook = generator.DefineLabel();

            int completeAnchorStartIndex = completeSumTotalIndex - 5;
            int incrementalAnchorStartIndex = incrementalSumTotalIndex - 5;
            sourceInstructions[completeSumTotalIndex + 1].labels.Add(
                skipCompleteEndHook);
            sourceInstructions[incrementalSumTotalIndex + 1].labels.Add(
                skipIncrementalEndHook);

            var instrumentedInstructions = new List<CodeInstruction>(
                sourceInstructions.Count + 22);
            var currentPublicationSessionCall = new CodeInstruction(
                OpCodes.Call,
                currentPublicationSessionHook);
            MoveLabels(
                sourceInstructions[0],
                currentPublicationSessionCall);
            instrumentedInstructions.Add(currentPublicationSessionCall);
            instrumentedInstructions.Add(CreateStoreLocalInstruction(
                publicationSessionLocal));

            for (var instructionIndex = 0;
                 instructionIndex < sourceInstructions.Count;
                 instructionIndex++)
            {
                if (instructionIndex == completeAnchorStartIndex)
                {
                    AddGuardedResourceTagBeginHook(
                        instrumentedInstructions,
                        publicationSessionLocal,
                        skipCompleteBeginHook,
                        sourceInstructions[completeAnchorStartIndex],
                        binding.ResourceTagGetter,
                        beginResourceTagHook);
                }
                else if (instructionIndex == incrementalAnchorStartIndex)
                {
                    AddGuardedResourceTagBeginHook(
                        instrumentedInstructions,
                        publicationSessionLocal,
                        skipIncrementalBeginHook,
                        sourceInstructions[incrementalAnchorStartIndex],
                        binding.ResourceTagGetter,
                        beginResourceTagHook);
                }

                instrumentedInstructions.Add(
                    sourceInstructions[instructionIndex]);

                if (instructionIndex == completeSumTotalIndex)
                {
                    AddGuardedResourceTagCompletionHook(
                        instrumentedInstructions,
                        publicationSessionLocal,
                        skipCompleteEndHook,
                        completeResourceTagHook);
                }
                else if (instructionIndex == incrementalSumTotalIndex)
                {
                    AddGuardedResourceTagCompletionHook(
                        instrumentedInstructions,
                        publicationSessionLocal,
                        skipIncrementalEndHook,
                        completeResourceTagHook);
                }
            }

            return instrumentedInstructions;
        }

        internal static IEnumerable<CodeInstruction>
            BackgroundWorldInventorySumTotalTranspiler(
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

            VerifiedWorldInventoryFeatureBinding binding =
                RequireVerifiedWorldInventoryFeatureBinding();
            var sourceInstructions = new List<CodeInstruction>(instructions);
            int[] candidateIndices = CreateCandidateInstructionIndices(
                sourceInstructions.Count);
            int totalAmountGetterIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index =>
                        index + 1 < sourceInstructions.Count &&
                        IsCall(
                            sourceInstructions[index],
                            binding.PickupableTotalAmountGetter) &&
                        sourceInstructions[index + 1].opcode == OpCodes.Add,
                    "FastTrack SumTotal filtered Pickupable.TotalAmount " +
                    "contribution");

            MethodInfo currentPublicationSessionHook =
                RequireAdapterStaticMethod(
                    nameof(GetCurrentPublicationSession),
                    typeof(FastTrackWorldInventoryPublicationSession),
                    Array.Empty<Type>());
            MethodInfo recordTemperatureAmountHook =
                RequireAdapterStaticMethod(
                    nameof(RecordFilteredPickupTemperatureAmount),
                    typeof(float),
                    new[]
                    {
                        typeof(Pickupable),
                        typeof(float),
                        typeof(FastTrackWorldInventoryPublicationSession)
                    });
            LocalBuilder publicationSessionLocal = generator.DeclareLocal(
                typeof(FastTrackWorldInventoryPublicationSession));
            Label originalTotalAmountGetterLabel = generator.DefineLabel();
            Label afterTotalAmountGetterLabel = generator.DefineLabel();
            sourceInstructions[totalAmountGetterIndex + 1].labels.Add(
                afterTotalAmountGetterLabel);

            var instrumentedInstructions = new List<CodeInstruction>(
                sourceInstructions.Count + 9);
            var currentPublicationSessionCall = new CodeInstruction(
                OpCodes.Call,
                currentPublicationSessionHook);
            MoveLabels(
                sourceInstructions[0],
                currentPublicationSessionCall);
            instrumentedInstructions.Add(currentPublicationSessionCall);
            instrumentedInstructions.Add(CreateStoreLocalInstruction(
                publicationSessionLocal));

            for (var instructionIndex = 0;
                 instructionIndex < sourceInstructions.Count;
                 instructionIndex++)
            {
                if (instructionIndex != totalAmountGetterIndex)
                {
                    instrumentedInstructions.Add(
                        sourceInstructions[instructionIndex]);
                    continue;
                }

                var publicationSessionGuardLoad = CreateLoadLocalInstruction(
                    publicationSessionLocal);
                MoveLabels(
                    sourceInstructions[instructionIndex],
                    publicationSessionGuardLoad);
                sourceInstructions[instructionIndex].labels.Add(
                    originalTotalAmountGetterLabel);
                instrumentedInstructions.Add(publicationSessionGuardLoad);
                instrumentedInstructions.Add(new CodeInstruction(
                    OpCodes.Brfalse,
                    originalTotalAmountGetterLabel));
                instrumentedInstructions.Add(new CodeInstruction(OpCodes.Dup));
                instrumentedInstructions.Add(new CodeInstruction(
                    sourceInstructions[instructionIndex].opcode,
                    binding.PickupableTotalAmountGetter));
                instrumentedInstructions.Add(CreateLoadLocalInstruction(
                    publicationSessionLocal));
                instrumentedInstructions.Add(new CodeInstruction(
                    OpCodes.Call,
                    recordTemperatureAmountHook));
                instrumentedInstructions.Add(new CodeInstruction(
                    OpCodes.Br,
                    afterTotalAmountGetterLabel));
                instrumentedInstructions.Add(
                    sourceInstructions[instructionIndex]);
            }

            return instrumentedInstructions;
        }

        internal static void BackgroundWorldInventoryRunUpdatePostfix(
            FastTrackWorldInventoryTemperatureCollectionInvocation __state)
        {
            if (!__state.IsActive)
            {
                return;
            }

            RequireCurrentPublicationSession(__state.PublicationSession);
            FastTrackWorldInventoryPublicationResult publicationResult =
                __state.PublicationSession.Complete();
            PublishResult(__state, publicationResult);
        }

        internal static Exception? BackgroundWorldInventoryRunUpdateFinalizer(
            Exception? __exception,
            FastTrackWorldInventoryTemperatureCollectionInvocation __state)
        {
            if (!__state.IsActive)
            {
                return __exception;
            }

            Exception? cleanupException = null;
            try
            {
                RequireCurrentPublicationSession(__state.PublicationSession);
                __state.PublicationSession.Discard();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            try
            {
                ThreadConfinedSessionSlot<
                    FastTrackWorldInventoryPublicationSession>.Exit(
                        __state.SessionScopeToken);
            }
            catch (Exception exception)
            {
                if (cleanupException == null)
                {
                    cleanupException = exception;
                }
            }

            if (__exception != null)
            {
                // Cleanup must never replace the original FastTrack/game failure.
                return __exception;
            }

            if (cleanupException != null)
            {
                ExceptionDispatchInfo.Capture(cleanupException).Throw();
            }

            return null;
        }

        private static FastTrackWorldInventoryPublicationSession
            CreatePublicationSession(object backgroundWorldInventory) =>
            new FastTrackWorldInventoryPublicationSession();

        private static FastTrackWorldInventoryPublicationSession?
            GetCurrentPublicationSession()
        {
            return ThreadConfinedSessionSlot<
                    FastTrackWorldInventoryPublicationSession>.TryGetCurrent(
                        out FastTrackWorldInventoryPublicationSession
                            publicationSession)
                ? publicationSession
                : null;
        }

        private static void BeginResourceTagEnumeration(
            FastTrackWorldInventoryPublicationSession publicationSession,
            Tag resourceTag)
        {
            if (publicationSession == null)
            {
                throw new ArgumentNullException(nameof(publicationSession));
            }

            publicationSession.BeginResourceTag(resourceTag);
        }

        private static float RecordFilteredPickupTemperatureAmount(
            Pickupable pickupable,
            float originalTotalAmount,
            FastTrackWorldInventoryPublicationSession publicationSession)
        {
            if (pickupable == null)
            {
                return originalTotalAmount;
            }

            if (publicationSession == null)
            {
                throw new ArgumentNullException(nameof(publicationSession));
            }

            // Read FastTrack's already selected Pickupable exactly once. The hook
            // is placed after its cell/world/private-storage filters and receives
            // the amount produced by the original TotalAmount getter.
            PrimaryElement primaryElement = pickupable.PrimaryElement;
            if (primaryElement != null)
            {
                publicationSession.AddTemperatureAmount(
                    primaryElement.Temperature,
                    originalTotalAmount);
            }

            return originalTotalAmount;
        }

        private static void CompleteResourceTagEnumeration(
            FastTrackWorldInventoryPublicationSession publicationSession)
        {
            if (publicationSession == null)
            {
                throw new ArgumentNullException(nameof(publicationSession));
            }

            publicationSession.CompleteResourceTag();
        }

        private static void PublishResult(
            FastTrackWorldInventoryTemperatureCollectionInvocation invocation,
            FastTrackWorldInventoryPublicationResult publicationResult)
        {
            WorldResourceTemperatureAmountCatalog catalog =
                invocation.GameSession.WorldResourceTemperatureAmounts;
            switch (publicationResult.Kind)
            {
                case FastTrackWorldInventoryPublicationKind.CompleteWorldAmounts:
                    if (!publicationResult
                        .TryGetCompleteWorldResourceTemperatureAmounts(
                            out CompleteWorldResourceTemperatureAmounts
                                completeWorldAmounts))
                    {
                        throw CreatePublicationPayloadViolation(
                            publicationResult.Kind);
                    }

                    catalog.PublishCompleteWorldResourceAmounts(
                        invocation.WorldId,
                        completeWorldAmounts);
                    break;

                case FastTrackWorldInventoryPublicationKind
                    .ResourceTagCoverageAndTemperatureSeries:
                    if (!publicationResult.TryGetWorldResourceTagCoverage(
                            out WorldResourceTagCoverage coverage) ||
                        !publicationResult
                            .TryGetWorldResourceTemperatureSeriesPublication(
                                out WorldResourceTemperatureSeriesPublication
                                    temperatureSeries))
                    {
                        throw CreatePublicationPayloadViolation(
                            publicationResult.Kind);
                    }

                    // Membership must be accepted first. If a concurrent newer
                    // generation wins, the series is deliberately not published.
                    if (catalog.PublishWorldResourceTagCoverage(
                            invocation.WorldId,
                            coverage))
                    {
                        catalog.PublishWorldResourceTemperatureSeries(
                            invocation.WorldId,
                            temperatureSeries);
                    }

                    break;

                case FastTrackWorldInventoryPublicationKind
                    .ResourceTemperatureSeries:
                    if (!publicationResult
                        .TryGetWorldResourceTemperatureSeriesPublication(
                            out WorldResourceTemperatureSeriesPublication
                                currentCoverageTemperatureSeries))
                    {
                        throw CreatePublicationPayloadViolation(
                            publicationResult.Kind);
                    }

                    catalog.PublishWorldResourceTemperatureSeries(
                        invocation.WorldId,
                        currentCoverageTemperatureSeries);
                    break;

                case FastTrackWorldInventoryPublicationKind
                    .ResourceTagCoverageOnly:
                    if (!publicationResult.TryGetWorldResourceTagCoverage(
                            out WorldResourceTagCoverage coverageOnly))
                    {
                        throw CreatePublicationPayloadViolation(
                            publicationResult.Kind);
                    }

                    catalog.PublishWorldResourceTagCoverage(
                        invocation.WorldId,
                        coverageOnly);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(publicationResult),
                        publicationResult.Kind,
                        "Unknown FastTrack world-inventory publication kind.");
            }
        }

        private static InvalidOperationException
            CreatePublicationPayloadViolation(
                FastTrackWorldInventoryPublicationKind publicationKind) =>
            new InvalidOperationException(
                "FastTrack world-inventory publication kind " +
                publicationKind +
                " did not expose its required canonical payload.");

        private static void RequireCurrentPublicationSession(
            FastTrackWorldInventoryPublicationSession expectedSession)
        {
            if (!ThreadConfinedSessionSlot<
                    FastTrackWorldInventoryPublicationSession>.TryGetCurrent(
                        out FastTrackWorldInventoryPublicationSession
                            currentSession) ||
                !ReferenceEquals(currentSession, expectedSession))
            {
                throw new InvalidOperationException(
                    "The thread-confined FastTrack world-inventory publication " +
                    "session no longer matches the invocation being completed.");
            }
        }

        private static void EmitUnknownCoverageDiagnosticOnce(
            DeliveryTemperatureGameSession gameSession,
            int worldId,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            if (gameSession.DiagnosticLimiter.ShouldEmit(
                    UnknownCoverageDiagnosticKey))
            {
                Debug.LogWarning(
                    "DeliveryTemperatureLimit: FastTrack world-inventory " +
                    "temperature publication was skipped because world " +
                    worldId +
                    " has no registered coverage contract for collection " +
                    collectionGeneration.Value +
                    ". No world identity or resource membership was guessed.");
            }
        }

        private static void AddGuardedResourceTagBeginHook(
            ICollection<CodeInstruction> destination,
            LocalBuilder publicationSessionLocal,
            Label skipHookLabel,
            CodeInstruction resourceTagPairAddressLoad,
            MethodInfo resourceTagGetter,
            MethodInfo beginResourceTagHook)
        {
            var publicationSessionGuardLoad = CreateLoadLocalInstruction(
                publicationSessionLocal);
            // Any upstream branch that entered the original semantic anchor must
            // now enter its guard. Add our private bypass label only afterward so
            // it remains on the original instruction and cannot loop to itself.
            MoveLabels(
                resourceTagPairAddressLoad,
                publicationSessionGuardLoad);
            resourceTagPairAddressLoad.labels.Add(skipHookLabel);
            destination.Add(publicationSessionGuardLoad);
            destination.Add(new CodeInstruction(
                OpCodes.Brfalse,
                skipHookLabel));
            destination.Add(CreateLoadLocalInstruction(
                publicationSessionLocal));
            destination.Add(new CodeInstruction(
                resourceTagPairAddressLoad.opcode,
                resourceTagPairAddressLoad.operand));
            destination.Add(new CodeInstruction(
                OpCodes.Call,
                resourceTagGetter));
            destination.Add(new CodeInstruction(
                OpCodes.Call,
                beginResourceTagHook));
        }

        private static void AddGuardedResourceTagCompletionHook(
            ICollection<CodeInstruction> destination,
            LocalBuilder publicationSessionLocal,
            Label skipHookLabel,
            MethodInfo completeResourceTagHook)
        {
            destination.Add(CreateLoadLocalInstruction(
                publicationSessionLocal));
            destination.Add(new CodeInstruction(
                OpCodes.Brfalse,
                skipHookLabel));
            destination.Add(CreateLoadLocalInstruction(
                publicationSessionLocal));
            destination.Add(new CodeInstruction(
                OpCodes.Call,
                completeResourceTagHook));
        }

        private static int RequireIncrementalBranchStartIndex(
            IReadOnlyList<CodeInstruction> instructions,
            int[] candidateIndices,
            FieldInfo firstUpdateField)
        {
            int firstUpdateBranchIndex =
                HarmonyPatchContractVerifier.RequireSingleMatch(
                    candidateIndices,
                    index =>
                        index > 0 &&
                        IsFieldLoad(
                            instructions[index - 1],
                            firstUpdateField) &&
                        IsFalseBranch(instructions[index]),
                    "FastTrack RunUpdate firstUpdate false branch");
            if (!(instructions[firstUpdateBranchIndex].operand is Label
                    incrementalBranchLabel))
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack RunUpdate firstUpdate branch does not target one " +
                    "typed IL label.");
            }

            return HarmonyPatchContractVerifier.RequireSingleMatch(
                candidateIndices,
                index => instructions[index].labels.Contains(
                    incrementalBranchLabel),
                "FastTrack RunUpdate incremental branch target");
        }

        private static bool MatchesResourceTagPublicationAnchor(
            IReadOnlyList<CodeInstruction> instructions,
            int sumTotalCallIndex,
            VerifiedWorldInventoryFeatureBinding binding)
        {
            if (sumTotalCallIndex < 5 ||
                sumTotalCallIndex + 1 >= instructions.Count ||
                !IsLoadLocalAddress(
                    instructions[sumTotalCallIndex - 5]) ||
                !IsCall(
                    instructions[sumTotalCallIndex - 4],
                    binding.ResourceTagGetter) ||
                !IsLoadLocalAddress(
                    instructions[sumTotalCallIndex - 3]) ||
                instructions[sumTotalCallIndex - 5].LocalIndex() !=
                    instructions[sumTotalCallIndex - 3].LocalIndex() ||
                !IsCall(
                    instructions[sumTotalCallIndex - 2],
                    binding.PickupableSetGetter) ||
                !IsLoadLocal(instructions[sumTotalCallIndex - 1]) ||
                !IsCall(
                    instructions[sumTotalCallIndex],
                    binding.SumTotal) ||
                !IsCall(
                    instructions[sumTotalCallIndex + 1],
                    binding.AccessibleAmountSetter))
            {
                return false;
            }

            return true;
        }

        private static MethodInfo RequireAdapterStaticMethod(
            string methodName,
            Type returnType,
            IReadOnlyList<Type> orderedParameterTypes) =>
            HarmonyPatchContractVerifier.RequireStaticMethod(
                typeof(FastTrackWorldInventoryTemperaturePatches),
                methodName,
                DeclaredMemberVisibility.NonPublic,
                returnType,
                orderedParameterTypes);

        private static VerifiedWorldInventoryFeatureBinding
            RequireVerifiedWorldInventoryFeatureBinding()
        {
            VerifiedWorldInventoryFeatureBinding? binding = Volatile.Read(
                ref verifiedWorldInventoryFeatureBinding);
            if (binding == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "The FastTrack world-inventory adapter was used before its " +
                    "Ready feature contract was bound.");
            }

            return binding;
        }

        private static int[] CreateCandidateInstructionIndices(
            int instructionCount)
        {
            var candidateIndices = new int[instructionCount];
            for (var index = 0; index < instructionCount; index++)
            {
                candidateIndices[index] = index;
            }

            return candidateIndices;
        }

        private static CodeInstruction CreateLoadLocalInstruction(
            LocalBuilder local) =>
            new CodeInstruction(OpCodes.Ldloc, local);

        private static CodeInstruction CreateStoreLocalInstruction(
            LocalBuilder local) =>
            new CodeInstruction(OpCodes.Stloc, local);

        private static void MoveLabels(
            CodeInstruction source,
            CodeInstruction destination)
        {
            if (source.labels.Count == 0)
            {
                return;
            }

            destination.labels.AddRange(source.labels);
            source.labels.Clear();
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

        private static bool IsFalseBranch(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Brfalse ||
            instruction.opcode == OpCodes.Brfalse_S;

        /// <summary>
        /// Exact value-type state carried by Harmony from prefix through postfix
        /// and finalizer. Its inactive value retains no game or inventory object.
        /// </summary>
        internal readonly struct
            FastTrackWorldInventoryTemperatureCollectionInvocation
        {
            private readonly DeliveryTemperatureGameSession? gameSession;
            private readonly FastTrackWorldInventoryPublicationSession?
                publicationSession;
            private readonly ThreadConfinedSessionSlot<
                    FastTrackWorldInventoryPublicationSession>
                .SessionScopeToken sessionScopeToken;

            private FastTrackWorldInventoryTemperatureCollectionInvocation(
                DeliveryTemperatureGameSession gameSession,
                int worldId,
                FastTrackWorldInventoryPublicationSession publicationSession,
                ThreadConfinedSessionSlot<
                        FastTrackWorldInventoryPublicationSession>
                    .SessionScopeToken sessionScopeToken)
            {
                this.gameSession = gameSession ??
                    throw new ArgumentNullException(nameof(gameSession));
                if (worldId < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(worldId));
                }

                this.publicationSession = publicationSession ??
                    throw new ArgumentNullException(nameof(publicationSession));
                WorldId = worldId;
                this.sessionScopeToken = sessionScopeToken;
            }

            internal static
                FastTrackWorldInventoryTemperatureCollectionInvocation Inactive =>
                default(FastTrackWorldInventoryTemperatureCollectionInvocation);

            internal bool IsActive => gameSession != null;

            internal DeliveryTemperatureGameSession GameSession =>
                gameSession ?? throw new InvalidOperationException(
                    "An inactive FastTrack inventory invocation has no game " +
                    "session.");

            internal int WorldId { get; }

            internal FastTrackWorldInventoryPublicationSession
                PublicationSession =>
                publicationSession ?? throw new InvalidOperationException(
                    "An inactive FastTrack inventory invocation has no " +
                    "publication session.");

            internal ThreadConfinedSessionSlot<
                    FastTrackWorldInventoryPublicationSession>
                .SessionScopeToken SessionScopeToken => sessionScopeToken;

            internal static
                FastTrackWorldInventoryTemperatureCollectionInvocation Active(
                    DeliveryTemperatureGameSession gameSession,
                    int worldId,
                    FastTrackWorldInventoryPublicationSession publicationSession,
                    ThreadConfinedSessionSlot<
                            FastTrackWorldInventoryPublicationSession>
                        .SessionScopeToken sessionScopeToken) =>
                new FastTrackWorldInventoryTemperatureCollectionInvocation(
                    gameSession,
                    worldId,
                    publicationSession,
                    sessionScopeToken);
        }

        /// <summary>
        /// Cold, immutable bridge from semantic compatibility roles to exact
        /// runtime members. FastTrack itself remains an optional reflection-only
        /// dependency; no compatibility facade or public shim is introduced.
        /// </summary>
        private sealed class VerifiedWorldInventoryFeatureBinding
        {
            private readonly Func<
                    WorldInventory,
                    Dictionary<Tag, HashSet<Pickupable>>>
                readWorldInventoryEntries;

            private VerifiedWorldInventoryFeatureBinding(
                MethodInfo runUpdate,
                MethodInfo sumTotal,
                FieldInfo firstUpdateField,
                FieldInfo updateIndexField,
                FieldInfo worldContainerField,
                FieldInfo worldInventoryField,
                FieldInfo worldInventoryEntriesField,
                Func<
                    WorldInventory,
                    Dictionary<Tag, HashSet<Pickupable>>>
                    readWorldInventoryEntries,
                MethodInfo resourceTagGetter,
                MethodInfo pickupableSetGetter,
                MethodInfo accessibleAmountSetter,
                MethodInfo pickupableTotalAmountGetter)
            {
                RunUpdate = runUpdate;
                SumTotal = sumTotal;
                FirstUpdateField = firstUpdateField;
                UpdateIndexField = updateIndexField;
                WorldContainerField = worldContainerField;
                WorldInventoryField = worldInventoryField;
                WorldInventoryEntriesField = worldInventoryEntriesField;
                this.readWorldInventoryEntries = readWorldInventoryEntries;
                ResourceTagGetter = resourceTagGetter;
                PickupableSetGetter = pickupableSetGetter;
                AccessibleAmountSetter = accessibleAmountSetter;
                PickupableTotalAmountGetter = pickupableTotalAmountGetter;
            }

            internal MethodInfo RunUpdate { get; }

            internal MethodInfo SumTotal { get; }

            internal FieldInfo FirstUpdateField { get; }

            internal FieldInfo UpdateIndexField { get; }

            internal FieldInfo WorldContainerField { get; }

            internal FieldInfo WorldInventoryField { get; }

            internal FieldInfo WorldInventoryEntriesField { get; }

            internal MethodInfo ResourceTagGetter { get; }

            internal MethodInfo PickupableSetGetter { get; }

            internal MethodInfo AccessibleAmountSetter { get; }

            internal MethodInfo PickupableTotalAmountGetter { get; }

            internal static VerifiedWorldInventoryFeatureBinding Create(
                FastTrackFeatureCompatibility compatibility)
            {
                MethodInfo runUpdate = RequireVerifiedMethod(
                    compatibility,
                    FastTrackVerifiedMember.BackgroundWorldInventoryRunUpdate,
                    isStatic: false,
                    typeof(void),
                    Array.Empty<Type>());
                MethodInfo sumTotal = RequireVerifiedMethod(
                    compatibility,
                    FastTrackVerifiedMember.BackgroundWorldInventorySumTotal,
                    isStatic: true,
                    typeof(float),
                    new[] { typeof(IEnumerable<Pickupable>), typeof(int) });
                FieldInfo firstUpdateField = RequireVerifiedField(
                    compatibility,
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryFirstUpdateField,
                    typeof(bool));
                FieldInfo updateIndexField = RequireVerifiedField(
                    compatibility,
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryUpdateIndexField,
                    typeof(int));
                FieldInfo worldContainerField = RequireVerifiedField(
                    compatibility,
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryWorldContainerField,
                    typeof(WorldContainer));
                FieldInfo worldInventoryField = RequireVerifiedField(
                    compatibility,
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryWorldInventoryField,
                    typeof(WorldInventory));
                FieldInfo worldInventoryEntriesField = RequireVerifiedField(
                    compatibility,
                    FastTrackVerifiedMember.WorldInventoryInventoryField,
                    typeof(Dictionary<Tag, HashSet<Pickupable>>));
                Type? declaringType = runUpdate.DeclaringType;
                if (declaringType == null ||
                    sumTotal.DeclaringType != declaringType ||
                    firstUpdateField.DeclaringType != declaringType ||
                    updateIndexField.DeclaringType != declaringType ||
                    worldContainerField.DeclaringType != declaringType ||
                    worldInventoryField.DeclaringType != declaringType)
                {
                    throw new HarmonyPatchContractViolationException(
                        "Verified FastTrack world-inventory members do not share " +
                        "one declaring implementation type.");
                }

                if (worldInventoryEntriesField.DeclaringType !=
                    typeof(WorldInventory))
                {
                    throw new HarmonyPatchContractViolationException(
                        "The verified FastTrack inventory field is not declared " +
                        "by the current ONI WorldInventory type.");
                }

                Func<WorldInventory, Dictionary<Tag, HashSet<Pickupable>>>
                    readWorldInventoryEntries =
                        CreateWorldInventoryEntriesReader(
                            worldInventoryEntriesField);

                MethodInfo resourceTagGetter =
                    HarmonyPatchContractVerifier.RequireInstanceMethod(
                        typeof(KeyValuePair<Tag, HashSet<Pickupable>>),
                        "get_Key",
                        DeclaredMemberVisibility.Public,
                        typeof(Tag),
                        Array.Empty<Type>());
                MethodInfo pickupableSetGetter =
                    HarmonyPatchContractVerifier.RequireInstanceMethod(
                        typeof(KeyValuePair<Tag, HashSet<Pickupable>>),
                        "get_Value",
                        DeclaredMemberVisibility.Public,
                        typeof(HashSet<Pickupable>),
                        Array.Empty<Type>());
                MethodInfo accessibleAmountSetter =
                    HarmonyPatchContractVerifier.RequireInstanceMethod(
                        typeof(Dictionary<Tag, float>),
                        "set_Item",
                        DeclaredMemberVisibility.Public,
                        typeof(void),
                        new[] { typeof(Tag), typeof(float) });
                MethodInfo pickupableTotalAmountGetter =
                    HarmonyPatchContractVerifier.RequireInstanceMethod(
                        typeof(Pickupable),
                        "get_TotalAmount",
                        DeclaredMemberVisibility.Public,
                        typeof(float),
                        Array.Empty<Type>());

                return new VerifiedWorldInventoryFeatureBinding(
                    runUpdate,
                    sumTotal,
                    firstUpdateField,
                    updateIndexField,
                    worldContainerField,
                    worldInventoryField,
                    worldInventoryEntriesField,
                    readWorldInventoryEntries,
                    resourceTagGetter,
                    pickupableSetGetter,
                    accessibleAmountSetter,
                    pickupableTotalAmountGetter);
            }

            internal bool ReferencesSameMembers(
                VerifiedWorldInventoryFeatureBinding other) =>
                Equals(RunUpdate, other.RunUpdate) &&
                Equals(SumTotal, other.SumTotal) &&
                Equals(FirstUpdateField, other.FirstUpdateField) &&
                Equals(UpdateIndexField, other.UpdateIndexField) &&
                Equals(WorldContainerField, other.WorldContainerField) &&
                Equals(WorldInventoryField, other.WorldInventoryField) &&
                Equals(
                    WorldInventoryEntriesField,
                    other.WorldInventoryEntriesField);

            internal Dictionary<Tag, HashSet<Pickupable>>?
                ReadWorldInventoryEntries(WorldInventory worldInventory) =>
                readWorldInventoryEntries(worldInventory);

            private static Func<
                    WorldInventory,
                    Dictionary<Tag, HashSet<Pickupable>>>
                CreateWorldInventoryEntriesReader(
                    FieldInfo worldInventoryEntriesField)
            {
                try
                {
                    var dynamicReader = new DynamicMethod(
                        "ReadVerifiedWorldInventoryEntries",
                        typeof(Dictionary<Tag, HashSet<Pickupable>>),
                        new[] { typeof(WorldInventory) },
                        typeof(FastTrackWorldInventoryTemperaturePatches),
                        skipVisibility: true);
                    ILGenerator generator = dynamicReader.GetILGenerator();
                    generator.Emit(OpCodes.Ldarg_0);
                    generator.Emit(
                        OpCodes.Ldfld,
                        worldInventoryEntriesField);
                    generator.Emit(OpCodes.Ret);
                    return (Func<
                        WorldInventory,
                        Dictionary<Tag, HashSet<Pickupable>>>)
                        dynamicReader.CreateDelegate(
                            typeof(Func<
                                WorldInventory,
                                Dictionary<Tag, HashSet<Pickupable>>>));
                }
                catch (ArgumentException exception)
                {
                    throw new HarmonyPatchContractViolationException(
                        "The verified ONI WorldInventory.Inventory field could " +
                        "not be bound to its typed worker-safe reader.",
                        exception);
                }
                catch (MethodAccessException exception)
                {
                    throw new HarmonyPatchContractViolationException(
                        "The verified ONI WorldInventory.Inventory field is not " +
                        "accessible through a pre-bound typed reader.",
                        exception);
                }
                catch (NotSupportedException exception)
                {
                    throw new HarmonyPatchContractViolationException(
                        "The current ONI runtime cannot create the verified typed " +
                        "WorldInventory.Inventory field reader.",
                        exception);
                }
            }

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

            private static FieldInfo RequireVerifiedField(
                FastTrackFeatureCompatibility compatibility,
                FastTrackVerifiedMember memberRole,
                Type fieldType)
            {
                if (!(compatibility.GetVerifiedMember(memberRole) is FieldInfo
                        field) ||
                    field.IsStatic ||
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
