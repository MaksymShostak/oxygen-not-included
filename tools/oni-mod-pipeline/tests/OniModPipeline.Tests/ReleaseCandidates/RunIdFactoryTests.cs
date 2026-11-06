using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
public sealed class RunIdFactoryTests
{
    [Test]
    public async Task Create_WhenGivenUtcAndEightBytes_UsesSortableCollisionResistantFormat()
    {
        var id = RunIdFactory.Create(
            new DateTimeOffset(2026, 8, 27, 14, 3, 2, TimeSpan.Zero)
                .AddTicks(1234567),
            Convert.FromHexString("0123456789abcdef"));

        await Assert.That(id).IsEqualTo("20260827T140302.1234567Z-0123456789abcdef");
    }

    [Test]
    public async Task Create_WhenOffsetIsNotUtc_NormalizesInstantToUtc()
    {
        var id = RunIdFactory.Create(
            new DateTimeOffset(2026, 8, 27, 17, 3, 2, TimeSpan.FromHours(3)),
            Convert.FromHexString("fedcba9876543210"));

        await Assert.That(id).IsEqualTo("20260827T140302.0000000Z-fedcba9876543210");
    }

    [Test]
    public async Task Create_WhenEntropyIsNotExactlyEightBytes_RejectsInput()
    {
        await Assert.That(() =>
            RunIdFactory.Create(DateTimeOffset.UtcNow, new byte[7])).ThrowsExactly<ArgumentException>();
        await Assert.That(() =>
            RunIdFactory.Create(DateTimeOffset.UtcNow, new byte[9])).ThrowsExactly<ArgumentException>();
    }
}
