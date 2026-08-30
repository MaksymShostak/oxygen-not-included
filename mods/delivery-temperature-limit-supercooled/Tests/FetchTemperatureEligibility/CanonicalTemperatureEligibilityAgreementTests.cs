using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class CanonicalTemperatureEligibilityAgreementTests
{
    private const int WorldInventoryStateMachineSeed = 0xC47A109;
    private const int WorldInventoryStateMachineOperationCount = 10_000;

    private static readonly Tag[] WorldInventoryStateMachineResourceTags =
    [
        new Tag("Iron"),
        new Tag("Copper"),
        new Tag("Food")
    ];

    [TestMethod]
    public void CanonicalTemperatureDomains_WhenEveryDecisionBucketIsEvaluated_AgreeWithIndependentOracle()
    {
        DeliveryTemperatureConstraint[] representativeConstraints =
            RepresentativeConstraints();
        AllowedTemperatureIntervalSet[] singleDestinationIntervalSets =
            representativeConstraints
                .Select(constraint =>
                    constraint.IsEnabled
                        ? AllowedTemperatureIntervalSet.CreateFromDestinations(
                            includesUnconstrainedDestination: false,
                            [constraint])
                        : AllowedTemperatureIntervalSet.CreateFromDestinations(
                            includesUnconstrainedDestination: true,
                            []))
                .ToArray();
        int[] representativeDecisionEndpoints = representativeConstraints
            .Where(constraint =>
                constraint.IsEnabled && !constraint.IsEmpty)
            .SelectMany(constraint => new[]
            {
                constraint.MinimumInclusiveKelvin,
                constraint.MaximumExclusiveKelvin
            })
            .Distinct()
            .ToArray();
        TemperaturePartitionDefinition partition =
            TemperaturePartitionDefinition.Create(
                definitionId: 1,
                representativeDecisionEndpoints);
        var firstAllowanceVectorByPartitionClass =
            new Dictionary<int, bool[]>();
        var observedTemperatureClassKeys =
            new HashSet<TemperatureEligibilityClassKey>();

        for (int bucketOrdinal =
                 TemperatureDecisionBucket.BelowMinimumKelvinOrdinal;
             bucketOrdinal <=
                 TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal;
             bucketOrdinal++)
        {
            float representativeTemperatureKelvin =
                ReferenceTemperatureEligibilityModel
                    .GetRepresentativeTemperatureKelvin(bucketOrdinal);
            TemperatureDecisionBucket decisionBucket =
                TemperatureDecisionBucket.FromTemperature(
                    representativeTemperatureKelvin);
            Assert.AreEqual(
                bucketOrdinal,
                decisionBucket.Ordinal,
                $"Canonical bucket round trip failed at ordinal {bucketOrdinal}.");

            // A one-entry sparse series makes its query result a direct Boolean
            // membership observation without allocating the complete bucket range.
            TemperatureAmountSeries singleBucketSeries =
                TemperatureAmountSeries.CreateFromOwnedArrays(
                    [bucketOrdinal],
                    [1.0f]);
            bool[] referenceAllowanceVector =
                ReferenceTemperatureEligibilityModel
                    .EvaluateDestinationConstraintAllowances(
                        representativeConstraints,
                        representativeTemperatureKelvin);

            for (int constraintIndex = 0;
                 constraintIndex < representativeConstraints.Length;
                 constraintIndex++)
            {
                DeliveryTemperatureConstraint constraint =
                    representativeConstraints[constraintIndex];
                bool expectedAllowed = referenceAllowanceVector[constraintIndex];
                Assert.AreEqual(
                    expectedAllowed,
                    ReferenceTemperatureEligibilityModel.AllowsTemperature(
                        constraint,
                        representativeTemperatureKelvin),
                    CanonicalDomainFailure(
                        "independent direct oracle",
                        bucketOrdinal,
                        constraintIndex));
                Assert.AreEqual(
                    expectedAllowed,
                    constraint.Allows(representativeTemperatureKelvin),
                    CanonicalDomainFailure(
                        "normalized direct constraint",
                        bucketOrdinal,
                        constraintIndex));
                Assert.AreEqual(
                    expectedAllowed,
                    singleDestinationIntervalSets[constraintIndex].Allows(
                        decisionBucket),
                    CanonicalDomainFailure(
                        "normalized interval membership",
                        bucketOrdinal,
                        constraintIndex));
                Assert.AreEqual(
                    expectedAllowed ? 1.0f : 0.0f,
                    singleBucketSeries.GetAmountAllowedBy(constraint),
                    CanonicalDomainFailure(
                        "sparse amount-series membership",
                        bucketOrdinal,
                        constraintIndex));
            }

            int partitionClass = partition.Classify(decisionBucket);
            if (firstAllowanceVectorByPartitionClass.TryGetValue(
                    partitionClass,
                    out bool[]? firstAllowanceVector))
            {
                AssertAllowanceVectorsEqual(
                    firstAllowanceVector,
                    referenceAllowanceVector,
                    bucketOrdinal,
                    "One optimized partition class admitted unequal destination " +
                    "eligibility vectors.");
            }
            else
            {
                firstAllowanceVectorByPartitionClass.Add(
                    partitionClass,
                    referenceAllowanceVector);
            }

            observedTemperatureClassKeys.Add(
                TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                    partition.DefinitionId,
                    partitionClass));
        }

        TemperatureEligibilityClassKey missingPrimaryElementClass =
            TemperatureEligibilityClassKey.MissingPrimaryElement();
        Assert.AreEqual(
            TemperatureEligibilityClassificationKind.MissingPrimaryElement,
            missingPrimaryElementClass.ClassificationKind);
        Assert.IsFalse(
            observedTemperatureClassKeys.Contains(missingPrimaryElementClass),
            "Missing primary-element state must remain a named non-temperature " +
            "classification rather than consuming a canonical bucket ordinal.");
    }

    [TestMethod]
    public void AllowedTemperatureIntervalUnions_WhenEveryDecisionBucketIsEvaluated_EqualDirectDestinationAnyEvaluation()
    {
        DeliveryTemperatureConstraint[] representativeConstraints =
            RepresentativeConstraints();
        DeliveryTemperatureConstraint[] enabledConstraints =
            representativeConstraints
                .Where(constraint => constraint.IsEnabled)
                .ToArray();
        AllowedTemperatureIntervalSet enabledDestinationUnion =
            AllowedTemperatureIntervalSet.CreateFromDestinations(
                includesUnconstrainedDestination: false,
                enabledConstraints);
        AllowedTemperatureIntervalSet unionIncludingUnconstrainedDestination =
            AllowedTemperatureIntervalSet.CreateFromDestinations(
                includesUnconstrainedDestination: true,
                enabledConstraints);

        for (int bucketOrdinal =
                 TemperatureDecisionBucket.BelowMinimumKelvinOrdinal;
             bucketOrdinal <=
                 TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal;
             bucketOrdinal++)
        {
            float representativeTemperatureKelvin =
                ReferenceTemperatureEligibilityModel
                    .GetRepresentativeTemperatureKelvin(bucketOrdinal);
            TemperatureDecisionBucket decisionBucket =
                TemperatureDecisionBucket.FromTemperature(
                    representativeTemperatureKelvin);
            Assert.AreEqual(
                ReferenceTemperatureEligibilityModel
                    .AnyDestinationAllowsTemperature(
                        enabledConstraints,
                        representativeTemperatureKelvin),
                enabledDestinationUnion.Allows(decisionBucket),
                $"Enabled-destination interval union differed at canonical " +
                $"bucket {bucketOrdinal}.");
            Assert.AreEqual(
                ReferenceTemperatureEligibilityModel
                    .AnyDestinationAllowsTemperature(
                        representativeConstraints,
                        representativeTemperatureKelvin),
                unionIncludingUnconstrainedDestination.Allows(decisionBucket),
                $"Unconstrained-destination interval union differed at " +
                $"canonical bucket {bucketOrdinal}.");
        }
    }

    [TestMethod]
    public void WorldInventoryPublicationModes_WhenBothAreComplete_ReturnEqualTemperatureConstrainedTotals()
    {
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var generation = new WorldInventoryCollectionGeneration(17);
        ReferenceWorldResourceTemperatureAmountSeries[][] publicationsByWorld =
        [
            [
                ReferenceSeries(
                    iron,
                    (-1.0f, 2.0f),
                    (0.0f, 3.0f),
                    (273.0f, 5.0f),
                    (5000.0f, 7.0f),
                    (10000.0f, 11.0f)),
                ReferenceSeries(copper, (274.0f, 13.0f))
            ],
            [
                ReferenceSeries(
                    iron,
                    (1.0f, 17.0f),
                    (5017.0f, 19.0f),
                    (9999.0f, 23.0f)),
                ReferenceSeries(copper, (6203.0f, 29.0f))
            ]
        ];
        var completeWorldCatalog = new WorldResourceTemperatureAmountCatalog();
        var incrementalCatalog = new WorldResourceTemperatureAmountCatalog();
        var completeWorldReference =
            new ReferenceWorldResourceTemperatureAmounts();
        var incrementalReference =
            new ReferenceWorldResourceTemperatureAmounts();

        for (int worldIndex = 0;
             worldIndex < publicationsByWorld.Length;
             worldIndex++)
        {
            int worldId = worldIndex + 1;
            completeWorldCatalog.RegisterWorld(worldId, parentWorldId: 10);
            incrementalCatalog.RegisterWorld(worldId, parentWorldId: 10);
            completeWorldReference.RegisterWorld(worldId, parentWorldId: 10);
            incrementalReference.RegisterWorld(worldId, parentWorldId: 10);

            ReferenceWorldResourceTemperatureAmountSeries[] worldPublication =
                publicationsByWorld[worldIndex];
            Assert.IsTrue(
                completeWorldCatalog.PublishCompleteWorldResourceAmounts(
                    worldId,
                    CompleteWorldPublication(generation, worldPublication)));
            Assert.IsTrue(
                completeWorldReference.TryPublishCompleteWorld(
                    worldId,
                    generation,
                    worldPublication));

            Tag[] presentResourceTags = worldPublication
                .Select(resourceSeries => resourceSeries.ResourceTag)
                .ToArray();
            Assert.IsTrue(
                incrementalCatalog.PublishWorldResourceTagCoverage(
                    worldId,
                    WorldResourceTagCoverage.Create(
                        generation,
                        presentResourceTags)));
            Assert.IsTrue(
                incrementalReference.TryPublishResourceTagCoverage(
                    worldId,
                    generation,
                    presentResourceTags));
            foreach (ReferenceWorldResourceTemperatureAmountSeries
                         resourceSeries in worldPublication)
            {
                Assert.IsTrue(
                    incrementalCatalog.PublishWorldResourceTemperatureSeries(
                        worldId,
                        ResourceTagTemperatureSeriesPublication(
                            generation,
                            resourceSeries)));
                Assert.IsTrue(
                    incrementalReference
                        .TryPublishResourceTagTemperatureAmounts(
                            worldId,
                            generation,
                            resourceSeries.ResourceTag,
                            resourceSeries.TemperatureAmounts));
            }
        }

        foreach (Tag resourceTag in new[] { iron, copper })
        {
            foreach (DeliveryTemperatureConstraint constraint in
                     RepresentativeConstraints())
            {
                TemperatureConstrainedAmountAvailability completeWorldResult =
                    completeWorldCatalog
                        .GetTemperatureConstrainedAmountAvailability(
                            parentWorldId: 10,
                            resourceTag,
                            constraint,
                            generation);
                TemperatureConstrainedAmountAvailability incrementalResult =
                    incrementalCatalog
                        .GetTemperatureConstrainedAmountAvailability(
                            parentWorldId: 10,
                            resourceTag,
                            constraint,
                            generation);
                ReferenceWorldResourceTemperatureAmountAvailability
                    completeWorldExpected = completeWorldReference
                        .GetTemperatureConstrainedAmountAvailability(
                            parentWorldId: 10,
                            resourceTag,
                            constraint,
                            generation);
                ReferenceWorldResourceTemperatureAmountAvailability
                    incrementalExpected = incrementalReference
                        .GetTemperatureConstrainedAmountAvailability(
                            parentWorldId: 10,
                            resourceTag,
                            constraint,
                            generation);

                AssertAvailabilityMatchesReference(
                    completeWorldExpected,
                    completeWorldResult,
                    "Complete-world publication differed from its independent " +
                    "reference.");
                AssertAvailabilityMatchesReference(
                    incrementalExpected,
                    incrementalResult,
                    "Coverage/single-tag publication differed from its " +
                    "independent reference.");
                AssertEquivalentAvailability(
                    completeWorldResult,
                    incrementalResult,
                    "Complete-world and coverage/single-tag modes produced " +
                    "different complete evidence.");
            }
        }
    }

    [TestMethod]
    public void WorldInventoryPublications_WhenTenThousandSeededOperationsRun_MatchIndependentCoverageAndPendingModel()
    {
        var random = new Random(WorldInventoryStateMachineSeed);
        var generation = new WorldInventoryCollectionGeneration(31);
        var catalog = new WorldResourceTemperatureAmountCatalog();
        var reference = new ReferenceWorldResourceTemperatureAmounts();
        DeliveryTemperatureConstraint[] representativeConstraints =
            RepresentativeConstraints();

        for (int operationIndex = 0;
             operationIndex < WorldInventoryStateMachineOperationCount;
             operationIndex++)
        {
            ApplyWorldInventoryStateMachineOperation(
                random,
                catalog,
                reference,
                generation,
                operationIndex);

            int parentWorldId = random.Next(1, 4);
            Tag resourceTag = WorldInventoryStateMachineResourceTags[
                random.Next(WorldInventoryStateMachineResourceTags.Length)];
            DeliveryTemperatureConstraint constraint =
                representativeConstraints[
                    random.Next(representativeConstraints.Length)];
            string diagnostic = WorldInventoryStateMachineDiagnostic(
                operationIndex,
                parentWorldId,
                resourceTag,
                constraint);
            AssertAvailabilityMatchesReference(
                reference.GetTemperatureConstrainedAmountAvailability(
                    parentWorldId,
                    resourceTag,
                    constraint,
                    generation),
                catalog.GetTemperatureConstrainedAmountAvailability(
                    parentWorldId,
                    resourceTag,
                    constraint,
                    generation),
                diagnostic);
        }
    }

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

    private static void ApplyWorldInventoryStateMachineOperation(
        Random random,
        WorldResourceTemperatureAmountCatalog catalog,
        ReferenceWorldResourceTemperatureAmounts reference,
        WorldInventoryCollectionGeneration generation,
        int operationIndex)
    {
        int worldId = random.Next(1, 9);
        switch (operationIndex % 8)
        {
            case 0:
            case 7:
            {
                int parentWorldId = random.Next(1, 4);
                catalog.RegisterWorld(worldId, parentWorldId);
                reference.RegisterWorld(worldId, parentWorldId);
                break;
            }

            case 1:
            {
                ReferenceWorldResourceTemperatureAmountSeries[]
                    completeWorldResourceSeries =
                        CreateRandomCompleteWorldResourceSeries(random);
                bool observedAccepted =
                    catalog.PublishCompleteWorldResourceAmounts(
                        worldId,
                        CompleteWorldPublication(
                            generation,
                            completeWorldResourceSeries));
                bool expectedAccepted = reference.TryPublishCompleteWorld(
                    worldId,
                    generation,
                    completeWorldResourceSeries);
                Assert.AreEqual(
                    expectedAccepted,
                    observedAccepted,
                    WorldInventoryStateMachineDiagnostic(
                        operationIndex,
                        parentWorldId: -1,
                        resourceTag: default,
                        constraint: default));
                break;
            }

            case 2:
            case 5:
            {
                Tag[] presentResourceTags =
                    CreateRandomResourceTagCoverage(random);
                bool observedAccepted =
                    catalog.PublishWorldResourceTagCoverage(
                        worldId,
                        WorldResourceTagCoverage.Create(
                            generation,
                            presentResourceTags));
                bool expectedAccepted =
                    reference.TryPublishResourceTagCoverage(
                        worldId,
                        generation,
                        presentResourceTags);
                Assert.AreEqual(
                    expectedAccepted,
                    observedAccepted,
                    WorldInventoryStateMachineDiagnostic(
                        operationIndex,
                        parentWorldId: -1,
                        resourceTag: default,
                        constraint: default));
                break;
            }

            case 3:
            case 4:
            {
                Tag resourceTag = WorldInventoryStateMachineResourceTags[
                    random.Next(
                        WorldInventoryStateMachineResourceTags.Length)];
                IReadOnlyList<ReferenceTemperatureAmount> temperatureAmounts =
                    CreateRandomTemperatureAmounts(random);
                var referenceSeries =
                    new ReferenceWorldResourceTemperatureAmountSeries(
                        resourceTag,
                        temperatureAmounts);
                bool observedAccepted =
                    catalog.PublishWorldResourceTemperatureSeries(
                        worldId,
                        ResourceTagTemperatureSeriesPublication(
                            generation,
                            referenceSeries));
                bool expectedAccepted = reference
                    .TryPublishResourceTagTemperatureAmounts(
                        worldId,
                        generation,
                        resourceTag,
                        temperatureAmounts);
                Assert.AreEqual(
                    expectedAccepted,
                    observedAccepted,
                    WorldInventoryStateMachineDiagnostic(
                        operationIndex,
                        parentWorldId: -1,
                        resourceTag,
                        constraint: default));
                break;
            }

            case 6:
                catalog.RemoveWorld(worldId);
                reference.RemoveWorld(worldId);
                break;

            default:
                Assert.Fail(
                    $"Unsupported state-machine operation at index " +
                    $"{operationIndex}.");
                break;
        }
    }

    private static ReferenceWorldResourceTemperatureAmountSeries[]
        CreateRandomCompleteWorldResourceSeries(Random random)
    {
        var result =
            new List<ReferenceWorldResourceTemperatureAmountSeries>();
        foreach (Tag resourceTag in WorldInventoryStateMachineResourceTags)
        {
            if (random.Next(2) == 0)
            {
                result.Add(
                    new ReferenceWorldResourceTemperatureAmountSeries(
                        resourceTag,
                        CreateRandomTemperatureAmounts(random)));
            }
        }

        return result.ToArray();
    }

    private static Tag[] CreateRandomResourceTagCoverage(Random random)
    {
        var presentResourceTags = new List<Tag>();
        foreach (Tag resourceTag in WorldInventoryStateMachineResourceTags)
        {
            if (random.Next(2) == 0)
            {
                presentResourceTags.Add(resourceTag);
            }
        }

        return presentResourceTags.ToArray();
    }

    private static IReadOnlyList<ReferenceTemperatureAmount>
        CreateRandomTemperatureAmounts(Random random)
    {
        float[] representativeTemperaturesKelvin =
        [
            -1.0f,
            0.0f,
            1.0f,
            273.0f,
            274.0f,
            5000.0f,
            5017.0f,
            5100.0f,
            6203.0f,
            9999.0f,
            10000.0f
        ];
        int amountEntryCount = random.Next(0, 5);
        var result = new ReferenceTemperatureAmount[amountEntryCount];
        for (int amountEntryIndex = 0;
             amountEntryIndex < amountEntryCount;
             amountEntryIndex++)
        {
            result[amountEntryIndex] = new ReferenceTemperatureAmount(
                representativeTemperaturesKelvin[
                    random.Next(representativeTemperaturesKelvin.Length)],
                random.Next(0, 21));
        }

        return result;
    }

    private static ReferenceWorldResourceTemperatureAmountSeries
        ReferenceSeries(
            Tag resourceTag,
            params (float TemperatureKelvin, float Amount)[]
                temperatureAmounts) =>
        new(
            resourceTag,
            temperatureAmounts
                .Select(temperatureAmount =>
                    new ReferenceTemperatureAmount(
                        temperatureAmount.TemperatureKelvin,
                        temperatureAmount.Amount))
                .ToArray());

    private static CompleteWorldResourceTemperatureAmounts
        CompleteWorldPublication(
            WorldInventoryCollectionGeneration generation,
            IReadOnlyList<ReferenceWorldResourceTemperatureAmountSeries>
                resourceSeries)
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(generation);
        foreach (ReferenceWorldResourceTemperatureAmountSeries series in
                 resourceSeries)
        {
            builder.BeginResourceTag(series.ResourceTag);
            foreach (ReferenceTemperatureAmount temperatureAmount in
                     series.TemperatureAmounts)
            {
                builder.AddTemperatureAmount(
                    temperatureAmount.TemperatureKelvin,
                    temperatureAmount.Amount);
            }

            builder.CompleteResourceTag();
        }

        return builder.Build();
    }

    private static WorldResourceTemperatureSeriesPublication
        ResourceTagTemperatureSeriesPublication(
            WorldInventoryCollectionGeneration generation,
            ReferenceWorldResourceTemperatureAmountSeries resourceSeries)
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        foreach (ReferenceTemperatureAmount temperatureAmount in
                 resourceSeries.TemperatureAmounts)
        {
            accumulator.AddTemperatureAmount(
                temperatureAmount.TemperatureKelvin,
                temperatureAmount.Amount);
        }

        return new WorldResourceTemperatureSeriesPublication(
            generation,
            resourceSeries.ResourceTag,
            accumulator.BuildSeries());
    }

    private static DeliveryTemperatureConstraint[] RepresentativeConstraints() =>
    [
        Constraint(0, 0),
        Constraint(0, 1),
        Constraint(0, 273),
        Constraint(1, 274),
        Constraint(273, 274),
        Constraint(274, 274),
        Constraint(274, 5000),
        Constraint(5000, 5017),
        Constraint(5017, 5100),
        Constraint(5100, 6203),
        Constraint(6203, 9999),
        Constraint(9999, 10000),
        Constraint(0, 10000)
    ];

    private static string CanonicalDomainFailure(
        string domainName,
        int bucketOrdinal,
        int constraintIndex) =>
        $"{domainName} differed at canonical bucket {bucketOrdinal}, " +
        $"representative constraint {constraintIndex}.";

    private static string WorldInventoryStateMachineDiagnostic(
        int operationIndex,
        int parentWorldId,
        Tag resourceTag,
        DeliveryTemperatureConstraint constraint) =>
        $"Seed=0x{WorldInventoryStateMachineSeed:X}; " +
        $"operation={operationIndex}; parent={parentWorldId}; " +
        $"tagHash={resourceTag.GetHashCode()}; " +
        $"constraint=[{constraint.MinimumInclusiveKelvin}, " +
        $"{constraint.MaximumExclusiveKelvin}).";

    private static void AssertAvailabilityMatchesReference(
        ReferenceWorldResourceTemperatureAmountAvailability expected,
        TemperatureConstrainedAmountAvailability actual,
        string failureReason)
    {
        TemperatureConstrainedAmountAvailabilityState expectedProductionState =
            expected.State switch
            {
                ReferenceWorldResourceTemperatureAmountAvailabilityState
                    .TemperatureConstraintDisabled =>
                    TemperatureConstrainedAmountAvailabilityState
                        .TemperatureConstraintDisabled,
                ReferenceWorldResourceTemperatureAmountAvailabilityState
                    .InventoryIncomplete =>
                    TemperatureConstrainedAmountAvailabilityState
                        .InventoryIncomplete,
                ReferenceWorldResourceTemperatureAmountAvailabilityState
                    .Complete =>
                    TemperatureConstrainedAmountAvailabilityState.Complete,
                _ => throw new InvalidOperationException(
                    $"Unknown reference availability state {expected.State}.")
            };
        Assert.AreEqual(expectedProductionState, actual.State, failureReason);
        if (expected.State ==
            ReferenceWorldResourceTemperatureAmountAvailabilityState.Complete)
        {
            Assert.IsTrue(expected.TryGetCompleteAmount(out float expectedAmount));
            Assert.IsTrue(
                actual.TryGetCompleteAvailableAmount(out float actualAmount),
                failureReason);
            Assert.AreEqual(expectedAmount, actualAmount, failureReason);
        }
        else
        {
            Assert.IsFalse(actual.TryGetCompleteAvailableAmount(out _));
        }
    }

    private static void AssertEquivalentAvailability(
        TemperatureConstrainedAmountAvailability expected,
        TemperatureConstrainedAmountAvailability actual,
        string failureReason)
    {
        Assert.AreEqual(expected.State, actual.State, failureReason);
        bool expectedHasAmount =
            expected.TryGetCompleteAvailableAmount(out float expectedAmount);
        bool actualHasAmount =
            actual.TryGetCompleteAvailableAmount(out float actualAmount);
        Assert.AreEqual(expectedHasAmount, actualHasAmount, failureReason);
        if (expectedHasAmount)
        {
            Assert.AreEqual(expectedAmount, actualAmount, failureReason);
        }
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
