using System.Reflection;

namespace DeliveryTemperatureLimit.Tests;

[TestClass]
public sealed class BuildingsEligibilityTests
{
    [TestMethod]
    public void IsEligible_WhenConfigIsStorageTile_ReturnsTrue()
    {
        var method = typeof(Buildings_Patch).GetMethod(
            "IsEligible",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "Buildings_Patch.IsEligible must remain discoverable.");

        var result = method.Invoke(
            null,
            [new StorageTileConfig(), new UnityEngine.GameObject()]);

        Assert.AreEqual(true, result);
    }
}
