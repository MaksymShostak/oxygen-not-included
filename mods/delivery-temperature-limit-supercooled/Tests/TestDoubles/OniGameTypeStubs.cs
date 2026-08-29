public readonly struct Tag { }

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type) { }
        public HarmonyPatch(string methodName) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute { }

    public sealed class HarmonyMethod
    {
        public HarmonyMethod(System.Reflection.MethodInfo method) { }
    }

    public sealed class Harmony
    {
        public void Patch(
            System.Reflection.MethodInfo original,
            HarmonyMethod? prefix = null,
            HarmonyMethod? postfix = null) { }
    }

    public static class AccessTools
    {
        public static System.Reflection.MethodInfo? Method(Type type, string name) => null;
        public static System.Reflection.FieldInfo? Field(Type type, string name) => null;
    }
}

namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly Dictionary<Type, object> components = new();

        public T? GetComponent<T>() where T : class =>
            components.TryGetValue(typeof(T), out var component) ? (T)component : null;

        public T AddComponent<T>() where T : class, new()
        {
            var component = new T();
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

namespace DeliveryTemperatureLimit
{
    public sealed class TemperatureLimit { }
}

public interface IBuildingConfig { }
public sealed class StorageTileConfig : IBuildingConfig { }

public sealed class BuildingConfigManager
{
    public static BuildingConfigManager Instance { get; } = new();
    public void ConfigurePost() { }
}

public sealed class BuildingDef
{
    public UnityEngine.GameObject? BuildingComplete { get; set; }
    public UnityEngine.GameObject? BuildingUnderConstruction { get; set; }
}

public sealed class ManualDeliveryKG { }
public sealed class Storage { public bool allowUIItemRemoval { get; set; } }
public sealed class StorageLocker { }
public sealed class ObjectDispenser { }
public sealed class SolidConduitInbox { }
public sealed class BottleEmptier { }
public sealed class CreatureFeeder { }
public sealed class RationBox { }
public sealed class Refrigerator { }
