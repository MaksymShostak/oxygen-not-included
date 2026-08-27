# Getting started with ONI Mod Pipeline

Set up a repository checkout so ONI Mod Pipeline can discover one mod profile, resolve the local Oxygen Not Included environment, and validate every declared input without changing source or game data.

[Workflow overview](oni-mod-development-workflow.md) · [Developing ONI mods](developing-oni-mods.md) · [Preparing releases](preparing-oni-mod-releases.md) · [Profile reference](oni-mod-pipeline-profile-reference.md) · [Troubleshooting](troubleshooting-oni-mod-pipeline.md)

## Meet the prerequisites

Provide all of the following before running the tool:

- the stable .NET 10 SDK selected by the repository's `global.json`;
- an `oni-mod-pipeline` command built from the same repository revision as the mod, or the repository-local invocation described below;
- a local Oxygen Not Included installation whose managed-assembly directory contains `Assembly-CSharp.dll` and `0Harmony.dll`;
- an existing ONI per-user data directory containing a `mods` directory;
- Git when validating release provenance or preparing a release candidate; and
- current Windows Notepad plus the authenticated Oxygen Not Included Uploader when completing the final Workshop text-representation check and manual handoff.

ONI Mod Pipeline does not download the SDK, install or update ONI, create the ONI user-data hierarchy, install the Uploader, or authenticate a Steam account.

## Restore the locked tool dependencies

From the repository root, restore the committed dependency closure:

```text
dotnet restore tools/oni-mod-pipeline/OniModPipeline.slnx --locked-mode
```

Locked mode must complete without changing either the project files or `packages.lock.json` files. If restore reports a stale lock file, treat that as a dependency-review task rather than bypassing locked mode.

## Choose an invocation form

Documentation examples use the installed command because it keeps the workflow readable:

```text
oni-mod-pipeline --help
```

When the tool is not installed on `PATH`, invoke the same CLI directly from this checkout:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- --help
```

Replace `--help` with the intended ONI Mod Pipeline command and options. The first `--` belongs to `dotnet run`; arguments after it belong to `oni-mod-pipeline`.

> [!TIP]
> Use the short `oni-mod-pipeline` form for routine mod work. Use the repository-local form when developing ONI Mod Pipeline itself or when you must prove that the CLI and mod came from the same checkout revision.

## Select the mod explicitly or by location

The profile filename is always `oni-mod-pipeline.toml`. Identify it in either of two ways:

1. Run a command from the mod root or one of its descendants. Profile discovery walks upward and stops at the Git worktree boundary.
2. Pass `--mod <path>`, where `<path>` is the mod directory, the profile itself, or a descendant path from which exactly one profile can be discovered.

For Delivery Temperature Limit, either change into its mod root:

```text
cd mods/delivery-temperature-limit-supercooled
oni-mod-pipeline diagnose
```

Or stay at the repository root and pass the mod path:

```text
oni-mod-pipeline diagnose --mod mods/delivery-temperature-limit-supercooled
```

Discovery must resolve exactly one profile. Do not place a second profile between a descendant working directory and the intended mod root.

## Diagnose environment discovery

Run `diagnose` before the first build and whenever the SDK, game installation, ONI user-data location, worktree, or artifact override changes:

```text
oni-mod-pipeline diagnose --mod mods/delivery-temperature-limit-supercooled
```

`diagnose` is read-only. It reports:

- the resolved profile and mod root;
- the Git worktree and contributing repository scope;
- the selected .NET SDK, operating system, and architecture;
- the ONI installation and managed-assembly directory;
- the ONI per-user data directory and its `mods/Dev` and `mods/Local` roots;
- the generated artifact root;
- available game-build metadata; and
- whether `OniUploader64.exe`, `OniUploader.exe`, or `OniUploader.app` exists directly beneath the selected game installation root.

It does not restore, build, test, install, prepare a candidate, or create an artifact directory.

Steam may install **Oxygen Not Included Uploader** as a separate application rather than beneath the ONI game root. In that layout, `diagnose` reports `ONI Uploader present: false` even though the separate Steam tool is installed. Treat this field as a game-root file check, not as a system-wide Uploader inventory.

## Override only the path that discovery cannot resolve

The common environment options use one consistent precedence rule:

| Purpose | Highest-precedence command option | Environment variable | Automatic or default source |
| --- | --- | --- | --- |
| ONI installation root | `--game-directory <path>` | `ONI_GAME_DIRECTORY` | Supported local Steam installation discovery |
| ONI per-user data root | `--user-data-directory <path>` | `ONI_USER_DATA_DIRECTORY` | Supported platform-specific ONI data discovery |
| Generated artifact root | `--artifacts-directory <absolute-path>` | `ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY` | `<git-worktree>/artifacts` |

An explicit command option overrides its environment variable. An environment variable overrides automatic discovery. Prefer automatic discovery when it is unambiguous; otherwise supply one exact override rather than hard-coding machine paths into tracked files.

The artifact override must be a dedicated absolute directory. The pipeline rejects unsafe broad locations, including:

- filesystem roots;
- the user's home directory;
- the user's Documents directory;
- the ONI per-user data root;
- Steam library roots;
- an existing file; and
- linked, junction, or reparse-point directories.

## Validate the declared mod

After `diagnose` resolves the intended environment, run:

```text
oni-mod-pipeline validate --mod mods/delivery-temperature-limit-supercooled
```

`validate` is also read-only. It checks the strict profile schema, ONI metadata, contained source paths, package allowlist, build declaration, test declaration, acceptance declaration, Workshop listing inputs, preview format, BBCode structure, and resolved environment.

Use release validation only when all contributing paths should already be tracked, committed, and clean:

```text
oni-mod-pipeline validate --mod mods/delivery-temperature-limit-supercooled --for-release
```

Do not use `--for-release` as a routine substitute for understanding the working tree. Its purpose is to fail closed when a candidate would otherwise record dirty or uncommitted release inputs.

## Request structured output when automating

Commands that expose `--format` accept `human` or `json`:

```text
oni-mod-pipeline diagnose --mod mods/delivery-temperature-limit-supercooled --format json
```

JSON output provides structured diagnostics, a command-specific value, and the stable exit code. Do not scrape human output in another program. `record-acceptance` is intentionally interactive and does not support JSON input or output.

## Confirm the checkout is ready

Use this checklist after initial setup:

- [ ] Locked restore succeeds without changing dependency declarations or lock files.
- [ ] `oni-mod-pipeline --help` names the command and all eight supported subcommands.
- [ ] `diagnose` resolves the intended profile, SDK, game, user-data, and artifact paths.
- [ ] The managed-assembly directory contains the required ONI assemblies.
- [ ] The user-data directory contains the expected `mods` hierarchy.
- [ ] `validate` succeeds without creating source or installation changes.

## Continue with your task

- To edit, build, test, and install repeatedly, continue to [Developing ONI mods](developing-oni-mods.md).
- To define or review a mod profile, use [ONI Mod Pipeline profile reference](oni-mod-pipeline-profile-reference.md).
- To resolve a failed prerequisite or discovery result, use [Troubleshooting ONI Mod Pipeline](troubleshooting-oni-mod-pipeline.md).
