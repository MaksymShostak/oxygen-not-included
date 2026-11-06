namespace DeliveryTemperatureLimit.Tests.TemperatureLimitedDeliveryTargets;
public sealed class TemperatureLimitedDeliveryTargetPrefabConfiguratorTests
{
    [Test]
    public async Task IsEligibleDeliveryTargetPrefab_WhenConfigurationIsStorageTile_ReturnsTrue()
    {
        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(
                new StorageTileConfig(),
                new UnityEngine.GameObject());

        await Assert.That(isEligible).IsTrue();
    }

    [Test]
    public async Task IsEligibleDeliveryTargetPrefab_WhenPrefabHasManualDelivery_ReturnsTrue()
    {
        var prefab = new UnityEngine.GameObject();
        prefab.AddComponent<ManualDeliveryKG>();

        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(configuration: null, prefab);

        await Assert.That(isEligible).IsTrue();
    }

    [Test]
    public async Task IsEligibleDeliveryTargetPrefab_WhenStorageAllowsUserRemoval_ReturnsTrue()
    {
        var prefab = new UnityEngine.GameObject();
        prefab.AddComponent<Storage>().allowUIItemRemoval = true;

        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(configuration: null, prefab);

        await Assert.That(isEligible).IsTrue();
    }

    [Test]
    public async Task IsEligibleDeliveryTargetPrefab_WhenStorageIsNotInteractiveAndNoDeliveryComponent_ReturnsFalse()
    {
        var prefab = new UnityEngine.GameObject();
        prefab.AddComponent<Storage>().allowUIItemRemoval = false;

        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(configuration: null, prefab);

        await Assert.That(isEligible).IsFalse();
    }

    [Test]
    public async Task IsEligibleDeliveryTargetPrefab_WhenPrefabIsNull_ReturnsFalse()
    {
        bool isEligible = TemperatureLimitedDeliveryTargetPrefabConfigurator
            .IsEligibleDeliveryTargetPrefab(
                new StorageTileConfig(),
                prefab: null);

        await Assert.That(isEligible).IsFalse();
    }
}
