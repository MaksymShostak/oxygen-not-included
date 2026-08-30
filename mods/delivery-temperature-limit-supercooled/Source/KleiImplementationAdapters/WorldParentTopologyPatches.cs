#nullable enable

using System;
using System.Reflection;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Translates main-thread ONI world lifecycle callbacks into integer-only game
    /// session mutations. Manual installation is deferred to the coordinated
    /// runtime installer; this class contains no patch-discovery metadata.
    /// </summary>
    internal static class WorldParentTopologyPatches
    {
        private const string InvalidRegisterWorldIdentityDiagnosticKey =
            "world-topology.register.invalid-identity";
        private const string InvalidUnregisterWorldIdentityDiagnosticKey =
            "world-topology.unregister.invalid-identity";
        private const string UnknownUnregisterWorldIdentityDiagnosticKey =
            "world-topology.unregister.unknown-world";
        private const string InvalidReparentWorldIdentityDiagnosticKey =
            "world-topology.reparent.invalid-identity";

        internal static MethodInfo
            ResolveClusterManagerRegisterWorldContainerTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManager),
                "RegisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(WorldContainer) });

        internal static MethodInfo
            ResolveClusterManagerUnregisterWorldContainerTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(ClusterManager),
                "UnregisterWorldContainer",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(WorldContainer) });

        internal static MethodInfo ResolveWorldContainerSetParentIdxTarget() =>
            HarmonyPatchContractVerifier.RequireInstanceMethod(
                typeof(WorldContainer),
                "SetParentIdx",
                DeclaredMemberVisibility.Public,
                typeof(void),
                new[] { typeof(int) });

        internal static void RegisterWorldContainerPostfix(
            WorldContainer worldContainer)
        {
            if (!TryCaptureValidatedWorldIdentity(
                    worldContainer,
                    InvalidRegisterWorldIdentityDiagnosticKey,
                    "register",
                    out var session,
                    out var worldId,
                    out var parentWorldId))
            {
                return;
            }

            session.RegisterWorld(worldId, parentWorldId);
        }

        internal static void UnregisterWorldContainerPrefix(
            WorldContainer worldContainer)
        {
            if (!TryCaptureValidatedWorldIdentity(
                    worldContainer,
                    InvalidUnregisterWorldIdentityDiagnosticKey,
                    "unregister",
                    out var session,
                    out var worldId,
                    out _))
            {
                return;
            }

            WorldParentTopologyChange change = session.RemoveWorld(worldId);
            if (!change.HasChanged)
            {
                EmitDiagnosticOnce(
                    session,
                    UnknownUnregisterWorldIdentityDiagnosticKey,
                    "Ignored an ONI world-unregistration callback for unknown " +
                    "world ID " +
                    worldId +
                    "; no topology mapping was guessed.");
            }
        }

        internal static void SetParentIdxPostfix(WorldContainer __instance)
        {
            if (!TryCaptureValidatedWorldIdentity(
                    __instance,
                    InvalidReparentWorldIdentityDiagnosticKey,
                    "reparent",
                    out var session,
                    out var worldId,
                    out var parentWorldId))
            {
                return;
            }

            // Read ParentWorldId only after SetParentIdx has completed; the session
            // receives the resulting integer relationship and never retains or
            // dereferences the Unity object from worker-accessible domain state.
            session.RegisterWorld(worldId, parentWorldId);
        }

        private static bool TryCaptureValidatedWorldIdentity(
            WorldContainer worldContainer,
            string invalidIdentityDiagnosticKey,
            string lifecycleOperation,
            out DeliveryTemperatureGameSession session,
            out int worldId,
            out int parentWorldId)
        {
            worldId = -1;
            parentWorldId = -1;
            if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out session))
            {
                return false;
            }

            if (worldContainer == null)
            {
                EmitDiagnosticOnce(
                    session,
                    invalidIdentityDiagnosticKey,
                    "Ignored an ONI world-" +
                    lifecycleOperation +
                    " callback without a WorldContainer instance.");
                return false;
            }

            // Unity objects are touched only in this main-thread adapter. Every
            // downstream service receives ordinary integer value identities.
            worldId = worldContainer.id;
            parentWorldId = worldContainer.ParentWorldId;
            if (worldId >= 0 && parentWorldId >= 0)
            {
                return true;
            }

            EmitDiagnosticOnce(
                session,
                invalidIdentityDiagnosticKey,
                "Ignored an ONI world-" +
                lifecycleOperation +
                " callback with invalid world ID " +
                worldId +
                " or parent-world ID " +
                parentWorldId +
                "; no topology mapping was guessed.");
            return false;
        }

        private static void EmitDiagnosticOnce(
            DeliveryTemperatureGameSession session,
            string diagnosticKey,
            string message)
        {
            if (session.DiagnosticLimiter.ShouldEmit(diagnosticKey))
            {
                Debug.LogWarning("DeliveryTemperatureLimit: " + message);
            }
        }
    }
}
