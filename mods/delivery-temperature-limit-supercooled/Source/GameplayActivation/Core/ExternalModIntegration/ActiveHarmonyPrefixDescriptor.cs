#nullable enable

using System;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable BCL-only copy of the authority facts for one active Harmony
    /// prefix. Only prefixes that can skip an original are represented here.
    /// </summary>
    internal sealed class ActiveHarmonyPrefixDescriptor
    {
        internal ActiveHarmonyPrefixDescriptor(
            MethodBase targetMethod,
            MethodInfo prefixMethod,
            string harmonyOwner,
            int priority)
        {
            TargetMethod = targetMethod ??
                throw new ArgumentNullException(nameof(targetMethod));
            PrefixMethod = prefixMethod ??
                throw new ArgumentNullException(nameof(prefixMethod));
            HarmonyOwner = ExternalModIntegrationModelValidation
                .RequireExactBoundedText(
                    harmonyOwner,
                    nameof(harmonyOwner),
                    256,
                    "An active Harmony prefix owner");
            Priority = priority;
        }

        internal MethodBase TargetMethod { get; }

        internal MethodInfo PrefixMethod { get; }

        internal string HarmonyOwner { get; }

        internal int Priority { get; }
    }
}
