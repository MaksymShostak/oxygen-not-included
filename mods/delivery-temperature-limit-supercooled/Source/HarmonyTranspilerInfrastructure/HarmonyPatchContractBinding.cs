#nullable enable

using System;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    internal enum HarmonyPatchContractKind
    {
        Prefix,
        Postfix,
        Transpiler,
        Finalizer
    }

    /// <summary>
    /// Holds one fully resolved target/patch pair for preparation-time binding
    /// verification and later Harmony installation.
    /// </summary>
    internal sealed class HarmonyPatchContractBinding
    {
        internal HarmonyPatchContractBinding(
            MethodBase targetMethod,
            MethodInfo patchMethod,
            HarmonyPatchContractKind patchKind)
        {
            TargetMethod = targetMethod ??
                throw new ArgumentNullException(nameof(targetMethod));
            PatchMethod = patchMethod ??
                throw new ArgumentNullException(nameof(patchMethod));
            PatchKind = patchKind;
        }

        internal MethodBase TargetMethod { get; }

        internal MethodInfo PatchMethod { get; }

        internal HarmonyPatchContractKind PatchKind { get; }
    }
}
