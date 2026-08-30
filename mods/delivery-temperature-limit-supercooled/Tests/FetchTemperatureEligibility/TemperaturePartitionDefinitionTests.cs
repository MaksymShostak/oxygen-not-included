using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class TemperaturePartitionDefinitionTests
{
    [TestMethod]
    public void TemperatureEligibilityClassKey_FactoriesPopulateOnlyKindSpecificFields()
    {
        var exactBucket = TemperatureDecisionBucket.FromIntegerKelvin(273);

        AssertKeyFields(
            TemperatureEligibilityClassKey.NoTemperatureDistinction(),
            TemperatureEligibilityClassificationKind.NoTemperatureDistinction,
            expectedPartitionDefinitionId: 0,
            expectedIntervalOrdinal: 0,
            expectedExactBucket: default);
        AssertKeyFields(
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                partitionDefinitionId: 7,
                intervalOrdinal: 3),
            TemperatureEligibilityClassificationKind.OptimizedPartitionInterval,
            expectedPartitionDefinitionId: 7,
            expectedIntervalOrdinal: 3,
            expectedExactBucket: default);
        AssertKeyFields(
            TemperatureEligibilityClassKey.ExactDecisionBucket(exactBucket),
            TemperatureEligibilityClassificationKind.ExactTemperatureDecisionBucket,
            expectedPartitionDefinitionId: 0,
            expectedIntervalOrdinal: 0,
            expectedExactBucket: exactBucket);
        AssertKeyFields(
            TemperatureEligibilityClassKey.MissingPrimaryElement(),
            TemperatureEligibilityClassificationKind.MissingPrimaryElement,
            expectedPartitionDefinitionId: 0,
            expectedIntervalOrdinal: 0,
            expectedExactBucket: default);
    }

    [TestMethod]
    public void TemperatureEligibilityClassKey_WhenOptimizedIdentityIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                partitionDefinitionId: 0,
                intervalOrdinal: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                partitionDefinitionId: 1,
                intervalOrdinal: -1));
    }

    [TestMethod]
    public void Classify_WhenEndpointsAreTenAndTwenty_ChangesClassAtEachEndpoint()
    {
        var definition = TemperaturePartitionDefinition.Create(7, [10, 20]);

        Assert.AreEqual(0, definition.Classify(Bucket(-1)));
        Assert.AreEqual(0, definition.Classify(Bucket(9)));
        Assert.AreEqual(1, definition.Classify(Bucket(10)));
        Assert.AreEqual(1, definition.Classify(Bucket(19)));
        Assert.AreEqual(2, definition.Classify(Bucket(20)));
        Assert.AreEqual(2, definition.Classify(Bucket(10000)));
    }

    [TestMethod]
    public void Create_WhenEndpointsAreUnsortedAndDuplicated_NormalizesThem()
    {
        var definition = TemperaturePartitionDefinition.Create(
            definitionId: 11,
            decisionEndpointsKelvin: new[] { 40, 10, 20, 10, 40, 30 });

        Assert.AreSequenceEqual(
            new[] { 10, 20, 30, 40 },
            definition.SortedDecisionEndpointsKelvin);
    }

    [TestMethod]
    public void Create_WhenEndpointIsZero_SeparatesBelowRangeFromZero()
    {
        var definition = TemperaturePartitionDefinition.Create(1, [0]);

        Assert.AreEqual(0, definition.Classify(Bucket(-1)));
        Assert.AreEqual(1, definition.Classify(Bucket(0)));
    }

    [TestMethod]
    public void Create_WhenEndpointIsOniMaximum_Separates9999FromAtOrAboveMaximum()
    {
        var definition = TemperaturePartitionDefinition.Create(
            1,
            [OniStorableTemperatureBounds.MaximumTemperatureKelvin]);

        Assert.AreEqual(
            0,
            definition.Classify(Bucket(
                OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1)));
        Assert.AreEqual(
            1,
            definition.Classify(Bucket(
                OniStorableTemperatureBounds.MaximumTemperatureKelvin)));
    }

    [TestMethod]
    public void Create_WhenNoEndpoints_ThrowsArgumentException()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            TemperaturePartitionDefinition.Create(1, Array.Empty<int>()));

        StringAssert.Contains(exception.Message, "decisionEndpointsKelvin");
    }

    [TestMethod]
    public void Create_WhenEndpointIsOutsideConfigurableRange_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperaturePartitionDefinition.Create(1, [-1]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperaturePartitionDefinition.Create(
                1,
                [OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1]));
    }

    [TestMethod]
    public void Classify_WhenInputIsEveryDecisionBucket_ReturnsMonotonicOrdinals()
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

            Assert.AreEqual(
                expectedIntervalOrdinal,
                observedIntervalOrdinal,
                "Unexpected interval ordinal for decision bucket " + bucketOrdinal + ".");
            Assert.IsTrue(
                previousIntervalOrdinal <= observedIntervalOrdinal,
                "Partition ordinals must be monotonic across decision buckets.");
            previousIntervalOrdinal = observedIntervalOrdinal;
        }
    }

    [TestMethod]
    public void TemperatureEligibilityClassKey_WhenOrdinalsMatchButDefinitionsDiffer_IsNotEqual()
    {
        var first = TemperatureEligibilityClassKey.OptimizedPartitionInterval(1, 2);
        var second = TemperatureEligibilityClassKey.OptimizedPartitionInterval(2, 2);

        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(0, first.CompareTo(second));
    }

    [TestMethod]
    public void TemperatureEligibilityClassKey_WhenDefinitionAndOrdinalMatch_IsEqual()
    {
        var first = TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2);
        var second = TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreEqual(0, first.CompareTo(second));
    }

    [TestMethod]
    public void TemperatureEligibilityClassKey_CompareTo_UsesKindThenKindSpecificIdentity()
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

        Assert.IsTrue(noDistinction.CompareTo(firstOptimized) < 0);
        Assert.IsTrue(firstOptimized.CompareTo(laterDefinition) < 0);
        Assert.IsTrue(laterDefinition.CompareTo(laterInterval) < 0);
        Assert.IsTrue(laterInterval.CompareTo(firstExact) < 0);
        Assert.IsTrue(firstExact.CompareTo(laterExact) < 0);
        Assert.IsTrue(laterExact.CompareTo(missing) < 0);
    }

    [TestMethod]
    public void ExactFallback_WhenPrimaryElementIsMissing_UsesDedicatedNonTemperatureClassification()
    {
        var missing = TemperatureEligibilityClassKey.MissingPrimaryElement();
        var belowRange = TemperatureEligibilityClassKey.ExactDecisionBucket(Bucket(-1));

        Assert.AreEqual(
            TemperatureEligibilityClassificationKind.MissingPrimaryElement,
            missing.ClassificationKind);
        Assert.AreNotEqual(missing, belowRange);
        Assert.AreEqual(0, missing.PartitionDefinitionId);
        Assert.AreEqual(0, missing.IntervalOrdinal);
        Assert.AreEqual(default, missing.ExactTemperatureDecisionBucket);
    }

    [TestMethod]
    public void PickupTagIdentity_WhenHashesMatchButPrefabTagsDiffer_IsNotEqual()
    {
        var iron = new PickupTagIdentity(42, new Tag("Iron"));
        var copper = new PickupTagIdentity(42, new Tag("Copper"));

        Assert.AreNotEqual(iron, copper);
    }

    [TestMethod]
    public void PickupTagIdentity_WhenHashAndPrefabTagMatch_IsEqual()
    {
        var first = new PickupTagIdentity(42, new Tag("Iron"));
        var second = new PickupTagIdentity(42, new Tag("Iron"));

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Classify_WhenEveryDecisionBucketIsComparedWithIndependentVectors_IsSoundAndMinimallyFragmented()
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
                Assert.IsTrue(
                    VectorsAreEqual(firstVector, referenceVector),
                    "A partition class combined eligibility-distinct buckets at " +
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
                Assert.AreEqual(
                    referenceVectorChanged,
                    partitionClassChanged,
                    "Partition fragmentation disagreed with the direct eligibility " +
                    "vector at decision bucket " + bucketOrdinal + ".");
            }

            previousVector = referenceVector;
            previousIntervalOrdinal = intervalOrdinal;
        }
    }

    [TestMethod]
    public void Create_WhenCallerMutatesEndpointSequence_DefinitionRemainsImmutable()
    {
        var endpoints = new List<int> { 20, 10, 20 };
        var definition = TemperaturePartitionDefinition.Create(3, endpoints);

        endpoints[0] = 9000;
        endpoints.Clear();

        Assert.AreSequenceEqual(
            new[] { 10, 20 },
            definition.SortedDecisionEndpointsKelvin);
        Assert.IsFalse(definition.SortedDecisionEndpointsKelvin is int[]);
        Assert.IsFalse(
            definition.SortedDecisionEndpointsKelvin is ICollection<int> mutable &&
            !mutable.IsReadOnly);
    }

    [TestMethod]
    public void Create_WhenDefinitionIdIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperaturePartitionDefinition.Create(0, [10]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperaturePartitionDefinition.Create(-1, [10]));
    }

    [TestMethod]
    public void Create_WhenEndpointSequencesMatch_PreservesCallerSuppliedDefinitionIds()
    {
        var first = TemperaturePartitionDefinition.Create(7, [10, 20]);
        var second = TemperaturePartitionDefinition.Create(8, [10, 20]);

        Assert.AreEqual(7, first.DefinitionId);
        Assert.AreEqual(8, second.DefinitionId);
        Assert.AreSequenceEqual(
            first.SortedDecisionEndpointsKelvin,
            second.SortedDecisionEndpointsKelvin);
    }

    private static void AssertKeyFields(
        TemperatureEligibilityClassKey key,
        TemperatureEligibilityClassificationKind expectedKind,
        int expectedPartitionDefinitionId,
        int expectedIntervalOrdinal,
        TemperatureDecisionBucket expectedExactBucket)
    {
        Assert.AreEqual(expectedKind, key.ClassificationKind);
        Assert.AreEqual(
            expectedPartitionDefinitionId,
            key.PartitionDefinitionId);
        Assert.AreEqual(expectedIntervalOrdinal, key.IntervalOrdinal);
        Assert.AreEqual(
            expectedExactBucket,
            key.ExactTemperatureDecisionBucket);
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
