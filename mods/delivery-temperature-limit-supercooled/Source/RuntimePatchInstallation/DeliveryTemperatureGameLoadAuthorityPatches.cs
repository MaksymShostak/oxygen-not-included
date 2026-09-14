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
            if (__instance == null) return;
            try
            {
                if (__instance.gameObject.AddOrGet<RuntimeFailureNotification>() == null)
                    throw new InvalidOperationException("Runtime warning component could not be created.");
            }
            catch (Exception exception)
            {
                DeliveryTemperatureSupportReporter.Record("DTL-WARNING-UNAVAILABLE",
                    SupportDiagnosticSeverity.Error, "Runtime warnings are unavailable; failures will be logged.", exception);
            }
            try
            {
                if (DeliveryTemperatureGameSessionHost.RuntimeFailure.HasFailed) return;
                _ = DeliveryTemperatureRuntimePatchInstaller
                    .TryStartAuthorizedGameSession(__instance);
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableGameplay(nameof(GameOnPrefabInitPrefix), exception);
            }
        }
    }
}
