namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportPathRedactorTests
{
    [Test]
    public async Task Redact_WhenRulesOverlap_AppliesLongestPrefixAndReportsDeterministicPlaceholders()
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

        await Assert.That(result.Content).IsEqualTo(@"Log <ONI_DATA>\Player.log then <USER_PROFILE>\AppData\LocalLow.");
        await Assert.That(result.AppliedPlaceholders.ToArray()).IsEquivalentTo(new[] { "<ONI_DATA>", "<USER_PROFILE>" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Redact_WhenWindowsRuleMatchesForwardSlashLogPath_ReplacesEquivalentSeparatorForm()
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

        await Assert.That(result.Content).IsEqualTo("Log <ONI_DATA>/mods/Dev/TemperatureLimit.");
        await Assert.That(result.AppliedPlaceholders.ToArray()).IsEquivalentTo(new[] { "<ONI_DATA>" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Redact_WhenComparisonIsOrdinal_LeavesDifferentlyCasedPathUntouched()
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

        await Assert.That(result.Content).IsEqualTo(@"c:\users\max\Player.log");
        await Assert.That(result.AppliedPlaceholders).IsEmpty();
    }

    [Test]
    public async Task Redact_WhenComparisonIgnoresCase_ReplacesDifferentlyCasedPath()
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

        await Assert.That(result.Content).IsEqualTo(@"<USER_PROFILE>\Player.log");
        await Assert.That(result.AppliedPlaceholders.ToArray()).IsEquivalentTo(new[] { "<USER_PROFILE>" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Redact_WhenPrefixAppearsInsideDifferentPathSegment_DoesNotReplaceIt()
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

        await Assert.That(result.Content).IsEqualTo(@"C:\Users\Maximum\Player.log");
        await Assert.That(result.AppliedPlaceholders).IsEmpty();
    }

    [Test]
    public async Task Constructor_WhenRuleIsBlankOrDuplicate_RejectsAmbiguousRedaction()
    {
        await Assert.That(() =>
            new SupportPathRedactionRule(" ", "<USER_PROFILE>")).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            new SupportPathRedactionRule(@"C:\Users\Max", " ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
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
                StringComparison.OrdinalIgnoreCase)).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
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
                StringComparison.OrdinalIgnoreCase)).ThrowsExactly<ArgumentException>();
    }
}
