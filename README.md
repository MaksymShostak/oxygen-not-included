# Delivery Temperature Limit (Supercooled)

![Oxygen Not Included Mod](https://img.shields.io/badge/Game-Oxygen_Not_Included-orange)

### DLC Compatibility
![Base Game](docs/badges/VanillaYes.png)
![Spaced Out!](docs/badges/Dlc1Yes.png)
![The Frosty Planet Pack](docs/badges/Dlc2Yes.png)
![The Bionic Booster Pack](docs/badges/Dlc3Yes.png)
![The Prehistoric Planet Pack](docs/badges/Dlc4Yes.png)
![The Aquatic Planet Pack](docs/badges/Dlc5Yes.png)


<!-- oni-mod-pipeline:workshop-description:start -->
Tired of Nisbet hauling a 95 °C chunk of Igneous Rock into the Storage Bin beside your carefully chilled Sleet Wheat? Have your Duplicants discovered that "Raw Minerals" and "portable space heater" are apparently the same storage category?

**Delivery Temperature Limit (Supercooled)** lets you set a safe temperature range for materials delivered to storage, buildings, and - optionally - construction.

Hot rock stays with the hot rock. Cold supplies stay cold. Duplicants remain free to make entirely different mistakes.

***

# Compatibility & Localisation

![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/VanillaYes.png) ![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc1Yes.png) ![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc2Yes.png)
![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc3Yes.png) ![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc4Yes.png) ![](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc5Yes.png)

**Localisation:** English + 18 translated locales, selected automatically from ONI's language setting.
Simplified & Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, Portuguese & Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian and Vietnamese.

**Tested with ONI build 744825.**

***

# What It Does

* **Temperature-Limited Storage:** Give supported delivery targets a minimum temperature, a maximum temperature, or both. Storage Bins, Storage Tiles, Refrigerators, and compatible storage buildings can all check the thermometer before accepting a delivery.
* **Temperature-Aware Errands:** Materials outside a target's allowed range are excluded from its delivery errands. "It's the nearest rock" is no longer a valid defence when the nearest rock is trying to cook the Sleet Wheat.
* **Optional Construction Limits:** Apply the same rules to construction materials so Duplicants do not carry furnace-hot building material into the cold room you have spent forty cycles cooling.
* **Thermo Sensor-Style Controls:** Set independent **At least** and **Below** thresholds directly from the building side-screen, with °C / °F / K labels, copy-settings support, a one-click **Clear** button, and a live plain-language summary of the active range.
* **Keyboard-Friendly UI:** Tab between fields, Enter to commit and Escape to cancel without sacrificing WASD panning or Space to pause. The camera controls have been formally released from temperature-box custody.

***

# What Supercooled Changes

**Supercooled** is a maintained and substantially refactored continuation of the original Delivery Temperature Limit mod. The basic idea is unchanged; quite a lot underneath it is not.

* **Direct Temperature Checks:** The old colony-wide inventory scanning path has been removed. Eligible targets keep their configured temperature range available for errand checks directly, reducing work in large late-game colonies. The mod no longer conducts an asteroid census every time Nisbet picks up a rock.
* **Storage Tile Support:** Storage Tiles support the same temperature limits, including inside rockets. "Under the floor" is not a thermal exemption. Thanks to [ShyLion](https://steamcommunity.com/id/shylion) for helping prevent the next unexpected orbital sauna.
* **Clean Colony State Between Loads:** Transient runtime tracking is cleared between colony sessions while saved temperature limits and mod settings remain intact. No haunted Storage Bins; no accidental exorcism of your actual settings.
* **Rebuilt Temperature UI:** The side-screen now uses independent **At least** and **Below** values, external unit labels, live range summaries, warning feedback for inverted limits, dedicated clearing, and reliable keyboard handling.
* **Safer Startup & Storage Discovery:** Defensive checks protect the mod's game hooks during startup and colony refreshes, while compatible storage targets introduced by other mods can be discovered dynamically.
* **Fast Track Compatibility:** Integrates with Peter Han's [_Fast Track_](https://github.com/peterhaneve/ONIMods/tree/main/FastTrack) when it is present and uses the normal game path when it is not.
* **Built-In Localisation:** English plus 18 translated locales are bundled with the mod and follow ONI's selected language automatically. Meep's interpretive-dance translation programme has been retired.

***

# Mod Options

The in-game Mod Options dialog provides:

* **Temperature-Aware "Lacks Resources":** Choose whether the warning also considers your temperature limits. Disable it if you prefer the lightest possible late-game warning checks.
* **Construction Material Limits:** Choose whether temperature restrictions also apply to materials delivered to blueprints.
* **Temperature Previews:** Preview values in Celsius, Fahrenheit, or Kelvin with automatic conversion, and reset the mod's options to their defaults in one click.

***

# Support & Diagnostics

If something goes thermally sideways, open Mod Options and choose **Create Support Report**.

Reports are generated locally and nothing is uploaded automatically. The standard report does not read _Player.log_; the clearly labelled **Extended Support Report** can include a bounded, best-effort-redacted log excerpt for harder failures.

Review the report on your computer, then attach it to the GitHub bug-report form.

Duplicants cannot currently be included in the diagnostic bundle, despite repeated requests from colony management.

***

# Credits

This mod continues the incredible work begun by the ONI modding community:

* **Original Concept & Code**: llunak - [_Delivery Temperature Limit_](https://steamcommunity.com/sharedfiles/filedetails/?id=2963257205)
* **Intermediate Maintenance**: \[sd] QooLiO - [_Delivery Temperature Limit \[Fixed\]_](https://steamcommunity.com/sharedfiles/filedetails/?id=3479021027)

***

Developed by [Maksym Shostak](https://orcid.org/0000-0001-8017-8797).
Free Dive into the source code on [GitHub](https://github.com/MaksymShostak/oxygen-not-included).

_Delivery Temperature Limit (Supercooled) is a community mod and is not affiliated with, sponsored by, or endorsed by Klei Entertainment._
<!-- oni-mod-pipeline:workshop-description:end -->

## Support and contributing

Use the in-game mod options to create a local support report without manually finding game versions, enabled DLCs, settings, or active mods. The standard report does not read `Player.log`; the clearly labeled extended report includes a bounded, best-effort-redacted copy for harder failures. Nothing is uploaded automatically.

- [Report a bug](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml)
- [Suggest a feature](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-feature.yml)
- [Support and privacy details](SUPPORT.md)
- [Contributing](CONTRIBUTING.md)

---

## 🌐 Community Translations

Translations for community languages are warmly welcomed!

*   **Translation Guide:** Check out the [Translation Guide](docs/guides/translating-delivery-temperature-limit-supercooled.md) for UI context, formatting tags, placeholders, and workflow guidance.
*   **Template (`.pot`):** The master Gettext template catalog containing all current strings is located at [`mods/delivery-temperature-limit-supercooled/translations/delivery_temperature_limit.pot`](mods/delivery-temperature-limit-supercooled/translations/delivery_temperature_limit.pot).
*   **Language Catalogs (`.po`):** Active translations (`de`, `es`, `fr`, `ko`, `pt`, `pt_BR`, `uk`, `zh`, `zh_tw`) live in the [`translations/`](mods/delivery-temperature-limit-supercooled/translations) directory. Submit new languages or updates via pull request!

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
