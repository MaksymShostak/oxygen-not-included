using System.Collections;
using System.Reflection;
using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
public sealed class FetchTemperatureEligibilityBuilderTests
{
    private const int CombinedFetchEligibilitySeed = 0xFE7C4;
    private const int RandomizedTopologyCount = 2000;

    [Test]
    public async Task AddFetchRequest_WhenTagHasConstrainedAndUnconstrainedDestinations_PreservesDistinctStorageAndPickupFacts()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron],
            enabledConstraint: Constraint(10, 20));
        builder.AddUnconstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron]);

        var snapshot = builder.Build();

        await Assert.That(snapshot.TryGetStorageEligibility(1, iron, out var storage)).IsTrue();
        await Assert.That(storage.AllowsEveryTemperature).IsTrue();
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(1, [iron]).ToArray()).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task AddUnconstrainedFetchRequest_WhenTagsAreRequested_ContributesUnconstrainedStorageAndNoEndpoints()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddUnconstrainedFetchRequest(1, [iron]);

        var snapshot = builder.Build();

        await Assert.That(snapshot.TryGetStorageEligibility(1, iron, out var storage)).IsTrue();
        await Assert.That(storage.AllowsEveryTemperature).IsTrue();
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEmpty();
    }

    [Test]
    public async Task AddTemperatureConstrainedFetchRequest_WhenConstraintIsDisabled_ThrowsArgumentException()
    {
        var builder = BeginBuilder();

        var exception = (await Assert.That(() =>
            builder.AddTemperatureConstrainedFetchRequest(
                parentWorldId: 1,
                requestedTags: [new Tag("Iron")],
                enabledConstraint: Constraint(10, 0))).ThrowsExactly<ArgumentException>())!;

        await Assert.That(exception.Message).Contains("enabledConstraint");
    }

    [Test]
    public async Task AddTemperatureConstrainedFetchRequest_WhenEnabledConstraintIsEmpty_ContributesAllowsNoneAndNoEndpoints()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron],
            enabledConstraint: Constraint(20, 10));

        var snapshot = builder.Build();

        await Assert.That(snapshot.TryGetStorageEligibility(1, iron, out var storage)).IsTrue();
        await Assert.That(storage.AllowsNoTemperature).IsTrue();
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEmpty();
    }

    [Test]
    public async Task AddTemperatureConstrainedFetchRequest_WhenEnabledConstraintIsNonEmpty_ContributesIntervalAndBothEndpoints()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron],
            enabledConstraint: Constraint(10, 20));

        var snapshot = builder.Build();

        await Assert.That(snapshot.TryGetStorageEligibility(1, iron, out var storage)).IsTrue();
        await Assert.That(storage.Intervals).IsEquivalentTo(new[] { new AllowedTemperatureInterval(10, 20) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task AddTemperatureConstrainedFetchRequest_WhenTagsRepeat_DeduplicatesPerRequest()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron, iron, iron],
            enabledConstraint: Constraint(10, 20));

        var snapshot = builder.Build();

        await Assert.That(snapshot.GetRequestedTags(1)).IsEquivalentTo(new[] { iron }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(snapshot.TryGetStorageEligibility(1, iron, out var storage)).IsTrue();
        await Assert.That(storage.Intervals).IsEquivalentTo(new[] { new AllowedTemperatureInterval(10, 20) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task AddTemperatureConstrainedFetchRequest_WhenSameTagExistsInDifferentParents_DoesNotCrossContaminate()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(1, [iron], Constraint(10, 20));
        builder.AddTemperatureConstrainedFetchRequest(2, [iron], Constraint(30, 40));

        var snapshot = builder.Build();

        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(2, [iron])).IsEquivalentTo(new[] { 30, 40 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Build_WhenNoFetchRequests_PublishesCompleteEmptySnapshot()
    {
        var gameSessionGeneration = new GameSessionGeneration(7);
        var constraintSnapshot = ConstraintSnapshot(generationValue: 11);
        var fetchTopologyVersion = new FetchRequestTopologyVersion(13);
        var worldTopology = WorldTopology(gameSessionGeneration, versionValue: 17);
        var builder = new FetchTemperatureEligibilityBuilder();
        builder.Begin(
            gameSessionGeneration,
            constraintSnapshot,
            fetchTopologyVersion,
            worldTopology);

        var snapshot = builder.Build();

        await Assert.That(snapshot.GameSessionGeneration).IsEqualTo(gameSessionGeneration);
        await Assert.That(snapshot.ConstraintGeneration).IsEqualTo(constraintSnapshot.Generation);
        await Assert.That(snapshot.FetchTopologyVersion).IsEqualTo(fetchTopologyVersion);
        await Assert.That(snapshot.WorldTopologyVersion).IsEqualTo(worldTopology.Version);
        await Assert.That(snapshot.TryGetStorageEligibility(
            parentWorldId: 1,
            requestedTag: new Tag("Iron"),
            out _)).IsFalse();
        await Assert.That(snapshot.GetRequestedTags(1)).IsEmpty();
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(
            parentWorldId: 1,
            applicableRequestedTags: [new Tag("Iron")])).IsEmpty();
    }

    [Test]
    public async Task Build_WhenCalledBeforeBegin_ThrowsInvalidOperationException()
    {
        var builder = new FetchTemperatureEligibilityBuilder();

        await Assert.That(() => builder.Build()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var builder = BeginBuilder();
        builder.Build();

        await Assert.That(() => builder.Build()).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Discard_WhenEnumerationThrows_DropsAllCandidateReferences()
    {
        var builder = BeginBuilder();
        var throwingTags = new ThrowingRequestedTagList(new Tag("Iron"));

        await Assert.That(() =>
            builder.AddUnconstrainedFetchRequest(1, throwingTags)).ThrowsExactly<InvalidOperationException>();
        builder.Discard();

        await Assert.That(((System.Collections.IEnumerable)(await GetDestinationRequirementDictionary(builder))).Cast<object?>()).IsEmpty();
        BeginBuilder(builder);
        var emptySnapshot = builder.Build();
        await Assert.That(emptySnapshot.GetRequestedTags(1)).IsEmpty();
    }

    [Test]
    public async Task Discard_WhenBuiltCandidatePublicationIsRejected_AllowsNextAuthoritativeTraversal()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron],
            enabledConstraint: Constraint(10, 20));
        var rejectedCandidate = builder.Build();

        // The adapter cannot know whether publication will succeed until after the
        // candidate has been built. Its finalizer therefore performs defensive
        // cleanup even on this already-completed builder state.
        builder.Discard();
        BeginBuilder(builder);
        builder.AddUnconstrainedFetchRequest(2, [iron]);
        var nextCandidate = builder.Build();

        await Assert.That(rejectedCandidate.CreateSortedDecisionEndpointUnion(1, [iron])).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(nextCandidate.TryGetStorageEligibility(
            parentWorldId: 2,
            requestedTag: iron,
            out var nextEligibility)).IsTrue();
        await Assert.That(nextEligibility.Allows(
            TemperatureDecisionBucket.FromTemperature(10_000.0f))).IsTrue();
    }

    [Test]
    public async Task Builder_WhenPriorEntryCountExceedsHighWater_ReplacesMutableMaps()
    {
        var builder = new FetchTemperatureEligibilityBuilder();
        var retentionLimit =
            RetainedCollectionCapacityLimits
                .MaximumRetainedFetchEligibilityEntryCount;

        BeginBuilder(builder);
        var dictionaryAtLimit = await GetDestinationRequirementDictionary(builder);
        AddUniqueUnconstrainedTags(builder, retentionLimit, tagIdentityOffset: 0);
        var atLimitSnapshot = builder.Build();
        await AssertEveryGeneratedTagExists(
            atLimitSnapshot,
            retentionLimit,
            tagIdentityOffset: 0);
        await Assert.That((await GetDestinationRequirementDictionary(builder))).IsSameReferenceAs(dictionaryAtLimit);

        BeginBuilder(builder);
        var dictionaryBeforeLimitExceeded =
            await GetDestinationRequirementDictionary(builder);
        AddUniqueUnconstrainedTags(
            builder,
            retentionLimit + 1,
            tagIdentityOffset: retentionLimit);
        var limitExceededSnapshot = builder.Build();
        await AssertEveryGeneratedTagExists(
            limitExceededSnapshot,
            retentionLimit + 1,
            tagIdentityOffset: retentionLimit);
        await Assert.That((await GetDestinationRequirementDictionary(builder))).IsNotSameReferenceAs(dictionaryBeforeLimitExceeded);

        BeginBuilder(builder);
        var dictionaryBeforeLargerWorkload =
            await GetDestinationRequirementDictionary(builder);
        var largerEntryCount = (retentionLimit * 2) + 17;
        AddUniqueUnconstrainedTags(
            builder,
            largerEntryCount,
            tagIdentityOffset: retentionLimit * 3);
        var largerSnapshot = builder.Build();
        await AssertEveryGeneratedTagExists(
            largerSnapshot,
            largerEntryCount,
            tagIdentityOffset: retentionLimit * 3);
        await Assert.That((await GetDestinationRequirementDictionary(builder))).IsNotSameReferenceAs(dictionaryBeforeLargerWorkload);
    }

    [Test]
    public async Task Snapshot_WhenBuilderIsReused_RemainsImmutable()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(1, [iron], Constraint(10, 20));
        var firstSnapshot = builder.Build();

        BeginBuilder(builder);
        builder.AddTemperatureConstrainedFetchRequest(1, [iron], Constraint(30, 40));
        var secondSnapshot = builder.Build();

        await Assert.That(firstSnapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(secondSnapshot.CreateSortedDecisionEndpointUnion(1, [iron])).IsEquivalentTo(new[] { 30, 40 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CreateSortedDecisionEndpointUnion_WhenPickupMatchesSeveralTags_ReturnsEveryApplicableEndpointOnce()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        var metal = new Tag("Metal");
        builder.AddTemperatureConstrainedFetchRequest(
            1,
            [iron],
            Constraint(10, 30));
        builder.AddTemperatureConstrainedFetchRequest(
            1,
            [metal],
            Constraint(20, 30));
        builder.AddTemperatureConstrainedFetchRequest(
            1,
            [iron, metal],
            Constraint(40, 50));
        var snapshot = builder.Build();

        var endpoints = snapshot.CreateSortedDecisionEndpointUnion(
            1,
            [metal, iron, metal]);

        await Assert.That(endpoints).IsEquivalentTo(new[] { 10, 20, 30, 40, 50 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(endpoints is int[]).IsFalse();
        await Assert.That(endpoints is ICollection<int> mutable && !mutable.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task CreateSortedDecisionEndpointUnion_WhenPickupMatchesNoRequestedTag_ReturnsEmptySequence()
    {
        var builder = BeginBuilder();
        builder.AddTemperatureConstrainedFetchRequest(
            1,
            [new Tag("Iron")],
            Constraint(10, 20));
        var snapshot = builder.Build();

        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(
            1,
            [new Tag("Copper")])).IsEmpty();
        await Assert.That(snapshot.CreateSortedDecisionEndpointUnion(
            99,
            [new Tag("Iron")])).IsEmpty();
    }

    [Test]
    public async Task GetRequestedTags_WhenParentContainsSeveralRequests_ReturnsImmutableFirstEncounterOrder()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var metal = new Tag("Metal");
        builder.AddUnconstrainedFetchRequest(1, [iron, copper, iron]);
        builder.AddTemperatureConstrainedFetchRequest(
            1,
            [metal, copper],
            Constraint(10, 20));
        var snapshot = builder.Build();

        var requestedTags = snapshot.GetRequestedTags(1);

        await Assert.That(requestedTags).IsEquivalentTo(new[] { iron, copper, metal }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(requestedTags is Tag[]).IsFalse();
        await Assert.That(requestedTags is ICollection<Tag> mutable && !mutable.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task GetRequestedTags_WhenParentIsUnknown_ReturnsEmptyImmutableSequence()
    {
        var snapshot = BeginBuilder().Build();

        var first = snapshot.GetRequestedTags(99);
        var second = snapshot.GetRequestedTags(100);

        await Assert.That(first).IsEmpty();
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(first is Tag[]).IsFalse();
    }

    [Test]
    public async Task Snapshot_WhenRepresentativeTopologiesCoverEveryDecisionBucket_MatchesReferenceModel()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var metal = new Tag("Metal");
        var representativeTopologies = new IReadOnlyList<ReferenceFetchTemperatureRequest>[]
        {
            Array.Empty<ReferenceFetchTemperatureRequest>(),
            new[]
            {
                ReferenceFetchTemperatureRequest.Unconstrained(1, [iron])
            },
            new[]
            {
                ReferenceFetchTemperatureRequest.TemperatureConstrained(
                    1,
                    [iron],
                    Constraint(10, 20))
            },
            new[]
            {
                ReferenceFetchTemperatureRequest.TemperatureConstrained(
                    1,
                    [iron],
                    Constraint(0, 10000)),
                ReferenceFetchTemperatureRequest.TemperatureConstrained(
                    2,
                    [iron],
                    Constraint(30, 40))
            },
            new[]
            {
                ReferenceFetchTemperatureRequest.Unconstrained(1, [iron]),
                ReferenceFetchTemperatureRequest.TemperatureConstrained(
                    1,
                    [iron, metal],
                    Constraint(10, 20)),
                ReferenceFetchTemperatureRequest.TemperatureConstrained(
                    1,
                    [copper, metal],
                    Constraint(30, 40))
            }
        };
        var everyBucketOrdinal = Enumerable.Range(
            TemperatureDecisionBucket.BelowMinimumKelvinOrdinal,
            TemperatureDecisionBucket.BucketCount).ToArray();

        for (var topologyIndex = 0;
             topologyIndex < representativeTopologies.Length;
             topologyIndex++)
        {
            await AssertSnapshotMatchesReference(
                representativeTopologies[topologyIndex],
                everyBucketOrdinal,
                "representative topology=" + topologyIndex);
        }
    }

    [Test]
    public async Task Snapshot_WhenTwoThousandSeededTopologiesAreSampled_MatchesIndependentReferenceModel()
    {
        var random = new Random(CombinedFetchEligibilitySeed);

        for (var topologyIndex = 0;
             topologyIndex < RandomizedTopologyCount;
             topologyIndex++)
        {
            var requests = CreateRandomTopology(random);
            var selectedBucketOrdinals =
                CreateSelectedBucketOrdinals(random, requests);

            await AssertSnapshotMatchesReference(
                requests,
                selectedBucketOrdinals,
                "Seed=0x" + CombinedFetchEligibilitySeed.ToString("X") +
                "; topology=" + topologyIndex);
        }
    }

    private static async Task AssertSnapshotMatchesReference(
        IReadOnlyList<ReferenceFetchTemperatureRequest> requests,
        IReadOnlyList<int> selectedBucketOrdinals,
        string assertionContext)
    {
        var snapshot = BuildSnapshot(requests);
        var parentWorldIds = requests
            .Select(request => request.ParentWorldId)
            .Distinct()
            .ToArray();

        foreach (var parentWorldId in parentWorldIds)
        {
            var expectedRequestedTags =
                ReferenceTemperatureEligibilityModel
                    .GetRequestedTagsInFirstEncounterOrder(
                        requests,
                        parentWorldId);
            await Assert.That(snapshot.GetRequestedTags(parentWorldId)).IsEquivalentTo(expectedRequestedTags, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because(assertionContext + "; parent=" + parentWorldId + ".");

            foreach (var requestedTag in expectedRequestedTags)
            {
                var logicalDestinationConstraints =
                    ReferenceTemperatureEligibilityModel
                        .GetStorageDestinationConstraints(
                            requests,
                            parentWorldId,
                            requestedTag);
                await Assert.That(logicalDestinationConstraints).IsNotEmpty().Because(assertionContext);
                await Assert.That(snapshot.TryGetStorageEligibility(
                        parentWorldId,
                        requestedTag,
                        out var storageEligibility)).IsTrue().Because(assertionContext + "; missing storage entry.");

                foreach (var bucketOrdinal in selectedBucketOrdinals)
                {
                    var temperatureKelvin =
                        RepresentativeTemperatureKelvin(bucketOrdinal);
                    var expectedAllows =
                        ReferenceTemperatureEligibilityModel
                            .AnyDestinationAllowsTemperature(
                                logicalDestinationConstraints,
                                temperatureKelvin);
                    var observedAllows = storageEligibility.Allows(
                        BucketFromOrdinal(bucketOrdinal));
                    await Assert.That(observedAllows).IsEqualTo(expectedAllows).Because(assertionContext + "; parent=" + parentWorldId +
                        "; bucket=" + bucketOrdinal + ".");
                }

                await AssertEndpointUnionMatchesReference(
                    snapshot,
                    requests,
                    parentWorldId,
                    [requestedTag],
                    selectedBucketOrdinals,
                    assertionContext);
            }

            // A pickup can satisfy several requested tags. The union must contain
            // only endpoints from those explicit matches, once each, regardless of
            // other parents or other tags retained by the same snapshot.
            await AssertEndpointUnionMatchesReference(
                snapshot,
                requests,
                parentWorldId,
                expectedRequestedTags,
                selectedBucketOrdinals,
                assertionContext);
        }

        await Assert.That(snapshot.TryGetStorageEligibility(
            int.MaxValue,
            new Tag("Unknown"),
            out _)).IsFalse();
        await Assert.That(snapshot.GetRequestedTags(int.MaxValue)).IsEmpty();
    }

    private static async Task AssertEndpointUnionMatchesReference(
        FetchTemperatureEligibilitySnapshot snapshot,
        IReadOnlyList<ReferenceFetchTemperatureRequest> requests,
        int parentWorldId,
        IReadOnlyList<Tag> applicableRequestedTags,
        IReadOnlyList<int> selectedBucketOrdinals,
        string assertionContext)
    {
        var expectedEndpoints =
            ReferenceTemperatureEligibilityModel
                .CreateSortedPickupDecisionEndpointUnion(
                    requests,
                    parentWorldId,
                    applicableRequestedTags);
        var observedEndpoints = snapshot.CreateSortedDecisionEndpointUnion(
            parentWorldId,
            applicableRequestedTags);
        await Assert.That(observedEndpoints).IsEquivalentTo(expectedEndpoints, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because(assertionContext + "; endpoint union mismatch for parent=" +
            parentWorldId + ".");

        if (expectedEndpoints.Length == 0)
        {
            return;
        }

        var applicableConstraints =
            ReferenceTemperatureEligibilityModel
                .GetApplicablePickupTemperatureConstraints(
                    requests,
                    parentWorldId,
                    applicableRequestedTags);
        var definition = TemperaturePartitionDefinition.Create(
            definitionId: 1,
            observedEndpoints);
        var firstVectorByIntervalOrdinal = new Dictionary<int, bool[]>();
        foreach (var bucketOrdinal in selectedBucketOrdinals)
        {
            var temperatureKelvin =
                RepresentativeTemperatureKelvin(bucketOrdinal);
            var referenceVector =
                ReferenceTemperatureEligibilityModel
                    .EvaluateDestinationConstraintAllowances(
                        applicableConstraints,
                        temperatureKelvin);
            var intervalOrdinal = definition.Classify(
                BucketFromOrdinal(bucketOrdinal));
            if (firstVectorByIntervalOrdinal.TryGetValue(
                    intervalOrdinal,
                    out var firstVector))
            {
                await Assert.That(VectorsAreEqual(firstVector, referenceVector)).IsTrue().Because(assertionContext + "; partition merged distinct vectors for " +
                    "parent=" + parentWorldId + "; bucket=" + bucketOrdinal + ".");
            }
            else
            {
                firstVectorByIntervalOrdinal.Add(intervalOrdinal, referenceVector);
            }
        }
    }

    private static FetchTemperatureEligibilitySnapshot BuildSnapshot(
        IReadOnlyList<ReferenceFetchTemperatureRequest> requests)
    {
        var builder = BeginBuilder();
        foreach (var request in requests)
        {
            if (request.HasEnabledTemperatureConstraint)
            {
                builder.AddTemperatureConstrainedFetchRequest(
                    request.ParentWorldId,
                    request.RequestedTags,
                    request.EnabledTemperatureConstraint);
            }
            else
            {
                builder.AddUnconstrainedFetchRequest(
                    request.ParentWorldId,
                    request.RequestedTags);
            }
        }

        return builder.Build();
    }

    private static ReferenceFetchTemperatureRequest[] CreateRandomTopology(
        Random random)
    {
        var parentWorldCount = random.Next(1, 9);
        var tagCount = random.Next(1, 33);
        var requestCount = random.Next(257);
        var tags = Enumerable.Range(0, tagCount)
            .Select(tagIndex => new Tag("RandomTag-" + tagIndex))
            .ToArray();
        var requests = new ReferenceFetchTemperatureRequest[requestCount];

        for (var requestIndex = 0;
             requestIndex < requestCount;
             requestIndex++)
        {
            var parentWorldId = random.Next(parentWorldCount);
            var requestedTagOccurrenceCount = random.Next(
                1,
                Math.Min(4, tagCount) + 1);
            var requestedTags = new Tag[requestedTagOccurrenceCount];
            for (var tagOccurrenceIndex = 0;
                 tagOccurrenceIndex < requestedTags.Length;
                 tagOccurrenceIndex++)
            {
                requestedTags[tagOccurrenceIndex] =
                    tags[random.Next(tags.Length)];
            }

            if (random.Next(4) == 0)
            {
                requests[requestIndex] =
                    ReferenceFetchTemperatureRequest.Unconstrained(
                        parentWorldId,
                        requestedTags);
            }
            else
            {
                var minimumInclusiveKelvin = random.Next(
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1);
                var maximumExclusiveKelvin = random.Next(
                    1,
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1);
                requests[requestIndex] =
                    ReferenceFetchTemperatureRequest.TemperatureConstrained(
                        parentWorldId,
                        requestedTags,
                        Constraint(
                            minimumInclusiveKelvin,
                            maximumExclusiveKelvin));
            }
        }

        return requests;
    }

    private static int[] CreateSelectedBucketOrdinals(
        Random random,
        IReadOnlyList<ReferenceFetchTemperatureRequest> requests)
    {
        var selectedBucketOrdinals = new HashSet<int>
        {
            TemperatureDecisionBucket.BelowMinimumKelvinOrdinal,
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal
        };
        foreach (var request in requests)
        {
            if (!request.HasEnabledTemperatureConstraint ||
                request.EnabledTemperatureConstraint.IsEmpty)
            {
                continue;
            }

            AddEndpointAdjacentBucketOrdinals(
                selectedBucketOrdinals,
                request.EnabledTemperatureConstraint.MinimumInclusiveKelvin);
            AddEndpointAdjacentBucketOrdinals(
                selectedBucketOrdinals,
                request.EnabledTemperatureConstraint.MaximumExclusiveKelvin);
        }

        for (var sampleIndex = 0; sampleIndex < 64; sampleIndex++)
        {
            selectedBucketOrdinals.Add(
                random.Next(TemperatureDecisionBucket.BucketCount));
        }

        return selectedBucketOrdinals.OrderBy(ordinal => ordinal).ToArray();
    }

    private static void AddEndpointAdjacentBucketOrdinals(
        ISet<int> selectedBucketOrdinals,
        int endpointKelvin)
    {
        var endpointBucketOrdinal =
            endpointKelvin >= OniStorableTemperatureBounds.MaximumTemperatureKelvin
                ? TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal
                : TemperatureDecisionBucket.FirstIntegerKelvinOrdinal +
                    endpointKelvin;
        for (var offset = -1; offset <= 1; offset++)
        {
            var candidateOrdinal = endpointBucketOrdinal + offset;
            if (candidateOrdinal >=
                    TemperatureDecisionBucket.BelowMinimumKelvinOrdinal &&
                candidateOrdinal <=
                    TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
            {
                selectedBucketOrdinals.Add(candidateOrdinal);
            }
        }
    }

    private static void AddUniqueUnconstrainedTags(
        FetchTemperatureEligibilityBuilder builder,
        int tagCount,
        int tagIdentityOffset)
    {
        for (var tagIndex = 0; tagIndex < tagCount; tagIndex++)
        {
            builder.AddUnconstrainedFetchRequest(
                parentWorldId: 1,
                requestedTags:
                [new Tag("RetainedTag-" + (tagIdentityOffset + tagIndex))]);
        }
    }

    private static async Task AssertEveryGeneratedTagExists(
        FetchTemperatureEligibilitySnapshot snapshot,
        int tagCount,
        int tagIdentityOffset)
    {
        await Assert.That(snapshot.GetRequestedTags(1).Count).IsEqualTo(tagCount);
        for (var tagIndex = 0; tagIndex < tagCount; tagIndex++)
        {
            await Assert.That(snapshot.TryGetStorageEligibility(
                parentWorldId: 1,
                requestedTag: new Tag(
                    "RetainedTag-" + (tagIdentityOffset + tagIndex)),
                out var eligibility)).IsTrue();
            await Assert.That(eligibility.AllowsEveryTemperature).IsTrue();
        }
    }

    private static async Task<IDictionary> GetDestinationRequirementDictionary(
        FetchTemperatureEligibilityBuilder builder)
    {
        var dictionaryField = typeof(FetchTemperatureEligibilityBuilder).GetField(
            "destinationRequirementsByParentWorldAndRequestedTag",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (dictionaryField is null) throw new TUnit.Assertions.Exceptions.AssertionException("The representation contract requires the exact private dictionary " +
            "FetchTemperatureEligibilityBuilder." +
            "destinationRequirementsByParentWorldAndRequestedTag.");
        return ((await Assert.That(dictionaryField.GetValue(builder)).IsAssignableTo<IDictionary>())!);
    }

    private static bool VectorsAreEqual(
        IReadOnlyList<bool> first,
        IReadOnlyList<bool> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private static FetchTemperatureEligibilityBuilder BeginBuilder()
    {
        var builder = new FetchTemperatureEligibilityBuilder();
        BeginBuilder(builder);
        return builder;
    }

    private static void BeginBuilder(FetchTemperatureEligibilityBuilder builder)
    {
        var gameSessionGeneration = new GameSessionGeneration(1);
        builder.Begin(
            gameSessionGeneration,
            ConstraintSnapshot(generationValue: 2),
            new FetchRequestTopologyVersion(3),
            WorldTopology(gameSessionGeneration, versionValue: 4));
    }

    private static ActiveTemperatureConstraintSnapshot ConstraintSnapshot(
        long generationValue) =>
        new ActiveTemperatureConstraintSnapshot(
            new TemperatureConstraintGeneration(generationValue),
            enabledConstraintCount: 0,
            enabledNonEmptyConstraintCount: 0,
            Array.AsReadOnly(Array.Empty<int>()));

    private static WorldParentTopologySnapshot WorldTopology(
        GameSessionGeneration gameSessionGeneration,
        long versionValue) =>
        new WorldParentTopologySnapshot(
            gameSessionGeneration,
            new WorldParentTopologyVersion(versionValue),
            new Dictionary<int, int>());

    private static TemperatureDecisionBucket BucketFromOrdinal(int bucketOrdinal)
    {
        if (bucketOrdinal == TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)
        {
            return TemperatureDecisionBucket.FromIntegerKelvin(-1);
        }

        if (bucketOrdinal ==
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
        {
            return TemperatureDecisionBucket.FromIntegerKelvin(
                OniStorableTemperatureBounds.MaximumTemperatureKelvin);
        }

        return TemperatureDecisionBucket.FromIntegerKelvin(
            bucketOrdinal - TemperatureDecisionBucket.FirstIntegerKelvinOrdinal);
    }

    private static int RepresentativeTemperatureKelvin(int bucketOrdinal)
    {
        if (bucketOrdinal == TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)
        {
            return -1;
        }

        if (bucketOrdinal ==
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
        {
            return OniStorableTemperatureBounds.MaximumTemperatureKelvin;
        }

        return bucketOrdinal - TemperatureDecisionBucket.FirstIntegerKelvinOrdinal;
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private sealed class ThrowingRequestedTagList : IReadOnlyList<Tag>
    {
        private readonly Tag firstTag;

        internal ThrowingRequestedTagList(Tag firstTag)
        {
            this.firstTag = firstTag;
        }

        public int Count => 2;

        public Tag this[int index] => index == 0
            ? firstTag
            : throw new InvalidOperationException(
                "Deterministic requested-tag enumeration failure.");

        public IEnumerator<Tag> GetEnumerator()
        {
            yield return firstTag;
            throw new InvalidOperationException(
                "Deterministic requested-tag enumeration failure.");
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
