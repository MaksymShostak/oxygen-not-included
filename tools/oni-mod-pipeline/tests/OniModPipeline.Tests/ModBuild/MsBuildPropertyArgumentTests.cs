using MaksymShostak.OniModPipeline.ModBuild;

namespace MaksymShostak.OniModPipeline.Tests.ModBuild;
public sealed class MsBuildPropertyArgumentTests
{
    [Test]
    [Arguments("Property")]
    [Arguments("_Property2")]
    [Arguments("OniManagedAssemblyDirectory")]
    public async Task Create_WhenNameMatchesApprovedGrammar_ReturnsOneQuotedToken(string name)
    {
        var result = MsBuildPropertyArgument.Create(name, "value");

        await Assert.That(result).IsEqualTo($"-p:{name}=\"value\"");
    }

    [Test]
    [Arguments("")]
    [Arguments("1Property")]
    [Arguments("Property.Name")]
    [Arguments("Property-Name")]
    [Arguments("Property Name")]
    public async Task Create_WhenNameDoesNotMatchApprovedGrammar_Throws(string name)
    {
        await Assert.That(() =>
            MsBuildPropertyArgument.Create(name, "value")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    [Arguments("contains\0nul")]
    [Arguments("contains\rreturn")]
    [Arguments("contains\nnewline")]
    [Arguments("contains\ttab")]
    [Arguments("contains\"quote")]
    public async Task Create_WhenValueContainsControlOrDoubleQuote_Throws(string value)
    {
        await Assert.That(() =>
            MsBuildPropertyArgument.Create("Property", value)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    [Arguments("path with spaces", "path with spaces")]
    [Arguments("path;with;semicolons", "path%3Bwith%3Bsemicolons")]
    [Arguments("value=with=equals", "value=with=equals")]
    [Arguments("literal%3Bsequence", "literal%253Bsequence")]
    [Arguments("C:\\trailing\\", "C:\\trailing\\")]
    [Arguments("zăpadă/温度", "zăpadă/温度")]
    public async Task Create_WhenValueContainsSupportedSpecialCharacters_UsesMsBuildEscaping(
        string value,
        string escapedValue)
    {
        var result = MsBuildPropertyArgument.Create("Property", value);

        await Assert.That(result).IsEqualTo($"-p:Property=\"{escapedValue}\"");
    }
}
