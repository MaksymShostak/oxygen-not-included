using System.Reflection;
using System.Runtime.Loader;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class TemperatureDisplayTextTests
{
    [TestMethod]
    [DataRow("-273", " °C", "-273\u00a0°C")]
    [DataRow("-459", " °F", "-459\u00a0°F")]
    [DataRow("10000", " K", "10000\u00a0K")]
    [DataRow("9 727", " °C", "9 727\u00a0°C")]
    [DataRow("9\u202f727,5", " °C", "9\u202f727,5\u00a0°C")]
    [DataRow("20", "\u00a0°C", "20\u00a0°C")]
    public void NumberAndUnit_StayTogetherWithoutChangingLocalizedNumberText(
        string number, string suffix, string expected)
    {
        var context = new AssemblyLoadContext("TemperatureDisplayText", isCollectible: true);
        try
        {
            Type? type = context.LoadFromAssemblyPath(BuildPath())
                .GetType("DeliveryTemperatureLimit.TemperatureDisplayText");
            Assert.IsNotNull(type, "Temperature messages need nonbreaking number/unit spacing.");
            MethodInfo? format = type.GetMethod("WithUnit", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(format);
            Assert.AreEqual(expected, format.Invoke(null, [number, suffix]));
        }
        finally { context.Unload(); }
    }

    [TestMethod]
    public void RangeAndValidationTemperatures_UseNonbreakingUnitFormatting()
    {
        var body = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(BuildPath(),
            "DeliveryTemperatureLimit.TemperatureLimitPresenter", "FormatTemperatureWithUnit").Single();
        Assert.IsTrue(body.Instructions.Any(i => i.ResolvedOperand ==
            "DeliveryTemperatureLimit.TemperatureDisplayText.WithUnit"),
            "The presenter must apply unit spacing to the temperatures inserted into help and errors.");
    }

    private static string BuildPath()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Assert.Inconclusive("Run with DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH.");
        return builds.Single().AssemblyPath;
    }
}
