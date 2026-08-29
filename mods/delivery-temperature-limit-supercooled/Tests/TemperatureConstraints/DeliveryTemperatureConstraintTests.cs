namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;

[TestClass]
public sealed class DeliveryTemperatureConstraintTests
{
    [DataRow(9.999f, false)] // C# truncates this value to 9 K.
    [DataRow(10.0f, true)]
    [DataRow(19.999f, true)] // C# truncates this value to 19 K.
    [DataRow(20.0f, false)]
    [TestMethod]
    public void Allows_WhenTemperatureIsComparedWithInclusiveExclusiveBounds_ReturnsExpectedDecision(
        float temperatureKelvin,
        bool expectedDecision)
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 10,
            serializedHighLimit: 20);

        Assert.AreEqual(expectedDecision, constraint.Allows(temperatureKelvin));
    }

    [TestMethod]
    public void FromSerializedLimits_WhenEnabledMinimumIsNotBelowMaximum_PreservesEmptyConstraint()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 100,
            serializedHighLimit: 100);

        Assert.IsTrue(constraint.IsEnabled);
        Assert.IsTrue(constraint.IsEmpty);
        Assert.IsFalse(constraint.Allows(100.0f));
    }

    [TestMethod]
    public void FromSerializedLimits_WhenHighIsZero_ReturnsDisabledConstraint()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 400,
            serializedHighLimit: 0);

        Assert.AreEqual(400, constraint.MinimumInclusiveKelvin);
        Assert.AreEqual(0, constraint.MaximumExclusiveKelvin);
        Assert.IsFalse(constraint.IsEnabled);
        Assert.IsFalse(constraint.IsEmpty);
    }

    [TestMethod]
    public void FromSerializedLimits_WhenHighClampsToZero_ReportsNotEmpty()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 100,
            serializedHighLimit: -1);

        Assert.AreEqual(100, constraint.MinimumInclusiveKelvin);
        Assert.AreEqual(0, constraint.MaximumExclusiveKelvin);
        Assert.IsFalse(constraint.IsEnabled);
        Assert.IsFalse(constraint.IsEmpty);
    }

    [TestMethod]
    public void FromSerializedLimits_WhenValuesExceedBounds_ClampsBothValues()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1000);

        Assert.AreEqual(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin,
            constraint.MinimumInclusiveKelvin);
        Assert.AreEqual(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin,
            constraint.MaximumExclusiveKelvin);
        Assert.IsTrue(constraint.IsEnabled);
        Assert.IsTrue(constraint.IsEmpty);
    }

    [TestMethod]
    public void FromSerializedLimits_WhenValuesAreNegative_ClampsBothValuesToZero()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: -100,
            serializedHighLimit: -1);

        Assert.AreEqual(0, constraint.MinimumInclusiveKelvin);
        Assert.AreEqual(0, constraint.MaximumExclusiveKelvin);
        Assert.IsFalse(constraint.IsEnabled);
        Assert.IsFalse(constraint.IsEmpty);
    }

    [TestMethod]
    public void Allows_WhenDisabledTemperatureIsBelowMinimum_ReturnsTrue()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 400,
            serializedHighLimit: 0);

        Assert.IsTrue(constraint.Allows(-100.0f));
    }

    [TestMethod]
    public void Allows_WhenDisabledTemperatureIsAtOrAboveMaximum_ReturnsTrue()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 400,
            serializedHighLimit: 0);

        Assert.IsTrue(constraint.Allows(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1000.0f));
    }

    [DataRow(-1.0f, false)]
    [DataRow(-0.999f, true)]
    [DataRow(-0.001f, true)]
    [DataRow(0.0f, true)]
    [TestMethod]
    public void Allows_WhenTemperatureHasNegativeFraction_TruncatesTowardZero(
        float temperatureKelvin,
        bool expectedDecision)
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: 1);

        Assert.AreEqual(expectedDecision, constraint.Allows(temperatureKelvin));
    }

    [DataRow(9999.999f, true)]
    [DataRow(10000.0f, false)]
    [DataRow(10000.999f, false)]
    [DataRow(20000.0f, false)]
    [TestMethod]
    public void Allows_WhenMaximumIsOniStorableTemperatureMaximum_RejectsExactMaximumAndAbove(
        float temperatureKelvin,
        bool expectedDecision)
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin);

        Assert.AreEqual(expectedDecision, constraint.Allows(temperatureKelvin));
    }

    [TestMethod]
    public void Allows_WhenEnabledTemperatureIsExactlyOniMaximum_ReturnsFalse()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin);

        Assert.IsFalse(constraint.Allows(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin));
    }

    [TestMethod]
    public void Allows_WhenDisabledTemperatureIsExactlyOniMaximum_ReturnsTrue()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: 0);

        Assert.IsTrue(constraint.Allows(
            OniStorableTemperatureBounds.MaximumTemperatureKelvin));
    }

    [TestMethod]
    public void DefaultConstraint_WhenInspected_IsDisabledAndNotEmpty()
    {
        var constraint = default(DeliveryTemperatureConstraint);

        Assert.AreEqual(0, constraint.MinimumInclusiveKelvin);
        Assert.AreEqual(0, constraint.MaximumExclusiveKelvin);
        Assert.IsFalse(constraint.IsEnabled);
        Assert.IsFalse(constraint.IsEmpty);
        Assert.IsTrue(constraint.Allows(300.0f));
    }

    [TestMethod]
    public void Equality_WhenNormalizedValuesMatch_IsValueBased()
    {
        var first = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: -1,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1);
        var second = DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: OniStorableTemperatureBounds.MaximumTemperatureKelvin);

        Assert.AreEqual(first, second);
        Assert.IsTrue(first.Equals(second));
        Assert.IsTrue(first.Equals((object)second));
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equality_WhenNormalizedValuesDiffer_ReturnsFalse()
    {
        var first = DeliveryTemperatureConstraint.FromSerializedLimits(10, 20);
        var second = DeliveryTemperatureConstraint.FromSerializedLimits(10, 21);

        Assert.AreNotEqual(first, second);
        Assert.IsFalse(first.Equals(second));
    }
}
