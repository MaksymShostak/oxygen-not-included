namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;

[TestClass]
public sealed class OniStorableTemperatureBoundsTests
{
    [TestMethod]
    public void MinimumTemperatureKelvin_WhenRead_IsPreservedConfigurableFloor()
    {
        Assert.AreEqual(
            0,
            ReadConstant(nameof(OniStorableTemperatureBounds.MinimumTemperatureKelvin)));
    }

    [TestMethod]
    public void MaximumTemperatureKelvin_WhenRead_MatchesReviewedOniStorableBound()
    {
        Assert.AreEqual(
            10000,
            ReadConstant(nameof(OniStorableTemperatureBounds.MaximumTemperatureKelvin)));
    }

    private static object? ReadConstant(string fieldName)
    {
        var field = typeof(OniStorableTemperatureBounds).GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(
            field,
            $"Linked production constant {fieldName} was not emitted into the test assembly.");
        Assert.IsTrue(field.IsLiteral);
        return field.GetRawConstantValue();
    }
}
