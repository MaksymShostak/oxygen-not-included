#nullable enable

using System;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Places the selected-owner check immediately before ONI begins initializing one
    /// game. This composition boundary spans Klei and optional FastTrack paths and
    /// therefore does not belong to either implementation adapter.
    /// </summary>
    internal static class DeliveryTemperatureGameLoadAuthorityPatches
    {
        internal static MethodInfo ResolveGameOnPrefabInitTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(Game),
                "OnPrefabInit",
                DeclaredMemberVisibility.NonPublic,
                typeof(void),
                Array.Empty<Type>());

        internal static void GameOnPrefabInitPrefix(Game __instance)
        {
            _ = DeliveryTemperatureRuntimePatchInstaller
                .TryStartAuthorizedGameSession(__instance);
        }
    }
}
