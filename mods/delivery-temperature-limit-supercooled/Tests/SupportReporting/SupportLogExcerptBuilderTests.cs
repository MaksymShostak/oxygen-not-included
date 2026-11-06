using System.Text;

namespace DeliveryTemperatureLimit.Tests.SupportReporting;
public sealed class SupportLogExcerptBuilderTests
{
    [Test]
    public async Task Create_WhenLogIsShort_PreservesUtf8ContentAndByteCounts()
    {
        const string content = "Start → Привіт → end\n";
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(bytes),
            "unity-console-log-path",
            CreateRedactor());

        await Assert.That(snapshot.State).IsEqualTo("available");
        await Assert.That(snapshot.OriginalByteCount).IsEqualTo(bytes.Length);
        await Assert.That(snapshot.IncludedRawByteCount).IsEqualTo(bytes.Length);
        await Assert.That(snapshot.Truncated).IsFalse();
        await Assert.That(snapshot.Content).IsEqualTo(content);
    }

    [Test]
    public async Task Create_WhenLogExceedsRawLimit_KeepsExactlyTheMostRecentBytes()
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

        await Assert.That(snapshot.OriginalByteCount).IsEqualTo(log.Length);
        await Assert.That(snapshot.IncludedRawByteCount).IsEqualTo(SupportReportLimits.MaximumRawPlayerLogBytes);
        await Assert.That(snapshot.Truncated).IsTrue();
        Assert.NotNull(snapshot.Content);
        await Assert.That(snapshot.Content.Length).IsEqualTo(SupportReportLimits.MaximumRawPlayerLogBytes);
        await Assert.That(snapshot.Content.All(character => character == 'x')).IsTrue();
    }

    [Test]
    public async Task Create_WhenRawTailBeginsInsideUtf8Character_DropsOnlyLeadingReplacement()
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

        Assert.NotNull(snapshot.Content);
        await Assert.That(snapshot.Content.Contains('\uFFFD')).IsFalse();
        await Assert.That(snapshot.Content.Length).IsEqualTo(SupportReportLimits.MaximumRawPlayerLogBytes - 1);
        await Assert.That(snapshot.Content.All(character => character == 'z')).IsTrue();
    }

    [Test]
    public async Task Create_WhenKnownPathsOccur_RedactsContentAndReportsUsedPlaceholders()
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

        await Assert.That(snapshot.Content).IsEqualTo(@"Loading <ONI_DATA>\mods\Dev\TemperatureLimit");
        await Assert.That(snapshot.RedactedPlaceholders.ToArray()).IsEquivalentTo(new[] { "<ONI_DATA>" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Create_WhenJsonEscapingWouldExceedLimit_KeepsNewestEscapedContentWithinLimit()
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
        Assert.NotNull(snapshot.Content);
        await Assert.That(snapshot.Content.Length).IsEqualTo(expectedCharacters);
        await Assert.That(snapshot.Content.All(character => character == '\\')).IsTrue();
        await Assert.That(snapshot.Truncated).IsTrue();
        await Assert.That(snapshot.Content.Length * 2).IsEqualTo(SupportReportLimits.MaximumEscapedPlayerLogBytes);
    }

    [Test]
    [Arguments("\u0085", 2)]
    [Arguments("\u2028", 3)]
    [Arguments("\u2029", 3)]
    public async Task Create_WhenJsonNetUsesUnicodeEscape_KeepsEscapedContentWithinLimit(
        string escapedCharacter,
        int rawUtf8ByteCount)
    {
        int sourceCharacterCount =
            SupportReportLimits.MaximumRawPlayerLogBytes / rawUtf8ByteCount;
        string content = new(escapedCharacter[0], sourceCharacterCount);
        byte[] log = Encoding.UTF8.GetBytes(content);
        await Assert.That(log).Count().IsEqualTo(SupportReportLimits.MaximumRawPlayerLogBytes);
        var builder = new SupportLogExcerptBuilder();

        SupportPlayerLogSnapshot snapshot = builder.Create(
            new MemoryStream(log),
            "unity-console-log-path",
            CreateRedactor());

        int expectedCharacters =
            SupportReportLimits.MaximumEscapedPlayerLogBytes / 6;
        Assert.NotNull(snapshot.Content);
        await Assert.That(snapshot.Content.Length).IsEqualTo(expectedCharacters);
        await Assert.That(snapshot.Content.All(
            character => character == escapedCharacter[0])).IsTrue();
        await Assert.That(snapshot.Truncated).IsTrue();
    }

    [Test]
    public async Task Create_WhenStreamCannotSeek_RejectsUnsupportedInput()
    {
        var builder = new SupportLogExcerptBuilder();
        using var stream = new NonSeekableReadStream(
            Encoding.UTF8.GetBytes("content"));

        await Assert.That(() => builder.Create(
            stream,
            "unity-console-log-path",
            CreateRedactor())).ThrowsExactly<ArgumentException>();
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
