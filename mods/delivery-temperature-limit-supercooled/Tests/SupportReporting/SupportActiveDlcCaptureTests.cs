namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportActiveDlcCaptureTests
{
    [TestMethod]
    public void Capture_WhenReaderReturnsNull_ReportsUnavailableInsteadOfNoDlcs()
    {
        var warnings = new List<string>();

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => null,
            warnings);

        Assert.AreEqual("unavailable", snapshot.State);
        Assert.AreEqual(
            "Active DLC identifiers were unavailable during report generation.",
            snapshot.UnavailableReason);
        Assert.HasCount(0, snapshot.Ids);
        CollectionAssert.AreEqual(
            new[]
            {
                "Active DLC identifiers were unavailable during report generation."
            },
            warnings);
    }

    [TestMethod]
    public void Capture_WhenReaderThrows_ReportsUnavailableInsteadOfNoDlcs()
    {
        var warnings = new List<string>();

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => throw new InvalidOperationException("DLC manager unavailable"),
            warnings);

        Assert.AreEqual("unavailable", snapshot.State);
        Assert.AreEqual(
            "Active DLC identifiers were unavailable during report generation.",
            snapshot.UnavailableReason);
        Assert.HasCount(0, snapshot.Ids);
        Assert.HasCount(1, warnings);
    }

    [TestMethod]
    public void Capture_WhenReaderReturnsEmptyList_ReportsAvailableNoDlcs()
    {
        var warnings = new List<string>();

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => Array.Empty<string>(),
            warnings);

        Assert.AreEqual("available", snapshot.State);
        Assert.IsNull(snapshot.UnavailableReason);
        Assert.HasCount(0, snapshot.Ids);
        Assert.HasCount(0, warnings);
    }

    [TestMethod]
    public void Capture_WhenReaderReturnsIds_NormalizesAndDefensivelyCopiesThem()
    {
        var observedIds = new List<string>
        {
            "EXPANSION1_ID",
            " ",
            "BASE_GAME"
        };

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => observedIds,
            new List<string>());
        observedIds[0] = "MUTATED";

        CollectionAssert.AreEqual(
            new[] { "BASE_GAME", "EXPANSION1_ID" },
            snapshot.Ids.ToArray());
    }
}
