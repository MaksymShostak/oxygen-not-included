using MaksymShostak.OniModPipeline.ModBuild;

namespace MaksymShostak.OniModPipeline.Tests.ModBuild;

[TestClass]
public sealed class MsBuildPropertyArgumentTests
{
    [TestMethod]
    [DataRow("Property")]
    [DataRow("_Property2")]
    [DataRow("OniManagedAssemblyDirectory")]
    public void Create_WhenNameMatchesApprovedGrammar_ReturnsOneQuotedToken(string name)
    {
        var result = MsBuildPropertyArgument.Create(name, "value");

        Assert.AreEqual($"-p:{name}=\"value\"", result);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1Property")]
    [DataRow("Property.Name")]
    [DataRow("Property-Name")]
    [DataRow("Property Name")]
    public void Create_WhenNameDoesNotMatchApprovedGrammar_Throws(string name)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            MsBuildPropertyArgument.Create(name, "value"));
    }

    [TestMethod]
    [DataRow("contains\0nul")]
    [DataRow("contains\rreturn")]
    [DataRow("contains\nnewline")]
    [DataRow("contains\ttab")]
    [DataRow("contains\"quote")]
    public void Create_WhenValueContainsControlOrDoubleQuote_Throws(string value)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            MsBuildPropertyArgument.Create("Property", value));
    }

    [TestMethod]
    [DataRow("path with spaces", "path with spaces")]
    [DataRow("path;with;semicolons", "path%3Bwith%3Bsemicolons")]
    [DataRow("value=with=equals", "value=with=equals")]
    [DataRow("literal%3Bsequence", "literal%253Bsequence")]
    [DataRow("C:\\trailing\\", "C:\\trailing\\")]
    [DataRow("zăpadă/温度", "zăpadă/温度")]
    public void Create_WhenValueContainsSupportedSpecialCharacters_UsesMsBuildEscaping(
        string value,
        string escapedValue)
    {
        var result = MsBuildPropertyArgument.Create("Property", value);

        Assert.AreEqual($"-p:Property=\"{escapedValue}\"", result);
    }
}
