using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
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

    [Test]
    public async Task CanonicalTemperatureDomains_WhenEveryDecisionBucketIsEvaluated_AgreeWithIndependentOracle()
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
            await Assert.That(decisionBucket.Ordinal).IsEqualTo(bucketOrdinal).Because($"Canonical bucket round trip failed at ordinal {bucketOrdinal}.");

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
                await Assert.That(ReferenceTemperatureEligibilityModel.AllowsTemperature(
                        constraint,
                        representativeTemperatureKelvin)).IsEqualTo(expectedAllowed).Because(CanonicalDomainFailure(
                        "independent direct oracle",
                        bucketOrdinal,
                        constraintIndex));
                await Assert.That(constraint.Allows(representativeTemperatureKelvin)).IsEqualTo(expectedAllowed).Because(CanonicalDomainFailure(
                        "normalized direct constraint",
                        bucketOrdinal,
                        constraintIndex));
                await Assert.That(singleDestinationIntervalSets[constraintIndex].Allows(
                        decisionBucket)).IsEqualTo(expectedAllowed).Because(CanonicalDomainFailure(
                        "normalized interval membership",
                        bucketOrdinal,
                        constraintIndex));
                await Assert.That(singleBucketSeries.GetAmountAllowedBy(constraint)).IsEqualTo(expectedAllowed ? 1.0f : 0.0f).Because(CanonicalDomainFailure(
                        "sparse amount-series membership",
                        bucketOrdinal,
                        constraintIndex));
            }

            int partitionClass = partition.Classify(decisionBucket);
            if (firstAllowanceVectorByPartitionClass.TryGetValue(
                    partitionClass,
                    out bool[]? firstAllowanceVector))
            {
                await AssertAllowanceVectorsEqual(
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
        await Assert.That(missingPrimaryElementClass.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind.MissingPrimaryElement);
        await Assert.That(observedTemperatureClassKeys.Contains(missingPrimaryElementClass)).IsFalse().Because("Missing primary-element state must remain a named non-temperature " +
            "classification rather than consuming a canonical bucket ordinal.");
    }

    [Test]
    public async Task AllowedTemperatureIntervalUnions_WhenEveryDecisionBucketIsEvaluated_EqualDirectDestinationAnyEvaluation()
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
            await Assert.That(enabledDestinationUnion.Allows(decisionBucket)).IsEqualTo(ReferenceTemperatureEligibilityModel
                    .AnyDestinationAllowsTemperature(
                        enabledConstraints,
                        representativeTemperatureKelvin)).Because($"Enabled-destination interval union differed at canonical " +
                $"bucket {bucketOrdinal}.");
            await Assert.That(unionIncludingUnconstrainedDestination.Allows(decisionBucket)).IsEqualTo(ReferenceTemperatureEligibilityModel
                    .AnyDestinationAllowsTemperature(
                        representativeConstraints,
                        representativeTemperatureKelvin)).Because($"Unconstrained-destination interval union differed at " +
                $"canonical bucket {bucketOrdinal}.");
        }
    }

    [Test]
    public async Task WorldInventoryPublicationModes_WhenBothAreComplete_ReturnEqualTemperatureConstrainedTotals()
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
            await Assert.That(completeWorldCatalog.PublishCompleteWorldResourceAmounts(
                    worldId,
                    CompleteWorldPublication(generation, worldPublication))).IsTrue();
            await Assert.That(completeWorldReference.TryPublishCompleteWorld(
                    worldId,
                    generation,
                    worldPublication)).IsTrue();

            Tag[] presentResourceTags = worldPublication
                .Select(resourceSeries => resourceSeries.ResourceTag)
                .ToArray();
            await Assert.That(incrementalCatalog.PublishWorldResourceTagCoverage(
                    worldId,
                    WorldResourceTagCoverage.Create(
                        generation,
                        presentResourceTags))).IsTrue();
            await Assert.That(incrementalReference.TryPublishResourceTagCoverage(
                    worldId,
                    generation,
                    presentResourceTags)).IsTrue();
            foreach (ReferenceWorldResourceTemperatureAmountSeries
                         resourceSeries in worldPublication)
            {
                await Assert.That(incrementalCatalog.PublishWorldResourceTemperatureSeries(
                        worldId,
                        ResourceTagTemperatureSeriesPublication(
                            generation,
                            resourceSeries))).IsTrue();
                await Assert.That(incrementalReference
                        .TryPublishResourceTagTemperatureAmounts(
                            worldId,
                            generation,
                            resourceSeries.ResourceTag,
                            resourceSeries.TemperatureAmounts)).IsTrue();
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

                await AssertAvailabilityMatchesReference(
                    completeWorldExpected,
                    completeWorldResult,
                    "Complete-world publication differed from its independent " +
                    "reference.");
                await AssertAvailabilityMatchesReference(
                    incrementalExpected,
                    incrementalResult,
                    "Coverage/single-tag publication differed from its " +
                    "independent reference.");
                await AssertEquivalentAvailability(
                    completeWorldResult,
                    incrementalResult,
                    "Complete-world and coverage/single-tag modes produced " +
                    "different complete evidence.");
            }
        }
    }

    [Test]
    public async Task WorldInventoryPublications_WhenTenThousandSeededOperationsRun_MatchIndependentCoverageAndPendingModel()
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
            await ApplyWorldInventoryStateMachineOperation(
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
            await AssertAvailabilityMatchesReference(
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

    [Test]
    public async Task Classify_WhenCurrentSnapshotCoversEveryDecisionBucket_ProducesSoundMinimalScopedClasses()
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

            await Assert.That(classification.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval);
            if (firstVectorByClassification.TryGetValue(
                    classification,
                    out var firstVector))
            {
                await AssertAllowanceVectorsEqual(
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
                    await Assert.That(classification).IsNotEqualTo(previousClassification).Because("Different adjacent direct eligibility vectors shared " +
                        $"one class at bucket {bucketOrdinal}.");
                }
                else if (!relevantEndpoints.Contains(
                             ReferenceTemperatureEligibilityModel
                                 .GetRepresentativeTruncatedKelvin(
                                     bucketOrdinal)))
                {
                    await Assert.That(classification).IsEqualTo(previousClassification).Because("Equivalent adjacent buckets without a relevant endpoint " +
                        $"were fragmented at bucket {bucketOrdinal}.");
                }
            }

            previousVector = referenceVector;
            previousClassification = classification;
        }
    }

    [Test]
    public async Task Classify_WhenSnapshotIsStaleAcrossEveryDecisionBucket_UsesOnlyExactBucketIdentity()
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

            await Assert.That(classification.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind
                    .ExactTemperatureDecisionBucket);
            await Assert.That(classification.ExactTemperatureDecisionBucket).IsEqualTo(expectedBucket);
            if (bucketByClassification.TryGetValue(
                    classification,
                    out var firstBucket))
            {
                await Assert.That(firstBucket).IsEqualTo(expectedBucket).Because("A stale-snapshot fallback key represented more than one " +
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
        await Assert.That(secondSameBucketClassification).IsEqualTo(firstSameBucketClassification).Because("Temperatures in the same canonical exact bucket must retain one " +
            "fallback identity.");
    }

    [Test]
    public async Task Classify_WhenAnotherParentHasOneThousandEndpoints_DoesNotFragmentScopedParent()
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

        await Assert.That(ReferenceTemperatureEligibilityModel
                .CreateSortedPickupDecisionEndpointUnion(
                    requests,
                    parentWorldId: 2,
                    applicableRequestedTags: [food])).Count().IsEqualTo(1000);
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
            await Assert.That(classification.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval);
            parentOneClassifications.Add(classification);
        }

        await Assert.That(parentOneClassifications).Count().IsEqualTo(3).Because("Parent 1/Iron must be partitioned only by endpoints 10 and 20; " +
            "parent 2/Food endpoints are unrelated.");
    }

    private static async Task ApplyWorldInventoryStateMachineOperation(
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
                await Assert.That(observedAccepted).IsEqualTo(expectedAccepted).Because(WorldInventoryStateMachineDiagnostic(
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
                await Assert.That(observedAccepted).IsEqualTo(expectedAccepted).Because(WorldInventoryStateMachineDiagnostic(
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
                await Assert.That(observedAccepted).IsEqualTo(expectedAccepted).Because(WorldInventoryStateMachineDiagnostic(
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

    private static async Task AssertAvailabilityMatchesReference(
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
        await Assert.That(actual.State).IsEqualTo(expectedProductionState).Because(failureReason);
        if (expected.State ==
            ReferenceWorldResourceTemperatureAmountAvailabilityState.Complete)
        {
            await Assert.That(expected.TryGetCompleteAmount(out float expectedAmount)).IsTrue();
            await Assert.That(actual.TryGetCompleteAvailableAmount(out float actualAmount)).IsTrue().Because(failureReason);
            await Assert.That(actualAmount).IsEqualTo(expectedAmount).Because(failureReason);
        }
        else
        {
            await Assert.That(actual.TryGetCompleteAvailableAmount(out _)).IsFalse();
        }
    }

    private static async Task AssertEquivalentAvailability(
        TemperatureConstrainedAmountAvailability expected,
        TemperatureConstrainedAmountAvailability actual,
        string failureReason)
    {
        await Assert.That(actual.State).IsEqualTo(expected.State).Because(failureReason);
        bool expectedHasAmount =
            expected.TryGetCompleteAvailableAmount(out float expectedAmount);
        bool actualHasAmount =
            actual.TryGetCompleteAvailableAmount(out float actualAmount);
        await Assert.That(actualHasAmount).IsEqualTo(expectedHasAmount).Because(failureReason);
        if (expectedHasAmount)
        {
            await Assert.That(actualAmount).IsEqualTo(expectedAmount).Because(failureReason);
        }
    }

    private static async Task AssertAllowanceVectorsEqual(
        IReadOnlyList<bool> expected,
        IReadOnlyList<bool> actual,
        int bucketOrdinal,
        string failureReason)
    {
        await Assert.That(actual.Count).IsEqualTo(expected.Count).Because($"{failureReason} Bucket {bucketOrdinal} had a length mismatch.");
        for (var constraintIndex = 0;
             constraintIndex < expected.Count;
             constraintIndex++)
        {
            await Assert.That(actual[constraintIndex]).IsEqualTo(expected[constraintIndex]).Because($"{failureReason} Bucket {bucketOrdinal}, constraint " +
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
