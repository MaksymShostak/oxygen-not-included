#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Session-owned catalog of immutable per-world temperature contributions and
    /// preaggregated parent-world/resource-tag series.
    /// </summary>
    /// <remarks>
    /// All publication state is protected by one private lock. Sparse series
    /// combination occurs outside that lock and is published only if the captured
    /// parent membership and per-tag evidence still match. Readers capture one
    /// immutable aggregate and therefore observe a whole old or whole new value.
    /// </remarks>
    internal sealed class WorldResourceTemperatureAmountCatalog
    {
        private readonly object synchronization = new object();
        private readonly Dictionary<int, WorldRegistration>
            worldRegistrationsByWorldId =
                new Dictionary<int, WorldRegistration>();
        private readonly Dictionary<int, WorldPublicationState>
            worldPublicationsByWorldId =
                new Dictionary<int, WorldPublicationState>();
        private readonly Dictionary<int, ParentWorldState>
            parentWorldStatesByParentWorldId =
                new Dictionary<int, ParentWorldState>();

        // The exact field name is an intentional optimized-reuse contract. Tests
        // capture a value reference to prove unrelated tag updates leave it intact.
        private readonly Dictionary<
            ParentWorldResourceTagKey,
            ParentWorldResourceTemperatureAggregate>
                aggregatesByParentWorldAndResourceTag =
                    new Dictionary<
                        ParentWorldResourceTagKey,
                        ParentWorldResourceTemperatureAggregate>();

        private bool acceptsPublications = true;
        private long latestObservedCollectionGenerationValue;

        internal void RegisterWorld(int worldId, int parentWorldId)
        {
            var parentWorldsToRebuild =
                new List<ParentWorldRebuildRequest>(capacity: 2);
            lock (synchronization)
            {
                ThrowIfNoLongerAcceptingRegistrations();

                WorldRegistration? existingRegistration;
                if (!worldRegistrationsByWorldId.TryGetValue(
                    worldId,
                    out existingRegistration))
                {
                    ParentWorldState parentWorldState =
                        GetOrCreateParentWorldStateLocked(parentWorldId);
                    long nextMemberSetVersion = checked(
                        parentWorldState.MemberSetVersion + 1L);
                    parentWorldState.MemberWorldIds.Add(worldId);
                    parentWorldState.MemberSetVersion = nextMemberSetVersion;
                    worldRegistrationsByWorldId.Add(
                        worldId,
                        new WorldRegistration(parentWorldId));
                    worldPublicationsByWorldId.Add(
                        worldId,
                        new WorldPublicationState());

                    QueueParentWorldRebuildLocked(
                        parentWorldId,
                        parentWorldsToRebuild);
                }
                else if (existingRegistration.ParentWorldId != parentWorldId)
                {
                    int previousParentWorldId =
                        existingRegistration.ParentWorldId;
                    ParentWorldState previousParentWorldState =
                        parentWorldStatesByParentWorldId[previousParentWorldId];
                    ParentWorldState nextParentWorldState =
                        GetOrCreateParentWorldStateLocked(parentWorldId);
                    long nextPreviousParentMemberSetVersion = checked(
                        previousParentWorldState.MemberSetVersion + 1L);
                    long nextNewParentMemberSetVersion = checked(
                        nextParentWorldState.MemberSetVersion + 1L);

                    previousParentWorldState.MemberWorldIds.Remove(worldId);
                    previousParentWorldState.MemberSetVersion =
                        nextPreviousParentMemberSetVersion;
                    nextParentWorldState.MemberWorldIds.Add(worldId);
                    nextParentWorldState.MemberSetVersion =
                        nextNewParentMemberSetVersion;
                    existingRegistration.ParentWorldId = parentWorldId;

                    QueueParentWorldRebuildLocked(
                        previousParentWorldId,
                        parentWorldsToRebuild);
                    QueueParentWorldRebuildLocked(
                        parentWorldId,
                        parentWorldsToRebuild);

                    if (previousParentWorldState.MemberWorldIds.Count == 0)
                    {
                        parentWorldStatesByParentWorldId.Remove(
                            previousParentWorldId);
                        RemoveParentWorldAggregatesLocked(
                            previousParentWorldId);
                        RemoveQueuedParentWorldRebuild(
                            previousParentWorldId,
                            parentWorldsToRebuild);
                    }
                }
                else
                {
                    return;
                }
            }

            RebuildQueuedParentWorlds(parentWorldsToRebuild);
        }

        internal bool PublishCompleteWorldResourceAmounts(
            int worldId,
            CompleteWorldResourceTemperatureAmounts resourceAmounts)
        {
            if (resourceAmounts == null)
            {
                throw new ArgumentNullException(nameof(resourceAmounts));
            }

            int parentWorldId;
            HashSet<Tag> affectedResourceTags;
            WorldInventoryCollectionGeneration collectionGeneration =
                resourceAmounts.CollectionGeneration;
            lock (synchronization)
            {
                WorldRegistration? registration;
                WorldPublicationState? priorWorldPublication;
                if (!acceptsPublications ||
                    !worldRegistrationsByWorldId.TryGetValue(
                        worldId,
                        out registration) ||
                    !worldPublicationsByWorldId.TryGetValue(
                        worldId,
                        out priorWorldPublication))
                {
                    return false;
                }

                if (priorWorldPublication.HasPublication &&
                    collectionGeneration.Value <
                        priorWorldPublication.CollectionGeneration.Value)
                {
                    return false;
                }

                parentWorldId = registration.ParentWorldId;
                affectedResourceTags = new HashSet<Tag>(
                    priorWorldPublication.PresentResourceTags);
                AddPendingParentResourceTagsLocked(
                    parentWorldId,
                    affectedResourceTags);

                var replacementPresentResourceTags = new HashSet<Tag>();
                var replacementTemperatureAmountsByResourceTag =
                    new Dictionary<Tag, TemperatureAmountSeries>(
                        resourceAmounts.ResourceTags.Count);
                foreach (Tag resourceTag in resourceAmounts.ResourceTags)
                {
                    TemperatureAmountSeries temperatureAmounts;
                    if (!resourceAmounts.TryGetSeries(
                        resourceTag,
                        out temperatureAmounts))
                    {
                        throw new InvalidOperationException(
                            "A complete-world publication listed a resource tag " +
                            "without its temperature amount series.");
                    }

                    replacementPresentResourceTags.Add(resourceTag);
                    replacementTemperatureAmountsByResourceTag.Add(
                        resourceTag,
                        temperatureAmounts);
                    affectedResourceTags.Add(resourceTag);
                }

                priorWorldPublication.ReplaceWithCompleteWorld(
                    collectionGeneration,
                    replacementPresentResourceTags,
                    replacementTemperatureAmountsByResourceTag);
                ObserveCollectionGenerationLocked(collectionGeneration);
                UpdateParentCoverageCompletionLocked(
                    parentWorldId,
                    collectionGeneration);
                InvalidateParentResourceTagAggregatesLocked(
                    parentWorldId,
                    collectionGeneration,
                    affectedResourceTags);
            }

            RebuildAffectedParentResourceTagAggregates(
                parentWorldId,
                affectedResourceTags,
                collectionGeneration);
            return true;
        }

        internal bool PublishWorldResourceTagCoverage(
            int worldId,
            WorldResourceTagCoverage resourceTagCoverage)
        {
            if (resourceTagCoverage == null)
            {
                throw new ArgumentNullException(nameof(resourceTagCoverage));
            }

            int parentWorldId;
            HashSet<Tag> affectedResourceTags;
            WorldInventoryCollectionGeneration collectionGeneration =
                resourceTagCoverage.CollectionGeneration;
            lock (synchronization)
            {
                WorldRegistration? registration;
                WorldPublicationState? priorWorldPublication;
                if (!acceptsPublications ||
                    !worldRegistrationsByWorldId.TryGetValue(
                        worldId,
                        out registration) ||
                    !worldPublicationsByWorldId.TryGetValue(
                        worldId,
                        out priorWorldPublication))
                {
                    return false;
                }

                if (priorWorldPublication.HasPublication)
                {
                    if (collectionGeneration.Value <
                        priorWorldPublication.CollectionGeneration.Value)
                    {
                        return false;
                    }

                    if (collectionGeneration.Equals(
                            priorWorldPublication.CollectionGeneration) &&
                        priorWorldPublication.PublicationStrength ==
                            WorldPublicationStrength.CompleteWorld)
                    {
                        // Coverage contains less evidence than the complete map at
                        // the same generation and may not semantically downgrade it.
                        return false;
                    }
                }

                parentWorldId = registration.ParentWorldId;
                var replacementPresentResourceTags = new HashSet<Tag>(
                    resourceTagCoverage.PresentResourceTags);
                affectedResourceTags = new HashSet<Tag>();
                if (!priorWorldPublication.HasPublication ||
                    !collectionGeneration.Equals(
                        priorWorldPublication.CollectionGeneration))
                {
                    affectedResourceTags.UnionWith(
                        priorWorldPublication.PresentResourceTags);
                    affectedResourceTags.UnionWith(
                        replacementPresentResourceTags);
                }
                else
                {
                    foreach (Tag previousResourceTag in
                        priorWorldPublication.PresentResourceTags)
                    {
                        if (!replacementPresentResourceTags.Contains(
                            previousResourceTag))
                        {
                            affectedResourceTags.Add(previousResourceTag);
                        }
                    }

                    foreach (Tag replacementResourceTag in
                        replacementPresentResourceTags)
                    {
                        if (!priorWorldPublication.PresentResourceTags.Contains(
                            replacementResourceTag))
                        {
                            affectedResourceTags.Add(replacementResourceTag);
                        }
                    }
                }

                // A concurrent or earlier membership change may have left a
                // requested tag explicitly pending even when this world's key set
                // did not change for that tag. The coverage arrival owns its one
                // rebuild attempt.
                AddPendingParentResourceTagsLocked(
                    parentWorldId,
                    affectedResourceTags);
                priorWorldPublication.ReplaceWithTagCoverage(
                    collectionGeneration,
                    replacementPresentResourceTags);
                ObserveCollectionGenerationLocked(collectionGeneration);
                UpdateParentCoverageCompletionLocked(
                    parentWorldId,
                    collectionGeneration);
                InvalidateParentResourceTagAggregatesLocked(
                    parentWorldId,
                    collectionGeneration,
                    affectedResourceTags);
            }

            RebuildAffectedParentResourceTagAggregates(
                parentWorldId,
                affectedResourceTags,
                collectionGeneration);
            return true;
        }

        internal bool PublishWorldResourceTemperatureSeries(
            int worldId,
            WorldResourceTemperatureSeriesPublication
                temperatureSeriesPublication)
        {
            int parentWorldId;
            WorldInventoryCollectionGeneration collectionGeneration =
                temperatureSeriesPublication.CollectionGeneration;
            Tag resourceTag = temperatureSeriesPublication.ResourceTag;
            lock (synchronization)
            {
                WorldRegistration? registration;
                WorldPublicationState? worldPublication;
                if (!acceptsPublications ||
                    !worldRegistrationsByWorldId.TryGetValue(
                        worldId,
                        out registration) ||
                    !worldPublicationsByWorldId.TryGetValue(
                        worldId,
                        out worldPublication) ||
                    !worldPublication.HasPublication ||
                    !worldPublication.CollectionGeneration.Equals(
                        collectionGeneration) ||
                    worldPublication.PublicationStrength ==
                        WorldPublicationStrength.NoCoverage)
                {
                    return false;
                }

                parentWorldId = registration.ParentWorldId;
                // Presence extension and series replacement are one lock
                // transaction: no reader can observe "present but pending" solely
                // because a newly discovered tag was published in two steps.
                worldPublication.PublishResourceTagTemperatureAmounts(
                    resourceTag,
                    temperatureSeriesPublication.TemperatureAmounts);
                UpdateParentCoverageCompletionLocked(
                    parentWorldId,
                    collectionGeneration);
                // Keep the prior immutable aggregate visible during this one-tag
                // optimistic rebuild. Readers may observe the whole old value or
                // the whole new value, but never a torn or manufactured-incomplete
                // intermediate solely because combination occurs outside the lock.
            }

            RebuildOneParentResourceTagAggregate(
                parentWorldId,
                resourceTag,
                collectionGeneration);
            return true;
        }

        internal WorldResourceTagCoverageRequirementState
            GetWorldResourceTagCoverageRequirementState(
                int worldId,
                WorldInventoryCollectionGeneration
                    expectedCollectionGeneration)
        {
            lock (synchronization)
            {
                if (!acceptsPublications ||
                    !worldRegistrationsByWorldId.ContainsKey(worldId))
                {
                    return WorldResourceTagCoverageRequirementState
                        .UnknownWorldOrCollectionGeneration;
                }

                WorldPublicationState worldPublication =
                    worldPublicationsByWorldId[worldId];
                if (!worldPublication.HasPublication)
                {
                    return WorldResourceTagCoverageRequirementState
                        .CoverageRequired;
                }

                if (worldPublication.CollectionGeneration.Equals(
                    expectedCollectionGeneration))
                {
                    return worldPublication.PublicationStrength ==
                        WorldPublicationStrength.NoCoverage
                            ? WorldResourceTagCoverageRequirementState
                                .CoverageRequired
                            : WorldResourceTagCoverageRequirementState
                                .CoverageCurrent;
                }

                return worldPublication.CollectionGeneration.Value <
                    expectedCollectionGeneration.Value
                        ? WorldResourceTagCoverageRequirementState
                            .CoverageRequired
                        : WorldResourceTagCoverageRequirementState
                            .UnknownWorldOrCollectionGeneration;
            }
        }

        internal TemperatureConstrainedAmountAvailability GetTemperatureConstrainedAmountAvailability(
                int parentWorldId,
                Tag resourceTag,
                DeliveryTemperatureConstraint constraint,
                WorldInventoryCollectionGeneration
                    expectedCollectionGeneration)
        {
            if (!constraint.IsEnabled)
            {
                return TemperatureConstrainedAmountAvailability
                    .TemperatureConstraintDisabled();
            }

            if (constraint.IsEmpty)
            {
                // No temperature can satisfy an enabled empty interval. This is a
                // complete semantic zero independent of inventory arrival.
                return TemperatureConstrainedAmountAvailability.Complete(0.0f);
            }

            TemperatureAmountSeries? completeTemperatureAmounts = null;
            lock (synchronization)
            {
                ParentWorldState? parentWorldState;
                if (!acceptsPublications ||
                    !parentWorldStatesByParentWorldId.TryGetValue(
                        parentWorldId,
                        out parentWorldState) ||
                    parentWorldState.MemberWorldIds.Count == 0 ||
                    parentWorldState.CoverageCompleteGenerationValue !=
                        expectedCollectionGeneration.Value ||
                    parentWorldState.CoverageCompleteMemberSetVersion !=
                        parentWorldState.MemberSetVersion)
                {
                    return TemperatureConstrainedAmountAvailability
                        .InventoryIncomplete();
                }

                var aggregateKey = new ParentWorldResourceTagKey(
                    parentWorldId,
                    resourceTag);
                ParentWorldResourceTemperatureAggregate? aggregate;
                if (!aggregatesByParentWorldAndResourceTag.TryGetValue(
                    aggregateKey,
                    out aggregate))
                {
                    // Complete coverage plus no aggregate entry proves that every
                    // member omitted this tag and therefore contributes known zero.
                    return TemperatureConstrainedAmountAvailability.Complete(0.0f);
                }

                if (aggregate.CollectionGeneration.Value !=
                        expectedCollectionGeneration.Value ||
                    aggregate.ParentMemberSetVersion !=
                        parentWorldState.MemberSetVersion ||
                    aggregate.PendingWorldCount != 0)
                {
                    return TemperatureConstrainedAmountAvailability
                        .InventoryIncomplete();
                }

                completeTemperatureAmounts = aggregate.TemperatureAmounts;
            }

            // Binary search executes after releasing the catalog lock. The series
            // is immutable, so a concurrent replacement cannot tear this result.
            float availableAmount =
                completeTemperatureAmounts.GetAmountAllowedBy(constraint);
            return TemperatureConstrainedAmountAvailability.Complete(
                availableAmount);
        }

        internal void RemoveWorld(int worldId)
        {
            int parentWorldId;
            HashSet<Tag>? affectedResourceTags = null;
            WorldInventoryCollectionGeneration? collectionGeneration = null;
            lock (synchronization)
            {
                if (!acceptsPublications)
                {
                    return;
                }

                WorldRegistration? registration;
                if (!worldRegistrationsByWorldId.TryGetValue(
                    worldId,
                    out registration))
                {
                    return;
                }

                parentWorldId = registration.ParentWorldId;
                ParentWorldState parentWorldState =
                    parentWorldStatesByParentWorldId[parentWorldId];
                long nextMemberSetVersion = checked(
                    parentWorldState.MemberSetVersion + 1L);
                affectedResourceTags = new HashSet<Tag>();
                CollectParentResourceTagsLocked(
                    parentWorldId,
                    affectedResourceTags);

                parentWorldState.MemberWorldIds.Remove(worldId);
                parentWorldState.MemberSetVersion = nextMemberSetVersion;
                worldRegistrationsByWorldId.Remove(worldId);
                worldPublicationsByWorldId.Remove(worldId);

                if (parentWorldState.MemberWorldIds.Count == 0)
                {
                    parentWorldStatesByParentWorldId.Remove(parentWorldId);
                    RemoveParentWorldAggregatesLocked(parentWorldId);
                    return;
                }

                if (latestObservedCollectionGenerationValue > 0)
                {
                    collectionGeneration =
                        new WorldInventoryCollectionGeneration(
                            latestObservedCollectionGenerationValue);
                    UpdateParentCoverageCompletionLocked(
                        parentWorldId,
                        collectionGeneration.Value);
                    InvalidateParentResourceTagAggregatesLocked(
                        parentWorldId,
                        collectionGeneration.Value,
                        affectedResourceTags);
                }
                else
                {
                    ClearParentCoverageCompletionLocked(parentWorldState);
                }
            }

            if (collectionGeneration.HasValue &&
                affectedResourceTags != null)
            {
                RebuildAffectedParentResourceTagAggregates(
                    parentWorldId,
                    affectedResourceTags,
                    collectionGeneration.Value);
            }
        }

        internal void ClearForGameSession()
        {
            lock (synchronization)
            {
                if (!acceptsPublications)
                {
                    return;
                }

                acceptsPublications = false;
                worldRegistrationsByWorldId.Clear();
                worldPublicationsByWorldId.Clear();
                parentWorldStatesByParentWorldId.Clear();
                aggregatesByParentWorldAndResourceTag.Clear();
                latestObservedCollectionGenerationValue = 0;
            }
        }

        private void RebuildAffectedParentResourceTagAggregates(
            int parentWorldId,
            IEnumerable<Tag> affectedResourceTags,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            foreach (Tag affectedResourceTag in affectedResourceTags)
            {
                TryRebuildParentResourceTagAggregate(
                    parentWorldId,
                    affectedResourceTag,
                    collectionGeneration);
            }
        }

        private void RebuildOneParentResourceTagAggregate(
            int parentWorldId,
            Tag resourceTag,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            TryRebuildParentResourceTagAggregate(
                parentWorldId,
                resourceTag,
                collectionGeneration);
        }

        private void TryRebuildParentResourceTagAggregate(
            int parentWorldId,
            Tag resourceTag,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            AggregateRebuildCapture? capture;
            lock (synchronization)
            {
                capture = CaptureAggregateRebuildLocked(
                    parentWorldId,
                    resourceTag,
                    collectionGeneration);
            }

            if (capture == null)
            {
                return;
            }

            // A pending aggregate is never queryable, so combining its partial
            // sources would create work and allocations that must be discarded.
            TemperatureAmountSeries combinedTemperatureAmounts =
                capture.PendingWorldCount == 0
                    ? TemperatureAmountSeries.Combine(
                        capture.SourceTemperatureAmounts)
                    : TemperatureAmountSeries.Empty;

            lock (synchronization)
            {
                if (!AggregateRebuildCaptureRemainsCurrentLocked(capture))
                {
                    // Exactly one optimistic attempt belongs to this mutation. A
                    // relevant concurrent mutation invalidated this capture and
                    // owns its own attempt; never retry or spin here.
                    return;
                }

                var aggregateKey = new ParentWorldResourceTagKey(
                    parentWorldId,
                    resourceTag);
                if (capture.PendingWorldCount == 0 &&
                    capture.SourceTemperatureAmounts.Count == 0)
                {
                    aggregatesByParentWorldAndResourceTag.Remove(aggregateKey);
                    return;
                }

                aggregatesByParentWorldAndResourceTag[aggregateKey] =
                    new ParentWorldResourceTemperatureAggregate(
                        collectionGeneration,
                        capture.ParentMemberSetVersion,
                        capture.PendingWorldCount,
                        combinedTemperatureAmounts);
            }
        }

        private AggregateRebuildCapture? CaptureAggregateRebuildLocked(
            int parentWorldId,
            Tag resourceTag,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            ParentWorldState? parentWorldState;
            if (!acceptsPublications ||
                !parentWorldStatesByParentWorldId.TryGetValue(
                    parentWorldId,
                    out parentWorldState) ||
                parentWorldState.MemberWorldIds.Count == 0)
            {
                return null;
            }

            var memberEvidence = new List<MemberResourceTagEvidence>(
                parentWorldState.MemberWorldIds.Count);
            var sourceTemperatureAmounts = new List<TemperatureAmountSeries>();
            int pendingWorldCount = 0;
            foreach (int memberWorldId in parentWorldState.MemberWorldIds)
            {
                MemberResourceTagEvidence evidence =
                    CaptureMemberResourceTagEvidenceLocked(
                        memberWorldId,
                        resourceTag,
                        collectionGeneration);
                memberEvidence.Add(evidence);
                if (!evidence.HasCurrentCoverage)
                {
                    pendingWorldCount++;
                    continue;
                }

                if (!evidence.ResourceTagIsPresent)
                {
                    continue;
                }

                if (evidence.TemperatureAmounts == null)
                {
                    pendingWorldCount++;
                    continue;
                }

                sourceTemperatureAmounts.Add(evidence.TemperatureAmounts);
            }

            return new AggregateRebuildCapture(
                parentWorldId,
                resourceTag,
                collectionGeneration,
                parentWorldState.MemberSetVersion,
                memberEvidence,
                sourceTemperatureAmounts,
                pendingWorldCount);
        }

        private bool AggregateRebuildCaptureRemainsCurrentLocked(
            AggregateRebuildCapture capture)
        {
            if (!acceptsPublications)
            {
                return false;
            }

            ParentWorldState? parentWorldState;
            if (!parentWorldStatesByParentWorldId.TryGetValue(
                    capture.ParentWorldId,
                    out parentWorldState) ||
                parentWorldState.MemberSetVersion !=
                    capture.ParentMemberSetVersion ||
                parentWorldState.MemberWorldIds.Count !=
                    capture.MemberEvidence.Count)
            {
                return false;
            }

            for (int memberIndex = 0;
                 memberIndex < capture.MemberEvidence.Count;
                 memberIndex++)
            {
                MemberResourceTagEvidence expectedEvidence =
                    capture.MemberEvidence[memberIndex];
                if (!parentWorldState.MemberWorldIds.Contains(
                        expectedEvidence.WorldId) ||
                    !expectedEvidence.Equals(
                        CaptureMemberResourceTagEvidenceLocked(
                            expectedEvidence.WorldId,
                            capture.ResourceTag,
                            capture.CollectionGeneration)))
                {
                    return false;
                }
            }

            return true;
        }

        private MemberResourceTagEvidence
            CaptureMemberResourceTagEvidenceLocked(
                int worldId,
                Tag resourceTag,
                WorldInventoryCollectionGeneration collectionGeneration)
        {
            WorldPublicationState worldPublication =
                worldPublicationsByWorldId[worldId];
            bool hasCurrentCoverage =
                worldPublication.HasPublication &&
                worldPublication.CollectionGeneration.Equals(
                    collectionGeneration) &&
                worldPublication.PublicationStrength !=
                    WorldPublicationStrength.NoCoverage;
            bool resourceTagIsPresent =
                hasCurrentCoverage &&
                worldPublication.PresentResourceTags.Contains(resourceTag);
            TemperatureAmountSeries? temperatureAmounts = null;
            if (resourceTagIsPresent)
            {
                worldPublication.TemperatureAmountsByResourceTag.TryGetValue(
                    resourceTag,
                    out temperatureAmounts);
            }

            return new MemberResourceTagEvidence(
                worldId,
                hasCurrentCoverage,
                resourceTagIsPresent,
                temperatureAmounts);
        }

        private void QueueParentWorldRebuildLocked(
            int parentWorldId,
            List<ParentWorldRebuildRequest> parentWorldsToRebuild)
        {
            if (latestObservedCollectionGenerationValue <= 0 ||
                !parentWorldStatesByParentWorldId.ContainsKey(parentWorldId))
            {
                return;
            }

            for (int requestIndex = 0;
                 requestIndex < parentWorldsToRebuild.Count;
                 requestIndex++)
            {
                if (parentWorldsToRebuild[requestIndex].ParentWorldId ==
                    parentWorldId)
                {
                    return;
                }
            }

            var affectedResourceTags = new HashSet<Tag>();
            CollectParentResourceTagsLocked(
                parentWorldId,
                affectedResourceTags);
            var collectionGeneration = new WorldInventoryCollectionGeneration(
                latestObservedCollectionGenerationValue);
            UpdateParentCoverageCompletionLocked(
                parentWorldId,
                collectionGeneration);
            InvalidateParentResourceTagAggregatesLocked(
                parentWorldId,
                collectionGeneration,
                affectedResourceTags);
            parentWorldsToRebuild.Add(new ParentWorldRebuildRequest(
                parentWorldId,
                collectionGeneration,
                affectedResourceTags));
        }

        private static void RemoveQueuedParentWorldRebuild(
            int parentWorldId,
            List<ParentWorldRebuildRequest> parentWorldsToRebuild)
        {
            for (int requestIndex = parentWorldsToRebuild.Count - 1;
                 requestIndex >= 0;
                 requestIndex--)
            {
                if (parentWorldsToRebuild[requestIndex].ParentWorldId ==
                    parentWorldId)
                {
                    parentWorldsToRebuild.RemoveAt(requestIndex);
                }
            }
        }

        private void RebuildQueuedParentWorlds(
            List<ParentWorldRebuildRequest> parentWorldsToRebuild)
        {
            foreach (ParentWorldRebuildRequest rebuildRequest in
                parentWorldsToRebuild)
            {
                RebuildAffectedParentResourceTagAggregates(
                    rebuildRequest.ParentWorldId,
                    rebuildRequest.AffectedResourceTags,
                    rebuildRequest.CollectionGeneration);
            }
        }

        private void CollectParentResourceTagsLocked(
            int parentWorldId,
            HashSet<Tag> resourceTags)
        {
            ParentWorldState? parentWorldState;
            if (!parentWorldStatesByParentWorldId.TryGetValue(
                parentWorldId,
                out parentWorldState))
            {
                return;
            }

            foreach (int memberWorldId in parentWorldState.MemberWorldIds)
            {
                resourceTags.UnionWith(
                    worldPublicationsByWorldId[memberWorldId]
                        .PresentResourceTags);
            }

            foreach (KeyValuePair<
                ParentWorldResourceTagKey,
                ParentWorldResourceTemperatureAggregate> entry in
                    aggregatesByParentWorldAndResourceTag)
            {
                if (entry.Key.ParentWorldId == parentWorldId)
                {
                    resourceTags.Add(entry.Key.ResourceTag);
                }
            }
        }

        private void AddPendingParentResourceTagsLocked(
            int parentWorldId,
            HashSet<Tag> affectedResourceTags)
        {
            foreach (KeyValuePair<
                ParentWorldResourceTagKey,
                ParentWorldResourceTemperatureAggregate> entry in
                    aggregatesByParentWorldAndResourceTag)
            {
                if (entry.Key.ParentWorldId == parentWorldId &&
                    entry.Value.PendingWorldCount != 0)
                {
                    affectedResourceTags.Add(entry.Key.ResourceTag);
                }
            }
        }

        private void InvalidateParentResourceTagAggregatesLocked(
            int parentWorldId,
            WorldInventoryCollectionGeneration collectionGeneration,
            IEnumerable<Tag> affectedResourceTags)
        {
            foreach (Tag affectedResourceTag in affectedResourceTags)
            {
                InvalidateParentResourceTagAggregateLocked(
                    parentWorldId,
                    affectedResourceTag,
                    collectionGeneration);
            }
        }

        private void InvalidateParentResourceTagAggregateLocked(
            int parentWorldId,
            Tag resourceTag,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            ParentWorldState? parentWorldState;
            if (!parentWorldStatesByParentWorldId.TryGetValue(
                parentWorldId,
                out parentWorldState))
            {
                return;
            }

            aggregatesByParentWorldAndResourceTag[
                new ParentWorldResourceTagKey(parentWorldId, resourceTag)] =
                ParentWorldResourceTemperatureAggregate.Incomplete(
                    collectionGeneration,
                    parentWorldState.MemberSetVersion);
        }

        private void UpdateParentCoverageCompletionLocked(
            int parentWorldId,
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            ParentWorldState? parentWorldState;
            if (!parentWorldStatesByParentWorldId.TryGetValue(
                    parentWorldId,
                    out parentWorldState) ||
                parentWorldState.MemberWorldIds.Count == 0)
            {
                return;
            }

            foreach (int memberWorldId in parentWorldState.MemberWorldIds)
            {
                WorldPublicationState worldPublication =
                    worldPublicationsByWorldId[memberWorldId];
                if (!worldPublication.HasPublication ||
                    !worldPublication.CollectionGeneration.Equals(
                        collectionGeneration) ||
                    worldPublication.PublicationStrength ==
                        WorldPublicationStrength.NoCoverage)
                {
                    ClearParentCoverageCompletionLocked(parentWorldState);
                    return;
                }
            }

            parentWorldState.CoverageCompleteGenerationValue =
                collectionGeneration.Value;
            parentWorldState.CoverageCompleteMemberSetVersion =
                parentWorldState.MemberSetVersion;
        }

        private static void ClearParentCoverageCompletionLocked(
            ParentWorldState parentWorldState)
        {
            parentWorldState.CoverageCompleteGenerationValue = 0;
            parentWorldState.CoverageCompleteMemberSetVersion = 0;
        }

        private void ObserveCollectionGenerationLocked(
            WorldInventoryCollectionGeneration collectionGeneration)
        {
            if (collectionGeneration.Value >
                latestObservedCollectionGenerationValue)
            {
                latestObservedCollectionGenerationValue =
                    collectionGeneration.Value;
            }
        }

        private ParentWorldState GetOrCreateParentWorldStateLocked(
            int parentWorldId)
        {
            ParentWorldState? parentWorldState;
            if (!parentWorldStatesByParentWorldId.TryGetValue(
                parentWorldId,
                out parentWorldState))
            {
                parentWorldState = new ParentWorldState();
                parentWorldStatesByParentWorldId.Add(
                    parentWorldId,
                    parentWorldState);
            }

            return parentWorldState;
        }

        private void RemoveParentWorldAggregatesLocked(int parentWorldId)
        {
            var aggregateKeysToRemove =
                new List<ParentWorldResourceTagKey>();
            foreach (ParentWorldResourceTagKey aggregateKey in
                aggregatesByParentWorldAndResourceTag.Keys)
            {
                if (aggregateKey.ParentWorldId == parentWorldId)
                {
                    aggregateKeysToRemove.Add(aggregateKey);
                }
            }

            foreach (ParentWorldResourceTagKey aggregateKey in
                aggregateKeysToRemove)
            {
                aggregatesByParentWorldAndResourceTag.Remove(aggregateKey);
            }
        }

        private void ThrowIfNoLongerAcceptingRegistrations()
        {
            if (!acceptsPublications)
            {
                throw new InvalidOperationException(
                    "The world resource temperature amount catalog no longer " +
                    "accepts registrations because game-session cleanup ran.");
            }
        }

        private enum WorldPublicationStrength
        {
            NoCoverage,
            TagCoverage,
            CompleteWorld
        }

        private sealed class WorldRegistration
        {
            internal WorldRegistration(int parentWorldId)
            {
                ParentWorldId = parentWorldId;
            }

            internal int ParentWorldId { get; set; }
        }

        private sealed class WorldPublicationState
        {
            internal WorldPublicationState()
            {
                PresentResourceTags = new HashSet<Tag>();
                TemperatureAmountsByResourceTag =
                    new Dictionary<Tag, TemperatureAmountSeries>();
            }

            internal bool HasPublication { get; private set; }

            internal WorldInventoryCollectionGeneration CollectionGeneration
            {
                get;
                private set;
            }

            internal WorldPublicationStrength PublicationStrength
            {
                get;
                private set;
            }

            internal HashSet<Tag> PresentResourceTags { get; private set; }

            internal Dictionary<Tag, TemperatureAmountSeries>
                TemperatureAmountsByResourceTag
            {
                get;
                private set;
            }

            internal void ReplaceWithCompleteWorld(
                WorldInventoryCollectionGeneration collectionGeneration,
                HashSet<Tag> presentResourceTags,
                Dictionary<Tag, TemperatureAmountSeries>
                    temperatureAmountsByResourceTag)
            {
                HasPublication = true;
                CollectionGeneration = collectionGeneration;
                PublicationStrength = WorldPublicationStrength.CompleteWorld;
                PresentResourceTags = presentResourceTags;
                TemperatureAmountsByResourceTag =
                    temperatureAmountsByResourceTag;
            }

            internal void ReplaceWithTagCoverage(
                WorldInventoryCollectionGeneration collectionGeneration,
                HashSet<Tag> presentResourceTags)
            {
                var retainedTemperatureAmountsByResourceTag =
                    new Dictionary<Tag, TemperatureAmountSeries>();
                if (HasPublication &&
                    CollectionGeneration.Equals(collectionGeneration))
                {
                    foreach (KeyValuePair<Tag, TemperatureAmountSeries> entry in
                        TemperatureAmountsByResourceTag)
                    {
                        if (presentResourceTags.Contains(entry.Key))
                        {
                            retainedTemperatureAmountsByResourceTag.Add(
                                entry.Key,
                                entry.Value);
                        }
                    }
                }

                HasPublication = true;
                CollectionGeneration = collectionGeneration;
                PublicationStrength = WorldPublicationStrength.TagCoverage;
                PresentResourceTags = presentResourceTags;
                TemperatureAmountsByResourceTag =
                    retainedTemperatureAmountsByResourceTag;
            }

            internal void PublishResourceTagTemperatureAmounts(
                Tag resourceTag,
                TemperatureAmountSeries temperatureAmounts)
            {
                PresentResourceTags.Add(resourceTag);
                TemperatureAmountsByResourceTag[resourceTag] =
                    temperatureAmounts;
            }
        }

        private sealed class ParentWorldState
        {
            internal ParentWorldState()
            {
                MemberWorldIds = new HashSet<int>();
            }

            internal HashSet<int> MemberWorldIds { get; }

            internal long MemberSetVersion { get; set; }

            internal long CoverageCompleteGenerationValue { get; set; }

            internal long CoverageCompleteMemberSetVersion { get; set; }
        }

        private readonly struct ParentWorldResourceTagKey :
            IEquatable<ParentWorldResourceTagKey>
        {
            private readonly int parentWorldId;
            private readonly Tag resourceTag;

            internal ParentWorldResourceTagKey(
                int parentWorldId,
                Tag resourceTag)
            {
                this.parentWorldId = parentWorldId;
                this.resourceTag = resourceTag;
            }

            internal int ParentWorldId => parentWorldId;

            internal Tag ResourceTag => resourceTag;

            public bool Equals(ParentWorldResourceTagKey other) =>
                parentWorldId == other.parentWorldId &&
                resourceTag.Equals(other.resourceTag);

            public override bool Equals(object? obj) =>
                obj is ParentWorldResourceTagKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (parentWorldId * 397) ^ resourceTag.GetHashCode();
                }
            }
        }

        private sealed class ParentWorldResourceTemperatureAggregate
        {
            internal ParentWorldResourceTemperatureAggregate(
                WorldInventoryCollectionGeneration collectionGeneration,
                long parentMemberSetVersion,
                int pendingWorldCount,
                TemperatureAmountSeries temperatureAmounts)
            {
                CollectionGeneration = collectionGeneration;
                ParentMemberSetVersion = parentMemberSetVersion;
                PendingWorldCount = pendingWorldCount;
                TemperatureAmounts = temperatureAmounts;
            }

            internal WorldInventoryCollectionGeneration CollectionGeneration
            {
                get;
            }

            internal long ParentMemberSetVersion { get; }

            internal int PendingWorldCount { get; }

            internal TemperatureAmountSeries TemperatureAmounts { get; }

            internal static ParentWorldResourceTemperatureAggregate Incomplete(
                WorldInventoryCollectionGeneration collectionGeneration,
                long parentMemberSetVersion) =>
                new ParentWorldResourceTemperatureAggregate(
                    collectionGeneration,
                    parentMemberSetVersion,
                    pendingWorldCount: 1,
                    TemperatureAmountSeries.Empty);
        }

        private sealed class MemberResourceTagEvidence :
            IEquatable<MemberResourceTagEvidence>
        {
            internal MemberResourceTagEvidence(
                int worldId,
                bool hasCurrentCoverage,
                bool resourceTagIsPresent,
                TemperatureAmountSeries? temperatureAmounts)
            {
                WorldId = worldId;
                HasCurrentCoverage = hasCurrentCoverage;
                ResourceTagIsPresent = resourceTagIsPresent;
                TemperatureAmounts = temperatureAmounts;
            }

            internal int WorldId { get; }

            internal bool HasCurrentCoverage { get; }

            internal bool ResourceTagIsPresent { get; }

            internal TemperatureAmountSeries? TemperatureAmounts { get; }

            public bool Equals(MemberResourceTagEvidence? other) =>
                other != null &&
                WorldId == other.WorldId &&
                HasCurrentCoverage == other.HasCurrentCoverage &&
                ResourceTagIsPresent == other.ResourceTagIsPresent &&
                ReferenceEquals(TemperatureAmounts, other.TemperatureAmounts);

            public override bool Equals(object? obj) =>
                Equals(obj as MemberResourceTagEvidence);

            public override int GetHashCode() => WorldId;
        }

        private sealed class AggregateRebuildCapture
        {
            internal AggregateRebuildCapture(
                int parentWorldId,
                Tag resourceTag,
                WorldInventoryCollectionGeneration collectionGeneration,
                long parentMemberSetVersion,
                List<MemberResourceTagEvidence> memberEvidence,
                List<TemperatureAmountSeries> sourceTemperatureAmounts,
                int pendingWorldCount)
            {
                ParentWorldId = parentWorldId;
                ResourceTag = resourceTag;
                CollectionGeneration = collectionGeneration;
                ParentMemberSetVersion = parentMemberSetVersion;
                MemberEvidence = memberEvidence;
                SourceTemperatureAmounts = sourceTemperatureAmounts;
                PendingWorldCount = pendingWorldCount;
            }

            internal int ParentWorldId { get; }

            internal Tag ResourceTag { get; }

            internal WorldInventoryCollectionGeneration CollectionGeneration
            {
                get;
            }

            internal long ParentMemberSetVersion { get; }

            internal List<MemberResourceTagEvidence> MemberEvidence { get; }

            internal List<TemperatureAmountSeries> SourceTemperatureAmounts
            {
                get;
            }

            internal int PendingWorldCount { get; }
        }

        private sealed class ParentWorldRebuildRequest
        {
            internal ParentWorldRebuildRequest(
                int parentWorldId,
                WorldInventoryCollectionGeneration collectionGeneration,
                HashSet<Tag> affectedResourceTags)
            {
                ParentWorldId = parentWorldId;
                CollectionGeneration = collectionGeneration;
                AffectedResourceTags = affectedResourceTags;
            }

            internal int ParentWorldId { get; }

            internal WorldInventoryCollectionGeneration CollectionGeneration
            {
                get;
            }

            internal HashSet<Tag> AffectedResourceTags { get; }
        }
    }
}
