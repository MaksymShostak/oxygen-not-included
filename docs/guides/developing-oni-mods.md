# Developing ONI mods

Use isolated development runs to validate, build, test, and install each source revision without mutating the mod tree or confusing a working build with an immutable release candidate.

[Workflow overview](oni-mod-development-workflow.md) · [Getting started](getting-started-with-oni-mod-pipeline.md) · [Preparing releases](preparing-oni-mod-releases.md) · [Profile reference](oni-mod-pipeline-profile-reference.md) · [Troubleshooting](troubleshooting-oni-mod-pipeline.md)

> [!IMPORTANT]
> Use explicit development build results for edit/build/test/install iteration. Do not prepare or reuse a release candidate as a convenient local build; candidate installation and acceptance evidence are intentionally write-once.

## Start from a valid environment

Run from the mod root, or add `--mod <mod-root>` to commands that accept it. For this repository:

```text
cd mods/delivery-temperature-limit-supercooled
oni-mod-pipeline diagnose
oni-mod-pipeline validate
```

Run `diagnose` again when an environmental input changes, including:

- the selected .NET SDK;
- the ONI installation or managed assemblies;
- the ONI per-user data directory;
- the worktree location; or
- the artifact-root override.

Run `validate` after changing any declared input, including:

- `oni-mod-pipeline.toml`;
- `mod.yaml` or `mod_info.yaml`;
- the build entry point or dependency lock files;
- package mappings;
- test-project declarations;
- acceptance-check declarations; or
- Workshop description, change notes, preview, tags, or DLC compatibility.

Both commands are read-only. A successful validation means the declaration is coherent; it does not prove that the mod builds, tests pass, or the game behavior is acceptable.

## Create an isolated development build

Run:

```text
oni-mod-pipeline build
```

`build` performs locked restore and creates a new run beneath:

```text
<artifacts>/builds/<static-id>/<run-id>/
```

The run contains isolated intermediate and output directories plus `build-result.json`. The result records the profile identity, source inputs, build arguments, primary output, merge inputs and output, hashes, and version information needed to verify a later development installation.

Retain the exact `build-result.json` path printed by the command. Do not select a result by sorting run directories.

Use the profile's declared MSBuild configuration by default. An explicit override is available for a deliberate development experiment:

```text
oni-mod-pipeline build --configuration Debug
```

Release preparation does not consume an arbitrary development build result; it creates and records its own locked Release build.

## Run every declared automated test

Run:

```text
oni-mod-pipeline test
```

`test` performs locked restore for the declared test projects, runs each project in an isolated run, and prints the exact automated-test-results directory. Each required project must produce one successful, parseable TRX file under that directory.

For Delivery Temperature Limit, the required test project covers the C# regression contract, including eligibility behavior, source non-mutation, public-surface parity, and deterministic merged output. The profile—not an ad hoc shell script—defines which projects are required.

Rerun `test` whenever mod behavior, test code, test data, project dependencies, or relevant game references change. A successful earlier TRX file is evidence for its original run only.

## Install the exact development build

Pass both the mod profile and the exact build result printed by `build`:

```text
oni-mod-pipeline install --mod . --build-result <exact-build-result.json> --target dev
```

The development installation form requires:

- `--mod <path>` to identify the current profile contract;
- `--build-result <path>` to identify one exact build run; and
- `--target dev` or `--target local` to identify the managed ONI installation root.

Prefer `--target dev` for normal source iteration. Reserve `--target local` for a deliberate Local-mod test or candidate acceptance workflow.

Before copying anything, installation:

1. reloads and validates the current profile;
2. verifies that the build result belongs to that profile and static ID;
3. re-hashes the recorded build inputs and outputs;
4. assembles only the declared runtime package;
5. stages the new managed directory beneath the selected target root;
6. verifies every staged runtime byte;
7. atomically replaces only a destination already owned by the same profile identity; and
8. verifies the live installed inventory again.

The installed directory contains `.oni-mod-pipeline-owner.json`. Keep that marker with the managed runtime files; it is what distinguishes a pipeline-owned destination from user-maintained data.

> [!CAUTION]
> An existing directory without a matching ownership marker is intentionally rejected and preserved. Review or move that directory yourself. Do not add a fabricated marker or ask the pipeline to adopt or erase unknown content.

## Repeat the development loop

After each relevant source change:

```text
oni-mod-pipeline validate
oni-mod-pipeline build
oni-mod-pipeline test
oni-mod-pipeline install --mod . --build-result <new-build-result.json> --target dev
```

The loop creates new build and test runs. It may replace the same owned Dev installation, but it never overwrites an earlier artifact run or infers which result is newest.

Use this decision table to avoid unnecessary work:

| Change | Minimum next command | Why |
| --- | --- | --- |
| Game path, user-data path, SDK, worktree, or artifact root | `diagnose` | Reconfirm environment discovery before generating artifacts. |
| Profile, metadata, listing input, package mapping, or declared path | `validate` | Recheck schema and semantic containment before building. |
| Mod source, project, dependency, or build declaration | `build` | Produce a new output bound to the changed inputs. |
| Behavior, test code, dependency, or game reference | `test` | Produce current automated evidence. |
| Need to exercise current bytes in ONI | development `install` | Install the exact verified result rather than copying output manually. |

## Understand what development commands never do

The development workflow does not:

- increment or rewrite `mod_info.yaml`;
- update `STEAM_DESCRIPTION.bbcode` or `STEAM_CHANGE_NOTES.bbcode`;
- write a DLL into the tracked mod root;
- select or overwrite a previous artifact run;
- infer the newest build result;
- copy undeclared build outputs;
- change Steam subscriptions or ONI's enabled-mod state;
- prepare release provenance or a Workshop candidate; or
- publish a Workshop update.

If an invoked build changes tracked source, the pipeline fails with a source-mutation diagnostic. Inspect the exact diff and fix the project or build target; do not accept generated source changes as a normal side effect.

## Recover from a failed development run

| Failure | Correct response |
| --- | --- |
| Locked restore fails | Review the project and lock file together. Regenerate the lock only for an intentional dependency change, then prove locked restore succeeds. |
| Build fails or the primary output is missing | Inspect the printed run path and captured MSBuild output, correct source or the profile, and create a new run. |
| A required test fails or lacks valid TRX | Inspect that exact results directory, fix the behavior or test, and run `test` again. |
| Recorded inputs or outputs no longer match | Build again from the current source; do not edit `build-result.json` or copy an older output into the run. |
| Destination is unowned | Preserve it. Move or resolve it manually, then retry only after the intended managed path is safe. |
| A duplicate subscribed copy is reported | Manually decide which copy ONI should enable. The pipeline does not change subscriptions or enabled-mod state. |

## Continue to release preparation

When the change is tested and the intended version, listing, preview, dependencies, and acceptance contract have all been reviewed, continue to [Preparing ONI mod releases](preparing-oni-mod-releases.md). Release preparation starts from committed source, not from a development installation or `build-result.json`.
