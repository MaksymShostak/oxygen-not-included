using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;

[TestClass]
public sealed class TemperatureAmountAccumulatorTests
{
    [TestMethod]
    public void BeginResourceTag_WhenPreviousTagTouchedFewBuckets_DoesNotCarryAmountsForward()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 4.0f);
        _ = accumulator.BuildSeries();

        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(20.0f, 3.0f);
        var second = accumulator.BuildSeries();

        Assert.AreEqual(1, second.OccupiedBucketCount);
        Assert.AreEqual(3.0f, second.TotalAmount);
        Assert.AreEqual(0.0f, second.GetAmountAllowedBy(Constraint(10, 11)));
        Assert.AreEqual(3.0f, second.GetAmountAllowedBy(Constraint(20, 21)));
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenSeveralAmountsShareBucket_SumsThem()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(10.1f, 2.0f);
        accumulator.AddTemperatureAmount(10.9f, 3.5f);
        var series = accumulator.BuildSeries();

        Assert.AreEqual(1, series.OccupiedBucketCount);
        Assert.AreEqual(5.5f, series.TotalAmount);
        Assert.AreEqual(5.5f, series.GetAmountAllowedBy(Constraint(10, 11)));
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenAmountIsZero_DoesNotTouchBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(10.0f, 0.0f);
        var series = accumulator.BuildSeries();

        Assert.AreSame(TemperatureAmountSeries.Empty, series);
        Assert.AreEqual(
            0,
            ReadPrivateInt32Field(accumulator, "touchedBucketCount"));
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenAmountsCancelToZero_OmitsBucketFromSeries()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(10.0f, 4.0f);
        accumulator.AddTemperatureAmount(10.5f, -4.0f);
        var series = accumulator.BuildSeries();

        Assert.AreSame(TemperatureAmountSeries.Empty, series);
        Assert.AreEqual(0, series.OccupiedBucketCount);
        Assert.AreEqual(0.0f, series.TotalAmount);
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenTemperatureIsBelowMinimumKelvin_UsesBelowRangeBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(-1.0f, 2.0f);
        accumulator.AddTemperatureAmount(-100.0f, 3.0f);
        var series = accumulator.BuildSeries();

        Assert.AreEqual(1, series.OccupiedBucketCount);
        Assert.AreEqual(5.0f, series.TotalAmount);
        Assert.AreEqual(0.0f, series.GetAmountAllowedBy(Constraint(0, 100)));
        Assert.AreEqual(5.0f, series.GetAmountAllowedBy(Constraint(100, 0)));
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenTemperatureIsAtOrAboveMaximumKelvin_UsesAboveRangeBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin,
            2.0f);
        accumulator.AddTemperatureAmount(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 500.0f,
            3.0f);
        var series = accumulator.BuildSeries();

        Assert.AreEqual(1, series.OccupiedBucketCount);
        Assert.AreEqual(5.0f, series.TotalAmount);
        Assert.AreEqual(
            0.0f,
            series.GetAmountAllowedBy(Constraint(
                0,
                OniStorableTemperatureBounds.MaximumTemperatureKelvin)));
        Assert.AreEqual(5.0f, series.GetAmountAllowedBy(Constraint(100, 0)));
    }

    [TestMethod]
    public void BeginResourceTag_WhenStampWraps_PerformsOneSafeFullReset()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 4.0f);
        _ = accumulator.BuildSeries();
        var stampField = RequirePrivateField(
            typeof(TemperatureAmountAccumulator),
            "stamp",
            typeof(int));
        stampField.SetValue(accumulator, int.MaxValue);

        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(20.0f, 3.0f);
        var seriesAfterWrap = accumulator.BuildSeries();

        Assert.AreEqual(1, ReadPrivateInt32Field(accumulator, "stamp"));
        Assert.AreEqual(1, seriesAfterWrap.OccupiedBucketCount);
        Assert.AreEqual(3.0f, seriesAfterWrap.TotalAmount);
        Assert.AreEqual(
            0.0f,
            seriesAfterWrap.GetAmountAllowedBy(Constraint(10, 11)));
        Assert.AreEqual(
            3.0f,
            seriesAfterWrap.GetAmountAllowedBy(Constraint(20, 21)));

        var amountsByBucket = ReadPrivateArray<float>(
            accumulator,
            "amountsByBucket");
        var stampsByBucket = ReadPrivateArray<int>(
            accumulator,
            "stampsByBucket");
        var oldBucketOrdinal =
            TemperatureDecisionBucket.FromTemperature(10.0f).Ordinal;
        Assert.AreEqual(0.0f, amountsByBucket[oldBucketOrdinal]);
        Assert.AreEqual(0, stampsByBucket[oldBucketOrdinal]);
    }

    [TestMethod]
    public void BuildSeries_WhenTouchedBucketsWereUnordered_SortsByBucketOrdinal()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(20.0f, 1.0f);
        accumulator.AddTemperatureAmount(5.0f, 2.0f);
        accumulator.AddTemperatureAmount(10.0f, 3.0f);

        var series = accumulator.BuildSeries();
        var occupiedBucketOrdinals = ReadPrivateArray<int>(
            series,
            "occupiedBucketOrdinals");

        Assert.AreSequenceEqual(
            new[]
            {
                TemperatureDecisionBucket.FromTemperature(5.0f).Ordinal,
                TemperatureDecisionBucket.FromTemperature(10.0f).Ordinal,
                TemperatureDecisionBucket.FromTemperature(20.0f).Ordinal,
            },
            occupiedBucketOrdinals);
    }

    [TestMethod]
    public void BuildSeries_WhenCalledWithoutBegin_ThrowsInvalidOperationException()
    {
        var accumulator = new TemperatureAmountAccumulator();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            accumulator.BuildSeries());

        StringAssert.Contains(exception.Message, "BeginResourceTag");
    }

    [TestMethod]
    public void AddTemperatureAmount_WhenCalledAfterBuildWithoutNewBegin_ThrowsInvalidOperationException()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        _ = accumulator.BuildSeries();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            accumulator.AddTemperatureAmount(10.0f, 1.0f));

        StringAssert.Contains(exception.Message, "BeginResourceTag");
    }

    [TestMethod]
    public void BeginResourceTag_WhenCurrentResourceTagIsOpen_ThrowsInvalidOperationException()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            accumulator.BeginResourceTag());

        StringAssert.Contains(exception.Message, "already collecting");
    }

    [TestMethod]
    public void AccumulatorRepresentation_WhenInspected_UsesExactlyThreeFormulaSizedArrays()
    {
        var accumulator = new TemperatureAmountAccumulator();
        var arrayFields = typeof(TemperatureAmountAccumulator)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType.IsArray)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.AreSequenceEqual(
            new[]
            {
                "amountsByBucket",
                "stampsByBucket",
                "touchedBucketOrdinals",
            },
            arrayFields.Select(field => field.Name).ToArray());
        Assert.AreEqual(typeof(float[]), arrayFields[0].FieldType);
        Assert.AreEqual(typeof(int[]), arrayFields[1].FieldType);
        Assert.AreEqual(typeof(int[]), arrayFields[2].FieldType);

        foreach (var arrayField in arrayFields)
        {
            var array = Assert.IsInstanceOfType<Array>(
                arrayField.GetValue(accumulator));
            Assert.AreEqual(TemperatureDecisionBucket.BucketCount, array.Length);
        }

        var currentArrayElementStorageBytes =
            TemperatureDecisionBucket.BucketCount *
            (sizeof(float) + sizeof(int) + sizeof(int));
        const int formerBucketCount = 5002;
        var formerArrayElementStorageBytes =
            formerBucketCount *
            (sizeof(float) + sizeof(int) + sizeof(int));

        Assert.AreEqual(120024, currentArrayElementStorageBytes);
        Assert.AreEqual(60000, currentArrayElementStorageBytes - formerArrayElementStorageBytes);
        Assert.AreEqual(
            117.2109375,
            currentArrayElementStorageBytes / 1024.0,
            delta: 0.0001);
        Assert.AreEqual(
            58.59375,
            (currentArrayElementStorageBytes - formerArrayElementStorageBytes) /
                1024.0,
            delta: 0.0001);

        var mutableStaticFields = typeof(TemperatureAmountAccumulator)
            .GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Where(field => !field.IsLiteral && !field.IsInitOnly)
            .ToArray();
        Assert.IsEmpty(mutableStaticFields);
    }

    [TestMethod]
    public void BeginResourceTag_WhenOrdinary_DoesNotReplaceArraysOrTouchUnobservedUpperRange()
    {
        var accumulator = new TemperatureAmountAccumulator();
        var amountsByBucket = ReadPrivateArray<float>(
            accumulator,
            "amountsByBucket");
        var stampsByBucket = ReadPrivateArray<int>(
            accumulator,
            "stampsByBucket");
        var touchedBucketOrdinals = ReadPrivateArray<int>(
            accumulator,
            "touchedBucketOrdinals");
        var unobservedUpperRangeOrdinal =
            TemperatureDecisionBucket.FromIntegerKelvin(9000).Ordinal;

        for (var resourceTagIndex = 0;
             resourceTagIndex < 8;
             resourceTagIndex++)
        {
            accumulator.BeginResourceTag();
            accumulator.AddTemperatureAmount(
                temperatureKelvin: 10.0f + resourceTagIndex,
                amount: 1.0f);
            _ = accumulator.BuildSeries();
        }

        Assert.AreSame(
            amountsByBucket,
            ReadPrivateArray<float>(accumulator, "amountsByBucket"));
        Assert.AreSame(
            stampsByBucket,
            ReadPrivateArray<int>(accumulator, "stampsByBucket"));
        Assert.AreSame(
            touchedBucketOrdinals,
            ReadPrivateArray<int>(accumulator, "touchedBucketOrdinals"));
        Assert.AreEqual(0.0f, amountsByBucket[unobservedUpperRangeOrdinal]);
        Assert.AreEqual(0, stampsByBucket[unobservedUpperRangeOrdinal]);
    }

    [TestMethod]
    public void ProductionSource_WhenInspected_HasNoRecurringCompleteRangeTraversalOrDictionary()
    {
        var accumulatorSource = File.ReadAllText(
            ResolveProductionSourcePath("TemperatureAmountAccumulator.cs"));
        var seriesSource = File.ReadAllText(
            ResolveProductionSourcePath("TemperatureAmountSeries.cs"));

        Assert.IsFalse(accumulatorSource.Contains("Dictionary<", StringComparison.Ordinal));
        Assert.IsFalse(accumulatorSource.Contains("System.Linq", StringComparison.Ordinal));
        Assert.IsFalse(seriesSource.Contains("Dictionary<", StringComparison.Ordinal));
        Assert.IsFalse(seriesSource.Contains("System.Linq", StringComparison.Ordinal));

        var beginResourceTagSource = ExtractMethodRegion(
            accumulatorSource,
            "internal void BeginResourceTag()",
            "internal void AddTemperatureAmount(");
        Assert.IsFalse(beginResourceTagSource.Contains("for (", StringComparison.Ordinal));
        Assert.IsFalse(beginResourceTagSource.Contains("foreach (", StringComparison.Ordinal));
        Assert.IsFalse(beginResourceTagSource.Contains("while (", StringComparison.Ordinal));
        Assert.IsFalse(beginResourceTagSource.Contains("new float[", StringComparison.Ordinal));
        Assert.IsFalse(beginResourceTagSource.Contains("new int[", StringComparison.Ordinal));

        var addTemperatureAmountSource = ExtractMethodRegion(
            accumulatorSource,
            "internal void AddTemperatureAmount(",
            "internal TemperatureAmountSeries BuildSeries()");
        Assert.IsFalse(addTemperatureAmountSource.Contains("for (", StringComparison.Ordinal));
        Assert.IsFalse(addTemperatureAmountSource.Contains("foreach (", StringComparison.Ordinal));
        Assert.IsFalse(addTemperatureAmountSource.Contains("while (", StringComparison.Ordinal));

        var buildSeriesSource = ExtractMethodRegion(
            accumulatorSource,
            "internal TemperatureAmountSeries BuildSeries()",
            "private void ThrowIfResourceTagIsNotOpen()");
        Assert.IsFalse(buildSeriesSource.Contains(
            "< TemperatureDecisionBucket.BucketCount",
            StringComparison.Ordinal));

        var querySource = ExtractMethodRegion(
            seriesSource,
            "internal float GetAmountAllowedBy(",
            "private static int LowerBound(");
        Assert.IsFalse(querySource.Contains("for (", StringComparison.Ordinal));
        Assert.IsFalse(querySource.Contains("foreach (", StringComparison.Ordinal));
        Assert.IsFalse(querySource.Contains("TemperatureDecisionBucket.BucketCount", StringComparison.Ordinal));
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static int ReadPrivateInt32Field(
        TemperatureAmountAccumulator accumulator,
        string exactFieldName)
    {
        var field = RequirePrivateField(
            typeof(TemperatureAmountAccumulator),
            exactFieldName,
            typeof(int));
        return Assert.IsInstanceOfType<int>(field.GetValue(accumulator));
    }

    private static T[] ReadPrivateArray<T>(
        object instance,
        string exactFieldName)
    {
        var field = RequirePrivateField(
            instance.GetType(),
            exactFieldName,
            typeof(T[]));
        return Assert.IsInstanceOfType<T[]>(field.GetValue(instance));
    }

    private static FieldInfo RequirePrivateField(
        Type declaringType,
        string exactFieldName,
        Type exactFieldType)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private field " +
            $"{declaringType.Name}.{exactFieldName}.");
        Assert.AreEqual(exactFieldType, field.FieldType);
        return field;
    }

    private static string ResolveProductionSourcePath(string sourceFileName)
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
                sourceFileName);
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
                sourceFileName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        Assert.Fail($"Could not locate production source file {sourceFileName}.");
        return string.Empty;
    }

    private static string ExtractMethodRegion(
        string source,
        string startMarker,
        string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsTrue(startIndex >= 0);
        var endIndex = source.IndexOf(
            endMarker,
            startIndex + startMarker.Length,
            StringComparison.Ordinal);
        Assert.IsTrue(endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
    }
}
