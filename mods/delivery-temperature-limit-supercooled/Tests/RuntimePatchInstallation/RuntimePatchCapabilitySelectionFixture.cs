using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

/// <summary>
/// Creates the complete provider-neutral Klei baseline used by tests outside the
/// runtime-plan behavior matrix. Production resolves the same responsibilities
/// against current game methods in the cold installer composition root.
/// </summary>
internal static class RuntimePatchCapabilitySelectionFixture
{
    private static readonly RuntimeCapabilityId GameSessionLifecycleCapabilityId =
        new("game-session-lifecycle");
    private static readonly RuntimeCapabilityId WorldParentTopologyCapabilityId =
        new("world-parent-topology");
    private static readonly RuntimeCapabilityId
        AuthoritativeFetchTemperatureEligibilityCapabilityId =
            new("authoritative-fetch-temperature-eligibility");

    internal static RuntimePatchCapabilitySelection CreateKleiBaselineSelection()
    {
        RuntimeCapabilityDefinition[] definitions =
        [
            RequiredDefinition(
                GameSessionLifecycleCapabilityId,
                "game-session-lifecycle",
                nameof(GameSessionLifecycleTarget)),
            RequiredDefinition(
                WorldParentTopologyCapabilityId,
                "world-parent-topology",
                nameof(WorldParentTopologyTarget)),
            RequiredDefinition(
                AuthoritativeFetchTemperatureEligibilityCapabilityId,
                "klei-authoritative-fetch-temperature-eligibility",
                nameof(AuthoritativeFetchTemperatureEligibilityTarget)),
            OptionalDefinition(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                "klei-world-inventory-temperature-publication",
                nameof(WorldInventoryTarget)),
            OptionalDefinition(
                RuntimeCapabilityId.TemperatureStatusAvailability,
                "temperature-status-availability",
                nameof(TemperatureStatusAvailabilityTarget)),
            RequiredDefinition(
                RuntimeCapabilityId.PickupTemperatureGrouping,
                "klei-pickup-temperature-grouping",
                nameof(PickupGroupingTarget)),
            RequiredDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                "klei-direct-delivery-eligibility",
                nameof(DirectDeliveryEligibilityTarget))
        ];
        return RuntimePatchCapabilitySelector.Select(
            definitions,
            Array.Empty<PreparedRuntimeAuthorityContribution>(),
            Array.Empty<ExternalModIntegrationOutcome>());
    }

    private static RuntimeCapabilityDefinition RequiredDefinition(
        RuntimeCapabilityId capabilityId,
        string patchGroupId,
        string targetMethodName) =>
        new(
            capabilityId,
            RuntimeCapabilityCriticality.Required,
            () => CreateKleiBaselineContribution(
                    capabilityId,
                    patchGroupId,
                    targetMethodName),
            atomicBundleId: null);

    private static RuntimeCapabilityDefinition OptionalDefinition(
        RuntimeCapabilityId capabilityId,
        string patchGroupId,
        string targetMethodName) =>
        new(
            capabilityId,
            RuntimeCapabilityCriticality.Optional,
            () => CreateKleiBaselineContribution(
                    capabilityId,
                    patchGroupId,
                    targetMethodName),
            atomicBundleId: null);

    private static PreparedRuntimeAuthorityContribution
        CreateKleiBaselineContribution(
            RuntimeCapabilityId capabilityId,
            string patchGroupId,
            string targetMethodName)
    {
        MethodInfo targetMethod = RequireMethod(targetMethodName);
        return new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity.KleiBaseline,
            capabilityId,
            new[] { new RuntimePatchGroupId(patchGroupId) },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    targetMethod,
                    RequireMethod(nameof(PreparedPostfix)),
                    HarmonyPatchContractKind.Postfix)
            },
            new[]
            {
                new RuntimeAuthorityRequirement(
                    targetMethod,
                    RuntimeAuthorityRequirementKind.KleiOriginal,
                    requiredHarmonyOwner: null,
                    requiredPrefixMethod: null,
                    Array.Empty<string>())
            },
            diagnosticCode: null,
            diagnosticMessage: null);
    }

    private static MethodInfo RequireMethod(string methodName) =>
        typeof(RuntimePatchCapabilitySelectionFixture).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Missing fixture method: " + methodName);

    private static void PreparedPostfix()
    {
    }

    private static void GameSessionLifecycleTarget()
    {
    }

    private static void WorldParentTopologyTarget()
    {
    }

    private static void AuthoritativeFetchTemperatureEligibilityTarget()
    {
    }

    private static void WorldInventoryTarget()
    {
    }

    private static void TemperatureStatusAvailabilityTarget()
    {
    }

    private static void PickupGroupingTarget()
    {
    }

    private static void DirectDeliveryEligibilityTarget()
    {
    }
}
