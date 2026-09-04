# Delivery Temperature Limit (Supercooled)

![Oxygen Not Included Mod](https://img.shields.io/badge/Game-Oxygen_Not_Included-orange)

### DLC Compatibility
![Base Game](docs/badges/VanillaYes.png)
![Spaced Out!](docs/badges/Dlc1Yes.png)
![The Frosty Planet Pack](docs/badges/Dlc2Yes.png)
![The Bionic Booster Pack](docs/badges/Dlc3Yes.png)
![The Prehistoric Planet Pack](docs/badges/Dlc4Yes.png)
![The Aquatic Planet Pack](docs/badges/Dlc5Yes.png)


Tired of Nisbet carrying a 95°C chunk of hot Igneous Rock straight from a volcano and storing it in a Storage Bin right next to your pristine Sleet Wheat farm, melting all the ice? Are your Duplicants cooking your base from the inside out because they keep bringing boiling dirt to fertilize your crops? 

**Delivery Temperature Limit (Supercooled)** is here to put Duplicant thermal ignorance in check. It allows you to specify a safe temperature range for materials delivered to storage and other buildings, keeping your hot zones hot and your cold zones frost-bite fresh.

---

## 🚀 Key Features

*   **Thermal Filtering:** Set minimum and maximum temperature limits on storage lockers, storage tiles, refrigerators, and other delivery targets.
*   **Intelligent Routing:** Duplicants will ignore containers if the material's temperature lies outside your set limits. No more melting ice in storage!
*   **Construction Limits (Optional):** Enable material temperature limits for blueprints to keep hot materials away from insulated structures or cryogenic chambers.
*   **Clean UI Integration:** Tap into the standard building settings to set your ranges. 
    *   *UI Hint:* Type a value in one field, and the min/max auto-populates. Hit the `Del` key to wipe the fields and disable the limit entirely.

---

## ⚡ What's New in the "Supercooled" Version?

The backend of the original mod has been completely overhauled to run as cleanly as a Thermo Regulator in a vacuum:

1.  **Late-Game Performance:** Our earlier claim of “lag spikes removed” was, we admit, researched before we unlocked the Virtual Planetarium. Asteroid-wide item scans are gone. Each eligible building keeps its allowed temperature range on file, and errands are checked against the file. Cycle 1000+ mega-bases hold up.
2.  **Storage Tiles Check the Thermometer:** Storage Tiles now support delivery temperature limits, including aboard rockets. When a limit is set, Duplicants will no longer tuck volcano-fresh cargo under the floor and stamp it “properly stored.” Many thanks to [ShyLion](https://steamcommunity.com/id/shylion) for suggesting the fix before the next mission became an orbital sauna.
3.  **Fresh Start on Every Load:** Returning to the main menu sweeps out all background tracking. A bin in your shiny new colony will never be haunted by the temperature rules of one you abandoned.
4.  **Smooth Controls & UI:** Fixed the camera lockup when clicking temperature boxes, and made typing or wiping values instantaneous—no more frozen input fields or locked WASD panning.
5.  **Rock-Solid Startup & Stability:** Pre-checks game hooks on startup and during colony resource refreshes, preventing crashes to desktop and ensuring safe loading every cycle.
6.  **Custom Mod Storage Support:** Storage containers, hoppers, and lockers introduced by other mods are automatically detected and supported out of the box.
7.  **Fast Track Compatibility:** Seamlessly coordinates with Peter Han's [*Fast Track*](https://github.com/peterhaneve/ONIMods/tree/main/FastTrack) if installed, with zero overhead when running on the standard game engine.
8.  **Fully Tested on Current Game Builds:** Tested and verified against the latest game updates (Build 744825+).

---

## 🎓 Homage & Credits

This mod is built upon the incredible work of the ONI modding community, standing on the shoulders of giants:
*   **Original Concept & Code:** Created as [Delivery Temperature Limit](https://steamcommunity.com/sharedfiles/filedetails/?id=2963257205) by the talented [llunak](https://steamcommunity.com/profiles/76561198116805945), who built the initial thermal delivery system.
*   **Intermediate Maintenance:** Patched and maintained as [Delivery Temperature Limit [Fixed]](https://steamcommunity.com/sharedfiles/filedetails/?id=3479021027) by [[sd] QooLiO](https://steamcommunity.com/profiles/76561198006853888) to resolve compatibility issues and keep the mod working.
*   **Supercooled Edition:** Maintained, refactored, and optimized by [Maksym Shostak](https://github.com/MaksymShostak).

---

## 🛠️ Mod Settings

You can adjust mod settings directly in the game Options menu:
*   **Include Temperature in "Lacks Resources" Warning:** Toggle whether the yellow "Lacks Resources" warning takes your temperature limits into account (turn off for maximum late-game performance).
*   **Apply Limits to Construction Materials:** Toggle whether temperature limits also apply to materials delivered to build new structures (prevents using hot materials in cold-area construction).

---

## Support and contributing

Use the in-game mod options to create a local support report without manually finding game versions, enabled DLCs, settings, or active mods. The standard report does not read `Player.log`; the clearly labeled extended report includes a bounded, best-effort-redacted copy for harder failures. Nothing is uploaded automatically.

- [Report a bug](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml)
- [Suggest a feature](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-feature.yml)
- [Support and privacy details](SUPPORT.md)
- [Contributing](CONTRIBUTING.md)

---

## Development and release workflow

[ONI Mod Pipeline](docs/guides/oni-mod-development-workflow.md) is the repository's single supported path for validating, building, testing, installing, and preparing this mod for a manual Workshop upload. The user-facing command is `oni-mod-pipeline`.

> [!IMPORTANT]
> Development builds are repeatable working artifacts. Release candidates are immutable, install-once inputs to human acceptance. Carry the exact path printed by each command into the next command; never infer a “latest” run. ONI Mod Pipeline never performs the authenticated **Publish** action.

### Choose the workflow that matches your goal

| Goal | Start here | Use when |
| --- | --- | --- |
| Set up a checkout | [Getting started with ONI Mod Pipeline](docs/guides/getting-started-with-oni-mod-pipeline.md) | You are configuring the SDK, command, ONI paths, or profile discovery for the first time. |
| Iterate on mod code | [Developing ONI mods](docs/guides/developing-oni-mods.md) | You need a new isolated build, automated tests, or a guarded `mods/Dev` installation. |
| Prepare a Workshop update | [Preparing ONI mod releases](docs/guides/preparing-oni-mod-releases.md) | The version, listing, dependencies, tests, and contributing source are reviewed and ready to become one exact candidate. |
| Define a mod | [ONI Mod Pipeline profile reference](docs/guides/oni-mod-pipeline-profile-reference.md) | You need the schema-v1 keys, path rules, package allowlist, test declarations, or acceptance declarations. |
| Resolve a failure | [Troubleshooting ONI Mod Pipeline](docs/guides/troubleshooting-oni-mod-pipeline.md) | A command reports an `ONIP####` diagnostic, nonzero exit code, unsafe destination, or invalid candidate. |

### Run the development loop

Run from `mods/delivery-temperature-limit-supercooled`, or add `--mod mods/delivery-temperature-limit-supercooled` when running elsewhere:

```text
oni-mod-pipeline diagnose
oni-mod-pipeline validate
oni-mod-pipeline build
oni-mod-pipeline test
oni-mod-pipeline install --mod . --build-result <exact-build-result.json> --target dev
```

`build` prints a new `build-result.json`; pass that exact file to `install`. After editing source, create a new build run rather than reusing or modifying an older result.

### Prepare a release candidate

Before release preparation, deliberately edit and commit the intended version in `mod_info.yaml`, the current `STEAM_CHANGE_NOTES.bbcode`, and every other contributing source or listing change. Then run:

```text
git status --short
oni-mod-pipeline validate --for-release
oni-mod-pipeline test
oni-mod-pipeline prepare-release
oni-mod-pipeline install --candidate <exact-candidate-directory> --target local
# Perform every check in release-evidence/acceptance-test-plan.json.
oni-mod-pipeline record-acceptance --candidate <exact-candidate-directory> --tester <display-name>
oni-mod-pipeline verify-release --candidate <exact-candidate-directory>
```

A successful `verify-release` reports `ready-for-upload` and regenerates the candidate's `release-summary.md` and `uploader-checklist.md`. In the ONI Uploader, select only the generated `workshop-content` directory for **Update Data** and copy the generated listing files from `workshop-listing`. Never upload `release-evidence` or use the mutable Dev/Local installation as Update Data.

Read [ONI mod development workflow](docs/guides/oni-mod-development-workflow.md) for the command lifecycle, exact-path discipline, and documentation map.

---

*Disclaimer: This is a community mod. It is not affiliated with, sponsored by, or endorsed by Klei Entertainment.*
