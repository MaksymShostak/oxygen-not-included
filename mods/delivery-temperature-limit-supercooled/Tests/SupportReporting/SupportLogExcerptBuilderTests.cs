using System.Text;

namespace DeliveryTemperatureLimit.Tests.SupportReporting;

[TestClass]
public sealed class SupportLogExcerptBuilderTests
{
    [TestMethod]
    public void Create_WhenLogIsShort_PreservesUtf8ContentAndByteCounts()
    {
        const string content = "Start → Привіт → end\n";
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(bytes),
            "unity-console-log-path",
            CreateRedactor());

        Assert.AreEqual("available", snapshot.State);
        Assert.AreEqual(bytes.Length, snapshot.OriginalByteCount);
        Assert.AreEqual(bytes.Length, snapshot.IncludedRawByteCount);
        Assert.IsFalse(snapshot.Truncated);
        Assert.AreEqual(content, snapshot.Content);
    }

    [TestMethod]
    public void Create_WhenLogExceedsRawLimit_KeepsExactlyTheMostRecentBytes()
    {
        byte[] prefix = Encoding.UTF8.GetBytes("discarded-prefix");
        byte[] retained = Enumerable.Repeat(
                (byte)'x',
                SupportReportLimits.MaximumRawPlayerLogBytes)
            .ToArray();
        byte[] log = new byte[prefix.Length + retained.Length];
        Buffer.BlockCopy(prefix, 0, log, 0, prefix.Length);
        Buffer.BlockCopy(retained, 0, log, prefix.Length, retained.Length);
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(log),
            "unity-console-log-path",
            CreateRedactor());

        Assert.AreEqual(log.Length, snapshot.OriginalByteCount);
        Assert.AreEqual(
            SupportReportLimits.MaximumRawPlayerLogBytes,
            snapshot.IncludedRawByteCount);
        Assert.IsTrue(snapshot.Truncated);
        Assert.IsNotNull(snapshot.Content);
        Assert.HasCount(
            SupportReportLimits.MaximumRawPlayerLogBytes,
            snapshot.Content);
        Assert.IsTrue(snapshot.Content.All(character => character == 'x'));
    }

    [TestMethod]
    public void Create_WhenRawTailBeginsInsideUtf8Character_DropsOnlyLeadingReplacement()
    {
        byte[] log = new byte[
            SupportReportLimits.MaximumRawPlayerLogBytes + 1];
        log[0] = 0xC3;
        log[1] = 0xA9;
        Array.Fill(log, (byte)'z', 2, log.Length - 2);
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(log),
            "unity-console-log-path",
            CreateRedactor());

        Assert.IsNotNull(snapshot.Content);
        Assert.IsFalse(snapshot.Content.Contains('\uFFFD'));
        Assert.HasCount(
            SupportReportLimits.MaximumRawPlayerLogBytes - 1,
            snapshot.Content);
        Assert.IsTrue(snapshot.Content.All(character => character == 'z'));
    }

    [TestMethod]
    public void Create_WhenKnownPathsOccur_RedactsContentAndReportsUsedPlaceholders()
    {
        const string content =
            @"Loading C:\Users\Максим\Documents\Klei\OxygenNotIncluded\mods\Dev\TemperatureLimit";
        var builder = new SupportLogExcerptBuilder();
        var redactor = new SupportPathRedactor(
            new[]
            {
                new SupportPathRedactionRule(
                    @"C:\Users\Максим\Documents\Klei\OxygenNotIncluded",
                    "<ONI_DATA>")
            },
            StringComparison.OrdinalIgnoreCase);

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(Encoding.UTF8.GetBytes(content)),
            "unity-console-log-path",
            redactor);

        Assert.AreEqual(
            @"Loading <ONI_DATA>\mods\Dev\TemperatureLimit",
            snapshot.Content);
        CollectionAssert.AreEqual(
            new[] { "<ONI_DATA>" },
            snapshot.RedactedPlaceholders.ToArray());
    }

    [TestMethod]
    public void Create_WhenJsonEscapingWouldExceedLimit_KeepsNewestEscapedContentWithinLimit()
    {
        byte[] log = Enumerable.Repeat(
                (byte)'\\',
                SupportReportLimits.MaximumRawPlayerLogBytes)
            .ToArray();
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(log),
            "unity-console-log-path",
            CreateRedactor());

        int expectedCharacters =
            SupportReportLimits.MaximumEscapedPlayerLogBytes / 2;
        Assert.IsNotNull(snapshot.Content);
        Assert.HasCount(expectedCharacters, snapshot.Content);
        Assert.IsTrue(snapshot.Content.All(character => character == '\\'));
        Assert.IsTrue(snapshot.Truncated);
        Assert.AreEqual(
            SupportReportLimits.MaximumEscapedPlayerLogBytes,
            snapshot.Content.Length * 2);
    }

    [TestMethod]
    [DataRow("\u0085", 2)]
    [DataRow("\u2028", 3)]
    [DataRow("\u2029", 3)]
    public void Create_WhenJsonNetUsesUnicodeEscape_KeepsEscapedContentWithinLimit(
        string escapedCharacter,
        int rawUtf8ByteCount)
    {
        int sourceCharacterCount =
            SupportReportLimits.MaximumRawPlayerLogBytes / rawUtf8ByteCount;
        string content = new(escapedCharacter[0], sourceCharacterCount);
        byte[] log = Encoding.UTF8.GetBytes(content);
        Assert.HasCount(
            SupportReportLimits.MaximumRawPlayerLogBytes,
            log);
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(log),
            "unity-console-log-path",
            CreateRedactor());

        int expectedCharacters =
            SupportReportLimits.MaximumEscapedPlayerLogBytes / 6;
        Assert.IsNotNull(snapshot.Content);
        Assert.HasCount(expectedCharacters, snapshot.Content);
        Assert.IsTrue(snapshot.Content.All(
            character => character == escapedCharacter[0]));
        Assert.IsTrue(snapshot.Truncated);
    }

    [TestMethod]
    public void Create_WhenStreamCannotSeek_RejectsUnsupportedInput()
    {
        var builder = new SupportLogExcerptBuilder();
        using var stream = new NonSeekableReadStream(
            Encoding.UTF8.GetBytes("content"));

        Assert.ThrowsExactly<ArgumentException>(() => builder.Create(
            stream,
            "unity-console-log-path",
            CreateRedactor()));
    }

    private static SupportPathRedactor CreateRedactor() =>
        new(
            Array.Empty<SupportPathRedactionRule>(),
            StringComparison.Ordinal);

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream inner;

        internal NonSeekableReadStream(byte[] bytes)
        {
            inner = new MemoryStream(bytes);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
