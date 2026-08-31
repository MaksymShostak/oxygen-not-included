# Support

Use this route for problems with Delivery Temperature Limit (Supercooled). For a general Oxygen Not Included problem that is not caused by this mod, use [Klei's Oxygen Not Included support resources](https://support.klei.com/hc/en-us/sections/360006123791-Oxygen-Not-Included).

## Fastest reporting path

1. In Oxygen Not Included, open **Mods → Delivery Temperature Limit → Options**.
2. Select **Create Support Report**.
3. Review the generated local JSON file. The mod also copies a compact diagnostic summary, opens the report folder, and opens the [Temperature Limit bug form](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml) with that summary prefilled where possible.
4. Describe what happened, attach the reviewed JSON report, and submit the issue.

The report remains on your computer until you choose to attach it. Nothing is uploaded automatically.

## Standard versus extended reports

**Create Support Report** is the default. It collects the structured mod, game, settings, compatibility, and diagnostic facts listed below. It does not locate or read `Player.log`.

**Create Extended Support Report** collects the same structured facts and also includes a bounded copy of the current `Player.log` for failures that need more context. It reads at most the most recent 6 MiB of raw log data, replaces known user-profile, ONI-data, and installation-root path prefixes on a best-effort basis, and keeps the complete JSON below 12 MiB. `Player.log` contains output from the game and other mods, so automatic redaction cannot guarantee removal of every sensitive value. Review the extended report before attaching it.

## What is collected

A standard report includes:

- a new random report ID and UTC generation time;
- the ONI build, branch, game and Unity versions, platform, process architecture, locale, and active DLC identifiers—or an explicit unavailable state when DLC discovery fails;
- this mod's static ID, title, package and assembly versions, and current Temperature Limit settings together with the selected temperature unit;
- the already-verified runtime patch plan and FastTrack compatibility state;
- sanitized active-mod titles, static IDs, declared versions, assembly names and versions, load order, and source kind, with no mod paths;
- bounded Temperature Limit diagnostic codes, severity, timestamps, repeat counts, sanitized messages, and sanitized exception details; and
- explicit information about facts that were included, unavailable, excluded, redacted, or truncated.

The report retains at most 512 active mods, 128 distinct diagnostics, and 2,048 characters per diagnostic message. Any omitted or shortened data is disclosed in the report.

## What is not collected

The standard report does not collect:

- absolute paths;
- user or account names;
- Steam user IDs;
- IP addresses or other network information;
- environment variables;
- save files or save metadata;
- screenshots or crash dumps;
- full game logs, including `Player.log`; or
- other mods' configuration contents.

The reporter does not contain an uploader or telemetry client. Its only network-adjacent action is asking the operating system to open the fixed GitHub bug-form URL in your browser.

## If the reporter cannot run

If the mod does not load far enough to show its options, open the [Temperature Limit bug form directly](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml). Describe the shortest reproduction and expected behavior. Attach the current `Player.log` if it is available; the form deliberately keeps attachments optional for startup failures.

Klei maintains current platform-specific instructions in [Logs and Useful Information for Bug Reports](https://support.klei.com/hc/en-us/articles/360029555392-Logs-and-Useful-Information-for-Bug-Reports).

## Before restarting after a crash

Copy the current `Player.log` to a safe temporary location before relaunching Oxygen Not Included. Klei advises collecting it before restart so it retains the most relevant failure details. Then review the copy and attach it to the Temperature Limit issue if the crash appears related to this mod.

## Public attachment privacy

GitHub issues and their attachments in this repository are public. Open every generated report, log, image, or archive and remove anything you do not want to publish before uploading it. Extended reports can contain arbitrary text written by ONI or other mods even after best-effort path redaction. Local report files are never submitted, synchronized, or deleted by the mod; you decide whether to attach or remove them.
