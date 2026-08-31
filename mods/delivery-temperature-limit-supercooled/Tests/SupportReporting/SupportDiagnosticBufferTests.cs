namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportDiagnosticBufferTests
{
    [TestMethod]
    public void Record_WhenCodeRepeats_AggregatesCountTimesAndLatestContent()
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

        Assert.HasCount(1, snapshot);
        Assert.AreEqual(2, snapshot[0].RepeatCount);
        Assert.AreEqual(first, snapshot[0].FirstOccurredAtUtc);
        Assert.AreEqual(second, snapshot[0].LastOccurredAtUtc);
        Assert.AreEqual("warning", snapshot[0].Severity);
        Assert.AreEqual(
            "Patch installation was degraded.",
            snapshot[0].Message);
        Assert.AreEqual(
            typeof(InvalidOperationException).FullName,
            snapshot[0].ExceptionType);
        Assert.AreEqual("Latest failure.", snapshot[0].ExceptionMessage);
    }

    [TestMethod]
    public void Record_WhenMessageExceedsLimit_StoresMarkerWithinExactCeiling()
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
        Assert.HasCount(
            SupportReportLimits.MaximumDiagnosticMessageCharacters,
            diagnostic.Message);
        Assert.EndsWith("… [truncated]", diagnostic.Message);
        Assert.IsNotNull(diagnostic.ExceptionMessage);
        Assert.HasCount(
            SupportReportLimits.MaximumDiagnosticMessageCharacters,
            diagnostic.ExceptionMessage);
        Assert.EndsWith("… [truncated]", diagnostic.ExceptionMessage);
    }

    [TestMethod]
    public void Record_WhenDistinctCapacityIsExceeded_OmitsOnlyNewCodes()
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
        Assert.HasCount(
            SupportReportLimits.MaximumDistinctDiagnostics,
            snapshot);
        Assert.AreEqual(1, buffer.OmittedDistinctDiagnosticCount);
        Assert.AreEqual("DTL-000", snapshot[0].Code);
        Assert.AreEqual(2, snapshot[0].RepeatCount);
        Assert.AreEqual(repeat, snapshot[0].LastOccurredAtUtc);
        Assert.IsFalse(
            snapshot.Any(diagnostic => diagnostic.Code == "DTL-OMITTED"));
    }

    [TestMethod]
    public void CaptureSnapshot_WhenCodesDiffer_PreservesFirstSeenOrdinalOrder()
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

        CollectionAssert.AreEqual(
            new[] { "DTL-z", "DTL-A", "DTL-a" },
            buffer.CaptureSnapshot()
                .Select(diagnostic => diagnostic.Code)
                .ToArray());
    }

    [TestMethod]
    public void Record_WhenOneCodeIsRecordedInParallel_RetainsEveryOccurrence()
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
        Assert.HasCount(1, snapshot);
        Assert.AreEqual(1_000, snapshot[0].RepeatCount);
        Assert.AreEqual(occurredAtUtc, snapshot[0].FirstOccurredAtUtc);
        Assert.AreEqual(occurredAtUtc, snapshot[0].LastOccurredAtUtc);
    }

    [TestMethod]
    public void Record_WhenInputsAreInvalid_RejectsAmbiguousDiagnostic()
    {
        var buffer = new SupportDiagnosticBuffer();

        Assert.ThrowsExactly<ArgumentException>(() => buffer.Record(
            " ",
            SupportDiagnosticSeverity.Information,
            "Message.",
            Utc(12, 0)));
        Assert.ThrowsExactly<ArgumentNullException>(() => buffer.Record(
            "DTL-NULL",
            SupportDiagnosticSeverity.Information,
            null!,
            Utc(12, 0)));
        Assert.ThrowsExactly<ArgumentException>(() => buffer.Record(
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
                TimeSpan.FromHours(3))));
    }

    private static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 8, 31, hour, minute, 0, TimeSpan.Zero);
}
