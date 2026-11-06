namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;
public sealed class OniStorableTemperatureBoundsTests
{
    [Test]
    public async Task MinimumTemperatureKelvin_WhenRead_IsPreservedConfigurableFloor()
    {
        await Assert.That((await ReadConstant(nameof(OniStorableTemperatureBounds.MinimumTemperatureKelvin)))).IsEqualTo(0);
    }

    [Test]
    public async Task MaximumTemperatureKelvin_WhenRead_MatchesReviewedOniStorableBound()
    {
        await Assert.That((await ReadConstant(nameof(OniStorableTemperatureBounds.MaximumTemperatureKelvin)))).IsEqualTo(10000);
    }

    private static async Task<object?> ReadConstant(string fieldName)
    {
        var field = typeof(OniStorableTemperatureBounds).GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"Linked production constant {fieldName} was not emitted into the test assembly.");
        await Assert.That(field.IsLiteral).IsTrue();
        return field.GetRawConstantValue();
    }
}
