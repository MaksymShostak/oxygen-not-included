using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class RuntimeFailureBoundaryContractTests
{
    [Test]
    [Arguments("TemperatureLimitSideScreen", "SetTarget")]
    [Arguments("TemperatureLimitSideScreen", "OnPrefabInit")]
    [Arguments("TemperatureLimitWidget", "OnPrefabInit")]
    [Arguments("TemperatureLimitInputScreen", "OnSpawn")]
    [Arguments("DeliveryTemperatureLimitMod", "OnLoad")]
    [Arguments("DeliveryTemperatureLimitMod", "OnAllModsLoaded")]
    [Arguments("DeliveryTemperatureGameLoadAuthorityPatches", "GameOnPrefabInitPrefix")]
    [Arguments("TemperatureLimit", "OnSpawn")]
    [Arguments("TemperatureLimit", "OnCleanUp")]
    [Arguments("DeliveryTemperatureGameSessionShutdownPatches", "GameDestroyInstancesFinalizer")]
    [Arguments("WorldParentTopologyPatches", "RegisterWorldContainerPostfix")]
    [Arguments("ConstructionMaterialTemperatureLimit", "BuildingDefinitionInstantiationPostfix")]
    [Arguments("TemperatureLimitSideScreenRegistrationPatches", "DetailsScreenPrefabInitializationPostfix")]
    [Arguments("TemperatureLimitedDeliveryTargetPrefabConfigurator", "ConfigureTemperatureLimitedDeliveryTargetPrefabsPostfix")]
    [Arguments("KleiWorldInventoryTemperaturePatches", "RecordFilteredPickupTemperatureAmount")]
    [Arguments("KleiWorldInventoryTemperaturePatches", "WorldInventoryUpdateFinalizer")]
    [Arguments("KleiAuthoritativeFetchTemperatureEligibilityPatches", "UpdateStorageFetchableBitsPrefix")]
    [Arguments("KleiAuthoritativeFetchTemperatureEligibilityPatches", "UpdateStorageFetchableBitsFinalizer")]
    [Arguments("KleiDirectDeliveryEligibilityPatches", "IsPickupAllowedForDestination")]
    [Arguments("FastTrackDirectDeliveryEligibilityPatches", "IsPickupAllowedForFetchChore")]
    [Arguments("FastTrackWorldInventoryTemperaturePatches", "RecordFilteredPickupTemperatureAmount")]
    [Arguments("FastTrackWorldInventoryTemperaturePatches", "BackgroundWorldInventoryRunUpdateFinalizer")]
    [Arguments("FastTrackPickupTemperaturePatches", "BeforeUpdatePickupsFinalizer")]
    public async Task GameEntryPoint_ContainsModFailure(string typeName, string methodName)
    {
        string path = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ResolvePipelineBuildOrInconclusive().AssemblyPath;
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t =>
            metadata.GetString(t.Namespace) == "DeliveryTemperatureLimit" &&
            metadata.GetString(t.Name) == typeName);
        var method = type.GetMethods().Select(metadata.GetMethodDefinition).Single(m =>
            metadata.GetString(m.Name) == methodName);
        await Assert.That(pe.GetMethodBody(method.RelativeVirtualAddress).ExceptionRegions.Any(region =>
            region.Kind == ExceptionRegionKind.Catch)).IsTrue().Because($"{typeName}.{methodName} must contain its own failures at the game boundary.");
        var instructions = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
            path, "DeliveryTemperatureLimit." + typeName, methodName).Single().Instructions;
        await Assert.That(instructions.Any(instruction => instruction.ResolvedOperand is
            "DeliveryTemperatureLimit.RuntimeFailureReporting.DisableGameplay" or "DeliveryTemperatureLimit.RuntimeFailureReporting.DisableUserInterface" or
            "DeliveryTemperatureLimit.DeliveryTemperatureGameSessionHost.TryDisableRuntime")).IsTrue().Because("Catching alone is insufficient: a failed feature must stop and report its loss of functionality.");
    }
}
