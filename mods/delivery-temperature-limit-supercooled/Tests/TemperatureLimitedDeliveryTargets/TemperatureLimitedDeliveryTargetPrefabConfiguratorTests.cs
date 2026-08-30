namespace DeliveryTemperatureLimit.Tests.TemperatureLimitedDeliveryTargets;

[TestClass]
public sealed class TemperatureLimitedDeliveryTargetPrefabConfiguratorTests
{
    [TestMethod]
    public void IsEligibleDeliveryTargetPrefab_WhenConfigurationIsStorageTile_ReturnsTrue()
    {
        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(
                new StorageTileConfig(),
                new UnityEngine.GameObject());

        Assert.IsTrue(isEligible);
    }

    [TestMethod]
    public void IsEligibleDeliveryTargetPrefab_WhenPrefabHasManualDelivery_ReturnsTrue()
    {
        var prefab = new UnityEngine.GameObject();
        prefab.AddComponent<ManualDeliveryKG>();

        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(configuration: null, prefab);

        Assert.IsTrue(isEligible);
    }

    [TestMethod]
    public void IsEligibleDeliveryTargetPrefab_WhenStorageAllowsUserRemoval_ReturnsTrue()
    {
        var prefab = new UnityEngine.GameObject();
        prefab.AddComponent<Storage>().allowUIItemRemoval = true;

        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(configuration: null, prefab);

        Assert.IsTrue(isEligible);
    }

    [TestMethod]
    public void IsEligibleDeliveryTargetPrefab_WhenStorageIsNotInteractiveAndNoDeliveryComponent_ReturnsFalse()
    {
        var prefab = new UnityEngine.GameObject();
        prefab.AddComponent<Storage>().allowUIItemRemoval = false;

        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(configuration: null, prefab);

        Assert.IsFalse(isEligible);
    }

    [TestMethod]
    public void IsEligibleDeliveryTargetPrefab_WhenPrefabIsNull_ReturnsFalse()
    {
        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(
                new StorageTileConfig(),
                prefab: null);

        Assert.IsFalse(isEligible);
    }
}
