#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Captures the cold, game-load-scoped evidence used to inspect FastTrack.
    /// The active patch list is copied so later Harmony changes cannot mutate a
    /// report while it is being built or consumed.
    /// </summary>
    internal sealed class FastTrackLoadedGameInspectionInput
    {
        internal FastTrackLoadedGameInspectionInput(
            bool isFastTrackEnabledForLoadedGame,
            Assembly? fastTrackAssembly,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activeHarmonyPrefixes)
        {
            if (activeHarmonyPrefixes == null)
            {
                throw new ArgumentNullException(nameof(activeHarmonyPrefixes));
            }

            var copiedPrefixes =
                new ActiveHarmonyPatchDescriptor[activeHarmonyPrefixes.Count];
            for (var prefixIndex = 0;
                 prefixIndex < activeHarmonyPrefixes.Count;
                 prefixIndex++)
            {
                ActiveHarmonyPatchDescriptor prefix =
                    activeHarmonyPrefixes[prefixIndex];
                if (prefix == null)
                {
                    throw new ArgumentException(
                        "An active Harmony prefix descriptor cannot be null.",
                        nameof(activeHarmonyPrefixes));
                }

                copiedPrefixes[prefixIndex] = prefix;
            }

            IsFastTrackEnabledForLoadedGame =
                isFastTrackEnabledForLoadedGame;
            FastTrackAssembly = fastTrackAssembly;
            ActiveHarmonyPrefixes = Array.AsReadOnly(copiedPrefixes);
        }

        internal bool IsFastTrackEnabledForLoadedGame { get; }

        internal Assembly? FastTrackAssembly { get; }

        internal IReadOnlyList<ActiveHarmonyPatchDescriptor>
            ActiveHarmonyPrefixes { get; }
    }
}
