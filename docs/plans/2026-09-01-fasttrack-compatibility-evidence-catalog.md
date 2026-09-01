# FastTrack Compatibility Evidence Implementation Plan

> **For agentic workers:** REQUIRED SKILLS: use `test-driven-development` for
> every behavior change and `committing-to-git` at the authorized commit
> checkpoint. Execute inline in the current task; do not delegate this plan.

**Goal:** Make supported FastTrack compatibility hermetic by preserving exact
content-addressed DLLs, enforcing fixture-to-production closure, admitting the
verified `0.18.4.0` and `0.18.5.0` identities, and replacing version-only
runtime admission with exact version-plus-SHA-256 matching.

**Architecture:** Supported FastTrack DLL evidence lives under file-version and
full-digest directories. A narrow test-only expectation table drives static
PE/IL verification and has exact closure with the production catalog and
fixture tree. A provider-local immutable production catalog admits exact build
identities. General provenance and incompatible-fixture shapes remain deferred,
non-operational stubs until a concrete need justifies activating them.

**Proportionality rule:** Use authoritative tools to execute and validate their
own formats; write repository code only for project-specific policy and
cross-artifact invariants those tools cannot know. Do not operate a generic
fixture-manifest parser, YAML parser, or incompatible-build catalog for the two
currently supported DLLs.

**Tech stack:** C# with nullable reference types; .NET Standard 2.1 production
source linked into MSTest on .NET 10; `System.Reflection.Metadata`;
`System.Reflection.PortableExecutable`; `System.Security.Cryptography`; Git
binary attributes; repository-owned pipeline CLI.

**Spec:**
`docs/specs/2026-09-01-external-mod-compatibility-evidence-design.md`

## Global constraints

- Treat the working tree as authoritative. Preserve existing integration work
  and the user-owned untracked root `AGENTS.md`.
- Do not use the `verification-before-completion` skill.
- Use strict red-green-refactor ordering for behavior changes.
- Do not load, reference, resolve, or execute either fixture assembly. Read it
  only as bytes through `PEReader` and `MetadataReader`.
- Do not use a mutable remote URL as a build or test input. Network access is
  limited to explicit candidate intake.
- Do not add a package, target-framework change, lockfile change, source-project
  reference, Git LFS rule, submodule, CI workflow, or pipeline setting.
- Do not modify `oni-mod-pipeline.toml`, either `packages.lock.json`,
  `Source/DeliveryTemperatureLimit.csproj`, the mod's own `mod.yaml` or
  `mod_info.yaml`, or Workshop metadata.
- Keep `FastTrackAssemblyBuildIdentity` and
  `FastTrackSupportedAssemblyBuildCatalog` inside
  `FastTrackCompatibility/FeatureContractVerification`. Generic activation
  core code must not reference them.
- Match support by exact file version and complete DLL SHA-256. No range,
  prefix, fallback, alias, shim, or version-only overload is permitted.
- Preserve the `0.18.4.0` DLL exactly at SHA-256
  `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`.
- Admit `0.18.5.0` only at SHA-256
  `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B`.
- The root `.gitattributes` DLL rule and test-project fixture-copy wildcard have
  exact approval. No other configuration edit is authorized by this plan.
- Before the non-trivial implementation commit, state
  `Implementation complete; /review pending`, run built-in review over the exact
  milestone, and resolve or explicitly defer every confirmed P0-P2 finding.
- Use the authorized commit message in Task 5. Do not push.

## File and responsibility map

| File | Responsibility |
|---|---|
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackAssemblyBuildIdentity.cs` | Validated exact file-version and DLL-digest identity |
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackSupportedAssemblyBuildCatalog.cs` | Immutable provider-local support policy |
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityInspector.cs` | Active ownership, exact admission, and deep loaded-assembly verification |
| `Tests/FastTrackCompatibility/FastTrackSupportedBuildFixtureExpectation.cs` | Independent expected PE identity and feature contract for each supported fixture |
| `Tests/FastTrackCompatibility/FastTrackSupportedBuildFixtureExpectationTests.cs` | Catalog/expectation/tree closed-world contracts |
| `Tests/FastTrackCompatibility/PreservedFastTrackAssemblyContractTests.cs` | Data-driven static metadata, signature, IL, and no-load contracts |
| `Tests/ExternalModCompatibility/DeferredFixtureCatalog/*.cs` | Non-operational future serialized-shape stubs |
| `Tests/ExternalModCompatibility/DeferredFixtureCatalog/README.md` | Activation criteria and authoritative-tool boundary for the stubs |
| `Tests/Fixtures/ThirdParty/FastTrack/<version>/sha256-<digest>/` | Exact inert DLL, human origin note, and upstream license |
| `.gitattributes` | Binary Git treatment for third-party fixture DLLs only |
| `Tests/DeliveryTemperatureLimit.Tests.csproj` | Copies the fixture tree to test output strictly as data |

## Task 1: Add exact production build identities and support policy

**Files:** identity, catalog, and focused catalog tests named above.

- [x] **Step 1: Write identity and catalog tests first**

Cover canonical uppercase SHA-256, null/malformed input, value equality,
deterministic immutable enumeration, duplicate rejection, exact-pair matching,
and non-throwing rejection of malformed observed digests.

- [x] **Step 2: Run the focused test and record compile-red**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackSupportedAssemblyBuildCatalogTests
```

Expected red: both production types are absent.

- [x] **Step 3: Implement the minimal immutable identity and catalog**

Copy and sort input identities. Compare the exact version-and-digest pair. Keep
all file, fixture, network, Harmony, Unity, and generic activation knowledge out
of the catalog.

- [x] **Step 4: Make the focused tests green**

Declare the exact `0.18.4.0` identity initially. Add the second identity only
after its static matrix reproduces the candidate probe in Task 3.

## Task 2: Establish proportional supported-fixture evidence and preserve future shapes as stubs

**Files:** the supported-build expectation and test plus the deferred fixture
catalog directory named in the file map.

- [x] **Step 1: Write the narrow closed-world acceptance tests first**

Require exact identity closure between
`FastTrackSupportedAssemblyBuildCatalog.Declared.Builds` and
`FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures`. Enumerate the
copied fixture tree and require exactly these files for every supported build:

```text
FastTrack.dll
README.md
UPSTREAM-LICENSE.txt
```

Expected initial red: `FastTrackSupportedBuildFixtureExpectation` does not
exist.

- [x] **Step 2: Implement the supported-fixture expectation table**

For each admitted identity, store only independent facts the PE/IL matrix uses:

- exact version and DLL digest;
- expected assembly version and module version identifier; and
- explicit presence or absence for world inventory, pickup grouping, and
  direct delivery.

Derive the content-addressed relative directory from the exact identity. Do not
introduce a generic loader or manifest abstraction.

- [x] **Step 3: Reduce the active tree to compatibility-relevant evidence**

Keep each exact DLL, README, and license. Remove active
`fixture-provenance.json`, `mod.yaml`, and `mod_info.yaml` files. Preserve known
archive/member hashes, observation facts, and package metadata values in the
README without treating prose as a machine authority.

This resolves the line-ending hazard for hash-pinned YAML without expanding
Git configuration and keeps the active objective centered on the DLL consumed
by runtime admission and static contracts.

- [x] **Step 4: Preserve future concepts as honest stubs**

Retain provider-neutral serialized shapes for provenance and an incompatibility
decision under `DeferredFixtureCatalog`. Add no loader, validator, test fixture,
or production reference. Document that activation requires a concrete retained
incompatible build or a second integration, starts with TDD, rejects ambiguous
JSON, and delegates package formats to authoritative consumers.

- [x] **Step 5: Run the narrow closure tests**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackSupportedBuildFixtureExpectationTests
```

Expected: 2 passed, zero skipped or inconclusive.

## Task 3: Preserve both exact DLLs and run the static PE/IL matrix

**Files:** two content-addressed fixture directories, the data-driven static
contract test, `.gitattributes`, and the approved test-project wildcard.

- [x] **Step 1: Protect fixture DLL bytes in Git**

Add exactly:

```gitattributes
mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/**/*.dll binary
```

- [x] **Step 2: Copy the complete FastTrack fixture tree as test data**

Replace the singular item with the approved setting:

```xml
<None Update="Fixtures\ThirdParty\FastTrack\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

- [x] **Step 3: Preserve `0.18.4.0` without changing its bytes**

Require the exact DLL digest, assembly version `0.18.0.0`, file version
`0.18.4.0`, and MVID `b1e31127-5b91-4607-b5b5-8ea255bd5288`. Record the known
origin limits and license source revision in the README. Do not synthesize
missing package metadata.

- [x] **Step 4: Acquire and probe `0.18.5.0` outside the fixture root**

Hash the archive before extraction and use authoritative archive tooling to
inspect it. Require the observed archive/member identities in the spec. Read
the DLL using platform PE metadata APIs. Reject a mismatch rather than updating
the accepted observation in place.

- [x] **Step 5: Refactor the singular contract into a data-driven matrix**

Reuse the existing `PEReader`/`MetadataReader` implementation and all
world-inventory, pickup-grouping, direct-delivery-absence, and no-load checks.
Drive every case from `FastTrackSupportedBuildFixtureExpectation`. Compare
actual bytes and PE metadata directly with independent expected values.

- [x] **Step 6: Reproduce the candidate probe before admission**

Run the matrix for `0.18.5.0`, then add its exact identity to the production
catalog and its expectation to the final two-row matrix only after it passes.

- [x] **Step 7: Run the final matrix**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~PreservedFastTrackAssemblyContractTests
```

Expected: 12 passed, covering six contracts for each exact DLL, with zero
skipped or inconclusive.

## Task 4: Replace runtime version-only admission with exact catalog matching

**Files:** the inspector, failure code, production composition, and associated
inspector, inactive-path, and architecture tests.

- [x] **Step 1: Rewrite runtime-admission tests first**

Cover exact matching, known-version/different-digest, unknown-version,
known-digest/different-version, digest case normalization, malformed observed
digest, one identity read, bounded diagnostics, and feature-local inactive
behavior.

- [x] **Step 2: Record the intended compile-red state**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTrackCompatibilityInspectorTests|FullyQualifiedName~FastTrackInactivePathArchitectureContractTests"
```

Expected red: the inspector lacks the catalog parameter and
`UnsupportedAssemblyBuild` does not exist.

- [x] **Step 3: Implement exact admission before structural verification**

Use this order independently per feature:

```text
replacement inactive -> ReplacementInactive
identity unavailable -> AssemblyFileIdentityUnavailable
exact pair absent -> UnsupportedAssemblyBuild
structural violation -> feature-specific contract violation
verified -> Ready
```

- [x] **Step 4: Update composition without a compatibility overload**

Production passes `FastTrackSupportedAssemblyBuildCatalog.Declared`. Tests pass
explicit catalogs. Do not add an optional parameter, default fallback, obsolete
overload, or adapter shim.

- [x] **Step 5: Keep architecture checks bounded**

Require the inspector to use the catalog, read physical identity once, contain
no accepted build literal, and keep the catalog out of
`GameplayActivation/Core`. Do not grow speculative source-text policy scans
beyond these architectural seams.

- [x] **Step 6: Run focused runtime tests**

Run the Step 2 command and exact inspector architecture contract. Expected: all
exact-admission and inactive-path behaviors pass.

## Task 5: Reconcile, verify, review, and commit the milestone

**Files:** this plan, the evidence spec, the safe-activation program, the
declared-integration plan, and every implementation/fixture file above.

- [x] **Step 1: Replace the obsolete mutable-download prerequisite**

The declared-integration continuation requires this exact catalog-backed,
content-addressed matrix. It does not reacquire `0.18.4.0` from the mutable URL.

- [x] **Step 2: Record program order and configuration boundaries**

```text
1. Declared integration foundation, Tasks 1-5
2. FastTrack compatibility evidence
3. Declared integration foundation, Tasks 6-10
4. Pure activation core
5. Activation failure response
6. Harmony/Klei integration and release evidence
```

Permit only the approved linked activation-core compile item, exact fixture
copy wildcard, and scoped `.gitattributes` DLL rule. Forbid all other
configuration changes.

- [x] **Step 3: Apply the proportionality correction after initial review**

The first review exposed hazards created by active speculative formats: Git
line-ending normalization of hash-pinned YAML, ambiguous duplicate JSON
properties, package semantics without an authoritative consumer,
overgeneralized unavailable-fact validation, and a future incompatible fixture
path without mandatory PE identity checks.

Remove those formats and validators from the active path. Preserve their
concepts as deferred stubs. Keep the exact DLL matrix, human provenance and
license evidence, production catalog, and runtime gate.

- [x] **Step 4: Run focused verification after the correction**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTrackSupportedAssemblyBuildCatalogTests|FullyQualifiedName~FastTrackSupportedBuildFixtureExpectationTests|FullyQualifiedName~PreservedFastTrackAssemblyContractTests|FullyQualifiedName~FastTrackCompatibilityInspectorTests|FullyQualifiedName~FastTrackInactivePathArchitectureContractTests"
```

- [x] **Step 5: Run authoritative repository verification and boundaries**

```powershell
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
git diff --check
```

Use the pipeline for its own profile and environment. Use MSBuild's evaluated
production item graph and the pipeline build-result inventory—not a repository
configuration parser—to prove no fixture data ships. Require no diff in every
forbidden configuration file.

- [x] **Step 6: Run the formal review gate ourselves**

State exactly:

`Implementation complete; /review pending`

Run built-in uncommitted review scoped to the FastTrack evidence,
content-addressed DLLs, exact runtime admission, deferred stubs, program-plan
amendments, approved configuration changes, and associated tests. Exclude the
user-owned untracked root `AGENTS.md`. Resolve or explicitly defer every
confirmed P0-P2 finding, then rerun affected tests and the authoritative suite.

- [x] **Step 7: Stage only this milestone and inspect the snapshot**

Show `git status --short`, `git diff --stat`, `git diff --cached --check`, and
the staged file list. Exclude root `AGENTS.md` and unrelated changes.

- [x] **Step 8: Create the authorized signed commit**

Load and follow `committing-to-git`, then use:

```text
fix(temperature-limit): Admit only preserved FastTrack builds

Preserve the verified FastTrack 0.18.4.0 and 0.18.5.0 artifacts as
content-addressed, provenance-recorded test evidence and exercise every
admitted build through the static PE and IL contract matrix.

Replace the mutable release reacquisition and version-only runtime gate
with a closed-world supported-build catalog keyed by exact file version
and DLL SHA-256, while retaining feature-specific runtime ownership and
structural verification for active replacements.

Keep third-party assemblies inert, offline, and outside production output,
and classify future upstream changes through an explicit preservation and
admission workflow.
```

- [x] **Step 9: Do not push**

Report the commit ID, subject, signature result, tests, review result, and next
active plan task. Pushing requires separate explicit authorization.
