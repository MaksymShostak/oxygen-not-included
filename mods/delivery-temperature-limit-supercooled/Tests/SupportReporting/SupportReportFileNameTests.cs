namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportReportFileNameTests
{
    [TestMethod]
    public void Create_WhenUtcIdentityIsValid_UsesStableTimestampAndShortReportId()
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

        Assert.AreEqual(
            "temperature-limit-support-20260831T070809123Z-00112233.json",
            fileName);
    }

    [TestMethod]
    public void Create_WhenTimestampIsNotUtc_RejectsAmbiguousFilenameTime()
    {
        var generatedAt = new DateTimeOffset(
            2026,
            8,
            31,
            10,
            8,
            9,
            TimeSpan.FromHours(3));

        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportFileName.Create(
                generatedAt,
                new Guid("00112233-4455-6677-8899-aabbccddeeff")));
    }

    [TestMethod]
    public void Create_WhenReportIdIsEmpty_RejectsCollisionProneFilename()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SupportReportFileName.Create(
                new DateTimeOffset(
                    2026,
                    8,
                    31,
                    7,
                    8,
                    9,
                    TimeSpan.Zero),
                Guid.Empty));
    }
}
