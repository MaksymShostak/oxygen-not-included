# External-Mod Compatibility Evidence Design

- **Status:** Implemented for supported FastTrack builds; generalized fixture
  catalog deferred
- **Date:** 2026-09-01
- **Initial integration:** FastTrack
- **Affected mod:** Delivery Temperature Limit (Supercooled)
- **Primary reliability objective:** Normal builds and tests retain every byte
  required to re-run accepted FastTrack compatibility contracts without a
  mutable remote release
- **Runtime safety objective:** FastTrack may own a required runtime seam only
  when its exact assembly build and active structural contract both verify
- **Maintenance objective:** A changed or unavailable upstream release is an
  ordinary candidate-intake result, not a failure of previously accepted
  evidence

## 1. Decision

Preserve each supported FastTrack DLL as inert, repository-owned,
content-addressed test evidence. Admit production support only for an exact pair
of assembly file version and complete DLL SHA-256. Continue to verify the loaded
assembly's active Harmony ownership, signatures, and IL semantics after that
identity gate.

Keep the implementation proportional to the compatibility decision being made.
The active system therefore contains:

1. exact preserved `FastTrack.dll` bytes;
2. human-readable origin and licensing notes;
3. a small test-only table of independently expected PE identity and feature
   contracts;
4. closed-world tests connecting that table, the fixture tree, and the
   production supported-build catalog; and
5. the provider-local production identity gate.

It does not operate a generalized provenance-manifest parser, a generic fixture
catalog, package-metadata validation, or an incompatible-fixture workflow.
Proposed serialized shapes for those capabilities remain as explicitly
deferred, non-operational stubs so a concrete future need can activate them
through TDD without pretending they are current guarantees.

Use authoritative tools to execute and validate their own formats. Repository
code enforces only project policy and cross-artifact invariants those tools
cannot know. In particular:

- `PEReader` and `MetadataReader` interpret .NET portable-executable metadata;
- SHA-256 identifies exact retained bytes;
- Git's binary attribute protects DLL bytes from text normalization;
- the repository pipeline evaluates its own project and package formats; and
- repository tests own only FastTrack support policy, fixture closure, and the
  compatibility-specific metadata and IL contracts.

## 2. Problem

The previous plan required re-downloading `FastTrack.zip` from the mutable
`FastTrackBeta` release and comparing it with a digest recorded while version
`0.18.4.0` was available. The same URL later served version `0.18.5.0`, and the
original archive was no longer obtainable there.

Rejecting changed bytes was correct: treating them as the reviewed artifact
would have fabricated provenance. Depending on the mutable URL was not correct
for ordinary verification. Compatibility tests must retain their accepted
inputs, while upstream acquisition must be a separate maintenance operation.

The old runtime gate also compared only the DLL's reported file version even
though the physical-file reader already computed its digest. A same-version
republish could therefore pass the identity gate with different bytes. Exact
version-plus-digest admission closes that gap without weakening the existing
runtime structural verification.

## 3. Goals

The implemented design must:

1. keep normal FastTrack compatibility tests offline and deterministic;
2. preserve the exact DLL bytes underlying each support decision;
3. identify builds by complete DLL digest as well as reported file version;
4. permit distinct same-version DLLs to occupy distinct content addresses;
5. require exact production catalog membership before deep verification of an
   active FastTrack replacement;
6. retain feature-specific runtime ownership, member, signature, and IL checks;
7. keep inactive replacements `NotApplicable`, even when another feature from
   the loaded build is unsupported;
8. keep FastTrack policy out of provider-neutral activation core code;
9. preserve available origin and license facts without presenting repository
   notes as upstream attestations;
10. prove one-to-one closure among supported identities, fixture expectations,
    and preserved DLL directories;
11. keep every fixture assembly unreferenced, unloaded, unexecuted, and outside
    production output; and
12. retain future generalized-provenance and incompatible-build concepts as
    honest stubs rather than active speculative machinery.

## 4. Non-goals

This milestone does not:

- mirror the complete ONIMods repository or retain the mutable release ZIP;
- add a submodule, Git LFS dependency, artifact server, package feed, or
  network-backed cache;
- download external mods during build, test, packaging, or ordinary CI;
- parse or validate FastTrack's package YAML, because package metadata is not
  an input to the DLL compatibility decision;
- create a repository-specific YAML parser;
- deserialize the deferred provenance or incompatibility document shapes;
- preserve an incompatible build before a concrete maintenance need justifies
  activating that workflow;
- accept an unknown build merely because runtime reflection appears promising;
- execute a preserved external-mod assembly;
- add external-mod identities to generic activation types;
- claim SLSA provenance or any upstream attestation;
- synthesize missing `0.18.4.0` package files; or
- retain a version-only constructor, fallback, alias, or compatibility shim.

## 5. Terminology

### 5.1 Assembly build identity

A **FastTrack assembly build identity** is exactly:

- the assembly file version reported for `FastTrack.dll`; and
- the SHA-256 digest of the complete `FastTrack.dll` file.

It is not an archive identity, source revision, release label, version range,
or claim of API compatibility.

### 5.2 Supported-build fixture

A **supported-build fixture** is one content-addressed directory containing:

- the exact admitted `FastTrack.dll`;
- `README.md`, which records known origin, identity, and compatibility facts;
  and
- `UPSTREAM-LICENSE.txt`.

The README is a human evidence note. It is not a machine-validated provenance
manifest and is not used as an authority for DLL identity. Tests derive the
identity from the directory expectation and the actual DLL bytes and metadata.

### 5.3 Supported build

A **supported build** is an exact assembly build identity that:

1. occurs once in `FastTrackSupportedAssemblyBuildCatalog`;
2. occurs once in the test-only supported-fixture expectation set;
3. resolves to one content-addressed DLL fixture; and
4. passes every applicable static FastTrack contract.

### 5.4 Candidate intake

**Candidate intake** is an explicit, network-dependent maintenance operation
that obtains and evaluates possible new upstream bytes. It runs outside normal
build and test execution. Failure or upstream change during intake cannot alter
an existing fixture or support declaration.

### 5.5 Deferred fixture-catalog stub

A **deferred fixture-catalog stub** is a source-level serialized shape retained
for future design continuity. It has no loader, discovery path, validator,
fixture instance, or production consumer. Its presence makes no current
provenance or incompatibility guarantee.

## 6. Architectural boundaries

| Boundary | Owns | Must not own |
|---|---|---|
| `Tests/Fixtures/ThirdParty/FastTrack` | Exact supported DLL bytes, human evidence notes, license text | Runtime policy, acquisition logic, active generic manifests |
| FastTrack static fixture tests | PE interpretation, exact hashes, fixture/catalog closure, FastTrack metadata and IL contracts | Assembly loading, package-format reimplementation, runtime Harmony state |
| Deferred fixture-catalog stubs | Proposed future serialized shapes and activation notes | Discovery, parsing, validation, or current decisions |
| `FastTrackCompatibility` | Exact build identity, supported-build catalog, provider-specific structural verification | Generic selection policy or test-fixture paths |
| `GameplayActivation/Core/ExternalModIntegration` | Provider-neutral declarations, observations, outcomes, capabilities, deterministic selection | FastTrack names, versions, digests, or fixture knowledge |
| `RuntimePatchInstallation` | Composition and selected-authority enforcement | Fixture discovery or provider-specific evidence parsing |
| Repository pipeline | Evaluation of its build, test, and package configuration | FastTrack compatibility policy |

## 7. Content-addressed fixture layout

Each supported build uses its reported file version and full lowercase DLL
SHA-256:

```text
Tests/Fixtures/ThirdParty/FastTrack/
  0.18.4.0/
    sha256-d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add/
      FastTrack.dll
      README.md
      UPSTREAM-LICENSE.txt
  0.18.5.0/
    sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/
      FastTrack.dll
      README.md
      UPSTREAM-LICENSE.txt
```

The complete digest prevents a same-version republish from overwriting or
impersonating accepted bytes. No old path is retained as an alias. No fixture
directory is keyed only by version.

The active tree is deliberately closed to supported DLLs. It contains no ZIP,
YAML, JSON provenance manifest, incompatibility record, or pending marker.
Should a future incompatible build be worth retaining, that need activates the
deferred design as a separate reviewed milestone rather than weakening the
meaning of this tree.

## 8. Preserved FastTrack evidence

### 8.1 FastTrack `0.18.4.0`

The migrated DLL remains byte-for-byte unchanged:

- assembly name: `FastTrack`;
- assembly version: `0.18.0.0`;
- file version: `0.18.4.0`;
- module version identifier:
  `b1e31127-5b91-4607-b5b5-8ea255bd5288`;
- DLL SHA-256:
  `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`;
- previously recorded archive SHA-256:
  `8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D`;
  and
- closest independently reviewed upstream source revision:
  `e24e8f3082a52785e971943a8f1fff8de0ca8dff`.

The mutable endpoint no longer provides the original archive, and the original
observation timestamp was not retained. The fixture README states those limits
and does not reconstruct missing package metadata. The license is the exact
upstream `LICENSE` from the cited immutable source revision.

### 8.2 FastTrack `0.18.5.0`

The candidate archive was observed at
`2026-09-01T05:00:41.3223477Z` with SHA-256
`3ED47A89966B3780DD4C8855DA20B6335B642AA15A92143DA749FBC3621F5211`.
Authoritative archive inspection established these member facts:

| Archive member | SHA-256 |
|---|---|
| `FastTrack/FastTrack.dll` | `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B` |
| `FastTrack/mod.yaml` | `32576251B1A57027DF93F47748679650E4916AD8F4E7F872C39C5D12E98EC20E` |
| `FastTrack/mod_info.yaml` | `7CAAF5D05ECC1AD5B362E52616A179519B935BF4204F9E2677A6CF02AADEAB5D` |

The observed package metadata declared static ID `PeterHan.FastTrack`, package
version `0.18.5.0`, minimum supported ONI build `736649`, and API version `2`.
Those observations remain in the fixture README, but the YAML bytes are not
active compatibility inputs and are not retained.

The preserved DLL identity is:

- assembly name: `FastTrack`;
- assembly version: `0.18.0.0`;
- file version: `0.18.5.0`;
- module version identifier:
  `bb4e7a11-4985-4d8f-b1c9-f497c6bb3d1e`; and
- DLL SHA-256:
  `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B`.

The same upstream license revision is retained beside this DLL.

## 9. Static fixture verification

Fixture assemblies remain data. Tests open them only through
`System.Reflection.PortableExecutable.PEReader` and
`System.Reflection.Metadata.MetadataReader`. They do not use `Assembly.Load`,
`Assembly.LoadFrom`, dependency resolution, a metadata load context, or a
compile reference.

`FastTrackSupportedBuildFixtureExpectation` declares independently expected
facts for each admitted build:

- exact file-version and DLL-digest identity;
- assembly version and module version identifier; and
- explicit presence or absence of the world-inventory, pickup-grouping, and
  direct-delivery replacements.

The static suite proves:

1. the expectation set and production catalog contain exactly the same build
   identities in deterministic order;
2. the copied fixture tree contains exactly `FastTrack.dll`, `README.md`, and
   `UPSTREAM-LICENSE.txt` for every expected supported build and no orphaned
   artifact;
3. each actual DLL SHA-256 matches its content-addressed expectation;
4. PE assembly name, assembly version, file version, and module version
   identifier match the independent expectation;
5. every applicable FastTrack type, member, signature, visibility, Harmony
   target, and IL-semantic contract passes for both builds;
6. feature presence and absence are explicit, so a removed or newly introduced
   replacement cannot silently escape review; and
7. no physical assembly named by a fixture is loaded into the test process.

No test parses README prose. Human origin notes and machine compatibility
assertions remain separate instead of deriving both from one editable manifest.

## 10. Production build identity and catalog

The provider-local production concepts are:

```text
FastTrackAssemblyBuildIdentity
FastTrackSupportedAssemblyBuildCatalog
```

`FastTrackAssemblyBuildIdentity` contains one non-null `Version` and one
canonical 64-character SHA-256 hexadecimal string. Construction rejects a
missing, malformed, or non-hexadecimal digest. Equality covers the exact pair.

`FastTrackSupportedAssemblyBuildCatalog` copies, sorts, and exposes immutable
unique identities. `Contains` performs exact pair membership, treats malformed
observed digests as unsupported, and performs no file, fixture, or network I/O.

The declared catalog admits exactly:

| File version | DLL SHA-256 |
|---|---|
| `0.18.4.0` | `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD` |
| `0.18.5.0` | `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B` |

No version range, digest prefix, version-only overload, fallback, or shim is
permitted.

## 11. Runtime compatibility flow

Runtime inspection preserves the provider-neutral activation model:

1. match the exact enabled mod entry with static ID `PeterHan.FastTrack`;
2. require the `FastTrack` assembly to originate from that same entry;
3. capture active Harmony prefix topology and physical file identity once;
4. determine independently whether FastTrack owns each declared capability;
5. classify an inactive replacement as `DoesNotOwn` / `NotEvaluated` /
   `NotApplicable` before consulting build support;
6. for an active replacement, require a successful physical identity read;
7. require exact catalog membership for file version and DLL SHA-256;
8. only then verify type, member, signature, owner, target, and IL semantics
   against the already loaded assembly; and
9. contribute generic runtime authority only after all applicable checks pass.

The catalog is necessary but not sufficient. An admitted DLL can still fail
against the active game or Harmony topology. `UnsupportedAssemblyBuild`
distinguishes unknown bytes from unavailable identity and structural failure.

When an unsupported FastTrack replacement actively owns a required execution
seam, activation remains fail-closed. The mod must not enable its Klei path
beneath FastTrack's skipping prefix.

## 12. Candidate intake and additive admission

New FastTrack releases are expected. Candidate intake follows this bounded
maintenance flow:

1. acquire the candidate into temporary storage outside the fixture tree;
2. use an authoritative archive implementation to enumerate and safely extract
   the package; do not implement ZIP parsing in repository code;
3. record the observation time, source, archive digest, relevant member paths,
   and member digests;
4. read `FastTrack.dll` through `PEReader`/`MetadataReader` and compute its full
   SHA-256;
5. compare the exact identity with existing fixtures so a changed remote or
   same-version republish becomes a new candidate, never an overwrite;
6. add a content-addressed DLL, README, license, and independent static
   expectation for a compatible candidate;
7. run the full PE/IL matrix before adding the exact production catalog entry;
8. commit fixture, expectation, tests, and production admission atomically; and
9. leave every older accepted build unchanged unless a separate retirement
   design is explicitly approved.

If package metadata becomes a real compatibility input, invoke its
authoritative parser or consumer and retain only the evidence that concrete
contract needs. Do not grow a repository YAML parser.

If an incompatible candidate is not useful after evaluation, do not retain it
in the active supported-fixture tree. If preserving it would materially help a
planned adapter, activate the deferred fixture-catalog design in a separate
TDD milestone with explicit classification and closed-world semantics.

## 13. Deferred generalized fixture-catalog stubs

The non-operational stubs live under:

```text
Tests/ExternalModCompatibility/DeferredFixtureCatalog/
```

They preserve proposed provider-neutral shapes for:

- observation source and time;
- archive digest and availability;
- assembly identity and retained artifact origins;
- unavailable facts; and
- an explicit incompatibility decision with failure codes and summary.

They intentionally have no loader, discovery API, validation suite, active JSON
document, or fixture consumer. No current test or production behavior depends
on them.

Activation requires a concrete incompatible-build retention need or a second
external integration. It starts with failing acceptance tests and must:

1. choose the format based on the concrete consumers;
2. use the platform serializer/parser for syntax and format semantics;
3. reject ambiguous duplicate and unknown properties when JSON is used;
4. keep repository validation to cross-artifact path, hash, origin,
   unavailable-fact, and classification invariants;
5. execute authoritative consumers for any retained package metadata; and
6. demonstrate that the generalized module is shallower than duplicating the
   concrete workflows it replaces.

## 14. Configuration amendments

The root `.gitattributes` contains exactly this new scoped rule:

```gitattributes
mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/**/*.dll binary
```

It protects third-party DLL evidence from text normalization and textual merge
treatment. No ZIP, YAML, JSON, or broad fixture rule is needed.

The test project replaces its singular FastTrack file copy with:

```xml
<None Update="Fixtures\ThirdParty\FastTrack\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

The wildcard copies inert fixture data into test output. It adds no compile
reference. Production output, package inputs, lockfiles, target frameworks,
`oni-mod-pipeline.toml`, and CI configuration remain unchanged.

The repository-owned pipeline and MSBuild evaluated item graphs are the
authorities for proving those project/package boundaries; repository code does
not parse their configuration formats.

## 15. Failure semantics

| Condition | Required behavior |
|---|---|
| Mutable URL unavailable | Candidate intake stops; accepted fixtures and tests remain green |
| Mutable URL serves new bytes | Treat them as a distinct candidate; never replace accepted bytes |
| Same file version, different DLL digest | Use a distinct content address and require a new decision |
| Malformed or unsafe archive | Authoritative archive intake rejects it before repository materialization |
| Unsupported exact DLL identity with inactive replacement | Feature remains `ReplacementInactive` / `NotApplicable` |
| Unsupported exact DLL identity with active replacement | `UnsupportedAssemblyBuild`; required activation fails closed |
| Admitted identity with structural mismatch | Feature-specific incompatibility; required activation fails closed |
| Missing historical origin fact | State the limitation in the README; do not synthesize it |
| Orphan fixture or catalog entry | Closed-world test fails |
| Future incompatible fixture is worth retaining | Activate the deferred design through a separate reviewed TDD change |

## 16. Acceptance criteria

The design is satisfied when:

1. normal tests need no FastTrack network fetch;
2. upstream deletion or change cannot affect existing fixture tests;
3. both preserved DLL hashes match their content addresses;
4. both builds pass the same static metadata, signature, and IL matrix;
5. production admits only the two exact version-and-digest pairs;
6. unknown versions and same-version digest changes fail closed only when their
   replacements are active;
7. runtime structural verification still runs for active features of an
   admitted build;
8. the production catalog, test expectation set, and fixture tree have exact
   one-to-one closure;
9. no fixture DLL is loaded, referenced, or shipped;
10. generic activation code contains no FastTrack identity or fixture policy;
11. deferred schema types remain non-operational;
12. focused tests and the authoritative repository pipeline pass with zero
    skipped or inconclusive tests; and
13. formal review has no unresolved, non-deferred P0-P2 finding.

## 17. Authoritative basis

This design applies these principles:

- Bazel's hermeticity model requires builds to be insensitive to undeclared
  external inputs. Repository-owned fixtures make accepted compatibility tests
  self-contained: <https://bazel.build/concepts/hermeticity>
- SLSA provenance distinguishes external inputs from resolved immutable
  identities such as digests. This repository uses that distinction without
  claiming an attestation:
  <https://slsa.dev/spec/v1.2/build-provenance>
- ECMA-335 defines the .NET metadata read through the platform metadata APIs:
  <https://ecma-international.org/publications-and-standards/standards/ecma-335/>
- Git's `binary` attribute macro disables text, diff, and merge treatment for
  binary paths: <https://git-scm.com/docs/gitattributes>
- FastTrack's MIT license permits redistribution when its copyright and license
  notice are preserved:
  <https://github.com/peterhaneve/ONIMods/blob/main/LICENSE>

## 18. Summary

FastTrack compatibility now rests on exact repository-owned DLL evidence, not
a mutable-release assumption. The active implementation is intentionally
narrow: a small exact build catalog, an independent supported-fixture table,
static PE/IL contracts, and a runtime exact-identity gate. General provenance
and incompatible-fixture concepts remain available as clearly deferred stubs,
ready for a concrete need but incapable of implying guarantees today.

New upstream bytes are routine candidate input. They cannot overwrite accepted
evidence, same-version republishing cannot pass by version alone, and no
provider-specific artifact policy leaks into generic activation architecture.
