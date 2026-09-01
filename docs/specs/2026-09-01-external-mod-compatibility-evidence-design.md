# External-Mod Compatibility Evidence Design

- **Status:** Approved architectural direction; implementation pending
- **Date:** 2026-09-01
- **Initial integration:** FastTrack
- **Affected mod:** Delivery Temperature Limit (Supercooled)
- **Primary reliability objective:** Normal builds and tests must retain every artifact needed to verify supported external-mod compatibility without depending on a mutable remote release
- **Runtime safety objective:** An external mod may own a required runtime seam only when its exact assembly build and active structural contract are both verified
- **Maintenance objective:** A changed or unavailable upstream release is an expected candidate-intake outcome, not a surprise that invalidates previously accepted evidence

## 1. Decision

Maintain repository-owned, inert, content-addressed compatibility fixtures for
external-mod builds that Temperature Limit has evaluated. FastTrack is the
first integration governed by this design.

Separate three facts that the previous mutable-download gate conflated:

1. A build was **observed** from an identified upstream source.
2. Its compatibility-relevant artifacts were **preserved** in this repository.
3. Its exact assembly build was **admitted** to production support.

Normal builds and tests consume only preserved repository files. They never
fetch a release, branch, tag, web page, or source repository. A remote release
is only a candidate-acquisition source used during an explicit maintenance
operation.

Production accepts a FastTrack build only by an exact pair of assembly file
version and `FastTrack.dll` SHA-256. This identity gate is necessary but not
sufficient: the loaded game must still pass the existing feature-specific
Harmony ownership, member, signature, and IL-semantic verification before a
generic runtime-authority contribution can be selected.

Admission is additive. A newly accepted build does not replace or silently
retire an older accepted build. Retirement is a separate reviewed decision and
must preserve historical evidence.

## 2. Problem

The active declared-integration plan required re-downloading `FastTrack.zip`
from the mutable `FastTrackBeta` release and comparing it with an archive
digest recorded while version `0.18.4.0` was available. The same URL now serves
version `0.18.5.0` with different bytes. The original archive is no longer
obtainable from that endpoint.

Stopping on the mismatch was correct for that plan: accepting changed bytes as
if they were the reviewed artifact would have fabricated provenance. The gate
nevertheless exposed an architectural defect. A compatibility suite cannot be
hermetic when essential evidence must be reacquired from a mutable remote.

The repository already retains the exact `0.18.4.0` `FastTrack.dll`, but the
fixture is keyed only by version, its surrounding package evidence is
incomplete, and production currently admits a file version without requiring
the already-computed DLL digest to match an accepted build. These limitations
make same-version republishing ambiguous and turn ordinary upstream evolution
into an exceptional planning failure.

## 3. Goals

The implementation must:

1. Keep every normal FastTrack compatibility test offline and deterministic.
2. Preserve the exact DLL bytes used for every compatibility decision.
3. Distinguish upstream observations, retained artifacts, and repository
   support decisions without overstating provenance.
4. Identify preserved DLL builds by content as well as reported version.
5. Permit two different DLLs that report the same file version to coexist.
6. Require an exact production catalog match before deeply inspecting an
   active FastTrack replacement.
7. Retain runtime structural verification after exact build admission.
8. Preserve compatible versions additively until an explicit retirement.
9. Preserve an evaluated incompatible build with an explicit reason when doing
   so is useful for future adaptation.
10. Make an unavailable or changed upstream package irrelevant to previously
    admitted builds and their tests.
11. Keep FastTrack-specific build policy inside the FastTrack adapter boundary.
12. Establish an evidence shape that another declared integration can reuse
    without prematurely imposing FastTrack's runtime policy on every mod.
13. Retain third-party license and origin information next to preserved bytes.
14. Reject malformed, inconsistent, unclassified, or orphaned fixtures.
15. Preserve the existing no-load rule: fixture assemblies are PE data and are
    never referenced, loaded, resolved, or executed by the test process.

## 4. Non-goals

This change will not:

- mirror the complete ONIMods repository;
- add a Git submodule, Git LFS dependency, artifact server, package feed, or
  network-backed test cache;
- download external mods during build, test, packaging, or ordinary CI;
- automatically poll upstream releases or open update pull requests;
- trust a version string, release label, mutable URL, or source revision as a
  substitute for the retained DLL digest;
- execute a preserved external-mod assembly;
- accept an unknown build merely because runtime reflection appears promising;
- weaken or remove existing Harmony ownership and IL-semantic contracts;
- add external-mod build identities to provider-neutral activation core types;
- create one universal runtime compatibility policy for all future mods;
- claim that repository-authored provenance is a SLSA provenance attestation;
- claim that reconstructed `0.18.4.0` metadata was extracted from an archive
  that is no longer available; or
- retire a previously supported build as part of admitting a new one.

## 5. Source-grounded terminology

### 5.1 External-mod compatibility fixture

An **external-mod compatibility fixture** is an inert repository-owned bundle
of exact files used to evaluate one external-mod assembly build. It is test
evidence, not a compile reference, runtime dependency, redistributed game mod,
or supported-build declaration.

### 5.2 Assembly build identity

A **FastTrack assembly build identity** is exactly:

- the assembly file version reported for `FastTrack.dll`; and
- the SHA-256 digest of the complete `FastTrack.dll` file.

The term does not designate an archive, source commit, release tag, semantic
version range, or API compatibility level.

### 5.3 Preserved build

A **preserved build** has a complete repository fixture whose directory
identity, manifest identity, and computed file digests agree. Preservation
does not itself mean production support.

### 5.4 Supported build

A **supported build** is a preserved build whose exact assembly build identity
occurs once in `FastTrackSupportedAssemblyBuildCatalog` and whose applicable
static contracts pass.

### 5.5 Incompatible preserved build

An **incompatible preserved build** is a complete fixture absent from the
production supported-build catalog and accompanied by exactly one explicit
`incompatibility-record.json`. The record names the failed compatibility
contracts and why the build is not admitted. It is not a runtime denial list;
absence from the supported-build catalog is sufficient to fail closed.

### 5.6 Candidate acquisition

**Candidate acquisition** is the explicit, network-dependent maintenance act
of obtaining and examining a possible new external-mod build. It is outside
normal build and test execution. Acquisition failure changes no accepted
fixture or production declaration.

## 6. Architectural boundaries

| Boundary | Owns | Must not own |
|---|---|---|
| `Tests/Fixtures/ThirdParty` | Retained external bytes, metadata evidence, provenance facts, license text | Runtime support policy, network acquisition logic |
| FastTrack fixture tests | Manifest integrity, safe PE inspection, fixture-to-catalog closure, static FastTrack contracts | Assembly loading or runtime Harmony state |
| `FastTrackCompatibility` | FastTrack assembly build identity, supported-build catalog, exact identity match, provider-specific structural verification | Generic selection policy or support-document shape |
| `GameplayActivation/Core/ExternalModIntegration` | Provider-neutral declarations, outcomes, capabilities, authority observations, deterministic selection | FastTrack names, versions, digests, fixture paths |
| `RuntimePatchInstallation` | Composition and generic selected-authority enforcement | Fixture discovery or provider-specific compatibility branching |
| Support reporting | Bounded generic observed facts and capability outcomes | Support admission decisions or fixture parsing |

The test fixture manifest may use provider-neutral evidence field names so a
later integration can reuse the format. Production does not gain a generic
artifact-policy framework until a second integration demonstrates the common
runtime abstraction.

## 7. Content-addressed fixture layout

Each exact FastTrack DLL build is stored under its reported file version and
full lowercase SHA-256:

```text
Tests/Fixtures/ThirdParty/FastTrack/
  0.18.4.0/
    sha256-d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add/
      FastTrack.dll
      mod.yaml                       # retained only when exact evidence exists
      mod_info.yaml                  # retained only when exact evidence exists
      fixture-provenance.json
      UPSTREAM-LICENSE.txt
      README.md
      incompatibility-record.json    # present only when not supported
```

The full digest is used rather than a prefix. A file version is retained as a
human-navigable grouping, while the digest prevents a same-version republish
from overwriting or impersonating earlier evidence.

`FastTrack.dll` is the runtime-verifiable compatibility artifact and therefore
the content-address key. The source archive digest remains provenance evidence
when known, but the archive is not required for normal tests and is not the
runtime build identity.

The fixture directory contains no `pending` marker. Every committed fixture is
classified atomically as supported by the production catalog or incompatible
by an explicit record.

## 8. Fixture provenance

`fixture-provenance.json` is a repository-specific evidence manifest. Its name
and content must not imply a cryptographic attestation by upstream, a SLSA
builder, or another authority.

The manifest records:

- a schema version;
- the declared integration identifier;
- upstream project and candidate source URI;
- observation time when actually known;
- archive digest when the archive was available for hashing;
- the relevant upstream source revision when independently identified;
- assembly name, assembly version, file version, module version identifier,
  and DLL SHA-256 read from the retained PE file;
- every retained artifact path and SHA-256;
- the evidence origin of each retained artifact;
- the original archive-member path when directly observed; and
- facts that are unavailable, including why they cannot be recovered.

Evidence origin is explicit per file. Valid origins include an exact retained
release-archive member, a previously extracted release-archive member whose
recorded archive is unavailable, and a file recovered from an immutable source
revision. These categories must not be presented as equivalent. Package
metadata files are optional when they were not retained and cannot be recovered
accurately; the manifest must name each missing fact instead of synthesizing a
replacement file.

### 8.1 Existing `0.18.4.0` evidence

The existing DLL remains byte-for-byte unchanged. Its known facts are retained:

- DLL SHA-256
  `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`;
- assembly version `0.18.0.0`;
- file version `0.18.4.0`;
- module version identifier `b1e31127-5b91-4607-b5b5-8ea255bd5288`;
- previously recorded archive SHA-256
  `8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D`;
- previously reviewed closest upstream source revision
  `e24e8f3082a52785e971943a8f1fff8de0ca8dff`; and
- upstream static mod ID `PeterHan.FastTrack`.

The manifest states that the original archive is no longer available from the
mutable release endpoint. Inspection of revision
`e24e8f3082a52785e971943a8f1fff8de0ca8dff` established that it contains
neither `FastTrack/mod.yaml` nor `FastTrack/mod_info.yaml`. Those two exact
package files are therefore omitted and recorded as unavailable facts. The
license text recovered from that revision is labeled source-revision evidence
and is not claimed to be an unavailable archive member.

### 8.2 Current `0.18.5.0` observation

The current candidate is acquired once and its archive digest is recorded
before extraction. Its retained members can therefore carry direct
archive-member evidence, including their exact member paths and individual
hashes. The isolated pre-admission observation established:

- archive SHA-256
  `3ED47A89966B3780DD4C8855DA20B6335B642AA15A92143DA749FBC3621F5211`;
- `FastTrack/FastTrack.dll` SHA-256
  `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B`;
- assembly version `0.18.0.0`;
- file version `0.18.5.0`;
- module version identifier `bb4e7a11-4985-4d8f-b1c9-f497c6bb3d1e`;
- `FastTrack/mod.yaml` SHA-256
  `32576251B1A57027DF93F47748679650E4916AD8F4E7F872C39C5D12E98EC20E`;
- `FastTrack/mod_info.yaml` SHA-256
  `7CAAF5D05ECC1AD5B362E52616A179519B935BF4204F9E2677A6CF02AADEAB5D`;
- packaged static ID `PeterHan.FastTrack`; and
- packaged version `0.18.5.0`.

The existing world-inventory, pickup-grouping, direct-delivery-absence, and
no-load static contract tests all passed when run against this candidate. The
only failure was the expected singular-fixture identity assertion, which still
required the `0.18.4.0` digest. The implementation therefore admits
`0.18.5.0` after reproducing these results through the permanent fixture
matrix and closed-world catalog tests; this pre-admission probe is evidence,
not a replacement for those gates.

## 9. Candidate acquisition and classification

Candidate intake follows this order:

1. Acquire the candidate into temporary storage outside the fixture catalog.
2. Compute its complete archive SHA-256 before extraction.
3. Enumerate archive entries and reject absolute paths, parent traversal,
   unsafe link-like entries, duplicate normalized paths, or unreasonable
   compatibility-artifact sizes.
4. Require one unambiguous `FastTrack.dll`, `mod.yaml`, and `mod_info.yaml` in
   the expected package boundary.
5. Extract only the declared evidence set into temporary storage.
6. Compute the SHA-256 of every retained file.
7. Read the DLL through `PEReader` and `MetadataReader`; never load it.
8. Confirm assembly name, assembly version, file version, module version
   identifier, package static ID, and manifest facts.
9. Materialize the content-addressed fixture and provenance manifest.
10. Run fixture-integrity and static compatibility tests.
11. If compatible, add the exact build to the production catalog and leave no
    incompatibility record.
12. If incompatible and preservation is useful, add one bounded
    incompatibility record and no production entry.
13. Commit the fixture, classification, tests, and production declaration as
    one coherent change.

No step rewrites an existing content-addressed fixture. Encountering an
existing directory with different retained bytes is an integrity failure.

## 10. Acquisition and evidence failure semantics

| Condition | Required behavior |
|---|---|
| Remote URL unavailable | Candidate intake stops; existing builds and tests are unaffected |
| Remote URL serves changed bytes | Record a distinct observation; never replace prior evidence |
| Same file version, different DLL digest | Create a separate content-addressed build and compatibility decision |
| Same DLL digest, different archive metadata | Retain the assembly build once and record only provenance that can be represented without ambiguity |
| Unsafe or malformed archive | Reject before fixture extraction |
| Wrong static mod ID | Do not admit as FastTrack; preserve only with an explicit incompatibility reason if useful |
| Manifest/file/hash disagreement | Fixture-integrity test fails |
| Static compatibility failure | Do not add production support; preserve with an explicit incompatibility record if useful |
| Missing original evidence | Record the unavailable fact; do not reconstruct or infer it silently |
| Unknown committed fixture | Closed-world test fails until it is supported or explicitly incompatible |

## 11. Production build identity and catalog

The provider-local production concepts are:

```text
FastTrackAssemblyBuildIdentity
FastTrackSupportedAssemblyBuildCatalog
```

`FastTrackAssemblyBuildIdentity` contains one validated `Version` and one
canonical SHA-256 value. Construction rejects a missing version, missing
digest, non-hexadecimal digest, or digest with a length other than 256 bits.
Digest comparison is over normalized bytes or canonical hexadecimal, not
culture-sensitive text.

`FastTrackSupportedAssemblyBuildCatalog` contains immutable unique build
identities. Construction rejects duplicate identities. It exposes exact
membership and enumeration for production composition and closed-world tests;
it performs no file access and has no fixture-path knowledge.

The existing singular `SupportedFastTrackFileVersion` constant is removed.
The catalog is injected into `FastTrackCompatibilityInspector` alongside the
existing physical file-identity reader. No compatibility shim or legacy
version-only path remains.

## 12. Runtime compatibility flow

Runtime inspection preserves the approved provider-neutral activation model:

1. Match the exact enabled mod entry whose static ID is
   `PeterHan.FastTrack`.
2. Require the `FastTrack` assembly to originate from that same mod entry.
3. Capture active Harmony prefix topology and physical assembly file identity
   once.
4. Determine independently whether FastTrack owns each declared runtime
   capability through its exact active replacement.
5. Treat an inactive replacement as `DoesNotOwn` / `NotEvaluated` /
   `NotApplicable`; build support does not invent authority.
6. For an active replacement, require a successful physical identity read.
7. Require an exact catalog match on file version and DLL SHA-256.
8. Only then run deep feature-specific type, member, signature, owner, target,
   and IL-semantic verification against the already loaded assembly.
9. Produce a complete generic contribution only after all applicable checks
   pass.

The supported-build catalog does not replace runtime verification. An exact
accepted DLL may still be incompatible with the active game or Harmony
topology, and feature activation remains independently observable.

## 13. Runtime outcomes

The approved mapping remains:

| Feature condition | Authority | Contract | Required disposition | Optional disposition |
|---|---|---|---|---|
| Mod absent | `DoesNotOwn` | `NotEvaluated` | `NotApplicable` | `NotApplicable` |
| Replacement inactive | `DoesNotOwn` | `NotEvaluated` | `NotApplicable` | `NotApplicable` |
| Exact accepted build and verified contract | `OwnsCompatible` | `Compatible` | `Selected` | `Selected` |
| Active but identity unavailable, unsupported, or structurally incompatible | `OwnsIncompatible` | `Incompatible` | `ActivationBlocking` | `Unavailable` |

`UnsupportedFileVersion` is replaced by the semantically complete
`UnsupportedAssemblyBuild` failure code. Its bounded diagnostic reports the
observed file version and digest. A known version with changed bytes is
therefore distinguishable from an unavailable identity without inventing a
new generic activation state.

When an unsupported FastTrack replacement owns a required execution seam, the
activation transaction remains fail-closed. It must not select the Klei
implementation beneath an active skipping prefix.

## 14. Static fixture verification

Fixture assemblies remain data. Tests open them with
`System.Reflection.PortableExecutable.PEReader` and
`System.Reflection.Metadata.MetadataReader`. Tests do not use `Assembly.Load`,
`Assembly.LoadFrom`, a metadata load context, dependency resolution, or a
compile reference.

The existing singular `FastTrackGitHubReleaseAssemblyContractTests` becomes a
preserved-build matrix with provider-neutral fixture discovery and
FastTrack-specific contracts. Naming must describe preserved evidence rather
than a live GitHub dependency.

The suite proves:

1. Every fixture path matches its computed DLL file version and SHA-256.
2. Every manifest retained-file digest matches the file.
3. Every fixture contains its manifest-declared metadata and license evidence;
   unavailable package metadata is named explicitly rather than required or
   reconstructed.
4. `mod.yaml` declares the expected static ID when it is direct package
   evidence; reconstructed metadata is tested according to its stated origin.
5. No fixture DLL appears in the test assembly reference closure or loaded
   assembly set.
6. Every supported catalog identity resolves to exactly one fixture.
7. Every fixture is either supported exactly once or has exactly one
   incompatibility record.
8. No supported fixture has an incompatibility record.
9. Every applicable FastTrack type, member, signature, visibility, Harmony
   target, and IL-semantic contract passes for each supported build.
10. Feature absence or presence expectations are explicit per preserved build;
    a removed type cannot make a contract silently disappear from coverage.
11. A same-version/different-digest synthetic fixture is not conflated with an
    admitted build.
12. A digest mismatch in the directory, manifest, or production catalog fails
    deterministically.

## 15. Configuration amendments

Two narrow repository/test configuration amendments are required.

### 15.1 Binary Git attributes

Add this scoped rule to the root `.gitattributes`:

```gitattributes
mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/**/*.dll binary
```

This prevents text normalization, textual diff, and merge treatment for the
inert third-party DLL evidence. It affects only Git handling of fixture DLLs.
No ZIP rule is added while no ZIP is retained.

### 15.2 Test fixture copying

In
`mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`,
replace the existing single-file FastTrack fixture item:

```xml
<None Update="Fixtures\ThirdParty\FastTrack\0.18.4.0\FastTrack.dll">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

with:

```xml
<None Update="Fixtures\ThirdParty\FastTrack\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

This is the smallest test-project change that lets the matrix consume all
content-addressed DLLs, metadata, manifests, license files, and compatibility
records from the test output. The files remain data: no assembly reference is
added. Production output, mod packaging, dependency resolution, lockfiles,
`oni-mod-pipeline.toml`, and CI behavior are unchanged except that the existing
test execution receives the complete fixture bundle.

No other configuration file changes are part of this design.

## 16. Migration and implementation order

1. Replace the obsolete mutable-download gate in the active implementation
   plan with this evidence-preservation and admission workflow.
2. Add red tests for build identity validation and exact catalog matching.
3. Implement the provider-local immutable identity and catalog.
4. Add red fixture-integrity and closed-world tests.
5. Move the existing `0.18.4.0` DLL without changing its bytes, recover only
   accurately attributable metadata, and add its provenance manifest.
6. Update fixture copying and Git binary handling after the exact configuration
   amendments are approved.
7. Acquire and preserve the current `0.18.5.0` candidate once.
8. Reproduce the successful static contract probe through the permanent
   preserved-build matrix before changing production support.
9. Add the exact `0.18.5.0` build identity to the production catalog after the
   matrix, provenance, and closed-world tests pass.
10. Change runtime inspector tests first to require exact version-plus-digest
    admission and the `UnsupportedAssemblyBuild` outcome.
11. Replace the version-only production gate with catalog membership.
12. Resume the declared FastTrack adapter and generic activation tasks from the
    active program plan.
13. Run focused tests, the complete test project, repository pipeline gates,
    `git diff --check`, and the formal built-in review before a behavior-changing
    commit.

No compatibility shim preserves version-only admission. No old fixture path is
retained as an alias.

## 17. Commit and review boundaries

The fixture migration, admitted-build catalog, runtime identity gate, adapter
projection, and their tests form one coherent behavioral milestone because
the closed-world fixture-to-production invariant must never be committed in a
half-updated state.

Before that commit, the implementation must state exactly:

`Implementation complete; /review pending`

The built-in review scope is all uncommitted declared-integration,
FastTrack-evidence, supported-build, runtime-plan, test-project, Git-attribute,
schema, and associated test changes, explicitly excluding the user-owned
untracked `AGENTS.md`. Every confirmed P0-P2 finding is resolved or explicitly
deferred before committing.

The design and implementation-plan documents may be committed as a separate
non-behavioral planning snapshot. Pushing remains separately authorized.

## 18. Acceptance criteria

The change is accepted when:

1. Normal tests succeed with network access unavailable.
2. Deleting or changing the upstream FastTrack release does not affect an
   existing fixture test.
3. The original `0.18.4.0` DLL digest remains unchanged after migration.
4. The current candidate is preserved with exact provenance and an explicit
   compatibility classification.
5. Production admits only exact cataloged version-and-digest pairs.
6. Unknown versions and same-version digest changes fail closed when their
   replacements are active.
7. Inactive FastTrack features remain `NotApplicable` rather than becoming
   incompatible merely because the loaded build is unknown.
8. Runtime structural verification still executes for active features of an
   accepted build.
9. Every production supported build and every preserved fixture satisfies the
   two-way closed-world invariant.
10. No fixture assembly is loaded or shipped with Temperature Limit.
11. Provider-neutral activation code contains no FastTrack version, digest,
    catalog, or fixture dependency.
12. Focused and full test gates pass with zero skipped or inconclusive tests.
13. The repository pipeline succeeds without a network fixture fetch.
14. Formal review has no unresolved, non-deferred P0-P2 finding.

## 19. Authoritative basis

This design applies these authoritative principles:

- Bazel's hermeticity model requires a build to be insensitive to libraries
  and other software outside its declared inputs. Repository-owned fixtures
  make compatibility tests self-contained:
  <https://bazel.build/concepts/hermeticity>
- SLSA build provenance treats external parameters as untrusted inputs and
  models resolved dependencies with immutable identifiers such as digests. The
  repository manifest adopts the input/digest distinction without claiming to
  be a SLSA attestation:
  <https://slsa.dev/spec/v1.2/build-provenance>
- SPDX package verification uses checksums to verify package identity and
  supports later/offline verification of recorded artifacts:
  <https://spdx.github.io/spdx-spec/v2.3.1-dev/how-to-use/>
- Git's `binary` attribute macro disables text, diff, and merge treatment for
  binary paths:
  <https://git-scm.com/docs/gitattributes>
- FastTrack's upstream MIT license permits redistribution subject to its
  copyright and license-notice requirements:
  <https://github.com/peterhaneve/ONIMods/blob/main/LICENSE>

## 20. Summary

FastTrack compatibility becomes a repository-owned, content-addressed evidence
system rather than a mutable-release assumption. Exact preserved DLL builds
are independently classified as supported or incompatible. Production support
requires an exact version-and-digest catalog match, while runtime authority and
IL contracts remain mandatory. New upstream bytes become an ordinary candidate
intake event. Remote disappearance cannot invalidate previously accepted test
evidence, same-version republishing cannot overwrite it, and provider-specific
facts remain outside the generic activation architecture.
