using DeliveryTemperatureLimit;

namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportReportSessionTests
{
    [TestMethod]
    public void NewDialog_DoesNotIncludeTheGameLog()
    {
        Assert.IsFalse(new SupportReportSession().IncludeGameLog);
    }

    [TestMethod]
    public void CreateAndOpen_PreservesTheLogChoiceAndPrefillsOnlyTheFreshSummary()
    {
        var session = new SupportReportSession { IncludeGameLog = true };
        var events = new List<string>();
        Assert.IsTrue(session.Create(true, kind =>
        {
            Assert.AreEqual(SupportReportKind.ExtendedPlayerLog, kind);
            events.Add("created");
            return "fresh diagnostic summary";
        }, url =>
        {
            Assert.AreEqual(SupportIssueUrlBuilder.Create("fresh diagnostic summary").Value, url);
            events.Add("opened");
        }));
        CollectionAssert.AreEqual(new[] { "created", "opened" }, events);
        Assert.IsTrue(session.IncludeGameLog);

        Assert.IsFalse(session.Create(true, _ => null,
            _ => Assert.Fail("A failed attempt must not open a form with a previous report.")));
        Assert.IsTrue(session.IncludeGameLog);
    }

    [TestMethod]
    public void LocalReport_DoesNotOpenTheBrowser()
    {
        var session = new SupportReportSession();
        Assert.IsTrue(session.Create(false, kind =>
        {
            Assert.AreEqual(SupportReportKind.Standard, kind);
            return "local summary";
        }, _ => Assert.Fail("Local report creation must not open GitHub.")));
        Assert.IsFalse(session.IncludeGameLog);
    }

    [TestMethod]
    public void BrowserFailure_DoesNotClearTheLogChoice()
    {
        var session = new SupportReportSession { IncludeGameLog = true };
        Assert.Throws<IOException>(() => session.Create(true, _ => "summary",
            _ => throw new IOException("Browser unavailable")));
        Assert.IsTrue(session.IncludeGameLog);
    }
}
