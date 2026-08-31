# Delivery Temperature Limit: Support Reporting and Community Design

- **Status:** Approved specification, configuration, public surface, and GitHub metadata; implementation pending formal review
- **Date:** 2026-08-31
- **Mod:** Delivery Temperature Limit (Supercooled)
- **Primary user:** An ONI player reporting a problem without manually locating versions, mods, settings, or logs
- **Secondary user:** A contributor preparing a focused issue or pull request
- **Privacy model:** Local generation, allowlisted collection, explicit extended-log action, user-reviewed upload, no telemetry
- **Development model:** Focused TDD followed by the repository-local ONI Mod Pipeline gates

## 1. Decision

Implement one low-friction support-reporting subsystem and a matching GitHub community-health surface.

The normal player path is an in-game action in the existing PLib options screen. It creates one self-contained JSON support report, copies a compact summary, opens the report directory, and opens a prefilled GitHub bug form. The player supplies only the observed behavior, the action or situation that triggered it, and the expected behavior; attaching the generated file and submitting the issue remain explicit user actions.

Two actions are exposed:

1. **Create Support Report** collects an allowlisted environment, configuration, loaded-mod, runtime-patch, compatibility, and mod-diagnostic snapshot. It does not read `Player.log`.
2. **Create Extended Support Report** collects the same snapshot and embeds a size-capped, path-redacted copy of the current `Player.log`. Its label and tooltip state that game logs can contain personal paths or unrelated mod output and should be reviewed before upload.

Nothing is uploaded automatically. The mod never requests a GitHub token, never calls a reporting backend, and never creates an issue on the player's behalf.

The repository gains a concise `CONTRIBUTING.md`, a player-focused `SUPPORT.md`, bug and feature issue forms, an issue-template configuration, and a pull-request template. These documents and forms describe the real automated path and are published together with it.

## 2. Evidence behind the design

GitHub treats contribution guidelines and support resources as separate discoverable community-health files, and its current issue-form schema supports structured fields, URL-prefilled field identifiers, and direct uploads of `.json`, `.log`, `.txt`, and `.zip` files. The form schema remains public preview, so the support documentation and bug form retain an ordinary attachment fallback.

The interaction follows established modding patterns: HugsLib gathers RimWorld runtime data in game, SMAPI turns Stardew logs into a structured shareable artifact, and Godot exposes product-generated system information. Current Klei guidance confirms that ONI's current `Player.log` is useful and must be preserved before a relaunch, while its platform-specific locations illustrate the work the mod should automate.

Relevant sources:

- [GitHub contribution guidelines](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/setting-guidelines-for-repository-contributors)
- [GitHub support resources](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/adding-support-resources-to-your-project)
- [GitHub form schema](https://docs.github.com/en/enterprise-cloud@latest/communities/using-templates-to-encourage-useful-issues-and-pull-requests/syntax-for-githubs-form-schema)
- [Klei ONI logs and bug-report information](https://support.klei.com/hc/en-us/articles/360052993772-Oxygen-Not-Included-DLCs-Logs-and-Useful-Information-for-Bug-Reports)
- [HugsLib](https://github.com/UnlimitedHugs/RimworldHugsLib)
- [SMAPI log facilities](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/web.md)

## 3. Goals

The implementation must:

1. Reduce a normal bug report to three short human answers, one attachment, review, and submit.
2. Collect machine-known facts automatically and mark unavailable facts honestly instead of failing the report.
3. Work from the Mods/options screen without requiring a colony to be loaded.
4. Initialize reporting before risky Harmony installation so a later initialization failure can still be described where ONI leaves the options entry available.
5. Preserve active loaded-mod order and publish no local installation paths.
6. Capture the installed runtime patch plan and FastTrack compatibility result from their existing semantic owners rather than re-inspecting Harmony or FastTrack during report generation.
7. Keep report generation off gameplay hot paths and run only after an explicit player action.
8. Bound diagnostic count, message length, URL length, optional log length, and total report size.
9. Show a visible success or failure result and leave a recoverable local file even if opening GitHub or the directory fails.
10. Preserve the existing production target, package versions, pipeline profile, release metadata, and merged dependency contract.
11. Make every published contributor instruction truthful and executable through the existing repository-local workflow.

## 4. Non-goals

This implementation will not:

- automatically submit a GitHub issue or upload any data;
- operate a log-hosting or telemetry service;
- request, store, or process GitHub credentials;
- include a save, screenshot, crash dump, colony name, account identifier, IP address, or arbitrary third-party configuration;
- copy the complete global log during ordinary standard reporting;
- promise that an arbitrary extended game log contains no sensitive text;
- ship an unsigned standalone executable or script in the first version;
- create a general-purpose diagnostics framework for unrelated mods;
- add a compression library or make the merged game assembly directly depend on `System.IO.Compression`;
- add a package, CI workflow, new test project, or pipeline-profile setting;
- change `mod_info.yaml`, `mod.yaml`, a package lockfile, or the current Workshop version/change notes;
- publish a Workshop update, create a commit, push, or create a pull request without separate authorization; or
- change GitHub Discussions, Wiki, Issues, visibility, default branch, fork relationship, or homepage without a separately verified reason.

## 5. Player flows

### 5.1 Standard report

1. The player opens the mod's PLib options and selects **Create Support Report**.
2. The reporter captures immutable snapshots and writes a unique `temperature-limit-support-<UTC timestamp>-<short report id>.json` beneath an application-data `DeliveryTemperatureLimit/support-reports` directory derived from Unity's persistent-data path.
3. The reporter copies a compact Markdown environment summary to the system clipboard.
4. The reporter best-effort opens the directory containing the report.
5. The reporter opens the GitHub `temperature-limit-bug.yml` form with its `diagnostics` field prefilled by the compact summary.
6. The player describes the failure, attaches the already-generated JSON file, reviews it, and submits.

### 5.2 Extended report

The extended action follows the same flow but also locates the current log through Unity's console-log path, with validated platform fallbacks only when necessary. It reads at most the most recent 6 MiB of raw log data, replaces known user-profile and ONI-data path prefixes with stable placeholders, and embeds the result with explicit truncation and redaction metadata.

The action's tooltip is the consent boundary. It explains that `Player.log` includes output from the game and other mods and that automatic redaction cannot guarantee removal of every sensitive value. The resulting file remains local for inspection before upload.

### 5.3 Partial failures

Report creation is successful when the JSON file is durably written. Clipboard, directory reveal, browser launch, and the final success notification are separate best-effort presentation steps. A failure in any presentation step is recorded without deleting the report, reclassifying generation as failed, or throwing through the PLib action.

If report creation itself fails, the player sees a concise in-game error and the full exception is emitted to `Player.log`. The support page provides the direct GitHub form and Klei's log-location fallback.

If the mod assembly never loads, no in-mod automation can run. The form therefore keeps its attachment optional and accepts a directly attached `.log` or `.txt` file. A standalone collector is considered only if real reports show this failure mode is common enough to justify signing, distribution, and cross-platform maintenance.

## 6. Architecture

### 6.1 Public facade

An internal `DeliveryTemperatureSupportReporter` is the single entry point. It owns early initialization, loaded-mod snapshot publication, runtime-patch snapshot publication, bounded diagnostic recording, and the two explicit report actions. Callers do not know file formats, paths, redaction rules, or GitHub query details.

`DeliveryTemperatureLimitOptions` gains exactly two public read-only `System.Action<object>` properties because PLib 4.24 maps that exact public option-property type to action buttons. The ignored argument is PLib presentation context, not persisted state:

- `CreateSupportReport`
- `CreateExtendedSupportReport`

They are not marked with `JsonProperty`, cannot be deserialized, and do not change persisted option keys. Their addition is an intentional public member-surface change and is added to the existing merged-assembly contract test. No new public type is introduced.

### 6.2 Pure support-reporting core

`Source/SupportReporting/Core` contains C# 8-compatible, Unity/Klei/PLib-free types for:

- immutable report inputs and output document;
- schema version and report-kind semantics;
- deterministic field normalization;
- path-prefix redaction;
- bounded diagnostic aggregation;
- bounded log selection and truncation metadata;
- compact Markdown summary rendering;
- issue-form URL construction and URL-length enforcement; and
- stable UTC/report-ID file naming.

The core accepts already-observed facts. It never reads the game, filesystem, clipboard, browser, or operating system directly.

### 6.3 ONI and operating-system adapter

`Source/SupportReporting/KleiIntegration` adapts the current game process to the pure core. It reads:

- `KleiVersion` build and branch;
- Unity/game version, platform, architecture, locale, persistent-data path, and console-log path where available;
- active DLC/content identifiers;
- mod static ID, package/assembly versions, title, active state, and loaded-mod order through supported `KMod` members;
- `DeliveryTemperatureLimitOptions.Instance`;
- the existing runtime patch plan and FastTrack compatibility snapshot; and
- the bounded mod diagnostic recorder.

It serializes the completed document with the game-provided Newtonsoft.Json reference, writes UTF-8 without a BOM to a unique temporary file in the destination directory, and atomically promotes that file to its final unique name. It never overwrites an existing report.

The adapter also owns clipboard, directory reveal, GitHub URL opening, and visible player notification. These operations sit behind narrow interfaces in tests so a failed presentation step can be exercised without launching external applications.

### 6.4 Runtime integration

`DeliveryTemperatureLimitMod.OnLoad` initializes the reporter before runtime patch installation. Both topology-independent and topology-dependent installation calls record a stable failure event before rethrowing an installation exception; runtime behavior remains fail-closed.

`OnAllModsLoaded` first publishes a sanitized active-mod snapshot, then performs the existing compatibility inspection and installation. On success, `DeliveryTemperatureRuntimePatchInstaller` publishes a read-only support snapshot from the already-verified plan and compatibility report. Reporting never performs a second Harmony authority scan.

Existing noteworthy `Debug.Log*` sites are routed through a small diagnostic method that preserves their current log output and additionally stores a bounded structured event. This first version records integration and compatibility milestones/failures, not every domain exception.

## 7. Report schema version 1

The JSON root contains:

- `schemaVersion`: integer `1`;
- `reportId`: random per-report ID with no cross-report installation identity;
- `generatedAtUtc`: ISO-8601 UTC timestamp;
- `reportKind`: `standard` or `extended-player-log`;
- `game`: build, branch, version, Unity version, platform, architecture, locale, and an availability-aware active-DLC snapshot containing the deterministically ordered IDs when collection succeeds;
- `temperatureLimit`: static ID, title, package version, assembly version, current settings, and the selected temperature unit used to interpret the displayed minimum and maximum values;
- `runtime`: reporter/installer state, selected ordered patch groups, optional path-free status-degradation diagnostic, and path-free FastTrack feature compatibility/identity evidence;
- `activeMods`: active mod title, static ID, declared version where available, assembly versions where safely available, load-order index, and source kind without any path;
- `diagnostics`: bounded stable code, severity, first/last UTC time, repeat count, sanitized message, and optional sanitized exception type/message;
- `playerLog`: absent for a standard report, otherwise source state, byte counts, truncation state, redaction placeholders, and bounded redacted content;
- `generation`: included/unavailable facts and nonfatal collection warnings; and
- `privacy`: explicit included, excluded, redacted, and potentially sensitive categories.

The report uses explicit `unavailable` states instead of empty strings or empty collections where absence has diagnostic meaning. A successfully captured empty DLC list means that no DLC is active; a null or failed DLC read remains unavailable and is never presented as `none`. Lists have deterministic order. It retains at most 128 distinct diagnostic entries, caps each stored diagnostic message at 2,048 characters, and retains at most 512 active-mod entries while recording any omitted count. A standard report targets well below 1 MiB. An extended report reads at most the most recent 6 MiB of raw `Player.log` data and must keep the final JSON below 12 MiB, safely below GitHub's current 25 MiB JSON upload limit; if redaction/JSON expansion would cross 12 MiB, the log content is shortened again and the additional truncation is disclosed.

The compact URL/clipboard summary contains only the report ID, report filename, ONI build/branch, Temperature Limit version, platform, DLC IDs or their explicit unavailable state, FastTrack state, and whether `Player.log` is included. It does not contain the active-mod list or raw diagnostics. The URL builder percent-encodes every value and keeps the complete issue URL at or below 1,800 characters, shortening only the human-readable diagnostic summary and recording that shortening in the generated report.

## 8. Privacy and security invariants

The implementation uses allowlisting, not broad machine inspection followed by hopeful filtering.

The standard report excludes:

- absolute paths;
- user and account names;
- Steam user IDs;
- IP/network information;
- environment variables;
- save files and save metadata;
- screenshots and crash dumps;
- full game logs; and
- other mods' configuration contents.

The extended report may contain arbitrary text originally written to `Player.log`. Known user-profile, persistent-data, and discovered installation-root prefixes are replaced before serialization, but the report declares that this is best effort. Redaction rules never rewrite the original log.

All generation is local. There is no HTTP client in the reporting subsystem. The only network-adjacent operation is opening a fixed HTTPS GitHub issue-form URL in the player's browser. URL construction permits only the fixed repository/template origin and encoded diagnostic-field data.

## 9. Community-health files

### 9.1 `CONTRIBUTING.md`

The root guide is contributor-facing and contains:

- project scope and conduct expectations in plain language;
- routing to `SUPPORT.md`, the bug form, and the feature form;
- the existing setup/development/release-guide links;
- exact `oni-mod-pipeline diagnose`, `validate`, `build`, `test`, and guarded install workflow;
- requirements for a linked issue, focused change, tests, manual ONI evidence where relevant, compatibility/performance/persistence/localization notes, and documentation updates; and
- the repository's configuration-approval, working-tree preservation, commit, and push boundaries stated for human contributors without copying agent-specific instructions.

### 9.2 `SUPPORT.md`

The root support guide contains:

- the two in-game reporting actions and their data differences;
- inspection, privacy, and upload instructions;
- a direct bug-form fallback;
- the warning to preserve `Player.log` before restarting;
- Klei's current platform-specific diagnostic link;
- separation between Temperature Limit problems and general ONI/Klei support; and
- a statement that issues are public and attachments should be reviewed.

### 9.3 `.github/ISSUE_TEMPLATE/temperature-limit-bug.yml`

The form has no forced title prefix and applies the existing `bug` label. Its fields are:

1. introductory Markdown linking `SUPPORT.md` and explaining the in-game action;
2. required `observed` textarea;
3. required `reproduction` textarea;
4. required `expected` textarea;
5. optional, URL-prefillable `diagnostics` textarea;
6. optional `files` upload accepting `.json,.log,.txt,.zip,.png,.jpg,.jpeg`; and
7. optional `context` textarea.

The attachment remains optional for pre-load failures. No duplicate-search attestation, version dropdown, manually entered mod list, severity, acceptance criteria, or implementation proposal is required.

### 9.4 `.github/ISSUE_TEMPLATE/temperature-limit-feature.yml`

The form applies the existing `enhancement` label and asks for:

1. required player problem/use case;
2. required desired experience;
3. optional concrete example;
4. optional current workaround; and
5. optional suggested behavior/additional context.

It does not request diagnostics by default.

### 9.5 `.github/ISSUE_TEMPLATE/config.yml`

The exact setting is:

```yaml
blank_issues_enabled: false
```

This routes external reporters through the two structured forms. GitHub still permits repository maintainers to open blank issues. No contact link is added because no separately maintained support destination has been verified.

### 9.6 `.github/pull_request_template.md`

The template asks for a linked issue, purpose, change summary, automated verification, manual ONI scenarios, compatibility/performance/persistence/localization/UI impact, screenshots where relevant, and known limitations. It does not require irrelevant sections to be fabricated; contributors mark them not applicable.

### 9.7 Existing public documentation

`README.md` gains a short Support and Contributing section linking the new files/forms. `STEAM_DESCRIPTION.bbcode` gains a concise support section telling players where the in-game actions are and linking the GitHub bug form. Release notes and version metadata remain unchanged until a separately authorized release.

## 10. GitHub repository metadata

The repository is currently a public fork with Issues enabled, Discussions disabled, Wiki enabled, an empty description, an empty homepage, and no topics. The minimal proposed public metadata change is:

- **Description:** `Optimized Oxygen Not Included mod for setting minimum and maximum temperatures on materials delivered to storage, buildings, and construction.`
- **Topics:** `c-sharp`, `dotnet`, `game-mod`, `harmony`, `oni-mod`, `oxygen-not-included`, `plib`, `steam-workshop`, `temperature-control`, `unity`
- **Homepage:** leave empty until the exact current Supercooled Workshop item URL is authoritatively verified; do not guess from a collection or predecessor item.
- **Repository features:** make no changes to Issues, Discussions, Wiki, visibility, default branch, or fork relationship.

The authenticated GitHub connector does not expose repository metadata mutation. After approval, use the GitHub CLI only for this exact `description` and topic update, then read the metadata back for verification. No other remote state changes are authorized by this design.

## 11. Configuration and policy approval dossier

Before implementation, the user must explicitly approve these exact changes because they affect build/test or repository policy:

| File or remote setting | Exact proposed change | Behavioral or pipeline impact | Defensive limit |
| --- | --- | --- | --- |
| `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj` | Add an explicit `UnityEngine.IMGUIModule` reference to the game-managed `UnityEngine.IMGUIModule.dll` with `<Private>false</Private>` immediately after the existing `UnityEngine.CoreModule` reference. | Compiles the clipboard presentation step against the game-provided `GUIUtility` assembly. | No copied game assembly, package, property, lockfile, or pipeline-profile change. |
| `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj` | Add exactly `<Compile Include="..\Source\SupportReporting\Core\**\*.cs" Link="Production\SupportReporting\Core\%(RecursiveDir)%(Filename)%(Extension)" />` to the existing first `ItemGroup`. | Compiles the same pure reporting core into the existing required test project. | No package/reference/property change; no second test project; no Unity/Klei adapter files linked. |
| `CONTRIBUTING.md` | Create the contributor guide described in section 9.1. | GitHub surfaces repository contribution rules to issue/PR authors. | Do not duplicate internal agent instructions or promise nonexistent automation/response times. |
| `SUPPORT.md` | Create the player support/privacy/fallback guide described in section 9.2. | GitHub surfaces a dedicated support route. | No external support channel is invented. |
| `.github/ISSUE_TEMPLATE/temperature-limit-bug.yml` | Create the exact seven-element bug form described in section 9.3 with existing label `bug`. | Standardizes high-signal bug reports and accepts generated JSON/log attachments. | Only three human narrative fields required; upload optional. |
| `.github/ISSUE_TEMPLATE/temperature-limit-feature.yml` | Create the five-field feature form described in section 9.4 with existing label `enhancement`. | Captures player needs without requiring maintainer design work. | No mandatory diagnostics or technical proposal. |
| `.github/ISSUE_TEMPLATE/config.yml` | Create with only `blank_issues_enabled: false`. | Routes external users through issue forms. | No contact links or other template settings. |
| `.github/pull_request_template.md` | Create the PR evidence checklist described in section 9.6. | Standardizes contributor verification and impact reporting. | No automatic gate or CI change. |
| GitHub repository description | Set exactly the description in section 10. | Populates the repository About/search summary. | No rename or other repository setting. |
| GitHub repository topics | Set exactly the ten topics in section 10. | Improves GitHub discovery and classification. | No homepage or feature-flag change. |

The implementation also updates the existing intentional public-surface test for the two PLib `Action<object>` properties, plus normal source/tests/README/Workshop-description text. Those are not configuration changes but remain limited to this design.

Apart from the exact non-copy-local `UnityEngine.IMGUIModule` reference above, no further change is approved or planned for `Source/DeliveryTemperatureLimit.csproj`, either lockfile, `global.json`, `oni-mod-pipeline.toml`, `mod.yaml`, `mod_info.yaml`, CI, deployment, or release configuration.

## 12. TDD and verification

Focused tests are written first for:

1. standard versus extended inclusion rules;
2. deterministic schema/version/report-kind values;
3. unavailable-value semantics;
4. active-mod load-order preservation and path exclusion;
5. bounded diagnostic aggregation and repeat counts;
6. profile/persistent/install path redaction;
7. log byte ceiling, recent-tail selection, UTF-8 handling, and truncation disclosure;
8. compact Markdown rendering;
9. issue URL origin, template, field ID, encoding, and length ceiling;
10. unique deterministic-format file names;
11. nonfatal collection/presentation failures;
12. intentional options public surface and non-serialized action properties; and
13. absence of HTTP/network clients and prohibited default data categories from the support-reporting source boundary.

Verification then runs:

```text
oni-mod-pipeline validate
oni-mod-pipeline build
oni-mod-pipeline test
```

The exact new build result is inspected. A local Dev install and manual ONI check verify that both action buttons render, the standard and extended files are created, the log warning is clear, clipboard/folder/browser behavior is usable, the prefilled issue form is correct, and no issue is submitted during testing.

The manual test also checks a missing/unreadable log, browser launch failure where practicable, a non-ASCII profile path fixture in automated tests, FastTrack absent/present snapshots, and a larger active-mod list.

## 13. Acceptance criteria

The work is complete when:

1. A normal player never manually finds or types the ONI build, mod version, DLCs, settings, active mod list, patch plan, or compatibility state.
2. The standard action creates one inspectable JSON report without reading `Player.log`.
3. The extended action creates one inspectable JSON report with a bounded, disclosed, best-effort-redacted current log.
4. Neither action uploads, authenticates, or submits anything.
5. The GitHub form requires only observed behavior, reproduction context, and expected behavior.
6. The report form accepts the generated JSON and direct fallback logs but does not require an attachment.
7. No absolute fixture path, user name, account ID, save, screenshot, crash dump, or third-party config is included by the standard collector.
8. Runtime patch/compatibility facts come from the existing verified plan rather than a second inspection path.
9. Reporting failures cannot crash or alter gameplay behavior.
10. The two action properties are non-persisted and the only new public members; no public type is added.
11. No production/test package, production project property, lockfile, pipeline profile, mod metadata, or CI file changes; the sole production-project change is an explicit non-copy-local reference to the game-provided `UnityEngine.IMGUIModule.dll` required for clipboard access.
12. The community files accurately route players and contributors and use the existing verified `bug` and `enhancement` labels.
13. The GitHub description and topics exactly match section 10 and all other metadata remains unchanged.
14. Focused tests and the repository-local validate/build/test gates pass with fresh evidence.
15. Manual testing confirms the complete player flow without submitting a real issue or publishing a Workshop update.

## 14. Rejected alternatives

### 14.1 One large `CONTRIBUTING.md`

Rejected because it conflates player support, issue reporting, contributor workflow, and maintainer design work. GitHub provides separate surfaced files and structured forms for these responsibilities.

### 14.2 A ZIP containing several report files

Rejected for the first version because one JSON attachment is equally automatable and machine-readable, while ZIP creation would add a direct compression dependency that conflicts with the existing merged-assembly contract.

### 14.3 Automatic upload or issue submission

Rejected because it requires credentials or a hosted service, removes the user's final review boundary, and creates abuse, retention, and privacy obligations disproportionate to this mod.

### 14.4 Full `Player.log` in every report

Rejected because standard environment and mod-owned facts should be private-by-default and small. The full game log is optional and explicitly requested through the extended action.

### 14.5 Standalone collector in the first release

Rejected because signing, antivirus trust, platform discovery, distribution, and support costs are not justified without evidence that pre-load failures dominate reports.

### 14.6 Manually entered environment fields

Rejected because the process already knows these facts, manual fields become stale or inconsistent, and unnecessary reporting labor discourages useful issues.

## 15. Final decision

Build a local-only, allowlisted, one-file support reporter behind two explicit PLib action buttons; publish a minimal player issue form and separate contributor/support guidance; dogfood the same contracts; and set only the exact GitHub description and topics stated above. Preserve the existing runtime, packaging, pipeline, release, and repository-feature configuration except for the approved non-copy-local clipboard assembly reference. No open product-design decision remains; the approved implementation remains subject to the repository's formal review gate before completion or remote metadata mutation.
