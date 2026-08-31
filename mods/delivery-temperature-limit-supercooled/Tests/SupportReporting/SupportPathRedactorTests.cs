namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportPathRedactorTests
{
    [TestMethod]
    public void Redact_WhenRulesOverlap_AppliesLongestPrefixAndReportsDeterministicPlaceholders()
    {
        var redactor = new SupportPathRedactor(
            new[]
            {
                new SupportPathRedactionRule(
                    @"C:\Users\Максим",
                    "<USER_PROFILE>"),
                new SupportPathRedactionRule(
                    @"C:\Users\Максим\Documents\Klei\OxygenNotIncluded",
                    "<ONI_DATA>")
            },
            StringComparison.OrdinalIgnoreCase);

        RedactedSupportText result = redactor.Redact(
            @"Log C:\Users\Максим\Documents\Klei\OxygenNotIncluded\Player.log then C:\Users\Максим\AppData\LocalLow.");

        Assert.AreEqual(
            @"Log <ONI_DATA>\Player.log then <USER_PROFILE>\AppData\LocalLow.",
            result.Content);
        CollectionAssert.AreEqual(
            new[] { "<ONI_DATA>", "<USER_PROFILE>" },
            result.AppliedPlaceholders.ToArray());
    }

    [TestMethod]
    public void Redact_WhenWindowsRuleMatchesForwardSlashLogPath_ReplacesEquivalentSeparatorForm()
    {
        var redactor = new SupportPathRedactor(
            new[]
            {
                new SupportPathRedactionRule(
                    @"C:\Users\Max",
                    "<USER_PROFILE>"),
                new SupportPathRedactionRule(
                    @"C:\Users\Max\Documents\Klei\OxygenNotIncluded",
                    "<ONI_DATA>")
            },
            StringComparison.OrdinalIgnoreCase);

        RedactedSupportText result = redactor.Redact(
            "Log C:/Users/Max/Documents/Klei/OxygenNotIncluded/mods/Dev/TemperatureLimit.");

        Assert.AreEqual(
            "Log <ONI_DATA>/mods/Dev/TemperatureLimit.",
            result.Content);
        CollectionAssert.AreEqual(
            new[] { "<ONI_DATA>" },
            result.AppliedPlaceholders.ToArray());
    }

    [TestMethod]
    public void Redact_WhenComparisonIsOrdinal_LeavesDifferentlyCasedPathUntouched()
    {
        var redactor = new SupportPathRedactor(
            new[]
            {
                new SupportPathRedactionRule(
                    @"C:\Users\Max",
                    "<USER_PROFILE>")
            },
            StringComparison.Ordinal);

        RedactedSupportText result = redactor.Redact(
            @"c:\users\max\Player.log");

        Assert.AreEqual(@"c:\users\max\Player.log", result.Content);
        Assert.IsEmpty(result.AppliedPlaceholders);
    }

    [TestMethod]
    public void Redact_WhenComparisonIgnoresCase_ReplacesDifferentlyCasedPath()
    {
        var redactor = new SupportPathRedactor(
            new[]
            {
                new SupportPathRedactionRule(
                    @"C:\Users\Max",
                    "<USER_PROFILE>")
            },
            StringComparison.OrdinalIgnoreCase);

        RedactedSupportText result = redactor.Redact(
            @"c:\users\max\Player.log");

        Assert.AreEqual(@"<USER_PROFILE>\Player.log", result.Content);
        CollectionAssert.AreEqual(
            new[] { "<USER_PROFILE>" },
            result.AppliedPlaceholders.ToArray());
    }

    [TestMethod]
    public void Redact_WhenPrefixAppearsInsideDifferentPathSegment_DoesNotReplaceIt()
    {
        var redactor = new SupportPathRedactor(
            new[]
            {
                new SupportPathRedactionRule(
                    @"C:\Users\Max",
                    "<USER_PROFILE>")
            },
            StringComparison.OrdinalIgnoreCase);

        RedactedSupportText result = redactor.Redact(
            @"C:\Users\Maximum\Player.log");

        Assert.AreEqual(@"C:\Users\Maximum\Player.log", result.Content);
        Assert.IsEmpty(result.AppliedPlaceholders);
    }

    [TestMethod]
    public void Constructor_WhenRuleIsBlankOrDuplicate_RejectsAmbiguousRedaction()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SupportPathRedactionRule(" ", "<USER_PROFILE>"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SupportPathRedactionRule(@"C:\Users\Max", " "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SupportPathRedactor(
                new[]
                {
                    new SupportPathRedactionRule(
                        @"C:\Users\Max",
                        "<ONE>"),
                    new SupportPathRedactionRule(
                        @"c:\users\max",
                        "<TWO>")
                },
                StringComparison.OrdinalIgnoreCase));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SupportPathRedactor(
                new[]
                {
                    new SupportPathRedactionRule(
                        @"C:\Users\Max",
                        "<ONE>"),
                    new SupportPathRedactionRule(
                        "C:/Users/Max",
                        "<TWO>")
                },
                StringComparison.OrdinalIgnoreCase));
    }
}
