using System.Reflection;
using System.Runtime.Loader;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class TemperatureDisplayTextTests
{
    [Test]
    [Arguments("-273", " °C", "-273\u00a0°C")]
    [Arguments("-459", " °F", "-459\u00a0°F")]
    [Arguments("10000", " K", "10000\u00a0K")]
    [Arguments("9 727", " °C", "9 727\u00a0°C")]
    [Arguments("9\u202f727,5", " °C", "9\u202f727,5\u00a0°C")]
    [Arguments("20", "\u00a0°C", "20\u00a0°C")]
    public async Task NumberAndUnit_StayTogetherWithoutChangingLocalizedNumberText(
        string number, string suffix, string expected)
    {
        var context = new AssemblyLoadContext("TemperatureDisplayText", isCollectible: true);
        try
        {
            Type? type = context.LoadFromAssemblyPath(BuildPath())
                .GetType("DeliveryTemperatureLimit.TemperatureDisplayText");
            if (type is null) throw new TUnit.Assertions.Exceptions.AssertionException("Temperature messages need nonbreaking number/unit spacing.");
            MethodInfo? format = type.GetMethod("WithUnit", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(format);
            await Assert.That(format.Invoke(null, [number, suffix])).IsEqualTo(expected);
        }
        finally { context.Unload(); }
    }

    [Test]
    public async Task RangeAndValidationTemperatures_UseNonbreakingUnitFormatting()
    {
        var body = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(BuildPath(),
            "DeliveryTemperatureLimit.TemperatureLimitPresenter", "FormatTemperatureWithUnit").Single();
        await Assert.That(body.Instructions.Any(i => i.ResolvedOperand ==
            "DeliveryTemperatureLimit.TemperatureDisplayText.WithUnit")).IsTrue().Because("The presenter must apply unit spacing to the temperatures inserted into help and errors.");
    }

    private static string BuildPath()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Skip.Test("Run with DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH.");
        return builds.Single().AssemblyPath;
    }
}
