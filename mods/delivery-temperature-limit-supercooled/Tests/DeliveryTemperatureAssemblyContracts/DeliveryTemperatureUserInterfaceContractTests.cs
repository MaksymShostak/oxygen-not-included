#nullable enable

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class DeliveryTemperatureUserInterfaceContractTests
{
    [TestMethod]
    public void TemperatureLimitWidget_WhenBuilt_NeutralizesRoguePLibKScreenAndAddsInputFieldMarker()
    {
        string sourceRoot = ResolveSourceRoot();
        string widgetSource = File.ReadAllText(
            Path.Combine(sourceRoot, "TemperatureLimitUserInterface", "TemperatureLimitWidget.cs"));

        // Must destroy the rogue KScreen (PTextFieldEvents) on realized text inputs
        StringAssert.Contains(
            widgetSource,
            "realizedInput.GetComponent<KScreen>()",
            "TemperatureLimitWidget must locate rogue KScreen component on realized input.");
        StringAssert.Contains(
            widgetSource,
            "DestroyImmediate",
            "TemperatureLimitWidget must destroy the rogue KScreen component before it can register in KScreenManager.");

        // Must add InputField marker so CameraController.WithinInputField() detects focus
        StringAssert.Contains(
            widgetSource,
            "realizedInput.AddComponent<InputField>()",
            "TemperatureLimitWidget must attach an InputField component so CameraController.WithinInputField() suppresses WASD panning only while focused.");

        // Must deactivate input fields in OnDisable
        StringAssert.Contains(
            widgetSource,
            "DeactivateInputField",
            "TemperatureLimitWidget.OnDisable must deactivate any focused text fields.");
    }

    [TestMethod]
    public void TemperatureLimitSideScreen_WhenInspected_DoesNotPollOrSwallowKeysInUpdate()
    {
        string sourceRoot = ResolveSourceRoot();
        string sideScreenSource = File.ReadAllText(
            Path.Combine(sourceRoot, "TemperatureLimitUserInterface", "TemperatureLimitSideScreen.cs"));

        Assert.IsFalse(
            sideScreenSource.Contains("Input.GetKeyDown(KeyCode.W)", StringComparison.Ordinal),
            "TemperatureLimitSideScreen must not poll WASD keys in Update.");
        Assert.IsFalse(
            sideScreenSource.Contains("[DEBUG-KEY]", StringComparison.Ordinal),
            "TemperatureLimitSideScreen must not retain diagnostic debug log probes.");
    }

    private static string ResolveSourceRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(
                directory,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new DirectoryNotFoundException(
            "Could not resolve the delivery-temperature-limit-supercooled Source directory.");
    }
}
