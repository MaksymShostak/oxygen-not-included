using DeliveryTemperatureLimit;

namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportReportSessionTests
{
    [Test]
    public async Task NewDialog_DoesNotIncludeTheGameLog()
    {
        await Assert.That(new SupportReportSession().IncludeGameLog).IsFalse();
    }

    [Test]
    public async Task CreateAndOpen_PreservesTheLogChoiceAndPrefillsOnlyTheFreshSummary()
    {
        var session = new SupportReportSession { IncludeGameLog = true };
        var events = new List<string>();
        SupportReportKind? observedKind = null;
        string? observedUrl = null;
        await Assert.That(session.Create(true, kind =>
        {
            observedKind = kind;
            events.Add("created");
            return "fresh diagnostic summary";
        }, url =>
        {
            observedUrl = url;
            events.Add("opened");
        })).IsTrue();
        await Assert.That(observedKind).IsEqualTo(SupportReportKind.ExtendedPlayerLog);
        await Assert.That(observedUrl).IsEqualTo(SupportIssueUrlBuilder.Create("fresh diagnostic summary").Value);
        await Assert.That(events).IsEquivalentTo(new[] { "created", "opened" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(session.IncludeGameLog).IsTrue();

        await Assert.That(session.Create(true, _ => null,
            _ => Assert.Fail("A failed attempt must not open a form with a previous report."))).IsFalse();
        await Assert.That(session.IncludeGameLog).IsTrue();
    }

    [Test]
    public async Task LocalReport_DoesNotOpenTheBrowser()
    {
        var session = new SupportReportSession();
        SupportReportKind? observedKind = null;
        await Assert.That(session.Create(false, kind =>
        {
            observedKind = kind;
            return "local summary";
        }, _ => Assert.Fail("Local report creation must not open GitHub."))).IsTrue();
        await Assert.That(observedKind).IsEqualTo(SupportReportKind.Standard);
        await Assert.That(session.IncludeGameLog).IsFalse();
    }

    [Test]
    public async Task BrowserFailure_DoesNotClearTheLogChoice()
    {
        var session = new SupportReportSession { IncludeGameLog = true };
        await Assert.That(() => session.Create(true, _ => "summary",
            _ => throw new IOException("Browser unavailable"))).Throws<IOException>();
        await Assert.That(session.IncludeGameLog).IsTrue();
    }
}
