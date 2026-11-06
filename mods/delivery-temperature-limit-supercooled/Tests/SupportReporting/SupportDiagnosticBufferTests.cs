namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportDiagnosticBufferTests
{
    [Test]
    public async Task Record_WhenCodeRepeats_AggregatesCountTimesAndLatestContent()
    {
        var buffer = new SupportDiagnosticBuffer();
        DateTimeOffset first = Utc(7, 0);
        DateTimeOffset second = Utc(7, 5);

        buffer.Record(
            "DTL-PATCH",
            SupportDiagnosticSeverity.Information,
            "Beginning patch installation.",
            first);
        buffer.Record(
            "DTL-PATCH",
            SupportDiagnosticSeverity.Warning,
            "Patch installation was degraded.",
            second,
            new InvalidOperationException("Latest failure."));

        IReadOnlyList<SupportDiagnosticSnapshot> snapshot =
            buffer.CaptureSnapshot();

        await Assert.That(snapshot).Count().IsEqualTo(1);
        await Assert.That(snapshot[0].RepeatCount).IsEqualTo(2);
        await Assert.That(snapshot[0].FirstOccurredAtUtc).IsEqualTo(first);
        await Assert.That(snapshot[0].LastOccurredAtUtc).IsEqualTo(second);
        await Assert.That(snapshot[0].Severity).IsEqualTo("warning");
        await Assert.That(snapshot[0].Message).IsEqualTo("Patch installation was degraded.");
        await Assert.That(snapshot[0].ExceptionType).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(snapshot[0].ExceptionMessage).IsEqualTo("Latest failure.");
    }

    [Test]
    public async Task Record_WhenMessageExceedsLimit_StoresMarkerWithinExactCeiling()
    {
        var buffer = new SupportDiagnosticBuffer();

        buffer.Record(
            "DTL-LONG",
            SupportDiagnosticSeverity.Error,
            new string('x',
                SupportReportLimits.MaximumDiagnosticMessageCharacters + 1),
            Utc(8, 0),
            new InvalidOperationException(
                new string(
                    'y',
                    SupportReportLimits.MaximumDiagnosticMessageCharacters + 1)));

        SupportDiagnosticSnapshot diagnostic = buffer.CaptureSnapshot()[0];
        await Assert.That(diagnostic.Message.Length).IsEqualTo(SupportReportLimits.MaximumDiagnosticMessageCharacters);
        await Assert.That(diagnostic.Message).EndsWith("… [truncated]");
        Assert.NotNull(diagnostic.ExceptionMessage);
        await Assert.That(diagnostic.ExceptionMessage.Length).IsEqualTo(SupportReportLimits.MaximumDiagnosticMessageCharacters);
        await Assert.That(diagnostic.ExceptionMessage).EndsWith("… [truncated]");
    }

    [Test]
    public async Task Record_WhenDistinctCapacityIsExceeded_OmitsOnlyNewCodes()
    {
        var buffer = new SupportDiagnosticBuffer();
        DateTimeOffset first = Utc(9, 0);
        DateTimeOffset repeat = Utc(9, 1);

        for (int index = 0;
             index < SupportReportLimits.MaximumDistinctDiagnostics;
             index++)
        {
            buffer.Record(
                "DTL-" + index.ToString("D3"),
                SupportDiagnosticSeverity.Information,
                "Retained " + index,
                first);
        }

        buffer.Record(
            "DTL-OMITTED",
            SupportDiagnosticSeverity.Warning,
            "This distinct code does not fit.",
            first);
        buffer.Record(
            "DTL-000",
            SupportDiagnosticSeverity.Error,
            "A retained code can still update.",
            repeat);

        IReadOnlyList<SupportDiagnosticSnapshot> snapshot =
            buffer.CaptureSnapshot();
        await Assert.That(snapshot).Count().IsEqualTo(SupportReportLimits.MaximumDistinctDiagnostics);
        await Assert.That(buffer.OmittedDistinctDiagnosticCount).IsEqualTo(1);
        await Assert.That(snapshot[0].Code).IsEqualTo("DTL-000");
        await Assert.That(snapshot[0].RepeatCount).IsEqualTo(2);
        await Assert.That(snapshot[0].LastOccurredAtUtc).IsEqualTo(repeat);
        await Assert.That(snapshot.Any(diagnostic => diagnostic.Code == "DTL-OMITTED")).IsFalse();
    }

    [Test]
    public async Task CaptureSnapshot_WhenCodesDiffer_PreservesFirstSeenOrdinalOrder()
    {
        var buffer = new SupportDiagnosticBuffer();

        buffer.Record(
            "DTL-z",
            SupportDiagnosticSeverity.Information,
            "First.",
            Utc(10, 0));
        buffer.Record(
            "DTL-A",
            SupportDiagnosticSeverity.Information,
            "Second.",
            Utc(10, 1));
        buffer.Record(
            "DTL-a",
            SupportDiagnosticSeverity.Information,
            "Third.",
            Utc(10, 2));

        await Assert.That(buffer.CaptureSnapshot()
                .Select(diagnostic => diagnostic.Code)
                .ToArray()).IsEquivalentTo(new[] { "DTL-z", "DTL-A", "DTL-a" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Record_WhenOneCodeIsRecordedInParallel_RetainsEveryOccurrence()
    {
        var buffer = new SupportDiagnosticBuffer();
        DateTimeOffset occurredAtUtc = Utc(11, 0);

        Parallel.For(
            0,
            1_000,
            _ => buffer.Record(
                "DTL-PARALLEL",
                SupportDiagnosticSeverity.Information,
                "Concurrent event.",
                occurredAtUtc));

        IReadOnlyList<SupportDiagnosticSnapshot> snapshot =
            buffer.CaptureSnapshot();
        await Assert.That(snapshot).Count().IsEqualTo(1);
        await Assert.That(snapshot[0].RepeatCount).IsEqualTo(1_000);
        await Assert.That(snapshot[0].FirstOccurredAtUtc).IsEqualTo(occurredAtUtc);
        await Assert.That(snapshot[0].LastOccurredAtUtc).IsEqualTo(occurredAtUtc);
    }

    [Test]
    public async Task Record_WhenInputsAreInvalid_RejectsAmbiguousDiagnostic()
    {
        var buffer = new SupportDiagnosticBuffer();

        await Assert.That(() => buffer.Record(
            " ",
            SupportDiagnosticSeverity.Information,
            "Message.",
            Utc(12, 0))).ThrowsExactly<ArgumentException>();
        await Assert.That(() => buffer.Record(
            "DTL-NULL",
            SupportDiagnosticSeverity.Information,
            null!,
            Utc(12, 0))).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => buffer.Record(
            "DTL-NON-UTC",
            SupportDiagnosticSeverity.Information,
            "Message.",
            new DateTimeOffset(
                2026,
                8,
                31,
                12,
                0,
                0,
                TimeSpan.FromHours(3)))).ThrowsExactly<ArgumentException>();
    }

    private static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 8, 31, hour, minute, 0, TimeSpan.Zero);
}
