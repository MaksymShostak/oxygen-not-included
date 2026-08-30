#nullable enable

using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Initializes player-facing services and delegates all Harmony mutation to
    /// the fail-closed runtime patch installer.
    /// </summary>
    public sealed class DeliveryTemperatureLimitMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary(false);
            Localization.RegisterForTranslation(
                typeof(STRINGS.TEMPERATURELIMIT));
            new POptions().RegisterOptions(
                this,
                typeof(DeliveryTemperatureLimitOptions));

            // These targets exist independently of the loaded-mod topology.
            // The installer still resolves every member before it mutates
            // Harmony, and rolls back only methods installed by this attempt.
            DeliveryTemperatureRuntimePatchInstaller
                .InstallLoadedModTopologyIndependentPatches(harmony);
        }

        public override void OnAllModsLoaded(
            Harmony harmony,
            IReadOnlyList<KMod.Mod> loadedMods)
        {
            base.OnAllModsLoaded(harmony, loadedMods);

            // FastTrack must be identified from ONI's active loaded-mod graph,
            // never from an assembly that merely happens to be loadable. This
            // phase selects one coherent implementation family and verifies the
            // complete selected contract before installing its first patch.
            DeliveryTemperatureRuntimePatchInstaller
                .InstallLoadedModTopologyDependentPatches(
                    harmony,
                    loadedMods);
        }
    }
}
