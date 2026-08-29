using System.Reflection;
using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class AllowedTemperatureIntervalSetTests
{
    private const int RandomizedDestinationSetSeed = 0x1A7E2A1;
    private const int RandomizedDestinationSetCount = 5000;

    [TestMethod]
    public void CreateFromDestinations_WhenIntervalsOverlapOrTouch_MergesThem()
    {
        var set = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: false,
            [Constraint(10, 20), Constraint(15, 30), Constraint(30, 40)]);

        Assert.AreSequenceEqual(
            new[] { new AllowedTemperatureInterval(10, 40) },
            set.Intervals.ToArray());
    }

    [TestMethod]
    public void CreateFromDestinations_WhenNoDestinationContributes_ReturnsAllowsNoTemperature()
    {
        var emptyDestinationSet =
            AllowedTemperatureIntervalSet.CreateFromDestinations(
                includesUnconstrainedDestination: false,
                Array.Empty<DeliveryTemperatureConstraint>());
        var onlyEmptyConstraints =
            AllowedTemperatureIntervalSet.CreateFromDestinations(
                includesUnconstrainedDestination: false,
                new[] { Constraint(10, 10), Constraint(20, 10) });

        Assert.IsTrue(emptyDestinationSet.AllowsNoTemperature);
        Assert.IsFalse(emptyDestinationSet.AllowsEveryTemperature);
        Assert.IsEmpty(emptyDestinationSet.Intervals);
        Assert.AreSame(emptyDestinationSet, onlyEmptyConstraints);
    }

    [TestMethod]
    public void CreateFromDestinations_WhenUnconstrainedDestinationExists_ReturnsAllowsEveryTemperature()
    {
        var first = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: true,
            new[] { Constraint(10, 20) });
        var second = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: true,
            Array.Empty<DeliveryTemperatureConstraint>());

        Assert.IsFalse(first.AllowsNoTemperature);
        Assert.IsTrue(first.AllowsEveryTemperature);
        Assert.IsEmpty(first.Intervals);
        Assert.AreSame(first, second);

        var intervalsField = typeof(AllowedTemperatureIntervalSet).GetField(
            "intervals",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(intervalsField);
        Assert.IsNull(
            intervalsField.GetValue(first),
            "AllowsEveryTemperature must carry no finite interval array.");
    }

    [TestMethod]
    public void CreateFromDestinations_WhenDisabledConstraintIsSupplied_ThrowsArgumentException()
    {
        var disabledConstraint = Constraint(100, 0);

        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            AllowedTemperatureIntervalSet.CreateFromDestinations(
                includesUnconstrainedDestination: false,
                new[] { disabledConstraint }));

        StringAssert.Contains(
            exception.Message,
            "enabledDestinationConstraints");
    }

    [TestMethod]
    public void CreateFromDestinations_WhenEnabledConstraintIsEmpty_IgnoresIt()
    {
        var set = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: false,
            new[]
            {
                Constraint(10, 10),
                Constraint(20, 10),
                Constraint(30, 40)
            });

        Assert.AreSequenceEqual(
            new[] { new AllowedTemperatureInterval(30, 40) },
            set.Intervals);
    }

    [TestMethod]
    public void CreateFromDestinations_WhenIntervalsDuplicate_CollapsesThem()
    {
        var set = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: false,
            new[]
            {
                Constraint(10, 20),
                Constraint(10, 20),
                Constraint(10, 20)
            });

        Assert.AreSequenceEqual(
            new[] { new AllowedTemperatureInterval(10, 20) },
            set.Intervals);
    }

    [TestMethod]
    public void CreateFromDestinations_WhenIntervalsAreDisjoint_SortsThem()
    {
        var set = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: false,
            new[]
            {
                Constraint(5000, 6000),
                Constraint(10, 20),
                Constraint(100, 200)
            });

        Assert.AreSequenceEqual(
            new[]
            {
                new AllowedTemperatureInterval(10, 20),
                new AllowedTemperatureInterval(100, 200),
                new AllowedTemperatureInterval(5000, 6000)
            },
            set.Intervals);
    }

    [TestMethod]
    public void Allows_WhenBucketIsAtInclusiveMinimum_ReturnsTrue()
    {
        var set = FiniteSet(Constraint(10, 20));

        Assert.IsTrue(set.Allows(
            TemperatureDecisionBucket.FromIntegerKelvin(10)));
    }

    [TestMethod]
    public void Allows_WhenBucketIsAtExclusiveMaximum_ReturnsFalse()
    {
        var set = FiniteSet(Constraint(10, 20));

        Assert.IsFalse(set.Allows(
            TemperatureDecisionBucket.FromIntegerKelvin(20)));
    }

    [TestMethod]
    public void Allows_WhenBucketIsBelowMinimumKelvin_ReturnsFalseUnlessAllowsEvery()
    {
        var belowMinimumBucket =
            TemperatureDecisionBucket.FromIntegerKelvin(-1);
        var finite = FiniteSet(Constraint(0, 10000));
        var every = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: true,
            Array.Empty<DeliveryTemperatureConstraint>());

        Assert.IsFalse(finite.Allows(belowMinimumBucket));
        Assert.IsTrue(every.Allows(belowMinimumBucket));
    }

    [TestMethod]
    public void Allows_WhenBucketIsAtOrAboveMaximumKelvin_ReturnsFalseUnlessAllowsEvery()
    {
        var atOrAboveMaximumBucket =
            TemperatureDecisionBucket.FromIntegerKelvin(10000);
        var finite = FiniteSet(Constraint(0, 10000));
        var every = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: true,
            Array.Empty<DeliveryTemperatureConstraint>());

        Assert.IsFalse(finite.Allows(atOrAboveMaximumBucket));
        Assert.IsTrue(every.Allows(atOrAboveMaximumBucket));
    }

    [TestMethod]
    public void PublishedIntervals_WhenInputListChanges_RemainImmutable()
    {
        var sourceConstraints = new List<DeliveryTemperatureConstraint>
        {
            Constraint(10, 20),
            Constraint(30, 40)
        };
        var set = AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: false,
            sourceConstraints);

        sourceConstraints[0] = Constraint(100, 200);
        sourceConstraints.Clear();

        Assert.AreSequenceEqual(
            new[]
            {
                new AllowedTemperatureInterval(10, 20),
                new AllowedTemperatureInterval(30, 40)
            },
            set.Intervals);
        Assert.IsFalse(set.Intervals is AllowedTemperatureInterval[]);
        Assert.IsFalse(
            set.Intervals is ICollection<AllowedTemperatureInterval> mutable &&
            !mutable.IsReadOnly);
    }

    [TestMethod]
    public void Allows_WhenFiveThousandSeededDestinationSetsComparedAcrossEveryDecisionBucket_MatchesReferenceModel()
    {
        var random = new Random(RandomizedDestinationSetSeed);
        for (var destinationSetIndex = 0;
             destinationSetIndex < RandomizedDestinationSetCount;
             destinationSetIndex++)
        {
            var logicalDestinationConstraints =
                CreateRandomLogicalDestinationConstraints(random);
            var includesUnconstrainedDestination =
                logicalDestinationConstraints.Any(constraint =>
                    !constraint.IsEnabled);
            var enabledDestinationConstraints =
                logicalDestinationConstraints
                    .Where(constraint => constraint.IsEnabled)
                    .ToArray();
            var intervalSet =
                AllowedTemperatureIntervalSet.CreateFromDestinations(
                    includesUnconstrainedDestination,
                    enabledDestinationConstraints);

            for (var bucketOrdinal = 0;
                 bucketOrdinal < TemperatureDecisionBucket.BucketCount;
                 bucketOrdinal++)
            {
                var representativeTemperatureKelvin =
                    RepresentativeTemperatureKelvin(bucketOrdinal);
                var bucket = TemperatureDecisionBucket.FromTemperature(
                    representativeTemperatureKelvin);
                var expected =
                    ReferenceTemperatureEligibilityModel
                        .AnyDestinationAllowsTemperature(
                            logicalDestinationConstraints,
                            representativeTemperatureKelvin);
                var observed = intervalSet.Allows(bucket);
                if (expected != observed)
                {
                    Assert.Fail(
                        $"Seed=0x{RandomizedDestinationSetSeed:X}; " +
                        $"destination set={destinationSetIndex}; " +
                        $"bucket ordinal={bucketOrdinal}; " +
                        $"temperature={representativeTemperatureKelvin}; " +
                        $"expected={expected}; observed={observed}.");
                }
            }
        }
    }

    [TestMethod]
    public void AllowedTemperatureInterval_WhenBoundsAreInvalid_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new AllowedTemperatureInterval(-1, 20));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new AllowedTemperatureInterval(10, 10001));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new AllowedTemperatureInterval(20, 20));
    }

    private static DeliveryTemperatureConstraint[]
        CreateRandomLogicalDestinationConstraints(Random random)
    {
        var destinationCount = random.Next(9);
        var destinationConstraints =
            new List<DeliveryTemperatureConstraint>(destinationCount);
        var boundaryValues = new[] { 0, 1, 10, 4999, 5000, 5001, 9999, 10000 };
        for (var destinationIndex = 0;
             destinationIndex < destinationCount;
             destinationIndex++)
        {
            switch (random.Next(7))
            {
                case 0:
                    destinationConstraints.Add(Constraint(
                        boundaryValues[random.Next(boundaryValues.Length)],
                        0));
                    break;

                case 1:
                {
                    var boundary =
                        boundaryValues[random.Next(boundaryValues.Length)];
                    var enabledEmptyBoundary = boundary == 0 ? 1 : boundary;
                    destinationConstraints.Add(Constraint(
                        enabledEmptyBoundary,
                        enabledEmptyBoundary));
                    break;
                }

                case 2 when destinationConstraints.Count > 0:
                    destinationConstraints.Add(
                        destinationConstraints[random.Next(
                            destinationConstraints.Count)]);
                    break;

                case 3 when destinationConstraints.Count > 0:
                {
                    var prior = destinationConstraints[
                        destinationConstraints.Count - 1];
                    var adjacentMinimum =
                        prior.MaximumExclusiveKelvin == 0
                            ? 1
                            : prior.MaximumExclusiveKelvin;
                    var adjacentMaximum = Math.Min(
                        OniStorableTemperatureBounds.MaximumTemperatureKelvin,
                        adjacentMinimum + random.Next(1, 101));
                    destinationConstraints.Add(Constraint(
                        adjacentMinimum,
                        adjacentMaximum));
                    break;
                }

                default:
                {
                    var firstBoundary = random.Next(
                        OniStorableTemperatureBounds.MinimumTemperatureKelvin,
                        OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1);
                    var secondBoundary = random.Next(
                        OniStorableTemperatureBounds.MinimumTemperatureKelvin,
                        OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1);
                    destinationConstraints.Add(Constraint(
                        Math.Min(firstBoundary, secondBoundary),
                        Math.Max(firstBoundary, secondBoundary)));
                    break;
                }
            }
        }

        return destinationConstraints.ToArray();
    }

    private static float RepresentativeTemperatureKelvin(int bucketOrdinal)
    {
        if (bucketOrdinal ==
            TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)
        {
            return -1.0f;
        }

        if (bucketOrdinal ==
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
        {
            return OniStorableTemperatureBounds.MaximumTemperatureKelvin;
        }

        return bucketOrdinal -
            TemperatureDecisionBucket.FirstIntegerKelvinOrdinal;
    }

    private static AllowedTemperatureIntervalSet FiniteSet(
        params DeliveryTemperatureConstraint[] constraints) =>
        AllowedTemperatureIntervalSet.CreateFromDestinations(
            includesUnconstrainedDestination: false,
            constraints);

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);
}
