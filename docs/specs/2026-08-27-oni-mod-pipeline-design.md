# ONI Mod Pipeline: Architecture and Release-Candidate Design

- **Status:** Proposed for written-specification review
- **Date:** 2026-08-27
- **Product name:** ONI Mod Pipeline
- **Command name:** `oni-mod-pipeline`
- **Implementation runtime:** .NET 10 LTS
- **Initial repository:** `MaksymShostak/oxygen-not-included`
- **Initial mod:** Delivery Temperature Limit (Supercooled)

## 1. Executive summary

ONI Mod Pipeline is a local-first, environment-independent command-line tool for developing and preparing Oxygen Not Included mods. It replaces the repository's shell-specific build, test, metadata-mutation, and local-deployment scripts with one .NET 10 workflow. Its generic layer supports any conventionally packaged ONI mod through a versioned, declarative manifest. A mod-specific profile supplies file locations, package contents, regression-test projects, and human acceptance cases without embedding this mod's assumptions into the generic implementation.

The pipeline deliberately stops before authenticated Steam publication. Its terminal product is a fully built, tested, validated, hashed, and upload-ready release candidate containing:

- the exact directory to select as **Update Data** in Klei's ONI Uploader;
- the exact Workshop description, change notes, and preview image to enter separately;
- deterministic manifests and provenance describing every uploadable byte;
- automated test results;
- digest-bound in-game acceptance results; and
- a concise Uploader checklist.

The authenticated ONI Uploader remains a human approval gate. The tool will not store Steam credentials, automate authentication, call `SubmitItemUpdate`, click **Publish**, or claim that a release was published.

The design also incorporates a reproduced Windows interoperability requirement. LF-only description text copied into the ONI Uploader loses intentional line breaks. The same logical text represented as CRLF plain Unicode preserves paragraphs, blank lines, separators, headings, and list blocks. Therefore, tracked Workshop text remains UTF-8/LF, while generated `workshop-listing/*.bbcode` handoff artifacts are UTF-8 without BOM and CRLF-only.

## 2. Context and current-state problems

The repository currently has a working mod and several separate workflow fragments:

- `Source/build.sh` builds, deletes DLLs, parses the project XML with text tools, and mutates `mod_info.yaml`.
- `scripts/deploy_mod_locally.bat` discovers the Windows Documents directory by invoking PowerShell and then copies a fixed list of files.
- `Source/ILRepack.targets` merges PLib and runs a post-build PowerShell command that rewrites the tracked `mod_info.yaml`.
- `Tests/ModInfoVersion.Tests.ps1` regression-tests the post-build mutation and its line endings.
- `Tests/BuildingsEligibility.Tests.ps1` builds a source-and-stubs test harness dynamically with PowerShell.
- `Source/Directory.Build.props` embeds one conventional Windows Steam installation path.
- `Source/DeliveryTemperatureLimit.csproj` already has an SDK-style root, but it still carries manually replicated legacy-era configuration: a project-local output path, output-path suppression flags, Debug/Release groups that mix SDK defaults with PDB suppression, explicit base-framework references, a release version stored as `AssemblyVersion`, and the misleading `GameFolder` property name.
- `DeliveryTemperatureLimit.dll` is written into the mod source directory rather than an isolated artifact directory.
- The Uploader points at a mutable local-mod directory, so it is easy to upload files that were not part of the tested release.
- Workshop listing text, runtime content, test evidence, and publication instructions are not assembled into a single, digest-bound release candidate.

The immediate newline defect demonstrated another class of release risk: a source file may be logically correct but represented incompatibly at a manual tool boundary. The pipeline must therefore validate both logical content and consumer-facing byte representation.

These fragments have served their immediate purpose, but they cannot provide one reproducible, cross-platform lifecycle or one trustworthy answer to the question, “Are these the exact bytes that were tested and are now ready for the Uploader?”

## 3. Research basis

The design distinguishes authoritative platform contracts from community-established ONI practices.

### 3.1 Authoritative platform contracts

- Steam Workshop updates separately set title, description, tags, content directory, preview image, and change note before the final `SubmitItemUpdate` call. Steam explicitly notes that an item update cannot be cancelled once submitted. This supports a prepared-candidate boundary followed by deliberate human submission: [Steam Workshop Implementation Guide](https://partner.steamgames.com/doc/features/workshop/implementation?language=english).
- Steam's content interface expects a directory, and its documentation advises against combining the Workshop content into a ZIP for upload efficiency. The pipeline therefore produces an upload-ready directory, not an archive, as its primary product: [ISteamUGC documentation](https://partner.steamgames.com/doc/api/ISteamUGC?language=english).
- Steam limits a Workshop description to `k_cchPublishedDocumentDescriptionMax`, currently 8,000 bytes. Validation measures UTF-8 bytes, not merely .NET characters: [ISteamRemoteStorage constants](https://partner.steamgames.com/doc/api/ISteamRemoteStorage?language=english).
- Steam's supported text-formatting help documents the BBCode forms used by the description, including headings, emphasis, links, horizontal rules, and lists: [Steam Text Formatting](https://steamcommunity.com/comment/Announcement/formattinghelp).
- Windows defines both `CF_TEXT` and `CF_UNICODETEXT` clipboard lines as CRLF-terminated. This matches the observed ONI Uploader behavior: [Microsoft Standard Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats).
- NuGet lock files and locked restore mode provide repeatable dependency resolution and fail rather than silently rewriting dependency closure: [NuGet PackageReference lock-file guidance](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files).
- Microsoft recommends the SDK-style project format even when a project remains on .NET Framework. SDK-style projects use concise target-framework monikers, default items, generated assembly attributes, and implicit framework references rather than manually restating the legacy project system: [Microsoft .NET porting guidance](https://learn.microsoft.com/en-us/dotnet/core/porting/framework-overview), [.NET project SDK overview](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview).
- SDK-style .NET Framework projects receive the reference-assemblies package implicitly, while an explicit `Microsoft.NETFramework.ReferenceAssemblies` reference is the documented fallback for non-SDK-style projects. This permits non-Windows compilation without adding a redundant package declaration to the modernized project: [Microsoft .NET Framework reference-assemblies guidance](https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/reference-assemblies).
- `dotnet test` can emit TRX evidence through the supported logger: [Microsoft `dotnet test` documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest).

### 3.2 ONI community practices treated as design inputs

- PLib's maintainer recommends merging the PLib version used to build a mod, keeping game, Unity, and Harmony assemblies non-local, and avoiding separate PLib DLL distribution because it can create cross-platform version conflicts. The pipeline validates these packaging rules rather than uploading every build output: [PLib README](https://github.com/peterhaneve/ONIMods/blob/main/PLib/README.md).
- Established ONI mod repositories compile against installed or reference copies of the game's managed assemblies, keep machine-specific game paths out of committed shared configuration, and install local test mods beneath ONI's per-user mod directory: [Peter Han's ONIMods repository](https://github.com/peterhaneve/ONIMods).
- Community support guidance repeatedly emphasizes testing local mods separately from subscribed Steam copies and reading ONI logs when validating a mod. The acceptance workflow makes those checks explicit instead of assuming a successful compilation is sufficient.
- Klei's Uploader requires a root `mod_info.yaml` even when archived-version layouts are involved. The package validator therefore treats required root metadata as a hard invariant: [Klei ONI Mod Uploader issue report](https://forums.kleientertainment.com/klei-bug-tracker/oni/mod-uploader-issue-r38003/).

Community material is evidence of working convention rather than a frozen platform specification. Every community-derived rule is either configurable or verified against the local game/Uploader installation.

## 4. Goals

The first implementation must:

1. Run locally without requiring GitHub Actions, Azure Pipelines, a hosted runner, a container service, or any other external orchestration service.
2. Use one .NET 10 CLI on Windows, macOS, and Linux for pipeline orchestration.
3. Keep the ONI mod's game-compatible target framework independent from the pipeline runtime. Delivery Temperature Limit remains a .NET Framework mod; it is not retargeted to .NET 10 merely because the tool uses .NET 10.
4. Complete and simplify the mod's existing SDK-style project modernization while retaining `net48` and gameplay behavior.
5. Support multiple ONI mods through per-mod declarative manifests and explicit package allowlists.
6. Discover or accept the installed game's managed-assembly location and the user's ONI data/mod directories without committed machine-specific paths.
7. Validate mod metadata, version consistency, required game references, package contents, Workshop listing inputs, and release preconditions.
8. Build without mutating tracked source files.
9. Replace all ONI workflow uses of PowerShell, batch, and Bash in this repository. No compatibility shims remain under the old command names.
10. Run generic tool tests and mod-specific regression tests through ordinary .NET test projects.
11. Install an exact build or release candidate into ONI's `dev` or `local` mod area using guarded, recoverable filesystem operations.
12. Produce a clean Workshop-content directory containing only declared runtime files.
13. Produce separately validated Workshop description, change notes, and preview artifacts.
14. Produce SHA-256 manifests, build provenance, machine-readable readiness evidence, TRX results, and a human-readable release summary.
15. Bind manual in-game acceptance to the exact release-content digest that was installed.
16. Refuse readiness when content changes after tests or acceptance.
17. Stop in a `ready-for-upload` state and leave authenticated publication to a human using Klei's ONI Uploader.

## 5. Non-goals

The initial implementation will not:

- publish, update, or delete a Steam Workshop item;
- store, request, or transmit Steam credentials, cookies, tokens, or one-time codes;
- automate the ONI Uploader UI;
- invoke SteamCMD as an alternate publication path;
- create a mandatory cloud CI workflow;
- require Docker or another container runtime;
- modify ONI installation files;
- redistribute Klei, Unity, Harmony, FMOD, or other game-managed assemblies;
- infer gameplay correctness solely from a successful build;
- automatically change a mod's version as a side effect of building;
- rewrite tracked metadata during `build`, `test`, or `prepare-release`;
- introduce arbitrary pre-build or post-build shell hooks;
- introduce a code-plugin interface before two genuinely different implementations require one;
- package the upload directory into a ZIP as the Uploader handoff;
- use `[br]` tags or other content transformations to compensate for incorrect clipboard line endings; or
- add a Windows clipboard command in the first implementation unless direct copying from the generated CRLF artifact fails its acceptance test.

## 6. Terminology and invariants

### 6.1 Terms

- **Mod source:** Tracked files from which a mod is built and described.
- **Mod profile:** The mod-specific declarative `oni-mod-pipeline.toml` plus referenced tests and acceptance definitions.
- **Build result:** Compiled output in an isolated artifact directory. It is not yet an upload package.
- **Workshop content:** The exact directory selected as **Update Data** in the ONI Uploader.
- **Workshop listing:** Description, change notes, preview image, and other values entered separately in the Uploader.
- **Release content:** `workshop-content/` plus `workshop-listing/`. This is the material whose digest binds tests and acceptance.
- **Release evidence:** Reports proving what was built, tested, installed, and accepted. It is not uploaded as mod content.
- **Release candidate:** One versioned directory containing release content and release evidence.
- **Content digest:** A canonical SHA-256 digest over the release-content manifest, independent of evidence-file updates.
- **Ready for upload:** All automated checks and required human acceptance checks passed against the current content digest.
- **Published:** External Steam state. The pipeline does not create or assert this state.

### 6.2 Non-negotiable invariants

1. Build and verification commands never mutate mod source.
2. Workshop content is assembled into a new, empty staging directory from an explicit allowlist.
3. A release candidate never uploads files from `bin`, `obj`, the repository root, or the mutable ONI local-mod directory by implication.
4. Relative paths in manifests are resolved beneath the manifest's mod root and may not escape through `..`, symlinks, or junctions.
5. Every release-content file has a normalized relative path, byte length, and SHA-256 hash.
6. Acceptance results contain the release-content digest and are invalid if that digest changes.
7. Evidence is never placed inside `workshop-content/`.
8. Required checks cannot be skipped and still produce `ready-for-upload`.
9. `verify-release` is non-interactive and deterministic for a fixed candidate state.
10. No command named or described as `publish` exists in the initial CLI.

## 7. Architectural shape

### 7.1 One local CLI, deep internal modules

The first version is one production assembly named `OniModPipeline`, with root namespace `MaksymShostak.OniModPipeline`, exposed through the executable command `oni-mod-pipeline`. It is kept in:

```text
tools/oni-mod-pipeline/
  OniModPipeline.slnx
  src/
    OniModPipeline/
      OniModPipeline.csproj
  tests/
    OniModPipeline.Tests/
      OniModPipeline.Tests.csproj
```

The assembly is divided into deep internal modules, not one assembly per folder. A module earns its existence by hiding policy and mechanics behind a small interface. Initial module responsibilities are:

- **ModProfile:** Loads and semantically validates the per-mod manifest and referenced ONI metadata.
- **EnvironmentDiscovery:** Locates and validates the .NET SDK, ONI installation, managed assemblies, user-data directory, and mod install targets.
- **ModBuild:** Performs locked restore and build, injects immutable build properties, and returns an explicit build result.
- **ModTest:** discovers declared .NET test projects, runs them, and captures TRX results.
- **ModInstallation:** installs an exact build or candidate into a guarded `dev` or `local` target.
- **WorkshopContent:** constructs an allowlisted runtime-content directory and validates its closure.
- **WorkshopListing:** validates listing text and preview inputs and renders Uploader-facing text artifacts.
- **ReleaseCandidate:** orchestrates preparation, evidence capture, acceptance binding, state transitions, and final verification.
- **ContentIntegrity:** computes per-file hashes, canonical manifests, logical text digests, and the release-content digest.
- **Diagnostics:** exposes stable diagnostic identifiers and human/JSON rendering.

CLI parsing is a thin composition layer. Command handlers invoke the deep modules; they do not reimplement validation, filesystem policy, hashing, or lifecycle decisions.

### 7.2 No speculative extension framework

The generic/mod-specific seam is initially declarative:

```text
generic .NET implementation
          |
          v
versioned oni-mod-pipeline.toml
          |
          +--> mod files and build project
          +--> explicit package mappings
          +--> .NET test projects
          +--> in-game acceptance checks
          +--> Workshop listing inputs
```

This repository supplies one mod profile, not a custom executable plugin. One custom adapter would create only a hypothetical seam. A code extension interface is introduced later only if a second mod has required behavior that cannot be expressed safely by the versioned schema. Arbitrary command hooks are expressly rejected because they would recreate shell coupling, undermine deterministic evidence, and expand the security surface.

### 7.3 Filesystem is a local-substitutable dependency

The production implementation uses `System.IO`. Integration tests exercise the same module interfaces against isolated temporary directories. Filesystem mechanics remain internal seams; callers do not receive a sprawling filesystem abstraction.

External process execution is limited to explicit executables such as `dotnet`. Arguments are passed through `ProcessStartInfo.ArgumentList`; no shell is involved, and no command string is assembled for interpretation.

## 8. Naming contract

Names communicate domain purpose rather than implementation mechanism.

| Concern | Name or convention |
|---|---|
| Product | ONI Mod Pipeline |
| Executable | `oni-mod-pipeline` |
| Production assembly | `OniModPipeline` |
| Root namespace | `MaksymShostak.OniModPipeline` |
| Tool directory | `tools/oni-mod-pipeline` |
| Solution | `OniModPipeline.slnx` |
| Per-mod manifest | `oni-mod-pipeline.toml` |
| Release output root | `artifacts/release-candidates` |
| Pipeline-owned file and directory names | lower-case kebab-case except ecosystem-mandated names |
| Mod-owned input paths | preserved as declared; new companion files follow that mod's established local convention |
| TOML keys | lower-case kebab-case |
| C# types | PascalCase nouns that identify the domain concept |
| C# methods | PascalCase verb phrases describing the operation |
| Boolean properties | affirmative predicates such as `IsReadyForUpload` |
| Async methods | `Async` suffix |
| Test methods | `Operation_WhenCondition_ExpectedOutcome` |
| Diagnostic IDs | stable `ONIP` prefix plus four digits, such as `ONIP1001` |

Names such as `Helper`, `Utils`, `Manager`, `Processor`, `Common`, `Misc`, and `DoWork` are rejected unless they are part of an external ecosystem contract. Types should name the responsibility they own, such as `ReleaseCandidateVerifier`, `WorkshopContentManifest`, or `GameInstallationLocator`.

The current mod's `STEAM_CHANGE_NOTES.bbcode` deliberately pairs with its existing `STEAM_DESCRIPTION.bbcode`. The generic tool does not rename a mod's source assets to enforce its own casing preference. Generated pipeline artifacts remain lower-case (`description.bbcode`, `change-notes.bbcode`, and `preview.png` for this PNG-based profile).

The term **manifest** is reserved for declarative file inventories or configuration documents. The term **report** is reserved for derived evidence. The term **receipt** is reserved for evidence that an external side effect, such as local installation, occurred.

## 9. Per-mod profile

Each mod root contains one `oni-mod-pipeline.toml`. The CLI locates it from `--mod <directory-or-manifest>` or by walking from the current directory toward the repository root. Ambiguous discovery is an error.

The manifest is versioned through a required integer `schema-version`. Unknown schema versions fail closed. Unknown keys are errors rather than silently ignored misspellings.

An illustrative Delivery Temperature Limit profile is:

```toml
schema-version = 1

[mod]
mod-yaml = "mod.yaml"
mod-info-yaml = "mod_info.yaml"

[build]
entry-point = "Source/DeliveryTemperatureLimit.csproj"
configuration = "Release"
game-managed-directory-property = "OniManagedAssemblyDirectory"
primary-output = "{build-output}/DeliveryTemperatureLimit.dll"

[[package-files]]
source = "mod.yaml"
destination = "mod.yaml"

[[package-files]]
source = "mod_info.yaml"
destination = "mod_info.yaml"

[[package-files]]
source = "{build-output}/DeliveryTemperatureLimit.dll"
destination = "DeliveryTemperatureLimit.dll"

[workshop-listing]
description = "STEAM_DESCRIPTION.bbcode"
change-notes = "STEAM_CHANGE_NOTES.bbcode"
preview = "Preview.png"
mod-types = ["new-features", "tweaks", "ui"]
dlc-compatibility = [
  "base-game",
  "spaced-out",
  "frosty-planet-pack",
  "bionic-booster-pack",
  "prehistoric-planet-pack",
  "aquatic-planet-pack"
]

[local-install]
directory-name = "DeliveryTemperatureLimit"

[[test-projects]]
id = "delivery-temperature-limit-regressions"
path = "Tests/DeliveryTemperatureLimit.Tests.csproj"
required = true

[[acceptance-checks]]
id = "storage-bin-temperature-filter"
title = "Storage Bin rejects out-of-range deliveries"
required = true

[[acceptance-checks]]
id = "storage-tile-rocket-temperature-filter"
title = "Storage Tile aboard a rocket rejects out-of-range deliveries"
required = true
setup = "Load the release test colony and configure the rocket Storage Tile's temperature range."
action = "Make both an out-of-range material and a valid control material available to Duplicants."
expected = "The invalid delivery is refused and the in-range control remains deliverable."

[[acceptance-checks]]
id = "temperature-side-screen-keyboard"
title = "Temperature side screen does not lock camera keyboard controls"
required = true
```

This example is normative about structure and naming but not authorization to create or modify configuration. Exact configuration-file changes require separate approval before implementation.

The profile deliberately does not duplicate values already owned by ONI metadata:

- `staticID` and title come from `mod.yaml`.
- release version, `supportedContent`, `minimumSupportedBuild`, and `APIVersion` come from `mod_info.yaml`.
- project assembly name is validated against the declared primary output rather than copied into multiple version fields.

Workshop change notes are an explicit, tracked release input. The declared source file is deliberately updated for each candidate; `prepare-release` never invents notes from Git history or accepts an implicit placeholder. Mod types and DLC compatibility use versioned schema identifiers that the generic layer maps to the labels shown by the installed Uploader. An unknown identifier fails closed so a newly introduced Klei option cannot be selected accidentally.

Package mappings are explicit. V1 supports directory mappings for genuine asset trees, but each mapping must name one contained source directory and one destination prefix. Recursion is limited to that declared tree, every resulting file is enumerated in evidence, and unrestricted `**/*` inclusion is not allowed at the mod root.

For v1, a conventionally buildable ONI mod means one whose code build can be entered through one declared MSBuild project or solution. That entry point can transitively build any number of referenced projects, and the profile can declare multiple resulting package files. A content-only profile can omit `[build]`; its build result contains no compiled outputs, while validation, declared asset packaging, tests, installation, and release evidence still apply. A mod that requires an unrelated generator or build system needs a named generic module and a schema revision; it cannot smuggle that behavior through a shell hook.

## 10. Runtime, SDK, and dependency policy

### 10.1 Pipeline runtime

The pipeline targets .NET 10 LTS. The repository pins SDK `10.0.400` in `global.json` with `rollForward` set to `latestPatch` and `allowPrerelease` set to `false`. This permits servicing releases only within the `10.0.4xx` feature band and fails if no compatible stable SDK exists; it does not silently select a later feature band. The exact configuration change must be separately approved. The semantics come from Microsoft's [`global.json` reference](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

The tool's package dependency closure is committed through `packages.lock.json`. Normal validation and release preparation use `dotnet restore --locked-mode`. Updating dependencies is an explicit maintenance operation that intentionally regenerates and reviews lock files.

### 10.2 Mod target framework

The mod's target framework is selected for ONI compatibility, not tool uniformity. Delivery Temperature Limit remains an SDK-style `net48` project; it is modernized without retargeting the runtime consumed by ONI. Under the pinned .NET 10 SDK, the project relies on the SDK's implicit .NET Framework reference-assemblies package rather than a Windows developer pack or a redundant explicit package reference. Locked restore captures that implicit dependency. The actual Klei/Unity reference assemblies still come from a locally installed or explicitly supplied ONI managed-assembly directory.

### 10.3 Dependency packaging

The build may merge PLib into the mod DLL because PLib's own guidance recommends shipping the version against which the mod was built. The following must not be copied or merged into Workshop content unless a mod profile explicitly documents a different, verified game contract:

- `0Harmony.dll`;
- `Assembly-CSharp.dll`;
- `Assembly-CSharp-firstpass.dll`;
- Unity assemblies;
- FMOD assemblies;
- Newtonsoft.Json supplied by the game; and
- other Klei/game-managed dependencies.

The profile names intended merge inputs. The package validator fails if an undeclared dependency DLL appears in `workshop-content/`.

## 11. Configuration and environment discovery

Committed configuration contains only portable, repository-relative facts. It never contains a developer's home directory, Documents directory, Steam-library path, or credentials.

Machine-specific input precedence is:

1. explicit CLI option;
2. documented environment variable;
3. platform-specific automatic discovery;
4. a diagnostic failure explaining every searched location and the exact override option.

The initial portable options and environment equivalents are:

| Purpose | CLI option | Environment variable |
|---|---|---|
| ONI installation root | `--game-directory` | `ONI_GAME_DIRECTORY` |
| ONI per-user data root | `--user-data-directory` | `ONI_USER_DATA_DIRECTORY` |
| Pipeline artifact root | `--artifacts-directory` | `ONI_PIPELINE_ARTIFACTS_DIRECTORY` |

Without an artifact-root override, the tool uses `<git-worktree-root>/artifacts` when the mod is inside a Git worktree and `<mod-root>/artifacts` otherwise. Artifact paths are never inferred from the current shell's temporary directory. The selected root is reported before a mutating command and participates in the same containment checks as every staging and cleanup path.

The managed-assembly directory is derived beneath the game installation and validated by required assembly names. The `dev` and `local` install targets are derived beneath the user-data root.

Automatic discovery inspects the applicable entries among:

- Steam's registered installation and library metadata on Windows;
- conventional Steam library locations on macOS and Linux;
- `Environment.GetFolderPath` on Windows so redirected Documents folders work; and
- conventional Klei per-user data paths on each supported operating system.

Discovery is read-only. If multiple plausible game installations remain, the tool reports them and requires an explicit choice instead of guessing.

`diagnose` reports the selected SDK, operating system, repository and mod roots, game path, game managed-assembly path, user-data path, local install targets, detected game build information where available, and whether the optional ONI Uploader is installed. It does not require the Uploader for any pre-publication command.

## 12. CLI command interface

All commands support `--help`. Commands that emit evidence also support `--format json` for machine-readable status while retaining concise human output by default. Success is exit code `0`; stable nonzero categories distinguish invalid input, missing environment, build/test failure, and release-readiness failure.

### 12.1 `diagnose`

```text
oni-mod-pipeline diagnose --mod <path>
```

Performs read-only environment discovery and reports actionable diagnostics. It does not restore packages, build, install, or create a candidate.

### 12.2 `validate`

```text
oni-mod-pipeline validate --mod <path> [--for-release]
```

Loads the profile and validates:

- schema and path safety;
- required `mod.yaml` and `mod_info.yaml` fields;
- static ID and version syntax;
- build project and declared output;
- game references;
- package destination uniqueness;
- source/package casing collisions;
- test and acceptance declarations;
- Workshop description and change-note syntax, source line endings, and byte limits;
- preview existence and supported image format;
- mod-type and DLC-compatibility identifiers; and
- relevant Git provenance and release preconditions when `--for-release` is supplied.

Validation is side-effect free. Without `--for-release`, it permits ordinary uncommitted development inputs. With `--for-release`, it applies the same committed-and-clean contributing-input rules as `prepare-release`.

### 12.3 `build`

```text
oni-mod-pipeline build --mod <path> [--configuration Release]
```

Runs locked restore and the configured build without a shell. Output is written under `artifacts/builds/<static-id>/<run-id>/`, never into the mod source root. It writes `build-result.json` at that run root, naming all declared outputs, the primary assembly and merged dependencies when present, source commit, version, game-reference hashes, SDK version, and actual output hashes. For a content-only profile, it skips restore/compilation explicitly and returns a build result with no compiled outputs rather than fabricating an assembly.

`build` does not update `mod_info.yaml`, copy to ONI's mod directory, or create a release candidate.

### 12.4 `test`

```text
oni-mod-pipeline test --mod <path>
```

Runs every required mod test project declared by the profile. Test-project IDs are required, unique, stable kebab-case identifiers; each names its TRX evidence file. Standalone results are written beneath `artifacts/test-runs/<static-id>/<run-id>/automated-test-results/`; `prepare-release` writes the same contract inside its staged candidate. A required project that is missing, skipped, has a duplicate ID, or produces no result is a failure.

The pipeline tool's own unit and integration suites are run from `OniModPipeline.slnx` when developing or releasing the tool; an installed CLI does not recursively test its own source on every mod invocation. Mod release provenance instead records the exact CLI informational version and executable SHA-256. When the CLI is built from the same worktree, `prepare-release` additionally requires its contributing tool source and test files to be committed and clean.

### 12.5 `install`

```text
oni-mod-pipeline install --candidate <candidate-directory> --target dev
oni-mod-pipeline install --candidate <candidate-directory> --target local
oni-mod-pipeline install --mod <path> --build-result <build-result.json> --target dev
oni-mod-pipeline install --mod <path> --build-result <build-result.json> --target local
```

The mutually exclusive `--candidate` and `--build-result` inputs install exact, named artifacts into the selected ONI mod target. Candidate-based installation is the only path that creates an acceptance installation receipt. A development build result also requires `--mod`; the installer verifies the profile and source-input hashes recorded by that result before assembling its declared package. An implicit “latest” build is never selected.

The installer:

- resolves and validates the absolute destination;
- refuses to replace any existing destination that lacks a matching pipeline ownership marker;
- writes to a sibling staging directory;
- validates the copied content against the candidate manifest;
- swaps the staged directory into place;
- writes an ownership marker in the local install only, never in Workshop content; and
- records an `installation-receipt.json` in release evidence with candidate digest, target kind, absolute target path, timestamp, and installed-file verification result.

It warns that an enabled subscribed Steam copy of the same mod can invalidate testing. It does not edit Steam subscriptions or ONI settings.

There is no force, adopt, or recursive-delete switch in v1. For the first managed installation, an existing hand-maintained destination must be moved aside by the developer and the now-absent target is then created by the pipeline. This makes the ownership transition visible and prevents a plausible static ID from being treated as permission to erase an unowned directory.

A release candidate has one acceptance installation receipt. If that receipt already exists, another candidate-based `install` fails instead of changing which installation the later attestation refers to. Iterative installs use development build results; a release candidate is prepared only when the developer is ready to perform the recorded acceptance pass.

### 12.6 `prepare-release`

```text
oni-mod-pipeline prepare-release --mod <path>
```

This is the orchestration command. It performs environment validation, relevant-source cleanliness validation, locked restore, Release build, automated tests, clean package construction, Workshop-listing rendering, hashing, provenance capture, and initial readiness reporting.

It either creates one complete candidate in `awaiting-acceptance` state or leaves no candidate directory. Temporary staging is removed after a failure. The command never reuses or mutates an older candidate's release content.

### 12.7 `record-acceptance`

```text
oni-mod-pipeline record-acceptance --candidate <candidate-directory> [--tester <display-name>]
```

Displays the candidate's immutable acceptance plan and records a human result for each check. It requires an interactive terminal because these results attest to actions performed in ONI. A non-empty tester display name is supplied by option or interactive prompt. Each result is `passed` or `failed`, with an optional note. Required checks cannot be recorded as skipped.

The command verifies that the candidate was installed, that the installed files still match the current content digest, and that the installation receipt names that digest. It writes `acceptance-test-results.json` with the tester display name, UTC timestamp, copied check identifiers and expected observations, tester-entered results, current content digest, and acceptance-plan SHA-256. It never changes release content, and an existing results file makes the command fail rather than overwrite an attestation.

For future laboratory automation, a separately authenticated result-file import may be designed, but the initial interface does not pretend that JSON produced without gameplay is human acceptance.

### 12.8 `verify-release`

```text
oni-mod-pipeline verify-release --candidate <candidate-directory>
```

Recomputes every release-content hash, verifies provenance and automated evidence, verifies that all required acceptance checks passed against the current digest, checks Uploader-facing representation rules, and writes the final readiness report.

It returns success only for `ready-for-upload`. It is non-interactive and safe to repeat. It does not open the Uploader or publish.

## 13. Release-candidate directory contract

```text
artifacts/
  release-candidates/
    <static-id>/
      <version>/
        <run-id>/
          workshop-content/
            <exact runtime files selected in the Uploader>
          workshop-listing/
            description.bbcode
            change-notes.bbcode
            preview.png
          release-evidence/
            release-readiness-report.json
            release-content-manifest.json
            build-provenance.json
            automated-test-results/
              <test-project-id>.trx
            acceptance-test-plan.json
            acceptance-test-results.json      # created by record-acceptance
            installation-receipt.json         # created by install
            release-summary.md
            uploader-checklist.md
```

The tree is concrete for Delivery Temperature Limit, whose preview is PNG. A profile using another accepted format produces `preview.jpg` or `preview.gif` in the same role.

`change-notes.bbcode` is always rendered from the profile's declared, tracked source, even when no Workshop description update is intended. Empty or whitespace-only notes cannot produce `ready-for-upload`. The trimmed entire source is also rejected, case-insensitively, when it equals one of the reserved placeholders `TODO`, `TBD`, `CHANGEME`, or `ONI_PIPELINE_CHANGE_NOTES_REQUIRED`.

The run ID has the invariant format `yyyyMMddTHHmmss.fffffffZ-<16-lowercase-hex-random>`. It is filesystem-safe, UTC-sortable, collision-resistant for repeated local runs, and metadata rather than the mod version.

The two lifecycle files annotated above are absent in a newly prepared `awaiting-acceptance` candidate. `install` creates the installation receipt only after it has re-read and verified the installed bytes. `record-acceptance` creates the acceptance results exactly once; it never overwrites a prior pass or failure. All other files and directories in the tree exist when `prepare-release` succeeds.

### 13.1 Content and evidence separation

Only `workshop-content/` is selected as **Update Data**. Files in `workshop-listing/` are supplied to their corresponding Uploader fields. `release-evidence/` is never selected for upload.

Release content becomes immutable when preparation succeeds. Prepared identity evidence—`release-content-manifest.json`, `build-provenance.json`, `automated-test-results/`, and `acceptance-test-plan.json`—is also immutable. `install` and `record-acceptance` may each create their one named lifecycle receipt exactly once. `verify-release` may atomically replace only the derived readiness report, release summary, and Uploader checklist. No lifecycle command may modify `workshop-content/` or `workshop-listing/`; a content change requires a new run ID and a new candidate.

### 13.2 Canonical content manifest

`release-content-manifest.json` includes both runtime content and Workshop-listing artifacts. The broader name is intentional: a file called a Workshop-content manifest must not silently include files outside `workshop-content/`. Entries are sorted by normalized, `/`-separated, Unicode-NFC, case-preserving relative path using ordinal comparison. Portable case-insensitive or Unicode-normalization collisions are rejected. Each entry records:

- content area (`workshop-content` or `workshop-listing`);
- relative path;
- byte length;
- lowercase SHA-256; and
- role (`runtime`, `description`, `change-notes`, or `preview`).

The release-content digest is SHA-256 over this exact UTF-8 canonical serialization, independent of JSON formatting:

```text
oni-release-content-manifest-v1\n
<content-area>\0<normalized-relative-path>\0<byte-length-in-invariant-decimal>\0<lowercase-sha256>\0<role>\n
...one line per sorted entry...
```

The header, separators, and record terminators are literal ASCII bytes; `\0` denotes one NUL byte and `\n` one LF byte. Paths cannot contain either byte. The serialization definition is tested with golden vectors so every supported operating system computes the same digest. The manifest's own SHA-256 is recorded in the readiness report but is not recursively included in the content digest.

### 13.3 Provenance

`build-provenance.json` records at least:

- profile schema version;
- pipeline informational version and executable SHA-256;
- mod static ID and release version;
- repository commit;
- relevant-path cleanliness result;
- UTC preparation time;
- operating system and architecture;
- .NET SDK version;
- project target framework and build configuration;
- locked dependency-closure hash;
- resolved ONI installation and detected game build metadata without personal credentials;
- hashes of referenced game assemblies used for compilation;
- build invocation as a structured executable-plus-argument list;
- primary output and merge-input hashes; and
- release-content digest.

`acceptance-test-plan.json` is rendered from the declared profile checks during preparation and is immutable for the candidate. Its SHA-256 is recorded in provenance. Acceptance results bind both that plan hash and the release-content digest, so neither the bytes under test nor the meaning of “passed” can drift between preparation and the human test.

Absolute user paths are omitted or normalized to named roots where they add no verification value.

## 14. Release-candidate lifecycle

The pipeline owns the following states:

```text
prepare-release
      |
      v
awaiting-acceptance
      |
      +-- install exact candidate --> installation receipt
      |
      +-- record-acceptance failed --> acceptance-failed
      |
      +-- record-acceptance passed
                    |
                    v
              verify-release
                    |
          +---------+---------+
          |                   |
          v                   v
   ready-for-upload     verification-failed
          |
          v
human opens authenticated ONI Uploader
          |
          v
publication is external and unrecorded by v1
```

The state is derived from evidence rather than trusted as a mutable label. `release-readiness-report.json` summarizes that derivation.

If any release-content byte changes after preparation:

- the recomputed digest differs;
- installation and acceptance receipts no longer match;
- `verify-release` fails;
- the candidate cannot return to ready state; and
- a new candidate must be prepared.

If an acceptance check fails, the candidate remains evidence of that failed test and its write-once acceptance file cannot be replaced. A corrected observation, code change, or content change requires a new candidate. This preserves an auditable relationship between failure, fix, and replacement candidate.

## 15. Build and packaging design

### 15.1 No source mutation

`mod_info.yaml` is the canonical ONI release-version source for this mod. The pipeline reads it before building and supplies the value as an immutable build property where binary metadata needs it. Building does not rewrite `mod_info.yaml`.

This replaces the current `UpdateModInfoVersion` post-build target rather than translating it to another language. It also avoids using a frequently changing `AssemblyVersion` as the release workflow's source of truth. Assembly binding version, file version, and informational version are treated according to their distinct .NET meanings.

Version changes are deliberate source changes made before validation. The generic pipeline does not impose semantic versioning or calendar versioning; it validates the syntax required by ONI and the mod profile.

### 15.2 Complete the existing SDK-style project modernization

`DeliveryTemperatureLimit.csproj` is already an SDK-style project because its root is `<Project Sdk="Microsoft.NET.Sdk">` and it uses `TargetFramework` plus `PackageReference`. The migration is therefore not a conversion from the legacy project schema. It is a deliberate completion and simplification of a partially modernized file.

The target project contract is:

| Current construct | Modernized contract |
|---|---|
| `<TargetFramework>net48</TargetFramework>` | Retain it; ONI compatibility, not the pipeline runtime, determines the mod target. |
| Project-local `OutputPath` and `Append*ToOutputPath` overrides | Remove them. The pipeline supplies `BaseOutputPath`, `BaseIntermediateOutputPath`, and the explicit merged-mod output path beneath the selected artifact run. |
| Conditional Debug/Release groups mixing SDK defaults with `DebugType=none` suppression | Remove them unless a measured mod requirement differs from the pinned SDK default. Portable debugging symbols may remain in build artifacts; the Workshop allowlist excludes them. |
| Explicit `System`, `System.Core`, `System.Xml*`, and `System.Data*` references | Remove them and use the SDK's implicit .NET Framework references. Only actual ONI, Unity, Harmony, FMOD, and other non-framework compile references remain explicit. |
| Literal release value in `<AssemblyVersion>` | Remove it as source-owned release metadata. The pipeline passes the validated `mod_info.yaml` version through standard SDK version properties and records the resulting assembly, file, and informational versions in provenance. |
| Unqualified `GameFolder` MSBuild property | Rename it to `OniManagedAssemblyDirectory`, matching the directory it actually contains. Every game-assembly `HintPath` and the merge task use this property. |
| Hard-coded fallback in `Directory.Build.props` | Delete the file after the project has an actionable validation target and the pipeline always supplies `OniManagedAssemblyDirectory`; there is no machine-specific fallback. |
| Unlocked package restore | Set `RestorePackagesWithLockFile` to `true`, commit `packages.lock.json`, and use locked restore in release workflows. |
| Build-tool package references indistinguishable from runtime dependencies | Mark build-only packages such as `ILRepack.Lib.MSBuild.Task` with `PrivateAssets="all"`; retain only the asset categories the current project consumes. PLib remains a declared merge input, not a loose Workshop DLL. |
| Merge logic constructing paths from `$(OutputPath)` and writing to the source root | Import the merge target explicitly, consume `$(TargetPath)` and the pipeline-supplied merged-mod output path, and fail before merging if required paths are absent. |

The modernized project continues to let the SDK generate compile items and assembly attributes. It does not introduce `packages.config`, manually enumerate `.cs` files, enable a newer language feature merely for fashion, or retarget the mod to `net10.0`. Any language-version or nullable-analysis change is a separate source-compatibility decision, not part of project-format modernization.

The migration is behavior-preserving. Before deleting the legacy properties and references, the implementation captures the evaluated target framework, assembly name, compile/reference closure, public assembly surface, and runtime package inventory. After modernization, the same checks must agree except for intentionally approved version/provenance metadata and isolated artifact paths.

### 15.3 Isolated build output

The build supplies explicit base output and intermediate-output directories beneath the run's artifact directory. ILRepack output is written there. The source mod directory does not receive newly built DLLs.

Restore runs in locked mode. Release builds enable deterministic compiler output and map machine-specific source paths out of production artifacts whenever the selected compiler supports those features. Build warnings are captured verbatim. The pipeline does not silently change a project's warning-as-error policy; any such policy remains an explicitly approved project configuration.

### 15.4 Package allowlist

Packaging creates an empty temporary directory and copies only declared mappings. After copying, the pipeline walks the complete staging tree and proves that every file is declared exactly once. It rejects:

- undeclared DLLs;
- source, tests, scripts, build logs, PDBs, or intermediate files unless explicitly required;
- path and casing collisions;
- symlinks or junctions;
- files outside the mod root or build result;
- root metadata omissions;
- zero-byte primary assemblies; and
- game-managed assemblies.

The completed directory is renamed into `workshop-content/` only after validation succeeds.

## 16. Workshop-listing design

### 16.1 Separation from runtime content

Workshop listing assets are versioned alongside source but staged outside the upload-content directory. This mirrors Steam's distinct update fields and prevents a preview or description source from being uploaded as a runtime mod file by accident.

The initial listing consists of:

- `description.bbcode`;
- `change-notes.bbcode`; and
- `preview.<validated-extension>` (`preview.png` for the initial mod profile).

The pipeline validates BBCode as plain text. It does not render or silently repair semantic markup. Known paired tags are checked for balanced nesting where Steam's grammar makes that meaningful. URLs are checked for a supported scheme. Markdown-link syntax in a BBCode file produces an actionable diagnostic.

V1 accepts PNG, JPEG, or GIF preview inputs whose decoded signature agrees with their extension. It preserves the image bytes and uses the normalized lower-case extension (`.png`, `.jpg`, or `.gif`) in the candidate; it does not transcode or merely relabel an image. The release summary and checklist always name the exact generated preview path.

Description size is measured as UTF-8 bytes and must not exceed Steam's documented 8,000-byte limit. Steam also documents an 8,000-byte `k_cchPublishedDocumentChangeDescriptionMax` constant but labels it unused. V1 therefore applies 8,000 UTF-8 bytes as a conservative change-note ceiling until a stricter Klei Uploader contract is established. A profile may lower either ceiling but cannot raise it without a schema/tool revision backed by new platform evidence.

### 16.2 Confirmed CRLF Uploader handoff

On 2026-08-27, the actual installed `OniUploader64.exe` was tested against the current 5,000-character Workshop description:

1. The tracked source was confirmed as UTF-8 with 54 logical lines and LF-only line endings.
2. Copying the LF representation into the Uploader caused paragraph boundaries, blank lines, `---`, headings, and list structures to concatenate.
3. The same logical content was placed on the Windows clipboard as plain Unicode containing 53 CRLF pairs and no lone CR or LF characters.
4. Pasting that representation preserved every reported structure.

The release contract is therefore:

| Artifact | Encoding | Line endings | Final newline |
|---|---|---|---|
| Tracked Workshop source | UTF-8, no BOM | LF | exactly one LF |
| Candidate `description.bbcode` | UTF-8, no BOM | CRLF only | exactly one CRLF |
| Candidate `change-notes.bbcode` | UTF-8, no BOM | CRLF only | exactly one CRLF |
| JSON, Markdown, TOML, YAML source/evidence | UTF-8, no BOM | LF unless an external contract requires otherwise | exactly one LF |

The repository's existing `.gitattributes` already enforces LF on text checkout, so the tracked-source side of this contract needs no configuration change. Candidate rendering is deliberately outside Git under `artifacts/` and emits the consumer-specific CRLF representation.

The renderer first parses logical lines, then emits the target convention. It never performs a naïve LF replacement that could turn existing CRLF into CRCRLF. It preserves every empty logical line and Unicode scalar value.

For each listing text, evidence records:

- encoding;
- BOM presence;
- line-ending convention;
- logical line count;
- line-break count;
- blank-line count;
- UTF-8 byte count;
- logical-content SHA-256 after canonical LF normalization; and
- artifact SHA-256 over actual CRLF bytes.

The source and artifact logical digests must match. This proves that staging changed representation, not content.

### 16.3 Clipboard command is deferred

The initial implementation does not add `copy-workshop-description`. A correct CRLF candidate file satisfies the user's requested direct file-copy workflow without introducing a Windows-desktop dependency into the generic CLI.

Implementation acceptance includes copying directly from the generated CRLF file into the installed Uploader. If that still fails in a supported editor, a later bounded design may add a Windows-only command using the official Unicode clipboard format, STA execution, persistence after process exit, bounded busy-clipboard retries, and read-back verification. That command would remain a convenience and would never publish.

### 16.4 Uploader checklist

`uploader-checklist.md` names exact absolute paths and requires the publisher to verify, before enabling any Uploader update checkbox:

- candidate state is `ready-for-upload`;
- **Update Data** points exactly to `workshop-content/`;
- the Uploader's displayed data directory is not the mutable local-test directory;
- description comes from `workshop-listing/description.bbcode`;
- paragraph, blank-line, separator, heading, and list sentinels remain separated after paste;
- change notes come from `workshop-listing/change-notes.bbcode`;
- preview comes from `workshop-listing/preview.png`;
- title, tags, DLC compatibility, and mod type match the release summary; and
- the human reviews the final form immediately before clicking **Publish**.

The checklist explicitly states that clicking **Publish** is irreversible from the pipeline's perspective and outside its automation scope.

## 17. Generic layer and Delivery Temperature Limit profile

### 17.1 Generic responsibilities

The generic layer understands stable concepts shared across ONI mods:

- ONI metadata presence and schema-level checks;
- project build invocation;
- game-managed references;
- declared merge inputs;
- explicit package mappings;
- local `dev` and `local` installation;
- Workshop listing roles;
- hashing and provenance;
- .NET test execution;
- acceptance-plan/result binding; and
- release lifecycle.

It does not know that Storage Tiles, rockets, temperature limits, or the mod's UI exist.

### 17.2 Mod-specific responsibilities

The Delivery Temperature Limit profile declares:

- `DeliveryTemperatureLimit.csproj` as the MSBuild entry point;
- `DeliveryTemperatureLimit.dll` as the runtime assembly;
- PLib as the only intended merge input;
- `mod.yaml`, `mod_info.yaml`, and the merged DLL as the initial runtime package;
- `STEAM_DESCRIPTION.bbcode` and `Preview.png` as Workshop sources;
- `STEAM_CHANGE_NOTES.bbcode` as the required, release-specific change-note source;
- the Uploader mod-type and DLC-compatibility selections shown in the profile;
- C# replacements for both current PowerShell regression tests;
- acceptance checks for storage buildings, Storage Tiles aboard rockets, construction-material behavior when enabled, side-screen editing, keyboard/camera behavior, save/load, and absence of relevant log exceptions; and
- the local installation directory name.

This is the “additional layer” specific to the current mod. It remains declarative plus ordinary mod-specific C# tests, not a fork of the generic pipeline.

## 18. Test strategy

### 18.1 Tool unit tests

Tests exercise module interfaces and observable results. Required areas include:

- manifest parsing and unknown-key rejection;
- path containment and symlink/junction rejection;
- metadata validation;
- environment-discovery precedence;
- package mapping and collision detection;
- canonical manifest serialization;
- SHA-256 golden vectors;
- lifecycle state derivation;
- acceptance-digest invalidation;
- diagnostic rendering; and
- line-ending normalization.

The Workshop text fixture includes paragraphs, consecutive blank lines, `---`, Unicode and emoji, headings, lists, links, and a final newline. Property-style cases cover LF, CRLF, lone CR, and mixed inputs. The transformation must be idempotent and preserve the logical digest.

### 18.2 Tool integration tests

Temporary-repository fixtures exercise:

- locked restore/build process invocation through a fake or minimal project;
- no source-file changes before versus after build;
- isolated output paths;
- clean package construction;
- candidate all-or-nothing creation;
- exact manifest/file equality after copy;
- guarded installation and ownership markers;
- evidence updates without release-content mutation; and
- verification failure after deliberate candidate tampering.

Tests that mutate temporary directories resolve and assert their containment before cleanup.

### 18.3 Mod-specific regression tests

The two PowerShell tests are replaced by a conventional C# test project:

- the Storage Tile eligibility regression compiles the relevant production behavior against maintained ONI/Unity/Harmony test doubles and asserts that `StorageTileConfig` is eligible;
- the metadata regression is replaced by a stronger invariant: build does not alter `mod_info.yaml`, and the staged copy retains LF-independent logical content while source bytes remain unchanged.

The implementation plan maps every case named in Sections 18.1–18.4 to a concrete test task; there is no unnamed “add tests later” gate. Game-independent policy is extracted and tested through ordinary interfaces. The Storage Tile integration that necessarily names ONI configuration types is compiled against maintained test doubles until a supported headless game harness exists; raw source concatenation is not the long-term interface for newly extracted policy.

### 18.4 Real-build verification

The pipeline performs a Release build against the discovered ONI managed assemblies and verifies:

- the evaluated project is SDK-style, targets exactly `net48`, and contains no legacy explicit base-framework references or machine-specific fallback path;
- locked restore succeeds using the implicit .NET Framework reference-assemblies dependency on every supported runner operating system;
- the primary DLL exists and is non-empty;
- ILRepack succeeded;
- intended PLib types are available as expected;
- forbidden game assemblies are absent from package output;
- the post-modernization public assembly surface and runtime package inventory match the captured baseline except for explicitly approved version/provenance metadata;
- metadata and build versions agree under the selected version policy; and
- no tracked source bytes changed.

### 18.5 Human in-game acceptance

Automated tests cannot prove Harmony patch compatibility, Unity lifecycle behavior, actual Duplicant errands, rocket interiors, save/load behavior, or side-screen interaction in the installed game. Required acceptance checks therefore include explicit setup, action, expected observation, and log-review instructions.

For the current Storage Tile change, the release-blocking scenario includes an out-of-range material, a configured Storage Tile aboard a rocket, an available competing delivery target where needed, observed refusal of the invalid delivery, and a control case showing that in-range material remains deliverable.

Acceptance results are meaningful only after installing the exact candidate and are invalidated by any content-digest change.

### 18.6 Uploader handoff acceptance

Before the first pipeline release—and again after a materially changed Uploader version—the generated CRLF description is opened in current Windows Notepad, copied from its candidate file, and pasted into the actual Uploader without enabling **Update Details** or publishing. Paragraph and BBCode line structure must remain intact. The acceptance record names the editor and Uploader versions. Other editors are acceptable conveniences, but Windows Notepad is the reproducible baseline because it ships with the Uploader's operating environment. This is a manual compatibility test of a third-party GUI seam, not a default automated test.

## 19. Failure model and diagnostics

The tool fails closed and explains the remedy. It does not continue after a failed prerequisite merely to collect a misleading candidate.

Stable exit categories are:

| Exit code | Meaning |
|---:|---|
| `0` | Requested operation completed successfully |
| `2` | Invalid command or mod-profile input |
| `3` | Required local environment is unavailable or ambiguous |
| `4` | Build or automated test failed |
| `5` | Installation failed or could not be verified |
| `6` | Release candidate is not ready for upload |
| `10` | Unexpected internal failure |

Every expected failure has a stable diagnostic ID, summary, evidence, and next action. Human output uses absolute paths only where the developer needs to act. JSON output includes structured fields without ANSI decoration.

Examples of required diagnostics include:

- missing or unsupported .NET SDK;
- multiple ONI installations found;
- required game assembly missing;
- profile path escapes mod root;
- duplicate package destination;
- description exceeds 8,000 UTF-8 bytes;
- listing file contains a bare LF in the candidate;
- source file changed during build;
- candidate manifest mismatch;
- acceptance digest differs from current content;
- Steam/local duplicate-mod risk; and
- destination directory is not owned by this pipeline/static ID.

On `prepare-release` failure, temporary staging is cleaned and no final candidate path is reported. Diagnostic cleanup failure is reported but never masks the primary failure.

## 20. Safety and trust model

### 20.1 Filesystem safety

- Resolve every source, staging, destination, and cleanup path to an absolute path.
- Enforce containment beneath an explicitly selected root before recursive copy, move, or deletion.
- Do not follow symlinks or junctions while packaging.
- Use unique sibling staging directories and atomic rename where supported.
- Replace only local installation directories carrying a matching ownership marker.
- Never delete a repository, workspace root, home directory, Documents root, ONI data root, or Steam library root.

### 20.2 Source-control provenance

`prepare-release` requires every mod source, profile, metadata, Workshop source, and declared test file that contributes to the candidate to be committed and unchanged. Unrelated repository paths do not block a release merely because another mod or document is being edited. The exact scoped paths and commit are recorded.

The pipeline executable is always identified by informational version and SHA-256. If it was built from the same worktree, its contributing source, project, lock, and test files join the clean scope and their commit is recorded. If it is an externally installed release, its source need not exist in the mod worktree; provenance records the executable identity instead. This is attribution and accidental-change protection, not a cryptographic claim that a human acceptance record cannot be forged by a hostile local user.

There is no `--allow-dirty` bypass in the initial release-candidate command. A developer can use `build`, `test`, and development installation while iterating, but an upload-ready candidate comes from attributable inputs.

### 20.3 Network and credentials

The pipeline's only normal network-capable operation is NuGet restore, and locked restore can use an existing local cache. It never contacts Steam for publication and never inspects or records Steam authentication data.

External CI may later invoke the same CLI commands, but it is an adapter around the local contract, not the source of pipeline semantics.

## 21. Evidence and reproducibility

The release claim is intentionally precise:

> These exact release-content bytes were produced from these attributable inputs, passed these automated checks, were installed at this digest, passed these recorded human checks, and satisfy every pre-upload invariant known to the pipeline.

It does not claim that Steam accepted the upload or that all future ONI versions will remain compatible.

Reproducibility measures include:

- pinned .NET SDK feature band;
- committed NuGet lock files and locked restore;
- exact game-reference hashes;
- deterministic compiler settings where supported;
- isolated output directories;
- explicit merge inputs;
- sorted package mappings;
- canonical hash serialization;
- stable text encodings and line-ending policies; and
- no timestamps inside the content digest unless the content format itself requires them.

The final `release-readiness-report.json` is also the evidence index. It records byte length and SHA-256 for every other evidence file, including each TRX file, installation receipt, acceptance plan/results, release summary, and Uploader checklist; it excludes only itself. Derived summary, checklist, and readiness bytes contain candidate/event times already captured by prepared or write-once evidence, never a fresh “verified now” time. Re-running `verify-release` against an unchanged candidate therefore reproduces the same derived files and hashes.

If ILRepack or another unavoidable tool produces non-reproducible binary bytes, the pipeline still hashes and proves the exact result. The implementation plan must test binary reproducibility rather than assume it; any limitation is recorded in provenance and release summary.

## 22. Documentation and human workflow

The implementation updates repository documentation to present one normal path:

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

The generated `release-summary.md` gives the publisher copy-ready values and paths without requiring them to inspect JSON. It includes version, commit, digest, automated-test summary, acceptance summary, exact content directory, exact listing files, preview metadata, Uploader tags/DLC selections declared by the profile, and any non-blocking warnings.

No documentation tells users to run the deleted PowerShell, batch, or Bash scripts after migration.

## 23. Planned repository/configuration changes

The following are design-approved intentions, not authorization to edit configuration. Repository policy requires a separate, exact approval before these changes are implemented.

| File | Intended change | Behavioral/pipeline impact |
|---|---|---|
| `global.json` | Add SDK `10.0.400`, `rollForward: latestPatch`, and `allowPrerelease: false` | Makes local and optional CI tool builds use stable `10.0.4xx` servicing releases only |
| `tools/oni-mod-pipeline/OniModPipeline.slnx` | Add solution containing production and test projects | Establishes the generic pipeline build/test entry point |
| `tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj` | Add .NET 10 CLI project and locked package policy | Creates the local cross-platform executable |
| `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj` | Add .NET test project | Replaces hand-written shell testing for generic behavior |
| `tools/oni-mod-pipeline/src/OniModPipeline/packages.lock.json` and `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/packages.lock.json` | Commit resolved tool dependency closures | Enables locked, repeatable tool restore |
| `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml` | Add the versioned mod profile | Supplies portable mod-specific facts to the generic tool |
| `mods/delivery-temperature-limit-supercooled/STEAM_CHANGE_NOTES.bbcode` | Add the required, tracked release-specific change-note source | Makes patch notes reviewable and gives every candidate an explicit Uploader input |
| `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj` | Complete the existing SDK-style modernization: retain `net48`; remove redundant output/configuration/base-framework declarations and source-owned release `AssemblyVersion`; rename `GameFolder` to `OniManagedAssemblyDirectory`; accept pipeline-supplied version/build/output properties; rely on the SDK's implicit reference-assemblies package; enable lock-file generation; mark build-only package assets private; import the merge target explicitly | Produces a concise, portable SDK-style project without changing ONI's runtime target or gameplay behavior |
| `mods/delivery-temperature-limit-supercooled/Source/packages.lock.json` | Commit the resolved mod-build dependency closure | Enables locked, repeatable mod restore |
| `mods/delivery-temperature-limit-supercooled/Source/ILRepack.targets` | Remove the PowerShell metadata rewrite and legacy XML namespace; consume `$(TargetPath)`, `$(OniManagedAssemblyDirectory)`, and the explicit merged-mod output property; stop writing to the source root | Modernizes the MSBuild target, eliminates source mutation, and directs merge output to the pipeline build result |
| `mods/delivery-temperature-limit-supercooled/Source/Directory.Build.props` | Delete after `DeliveryTemperatureLimit.csproj` validates the required pipeline-supplied `OniManagedAssemblyDirectory` property | Removes the machine-specific Steam fallback instead of preserving an otherwise empty compatibility file |
| `mods/delivery-temperature-limit-supercooled/Tests/ModInfoVersion.Tests.ps1` | Delete after C# parity/regression coverage is green | Removes PowerShell-only testing |
| `mods/delivery-temperature-limit-supercooled/Tests/BuildingsEligibility.Tests.ps1` | Delete after C# parity/regression coverage is green | Removes dynamic PowerShell source compilation |
| `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`, C# test sources, and `Tests/packages.lock.json` | Add the conventional mod-specific regression-test project and locked dependency closure | Replaces both PowerShell test harnesses with portable, discoverable TRX-producing tests |
| `mods/delivery-temperature-limit-supercooled/scripts/deploy_mod_locally.bat` | Delete after guarded `install` parity is verified | Removes Windows batch/PowerShell deployment |
| `mods/delivery-temperature-limit-supercooled/Source/build.sh` | Delete after `build` and `prepare-release` parity is verified | Removes Bash-specific build and mutation logic |
| `README.md` | Document the new commands and manual publication gate | Establishes one supported workflow |

The existing `.gitignore` already excludes `artifacts/`; no change is required merely to add release candidates. Any later configuration change not listed here requires its own explanation and approval.

The tracked root `DeliveryTemperatureLimit.dll` is treated as a legacy artifact during migration. The new pipeline neither reads nor overwrites it. Removing it from source control is a separate material deletion requiring explicit approval after candidate parity has been demonstrated.

## 24. Migration strategy

The implementation plan should order work so every replacement proves parity before legacy removal:

1. Add the .NET 10 tool/test skeleton and versioned mod profile after exact configuration approval.
2. Implement manifest loading, path safety, diagnostics, and temporary-filesystem tests.
3. Implement environment discovery and validate against the installed ONI game.
4. Implement C# regression tests equivalent to both PowerShell tests and watch them fail/pass through test-driven development.
5. Capture the current evaluated project, public assembly surface, reference closure, and deployed runtime-package baseline before changing project configuration.
6. Complete the SDK-style `.csproj` and MSBuild-target modernization, implement isolated build, and prove baseline parity plus zero source mutation.
7. Implement guarded local installation and compare its runtime package with the existing manual deployment.
8. Implement Workshop package allowlisting, listing validation, CRLF rendering, hashing, and evidence.
9. Implement release preparation, acceptance recording, and readiness verification.
10. Run the full lifecycle against Delivery Temperature Limit, including in-game Storage Tile/rocket acceptance and direct CRLF-file paste into the real Uploader without publishing.
11. Remove the old PowerShell, batch, Bash, and post-build mutation paths in the same migration only after parity evidence is green.
12. Update documentation and perform a final clean-repository candidate rehearsal.

This is sequencing guidance, not the task-by-task implementation plan. The implementation plan is created only after this written specification is reviewed and approved.

## 25. Acceptance criteria for the pipeline

The architecture is successfully implemented when all of the following are true:

1. A fresh local checkout with the pinned .NET 10 SDK can restore the tool in locked mode.
2. `oni-mod-pipeline diagnose` identifies or clearly requests the local ONI paths on every supported operating system.
3. `oni-mod-pipeline validate` finds malformed metadata, unsafe paths, invalid package mappings, oversized listing text, and missing game references without changing files.
4. `DeliveryTemperatureLimit.csproj` remains SDK-style `net48` but contains no redundant legacy output/configuration/base-framework declarations, source-owned release version, or machine-specific game path; locked restore and explicit non-framework references remain.
5. The modernized project and merge target reproduce the captured public assembly surface and Workshop runtime inventory except for approved version/provenance metadata and artifact paths.
6. `oni-mod-pipeline build` produces the merged mod DLL beneath `artifacts/` and leaves every tracked source byte unchanged.
7. All former PowerShell regression behavior is covered by passing C# tests.
8. No supported ONI development workflow invokes PowerShell, batch, or Bash.
9. `install` places an exact, manifest-verified candidate in a guarded `dev` or `local` directory.
10. `prepare-release` produces the documented directory contract or no final candidate at all.
11. `workshop-content/` contains only declared runtime files and no game-managed assemblies, source, scripts, tests, logs, or evidence.
12. Every release-content file and `release-content-manifest.json` have verified SHA-256 values.
13. Description and change-note source logical digests equal their generated-artifact logical digests.
14. Generated listing text is UTF-8 without BOM, CRLF-only, and ends with exactly one CRLF.
15. Copying the generated description file into the actual ONI Uploader preserves all intentional line structure.
16. Acceptance cannot be recorded without an installation receipt for the same content digest, live installed-byte verification, and the immutable acceptance-plan hash.
17. Tampering with any release-content byte invalidates installation/acceptance evidence and prevents `ready-for-upload`.
18. A passing candidate includes TRX results, provenance, an immutable acceptance plan, acceptance results, summary, and checklist.
19. `verify-release` is repeatable and returns success only when every required condition is satisfied.
20. No command stores Steam credentials, opens an authenticated publication flow, or submits a Workshop update.
21. The final documented action is a human review and click in Klei's ONI Uploader.

## 26. Rejected alternatives

### 26.1 PowerShell as the orchestrator

Rejected because it couples the normal workflow to Windows, has already caused a line-ending mutation defect, and would require parallel implementations for other environments.

### 26.2 Python as the orchestrator

Rejected because a separately provisioned Python runtime and dependency environment add another toolchain beside the C#/.NET mod ecosystem. .NET 10 provides the required cross-platform filesystem, process, hashing, JSON, and test capabilities directly.

### 26.3 .NET 8

Rejected because the pipeline is a new long-lived tool and .NET 10 is the current approved LTS runtime for this design. The mod's game framework remains independent.

### 26.4 Mandatory hosted CI

Rejected because correctness must not depend on an external vendor, account, runner image, or network service. Hosted CI may invoke the same commands later without owning the workflow.

### 26.5 Automatic Steam publication

Rejected because authentication, irreversible submission, legal/account context, and final visual review belong at an explicit human gate. Steam documents `SubmitItemUpdate` as the operation that initiates upload and notes that the update cannot then be cancelled.

### 26.6 Uploading from the local-mod directory

Rejected because it is mutable and can accumulate undeclared or stale files. The Uploader receives only a clean candidate content directory.

### 26.7 ZIP as the primary handoff

Rejected because Klei's Uploader selects a directory and Steam advises providing content files rather than combining them into one archive for this interface.

### 26.8 Changing tracked BBCode to CRLF

Rejected because repository source should keep one LF policy. CRLF is an external Uploader-handoff representation generated under `artifacts/`, not a source-control exception.

### 26.9 Adding `[br]` to preserve lines

Rejected because it changes logical Workshop content, complicates list/heading semantics, and treats a byte-representation defect as markup.

### 26.10 Clipboard automation in v1

Deferred because the generated CRLF file is simpler and fully cross-platform. A clipboard command is justified only by a failed direct-copy acceptance test.

### 26.11 Arbitrary shell hooks

Rejected because they would undermine the no-shim decision, portability, path safety, and provenance. New generic behavior belongs in a named module or versioned manifest concept.

### 26.12 Plugin architecture for one mod

Rejected because one adapter is a hypothetical seam. The declarative profile and normal C# test projects cover the currently known variation.

## 27. Final decision

Build ONI Mod Pipeline as a local-first .NET 10 CLI with a versioned per-mod declarative profile. Keep generic orchestration independent of Delivery Temperature Limit while expressing that mod's files, tests, packaging, and acceptance cases in its profile. Complete the mod's existing SDK-style `.csproj` modernization without retargeting it from `net48`, and prove its build/package behavior against a captured baseline. Replace, rather than wrap, the existing PowerShell, batch, Bash, and post-build mutation workflows. Produce immutable, hashed release content and digest-bound evidence, ending in `ready-for-upload`. Generate Workshop listing text as UTF-8/CRLF handoff artifacts while preserving UTF-8/LF tracked sources. Leave the authenticated ONI Uploader's final **Publish** action to a human.

No open design questions remain in this specification.
