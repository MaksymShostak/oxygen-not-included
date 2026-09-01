#nullable enable

namespace DeliveryTemperatureLimit
{
    internal interface IRuntimeAuthorityIntegrationInspector
    {
        DeclaredModIntegrationId IntegrationId { get; }

        PreparedRuntimeAuthorityInspection Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context);
    }
}
