# Delivery Temperature Limit Large-Colony Performance Rewrite Implementation Plan

> **For agentic workers:** REQUIRED EXECUTION MODE: Implement this plan inline with one agent, task by task. **Do not spawn subagents, delegate work, or use parallel agent execution.** Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mod's global temperature-band subsystem with scoped immutable constraints, sparse world inventory, tag/world-specific fetch eligibility, collision-free FastTrack grouping, and game-session-safe publication so very large ONI colonies pay only for distinctions that can affect an actual delivery decision.

**Architecture:** Pure `netstandard2.1` production modules define current-ONI Kelvin semantics, constraint registration, sparse amount series, normalized storage intervals, scoped pickup partitions, content-neutral authoritative world topology, and generation-validated snapshots; the same physical pure files are linked into the `net10.0` test project. Thin Harmony adapters preserve Klei complete-world inventory enumeration or a structurally verified FastTrack complete-first/one-tag-incremental enumeration and publish through one `DeliveryTemperatureGameSession`; base-game versus Spaced Out content mode is an independent topology axis, not an adapter selector. A single coordinated activation removes the old global model instead of bridging it. Focused TDD, mandatory pipeline gates, and signed commits occur throughout; only the modest in-game baseline/candidate comparison waits until every rewrite component is integrated.

**Tech Stack:** SDK-style C# 8.0 targeting .NET Standard 2.1 for the game-loaded mod; C# 14 `net10.0` MSTest.Sdk 4.3.3 test-only code/tooling; the same physical production files linked into tests remain C# 8.0-compatible; `System.Reflection.Metadata` from the modern tooling runtime; Harmony; PLib 4.24.0; ILRepack 2.0.34; current installed ONI changelist 744825/Unity 6000.3.5f2/MonoBleedingEdge; optional best-efforts FastTrack 0.18.4.0 adapter; repository-local .NET 10 ONI Mod Pipeline; configured signed Git commit workflow.

**Spec:** `docs/specs/2026-08-29-delivery-temperature-limit-performance-rewrite-design.md`

## Global Constraints

- Implement the approved specification exactly. If source or installed binary evidence contradicts it, stop and amend the specification and plan with the user; do not improvise a materially different architecture.
- Strictly do not spawn subagents. This applies to implementation, review, research, testing, profiling, and remediation.
- Deliver one big-bang runtime migration. New modules may be developed and tested before activation, but no build intended for players may execute old and new temperature-eligibility algorithms in parallel.
- Run focused red-green-refactor cycles throughout. Direct filtered tests are an inner-loop convenience only. Before every meaningful commit, the complete current working tree must pass pipeline `validate`, pipeline `build`, and pipeline `test`. Only the four-run in-game baseline/candidate comparison is deferred until every fix is integrated.
- Commit after every meaningful complete chunk. A meaningful chunk has a deliberate failing test, complete passing behavior, a buildable affected source set, correct names, durable comments, no temporary diagnostics, no disabled assertions, no unresolved placeholder comment, no half-migrated caller, and no unapproved shim.
- Use the signed commit workflow in `C:\Users\maksy\.agents\skills\committing-to-git`; do not substitute raw `git add` or `git commit`. Obtain exact approval for the prepared snapshot and exact commit message immediately before every commit. Do not push without separate explicit authorization.
- Preserve unrelated user-owned changes. At plan-writing time these include untracked `AGENTS.md` and `mods/delivery-temperature-limit-supercooled/screenshot-guidance.md`; re-check rather than assuming that list remains complete.
- Never create, edit, rename, or delete configuration outside the exact approved staged changes in the Configuration Approval Dossier. Verify the live context before applying an approved edit; a broader or different delta requires renewed exact approval.
- Make no package-version change or package addition. Refresh `Source/packages.lock.json` only for the approved framework-graph change. If implementation appears to need another dependency or graph change, stop and present its exact version, transitive/pipeline impact, and package-free alternative.
- Target the game-loaded assembly exactly to `netstandard2.1`; target tests/tooling exactly to `net10.0`. Do not multi-target, target the game DLL to .NET 8/9/10, retain `net48`, or ship a modern-runtime sidecar.
- Treat C# `8.0` as the exact language ceiling for every production source file, including the same physical files linked into the test project. Test-only `net10.0` files may use C# `14`, but no test-only syntax may leak into a linked production file. Retain the target-framework-derived language defaults and do not add `LangVersion`; `ProjectTargetFrameworkContractTests` and the mandatory production build enforce this split.
- Support the current public ONI build only: changelist `744825`, release branch, `minimumSupportedBuild: 744825`. Do not add historical-signature compatibility analysis or `archived_versions`. If public ONI changes before release, stop and request a decision.
- Preserve serialized type `DeliveryTemperatureLimit.TemperatureLimit` and private serialized integer fields `lowLimit` and `highLimit` with `[KSerialization.Serialize]` and `[UnityEngine.SerializeField]`.
- Preserve option names, JSON properties, defaults, construction behavior, copy-settings behavior, inclusive-low/exclusive-high boundaries, enabled-but-empty semantics, and `(int)temperatureKelvin` truncation toward zero.
- No shims by default. A legacy type, member, alias, wrapper, fallback implementation, or parallel subsystem requires a named reproducible consumer, precise legacy semantics, no clean migration, focused tests, owner/removal criteria, and explicit user approval for that exact exception.
- The single authoritative legacy-removal registry is specification section 6.2. Tasks 24 and 25, the no-shim executable table, and final audits consume that registry by reference; this plan must not maintain a competing symbol/file subset.
- Keep all new domain and adapter types `internal`. Public visibility is allowed only for the curated Unity/Klei/PLib entry points enumerated in the coordinated-activation task.
- Use semantic names from the contract registry. Do not introduce `Helper`, `Utils`, `Common`, `Misc`, bare `Data`, or generic `Manager` names.
- Never use the unqualified word “vanilla.” Use exactly `base-game content mode`, `Spaced Out content mode`, `Klei inventory update path`, `FastTrack inventory update path`, `Klei pickup grouping path`, or `FastTrack pickup grouping path`; use `Klei implementation paths` or `FastTrack implementation paths` only when deliberately referring to several corresponding paths together. Content mode and implementation path are independent axes; names such as `VanillaInventoryAdapter` and `NonVanillaAdapter` are forbidden.
- Add comments for conversion semantics, eligibility invariants, lock/snapshot ownership, generation validation, Harmony anchors, exact stale-snapshot classification, FastTrack key allocation, and high-water retention. Do not comment obvious syntax.
- Never hold more than one domain-service lock at a time. Never call Unity, Klei, PLib, Harmony, FastTrack, logging, sorting, another domain service, or large allocation code while a domain lock is held.
- Worker-capable code may read captured immutable snapshots, thread-confined state, and only the exact pickup candidate/cached-primary fields whose managed access and cross-thread stability were verified before activation. It must not perform `GetComponent`, enumerate `ClusterManager`/`WorldContainer`, query mutable game topology, or call unverified Unity APIs. A failed proof invokes the named subsystem policy—exact bucket classification for stale domain snapshots or coherent activation failure for an unsafe active replacement—rather than weakening this rule.
- A missing/stale pickup partition uses exact temperature-decision classes. Incomplete status inventory leaves ONI's existing availability unchanged. Missing/stale sweep eligibility returns conservative `false` after preserving an original `false` result.
- Publish combined fetch state only when game-session generation, constraint generation, fetch topology version, and world-parent topology version all match the values captured at build start.
- Derive the upper configurable endpoint from the named compile-time constant `OniStorableTemperatureBounds.MaximumTemperatureKelvin`, currently `10000`, whose current-build source is statically verified against `Sim.MaxTemperature`, `PrimaryElement.OnDeserialized`, and `SimMessages.ModifyCell`. Preserve high-exclusive mod semantics; do not expose `10001`.
- Use exactly `TemperatureDecisionBucket.BucketCount = 1 + OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1`, currently `10,002`: one below-range bucket, integer Kelvin `0..9999`, and one at-or-above-maximum bucket. Missing `PrimaryElement` is a separate named classification, never a synthetic temperature ordinal.
- Never scan all `10,002` buckets or `10,001` endpoints in a per-pickup, per-tag, per-world, comparator, suppression, status-query, or recurring-update path. Fixed arrays are permitted only for bounded reusable accumulators/reference counts whose ordinary work touches observed buckets or changed endpoints.
- When FastTrack is absent, disabled, or inactive for the loaded game, install the Klei path directly and perform no FastTrack hot-path lookup, branch, reflection, allocation, or adapter dispatch. Active critical FastTrack contract failure aborts coherent activation before patching; status-only failure omits only status integration.
- Rate-limit diagnostics by game-session generation and diagnostic key. When authority rejection deliberately prevents session publication, key the one cold diagnostic by the rejected `Game` instance/load identity plus diagnostic key instead. Never emit per-pickup or per-status-item warning storms.
- Reverify the selected Klei/FastTrack replacement authority once at every `Game.OnLoadLevel` before publishing the game session. If a later mod changed a required Harmony prefix topology after `OnAllModsLoaded`, publish no DTL game session, leave already installed DTL patches as guarded no-ops, unpatch no third-party owner, and emit one session-scoped diagnostic. This recheck must never enter an update, pickup, status, comparator, or delivery hot path.
- Retained collection limits are immutable production policy constants, never injectable test settings. Tests use each real named limit, `limit + 1`, and a larger lightweight workload; crossing a limit may replace retained storage only after all work completes and must never drop a workload entry.
- Run commands individually. Do not chain commands with `;`, `&&`, `||`, pipes, background operators, or command substitution.
- Use `rg` and `rg --files` for repository searches.
- Use `apply_patch` for targeted file edits. Never use Git restoration commands to undo working-tree changes.
- Do not automate ONI, run a game CPU/allocation/GC profiler campaign, add timing assertions, repeat manual measurements, or require a Markdown performance record. Final game evidence is deliberately indicative: separate baseline-role and candidate-role derivatives copied from each of two untouched late-game colonies, producing exactly four one-pass sessions with all other mods disabled.
- Do not claim completion until the final verification task has fresh pipeline/static output and the exact candidate passes the approved four manual runs. Publishing/uploading remains outside scope and separately authorized.

---

## Delivery Shape and Review Gates

The work is one release-level migration with five review gates. Only Gate D activates the rewrite in the mod entrypoint; Gates A–C add complete tested modules that remain uninstalled and therefore cannot create a mixed runtime.

| Gate | Independently reviewable result | Player runtime path |
|---|---|---|
| A — Canonical state | Kelvin semantics, constraint registry, owned component index, and game-session lifetime | Existing path remains active |
| B — Sparse/scoped domain | World topology, sparse inventory, intervals, partitions, combined fetch snapshots, exhaustive reference-model tests | Existing path remains active |
| C — Verified adapters | Thin Klei/FastTrack implementation-path and lifecycle Harmony adapters compile and their pure contracts are tested; no runtime patch installer exists yet, so nothing can invoke them | Existing path remains active |
| D — Coordinated activation | `TemperatureLimit` and `Mod` switch once; obsolete patch/status/index files are deleted in the same chunk | New path only |
| E — Final evidence | Fresh pipeline suite, static baseline/candidate and runtime-contract analysis, exact candidate install, and four simple Klei-path colony runs | New path only, release-eligible after pass |

Do not install a Gate A–C build into ONI. Those commits are code-review checkpoints, not partially migrated releases.

## Configuration Approval Dossier

The user approved these exact staged changes during grilling. Do not request the same decision again if the live files still match the recorded starting state. Do stop and request renewed approval if a live file changed or the required delta is broader.

### First meaningful implementation commit

1. In `Source/DeliveryTemperatureLimit.csproj`, replace only `net48` with `netstandard2.1`; add `CopyLocalLockFileAssemblies=true` and `TreatWarningsAsErrors=true`; do not yet add project-wide nullable.
2. Refresh `Source/packages.lock.json` only for the target-framework restore graph; retain PLib `4.24.0` and ILRepack `2.0.34` and introduce no deliberate version change.
3. In `Tests/DeliveryTemperatureLimit.Tests.csproj`, add only the linked pure production roots and reflection-only contract sources named in the File and Module Map; retain `net10.0`, MSTest.Sdk `4.3.3`, `Nullable=annotations`, warnings as errors, and locked restore.
4. In `mod_info.yaml`, replace only `minimumSupportedBuild: 596100` with `minimumSupportedBuild: 744825`; retain `supportedContent: ALL`, `version: 2026.8.26`, and `APIVersion: 2`.
5. Add static project/reference/package/ONI contracts. `ProjectTargetFrameworkContractTests` must assert the evaluated production/test target frameworks, the production C# `8.0` versus test-only C# `14` split, and the absence of a `LangVersion` override. Every new C# file begins with `#nullable enable`; nullable annotations do not authorize post-C#-8 syntax in linked production files.

The first commit deliberately leaves project-wide nullable staged because the current legacy game source produces 38 nullable errors and the linked legacy `Buildings.cs` path produces 12. Suppressing those errors or weakening warnings is forbidden.

### FastTrack static-fixture task

1. In Task 21 only, update the SDK-default `None` item for `Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/FastTrack.dll` with `CopyToOutputDirectory=PreserveNewest`.
2. Do not add the DLL as a `<Reference>`, compile item, analyzer, production output, package file, or restore input. Make no other project change in that task.

### Coordinated big-bang activation

1. In `Source/DeliveryTemperatureLimit.csproj`, add `<Nullable>enable</Nullable>` after obsolete legacy files have been deleted or rewritten.
2. In `Tests/DeliveryTemperatureLimit.Tests.csproj`, replace `<Nullable>annotations</Nullable>` with `<Nullable>enable</Nullable>` after the legacy `Buildings.cs` link/file has been removed or rewritten.
3. Make no other configuration change.

### Explicit byte-for-byte invariant

`mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml` must remain byte-for-byte unchanged from the approved 5,413-byte LF file whose SHA-256 is `5A03C7656F75B539B226C1CD6FF231D85C7DE200E701B5274751F09F00739AFD`. The existing profile is the production suite; the manual performance comparison is supplemental and a concise Markdown result is non-blocking.

### Explicitly rejected configuration workarounds

Do not add `LangVersion`, multi-targeting, package references, framework DLLs, binding redirects, `AutoUnify`, `NoWarn`, direct `System.IO.Compression`/`System.Net.Http` pins, application configuration files, CI changes, pipeline acceptance entries, or a sidecar assembly. The two known MSB3277 roots remain visible and are statically bounded instead.

## File and Module Map

All new production types are `internal` unless the coordinated-activation task's curated runtime contract explicitly says otherwise. These exact semantically named roots are authoritative; do not replace them with `Domain`, `Runtime`, `Patches`, `FastTrack`, `Helpers`, `Utils`, or another vague grouping.

```text
mods/delivery-temperature-limit-supercooled/
  Source/
    DeliveryTemperatureLimit.csproj
    packages.lock.json
    DeliveryTemperatureLimitMod.cs
    DeliveryTemperatureLimitOptions.cs
    DeliveryTemperatureLimitStrings.cs
    TemperatureConstraints/
      OniStorableTemperatureBounds.cs
      DeliveryTemperatureConstraint.cs
      TemperatureDecisionBucket.cs
      TemperatureConstraintGeneration.cs
      TemperatureConstraintRegistrationToken.cs
      ActiveTemperatureConstraintSnapshot.cs
      TemperatureConstraintRegistry.cs
      TemperatureLimitComponentIndex.cs
    WorldParentTopology/
      WorldParentTopologyVersion.cs
      WorldParentTopologyChange.cs
      WorldParentTopologySnapshot.cs
      WorldParentTopologyCatalog.cs
    WorldResourceTemperatureAmounts/
      WorldInventoryCollectionGeneration.cs
      TemperatureAmountAccumulator.cs
      TemperatureAmountSeries.cs
      CompleteWorldResourceTemperatureAmounts.cs
      WorldResourceTagCoverage.cs
      WorldResourceTemperatureSeriesPublication.cs
      CompleteWorldResourceTemperatureAmountsBuilder.cs
      WorldResourceTemperatureAmountCatalog.cs
      WorldResourceTagCoverageRequirementState.cs
      TemperatureConstrainedAmountAvailabilityState.cs
      TemperatureConstrainedAmountAvailability.cs
      TemperatureStatusAvailabilityDecision.cs
    FetchTemperatureEligibility/
      AllowedTemperatureInterval.cs
      AllowedTemperatureIntervalSet.cs
      FetchRequestTopologyVersion.cs
      FetchRequestTopologyTracker.cs
      PickupTagIdentity.cs
      TemperaturePartitionDefinition.cs
      TemperatureEligibilityClassificationKind.cs
      TemperatureEligibilityClassKey.cs
      FetchTemperatureEligibilitySnapshot.cs
      FetchTemperatureEligibilityBuilder.cs
      PickupTemperatureGroupingSession.cs
      ClearableDestinationSweepEligibilityInput.cs
      ClearableDestinationSweepEligibility.cs
      FetchChoreTemperatureConstraintContainment.cs
    DeliveryTemperatureGameSessionLifecycle/
      GameSessionGeneration.cs
      GameSessionTemperatureLimitRegistrationToken.cs
      DeliveryTemperatureGameSession.cs
      DeliveryTemperatureGameSessionHost.cs
      ThreadConfinedSessionSlot.cs
      SessionDiagnosticLimiter.cs
      RetainedCollectionCapacityLimits.cs
    RuntimePatchInstallation/
      DeliveryTemperatureRuntimePatchGroup.cs
      DeliveryTemperatureRuntimePatchPlan.cs
      DeliveryTemperatureRuntimePatchInstaller.cs
      DeliveryTemperatureGameLoadAuthorityPatches.cs
      FastTrackDeliveryEligibilityCompatibilityException.cs
    TemperatureLimitedDeliveryTargets/
      TemperatureLimit.cs
      TemperatureLimitedDeliveryTargetPrefabConfigurator.cs
      ConstructionMaterialTemperatureLimit.cs
    KleiImplementationAdapters/
      DeliveryTemperatureGameSessionShutdownPatches.cs
      WorldParentTopologyPatches.cs
      KleiWorldInventoryTemperaturePatches.cs
      TemperatureStatusAvailabilityPatches.cs
      KleiAuthoritativeFetchTemperatureEligibilityPatches.cs
      KleiDirectDeliveryEligibilityPatches.cs
      KleiPickupTemperatureGroupingPatches.cs
    FastTrackCompatibility/
      FeatureContractVerification/
        FastTrackFeature.cs
        FastTrackFeatureCompatibilityState.cs
        FastTrackVerifiedMember.cs
        FastTrackFeatureCompatibility.cs
        FastTrackCompatibilityReport.cs
        FastTrackLoadedGameInspectionInput.cs
        FastTrackAssemblyFileIdentityReadState.cs
        FastTrackAssemblyFileIdentity.cs
        IFastTrackAssemblyFileIdentityReader.cs
        FastTrackAssemblyFileIdentityReader.cs
        ActiveHarmonyPatchDescriptor.cs
        FastTrackCompatibilityInspector.cs
      InventoryUpdateAdapters/
        FastTrackWorldInventoryPublicationKind.cs
        FastTrackWorldInventoryPublicationResult.cs
        FastTrackWorldInventoryPublicationSession.cs
        FastTrackWorldInventoryTemperaturePatches.cs
      PickupGroupingAdapters/
        FastTrackPickupGroupingKeyAllocator.cs
        FastTrackPickupTemperaturePatches.cs
      DirectDeliveryEligibilityAdapters/
        FastTrackDirectDeliveryEligibilityPatches.cs
    TemperatureLimitUserInterface/
      TemperatureLimitWidget.cs
      TemperatureLimitSideScreen.cs
    HarmonyTranspilerInfrastructure/
      HarmonyPatchContractViolationException.cs
      HarmonyPatchContractVerifier.cs
      HarmonyCodeInstructionFactory.cs
  Tests/
    TemperatureConstraints/
      OniStorableTemperatureBoundsTests.cs
      DeliveryTemperatureConstraintTests.cs
      TemperatureDecisionBucketTests.cs
      TemperatureConstraintRegistryTests.cs
      TemperatureLimitComponentIndexTests.cs
    DeliveryTemperatureGameSessionLifecycle/
      DeliveryTemperatureGameSessionTests.cs
      SessionDiagnosticLimiterTests.cs
      ThreadConfinedSessionSlotTests.cs
    RuntimePatchInstallation/
      DeliveryTemperatureRuntimePatchPlanTests.cs
    WorldParentTopology/
      WorldParentTopologyCatalogTests.cs
    WorldResourceTemperatureAmounts/
      TemperatureAmountAccumulatorTests.cs
      TemperatureAmountSeriesTests.cs
      WorldResourceTemperaturePublicationTests.cs
      CompleteWorldResourceTemperatureAmountsBuilderTests.cs
      WorldResourceTemperatureAmountCatalogTests.cs
      TemperatureStatusAvailabilityDecisionTests.cs
    FetchTemperatureEligibility/
      AllowedTemperatureIntervalSetTests.cs
      TemperaturePartitionDefinitionTests.cs
      FetchRequestTopologyTrackerTests.cs
      FetchTemperatureEligibilityBuilderTests.cs
      PickupTemperatureGroupingSessionTests.cs
      ClearableDestinationSweepEligibilityTests.cs
      FetchChoreTemperatureConstraintContainmentTests.cs
      CanonicalTemperatureEligibilityAgreementTests.cs
    TemperatureLimitedDeliveryTargets/
      TemperatureLimitedDeliveryTargetPrefabConfiguratorTests.cs
    FastTrackCompatibility/
      FastTrackPickupGroupingKeyAllocatorTests.cs
      FastTrackWorldInventoryPublicationSessionTests.cs
      FastTrackInactivePathArchitectureContractTests.cs
      FastTrackCompatibilityInspectorTests.cs
      FastTrackAssemblyFileIdentityReaderTests.cs
      FastTrackReflectionEmitFixture.cs
      FastTrackGitHubReleaseAssemblyContractTests.cs
      FastTrackPickupTemperaturePatchContractTests.cs
      FastTrackDirectDeliveryEligibilityPatchContractTests.cs
      FastTrackCoherentActivationContractTests.cs
    HarmonyTranspilerInfrastructure/
      HarmonyPatchContractVerifierTests.cs
    DeliveryTemperatureAssemblyContracts/
      OniStorableTemperatureBoundsContractTests.cs
      CurrentOniRuntimeContractTests.cs
      ProjectTargetFrameworkContractTests.cs
      KnownOniReferenceConflictContractTests.cs
      DeliveryTemperatureAssemblyMetadataReader.cs
      MergedDeliveryTemperatureAssemblyContractTests.cs
      DeliveryTemperaturePackageBoundaryContractTests.cs
      IntentionalRuntimeContractTests.cs
      NoShimArchitectureContractTests.cs
      ImplementationTerminologyContractTests.cs
      PerformanceArchitectureContractTests.cs
      LinkedProductionSourceBoundaryContractTests.cs
    ReferenceTemperatureModels/
      ReferenceTemperatureEligibilityModel.cs
      ReferenceWorldResourceTemperatureAmounts.cs
    OniModPipelineIntegration/
      OniModPipelineProfileInvarianceTests.cs
      PipelineProvenanceBoundAssemblyLocator.cs
      PipelineTestTemporaryDirectory.cs
      DotnetCommandRunner.cs
    TestDoubles/
      OniGameTypeStubs.cs
    Fixtures/
      ThirdParty/
        FastTrack/
          0.18.4.0/
            README.md
            FastTrack.dll
    DeliveryTemperatureLimit.Tests.csproj
    packages.lock.json
  DeliveryTemperatureLimit.dll
  mod.yaml
  mod_info.yaml
  oni-mod-pipeline.toml
```

At coordinated activation, delete every superseded file and member in the authoritative legacy-removal registry in specification section 6.2. This module map does not repeat that list; any task that needs the removal scope must reference that section.

`Source/HarmonyTranspilerInfrastructure/HarmonyCodeInstructionFactory.cs` replaces only instruction-construction mechanics that remain necessary. It does not preserve the public `CodeInstruction2` name or act as a compatibility facade. Fixture DLL inclusion in the test project must use `CopyToOutputDirectory=PreserveNewest` or an equivalent exact test-data item; the pipeline package-boundary contract must prove neither fixture enters the released mod package.

## Cross-Task Contract Registry

Later tasks must use these names and signatures. A genuine implementation discovery may change them only through a coordinated plan/spec amendment before dependent code is written.

```csharp
internal static class OniStorableTemperatureBounds
{
    // ONI release changelist 744825: Sim.MaxTemperature == 10000f;
    // PrimaryElement.OnDeserialized and SimMessages.ModifyCell accept the bound inclusively.
    internal const int MinimumTemperatureKelvin = 0;
    internal const int MaximumTemperatureKelvin = 10000;
}

internal readonly struct DeliveryTemperatureConstraint : IEquatable<DeliveryTemperatureConstraint>
{
    internal int MinimumInclusiveKelvin { get; }
    internal int MaximumExclusiveKelvin { get; }
    internal bool IsEnabled { get; }
    internal bool IsEmpty { get; }

    internal static DeliveryTemperatureConstraint FromSerializedLimits(
        int serializedLowLimit,
        int serializedHighLimit);
    internal bool Allows(float temperatureKelvin);
}

internal readonly struct TemperatureDecisionBucket :
    IEquatable<TemperatureDecisionBucket>, IComparable<TemperatureDecisionBucket>
{
    internal const int BucketCount =
        1 + OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;
    internal const int BelowMinimumKelvinOrdinal = 0;
    internal const int FirstIntegerKelvinOrdinal = BelowMinimumKelvinOrdinal + 1;
    internal const int AtOrAboveMaximumKelvinOrdinal = BucketCount - 1;
    internal const int HighestIntegerKelvinOrdinal =
        AtOrAboveMaximumKelvinOrdinal - 1;
    internal int Ordinal { get; }
    internal bool IsBelowMinimumKelvin { get; }
    internal bool IsAtOrAboveMaximumKelvin { get; }
    internal bool TryGetIntegerKelvin(out int integerKelvin);
    internal static TemperatureDecisionBucket FromTemperature(float temperatureKelvin);
    internal static TemperatureDecisionBucket FromIntegerKelvin(int truncatedKelvin);
}

internal readonly struct TemperatureConstraintGeneration : IEquatable<TemperatureConstraintGeneration>
{
    internal long Value { get; }
    internal TemperatureConstraintGeneration(long value);
}

internal readonly struct TemperatureConstraintRegistrationToken :
    IEquatable<TemperatureConstraintRegistrationToken>
{
    internal int ComponentInstanceId { get; }
    internal long RegistrationSequence { get; }
    internal TemperatureConstraintRegistrationToken(
        int componentInstanceId,
        long registrationSequence);
}

internal sealed class ActiveTemperatureConstraintSnapshot
{
    internal TemperatureConstraintGeneration Generation { get; }
    internal int EnabledConstraintCount { get; }
    internal int EnabledNonEmptyConstraintCount { get; }
    internal IReadOnlyList<int> SortedDecisionEndpointsKelvin { get; }
}

internal sealed class TemperatureConstraintRegistry
{
    internal ActiveTemperatureConstraintSnapshot CaptureSnapshot();
    internal TemperatureConstraintRegistrationToken Register(
        int componentInstanceId,
        DeliveryTemperatureConstraint constraint,
        out bool effectiveStateChanged);
    internal bool TryReplace(
        TemperatureConstraintRegistrationToken registrationToken,
        DeliveryTemperatureConstraint constraint,
        out bool effectiveStateChanged);
    internal bool TryRemove(
        TemperatureConstraintRegistrationToken registrationToken,
        out bool effectiveStateChanged);
}

internal sealed class TemperatureLimitComponentIndex
{
    internal bool TryRegister(
        int gameObjectInstanceId,
        TemperatureLimit component,
        TemperatureConstraintRegistrationToken registrationToken,
        DeliveryTemperatureConstraint constraint);
    internal bool TryReplaceConstraint(
        int gameObjectInstanceId,
        TemperatureConstraintRegistrationToken registrationToken,
        DeliveryTemperatureConstraint constraint);
    internal bool TryRemove(
        int gameObjectInstanceId,
        TemperatureConstraintRegistrationToken registrationToken);
    internal bool TryGetRegisteredComponent(
        int gameObjectInstanceId,
        out TemperatureLimit component,
        out TemperatureConstraintRegistrationToken registrationToken);
    internal bool TryGetConstraint(
        int gameObjectInstanceId,
        out DeliveryTemperatureConstraint constraint,
        out TemperatureConstraintRegistrationToken registrationToken);
}

internal readonly struct GameSessionGeneration : IEquatable<GameSessionGeneration>
{
    internal long Value { get; }
    internal GameSessionGeneration(long value);
}

internal readonly struct GameSessionTemperatureLimitRegistrationToken :
    IEquatable<GameSessionTemperatureLimitRegistrationToken>
{
    internal GameSessionGeneration GameSessionGeneration { get; }
    internal int GameObjectInstanceId { get; }
    internal TemperatureConstraintRegistrationToken ConstraintRegistrationToken { get; }
    internal GameSessionTemperatureLimitRegistrationToken(
        GameSessionGeneration gameSessionGeneration,
        int gameObjectInstanceId,
        TemperatureConstraintRegistrationToken constraintRegistrationToken);
}

internal sealed class DeliveryTemperatureGameSession
{
    internal GameSessionGeneration Generation { get; }
    internal int GameInstanceId { get; }
    internal bool IsAcceptingPublications { get; }
    internal TemperatureConstraintRegistry TemperatureConstraints { get; }
    internal TemperatureLimitComponentIndex TemperatureLimitComponents { get; }
    internal WorldParentTopologyCatalog WorldParentTopology { get; }
    internal WorldResourceTemperatureAmountCatalog WorldResourceTemperatureAmounts { get; }
    internal FetchRequestTopologyTracker FetchRequestTopology { get; }
    internal WorldInventoryCollectionGeneration CurrentWorldInventoryCollectionGeneration { get; }
    internal FetchTemperatureEligibilitySnapshot? CurrentFetchTemperatureEligibility { get; }

    internal GameSessionTemperatureLimitRegistrationToken RegisterTemperatureLimit(
        int gameObjectInstanceId,
        int componentInstanceId,
        TemperatureLimit component,
        DeliveryTemperatureConstraint constraint);
    internal bool TryReplaceTemperatureConstraint(
        GameSessionTemperatureLimitRegistrationToken registrationToken,
        DeliveryTemperatureConstraint constraint);
    internal void RemoveTemperatureLimit(
        GameSessionTemperatureLimitRegistrationToken registrationToken);
    internal bool TryPublishFetchTemperatureEligibility(
        FetchTemperatureEligibilitySnapshot candidate);
    internal WorldParentTopologyChange RegisterWorld(
        int worldId,
        int parentWorldId);
    internal WorldParentTopologyChange RemoveWorld(int worldId);
    internal void StopAcceptingPublications();
    internal void ReleaseOwnedState();
}

internal static class DeliveryTemperatureGameSessionHost
{
    internal static DeliveryTemperatureGameSession EnsureGameSession(int gameInstanceId);
    internal static bool TryCaptureCurrent(out DeliveryTemperatureGameSession session);
    internal static DeliveryTemperatureGameSession? DetachGameSession(int gameInstanceId);
    internal static void CompleteShutdown(DeliveryTemperatureGameSession? detachedSession);
}
```

```csharp
internal readonly struct WorldParentTopologyVersion : IEquatable<WorldParentTopologyVersion>
{
    internal long Value { get; }
    internal WorldParentTopologyVersion(long value);
}

internal readonly struct WorldParentTopologyChange
{
    internal bool HasChanged { get; }
    internal int WorldId { get; }
    internal int? PreviousParentWorldId { get; }
    internal int? CurrentParentWorldId { get; }
}

internal sealed class WorldParentTopologySnapshot
{
    internal GameSessionGeneration GameSessionGeneration { get; }
    internal WorldParentTopologyVersion Version { get; }
    internal bool TryResolveParentWorld(int worldId, out int parentWorldId);
    internal IReadOnlyList<int> GetMemberWorldIds(int parentWorldId);
}

internal sealed class WorldParentTopologyCatalog
{
    internal WorldParentTopologySnapshot CaptureSnapshot();
    internal WorldParentTopologyChange RegisterWorld(int worldId, int parentWorldId);
    internal WorldParentTopologyChange RemoveWorld(int worldId);
}

internal readonly struct WorldInventoryCollectionGeneration :
    IEquatable<WorldInventoryCollectionGeneration>
{
    internal long Value { get; }
    internal WorldInventoryCollectionGeneration(long value);
}

internal sealed class TemperatureAmountAccumulator
{
    internal void BeginResourceTag();
    internal void AddTemperatureAmount(float temperatureKelvin, float amount);
    internal TemperatureAmountSeries BuildSeries();
}

internal sealed class TemperatureAmountSeries
{
    internal static TemperatureAmountSeries Empty { get; }
    internal int OccupiedBucketCount { get; }
    internal float TotalAmount { get; }
    internal float GetAmountAllowedBy(DeliveryTemperatureConstraint constraint);
    internal static TemperatureAmountSeries Combine(
        IReadOnlyList<TemperatureAmountSeries> sourceSeries);
}

internal sealed class CompleteWorldResourceTemperatureAmounts
{
    internal WorldInventoryCollectionGeneration CollectionGeneration { get; }
    internal bool TryGetSeries(Tag resourceTag, out TemperatureAmountSeries series);
    internal IReadOnlyList<Tag> ResourceTags { get; }
}

internal sealed class WorldResourceTagCoverage
{
    internal WorldInventoryCollectionGeneration CollectionGeneration { get; }
    internal IReadOnlyList<Tag> PresentResourceTags { get; }
    internal bool Contains(Tag resourceTag);
    internal static WorldResourceTagCoverage Create(
        WorldInventoryCollectionGeneration collectionGeneration,
        IReadOnlyCollection<Tag> presentResourceTags);
}

internal readonly struct WorldResourceTemperatureSeriesPublication
{
    internal WorldInventoryCollectionGeneration CollectionGeneration { get; }
    internal Tag ResourceTag { get; }
    internal TemperatureAmountSeries TemperatureAmounts { get; }
    internal WorldResourceTemperatureSeriesPublication(
        WorldInventoryCollectionGeneration collectionGeneration,
        Tag resourceTag,
        TemperatureAmountSeries temperatureAmounts);
}

internal sealed class CompleteWorldResourceTemperatureAmountsBuilder
{
    internal void BeginWorld(WorldInventoryCollectionGeneration collectionGeneration);
    internal void BeginResourceTag(Tag resourceTag);
    internal void AddTemperatureAmount(float temperatureKelvin, float amount);
    internal void CompleteResourceTag();
    internal CompleteWorldResourceTemperatureAmounts Build();
    internal void Discard();
}

internal sealed class WorldResourceTemperatureAmountCatalog
{
    internal void RegisterWorld(int worldId, int parentWorldId);
    internal bool PublishCompleteWorldResourceAmounts(
        int worldId,
        CompleteWorldResourceTemperatureAmounts resourceAmounts);
    internal bool PublishWorldResourceTagCoverage(
        int worldId,
        WorldResourceTagCoverage resourceTagCoverage);
    internal bool PublishWorldResourceTemperatureSeries(
        int worldId,
        WorldResourceTemperatureSeriesPublication temperatureSeriesPublication);
    internal WorldResourceTagCoverageRequirementState
        GetWorldResourceTagCoverageRequirementState(
        int worldId,
        WorldInventoryCollectionGeneration expectedCollectionGeneration);
    internal TemperatureConstrainedAmountAvailability
        GetTemperatureConstrainedAmountAvailability(
        int parentWorldId,
        Tag resourceTag,
        DeliveryTemperatureConstraint constraint,
        WorldInventoryCollectionGeneration expectedCollectionGeneration);
    internal void RemoveWorld(int worldId);
    internal void ClearTemperatureAmountPublicationsForCollectionBypass();
    internal void ClearForGameSession();
}

internal enum WorldResourceTagCoverageRequirementState
{
    UnknownWorldOrCollectionGeneration,
    CoverageRequired,
    CoverageCurrent
}

internal enum TemperatureConstrainedAmountAvailabilityState
{
    TemperatureConstraintDisabled,
    InventoryIncomplete,
    Complete
}

internal readonly struct TemperatureConstrainedAmountAvailability
{
    internal TemperatureConstrainedAmountAvailabilityState State { get; }
    internal bool TryGetCompleteAvailableAmount(out float availableAmount);
    internal static TemperatureConstrainedAmountAvailability TemperatureConstraintDisabled();
    internal static TemperatureConstrainedAmountAvailability InventoryIncomplete();
    internal static TemperatureConstrainedAmountAvailability Complete(float availableAmount);
}
```

```csharp
internal readonly struct AllowedTemperatureInterval : IEquatable<AllowedTemperatureInterval>
{
    internal int MinimumInclusiveKelvin { get; }
    internal int MaximumExclusiveKelvin { get; }
    internal AllowedTemperatureInterval(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin);
}

internal sealed class AllowedTemperatureIntervalSet
{
    internal bool AllowsNoTemperature { get; }
    internal bool AllowsEveryTemperature { get; }
    internal IReadOnlyList<AllowedTemperatureInterval> Intervals { get; }
    internal bool Allows(TemperatureDecisionBucket bucket);
    internal static AllowedTemperatureIntervalSet CreateFromDestinations(
        bool includesUnconstrainedDestination,
        IReadOnlyList<DeliveryTemperatureConstraint> enabledDestinationConstraints);
}

internal readonly struct FetchRequestTopologyVersion : IEquatable<FetchRequestTopologyVersion>
{
    internal long Value { get; }
    internal FetchRequestTopologyVersion(long value);
}

internal sealed class FetchRequestTopologyTracker
{
    internal FetchRequestTopologyVersion CaptureVersion();
    internal FetchRequestTopologyVersion RecordEffectiveChange();
}

internal readonly struct PickupTagIdentity : IEquatable<PickupTagIdentity>
{
    internal int OriginalTagBitsHash { get; }
    internal Tag PrefabTag { get; }
    internal PickupTagIdentity(int originalTagBitsHash, Tag prefabTag);
}

internal sealed class TemperaturePartitionDefinition
{
    internal int DefinitionId { get; }
    internal IReadOnlyList<int> SortedDecisionEndpointsKelvin { get; }
    internal int Classify(TemperatureDecisionBucket bucket);
    internal static TemperaturePartitionDefinition Create(
        int definitionId,
        IReadOnlyList<int> decisionEndpointsKelvin);
}

internal enum TemperatureEligibilityClassificationKind
{
    NoTemperatureDistinction,
    OptimizedPartitionInterval,
    ExactTemperatureDecisionBucket,
    MissingPrimaryElement
}

internal readonly struct TemperatureEligibilityClassKey :
    IEquatable<TemperatureEligibilityClassKey>, IComparable<TemperatureEligibilityClassKey>
{
    internal TemperatureEligibilityClassificationKind ClassificationKind { get; }
    internal int PartitionDefinitionId { get; }
    internal int IntervalOrdinal { get; }
    internal TemperatureDecisionBucket ExactTemperatureDecisionBucket { get; }

    internal static TemperatureEligibilityClassKey NoTemperatureDistinction();
    internal static TemperatureEligibilityClassKey OptimizedPartitionInterval(
        int partitionDefinitionId,
        int intervalOrdinal);
    internal static TemperatureEligibilityClassKey ExactDecisionBucket(
        TemperatureDecisionBucket temperatureDecisionBucket);
    internal static TemperatureEligibilityClassKey MissingPrimaryElement();
}

internal sealed class FetchTemperatureEligibilitySnapshot
{
    internal GameSessionGeneration GameSessionGeneration { get; }
    internal TemperatureConstraintGeneration ConstraintGeneration { get; }
    internal FetchRequestTopologyVersion FetchTopologyVersion { get; }
    internal WorldParentTopologyVersion WorldTopologyVersion { get; }
    internal bool TryGetStorageEligibility(
        int parentWorldId,
        Tag requestedTag,
        out AllowedTemperatureIntervalSet intervals);
    internal IReadOnlyList<Tag> GetRequestedTags(int parentWorldId);
    internal IReadOnlyList<int> CreateSortedDecisionEndpointUnion(
        int parentWorldId,
        IReadOnlyList<Tag> applicableRequestedTags);
}

internal sealed class FetchTemperatureEligibilityBuilder
{
    internal void Begin(
        GameSessionGeneration gameSessionGeneration,
        ActiveTemperatureConstraintSnapshot constraints,
        FetchRequestTopologyVersion fetchTopologyVersion,
        WorldParentTopologySnapshot worldTopology);
    internal void AddUnconstrainedFetchRequest(
        int parentWorldId,
        IReadOnlyList<Tag> requestedTags);
    internal void AddTemperatureConstrainedFetchRequest(
        int parentWorldId,
        IReadOnlyList<Tag> requestedTags,
        DeliveryTemperatureConstraint enabledConstraint);
    internal FetchTemperatureEligibilitySnapshot Build();
    internal void Discard();
}

internal sealed class PickupTemperatureGroupingSession
{
    internal void Begin(
        DeliveryTemperatureGameSession session,
        int? resolvedParentWorldId,
        ActiveTemperatureConstraintSnapshot constraints,
        FetchTemperatureEligibilitySnapshot? eligibilitySnapshot,
        WorldParentTopologySnapshot worldTopology);
    internal TemperatureEligibilityClassKey Classify(
        int pickupInstanceId,
        PickupTagIdentity tagIdentity,
        IReadOnlyList<Tag> applicableRequestedTags,
        bool hasPrimaryElement,
        float temperatureKelvin);
    internal void Complete();
    internal void Discard();
}

internal readonly struct ClearableDestinationSweepEligibilityInput
{
    internal bool OriginalHasDestination { get; }
    internal int EnabledTemperatureConstraintCount { get; }
    internal bool HasPrimaryElement { get; }
    internal bool IsParentWorldResolved { get; }
    internal bool IsEligibilitySnapshotCurrent { get; }
    internal bool CurrentEligibilityAllowsPickup { get; }
    internal ClearableDestinationSweepEligibilityInput(
        bool originalHasDestination,
        int enabledTemperatureConstraintCount,
        bool hasPrimaryElement,
        bool isParentWorldResolved,
        bool isEligibilitySnapshotCurrent,
        bool currentEligibilityAllowsPickup);
}

internal static class ClearableDestinationSweepEligibility
{
    internal static bool AllowsClearing(
        ClearableDestinationSweepEligibilityInput input);
}

internal sealed class FastTrackPickupGroupingKeyAllocator
{
    internal void Begin(bool temperatureGroupingIsActive);
    internal int GetOrAllocate(
        int originalTagBitsHash,
        TemperatureEligibilityClassKey temperatureClass);
    internal void Complete();
    internal void Discard();
}

internal enum FastTrackFeature
{
    WorldInventory,
    PickupGrouping,
    DirectDeliveryEligibility
}

internal enum FastTrackFeatureCompatibilityState
{
    ModNotLoaded,
    ReplacementInactive,
    Ready,
    Incompatible
}

internal enum FastTrackVerifiedMember
{
    BackgroundWorldInventoryRunUpdate,
    BackgroundWorldInventorySumTotal,
    BackgroundWorldInventoryFirstUpdateField,
    BackgroundWorldInventoryWorldInventoryField,
    BackgroundWorldInventoryWorldContainerField,
    BackgroundInventoryUpdaterStartUpdateAll,
    WorldInventoryUpdateReplacementPrefix,
    WorldInventoryRemovedFetchablePrefix,
    FetchManagerBeforeUpdatePickups,
    PickupTagDictionaryAddItem,
    PickupTagKeyConstructor,
    DirectChoreComparisonMethod
}

internal sealed class FastTrackFeatureCompatibility
{
    internal FastTrackFeature Feature { get; }
    internal FastTrackFeatureCompatibilityState State { get; }
    internal string? FailureCode { get; }
    internal string? FailureMessage { get; }
    internal bool TryGetVerifiedMember(
        FastTrackVerifiedMember requestedMember,
        out MemberInfo member);
}

internal sealed class FastTrackCompatibilityReport
{
    internal string? AssemblyIdentity { get; }
    internal Version? AssemblyVersion { get; }
    internal Version? FileVersion { get; }
    internal string? AssemblySha256 { get; }
    internal FastTrackFeatureCompatibility GetFeature(FastTrackFeature feature);
}

internal sealed class FastTrackLoadedGameInspectionInput
{
    internal bool IsFastTrackEnabledForActiveContent { get; }
    internal Assembly? FastTrackAssembly { get; }
    internal IReadOnlyList<ActiveHarmonyPatchDescriptor> ActivePatches { get; }
    internal FastTrackLoadedGameInspectionInput(
        bool isFastTrackEnabledForActiveContent,
        Assembly? fastTrackAssembly,
        IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches);
}

internal enum FastTrackAssemblyFileIdentityReadState
{
    Available,
    DynamicAssembly,
    LocationUnavailable,
    FileUnavailable,
    ReadFailed
}

internal sealed class FastTrackAssemblyFileIdentity
{
    internal FastTrackAssemblyFileIdentityReadState State { get; }
    internal Version? FileVersion { get; }
    internal string? AssemblySha256 { get; }
    internal string? FailureMessage { get; }
    internal static FastTrackAssemblyFileIdentity Available(
        Version fileVersion,
        string assemblySha256);
    internal static FastTrackAssemblyFileIdentity Unavailable(
        FastTrackAssemblyFileIdentityReadState state,
        string failureMessage);
}

internal interface IFastTrackAssemblyFileIdentityReader
{
    FastTrackAssemblyFileIdentity Read(Assembly assembly);
}

internal sealed class FastTrackAssemblyFileIdentityReader :
    IFastTrackAssemblyFileIdentityReader
{
    FastTrackAssemblyFileIdentity IFastTrackAssemblyFileIdentityReader.Read(
        Assembly assembly);
}

internal sealed class ActiveHarmonyPatchDescriptor
{
    internal MethodBase TargetMethod { get; }
    internal MethodInfo PatchMethod { get; }
    internal string HarmonyOwner { get; }
    internal int Priority { get; }
    internal ActiveHarmonyPatchDescriptor(
        MethodBase targetMethod,
        MethodInfo patchMethod,
        string harmonyOwner,
        int priority);
}

internal sealed class FastTrackCompatibilityInspector
{
    internal FastTrackCompatibilityInspector(
        IFastTrackAssemblyFileIdentityReader assemblyFileIdentityReader);
    internal FastTrackCompatibilityReport Inspect(
        FastTrackLoadedGameInspectionInput inspectionInput);
}

internal enum FastTrackWorldInventoryPublicationKind
{
    CompleteWorldAmounts,
    ResourceTagCoverageAndTemperatureSeries,
    ResourceTemperatureSeries,
    ResourceTagCoverageOnly
}

internal sealed class FastTrackWorldInventoryPublicationResult
{
    internal FastTrackWorldInventoryPublicationKind PublicationKind { get; }
    internal bool TryGetCompleteWorldResourceAmounts(
        out CompleteWorldResourceTemperatureAmounts resourceAmounts);
    internal bool TryGetResourceTagCoverage(
        out WorldResourceTagCoverage resourceTagCoverage);
    internal bool TryGetResourceTemperatureSeries(
        out WorldResourceTemperatureSeriesPublication temperatureSeriesPublication);
}

internal sealed class FastTrackWorldInventoryPublicationSession
{
    internal void BeginCompleteWorldUpdate(
        GameSessionGeneration gameSessionGeneration,
        WorldInventoryCollectionGeneration collectionGeneration);
    internal void BeginIncrementalResourceTagUpdateRequiringCoverage(
        GameSessionGeneration gameSessionGeneration,
        WorldInventoryCollectionGeneration collectionGeneration,
        IReadOnlyCollection<Tag> presentResourceTags);
    internal void BeginIncrementalResourceTagUpdateWithCurrentCoverage(
        GameSessionGeneration gameSessionGeneration,
        WorldInventoryCollectionGeneration collectionGeneration);
    internal void BeginResourceTag(Tag resourceTag);
    internal void AddTemperatureAmount(float temperatureKelvin, float amount);
    internal void CompleteResourceTag();
    internal FastTrackWorldInventoryPublicationResult Complete();
    internal void Discard();
}

internal enum DeliveryTemperatureRuntimePatchGroup
{
    GameSessionLifecycle,
    WorldParentTopology,
    KleiAuthoritativeFetchTemperatureEligibility,
    KleiWorldInventoryTemperaturePublication,
    FastTrackWorldInventoryTemperaturePublication,
    TemperatureStatusAvailability,
    KleiPickupTemperatureGrouping,
    FastTrackPickupTemperatureGrouping,
    KleiDirectDeliveryEligibility,
    FastTrackDirectDeliveryEligibility
}

internal sealed class DeliveryTemperatureRuntimePatchPlan
{
    internal IReadOnlyList<DeliveryTemperatureRuntimePatchGroup> OrderedPatchGroups { get; }
    internal string? StatusCompatibilityDiagnostic { get; }

    internal static DeliveryTemperatureRuntimePatchPlan Create(
        bool checkTemperatureForStatusItems,
        FastTrackCompatibilityReport fastTrackCompatibility);
    internal void VerifySelectedAuthority(
        IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches);
}

internal static class DeliveryTemperatureRuntimePatchInstaller
{
    internal static bool TryStartAuthorizedGameSession(Game game);
}

internal static class DeliveryTemperatureGameLoadAuthorityPatches
{
    internal static MethodInfo ResolveGameOnLoadLevelTarget();
    internal static void GameOnLoadLevelPrefix(Game __instance);
}

internal sealed class FastTrackDeliveryEligibilityCompatibilityException : Exception
{
    internal FastTrackDeliveryEligibilityCompatibilityException(
        string message,
        FastTrackCompatibilityReport compatibilityReport);
}

internal enum DeclaredMemberVisibility
{
    Public,
    NonPublic
}

internal enum FieldStorageKind
{
    Instance,
    Static
}

internal static class HarmonyPatchContractVerifier
{
    internal static MethodInfo RequireInstanceMethod(
        Type declaringType,
        string methodName,
        DeclaredMemberVisibility visibility,
        Type returnType,
        IReadOnlyList<Type> orderedParameterTypes);
    internal static MethodInfo RequireStaticMethod(
        Type declaringType,
        string methodName,
        DeclaredMemberVisibility visibility,
        Type returnType,
        IReadOnlyList<Type> orderedParameterTypes);
    internal static ConstructorInfo RequireConstructor(
        Type declaringType,
        DeclaredMemberVisibility visibility,
        IReadOnlyList<Type> orderedParameterTypes);
    internal static FieldInfo RequireField(
        Type declaringType,
        string fieldName,
        DeclaredMemberVisibility visibility,
        FieldStorageKind storageKind,
        Type fieldType);
    internal static Type RequireNestedType(
        Type declaringType,
        string nestedTypeName,
        DeclaredMemberVisibility visibility);
    internal static T RequireSingleMatch<T>(
        IReadOnlyList<T> candidates,
        Func<T, bool> semanticMatch,
        string semanticAnchorName);
    internal static bool VerifyKleiAuthority(
        MethodBase targetMethod,
        IReadOnlyList<ActiveHarmonyPatchDescriptor> activePatches,
        IReadOnlyCollection<string> permittedSkippingPrefixOwners);
}
```

The registry above is final before Task 1: later tasks implement these interfaces but do not replace or append a previously known declaration. If installed evidence requires a different interface, stop for a coordinated specification/plan amendment before writing dependent code.

`DeliveryTemperatureConstraint.FromSerializedLimits` first clamps each raw serialized value independently to `0..OniStorableTemperatureBounds.MaximumTemperatureKelvin`. A normalized high value of zero then produces a constraint with `IsEnabled == false` and `IsEmpty == false`; only a constraint with `IsEnabled == true` can have `IsEmpty == true`, and it is empty exactly when its normalized low is greater than or equal to its normalized high. This preserves the existing negative-high-to-disabled save behavior. `CreateFromDestinations` accepts only enabled entries in `enabledDestinationConstraints`; a disabled entry is a caller contract violation. An unconstrained destination wins before enabled-empty entries are considered.

The test double identities are also registry contracts: `Tag` is `global::Tag`, and the component stub is `DeliveryTemperatureLimit.TemperatureLimit`. The component stub is initially an empty sealed reference type because linked production source only stores/returns its identity; component-index tests distinguish owners with `Assert.AreSame`, not a convenience label. A stub gains a member only in the same TDD chunk where linked production source genuinely consumes that exact production member and the parity contract is amended. `LinkedProductionSourceBoundaryContractTests` fail when a linked source consumes a member that the corresponding stub does not define, when either namespace/type identity drifts, or when a test-only member is referenced from production.

Ordinary behavior tests use module interfaces. Only these representation contracts may use narrowly scoped private reflection, and each test must assert its exact field before mutation/identity comparison:

- `TemperatureConstraintRegistry.nextRegistrationSequence` for checked registration-token exhaustion;
- `TemperatureConstraintRegistry.generation` for checked registry-generation exhaustion;
- `DeliveryTemperatureGameSessionHost.lastIssuedGameSessionGeneration` for checked lock-free game-session-generation exhaustion;
- `WorldParentTopologyCatalog.version` for checked topology-version exhaustion;
- `FetchRequestTopologyTracker.version` for checked fetch-topology-version exhaustion;
- `DeliveryTemperatureGameSession.currentWorldInventoryCollectionGenerationValue` for checked inventory-collection-generation exhaustion;
- `TemperatureAmountAccumulator.currentResourceTagStamp` for generation wrap recovery;
- `CompleteWorldResourceTemperatureAmountsBuilder.temperatureAmountsByResourceTag` for retained-map replacement;
- `WorldResourceTemperatureAmountCatalog.aggregatesByParentWorldAndResourceTag` for unaffected aggregate reuse;
- `FetchTemperatureEligibilityBuilder.destinationRequirementsByParentWorldAndRequestedTag` for retained-map replacement;
- `PickupTemperatureGroupingSession.temperatureClassesByPickupInstanceId` for retained-map replacement;
- `FastTrackPickupGroupingKeyAllocator.nextAllocatedGroupingKey` for integer exhaustion; and
- `FastTrackPickupGroupingKeyAllocator.allocatedGroupingKeysByCompositeIdentity` for retained-map replacement.

No diagnostic property, test-only constructor, injectable production limit, conditional test branch, or public/internal facade may be added merely to expose these representations.

## Test Naming, Seeds, and Reference Rules

- Behavioral test methods use `Operation_WhenCondition_ExpectedOutcome`. Architecture, identity, publication, and concurrency contracts may use `Subject_WhenCondition_ExpectedOutcome`. Every name has exactly three semantic segments and each test proves one outcome.
- Exhaustive temperature tests iterate `TemperatureDecisionBucket.BelowMinimumKelvinOrdinal` through `AtOrAboveMaximumKelvinOrdinal` inclusive. Tests derive all counts from named production bounds and also assert that build `744825` resolves the formula to `10,002`; loops must not repeat the literal as an independent bound. Ordinal expectations use `FirstIntegerKelvinOrdinal` or `HighestIntegerKelvinOrdinal`, never a Kelvin-value constant merely because its current integer equals an ordinal.
- Use fixed seeds and print the seed plus generated operation index in assertion messages:
  - constraint registry operations: `0x51A7E`;
  - interval normalization: `0x1A7E2A1`;
  - amount series: `0xA60A17`;
  - combined fetch eligibility: `0xFE7C4`;
  - lifecycle/concurrency schedules: `0x5E5510`; and
  - world-resource catalog publication/topology schedules: `0xC47A109`.
- `Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs` and `ReferenceWorldResourceTemperatureAmounts.cs` must implement direct loops independently. They must not call production normalization, bucket classification, prefix summation, partition classification, interval merging, or catalog aggregation.
- Ordinary unit tests assert counts, equality, generations, completeness, and allocation structure. Do not use wall-clock thresholds in the unit suite.
- Every exception assertion uses MSTest 4 `Assert.ThrowsExactly<T>`, verifies a message naming the violated invariant with `Assert.Contains(expectedSubstring, actualMessage)`, and does not pin punctuation or full stack traces. New sequence assertions use `Assert.AreSequenceEqual(expected, actual)` rather than legacy `CollectionAssert`.

## Focused Command Catalog

Run commands from the repository root unless a task explicitly says otherwise.

Restore the mod tests after the approved project edit:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --locked-mode
```

Run one test class:

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore -- --filter "FullyQualifiedName~CLASS_NAME"
```

Replace `CLASS_NAME` with the exact class named by the task. Do not pass several commands in one shell invocation.

This `--filter` form was executed successfully with the repository's MSTest.Sdk `4.3.3` project on 2026-08-29. It is the one authoritative focused syntax. Do not add or silently select a `--treenode-filter` fallback; if a future test-platform version rejects this command, stop and amend the catalog from fresh evidence.

Validate through the repository-local pipeline before every meaningful commit:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- validate --mod mods/delivery-temperature-limit-supercooled
```

Build through the repository-local pipeline before every meaningful commit:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- build --mod mods/delivery-temperature-limit-supercooled
```

Expected build success includes one printed exact build-result JSON path. Do not install a Gate A–C build.

Run the authoritative pipeline-declared test project before every meaningful commit:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Each task's final green step means its focused tests **and all three pipeline commands above** pass against the same unmodified working tree. Do not prepare a commit after only direct `dotnet test` or a direct source build.

Record the duration printed by the already-required pipeline `test` invocation as non-statistical developer evidence. An order-of-magnitude increase from the Task 0 baseline requires investigation before commit, but no wall-clock assertion, repeated timing run, machine-independent speed claim, or publishing blocker is created from that single duration.

## Commit Protocol for Every Meaningful Chunk

For every task commit:

1. Run `git status --short` and confirm unrelated paths remain unstaged.
2. Run targeted `git diff --check -- <exact task paths>`.
3. Run pipeline `validate`, then `build`, then `test` as three separate commands; retain their fresh success evidence for the unchanged working tree.
4. Load and follow `C:\Users\maksy\.agents\skills\committing-to-git\SKILL.md` in full.
5. Invoke its `workflow prepare` in `actual`/`paths` mode with every exact whole path listed by the task, `--evidence reuse`, `--basis authored-current-task`, and only the task's stated allowed Conventional Commit type.
6. Show the helper's `displayText` verbatim and obtain explicit authorization for that exact snapshot and exact message.
7. Without reading or changing any artifact between prepare and commit, invoke `workflow commit` with the opaque transaction, the exact approved transport-safe subject, and `--verification required`.
8. Parse and report the helper's JSON result. If the commit exists but a later gate fails or the outcome is unknown, use the skill's recovery procedure; never repeat the commit blindly.
9. Do not push.

Every subject below begins with an uppercase description as required by the commit workflow.

Commit types describe the delivered change, not an aspirational benefit: Task 1 is `build` because it changes the runtime target/harness; inactive Gate A–C production modules and adapters are `refactor` because they do not yet alter the player runtime; fixture/architecture/artifact contract chunks are `test`; coordinated activation is `perf` because it is the point where the performance rewrite changes runtime behavior; and any later plan/spec-only amendment is `docs`. Do not label an inactive module `perf` or a contract-only chunk `feat`.

---

### Task 0: Preflight, Approved Configuration Context, and Evidence Baselines

**Files:**
- Read: `docs/specs/2026-08-29-delivery-temperature-limit-performance-rewrite-design.md`
- Read: `docs/plans/2026-08-29-delivery-temperature-limit-performance-rewrite.md`
- Inspect: `mods/delivery-temperature-limit-supercooled/Source/*.cs`
- Inspect: `mods/delivery-temperature-limit-supercooled/Tests/*`
- Inspect: `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`
- Inspect: `mods/delivery-temperature-limit-supercooled/DeliveryTemperatureLimit.dll`
- Inspect: installed ONI `Assembly-CSharp.dll`
- Do not modify any file in this task.

**Interfaces:**
- Consumes: approved architecture and current working tree.
- Produces: a verified clean implementation scope, confirmation that the already-approved configuration context still matches, and immutable baseline/runtime facts.

- [ ] **Step 1: Re-read repository instructions and both approved documents completely**

Record any contradiction before proceeding. The specification wins over this plan only when it is more specific; a real contradiction requires user resolution.

- [ ] **Step 2: Inspect the working tree without changing it**

Run:

```text
git status --short
```

Expected: identify every pre-existing modification/untracked path, including but not limited to the two paths recorded in Global Constraints. Treat all as user-owned.

- [ ] **Step 3: Verify the approved configuration context without editing it**

Read the four approved mutable files and byte-hash the invariant pipeline profile. Confirm:

- source target is still `net48`, without `Nullable`, `TreatWarningsAsErrors`, or `CopyLocalLockFileAssemblies`;
- tests still target `net10.0`, `Nullable=annotations`, warnings as errors, with only the legacy `Buildings.cs` link;
- `minimumSupportedBuild` is still `596100`; and
- no work since plan approval changed the target sections.

Expected: exact match. If any item differs, stop and show the user the old approved value, current value, and smallest revised delta. Do not assume the earlier approval covers a different file state.

- [ ] **Step 4: Confirm the current focused tests before changing the harness**

Run:

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore -- --filter "FullyQualifiedName~BuildingsEligibilityTests"
```

Expected: PASS. If restore assets are absent, run the locked restore command once, then rerun. Do not launch ONI.

- [ ] **Step 5: Confirm the local pipeline entrypoint without running the deep campaign**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- diagnose --mod mods/delivery-temperature-limit-supercooled
```

Expected: successful environment diagnosis naming the installed ONI managed directory. If tool restore assets are absent, perform a locked restore of the tool project as a separate command. Do not change configuration to work around discovery.

- [ ] **Step 6: Verify the published baseline and current ONI evidence**

Run separate read-only commands to prove and record:

- Git object `5f7bf43aa823bbb4771936b058c6d573484b6d91` exists;
- tracked baseline `DeliveryTemperatureLimit.dll` has file version `2026.8.26.0` and SHA-256 `02A14F2E123F42BDD87847C15AB434DAFC8A4D4BC92B465F9DCD367364BF465E`;
- installed `Assembly-CSharp.dll` has SHA-256 `A58E04D0FFDF89B86FB28B71AD900625B3B539DB30D67F8C6269F73A9F5AE599`;
- `KleiVersion.ChangeList == 744825`, `KleiVersion.BuildBranch == "release"`, and `Sim.MaxTemperature == 10000f`; and
- installed element YAML still records `MoltenCarbon.highTemp=5100`, `MoltenNiobium.highTemp=5017`, and `MoltenTungsten.highTemp=6203`.

Expected: exact match. A mismatch is a planning input change, not a reason to edit expectations silently.

- [ ] **Step 7: Run the unchanged baseline through the pipeline gates**

Run pipeline `validate`, `build`, and `test` as separate commands. Record any pre-existing MSB3277 reference warnings exactly. Expected: all commands succeed; the warning inventory becomes diagnostic evidence for Task 1 rather than a suppression target.

- [ ] **Step 8: Record the big-bang boundary in the implementation log**

Use the task conversation/status update, not a repository file, to state:

```text
Gate A–C builds are compile/test artifacts only and must not be installed.
The first installable build is the Gate D coordinated activation.
```

There is no commit for Task 0.

---

### Task 1: Current ONI Runtime Target and Static Contract Harness

This is the first meaningful implementation commit boundary. Its approved commit subject/body intent is fixed below; if the actual prepared snapshot needs a materially different message, show the difference and obtain exact approval through the commit workflow.

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/packages.lock.json`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Modify: `mods/delivery-temperature-limit-supercooled/mod_info.yaml`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/OniStorableTemperatureBounds.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/TemperatureConstraints/OniStorableTemperatureBoundsTests.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Tests/PublicAssemblySurface.cs` to `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/DeliveryTemperatureAssemblyMetadataReader.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/DeliveryTemperatureAssemblyMetadataReader.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Tests/ModBuildContractTests.cs` to `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/MergedDeliveryTemperatureAssemblyContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/MergedDeliveryTemperatureAssemblyContractTests.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Tests/TemporaryDirectory.cs` to `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/PipelineTestTemporaryDirectory.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/PipelineTestTemporaryDirectory.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Tests/DotnetProcess.cs` to `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/DotnetCommandRunner.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/DotnetCommandRunner.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/OniStorableTemperatureBoundsContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/CurrentOniRuntimeContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/ProjectTargetFrameworkContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/KnownOniReferenceConflictContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/LinkedProductionSourceBoundaryContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/DeliveryTemperaturePackageBoundaryContractTests.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Tests/GameStubs.cs` to `mods/delivery-temperature-limit-supercooled/Tests/TestDoubles/OniGameTypeStubs.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/TestDoubles/OniGameTypeStubs.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/OniModPipelineProfileInvarianceTests.cs`

**Interfaces:**
- Consumes: installed ONI directory from `ONI_MANAGED_ASSEMBLY_DIRECTORY`, repository root from `ONI_MOD_PIPELINE_REPOSITORY_ROOT`, the published baseline DLL, the approved staged configuration dossier, and the current pipeline profile.
- Produces: `OniStorableTemperatureBounds.MaximumTemperatureKelvin == 10000`, `netstandard2.1` production targeting with an exact C# 8 production/linked-source language ceiling, C# 14 test-only source, current-build metadata contracts, exact known-warning contracts, merge/package boundary contracts, semantically named test infrastructure, exact test-stub parity contracts, and a linked-source test boundary without changing `oni-mod-pipeline.toml`.

- [ ] **Step 1: Write red project/runtime contract tests before changing configuration**

Use `System.Reflection.Metadata` and `PEReader` from `net10.0`; do not add a package. Tests must fail for the intended old facts:

- source project still targets `net48`;
- source project lacks `CopyLocalLockFileAssemblies` and `TreatWarningsAsErrors`;
- `mod_info.yaml` still names build `596100`;
- `OniStorableTemperatureBounds` does not exist; and
- the test project does not link the exact approved algorithm/session and reflection-only source set; and
- the existing global `TemperatureLimit` test stub has not been moved to the exact `DeliveryTemperatureLimit.TemperatureLimit` identity or constrained by member parity.

The same class must assert the evaluated compiler properties: the production project has no authored `LangVersion` override and resolves to C# 8 from `netstandard2.1`, while the `net10.0` test project resolves to test-only C# 14. Do not add `LangVersion` merely to make the test explicit. The actual production compiler invocation in the mandatory pipeline `build` is the authoritative syntax guard; do not substitute a hand-maintained list of newer C# tokens. `LinkedProductionSourceBoundaryContractTests` must prove that every production file linked into tests is also an exact production `Compile` input, so no linked-only copy can evade that C# 8 compilation.

The runtime reader resolves `Assembly-CSharp.dll` only beneath the full path supplied by `ONI_MANAGED_ASSEMBLY_DIRECTORY`, rejects a missing/non-rooted/out-of-directory path, and reports observed assembly digest/build/branch/value on failure. It verifies `KleiVersion.ChangeList`, `KleiVersion.BuildBranch`, `Sim.MaxTemperature`, the inclusive `10000f` validation shape in `PrimaryElement.OnDeserialized`, and the matching `SimMessages.ModifyCell` bound. Do not load the game assembly for execution. Rename the existing generic test infrastructure as listed; update namespaces and callers atomically rather than leaving aliases for the old names.

- [ ] **Step 2: Run the new static contract class red**

Run the class through direct filtered `dotnet test`, supplying the same two environment variables the pipeline supplies. Expected: failures name the old target/build and missing bound source, not an environment discovery error.

- [ ] **Step 3: Apply only the approved first-stage project and metadata changes**

Make these exact edits:

```xml
<TargetFramework>netstandard2.1</TargetFramework>
<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Do not add `<Nullable>` to the production project yet. In the test project, retain `net10.0`, `Nullable=annotations`, and every existing property. Retain the legacy `Buildings.cs` link, then add only these future-safe linked items under matching `Production\<semantic-root>\...` paths:

- `..\Source\TemperatureConstraints\**\*.cs`;
- `..\Source\WorldParentTopology\**\*.cs`;
- `..\Source\WorldResourceTemperatureAmounts\**\*.cs`;
- `..\Source\FetchTemperatureEligibility\**\*.cs`;
- `..\Source\DeliveryTemperatureGameSessionLifecycle\**\*.cs`;
- `..\Source\FastTrackCompatibility\FeatureContractVerification\**\*.cs`;
- `..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationKind.cs`;
- `..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationResult.cs`;
- `..\Source\FastTrackCompatibility\InventoryUpdateAdapters\FastTrackWorldInventoryPublicationSession.cs`;
- `..\Source\FastTrackCompatibility\PickupGroupingAdapters\FastTrackPickupGroupingKeyAllocator.cs`; and
- `..\Source\HarmonyTranspilerInfrastructure\HarmonyPatchContract*.cs`;
- `..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchGroup.cs`; and
- `..\Source\RuntimePatchInstallation\DeliveryTemperatureRuntimePatchPlan.cs`.

The `TemperatureConstraints` glob immediately matches the bound file created in this task. Every other semantic-root glob or exact linked item deliberately matches no file until its later task creates that source; none may include a runtime Harmony adapter file. Set only `minimumSupportedBuild: 744825` in `mod_info.yaml`.

`LinkedProductionSourceBoundaryContractTests` parses both project item graphs and the source boundary on every later pipeline run. It permits only the exact items above and the legacy `Buildings.cs` link until Task 24. It rejects a copied production algorithm, a test-linked production file absent from the production `Compile` graph, a broad `FastTrackCompatibility\**` or adapter glob, Unity/Klei/Harmony/PLib/FastTrack API invocation from linked algorithm/session code, conditional framework fork, or test-only branch. The evaluated-property test plus real production compilation—not a brittle syntax substring scan—reject post-C#-8 source. Exact plan-named value/identity types supplied by `OniGameTypeStubs` are allowed; game-object traversal is not. Move the test-double file atomically in this task, preserve every pre-existing required non-domain stub, relocate the empty component to the exact `DeliveryTemperatureLimit.TemperatureLimit` identity, and add an initially empty `readonly struct Tag` in the global namespace. Task 8 extends only the verified value behavior needed by newly linked sources. The stubs expose only member shapes enumerated by the dedicated parity contract; convenient test-only aliases, alternate namespaces, and additional members are forbidden.

- [ ] **Step 4: Add the pure bound source with an evidence comment**

Start the file with `#nullable enable`. Define only the `internal` static class and its two named minimum/maximum constants from the Contract Registry. The maximum's comment must name changelist `744825`, `Sim.MaxTemperature`, inclusive validity, and the static test that forces review after an ONI change; the minimum comment identifies the mod's preserved configurable floor. Do not reference a Klei type from this linked pure file and do not add runtime reflection.

- [ ] **Step 5: Refresh and inspect the locked restore graph**

Run a non-locked restore once to update `Source/packages.lock.json`, then inspect the diff before any locked command. Expected: only the target-framework graph changes; PLib remains `4.24.0`, ILRepack remains `2.0.34`, and no package version or unrelated framework graph changes. Any broader delta stops this task.

- [ ] **Step 6: Characterize the two visible ONI reference conflicts**

Capture `ResolveReferences` through the production project using the pipeline-provided ONI directory and isolated output/intermediate directories. `KnownOniReferenceConflictContractTests` must describe exactly:

- `System.IO.Compression`: framework/reference root `4.1.3.0`, ONI root `4.2.0.0`; and
- `System.Net.Http`: framework/reference root `4.1.2.0`, ONI root `4.2.0.0`.

The test/project scan fails if a third root appears, either version changes, the warnings disappear through suppression/redirect/pinning, or `NoWarn`, binding redirects, `AutoUnify`, or direct framework-reference workarounds appear. Compiler warnings remain errors; these two MSBuild resolution warnings remain visible.

- [ ] **Step 7: Add merge, package, baseline, and pipeline invariance contracts**

Tests must prove:

- tracked baseline DLL version/hash/source-commit facts;
- the baseline DLL directly references neither `System.IO.Compression` nor `System.Net.Http`;
- pipeline profile declares PLib as the only merge input;
- pipeline package files are exactly `mod.yaml`, `mod_info.yaml`, and the merged `DeliveryTemperatureLimit.dll`;
- no framework DLL or `.config` file can enter the package declaration; and
- `oni-mod-pipeline.toml` equals the Task 0 recorded bytes/hash.

`MergedDeliveryTemperatureAssemblyContractTests` starts with one always-present published-baseline data row. Task 26 extends that provider with an exact pipeline-build row when an explicit build-result path is supplied and an exact release-candidate row when an explicit provenance-bound candidate directory is supplied. Ordinary pipeline `test` has no skipped or inconclusive case and cannot silently claim external artifact evidence; the final gates supply each exact path and verify the corresponding named row executed.

- [ ] **Step 8: Run red-to-green focused tests and locked builds**

Run the new contract classes, locked test restore, and a production `netstandard2.1` build through the pipeline. Expected: contracts pass; the production and every linked production source file compile as C# 8; test-only files compile under the `net10.0` SDK default; the exact `global::Tag` and `DeliveryTemperatureLimit.TemperatureLimit` stub shapes pass parity inspection; exactly the two known MSB3277 roots remain visible; the merged DLL directly references neither disputed assembly; no framework DLL/config is packaged.

- [ ] **Step 9: Run the mandatory commit gates**

Run pipeline `validate`, `build`, and `test` separately on the unchanged working tree. Confirm `oni-mod-pipeline.toml` is byte-for-byte unchanged and both project nullable stages remain exactly as approved.

- [ ] **Step 10: Commit the first meaningful implementation chunk**

Allowed type: `build`. Prepare only the exact files listed in this task. Use this detailed message intent:

```text
build: Target Delivery Temperature Limit for current ONI

Compile the game-loaded assembly for .NET Standard 2.1 and refresh
the locked restore graph without changing package versions.

Record ONI build 744825 as the supported public runtime and add
static contracts for the target framework, assembly references,
known ONI reference-analysis conflicts, merge inputs, and package
boundary. Establish the C# 8 linked-production ceiling and exact
global Tag and DeliveryTemperatureLimit.TemperatureLimit test-double
identities.
```

Use the Commit Protocol; the user's prior approval of this intent does not bypass the required exact prepared-snapshot/message authorization. Do not push.

---

### Task 2: Canonical Delivery Constraint and Temperature Decision Buckets

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/DeliveryTemperatureConstraint.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/TemperatureDecisionBucket.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/TemperatureConstraints/DeliveryTemperatureConstraintTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/TemperatureConstraints/TemperatureDecisionBucketTests.cs`

**Interfaces:**
- Consumes: linked `OniStorableTemperatureBounds` from Task 1 and exact existing serialized/truncation semantics.
- Produces: `DeliveryTemperatureConstraint` and the formula-derived `TemperatureDecisionBucket` exactly as declared in the Contract Registry.

- [ ] **Step 1: Confirm the linked pure-source boundary is active**

Run `OniStorableTemperatureBoundsTests` through the pipeline-declared test project and confirm the production source is linked from `Source/TemperatureConstraints`, not copied. Do not modify either project in this task.

- [ ] **Step 2: Write failing constraint characterization tests**

Create tests covering disabled, normalized, enabled-empty, boundaries, and truncation. Include this core:

```csharp
[DataRow(9.999f, false)]  // truncates to 9
[DataRow(10.0f, true)]
[DataRow(19.999f, true)]  // truncates to 19
[DataRow(20.0f, false)]
[TestMethod]
public void Allows_WhenTemperatureIsComparedWithInclusiveExclusiveBounds_ReturnsExpectedDecision(
    float temperatureKelvin,
    bool expectedDecision)
{
    var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
        serializedLowLimit: 10,
        serializedHighLimit: 20);

    Assert.AreEqual(expectedDecision, constraint.Allows(temperatureKelvin));
}

[TestMethod]
public void FromSerializedLimits_WhenEnabledMinimumIsNotBelowMaximum_PreservesEmptyConstraint()
{
    var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(
        serializedLowLimit: 100,
        serializedHighLimit: 100);

    Assert.IsTrue(constraint.IsEnabled);
    Assert.IsTrue(constraint.IsEmpty);
    Assert.IsFalse(constraint.Allows(100.0f));
}
```

Also add exact tests named:

- `FromSerializedLimits_WhenHighIsZero_ReturnsDisabledConstraint`
- `FromSerializedLimits_WhenHighClampsToZero_ReportsNotEmpty`
- `FromSerializedLimits_WhenValuesExceedBounds_ClampsBothValues`
- `FromSerializedLimits_WhenValuesAreNegative_ClampsBothValuesToZero`
- `Allows_WhenDisabledTemperatureIsBelowMinimum_ReturnsTrue`
- `Allows_WhenDisabledTemperatureIsAtOrAboveMaximum_ReturnsTrue`
- `Allows_WhenTemperatureHasNegativeFraction_TruncatesTowardZero`
- `Allows_WhenMaximumIsOniStorableTemperatureMaximum_RejectsExactMaximumAndAbove`
- `Allows_WhenEnabledTemperatureIsExactlyOniMaximum_ReturnsFalse`
- `Allows_WhenDisabledTemperatureIsExactlyOniMaximum_ReturnsTrue`

- [ ] **Step 3: Write failing decision-bucket tests**

Include:

```csharp
[DataRow(-1.0f, TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)]
[DataRow(-0.999f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)]
[DataRow(0.0f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal)]
[DataRow(273.15f, TemperatureDecisionBucket.FirstIntegerKelvinOrdinal + 273)]
[DataRow(9999.999f, TemperatureDecisionBucket.HighestIntegerKelvinOrdinal)]
[DataRow(10000.0f, TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)]
[TestMethod]
public void FromTemperature_WhenGivenBoundaryValue_UsesCSharpTruncation(
    float temperatureKelvin,
    int expectedOrdinal)
{
    Assert.AreEqual(
        expectedOrdinal,
        TemperatureDecisionBucket.FromTemperature(temperatureKelvin).Ordinal);
}
```

Add exhaustive round-trip coverage for integer Kelvin `0..OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1` (currently `0..9999`) and tests that every representative truncated integer below zero shares `BelowMinimumKelvinOrdinal` while every representative integer at/above `10000` shares `AtOrAboveMaximumKelvinOrdinal`. Assert `BucketCount` from its formula and assert the current reviewed result is `10002` once; do not use a repeated literal as a loop bound.

- [ ] **Step 4: Run both test classes and observe the intended red**

Run the focused command once with `DeliveryTemperatureConstraintTests`, then once with `TemperatureDecisionBucketTests`.

Expected: compilation fails because the two new production types do not exist. Any project XML, lockfile, installed-ONI, or unrelated compilation failure must be resolved before implementation.

- [ ] **Step 5: Implement the minimal immutable constraint**

Use explicit normalization and exactly one conversion point:

```csharp
internal bool Allows(float temperatureKelvin)
{
    if (!IsEnabled)
    {
        return true;
    }

    int truncatedKelvin = (int)temperatureKelvin;
    return MinimumInclusiveKelvin <= truncatedKelvin &&
        truncatedKelvin < MaximumExclusiveKelvin;
}
```

Add comments explaining why disabled and empty are distinct and why the cast must precede comparison. Do not call `Math.Floor`, `Math.Round`, Celsius conversion, or a caller-supplied classifier.

- [ ] **Step 6: Implement the formula-derived canonical bucket mapping**

Use ordinal mapping:

```csharp
if (truncatedKelvin < OniStorableTemperatureBounds.MinimumTemperatureKelvin)
{
    return new TemperatureDecisionBucket(BelowMinimumKelvinOrdinal);
}

if (truncatedKelvin >= OniStorableTemperatureBounds.MaximumTemperatureKelvin)
{
    return new TemperatureDecisionBucket(AtOrAboveMaximumKelvinOrdinal);
}

return new TemperatureDecisionBucket(
    FirstIntegerKelvinOrdinal + truncatedKelvin);
```

Comment why negative Celsius is not negative Kelvin, why `-0.999 K` truncates into the zero-Kelvin bucket, and why exact `10000 K` shares a rejection-equivalent bucket with higher values despite being valid ONI state.

- [ ] **Step 7: Run the focused tests green and refactor**

Expected: both classes PASS. Confirm equality/hash/compare implementations are value-based and allocation-free. Run `git diff --check` on the four task paths.

- [ ] **Step 8: Perform the chunk shim and naming scan**

Run:

```text
rg -n "TemperatureIndexData|getTemperatureIndexData|Helper|Utils|DeliveryTemperatureBounds|UnderflowOrdinal|OverflowOrdinal" mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints mods/delivery-temperature-limit-supercooled/Tests/TemperatureConstraints
```

Expected: no new legacy reference, generic utility name, or incomplete marker. Existing legacy references outside the new directories are expected until Gate D.

- [ ] **Step 9: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm the project/lock/metadata files from Task 1 remain unchanged and the two known reference warnings remain exactly bounded.

- [ ] **Step 10: Prepare and commit the exact chunk**

Use the Commit Protocol with the four task paths, allowed type `refactor`, and exact subject:

```text
refactor: Define canonical delivery temperature semantics
```

---

### Task 3: Immutable Constraint Registry and Endpoint Reference Counts

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/TemperatureConstraintGeneration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/TemperatureConstraintRegistrationToken.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/ActiveTemperatureConstraintSnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/TemperatureConstraintRegistry.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/TemperatureConstraints/TemperatureConstraintRegistryTests.cs`

**Interfaces:**
- Consumes: current ONI bounds from Task 1 and canonical constraints/buckets from Task 2.
- Produces: token-owned O(1) registry mutation and eagerly published immutable endpoint snapshots.

- [ ] **Step 1: Write failing registry lifecycle tests**

Include this ownership case:

```csharp
[TestMethod]
public void TryRemove_WhenRegistrationTokenIsStale_DoesNotRemoveReplacement()
{
    var registry = new TemperatureConstraintRegistry();
    var first = registry.Register(41, Constraint(10, 20), out _);
    var replacement = registry.Register(41, Constraint(30, 40), out _);

    Assert.IsFalse(registry.TryRemove(first, out var changed));
    Assert.IsFalse(changed);
    Assert.AreEqual(1, registry.CaptureSnapshot().EnabledConstraintCount);
    Assert.IsTrue(registry.TryRemove(replacement, out changed));
    Assert.IsTrue(changed);
}
```

Add exact tests:

- `Register_WhenIdentityIsNew_AddsConstraintAndIncrementsGenerationOnce`
- `Register_WhenConstraintIsIdentical_ReturnsExistingRegistrationWithoutGenerationChange`
- `Register_WhenConstraintDiffers_ReplacesEntryAndUpdatesEndpointCounts`
- `Register_WhenConstraintIsDisabled_DoesNotAddEndpoints`
- `Register_WhenConstraintIsEnabledButEmpty_CountsActiveWithoutAddingEndpoints`
- `TryReplace_WhenConstraintIsIdentical_IsNoOp`
- `TryReplace_WhenRegistrationIsUnknown_ReturnsFalse`
- `TryRemove_WhenRegistrationIsUnknown_IsIdempotent`
- `CaptureSnapshot_WhenEndpointsHaveDuplicates_ContainsEachEndpointOnceSorted`
- `CaptureSnapshot_AfterLastReferenceRemoved_RemovesEndpoint`
- `CaptureSnapshot_WhenEndpointMembershipIsUnchanged_ReusesSortedEndpointReference`
- `CaptureSnapshot_WhenCallerMutatesReturnedView_CannotMutateRegistryState` (the API should expose read-only arrays/views that cannot be cast back to mutable owned arrays).

- [ ] **Step 2: Add deterministic randomized reference-model test**

Using seed `0x51A7E`, execute 50,000 register/replace/remove operations across component IDs `0..2047`. Maintain a test-only dictionary and rebuild expected enabled/nonempty counts and endpoints by direct loops after each sampled operation. Every 97 operations assert the two counts, generation increments, and sorted endpoints. Do not expose or copy registered constraints merely to support this test: tokens test ownership, while snapshot consumers need only counts, generation, and canonical endpoints.

- [ ] **Step 3: Run the class and observe red**

Expected: compilation fails for missing registry types.

- [ ] **Step 4: Implement token ownership and O(1) mutation**

Use:

```csharp
private readonly Dictionary<int, RegistryEntry> entriesByComponentInstanceId;
private readonly int[] endpointReferenceCounts =
    new int[OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1];
private readonly ulong[] activeEndpointMembershipWords =
    new ulong[(OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1 + 63) / 64];
private long nextRegistrationSequence;
private long generation;
private ActiveTemperatureConstraintSnapshot publishedSnapshot;
```

Registration sequence zero is invalid. Increment with checked semantics; exhaustion throws a named `InvalidOperationException` and never reuses a token. Endpoint increments/decrements occur only for enabled, nonempty constraints. A `0 -> 1` transition sets the endpoint membership bit; a `1 -> 0` transition clears it. Guard reference-count underflow and an inconsistent bit with invariant exceptions.

Add focused overflow tests that set the exact private fields `nextRegistrationSequence` and `generation` to `long.MaxValue` through the predeclared representation-contract reflection path, then prove the next changing operation throws without publishing a partial mutation or wrapping to a reusable identity. No injectable counter limit, alternate constructor, or test-only branch is permitted.

- [ ] **Step 5: Eagerly publish immutable snapshots on changed mutations**

Track whether any endpoint membership bit changed, separately from whether an endpoint reference count changed. Reconstruct sorted endpoints only after a `0 -> 1` or `1 -> 0` membership transition by scanning the fixed membership words in ascending order and emitting only set endpoint bits; for the current bound this inspects `ceil(10001 / 64) = 157` words rather than all `10,001` counts. If generation/counts change while membership stays identical—for example, another component references already-active endpoints—reuse the previous immutable sorted-endpoint view by reference.

Do not use `SortedSet`, LINQ, sort all components, or copy per-component registrations into the snapshot. `ActiveTemperatureConstraintSnapshot` contains only generation, enabled count, enabled-nonempty count, and the sorted unique endpoints required by consumers. Publish the new reference only after all fields are complete and while the registry owns its private lock.

Return the already-published reference from `CaptureSnapshot`; it must not acquire another service or cause deferred work.

- [ ] **Step 6: Run focused tests and randomized test green**

Expected: all registry tests PASS. Confirm identical operations preserve object reference equality of the published snapshot as evidence that they are true no-ops.

- [ ] **Step 7: Inspect bounded complexity and comments**

Verify a membership-changing mutation performs only the 157-word membership scan plus emission of actually active endpoints, while a count/generation-only mutation reuses the prior endpoint view and performs neither. There is no O(number-of-registered-components) snapshot copy, full temperature-range scan, LINQ `Sort`, `Distinct`, `allLimits` scan, lazy flag, or worker callback. Assert endpoint-count element storage is `40,004` bytes (`10,001 × 4`), approximately `39.1 KiB` and `19.5 KiB` more than the former range. Comments must identify token ownership, reference-count/bit invariants, endpoint-view reuse, and why unused `5000..9999 K` values add fixed memory but no recurring work.

- [ ] **Step 8: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm the bounded-reference tests and candidate build remain green.

- [ ] **Step 9: Prepare and commit**

Use the five task paths, allowed type `refactor`, and exact subject:

```text
refactor: Publish immutable active temperature constraints
```

---

### Task 4: Owned TemperatureLimit Component Index

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/TemperatureLimitComponentIndex.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/TemperatureConstraints/TemperatureLimitComponentIndexTests.cs`

**Interfaces:**
- Consumes: `DeliveryTemperatureConstraint`, `TemperatureConstraintRegistrationToken`, and the exact `DeliveryTemperatureLimit.TemperatureLimit` test-double identity established in Task 1.
- Produces: O(1) thread-safe GameObject-instance lookup with remove-if-owned semantics; Task 5's `GameSessionTemperatureLimitRegistrationToken` composes index and registry ownership after game-session generation exists.

- [ ] **Step 1: Confirm the exact component stub established in Task 1**

The test component stub must still have the exact production identity and no convenience members:

```csharp
namespace DeliveryTemperatureLimit
{
    public sealed class TemperatureLimit
    {
    }
}
```

Keep `Tag` in the global namespace and this component in `DeliveryTemperatureLimit`; do not add aliases. Update the member-parity contract before compiling linked production source. Do not add Unity lifecycle behavior to the stub; the index must not depend on it.

- [ ] **Step 2: Write failing ownership and lookup tests**

Include:

```csharp
[TestMethod]
public void TryRemove_WhenGameObjectIdWasReused_DoesNotRemoveNewOwner()
{
    var index = new TemperatureLimitComponentIndex();
    var oldRegistration = Registration(componentId: 1, sequence: 10);
    var newRegistration = Registration(componentId: 2, sequence: 11);
    var oldComponent = new TemperatureLimit();
    var newComponent = new TemperatureLimit();

    Assert.IsTrue(index.TryRegister(77, oldComponent, oldRegistration, Constraint(10, 20)));
    Assert.IsTrue(index.TryRegister(77, newComponent, newRegistration, Constraint(30, 40)));

    Assert.IsFalse(index.TryRemove(77, oldRegistration));
    Assert.IsTrue(index.TryGetRegisteredComponent(
        77,
        out var component,
        out var survivingRegistration));
    Assert.AreSame(newComponent, component);
    Assert.AreEqual(newRegistration, survivingRegistration);
}
```

Add exact tests:

- `TryRegister_WhenEntryIsNew_PublishesComponentAndConstraint`
- `TryRegister_WhenSameOwnerAndStateRepeats_IsIdempotent`
- `TryRegister_WhenDifferentOwnerUsesSameGameObjectId_ReplacesAtomically`
- `TryReplaceConstraint_WhenTokenMatches_ChangesOnlyConstraint`
- `TryReplaceConstraint_WhenTokenIsStale_LeavesEntryUnchanged`
- `TryGetConstraint_WhenGameObjectIsUnknown_ReturnsFalse`
- `TryRemove_WhenCalledTwice_IsIdempotent`
- `ConcurrentReaders_WhenEntryIsReplaced_ObserveOnlyWholeOldOrWholeNewEntry`

- [ ] **Step 3: Run red**

Expected: missing index and registration types.

- [ ] **Step 4: Implement the index with whole immutable entries**

Use `ConcurrentDictionary<int, TemperatureLimitComponentIndexEntry>` because reads can occur from FastTrack-related paths. Define `TemperatureLimitComponentIndexEntry` as a private sealed immutable nested type containing the component reference, registration token, and immutable constraint; do not shorten it to a generic `Entry`. `TryGetRegisteredComponent` copies the component and its token from one captured entry, never from two dictionary reads. Use `TryUpdate` for owned replacement. For owned removal under the .NET Standard 2.1 API surface, capture the exact immutable entry and invoke `ICollection<KeyValuePair<int, TemperatureLimitComponentIndexEntry>>.Remove(expectedPair)` on the dictionary; that conditional key-and-value operation must be the sole removal primitive. Do not perform an owner check followed by `TryRemove(gameObjectInstanceId, out _)`, because a replacement can win between those calls and be removed by stale cleanup. Add adjacent comments explaining both the atomic paired read and this framework-compatible atomic-removal choice.

Do not expose the dictionary, return mutable entries, or call Unity from the index.

- [ ] **Step 5: Run tests green and inspect allocations**

Expected: all index tests PASS. Ordinary successful reads must perform no allocation. Replacement may allocate one immutable entry; it occurs only on configuration change.

- [ ] **Step 6: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm the Task 1 stub identities/member-parity contract remain unchanged and existing tests still resolve their required non-domain stubs.

- [ ] **Step 7: Prepare and commit**

Use the two task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add owned temperature component lookup
```

---

### Task 5: Game-Session Ownership, Registration Coordination, and Diagnostic Limiting

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/GameSessionGeneration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/GameSessionTemperatureLimitRegistrationToken.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/RetainedCollectionCapacityLimits.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/SessionDiagnosticLimiter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionHost.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/SessionDiagnosticLimiterTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionTests.cs`

**Interfaces:**
- Consumes: constraint registry and component index.
- Produces: one atomically published game session, composite component registration, stale-session rejection, idempotent two-phase shutdown, and per-session diagnostic rate limiting. Later tasks extend the same session with their completed world/fetch services; do not add null or `object` placeholders for types that do not yet exist.

- [ ] **Step 1: Write failing host lifecycle tests**

Include:

```csharp
[TestMethod]
public void EnsureGameSession_WhenGameIdentityChanges_DetachesAndInvalidatesOldSession()
{
    var oldSession = DeliveryTemperatureGameSessionHost.EnsureGameSession(100);

    var newSession = DeliveryTemperatureGameSessionHost.EnsureGameSession(200);

    Assert.AreNotSame(oldSession, newSession);
    Assert.IsFalse(oldSession.IsAcceptingPublications);
    Assert.IsTrue(newSession.IsAcceptingPublications);
    Assert.AreNotEqual(oldSession.Generation, newSession.Generation);
}
```

Each test uses unique game identities and performs cleanup through the real detach/complete lifecycle in `TestCleanup`. Do not add `ResetForTests`, `InternalsVisibleTo`-only behavior branches, or any test-only method to production.

Add exact tests:

- `EnsureGameSession_WhenIdentityMatches_ReturnsSameSession`
- `TryCaptureCurrent_WhenNoSession_ReturnsFalse`
- `DetachGameSession_WhenIdentityMatches_StopsAndReturnsSession`
- `DetachGameSession_WhenIdentityDiffers_DoesNotDetachCurrentSession`
- `CompleteShutdown_WhenCalledTwice_IsIdempotent`
- `OldSession_WhenNewSessionExists_RejectsTemperatureLimitRegistration`
- `RegisterTemperatureLimit_WhenSessionIsStopping_ThrowsLifecycleViolation`
- `RemoveTemperatureLimit_WhenRegistrationBelongsToOldSession_DoesNotTouchCurrentSession`
- `TryReplaceTemperatureConstraint_WhenNormalizedConstraintIsIdentical_DoesNotAdvanceGeneration`

- [ ] **Step 2: Write failing diagnostic limiter tests**

Cover first occurrence, repeated same key, different key, and new-session reset:

```csharp
Assert.IsTrue(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
Assert.IsFalse(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
Assert.IsTrue(limiter.ShouldEmit("DTL_WORLD_UNRESOLVED"));
```

- [ ] **Step 3: Run both classes red**

Expected: missing runtime types. Implement only the completed constraint/component dependencies in this task. Do not reference not-yet-created world, inventory, or fetch types merely to mimic the final contract shape.

- [ ] **Step 4: Implement atomic host publication**

Allocate nonzero game-session generations with a lock-free `Volatile.Read` plus `Interlocked.CompareExchange` loop. Compute the candidate inside a checked context, throw a named `InvalidOperationException` at `long.MaxValue`, and never wrap or publish generation zero. Use `Volatile.Read` to capture the current session and `Interlocked.CompareExchange`/`Exchange` for publication and detachment. Store only the integer `Game` instance identity, never a Unity object, in the pure host.

Add a focused `[DoNotParallelize]` overflow test that first proves no current session is published, sets the exact private static field `lastIssuedGameSessionGeneration` to `long.MaxValue`, calls `EnsureGameSession`, and proves that no session is published. Restore only the value that this test itself changed in a `finally` block while the test owns exclusive execution for this static host. Do not introduce an injectable sequence source or a production reset API.

`EnsureGameSession` must stop a different old session before publishing the replacement. A worker holding the old object can then mutate only an inactive, detached session whose publication methods reject it.

- [ ] **Step 5: Implement composite component registration transaction**

Sequence mutation so a partially failed registration is rolled back in the same session. Never hold both service locks. Add a comment explaining the short observable ordering window and why fetch snapshot generation validation prevents mixed publication.

`GameSessionTemperatureLimitRegistrationToken` includes session generation, GameObject ID, and registry token. Every replace/remove validates all three before mutation.

- [ ] **Step 6: Add named retained collection limits**

Use these initial limits and comments explaining that Task 25 verifies their retention behavior structurally and Task 28 permits an indicative manual observation:

```csharp
internal const int MaximumRetainedPickupClassificationCount = 16384;
internal const int MaximumRetainedFastTrackGroupingKeyCount = 8192;
internal const int MaximumRetainedFetchEligibilityEntryCount = 4096;
internal const int MaximumRetainedWorldResourceTagCount = 4096;
```

Do not call them cache sizes. Each consumer later replaces its variable-capacity collection after exceeding its relevant limit.

These constants are retention limits, never functional workload caps: an invocation may grow beyond them and must still process every entry correctly, then discard/replace the oversized backing collection only at its documented safe completion/finalizer boundary. The powers of two are deliberately generous for a community-mod large-colony workload while preventing one pathological session from pinning its peak capacity forever. Tests must prove no item is dropped at `limit`, `limit + 1`, or a much larger count and that only the post-operation reusable instance changes. Changing a value later is a named resource-policy decision, not a silent micro-optimization.

- [ ] **Step 7: Run green, then run lifecycle schedule stress**

Use seed `0x5E5510` to run 10,000 deterministic ensure/capture/detach/complete/register/remove operations. Assert no old generation removes or publishes into the current session. This is invariant testing, not wall-clock benchmarking.

- [ ] **Step 8: Scan for static mutable gameplay collections**

Within the new semantic production directories, only the host's atomic current-session reference and monotonic generation source may be mutable static state. `SessionDiagnosticLimiter` belongs to a session instance. The architecture test must name every allowed field rather than allowlisting a directory.

- [ ] **Step 9: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm lifecycle schedule tests pass under the linked pure-source build and no test-only production API exists.

- [ ] **Step 10: Prepare and commit**

Use the eight task paths, allowed type `refactor`, and exact subject:

```text
refactor: Scope temperature state to game sessions
```

---

### Task 6: Immutable World-to-Parent Topology

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldParentTopology/WorldParentTopologyVersion.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldParentTopology/WorldParentTopologyChange.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldParentTopology/WorldParentTopologySnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldParentTopology/WorldParentTopologyCatalog.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldParentTopology/WorldParentTopologyCatalogTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionTests.cs`

**Interfaces:**
- Consumes: `GameSessionGeneration` and session lifetime from Task 5.
- Produces: immutable world-parent snapshots, exact old/new parent changes, sorted member-world lookup, and the session's `WorldParentTopology` property.

- [ ] **Step 1: Write failing topology mutation tests**

Include:

```csharp
[TestMethod]
public void RegisterWorld_WhenExistingWorldChangesParent_ReturnsBothAffectedParents()
{
    var catalog = CreateCatalog();
    catalog.RegisterWorld(worldId: 7, parentWorldId: 1);

    var change = catalog.RegisterWorld(worldId: 7, parentWorldId: 2);

    Assert.IsTrue(change.HasChanged);
    Assert.AreEqual(1, change.PreviousParentWorldId);
    Assert.AreEqual(2, change.CurrentParentWorldId);
    Assert.IsTrue(catalog.CaptureSnapshot().TryResolveParentWorld(7, out var parent));
    Assert.AreEqual(2, parent);
}
```

Add exact tests:

- `RegisterWorld_WhenMappingIsNew_IncrementsVersionOnce`
- `RegisterWorld_WhenMappingIsIdentical_DoesNotChangeVersionOrSnapshotReference`
- `RegisterWorld_WhenWorldIsItsOwnParent_PreservesSelfParentMapping`
- `RegisterWorld_WhenWorldIdIsNegative_ThrowsArgumentOutOfRangeException`
- `RegisterWorld_WhenParentWorldIdIsNegative_ThrowsArgumentOutOfRangeException`
- `RemoveWorld_WhenKnown_ReturnsPreviousParentAndRemovesMapping`
- `RemoveWorld_WhenUnknown_IsIdempotent`
- `GetMemberWorldIds_WhenParentHasSeveralWorlds_ReturnsSortedImmutableIds`
- `CaptureSnapshot_WhenMappingChanges_ReaderSeesCompleteOldOrCompleteNewMapping`
- `TryResolveParentWorld_WhenWorldIsUnknown_ReturnsFalseWithoutFallback`
- `RegisterWorld_WhenTopologyVersionIsExhausted_ThrowsWithoutChangingSnapshot`

- [ ] **Step 2: Run red**

Expected: missing topology types and session property.

- [ ] **Step 3: Implement immutable snapshot publication**

Keep one private dictionary behind one private lock. On an effective mutation, copy it into a new snapshot and build a parent-to-sorted-member array map before publication. Repeated identical registration returns the existing snapshot reference.

Compute the next nonzero topology version in a checked context before changing the dictionary. The exhaustion test sets only the predeclared private `version` field to `long.MaxValue` and proves the effective mutation throws without altering either mapping or published snapshot; no injectable counter exists.

Do not infer a parent for unknown worlds. Do not normalize an explicit self-parent into another sentinel.

- [ ] **Step 4: Extend `DeliveryTemperatureGameSession` with the completed catalog**

Construct it with the session generation, expose it through `WorldParentTopology`, and clear its owned mutable dictionary in `ReleaseOwnedState`. Session shutdown must leave already-captured snapshots usable but detached from future publication.

- [ ] **Step 5: Run topology and amended session tests green**

Expected: both classes PASS. The concurrent reader test must use barriers and assert whole snapshot states, not a timing deadline.

- [ ] **Step 6: Review world semantic names and locking**

Confirm every integer parameter/property says `worldId` or `parentWorldId`; no bare `id`, `index`, or `worldMap` survives. Confirm snapshot construction calls no other domain service while locked.

- [ ] **Step 7: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm both content-neutral topology shapes remain covered by pure tests; do not add DLC-specific branches.

- [ ] **Step 8: Prepare and commit**

Use the seven task paths, allowed type `refactor`, and exact subject:

```text
refactor: Publish immutable parent world topology
```

---

### Task 7: Sparse Temperature Amount Accumulator and Prefix-Summed Series

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/TemperatureAmountAccumulator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/TemperatureAmountSeries.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/TemperatureAmountAccumulatorTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/TemperatureAmountSeriesTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs`

**Interfaces:**
- Consumes: `TemperatureDecisionBucket` and `DeliveryTemperatureConstraint`.
- Produces: O(1) stamped accumulation over formula-sized `10,002`-bucket arrays and immutable sparse prefix-sum range queries whose recurring work depends only on touched/occupied buckets.

- [ ] **Step 1: Write failing accumulator tests**

Include:

```csharp
[TestMethod]
public void BeginResourceTag_WhenPreviousTagTouchedFewBuckets_DoesNotCarryAmountsForward()
{
    var accumulator = new TemperatureAmountAccumulator();
    accumulator.BeginResourceTag();
    accumulator.AddTemperatureAmount(10.0f, 4.0f);
    _ = accumulator.BuildSeries();

    accumulator.BeginResourceTag();
    accumulator.AddTemperatureAmount(20.0f, 3.0f);
    var second = accumulator.BuildSeries();

    Assert.AreEqual(1, second.OccupiedBucketCount);
    Assert.AreEqual(3.0f, second.TotalAmount);
}
```

Add exact tests:

- `AddTemperatureAmount_WhenSeveralAmountsShareBucket_SumsThem`
- `AddTemperatureAmount_WhenAmountIsZero_DoesNotTouchBucket`
- `AddTemperatureAmount_WhenAmountsCancelToZero_OmitsBucketFromSeries`
- `AddTemperatureAmount_WhenTemperatureIsBelowMinimumKelvin_UsesBelowRangeBucket`
- `AddTemperatureAmount_WhenTemperatureIsAtOrAboveMaximumKelvin_UsesAboveRangeBucket`
- `BeginResourceTag_WhenStampWraps_PerformsOneSafeFullReset`
- `BuildSeries_WhenTouchedBucketsWereUnordered_SortsByBucketOrdinal`
- `BuildSeries_WhenCalledWithoutBegin_ThrowsInvalidOperationException`
- `AddTemperatureAmount_WhenCalledAfterBuildWithoutNewBegin_ThrowsInvalidOperationException`

Do not expose a test-only constructor or production branch. The linked test assembly may set the private stamp field to `int.MaxValue` through narrowly scoped reflection immediately before the wraparound assertion; the test must assert the exact private field name so a representation change forces deliberate test review.

- [ ] **Step 2: Write failing amount-series tests**

Include disabled, empty, underflow, overflow, and a normal interval:

```csharp
[TestMethod]
public void GetAmountAllowedBy_WhenConstraintIsTenThroughTwenty_SumsOnlyTenThroughNineteen()
{
    var series = Series(
        (-1.0f, 2.0f),
        (9.0f, 3.0f),
        (10.0f, 5.0f),
        (19.0f, 7.0f),
        (20.0f, 11.0f),
        (6203.0f, 13.0f),
        (10000.0f, 17.0f));

    Assert.AreEqual(
        12.0f,
        series.GetAmountAllowedBy(Constraint(10, 20)));
}
```

Add exact tests:

- `GetAmountAllowedBy_WhenConstraintIsDisabled_ReturnsTotalIncludingBelowAndAboveRangeBuckets`
- `GetAmountAllowedBy_WhenConstraintIsEmpty_ReturnsZero`
- `GetAmountAllowedBy_WhenNoBucketOccupied_ReturnsZero`
- `GetAmountAllowedBy_WhenMaximumIsOniStorableTemperatureMaximum_ExcludesExactMaximumAndAbove`
- `GetAmountAllowedBy_WhenMinimumIsZero_ExcludesBelowRangeBucket`
- `GetAmountAllowedBy_WhenRangeIncludesTemperaturesAboveFiveThousand_IncludesObservedBuckets`
- `PublishedArrays_WhenSourceBuffersAreReused_DoNotChange`

- [ ] **Step 3: Add deterministic reference comparison**

With seed `0xA60A17`, generate 10,000 sparse series with bucket counts `0..256`, duplicate additions, cancellation, and random normalized constraints. Compare `GetAmountAllowedBy` with `ReferenceTemperatureEligibilityModel.SumAllowedAmounts`, which directly iterates original `(temperature, amount)` pairs and calls its own explicit truncation/comparison.

- [ ] **Step 4: Run both classes red**

Expected: missing accumulator/series types.

- [ ] **Step 5: Implement generation stamps and touched indices**

Allocate exactly three fixed arrays once per accumulator:

```csharp
private readonly float[] amountsByBucket = new float[TemperatureDecisionBucket.BucketCount];
private readonly int[] stampsByBucket = new int[TemperatureDecisionBucket.BucketCount];
private readonly int[] touchedBucketOrdinals = new int[TemperatureDecisionBucket.BucketCount];
```

On the first touch under the current stamp, initialize the bucket amount and append its ordinal. Do not clear all arrays for ordinary tags. On wraparound, `Array.Clear` amounts/stamps once, set stamp to one, and clear touched count.

- [ ] **Step 6: Implement immutable sorted series and binary-search queries**

Filter exact zero totals, sort only the touched ordinal segment, copy occupied ordinals/amounts into publication-owned arrays, and compute cumulative totals once. `GetAmountAllowedBy` performs two lower-bound searches and one subtraction. It must not loop through every Kelvin class.

Add comments showing the ordinal mapping used for `[minimum, maximum)` and why below-range and at-or-above-maximum buckets are excluded from every enabled valid constraint.

- [ ] **Step 7: Run all tests green and inspect representation**

Expected: both focused classes and randomized reference comparison PASS. Verify no dictionary, LINQ iterator, complete-range loop, or per-`BeginResourceTag` allocation exists. Assert the current three-array element storage is `120,024` bytes (`10,002 × 12`), approximately `117.2 KiB` excluding headers and approximately `58.6 KiB` more than the former range. Assert that only a small bounded number of accumulators may exist and that an unobserved upper-range ordinal causes no read/write/iteration after construction. This is a capacity/structure contract, not an allocation benchmark.

- [ ] **Step 8: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm the static performance-shape test finds no recurring full-range scan and the known reference warnings remain unchanged.

- [ ] **Step 9: Prepare and commit**

Use the five task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add sparse temperature amount series
```

---

### Task 8: Immutable Complete-World, Coverage, and Single-Resource Inventory Publications

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/WorldInventoryCollectionGeneration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/CompleteWorldResourceTemperatureAmounts.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/WorldResourceTagCoverage.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/WorldResourceTemperatureSeriesPublication.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/CompleteWorldResourceTemperatureAmountsBuilder.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/TestDoubles/OniGameTypeStubs.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/WorldResourceTemperaturePublicationTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/CompleteWorldResourceTemperatureAmountsBuilderTests.cs`

**Interfaces:**
- Consumes: sparse accumulator/series and `RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount`.
- Produces: three non-interchangeable immutable contracts: one complete world map, one complete present-tag coverage set, and one complete temperature series for exactly one resource tag.

- [ ] **Step 1: Extend the exact global `Tag` test stub under its parity contract**

Define only the value behavior required by production domain files:

```csharp
public readonly struct Tag : IEquatable<Tag>
{
    private readonly string value;

    public Tag(string value) => this.value = value;
    public bool Equals(Tag other) => StringComparer.Ordinal.Equals(value, other.value);
    public override bool Equals(object? obj) => obj is Tag other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
}
```

Before freezing this stub, statically confirm that the installed global `Tag` exposes the constructor/equality/hash shape used by linked/test code. Keep test identity state private; do not add a public `Value`/label property, game-only conversion, implicit operator, or member unused by the linked pure-domain sources.
The type must remain `global::Tag`; do not place it under `DeliveryTemperatureLimit` or a test namespace. Amend the Task 1 member-parity expectation in the same red-green cycle so the new value members are an explicit production dependency, not an accidental convenience API.

- [ ] **Step 2: Write failing publication-contract tests**

Create `WorldResourceTemperaturePublicationTests` with these exact tests:

- `CreateCoverage_WhenInputContainsDuplicateTags_PublishesFirstSeenUniquePresentTags`
- `CreateCoverage_WhenSourceCollectionMutates_PublishedCoverageDoesNotChange`
- `Contains_WhenTagWasPresent_ReturnsTrue`
- `Contains_WhenTagWasAbsent_ReturnsFalse`
- `CreateCoverage_WhenPresentTagsIsNull_ThrowsArgumentNullException`
- `CreateSeriesPublication_WhenSeriesIsNull_ThrowsArgumentNullException`
- `CreateSeriesPublication_WhenConstructed_PreservesGenerationTagAndSeriesIdentity`

The coverage test must deliberately mutate the source list after construction. The immutable list preserves first-seen input order while deduplicating by `Tag` equality; tests may not assume that ONI dictionary enumeration itself is alphabetically sorted.

- [ ] **Step 3: Run the publication-contract tests red**

Run the focused command with `CLASS_NAME=WorldResourceTemperaturePublicationTests`.

Expected: compilation fails because the generation, coverage, and single-tag publication types do not exist. A test that passes against a local test-only replacement is invalid.

- [ ] **Step 4: Implement the immutable coverage and single-tag contracts**

`WorldResourceTagCoverage.Create` must validate arguments, copy, and deduplicate tags into a private array plus a private owned membership set. `Contains` uses that set and performs no allocation. `WorldResourceTemperatureSeriesPublication` validates its non-null series in the constructor and exposes no mutable state.

Add comments stating the crucial distinction: coverage proves whether a tag key was present at one complete key enumeration; only a series publication proves that a present tag's temperature amounts were refreshed. Do not give either type a method whose name implies complete-world publication.

- [ ] **Step 5: Run the publication-contract tests green**

Expected: every test from Step 2 passes. Inspect the production files and confirm neither stores the caller's mutable collection reference.

- [ ] **Step 6: Write failing complete-world builder-state tests**

Include this core test:

```csharp
[TestMethod]
public void Build_WhenResourceTagIsAbsentFromCandidate_PublishesACompleteMapWithoutThatTag()
{
    var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
    builder.BeginWorld(new WorldInventoryCollectionGeneration(4));
    builder.BeginResourceTag(new Tag("Iron"));
    builder.AddTemperatureAmount(300.0f, 10.0f);
    builder.CompleteResourceTag();

    var amounts = builder.Build();

    Assert.IsTrue(amounts.TryGetSeries(new Tag("Iron"), out _));
    Assert.IsFalse(amounts.TryGetSeries(new Tag("Copper"), out _));
}
```

Add exact tests:

- `BeginWorld_WhenAlreadyBuilding_ThrowsInvalidOperationException`
- `BeginResourceTag_WhenAnotherTagIsOpen_ThrowsInvalidOperationException`
- `CompleteResourceTag_WhenNoTagIsOpen_ThrowsInvalidOperationException`
- `BeginResourceTag_WhenTagRepeatsInOneWorld_ThrowsInvalidOperationException`
- `Build_WhenTagIsOpen_ThrowsInvalidOperationException`
- `Build_WhenComplete_PublishesImmutableSeriesByTag`
- `Build_WhenSourceBuilderIsReused_PreviousPublicationDoesNotChange`
- `Discard_WhenBuildIsIncomplete_ReleasesCandidateReferences`
- `Build_WhenPreviousCandidateExceededRetainedTagLimit_ReplacesMutableDictionary`
- `Build_WhenCalledTwice_ThrowsInvalidOperationException`

Use the real `RetainedCollectionCapacityLimits.MaximumRetainedWorldResourceTagCount` in every retention test. Exercise exactly the limit, `limit + 1`, and a larger but lightweight count generated from deterministic short tag values. Assert that all tags survive the operation and that only the reusable private dictionary is replaced after completion. Do not add an injectable limit, alternate constructor, test policy, or reduced threshold.

- [ ] **Step 7: Run the builder tests red**

Run the focused command with `CLASS_NAME=CompleteWorldResourceTemperatureAmountsBuilderTests`.

Expected: missing complete-world builder and publication types. Confirm the failures are not caused by the earlier coverage types.

- [ ] **Step 8: Implement the explicit complete-world builder state machine**

Use named states `Idle`, `BuildingWorld`, `BuildingResourceTag`, and `Completed`. `Build` copies the tag-to-series mapping into `CompleteWorldResourceTemperatureAmounts`; the publication never exposes the reusable dictionary. `Discard` returns to `Idle` and clears all candidate references after any exception path.

When retained entry count exceeds the configured limit, replace the mutable dictionary instance instead of only calling `Clear`. Verify replacement by narrowly scoped private-field reflection in the linked test assembly; do not add test-only or public diagnostic surface.

- [ ] **Step 9: Run both publication classes green and inspect allocation ownership**

Run `WorldResourceTemperaturePublicationTests`, then `CompleteWorldResourceTemperatureAmountsBuilderTests`, as separate commands. Build candidate A, reuse the builder for candidate B, and verify candidate A remains byte-for-byte equivalent through its semantic accessors.

Expected: both classes pass. Confirm no publication exposes mutable arrays, dictionaries, or source collections and no type uses the ambiguous unqualified term prohibited by Global Constraints.

- [ ] **Step 10: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm fixture/test doubles remain outside production/package declarations and all publications are immutable.

- [ ] **Step 11: Prepare and commit**

Use the eight task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add explicit world inventory publications
```

---

### Task 9: Preaggregated Parent-World Temperature Amount Catalog

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/WorldResourceTemperatureAmountCatalog.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/WorldResourceTagCoverageRequirementState.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/TemperatureConstrainedAmountAvailabilityState.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/TemperatureConstrainedAmountAvailability.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/TemperatureAmountSeries.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/WorldResourceTemperatureAmountCatalogTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/TemperatureAmountSeriesTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionTests.cs`

**Interfaces:**
- Consumes: complete-world publications, FastTrack coverage and single-tag publications, world-parent changes, constraints, and session ownership.
- Produces: per-parent/tag immutable aggregate series, O(1) completeness lookup followed by O(log occupied buckets) amount lookup, and session-owned cleanup without `WorldContainer` enumeration.

- [ ] **Step 1: Write failing complete-world and coverage completeness tests**

Include this test exactly:

```csharp
[TestMethod]
public void GetTemperatureConstrainedAmountAvailability_WhenCoverageContainsTagButSeriesHasNotArrived_ReturnsInventoryIncomplete()
{
    var catalog = new WorldResourceTemperatureAmountCatalog();
    catalog.RegisterWorld(worldId: 1, parentWorldId: 1);
    var generation = new WorldInventoryCollectionGeneration(9);
    catalog.PublishWorldResourceTagCoverage(
        1,
        WorldResourceTagCoverage.Create(generation, new[] { new Tag("Iron") }));

    var availability = catalog.GetTemperatureConstrainedAmountAvailability(
        parentWorldId: 1,
        resourceTag: new Tag("Iron"),
        constraint: Constraint(250, 350),
        expectedCollectionGeneration: generation);

    Assert.AreEqual(
        TemperatureConstrainedAmountAvailabilityState.InventoryIncomplete,
        availability.State);
    Assert.IsFalse(availability.TryGetCompleteAvailableAmount(out _));
}
```

Add exact tests:

- `GetTemperatureConstrainedAmountAvailability_WhenEveryMemberHasCompleteWorldPublication_ReturnsCompleteParentAndChildSum`
- `GetTemperatureConstrainedAmountAvailability_WhenTagIsAbsentFromCompleteWorld_ReturnsCompleteKnownZeroContribution`
- `GetTemperatureConstrainedAmountAvailability_WhenEveryCoverageExcludesTag_ReturnsCompleteZero`
- `GetTemperatureConstrainedAmountAvailability_WhenOneMemberCoverageIsMissing_ReturnsInventoryIncomplete`
- `GetTemperatureConstrainedAmountAvailability_WhenCoverageContainsTagAndCurrentSeriesExists_ReturnsCompleteAmount`
- `GetTemperatureConstrainedAmountAvailability_WhenOnePresentMemberSeriesIsPending_ReturnsInventoryIncompleteRatherThanZero`
- `PublishCompleteWorldResourceAmounts_WhenWorldIsUnknown_ReturnsFalse`
- `PublishWorldResourceTagCoverage_WhenGenerationIsOlder_RejectsLatePublication`
- `PublishWorldResourceTemperatureSeries_WhenNoCurrentCoverageOrCompletePublicationExists_ReturnsFalse`
- `PublishWorldResourceTagCoverage_AfterCompletePublicationForSameGeneration_RejectsSemanticDowngrade`
- `PublishCompleteWorldResourceAmounts_AfterCoveragePublication_UpgradesToCompleteWorldState`
- `GetWorldResourceTagCoverageRequirementState_WhenWorldOrGenerationIsUnknown_ReturnsUnknownWorldOrCollectionGeneration`
- `GetWorldResourceTagCoverageRequirementState_WhenGenerationHasNoCoverage_ReturnsCoverageRequired`
- `GetWorldResourceTagCoverageRequirementState_WhenCoverageOrCompletePublicationIsCurrent_ReturnsCoverageCurrent`

For every availability state, assert both the state and the legal value-access behavior. `TryGetCompleteAvailableAmount` returns `true` only for `Complete`; it returns `false` for `InventoryIncomplete` and `TemperatureConstraintDisabled`, and callers must not inspect the `out` value in either case. This is a result-state contract, not a renamed completion boolean.

- [ ] **Step 2: Write failing incremental replacement and topology tests**

Add exact tests:

- `PublishWorldResourceTemperatureSeries_WhenSameTagRepeats_ReplacesOnlyThatWorldTagContribution`
- `PublishWorldResourceTemperatureSeries_WhenTagWasAbsentFromCoverage_AddsPresentCurrentTagAtomically`
- `PublishWorldResourceTemperatureSeries_WhenDifferentTagChanges_DoesNotRebuildUnaffectedParentTag`
- `PublishWorldResourceTagCoverage_WhenSameGenerationSetChanges_RecomputesOnlyChangedTagCompleteness`
- `RegisterWorld_WhenWorldMovesParent_InvalidatesOldAndNewParentMembershipVersions`
- `RemoveWorld_WhenKnown_RemovesItsContributionAndRecomputesAffectedAggregates`
- `RemoveWorld_WhenLatePublicationArrives_RejectsIt`
- `ClearForGameSession_WhenCalledTwice_IsIdempotent`
- `GetTemperatureConstrainedAmountAvailability_WhenConstraintIsEmpty_ReturnsCompleteZeroWithoutSeriesSearch`
- `GetTemperatureConstrainedAmountAvailability_WhenConstraintIsDisabled_ReturnsTemperatureConstraintDisabled`
- `GetTemperatureConstrainedAmountAvailability_WhenSingleTagPublicationIsConcurrent_ReturnsWholeOldOrNewAggregate`

Prove unaffected aggregate reuse by capturing the relevant private immutable aggregate reference through narrowly scoped test reflection before and after the update. Do not add diagnostic counters, test-mode branches, or observers to the production hot path.

- [ ] **Step 3: Write failing sparse series-combination tests**

Add `TemperatureAmountSeries.Combine` tests for empty inputs, overlapping buckets, disjoint buckets, below-range/at-or-above-maximum buckets, source immutability, and `Combine_WhenSameSeriesAppearsTwice_CountsTwoListedContributions`. Each input-list entry semantically represents one member-world contribution even when two entries reference the same immutable object. The implementation must merge sorted sparse bucket arrays and must not expand any aggregate rebuild to `TemperatureDecisionBucket.BucketCount` entries.

- [ ] **Step 4: Run all three affected classes red**

Run `WorldResourceTemperatureAmountCatalogTests`, `TemperatureAmountSeriesTests`, and `DeliveryTemperatureGameSessionTests` separately.

Expected: failures identify the missing catalog/combine/session members. Correct test setup must not depend on Unity or Harmony.

- [ ] **Step 5: Implement explicit per-world publication state**

Maintain these semantic records under one catalog lock:

```text
world registration -> parent world ID and parent-membership version
world publication -> collection generation, publication strength, immutable present-tag coverage
world/resource tag -> current immutable TemperatureAmountSeries and collection generation
parent world -> member-set version and coverage-complete generation
parent world/resource tag -> immutable aggregate series, pending-present-world count, and validated member-set version
```

Publication strength is domain state, not a boolean named `isComplete`: `NoCoverage`, `TagCoverage`, or `CompleteWorld`. A complete-world publication replaces the entire world state. Coverage establishes known presence/absence but leaves each present tag pending until a series arrives. A single-tag publication is rejected until the world has current coverage or a complete-world publication; when accepted for a previously absent tag, it extends coverage and publishes the series in the same lock transaction.

An aggregate is queryable only if the parent member-set version and expected collection generation match, every member has current coverage, and its pending-present-world count is zero. If every member has coverage and no member reports the requested tag, return `TemperatureConstrainedAmountAvailabilityState.Complete` with zero without constructing a zero-filled series. Missing world publication, missing coverage, or a pending present tag returns `InventoryIncomplete`; a disabled constraint returns `TemperatureConstraintDisabled` before searching a series. Never encode any of these three states as a boolean/value pair.

- [ ] **Step 6: Implement publication-specific aggregate rebuilds**

For `PublishCompleteWorldResourceAmounts`, compute the union of the old and replacement tag sets and rebuild only those parent/tag entries. For `PublishWorldResourceTemperatureSeries`, rebuild exactly one parent/tag entry. For coverage replacement, update completeness only for tags whose presence changed plus currently pending tags; do not rebuild an unrelated amount series.

Capture immutable member series and catalog versions under the lock, release it, call `TemperatureAmountSeries.Combine` outside the lock, then reacquire and publish only if registration/publication versions remain unchanged. Perform exactly one optimistic combine attempt per triggering mutation. If validation fails, discard that candidate; the concurrent mutation that changed the version owns its own rebuild attempt, while the aggregate remains explicitly incomplete until a current candidate publishes. Do not retry or spin. Never acquire or call `WorldParentTopologyCatalog` while holding the inventory lock.

- [ ] **Step 7: Extend game-session ownership**

Construct and expose `WorldResourceTemperatureAmounts`. When topology changes, the game adapter calls topology and amount-catalog services sequentially after releasing each service's lock. `ReleaseOwnedState` invokes `ClearForGameSession` exactly once; late detached-session publications return `false` and retain no candidate reference.

- [ ] **Step 8: Run green and deterministic mixed-publication stress**

Using the dedicated world-resource publication/topology seed `0xC47A109`, execute 10,000 operations covering world registration, complete publication, coverage publication, single-tag replacement, new tag publication, coverage replacement, removal, and reparenting. Compare every query with a test-only direct reference sum over the currently registered publication states.

The reference model must independently implement the three-proof rule; it must not call catalog completeness methods. Assertion messages include seed and operation index.

- [ ] **Step 9: Verify fallback and complexity contracts**

Assert that `GetTemperatureConstrainedAmountAvailability` returns `InventoryIncomplete` for every incomplete case, `TemperatureConstraintDisabled` for the caller-bypass case, and permits amount extraction only for `Complete`. Comments must tell the status adapter to preserve ONI's incoming `fetchable` unchanged for the first two states. Use immutable-reference checks plus narrowly enumerated static call/loop inspection to prove a single-tag update invokes only its named one-tag rebuild primitive, does not call the known complete-world rebuild primitive, does not enumerate `WorldContainer`, and does not scan the temperature range. Do not attempt to prove an unbounded negative such as “contains no loop.”

- [ ] **Step 10: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm incomplete publications remain conservative and the one-tag structural contracts pass.

- [ ] **Step 11: Prepare and commit**

Use the nine task paths, allowed type `refactor`, and exact subject:

```text
refactor: Preaggregate world resource temperature amounts
```

---

### Task 10: Normalized Storage Temperature Interval Sets

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/AllowedTemperatureInterval.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/AllowedTemperatureIntervalSet.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/AllowedTemperatureIntervalSetTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs`

**Interfaces:**
- Consumes: canonical enabled constraints and decision buckets, plus a separately named fact recording whether any destination is unconstrained.
- Produces: immutable `AllowsNoTemperature`, `AllowsEveryTemperature`, or sorted merged inclusive-low/exclusive-high intervals with O(log interval count) lookup; a disabled constraint is rejected rather than silently reinterpreted.

- [ ] **Step 1: Freeze the factory signature before writing its failing tests**

Use this exact production signature in the tests written in Step 2; do not implement it before those tests fail for its absence:

```csharp
internal static AllowedTemperatureIntervalSet CreateFromDestinations(
    bool includesUnconstrainedDestination,
    IReadOnlyList<DeliveryTemperatureConstraint> enabledDestinationConstraints);
```

A missing or disabled `TemperatureLimit` is represented by `includesUnconstrainedDestination = true`; disabled constraints must never be inserted into `enabledDestinationConstraints`, and the factory throws an argument-contract exception if one appears. Do not insert a fake `[0, OniStorableTemperatureBounds.MaximumTemperatureKelvin)` constraint.

- [ ] **Step 2: Write failing state and normalization tests**

Include:

```csharp
[TestMethod]
public void CreateFromDestinations_WhenIntervalsOverlapOrTouch_MergesThem()
{
    var set = AllowedTemperatureIntervalSet.CreateFromDestinations(
        includesUnconstrainedDestination: false,
        [Constraint(10, 20), Constraint(15, 30), Constraint(30, 40)]);

    Assert.AreSequenceEqual(
        new[] { new AllowedTemperatureInterval(10, 40) },
        set.Intervals.ToArray());
}
```

Add exact tests:

- `CreateFromDestinations_WhenNoDestinationContributes_ReturnsAllowsNoTemperature`
- `CreateFromDestinations_WhenUnconstrainedDestinationExists_ReturnsAllowsEveryTemperature`
- `CreateFromDestinations_WhenDisabledConstraintIsSupplied_ThrowsArgumentException`
- `CreateFromDestinations_WhenEnabledConstraintIsEmpty_IgnoresIt`
- `CreateFromDestinations_WhenIntervalsDuplicate_CollapsesThem`
- `CreateFromDestinations_WhenIntervalsAreDisjoint_SortsThem`
- `Allows_WhenBucketIsAtInclusiveMinimum_ReturnsTrue`
- `Allows_WhenBucketIsAtExclusiveMaximum_ReturnsFalse`
- `Allows_WhenBucketIsBelowMinimumKelvin_ReturnsFalseUnlessAllowsEvery`
- `Allows_WhenBucketIsAtOrAboveMaximumKelvin_ReturnsFalseUnlessAllowsEvery`
- `PublishedIntervals_WhenInputListChanges_RemainImmutable`

- [ ] **Step 3: Add exhaustive and randomized reference tests**

For each generated logical destination set, compare interval membership for every formula-derived decision bucket with direct “any destination allows” evaluation from `ReferenceTemperatureEligibilityModel`. Use seed `0x1A7E2A1`, 5,000 destination sets, duplicates, adjacency, disabled, empty, zero, `5000`, `9999`, and exact `10000` boundaries. Translate each generated disabled destination into `includesUnconstrainedDestination: true` and exclude it from `enabledDestinationConstraints`; pass only generated enabled constraints in that list. Keep the separate contract-violation test proving that supplying a disabled value in the enabled-only list throws.

- [ ] **Step 4: Run red**

Expected: missing interval types/factory.

- [ ] **Step 5: Implement sort-and-merge normalization**

Return singleton immutable instances for allows-none and allows-every. For finite constraints, copy valid intervals, sort by minimum then maximum, and merge when `next.MinimumInclusiveKelvin <= current.MaximumExclusiveKelvin`. That comparison intentionally merges adjacency.

Membership uses binary search against interval minima/maxima. Do not allocate a `TemperatureDecisionBucket.BucketCount` membership array and do not retain the input list.

- [ ] **Step 6: Run green and inspect minimal representation**

Expected: all interval and reference tests PASS. Assert `AllowsEveryTemperature` carries no interval array and empty constraints do not contribute endpoints.

- [ ] **Step 7: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm exhaustive current-bound tests and linked-source boundary contracts pass.

- [ ] **Step 8: Prepare and commit**

Use the four task paths, allowed type `refactor`, and exact subject:

```text
refactor: Normalize storage temperature eligibility
```

---

### Task 11: Scoped Temperature Partition Definitions and Class Keys

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/PickupTagIdentity.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/TemperaturePartitionDefinition.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/TemperatureEligibilityClassificationKind.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/TemperatureEligibilityClassKey.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/TemperaturePartitionDefinitionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs`

**Interfaces:**
- Consumes: decision buckets, a caller-supplied immutable endpoint sequence, and a caller-owned positive definition ID.
- Produces: one minimal behavior-equivalence partition plus exact fallback/missing-element class values. Task 12 owns immutable per-parent/tag endpoint facts; Task 13 alone owns per-update endpoint-union interning and definition-ID allocation. No standalone partition catalog or second cache is created.

- [ ] **Step 1: Fix explicit classification kinds and factories in tests**

Use the exact `TemperatureEligibilityClassificationKind` enum and factory methods from the Contract Registry. Assert:

```csharp
TemperatureEligibilityClassKey.NoTemperatureDistinction();
TemperatureEligibilityClassKey.OptimizedPartitionInterval(definitionId, intervalOrdinal);
TemperatureEligibilityClassKey.ExactDecisionBucket(temperatureDecisionBucket);
TemperatureEligibilityClassKey.MissingPrimaryElement();
```

Do not reserve magic negative definition IDs or place `MissingPrimaryElement` immediately after the temperature bucket range. Each key kind validates that only its meaningful fields are populated. Optimized definition IDs are positive and unique within one pickup-update grouping session; all other kinds carry no invented definition ID.

- [ ] **Step 2: Write failing partition boundary tests**

Include:

```csharp
[TestMethod]
public void Classify_WhenEndpointsAreTenAndTwenty_ChangesClassAtEachEndpoint()
{
    var definition = TemperaturePartitionDefinition.Create(7, [10, 20]);

    Assert.AreEqual(0, definition.Classify(Bucket(-1)));
    Assert.AreEqual(0, definition.Classify(Bucket(9)));
    Assert.AreEqual(1, definition.Classify(Bucket(10)));
    Assert.AreEqual(1, definition.Classify(Bucket(19)));
    Assert.AreEqual(2, definition.Classify(Bucket(20)));
    Assert.AreEqual(2, definition.Classify(Bucket(10000)));
}
```

Add exact tests:

- `Create_WhenEndpointsAreUnsortedAndDuplicated_NormalizesThem`
- `Create_WhenEndpointIsZero_SeparatesBelowRangeFromZero`
- `Create_WhenEndpointIsOniMaximum_Separates9999FromAtOrAboveMaximum`
- `Create_WhenNoEndpoints_ThrowsArgumentException`
- `Classify_WhenInputIsEveryDecisionBucket_ReturnsMonotonicOrdinals`
- `TemperatureEligibilityClassKey_WhenOrdinalsMatchButDefinitionsDiffer_IsNotEqual`
- `TemperatureEligibilityClassKey_WhenDefinitionAndOrdinalMatch_IsEqual`
- `ExactFallback_WhenPrimaryElementIsMissing_UsesDedicatedNonTemperatureClassification`
- `PickupTagIdentity_WhenHashesMatchButPrefabTagsDiffer_IsNotEqual`

- [ ] **Step 3: Write equivalence and minimal-fragmentation proof tests**

For constraint set `[10,20)` and `[30,40)`, iterate every formula-derived decision bucket once. For each bucket compute an independent direct allow/deny vector and assert that every bucket assigned to an existing partition class has the same vector as that class's first bucket. Then compare each adjacent bucket pair and assert that the partition class changes if and only if the reference vector changes at a configured endpoint. This O(bucket-count) proof establishes soundness and minimal fragmentation without an unnecessary O(bucket-count²) test loop.

Implement the allow/deny vector in the independent reference model, not via `TemperaturePartitionDefinition`.

- [ ] **Step 4: Write immutable-definition ownership tests**

Add tests proving construction copies and normalizes the caller's endpoint sequence, later caller mutation cannot change the definition, definition ID zero/negative is rejected, and two separately created definitions preserve their caller-supplied IDs even when endpoint sequences are identical. Structural sharing is deliberately absent here and is tested at the per-update owner in Task 13.

- [ ] **Step 5: Run red**

Expected: missing partition/key types.

- [ ] **Step 6: Implement upper-bound classification**

Classification ordinal equals the number of endpoints less than or equal to the decision temperature. Treat `BelowMinimumKelvinOrdinal` as below zero and `AtOrAboveMaximumKelvinOrdinal` as at/above `OniStorableTemperatureBounds.MaximumTemperatureKelvin`. Use binary upper-bound search; do not linearly scan endpoints in the per-pickup path.

Create copies of endpoint inputs and require both a positive caller-supplied definition ID and at least one normalized endpoint. An empty applicable union is represented directly by `TemperatureEligibilityClassKey.NoTemperatureDistinction()` and must never create a meaningless optimized definition. This type neither allocates IDs nor interns definitions.

- [ ] **Step 7: Verify the module has one responsibility**

Inspect the implementation and test source. Neither may contain a parent world, requested-tag map, union cache, mutable ID counter, global/static partition state, or second owner for endpoint facts. Those responsibilities belong to the snapshot and grouping session named above.

- [ ] **Step 8: Run green and exhaustive proof tests**

Expected: every boundary, equality, immutability, and formula-derived exhaustive proof test PASS.

- [ ] **Step 9: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm the classification kinds contain no sentinel magic and the per-pickup classifier contains only binary search.

- [ ] **Step 10: Prepare and commit**

Use the six task paths, allowed type `refactor`, and exact subject:

```text
refactor: Define scoped pickup temperature partitions
```

---

### Task 12: Combined Fetch Eligibility Builder and Version-Validated Publication

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/FetchRequestTopologyVersion.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/FetchRequestTopologyTracker.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/FetchTemperatureEligibilitySnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/FetchTemperatureEligibilityBuilder.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSession.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/WorldResourceTemperatureAmountCatalog.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/FetchRequestTopologyTrackerTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/FetchTemperatureEligibilityBuilderTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs`

**Interfaces:**
- Consumes: the active constraint snapshot/generation, component lookup, world topology, and normalized interval sets.
- Produces: one immutable storage-plus-pickup snapshot stamped with all four validity dimensions, owning the sole per-`(parentWorldId, requestedTag)` endpoint arrays, plus session-side rejection of stale candidates. It does not own partition definitions or definition IDs.

- [ ] **Step 1: Write failing topology-version tests**

Test initial version, one increment per effective change, monotonic ordering, and checked exhaustion. `RecordEffectiveChange_WhenVersionIsExhausted_ThrowsWithoutChangingCurrentVersion` sets only the predeclared private `version` field to `long.MaxValue`. Repeated calls represent actual topology events and always increment; event adapters are responsible for suppressing known no-op callbacks. No injectable counter exists.

- [ ] **Step 2: Write failing builder behavior tests**

Include:

```csharp
[TestMethod]
public void AddFetchRequest_WhenTagHasConstrainedAndUnconstrainedDestinations_PreservesDistinctStorageAndPickupFacts()
{
    var builder = BeginBuilder();
    var iron = new Tag("Iron");
    builder.AddTemperatureConstrainedFetchRequest(
        parentWorldId: 1,
        requestedTags: [iron],
        enabledConstraint: Constraint(10, 20));
    builder.AddUnconstrainedFetchRequest(
        parentWorldId: 1,
        requestedTags: [iron]);

    var snapshot = builder.Build();

    Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
    Assert.IsTrue(storage.AllowsEveryTemperature);
    Assert.AreSequenceEqual(
        new[] { 10, 20 },
        snapshot.CreateSortedDecisionEndpointUnion(1, [iron]).ToArray());
}
```

Add exact tests:

- `AddUnconstrainedFetchRequest_WhenTagsAreRequested_ContributesUnconstrainedStorageAndNoEndpoints`
- `AddTemperatureConstrainedFetchRequest_WhenConstraintIsDisabled_ThrowsArgumentException`
- `AddTemperatureConstrainedFetchRequest_WhenEnabledConstraintIsEmpty_ContributesAllowsNoneAndNoEndpoints`
- `AddTemperatureConstrainedFetchRequest_WhenEnabledConstraintIsNonEmpty_ContributesIntervalAndBothEndpoints`
- `AddTemperatureConstrainedFetchRequest_WhenTagsRepeat_DeduplicatesPerRequest`
- `AddTemperatureConstrainedFetchRequest_WhenSameTagExistsInDifferentParents_DoesNotCrossContaminate`
- `Build_WhenNoFetchRequests_PublishesCompleteEmptySnapshot`
- `Build_WhenCalledBeforeBegin_ThrowsInvalidOperationException`
- `Build_WhenCalledTwice_ThrowsInvalidOperationException`
- `Discard_WhenEnumerationThrows_DropsAllCandidateReferences`
- `Builder_WhenPriorEntryCountExceedsHighWater_ReplacesMutableMaps`
- `Snapshot_WhenBuilderIsReused_RemainsImmutable`
- `CreateSortedDecisionEndpointUnion_WhenPickupMatchesSeveralTags_ReturnsEveryApplicableEndpointOnce`
- `CreateSortedDecisionEndpointUnion_WhenPickupMatchesNoRequestedTag_ReturnsEmptySequence`
- `GetRequestedTags_WhenParentContainsSeveralRequests_ReturnsImmutableFirstEncounterOrder`
- `GetRequestedTags_WhenParentIsUnknown_ReturnsEmptyImmutableSequence`

The retained-map test uses the real `MaximumRetainedFetchEligibilityEntryCount` at the limit, `limit + 1`, and a deterministic larger lightweight workload. It asserts no parent/tag entry is lost and inspects only the predeclared private dictionary identity after `Build`; no injectable threshold or diagnostic surface is allowed.

- [ ] **Step 3: Write failing publication-rejection tests in the session**

For an otherwise valid candidate, separately change each of:

- game-session generation by detaching/replacing the session;
- active constraint generation by replacing one constraint;
- fetch topology version by recording one change; and
- world topology version by registering/reparenting a world.

Assert `TryPublishFetchTemperatureEligibility` returns `false` and the previously published snapshot reference remains unchanged. Add one control test where all versions match and the whole candidate becomes current.

- [ ] **Step 4: Run red**

Expected: missing tracker, builder, snapshot, and session publication methods.

- [ ] **Step 5: Implement one builder with two projections**

For each `(parentWorldId, requestedTag)` builder entry, maintain independently:

- storage destination state: unconstrained flag plus finite constraints;
- pickup decision endpoints: reference-count or deduplicated endpoint collection from enabled nonempty constraints.

Do not derive pickup endpoints from the normalized storage result: an unconstrained storage destination does not make constrained construction/fetch destinations temperature-insensitive.

Build storage interval sets, immutable sorted unique endpoint arrays, and one immutable requested-tag list per parent from the same fully traversed entry set, then stamp one snapshot with captured generations/versions. Requested tags preserve deterministic first encounter during the authoritative traversal after per-request deduplication; they are not sorted through an invented `Tag` ordering. `FetchTemperatureEligibilitySnapshot` is the only retained owner of these facts. It merges only the explicitly supplied applicable requested tags when `CreateSortedDecisionEndpointUnion` is called and never allocates or interns a `TemperaturePartitionDefinition`.

- [ ] **Step 6: Extend the game session with fetch services**

Add `FetchRequestTopology`, an atomically read current `FetchTemperatureEligibilitySnapshot`, and `TryPublishFetchTemperatureEligibility`. Registration changes that alter effective constraints call `RecordEffectiveChange` after registry/index mutation completes. World add/remove/reparent session methods update world topology and inventory first, then record one fetch topology change after both locks are released.

Also expose the current `WorldInventoryCollectionGeneration`. Increment it on an enabled-count transition from zero to nonzero, keep it unchanged for constraint edits while the enabled count remains nonzero because fixed decision-bucket inventory is constraint-independent, clear world resource temperature amounts on a nonzero-to-zero transition, and increment again on the next zero-to-nonzero transition. Perform that clear through `WorldResourceTemperatureAmountCatalog.ClearTemperatureAmountPublicationsForCollectionBypass`: it must replace potentially oversized per-world publication collections, clear derived parent/tag aggregates and collection-completeness evidence, and preserve the live world/parent registrations that will not be replayed merely because collection paused. Do not replace the catalog, duplicate topology state in the session, or call the terminal `ClearForGameSession` operation for a reversible bypass transition. A world added while active must establish the proof appropriate to the selected inventory implementation—complete-world publication for the Klei inventory update path, or coverage plus required present-tag series for the FastTrack inventory update path—before its parent/tag becomes complete. While the enabled count is zero, inventory adapters must decline to open an accumulator/builder session.

Compute the next inventory collection generation in a checked context before the zero-to-nonzero transition publishes any new active state. Add `RegisterTemperatureLimit_WhenInventoryCollectionGenerationIsExhausted_ThrowsWithoutStartingCollection`, using only the predeclared private field, and prove the prior bypass state/catalog remain unchanged.

Candidate publication captures the current active constraint snapshot and current world snapshot once, compares every stamp, and uses `Volatile.Write` only after all comparisons pass.

- [ ] **Step 7: Add deterministic combined reference comparison**

First, run every formula-derived decision bucket through a fixed suite of representative empty, unconstrained, single-parent, multi-parent, single-tag, and multi-tag topologies. Then, using seed `0xFE7C4`, generate 2,000 topologies with `1..8` parents, `1..32` tags, and `0..256` fetch requests; for each generated topology inspect every endpoint-adjacent bucket plus 64 deterministic sampled buckets. This preserves exhaustive range coverage without multiplying 2,000 large topologies by every bucket and every request.

For each selected bucket:

- compare storage interval results with direct “any destination allows” evaluation;
- compare partition equivalence vectors with every relevant constrained destination;
- assert unrelated parent/tag endpoints never appear; and
- assert multi-tag unions contain exactly the endpoints from matched requested tags.

- [ ] **Step 8: Run all affected tests green**

Expected: tracker, builder, session, and reference tests PASS. Verify candidate build performs no Unity call and publication never merges candidate dictionaries into live state.

- [ ] **Step 9: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm all four generation/version rejection dimensions remain independently covered.

- [ ] **Step 10: Prepare and commit**

Use the ten task paths, allowed type `refactor`, and exact subject:

```text
refactor: Build combined fetch temperature eligibility
```

---

### Task 13: Per-Update Pickup Grouping Session and Exact Fallback

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/PickupTemperatureGroupingSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/PickupTemperatureGroupingSessionTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/CanonicalTemperatureEligibilityAgreementTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs`

**Interfaces:**
- Consumes: one captured game session, active constraint snapshot, optional combined snapshot, resolved optional parent world, and applicable requested tags.
- Produces: stable full classification per pickup for one update, exact fallback for every unverifiable state, and bounded reusable per-update dictionaries.

- [ ] **Step 1: Confirm the final registered session signatures before writing tests**

Use these exact signatures already frozen in the Contract Registry:

```csharp
internal void Begin(
    DeliveryTemperatureGameSession session,
    int? resolvedParentWorldId,
    ActiveTemperatureConstraintSnapshot constraints,
    FetchTemperatureEligibilitySnapshot? eligibilitySnapshot,
    WorldParentTopologySnapshot worldTopology);

internal TemperatureEligibilityClassKey Classify(
    int pickupInstanceId,
    PickupTagIdentity tagIdentity,
    IReadOnlyList<Tag> applicableRequestedTags,
    bool hasPrimaryElement,
    float temperatureKelvin);
```

`null` means the parent could not be resolved and forces exact fallback. It never means active world or parent zero.
Do not amend, overload, or append a convenience signature in this task; a genuine mismatch requires a coordinated plan/spec amendment before code is written.

- [ ] **Step 2: Write failing decision-mode tests**

Add exact tests:

- `Classify_WhenNoEnabledConstraints_ReturnsNoTemperatureDistinctionWithoutCacheGrowth`
- `Classify_WhenSnapshotIsCurrent_UsesScopedPartition`
- `Classify_WhenSnapshotIsNull_UsesExactDecisionBucket`
- `Classify_WhenSnapshotConstraintGenerationIsStale_UsesExactDecisionBucket`
- `Classify_WhenSnapshotFetchVersionIsStale_UsesExactDecisionBucket`
- `Classify_WhenSnapshotWorldVersionIsStale_UsesExactDecisionBucket`
- `Classify_WhenParentWorldIsUnresolved_UsesExactDecisionBucket`
- `Classify_WhenPrimaryElementIsMissing_UsesDedicatedMissingClass`
- `Classify_WhenSamePickupRepeats_ReturnsCachedFullKey`
- `Classify_WhenSameTagIdentityRepeatsAcrossPickups_ReusesPartitionDefinition`
- `Classify_WhenApplicableTagsDiffer_DoesNotReuseWrongUnion`
- `Begin_WhenAlreadyActive_ThrowsInvalidOperationException`
- `Complete_WhenInactive_IsIdempotent`
- `Discard_WhenExceptionOccurs_ReleasesPerCallReferences`
- `Complete_WhenPickupCacheExceededHighWater_ReplacesDictionary`

For the high-water case, use the real `MaximumRetainedPickupClassificationCount` at the limit, `limit + 1`, and a deterministic larger lightweight input. Prove every pickup was classified before completion, then compare only the exact predeclared private dictionary identity. No reduced/injected limit is permitted.

- [ ] **Step 3: Write exhaustive cross-domain correctness invariant**

For each generated `(parent, PickupTagIdentity, applicable tags)`, iterate the formula-derived decision buckets once in ordinal order, evaluate every relevant destination constraint directly, and retain the first direct result vector observed for each produced key. Assert:

```text
same TemperatureEligibilityClassKey
    => identical allow/deny result for every relevant constraint

adjacent identical result vectors with no relevant endpoint at the boundary
    => same optimized TemperatureEligibilityClassKey
```

Run the property once with a current optimized snapshot and once with a stale snapshot. Under stale fallback, only identical exact buckets may share a temperature class. This linear exhaustive proof is deliberately not an O(bucket-count²) pair loop.

- [ ] **Step 4: Write a global-fragmentation regression**

Create parent 1/tag Iron with endpoints `[10,20]` and parent 2/tag Food with 1,000 distinct valid endpoints. Assert Iron in parent 1 still has exactly three optimized interval classes. This test directly prevents reintroduction of a global partition.

- [ ] **Step 5: Run red**

Expected: missing grouping session.

- [ ] **Step 6: Implement capture-once mode selection**

At `Begin`, decide one immutable mode:

```text
NoTemperatureDistinction
CurrentScopedSnapshot
ExactDecisionFallback
```

Do not re-read the host or a snapshot during `Classify`. Cache the full `TemperatureEligibilityClassKey` by pickup instance ID. For current-snapshot mode, ask the captured snapshot for the endpoint union of the resolved parent and exact applicable requested tags, intern equal immutable union sequences within this one update, assign positive definition IDs in deterministic first-encounter order, and cache the resulting definition by the complete `PickupTagIdentity` plus an immutable normalized applicable-tag key. No union or definition survives `Complete`/`Discard`, and no second persistent partition catalog exists.

- [ ] **Step 7: Implement cleanup and high-water replacement**

`Complete` and `Discard` clear captured session/snapshot references. If the pickup-classification dictionary exceeded `MaximumRetainedPickupClassificationCount`, replace it. Never retain a `Pickupable`, `GameObject`, `Navigator`, or Unity component in the pure grouping session.

- [ ] **Step 8: Run green and the exhaustive suite**

Expected: grouping and cross-domain tests PASS. Search the grouping implementation and verify it contains no `TemperatureLimit.getTemperatureIndexData`, global endpoint list, Unity type, or `ClusterManager` call.

- [ ] **Step 9: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm stale fallback produces explicit `ExactTemperatureDecisionBucket` keys and missing primary elements produce the separate non-temperature key kind.

- [ ] **Step 10: Prepare and commit**

Use the four task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add exact fallback pickup grouping
```

---

### Task 14: Collision-Free FastTrack Grouping-Key Allocation

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/PickupGroupingAdapters/FastTrackPickupGroupingKeyAllocator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPickupGroupingKeyAllocatorTests.cs`

**Interfaces:**
- Consumes: complete `TemperatureEligibilityClassKey` values from Task 13.
- Produces: update-local one-to-one mapping from `(originalTagBitsHash, temperatureClass)` to FastTrack's required integer key.

- [ ] **Step 1: Write failing allocation tests**

Include:

```csharp
[TestMethod]
public void GetOrAllocate_WhenOriginalHashMatchesButTemperatureClassDiffers_ReturnsDifferentIntegers()
{
    var allocator = new FastTrackPickupGroupingKeyAllocator();
    allocator.Begin(temperatureGroupingIsActive: true);

    var first = allocator.GetOrAllocate(
        123,
        TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1));
    var second = allocator.GetOrAllocate(
        123,
        TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2));

    Assert.AreNotEqual(first, second);
}
```

Add exact tests:

- `GetOrAllocate_WhenCompositeRepeats_ReusesInteger`
- `GetOrAllocate_WhenOriginalHashesDiffer_ReturnsDifferentIntegers`
- `GetOrAllocate_WhenDefinitionIdsDiffer_ReturnsDifferentIntegers`
- `GetOrAllocate_WhenMissingPrimaryElementClassIsUsed_AllocatesNormally`
- `GetOrAllocate_WhenGroupingIsInactive_ReturnsOriginalHashWithoutRetainingEntry`
- `GetOrAllocate_WhenEveryOriginalHashCollides_StillAllocatesUniqueIntegers`
- `GetOrAllocate_WhenIntegerSpaceIsExhausted_ThrowsWithoutWraparound`
- `Begin_WhenAlreadyActive_ThrowsInvalidOperationException`
- `Discard_WhenCalledAfterFailure_ClearsActiveState`
- `Complete_WhenEntryCountExceededHighWater_ReplacesDictionary`

Do not add a test-only constructor. Set the private next-allocation field to `int.MaxValue` with narrow reflection immediately before the exhaustion assertion. Normal production allocation starts at zero; because every candidate uses the allocator while active, allocated values cannot collide with an unallocated raw key in that update.

The high-water test uses the real `MaximumRetainedFastTrackGroupingKeyCount` at the limit, `limit + 1`, and a deterministic larger lightweight composite set. Assert every composite remains retrievable before `Complete`, and assert only the exact predeclared private dictionary identity changes after completion. Do not inject a smaller policy value.

- [ ] **Step 2: Add deterministic uniqueness stress**

Generate 100,000 composite inputs deliberately reusing only 16 original hashes and 257 class keys. Maintain a reference dictionary and assert equality iff the full composite is equal.

- [ ] **Step 3: Run red**

Expected: missing allocator.

- [ ] **Step 4: Implement checked sequential allocation**

Use a dictionary keyed by an immutable composite struct. Allocate a new integer with checked increment only for unseen composites. Do not hash-mix the original value, temperature ordinal, or definition ID into the returned key.

Inactive mode returns the exact original hash and must leave retained entry count at zero.

- [ ] **Step 5: Run green and inspect the old collision expression boundary**

Expected: all allocator tests PASS. The new adapter directory must not contain `(num << 6)`, `(num << 16)`, SDBM commentary, or “extremely unlikely collision” reasoning.

- [ ] **Step 6: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm inactive grouping retains no mapping entries and full composite identity is used.

- [ ] **Step 7: Prepare and commit**

Use the two task paths, allowed type `refactor`, and exact subject:

```text
refactor: Allocate collision-free FastTrack pickup keys
```

---

### Task 15: Harmony Target and Unique-Anchor Contract Verification

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/HarmonyTranspilerInfrastructure/HarmonyPatchContractViolationException.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifier.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/ActiveHarmonyPatchDescriptor.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: reflection metadata, reflection-only active-prefix descriptors, and adapter-supplied instruction predicates; it has no Harmony or game compile-time dependency.
- Produces: exact declared instance/static method, constructor, field, nested-type, single-anchor, and Klei-authority verification primitives for every later adapter.

- [ ] **Step 1: Write failing exact method-contract tests**

Define private fixture classes with overloads and add exact tests:

- `RequireInstanceMethod_WhenOneSignatureMatches_ReturnsThatMethod`
- `RequireInstanceMethod_WhenMethodIsMissing_ThrowsContractViolation`
- `RequireInstanceMethod_WhenOnlyWrongParametersExist_ThrowsContractViolation`
- `RequireInstanceMethod_WhenOnlyWrongReturnTypeExists_ThrowsContractViolation`
- `RequireInstanceMethod_WhenStaticnessDiffers_ThrowsContractViolation`
- `RequireInstanceMethod_WhenSeveralExactCandidatesExist_ThrowsContractViolation`

Core invocation:

```csharp
var method = HarmonyPatchContractVerifier.RequireInstanceMethod(
    typeof(MethodFixture),
    "Target",
    DeclaredMemberVisibility.NonPublic,
    typeof(bool),
    [typeof(int), typeof(string)]);
```

Add symmetric tests for `RequireStaticMethod`, `RequireConstructor`, `RequireField`, and `RequireNestedType`. Every primitive must distinguish public/nonpublic visibility, declared versus inherited membership, instance/static storage where applicable, exact field/return/parameter types, and zero/one/multiple matches. Do not defer these primitives until an adapter needs them and do not let later tasks add a one-off reflection helper.

- [ ] **Step 2: Write failing unique-anchor tests**

Use a small list of test instruction labels:

```csharp
[TestMethod]
public void RequireSingleMatch_WhenTwoInstructionsMatch_ThrowsWithMatchCount()
{
    var exception = Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
        HarmonyPatchContractVerifier.RequireSingleMatch(
            ["load", "anchor", "anchor", "return"],
            instruction => instruction == "anchor",
            "Fixture.Target anchor"));

    Assert.Contains("2", exception.Message);
    Assert.Contains("Fixture.Target anchor", exception.Message);
}
```

Add zero, one, and predicate-exception cases. Predicate failure must be wrapped with the contract name and original exception as `InnerException`.

Add `VerifyKleiAuthority` cases covering no skipping prefix, only a permitted skipping owner, one foreign bool-returning skipping prefix, a foreign non-skipping prefix, a descriptor for another target, and several owners in different priorities. The method receives only descriptors already filtered to active prefixes by the installer, treats an exact-target bool-returning prefix as capable of suppressing the Klei body, and returns `false` when any such owner is outside the exact permitted-owner set. It must not infer authority from patch order alone.

- [ ] **Step 3: Run red**

Expected: missing verifier and exception.

- [ ] **Step 4: Implement reflection-only verification**

Each member primitive examines only the declared type with explicit visibility/staticness flags, exact names where named members apply, exact types, exact ordered parameter types, and non-generic method status. It never selects “first member by name,” searches a base type implicitly, or accepts assignability where the contract requires identity.

`RequireSingleMatch<T>` makes one pass, records match count/index, and throws unless exactly one matches. It does not interpret `ToString()` as a semantic signature; later Harmony predicates inspect opcode and typed `MethodInfo`/`FieldInfo` operands.

- [ ] **Step 5: Run tests green and validate linked test source**

Expected: `HarmonyPatchContractVerifierTests` PASS under the approved test-project links. Verify the three production files import only `System`, `System.Collections.Generic`, and `System.Reflection` namespaces actually required. `ActiveHarmonyPatchDescriptor` is created here because it is part of the frozen Task 15 verifier signature; Task 21 consumes this already-complete reflection-only value rather than introducing a temporary declaration or moving the type later.

- [ ] **Step 6: Run mandatory pipeline gates**

Run pipeline `validate`, `build`, and `test` separately. Confirm the linked reflection-only files reference no Harmony, Unity, Klei, PLib, or FastTrack compile-time type.

- [ ] **Step 7: Prepare and commit**

Use the four task paths, allowed type `refactor`, and exact subject:

```text
refactor: Verify Harmony patch contracts explicitly
```

---

### Task 16: Inactive Game-Session Shutdown and World Lifecycle Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/DeliveryTemperatureGameSessionShutdownPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/WorldParentTopologyPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSession.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: verified installed methods `Game.DestroyInstances()`, `ClusterManager.RegisterWorldContainer(WorldContainer)`, `ClusterManager.UnregisterWorldContainer(WorldContainer)`, and `WorldContainer.SetParentIdx(int)`.
- Produces: inactive manual-patch methods for two-phase shutdown, world add/remove/reparent, and one session operation per effective world change. Coordinated `Game.OnLoadLevel` authority verification/session start belongs exclusively to Task 24's `RuntimePatchInstallation` composition root and is deliberately absent here.

- [ ] **Step 1: Add failing session world-mutation tests**

Add exact tests:

- `RegisterWorld_WhenNew_UpdatesTopologyInventoryAndFetchVersionOnce`
- `RegisterWorld_WhenIdentical_DoesNotAdvanceFetchVersion`
- `RegisterWorld_WhenReparented_InvalidatesBothInventoryParentsAndAdvancesFetchVersionOnce`
- `RemoveWorld_WhenKnown_RemovesTopologyAndInventoryBeforeAdvancingFetchVersion`
- `RemoveWorld_WhenUnknown_IsIdempotent`
- `StopAcceptingPublications_WhenLateWorldCallbackArrives_RejectsMutation`

These tests call session methods, not Harmony adapters.

- [ ] **Step 2: Run red**

Expected: the session lacks coordinated world methods.

- [ ] **Step 3: Implement session world operations without nested locks**

The order is:

```text
capture old immutable topology
mutate/publish topology
release topology lock
apply returned WorldParentTopologyChange to world resource temperature amounts
release amount-catalog lock
record one fetch topology change if HasChanged
```

Reject calls after `StopAcceptingPublications`. Add comments explaining why old/new parent invalidation is based on the returned change rather than a fresh mutable world lookup.

- [ ] **Step 4: Write and run failing lifecycle target-contract tests**

Using reflection fixtures shaped like the installed game, add exact tests for `Game.DestroyInstances()`, `ClusterManager.RegisterWorldContainer(WorldContainer)`, `ClusterManager.UnregisterWorldContainer(WorldContainer)`, and `WorldContainer.SetParentIdx(int)`. Each test asserts full declaring type, static/instance form, return type, and parameter types. Mutations with an overload-only match or changed return type must fail with `HarmonyPatchContractViolationException`. Do not add a `Game.OnLoadLevel` target or prefix here; Task 24 must verify and own that cross-implementation-path authorization boundary.

Run `HarmonyPatchContractVerifierTests`. Expected: the new lifecycle target-resolution assertions fail because the adapter resolution methods do not exist.

- [ ] **Step 5: Implement inactive lifecycle adapter methods**

Do **not** add `[HarmonyPatch]`, `[HarmonyPrefix]`, `[HarmonyPostfix]`, or `[HarmonyFinalizer]` attributes in Gate C files. Do not call these adapters from `Mod` yet.

Provide methods with manual-patch-compatible signatures chosen only after Step 4 proves the installed static/instance form. For `Game.DestroyInstances`, use exactly one of these two shapes and delete the inapplicable form:

```csharp
// Use only when the installed target is an instance method.
internal static void GameDestroyInstancesPrefix(
    Game __instance,
    out DeliveryTemperatureGameSession __state);

// Use instead when the installed target is static. Capture the current session,
// then detach by __state.GameInstanceId; never invent a Game instance.
internal static void GameDestroyInstancesPrefix(
    out DeliveryTemperatureGameSession __state);

internal static Exception GameDestroyInstancesFinalizer(
    Exception __exception,
    DeliveryTemperatureGameSession __state);
```

The prefix detaches/stops before ONI destroys objects. The instance form derives the identity from `__instance`; the static form first captures the current session and detaches by its already stored integer `GameInstanceId`. The finalizer completes release and returns the original exception unchanged. A verifier result and patch signature that disagree is a compile/test failure, never a runtime guess.

- [ ] **Step 6: Implement inactive world topology adapter methods**

Provide:

```csharp
internal static void RegisterWorldContainerPostfix(WorldContainer worldContainer);
internal static void UnregisterWorldContainerPrefix(WorldContainer worldContainer);
internal static void SetParentIdxPostfix(WorldContainer __instance);
```

Read `id` and resulting `ParentWorldId` only on the main thread, then pass integers to the session. Unknown/invalid world IDs produce one rate-limited diagnostic and no guessed mapping.

- [ ] **Step 7: Add target-resolution methods using `HarmonyPatchContractVerifier`**

Each adapter exposes an `internal static MethodInfo Resolve...Target()` with the exact declaring type, return type, and parameter list. Do not use name-only `AccessTools.Method` resolution.

- [ ] **Step 8: Run focused and mandatory pipeline gates**

Run `DeliveryTemperatureGameSessionTests` and `HarmonyPatchContractVerifierTests`, then pipeline `validate`, `build`, and `test` as separate commands.

Expected: PASS/build success. Do not install the build. Inspect the built assembly metadata or source to confirm these new classes contain no Harmony patch-discovery attributes.

- [ ] **Step 9: Prepare and commit**

Use the five task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add session shutdown and world lifecycle adapters
```

---

### Task 17: Inactive Klei World Inventory and Status Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/WorldResourceTemperatureAmounts/TemperatureStatusAvailabilityDecision.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiWorldInventoryTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/TemperatureStatusAvailabilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/TemperatureStatusAvailabilityDecisionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/WorldResourceTemperatureAmounts/WorldResourceTemperatureAmountCatalogTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: complete-world builder/catalog contract, current collection generation, component constraints, and topology snapshot.
- Produces: exception-safe Klei `WorldInventory.Update` enumeration bracketing and exact status `fetchable` replacement, still inactive until Gate D.

- [ ] **Step 1: Write failing status formula characterization tests**

Use the exact current behavior:

```csharp
[DataRow(7.0f, 20.0f, 14.0f)]
[DataRow(7.0f, 3.0f, 10.0f)]
[TestMethod]
public void CalculateFetchable_WhenEligibleTotalAndRemainingAreKnown_ReturnsCharacterizedAmount(
    float eligibleTotal,
    float remaining,
    float expectedFetchable)
{
    Assert.AreEqual(
        expectedFetchable,
        TemperatureStatusAvailabilityDecision.CalculateFetchable(
            eligibleTotal,
            remaining));
}
```

Add:

- `ShouldTryReplacement_WhenOriginalStorageAndFetchableAreBelowMinimum_ReturnsFalse`
- `ShouldTryReplacement_WhenOriginalAmountsMeetMinimum_ReturnsTrue`
- `CalculateFetchable_WhenEligibleTotalIsZero_ReturnsZero`
- `CalculateFetchable_WhenRemainingIsNegative_PreservesMathfMinEquivalent` (characterize actual accepted inputs; do not clamp silently).

- [ ] **Step 2: Add catalog-to-status behavior tests**

Test the adapter decision through a pure method taking `TemperatureConstrainedAmountAvailability`:

- `TemperatureConstraintDisabled`/no component leaves the original `fetchable` unchanged;
- `InventoryIncomplete` leaves it unchanged;
- `Complete` with an extractable amount applies the exact formula;
- enabled-empty constraint applies complete zero without scanning;
- original early-insufficient condition avoids catalog query.

The pure decision must switch exhaustively on the named availability state. It must not collapse the result back into `bool complete` plus an amount, treat an unavailable `out` value as meaningful, or use a default branch that would silently accept a future enum member.

- [ ] **Step 3: Run red**

Expected: missing formula/decision type.

- [ ] **Step 4: Implement the pure status calculation**

Keep it free of Unity and `Mathf`; `Math.Min(float,float)` preserves the required arithmetic. The Harmony hook passes by reference only after the pure method reports a complete replacement.

- [ ] **Step 5: Write and run failing Klei inventory/status patch-contract tests**

Add emitted/captured instruction fixtures for the installed `WorldInventory.Update` and `FetchListStatusItemUpdater.Render200ms` shapes. Require unique semantic anchors for tag start, filtered pickup contribution, tag completion, status early-insufficient branch, and the exact `fetchable` assignment point. Add zero-anchor, duplicate-anchor, wrong-`TotalAmount` getter, and reordered-status-branch mutations.

Run `HarmonyPatchContractVerifierTests`. Expected: new assertions fail because the Klei inventory/status target and transpiler contract methods are absent.

- [ ] **Step 6: Implement inactive Klei inventory bracketing**

Manual patches target `WorldInventory.Update`. Use a per-invocation state containing captured session, world ID, collection generation, and `CompleteWorldResourceTemperatureAmountsBuilder`. The prefix first captures the active-constraint snapshot; when its enabled count is zero, return an explicitly inactive invocation state and allocate/open no builder.

The transpiler must establish exactly one hook for each semantic point:

1. resource-tag enumeration begins;
2. each `Pickupable.TotalAmount` is about to contribute;
3. that resource tag completes.

Prefix begins the complete-world candidate. Postfix builds it and calls `PublishCompleteWorldResourceAmounts`. Finalizer discards it after any exception and clears invocation state. Publication occurs only in postfix after every tag completed. The adapter adds temperature accumulation to Klei's existing pickupable enumeration; it must not perform a second inventory or pickupable traversal.

Use typed operands and `RequireSingleMatch`/explicit expected match counts. If the installed method has a changed or duplicate anchor, throw during patch installation; do not warning-and-run a partial instrument.

- [ ] **Step 7: Implement the inactive shared status availability hook**

Target `FetchListStatusItemUpdater.Render200ms`. Preserve the original insufficient-material early path. Resolve the destination constraint from the captured current session, resolve parent through a captured topology snapshot, and ask the catalog with `session.CurrentWorldInventoryCollectionGeneration`.

When the catalog returns `TemperatureConstraintDisabled` or `InventoryIncomplete`, leave the incoming `fetchable` exactly unchanged. Only `Complete` may supply an amount and assign the characterized formula. Do not enumerate `ClusterManager.Instance.WorldContainers`.

- [ ] **Step 8: Keep status installation conditional by construction**

These adapter methods contain no option lookup in per-update code. Task 24's installer omits all Klei and FastTrack inventory/status patches when `DeliveryTemperatureLimitOptions.Instance.CheckTemperatureForStatusItems` is false.

- [ ] **Step 9: Run focused tests and production build**

Run status tests, catalog tests, and `HarmonyPatchContractVerifierTests` separately. Then run pipeline `validate`, `build`, and `test` separately. Expected: tests and all pipeline gates pass. Do not install. Verify new adapters have no patch-discovery attributes, no static world/tag amount dictionary, no FastTrack reflection, and no ambiguous content-mode terminology.

- [ ] **Step 10: Prepare and commit**

Use the six task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add sparse temperature status adapters
```

---

### Task 18: Inactive Authoritative Fetch Traversal and Sweep Eligibility Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/ClearableDestinationSweepEligibilityInput.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/ClearableDestinationSweepEligibility.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiAuthoritativeFetchTemperatureEligibilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/ClearableDestinationSweepEligibilityTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/FetchTemperatureEligibilityBuilderTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: `GlobalChoreProvider.fetchMap` traversal, combined builder/snapshot, component index, topology snapshot, and fetch topology tracker.
- Produces: one exception-safe combined snapshot candidate per authoritative traversal, topology version hooks, and conservative sweep decisions; inactive until Gate D.

- [ ] **Step 1: Write failing conservative sweep-decision tests**

Pin exact decision order:

```csharp
[TestMethod]
public void AllowsClearing_WhenOriginalDestinationExistsButSnapshotIsStale_ReturnsFalse()
{
    var input = new ClearableDestinationSweepEligibilityInput(
        originalHasDestination: true,
        enabledTemperatureConstraintCount: 1,
        hasPrimaryElement: true,
        isParentWorldResolved: true,
        isEligibilitySnapshotCurrent: false,
        currentEligibilityAllowsPickup: false);

    Assert.IsFalse(ClearableDestinationSweepEligibility.AllowsClearing(input));
}
```

Add:

- `AllowsClearing_WhenOriginalDestinationIsAbsent_ReturnsFalse`
- `AllowsClearing_WhenNoTemperatureConstraintIsEnabled_ReturnsOriginalDestinationResult`
- `AllowsClearing_WhenPrimaryElementIsMissing_ReturnsFalse`
- `AllowsClearing_WhenParentWorldIsUnresolved_ReturnsFalse`
- `AllowsClearing_WhenCurrentEligibilityAllowsPickup_ReturnsTrue`
- `AllowsClearing_WhenCurrentEligibilityRejectsPickup_ReturnsFalse`

Make parameters semantic—prefer a small immutable input value over four ambiguous booleans in production.

- [ ] **Step 2: Run red**

Expected: missing sweep decision type.

- [ ] **Step 3: Write failing authoritative-traversal and event target-contract tests**

Add exact reflection/instruction fixtures for all three topology event methods, `GlobalChoreProvider.UpdateStorageFetchableBits`, and `GlobalChoreProvider.ClearableHasDestination`. Require unique typed anchors for parent-section start and selected `FetchChore` traversal. Add mutations for overload ambiguity, a second fetch-map traversal anchor, a missing selected-chore anchor, and a changed `OnTagsChanged` parameter.

- [ ] **Step 4: Run domain and patch-contract tests red**

Run `ClearableDestinationSweepEligibilityTests` and `HarmonyPatchContractVerifierTests` separately. Expected: the pure decision/input types and inactive adapter contract entry points are missing.

- [ ] **Step 5: Implement inactive fetch-topology event methods**

Manual adapters target:

- `GlobalChoreProvider.AddChore(Chore)` postfix;
- `GlobalChoreProvider.RemoveChore(Chore)` postfix; and
- `FetchChore.OnTagsChanged(object)` postfix.

Record one change only when the affected chore is/was a fetch request and the callback represents an effective topology/tag change. For `OnTagsChanged`, capture the pre-call requested tag identity in prefix and compare in postfix; do not increment merely because an event fired with identical tags.

- [ ] **Step 6: Implement one complete `UpdateStorageFetchableBits` build session**

Prefix captures session, active constraints, fetch version, and world topology and calls builder `Begin`. Transpiler hooks:

- the start of each authoritative parent-world `fetchMap` section; and
- each traversed `FetchChore` after ONI has selected it for the section.

For each chore, copy/read requested tags during the main-thread traversal and resolve the destination GameObject ID plus immutable constraint through the component index. Call `AddUnconstrainedFetchRequest` for a missing/disabled component, or `AddTemperatureConstrainedFetchRequest` for an enabled constraint. Never encode the distinction through a boolean paired with a possibly-default constraint. Postfix builds and attempts one combined publication. Finalizer discards on exception or after a rejected candidate.

Do not traverse `fetchMap` a second time for pickup partitions.

- [ ] **Step 7: Implement inactive `ClearableHasDestination` postfix**

Capture the session and snapshots once. Preserve original false and zero-active bypass. Missing primary, unresolved parent, missing tag, or any stale version produces conservative false. Current interval membership uses one canonical `TemperatureDecisionBucket`.

- [ ] **Step 8: Verify target and anchor contracts**

Resolve exact installed signatures. Require each semantic insertion count explicitly; a zero or duplicate match throws `HarmonyPatchContractViolationException`. No anchor may depend solely on local number or `operand.ToString()` text.

- [ ] **Step 9: Run focused tests and production build**

Run clearable tests, builder tests, and `HarmonyPatchContractVerifierTests` separately. Then run pipeline `validate`, `build`, and `test` separately. Expected: focused tests and all pipeline gates pass. Do not install. Verify new code has no `HashSet<Tag>[]`, global band, complete-temperature-range scan, or synchronous rebuild in setter/event hooks.

- [ ] **Step 10: Prepare and commit**

Use the six task paths, allowed type `refactor`, and exact subject:

```text
refactor: Capture authoritative fetch temperature eligibility
```

---

### Task 19: Inactive Klei Pickup Grouping Adapter

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/ThreadConfinedSessionSlot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiPickupTemperatureGroupingPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/ThreadConfinedSessionSlotTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/PickupTemperatureGroupingSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: `FetchManager.FetchablesByPrefabId.UpdatePickups(Navigator,int)`, its private comparator, current combined snapshot, requested tags by parent, and pickup grouping session.
- Produces: one capture-once update context, applicable-tag union once per base identity, comparator/suppression agreement, and exception-safe nested state; inactive until Gate D.

- [ ] **Step 1: Confirm the requested-tag snapshot contract implemented in Task 12**

Implement:

```csharp
internal IReadOnlyList<Tag> GetRequestedTags(int parentWorldId);
```

Run the two `GetRequestedTags` Task 12 tests and confirm the method returns deterministic immutable first-encounter order and an empty immutable list for an unknown parent. This task consumes that finalized method; it must not modify the snapshot API or reimplement requested-tag storage. The adapter uses it to evaluate `KPrefabID.HasTag` once per distinct `PickupTagIdentity`, cache applicable tags, and avoid scanning requested tags for every pickup.

- [ ] **Step 2: Write failing thread-confined nesting tests**

`ThreadConfinedSessionSlot<T>` supports explicit push/pop tokens:

- `Enter_WhenEmpty_SetsCurrent`
- `Enter_WhenNested_SavesPreviousAndSetsNested`
- `Exit_WhenNested_RestoresPrevious`
- `Exit_WhenTokenIsStale_ThrowsLifecycleViolation`
- `DiscardAll_AfterException_ClearsReferences`
- `Enter_WhenGameSessionGenerationChanges_DiscardsOldThreadStateBeforeUse`

Do not use `AsyncLocal`; FastTrack/ONI worker identity is thread-based and the game-loaded runtime target is `netstandard2.1`.

- [ ] **Step 3: Add applicable-tag caching tests**

Extend grouping tests to count the callback used to identify applicable requested tags. For 10,000 pickups with the same complete `PickupTagIdentity`, assert it runs once in the update. When `PrefabTag` differs despite equal original hash, assert it runs separately.

- [ ] **Step 4: Run red**

Expected: missing slot and requested-tag API.

- [ ] **Step 5: Write and run failing Klei pickup-path patch-contract tests**

Add exact fixtures for `FetchManager.FetchablesByPrefabId.UpdatePickups(Navigator,int)`, private `PickupComparerIncludingPriority.Compare`, and duplicate suppression. Require one comparator extension anchor and one suppression extension anchor that both consume the same full semantic key. Add mutations for changed candidate type, duplicate compare anchor, missing suppression anchor, and an installed method shape that would require an unverified Unity/native call from a worker.

Run `HarmonyPatchContractVerifierTests`. Expected: new Klei pickup target/anchor assertions fail because the adapter entry points are absent.

- [ ] **Step 6: Implement inactive `UpdatePickups` prefix/postfix/finalizer**

Prefix runs on the authoritative invocation thread. Before activation, the patch contract must characterize whether that installed method is main-thread or worker-scheduled; the implementation must not assume either. At entry it must:

- capture the current game session and active constraints;
- call `Navigator.GetAnchorCell()` once;
- resolve raw world and parent through an immutable topology snapshot; and
- capture the combined snapshot once.

Enter a thread-confined update context containing one `PickupTemperatureGroupingSession`. Postfix completes it. Finalizer discards/restores the previous nested context and returns the original exception.

- [ ] **Step 7: Implement applicable-tag resolution once per identity**

For each new `PickupTagIdentity`, iterate only `snapshot.GetRequestedTags(parentWorldId)`, evaluate requested-tag membership once, freeze the matches, and cache the resulting partition definition. On a worker-scheduled method, membership and temperature may read only the exact cached `KPrefabID`/`PrimaryElement` managed fields whose no-native-transition and cross-thread-read contract was verified from the installed game. Never call `GetComponent`, enumerate Unity objects, or query mutable topology. If those reads cannot be proved, the Klei pickup adapter is incompatible and coordinated activation must stop; it cannot be advertised as a FastTrack fallback.

- [ ] **Step 8: Patch comparator and duplicate suppression with one full key**

Preserve every original comparator field/order. After original equality through priority/tag grouping, compare the complete `TemperatureEligibilityClassKey` using its explicit classification-kind-aware ordering. Duplicate suppression uses that same cached full key under the same update context; it must not compare only definition/ordinal fields that are meaningless for exact or missing-primary classifications.

Missing/stale/unresolved cases classify through exact buckets; zero enabled constraints add no temperature comparison.

- [ ] **Step 9: Verify target/anchor contracts and build**

Require exact signatures for `UpdatePickups` and private `PickupComparerIncludingPriority.Compare`. Require unique structural anchors for the comparator insertion and suppression insertion. Run affected focused tests, then pipeline `validate`, `build`, and `test` separately. Do not install.

- [ ] **Step 10: Prepare and commit**

Use the five task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add scoped Klei pickup grouping adapter
```

---

### Task 20: Inactive Direct Eligibility and Fetch-Coalescing Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/FetchChoreTemperatureConstraintContainment.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiDirectDeliveryEligibilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/FetchChoreTemperatureConstraintContainmentTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: component index constraints and canonical direct `Allows` semantics.
- Produces: allocation-free direct checks plus explicit root/candidate fetch-chore containment; inactive until Gate D.

- [ ] **Step 1: Write failing fetch-coalescing characterization tests**

Represent a missing/disabled constraint as unconstrained and add exact tests:

- `CanCombine_WhenCandidateIsUnconstrained_ReturnsTrue`
- `CanCombine_WhenRootIsUnconstrainedButCandidateIsConstrained_ReturnsFalse`
- `CanCombine_WhenCandidateIntervalIsInsideRoot_ReturnsTrue`
- `CanCombine_WhenCandidateMinimumIsBelowRoot_ReturnsFalse`
- `CanCombine_WhenCandidateMaximumIsAboveRoot_ReturnsFalse`
- `CanCombine_WhenConstraintsAreEqual_ReturnsTrue`
- `CanCombine_WhenCandidateIsEmpty_ReturnsTrueBecauseItAdmitsNoAdditionalPickup`
- `CanCombine_WhenRootIsEmptyAndCandidateIsNonEmpty_ReturnsFalse`

Include comments in the test explaining that combination is safe only when every pickup admitted by the candidate destination is also admitted by the root destination.

- [ ] **Step 2: Run red**

Expected: missing compatibility type.

- [ ] **Step 3: Implement semantic set containment**

Do not compare component references. Implement unconstrained/empty/finite cases explicitly, then finite containment as:

```csharp
root.MinimumInclusiveKelvin <= candidate.MinimumInclusiveKelvin &&
candidate.MaximumExclusiveKelvin <= root.MaximumExclusiveKelvin
```

- [ ] **Step 4: Write and run failing direct-adapter patch-contract tests**

Add exact reflection/instruction fixtures for all four Klei targets, including the internal `ClearableManager` type and compiler-generated candidate delegate. Require unique typed anchors and reject name-only, local-number-only, or display-class-name-only matches. Add mutations for a changed delegate closure field, two `CanReach` delegates, a missing direct-result branch, and a changed `IsFetchablePickup` return type.

Run `HarmonyPatchContractVerifierTests`. Expected: new direct-adapter contract tests fail because target/anchor methods are absent.

- [ ] **Step 5: Implement inactive direct adapter methods**

Cover:

- `FetchManager.IsFetchablePickup` postfix;
- internal `ClearableManager.CollectChores` transpiler hook;
- `FetchAreaChore.StatesInstance.Begin` coalescing hook; and
- its candidate `CanReach` delegate hook.

Each direct candidate check:

1. preserves an existing false result;
2. resolves the destination constraint with one component-index lookup;
3. bypasses missing/disabled constraint;
4. preserves characterized permissive behavior for missing `PrimaryElement`; and
5. calls canonical `constraint.Allows(temperature)` once.

No direct hook captures/rebuilds a global snapshot or allocates a collection.

- [ ] **Step 6: Replace fragile transpiler matching**

Use exact reflected fields/methods and unique typed instruction sequences. If the internal `ClearableManager` type/method or delegate display class differs, installation reports a contract violation; it must not leave a partially modified instruction stream.

- [ ] **Step 7: Run focused tests and production build**

Run containment tests and `HarmonyPatchContractVerifierTests` separately, then pipeline `validate`, `build`, and `test` separately. Expected: focused tests and all pipeline gates pass. Do not install. Review generated code paths for boxing, LINQ, tuples allocated on the heap, repeated component lookup, or any complete-range scan.

- [ ] **Step 8: Prepare and commit**

Use the four task paths, allowed type `refactor`, and exact subject:

```text
refactor: Centralize direct fetch temperature checks
```

---

### Task 21: FastTrack 0.18.4.0 Feature Contract Verification

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackFeature.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackFeatureCompatibilityState.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackVerifiedMember.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackFeatureCompatibility.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityReport.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackLoadedGameInspectionInput.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackAssemblyFileIdentityReadState.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackAssemblyFileIdentity.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/IFastTrackAssemblyFileIdentityReader.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackAssemblyFileIdentityReader.cs`
- Consume unchanged from Task 15: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/ActiveHarmonyPatchDescriptor.cs` (not a Task 21 commit path)
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityInspector.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCompatibilityInspectorTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackAssemblyFileIdentityReaderTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackReflectionEmitFixture.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackGitHubReleaseAssemblyContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/README.md`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/FastTrack.dll`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/LinkedProductionSourceBoundaryContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: enabled-mod evidence supplied by `DeliveryTemperatureLimitMod.OnAllModsLoaded`, loaded assembly metadata, one injected physical-file identity reader, Harmony patch-owner snapshots converted to reflection-only descriptors, `HarmonyPatchContractVerifier`, and the separately provenance-pinned real FastTrack fixture.
- Produces: one immutable compatibility report that independently classifies FastTrack world inventory, pickup grouping, and direct chore comparison as `ModNotLoaded`, `ReplacementInactive`, `Ready`, or `Incompatible`.

- [ ] **Step 1: Declare the exact reflection-only compatibility contract in failing tests**

The test-visible contract is:

```csharp
internal enum FastTrackFeature
{
    WorldInventory,
    PickupGrouping,
    DirectDeliveryEligibility
}

internal enum FastTrackFeatureCompatibilityState
{
    ModNotLoaded,
    ReplacementInactive,
    Ready,
    Incompatible
}

internal sealed class FastTrackCompatibilityReport
{
    internal string? AssemblyIdentity { get; }
    internal Version? AssemblyVersion { get; }
    internal Version? FileVersion { get; }
    internal string? AssemblySha256 { get; }
    internal FastTrackFeatureCompatibility GetFeature(FastTrackFeature feature);
}

internal sealed class FastTrackCompatibilityInspector
{
    internal FastTrackCompatibilityInspector(
        IFastTrackAssemblyFileIdentityReader assemblyFileIdentityReader);

    internal FastTrackCompatibilityReport Inspect(
        FastTrackLoadedGameInspectionInput inspectionInput);
}
```

`FastTrackFeatureCompatibility` must expose the feature, state, verified reflected method handles needed by its adapter, and one semantic failure code/message only when incompatible. It must not expose Harmony types. `FastTrackLoadedGameInspectionInput` contains the enabled-for-active-content evidence, optional assembly, and immutable `ActiveHarmonyPatchDescriptor` values prepared by the installer.

Add exact tests:

- `Inspect_WhenFastTrackModIsNotLoaded_ClassifiesEveryFeatureAsModNotLoaded`
- `Inspect_WhenAssemblyIsLoadedButWorldInventoryReplacementIsInactive_ClassifiesWorldInventoryAsReplacementInactive`
- `Inspect_WhenAssemblyIsLoadedButPickupPrefixIsNotActive_ClassifiesPickupGroupingAsReplacementInactive`
- `Inspect_WhenFeaturesHaveDifferentActivationStates_ClassifiesEachIndependently`
- `Inspect_WhenFileVersionIsNotExactly01840_ClassifiesActiveFeaturesAsIncompatible`
- `Inspect_WhenAssemblyIsPresentButDisabledForLoadedGame_PerformsNoFeatureBinding`
- `GetFeature_WhenFeatureValueIsUnknown_ThrowsArgumentOutOfRangeException`

- [ ] **Step 2: Build emitted FastTrack contract fixtures without adding a dependency**

`FastTrackReflectionEmitFixture` uses `System.Reflection.Emit` to create an in-memory assembly with the exact expected full type names, private fields, method names, signatures, and minimal typed IL shapes. Provide explicit fixture methods that remove one field, change one signature, duplicate one semantic anchor, or change `PickupTagKey.Equals` semantics.

Do not compile source text with an external compiler and do not add a mock package. Fixture method names must state the mutation, for example `CreateWithRunUpdateMissingSingleTagBranch`. Active-patch descriptors identify the exact emitted prefix method, target method, Harmony owner string, and priority using reflection-only values.

Dynamic emitted assemblies have no physical file identity. Inspector tests that need to reach structural feature classification inject a narrowly named test adapter implementing `IFastTrackAssemblyFileIdentityReader` and returning an explicit identity result; they do not exercise the production file reader. Separate `FastTrackAssemblyFileIdentityReaderTests` exercise the production reader with a real temporary copied assembly and cover dynamic assembly, unavailable location, missing file, read failure, exact file version, and exact uppercase SHA-256 states. The adapter seam exists to separate physical identity I/O from structural inspection, not to relax `Ready` semantics.

- [ ] **Step 3: Add the provenance-pinned real FastTrack fixture and static contracts**

Obtain the latest available DLL from the official GitHub repository release asset—not from an unverified mirror—and verify before placing it in the fixture directory:

```text
release URL: https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta
closest source revision: e24e8f3082a52785e971943a8f1fff8de0ca8dff
file version: 0.18.4.0
assembly version: 0.18.0.0
FastTrack.dll SHA-256: D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD
download ZIP SHA-256: 8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D
```

If the implementation environment cannot retrieve that official asset, stop and ask the user to supply the official ZIP/DLL or clone the named upstream repository/revision, as they offered. Verify the supplied bytes against both recorded digests before use. Do not substitute a mirror, recompile a lookalike fixture, or weaken the provenance test.

The fixture README must state plainly that the actual Steam Workshop-distributed DLL could not be located or proven byte-identical; support is to this available `0.18.4.0` artifact on a best-efforts basis. `FastTrackGitHubReleaseAssemblyContractTests` use `System.Reflection.Metadata`/`PEReader` to inspect the GitHub release DLL without resolving or executing its dependencies. They assert the exact version and digest, every required world-inventory and pickup-grouping type/member/branch/anchor contract, and the absence of the former direct chore replacement. That absence is affirmative contract evidence: the official artifact's direct feature is `ReplacementInactive`, and the Klei direct-delivery path remains authoritative.

The GitHub artifact does not contain `PeterHan.FastTrack.GamePatches.ChoreComparator.CheckFetchChore` or `ChorePatches.GlobalChoreProvider_CollectChores_Patch`; upstream removed those replacements in commit `201d2457162544504fbbf185ba076da1e9e9d41a`. Do not invent a current-artifact direct contract, relabel absence as incompatibility, or imply that the static fixture verifies a direct adapter. The emitted fixture retains the former exact shape solely so runtime inspection can fail closed if an unproven same-file-version Workshop binary actually activates that replacement.

Apply the exact Task 21 configuration approval by updating, not duplicating, the SDK-default `None` item:

```xml
<None Update="Fixtures\ThirdParty\FastTrack\0.18.4.0\FastTrack.dll">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

Do not add FastTrack as a `<Reference>`, production dependency, analyzer, or compile item. Do not copy it to production output or change either lockfile. The fixture is test input only, and the package-boundary test must prove it cannot be shipped in the mod package.

- [ ] **Step 4: Run compatibility tests red**

Run `FastTrackCompatibilityInspectorTests`.

Expected: missing report/inspector/state types. If fixture construction fails first, correct the fixture until the intended missing-production-type failure is reached.

- [ ] **Step 5: Implement exact assembly and feature activation inspection**

The inspector must verify, by full name and exact signature, the installed equivalents of at least:

- `PeterHan.FastTrack.UIPatches.BackgroundWorldInventory.RunUpdate`;
- `BackgroundWorldInventory.SumTotal`;
- the fields that distinguish the first complete update and identify `WorldInventory`/`WorldContainer`;
- the active FastTrack prefix replacing `WorldInventory.Update`;
- `PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate.BeforeUpdatePickups`;
- nested `PickupTagDict.AddItem` and `PickupTagKey` constructor/equality shape; and
- the former FastTrack direct chore-comparison target only when active Harmony descriptors prove that a loaded same-file-version binary actually contains and activates it.

Treat the loaded assembly and actual active Harmony ownership as authoritative at runtime. The production inspector instance must receive `FastTrackAssemblyFileIdentityReader`; require its successful physical-file result and file version exactly `0.18.4.0` for a `Ready` feature, then verify structure. Do not use the fixture SHA-256 as a runtime allowlist. Current upstream source and fixture are evidence for expected semantics, not permission to accept a structurally different loaded body. Assembly presence alone can produce only `ReplacementInactive`, never `Ready`; dynamic/unreadable/missing physical identity makes an active feature `Incompatible`, with the exact reader state in its diagnostic.

Direct-delivery inspection is deliberately asymmetric. For the provenance-pinned official artifact, the missing replacement must remain `ReplacementInactive` and incur no FastTrack direct-path runtime work. If a not-proven-byte-identical Workshop DLL with the same file version exposes an active former replacement, only exact owner, target, member, signature, and IL verification may make that feature `Ready`; any mismatch is `Incompatible` so Task 24 can abort coherently. This defensive inspection does not broaden the support claim beyond the recorded best-efforts qualification.

The world-inventory `Ready` contract must prove the two behavioral branches: first update iterates all inventory entries; later updates select one entry through `updateIndex`. It must also prove that removing a pickupable does not remove the dictionary key. If the latter cannot be proved, classify world inventory as `Incompatible` because a one-time coverage set could become false.

- [ ] **Step 6: Write and run mutation tests for every required contract**

Add exact tests:

- `Inspect_WhenWorldInventoryRunUpdateSignatureChanges_ClassifiesOnlyWorldInventoryAsIncompatible`
- `Inspect_WhenRunUpdateNoLongerHasCompleteAndSingleTagBranches_ClassifiesWorldInventoryAsIncompatible`
- `Inspect_WhenRemovedFetchableCanDeleteTagKey_ClassifiesWorldInventoryAsIncompatible`
- `Inspect_WhenPickupTagKeyEqualityUsesMoreThanAllocatedHash_ClassifiesPickupGroupingAsIncompatible`
- `Inspect_WhenAddItemConstructorAnchorIsMissing_ClassifiesPickupGroupingAsIncompatible`
- `Inspect_WhenAddItemConstructorAnchorIsDuplicated_ClassifiesPickupGroupingAsIncompatible`
- `Inspect_WhenHarmonyOwnerDoesNotMatchFastTrack_ClassifiesReplacementAsInactiveRatherThanClaimingReady`
- `Inspect_WhenDirectComparatorContractChanges_ClassifiesOnlyDirectDeliveryEligibilityAsIncompatible`

Expected: each mutation affects only the named feature. A broad catch that marks every feature incompatible is not acceptable.

- [ ] **Step 7: Implement and consume one physical-file identity read**

`FastTrackAssemblyFileIdentityReader.Read` is the only production code that reads `Assembly.Location`, `FileVersionInfo`, or the assembly file stream. It returns the exact `FastTrackAssemblyFileIdentityReadState` plus file version/digest only when available. Use the `netstandard2.1`-available `SHA256.Create().ComputeHash(stream)` pattern, dispose both objects, and normalize the digest to uppercase hexadecimal without a newer-runtime-only convenience API. It catches only the enumerated file/metadata/I/O/access failures and converts them to `ReadFailed` with a semantic message; do not catch process-corruption exceptions.

The inspector calls the reader exactly once for an enabled loaded assembly, then reads `AssemblyName.FullName`/`AssemblyName.Version`, combines both identity sources into the immutable report, and never reopens the file. Tests use a temporary fixture file and assert exact version/digest, reader call count, and state propagation. Dispose every stream deterministically and share the immutable report thereafter. No update, pickup, status, comparator, or delivery hook may call the reader.

- [ ] **Step 8: Run green and mandatory pipeline gates**

Run `FastTrackCompatibilityInspectorTests`, `FastTrackAssemblyFileIdentityReaderTests`, `FastTrackGitHubReleaseAssemblyContractTests`, and `HarmonyPatchContractVerifierTests` separately, then pipeline `validate`, `build`, and `test` separately. Do not install or launch ONI with FastTrack.

Inspect the new production files and confirm they reference only BCL reflection/IO types plus reflection-only patch descriptors. They must not reference FastTrack, Harmony, Klei, Unity, or PLib types at compile time.

- [ ] **Step 9: Prepare and commit**

Use all twenty created or modified Task 21 paths, excluding the unchanged Task 15 descriptor, allowed type `test`, and exact subject. The twentieth path is the linked-source boundary contract, which must continue forbidding compile-time third-party dependencies while permitting `PeterHan.` identities only as explicit reflection-contract string literals in the dedicated feature-verification directory:

```text
test: Verify active FastTrack feature contracts
```

---

### Task 22: Inactive FastTrack Incremental World Inventory Adapter

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/InventoryUpdateAdapters/FastTrackWorldInventoryPublicationKind.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/InventoryUpdateAdapters/FastTrackWorldInventoryPublicationResult.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/InventoryUpdateAdapters/FastTrackWorldInventoryPublicationSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/InventoryUpdateAdapters/FastTrackWorldInventoryTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackWorldInventoryPublicationSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCompatibilityInspectorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: a `Ready` world-inventory compatibility feature, current game session and collection generation, coverage-requirement query, sparse accumulator, and all three inventory publication contracts.
- Produces: exception-safe complete-world publication for FastTrack's actual first full update and exactly one single-resource-tag publication for each later update, still inactive until Gate D.

- [ ] **Step 1: Write failing publication-session mode tests**

`FastTrackWorldInventoryPublicationSession` must expose `BeginCompleteWorldUpdate`, `BeginIncrementalResourceTagUpdateRequiringCoverage`, and `BeginIncrementalResourceTagUpdateWithCurrentCoverage`; a boolean coverage/current/full parameter is forbidden. Add exact tests:

- `BeginCompleteWorldUpdate_WhenTwoTagsComplete_ProducesOneCompleteWorldPublication`
- `BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenOneTagCompletes_ProducesResourceTemperatureSeries`
- `BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenSecondTagBegins_ThrowsLifecycleViolation`
- `BeginIncrementalResourceTagUpdateRequiringCoverage_WhenOneTagCompletes_ProducesCoverageAndTemperatureSeries`
- `BeginIncrementalResourceTagUpdateWithCurrentCoverage_WhenOneTagCompletes_ProducesOnlyTemperatureSeries`
- `BeginIncrementalResourceTagUpdateRequiringCoverage_WhenInventoryHasNoTags_ProducesCoverageOnly`
- `AddTemperatureAmount_WhenPrimaryElementIsMissing_IsNotCalledByAdapter`
- `Complete_WhenResourceTagIsStillOpen_ThrowsLifecycleViolation`
- `Discard_AfterException_ReleasesCoverageTagsAndAccumulatorReferences`
- `Begin_WhenGameSessionGenerationChanges_DiscardsRetainedOldSessionState`

The result exposes `FastTrackWorldInventoryPublicationKind` and guarded `TryGet...` accessors for the four valid outcomes: `CompleteWorldAmounts`, `ResourceTagCoverageAndTemperatureSeries`, `ResourceTemperatureSeries`, and `ResourceTagCoverageOnly`. Construction validates that the kind and payloads agree; complete-world and incremental payloads can never coexist. Do not call it `Data`, encode the outcome as nullable-member combinations alone, or use a tuple whose positions could be interchanged.

- [ ] **Step 2: Run the publication-session tests red**

Expected: missing FastTrack publication kind, publication result, and session. Confirm existing Task 8/9 publication/catalog tests remain green before implementation.

- [ ] **Step 3: Implement the minimal session over canonical domain types**

Reuse `CompleteWorldResourceTemperatureAmountsBuilder` for complete mode and `TemperatureAmountAccumulator` for single-tag mode. Coverage mode copies only the supplied dictionary keys through `WorldResourceTagCoverage.Create`; it never visits the key's pickupable set.

The session must contain no alternative constraint, temperature-bucket, or availability implementation. Comments explain why complete and incremental modes are separate and why a present coverage tag without a series remains incomplete.

- [ ] **Step 4: Run the session green and prove incremental isolation**

Assert complete mode's immutable result contains the two fixture tags and incremental mode's result contains exactly its one named resource-tag publication. Mutate an unrelated fixture collection and prove the incremental result does not contain or retain it. Do not add a production diagnostic counter or test branch.

Expected: session tests pass with no complete-world dictionary allocation in the incremental mode.

- [ ] **Step 5: Add failing installed-shape anchor tests**

Use the emitted fixture to exercise reflection/transpiler binding and the `System.Reflection.Metadata` reader to assert the equivalent signatures/IL anchors in the real GitHub DLL without loading or executing it. Both evidence paths must require unique typed anchors for:

1. reading the pre-call `firstUpdate` field;
2. entering each `inventory` key/value pair before `SumTotal`;
3. completing that same tag after `SumTotal` returns;
4. observing a pickupable only after FastTrack's existing world/private-storage filters have passed and immediately before `TotalAmount` contributes; and
5. the method exit/finalizer cleanup path.

Tests must fail on zero or duplicate anchors. Local-variable numbers and `operand.ToString()` are forbidden anchors.

- [ ] **Step 6: Implement the inactive FastTrack adapter**

Bind reflected fields/methods from the immutable compatibility report once. Do not perform `Type.GetType`, `AccessTools`, option lookup, assembly enumeration, or digest calculation from `RunUpdate` or `SumTotal`.

At `RunUpdate` prefix:

- capture the current active session, world ID, collection generation, and the verified pre-call `firstUpdate` value;
- if the active-constraint snapshot has zero enabled constraints, enter no thread context, enumerate no coverage keys, and leave FastTrack completely untouched;
- choose complete mode only when FastTrack will execute its complete branch;
- otherwise call `GetWorldResourceTagCoverageRequirementState` once;
- for `CoverageRequired`, enumerate `WorldInventory.Inventory.Keys` exactly once and call `BeginIncrementalResourceTagUpdateRequiringCoverage` with the copied keys;
- for `CoverageCurrent`, call `BeginIncrementalResourceTagUpdateWithCurrentCoverage` without enumerating keys;
- for `UnknownWorldOrCollectionGeneration`, enter no publication context and emit at most one session-scoped diagnostic; and
- enter a thread-confined nested context tied to game-session generation.

The `SumTotal` hook calls `AddTemperatureAmount` with the cached `PrimaryElement.Temperature` and `TotalAmount` only at FastTrack's already-filtered contribution point. A missing `PrimaryElement` is skipped, matching the characterized Klei status path. No `GetComponent`, `ClusterManager`, world enumeration, constraint lookup, or logging is permitted in that loop.

At successful postfix, publish in this order: coverage, then single-tag series; or the one complete-world publication. If coverage succeeds and a concurrent generation change rejects the series, the tag remains pending, which is safe. A finalizer always discards/restores thread state and returns the original exception. It never publishes a partially accumulated tag.

- [ ] **Step 7: Prove there is no per-update complete-world reconstruction**

Extend fixtures so `RunUpdate` executes one full branch followed by three incremental invocations. Assert publication callbacks receive one complete world and then exactly three single-tag series. Count key enumeration and pickup accumulation:

- coverage keys are enumerated once for a generation that begins after FastTrack's original full update;
- each incremental invocation accumulates only FastTrack's selected tag;
- unrelated tag series are not combined or copied; and
- a second invocation in the same generation does not repeat coverage enumeration.

This is a structural test, not a wall-clock benchmark. Also assert DeliveryTemperatureLimit installs no `BackgroundInventoryUpdater.StartUpdateAll` world-discovery prefix in the normal `Ready` path. FastTrack's own upstream per-update `WorldContainers` scan is outside this mod's scheduling ownership; this rewrite removes the mod's former duplicate setup scan and must not add another.

- [ ] **Step 8: Run focused tests and production build**

Run `FastTrackWorldInventoryPublicationSessionTests`, `FastTrackCompatibilityInspectorTests`, `FastTrackGitHubReleaseAssemblyContractTests`, `WorldResourceTemperatureAmountCatalogTests`, and `HarmonyPatchContractVerifierTests` separately. Then run pipeline `validate`, `build`, and `test`; do not install.

Expected: all pass, and the build contains the inactive patch class without any patch-discovery attribute. Review all worker code against the captured-field rule; if safe access to the cached primary element cannot be proved for the installed ONI/FastTrack contract, classify this feature incompatible. Task 24 must then abort coherent activation when the active feature is required; do not weaken the rule or unpatch FastTrack.

- [ ] **Step 9: Prepare and commit**

Use the seven task paths, allowed type `refactor`, and exact subject:

```text
refactor: Preserve FastTrack incremental inventory updates
```

---

### Task 23: Inactive FastTrack Pickup Grouping and Direct Delivery Eligibility Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/PickupGroupingAdapters/FastTrackPickupTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/DirectDeliveryEligibilityAdapters/FastTrackDirectDeliveryEligibilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPickupTemperaturePatchContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackDirectDeliveryEligibilityPatchContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackPickupGroupingKeyAllocatorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/PickupTemperatureGroupingSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCompatibilityInspectorTests.cs`

**Interfaces:**
- Consumes: a `Ready` pickup-grouping report; an optional `Ready` direct-delivery report only for a loaded same-file-version binary whose former direct replacement is active and fully verified; the canonical grouping session; the collision-free key allocator; and canonical direct constraint checks.
- Produces: collision-free FastTrack grouping with exact lifecycle cleanup and, only for that verified optional former replacement, canonical direct-delivery eligibility; inactive until Gate D. The official GitHub `0.18.4.0` artifact keeps the Klei direct path because its direct replacement is absent. This adapter does not modify, suppress, or unpatch FastTrack.

- [ ] **Step 1: Write failing full-key allocation integration tests**

Exercise the allocator through a pure representation of `PickupTagDict.AddItem` and add exact tests:

- `Allocate_WhenOriginalHashesDifferAndTemperatureClassMatches_ReturnsDifferentKeys`
- `Allocate_WhenOriginalHashMatchesAndTemperatureClassesDiffer_ReturnsDifferentKeys`
- `Allocate_WhenCompositeIdentityRepeats_ReturnsSameKey`
- `Allocate_WhenPrimaryElementIsMissing_UsesDedicatedNonTemperatureClass`
- `Allocate_WhenScopedSnapshotIsStale_UsesExactDecisionBucketClass`
- `Allocate_WhenTemperatureGroupingIsInactive_ReturnsOriginalHashWithoutDictionaryEntry`
- `Allocate_WhenIntegerSpaceIsExhausted_ThrowsWithoutReusingAKey`

Inspect the produced candidate and assert its original `tagBitsHash` remains unchanged. Only the private `PickupTagKey` constructor argument may receive the allocated key.

- [ ] **Step 2: Write failing emitted-IL patch-contract tests**

Require exactly one constructor call matching FastTrack's private `PickupTagKey(int,KPrefabID)` inside `PickupTagDict.AddItem`. The rewritten instruction sequence must alter only the first constructor argument. Add mutations for zero constructor calls, two calls, reversed arguments, changed equality semantics, and a changed `AddItem` signature.

For update lifecycle, require exact prefix/postfix/finalizer hooks around `FetchManagerFastUpdate.BeforeUpdatePickups`. Tests assert nested entry restores the prior thread-confined grouping/allocator sessions after success and exception.

- [ ] **Step 3: Run FastTrack pickup tests red**

Run `FastTrackPickupTemperaturePatchContractTests`, then the two modified domain classes.

Expected: failures identify missing inactive adapters or missing integration entry points, not the already passing allocator core.

- [ ] **Step 4: Implement one capture-once FastTrack pickup update context**

The prefix captures the current game session, active constraint snapshot, parent world resolved from the navigator anchor, and combined fetch eligibility snapshot exactly once. It enters one `PickupTemperatureGroupingSession` and one `FastTrackPickupGroupingKeyAllocator` in thread-confined slots.

For each candidate, form `PickupTagIdentity` from the original tag-bits hash plus verified prefab tag. Resolve applicable requested tags once per identity and cache the result for the update. Read temperature through the verified cached `PrimaryElement` reference only; never call `GetComponent` or query mutable world topology from the worker. If that cached-read safety contract is not verified, mark the active adapter incompatible; Task 24 aborts coordinated activation rather than silently changing implementation paths.

Every candidate passes the complete composite identity to the allocator while grouping is active, including missing-primary-element candidates. The transpiler replaces only the constructor argument; it does not modify the fetchable, candidate, `KPrefabID`, or FastTrack dictionary implementation.

- [ ] **Step 5: Implement exception-safe completion and retained-capacity release**

Postfix completes both sessions. Finalizer discards both and restores any nested prior context while preserving the original exception. After `MaximumRetainedFastTrackGroupingKeyCount` is exceeded, the allocator replaces its variable dictionary before the next session. Test the real named threshold with threshold-plus-one lightweight composite keys and private-reference inspection; do not inject a production test limit.

- [ ] **Step 6: Implement the inactive direct FastTrack chore adapter**

Patch the former exact installed FastTrack chore comparator target only when runtime inspection reports `DirectDeliveryEligibility` as `Ready`. The provenance-pinned GitHub artifact must never select this adapter: its absent replacement is `ReplacementInactive`, which selects the Klei direct-delivery implementation. The optional adapter exists only to preserve correctness if the unproven Workshop-distributed binary has the same file version but still activates the former replacement; emitted-contract tests, not the GitHub fixture, cover that conditional shape.

When selected, preserve an existing false result, resolve the destination through the component index, bypass disabled/missing constraints, preserve characterized missing-primary behavior, and call `DeliveryTemperatureConstraint.Allows` once. No alternative boundary calculation, global snapshot reconstruction, per-call reflection, speculative target discovery, or assembly-presence fallback is permitted.

- [ ] **Step 7: Prove fail-closed ownership and non-interference**

Add exact tests proving:

- adapter binding is attempted only for a `Ready` feature;
- `ModNotLoaded` and `ReplacementInactive` select no FastTrack patch methods;
- an `Incompatible` active pickup or direct-delivery feature yields a release-blocking compatibility result consumed by Task 24;
- installer rollback metadata names only this mod's exact patch methods and Harmony owner; and
- no code calls Harmony unpatch APIs for a FastTrack method or owner.

There is no Klei fallback shim for an active incompatible FastTrack replacement. The coherent activation exception is implemented and tested in Task 24.

- [ ] **Step 8: Run focused tests and production build**

Run both new FastTrack test classes, allocator tests, grouping-session tests, real-DLL contracts, and compatibility-inspector tests separately. The real-DLL suite must continue proving the direct replacement is absent; the emitted suite must prove any optional direct adapter binds only to the exact former active contract. Then run pipeline `validate`, `build`, and `test`; do not install or launch ONI with FastTrack.

Expected: all pass. Verify there is no per-candidate reflection, option lookup, assembly lookup, logging, complete snapshot build, original-hash mutation, or unbounded dictionary retention.

- [ ] **Step 9: Prepare and commit**

Use the seven task paths, allowed type `refactor`, and exact subject:

```text
refactor: Add collision-free FastTrack pickup adapters
```

---

### Task 24: Coordinated Big-Bang Runtime Activation and Legacy Removal

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchGroup.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureGameLoadAuthorityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/FastTrackDeliveryEligibilityCompatibilityException.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/HarmonyTranspilerInfrastructure/HarmonyCodeInstructionFactory.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimit.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/Mod.cs` to `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitMod.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/Buildings.cs` to `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfigurator.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/Construction.cs` to `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/ConstructionMaterialTemperatureLimit.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/Widget.cs` to `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitUserInterface/TemperatureLimitWidget.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/SideScreen.cs` to `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitUserInterface/TemperatureLimitSideScreen.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/Options.cs` to `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitOptions.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Source/Strings.cs` to `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitStrings.cs`
- Delete: every superseded production file named by the single authoritative legacy-removal registry in specification section 6.2; do not duplicate that filename list here or create a second registry in tests.
- Create: `mods/delivery-temperature-limit-supercooled/Tests/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlanTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCoherentActivationContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/IntentionalRuntimeContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/MergedDeliveryTemperatureAssemblyContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/DeliveryTemperatureAssemblyMetadataReader.cs`
- Move: `mods/delivery-temperature-limit-supercooled/Tests/BuildingsEligibilityTests.cs` to `mods/delivery-temperature-limit-supercooled/Tests/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfiguratorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfiguratorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: every Gate A–C module and compatibility report.
- Produces: the first installable build, with exactly one canonical runtime algorithm, selected Klei/FastTrack adapters, intentional public surface, and no obsolete temperature-index subsystem.

- [ ] **Step 1: Write failing implementation-path selection tests**

`DeliveryTemperatureRuntimePatchPlan.Create` takes `checkTemperatureForStatusItems` plus the immutable FastTrack compatibility report. Base-game versus Spaced Out content mode is deliberately not an input because adapter selection is orthogonal to content mode. It does not take path enums, “fallback verified” booleans, or generic `useFastTrack` flags because an active incompatible replacement is never neutralized or unpatched by this mod.

Add exact tests:

- `Create_WhenFastTrackIsNotLoaded_OrdersKleiInventoryPickupAndDirectGroups`
- `Create_WhenFastTrackReplacementsAreInactive_OrdersKleiInventoryPickupAndDirectGroups`
- `Create_WhenFastTrackWorldInventoryIsReady_OrdersFastTrackInventoryGroup`
- `Create_WhenFastTrackPickupGroupingIsReady_OrdersFastTrackPickupGroup`
- `Create_WhenStatusOptionIsDisabled_SelectsNoInventoryOrStatusInstrumentation`
- `Create_WhenStatusOptionIsDisabledAndFastTrackWorldInventoryIsIncompatible_DoesNotBlockAnUnusedStatusFeature`
- `Create_WhenActivePickupFeatureIsIncompatible_ThrowsFastTrackDeliveryEligibilityCompatibilityException`
- `Create_WhenActiveDirectDeliveryFeatureIsIncompatible_ThrowsFastTrackDeliveryEligibilityCompatibilityException`
- `Create_WhenStatusIsEnabledAndWorldInventoryFeatureIsIncompatible_OmitsOnlyStatusIntegrationAndReturnsDiagnostic`
- `Create_WhenDirectDeliveryFeatureIsInactive_OmitsOnlyFastTrackDirectAdapter`
- `Create_WhenFastTrackIsDisabledForLoadedGame_OrdersKleiGroupsWithoutFastTrackAdapterState`

Assert the complete `OrderedPatchGroups` sequence in every case with `Assert.AreSequenceEqual`. The retained order is the Contract Registry enum order after filtering mutually exclusive/disabled groups: lifecycle, topology, authoritative fetch, optional selected inventory, optional status, selected pickup, and selected direct delivery. No separate path enum or boolean mirrors this list.

- [ ] **Step 2: Run selection tests red, then implement the immutable activation plan**

Expected red: missing runtime patch group, runtime-patch-plan, and semantic compatibility-exception types. Implement only the ordered-group selection matrix and rerun green before editing the mod entrypoint.

The runtime patch plan exposes one immutable ordered patch-group list, not individual booleans scattered across the installer. Construction validates impossible combinations, such as selecting either inventory group when the status option is disabled, selecting both implementations of one responsibility, or selecting FastTrack when compatibility is not `Ready`. Critical failure messages name feature, file/assembly version, digest if available, exact failed member/anchor, and the best-efforts `0.18.4.0` support qualification.

- [ ] **Step 3: Write failing curated runtime/serialization contract tests**

Replace whole-assembly equality with `IntentionalRuntimeContract`. Assert that the only intentionally public or nested-public types declared by this assembly are:

```text
DeliveryTemperatureLimit.DeliveryTemperatureLimitMod
DeliveryTemperatureLimit.DeliveryTemperatureLimitOptions
DeliveryTemperatureLimit.TemperatureLimit
STRINGS.TEMPERATURELIMIT
```

Permit the existing semantically accurate `TemperatureLimit` component operations: `MinValue`, `MaxValue`, `IsDisabled`, `LowLimit`, `HighLimit`, `Get(GameObject)`, `CopySettings`, `SetLowLimit`, `SetHighLimit`, `Disable`, and `AllowedByTemperature`. Assert `MinValue == 0` and `MaxValue == OniStorableTemperatureBounds.MaximumTemperatureKelvin == 10000`. Permit only required mod-entrypoint and options members.

Preserve `STRINGS.TEMPERATURELIMIT` plus its existing public static `LocString` fields `LABEL`, `RANGE_SEPARATOR`, `TOOLTIP_RANGE`, `TOOLTIP_NOTSET`, and `SIDESCREEN_TITLE` because their full Klei localization paths are intentional external keys. `Source/DeliveryTemperatureLimitStrings.cs` is the precise responsibility-oriented filename; it must not introduce a parallel `DeliveryTemperatureLimitStrings` facade/type or duplicate the fields under a new key. This retained Klei contract is not a shim. `TemperatureLimitWidget`, `TemperatureLimitSideScreen`, every adapter, every pure domain type, and every compatibility type must be internal.

Assert that `DeliveryTemperatureLimitOptions` retains opt-in JSON serialization, shared `config.json` location semantics, restart requirement, and the exact four public JSON property names/types/defaults: `CheckTemperatureForStatusItems`, `UnderConstructionLimit`, `MaxConstructionTemperature`, and `MinConstructionTemperature`. Verify `DeliveryTemperatureLimitMod` registers that exact type with PLib. The type rename must not rename JSON properties, change the primary assembly/static ID, introduce a second config file, or migrate settings through a shim.

Add metadata assertions that `TemperatureLimit` still contains private `int lowLimit` and `int highLimit` with both `[KSerialization.Serialize]` and `[UnityEngine.SerializeField]`. Assert the absence of nested `TemperatureIndexData` and `getTemperatureIndexData` by exact metadata name.

- [ ] **Step 4: Implement the new `TemperatureLimit` component over the game session**

Preserve serialized fields, constants, player-facing operations, callbacks, and copy-settings behavior. Normalize setters before comparison. If the normalized value is unchanged, return without registry generation change. Otherwise update the component fields and atomically replace the session registration.

`OnSpawn` obtains a `GameSessionTemperatureLimitRegistrationToken` from the current `DeliveryTemperatureGameSession`. `OnCleanUp` removes that exact token idempotently. When session activation was rejected, lifecycle methods retain no registration and safely no-op after the one load diagnostic.

`Get(GameObject gameObject)` first applies Unity's overloaded null check (`gameObject == null`) before calling `GetInstanceID`. It then calls `TemperatureLimitComponentIndex.TryGetRegisteredComponent`, which returns the component and exact registration token from one captured immutable entry. If the indexed component is Unity-destroyed (`component == null` under Unity's overloaded operator), compose the current-session token from that returned registration token and call `RemoveTemperatureLimit`; token ownership guarantees a concurrent replacement cannot be removed. Never perform separate component/token index lookups. Return `null` for a destroyed object/component, no current session, no registration, or a stale removal race. Never return a destroyed Unity object or use `ReferenceEquals` as the Unity-liveness test. No static component dictionary, all-limit list, dirty flag, lazy rebuild, or load-level collection clear is permitted.

Add comments at the serialized fields and lifecycle token explaining save identity and stale-callback rejection. Do not add a compatibility facade for removed index members.

- [ ] **Step 5: Convert UI, construction, and building callers to semantic component operations**

Update each caller to use the new component methods and canonical constraints without recreating clamp/boundary logic. Make patch classes internal. Preserve exact player-facing option/default/copy/UI behavior and existing tests. No file may call a removed global index or create a parallel lookup dictionary.

- [ ] **Step 6: Implement two-phase patch installation**

`DeliveryTemperatureRuntimePatchInstaller` first resolves and verifies every exact target, signature, typed anchor, Harmony owner, and path selection needed by the immutable runtime patch plan. It applies nothing during this verification phase.

Before claiming that a Klei implementation path is authoritative, inspect the actual prefix topology on its target through the already-tested `VerifyKleiAuthority` primitive. An unrelated postfix or non-skipping observer is permitted only when its semantics do not replace the method. An unknown prefix capable of suppressing/replacing `WorldInventory.Update` or `UpdatePickups` makes Klei authority unproved; fail activation for the affected required behavior instead of treating “not FastTrack” as “Klei.” Add installer integration cases that consume the primitive; do not repeat Task 15's primitive unit cases under new names.

Only after all required contracts pass may it apply patch groups. Record every exact `(target, patch method)` installed by this mod. If application throws, remove only methods recorded for this attempt under this mod's own Harmony owner and rethrow the semantic contract violation. Never call broad `UnpatchAll`, never unpatch FastTrack or another owner, and never continue after a partially installed required group.

Always-selected runtime groups are lifecycle, topology, authoritative fetch snapshot, one pickup implementation, and one direct-delivery implementation. Construction/building/UI behavior is activated by replacing its callers/components in this big-bang change rather than by inventing a generic patch group. Status-enabled plans install the shared status hook plus exactly one inventory publication group when that feature is compatible. Status-disabled plans install neither Klei nor FastTrack inventory/status instrumentation and allocate no catalog buffers merely for status.

- [ ] **Step 7: Replace automatic discovery with explicit startup sequencing**

`DeliveryTemperatureLimitMod.OnLoad` retains PLib initialization, localization, and options registration, then installs only groups whose contracts do not depend on the complete loaded-mod topology. `DeliveryTemperatureLimitMod.OnAllModsLoaded` first performs the cold FastTrack presence/enabled/replacement gate, builds a compatibility report only for relevant active features, creates one runtime patch plan, preverifies remaining groups, and installs them once.

Do not call blanket `PatchAll`. Guard duplicate callbacks with an installer state machine that distinguishes `NotStarted`, `Verifying`, `Installed`, and `Failed`; a second successful call is an idempotent no-op, while reentry during verification or after failure throws a diagnostic lifecycle violation.

`DeliveryTemperatureGameLoadAuthorityPatches` belongs in `RuntimePatchInstallation`, not `KleiImplementationAdapters`, because it spans the selected Klei/FastTrack responsibilities. First add an exact installed-game/fixture contract proving `Game.OnLoadLevel()` is the expected instance method with the exact return and parameter types; then implement only `GameOnLoadLevelPrefix(Game __instance)` plus `ResolveGameOnLoadLevelTarget()` exactly as declared in the Contract Registry. The prefix calls only the installer's static `TryStartAuthorizedGameSession(Game game)` operation. No other module may call `DeliveryTemperatureGameSessionHost.EnsureGameSession` for a game load.

Put the executable authority decision in the already-linked pure `DeliveryTemperatureRuntimePatchPlan.VerifySelectedAuthority(IReadOnlyList<ActiveHarmonyPatchDescriptor>)` method. It verifies exactly the groups in `OrderedPatchGroups`, including mutual-exclusion semantics and the exact permitted skipping-prefix owners; success returns normally and a changed selected authority throws `HarmonyPatchContractViolationException` with the affected group/target/owner. Runtime glue may collect Harmony state, convert it into immutable descriptors, and call this method, but it may not reimplement the owner decision.

At every `Game.OnLoadLevel`, and only once for that load identity, `TryStartAuthorizedGameSession` re-reads the active Harmony prefix descriptors and calls `VerifySelectedAuthority` before `DeliveryTemperatureGameSessionHost.EnsureGameSession`. If authority changed, catch only the named contract violation at this game-load boundary, emit the single diagnostic, and return `false`; do not publish a game session, select a fallback, unpatch any owner, or partially reinstall. Leave the already installed Delivery Temperature Limit patch methods in place as guarded no-ops because every gameplay hook must first capture an active session. Do not poll or recheck in any update, pickup, status, comparator, sweep, or direct-delivery path. A repeated prefix callback for the same active load returns the existing authorized session outcome without re-running reflection; a later distinct game load performs one fresh check.

Add emitted-descriptor behavioral tests directly against the linked pure plan for unchanged Klei authority, changed Klei authority, unchanged FastTrack authority, changed FastTrack authority, and unselected-owner changes that must not matter. Add static source/IL contracts against the production installer proving `TryStartAuthorizedGameSession` obtains descriptors, calls `VerifySelectedAuthority`, handles only `HarmonyPatchContractViolationException`, and can call `EnsureGameSession` only on the verified-success branch and only once for a load identity. Separately add the exact `Game.OnLoadLevel` target contract, guarded-no-session hook cases, and a later-load state transition. These are static/reflection-only game-boundary tests plus executable pure-plan tests; no automated ONI launch or copied authority algorithm is allowed.

- [ ] **Step 8: Prove coherent FastTrack failure behavior without fallback shims**

For an incompatible active FastTrack pickup-grouping or direct-delivery replacement, `DeliveryTemperatureRuntimePatchPlan.Create` throws `FastTrackDeliveryEligibilityCompatibilityException` before the installer applies any Delivery Temperature Limit patch group. The exception and one rate-limited diagnostic contain the exact structural failure and best-efforts version qualification. No Klei fallback is selected because FastTrack remains the active replacement.

For an incompatible active FastTrack world-inventory feature when status-temperature accounting is requested, install coherent delivery/pickup behavior, omit both FastTrack/Klei temperature inventory instrumentation and the temperature-aware status replacement, leave ONI's existing status availability unchanged, and emit one status-only diagnostic. Do not combine partial FastTrack deltas with a Klei complete-world candidate, run two inventory enumerations, or claim temperature-aware lacks-resources status is active.

`FastTrackCoherentActivationContractTests` verify both cases and prove the source contains no FastTrack unpatch, guard-forcing, or compatibility-facade code.

- [ ] **Step 9: Delete the obsolete implementation in the same change**

After every caller compiles against the new services, delete every file/member in the authoritative legacy-removal registry in specification section 6.2. Do not restate or fork that registry here. Do not leave forwarding types, type aliases, obsolete wrappers, unused Harmony entry points, commented-out code, or conditional compilation that can restore the old global path.

Run `rg` for exact removed symbols and semantic equivalents. A match in the approved design/plan explaining removal is allowed; a production or test fixture implementation match is not.

- [ ] **Step 10: Complete the approved nullable transition and exact test link rename**

Add `<Nullable>enable</Nullable>` to `Source/DeliveryTemperatureLimit.csproj`. Replace `<Nullable>annotations</Nullable>` with `<Nullable>enable</Nullable>` in `Tests/DeliveryTemperatureLimit.Tests.csproj`. Replace the explicit legacy `..\Source\Buildings.cs` link with the semantically named `..\Source\TemperatureLimitedDeliveryTargets\TemperatureLimitedDeliveryTargetPrefabConfigurator.cs` link and a matching semantic `Production\TemperatureLimitedDeliveryTargets\...` link path.

Resolve every nullable error through precise types, guards, ownership, or lifecycle invariants. Do not use null-forgiving operators without an adjacent proven invariant, broad `#nullable disable`, warning suppression, `LangVersion`, or a test/production framework conditional. Confirm `Source/packages.lock.json`, package versions, and `oni-mod-pipeline.toml` remain unchanged.

- [ ] **Step 11: Run focused activation and build-contract tests**

Run, separately:

- `DeliveryTemperatureRuntimePatchPlanTests`;
- `DeliveryTemperatureGameSessionTests`;
- `TemperatureLimitComponentIndexTests`;
- `TemperatureLimitedDeliveryTargetPrefabConfiguratorTests`;
- `MergedDeliveryTemperatureAssemblyContractTests`;
- both FastTrack contract classes; and
- `HarmonyPatchContractVerifierTests`.

Then run pipeline `validate`, `build`, and `test` separately. This is the first build permitted to be installed later, but do not install it yet. Expected: all focused tests and pipeline gates pass, both projects have nullable enabled, the merged DLL contains only the curated public contract, and no obsolete source is compiled.

- [ ] **Step 12: Review the big-bang boundary before commit**

Inspect every task path and confirm:

- every active patch reads the new session/domain services;
- no build can execute the old and new eligibility models together;
- the status-off selection installs no inventory instrumentation;
- Klei implementation paths contain no FastTrack compatibility work;
- content mode is not used to select implementation path;
- comments document every non-obvious ownership and fail-closed compatibility invariant; and
- all deleted responsibilities have a named new owner.

- [ ] **Step 13: Prepare and commit**

Use every exact created, modified, and moved whole path listed by this task plus every exact deleted production whole path enumerated by specification section 6.2. Expand the registry entries into their concrete paths when preparing the snapshot; the section reference itself is not a valid path argument. Use allowed type `perf` and exact subject:

```text
perf: Activate scoped delivery temperature runtime
```

---

### Task 25: Exhaustive Architecture, Correctness, and Performance-Shape Contracts

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/NoShimArchitectureContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/ImplementationTerminologyContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/PerformanceArchitectureContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackInactivePathArchitectureContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FetchTemperatureEligibility/CanonicalTemperatureEligibilityAgreementTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceWorldResourceTemperatureAmounts.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/MergedDeliveryTemperatureAssemblyContractTests.cs`

**Interfaces:**
- Consumes: the fully activated Gate D implementation.
- Produces: automated proof that removed architecture cannot silently return, all canonical representations agree over every formula-derived bucket (`10,002` for build `744825`), unused buckets add no recurring work, and hot-path structural invariants remain enforced.

- [ ] **Step 1: Write and run the no-shim architecture tests red**

Scan production syntax/metadata, not documentation prose, and consume every exact file/member/type identity from one `RemovedArchitectureIdentities` table declared once inside `NoShimArchitectureContractTests`. That table is the executable transcription of specification section 6.2; all source, metadata, forwarding, alias, wrapper, facade, dense-status-shape, deleted-file, and public-surface assertions iterate it rather than restating subsets in separate tests. A completeness assertion covers each registry category so adding a check cannot silently fork the removal scope.

Expected red should identify any residue left by Task 24. Remove residue in the relevant production owner; do not weaken the test with an allowlist unless the user approves a full shim-exception dossier.

- [ ] **Step 2: Enforce semantic terminology mechanically**

`ImplementationTerminologyContractTests` scans only the stable implementation artifacts it owns: production/test `.cs` file paths, namespaces, declared identifiers, comments, and string literals. It rejects the unqualified ambiguous word prohibited by Global Constraints. It does not read this plan/specification, Git history, generated acceptance evidence, or pipeline configuration; terminology in commit messages and acceptance records is enforced by the commit/final review protocols rather than a brittle test dependency on a document path.

Also reject type identifiers containing `NonVanilla` or beginning with the ambiguous term. The failure reports exact file and line. Do not scan `.git`, documentation, generated artifacts, third-party DLLs, pipeline evidence, or user-owned `screenshot-guidance.md`.

- [ ] **Step 3: Write performance-shape metadata and diagnostic tests**

Add exact tests:

- `ConstraintReadPath_WhenInspected_DoesNotCallSortDistinctOrRegistryRebuild`
- `StatusQueryPath_WhenInspected_DoesNotReferenceClusterManagerOrWorldContainers`
- `KleiInventoryPublication_WhenOneUpdateRuns_EnumeratesEachContributingPickupableOnce`
- `FastTrackIncrementalPublication_WhenOneTagRuns_DoesNotConstructCompleteWorldPublication`
- `FastTrackIncrementalPublication_WhenOneTagRuns_RebuildsOneParentTagAggregate`
- `DirectEligibilityPath_WhenInspected_CallsNoAllocatorReflectionOrSnapshotRebuild`
- `PickupComparator_WhenInspected_CapturesNoNewSnapshotAndCreatesNoCollection`
- `StatusOptionDisabled_WhenActivationPlanIsInspected_ContainsNoInventoryOrStatusPatchGroup`
- `RetainedCollections_WhenHighWaterLimitWasExceeded_ReplaceVariableCapacityStorage`
- `UnusedDecisionBuckets_WhenHotMethodsAreInspected_CauseNoCompleteRangeLoop`
- `TemperatureAmountAccumulator_WhenOneBucketIsObserved_TouchesOnlyThatBucket`
- `KleiImplementationPaths_WhenFastTrackIsAbsent_ReferenceNoFastTrackHotPathMethod`
- `FastTrackFixture_WhenPackageBoundaryIsInspected_IsNeverPackaged`

Use metadata call inspection, source-syntax/control-flow inspection, immutable-reference identity, and semantic output counts. Every metadata/source inspection must name an exact subject method and either (a) compare its direct call targets with a small explicit permitted-call set, or (b) reject a small explicit forbidden-call/reference set whose members are named in the assertion. For control flow, inspect back-edges only in an exact method where the architecture requires none, or permit only the named binary-search/occupied-item loops. Never assert the vague proposition that a directory “has no loops,” allowlist an entire namespace, or use source substring absence as proof when a resolvable call edge is available.

`PerformanceArchitectureContractTests` declares each method contract once as `declaring type + method name + exact parameter types + permitted direct calls + forbidden direct calls/field references + back-edge policy`. Test names above select those declarations. Failure output prints the unexpected resolved member and subject signature. The approved permitted sets must include ordinary guard/argument/collection primitives actually needed by the implementation; an implementation discovery amends the exact set in the same TDD chunk rather than broadening it to a namespace.

Do not add production diagnostic counters, elapsed-time assertions, `GC.GetAllocatedBytesForCurrentThread`, BenchmarkDotNet, profiler hooks, or repeated timing loops.

`FastTrackInactivePathArchitectureContractTests` owns the last two-path separation proofs: for `ModNotLoaded`, disabled-for-active-content, and `ReplacementInactive`, the immutable plan selects Klei directly; the selected Klei patch methods have no call edge to the compatibility inspector, FastTrack publication sessions, coverage state, reflected member handles, or FastTrack key allocator. A single cold call in `OnAllModsLoaded` may establish the startup report; no selected per-update method may branch on that report.

- [ ] **Step 4: Write exhaustive cross-domain equivalence tests**

For every decision bucket from `BelowMinimumKelvinOrdinal` through `AtOrAboveMaximumKelvinOrdinal`, compare direct `DeliveryTemperatureConstraint.Allows`, normalized interval membership, pickup partition classification/equivalence, sparse amount-series queries, and the independent reference model. Include missing-primary-element as a separate classification, not a temperature ordinal.

For representative constraint sets drawn from endpoints `{0, 1, 273, 274, 5000, 5017, 5100, 6203, 9999, 10000}` plus disabled and empty cases, run a linear ordinal pass and prove:

- equal partition classes imply equal eligibility vectors for every applicable destination;
- unequal eligibility vectors never share an optimized class;
- storage union intervals equal direct destination-any evaluation; and
- complete-world and coverage/single-tag inventory modes return equal totals once both are complete.

- [ ] **Step 5: Run deterministic randomized state-machine suites**

Run the fixed seeds from the global test rules and include seed/operation index in failures. The world inventory reference model must exercise at least 10,000 mixed operations and independently enforce coverage/pending semantics. Lifecycle schedules interleave stop, detach, publish, cleanup, reparent, and new-session creation without relying on nondeterministic thread timing.

- [ ] **Step 6: Run the complete mod test project for the first post-activation deep automated pass**

Run:

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
```

Expected: all tests pass with zero failed, skipped, or inconclusive cases. A platform-dependent contract must assert the observed supported platform facts or pass through an explicit non-applicable data case; it must not disappear through a skip. Any failure returns to a focused red-green-refactor correction and its own meaningful commit before rerunning this full command.

- [ ] **Step 7: Run production build and source audits**

Run pipeline `validate`, `build`, and `test`, then `rg` removed-symbol audits, `rg` ambiguous-terminology audits, and `git diff --check` as separate commands. Do not install.

Expected: build succeeds; structural tests and searches agree; no generated build artifact is staged.

- [ ] **Step 8: Prepare and commit**

Use the eight test paths, allowed type `test`, and exact subject:

```text
test: Enforce temperature performance architecture
```

---

### Task 26: Immutable ONI Mod Pipeline Profile and Provenance-Bound Assembly Contract

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/OniModPipelineProfileInvarianceTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/OniModPipelineIntegration/PipelineProvenanceBoundAssemblyLocator.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/MergedDeliveryTemperatureAssemblyContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/DeliveryTemperaturePackageBoundaryContractTests.cs`
- Inspect only: `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`

**Interfaces:**
- Consumes: the Task 0 profile bytes/hash, exact pipeline build-result path, and complete Gate D implementation.
- Produces: a byte-for-byte profile invariant and static proof against either an exact pipeline build or an exact manifest/provenance-bound release candidate. It creates no new pipeline profile requirement.

- [ ] **Step 1: Write the profile-invariance test red before adding artifact lookup**

`OniModPipelineProfileInvarianceTests` reads the repository file as bytes, compares its SHA-256 and byte sequence with Task 0, and parses a second in-memory copy to assert the already-existing semantic declarations:

- source entrypoint `Source/DeliveryTemperatureLimit.csproj` and Release configuration;
- game managed-directory property and merged `DeliveryTemperatureLimit.dll` primary output;
- `PLib` as the only merge input;
- exactly three package mappings: `mod.yaml`, `mod_info.yaml`, and the merged DLL;
- local-install directory `DeliveryTemperatureLimit`;
- required test project ID `delivery-temperature-limit-regressions`; and
- every existing required acceptance-check ID, including the non-publishing Windows Uploader representation check.

The test must distinguish a byte change from a semantic declaration change in its failure. Never update the expected bytes/digest merely to accommodate implementation work. A genuine pipeline-profile requirement is a specification/configuration decision that requires the user's new exact approval.

- [ ] **Step 2: Write the exact provenance-bound assembly locator red**

`PipelineProvenanceBoundAssemblyLocator` exposes four deliberately distinct operations:

1. a build-data-row probe that returns no build row only when `DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH` is absent or whitespace;
2. a required build resolver that rejects absence and every invalid supplied build value with a semantic exception;
3. a release-candidate-data-row probe that returns no candidate row only when `DELIVERY_TEMPERATURE_LIMIT_RELEASE_CANDIDATE_DIRECTORY` is absent or whitespace; and
4. a required release-candidate resolver that rejects absence and every invalid supplied candidate value with a semantic exception.

When the build variable contains any value, the build resolver requires one explicit rooted `build-result.json` path. It rejects a relative path, nonexistent file, wrong filename/schema, symlink or reparse-point escape, path outside the pipeline-diagnosed artifacts root, mismatched mod static ID, mismatched source commit/fingerprint, missing primary output, primary output outside the declared build output, or output length/SHA-256 mismatch.

When the release-candidate variable contains any value, the candidate resolver requires one explicit rooted immutable candidate directory. It binds `workshop-content/DeliveryTemperatureLimit.dll` through `release-evidence/release-content-manifest.json` and `release-evidence/build-provenance.json`, and rejects a relative/nonexistent/wrong-layout path, symlink/reparse escape, wrong static ID/source commit, mismatched release-content digest, primary-output mismatch, or DLL length/SHA-256 mismatch. This is an assembly-binding check, not a substitute for pipeline candidate preparation, installation, acceptance, or `verify-release`.

Both resolvers canonicalize paths segment by segment with .NET 10 APIs; string-prefix containment alone is insufficient. Neither may enumerate artifact directories, sort timestamps, consult “latest,” fall back to the tracked root DLL, or accept a caller-supplied DLL unbound to pipeline provenance.

Extend the assembly/package tests with a dynamic data source that always yields `PublishedBaseline`, yields `ExactPipelineBuild` only after validating the build-result variable, and yields `ExactReleaseCandidate` only after validating the candidate-directory variable. Ordinary pipeline `test` therefore has a complete zero-skipped suite without claiming external artifact evidence. Tasks 26/27 must prove the build row executes; Task 28 must prove the release-candidate row executes.

- [ ] **Step 3: Prove invalid supplied input fails rather than disappearing**

Open one persistent PowerShell session. As one command, assign `DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH` a rooted existing repository Markdown file. As a separate command in that same session, run `MergedDeliveryTemperatureAssemblyContractTests`.

Expected red: the external-data-source discovery reports a semantic invalid-build-result error naming the supplied path. The baseline row may pass; the process must fail overall. Clear the environment variable with a separate command before continuing. A missing error in this exercise is a failed test design.

Unit-test the candidate resolver with a semantically named temporary candidate layout: one valid manifest/provenance-bound DLL case and mutations for path escape, wrong static ID, changed candidate DLL, changed primary-output digest, and undeclared package file. These are data-binding fixtures only; do not fabricate an `awaiting-acceptance` or `ready-for-upload` pipeline state.

- [ ] **Step 4: Build through the authoritative pipeline and retain only its exact result path**

Run pipeline `build` once and copy the exact rooted `build-result.json` path printed by that invocation into the task notes. Do not discover it again from the filesystem, and do not install the build. Confirm the path is under the artifacts root reported by pipeline diagnosis and the result identifies the current committed source plus current working-tree fingerprint according to the pipeline schema.

- [ ] **Step 5: Inspect the exact merged pipeline build through the declared test project**

In one persistent PowerShell session, set `DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH` to the exact Step 4 path as one command. Run `MergedDeliveryTemperatureAssemblyContractTests` and `DeliveryTemperaturePackageBoundaryContractTests` as separate commands. Require the output to name `ExactPipelineBuild`, then require:

- target framework exactly `.NETStandard,Version=v2.1` for `ExactPipelineBuild` and the characterized legacy target only for `PublishedBaseline`;
- current intentional public/serialized surface, including private serialized `lowLimit`/`highLimit`, and exact absence of `TemperatureIndexData`/`getTemperatureIndexData`;
- no direct `System.IO.Compression` or `System.Net.Http` assembly reference;
- no legacy global-index, dense-band, old status, or old patch implementation type;
- expected PLib merge presence according to the existing ILRepack contract, with no separate PLib package file;
- no FastTrack compile reference, fixture bytes, fixture digest, or fixture resource in the merged DLL;
- package declarations and build-result outputs consistent with exactly `mod.yaml`, `mod_info.yaml`, and merged `DeliveryTemperatureLimit.dll`; and
- no framework DLL, test assembly, fixture, `.config`, `.pdb`, sidecar, or undeclared file in the package inventory.

Clear the variable as a separate command. Do not retain it for ordinary pipeline testing, where it could accidentally bind later tests to stale evidence.

- [ ] **Step 6: Run mandatory gates on the unchanged task snapshot**

Run pipeline `validate`, `build`, and `test` separately. The ordinary pipeline suite must have no failed, skipped, or inconclusive tests; it executes the baseline row and all static/source contracts but makes no candidate claim. Hash `oni-mod-pipeline.toml` again and compare bytes and SHA-256 with Task 0. Inspect `git diff --name-only`; the profile must not appear.

- [ ] **Step 7: Prepare and commit**

Use the four modified/created test paths only, allowed type `test`, and exact subject:

```text
test: Verify pipeline-built delivery temperature artifacts
```

---

### Task 27: Final Automated Pipeline Gate

**Files:**
- Inspect only; do not modify source, configuration, tests, candidate artifacts, or installations unless a failure begins a new focused TDD correction chunk.

**Interfaces:**
- Consumes: a clean committed Gate D implementation, Task 26 artifact contracts, and the unchanged production pipeline profile.
- Produces: fresh authoritative pipeline evidence plus an exact statically inspected build artifact suitable for release-candidate preparation.

- [ ] **Step 1: Verify committed source and preserve unrelated work**

Run `git status --short`. Every contributing implementation/configuration/test change must already be committed; user-owned unrelated paths may remain untracked only if the pipeline's relevant-source rules permit them. If candidate preparation would reject a contributing untracked file, resolve ownership with the user rather than deleting or committing it implicitly.

- [ ] **Step 2: Run locked restore and the full direct suite as supplemental developer evidence**

Run as separate commands:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --locked-mode
```

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
```

Expected: restore makes no lockfile change; every test passes with zero skipped/inconclusive cases. Record total/passed/failed/skipped counts from fresh output. This command is useful developer evidence, but it does not replace the pipeline-declared `test` operation below.

- [ ] **Step 3: Run repository-local environment diagnosis**

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- diagnose --mod mods/delivery-temperature-limit-supercooled
```

Expected: the intended game, managed-assembly, user-data, Dev/Local, SDK, and artifact paths resolve. Do not alter configuration to hide a diagnostic.

- [ ] **Step 4: Validate, build, and test through the authoritative pipeline**

Run each command separately in this exact order:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- validate --mod mods/delivery-temperature-limit-supercooled
```

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- build --mod mods/delivery-temperature-limit-supercooled
```

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Expected: all commands succeed. Copy the exact rooted `build-result.json` printed by this specific `build` invocation and the exact automated-test evidence directory printed by the pipeline. Never substitute a path selected by timestamp, directory enumeration, another build, or the tracked source-root DLL. Hash `oni-mod-pipeline.toml` and require the Task 0 bytes/digest.

- [ ] **Step 5: Bind candidate-specific static tests to that exact build result**

In one persistent PowerShell session, assign `DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH` the exact Step 4 path as one command. Run `MergedDeliveryTemperatureAssemblyContractTests` and `DeliveryTemperaturePackageBoundaryContractTests` as separate commands. Confirm each relevant output contains the named `ExactPipelineBuild` row and that every assertion passes. Clear the variable as a separate command.

Review the fresh production build output for the two characterized MSB3277 conflict roots only:

- `System.IO.Compression` `4.1.3.0` versus ONI `4.2.0.0`; and
- `System.Net.Http` `4.1.2.0` versus ONI `4.2.0.0`.

A missing root, changed version, third root, suppressed warning, or direct reference workaround is a contract failure, not an opportunity to relax the test.

- [ ] **Step 6: Treat any failure as a new TDD correction, not a pipeline workaround**

For a failure, identify the smallest owning behavior, add or refine a focused failing test, implement the correction, run focused and complete tests, prepare a meaningful commit with exact snapshot/message authorization, and restart Task 26 at its mandatory-gate step before rerunning this entire task. Do not edit generated evidence, relax acceptance, change the profile, suppress warnings, or bypass locked restore.

There is no commit for a successful Task 27 because it creates evidence only.

---

### Task 28: Final Exact-Candidate Static and Four-Run Manual Validation

**Files and external evidence:**
- Read: `docs/guides/preparing-oni-mod-releases.md`
- Read: the candidate's generated build provenance, content manifest, acceptance plan, release summary, and uploader checklist.
- Read: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/README.md` and its statically inspected DLL only; do not load it into ONI.
- Inspect: the exact published-baseline package, exact release candidate, four derivative save files, and `Player.log` captured after each game session.
- Optional write: `docs/performance-results/<release-version>-delivery-temperature-limit-indicative-comparison.md` only if the user wants a durable summary. Its absence is explicitly not a publication blocker.
- Write candidate lifecycle files only through ONI Mod Pipeline. Never hand-edit immutable candidate content or evidence.

**Interfaces:**
- Consumes: fresh Task 27 evidence, the published baseline identity, two user-selected late-game colonies, and clean committed release inputs.
- Produces: an exact static baseline/candidate comparison, exactly four one-pass Klei-path manual game sessions, truthful pipeline acceptance evidence, and a deterministic release-readiness result. It does not test FastTrack in game, upload, publish, or push.

- [ ] **Step 1: Verify the comparison control before preparing or installing anything**

Use only the already-published Delivery Temperature Limit package as the runtime baseline. Its `DeliveryTemperatureLimit.dll` must match every recorded fact:

```text
source commit: 5f7bf43aa823bbb4771936b058c6d573484b6d91
file version: 2026.8.26.0
SHA-256: 02A14F2E123F42BDD87847C15AB434DAFC8A4D4BC92B465F9DCD367364BF465E
```

Prefer the installed official Steam Workshop copy because the user confirmed this version was published. Verify its `mod.yaml` static ID and DLL digest before launch. If the Workshop copy is unavailable, differs, or cannot be identified unambiguously, stop and ask the user how to obtain the published bytes; do not rebuild the baseline from source, substitute a later DLL, or silently assemble a new control package.

Record the baseline package directory, version, DLL length/hash, ONI changelist, and enabled content pack for each colony. FastTrack and every mod other than this exact baseline must be disabled. The baseline comparison is a control exercise; it does not bypass the pipeline requirement for the new implementation.

- [ ] **Step 2: Create four derivative saves from two untouched originals**

Exit ONI before copying. Preserve the user's original late-game base-game content-mode and Spaced Out content-mode saves byte-for-byte. From each original load point, create two separately named derivative copies before either is opened:

```text
BaseGameContentMode_PublishedBaseline
BaseGameContentMode_ReleaseCandidate
SpacedOutContentMode_PublishedBaseline
SpacedOutContentMode_ReleaseCandidate
```

Names may include the colony name, but they must retain the explicit content-mode and package-role meaning. Record each original and derivative path, byte length, and SHA-256 so both members of a pair are demonstrably based on the same starting save. Do not rely on Steam Cloud to create the comparison copies. Never overwrite or save into an original; allow autosaves only in the corresponding derivative's own history.

Before the first game session, write one short scenario sheet used unchanged for all four sessions:

- selected simulation speed;
- camera location and representative busy-colony workload;
- a fixed 60-second settling interval after the colony becomes interactive;
- one fixed 120-second observation interval measured once, with start/end displayed colony-cycle time recorded;
- one existing or deliberately created in-range and out-of-range delivery case;
- the same temperature-limit settings and relevant gameplay options; and
- the UI/status/error observations listed below.

Do not add a profiler, instrumentation mod, benchmark mod, scripted input, automated game control, repeated sample, or timing threshold. If an exact scenario is unavailable in one content mode, record the semantically equivalent scenario selected before either package is tested in that content mode.

- [ ] **Step 3: Confirm release inputs, then prepare one immutable release candidate through the real pipeline**

Follow the release guide's input review. This performance rewrite does not itself authorize an inferred version bump, change-note rewrite, preview change, or listing change. If the intended release version or release notes are not already explicitly approved and committed, pause for the user's exact approval of those specific release-input changes before running release preparation. The release is a normal release, not a beta.

Run as separate commands:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- validate --mod mods/delivery-temperature-limit-supercooled --for-release
```

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- prepare-release --mod mods/delivery-temperature-limit-supercooled
```

Expected: relevant inputs are clean and committed; preparation reruns locked build/tests and prints one exact candidate directory, release-content digest, and `awaiting-acceptance` state. Record them verbatim. Inspect the generated manifest, provenance, acceptance plan, summary, and uploader checklist. Require the intended commit, current ONI support metadata, release version, target framework, package inventory, automated-test success, and exactly unchanged profile semantics. Never modify or reuse the candidate after preparation.

- [ ] **Step 4: Statically bind the prepared candidate to Task 27 and compare it with the baseline**

Before launching ONI, validate the prepared candidate through the pipeline and its evidence. Require:

- `workshop-content/DeliveryTemperatureLimit.dll` matches both the release-content manifest and `BuildProvenance.PrimaryOutput` by byte length and SHA-256;
- repository commit and every relevant build-input/game-reference digest match the intended clean commit and current ONI installation;
- the package inventory is exactly `mod.yaml`, `mod_info.yaml`, and `DeliveryTemperatureLimit.dll`; and
- no test fixture, FastTrack DLL, framework DLL, symbols, application configuration, sidecar, or ownership marker is inside `workshop-content`.

In one persistent PowerShell session, assign `DELIVERY_TEMPERATURE_LIMIT_RELEASE_CANDIDATE_DIRECTORY` the exact Step 3 candidate directory as one command. Run `MergedDeliveryTemperatureAssemblyContractTests` and `DeliveryTemperaturePackageBoundaryContractTests` separately. Require the named `ExactReleaseCandidate` row and all candidate-specific assertions to pass, then clear the variable as a separate command. This inspects the actual packaged bytes even if ILRepack gives separate builds different MVIDs or hashes; never require byte identity with Task 27 merely because both derive from the same source.

Create a concise static comparison in task notes—not necessarily a repository file—covering baseline and exact release-candidate DLL hash, length, file/assembly version, target-framework metadata, direct assembly references, declared public types/members, and presence/absence of the legacy global temperature-index/status/patch types. Reuse the metadata reader and provenance-bound test output; do not write an ad-hoc parser or execute either assembly.

Also attach the fresh static-contract conclusions:

- current ONI changelist `744825`, `Sim.MaxTemperature == 10000`, inclusive game validity, and the high-exclusive mod rule;
- formula-derived `10,002` decision buckets and `10,001` endpoint counters;
- fixed-memory increase only, with no ordinary work for untouched upper buckets and no full-range recurring scan;
- absence of direct `System.IO.Compression`/`System.Net.Http` dependencies despite the two bounded resolution warnings; and
- FastTrack best-efforts support only for the GitHub release artifact with file version `0.18.4.0`, explicitly not proven byte-identical to the Steam Workshop-distributed DLL.

No failure to observe a dramatic file-size reduction is meaningful; this check proves structure and artifact identity, not a performance threshold.

- [ ] **Step 5: Run the two published-baseline sessions exactly once**

With ONI closed, ensure no Local/Dev candidate copy exists or is enabled. Enable only the verified published baseline. Start ONI and confirm the enabled-mod screen identifies only Delivery Temperature Limit plus the game/content pack itself.

Run exactly once, in this order:

1. `BaseGameContentMode_PublishedBaseline`; and
2. `SpacedOutContentMode_PublishedBaseline`.

For each derivative, follow the unchanged scenario sheet. After the settling interval, record the displayed start/end colony-cycle time across the single observation interval and concise factual observations about whether the chosen simulation speed remained usable, delivery errands responded, the side screen/status remained responsive, and visible stalls occurred. Exercise one in-range control and one out-of-range rejection without trying to exhaust every boundary. Exit ONI after each session and preserve a separately named copy of that session's `Player.log` before the next launch overwrites it.

Do not repeat a baseline session to reduce noise, improve a result, or obtain an average. An interruption that invalidates a session invalidates the campaign; it is not permission to select the more favorable observation.

- [ ] **Step 6: Make the candidate the only enabled copy and install it exactly once**

Exit ONI. Follow the release guide's duplicate-copy checklist. Disable the subscribed Workshop baseline and any competing Dev/Local copy; the pipeline never changes subscriptions or enabled-mod state. If the intended Local destination is unowned or hand-maintained, move it aside manually and record what was moved rather than asking the pipeline to adopt or erase it.

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- install --candidate <exact-candidate-directory> --target local
```

Replace the token with the exact Step 3 directory. Expected: the pipeline re-verifies the candidate, creates one ownership-guarded Local installation and one write-once installation receipt, and proves every installed runtime hash matches the candidate. Never reinstall, edit, repair, or replace bytes within this candidate or its managed installation.

- [ ] **Step 7: Run the two release-candidate sessions exactly once and perform declared gameplay acceptance**

Start ONI and confirm only the exact Local release candidate is enabled. FastTrack and every other mod remain disabled, so these sessions exercise the Klei inventory update and Klei pickup grouping paths in both content modes.

Run exactly once, in this order:

1. `BaseGameContentMode_ReleaseCandidate`; and
2. `SpacedOutContentMode_ReleaseCandidate`.

Use the same scenario sheet and one observation interval per derivative. Record the same colony-cycle and responsiveness observations as the corresponding baseline. In the candidate sessions, also complete every applicable game-based acceptance action declared by the immutable pipeline plan:

- bounded Storage Bin rejects the out-of-range delivery while an in-range control remains deliverable;
- construction-material option blocks and then restores the declared control behavior;
- side-screen high-first editing, ordinary editing, Del-to-clear, and keyboard/camera focus behave normally;
- a configured limit survives the required save/main-menu/reload sequence within the derivative save; and
- in Spaced Out content mode, the rocket-interior Storage Tile check exercises both out-of-range rejection and in-range control.

The save/load action is part of the one candidate content-mode session, not a second performance sample; do not rerun the 120-second observation interval after reload. Where a declared acceptance scenario is structurally inapplicable to base-game content mode, execute it in the Spaced Out session and state the exact reason rather than fabricating a base-game substitute.

After each session, exit ONI and preserve that session's `Player.log` under an unambiguous content-mode/package-role name. Review it for DeliveryTemperatureLimit initialization, Harmony contract/lifecycle messages, repeated diagnostics, Unity exceptions, and unhandled exceptions. FastTrack messages are neither expected nor evidence because FastTrack is disabled.

- [ ] **Step 8: Interpret the indicative comparison without overstating it**

Compare each candidate observation only with its same-content-mode baseline. Report the two start/end colony-cycle deltas and concise visible responsiveness notes. These four one-pass sessions are intentionally indicative and non-statistical:

- no CPU attribution, allocation count, garbage-collection count, lock profile, benchmark confidence interval, or universal speedup percentage may be claimed;
- an inconclusive or noisy visible delta is not a release failure when structural tests pass and there is no obvious regression;
- absence of an optional Markdown performance report is not a release or publication blocker; and
- a reproducible correctness failure, crash, relevant exception, warning storm, or obvious candidate-only slowdown is a real failure and must not be waived as noise.

The defensible performance conclusion comes from static/TDD proof that avoidable scaling work was removed, supplemented—not established—by these colony observations.

- [ ] **Step 9: Complete the remaining immutable pipeline acceptance check without publishing**

Complete the candidate's required Windows Uploader representation check exactly as the release guide specifies: open the generated description in current Windows Notepad, open the authenticated ONI Uploader Edit Mod form, leave every update checkbox disabled, paste into Description, verify line structure, record the Notepad/Uploader versions, and cancel. Do not select Publish, Update Data, or any update checkbox. This is a representation check required by the existing unchanged pipeline profile, not publication authorization.

Confirm every immutable acceptance-plan check now has a truthful observation. Baseline performance notes are supplemental and must not be presented as candidate acceptance evidence.

- [ ] **Step 10: Record acceptance once and verify release readiness**

Only after every required acceptance check was genuinely executed, run in an interactive terminal:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- record-acceptance --candidate <exact-candidate-directory> --tester <tester-display-name>
```

Enter factual `passed` or `failed` results and concise notes; do not pre-author, infer, or overwrite answers. Then run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- verify-release --candidate <exact-candidate-directory>
```

Expected for a successful candidate: `ready-for-upload`, with exact summary/checklist/report paths. “Ready for upload” is a pipeline lifecycle state, not authorization to upload or publish.

- [ ] **Step 11: Handle failures immutably and without selective repetition**

If any required check fails, record the truthful failed result if acceptance recording has begun and preserve the candidate, receipt, saves, logs, and evidence. Add or refine the smallest failing automated test, correct tracked source through TDD, run all commit gates, obtain exact commit authorization, and prepare a new candidate with a new run ID. Never edit/delete acceptance evidence, reinstall a candidate with a receipt, replace candidate bytes, or change a failed answer.

Do not repeat a run on the same candidate to seek a better result. A corrected candidate starts a new four-session campaign from fresh derivatives of the original saves; the prior failed campaign remains evidence and is not averaged into the new one.

There is no source commit for a successful Task 28 because candidate and acceptance evidence are generated and immutable. There is no upload, publication, or Git push in this task.

---

### Task 29: Final Evidence Review and Handoff

**Files:**
- Inspect only. Do not modify committed source or immutable candidate evidence.

**Interfaces:**
- Consumes: fresh Task 27 automated/static output and Task 28 verified candidate/manual evidence.
- Produces: a precise completion report with no unsupported performance claim.

- [ ] **Step 1: Load and follow verification-before-completion instructions**

Read `C:\Users\maksy\.agents\skills\verification-before-completion\SKILL.md` completely. Apply it to every claim about tests, build, compatibility, performance, or release readiness.

- [ ] **Step 2: Re-read the approved specification acceptance criteria**

Map every numbered criterion to fresh automated output, an immutable acceptance result, static source/metadata inspection, one of the four approved manual sessions, or exact candidate verification. A criterion without evidence is incomplete; do not infer it from a neighboring check or substitute a performance impression for a correctness proof.

- [ ] **Step 3: Verify repository and candidate identity one final time**

Run `git status --short` and `verify-release` again as separate commands. Confirm unrelated user-owned paths remain untouched, every contributing change is committed, the installed receipt still matches the exact candidate, and no immutable evidence/candidate byte changed after acceptance. Confirm `oni-mod-pipeline.toml` still matches the Task 0 bytes/hash.

- [ ] **Step 4: Report outcome, evidence, and residual limits**

Lead with pass/fail. Include:

- exact candidate path and digest;
- commit IDs for every meaningful implementation chunk;
- automated test totals and pipeline evidence paths;
- exact baseline identity and all four content-mode/package-role session results;
- the two simple same-content-mode cycle-progress/responsiveness comparisons, expressly labelled indicative and non-statistical;
- current ONI build/reference identity and the exact `netstandard2.1` game-loaded target;
- FastTrack fixture provenance, version/digest, per-feature static contract result, and the qualification that the actual Steam Workshop DLL was unavailable and no FastTrack game run occurred;
- confirmation that no unqualified ambiguous terminology or shim remains;
- confirmation that the Klei inventory update path pays no FastTrack delta/coverage overhead;
- confirmation that the FastTrack inventory update path does not reconstruct complete worlds for steady-state tag updates; and
- confirmation that untouched decision buckets above observed material temperatures add fixed memory only, not ordinary recurring work;
- exact bounded array sizes (`10,001` endpoints and `10,002` decision buckets for ONI changelist `744825`) plus the prohibition/proof against complete-range hot-path scans;
- any unavoidable ONI input-proportional cost or inapplicable scenario with its exact reason; and
- the pipeline lifecycle state, while making clear that upload/publication/push remain separately authorized actions.

Do not say “as fast as possible,” “proven faster in every colony,” or quote a universal percentage. The defensible completion claim is that the approved avoidable scaling mechanisms were removed or bounded by static structure and exhaustive TDD; runtime work is proportional to actual constraints, touched buckets, relevant worlds/tags, and queried pickupables rather than the unused configured range; the two one-pass late-game comparisons revealed no named candidate-only regression (if that is what was actually observed); and no further low-risk mitigation was identified within preserved behavior and verified patch contracts.

If the optional concise Markdown performance result was not created, state that the observations are captured in the final handoff and immutable pipeline acceptance evidence where applicable; do not downgrade an otherwise ready candidate for that absence.

There is no commit, push, upload, or publication in Task 29.
