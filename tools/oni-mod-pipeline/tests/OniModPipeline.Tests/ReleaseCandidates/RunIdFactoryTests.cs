using MaksymShostak.OniModPipeline.ReleaseCandidates;

namespace MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;

[TestClass]
public sealed class RunIdFactoryTests
{
    [TestMethod]
    public void Create_WhenGivenUtcAndEightBytes_UsesSortableCollisionResistantFormat()
    {
        var id = RunIdFactory.Create(
            new DateTimeOffset(2026, 8, 27, 14, 3, 2, TimeSpan.Zero)
                .AddTicks(1234567),
            Convert.FromHexString("0123456789abcdef"));

        Assert.AreEqual("20260827T140302.1234567Z-0123456789abcdef", id);
    }

    [TestMethod]
    public void Create_WhenOffsetIsNotUtc_NormalizesInstantToUtc()
    {
        var id = RunIdFactory.Create(
            new DateTimeOffset(2026, 8, 27, 17, 3, 2, TimeSpan.FromHours(3)),
            Convert.FromHexString("fedcba9876543210"));

        Assert.AreEqual("20260827T140302.0000000Z-fedcba9876543210", id);
    }

    [TestMethod]
    public void Create_WhenEntropyIsNotExactlyEightBytes_RejectsInput()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            RunIdFactory.Create(DateTimeOffset.UtcNow, new byte[7]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            RunIdFactory.Create(DateTimeOffset.UtcNow, new byte[9]));
    }
}
