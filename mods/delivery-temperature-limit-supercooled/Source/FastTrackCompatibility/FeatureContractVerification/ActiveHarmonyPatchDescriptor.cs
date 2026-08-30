#nullable enable

using System;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Carries only the immutable reflection metadata needed to reason about an
    /// already-active Harmony prefix. Keeping this value Harmony-free prevents
    /// the compatibility verifier from acquiring a compile-time mod dependency.
    /// </summary>
    internal sealed class ActiveHarmonyPatchDescriptor
    {
        internal ActiveHarmonyPatchDescriptor(
            MethodBase targetMethod,
            MethodInfo patchMethod,
            string harmonyOwner,
            int priority)
        {
            TargetMethod = targetMethod ??
                throw new ArgumentNullException(nameof(targetMethod));
            PatchMethod = patchMethod ??
                throw new ArgumentNullException(nameof(patchMethod));
            if (string.IsNullOrWhiteSpace(harmonyOwner))
            {
                throw new ArgumentException(
                    "An active Harmony patch must identify its non-blank owner.",
                    nameof(harmonyOwner));
            }

            HarmonyOwner = harmonyOwner;
            Priority = priority;
        }

        internal MethodBase TargetMethod { get; }

        internal MethodInfo PatchMethod { get; }

        internal string HarmonyOwner { get; }

        internal int Priority { get; }
    }
}
