#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Adds <see cref="TemperatureLimit"/> only to building prefabs that accept
    /// player-directed deliveries or interactive stored resources.
    /// </summary>
    internal static class TemperatureLimitedDeliveryTargetPrefabConfigurator
    {
        internal static MethodInfo ResolveBuildingConfigurationTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(BuildingConfigManager),
                nameof(BuildingConfigManager.ConfigurePost),
                DeclaredMemberVisibility.Public,
                typeof(void),
                Array.Empty<Type>());

        internal static FieldInfo ResolveBuildingConfigurationTableField() =>
            HarmonyPatchContractVerifier.RequireField(
                typeof(BuildingConfigManager),
                "configTable",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(Dictionary<IBuildingConfig, BuildingDef>));

        internal static void ConfigureTemperatureLimitedDeliveryTargetPrefabsPostfix(
            Dictionary<IBuildingConfig, BuildingDef> ___configTable)
        {
            if (___configTable == null)
            {
                DeliveryTemperatureSupportReporter.Record(
                    "DTL-PREFAB-CONFIGURATION-SKIPPED",
                    SupportDiagnosticSeverity.Error,
                    "Delivery Temperature Limit could not read the verified " +
                    "building configuration table.");
                return;
            }

            int configuredCompletePrefabCount = 0;
            foreach (KeyValuePair<IBuildingConfig, BuildingDef>
                     buildingConfiguration in ___configTable)
            {
                IBuildingConfig? configuration = buildingConfiguration.Key;
                BuildingDef? buildingDefinition = buildingConfiguration.Value;
                if (buildingDefinition == null)
                {
                    continue;
                }

                if (TryAddTemperatureLimit(
                        configuration,
                        buildingDefinition.BuildingComplete))
                {
                    configuredCompletePrefabCount++;
                }

                // Eligibility must be evaluated against the under-construction
                // prefab itself. Reusing BuildingComplete here silently omitted
                // construction-only delivery/storage components from modded defs.
                _ = TryAddTemperatureLimit(
                    configuration,
                    buildingDefinition.BuildingUnderConstruction);
            }

            DeliveryTemperatureSupportReporter.Record(
                "DTL-PREFAB-CONFIGURATION-COMPLETE",
                SupportDiagnosticSeverity.Information,
                "Delivery Temperature Limit configured " +
                configuredCompletePrefabCount +
                " eligible complete-building prefab types.");
        }

        internal static bool IsEligibleDeliveryTargetPrefab(
            IBuildingConfig? configuration,
            GameObject? prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            if (configuration is StorageTileConfig)
            {
                return true;
            }

            string? configurationAssemblyName =
                configuration?.GetType().Assembly.GetName().Name;
            if (string.Equals(
                    configurationAssemblyName,
                    "MoveThisHere",
                    StringComparison.Ordinal) ||
                string.Equals(
                    configurationAssemblyName,
                    "Storage Pod",
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (prefab.GetComponent<ManualDeliveryKG>() != null)
            {
                return true;
            }

            Storage? storage = prefab.GetComponent<Storage>();
            return (storage != null && storage.allowUIItemRemoval) ||
                prefab.GetComponent<StorageLocker>() != null ||
                prefab.GetComponent<ObjectDispenser>() != null ||
                prefab.GetComponent<SolidConduitInbox>() != null ||
                prefab.GetComponent<BottleEmptier>() != null ||
                prefab.GetComponent<CreatureFeeder>() != null ||
                prefab.GetComponent<RationBox>() != null ||
                prefab.GetComponent<Refrigerator>() != null;
        }

        private static bool TryAddTemperatureLimit(
            IBuildingConfig? configuration,
            GameObject? prefab)
        {
            if (!IsEligibleDeliveryTargetPrefab(configuration, prefab) ||
                prefab == null ||
                prefab.GetComponent<TemperatureLimit>() != null)
            {
                return false;
            }

            prefab.AddComponent<TemperatureLimit>();
            return true;
        }
    }
}
