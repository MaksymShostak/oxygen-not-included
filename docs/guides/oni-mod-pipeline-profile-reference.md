# ONI Mod Pipeline profile reference

Use `oni-mod-pipeline.toml` to declare one mod's metadata sources, build contract, closed runtime package, Workshop listing, managed installation name, automated tests, and human acceptance contract. Schema version 1 is strict and data-only.

[Workflow overview](oni-mod-development-workflow.md) · [Getting started](getting-started-with-oni-mod-pipeline.md) · [Developing ONI mods](developing-oni-mods.md) · [Preparing releases](preparing-oni-mod-releases.md) · [Troubleshooting](troubleshooting-oni-mod-pipeline.md)

> [!NOTE]
> Unknown keys are errors. ONI Mod Pipeline does not support executable profile hooks, implicit package discovery, machine-specific fallback properties, or arbitrary Uploader identifiers.

## Locate and discover a profile

The profile filename is exactly `oni-mod-pipeline.toml`. Its containing directory is the mod root, and every declared source path is resolved relative to that directory.

Commands discover a profile from:

- the current directory when `--mod` is omitted; or
- the mod directory, profile file, or descendant path supplied to `--mod`.

Discovery walks upward and stops at the Git worktree boundary. It must identify exactly one profile.

## Understand the schema structure

| Object | Cardinality | Purpose |
| --- | ---: | --- |
| `schema-version` | Exactly one | Selects the strict profile schema. Version `1` is supported. |
| `[mod]` | Exactly one | Identifies the authoritative ONI metadata files. |
| `[build]` | Zero or one | Declares one MSBuild entry point and its primary output for a compiled mod. Omit it for a content-only mod. |
| `[[package-files]]` | One or more | Defines the complete allowlist of runtime content and portable destinations. |
| `[workshop-listing]` | Exactly one | Declares listing text, preview, mod types, DLC compatibility, and optional byte ceilings. |
| `[local-install]` | Exactly one | Declares one portable managed directory name beneath `mods/Dev` or `mods/Local`. |
| `[[test-projects]]` | Zero or more | Declares automated test projects and whether each result blocks readiness. |
| `[[acceptance-checks]]` | Zero or more syntactically; one or more for a releasable candidate | Declares stable human checks copied into immutable candidate evidence. |

<details>

<summary>Complete compiled-mod profile example</summary>

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
merge-inputs = ["PLib"]

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
title = "Representative colony behavior remains correct"
required = true
setup = "Load the designated acceptance save and enable only the candidate copy."
action = "Exercise the changed behavior and one unchanged control case."
expected = "The changed behavior succeeds, the control remains unchanged, and Player.log contains no relevant exception."
```

</details>

The repository's maintained real profile is [`mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`](../../mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml).

## Root key reference

| Key | Type | Required | Semantics |
| --- | --- | --- | --- |
| `schema-version` | 32-bit integer | Yes | Must equal `1`. Any other value fails as unsupported. |
| `mod` | Table | Yes | Contains only `mod-yaml` and `mod-info-yaml`. |
| `build` | Table | No | When present, every build key except `merge-inputs` is required. |
| `package-files` | Array of tables | Effectively yes | Validation requires at least one mapping and root destinations for both metadata files. |
| `workshop-listing` | Table | Yes | Every listing key except the two byte limits is required. |
| `local-install` | Table | Yes | Contains exactly one `directory-name`. |
| `test-projects` | Array of tables | No | Every item requires `id`, `path`, and `required`. |
| `acceptance-checks` | Array of tables | No | Every item requires `id`, `title`, and `required`; setup/action/expected are optional strings but should be concrete for useful evidence. |

Root keys and nested keys are ordinal and exact. For example, `schemaVersion`, `package_files`, and `changeNotes` are not aliases.

## Metadata source reference

The `[mod]` table contains:

| Key | Type | Meaning |
| --- | --- | --- |
| `mod-yaml` | Relative file path | Authoritative `mod.yaml` containing the title, description, and static ID. |
| `mod-info-yaml` | Relative file path | Authoritative `mod_info.yaml` containing supported content, minimum supported build, API version, and release version. |

Both files must exist as regular, non-linked files beneath the mod root.

The parsed metadata must satisfy these rules:

| Field | Rule |
| --- | --- |
| `mod.yaml` `title` | Nonempty string |
| `mod.yaml` `description` | Nonempty string |
| `mod.yaml` `staticID` | Matches `^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$` |
| `mod_info.yaml` `supportedContent` | Nonempty string |
| `mod_info.yaml` `minimumSupportedBuild` | Positive integer |
| `mod_info.yaml` `APIVersion` | Positive integer |
| `mod_info.yaml` `version` | Two through four nonnegative numeric components, each no greater than `65534`, accepted by .NET version parsing |

The pipeline never rewrites these files or increments the version.

## Build reference

Omit `[build]` for a content-only mod. For a compiled mod, the table contains:

| Key | Type | Required | Meaning |
| --- | --- | --- | --- |
| `entry-point` | Relative file path | Yes | One existing `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` beneath the mod root. |
| `configuration` | String | Yes | Default nonempty MSBuild configuration, normally `Release`. |
| `game-managed-directory-property` | String | Yes | MSBuild property that receives the resolved ONI managed-assembly directory. It must match `^[A-Za-z_][A-Za-z0-9_]*$`. |
| `primary-output` | Portable build-output path | Yes | The primary assembly path, beginning exactly with `{build-output}/`. |
| `merge-inputs` | Array of strings | No | Unique assembly simple names to merge, without a `.dll` suffix. Defaults to an empty array. |

Build output is isolated beneath the run artifact directory. The pipeline also supplies `OniMergedModOutputPath` to the build. The declared primary output must originate beneath `{build-output}` and must be selected by exactly one package mapping.

Each merge input must match `^[A-Za-z_][A-Za-z0-9_.-]*$`. For example, use `"PLib"`, not `"PLib.dll"`.

## Runtime package reference

Every `[[package-files]]` item contains:

| Key | Type | Meaning |
| --- | --- | --- |
| `source` | Relative source path or `{build-output}/…` path | Selects one tracked file, one nonempty tracked directory tree, or one recorded build output. |
| `destination` | Portable relative path | Selects the path beneath candidate `workshop-content`. |

Package mappings form a closed allowlist. Build products, symbols, configuration files, test results, preview images, and other neighboring files are not copied unless a mapping explicitly selects them.

Source rules:

- ordinary sources must remain beneath the mod root;
- a source directory expands recursively into its destination root;
- source directories must be nonempty and contain no links or reparse points;
- `{build-output}` may be used only when `[build]` exists;
- a `{build-output}` source must name a file recorded in the exact `build-result.json`; and
- the primary build output must be selected exactly once.

Destination rules:

- use `/` as the portable separator;
- do not use an absolute path, drive prefix, empty segment, `.`, or `..`;
- destinations must be unique under Unicode normalization and portable case-insensitive comparison;
- one mapping must place `mod.yaml` at the package root; and
- one mapping must place `mod_info.yaml` at the package root.

Runtime content may use subdirectories, but only the two metadata files are required at the package root.

## Workshop listing reference

The `[workshop-listing]` table contains:

| Key | Type | Required | Meaning |
| --- | --- | --- | --- |
| `description` | Relative file path | Yes | Reviewed tracked Workshop description source. |
| `change-notes` | Relative file path | Yes | Reviewed tracked change-note source for the current release. |
| `preview` | Relative file path | Yes | PNG, JPEG, GIF87a, or GIF89a preview with a matching extension and file signature. |
| `mod-types` | Array of closed identifiers | Yes | One or more unique identifiers mapped to current Uploader labels. |
| `dlc-compatibility` | Array of closed identifiers | Yes | One or more unique identifiers mapped to current Uploader DLC labels. |
| `description-byte-limit` | Integer from `1` through `8000` | No | Maximum generated UTF-8 description size; defaults to `8000`. |
| `change-notes-byte-limit` | Integer from `1` through `8000` | No | Maximum generated UTF-8 change-note size; defaults to `8000`. |

Supported preview extensions are `.png`, `.jpg`, `.jpeg`, and `.gif`. Candidate preparation normalizes JPEG output to `.jpg`; the extension must agree with the detected signature.

### Mod type identifiers

| Profile identifier | Uploader label |
| --- | --- |
| `language` | `language` |
| `worldgen` | `worldgen` |
| `new-features` | `new features` |
| `tweaks` | `tweaks` |
| `ui` | `ui` |

### DLC compatibility identifiers

| Profile identifier | Uploader label |
| --- | --- |
| `base-game` | `Base Game` |
| `spaced-out` | `Spaced Out!` |
| `frosty-planet-pack` | `The Frosty Planet Pack` |
| `bionic-booster-pack` | `The Bionic Booster Pack` |
| `prehistoric-planet-pack` | `The Prehistoric Planet Pack` |
| `aquatic-planet-pack` | `The Aquatic Planet Pack` |

Unknown or duplicate identifiers fail validation instead of being rendered verbatim.

### Listing text requirements

Tracked description and change-note files must be:

- regular, non-linked files beneath the mod root;
- BOM-free UTF-8;
- LF-only with exactly one final LF;
- nonempty after trimming;
- within the configured generated-byte ceiling;
- free of unresolved placeholders such as `TODO`, `TBD`, `CHANGEME`, and `ONI_MOD_PIPELINE_CHANGE_NOTES_REQUIRED` in change notes; and
- valid under the conservative BBCode rules.

Recognized nested BBCode tags are `b`, `i`, `u`, `strike`, `spoiler`, `h1`, `h2`, `h3`, `url`, `list`, and `quote`. `[*]` is a list-item token, and literal `---` is allowed. URLs use `http` or `https`. Markdown link syntax such as `[label](https://example.com)` is rejected in listing source.

Candidate preparation renders a separate BOM-free UTF-8/CRLF artifact with exactly one final CRLF. It records both the logical source representation and generated artifact representation in build provenance.

## Managed installation reference

The `[local-install]` table contains one key:

| Key | Type | Meaning |
| --- | --- | --- |
| `directory-name` | Portable directory name | Final managed directory beneath the selected `mods/Dev` or `mods/Local` root. |

The value must be one nonempty name. It cannot be `.`, `..`, contain `/` or `\`, include control characters, or use Windows-reserved characters `< > : " | ? *`.

Use a stable directory name derived from the mod identity, not a version, environment, or timestamp. For example, `DeliveryTemperatureLimit` remains valid across development and release runs.

## Automated test reference

Each `[[test-projects]]` item contains:

| Key | Type | Meaning |
| --- | --- | --- |
| `id` | Lowercase kebab-case identifier | Stable evidence identity used in artifacts and readiness reports. |
| `path` | Relative existing file path | Test project beneath the mod root. |
| `required` | Boolean | Whether a missing or failed result blocks release readiness. |

Test IDs must match `^[a-z0-9]+(?:-[a-z0-9]+)*$` and remain unique across both automated tests and acceptance checks.

The test command runs each declared project through Microsoft.Testing.Platform, requires one parseable TRX result per project, and records the result under the declared ID. Choose IDs for the stable behavior contract, not for a temporary bug number or implementation class.

## Human acceptance reference

Each `[[acceptance-checks]]` item contains:

| Key | Type | Required | Meaning |
| --- | --- | --- | --- |
| `id` | Lowercase kebab-case identifier | Yes | Stable evidence identity, unique across tests and acceptance checks. |
| `title` | Nonempty string | Yes | Observable result stated clearly for a human tester. |
| `required` | Boolean | Yes | Whether a failed check blocks `ready-for-upload`. |
| `setup` | String | No syntactically | Preconditions and test data needed to make the observation meaningful. |
| `action` | String | No syntactically | Exact behavior the tester performs. |
| `expected` | String | No syntactically | Observable result that distinguishes pass from fail. |

Although setup, action, and expected are syntactically optional in schema v1, use concrete nonempty text for every releasable check. Candidate preparation copies these fields in declared order into immutable `acceptance-test-plan.json`; vague or empty instructions produce weak evidence even when the profile parses.

Use stable semantic IDs such as `storage-bin-temperature-filter`, not implementation names such as `patch-method-2` or release-specific names such as `fix-2026-08-26`.

Required checks should cover every human-observable behavior that must block publication, including representative control cases, save/load behavior, relevant log review, and the Uploader text-representation seam when applicable.

## Apply portable path semantics

All declared source paths must stay beneath the mod root after normalization. ONI Mod Pipeline rejects:

- absolute paths where a relative path is required;
- `.` or `..` traversal segments;
- linked files, symlinks, junctions, or reparse points used to escape containment;
- duplicate portable destinations that differ only by case or Unicode representation; and
- a profile or declared input that cannot be resolved as an existing regular object of the required kind.

Keep machine-specific paths out of the profile. Game, user-data, and artifact roots belong to command options, environment variables, or automatic discovery described in [Getting started with ONI Mod Pipeline](getting-started-with-oni-mod-pipeline.md).

## Validate profile changes

After editing the profile, run:

```text
oni-mod-pipeline validate --mod <mod-root>
```

Before release preparation, run:

```text
oni-mod-pipeline validate --mod <mod-root> --for-release
```

The first command validates schema, semantics, files, and environment. The second additionally requires every contributing path to be tracked, committed, and clean.
