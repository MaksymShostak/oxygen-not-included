# Delivery Temperature Limit (Supercooled)

![Oxygen Not Included Mod](https://img.shields.io/badge/Game-Oxygen_Not_Included-orange)
![Compatible with Base Game](https://img.shields.io/badge/DLC-Base_Game-orange?style=flat&logo=steam)
![Compatible with Spaced Out!](https://img.shields.io/badge/DLC-Spaced_Out!-blue?style=flat&logo=steam)
![Compatible with Frosty Planet Pack](https://img.shields.io/badge/DLC-Frosty_Planet_Pack-cyan?style=flat&logo=steam)
![Compatible with Bionic Booster Pack](https://img.shields.io/badge/DLC-Bionic_Booster_Pack-purple?style=flat&logo=steam)
![Compatible with Prehistoric Planet Pack](https://img.shields.io/badge/DLC-Prehistoric_Planet_Pack-brown?style=flat&logo=steam)
![Compatible with Aquatic Planet Pack](https://img.shields.io/badge/DLC-Aquatic_Planet_Pack-teal?style=flat&logo=steam)
![API Version](https://img.shields.io/badge/API_Version-APIVersion_2-green)

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

1.  **Storage Tiles Check the Thermometer:** Storage Tiles now support delivery temperature limits, including aboard rockets. When a limit is set, Duplicants will no longer tuck volcano-fresh cargo under the floor and stamp it “properly stored.” Many thanks to [ShyLion](https://steamcommunity.com/id/shylion) for suggesting the fix before the next mission became an orbital sauna.
2.  **Late-Game Lag Spikes Removed:** Completely optimized the temperature check logic. The mod now runs incredibly smoothly even in massive, highly populated late-game colonies with hundreds of delivery-capable buildings.
3.  **No More Camera Lockups:** Fixed the bug where selecting the mod's temperature panel blocked you from moving your camera using the keyboard.
4.  **UI Freeze Protection:** Fixed the interface lockup that occurred when clearing out temperature limit fields or typing a high limit first on a new bin.
5.  **Game Loading Stability:** Resolved startup and loading crashes, ensuring the mod initializes safely when loading into a save.
6.  **Inventory Update Crash Fix:** Resolved a crash that occurred when the colony's world inventory list refreshed.
7.  **Modded Building Compatibility:** The mod now dynamically registers storage buildings, meaning it automatically works with custom storage bins and buildings introduced by other mods.
8.  **Latest Game Update Support:** Fully compatible with Game Update 737790, resolving UI layout errors present in older versions.
9.  **Full DLC Support & Future-Proofed:** Out-of-the-box compatibility with the Base Game, *Spaced Out!*, *The Frosty Planet Pack*, *The Bionic Booster Pack*, *The Prehistoric Planet Pack*, and *The Aquatic Planet Pack* DLCs. The mod dynamically hooks into the game's core delivery rules, meaning new buildings in upcoming DLCs are supported automatically.

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

## Development and Release Workflow

[ONI Mod Pipeline](docs/guides/oni-mod-development-workflow.md) is the repository's single supported path for building, testing, installing, and preparing this mod for a manual Workshop upload. It requires the .NET 10 SDK selected by `global.json`, a local Oxygen Not Included installation, an existing ONI user-data directory, and the `oni-mod-pipeline` command on `PATH`.

Run the normal release sequence from `mods/delivery-temperature-limit-supercooled` (or pass that directory with `--mod`):

```text
oni-mod-pipeline diagnose
oni-mod-pipeline validate
oni-mod-pipeline test
oni-mod-pipeline prepare-release
oni-mod-pipeline install --candidate <path> --target local
# perform in-game acceptance
oni-mod-pipeline record-acceptance --candidate <path>
oni-mod-pipeline verify-release --candidate <path>
# inspect release-summary.md and uploader-checklist.md
# publish manually with the authenticated ONI Uploader
```

`oni-mod-pipeline.toml` is discovered from the current directory, an explicit profile path, or a descendant path by walking upward to the repository boundary. Explicit command options take precedence over environment variables and automatic discovery:

| Purpose | Command option | Environment variable |
| --- | --- | --- |
| ONI installation root | `--game-directory` | `ONI_GAME_DIRECTORY` |
| ONI per-user data root | `--user-data-directory` | `ONI_USER_DATA_DIRECTORY` |
| Generated artifact root | `--artifacts-directory` | `ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY` |

The version in `mod_info.yaml` and the text in `STEAM_CHANGE_NOTES.bbcode` are deliberate, reviewed source edits made before `prepare-release`. Builds never increment or rewrite either file. ONI Mod Pipeline prepares and verifies exact candidate bytes; it never performs the authenticated **Publish** action.

---

*Disclaimer: This is a community mod. It is not affiliated with, sponsored by, or endorsed by Klei Entertainment.*
