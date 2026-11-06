using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
public sealed class TemperaturePartitionDefinitionTests
{
    [Test]
    public async Task TemperatureEligibilityClassKey_FactoriesPopulateOnlyKindSpecificFields()
    {
        var exactBucket = TemperatureDecisionBucket.FromIntegerKelvin(273);

        await AssertKeyFields(
            TemperatureEligibilityClassKey.NoTemperatureDistinction(),
            TemperatureEligibilityClassificationKind.NoTemperatureDistinction,
            expectedPartitionDefinitionId: 0,
            expectedIntervalOrdinal: 0,
            expectedExactBucket: default);
        await AssertKeyFields(
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                partitionDefinitionId: 7,
                intervalOrdinal: 3),
            TemperatureEligibilityClassificationKind.OptimizedPartitionInterval,
            expectedPartitionDefinitionId: 7,
            expectedIntervalOrdinal: 3,
            expectedExactBucket: default);
        await AssertKeyFields(
            TemperatureEligibilityClassKey.ExactDecisionBucket(exactBucket),
            TemperatureEligibilityClassificationKind.ExactTemperatureDecisionBucket,
            expectedPartitionDefinitionId: 0,
            expectedIntervalOrdinal: 0,
            expectedExactBucket: exactBucket);
        await AssertKeyFields(
            TemperatureEligibilityClassKey.MissingPrimaryElement(),
            TemperatureEligibilityClassificationKind.MissingPrimaryElement,
            expectedPartitionDefinitionId: 0,
            expectedIntervalOrdinal: 0,
            expectedExactBucket: default);
    }

    [Test]
    public async Task TemperatureEligibilityClassKey_WhenOptimizedIdentityIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() =>
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                partitionDefinitionId: 0,
                intervalOrdinal: 0)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() =>
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                partitionDefinitionId: 1,
                intervalOrdinal: -1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Classify_WhenEndpointsAreTenAndTwenty_ChangesClassAtEachEndpoint()
    {
        var definition = TemperaturePartitionDefinition.Create(7, [10, 20]);

        await Assert.That(definition.Classify(Bucket(-1))).IsEqualTo(0);
        await Assert.That(definition.Classify(Bucket(9))).IsEqualTo(0);
        await Assert.That(definition.Classify(Bucket(10))).IsEqualTo(1);
        await Assert.That(definition.Classify(Bucket(19))).IsEqualTo(1);
        await Assert.That(definition.Classify(Bucket(20))).IsEqualTo(2);
        await Assert.That(definition.Classify(Bucket(10000))).IsEqualTo(2);
    }

    [Test]
    public async Task Create_WhenEndpointsAreUnsortedAndDuplicated_NormalizesThem()
    {
        var definition = TemperaturePartitionDefinition.Create(
            definitionId: 11,
            decisionEndpointsKelvin: new[] { 40, 10, 20, 10, 40, 30 });

        await Assert.That(definition.SortedDecisionEndpointsKelvin).IsEquivalentTo(new[] { 10, 20, 30, 40 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Create_WhenEndpointIsZero_SeparatesBelowRangeFromZero()
    {
        var definition = TemperaturePartitionDefinition.Create(1, [0]);

        await Assert.That(definition.Classify(Bucket(-1))).IsEqualTo(0);
        await Assert.That(definition.Classify(Bucket(0))).IsEqualTo(1);
    }

    [Test]
    public async Task Create_WhenEndpointIsOniMaximum_Separates9999FromAtOrAboveMaximum()
    {
        var definition = TemperaturePartitionDefinition.Create(
            1,
            [OniStorableTemperatureBounds.MaximumTemperatureKelvin]);

        await Assert.That(definition.Classify(Bucket(
                OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1))).IsEqualTo(0);
        await Assert.That(definition.Classify(Bucket(
                OniStorableTemperatureBounds.MaximumTemperatureKelvin))).IsEqualTo(1);
    }

    [Test]
    public async Task Create_WhenNoEndpoints_ThrowsArgumentException()
    {
        var exception = (await Assert.That(() =>
            TemperaturePartitionDefinition.Create(1, Array.Empty<int>())).ThrowsExactly<ArgumentException>())!;

        await Assert.That(exception.Message).Contains("decisionEndpointsKelvin");
    }

    [Test]
    public async Task Create_WhenEndpointIsOutsideConfigurableRange_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() =>
            TemperaturePartitionDefinition.Create(1, [-1])).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() =>
            TemperaturePartitionDefinition.Create(
                1,
                [OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1])).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Classify_WhenInputIsEveryDecisionBucket_ReturnsMonotonicOrdinals()
    {
        var endpoints = new[]
        {
            0,
            1,
            10,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin
        };
        var definition = TemperaturePartitionDefinition.Create(1, endpoints);
        var previousIntervalOrdinal = -1;

        for (var bucketOrdinal = 0;
             bucketOrdinal < TemperatureDecisionBucket.BucketCount;
             bucketOrdinal++)
        {
            var bucket = BucketFromOrdinal(bucketOrdinal);
            var representativeKelvin = RepresentativeTemperatureKelvin(bucketOrdinal);
            var expectedIntervalOrdinal = endpoints.Count(endpoint =>
                endpoint <= representativeKelvin);
            var observedIntervalOrdinal = definition.Classify(bucket);

            await Assert.That(observedIntervalOrdinal).IsEqualTo(expectedIntervalOrdinal).Because("Unexpected interval ordinal for decision bucket " + bucketOrdinal + ".");
            await Assert.That(previousIntervalOrdinal <= observedIntervalOrdinal).IsTrue().Because("Partition ordinals must be monotonic across decision buckets.");
            previousIntervalOrdinal = observedIntervalOrdinal;
        }
    }

    [Test]
    public async Task TemperatureEligibilityClassKey_WhenOrdinalsMatchButDefinitionsDiffer_IsNotEqual()
    {
        var first = TemperatureEligibilityClassKey.OptimizedPartitionInterval(1, 2);
        var second = TemperatureEligibilityClassKey.OptimizedPartitionInterval(2, 2);

        await Assert.That(second).IsNotEqualTo(first);
        await Assert.That(first.CompareTo(second)).IsNotEqualTo(0);
    }

    [Test]
    public async Task TemperatureEligibilityClassKey_WhenDefinitionAndOrdinalMatch_IsEqual()
    {
        var first = TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2);
        var second = TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second.GetHashCode()).IsEqualTo(first.GetHashCode());
        await Assert.That(first.CompareTo(second)).IsEqualTo(0);
    }

    [Test]
    public async Task TemperatureEligibilityClassKey_CompareTo_UsesKindThenKindSpecificIdentity()
    {
        var noDistinction =
            TemperatureEligibilityClassKey.NoTemperatureDistinction();
        var firstOptimized =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(1, 4);
        var laterDefinition =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(2, 0);
        var laterInterval =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(2, 1);
        var firstExact = TemperatureEligibilityClassKey.ExactDecisionBucket(Bucket(10));
        var laterExact = TemperatureEligibilityClassKey.ExactDecisionBucket(Bucket(20));
        var missing = TemperatureEligibilityClassKey.MissingPrimaryElement();

        await Assert.That(noDistinction.CompareTo(firstOptimized) < 0).IsTrue();
        await Assert.That(firstOptimized.CompareTo(laterDefinition) < 0).IsTrue();
        await Assert.That(laterDefinition.CompareTo(laterInterval) < 0).IsTrue();
        await Assert.That(laterInterval.CompareTo(firstExact) < 0).IsTrue();
        await Assert.That(firstExact.CompareTo(laterExact) < 0).IsTrue();
        await Assert.That(laterExact.CompareTo(missing) < 0).IsTrue();
    }

    [Test]
    public async Task ExactFallback_WhenPrimaryElementIsMissing_UsesDedicatedNonTemperatureClassification()
    {
        var missing = TemperatureEligibilityClassKey.MissingPrimaryElement();
        var belowRange = TemperatureEligibilityClassKey.ExactDecisionBucket(Bucket(-1));

        await Assert.That(missing.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind.MissingPrimaryElement);
        await Assert.That(belowRange).IsNotEqualTo(missing);
        await Assert.That(missing.PartitionDefinitionId).IsEqualTo(0);
        await Assert.That(missing.IntervalOrdinal).IsEqualTo(0);
        await Assert.That(missing.ExactTemperatureDecisionBucket).IsEqualTo(default);
    }

    [Test]
    public async Task PickupTagIdentity_WhenHashesMatchButPrefabTagsDiffer_IsNotEqual()
    {
        var iron = new PickupTagIdentity(42, new Tag("Iron"));
        var copper = new PickupTagIdentity(42, new Tag("Copper"));

        await Assert.That(copper).IsNotEqualTo(iron);
    }

    [Test]
    public async Task PickupTagIdentity_WhenHashAndPrefabTagMatch_IsEqual()
    {
        var first = new PickupTagIdentity(42, new Tag("Iron"));
        var second = new PickupTagIdentity(42, new Tag("Iron"));

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second.GetHashCode()).IsEqualTo(first.GetHashCode());
    }

    [Test]
    public async Task Classify_WhenEveryDecisionBucketIsComparedWithIndependentVectors_IsSoundAndMinimallyFragmented()
    {
        var constraints = new[] { Constraint(10, 20), Constraint(30, 40) };
        var definition = TemperaturePartitionDefinition.Create(
            definitionId: 1,
            decisionEndpointsKelvin: new[] { 10, 20, 30, 40 });
        var firstVectorByIntervalOrdinal = new Dictionary<int, bool[]>();
        bool[]? previousVector = null;
        var previousIntervalOrdinal = -1;

        for (var bucketOrdinal = 0;
             bucketOrdinal < TemperatureDecisionBucket.BucketCount;
             bucketOrdinal++)
        {
            var bucket = BucketFromOrdinal(bucketOrdinal);
            var temperatureKelvin = RepresentativeTemperatureKelvin(bucketOrdinal);
            var referenceVector =
                ReferenceTemperatureEligibilityModel
                    .EvaluateDestinationConstraintAllowances(
                        constraints,
                        temperatureKelvin);
            var intervalOrdinal = definition.Classify(bucket);

            if (firstVectorByIntervalOrdinal.TryGetValue(
                    intervalOrdinal,
                    out var firstVector))
            {
                await Assert.That(VectorsAreEqual(firstVector, referenceVector)).IsTrue().Because("A partition class combined eligibility-distinct buckets at " +
                    "decision bucket " + bucketOrdinal + ".");
            }
            else
            {
                firstVectorByIntervalOrdinal.Add(intervalOrdinal, referenceVector);
            }

            if (previousVector is not null)
            {
                var referenceVectorChanged =
                    !VectorsAreEqual(previousVector, referenceVector);
                var partitionClassChanged =
                    previousIntervalOrdinal != intervalOrdinal;
                await Assert.That(partitionClassChanged).IsEqualTo(referenceVectorChanged).Because("Partition fragmentation disagreed with the direct eligibility " +
                    "vector at decision bucket " + bucketOrdinal + ".");
            }

            previousVector = referenceVector;
            previousIntervalOrdinal = intervalOrdinal;
        }
    }

    [Test]
    public async Task Create_WhenCallerMutatesEndpointSequence_DefinitionRemainsImmutable()
    {
        var endpoints = new List<int> { 20, 10, 20 };
        var definition = TemperaturePartitionDefinition.Create(3, endpoints);

        endpoints[0] = 9000;
        endpoints.Clear();

        await Assert.That(definition.SortedDecisionEndpointsKelvin).IsEquivalentTo(new[] { 10, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(definition.SortedDecisionEndpointsKelvin is int[]).IsFalse();
        await Assert.That(definition.SortedDecisionEndpointsKelvin is ICollection<int> mutable &&
            !mutable.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task Create_WhenDefinitionIdIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() =>
            TemperaturePartitionDefinition.Create(0, [10])).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() =>
            TemperaturePartitionDefinition.Create(-1, [10])).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Create_WhenEndpointSequencesMatch_PreservesCallerSuppliedDefinitionIds()
    {
        var first = TemperaturePartitionDefinition.Create(7, [10, 20]);
        var second = TemperaturePartitionDefinition.Create(8, [10, 20]);

        await Assert.That(first.DefinitionId).IsEqualTo(7);
        await Assert.That(second.DefinitionId).IsEqualTo(8);
        await Assert.That(second.SortedDecisionEndpointsKelvin).IsEquivalentTo(first.SortedDecisionEndpointsKelvin, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static async Task AssertKeyFields(
        TemperatureEligibilityClassKey key,
        TemperatureEligibilityClassificationKind expectedKind,
        int expectedPartitionDefinitionId,
        int expectedIntervalOrdinal,
        TemperatureDecisionBucket expectedExactBucket)
    {
        await Assert.That(key.ClassificationKind).IsEqualTo(expectedKind);
        await Assert.That(key.PartitionDefinitionId).IsEqualTo(expectedPartitionDefinitionId);
        await Assert.That(key.IntervalOrdinal).IsEqualTo(expectedIntervalOrdinal);
        await Assert.That(key.ExactTemperatureDecisionBucket).IsEqualTo(expectedExactBucket);
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

    private static TemperatureDecisionBucket BucketFromOrdinal(int bucketOrdinal)
    {
        if (bucketOrdinal == TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)
        {
            return Bucket(-1);
        }

        if (bucketOrdinal ==
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
        {
            return Bucket(OniStorableTemperatureBounds.MaximumTemperatureKelvin);
        }

        return Bucket(
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

    private static TemperatureDecisionBucket Bucket(int integerKelvin) =>
        TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);
}
