using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;
public sealed class TemperatureAmountSeriesTests
{
    private const int RandomizedReferenceSeed = 0xA60A17;
    private const int RandomizedSeriesCount = 10000;
    private const int MaximumRandomizedAdditionCount = 256;
    private const int RandomizedTemperaturePoolSize = 32;

    [Test]
    public async Task GetAmountAllowedBy_WhenConstraintIsTenThroughTwenty_SumsOnlyTenThroughNineteen()
    {
        var series = Series(
            Amount(-1.0f, 2.0f),
            Amount(9.0f, 3.0f),
            Amount(10.0f, 5.0f),
            Amount(19.0f, 7.0f),
            Amount(20.0f, 11.0f),
            Amount(6203.0f, 13.0f),
            Amount(10000.0f, 17.0f));

        await Assert.That(series.GetAmountAllowedBy(Constraint(10, 20))).IsEqualTo(12.0f);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenConstraintIsDisabled_ReturnsTotalIncludingBelowAndAboveRangeBuckets()
    {
        var series = Series(
            Amount(-10.0f, 2.0f),
            Amount(100.0f, 3.0f),
            Amount(10000.0f, 5.0f));

        var allowedAmount = series.GetAmountAllowedBy(Constraint(400, 0));

        await Assert.That(allowedAmount).IsEqualTo(10.0f);
        await Assert.That(allowedAmount).IsEqualTo(series.TotalAmount);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenConstraintIsEmpty_ReturnsZero()
    {
        var series = Series(
            Amount(-10.0f, 2.0f),
            Amount(100.0f, 3.0f),
            Amount(10000.0f, 5.0f));

        await Assert.That(series.GetAmountAllowedBy(Constraint(100, 100))).IsEqualTo(0.0f);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenNoBucketOccupied_ReturnsZero()
    {
        await Assert.That(TemperatureAmountSeries.Empty.OccupiedBucketCount).IsEqualTo(0);
        await Assert.That(TemperatureAmountSeries.Empty.TotalAmount).IsEqualTo(0.0f);
        await Assert.That(TemperatureAmountSeries.Empty.GetAmountAllowedBy(Constraint(10, 20))).IsEqualTo(0.0f);
        await Assert.That(TemperatureAmountSeries.Empty.GetAmountAllowedBy(Constraint(20, 0))).IsEqualTo(0.0f);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenMaximumIsOniStorableTemperatureMaximum_ExcludesExactMaximumAndAbove()
    {
        var maximumTemperatureKelvin =
            OniStorableTemperatureBounds.MaximumTemperatureKelvin;
        var series = Series(
            Amount(maximumTemperatureKelvin - 1.0f, 2.0f),
            Amount(maximumTemperatureKelvin, 3.0f),
            Amount(maximumTemperatureKelvin + 100.0f, 5.0f));

        await Assert.That(series.GetAmountAllowedBy(Constraint(
                0,
                maximumTemperatureKelvin))).IsEqualTo(2.0f);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenMinimumIsZero_ExcludesBelowRangeBucket()
    {
        var series = Series(
            Amount(-1.0f, 2.0f),
            Amount(-0.75f, 3.0f),
            Amount(0.0f, 5.0f),
            Amount(9.0f, 7.0f));

        await Assert.That(series.GetAmountAllowedBy(Constraint(0, 10))).IsEqualTo(15.0f);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenRangeIncludesTemperaturesAboveFiveThousand_IncludesObservedBuckets()
    {
        var series = Series(
            Amount(4999.0f, 2.0f),
            Amount(5000.0f, 3.0f),
            Amount(6203.0f, 5.0f),
            Amount(6999.0f, 7.0f),
            Amount(7000.0f, 11.0f));

        await Assert.That(series.GetAmountAllowedBy(Constraint(5000, 7000))).IsEqualTo(15.0f);
    }

    [Test]
    public async Task PublishedArrays_WhenSourceBuffersAreReused_DoNotChange()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 2.0f);
        accumulator.AddTemperatureAmount(20.0f, 3.0f);
        var firstPublication = accumulator.BuildSeries();

        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 100.0f);
        var secondPublication = accumulator.BuildSeries();

        await Assert.That(firstPublication.TotalAmount).IsEqualTo(5.0f);
        await Assert.That(firstPublication.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(2.0f);
        await Assert.That(secondPublication.TotalAmount).IsEqualTo(100.0f);
        await Assert.That(secondPublication.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(100.0f);
    }

    [Test]
    public async Task GetAmountAllowedBy_WhenRandomizedSparseSeriesGenerated_MatchesReferenceModel()
    {
        var random = new Random(RandomizedReferenceSeed);

        for (var seriesIndex = 0;
             seriesIndex < RandomizedSeriesCount;
             seriesIndex++)
        {
            var sourceTemperatureAmounts =
                CreateRandomSourceTemperatureAmounts(random);
            var constraint = Constraint(
                random.Next(
                    OniStorableTemperatureBounds.MinimumTemperatureKelvin - 200,
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin + 201),
                random.Next(
                    OniStorableTemperatureBounds.MinimumTemperatureKelvin - 200,
                    OniStorableTemperatureBounds.MaximumTemperatureKelvin + 201));
            var series = Series(sourceTemperatureAmounts.ToArray());

            var expectedAllowedAmount =
                ReferenceTemperatureEligibilityModel.SumAllowedAmounts(
                    sourceTemperatureAmounts,
                    constraint);
            var observedAllowedAmount = series.GetAmountAllowedBy(constraint);

            await Assert.That(observedAllowedAmount).IsEqualTo(expectedAllowedAmount).Because($"Seed=0x{RandomizedReferenceSeed:X}; series index={seriesIndex}; " +
                $"addition count={sourceTemperatureAmounts.Count}; " +
                $"constraint=[{constraint.MinimumInclusiveKelvin}, " +
                $"{constraint.MaximumExclusiveKelvin}).");
        }
    }

    [Test]
    public async Task Combine_WhenInputIsEmpty_ReturnsEmptySeries()
    {
        var combined = TemperatureAmountSeries.Combine(
            Array.Empty<TemperatureAmountSeries>());

        await Assert.That(combined).IsSameReferenceAs(TemperatureAmountSeries.Empty);
    }

    [Test]
    public async Task Combine_WhenBucketsOverlap_SumsOverlappingBucketAmounts()
    {
        var first = Series(
            Amount(10.0f, 2.0f),
            Amount(20.0f, 3.0f));
        var second = Series(
            Amount(10.9f, 5.0f),
            Amount(30.0f, 7.0f));

        var combined = TemperatureAmountSeries.Combine(
            new[] { first, second });

        await Assert.That(combined.OccupiedBucketCount).IsEqualTo(3);
        await Assert.That(combined.TotalAmount).IsEqualTo(17.0f);
        await Assert.That(combined.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(7.0f);
    }

    [Test]
    public async Task Combine_WhenBucketsAreDisjoint_PreservesEverySparseBucket()
    {
        var combined = TemperatureAmountSeries.Combine(
            new[]
            {
                Series(Amount(10.0f, 2.0f)),
                Series(Amount(20.0f, 3.0f)),
                Series(Amount(6203.0f, 5.0f))
            });

        await Assert.That(combined.OccupiedBucketCount).IsEqualTo(3);
        await Assert.That(combined.TotalAmount).IsEqualTo(10.0f);
        await Assert.That(combined.GetAmountAllowedBy(Constraint(6000, 7000))).IsEqualTo(5.0f);
    }

    [Test]
    public async Task Combine_WhenSourcesContainBelowAndAboveRangeBuckets_PreservesSentinelAmounts()
    {
        var combined = TemperatureAmountSeries.Combine(
            new[]
            {
                Series(
                    Amount(-10.0f, 2.0f),
                    Amount(300.0f, 3.0f)),
                Series(
                    Amount(10000.0f, 5.0f),
                    Amount(10500.0f, 7.0f))
            });

        await Assert.That(combined.TotalAmount).IsEqualTo(17.0f);
        await Assert.That(combined.GetAmountAllowedBy(Constraint(0, 10000))).IsEqualTo(3.0f);
        await Assert.That(combined.GetAmountAllowedBy(Constraint(100, 0))).IsEqualTo(17.0f);
    }

    [Test]
    public async Task Combine_WhenSourcesArePublished_DoesNotMutateAnySourceSeries()
    {
        var first = Series(
            Amount(10.0f, 2.0f),
            Amount(20.0f, 3.0f));
        var second = Series(Amount(30.0f, 5.0f));

        _ = TemperatureAmountSeries.Combine(new[] { first, second });

        await Assert.That(first.TotalAmount).IsEqualTo(5.0f);
        await Assert.That(first.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(2.0f);
        await Assert.That(second.TotalAmount).IsEqualTo(5.0f);
        await Assert.That(second.GetAmountAllowedBy(Constraint(30, 31))).IsEqualTo(5.0f);
    }

    [Test]
    public async Task Combine_WhenSameSeriesAppearsTwice_CountsTwoListedContributions()
    {
        var source = Series(
            Amount(10.0f, 2.0f),
            Amount(20.0f, 3.0f));

        var combined = TemperatureAmountSeries.Combine(
            new[] { source, source });

        await Assert.That(combined.TotalAmount).IsEqualTo(10.0f);
        await Assert.That(combined.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(4.0f);
    }

    [Test]
    public async Task Combine_WhenInputOrEntryIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            TemperatureAmountSeries.Combine(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() =>
            TemperatureAmountSeries.Combine(
                new TemperatureAmountSeries[] { TemperatureAmountSeries.Empty, null! })).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ProductionSource_WhenCombineIsInspected_DoesNotExpandCanonicalBucketRange()
    {
        var source = File.ReadAllText(ResolveTemperatureAmountSeriesSourcePath());
        var combineStart = source.IndexOf(
            "internal static TemperatureAmountSeries Combine(",
            StringComparison.Ordinal);
        var queryStart = source.IndexOf(
            "internal float GetAmountAllowedBy(",
            combineStart,
            StringComparison.Ordinal);

        await Assert.That(combineStart >= 0).IsTrue();
        await Assert.That(queryStart > combineStart).IsTrue();
        var combineSource = source.Substring(
            combineStart,
            queryStart - combineStart);
        await Assert.That(combineSource.Contains(
            "TemperatureDecisionBucket.BucketCount",
            StringComparison.Ordinal)).IsFalse();
        await Assert.That(combineSource.Contains(
            "new Dictionary<",
            StringComparison.Ordinal)).IsFalse();
    }

    private static List<ReferenceTemperatureAmount>
        CreateRandomSourceTemperatureAmounts(Random random)
    {
        var additionCount = random.Next(MaximumRandomizedAdditionCount + 1);
        var temperaturePool = new float[RandomizedTemperaturePoolSize];
        for (var poolIndex = 0;
             poolIndex < temperaturePool.Length;
             poolIndex++)
        {
            temperaturePool[poolIndex] = CreateRandomTemperatureKelvin(random);
        }

        var sourceTemperatureAmounts =
            new List<ReferenceTemperatureAmount>(additionCount);
        for (var additionIndex = 0;
             additionIndex < additionCount;
             additionIndex++)
        {
            var temperatureKelvin =
                temperaturePool[random.Next(temperaturePool.Length)];
            var amount = random.Next(-20, 21);
            sourceTemperatureAmounts.Add(Amount(temperatureKelvin, amount));

            if (additionIndex + 1 < additionCount && random.Next(7) == 0)
            {
                // The adjacent inverse is an explicit exact cancellation. Other
                // repeated temperatures exercise duplicate bucket accumulation.
                sourceTemperatureAmounts.Add(Amount(temperatureKelvin, -amount));
                additionIndex++;
            }
        }

        return sourceTemperatureAmounts;
    }

    private static float CreateRandomTemperatureKelvin(Random random)
    {
        switch (random.Next(8))
        {
            case 0:
                return -random.Next(1, 101) - 0.75f;

            case 1:
                // This negative fractional value deliberately truncates to zero.
                return -0.75f;

            case 2:
                return OniStorableTemperatureBounds.MaximumTemperatureKelvin;

            case 3:
                return OniStorableTemperatureBounds.MaximumTemperatureKelvin +
                    random.Next(1, 101) +
                    0.75f;

            default:
                return random.Next(
                        OniStorableTemperatureBounds.MinimumTemperatureKelvin,
                        OniStorableTemperatureBounds.MaximumTemperatureKelvin) +
                    (random.Next(4) * 0.25f);
        }
    }

    private static TemperatureAmountSeries Series(
        params ReferenceTemperatureAmount[] sourceTemperatureAmounts)
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        foreach (var sourceTemperatureAmount in sourceTemperatureAmounts)
        {
            accumulator.AddTemperatureAmount(
                sourceTemperatureAmount.TemperatureKelvin,
                sourceTemperatureAmount.Amount);
        }

        return accumulator.BuildSeries();
    }

    private static ReferenceTemperatureAmount Amount(
        float temperatureKelvin,
        float amount) =>
        new ReferenceTemperatureAmount(temperatureKelvin, amount);

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static string ResolveTemperatureAmountSeriesSourcePath()
    {
        var repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return Path.Combine(
                repositoryRoot,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source",
                "WorldResourceTemperatureAmounts",
                "TemperatureAmountSeries.cs");
        }

        var candidateDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            var candidatePath = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source",
                "WorldResourceTemperatureAmounts",
                "TemperatureAmountSeries.cs");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        Assert.Fail("Could not locate TemperatureAmountSeries.cs.");
        return string.Empty;
    }
}
