# Delivery Temperature Limit (Supercooled)

![Oxygen Not Included Mod](https://img.shields.io/badge/Game-Oxygen_Not_Included-orange)

<!-- oni-mod-pipeline:workshop-description:start -->

Tired of Nisbet hauling a 95 °C chunk of Igneous Rock into the Storage Bin beside your carefully chilled Sleet Wheat?
Have your Duplicants discovered that "Raw Minerals" and "portable space heater" are apparently the same storage category?

**Delivery Temperature Limit (Supercooled)** lets you set a safe temperature range for materials delivered to storage, buildings, and - optionally - construction.

Hot rock stays with the hot rock.
Cold supplies stay cold.
Duplicants remain free to make entirely different mistakes.

---

## Compatibility & Localisation

![Base game supported](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/VanillaYes.png) ![Spaced Out! supported](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc1Yes.png) ![The Frosty Planet Pack supported](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc2Yes.png) ![The Bionic Booster Pack supported](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc3Yes.png) ![The Prehistoric Planet Pack supported](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc4Yes.png) ![The Aquatic Planet Pack supported](https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/Dlc5Yes.png)

**Localisation:** English + 18 translated locales, selected automatically from ONI's language setting.
Simplified & Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, Portuguese & Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian and Vietnamese.

**Tested with ONI build 744825.**

---

## What It Does

- **Temperature-Limited Storage:** Give supported delivery targets a minimum temperature, a maximum temperature, or both.
  Storage Bins, Storage Tiles, Refrigerators, and compatible storage buildings can all check the thermometer before accepting a delivery.
- **Temperature-Aware Errands:** Materials outside a target's allowed range are excluded from its delivery errands.
  "It's the nearest rock" is no longer a valid defence when the nearest rock is trying to cook the Sleet Wheat.
- **Optional Construction Limits:** Apply the same rules to construction materials so Duplicants do not carry furnace-hot building material into the cold room you have spent forty cycles cooling.
- **Thermo Sensor-Style Controls:** Set independent **At least** and **Below** thresholds directly from the building side-screen, with °C / °F / K labels, copy-settings support, a one-click **Clear** button, and a live plain-language summary of the active range.
- **Keyboard-Friendly UI:** Tab between fields, Enter to commit and Escape to cancel without sacrificing WASD panning or Space to pause.
  The camera controls have been formally released from temperature-box custody.

---

## What Supercooled Changes

**Supercooled** is a maintained and substantially refactored continuation of the original Delivery Temperature Limit mod.
The basic idea is unchanged; quite a lot underneath it is not.

- **Direct Temperature Checks:** The old colony-wide inventory scanning path has been removed.
  Eligible targets keep their configured temperature range available for errand checks directly, reducing work in large late-game colonies.
  The mod no longer conducts an asteroid census every time Nisbet picks up a rock.
- **Storage Tile Support:** Storage Tiles support the same temperature limits, including inside rockets.
  "Under the floor" is not a thermal exemption.
  Thanks to [ShyLion](https://steamcommunity.com/id/shylion) for helping prevent the next unexpected orbital sauna.
- **Clean Colony State Between Loads:** Transient runtime tracking is cleared between colony sessions while saved temperature limits and mod settings remain intact.
  No haunted Storage Bins; no accidental exorcism of your actual settings.
- **Rebuilt Temperature UI:** The side-screen now uses independent **At least** and **Below** values, external unit labels, live range summaries, warning feedback for inverted limits, dedicated clearing, and reliable keyboard handling.
- **Safer Startup & Storage Discovery:** Defensive checks protect the mod's game hooks during startup and colony refreshes, while compatible storage targets introduced by other mods can be discovered dynamically.
- **Fast Track Compatibility:** Integrates with Peter Han's [_Fast Track_](https://github.com/peterhaneve/ONIMods/tree/main/FastTrack) when it is present and uses the normal game path when it is not.
- **Built-In Localisation:** English plus 18 translated locales are bundled with the mod and follow ONI's selected language automatically.
  Meep's interpretive-dance translation programme has been retired.

---

## Mod Options

The in-game Mod Options dialog provides:

- **Temperature-Aware "Lacks Resources":** Choose whether the warning also considers your temperature limits.
  Disable it if you prefer the lightest possible late-game warning checks.
- **Construction Material Limits:** Choose whether temperature restrictions also apply to materials delivered to blueprints.
- **Temperature Previews:** Preview values in Celsius, Fahrenheit, or Kelvin with automatic conversion, and reset the mod's options to their defaults in one click.

---

## Support & Diagnostics

If something goes thermally sideways, open Mod Options and choose **Report a bug**.

Reports are generated locally and nothing is uploaded automatically.
The standard report does not read _Player.log_; the clearly labelled **Include game log (optional)** can include a bounded, best-effort-redacted log excerpt for harder failures.

Review the report on your computer, then attach it to the GitHub bug-report form.

Duplicants cannot currently be included in the diagnostic bundle, despite repeated requests from colony management.

---

## Credits

This mod continues the incredible work begun by the ONI modding community:

- **Original Concept & Code**: llunak - [_Delivery Temperature Limit_](https://steamcommunity.com/sharedfiles/filedetails/?id=2963257205)
- **Intermediate Maintenance**: \[sd] QooLiO - [_Delivery Temperature Limit \[Fixed\]_](https://steamcommunity.com/sharedfiles/filedetails/?id=3479021027)

---

Developed by [Maksym Shostak](https://orcid.org/0000-0001-8017-8797).
Free Dive into the source code on [GitHub](https://github.com/MaksymShostak/oxygen-not-included).

_Delivery Temperature Limit (Supercooled) is a community mod and is not affiliated with, sponsored by, or endorsed by Klei Entertainment._
<!-- oni-mod-pipeline:workshop-description:end -->

## Repository and contributing

See the [repository overview](../../README.md), [support and privacy details](../../SUPPORT.md), and [translation guide](../../docs/guides/translating-delivery-temperature-limit-supercooled.md).
