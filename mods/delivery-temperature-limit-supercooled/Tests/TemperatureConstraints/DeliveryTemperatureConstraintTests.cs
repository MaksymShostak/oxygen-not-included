namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;
public sealed class DeliveryTemperatureConstraintTests
{
    [Arguments(9.999f, false)] // C# truncates this value to 9 K.
    [Arguments(10.0f, true)]
    [Arguments(19.999f, true)] // C# truncates this value to 19 K.
    [Arguments(20.0f, false)]
    [Test]
    public async Task Allows_WhenTemperatureIsComparedWithInclusiveExclusiveBounds_ReturnsExpectedDecision(
        float temperatureKelvin,
        bool expectedDecision)
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 10,
            serializedHighLimit: 20);

        await Assert.That(constraint.Allows(temperatureKelvin)).IsEqualTo(expectedDecision);
    }

    [Test]
    public async Task FromSerializedLimits_WhenEnabledMinimumIsNotBelowMaximum_PreservesEmptyConstraint()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 100,
            serializedHighLimit: 100);

        await Assert.That(constraint.IsEnabled).IsTrue();
        await Assert.That(constraint.IsEmpty).IsTrue();
        await Assert.That(constraint.Allows(100.0f)).IsFalse();
    }

    [Test]
    public async Task FromSerializedLimits_WhenHighIsZero_ReturnsDisabledConstraint()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 400,
            serializedHighLimit: 0);

        await Assert.That(constraint.MinimumInclusiveKelvin).IsEqualTo(400);
        await Assert.That(constraint.MaximumExclusiveKelvin).IsEqualTo(0);
        await Assert.That(constraint.IsEnabled).IsFalse();
        await Assert.That(constraint.IsEmpty).IsFalse();
    }

    [Test]
    public async Task FromSerializedLimits_WhenHighClampsToZero_ReportsNotEmpty()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 100,
            serializedHighLimit: -1);

        await Assert.That(constraint.MinimumInclusiveKelvin).IsEqualTo(100);
        await Assert.That(constraint.MaximumExclusiveKelvin).IsEqualTo(0);
        await Assert.That(constraint.IsEnabled).IsFalse();
        await Assert.That(constraint.IsEmpty).IsFalse();
    }

    [Test]
    public async Task FromSerializedLimits_WhenValuesExceedBounds_ClampsBothValues()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1000);

        await Assert.That(constraint.MinimumInclusiveKelvin).IsEqualTo(OniStorableTemperatureBounds.MaximumTemperatureKelvin);
        await Assert.That(constraint.MaximumExclusiveKelvin).IsEqualTo(OniStorableTemperatureBounds.MaximumTemperatureKelvin);
        await Assert.That(constraint.IsEnabled).IsTrue();
        await Assert.That(constraint.IsEmpty).IsTrue();
    }

    [Test]
    public async Task FromSerializedLimits_WhenValuesAreNegative_ClampsBothValuesToZero()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: -100,
            serializedHighLimit: -1);

        await Assert.That(constraint.MinimumInclusiveKelvin).IsEqualTo(0);
        await Assert.That(constraint.MaximumExclusiveKelvin).IsEqualTo(0);
        await Assert.That(constraint.IsEnabled).IsFalse();
        await Assert.That(constraint.IsEmpty).IsFalse();
    }

    [Test]
    public async Task Allows_WhenDisabledTemperatureIsBelowMinimum_ReturnsTrue()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 400,
            serializedHighLimit: 0);

        await Assert.That(constraint.Allows(-100.0f)).IsTrue();
    }

    [Test]
    public async Task Allows_WhenDisabledTemperatureIsAtOrAboveMaximum_ReturnsTrue()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 400,
            serializedHighLimit: 0);

        await Assert.That(constraint.Allows(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1000.0f)).IsTrue();
    }

    [Arguments(-1.0f, false)]
    [Arguments(-0.999f, true)]
    [Arguments(-0.001f, true)]
    [Arguments(0.0f, true)]
    [Test]
    public async Task Allows_WhenTemperatureHasNegativeFraction_TruncatesTowardZero(
        float temperatureKelvin,
        bool expectedDecision)
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: 1);

        await Assert.That(constraint.Allows(temperatureKelvin)).IsEqualTo(expectedDecision);
    }

    [Arguments(9999.999f, true)]
    [Arguments(10000.0f, false)]
    [Arguments(10000.999f, false)]
    [Arguments(20000.0f, false)]
    [Test]
    public async Task Allows_WhenMaximumIsOniStorableTemperatureMaximum_RejectsExactMaximumAndAbove(
        float temperatureKelvin,
        bool expectedDecision)
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin);

        await Assert.That(constraint.Allows(temperatureKelvin)).IsEqualTo(expectedDecision);
    }

    [Test]
    public async Task Allows_WhenEnabledTemperatureIsExactlyOniMaximum_ReturnsFalse()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin);

        await Assert.That(constraint.Allows(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin)).IsFalse();
    }

    [Test]
    public async Task Allows_WhenDisabledTemperatureIsExactlyOniMaximum_ReturnsTrue()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: 0);

        await Assert.That(constraint.Allows(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin)).IsTrue();
    }

    [Test]
    public async Task DefaultConstraint_WhenInspected_IsDisabledAndNotEmpty()
    {
        var constraint = default(DeliveryTemperatureConstraint);

        await Assert.That(constraint.MinimumInclusiveKelvin).IsEqualTo(0);
        await Assert.That(constraint.MaximumExclusiveKelvin).IsEqualTo(0);
        await Assert.That(constraint.IsEnabled).IsFalse();
        await Assert.That(constraint.IsEmpty).IsFalse();
        await Assert.That(constraint.Allows(300.0f)).IsTrue();
    }

    [Test]
    public async Task Equality_WhenNormalizedValuesMatch_IsValueBased()
    {
        var first = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: -1,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1);
        var second = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(first.Equals(second)).IsTrue();
        await Assert.That(first.Equals((object)second)).IsTrue();
        await Assert.That(second.GetHashCode()).IsEqualTo(first.GetHashCode());
    }

    [Test]
    public async Task Equality_WhenNormalizedValuesDiffer_ReturnsFalse()
    {
        var first = DeliveryTemperatureConstraint.FromSerializedLimits(10, 20);
        var second = DeliveryTemperatureConstraint.FromSerializedLimits(10, 21);

        await Assert.That(second).IsNotEqualTo(first);
        await Assert.That(first.Equals(second)).IsFalse();
    }
}
