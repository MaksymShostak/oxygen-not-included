#nullable enable

using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns the optional blueprint material limit, its build-screen widget, and
    /// transfer of that exact setting to newly instantiated construction sites.
    /// </summary>
    internal static class ConstructionMaterialTemperatureLimit
    {
        private static TemperatureLimit? currentBlueprintTemperatureLimit;
        private static FieldInfo? verifiedMaterialSelectionPanelField;

        internal static MethodInfo ResolveMaterialSelectionPanelPrefabInitializationTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MaterialSelectionPanel),
                "OnPrefabInit",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());

        internal static MethodInfo ResolveMaterialSelectionPanelConfigurationTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(MaterialSelectionPanel),
                "ConfigureScreen",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[]
                {
                    typeof(Recipe),
                    typeof(MaterialSelectionPanel.GetBuildableStateDelegate),
                    typeof(MaterialSelectionPanel.GetBuildableTooltipDelegate)
                });

        internal static MethodInfo ResolveBuildingDefinitionInstantiationTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(BuildingDef),
                "Instantiate",
                DeclaredMemberVisibility.Public,
                typeof(GameObject),
                new[]
                {
                    typeof(Vector3),
                    typeof(Orientation),
                    typeof(IList<Tag>),
                    typeof(int)
                });

        internal static MethodInfo ResolveBuildingDefinitionPostProcessingTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(BuildingDef),
                "PostProcess",
                DeclaredMemberVisibility.Public,
                typeof(void),
                Array.Empty<Type>());

        internal static FieldInfo ResolveDetailsScreenMaterialSelectionPanelField()
        {
            FieldInfo verifiedField = HarmonyPatchContractVerifier.RequireField(
                typeof(DetailsScreenMaterialPanel),
                "materialSelectionPanel",
                DeclaredMemberVisibility.NonPublic,
                FieldStorageKind.Instance,
                typeof(MaterialSelectionPanel));
            verifiedMaterialSelectionPanelField = verifiedField;
            return verifiedField;
        }

        internal static void MaterialSelectionPanelPrefabInitializationPostfix(
            MaterialSelectionPanel __instance)
        {
            if (!DeliveryTemperatureLimitOptions.Instance.UnderConstructionLimit ||
                __instance == null ||
                !IsSupportedBlueprintMaterialSelectionPanel(__instance))
            {
                return;
            }

            // All build-menu panel instances represent the same not-yet-created
            // target. The singleton lives on the first suitable panel object and
            // is copied into each construction site only at instantiation.
            if (currentBlueprintTemperatureLimit == null)
            {
                currentBlueprintTemperatureLimit =
                    __instance.gameObject.AddOrGet<TemperatureLimit>();
            }

            ResetConstructionMaterialTemperatureLimitToDefaultsIfOwned(
                currentBlueprintTemperatureLimit);
            _ = __instance.gameObject.AddOrGet<TemperatureLimitWidget>();
        }

        internal static void MaterialSelectionPanelConfigurationPostfix(
            MaterialSelectionPanel __instance)
        {
            if (!DeliveryTemperatureLimitOptions.Instance.UnderConstructionLimit ||
                __instance == null)
            {
                return;
            }

            TemperatureLimitWidget? widget =
                __instance.GetComponent<TemperatureLimitWidget>();
            widget?.SetTarget(currentBlueprintTemperatureLimit);
        }

        internal static void BuildingDefinitionInstantiationPostfix(
            GameObject? __result)
        {
            if (!DeliveryTemperatureLimitOptions.Instance.UnderConstructionLimit ||
                __result == null ||
                currentBlueprintTemperatureLimit == null)
            {
                // MoveThisHere may deliberately replace Instantiate with a null
                // result; no component lookup is safe or useful in that case.
                return;
            }

            __result.AddOrGet<TemperatureLimit>().CopySettings(
                currentBlueprintTemperatureLimit);
        }

        internal static void BuildingDefinitionPostProcessingPostfix(
            BuildingDef __instance)
        {
            if (!DeliveryTemperatureLimitOptions.Instance.UnderConstructionLimit ||
                __instance == null ||
                __instance.BuildingUnderConstruction == null)
            {
                return;
            }

            // Every under-construction prefab needs the serialized component so
            // an existing save can load a blueprint configured by an earlier run.
            _ = __instance.BuildingUnderConstruction.gameObject
                .AddOrGet<TemperatureLimit>();
        }

        internal static void
            ResetConstructionMaterialTemperatureLimitToDefaultsIfOwned(
                TemperatureLimit? candidateTemperatureLimit)
        {
            if (candidateTemperatureLimit == null ||
                candidateTemperatureLimit != currentBlueprintTemperatureLimit)
            {
                return;
            }

            candidateTemperatureLimit.SetLowLimit(
                ConvertDisplayedTemperatureToKelvin(
                    DeliveryTemperatureLimitOptions.Instance
                        .MinConstructionTemperature));
            candidateTemperatureLimit.SetHighLimit(
                ConvertDisplayedTemperatureToKelvin(
                    DeliveryTemperatureLimitOptions.Instance
                        .MaxConstructionTemperature));
        }

        private static bool IsSupportedBlueprintMaterialSelectionPanel(
            MaterialSelectionPanel candidatePanel)
        {
            DetailsScreen? detailsScreen = DetailsScreen.Instance;
            if (detailsScreen == null)
            {
                // Until the Details Screen exists, the change-material panel
                // cannot be distinguished from a build-menu panel. Deferring is
                // safer than attaching blueprint state to the wrong singleton.
                return false;
            }

            var materialTab = detailsScreen.GetTabOfType(
                DetailsScreen.SidescreenTabTypes.Material);
            if (materialTab?.bodyInstance == null)
            {
                return false;
            }

            DetailsScreenMaterialPanel? materialPanel =
                materialTab.bodyInstance
                    .GetComponentInChildren<DetailsScreenMaterialPanel>();
            if (materialPanel == null)
            {
                return false;
            }

            object? changeMaterialPanel =
                (verifiedMaterialSelectionPanelField ??
                 ResolveDetailsScreenMaterialSelectionPanelField())
                    .GetValue(materialPanel);
            return !ReferenceEquals(candidatePanel, changeMaterialPanel);
        }

        private static int ConvertDisplayedTemperatureToKelvin(
            int displayedTemperature) =>
            (int)Math.Round(GameUtil.GetTemperatureConvertedToKelvin(
                displayedTemperature,
                GameUtil.temperatureUnit));
    }
}
