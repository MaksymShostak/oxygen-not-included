using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
public sealed class FetchRequestTopologyTrackerTests
{
    [Test]
    public async Task CaptureVersion_WhenTrackerIsNew_ReturnsInitialZeroVersion()
    {
        var tracker = new FetchRequestTopologyTracker();

        await Assert.That(tracker.CaptureVersion().Value).IsEqualTo(0L);
    }

    [Test]
    public async Task RecordEffectiveChange_WhenCalledOnce_IncrementsVersionOnce()
    {
        var tracker = new FetchRequestTopologyTracker();

        var changedVersion = tracker.RecordEffectiveChange();

        await Assert.That(changedVersion.Value).IsEqualTo(1L);
        await Assert.That(tracker.CaptureVersion()).IsEqualTo(changedVersion);
    }

    [Test]
    public async Task RecordEffectiveChange_WhenRepeated_AlwaysAdvancesMonotonically()
    {
        var tracker = new FetchRequestTopologyTracker();
        var priorVersion = tracker.CaptureVersion();

        for (var changeIndex = 0; changeIndex < 1000; changeIndex++)
        {
            var changedVersion = tracker.RecordEffectiveChange();

            await Assert.That(changedVersion.Value).IsEqualTo(priorVersion.Value + 1);
            await Assert.That(tracker.CaptureVersion()).IsEqualTo(changedVersion);
            priorVersion = changedVersion;
        }
    }

    [Test]
    public async Task RecordEffectiveChange_WhenVersionIsExhausted_ThrowsWithoutChangingCurrentVersion()
    {
        var tracker = new FetchRequestTopologyTracker();
        var versionField = typeof(FetchRequestTopologyTracker).GetField(
            "version",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (versionField is null) throw new TUnit.Assertions.Exceptions.AssertionException("The representation contract requires the exact private field " +
            "FetchRequestTopologyTracker.version.");
        await Assert.That(versionField.FieldType).IsEqualTo(typeof(long));
        versionField.SetValue(tracker, long.MaxValue);

        var exception = (await Assert.That(() =>
            tracker.RecordEffectiveChange()).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("exhausted");
        await Assert.That(tracker.CaptureVersion().Value).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task FetchRequestTopologyVersion_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() =>
            new FetchRequestTopologyVersion(-1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
