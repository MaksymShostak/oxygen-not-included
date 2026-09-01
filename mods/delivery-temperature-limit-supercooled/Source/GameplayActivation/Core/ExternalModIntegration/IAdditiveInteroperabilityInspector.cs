#nullable enable

namespace DeliveryTemperatureLimit
{
    internal interface IAdditiveInteroperabilityInspector
    {
        DeclaredModIntegrationId IntegrationId { get; }

        ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context);
    }
}
