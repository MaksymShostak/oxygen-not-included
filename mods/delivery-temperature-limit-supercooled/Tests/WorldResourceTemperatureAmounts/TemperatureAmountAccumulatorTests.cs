using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;
public sealed class TemperatureAmountAccumulatorTests
{
    [Test]
    public async Task BeginResourceTag_WhenPreviousTagTouchedFewBuckets_DoesNotCarryAmountsForward()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 4.0f);
        _ = accumulator.BuildSeries();

        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(20.0f, 3.0f);
        var second = accumulator.BuildSeries();

        await Assert.That(second.OccupiedBucketCount).IsEqualTo(1);
        await Assert.That(second.TotalAmount).IsEqualTo(3.0f);
        await Assert.That(second.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(0.0f);
        await Assert.That(second.GetAmountAllowedBy(Constraint(20, 21))).IsEqualTo(3.0f);
    }

    [Test]
    public async Task AddTemperatureAmount_WhenSeveralAmountsShareBucket_SumsThem()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(10.1f, 2.0f);
        accumulator.AddTemperatureAmount(10.9f, 3.5f);
        var series = accumulator.BuildSeries();

        await Assert.That(series.OccupiedBucketCount).IsEqualTo(1);
        await Assert.That(series.TotalAmount).IsEqualTo(5.5f);
        await Assert.That(series.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(5.5f);
    }

    [Test]
    public async Task AddTemperatureAmount_WhenAmountIsZero_DoesNotTouchBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(10.0f, 0.0f);
        var series = accumulator.BuildSeries();

        await Assert.That(series).IsSameReferenceAs(TemperatureAmountSeries.Empty);
        await Assert.That((await ReadPrivateInt32Field(accumulator, "touchedBucketCount"))).IsEqualTo(0);
    }

    [Test]
    public async Task AddTemperatureAmount_WhenAmountsCancelToZero_OmitsBucketFromSeries()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(10.0f, 4.0f);
        accumulator.AddTemperatureAmount(10.5f, -4.0f);
        var series = accumulator.BuildSeries();

        await Assert.That(series).IsSameReferenceAs(TemperatureAmountSeries.Empty);
        await Assert.That(series.OccupiedBucketCount).IsEqualTo(0);
        await Assert.That(series.TotalAmount).IsEqualTo(0.0f);
    }

    [Test]
    public async Task AddTemperatureAmount_WhenTemperatureIsBelowMinimumKelvin_UsesBelowRangeBucket()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        accumulator.AddTemperatureAmount(-1.0f, 2.0f);
        accumulator.AddTemperatureAmount(-100.0f, 3.0f);
        var series = accumulator.BuildSeries();

        await Assert.That(series.OccupiedBucketCount).IsEqualTo(1);
        await Assert.That(series.TotalAmount).IsEqualTo(5.0f);
        await Assert.That(series.GetAmountAllowedBy(Constraint(0, 100))).IsEqualTo(0.0f);
        await Assert.That(series.GetAmountAllowedBy(Constraint(100, 0))).IsEqualTo(5.0f);
    }

    [Test]
    public async Task AddTemperatureAmount_WhenTemperatureIsAtOrAboveMaximumKelvin_UsesAboveRangeBucket()
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

        await Assert.That(series.OccupiedBucketCount).IsEqualTo(1);
        await Assert.That(series.TotalAmount).IsEqualTo(5.0f);
        await Assert.That(series.GetAmountAllowedBy(Constraint(
                0,
                OniStorableTemperatureBounds.MaximumTemperatureKelvin))).IsEqualTo(0.0f);
        await Assert.That(series.GetAmountAllowedBy(Constraint(100, 0))).IsEqualTo(5.0f);
    }

    [Test]
    public async Task BeginResourceTag_WhenStampWraps_PerformsOneSafeFullReset()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(10.0f, 4.0f);
        _ = accumulator.BuildSeries();
        var stampField = await RequirePrivateField(
            typeof(TemperatureAmountAccumulator),
            "stamp",
            typeof(int));
        stampField.SetValue(accumulator, int.MaxValue);

        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(20.0f, 3.0f);
        var seriesAfterWrap = accumulator.BuildSeries();

        await Assert.That((await ReadPrivateInt32Field(accumulator, "stamp"))).IsEqualTo(1);
        await Assert.That(seriesAfterWrap.OccupiedBucketCount).IsEqualTo(1);
        await Assert.That(seriesAfterWrap.TotalAmount).IsEqualTo(3.0f);
        await Assert.That(seriesAfterWrap.GetAmountAllowedBy(Constraint(10, 11))).IsEqualTo(0.0f);
        await Assert.That(seriesAfterWrap.GetAmountAllowedBy(Constraint(20, 21))).IsEqualTo(3.0f);

        var amountsByBucket = await ReadPrivateArray<float>(
            accumulator,
            "amountsByBucket");
        var stampsByBucket = await ReadPrivateArray<int>(
            accumulator,
            "stampsByBucket");
        var oldBucketOrdinal =
            TemperatureDecisionBucket.FromTemperature(10.0f).Ordinal;
        await Assert.That(amountsByBucket[oldBucketOrdinal]).IsEqualTo(0.0f);
        await Assert.That(stampsByBucket[oldBucketOrdinal]).IsEqualTo(0);
    }

    [Test]
    public async Task BuildSeries_WhenTouchedBucketsWereUnordered_SortsByBucketOrdinal()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        accumulator.AddTemperatureAmount(20.0f, 1.0f);
        accumulator.AddTemperatureAmount(5.0f, 2.0f);
        accumulator.AddTemperatureAmount(10.0f, 3.0f);

        var series = accumulator.BuildSeries();
        var occupiedBucketOrdinals = await ReadPrivateArray<int>(
            series,
            "occupiedBucketOrdinals");

        await Assert.That(occupiedBucketOrdinals).IsEquivalentTo(new[]
            {
                TemperatureDecisionBucket.FromTemperature(5.0f).Ordinal,
                TemperatureDecisionBucket.FromTemperature(10.0f).Ordinal,
                TemperatureDecisionBucket.FromTemperature(20.0f).Ordinal,
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task BuildSeries_WhenCalledWithoutBegin_ThrowsInvalidOperationException()
    {
        var accumulator = new TemperatureAmountAccumulator();

        var exception = (await Assert.That(() =>
            accumulator.BuildSeries()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("BeginResourceTag");
    }

    [Test]
    public async Task AddTemperatureAmount_WhenCalledAfterBuildWithoutNewBegin_ThrowsInvalidOperationException()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();
        _ = accumulator.BuildSeries();

        var exception = (await Assert.That(() =>
            accumulator.AddTemperatureAmount(10.0f, 1.0f)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("BeginResourceTag");
    }

    [Test]
    public async Task BeginResourceTag_WhenCurrentResourceTagIsOpen_ThrowsInvalidOperationException()
    {
        var accumulator = new TemperatureAmountAccumulator();
        accumulator.BeginResourceTag();

        var exception = (await Assert.That(() =>
            accumulator.BeginResourceTag()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("already collecting");
    }

    [Test]
    public async Task AccumulatorRepresentation_WhenInspected_UsesExactlyThreeFormulaSizedArrays()
    {
        var accumulator = new TemperatureAmountAccumulator();
        var arrayFields = typeof(TemperatureAmountAccumulator)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType.IsArray)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(arrayFields.Select(field => field.Name).ToArray()).IsEquivalentTo(new[]
            {
                "amountsByBucket",
                "stampsByBucket",
                "touchedBucketOrdinals",
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(arrayFields[0].FieldType).IsEqualTo(typeof(float[]));
        await Assert.That(arrayFields[1].FieldType).IsEqualTo(typeof(int[]));
        await Assert.That(arrayFields[2].FieldType).IsEqualTo(typeof(int[]));

        foreach (var arrayField in arrayFields)
        {
            var array = (await Assert.That(arrayField.GetValue(accumulator)).IsAssignableTo<Array>())!;
            await Assert.That(array.Length).IsEqualTo(TemperatureDecisionBucket.BucketCount);
        }

        var currentArrayElementStorageBytes =
            TemperatureDecisionBucket.BucketCount *
            (sizeof(float) + sizeof(int) + sizeof(int));
        const int formerBucketCount = 5002;
        var formerArrayElementStorageBytes =
            formerBucketCount *
            (sizeof(float) + sizeof(int) + sizeof(int));

        await Assert.That(currentArrayElementStorageBytes).IsEqualTo(120024);
        await Assert.That(currentArrayElementStorageBytes - formerArrayElementStorageBytes).IsEqualTo(60000);
        await Assert.That(Math.Abs(currentArrayElementStorageBytes / 1024.0 - 117.2109375)).IsLessThanOrEqualTo(0.0001);
        await Assert.That(Math.Abs((currentArrayElementStorageBytes - formerArrayElementStorageBytes) /
                1024.0 - 58.59375)).IsLessThanOrEqualTo(0.0001);

        var mutableStaticFields = typeof(TemperatureAmountAccumulator)
            .GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Where(field => !field.IsLiteral && !field.IsInitOnly)
            .ToArray();
        await Assert.That(mutableStaticFields).IsEmpty();
    }

    [Test]
    public async Task BeginResourceTag_WhenOrdinary_DoesNotReplaceArraysOrTouchUnobservedUpperRange()
    {
        var accumulator = new TemperatureAmountAccumulator();
        var amountsByBucket = await ReadPrivateArray<float>(
            accumulator,
            "amountsByBucket");
        var stampsByBucket = await ReadPrivateArray<int>(
            accumulator,
            "stampsByBucket");
        var touchedBucketOrdinals = await ReadPrivateArray<int>(
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

        await Assert.That((await ReadPrivateArray<float>(accumulator, "amountsByBucket"))).IsSameReferenceAs(amountsByBucket);
        await Assert.That((await ReadPrivateArray<int>(accumulator, "stampsByBucket"))).IsSameReferenceAs(stampsByBucket);
        await Assert.That((await ReadPrivateArray<int>(accumulator, "touchedBucketOrdinals"))).IsSameReferenceAs(touchedBucketOrdinals);
        await Assert.That(amountsByBucket[unobservedUpperRangeOrdinal]).IsEqualTo(0.0f);
        await Assert.That(stampsByBucket[unobservedUpperRangeOrdinal]).IsEqualTo(0);
    }

    [Test]
    public async Task ProductionSource_WhenInspected_HasNoRecurringCompleteRangeTraversalOrDictionary()
    {
        var accumulatorSource = File.ReadAllText(
            ResolveProductionSourcePath("TemperatureAmountAccumulator.cs"));
        var seriesSource = File.ReadAllText(
            ResolveProductionSourcePath("TemperatureAmountSeries.cs"));

        await Assert.That(accumulatorSource.Contains("Dictionary<", StringComparison.Ordinal)).IsFalse();
        await Assert.That(accumulatorSource.Contains("System.Linq", StringComparison.Ordinal)).IsFalse();
        await Assert.That(seriesSource.Contains("Dictionary<", StringComparison.Ordinal)).IsFalse();
        await Assert.That(seriesSource.Contains("System.Linq", StringComparison.Ordinal)).IsFalse();

        var beginResourceTagSource = await ExtractMethodRegion(
            accumulatorSource,
            "internal void BeginResourceTag()",
            "internal void AddTemperatureAmount(");
        await Assert.That(beginResourceTagSource.Contains("for (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(beginResourceTagSource.Contains("foreach (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(beginResourceTagSource.Contains("while (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(beginResourceTagSource.Contains("new float[", StringComparison.Ordinal)).IsFalse();
        await Assert.That(beginResourceTagSource.Contains("new int[", StringComparison.Ordinal)).IsFalse();

        var addTemperatureAmountSource = await ExtractMethodRegion(
            accumulatorSource,
            "internal void AddTemperatureAmount(",
            "internal TemperatureAmountSeries BuildSeries()");
        await Assert.That(addTemperatureAmountSource.Contains("for (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(addTemperatureAmountSource.Contains("foreach (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(addTemperatureAmountSource.Contains("while (", StringComparison.Ordinal)).IsFalse();

        var buildSeriesSource = await ExtractMethodRegion(
            accumulatorSource,
            "internal TemperatureAmountSeries BuildSeries()",
            "private void ThrowIfResourceTagIsNotOpen()");
        await Assert.That(buildSeriesSource.Contains(
            "< TemperatureDecisionBucket.BucketCount",
            StringComparison.Ordinal)).IsFalse();

        var querySource = await ExtractMethodRegion(
            seriesSource,
            "internal float GetAmountAllowedBy(",
            "private static int LowerBound(");
        await Assert.That(querySource.Contains("for (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(querySource.Contains("foreach (", StringComparison.Ordinal)).IsFalse();
        await Assert.That(querySource.Contains("TemperatureDecisionBucket.BucketCount", StringComparison.Ordinal)).IsFalse();
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static async Task<int> ReadPrivateInt32Field(
        TemperatureAmountAccumulator accumulator,
        string exactFieldName)
    {
        var field = await RequirePrivateField(
            typeof(TemperatureAmountAccumulator),
            exactFieldName,
            typeof(int));
        return ((await Assert.That(field.GetValue(accumulator)).IsAssignableTo<int>())!);
    }

    private static async Task<T[]> ReadPrivateArray<T>(
        object instance,
        string exactFieldName)
    {
        var field = await RequirePrivateField(
            instance.GetType(),
            exactFieldName,
            typeof(T[]));
        return ((await Assert.That(field.GetValue(instance)).IsAssignableTo<T[]>())!);
    }

    private static async Task<FieldInfo> RequirePrivateField(
        Type declaringType,
        string exactFieldName,
        Type exactFieldType)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private field " +
            $"{declaringType.Name}.{exactFieldName}.");
        await Assert.That(field.FieldType).IsEqualTo(exactFieldType);
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

    private static async Task<string> ExtractMethodRegion(
        string source,
        string startMarker,
        string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        await Assert.That(startIndex >= 0).IsTrue();
        var endIndex = source.IndexOf(
            endMarker,
            startIndex + startMarker.Length,
            StringComparison.Ordinal);
        await Assert.That(endIndex > startIndex).IsTrue();
        return source.Substring(startIndex, endIndex - startIndex);
    }
}
