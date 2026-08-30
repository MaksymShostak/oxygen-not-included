using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class CanonicalTemperatureEligibilityAgreementTests
{
    [TestMethod]
    public void Classify_WhenCurrentSnapshotCoversEveryDecisionBucket_ProducesSoundMinimalScopedClasses()
    {
        var iron = new Tag("Iron");
        var metal = new Tag("Metal");
        var copper = new Tag("Copper");
        var requests = new[]
        {
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                1,
                [iron],
                Constraint(10, 20)),
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                1,
                [metal],
                Constraint(30, 40)),
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                1,
                [iron, metal],
                Constraint(50, 60)),
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                1,
                [iron],
                Constraint(20, 20)),
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                1,
                [copper],
                Constraint(70, 80)),
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                2,
                [iron],
                Constraint(90, 100))
        };
        var applicableRequestedTags = new[] { iron, metal };
        var relevantConstraints =
            ReferenceTemperatureEligibilityModel
                .GetApplicablePickupTemperatureConstraints(
                    requests,
                    parentWorldId: 1,
                    applicableRequestedTags);
        var relevantEndpoints = new HashSet<int>(
            ReferenceTemperatureEligibilityModel
                .CreateSortedPickupDecisionEndpointUnion(
                    requests,
                    parentWorldId: 1,
                    applicableRequestedTags));
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, requests);
        var groupingSession = BeginGroupingSession(session, 1, snapshot);
        var firstVectorByClassification =
            new Dictionary<TemperatureEligibilityClassKey, bool[]>();
        bool[]? previousVector = null;
        var previousClassification = default(TemperatureEligibilityClassKey);

        for (var bucketOrdinal = 0;
             bucketOrdinal < TemperatureDecisionBucket.BucketCount;
             bucketOrdinal++)
        {
            var temperatureKelvin =
                ReferenceTemperatureEligibilityModel
                    .GetRepresentativeTemperatureKelvin(bucketOrdinal);
            var classification = groupingSession.Classify(
                pickupInstanceId: bucketOrdinal,
                new PickupTagIdentity(17, iron),
                applicableRequestedTags,
                hasPrimaryElement: true,
                temperatureKelvin);
            var referenceVector =
                ReferenceTemperatureEligibilityModel
                    .EvaluateDestinationConstraintAllowances(
                        relevantConstraints,
                        temperatureKelvin);

            Assert.AreEqual(
                TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval,
                classification.ClassificationKind);
            if (firstVectorByClassification.TryGetValue(
                    classification,
                    out var firstVector))
            {
                AssertAllowanceVectorsEqual(
                    firstVector,
                    referenceVector,
                    bucketOrdinal,
                    "One optimized key admitted different direct eligibility " +
                    "vectors.");
            }
            else
            {
                firstVectorByClassification.Add(
                    classification,
                    referenceVector);
            }

            if (previousVector != null)
            {
                var adjacentVectorsMatch =
                    ReferenceTemperatureEligibilityModel
                        .AllowanceVectorsAreEqual(
                            previousVector,
                            referenceVector);
                if (!adjacentVectorsMatch)
                {
                    Assert.AreNotEqual(
                        previousClassification,
                        classification,
                        "Different adjacent direct eligibility vectors shared " +
                        $"one class at bucket {bucketOrdinal}.");
                }
                else if (!relevantEndpoints.Contains(
                             ReferenceTemperatureEligibilityModel
                                 .GetRepresentativeTruncatedKelvin(
                                     bucketOrdinal)))
                {
                    Assert.AreEqual(
                        previousClassification,
                        classification,
                        "Equivalent adjacent buckets without a relevant endpoint " +
                        $"were fragmented at bucket {bucketOrdinal}.");
                }
            }

            previousVector = referenceVector;
            previousClassification = classification;
        }
    }

    [TestMethod]
    public void Classify_WhenSnapshotIsStaleAcrossEveryDecisionBucket_UsesOnlyExactBucketIdentity()
    {
        var iron = new Tag("Iron");
        var requests = new[]
        {
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                1,
                [iron],
                Constraint(10, 20))
        };
        var session = CreateSessionWithEnabledConstraint();
        var staleSnapshot = BuildCurrentEligibilitySnapshot(session, requests);
        session.FetchRequestTopology.RecordEffectiveChange();
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            staleSnapshot);
        var bucketByClassification =
            new Dictionary<
                TemperatureEligibilityClassKey,
                TemperatureDecisionBucket>();

        for (var bucketOrdinal = 0;
             bucketOrdinal < TemperatureDecisionBucket.BucketCount;
             bucketOrdinal++)
        {
            var temperatureKelvin =
                ReferenceTemperatureEligibilityModel
                    .GetRepresentativeTemperatureKelvin(bucketOrdinal);
            var expectedBucket =
                TemperatureDecisionBucket.FromTemperature(temperatureKelvin);
            var classification = groupingSession.Classify(
                pickupInstanceId: bucketOrdinal,
                new PickupTagIdentity(17, iron),
                applicableRequestedTags: [iron],
                hasPrimaryElement: true,
                temperatureKelvin);

            Assert.AreEqual(
                TemperatureEligibilityClassificationKind
                    .ExactTemperatureDecisionBucket,
                classification.ClassificationKind);
            Assert.AreEqual(
                expectedBucket,
                classification.ExactTemperatureDecisionBucket);
            if (bucketByClassification.TryGetValue(
                    classification,
                    out var firstBucket))
            {
                Assert.AreEqual(
                    expectedBucket,
                    firstBucket,
                    "A stale-snapshot fallback key represented more than one " +
                    "exact decision bucket.");
            }
            else
            {
                bucketByClassification.Add(classification, expectedBucket);
            }
        }

        var firstSameBucketClassification = groupingSession.Classify(
            pickupInstanceId: TemperatureDecisionBucket.BucketCount + 1,
            new PickupTagIdentity(17, iron),
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 10.1f);
        var secondSameBucketClassification = groupingSession.Classify(
            pickupInstanceId: TemperatureDecisionBucket.BucketCount + 2,
            new PickupTagIdentity(17, iron),
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 10.9f);
        Assert.AreEqual(
            firstSameBucketClassification,
            secondSameBucketClassification,
            "Temperatures in the same canonical exact bucket must retain one " +
            "fallback identity.");
    }

    [TestMethod]
    public void Classify_WhenAnotherParentHasOneThousandEndpoints_DoesNotFragmentScopedParent()
    {
        var iron = new Tag("Iron");
        var food = new Tag("Food");
        var requests = new List<ReferenceFetchTemperatureRequest>
        {
            ReferenceFetchTemperatureRequest.TemperatureConstrained(
                parentWorldId: 1,
                requestedTags: [iron],
                enabledTemperatureConstraint: Constraint(10, 20))
        };
        var session = CreateSessionWithEnabledConstraint();
        for (var endpointPairIndex = 0;
             endpointPairIndex < 500;
             endpointPairIndex++)
        {
            var minimumInclusiveKelvin = endpointPairIndex * 2;
            var maximumExclusiveKelvin = minimumInclusiveKelvin + 1;
            var constraint = Constraint(
                minimumInclusiveKelvin,
                maximumExclusiveKelvin);
            requests.Add(
                ReferenceFetchTemperatureRequest.TemperatureConstrained(
                    parentWorldId: 2,
                    requestedTags: [food],
                    enabledTemperatureConstraint: constraint));
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 20000 + endpointPairIndex,
                componentInstanceId: 30000 + endpointPairIndex,
                new TemperatureLimit(),
                constraint);
        }

        Assert.HasCount(
            1000,
            ReferenceTemperatureEligibilityModel
                .CreateSortedPickupDecisionEndpointUnion(
                    requests,
                    parentWorldId: 2,
                    applicableRequestedTags: [food]));
        var snapshot = BuildCurrentEligibilitySnapshot(session, requests);
        var groupingSession = BeginGroupingSession(session, 1, snapshot);
        var parentOneClassifications =
            new HashSet<TemperatureEligibilityClassKey>();

        for (var bucketOrdinal = 0;
             bucketOrdinal < TemperatureDecisionBucket.BucketCount;
             bucketOrdinal++)
        {
            var classification = groupingSession.Classify(
                pickupInstanceId: bucketOrdinal,
                new PickupTagIdentity(17, iron),
                applicableRequestedTags: [iron],
                hasPrimaryElement: true,
                temperatureKelvin: ReferenceTemperatureEligibilityModel
                    .GetRepresentativeTemperatureKelvin(bucketOrdinal));
            Assert.AreEqual(
                TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval,
                classification.ClassificationKind);
            parentOneClassifications.Add(classification);
        }

        Assert.HasCount(
            3,
            parentOneClassifications,
            "Parent 1/Iron must be partitioned only by endpoints 10 and 20; " +
            "parent 2/Food endpoints are unrelated.");
    }

    private static void AssertAllowanceVectorsEqual(
        IReadOnlyList<bool> expected,
        IReadOnlyList<bool> actual,
        int bucketOrdinal,
        string failureReason)
    {
        Assert.AreEqual(
            expected.Count,
            actual.Count,
            $"{failureReason} Bucket {bucketOrdinal} had a length mismatch.");
        for (var constraintIndex = 0;
             constraintIndex < expected.Count;
             constraintIndex++)
        {
            Assert.AreEqual(
                expected[constraintIndex],
                actual[constraintIndex],
                $"{failureReason} Bucket {bucketOrdinal}, constraint " +
                $"{constraintIndex} differed.");
        }
    }

    private static DeliveryTemperatureGameSession
        CreateSessionWithEnabledConstraint()
    {
        var session = new DeliveryTemperatureGameSession(
            new GameSessionGeneration(1),
            gameInstanceId: 1);
        session.RegisterTemperatureLimit(
            gameObjectInstanceId: 10001,
            componentInstanceId: 10002,
            new TemperatureLimit(),
            Constraint(10, 20));
        return session;
    }

    private static FetchTemperatureEligibilitySnapshot
        BuildCurrentEligibilitySnapshot(
            DeliveryTemperatureGameSession session,
            IReadOnlyList<ReferenceFetchTemperatureRequest> requests)
    {
        var builder = new FetchTemperatureEligibilityBuilder();
        builder.Begin(
            session.Generation,
            session.TemperatureConstraints.CaptureSnapshot(),
            session.FetchRequestTopology.CaptureVersion(),
            session.WorldParentTopology.CaptureSnapshot());
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

    private static PickupTemperatureGroupingSession BeginGroupingSession(
        DeliveryTemperatureGameSession session,
        int? resolvedParentWorldId,
        FetchTemperatureEligibilitySnapshot? eligibilitySnapshot)
    {
        var groupingSession = new PickupTemperatureGroupingSession();
        groupingSession.Begin(
            session,
            resolvedParentWorldId,
            session.TemperatureConstraints.CaptureSnapshot(),
            eligibilitySnapshot,
            session.WorldParentTopology.CaptureSnapshot());
        return groupingSession;
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);
}
