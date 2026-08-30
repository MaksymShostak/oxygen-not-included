using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class FetchRequestTopologyTrackerTests
{
    [TestMethod]
    public void CaptureVersion_WhenTrackerIsNew_ReturnsInitialZeroVersion()
    {
        var tracker = new FetchRequestTopologyTracker();

        Assert.AreEqual(0L, tracker.CaptureVersion().Value);
    }

    [TestMethod]
    public void RecordEffectiveChange_WhenCalledOnce_IncrementsVersionOnce()
    {
        var tracker = new FetchRequestTopologyTracker();

        var changedVersion = tracker.RecordEffectiveChange();

        Assert.AreEqual(1L, changedVersion.Value);
        Assert.AreEqual(changedVersion, tracker.CaptureVersion());
    }

    [TestMethod]
    public void RecordEffectiveChange_WhenRepeated_AlwaysAdvancesMonotonically()
    {
        var tracker = new FetchRequestTopologyTracker();
        var priorVersion = tracker.CaptureVersion();

        for (var changeIndex = 0; changeIndex < 1000; changeIndex++)
        {
            var changedVersion = tracker.RecordEffectiveChange();

            Assert.AreEqual(priorVersion.Value + 1, changedVersion.Value);
            Assert.AreEqual(changedVersion, tracker.CaptureVersion());
            priorVersion = changedVersion;
        }
    }

    [TestMethod]
    public void RecordEffectiveChange_WhenVersionIsExhausted_ThrowsWithoutChangingCurrentVersion()
    {
        var tracker = new FetchRequestTopologyTracker();
        var versionField = typeof(FetchRequestTopologyTracker).GetField(
            "version",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            versionField,
            "The representation contract requires the exact private field " +
            "FetchRequestTopologyTracker.version.");
        Assert.AreEqual(typeof(long), versionField.FieldType);
        versionField.SetValue(tracker, long.MaxValue);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            tracker.RecordEffectiveChange());

        StringAssert.Contains(exception.Message, "exhausted");
        Assert.AreEqual(long.MaxValue, tracker.CaptureVersion().Value);
    }

    [TestMethod]
    public void FetchRequestTopologyVersion_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new FetchRequestTopologyVersion(-1));
    }
}
