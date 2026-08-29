using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;

[TestClass]
public sealed class TemperatureDecisionBucketTests
{
    [DataRow(-1.0f, TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)]
    [DataRow(-0.999f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)]
    [DataRow(0.0f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)]
    [DataRow(273.15f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal + 273)]
    [DataRow(9999.999f, TemperatureDecisionBucket.HighestIntegerKelvinOrdinal)]
    [DataRow(10000.0f, TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)]
    [TestMethod]
    public void FromTemperature_WhenGivenBoundaryValue_UsesCSharpTruncation(
        float temperatureKelvin,
        int expectedOrdinal)
    {
        Assert.AreEqual(
            expectedOrdinal,
            TemperatureDecisionBucket.FromTemperature(temperatureKelvin).Ordinal);
    }

    [TestMethod]
    public void FromIntegerKelvin_WhenGivenEveryRepresentableConfiguredTemperature_RoundTrips()
    {
        for (
            var integerKelvin = OniStorableTemperatureBounds.MinimumTemperatureKelvin;
            integerKelvin < OniStorableTemperatureBounds.MaximumTemperatureKelvin;
            integerKelvin++)
        {
            var bucket = TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);
            var expectedOrdinal =
                TemperatureDecisionBucket.FirstIntegerKelvinOrdinal + integerKelvin;

            Assert.AreEqual(
                expectedOrdinal,
                bucket.Ordinal,
                $"Unexpected ordinal for {integerKelvin} K.");
            Assert.IsTrue(
                bucket.TryGetIntegerKelvin(out var observedIntegerKelvin),
                $"The {integerKelvin} K bucket must expose its integer Kelvin identity.");
            Assert.AreEqual(integerKelvin, observedIntegerKelvin);
            Assert.AreEqual(
                bucket,
                TemperatureDecisionBucket.FromTemperature(integerKelvin));
        }
    }

    [DataRow(int.MinValue)]
    [DataRow(-10000)]
    [DataRow(-274)]
    [DataRow(-1)]
    [TestMethod]
    public void FromIntegerKelvin_WhenBelowMinimum_UsesOneBehaviorallyEquivalentBucket(
        int integerKelvin)
    {
        var bucket = TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);

        Assert.AreEqual(
            TemperatureDecisionBucket.BelowMinimumKelvinOrdinal,
            bucket.Ordinal);
        Assert.IsTrue(bucket.IsBelowMinimumKelvin);
        Assert.IsFalse(bucket.IsAtOrAboveMaximumKelvin);
        Assert.IsFalse(bucket.TryGetIntegerKelvin(out var observedIntegerKelvin));
        Assert.AreEqual(0, observedIntegerKelvin);
    }

    [DataRow(OniStorableTemperatureBounds.MaximumTemperatureKelvin)]
    [DataRow(OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1)]
    [DataRow(20000)]
    [DataRow(int.MaxValue)]
    [TestMethod]
    public void FromIntegerKelvin_WhenAtOrAboveMaximum_UsesOneBehaviorallyEquivalentBucket(
        int integerKelvin)
    {
        var bucket = TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);

        Assert.AreEqual(
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal,
            bucket.Ordinal);
        Assert.IsFalse(bucket.IsBelowMinimumKelvin);
        Assert.IsTrue(bucket.IsAtOrAboveMaximumKelvin);
        Assert.IsFalse(bucket.TryGetIntegerKelvin(out var observedIntegerKelvin));
        Assert.AreEqual(0, observedIntegerKelvin);
    }

    [TestMethod]
    public void BucketCount_WhenInspected_IsDerivedFromReviewedOniMaximum()
    {
        var observedBucketCount = ReadConstant(nameof(TemperatureDecisionBucket.BucketCount));
        var expectedBucketCount =
            1 + OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;

        Assert.AreEqual(expectedBucketCount, observedBucketCount);
        Assert.AreEqual(10002, observedBucketCount);
    }

    [TestMethod]
    public void NamedOrdinals_WhenInspected_DescribeEveryBucketBoundary()
    {
        Assert.AreEqual(
            0,
            ReadConstant(nameof(TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)));
        Assert.AreEqual(
            1,
            ReadConstant(nameof(TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)));
        Assert.AreEqual(
            10000,
            ReadConstant(nameof(TemperatureDecisionBucket.HighestIntegerKelvinOrdinal)));
        Assert.AreEqual(
            10001,
            ReadConstant(nameof(TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)));
    }

    [TestMethod]
    public void DefaultBucket_WhenInspected_IsBelowMinimumSentinel()
    {
        var bucket = default(TemperatureDecisionBucket);

        Assert.AreEqual(
            TemperatureDecisionBucket.BelowMinimumKelvinOrdinal,
            bucket.Ordinal);
        Assert.IsTrue(bucket.IsBelowMinimumKelvin);
    }

    [TestMethod]
    public void EqualityAndComparison_WhenOrdinalsMatch_AreValueBased()
    {
        var first = TemperatureDecisionBucket.FromIntegerKelvin(273);
        var second = TemperatureDecisionBucket.FromTemperature(273.999f);

        Assert.AreEqual(first, second);
        Assert.IsTrue(first.Equals(second));
        Assert.IsTrue(first.Equals((object)second));
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreEqual(0, first.CompareTo(second));
        Assert.IsTrue(
            TemperatureDecisionBucket.FromIntegerKelvin(272).CompareTo(first) < 0);
        Assert.IsTrue(
            TemperatureDecisionBucket.FromIntegerKelvin(274).CompareTo(first) > 0);
    }

    private static object? ReadConstant(string fieldName)
    {
        var field = typeof(TemperatureDecisionBucket).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, $"Expected constant {fieldName} was not emitted.");
        Assert.IsTrue(field.IsLiteral);
        return field.GetRawConstantValue();
    }
}
