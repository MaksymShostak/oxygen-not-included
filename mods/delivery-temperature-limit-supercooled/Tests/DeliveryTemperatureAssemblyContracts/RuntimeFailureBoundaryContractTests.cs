using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class RuntimeFailureBoundaryContractTests
{
    [TestMethod]
    [DataRow("TemperatureLimitSideScreen", "SetTarget")]
    [DataRow("TemperatureLimitSideScreen", "OnPrefabInit")]
    [DataRow("TemperatureLimitWidget", "OnPrefabInit")]
    [DataRow("TemperatureLimitInputScreen", "OnSpawn")]
    [DataRow("DeliveryTemperatureLimitMod", "OnLoad")]
    [DataRow("DeliveryTemperatureLimitMod", "OnAllModsLoaded")]
    [DataRow("DeliveryTemperatureGameLoadAuthorityPatches", "GameOnPrefabInitPrefix")]
    [DataRow("TemperatureLimit", "OnSpawn")]
    [DataRow("TemperatureLimit", "OnCleanUp")]
    [DataRow("DeliveryTemperatureGameSessionShutdownPatches", "GameDestroyInstancesFinalizer")]
    [DataRow("WorldParentTopologyPatches", "RegisterWorldContainerPostfix")]
    [DataRow("ConstructionMaterialTemperatureLimit", "BuildingDefinitionInstantiationPostfix")]
    [DataRow("TemperatureLimitSideScreenRegistrationPatches", "DetailsScreenPrefabInitializationPostfix")]
    [DataRow("TemperatureLimitedDeliveryTargetPrefabConfigurator", "ConfigureTemperatureLimitedDeliveryTargetPrefabsPostfix")]
    [DataRow("KleiWorldInventoryTemperaturePatches", "RecordFilteredPickupTemperatureAmount")]
    [DataRow("KleiWorldInventoryTemperaturePatches", "WorldInventoryUpdateFinalizer")]
    [DataRow("KleiAuthoritativeFetchTemperatureEligibilityPatches", "UpdateStorageFetchableBitsPrefix")]
    [DataRow("KleiAuthoritativeFetchTemperatureEligibilityPatches", "UpdateStorageFetchableBitsFinalizer")]
    [DataRow("KleiDirectDeliveryEligibilityPatches", "IsPickupAllowedForDestination")]
    [DataRow("FastTrackDirectDeliveryEligibilityPatches", "IsPickupAllowedForFetchChore")]
    [DataRow("FastTrackWorldInventoryTemperaturePatches", "RecordFilteredPickupTemperatureAmount")]
    [DataRow("FastTrackWorldInventoryTemperaturePatches", "BackgroundWorldInventoryRunUpdateFinalizer")]
    [DataRow("FastTrackPickupTemperaturePatches", "BeforeUpdatePickupsFinalizer")]
    public void GameEntryPoint_ContainsModFailure(string typeName, string methodName)
    {
        string path = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ResolveRequiredPipelineBuild().AssemblyPath;
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t =>
            metadata.GetString(t.Namespace) == "DeliveryTemperatureLimit" &&
            metadata.GetString(t.Name) == typeName);
        var method = type.GetMethods().Select(metadata.GetMethodDefinition).Single(m =>
            metadata.GetString(m.Name) == methodName);
        Assert.IsTrue(pe.GetMethodBody(method.RelativeVirtualAddress).ExceptionRegions.Any(region =>
            region.Kind == ExceptionRegionKind.Catch),
            $"{typeName}.{methodName} must contain its own failures at the game boundary.");
        var instructions = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
            path, "DeliveryTemperatureLimit." + typeName, methodName).Single().Instructions;
        Assert.IsTrue(instructions.Any(instruction => instruction.ResolvedOperand is
            "DeliveryTemperatureLimit.RuntimeFailureReporting.DisableGameplay" or "DeliveryTemperatureLimit.RuntimeFailureReporting.DisableUserInterface" or
            "DeliveryTemperatureLimit.DeliveryTemperatureGameSessionHost.TryDisableRuntime"),
            "Catching alone is insufficient: a failed feature must stop and report its loss of functionality.");
    }
}
