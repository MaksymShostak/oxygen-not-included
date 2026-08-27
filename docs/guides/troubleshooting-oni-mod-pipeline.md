# Troubleshooting ONI Mod Pipeline

Resolve ONI Mod Pipeline failures by following the stable diagnostic ID, exact evidence path, and bounded next action reported by the command. Preserve source, installations, candidates, and evidence until the failing object is understood.

[Workflow overview](oni-mod-development-workflow.md) · [Getting started](getting-started-with-oni-mod-pipeline.md) · [Developing ONI mods](developing-oni-mods.md) · [Preparing releases](preparing-oni-mod-releases.md) · [Profile reference](oni-mod-pipeline-profile-reference.md)

> [!TIP]
> Start with the diagnostic's **Evidence** field, not with the newest artifact directory. ONI Mod Pipeline prints exact paths because substituting a similarly named run can hide the real failure or invalidate provenance.

## Read the complete diagnostic

Human-readable diagnostics use this stable structure:

```text
ONIP#### [severity]: <summary>
Evidence: <specific value or path>
Next action: <bounded remedy>
```

Interpret all three lines:

1. The diagnostic ID identifies the failure category.
2. **Evidence** identifies the exact profile field, source path, environment path, build result, test result, candidate, installation, or lifecycle file that failed.
3. **Next action** describes a bounded recovery step that preserves unrelated data and evidence.

When another program needs diagnostics, use `--format json` on commands that support it. Structured output contains `diagnostics`, the command-specific `value`, and `exitCode`. Do not parse human prose.

## Use the diagnostic range

| Diagnostic range | Area | First response |
| --- | --- | --- |
| `ONIP1001`–`ONIP1008` | Profile schema, metadata, declared paths, package mappings, Workshop listing, preview, or missing inputs | Open the exact field or path from **Evidence**, correct the tracked declaration or source, and rerun `validate`. |
| `ONIP2001`–`ONIP2005` | SDK, game installation, managed assemblies, ONI user data, artifact root, or duplicate subscribed mod | Run `diagnose`; add one exact override only when discovery is unavailable or ambiguous; review duplicate enabled copies manually. |
| `ONIP3001`–`ONIP3005` | Locked restore, build, source mutation, required output, or automated tests | Inspect the printed build or test run, including captured output and TRX, then fix tracked source or dependencies and create a new run. |
| `ONIP4001`–`ONIP4003` | Unowned destination, installed-byte mismatch, or existing candidate installation receipt | Preserve unknown data, use the exact intended destination and input, and create a new candidate when a receipt already exists. |
| `ONIP5001`–`ONIP5008` | Dirty release input, candidate/content mismatch, acceptance binding, required acceptance, Uploader representation, readiness, run collision, or noninteractive acceptance | Inspect the candidate readiness report and named evidence. Do not mutate a failed candidate; prepare a new one after correcting the cause. |
| `ONIP9001`–`ONIP9002` | Unexpected internal failure or secondary cleanup failure | Preserve the complete diagnostic and artifact tree. Resolve the primary failure first and report the reproducible command, ID, and path. |

The ID is stable enough for support and automation. The summary and evidence provide the instance-specific detail.

## Interpret command exit codes

| Exit code | Meaning | Typical response |
| ---: | --- | --- |
| `0` | Success | Retain the printed exact result path and continue. Review any accompanying warning. |
| `2` | Invalid input | Correct the profile, metadata, path, option, or declared source identified by the diagnostic. |
| `3` | Environment unavailable | Resolve SDK, game, managed assemblies, user-data, or artifact discovery. |
| `4` | Build or test failure | Inspect the exact isolated run and correct source, dependency, or test behavior. |
| `5` | Installation failure | Preserve the destination, review ownership and installed hashes, and retry only when safe. |
| `6` | Release not ready | Complete missing acceptance, preserve a failed candidate, or diagnose verification failure as reported. |
| `10` | Internal failure | Preserve complete evidence and report the reproducible failure without manual cleanup first. |

A warning may accompany exit code `0`. In particular, `ONIP2005` is non-blocking because only a human can decide which subscribed or local copy ONI should enable; it still requires review before acceptance.

## Resolve setup and discovery failures

Run:

```text
oni-mod-pipeline diagnose --mod <mod-root>
```

### Profile is not found or is ambiguous

Confirm that:

- the filename is exactly `oni-mod-pipeline.toml`;
- `--mod` identifies the mod root, profile, or a descendant path;
- upward discovery remains inside the intended Git worktree; and
- no second profile exists between the starting path and intended root.

Pass the profile path explicitly when location, rather than content, is ambiguous.

### The .NET SDK is unavailable or unsupported

Run `dotnet --version` and compare it with the repository's `global.json`. The supported toolchain is a stable `10.0.4xx` SDK. ONI Mod Pipeline does not download or select an SDK automatically.

Install or select the intended SDK outside the pipeline, then rerun `diagnose`. Do not change `global.json` merely to match an unrelated machine installation.

### The game installation is missing or ambiguous

The selected ONI installation must contain the required managed assemblies. Supply the installation root—not the managed subdirectory—when overriding discovery:

```text
oni-mod-pipeline diagnose --mod <mod-root> --game-directory <oni-installation-root>
```

Use `ONI_GAME_DIRECTORY` only for a stable local environment preference. An explicit option remains higher precedence for one command.

### The ONI user-data directory is unavailable

The selected directory must already exist and contain `mods`. Supply the per-user ONI root, not `mods/Dev` or `mods/Local`:

```text
oni-mod-pipeline diagnose --mod <mod-root> --user-data-directory <oni-user-data-root>
```

ONI Mod Pipeline does not create a guessed user-data hierarchy because that could conceal a wrong account, platform, or document location.

### The artifact root is rejected

Use a dedicated absolute directory. Do not use a filesystem root, home directory, Documents root, ONI user-data root, Steam library root, existing file, or linked directory.

When no override is needed, remove it and use the default `<git-worktree>/artifacts` path.

### `diagnose` reports that the Uploader is absent

The `ONI Uploader present` field checks only for `OniUploader64.exe`, `OniUploader.exe`, or `OniUploader.app` directly beneath the selected ONI game root. Steam can install **Oxygen Not Included Uploader** as a separate application, in which case this field is `false` even though that tool is installed elsewhere in the Steam library.

Check Steam's installed tools when the Uploader is not colocated with ONI. Do not change the game-directory override to the Uploader's directory; `--game-directory` must continue to identify the ONI installation that contains the required managed assemblies.

## Resolve profile and validation failures

Run validation after each correction:

```text
oni-mod-pipeline validate --mod <mod-root>
```

### An unknown key is reported

Schema version 1 is closed. Compare the exact key path with [ONI Mod Pipeline profile reference](oni-mod-pipeline-profile-reference.md). Correct spelling and table placement; do not assume an unknown key is ignored.

### A declared path escapes the mod root

Replace absolute, parent-traversing, linked, or junction-based input with a regular contained path. Machine-specific game, user-data, and artifact paths belong to options or environment variables, not the tracked profile.

### Package destinations collide

Destinations are compared using portable `/` normalization, Unicode normalization, and case-insensitive keys. Rename mappings so every runtime path is unique on supported filesystems. Ensure `mod.yaml` and `mod_info.yaml` each appear once at the package root.

### Workshop text is rejected

Check the exact source file for:

- a UTF-8 BOM;
- CRLF or lone CR source line endings instead of LF;
- a missing or duplicate final newline;
- an unresolved change-note placeholder;
- malformed or improperly nested recognized BBCode;
- Markdown `[label](url)` syntax;
- a non-HTTP(S) BBCode URL; or
- generated UTF-8 size beyond the configured ceiling.

Correct the tracked source. Do not edit a generated candidate listing artifact.

### The preview is rejected

Use a `.png`, `.jpg`, `.jpeg`, or `.gif` file whose extension matches its detected PNG, JPEG, GIF87a, or GIF89a signature. Renaming an unrelated file extension does not convert its bytes.

## Resolve locked restore, build, and test failures

### Locked restore fails

Normal build, test, and release work must consume committed lock files without changing them. If a dependency change is intentional:

1. edit the exact project dependency declaration;
2. run a normal restore for that project or solution to regenerate its lock file;
3. review the project and lock-file diffs together;
4. commit both; and
5. rerun locked restore.

Do not delete a lock file, disable locked mode, or accept an incidental rewrite as a dependency update.

### Build fails or produces no primary output

Inspect the exact build run printed by `build`. Check captured MSBuild output, the declared `entry-point`, selected configuration, supplied managed-assembly property, `{build-output}` path, merge inputs, and primary output.

Fix tracked source or the profile and create a new run. Do not copy a manually built DLL into the artifact directory or edit `build-result.json`.

### The build mutated source

Inspect the exact tracked diff. Remove or redirect the build target that wrote into the source tree. ONI Mod Pipeline expects intermediate, output, merged DLL, and test artifacts only beneath the isolated run.

Do not commit generated source mutations merely to make the guard pass unless the generated file is intentionally becoming a reviewed source input.

### A declared test fails or has invalid evidence

Open the exact automated-test-results directory printed by `test`. Inspect the declared project's TRX and captured output. A required result must exist, parse, identify the declared test project, and pass.

Fix the behavior or test and create a new test run. Do not edit TRX, import a result from another run, or change `required` solely to hide a regression.

## Resolve installation failures

### The destination is unowned

An unowned directory is preserved because its contents may be hand-maintained or belong to another workflow. Inspect the exact path from **Evidence**. Move or resolve it manually if that is genuinely intended, then retry.

Never fabricate `.oni-mod-pipeline-owner.json`, delete unknown content automatically, or broaden a cleanup command to the entire `mods` tree.

### Installed bytes do not match

Stop using that installation for acceptance. Preserve the candidate, build result, destination, ownership marker, and diagnostic. Determine whether another process changed the directory or the wrong artifact path was supplied.

For development, create and install a new exact build result after resolving the cause. For a candidate with a receipt, preserve it and prepare a new candidate; do not reinstall or repair the accepted path in place.

### A candidate receipt already exists

Candidate installation is one-time by design. The existing receipt binds that candidate to one acceptance installation. Continue with acceptance if the installed bytes still verify, or prepare a new candidate for another attempt.

### A duplicate subscribed copy is reported

Diagnostic `ONIP2005` names matching static IDs beneath `mods/Steam`. In ONI's mod-management UI, ensure the intended Dev or Local candidate copy is the only enabled implementation for the test. Do not delete the Steam directory or expect ONI Mod Pipeline to unsubscribe it.

## Resolve acceptance and verification failures

> [!WARNING]
> Never repair a candidate by editing content, hashes, receipts, acceptance results, or invalidation state. Preserve the failed candidate and create a new one after correcting the tracked cause.

### Acceptance requires an interactive terminal

`record-acceptance` prompts for each immutable check and therefore refuses redirected or noninteractive input with `ONIP5008`. Run it in an interactive terminal and use `--tester <display-name>` only to provide the tester identity up front.

### Acceptance results already exist

The record is write-once even when an answer failed or was entered incorrectly. Preserve it. Correct the underlying source, test procedure, or human error in a new release attempt and prepare a new candidate.

### The live installation no longer matches

The recorder and verifier compare the receipt, ownership marker, exact live inventory, byte lengths, and SHA-256 values with candidate runtime content. Stop acceptance when they differ.

Do not remove extra files, copy missing files, or rewrite the marker to force equality. Preserve the evidence, diagnose what changed the installation, and prepare a new candidate when the current candidate can no longer support truthful acceptance.

### Verification reports `awaiting-acceptance`

Read the blocking conditions in `release-evidence/release-readiness-report.json`. The candidate lacks a valid installation receipt, acceptance result, or both. Complete only the lifecycle step that remains valid under the write-once rules.

### Verification reports `acceptance-failed`

At least one required check was truthfully recorded as failed. Preserve the candidate and result, fix the behavior or test setup in tracked source/process, and prepare a new candidate.

### Verification reports `verification-failed`

Inspect the readiness report and exact evidence path. Verification can fail for an unsafe layout, missing or unexpected file, content mismatch, provenance mismatch, failed required TRX, listing representation mismatch, receipt/marker mismatch, installed-byte mismatch, acceptance-plan mismatch, or invalid acceptance result.

When the report marks the candidate irreversibly invalidated, later runs cannot restore that run ID to readiness. Preserve it for diagnosis and prepare a new candidate after the cause is fixed.

### Generated Uploader text pastes incorrectly

Confirm that you opened the candidate's generated `workshop-listing/description.bbcode` in current Windows Notepad and copied directly from there. Do not use the tracked LF source or an editor that silently rewrites line endings.

If the generated representation itself is wrong, cancel the Uploader form without publishing, correct the tracked source or renderer defect, commit the correction, and prepare a new candidate.

## Find the exact artifact or evidence

| Successful command | Exact path it reports or creates |
| --- | --- |
| `build` | `build-result.json` in one isolated build run |
| `test` | One automated-test-results directory containing declared TRX evidence |
| `prepare-release` | Candidate directory, canonical content digest, and initial state |
| development `install` | Managed Dev/Local destination containing `.oni-mod-pipeline-owner.json` |
| candidate `install` | Managed destination plus candidate `release-evidence/installation-receipt.json` |
| `record-acceptance` | Candidate `release-evidence/acceptance-test-results.json` |
| `verify-release` | Regenerated `release-summary.md`, `uploader-checklist.md`, and `release-readiness-report.json` |

Never select an object by “latest,” a timestamp guess, a directory sort, or similarity of names. Carry the exact printed path forward.

## Report an internal failure responsibly

For `ONIP9001` or `ONIP9002`, retain:

- the exact command and working directory;
- the full diagnostic text or JSON;
- ONI Mod Pipeline version;
- operating system and architecture;
- selected .NET SDK;
- exact profile, artifact, candidate, or installation path named by **Evidence**; and
- the complete relevant artifact tree, excluding credentials or unrelated personal data.

Resolve any earlier primary diagnostic before acting on a secondary cleanup diagnostic. Do not delete broad directories to make a reproduction appear clean.
