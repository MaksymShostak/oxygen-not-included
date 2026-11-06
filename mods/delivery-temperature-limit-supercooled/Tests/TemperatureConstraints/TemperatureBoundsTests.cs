namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;
public sealed class TemperatureBoundsTests
{
    [Test]
    public async Task Unbounded_WhenEvaluated_HasNoBoundsAndIsNotHazardous()
    {
        var bounds = TemperatureBounds.Unbounded;

        await Assert.That(bounds.IsUnbounded).IsTrue();
        await Assert.That(bounds.IsEmpty).IsFalse();
        await Assert.That(bounds.IsEqualBounds).IsFalse();
        await Assert.That(bounds.LowerKelvin).IsNull();
        await Assert.That(bounds.UpperKelvin).IsNull();
    }

    [Test]
    public async Task FromConstraint_WhenDisabled_MapsToUnbounded()
    {
        var disabledConstraint = DeliveryTemperatureConstraint.FromSerializedLimits(0, 0);
        var bounds = TemperatureBounds.FromConstraint(disabledConstraint);

        await Assert.That(bounds.IsUnbounded).IsTrue();
        await Assert.That(bounds).IsEqualTo(TemperatureBounds.Unbounded);
    }

    [Test]
    public async Task FromConstraint_WhenFloorAndCeiling_MapsToUnbounded()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(0, 10000);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        await Assert.That(bounds.IsUnbounded).IsTrue();
        await Assert.That(bounds.LowerKelvin).IsNull();
        await Assert.That(bounds.UpperKelvin).IsNull();
    }

    [Test]
    public async Task FromConstraint_WhenLowerOnly_PreservesLowerAndUnboundsUpper()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(250, 10000);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        await Assert.That(bounds.IsUnbounded).IsFalse();
        await Assert.That(bounds.IsEmpty).IsFalse();
        await Assert.That(bounds.LowerKelvin).IsEqualTo(250);
        await Assert.That(bounds.UpperKelvin).IsNull();
    }

    [Test]
    public async Task FromConstraint_WhenUpperOnly_PreservesUpperAndUnboundsLower()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(0, 350);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        await Assert.That(bounds.IsUnbounded).IsFalse();
        await Assert.That(bounds.IsEmpty).IsFalse();
        await Assert.That(bounds.LowerKelvin).IsNull();
        await Assert.That(bounds.UpperKelvin).IsEqualTo(350);
    }

    [Test]
    public async Task FromConstraint_WhenBoundedRange_PreservesBothEndpoints()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(273, 373);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        await Assert.That(bounds.IsUnbounded).IsFalse();
        await Assert.That(bounds.IsEmpty).IsFalse();
        await Assert.That(bounds.IsEqualBounds).IsFalse();
        await Assert.That(bounds.LowerKelvin).IsEqualTo(273);
        await Assert.That(bounds.UpperKelvin).IsEqualTo(373);
    }

    [Test]
    public async Task FromConstraint_WhenEqualBounds_FlagsHazardousEmptyRange()
    {
        var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(300, 300);
        var bounds = TemperatureBounds.FromConstraint(constraint);

        await Assert.That(bounds.IsEmpty).IsTrue();
        await Assert.That(bounds.IsEqualBounds).IsTrue();
        await Assert.That(bounds.LowerKelvin).IsEqualTo(300);
        await Assert.That(bounds.UpperKelvin).IsEqualTo(300);
    }

    [Test]
    public async Task ToConstraint_WhenUnbounded_ReturnsDisabledConstraint()
    {
        var constraint = TemperatureBounds.Unbounded.ToConstraint();

        await Assert.That(constraint.IsEnabled).IsFalse();
        await Assert.That(constraint.MaximumExclusiveKelvin).IsEqualTo(0);
    }

    [Test]
    public async Task ToConstraint_WhenOneSidedOrBounded_MapsToExpectedKelvinLimits()
    {
        var lowOnly = new TemperatureBounds(200, null).ToConstraint();
        await Assert.That(lowOnly.IsEnabled).IsTrue();
        await Assert.That(lowOnly.MinimumInclusiveKelvin).IsEqualTo(200);
        await Assert.That(lowOnly.MaximumExclusiveKelvin).IsEqualTo(10000);

        var highOnly = new TemperatureBounds(null, 300).ToConstraint();
        await Assert.That(highOnly.IsEnabled).IsTrue();
        await Assert.That(highOnly.MinimumInclusiveKelvin).IsEqualTo(0);
        await Assert.That(highOnly.MaximumExclusiveKelvin).IsEqualTo(300);

        var bounded = new TemperatureBounds(250, 350).ToConstraint();
        await Assert.That(bounded.IsEnabled).IsTrue();
        await Assert.That(bounded.MinimumInclusiveKelvin).IsEqualTo(250);
        await Assert.That(bounded.MaximumExclusiveKelvin).IsEqualTo(350);
    }

    [Test]
    public async Task ToSerializedLimits_WhenInvoked_ProducesExpectedTuple()
    {
        var (unboundedLow, unboundedHigh) = TemperatureBounds.Unbounded.ToSerializedLimits();
        await Assert.That(unboundedLow).IsEqualTo(0);
        await Assert.That(unboundedHigh).IsEqualTo(0);

        var (lowOnlyLow, lowOnlyHigh) = new TemperatureBounds(150, null).ToSerializedLimits();
        await Assert.That(lowOnlyLow).IsEqualTo(150);
        await Assert.That(lowOnlyHigh).IsEqualTo(10000);

        var (boundedLow, boundedHigh) = new TemperatureBounds(280, 320).ToSerializedLimits();
        await Assert.That(boundedLow).IsEqualTo(280);
        await Assert.That(boundedHigh).IsEqualTo(320);
    }

    [Test]
    public async Task Equals_WhenCompared_FollowsValueEquality()
    {
        var first = new TemperatureBounds(100, 200);
        var second = new TemperatureBounds(100, 200);
        var different = new TemperatureBounds(100, 201);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second.GetHashCode()).IsEqualTo(first.GetHashCode());
        await Assert.That(different).IsNotEqualTo(first);
        await Assert.That(first.Equals((object)second)).IsTrue();
    }
}
