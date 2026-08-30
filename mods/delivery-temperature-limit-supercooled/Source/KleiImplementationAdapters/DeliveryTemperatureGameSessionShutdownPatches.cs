#nullable enable

using System;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Provides the manually installed two-phase boundary around ONI game-object
    /// destruction. This class is intentionally undiscoverable by Harmony until
    /// the coordinated runtime installer explicitly selects and applies it.
    /// </summary>
    internal static class DeliveryTemperatureGameSessionShutdownPatches
    {
        internal static MethodInfo ResolveGameDestroyInstancesTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Game),
                "DestroyInstances",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());

        internal static void GameDestroyInstancesPrefix(
            Game __instance,
            out DeliveryTemperatureGameSession? __state)
        {
            if (__instance == null)
            {
                throw new ArgumentNullException(nameof(__instance));
            }

            // Detach first so every later hook observes no accepting session while
            // ONI begins destroying the objects that callbacks might otherwise use.
            __state = DeliveryTemperatureGameSessionHost.DetachGameSession(
                __instance.GetInstanceID());
        }

        internal static Exception? GameDestroyInstancesFinalizer(
            Exception? __exception,
            DeliveryTemperatureGameSession? __state)
        {
            // A finalizer is required rather than a postfix: owned pure-domain state
            // must be released even when ONI's destruction method throws. Returning
            // the same reference preserves the game's original exception outcome.
            DeliveryTemperatureGameSessionHost.CompleteShutdown(__state);
            return __exception;
        }
    }
}
