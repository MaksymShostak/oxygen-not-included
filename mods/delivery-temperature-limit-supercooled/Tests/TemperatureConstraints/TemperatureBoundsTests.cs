namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;

[TestClass]
public sealed class TemperatureBoundsTests
{
    [TestMethod]
    public void Unbounded_WhenEvaluated_HasNoBoundsAndIsNotHazardous()
    {
        var bounds = TemperatureBounds.Unbounded;

        Assert.IsTrue(bounds.IsUnbounded);
        Assert.IsFalse(bounds.IsEmpty);
        Assert.IsFalse(bounds.IsEqualBounds);
        Assert.IsNull(bounds.LowerKelvin);
        Assert.IsNull(bounds.UpperKelvin);
    }

    [TestMethod]
    public void FromConstraint_WhenDisabled_MapsToUnbounded()
    {
        var disabledConstraint = DeliveryTemperatureConstraint.FromSerializedLimits(0, 0);
        var bounds = TemperatureBounds.FromConstraint(disabledConstraint);

        Assert.IsTrue(bounds.IsUnbounded);
        Assert.AreEqual(TemperatureBounds.Unbounded, bounds);
    }

    [TestMethod]
    public void FromConstraint_WhenFloorAndCeiling_MapsToUnbounded()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(0, 10000);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        Assert.IsTrue(bounds.IsUnbounded);
        Assert.IsNull(bounds.LowerKelvin);
        Assert.IsNull(bounds.UpperKelvin);
    }

    [TestMethod]
    public void FromConstraint_WhenLowerOnly_PreservesLowerAndUnboundsUpper()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(250, 10000);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        Assert.IsFalse(bounds.IsUnbounded);
        Assert.IsFalse(bounds.IsEmpty);
        Assert.AreEqual(250, bounds.LowerKelvin);
        Assert.IsNull(bounds.UpperKelvin);
    }

    [TestMethod]
    public void FromConstraint_WhenUpperOnly_PreservesUpperAndUnboundsLower()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(0, 350);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        Assert.IsFalse(bounds.IsUnbounded);
        Assert.IsFalse(bounds.IsEmpty);
        Assert.IsNull(bounds.LowerKelvin);
        Assert.AreEqual(350, bounds.UpperKelvin);
    }

    [TestMethod]
    public void FromConstraint_WhenBoundedRange_PreservesBothEndpoints()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(273, 373);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        Assert.IsFalse(bounds.IsUnbounded);
        Assert.IsFalse(bounds.IsEmpty);
        Assert.IsFalse(bounds.IsEqualBounds);
        Assert.AreEqual(273, bounds.LowerKelvin);
        Assert.AreEqual(373, bounds.UpperKelvin);
    }

    [TestMethod]
    public void FromConstraint_WhenEqualBounds_FlagsHazardousEmptyRange()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(300, 300);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        Assert.IsTrue(bounds.IsEmpty);
        Assert.IsTrue(bounds.IsEqualBounds);
        Assert.AreEqual(300, bounds.LowerKelvin);
        Assert.AreEqual(300, bounds.UpperKelvin);
    }

    [TestMethod]
    public void ToConstraint_WhenUnbounded_ReturnsDisabledConstraint()
    {
        var constraint = TemperatureBounds.Unbounded.ToConstraint();

        Assert.IsFalse(constraint.IsEnabled);
        Assert.AreEqual(0, constraint.MaximumExclusiveKelvin);
    }

    [TestMethod]
    public void ToConstraint_WhenOneSidedOrBounded_MapsToExpectedKelvinLimits()
    {
        var lowOnly = new TemperatureBounds(200, null).ToConstraint();
        Assert.IsTrue(lowOnly.IsEnabled);
        Assert.AreEqual(200, lowOnly.MinimumInclusiveKelvin);
        Assert.AreEqual(10000, lowOnly.MaximumExclusiveKelvin);

        var highOnly = new TemperatureBounds(null, 300).ToConstraint();
        Assert.IsTrue(highOnly.IsEnabled);
        Assert.AreEqual(0, highOnly.MinimumInclusiveKelvin);
        Assert.AreEqual(300, highOnly.MaximumExclusiveKelvin);

        var bounded = new TemperatureBounds(250, 350).ToConstraint();
        Assert.IsTrue(bounded.IsEnabled);
        Assert.AreEqual(250, bounded.MinimumInclusiveKelvin);
        Assert.AreEqual(350, bounded.MaximumExclusiveKelvin);
    }

    [TestMethod]
    public void ToSerializedLimits_WhenInvoked_ProducesExpectedTuple()
    {
        var (unboundedLow, unboundedHigh) = TemperatureBounds.Unbounded.ToSerializedLimits();
        Assert.AreEqual(0, unboundedLow);
        Assert.AreEqual(0, unboundedHigh);

        var (lowOnlyLow, lowOnlyHigh) = new TemperatureBounds(150, null).ToSerializedLimits();
        Assert.AreEqual(150, lowOnlyLow);
        Assert.AreEqual(10000, lowOnlyHigh);

        var (boundedLow, boundedHigh) = new TemperatureBounds(280, 320).ToSerializedLimits();
        Assert.AreEqual(280, boundedLow);
        Assert.AreEqual(320, boundedHigh);
    }

    [TestMethod]
    public void Equals_WhenCompared_FollowsValueEquality()
    {
        var first = new TemperatureBounds(100, 200);
        var second = new TemperatureBounds(100, 200);
        var different = new TemperatureBounds(100, 201);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreNotEqual(first, different);
        Assert.IsTrue(first.Equals((object)second));
    }
}
