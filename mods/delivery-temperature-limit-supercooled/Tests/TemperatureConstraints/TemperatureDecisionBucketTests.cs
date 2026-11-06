using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;
public sealed class TemperatureDecisionBucketTests
{
    [Arguments(-1.0f, TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)]
    [Arguments(-0.999f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)]
    [Arguments(0.0f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)]
    [Arguments(273.15f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal + 273)]
    [Arguments(9999.999f, TemperatureDecisionBucket.HighestIntegerKelvinOrdinal)]
    [Arguments(10000.0f, TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)]
    [Test]
    public async Task FromTemperature_WhenGivenBoundaryValue_UsesCSharpTruncation(
        float temperatureKelvin,
        int expectedOrdinal)
    {
        await Assert.That(TemperatureDecisionBucket.FromTemperature(temperatureKelvin).Ordinal).IsEqualTo(expectedOrdinal);
    }

    [Test]
    public async Task FromIntegerKelvin_WhenGivenEveryRepresentableConfiguredTemperature_RoundTrips()
    {
        for (
            var integerKelvin = OniStorableTemperatureBounds.MinimumTemperatureKelvin;
            integerKelvin < OniStorableTemperatureBounds.MaximumTemperatureKelvin;
            integerKelvin++)
        {
            var bucket = TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);
            var expectedOrdinal =
                TemperatureDecisionBucket.FirstIntegerKelvinOrdinal + integerKelvin;

            await Assert.That(bucket.Ordinal).IsEqualTo(expectedOrdinal).Because($"Unexpected ordinal for {integerKelvin} K.");
            await Assert.That(bucket.TryGetIntegerKelvin(out var observedIntegerKelvin)).IsTrue().Because($"The {integerKelvin} K bucket must expose its integer Kelvin identity.");
            await Assert.That(observedIntegerKelvin).IsEqualTo(integerKelvin);
            await Assert.That(TemperatureDecisionBucket.FromTemperature(integerKelvin)).IsEqualTo(bucket);
        }
    }

    [Arguments(int.MinValue)]
    [Arguments(-10000)]
    [Arguments(-274)]
    [Arguments(-1)]
    [Test]
    public async Task FromIntegerKelvin_WhenBelowMinimum_UsesOneBehaviorallyEquivalentBucket(
        int integerKelvin)
    {
        var bucket = TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);

        await Assert.That(bucket.Ordinal).IsEqualTo(TemperatureDecisionBucket.BelowMinimumKelvinOrdinal);
        await Assert.That(bucket.IsBelowMinimumKelvin).IsTrue();
        await Assert.That(bucket.IsAtOrAboveMaximumKelvin).IsFalse();
        await Assert.That(bucket.TryGetIntegerKelvin(out var observedIntegerKelvin)).IsFalse();
        await Assert.That(observedIntegerKelvin).IsEqualTo(0);
    }

    [Arguments(OniStorableTemperatureBounds.MaximumTemperatureKelvin)]
    [Arguments(OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1)]
    [Arguments(20000)]
    [Arguments(int.MaxValue)]
    [Test]
    public async Task FromIntegerKelvin_WhenAtOrAboveMaximum_UsesOneBehaviorallyEquivalentBucket(
        int integerKelvin)
    {
        var bucket = TemperatureDecisionBucket.FromIntegerKelvin(integerKelvin);

        await Assert.That(bucket.Ordinal).IsEqualTo(TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal);
        await Assert.That(bucket.IsBelowMinimumKelvin).IsFalse();
        await Assert.That(bucket.IsAtOrAboveMaximumKelvin).IsTrue();
        await Assert.That(bucket.TryGetIntegerKelvin(out var observedIntegerKelvin)).IsFalse();
        await Assert.That(observedIntegerKelvin).IsEqualTo(0);
    }

    [Test]
    public async Task BucketCount_WhenInspected_IsDerivedFromReviewedOniMaximum()
    {
        var observedBucketCount = await ReadConstant(nameof(TemperatureDecisionBucket.BucketCount));
        var expectedBucketCount =
            1 + OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;

        await Assert.That(observedBucketCount).IsEqualTo(expectedBucketCount);
        await Assert.That(observedBucketCount).IsEqualTo(10002);
    }

    [Test]
    public async Task NamedOrdinals_WhenInspected_DescribeEveryBucketBoundary()
    {
        await Assert.That((await ReadConstant(nameof(TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)))).IsEqualTo(0);
        await Assert.That((await ReadConstant(nameof(TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)))).IsEqualTo(1);
        await Assert.That((await ReadConstant(nameof(TemperatureDecisionBucket.HighestIntegerKelvinOrdinal)))).IsEqualTo(10000);
        await Assert.That((await ReadConstant(nameof(TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)))).IsEqualTo(10001);
    }

    [Test]
    public async Task DefaultBucket_WhenInspected_IsBelowMinimumSentinel()
    {
        var bucket = default(TemperatureDecisionBucket);

        await Assert.That(bucket.Ordinal).IsEqualTo(TemperatureDecisionBucket.BelowMinimumKelvinOrdinal);
        await Assert.That(bucket.IsBelowMinimumKelvin).IsTrue();
    }

    [Test]
    public async Task EqualityAndComparison_WhenOrdinalsMatch_AreValueBased()
    {
        var first = TemperatureDecisionBucket.FromIntegerKelvin(273);
        var second = TemperatureDecisionBucket.FromTemperature(273.999f);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(first.Equals(second)).IsTrue();
        await Assert.That(first.Equals((object)second)).IsTrue();
        await Assert.That(second.GetHashCode()).IsEqualTo(first.GetHashCode());
        await Assert.That(first.CompareTo(second)).IsEqualTo(0);
        await Assert.That(TemperatureDecisionBucket.FromIntegerKelvin(272).CompareTo(first) < 0).IsTrue();
        await Assert.That(TemperatureDecisionBucket.FromIntegerKelvin(274).CompareTo(first) > 0).IsTrue();
    }

    private static async Task<object?> ReadConstant(string fieldName)
    {
        var field = typeof(TemperatureDecisionBucket).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"Expected constant {fieldName} was not emitted.");
        await Assert.That(field.IsLiteral).IsTrue();
        return field.GetRawConstantValue();
    }
}
