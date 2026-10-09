using MaksymShostak.OniModPipeline.Readme;

namespace MaksymShostak.OniModPipeline.Tests.Readme;

[TestClass]
public sealed class CompatibilityBadgeLabelsTests
{
    [TestMethod]
    [DataRow("VanillaYes.png", "Base game supported")]
    [DataRow("Dlc1Yes.png", "Spaced Out! supported")]
    [DataRow("Dlc2Yes.png", "The Frosty Planet Pack supported")]
    [DataRow("Dlc3Yes.png", "The Bionic Booster Pack supported")]
    [DataRow("Dlc4Yes.png", "The Prehistoric Planet Pack supported")]
    [DataRow("Dlc5Yes.png", "The Aquatic Planet Pack supported")]
    public void GeneratedDescription_SuppliesTheDomainLabelBeforeCanonicalization(string file, string label)
    {
        var url = "https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/" + file;
        Assert.AreEqual($"![{label}]({url})", CompatibilityBadgeLabels.RenderGeneratedDescription($"![]({url})"));
    }

    [TestMethod]
    public void GeneratedDescription_PreservesUnknownImagesAndExistingAuthoredLabels()
    {
        const string text = "![](https://example.org/unknown.png) ![Owner label](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc1Yes.png)";
        Assert.AreEqual(text, CompatibilityBadgeLabels.RenderGeneratedDescription(text));
    }
}
