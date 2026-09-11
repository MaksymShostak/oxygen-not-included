# Packaged BBCode consumer in ONI Mod Pipeline

## Accepted intent and release boundary

The owner approved this design in the migration conversation on 11 September 2026: ONI Mod Pipeline must consume the actual Steam Community BBCode package, convert its generated Workshop description to GFM, and synchronize a marked description block in the mod repository README. This adds a consumer acceptance gate to the extraction plan; it supersedes that plan's exclusion of ONI consumer integration for this specific workflow only.

The release candidate is not accepted solely because converter tests pass. Before publication, the real pipeline must consume the exact retained tarball. After publication, repeat with the exact registry coordinate and integrity. Preserve the distinction between candidate-consumer success and public-registry consumer success, following owlapi's pre-registry/public-registry consumer separation.

The existing ONI checkout has owner edits to README.md and STEAM_DESCRIPTION.bbcode. Work in the isolated feature/bbcode-readme-consumer worktree based on main 975acf599d06ec3d274c55bac8d1731278ffa153. Do not overwrite or import those edits into commits without separate authority. Read-only snapshots may supply realistic qualification inputs and retain their hashes.

## Native integration

Use the existing .NET ExternalProcessRunner, Node and the installed package's declared public bin entry. Read package.json with System.Text.Json; require the intended package name and resolve its bin inside that installed package. Do not import a converter checkout or hard-code a private implementation entry path. Do not add an embedded JavaScript conversion bridge, parser, fallback converter or NuGet dependency.

Invoke the public CLI with to-gfm, --format=json, --profile=workshop-item and --fail-on=lossy. Consume its value and diagnostics only after a successful exit; report approximate transformations for review. Preserve CLI status and diagnostics in qualification evidence. No installation, download or npm credential access happens during synchronization or release checks.

Reuse WorkshopListingValidator/ListingTextRenderer to produce the same BOM-free UTF-8, CRLF description bytes used in Workshop listing assembly. Pass the generated file to the CLI, not the original source or a hand-converted substitute. Verify correspondence with WorkshopListingAssembler output in integration tests.

## README ownership and lifecycle

Add an optional profile table [readme] with repository-path, relative to the discovered Git worktree root. For Delivery Temperature Limit the value is README.md. Profiles without this table retain existing behavior and require no converter installation.

The managed block is delimited by exactly one pair of standalone markers:

```text
<!-- oni-mod-pipeline:workshop-description:start -->
<!-- oni-mod-pipeline:workshop-description:end -->
```

Initially place these around the existing mod-description portion, ending before the Support and contributing section. All surrounding bytes, including support, translations and development guidance, remain authoritative and must be preserved. Missing, repeated or reversed markers fail without writing. Preserve the README's existing newline convention outside and within the generated block; do not normalize unrelated bytes. Resolve the configured path inside the repository and reject linked or escaping destinations.

Introduce sync-readme with --check. Ordinary synchronization replaces only the managed content after conversion succeeds. --check reports drift without writing. Detect a changed README before replacement and retain the original on conversion, parsing, write or cancellation failures. Repeating synchronization must produce no change.

Run synchronization before reviewing and committing release inputs. validate --for-release and prepare-release check the configured README without rewriting it. Include the configured README and npm dependency declarations in contributing-source cleanliness checks. Release candidate directories and frozen source remain immutable.

## Configuration approval boundary

The design approval does not manufacture exact configuration approval. The proposed configuration changes are root package.json devDependencies.steam-community-bbcode = 1.0.0-rc.1, its native npm lock update after registry availability, and [readme] repository-path = README.md in the Delivery Temperature Limit profile. Existing dependencies, .NET configuration and licence boundaries remain unchanged.

For pre-publication qualification, install the approved exact tarball into an ignored isolated consumer directory using native npm with lifecycle scripts disabled. Retain its generated package manifest/lock and archive integrity as local evidence. An explicit converter-package directory parameter permits this normal installed-package location; default discovery uses repository node_modules/steam-community-bbcode. No machine-specific tarball path enters the committed dependency lock. The permanent registry dependency and lock are installed only once the exact coordinate exists.

## Acceptance criteria

- AC-001: An opted-in mod uses the actual installed public CLI and generated Workshop-description bytes; no converter source checkout is reachable through the integration.
- AC-002: A representative description produces independently reviewed GFM text, headings, lists, links and images and is inspected through GitHub rendering. A successful process exit or round trip alone is insufficient.
- AC-003: Synchronization changes only the marked block; repeated synchronization is byte-stable, and --check is read-only and fails on drift.
- AC-004: Missing runtime/package, nonzero converter exit, malformed result, unsupported/lossy content, invalid markers, unsafe target and concurrent target changes cannot modify the README.
- AC-005: Release validation rejects stale or dirty configured documentation without changing source or existing candidate files. Non-opted-in profiles keep their previous behavior.
- AC-006: Exact tarball consumer qualification precedes publication; exact registry/integrity consumer qualification follows bootstrap and precedes RC acceptance. Capture source revisions, input/output/archive hashes, commands and actual results.
- AC-007: Existing root MIT/Klei terms and external package AGPL terms retain their separate boundaries; no converter code is copied into ONI.

## Evidence and research

Existing implementation inspected: WorkshopListingAssembler, WorkshopListingValidator, ListingTextRenderer, ExternalProcessRunner, CliApplication, RelevantSourceSet and the native profile parser. The public package CLI already supplies structured output, fidelity policy and exit codes. The custom gap is pipeline orchestration, bounded README ownership and consumer evidence, not conversion.

References: [npm bin contract](https://docs.npmjs.com/cli/v12/configuring-npm/package-json/#bin), [native npm installation](https://docs.npmjs.com/cli/v12/commands/npm-install/), and owlapi docs/plans/phase19d-pre-registry-consumer-decoupling.md at 2ac41c94e6630ca47ce110a484ec9af3b0b1f335. Registry authentication and publication remain separate owner actions.
