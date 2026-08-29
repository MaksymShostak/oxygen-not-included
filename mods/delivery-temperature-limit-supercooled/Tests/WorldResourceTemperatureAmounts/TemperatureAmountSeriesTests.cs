using DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;

[TestClass]
public sealed class TemperatureAmountSeriesTests
{
    private const int RandomizedReferenceSeed = 0xA60A17;
    private const int RandomizedSeriesCount = 10000;
    private const int MaximumRandomizedAdditionCount = 256;
    private const int RandomizedTemperaturePoolSize = 32;

    [TestMethod]
    public void GetAmountAllowedBy_WhenConstraintIsTenThroughTwenty_SumsOnlyTenThroughNineteen()
    {
        var series = Series(
            Amount(-1.0f, 2.0f),
            Amount(9.0f, 3.0f),
            Amount(10.0f, 5.0f),
            Amount(19.0f, 7.0f),
            Amount(20.0f, 11.0f),
            Amount(6203.0f, 13.0f),
            Amount(10000.0f, 17.0f));

        Assert.AreEqual(
            12.0f,
            series.GetAmountAllowedBy(Constraint(10, 20)));
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenConstraintIsDisabled_ReturnsTotalIncludingBelowAndAboveRangeBuckets()
    {
        var series = Series(
            Amount(-10.0f, 2.0f),
            Amount(100.0f, 3.0f),
            Amount(10000.0f, 5.0f));

        var allowedAmount = series.GetAmountAllowedBy(Constraint(400, 0));

        Assert.AreEqual(10.0f, allowedAmount);
        Assert.AreEqual(series.TotalAmount, allowedAmount);
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenConstraintIsEmpty_ReturnsZero()
    {
        var series = Series(
            Amount(-10.0f, 2.0f),
            Amount(100.0f, 3.0f),
            Amount(10000.0f, 5.0f));

        Assert.AreEqual(
            0.0f,
            series.GetAmountAllowedBy(Constraint(100, 100)));
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenNoBucketOccupied_ReturnsZero()
    {
        Assert.AreEqual(0, TemperatureAmountSeries.Empty.OccupiedBucketCount);
        Assert.AreEqual(0.0f, TemperatureAmountSeries.Empty.TotalAmount);
        Assert.AreEqual(
            0.0f,
            TemperatureAmountSeries.Empty.GetAmountAllowedBy(Constraint(10, 20)));
        Assert.AreEqual(
            0.0f,
            TemperatureAmountSeries.Empty.GetAmountAllowedBy(Constraint(20, 0)));
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenMaximumIsOniStorableTemperatureMaximum_ExcludesExactMaximumAndAbove()
    {
        var maximumTemperatureKelvin =
            OniStorableTemperatureBounds.MaximumTemperatureKelvin;
        var series = Series(
            Amount(maximumTemperatureKelvin - 1.0f, 2.0f),
            Amount(maximumTemperatureKelvin, 3.0f),
            Amount(maximumTemperatureKelvin + 100.0f, 5.0f));

        Assert.AreEqual(
            2.0f,
            series.GetAmountAllowedBy(Constraint(
                0,
                maximumTemperatureKelvin)));
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenMinimumIsZero_ExcludesBelowRangeBucket()
    {
        var series = Series(
            Amount(-1.0f, 2.0f),
            Amount(-0.75f, 3.0f),
            Amount(0.0f, 5.0f),
            Amount(9.0f, 7.0f));

        Assert.AreEqual(
            15.0f,
            series.GetAmountAllowedBy(Constraint(0, 10)));
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenRangeIncludesTemperaturesAboveFiveThousand_IncludesObservedBuckets()
    {
        var series = Series(
            Amount(4999.0f, 2.0f),
            Amount(5000.0f, 3.0f),
            Amount(6203.0f, 5.0f),
            Amount(6999.0f, 7.0f),
            Amount(7000.0f, 11.0f));

        Assert.AreEqual(
            15.0f,
            series.GetAmountAllowedBy(Constraint(5000, 7000)));
    }

    [TestMethod]
    public void PublishedArrays_WhenSourceBuffersAreReused_DoNotChange()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 2.0f);
        accumulator.AddTemperatureAmount(20.0f, 3.0f);
        var firstPublication = accumulator.BuildSeries();

        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 100.0f);
        var secondPublication = accumulator.BuildSeries();

        Assert.AreEqual(5.0f, firstPublication.TotalAmount);
        Assert.AreEqual(
            2.0f,
            firstPublication.GetAmountAllowedBy(Constraint(10, 11)));
        Assert.AreEqual(100.0f, secondPublication.TotalAmount);
        Assert.AreEqual(
            100.0f,
            secondPublication.GetAmountAllowedBy(Constraint(10, 11)));
    }

    [TestMethod]
    public void GetAmountAllowedBy_WhenRandomizedSparseSeriesGenerated_MatchesReferenceModel()
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

            Assert.AreEqual(
                expectedAllowedAmount,
                observedAllowedAmount,
                $"Seed=0x{RandomizedReferenceSeed:X}; series index={seriesIndex}; " +
                $"addition count={sourceTemperatureAmounts.Count}; " +
                $"constraint=[{constraint.MinimumInclusiveKelvin}, " +
                $"{constraint.MaximumExclusiveKelvin}).");
        }
    }

    [TestMethod]
    public void Combine_WhenInputIsEmpty_ReturnsEmptySeries()
    {
        var combined = TemperatureAmountSeries.Combine(
            Array.Empty<TemperatureAmountSeries>());

        Assert.AreSame(TemperatureAmountSeries.Empty, combined);
    }

    [TestMethod]
    public void Combine_WhenBucketsOverlap_SumsOverlappingBucketAmounts()
    {
        var first = Series(
            Amount(10.0f, 2.0f),
            Amount(20.0f, 3.0f));
        var second = Series(
            Amount(10.9f, 5.0f),
            Amount(30.0f, 7.0f));

        var combined = TemperatureAmountSeries.Combine(
            new[] { first, second });

        Assert.AreEqual(3, combined.OccupiedBucketCount);
        Assert.AreEqual(17.0f, combined.TotalAmount);
        Assert.AreEqual(
            7.0f,
            combined.GetAmountAllowedBy(Constraint(10, 11)));
    }

    [TestMethod]
    public void Combine_WhenBucketsAreDisjoint_PreservesEverySparseBucket()
    {
        var combined = TemperatureAmountSeries.Combine(
            new[]
            {
                Series(Amount(10.0f, 2.0f)),
                Series(Amount(20.0f, 3.0f)),
                Series(Amount(6203.0f, 5.0f))
            });

        Assert.AreEqual(3, combined.OccupiedBucketCount);
        Assert.AreEqual(10.0f, combined.TotalAmount);
        Assert.AreEqual(
            5.0f,
            combined.GetAmountAllowedBy(Constraint(6000, 7000)));
    }

    [TestMethod]
    public void Combine_WhenSourcesContainBelowAndAboveRangeBuckets_PreservesSentinelAmounts()
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

        Assert.AreEqual(17.0f, combined.TotalAmount);
        Assert.AreEqual(
            3.0f,
            combined.GetAmountAllowedBy(Constraint(0, 10000)));
        Assert.AreEqual(
            17.0f,
            combined.GetAmountAllowedBy(Constraint(100, 0)));
    }

    [TestMethod]
    public void Combine_WhenSourcesArePublished_DoesNotMutateAnySourceSeries()
    {
        var first = Series(
            Amount(10.0f, 2.0f),
            Amount(20.0f, 3.0f));
        var second = Series(Amount(30.0f, 5.0f));

        _ = TemperatureAmountSeries.Combine(new[] { first, second });

        Assert.AreEqual(5.0f, first.TotalAmount);
        Assert.AreEqual(
            2.0f,
            first.GetAmountAllowedBy(Constraint(10, 11)));
        Assert.AreEqual(5.0f, second.TotalAmount);
        Assert.AreEqual(
            5.0f,
            second.GetAmountAllowedBy(Constraint(30, 31)));
    }

    [TestMethod]
    public void Combine_WhenSameSeriesAppearsTwice_CountsTwoListedContributions()
    {
        var source = Series(
            Amount(10.0f, 2.0f),
            Amount(20.0f, 3.0f));

        var combined = TemperatureAmountSeries.Combine(
            new[] { source, source });

        Assert.AreEqual(10.0f, combined.TotalAmount);
        Assert.AreEqual(
            4.0f,
            combined.GetAmountAllowedBy(Constraint(10, 11)));
    }

    [TestMethod]
    public void Combine_WhenInputOrEntryIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            TemperatureAmountSeries.Combine(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            TemperatureAmountSeries.Combine(
                new TemperatureAmountSeries[] { TemperatureAmountSeries.Empty, null! }));
    }

    [TestMethod]
    public void ProductionSource_WhenCombineIsInspected_DoesNotExpandCanonicalBucketRange()
    {
        var source = File.ReadAllText(ResolveTemperatureAmountSeriesSourcePath());
        var combineStart = source.IndexOf(
            "internal static TemperatureAmountSeries Combine(",
            StringComparison.Ordinal);
        var queryStart = source.IndexOf(
            "internal float GetAmountAllowedBy(",
            combineStart,
            StringComparison.Ordinal);

        Assert.IsTrue(combineStart >= 0);
        Assert.IsTrue(queryStart > combineStart);
        var combineSource = source.Substring(
            combineStart,
            queryStart - combineStart);
        Assert.IsFalse(combineSource.Contains(
            "TemperatureDecisionBucket.BucketCount",
            StringComparison.Ordinal));
        Assert.IsFalse(combineSource.Contains(
            "new Dictionary<",
            StringComparison.Ordinal));
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
