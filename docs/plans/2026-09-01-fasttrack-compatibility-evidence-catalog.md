# FastTrack Compatibility Evidence Catalog Implementation Plan

> **For agentic workers:** REQUIRED SKILLS: use `test-driven-development` for every production behavior change and `committing-to-git` at each authorized commit checkpoint. Execute inline in the current task; do not delegate this plan.

**Goal:** Make supported FastTrack compatibility hermetic by preserving exact content-addressed builds, enforcing fixture-to-production closure, admitting the verified `0.18.4.0` and `0.18.5.0` DLL identities, and replacing runtime version-only admission with exact version-plus-SHA-256 matching.

**Architecture:** Inert FastTrack package evidence lives under version and full DLL SHA-256 directories. Test-only manifest readers validate provenance, hashes, classification, and static PE/IL contracts without loading either DLL. A provider-local immutable production catalog admits exact assembly build identities; `FastTrackCompatibilityInspector` requires catalog membership before deep verification of an active replacement, then projects through the existing generic activation boundary.

**Tech Stack:** C# with nullable reference types; .NET Standard 2.1 production source linked into MSTest on .NET 10; `System.Reflection.Metadata`; `System.Reflection.PortableExecutable`; `System.Security.Cryptography`; `System.Text.Json`; Git binary attributes.

**Spec:** `docs/specs/2026-09-01-external-mod-compatibility-evidence-design.md`

## Global Constraints

- Treat the working tree as authoritative. Preserve all existing declared-integration work and the user-owned untracked `AGENTS.md`.
- Do not use the `verification-before-completion` skill.
- Use strict red-green-refactor ordering. Do not write production implementation before the named failing test has failed for the intended reason.
- Do not load, reference, resolve, or execute either FastTrack fixture assembly. Read it only as bytes through `PEReader` and `MetadataReader`.
- Do not retain a mutable remote URL as a build/test input. Network access is permitted only during the explicit candidate-evidence acquisition step.
- Do not add a package, test project, target-framework change, lockfile change, source-project reference, Git LFS rule, submodule, CI workflow, or pipeline setting.
- Do not modify `oni-mod-pipeline.toml`, either `packages.lock.json`, `Source/DeliveryTemperatureLimit.csproj`, `mod.yaml`, `mod_info.yaml`, or Workshop metadata.
- Keep `FastTrackAssemblyBuildIdentity` and `FastTrackSupportedAssemblyBuildCatalog` inside `FastTrackCompatibility/FeatureContractVerification`. Generic activation core code must not reference either type.
- Match support by exact file version and complete DLL SHA-256. No version range, digest prefix, fallback, compatibility shim, or legacy version-only constructor is permitted.
- Preserve the existing `0.18.4.0` DLL bytes exactly. Its SHA-256 must remain `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD` after the move.
- Admit the observed `0.18.5.0` DLL only at SHA-256 `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B` after the permanent matrix reproduces the successful static probe.
- The root `.gitattributes` DLL rule in Task 3 has already received exact approval. Do not add a ZIP rule because this plan retains no ZIP.
- Do not make the Task 3 test-project wildcard edit until the exact `DeliveryTemperatureLimit.Tests.csproj` setting in the spec and this plan has received repository-required explicit approval.
- Every committed fixture must be classified: exactly one matching production catalog entry or exactly one `incompatibility-record.json`, never both.
- Before each non-trivial implementation commit, state `Implementation complete; /review pending`, run built-in review over the exact uncommitted milestone, and resolve or explicitly defer every confirmed P0-P2 finding.
- Use the user's pre-authorization for the commit messages printed in this plan. Do not push.

---

## File and responsibility map

| File | Responsibility |
|---|---|
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackAssemblyBuildIdentity.cs` | Validated immutable file-version and DLL-digest identity |
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackSupportedAssemblyBuildCatalog.cs` | Immutable unique set of exact admitted FastTrack builds |
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityInspector.cs` | Active-owner detection, exact build admission, and deep loaded-assembly contract verification |
| `Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackFixtureProvenanceDocument.cs` | Strict test-only JSON evidence shape |
| `Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackFixtureIncompatibilityRecordDocument.cs` | Strict test-only unsupported-build decision shape |
| `Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackPreservedBuildFixture.cs` | One validated content-addressed fixture and its retained paths |
| `Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackPreservedBuildFixtureCatalog.cs` | Offline discovery, hash verification, and classification inventory |
| `Tests/FastTrackCompatibility/FastTrackPreservedBuildFixtureCatalogTests.cs` | Invalid-manifest and closed-world fixture/catalog contracts |
| `Tests/FastTrackCompatibility/PreservedFastTrackAssemblyContractTests.cs` | Data-driven static metadata/signature/IL contracts for every admitted build |
| `Tests/FastTrackCompatibility/FastTrackPreservedBuildContractExpectation.cs` | Explicit per-build feature-presence expectations used by the static matrix |
| `Tests/Fixtures/ThirdParty/FastTrack/<version>/sha256-<digest>/` | Exact inert DLL, available metadata evidence, provenance, license, and human notes |
| `.gitattributes` | Binary Git treatment for third-party fixture DLLs only |
| `Tests/DeliveryTemperatureLimit.Tests.csproj` | Copies the complete fixture tree to test output as data |

## Task 1: Add exact FastTrack assembly build identities and the immutable supported-build catalog

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackAssemblyBuildIdentity.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackSupportedAssemblyBuildCatalog.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackSupportedAssemblyBuildCatalogTests.cs`

**Interfaces:**

- Produces:

```csharp
internal sealed class FastTrackAssemblyBuildIdentity :
    IEquatable<FastTrackAssemblyBuildIdentity>
{
    internal FastTrackAssemblyBuildIdentity(
        Version fileVersion,
        string assemblySha256);

    internal Version FileVersion { get; }
    internal string AssemblySha256 { get; }
}

internal sealed class FastTrackSupportedAssemblyBuildCatalog
{
    internal FastTrackSupportedAssemblyBuildCatalog(
        IEnumerable<FastTrackAssemblyBuildIdentity> builds);

    internal static FastTrackSupportedAssemblyBuildCatalog Declared { get; }
    internal IReadOnlyList<FastTrackAssemblyBuildIdentity> Builds { get; }

    internal bool Contains(
        Version fileVersion,
        string assemblySha256);
}
```

- `Declared` initially contains only the preserved `0.18.4.0` identity. Task 4 adds `0.18.5.0` only after its permanent fixture contracts pass.
- The constructor remains internal and accepts arbitrary test catalogs; no interface or mutable global replacement seam is added.

- [ ] **Step 1: Write identity and catalog tests first**

Add tests with these exact behaviors:

```csharp
[TestMethod]
public void BuildIdentity_WhenDigestUsesLowercase_StoresCanonicalUppercaseHexadecimal()
{
    var identity = new FastTrackAssemblyBuildIdentity(
        new Version(0, 18, 4, 0),
        "d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add");

    Assert.AreEqual(
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD",
        identity.AssemblySha256);
}

[TestMethod]
public void Catalog_Contains_RequiresVersionAndDigestFromTheSameDeclaredBuild()
{
    var first = new FastTrackAssemblyBuildIdentity(
        new Version(0, 18, 4, 0),
        DigestA);
    var second = new FastTrackAssemblyBuildIdentity(
        new Version(0, 18, 5, 0),
        DigestB);
    var catalog = new FastTrackSupportedAssemblyBuildCatalog([first, second]);

    Assert.IsTrue(catalog.Contains(first.FileVersion, DigestA.ToLowerInvariant()));
    Assert.IsFalse(catalog.Contains(first.FileVersion, DigestB));
    Assert.IsFalse(catalog.Contains(second.FileVersion, DigestA));
}
```

Also require:

- null `Version` rejection;
- null, empty, 63-character, 65-character, and nonhexadecimal digest rejection;
- value equality and equal hash codes for canonical-equivalent identities;
- inequality when either version or digest differs;
- immutable input copying and deterministic ascending version-then-digest enumeration;
- duplicate identity rejection even when digest casing differs;
- `Contains` returning `false`, not throwing, for an observed malformed digest;
- exact `Declared` entry for `0.18.4.0` and its 64-character digest.

- [ ] **Step 2: Run the focused tests and require the intended red state**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackSupportedAssemblyBuildCatalogTests
```

Expected: compilation fails because both production types are absent.

- [ ] **Step 3: Implement the minimal immutable value and catalog**

Implement ordinal, culture-independent digest validation and normalization.
Copy, sort, and expose catalog entries through a read-only collection. Reject
duplicates after normalization. `Contains` must validate an observed digest
non-throwingly and compare the exact pair; it must not search by version and
digest independently.

Declare exactly:

```csharp
new FastTrackAssemblyBuildIdentity(
    new Version(0, 18, 4, 0),
    "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD")
```

- [ ] **Step 4: Run the focused tests again**

Run the Step 2 command.

Expected: all `FastTrackSupportedAssemblyBuildCatalogTests` pass with zero
skipped or inconclusive tests.

- [ ] **Step 5: Run the existing FastTrack inspector tests as a regression gate**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackCompatibilityInspectorTests
```

Expected: green because the new catalog is not wired into runtime inspection
until Task 5.

## Task 2: Add strict offline fixture provenance and classification validation

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackFixtureProvenanceDocument.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackFixtureIncompatibilityRecordDocument.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackPreservedBuildFixture.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/PreservedBuildFixtures/FastTrackPreservedBuildFixtureCatalog.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPreservedBuildFixtureCatalogTests.cs`

**Interfaces:**

- Produces:

```csharp
internal sealed class FastTrackPreservedBuildFixtureCatalog
{
    internal static FastTrackPreservedBuildFixtureCatalog Load(
        string fastTrackFixtureRoot);

    internal IReadOnlyList<FastTrackPreservedBuildFixture> Fixtures { get; }
}

internal sealed class FastTrackPreservedBuildFixture
{
    internal Version FileVersion { get; }
    internal string AssemblySha256 { get; }
    internal string FixtureDirectory { get; }
    internal string AssemblyPath { get; }
    internal string ProvenancePath { get; }
    internal string? IncompatibilityRecordPath { get; }
    internal FastTrackFixtureProvenanceDocument Provenance { get; }
}
```

- The loader has no production dependency and performs no network access.
- `FastTrackFixtureProvenanceDocument` uses exact JSON property names from the design: `schemaVersion`, `integrationId`, upstream/source observation facts, `assembly`, `retainedArtifacts`, and `unavailableFacts`.
- Unknown JSON members, duplicate retained paths, rooted retained paths, parent traversal, backslash/forward-slash ambiguity, and case-colliding retained paths are rejected.

Use this exact JSON document shape, represented by focused nested DTOs with
`JsonPropertyName` attributes:

```json
{
  "schemaVersion": 1,
  "integrationId": "fast-track",
  "upstreamProjectUri": "https://github.com/peterhaneve/ONIMods",
  "candidateSourceUri": "https://github.com/peterhaneve/ONIMods/releases/download/FastTrackBeta/FastTrack.zip",
  "archiveObservation": {
    "availability": "available-at-observation",
    "observedAtUtc": "2026-09-01T00:00:00Z",
    "sha256": "3ED47A89966B3780DD4C8855DA20B6335B642AA15A92143DA749FBC3621F5211"
  },
  "assembly": {
    "fileName": "FastTrack.dll",
    "assemblyName": "FastTrack",
    "assemblyVersion": "0.18.0.0",
    "fileVersion": "0.18.5.0",
    "moduleVersionId": "bb4e7a11-4985-4d8f-b1c9-f497c6bb3d1e",
    "sha256": "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B"
  },
  "retainedArtifacts": [
    {
      "path": "FastTrack.dll",
      "sha256": "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B",
      "origin": {
        "kind": "release-archive-member",
        "archiveMemberPath": "FastTrack/FastTrack.dll",
        "sourceRevision": null,
        "sourcePath": null
      }
    }
  ],
  "unavailableFacts": []
}
```

Allow only archive availability values `available-at-observation` and
`unavailable-at-migration`. Allow only origin kinds
`release-archive-member`, `previously-extracted-release-archive-member`, and
`upstream-source-revision`, with the corresponding archive/source fields
required and forbidden exactly. `observedAtUtc` may be null only for
`unavailable-at-migration`; the archive digest may remain present when it was
recorded earlier.

When present, `incompatibility-record.json` has exactly:

```json
{
  "schemaVersion": 1,
  "assemblySha256": "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B",
  "evaluatedAtUtc": "2026-09-01T00:00:00Z",
  "failureCodes": ["world-inventory-contract-violation"],
  "summary": "The preserved build does not satisfy the required world-inventory publication contract."
}
```

Reject an empty or duplicate failure-code collection, a summary longer than
512 characters, a non-UTC timestamp, and a record digest that differs from the
fixture DLL.

- [ ] **Step 1: Write failing manifest-validation tests using isolated temporary fixture roots**

Build synthetic fixture roots in each test and require these exact outcomes:

```csharp
[TestMethod]
public void Load_WhenDirectoryDigestDoesNotMatchAssembly_ThrowsInvalidDataException()
{
    string root = CreateCompleteFixtureRoot();
    RenameDigestDirectory(root, new string('A', 64));

    InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(
        () => FastTrackPreservedBuildFixtureCatalog.Load(root));

    StringAssert.Contains(exception.Message, "directory SHA-256");
}

[TestMethod]
public void Load_WhenRetainedArtifactEscapesFixture_ThrowsInvalidDataException()
{
    string root = CreateCompleteFixtureRoot(
        retainedArtifactPath: "../outside.dll");

    InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(
        () => FastTrackPreservedBuildFixtureCatalog.Load(root));

    StringAssert.Contains(exception.Message, "retained artifact path");
}
```

Cover schema version other than `1`, integration ID other than `fast-track`,
invalid version directory, digest directory without the `sha256-` prefix,
missing `FastTrack.dll`, missing manifest, missing license, missing README,
manifest DLL digest mismatch, retained file digest mismatch, unlisted retained
file, listed missing file, a metadata file present but absent from the retained
artifact declaration, duplicate normalized artifact path, and a valid complete
fixture both with and without package metadata. Use `SHA256.HashData` or
`SHA256.Create().ComputeHash` over bytes; never trust manifest text while
determining identity.

Also cover every invalid incompatibility-record condition above and one valid
unsupported fixture. The current real FastTrack fixtures remain supported and
therefore contain no incompatibility record.

- [ ] **Step 2: Run the focused tests and require red**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackPreservedBuildFixtureCatalogTests
```

Expected: compilation fails because the test-only fixture types are absent.

- [ ] **Step 3: Implement strict test-only JSON parsing and fixture validation**

Use `JsonSerializer` with case-sensitive property matching, trailing commas
disallowed, comments disallowed, and unmapped members disallowed. Resolve every
retained path beneath the already resolved fixture directory and verify that
the result remains within that directory. Enumerate with ordinal deterministic
ordering. Return immutable collections and bounded semantic errors that name
the violated evidence rule without reading any remote resource.

- [ ] **Step 4: Run the focused tests again**

Run the Step 2 command.

Expected: all synthetic fixture validation cases pass.

## Task 3: Migrate the preserved `0.18.4.0` evidence and make static tests data-driven

**Files:**

- Modify: `.gitattributes`
- Modify after exact approval: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Move without byte changes: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/FastTrack.dll`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/sha256-d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add/FastTrack.dll`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/sha256-d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add/fixture-provenance.json`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/sha256-d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add/UPSTREAM-LICENSE.txt`
- Move and revise: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/README.md`
- Delete after the exact move is verified: the empty old `0.18.4.0` fixture root files
- Rename: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackGitHubReleaseAssemblyContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/PreservedFastTrackAssemblyContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPreservedBuildContractExpectation.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPreservedBuildFixtureCatalogTests.cs`

**Interfaces:**

- `FastTrackPreservedBuildContractExpectation` maps one exact
  `FastTrackAssemblyBuildIdentity` to explicit Boolean presence expectations
  for world-inventory replacement, pickup-grouping replacement, and direct-
  delivery replacement.
- `PreservedFastTrackAssemblyContractTests` receives fixtures through
  `DynamicData`; no test constructs a live GitHub URL or hard-codes the old
  non-content-addressed path.

- [ ] **Step 1: Add the real-fixture closed-world test before moving the fixture**

```csharp
[TestMethod]
public void PreservedFixtures_AndDeclaredSupportedBuilds_FormAClosedWorld()
{
    FastTrackPreservedBuildFixtureCatalog fixtures =
        FastTrackPreservedBuildFixtureCatalog.Load(RequireCopiedFixtureRoot());
    FastTrackSupportedAssemblyBuildCatalog supported =
        FastTrackSupportedAssemblyBuildCatalog.Declared;

    AssertFixtureClassificationIsExact(fixtures, supported);
}
```

The assertion must prove both directions and reject a fixture that is both
supported and accompanied by `incompatibility-record.json`.

- [ ] **Step 2: Run the fixture catalog tests and require red**

Run the Task 2 focused command.

Expected: failure explains that the old `0.18.4.0` directory is not a complete
content-addressed fixture.

- [ ] **Step 3: Apply the already approved Git binary rule**

Add exactly:

```gitattributes
mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/**/*.dll binary
```

Do not add a ZIP rule or change any existing attribute.

- [ ] **Step 4: Apply the test-project data-copy wildcard only after its exact approval**

Replace only the existing singular FastTrack `<None Update>` item with:

```xml
<None Update="Fixtures\ThirdParty\FastTrack\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

Do not modify the already approved `GameplayActivation/Core/**/*.cs` compile
item or any package/property item.

- [ ] **Step 5: Move the exact DLL and verify its bytes immediately**

Resolve both source and target as absolute workspace paths before the move.
Move only `FastTrack.dll`; do not recursively move or delete the fixture root.
Then run:

```powershell
Get-FileHash -Algorithm SHA256 mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/sha256-d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add/FastTrack.dll
```

Expected hash:
`D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`.

- [ ] **Step 6: Add accurately attributed provenance, license, and README**

Acquire `LICENSE` from immutable upstream revision
`e24e8f3082a52785e971943a8f1fff8de0ca8dff`. Record its source-revision path
and computed retained-file hash. Do not create `mod.yaml` or `mod_info.yaml`:
that exact revision contains neither package file. The manifest must state:

- the exact DLL and previously recorded archive hashes from the spec;
- assembly name `FastTrack`, assembly version `0.18.0.0`, file version
  `0.18.4.0`, and module version identifier
  `b1e31127-5b91-4607-b5b5-8ea255bd5288` read from the retained DLL;
- that the original archive is unavailable at migration time;
- that the retained DLL was previously extracted from that archive;
- that exact packaged `mod.yaml`, `mod_info.yaml`, their member paths, and their
  hashes are unavailable;
- that the source-revision license is not claimed as an exact unavailable
  archive member; and
- every fact that could not be recovered.

The fixture has no incompatibility record because `0.18.4.0` is in the
production supported-build catalog.

- [ ] **Step 7: Refactor the singular static contract class into the admitted-build matrix**

Rename the class to `PreservedFastTrackAssemblyContractTests`. Reuse its
existing `PEReader`/`MetadataReader` implementation. Replace
`ExpectedAssemblySha256` and the single old path with a `DynamicData` source
that enumerates the intersection of complete fixtures and declared supported
builds. Preserve all existing world-inventory and pickup-grouping IL assertions.

For `0.18.4.0`, declare explicitly:

```csharp
new FastTrackPreservedBuildContractExpectation(
    new FastTrackAssemblyBuildIdentity(
        new Version(0, 18, 4, 0),
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD"),
    worldInventoryReplacementPresent: true,
    pickupGroupingReplacementPresent: true,
    directDeliveryReplacementPresent: false)
```

Presence tests must fail if an expected type is removed; absence tests must fail
if an unexpected replacement appears without a new reviewed contract.
For every row, read assembly name, assembly version, file version, module
version identifier, and DLL digest directly from the PE file and require exact
agreement with `fixture-provenance.json`.

- [ ] **Step 8: Run fixture and static contract tests**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTrackPreservedBuildFixtureCatalogTests|FullyQualifiedName~PreservedFastTrackAssemblyContractTests"
```

Expected: the content-addressed `0.18.4.0` fixture, provenance, closed-world
catalog, static contracts, and no-load assertion all pass.

- [ ] **Step 9: Run the complete test project before admitting another build**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
```

Expected: all tests pass with zero skipped or inconclusive results.

## Task 4: Preserve and admit the verified `0.18.5.0` build

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.5.0/sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/FastTrack.dll`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.5.0/sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/mod.yaml`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.5.0/sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/mod_info.yaml`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.5.0/sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/fixture-provenance.json`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.5.0/sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/UPSTREAM-LICENSE.txt`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.5.0/sha256-cdf0150546952fda3a31a612d61fbef3808e05db89b9b6e8cceea1f3c752aa3b/README.md`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackSupportedAssemblyBuildCatalog.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPreservedBuildContractExpectation.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackSupportedAssemblyBuildCatalogTests.cs`

**Interfaces:**

- Adds one exact declared build:

```csharp
new FastTrackAssemblyBuildIdentity(
    new Version(0, 18, 5, 0),
    "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B")
```

- Adds one explicit expectation with world inventory present, pickup grouping
  present, and direct-delivery replacement absent.

- [ ] **Step 1: Acquire and validate the exact candidate outside the fixture root**

Download the `FastTrackBeta` release asset once into an isolated temporary
directory. Before extraction require archive SHA-256:

`3ED47A89966B3780DD4C8855DA20B6335B642AA15A92143DA749FBC3621F5211`.

Require exactly these members and hashes:

| Member | SHA-256 |
|---|---|
| `FastTrack/FastTrack.dll` | `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B` |
| `FastTrack/mod.yaml` | `32576251B1A57027DF93F47748679650E4916AD8F4E7F872C39C5D12E98EC20E` |
| `FastTrack/mod_info.yaml` | `7CAAF5D05ECC1AD5B362E52616A179519B935BF4204F9E2677A6CF02AADEAB5D` |

Require static ID `PeterHan.FastTrack` and packaged version `0.18.5.0`.
Reject any mismatch rather than updating these accepted observations.
Require assembly name `FastTrack`, assembly version `0.18.0.0`, file version
`0.18.5.0`, and module version identifier
`bb4e7a11-4985-4d8f-b1c9-f497c6bb3d1e` from the DLL metadata.
Before extracting, enumerate every archive entry and reject an absolute path,
parent traversal, duplicate normalized path, unsafe link-like entry, or an
unexpected compatibility-critical file location. Extract only the three named
members after these checks pass.

- [ ] **Step 2: Preserve exact members and complete provenance before production admission**

Copy only the three verified members into the content-addressed directory. Add
the upstream MIT license and README. Record exact archive/member origins,
observation time, source URI, assembly metadata read by `PEReader`, every
retained hash, and no unavailable package fact. Do not retain the ZIP.

- [ ] **Step 3: Run the closed-world test and require red**

Run the Task 3 focused fixture/static command.

Expected: the fixture-integrity checks pass, but closed-world classification
fails because `0.18.5.0` is preserved and has neither a supported-build entry
nor an incompatibility record.

- [ ] **Step 4: Add the exact catalog entry and explicit feature expectation**

Add the `0.18.5.0` identity shown above to `Declared`. Add its explicit feature
presence expectation. Update the catalog test to require exactly two declared
builds in deterministic order and neither a version-only nor digest-only match.

- [ ] **Step 5: Run the permanent fixture and static matrix**

Run the Task 3 focused fixture/static command.

Expected: both builds pass identity, provenance, no-load, world-inventory,
pickup-grouping, direct-delivery-absence, and closed-world contracts.

- [ ] **Step 6: Prove ordinary fixture tests have no network dependency**

Search the fixture catalog and static contract test source for `HttpClient`,
`WebClient`, `Invoke-WebRequest`, `curl`, `github.com`, and the release asset
URL. The only permitted upstream URLs are inert strings inside retained README
or provenance data. Test code must contain none of the network tokens.

Implement that token search as a source-boundary test and run it with the
focused fixture matrix. The deterministic acceptance evidence is that fixture
test code contains no network API or upstream URL and the complete matrix
succeeds using only copied test-output data; no environment-dependent network
isolation step is required.

## Task 5: Replace runtime version-only admission with exact supported-build matching

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityInspector.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackFeatureCompatibility.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCompatibilityInspectorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackInactivePathArchitectureContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**

- Replaces the inspector constructor, without retaining an overload:

```csharp
internal FastTrackCompatibilityInspector(
    IFastTrackAssemblyFileIdentityReader assemblyFileIdentityReader,
    FastTrackSupportedAssemblyBuildCatalog supportedAssemblyBuildCatalog)
```

- Replaces `FastTrackFeatureCompatibilityFailureCode.UnsupportedFileVersion`
  with `UnsupportedAssemblyBuild`.
- Production composition passes
  `FastTrackSupportedAssemblyBuildCatalog.Declared`.

- [ ] **Step 1: Rewrite runtime-admission tests first**

Use 64-character test digests and inject a catalog matching the intended test
identity. Replace the version-only test with:

```csharp
[TestMethod]
public void Inspect_WhenActiveBuildHasKnownVersionButDifferentDigest_ClassifiesActiveFeaturesAsUnsupportedAssemblyBuild()
{
    FastTrackEmittedAssembly fixture =
        FastTrackReflectionEmitFixture.CreateExpectedContract();
    var supportedIdentity = new FastTrackAssemblyBuildIdentity(
        new Version(0, 18, 4, 0),
        SupportedDigest);
    var observedIdentity = SuccessfulFileIdentity(
        new Version(0, 18, 4, 0),
        DifferentDigest);

    FastTrackCompatibilityReport report = Inspect(
        fixture,
        new RecordingAssemblyFileIdentityReader(observedIdentity),
        new FastTrackSupportedAssemblyBuildCatalog([supportedIdentity]),
        fixture.AllReplacements.ToArray());

    AssertEveryFeatureHasFailure(
        report,
        FastTrackFeatureCompatibilityFailureCode.UnsupportedAssemblyBuild);
}
```

Also prove:

- exact version and digest proceeds to structural verification;
- unknown version with a known digest is unsupported;
- known version with an unknown digest is unsupported;
- digest casing is normalized;
- malformed observed digest is unsupported without throwing;
- unsupported build affects only active features;
- all inactive replacements remain `ReplacementInactive` for an unsupported
  build;
- file identity is still read exactly once; and
- the unsupported-build diagnostic includes the observed version and complete
  digest but does not enumerate every admitted digest.

- [ ] **Step 2: Run inspector and inactive-path tests and require red**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTrackCompatibilityInspectorTests|FullyQualifiedName~FastTrackInactivePathArchitectureContractTests"
```

Expected: compilation fails because the inspector lacks the catalog parameter
and `UnsupportedAssemblyBuild` does not exist.

- [ ] **Step 3: Implement exact catalog admission before deep verification**

Delete `SupportedFastTrackFileVersion`. Store the non-null injected catalog.
For each feature, preserve this exact evaluation order:

```text
replacement inactive
  -> ReplacementInactive
identity read unavailable
  -> AssemblyFileIdentityUnavailable
version-plus-digest absent from catalog
  -> UnsupportedAssemblyBuild
deep structural contract violation
  -> feature-specific contract violation
verified
  -> Ready
```

The inspector may compute one shared catalog-match fact after its one physical
identity read, but `ClassifyFeature` must return `ReplacementInactive` before
using that fact. Do not move file I/O, SHA computation, fixture access, or
network access into the inspector.

- [ ] **Step 4: Update every constructor call without a compatibility overload**

Production composition passes `FastTrackSupportedAssemblyBuildCatalog.Declared`.
Each reflection-emitted unit test constructs the exact catalog it needs. Update
inactive-path tests similarly. Do not add an optional parameter, default
catalog, obsolete overload, or adapter shim.

- [ ] **Step 5: Update source-boundary assertions**

Change `FastTrackCompatibilityInspectorSource_WhenInspected_...` to require:

- `FastTrackSupportedAssemblyBuildCatalog` in the inspector;
- exactly one `assemblyFileIdentityReader.Read(` call;
- no `new Version(0, 18, 4, 0)` or accepted digest literal in the inspector;
- accepted version and digest literals only in
  `FastTrackSupportedAssemblyBuildCatalog.cs`;
- no file, network, Harmony, Unity, PeterHan, `Type.GetType`, or arbitrary
  assembly-scanning API in the catalog; and
- no catalog type reference in `GameplayActivation/Core`.

- [ ] **Step 6: Run focused runtime and architecture tests**

Run the Step 2 command, then:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~HarmonyPatchContractVerifierTests.FastTrackCompatibilityInspectorSource
```

Expected: exact build admission and all existing feature-specific structural
distinctions pass.

## Task 6: Reconcile the active program, verify the milestone, review, and commit

**Files:**

- Modify: `docs/plans/2026-09-01-temperature-limit-safe-activation-program.md`
- Modify: `docs/plans/2026-09-01-temperature-limit-declared-integration-foundation.md`
- Verify all files named by Tasks 1-5

- [ ] **Step 1: Remove the obsolete mutable-download instruction**

In declared-integration Task 6, replace the instruction to reacquire the old
archive with a prerequisite that this plan is complete. Preserve the exact
static ID assertion and require the adapter to consume the catalog-backed
inspector. Remove the old `0.18.4.0/README.md` file entry and reference the
content-addressed fixture matrix instead.

- [ ] **Step 2: Update program dependency and configuration invariants**

The program order becomes:

```text
1. Declared integration foundation, Tasks 1-5
2. FastTrack compatibility evidence catalog
3. Declared integration foundation, Tasks 6-10
4. Pure activation core
5. Activation failure response
6. Harmony/Klei integration and release evidence
```

Permit only the previously approved `GameplayActivation/Core/**/*.cs` compile
item, the exact FastTrack fixture-copy wildcard, and the scoped `.gitattributes`
DLL rule. Continue to forbid every other configuration change.

- [ ] **Step 3: Run focused and complete verification**

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTrackSupportedAssemblyBuildCatalogTests|FullyQualifiedName~FastTrackPreservedBuildFixtureCatalogTests|FullyQualifiedName~PreservedFastTrackAssemblyContractTests|FullyQualifiedName~FastTrackCompatibilityInspectorTests|FullyQualifiedName~FastTrackInactivePathArchitectureContractTests"
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
git diff --check
```

Expected: every test passes, zero tests are skipped or inconclusive, and
`git diff --check` emits no output.

- [ ] **Step 4: Run configuration and artifact boundary checks**

Require the project diff to contain only the already approved linked core item
and exact fixture-copy wildcard. Require the `.gitattributes` diff to contain
only the scoped DLL rule. Require no diff for every forbidden configuration
file. Search production packaging inputs and output to prove no fixture DLL,
metadata, provenance, license, or README is shipped.

- [ ] **Step 5: Run the formal review gate ourselves**

State exactly:

`Implementation complete; /review pending`

Run built-in uncommitted review scoped to the FastTrack evidence catalog,
content-addressed fixtures, exact runtime admission, program-plan amendments,
approved configuration changes, and associated tests. Explicitly exclude the
user-owned untracked `AGENTS.md`. Resolve or explicitly defer every confirmed
P0-P2 finding, then rerun every affected focused test and the complete suite.

- [ ] **Step 6: Stage only this milestone and inspect the exact snapshot**

Show `git status --short`, `git diff --stat`, `git diff --cached --check`, and
the staged file list. Exclude `AGENTS.md` and unrelated working-tree files.

- [ ] **Step 7: Create the authorized signed commit**

Load and follow `committing-to-git`, then use:

```text
fix(temperature-limit): admit only preserved FastTrack builds

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

- [ ] **Step 8: Do not push**

Report the commit ID, subject, signature result, tests, review result, and the
next active plan task. Pushing requires separate explicit authorization.
