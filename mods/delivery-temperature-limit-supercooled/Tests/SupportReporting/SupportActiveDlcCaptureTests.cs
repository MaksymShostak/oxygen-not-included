namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportActiveDlcCaptureTests
{
    [Test]
    public async Task Capture_WhenReaderReturnsNull_ReportsUnavailableInsteadOfNoDlcs()
    {
        var warnings = new List<string>();

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => null,
            warnings);

        await Assert.That(snapshot.State).IsEqualTo("unavailable");
        await Assert.That(snapshot.UnavailableReason).IsEqualTo("Active DLC identifiers were unavailable during report generation.");
        await Assert.That(snapshot.Ids).Count().IsEqualTo(0);
        await Assert.That(warnings).IsEquivalentTo(new[]
            {
                "Active DLC identifiers were unavailable during report generation."
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Capture_WhenReaderThrows_ReportsUnavailableInsteadOfNoDlcs()
    {
        var warnings = new List<string>();

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => throw new InvalidOperationException("DLC manager unavailable"),
            warnings);

        await Assert.That(snapshot.State).IsEqualTo("unavailable");
        await Assert.That(snapshot.UnavailableReason).IsEqualTo("Active DLC identifiers were unavailable during report generation.");
        await Assert.That(snapshot.Ids).Count().IsEqualTo(0);
        await Assert.That(warnings).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Capture_WhenReaderReturnsEmptyList_ReportsAvailableNoDlcs()
    {
        var warnings = new List<string>();

        SupportActiveDlcSnapshot snapshot = SupportActiveDlcCapture.Capture(
            () => Array.Empty<string>(),
            warnings);

        await Assert.That(snapshot.State).IsEqualTo("available");
        await Assert.That(snapshot.UnavailableReason).IsNull();
        await Assert.That(snapshot.Ids).Count().IsEqualTo(0);
        await Assert.That(warnings).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Capture_WhenReaderReturnsIds_NormalizesAndDefensivelyCopiesThem()
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

        await Assert.That(snapshot.Ids.ToArray()).IsEquivalentTo(new[] { "BASE_GAME", "EXPANSION1_ID" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
