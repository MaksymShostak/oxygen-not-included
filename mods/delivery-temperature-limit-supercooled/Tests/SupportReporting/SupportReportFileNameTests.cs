namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportReportFileNameTests
{
    [Test]
    public async Task Create_WhenUtcIdentityIsValid_UsesStableTimestampAndShortReportId()
    {
        var generatedAtUtc = new DateTimeOffset(
            2026,
            8,
            31,
            7,
            8,
            9,
            123,
            TimeSpan.Zero);
        var reportId = new Guid("00112233-4455-6677-8899-aabbccddeeff");

        string fileName = SupportReportFileName.Create(
            generatedAtUtc,
            reportId);

        await Assert.That(fileName).IsEqualTo("temperature-limit-support-20260831T070809123Z-00112233.json");
    }

    [Test]
    public async Task Create_WhenTimestampIsNotUtc_RejectsAmbiguousFilenameTime()
    {
        var generatedAt = new DateTimeOffset(
            2026,
            8,
            31,
            10,
            8,
            9,
            TimeSpan.FromHours(3));

        await Assert.That(() =>
            SupportReportFileName.Create(
                generatedAt,
                new Guid("00112233-4455-6677-8899-aabbccddeeff"))).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Create_WhenReportIdIsEmpty_RejectsCollisionProneFilename()
    {
        await Assert.That(() =>
            SupportReportFileName.Create(
                new DateTimeOffset(
                    2026,
                    8,
                    31,
                    7,
                    8,
                    9,
                    TimeSpan.Zero),
                Guid.Empty)).ThrowsExactly<ArgumentException>();
    }
}
