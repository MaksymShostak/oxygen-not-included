$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\Source\Buildings.cs'
$productionSource = Get-Content -Raw $sourcePath

$gameStubs = @'
namespace HarmonyLib
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch(System.Type type) { }
        public HarmonyPatch(string methodName) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class HarmonyPostfix : System.Attribute { }

    public sealed class HarmonyMethod
    {
        public HarmonyMethod(System.Reflection.MethodInfo method) { }
    }

    public sealed class Harmony
    {
        public void Patch(
            System.Reflection.MethodInfo original,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null) { }
    }

    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(System.Type type, string name) => null;
        public static System.Reflection.FieldInfo Field(System.Type type, string name) => null;
    }
}

namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly System.Collections.Generic.Dictionary<System.Type, object> components =
            new System.Collections.Generic.Dictionary<System.Type, object>();

        public T GetComponent<T>() where T : class
        {
            object component;
            return components.TryGetValue(typeof(T), out component) ? (T)component : null;
        }

        public T AddComponent<T>() where T : class, new()
        {
            T component = new T();
            components[typeof(T)] = component;
            return component;
        }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) { }
    }
}

public interface IBuildingConfig { }
public sealed class StorageTileConfig : IBuildingConfig { }

public sealed class BuildingConfigManager
{
    public static BuildingConfigManager Instance { get; } = new BuildingConfigManager();
    public void ConfigurePost() { }
}

public sealed class BuildingDef
{
    public UnityEngine.GameObject BuildingComplete;
    public UnityEngine.GameObject BuildingUnderConstruction;
}

public sealed class TemperatureLimit { }
public sealed class ManualDeliveryKG { }
public sealed class Storage { public bool allowUIItemRemoval; }
public sealed class StorageLocker { }
public sealed class ObjectDispenser { }
public sealed class SolidConduitInbox { }
public sealed class BottleEmptier { }
public sealed class CreatureFeeder { }
public sealed class RationBox { }
public sealed class Refrigerator { }
'@

$testSource = @'
namespace DeliveryTemperatureLimit.Tests
{
    public static class BuildingsEligibilityRegression
    {
        public static void Run()
        {
            System.Reflection.MethodInfo method = typeof(Buildings_Patch).GetMethod(
                "IsEligible",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method == null)
                throw new System.Exception("Buildings_Patch.IsEligible was not found.");

            bool result = (bool)method.Invoke(
                null,
                new object[] { new StorageTileConfig(), new UnityEngine.GameObject() });

            if (!result)
                throw new System.Exception("StorageTileConfig should be eligible for temperature limits.");
        }
    }
}
'@

$combinedSource = $productionSource + [Environment]::NewLine + $gameStubs + [Environment]::NewLine + $testSource
Add-Type -TypeDefinition $combinedSource

[DeliveryTemperatureLimit.Tests.BuildingsEligibilityRegression]::Run()
Write-Output 'PASS: StorageTileConfig is eligible for temperature limits.'
