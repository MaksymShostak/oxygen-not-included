using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections;

namespace DeliveryTemperatureLimit
{
    [HarmonyPatch(typeof(BuildingConfigManager))]
    [HarmonyPatch(nameof(BuildingConfigManager.ConfigurePost))]
    public class Buildings_Patch
    {
        public static void Patch( Harmony harmony )
        {
            MethodInfo original = AccessTools.Method(typeof(BuildingConfigManager), nameof(BuildingConfigManager.ConfigurePost));
            MethodInfo postfix = AccessTools.Method(typeof(Buildings_Patch), nameof(Postfix));
            if (original != null && postfix != null)
            {
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            }
            else
            {
                Debug.LogError("DeliveryTemperatureLimit: Failed to find BuildingConfigManager.ConfigurePost to patch");
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                FieldInfo configTableField = AccessTools.Field(typeof(BuildingConfigManager), "configTable");
                if (configTableField == null)
                {
                    Debug.LogError("DeliveryTemperatureLimit: Failed to find configTable field in BuildingConfigManager");
                    return;
                }

                IDictionary configTable = configTableField.GetValue(BuildingConfigManager.Instance) as IDictionary;
                if (configTable == null)
                {
                    Debug.LogError("DeliveryTemperatureLimit: configTable is null or not IDictionary");
                    return;
                }

                int addedCount = 0;
                foreach (DictionaryEntry entry in configTable)
                {
                    IBuildingConfig config = entry.Key as IBuildingConfig;
                    BuildingDef def = entry.Value as BuildingDef;
                    if (def == null) continue;

                    // Add TemperatureLimit to complete building if eligible
                    GameObject completeGo = def.BuildingComplete;
                    if (completeGo != null && IsEligible(config, completeGo))
                    {
                        if (completeGo.GetComponent<TemperatureLimit>() == null)
                        {
                            completeGo.AddComponent<TemperatureLimit>();
                            addedCount++;
                        }
                    }

                    // Add TemperatureLimit to under-construction building if eligible
                    GameObject underConstructionGo = def.BuildingUnderConstruction;
                    if (underConstructionGo != null && IsEligible(config, completeGo))
                    {
                        if (underConstructionGo.GetComponent<TemperatureLimit>() == null)
                        {
                            underConstructionGo.AddComponent<TemperatureLimit>();
                        }
                    }
                }
                Debug.Log($"DeliveryTemperatureLimit: Successfully initialized temperature limits for {addedCount} building types dynamically.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"DeliveryTemperatureLimit: Error in Buildings_Patch.Postfix: {ex}");
            }
        }

        private static bool IsEligible(IBuildingConfig config, GameObject go)
        {
            if (go == null) return false;

            // Check if it belongs to the specific modded assemblies
            if (config != null)
            {
                string asmName = config.GetType().Assembly.GetName().Name;
                if (asmName == "MoveThisHere" || asmName == "Storage Pod")
                {
                    return true;
                }
            }

            // Check if it has a manual delivery component
            if (go.GetComponent<ManualDeliveryKG>() != null) return true;

            // Check if it has any user-interactive storage
            var storage = go.GetComponent<Storage>();
            if (storage != null && storage.allowUIItemRemoval) return true;

            // Check for other standard delivery components
            if (go.GetComponent<StorageLocker>() != null) return true;
            if (go.GetComponent<ObjectDispenser>() != null) return true;
            if (go.GetComponent<SolidConduitInbox>() != null) return true;
            if (go.GetComponent<BottleEmptier>() != null) return true;
            if (go.GetComponent<CreatureFeeder>() != null) return true;
            if (go.GetComponent<RationBox>() != null) return true;
            if (go.GetComponent<Refrigerator>() != null) return true;

            return false;
        }
    }
}
