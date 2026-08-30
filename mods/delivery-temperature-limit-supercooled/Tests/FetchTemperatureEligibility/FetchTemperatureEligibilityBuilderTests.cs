using System.Collections;
using System.Reflection;
using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class FetchTemperatureEligibilityBuilderTests
{
    private const int CombinedFetchEligibilitySeed = 0xFE7C4;
    private const int RandomizedTopologyCount = 2000;

    [TestMethod]
    public void AddFetchRequest_WhenTagHasConstrainedAndUnconstrainedDestinations_PreservesDistinctStorageAndPickupFacts()
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

        Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
        Assert.IsTrue(storage.AllowsEveryTemperature);
        Assert.AreSequenceEqual(
            new[] { 10, 20 },
            snapshot.CreateSortedDecisionEndpointUnion(1, [iron]).ToArray());
    }

    [TestMethod]
    public void AddUnconstrainedFetchRequest_WhenTagsAreRequested_ContributesUnconstrainedStorageAndNoEndpoints()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddUnconstrainedFetchRequest(1, [iron]);

        var snapshot = builder.Build();

        Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
        Assert.IsTrue(storage.AllowsEveryTemperature);
        Assert.IsEmpty(snapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
    }

    [TestMethod]
    public void AddTemperatureConstrainedFetchRequest_WhenConstraintIsDisabled_ThrowsArgumentException()
    {
        var builder = BeginBuilder();

        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            builder.AddTemperatureConstrainedFetchRequest(
                parentWorldId: 1,
                requestedTags: [new Tag("Iron")],
                enabledConstraint: Constraint(10, 0)));

        StringAssert.Contains(exception.Message, "enabledConstraint");
    }

    [TestMethod]
    public void AddTemperatureConstrainedFetchRequest_WhenEnabledConstraintIsEmpty_ContributesAllowsNoneAndNoEndpoints()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron],
            enabledConstraint: Constraint(20, 10));

        var snapshot = builder.Build();

        Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
        Assert.IsTrue(storage.AllowsNoTemperature);
        Assert.IsEmpty(snapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
    }

    [TestMethod]
    public void AddTemperatureConstrainedFetchRequest_WhenEnabledConstraintIsNonEmpty_ContributesIntervalAndBothEndpoints()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron],
            enabledConstraint: Constraint(10, 20));

        var snapshot = builder.Build();

        Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
        Assert.AreSequenceEqual(
            new[] { new AllowedTemperatureInterval(10, 20) },
            storage.Intervals);
        Assert.AreSequenceEqual(
            new[] { 10, 20 },
            snapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
    }

    [TestMethod]
    public void AddTemperatureConstrainedFetchRequest_WhenTagsRepeat_DeduplicatesPerRequest()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(
            parentWorldId: 1,
            requestedTags: [iron, iron, iron],
            enabledConstraint: Constraint(10, 20));

        var snapshot = builder.Build();

        Assert.AreSequenceEqual(new[] { iron }, snapshot.GetRequestedTags(1));
        Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
        Assert.AreSequenceEqual(
            new[] { new AllowedTemperatureInterval(10, 20) },
            storage.Intervals);
        Assert.AreSequenceEqual(
            new[] { 10, 20 },
            snapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
    }

    [TestMethod]
    public void AddTemperatureConstrainedFetchRequest_WhenSameTagExistsInDifferentParents_DoesNotCrossContaminate()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(1, [iron], Constraint(10, 20));
        builder.AddTemperatureConstrainedFetchRequest(2, [iron], Constraint(30, 40));

        var snapshot = builder.Build();

        Assert.AreSequenceEqual(
            new[] { 10, 20 },
            snapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
        Assert.AreSequenceEqual(
            new[] { 30, 40 },
            snapshot.CreateSortedDecisionEndpointUnion(2, [iron]));
    }

    [TestMethod]
    public void Build_WhenNoFetchRequests_PublishesCompleteEmptySnapshot()
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

        Assert.AreEqual(gameSessionGeneration, snapshot.GameSessionGeneration);
        Assert.AreEqual(
            constraintSnapshot.Generation,
            snapshot.ConstraintGeneration);
        Assert.AreEqual(fetchTopologyVersion, snapshot.FetchTopologyVersion);
        Assert.AreEqual(worldTopology.Version, snapshot.WorldTopologyVersion);
        Assert.IsFalse(snapshot.TryGetStorageEligibility(
            parentWorldId: 1,
            requestedTag: new Tag("Iron"),
            out _));
        Assert.IsEmpty(snapshot.GetRequestedTags(1));
        Assert.IsEmpty(snapshot.CreateSortedDecisionEndpointUnion(
            parentWorldId: 1,
            applicableRequestedTags: [new Tag("Iron")]));
    }

    [TestMethod]
    public void Build_WhenCalledBeforeBegin_ThrowsInvalidOperationException()
    {
        var builder = new FetchTemperatureEligibilityBuilder();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.Build());
    }

    [TestMethod]
    public void Build_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var builder = BeginBuilder();
        builder.Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.Build());
    }

    [TestMethod]
    public void Discard_WhenEnumerationThrows_DropsAllCandidateReferences()
    {
        var builder = BeginBuilder();
        var throwingTags = new ThrowingRequestedTagList(new Tag("Iron"));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.AddUnconstrainedFetchRequest(1, throwingTags));
        builder.Discard();

        Assert.IsEmpty(GetDestinationRequirementDictionary(builder));
        BeginBuilder(builder);
        var emptySnapshot = builder.Build();
        Assert.IsEmpty(emptySnapshot.GetRequestedTags(1));
    }

    [TestMethod]
    public void Builder_WhenPriorEntryCountExceedsHighWater_ReplacesMutableMaps()
    {
        var builder = new FetchTemperatureEligibilityBuilder();
        var retentionLimit =
            RetainedCollectionCapacityLimits
                .MaximumRetainedFetchEligibilityEntryCount;

        BeginBuilder(builder);
        var dictionaryAtLimit = GetDestinationRequirementDictionary(builder);
        AddUniqueUnconstrainedTags(builder, retentionLimit, tagIdentityOffset: 0);
        var atLimitSnapshot = builder.Build();
        AssertEveryGeneratedTagExists(
            atLimitSnapshot,
            retentionLimit,
            tagIdentityOffset: 0);
        Assert.AreSame(
            dictionaryAtLimit,
            GetDestinationRequirementDictionary(builder));

        BeginBuilder(builder);
        var dictionaryBeforeLimitExceeded =
            GetDestinationRequirementDictionary(builder);
        AddUniqueUnconstrainedTags(
            builder,
            retentionLimit + 1,
            tagIdentityOffset: retentionLimit);
        var limitExceededSnapshot = builder.Build();
        AssertEveryGeneratedTagExists(
            limitExceededSnapshot,
            retentionLimit + 1,
            tagIdentityOffset: retentionLimit);
        Assert.AreNotSame(
            dictionaryBeforeLimitExceeded,
            GetDestinationRequirementDictionary(builder));

        BeginBuilder(builder);
        var dictionaryBeforeLargerWorkload =
            GetDestinationRequirementDictionary(builder);
        var largerEntryCount = (retentionLimit * 2) + 17;
        AddUniqueUnconstrainedTags(
            builder,
            largerEntryCount,
            tagIdentityOffset: retentionLimit * 3);
        var largerSnapshot = builder.Build();
        AssertEveryGeneratedTagExists(
            largerSnapshot,
            largerEntryCount,
            tagIdentityOffset: retentionLimit * 3);
        Assert.AreNotSame(
            dictionaryBeforeLargerWorkload,
            GetDestinationRequirementDictionary(builder));
    }

    [TestMethod]
    public void Snapshot_WhenBuilderIsReused_RemainsImmutable()
    {
        var builder = BeginBuilder();
        var iron = new Tag("Iron");
        builder.AddTemperatureConstrainedFetchRequest(1, [iron], Constraint(10, 20));
        var firstSnapshot = builder.Build();

        BeginBuilder(builder);
        builder.AddTemperatureConstrainedFetchRequest(1, [iron], Constraint(30, 40));
        var secondSnapshot = builder.Build();

        Assert.AreSequenceEqual(
            new[] { 10, 20 },
            firstSnapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
        Assert.AreSequenceEqual(
            new[] { 30, 40 },
            secondSnapshot.CreateSortedDecisionEndpointUnion(1, [iron]));
    }

    [TestMethod]
    public void CreateSortedDecisionEndpointUnion_WhenPickupMatchesSeveralTags_ReturnsEveryApplicableEndpointOnce()
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

        Assert.AreSequenceEqual(
            new[] { 10, 20, 30, 40, 50 },
            endpoints);
        Assert.IsFalse(endpoints is int[]);
        Assert.IsFalse(endpoints is ICollection<int> mutable && !mutable.IsReadOnly);
    }

    [TestMethod]
    public void CreateSortedDecisionEndpointUnion_WhenPickupMatchesNoRequestedTag_ReturnsEmptySequence()
    {
        var builder = BeginBuilder();
        builder.AddTemperatureConstrainedFetchRequest(
            1,
            [new Tag("Iron")],
            Constraint(10, 20));
        var snapshot = builder.Build();

        Assert.IsEmpty(snapshot.CreateSortedDecisionEndpointUnion(
            1,
            [new Tag("Copper")]));
        Assert.IsEmpty(snapshot.CreateSortedDecisionEndpointUnion(
            99,
            [new Tag("Iron")]));
    }

    [TestMethod]
    public void GetRequestedTags_WhenParentContainsSeveralRequests_ReturnsImmutableFirstEncounterOrder()
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

        Assert.AreSequenceEqual(new[] { iron, copper, metal }, requestedTags);
        Assert.IsFalse(requestedTags is Tag[]);
        Assert.IsFalse(
            requestedTags is ICollection<Tag> mutable && !mutable.IsReadOnly);
    }

    [TestMethod]
    public void GetRequestedTags_WhenParentIsUnknown_ReturnsEmptyImmutableSequence()
    {
        var snapshot = BeginBuilder().Build();

        var first = snapshot.GetRequestedTags(99);
        var second = snapshot.GetRequestedTags(100);

        Assert.IsEmpty(first);
        Assert.AreSame(first, second);
        Assert.IsFalse(first is Tag[]);
    }

    [TestMethod]
    public void Snapshot_WhenRepresentativeTopologiesCoverEveryDecisionBucket_MatchesReferenceModel()
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
            AssertSnapshotMatchesReference(
                representativeTopologies[topologyIndex],
                everyBucketOrdinal,
                "representative topology=" + topologyIndex);
        }
    }

    [TestMethod]
    public void Snapshot_WhenTwoThousandSeededTopologiesAreSampled_MatchesIndependentReferenceModel()
    {
        var random = new Random(CombinedFetchEligibilitySeed);

        for (var topologyIndex = 0;
             topologyIndex < RandomizedTopologyCount;
             topologyIndex++)
        {
            var requests = CreateRandomTopology(random);
            var selectedBucketOrdinals =
                CreateSelectedBucketOrdinals(random, requests);

            AssertSnapshotMatchesReference(
                requests,
                selectedBucketOrdinals,
                "Seed=0x" + CombinedFetchEligibilitySeed.ToString("X") +
                "; topology=" + topologyIndex);
        }
    }

    private static void AssertSnapshotMatchesReference(
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
            Assert.AreSequenceEqual(
                expectedRequestedTags,
                snapshot.GetRequestedTags(parentWorldId),
                assertionContext + "; parent=" + parentWorldId + ".");

            foreach (var requestedTag in expectedRequestedTags)
            {
                var logicalDestinationConstraints =
                    ReferenceTemperatureEligibilityModel
                        .GetStorageDestinationConstraints(
                            requests,
                            parentWorldId,
                            requestedTag);
                Assert.IsNotEmpty(logicalDestinationConstraints, assertionContext);
                Assert.IsTrue(
                    snapshot.TryGetStorageEligibility(
                        parentWorldId,
                        requestedTag,
                        out var storageEligibility),
                    assertionContext + "; missing storage entry.");

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
                    Assert.AreEqual(
                        expectedAllows,
                        observedAllows,
                        assertionContext + "; parent=" + parentWorldId +
                        "; bucket=" + bucketOrdinal + ".");
                }

                AssertEndpointUnionMatchesReference(
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
            AssertEndpointUnionMatchesReference(
                snapshot,
                requests,
                parentWorldId,
                expectedRequestedTags,
                selectedBucketOrdinals,
                assertionContext);
        }

        Assert.IsFalse(snapshot.TryGetStorageEligibility(
            int.MaxValue,
            new Tag("Unknown"),
            out _));
        Assert.IsEmpty(snapshot.GetRequestedTags(int.MaxValue));
    }

    private static void AssertEndpointUnionMatchesReference(
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
        Assert.AreSequenceEqual(
            expectedEndpoints,
            observedEndpoints,
            assertionContext + "; endpoint union mismatch for parent=" +
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
                Assert.IsTrue(
                    VectorsAreEqual(firstVector, referenceVector),
                    assertionContext + "; partition merged distinct vectors for " +
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

    private static void AssertEveryGeneratedTagExists(
        FetchTemperatureEligibilitySnapshot snapshot,
        int tagCount,
        int tagIdentityOffset)
    {
        Assert.AreEqual(tagCount, snapshot.GetRequestedTags(1).Count);
        for (var tagIndex = 0; tagIndex < tagCount; tagIndex++)
        {
            Assert.IsTrue(snapshot.TryGetStorageEligibility(
                parentWorldId: 1,
                requestedTag: new Tag(
                    "RetainedTag-" + (tagIdentityOffset + tagIndex)),
                out var eligibility));
            Assert.IsTrue(eligibility.AllowsEveryTemperature);
        }
    }

    private static IDictionary GetDestinationRequirementDictionary(
        FetchTemperatureEligibilityBuilder builder)
    {
        var dictionaryField = typeof(FetchTemperatureEligibilityBuilder).GetField(
            "destinationRequirementsByParentWorldAndRequestedTag",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            dictionaryField,
            "The representation contract requires the exact private dictionary " +
            "FetchTemperatureEligibilityBuilder." +
            "destinationRequirementsByParentWorldAndRequestedTag.");
        return Assert.IsInstanceOfType<IDictionary>(
            dictionaryField.GetValue(builder));
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
