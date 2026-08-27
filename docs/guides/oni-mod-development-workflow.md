# ONI Mod Development Workflow

ONI Mod Pipeline is the supported local workflow for developing an Oxygen Not Included mod, preparing exact Workshop content, recording human acceptance, and proving that a release candidate is ready for manual upload. The user-facing command is `oni-mod-pipeline`.

The pipeline stops at the authenticated uploader boundary. It does not change Workshop subscriptions, open the ONI Uploader, select Workshop metadata, or publish a mod.

## Prerequisites

Install or provide all of the following before working on a mod:

- the .NET 10 SDK selected by the repository's `global.json`;
- an `oni-mod-pipeline` command built from the same clean repository revision as the mod;
- a local Oxygen Not Included installation containing `Assembly-CSharp.dll` and `0Harmony.dll` in its managed-assembly directory;
- an existing ONI per-user data directory containing the `mods` directory;
- Git when release provenance and release-candidate preparation are required; and
- current Windows Notepad plus the authenticated ONI Uploader for the final text-representation check and manual handoff.

Run a command from the mod root or one of its descendants, or identify the mod explicitly with `--mod <path>`. Profile discovery looks for `oni-mod-pipeline.toml` at the supplied path and then walks upward, stopping at the Git worktree boundary. Discovery must find exactly one profile.

The common commands accept these environment paths:

| Purpose | Highest-precedence command option | Environment variable | Automatic/default source |
| --- | --- | --- | --- |
| ONI installation root | `--game-directory <path>` | `ONI_GAME_DIRECTORY` | Supported local Steam installation discovery |
| ONI per-user data root | `--user-data-directory <path>` | `ONI_USER_DATA_DIRECTORY` | Supported platform-specific ONI data discovery |
| Generated artifact root | `--artifacts-directory <absolute-path>` | `ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY` | `<git-worktree>/artifacts` |

An explicit command option wins over its environment variable, and an environment variable wins over automatic discovery. The artifact override must be a dedicated absolute path. The pipeline rejects filesystem roots, the user's home or Documents directory, the ONI user-data root, Steam library roots, existing files, and linked or reparse-point directories.

Use `--format json` on commands that expose the option when a calling program needs structured output. Human output is the default. `record-acceptance` is deliberately interactive and does not offer JSON input or output.

## Onboard a Mod with a Schema-v1 Profile

Place `oni-mod-pipeline.toml` in the mod root. Schema v1 is strict: unknown keys, unsafe paths, duplicate portable destinations, invalid identifiers, and missing declared files are errors rather than ignored input.

This is a representative compiled-mod profile:

```toml
schema-version = 1

[mod]
mod-yaml = "mod.yaml"
mod-info-yaml = "mod_info.yaml"

[build]
entry-point = "Source/ExampleMod.csproj"
configuration = "Release"
game-managed-directory-property = "OniManagedAssemblyDirectory"
primary-output = "{build-output}/ExampleMod.dll"
merge-inputs = []

[[package-files]]
source = "mod.yaml"
destination = "mod.yaml"

[[package-files]]
source = "mod_info.yaml"
destination = "mod_info.yaml"

[[package-files]]
source = "{build-output}/ExampleMod.dll"
destination = "ExampleMod.dll"

[workshop-listing]
description = "STEAM_DESCRIPTION.bbcode"
change-notes = "STEAM_CHANGE_NOTES.bbcode"
preview = "Preview.png"
mod-types = ["new-features", "tweaks"]
dlc-compatibility = ["base-game", "spaced-out"]

[local-install]
directory-name = "ExampleMod"

[[test-projects]]
id = "example-mod-regressions"
path = "Tests/ExampleMod.Tests.csproj"
required = true

[[acceptance-checks]]
id = "example-mod-smoke-test"
title = "Example behavior works in a representative colony"
required = true
setup = "Load a known acceptance save and enable only the candidate copy of the mod."
action = "Exercise the changed behavior and its unchanged control case."
expected = "The changed behavior succeeds, the control remains unchanged, and the game log has no relevant exception."
```

The profile defines data, not executable hooks:

- `[mod]` points to the root `mod.yaml` and `mod_info.yaml` files.
- `[build]` names one MSBuild project or solution. The pipeline supplies the configured managed-assembly property and `OniMergedModOutputPath`, builds below an isolated artifact directory, and requires the primary output to originate beneath `{build-output}`.
- A content-only mod may omit `[build]`. Its package mappings must then use source files rather than `{build-output}`.
- Each `[[package-files]]` entry is an allowlisted runtime file. The package must include root destinations named `mod.yaml` and `mod_info.yaml`; undeclared build outputs are not copied.
- `[workshop-listing]` declares the tracked description, current change notes, preview, mod types, and DLC compatibility. The optional `description-byte-limit` and `change-notes-byte-limit` fields default to 8000 and may be set from 1 through 8000.
- `[local-install]` provides one portable managed directory name beneath ONI's `mods/Dev` or `mods/Local` directory.
- Each `[[test-projects]]` entry names an ordinary test project and whether it is required for release readiness.
- Each `[[acceptance-checks]]` entry becomes immutable candidate evidence. Use stable kebab-case IDs and concrete setup, action, and expected-result text. A releasable candidate needs at least one acceptance check; mark every behavior that must block release as required.

Schema-v1 Workshop identifiers are intentionally closed. Mod types are `language`, `worldgen`, `new-features`, `tweaks`, and `ui`. DLC compatibility values are `base-game`, `spaced-out`, `frosty-planet-pack`, `bionic-booster-pack`, `prehistoric-planet-pack`, and `aquatic-planet-pack`.

Keep all declared source paths beneath the mod root. Package destinations are `/`-normalized, compared with portable case-insensitive and Unicode-normalized rules, and must remain unique. Do not use links or junctions to bypass containment.

The metadata files remain authoritative. In particular, `mod.yaml` supplies the title and static ID, while `mod_info.yaml` supplies the release version, API version, supported content, and minimum supported build. A release version must contain two through four nonnegative numeric components accepted by .NET version parsing, with each component no greater than 65534.

## Local Development Iteration

Use an explicit mod path when running outside the mod tree:

```text
oni-mod-pipeline diagnose --mod <mod-root>
oni-mod-pipeline validate --mod <mod-root>
oni-mod-pipeline build --mod <mod-root>
oni-mod-pipeline test --mod <mod-root>
```

`diagnose` is read-only. It reports the resolved profile, game, managed assemblies, user-data paths, artifact root, SDK, operating system, and architecture. It does not restore, build, test, install, or prepare a candidate.

`validate` is also read-only. It checks the profile schema, metadata, contained paths, package allowlist, test declarations, Workshop inputs, and resolved environment. Add `--for-release` to require every contributing path to be tracked, committed, and clean.

`build` performs locked restore and an isolated build, then prints the exact generated `build-result.json` path. It never selects or overwrites an earlier run, installs the mod, increments a version, or rewrites tracked metadata.

`test` restores and runs every declared test project and prints the exact `automated-test-results` directory. Required test projects must produce successful, parseable TRX evidence.

To install one development build, copy the exact path printed by `build`:

```text
oni-mod-pipeline install --mod <mod-root> --build-result <build-result.json> --target dev
```

The development form requires both `--mod` and `--build-result`. The pipeline re-hashes the build result's recorded inputs and outputs before assembling the declared runtime package. It never installs an implicit “latest” build.

Installation is ownership guarded. A new destination is allowed; replacement is allowed only when its `.oni-mod-pipeline-owner.json` marker identifies the same managed directory and static ID. An existing unowned directory is preserved and rejected. Use development builds for repeated edit-build-install cycles because candidate installation receipts are intentionally write-once.

## Review Version and Workshop Source Text

Before release preparation, deliberately review and edit the tracked source of truth:

1. Set the intended release version in `mod_info.yaml`.
2. Confirm the title and static ID in `mod.yaml`, then confirm supported content, API version, and minimum supported build in `mod_info.yaml`.
3. Update `STEAM_DESCRIPTION.bbcode` only when the stable listing changes.
4. Replace `STEAM_CHANGE_NOTES.bbcode` with the reviewed notes for this update.
5. Review the preview image, mod types, and DLC compatibility declared by the profile.
6. Commit every contributing change before preparing a candidate.

Treat version and change-note changes as authored release decisions. ONI Mod Pipeline validates and records them but never increments, derives, or rewrites them. If review finds a mistake after preparation, correct the tracked source, commit it, and prepare a new candidate.

Keep tracked BBCode as BOM-free UTF-8 with LF line endings. Candidate preparation creates the separate Windows-uploader representation described below.

## Maintain Locked Dependencies and a Clean Release Scope

Normal verification and release work must resolve the committed dependency closure without changing it:

```text
dotnet restore tools/oni-mod-pipeline/OniModPipeline.slnx --locked-mode
```

Mod build and test projects also opt into lock files. `build`, `test`, and `prepare-release` use locked restore for the declared mod projects. A missing, stale, or incompatible lock file is a failure; do not bypass locked mode during release preparation.

When intentionally changing a dependency, edit the exact project dependency declaration, run a normal restore for that project or solution to regenerate its `packages.lock.json`, review the project and lock-file diffs together, and commit both. Then rerun locked restore to prove the reviewed closure is sufficient. An incidental lock-file rewrite is not a dependency update.

Before preparation, inspect repository state and run release validation:

```text
git status --short
oni-mod-pipeline validate --mod <mod-root> --for-release
```

Every contributing mod, test, profile, lock, Workshop, and in-worktree pipeline source path must be tracked, committed, and clean. Unrelated files outside the contributing set do not change candidate provenance, but they should still be understood rather than silently discarded. `prepare-release` repeats the relevant-source cleanliness check and fails closed on a dirty input.

## Prepare a Release Candidate

Run the release sequence from the mod root, or add `--mod <mod-root>` to the first four commands:

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

`prepare-release` performs the release cleanliness check, locked build, required automated tests, closed package assembly, Workshop-listing rendering, hashing, and provenance capture as one operation. It prints the exact candidate directory and creates it beneath:

```text
<artifacts>/release-candidates/<static-id>/<version>/<run-id>/
```

The candidate has three deliberately separate areas:

```text
<candidate>/
  workshop-content/
    <only the runtime files selected by package-files>
  workshop-listing/
    description.bbcode
    change-notes.bbcode
    preview.<ext>
  release-evidence/
    acceptance-test-plan.json
    automated-test-results/
    build-provenance.json
    release-content-manifest.json
    release-readiness-report.json
    release-summary.md
    uploader-checklist.md
```

Point the ONI Uploader's **Update Data** field only at `workshop-content`. Copy listing fields only from `workshop-listing`. Never upload `release-evidence`; it exists to prove what was built, tested, installed, and accepted.

The release-content manifest binds every runtime and Workshop-listing file by normalized relative path, byte length, and SHA-256, then derives one canonical content digest. Evidence is outside that content set so controlled lifecycle records can be added without changing the bytes intended for the Workshop.

Treat candidate content and foundational evidence as immutable. Do not edit a candidate to “repair” it. Candidate installation adds one receipt, acceptance adds one result, and verification atomically replaces only the derived readiness report, summary, and checklist after rechecking their inputs.

## Install and Perform Human Acceptance

Install the exact candidate printed by `prepare-release`:

```text
oni-mod-pipeline install --candidate <candidate-directory> --target local
```

Candidate installation re-verifies the manifest, copies exactly the runtime package, verifies the installed bytes, and writes `release-evidence/installation-receipt.json`. A candidate can receive only one installation receipt. Use a new candidate rather than reinstalling one for a second acceptance attempt.

Before starting ONI:

- ensure the candidate's managed directory is the only enabled copy of that static ID;
- disable any subscribed Workshop copy and any competing Dev/Local copy;
- keep `.oni-mod-pipeline-owner.json` in the installed directory; and
- treat diagnostic `ONIP2005` as a concrete duplicate-copy warning requiring manual enablement review. ONI Mod Pipeline does not change subscriptions or enabled-mod state.

Run every setup, action, and expected result from `release-evidence/acceptance-test-plan.json`. Exercise both the changed behavior and an unchanged control. Complete save/load cases when declared. After the session, inspect the current `Player.log` for the mod's initialization messages, patch failures, Unity lifecycle failures, and unhandled exceptions relevant to the candidate.

Record the results directly in an interactive terminal:

```text
oni-mod-pipeline record-acceptance --candidate <candidate-directory>
```

Use `--tester <display-name>` to supply the tester identity up front, or enter it when prompted. For every check, the recorder displays the immutable setup, action, and expected result, then asks for `passed` or `failed` plus an optional note.

The recorder verifies the candidate digest, acceptance-plan hash, installation receipt, ownership marker, exact installed inventory, and installed file hashes both before and after collecting answers. It writes `acceptance-test-results.json` with create-new semantics. Existing results are never overwritten.

This write-once rule applies even when a response failed. The recorder returns a release-not-ready result when any check is recorded as failed; final verification blocks failed required checks and reports failed optional checks as warnings. If a required scenario fails, an answer was entered incorrectly, or the installed bytes change, preserve the evidence, fix the tracked source or test setup, and prepare a new candidate. Do not edit or delete the recorded result to make the old candidate pass.

## Copy Workshop Text with the Exact CRLF Representation

Tracked description and change-note sources use UTF-8/LF for stable review. Candidate preparation renders separate `workshop-listing/description.bbcode` and `workshop-listing/change-notes.bbcode` files as BOM-free UTF-8 with CRLF line endings and exactly one final CRLF. Their logical and artifact SHA-256 values are recorded in provenance.

For the required representation check:

1. Open the candidate's `workshop-listing/description.bbcode` in the current Windows Notepad.
2. Open the authenticated ONI Uploader's Edit Mod form with every update checkbox disabled.
3. Copy all text directly from Notepad and paste it into **Description**.
4. Confirm that paragraphs, blank lines, `---`, headings, and `[list]` blocks remain on separate lines.
5. Record the Notepad and Uploader versions in the acceptance result note.
6. Cancel the form without publishing.

Use the same direct-copy rule for `change-notes.bbcode` during the final handoff. Do not copy the tracked LF source, pass candidate text through another formatter, or save edits into the candidate. If the representation is wrong, correct the tracked source and prepare a new candidate.

## Verify Deterministically and Recover from Tampering

After the write-once installation and acceptance evidence exists, run:

```text
oni-mod-pipeline verify-release --candidate <candidate-directory>
```

Verification rechecks the canonical candidate layout, exact inventories, content manifest and digest, build provenance, required TRX results, source-cleanliness evidence, uploader text bytes, preview image, installation receipt, live ownership marker, live installed runtime bytes, acceptance-plan hash, and recorded acceptance results. It derives one of four states:

| State | Meaning |
| --- | --- |
| `awaiting-acceptance` | The exact candidate lacks its installation receipt or acceptance result. |
| `acceptance-failed` | At least one required acceptance check failed. |
| `ready-for-upload` | All immutable content, evidence, live installed bytes, and required acceptance checks verify. |
| `verification-failed` | Verification found inconsistent, missing, unsafe, or tampered evidence/content. |

On every consistent run, verification deterministically regenerates `release-evidence/release-summary.md`, `release-evidence/uploader-checklist.md`, and `release-evidence/release-readiness-report.json`. A successful command exits only for `ready-for-upload`.

Content, manifest, representation, or evidence tampering fails closed. An irreversible verification failure is persisted in the candidate readiness report so later runs cannot rehabilitate the same run ID. Preserve the failed candidate for diagnosis, correct the tracked source or workflow defect, commit the correction, and run `prepare-release` to create a new candidate. Never patch hashes, delete the invalidation, reuse the run ID, or copy “good” evidence from another candidate.

## Follow the Generated ONI Uploader Checklist

The generated `release-evidence/uploader-checklist.md` is authoritative because it contains the candidate's absolute paths and content digest. For a `ready-for-upload` candidate, it uses this checklist structure:

```text
# ONI Uploader checklist

Candidate: `<candidate-directory>`
Content digest: `<content-digest>`
Current state: `ready-for-upload`

[ ] Candidate state is ready-for-upload.
[ ] Update Data points exactly to `<workshop-content-directory>`.
[ ] The displayed data path is not the mutable Dev/Local test directory.
[ ] Description comes from `<description.bbcode-path>`.
[ ] Paragraphs, blank lines, ---, headings, and [list] blocks remain separate after paste.
[ ] Change notes come from `<change-notes.bbcode-path>`.
[ ] Preview comes from `<preview-path>`.
[ ] Title, mod types, tags, and DLC compatibility match release-summary.md.
[ ] The final form has been reviewed immediately before Publish.

Publish is a deliberate authenticated human action. ONI Mod Pipeline does not perform or record it.
```

Use the paths from the generated file, not the placeholders above. Re-run `verify-release` immediately before handoff, confirm the state remains `ready-for-upload`, open `release-summary.md` and `uploader-checklist.md`, and check each item against the visible Uploader form. A rehearsal ends by cancelling the form. An actual release still requires the authenticated human to decide whether to select **Publish**.

## Troubleshoot by Diagnostic ID and Artifact Path

Human diagnostics have a stable ID followed by three fields:

```text
ONIP#### [severity]: <summary>
Evidence: <specific value or path>
Next action: <bounded remedy>
```

Read all three lines. The ID classifies the failure; the evidence path identifies the exact profile, source, build result, test result, candidate, install, or evidence file that failed. Do not substitute a newer run or a similarly named directory. Add `--format json` when another tool must parse `diagnostics`, `value`, and `exitCode` without scraping human text.

| Diagnostic range | Area | First response |
| --- | --- | --- |
| `ONIP1001`–`ONIP1008` | profile schema, metadata, paths, package mappings, listing inputs, or declared files | Open the exact profile/source path from **Evidence**, correct the tracked declaration or file, then rerun `validate`. |
| `ONIP2001`–`ONIP2005` | SDK, game installation, managed assemblies, user-data discovery, or duplicate subscribed mod | Run `diagnose`; supply one exact override when discovery is missing or ambiguous; manually disable duplicate enabled copies for `ONIP2005`. |
| `ONIP3001`–`ONIP3005` | locked restore, build, source mutation, missing output, or automated test failure | Inspect the printed build run or `automated-test-results` path, including its TRX and captured output; fix the tracked source/dependency issue and create a new run. |
| `ONIP4001`–`ONIP4003` | unowned destination, installed-byte mismatch, or existing candidate receipt | Preserve unowned data, use the exact printed target/build/candidate path, and create a new candidate when a receipt already exists. |
| `ONIP5001`–`ONIP5008` | dirty release input, candidate/content mismatch, acceptance binding, required acceptance, uploader representation, readiness, run collision, or noninteractive recording | Inspect `release-evidence/release-readiness-report.json` and the named evidence path. Do not mutate a failed candidate; correct tracked inputs and prepare a new one when instructed. Run acceptance recording in an interactive terminal for `ONIP5008`. |
| `ONIP9001`–`ONIP9002` | unexpected internal failure or secondary cleanup failure | Preserve the complete diagnostic and artifact tree. Resolve the primary diagnostic first; report the reproducible command, ID, and exact path before manually removing anything. |

Command exit codes are stable: `0` success, `2` invalid input, `3` unavailable environment, `4` build or test failure, `5` installation failure, `6` release not ready, and `10` internal failure. A warning can accompany a successful operation, but it still requires review when it identifies a duplicate installed mod or another acceptance risk.

The most useful success paths are printed rather than inferred:

- `build` prints the exact `build-result.json`;
- `test` prints the exact `automated-test-results` directory;
- `prepare-release` prints the exact candidate directory, digest, and state;
- `install` prints the exact destination and whether a candidate receipt was written; the ownership marker is `.oni-mod-pipeline-owner.json` in that destination, and a candidate receipt is `release-evidence/installation-receipt.json`;
- `record-acceptance` prints the exact `acceptance-test-results.json`; and
- `verify-release` writes the final readiness report, release summary, and uploader checklist beneath that candidate's `release-evidence` directory.

Never select an artifact by “latest,” timestamp guesswork, or directory sorting. Carry the exact path printed by one command into the next command.

Publish is a deliberate authenticated human action. ONI Mod Pipeline does not perform or record it.
