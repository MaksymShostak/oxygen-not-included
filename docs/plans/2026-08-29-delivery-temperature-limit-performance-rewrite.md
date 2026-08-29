# Delivery Temperature Limit Large-Colony Performance Rewrite Implementation Plan

> **For agentic workers:** REQUIRED EXECUTION MODE: Implement this plan inline with one agent, task by task. **Do not spawn subagents, delegate work, or use parallel agent execution.** Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mod's global temperature-band subsystem with scoped immutable constraints, sparse world inventory, tag/world-specific fetch eligibility, collision-free FastTrack grouping, and game-session-safe publication so very large ONI colonies pay only for distinctions that can affect an actual delivery decision.

**Architecture:** Pure domain modules define canonical Kelvin semantics, constraint registration, sparse amount series, normalized storage intervals, scoped pickup partitions, content-neutral authoritative world topology, and generation-validated snapshots. Thin Harmony adapters preserve Klei complete-world inventory enumeration or FastTrack complete-first/one-tag-incremental enumeration and publish through one `DeliveryTemperatureGameSession`; base-game versus Spaced Out content mode is an independent topology axis, not an adapter selector. A single coordinated activation removes the old global model instead of bridging it. Focused TDD and signed commits occur throughout, while the expensive installed-game/pipeline/profiler campaign runs only after every rewrite component is integrated.

**Tech Stack:** SDK-style C# targeting .NET Framework 4.8 for the mod; C# `net10.0` MSTest.Sdk 4.3.3 test project with nullable annotations and warnings as errors; Harmony; PLib 4.24.0; installed ONI managed assemblies; optional FastTrack adapter; repository-local .NET 10 ONI Mod Pipeline; Git commit workflow with configured signing.

**Spec:** `docs/specs/2026-08-29-delivery-temperature-limit-performance-rewrite-design.md`

## Global Constraints

- Implement the approved specification exactly. If source or installed binary evidence contradicts it, stop and amend the specification and plan with the user; do not improvise a materially different architecture.
- Strictly do not spawn subagents. This applies to implementation, review, research, testing, profiling, and remediation.
- Deliver one big-bang runtime migration. New modules may be developed and tested before activation, but no build intended for players may execute old and new temperature-eligibility algorithms in parallel.
- Run focused red-green-refactor cycles throughout. Only the expensive whole-pipeline, installed-game, four-way base-game/Spaced-Out and Klei/FastTrack, large-colony, profiler, allocation, GC, save/load, and lifecycle campaign is deferred until every fix is integrated.
- Commit after every meaningful complete chunk. A meaningful chunk has a deliberate failing test, complete passing behavior, a buildable affected source set, correct names, durable comments, no temporary diagnostics, no disabled assertions, no unresolved placeholder comment, no half-migrated caller, and no unapproved shim.
- Use the signed commit workflow in `C:\Users\maksy\.agents\skills\committing-to-git`; do not substitute raw `git add` or `git commit`. Obtain exact approval for the prepared snapshot and exact commit message immediately before every commit. Do not push without separate explicit authorization.
- Preserve unrelated user-owned changes. At plan-writing time these include untracked `AGENTS.md` and `mods/delivery-temperature-limit-supercooled/screenshot-guidance.md`; re-check rather than assuming that list remains complete.
- Never create, edit, rename, or delete configuration without explicit approval for the exact file and setting in the Configuration Approval Dossier. Plan approval is not configuration approval.
- Make no package or lockfile change. The design requires no new dependency. If implementation appears to need one, stop and present the exact package, version, transitive/pipeline impact, and package-free alternative.
- Keep the production mod on `net48`. Do not retarget it to the test project's `net10.0`.
- Preserve serialized type `DeliveryTemperatureLimit.TemperatureLimit` and private serialized integer fields `lowLimit` and `highLimit` with `[KSerialization.Serialize]` and `[UnityEngine.SerializeField]`.
- Preserve option names, JSON properties, defaults, construction behavior, copy-settings behavior, inclusive-low/exclusive-high boundaries, enabled-but-empty semantics, and `(int)temperatureKelvin` truncation toward zero.
- No shims by default. A legacy type, member, alias, wrapper, fallback implementation, or parallel subsystem requires a named reproducible consumer, precise legacy semantics, no clean migration, focused tests, owner/removal criteria, and explicit user approval for that exact exception.
- Specifically remove `TemperatureLimit.TemperatureIndexData`, `TemperatureLimit.getTemperatureIndexData()`, `allLimits`, `limitsDirty`, lazy index rebuilding, dense storage band sets, dense status `(Tag, index)` dictionaries, and FastTrack hash mixing.
- Keep all new domain and patch types `internal`. Public visibility is allowed only for the curated Unity/Klei/PLib entry points enumerated in Task 23.
- Use semantic names from the contract registry. Do not introduce `Helper`, `Utils`, `Common`, `Misc`, bare `Data`, or generic `Manager` names.
- Never use the unqualified word “vanilla.” Use exactly `base-game content mode`, `Spaced Out content mode`, `Klei inventory update path`, `FastTrack inventory update path`, `Klei pickup grouping path`, or `FastTrack pickup grouping path`; use `Klei implementation paths` or `FastTrack implementation paths` only when deliberately referring to several corresponding paths together. Content mode and implementation path are independent axes; names such as `VanillaInventoryAdapter` and `NonVanillaAdapter` are forbidden.
- Add comments for conversion semantics, eligibility invariants, lock/snapshot ownership, generation validation, Harmony anchors, fallback correctness, FastTrack key allocation, and high-water retention. Do not comment obvious syntax.
- Never hold more than one domain-service lock at a time. Never call Unity, Klei, PLib, Harmony, FastTrack, logging, sorting, another domain service, or large allocation code while a domain lock is held.
- Worker-capable code may read captured immutable snapshots, thread-confined state, and only the exact pickup candidate/cached-primary fields whose managed access and cross-thread stability were verified before activation. It must not perform `GetComponent`, enumerate `ClusterManager`/`WorldContainer`, query mutable game topology, or call unverified Unity APIs; a failed proof selects the specified fallback instead of weakening this rule.
- A missing/stale pickup partition uses exact temperature-decision classes. Incomplete status inventory leaves ONI's existing availability unchanged. Missing/stale sweep eligibility returns conservative `false` after preserving an original `false` result.
- Publish combined fetch state only when game-session generation, constraint generation, fetch topology version, and world-parent topology version all match the values captured at build start.
- Rate-limit diagnostics by game-session generation and diagnostic key. Never emit per-pickup or per-status-item warning storms.
- Run commands individually. Do not chain commands with `;`, `&&`, `||`, pipes, background operators, or command substitution.
- Use `rg` and `rg --files` for repository searches.
- Use `apply_patch` for targeted file edits. Never use Git restoration commands to undo working-tree changes.
- Do not claim completion until the final verification task has fresh command output and the complete deep campaign has passed against the exact installed candidate.

---

## Delivery Shape and Review Gates

The work is one release-level migration with five review gates. Only Gate D activates the rewrite in the mod entrypoint; Gates A–C add complete tested modules that remain uninstalled and therefore cannot create a mixed runtime.

| Gate | Independently reviewable result | Player runtime path |
|---|---|---|
| A — Canonical state | Kelvin semantics, constraint registry, owned component index, and game-session lifetime | Existing path remains active |
| B — Sparse/scoped domain | World topology, sparse inventory, intervals, partitions, combined fetch snapshots, exhaustive reference-model tests | Existing path remains active |
| C — Verified adapters | Thin Klei/FastTrack implementation-path and lifecycle Harmony adapters compile and their pure contracts are tested, but installer does not invoke them | Existing path remains active |
| D — Coordinated activation | `TemperatureLimit` and `Mod` switch once; obsolete patch/status/index files are deleted in the same chunk | New path only |
| E — Final evidence | Required pipeline profile, complete automated suite, exact candidate install, gameplay matrix, profiler/GC review, acceptance evidence | New path only, release-eligible after pass |

Do not install a Gate A–C build into ONI. Those commits are code-review checkpoints, not partially migrated releases.

## Configuration Approval Dossier

Before Task 1, request explicit approval for the following exact configuration changes. If the file contents or intended values differ when implementation starts, show the delta and obtain renewed approval.

### `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`

Add this item to the existing `<ItemGroup>`; retain the existing explicit `Buildings.cs` link and every current property/package setting:

```xml
<Compile Include="..\Source\Domain\**\*.cs"
         Link="Production\Domain\%(RecursiveDir)%(Filename)%(Extension)" />
<Compile Include="..\Source\Patching\PatchContractViolationException.cs"
         Link="Production\Patching\PatchContractViolationException.cs" />
<Compile Include="..\Source\Patching\PatchContractVerifier.cs"
         Link="Production\Patching\PatchContractVerifier.cs" />
<Compile Include="..\Source\FastTrack\FastTrackFeatureCompatibilityState.cs"
         Link="Production\FastTrack\FastTrackFeatureCompatibilityState.cs" />
<Compile Include="..\Source\FastTrack\FastTrackCompatibilityReport.cs"
         Link="Production\FastTrack\FastTrackCompatibilityReport.cs" />
<Compile Include="..\Source\FastTrack\FastTrackCompatibilityInspector.cs"
         Link="Production\FastTrack\FastTrackCompatibilityInspector.cs" />
```

Impact: the already-required pipeline test project compiles the exact pure-domain production sources plus the two reflection-only patch-contract files and three reflection-only FastTrack compatibility files so internal algorithms/contracts are tested without a game process. The linked patch-contract and FastTrack files must not reference Harmony, Unity, Klei, PLib, or FastTrack compile-time types. No package, target framework, lockfile, warning, nullable, or pipeline test-project change is involved.

### `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`

Append exactly these required acceptance checks in Task 25; do not alter existing build, package, listing, installation, test-project, or acceptance settings:

```toml
[[acceptance-checks]]
id = "base-game-klei-path-temperature-performance"
title = "Base-game content mode remains responsive on Klei inventory and pickup paths"
required = true
setup = "Disable the Spaced Out DLC, ensure the FastTrack inventory and pickup replacements are not active, use the designated large-colony save, enable status temperature accounting, record active destination, distinct endpoint, pickupable, requested-tag, and authoritative world counts, and configure diverse nonempty, empty, disabled, underflow-adjacent, and 5000 K-boundary limits."
action = "Run the fixed profiling interval at the same simulation speed, edit representative limits while fetches are active, and exercise storage, sweeping, construction, and lacks-resources status paths."
expected = "Delivery decisions and statuses remain correct; the Klei inventory update path publishes one complete world candidate without a second pickupable scan; stale windows use the specified conservative behavior; no old global-index rebuild or dense world-tag-band work appears; mod-attributed hot paths and allocations satisfy the recorded final performance review."

[[acceptance-checks]]
id = "base-game-fasttrack-path-temperature-performance"
title = "Base-game content mode preserves FastTrack incremental inventory performance"
required = true
setup = "Disable the Spaced Out DLC, enable the verified FastTrack inventory and pickup replacements, repeat the designated large-colony topology and temperature-limit distribution, and record the FastTrack version, assembly identity, options, Harmony ownership, and SHA-256 digest."
action = "Run the fixed profiling interval with FastTrack pickup updates, background world inventory, status replacement, active constraint edits, and multi-tag pickupables."
expected = "FastTrack and corresponding Klei implementation-path reference decisions are equivalent; distinct composite pickup classes never collapse; FastTrack's initial full update may publish one complete world candidate, each later update publishes exactly one selected resource-tag series, unrelated tags are not rebuilt, coverage distinguishes absent from pending tags, and no relevant exception, warning storm, unbounded retained collection, or temperature-unaware replacement occurs."

[[acceptance-checks]]
id = "spaced-out-klei-path-temperature-performance"
title = "Spaced Out content mode uses authoritative topology on Klei inventory and pickup paths"
required = true
setup = "Enable the Spaced Out DLC, ensure the FastTrack inventory and pickup replacements are not active, load the designated multi-asteroid large-colony save, enable status temperature accounting, and record asteroid, rocket-interior, parent-world, destination, requested-tag, and pickupable counts."
action = "Run the fixed profiling interval while exercising storage, sweeping, construction, lacks-resources status, representative limit edits, asteroid switching, and supported rocket-interior lifecycle transitions."
expected = "Eligibility and status totals use only the authoritative current parent-world membership; the Klei inventory update path publishes complete world contributions without repeated world discovery in status queries; topology changes invalidate only affected aggregates; no DLC-specific single-world assumption or dense world-tag-band work appears."

[[acceptance-checks]]
id = "spaced-out-fasttrack-path-temperature-performance"
title = "Spaced Out content mode preserves FastTrack incremental work across authoritative worlds"
required = true
setup = "Enable the Spaced Out DLC and the verified FastTrack inventory and pickup replacements, load the designated multi-asteroid large-colony save, and record topology counts plus the FastTrack version, assembly identity, options, Harmony ownership, and SHA-256 digest."
action = "Run the fixed profiling interval with FastTrack background updates and pickup grouping while exercising status queries, active limit edits, asteroid switching, multi-tag resources, and supported rocket-interior lifecycle transitions."
expected = "Each world follows FastTrack's complete-first-or-coverage-then-single-tag publication contract; a one-tag refresh never reconstructs unrelated world/tag data; parent aggregates never mix worlds from different current parent groups; collision-free grouping and conservative stale behavior remain correct without relevant exceptions, warning storms, or unbounded retained collections."

[[acceptance-checks]]
id = "temperature-status-disabled-overhead"
title = "Disabled status accounting installs no inventory instrumentation"
required = true
setup = "Disable Include Temperature in Lacks Resources, restart ONI as required, load the designated large-colony save, and record enabled mods plus profiler configuration."
action = "Run the same fixed profiling interval and exercise delivery limits while inspecting Harmony ownership and mod-attributed allocations."
expected = "Direct delivery limits remain correct; Klei and FastTrack inventory/status temperature hooks are absent; no status accumulator, coverage scan, amount series, or world-temperature catalog work is observed."

[[acceptance-checks]]
id = "temperature-parent-world-topology"
title = "Asteroids and rocket interiors use current parent-world temperature data"
required = true
setup = "Prepare at least one asteroid parent group and one rocket interior with distinguishable in-range and out-of-range resources and destinations."
action = "Exercise storage and status queries, create or destroy a rocket interior where supported, change a world parent through normal gameplay, and repeat the queries after each topology change."
expected = "Only resources from the current parent group contribute; old-parent contributions disappear; new-parent data remains conservative until complete; no worker performs component lookup or queries mutable ClusterManager state."

[[acceptance-checks]]
id = "temperature-session-lifecycle"
title = "Temperature runtime state does not cross game sessions"
required = true
setup = "Configure distinctive limits in colony A and prepare colony B with different worlds, destinations, and material temperatures; prepare both base-game and Spaced Out content-mode variants."
action = "Load colony A, return to the main menu, load colony B, reload a save in colony B, and repeat the sequence for the Klei and FastTrack implementation paths in each applicable content mode."
expected = "Colony B observes no component, topology, inventory, grouping, diagnostic-suppression, or high-water variable state from colony A; late old-session work is rejected and logs contain one start and one completed shutdown per observed session."

[[acceptance-checks]]
id = "temperature-performance-evidence"
title = "Profiler, allocation, and retained-memory evidence has no unexplained mod hotspot"
required = true
setup = "Use the exact candidate installed by the pipeline and prepare the four base-game/Spaced-Out and Klei/FastTrack combinations plus status-disabled and active-limit scenarios, with counts and tool versions recorded."
action = "Capture CPU samples, allocation samples, generation-zero collection counts, retained collection capacities, lock contention, patch diagnostics, and the exact candidate plus optional-mod digests."
expected = "Every material mod-attributed cost is either removed or documented as unavoidable input-proportional work; direct checks allocate zero after warm-up; retained variable collections obey high-water replacement; no dense band multiplication, hot-reader rebuild, repeated world scan, FastTrack single-tag-to-complete-world reconstruction, or unexplained lock contention remains."
```

Impact: these cases become required, digest-bound human acceptance for release candidates. They are added only after the implementation exists, so pipeline validation never points at an impossible intermediate requirement.

## File and Module Map

All new production types are `internal` unless Task 23's curated runtime contract explicitly says otherwise.

```text
mods/delivery-temperature-limit-supercooled/
  Source/
    Domain/
      TemperatureConstraints/
        DeliveryTemperatureBounds.cs
        DeliveryTemperatureConstraint.cs
        TemperatureDecisionBucket.cs
        TemperatureConstraintGeneration.cs
        TemperatureConstraintRegistration.cs
        ActiveTemperatureConstraintSnapshot.cs
        TemperatureConstraintRegistry.cs
        TemperatureLimitComponentIndex.cs
        TemperatureLimitRegistration.cs
      Runtime/
        GameSessionGeneration.cs
        DeliveryTemperatureGameSession.cs
        DeliveryTemperatureGameSessionHost.cs
        SessionDiagnosticLimiter.cs
        RetainedCollectionLimits.cs
        InventoryImplementationPath.cs
        PickupGroupingImplementationPath.cs
        DeliveryTemperaturePatchActivationPlan.cs
      WorldTopology/
        WorldParentTopologyVersion.cs
        WorldParentTopologyChange.cs
        WorldParentTopologySnapshot.cs
        WorldParentTopologyCatalog.cs
      WorldInventory/
        WorldInventoryCollectionGeneration.cs
        TemperatureAmountAccumulator.cs
        TemperatureAmountSeries.cs
        CompleteWorldResourceTemperatureAmounts.cs
        WorldResourceTagCoverage.cs
        WorldResourceTemperatureSeriesPublication.cs
        CompleteWorldResourceTemperatureAmountsBuilder.cs
        WorldTemperatureInventoryCatalog.cs
      FetchEligibility/
        AllowedTemperatureInterval.cs
        AllowedTemperatureIntervalSet.cs
        FetchRequestTopologyVersion.cs
        FetchRequestTopologyTracker.cs
        PickupTagIdentity.cs
        TemperaturePartitionDefinition.cs
        TemperatureEligibilityClassKey.cs
        PickupTemperaturePartitionCatalog.cs
        FetchTemperatureEligibilitySnapshot.cs
        FetchTemperatureEligibilityBuilder.cs
        PickupTemperatureGroupingSession.cs
      FastTrack/
        FastTrackPickupGroupingKeyAllocator.cs
        FastTrackWorldInventoryUpdateKind.cs
        FastTrackWorldInventoryPublicationResult.cs
        FastTrackWorldInventoryPublicationSession.cs
    Patching/
      PatchContractViolationException.cs
      PatchContractVerifier.cs
      DeliveryTemperaturePatchInstaller.cs
      DeliveryTemperatureLifecyclePatches.cs
      WorldParentTopologyPatches.cs
      KleiWorldInventoryTemperaturePatches.cs
      StatusAvailabilityPatches.cs
      FetchTemperatureEligibilityPatches.cs
      DirectFetchTemperatureEligibilityPatches.cs
      KleiPickupTemperatureGroupingPatches.cs
      CodeInstructionFactory.cs
    FastTrack/
      FastTrackFeatureCompatibilityState.cs
      FastTrackCompatibilityReport.cs
      FastTrackCompatibilityInspector.cs
      FastTrackWorldInventoryTemperaturePatches.cs
      FastTrackPickupTemperaturePatches.cs
      FastTrackDirectFetchTemperaturePatches.cs
    TemperatureLimit.cs
    Mod.cs
    Construction.cs
    Widget.cs
    SideScreen.cs
    Buildings.cs
    Strings.cs
    Options.cs
  Tests/
    Domain/
      DeliveryTemperatureConstraintTests.cs
      TemperatureDecisionBucketTests.cs
      TemperatureConstraintRegistryTests.cs
      TemperatureLimitComponentIndexTests.cs
      DeliveryTemperatureGameSessionTests.cs
      SessionDiagnosticLimiterTests.cs
      WorldParentTopologyCatalogTests.cs
      TemperatureAmountAccumulatorTests.cs
      TemperatureAmountSeriesTests.cs
      WorldResourceTemperaturePublicationTests.cs
      CompleteWorldResourceTemperatureAmountsBuilderTests.cs
      WorldTemperatureInventoryCatalogTests.cs
      AllowedTemperatureIntervalSetTests.cs
      TemperaturePartitionDefinitionTests.cs
      FetchRequestTopologyTrackerTests.cs
      FetchTemperatureEligibilityBuilderTests.cs
      PickupTemperatureGroupingSessionTests.cs
      FastTrackPickupGroupingKeyAllocatorTests.cs
      FastTrackWorldInventoryPublicationSessionTests.cs
      DeliveryTemperaturePatchActivationPlanTests.cs
      CrossDomainTemperatureEligibilityTests.cs
    Patching/
      PatchContractVerifierTests.cs
    FastTrack/
      FastTrackCompatibilityInspectorTests.cs
      FastTrackReflectionEmitFixture.cs
      FastTrackPickupTemperaturePatchContractTests.cs
      FastTrackFallbackContractTests.cs
    Architecture/
      NoShimArchitectureContractTests.cs
      ImplementationTerminologyContractTests.cs
      PerformanceArchitectureContractTests.cs
    ReferenceModels/
      ReferenceTemperatureEligibility.cs
      ReferenceWorldTemperatureInventory.cs
    GameStubs.cs
    IntentionalRuntimeContract.cs
    PipelineAcceptanceProfileTests.cs
    ModBuildContractTests.cs
    PublicAssemblySurface.cs
    DeliveryTemperatureLimit.Tests.csproj
  oni-mod-pipeline.toml
```

At coordinated activation, delete rather than retain:

```text
Source/Limits.cs
Source/Patch.cs
Source/PatchFastTrack.cs
Source/StatusItems.cs
Source/Harmony.cs
```

`Source/Patching/CodeInstructionFactory.cs` replaces only the instruction-construction mechanics that remain necessary. It does not preserve the public `CodeInstruction2` name or act as a compatibility facade.

## Cross-Task Contract Registry

Later tasks must use these names and signatures. A genuine implementation discovery may change them only through a coordinated plan/spec amendment before dependent code is written.

```csharp
internal static class DeliveryTemperatureBounds
{
    internal const int MinimumSupportedKelvin = 0;
    internal const int MaximumSupportedKelvinExclusive = 5000;
    internal const int SupportedIntegerKelvinCount = 5000;
}

internal readonly struct DeliveryTemperatureConstraint : IEquatable<DeliveryTemperatureConstraint>
{
    internal int MinimumInclusiveKelvin { get; }
    internal int MaximumExclusiveKelvin { get; }
    internal bool IsEnabled { get; }
    internal bool IsEmpty { get; }

    internal static DeliveryTemperatureConstraint FromSerializedLimits(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin);
    internal bool Allows(float temperatureKelvin);
}

internal readonly struct TemperatureDecisionBucket :
    IEquatable<TemperatureDecisionBucket>, IComparable<TemperatureDecisionBucket>
{
    internal const int BucketCount = 5002;
    internal const int UnderflowOrdinal = 0;
    internal const int OverflowOrdinal = 5001;
    internal int Ordinal { get; }
    internal bool IsUnderflow { get; }
    internal bool IsOverflow { get; }
    internal bool TryGetIntegerKelvin(out int integerKelvin);
    internal static TemperatureDecisionBucket FromTemperature(float temperatureKelvin);
    internal static TemperatureDecisionBucket FromIntegerKelvin(int truncatedKelvin);
}

internal readonly struct TemperatureConstraintGeneration : IEquatable<TemperatureConstraintGeneration>
{
    internal long Value { get; }
}

internal readonly struct TemperatureConstraintRegistration : IEquatable<TemperatureConstraintRegistration>
{
    internal int ComponentInstanceId { get; }
    internal long RegistrationSequence { get; }
}

internal sealed class ActiveTemperatureConstraintSnapshot
{
    internal TemperatureConstraintGeneration Generation { get; }
    internal int EnabledConstraintCount { get; }
    internal int EnabledNonEmptyConstraintCount { get; }
    internal IReadOnlyList<int> SortedDecisionEndpointsKelvin { get; }
    internal IReadOnlyList<RegisteredTemperatureConstraint> RegisteredConstraints { get; }
}

internal sealed class TemperatureConstraintRegistry
{
    internal ActiveTemperatureConstraintSnapshot CaptureSnapshot();
    internal TemperatureConstraintRegistration Register(
        int componentInstanceId,
        DeliveryTemperatureConstraint constraint,
        out bool effectiveStateChanged);
    internal bool TryReplace(
        TemperatureConstraintRegistration registration,
        DeliveryTemperatureConstraint constraint,
        out bool effectiveStateChanged);
    internal bool TryRemove(
        TemperatureConstraintRegistration registration,
        out bool effectiveStateChanged);
}

internal sealed class TemperatureLimitComponentIndex
{
    internal bool TryRegister(
        int gameObjectInstanceId,
        TemperatureLimit component,
        TemperatureConstraintRegistration registration,
        DeliveryTemperatureConstraint constraint);
    internal bool TryReplaceConstraint(
        int gameObjectInstanceId,
        TemperatureConstraintRegistration registration,
        DeliveryTemperatureConstraint constraint);
    internal bool TryRemove(
        int gameObjectInstanceId,
        TemperatureConstraintRegistration registration);
    internal bool TryGetComponent(int gameObjectInstanceId, out TemperatureLimit component);
    internal bool TryGetConstraint(
        int gameObjectInstanceId,
        out DeliveryTemperatureConstraint constraint,
        out TemperatureConstraintRegistration registration);
}

internal readonly struct GameSessionGeneration : IEquatable<GameSessionGeneration>
{
    internal long Value { get; }
}

internal readonly struct TemperatureLimitRegistration : IEquatable<TemperatureLimitRegistration>
{
    internal GameSessionGeneration GameSessionGeneration { get; }
    internal int GameObjectInstanceId { get; }
    internal TemperatureConstraintRegistration ConstraintRegistration { get; }
}

internal sealed class DeliveryTemperatureGameSession
{
    internal GameSessionGeneration Generation { get; }
    internal int GameInstanceId { get; }
    internal bool IsAcceptingPublications { get; }
    internal TemperatureConstraintRegistry TemperatureConstraints { get; }
    internal TemperatureLimitComponentIndex TemperatureLimitComponents { get; }
    internal WorldParentTopologyCatalog WorldParentTopology { get; }
    internal WorldTemperatureInventoryCatalog WorldTemperatureInventory { get; }
    internal FetchRequestTopologyTracker FetchRequestTopology { get; }

    internal TemperatureLimitRegistration RegisterTemperatureLimit(
        int gameObjectInstanceId,
        int componentInstanceId,
        TemperatureLimit component,
        DeliveryTemperatureConstraint constraint);
    internal bool TryReplaceTemperatureConstraint(
        TemperatureLimitRegistration registration,
        DeliveryTemperatureConstraint constraint);
    internal void RemoveTemperatureLimit(TemperatureLimitRegistration registration);
    internal bool TryPublishFetchTemperatureEligibility(
        FetchTemperatureEligibilitySnapshot candidate);
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
}

internal sealed class TemperatureAmountAccumulator
{
    internal int TouchedBucketCount { get; }
    internal void BeginResourceTag();
    internal void Add(float temperatureKelvin, float amount);
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
    internal void AddPickup(float temperatureKelvin, float amount);
    internal void CompleteResourceTag();
    internal CompleteWorldResourceTemperatureAmounts Build();
    internal void Discard();
}

internal sealed class WorldTemperatureInventoryCatalog
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
    internal bool TryGetWorldResourceTagCoverageRequirement(
        int worldId,
        WorldInventoryCollectionGeneration expectedCollectionGeneration,
        out bool coverageRequired);
    internal bool TryGetAvailableAmount(
        int parentWorldId,
        Tag resourceTag,
        DeliveryTemperatureConstraint constraint,
        WorldInventoryCollectionGeneration expectedCollectionGeneration,
        out float availableAmount);
    internal void RemoveWorld(int worldId);
    internal void ClearForGameSession();
}
```

```csharp
internal readonly struct AllowedTemperatureInterval : IEquatable<AllowedTemperatureInterval>
{
    internal int MinimumInclusiveKelvin { get; }
    internal int MaximumExclusiveKelvin { get; }
}

internal sealed class AllowedTemperatureIntervalSet
{
    internal bool AllowsNoTemperature { get; }
    internal bool AllowsEveryTemperature { get; }
    internal IReadOnlyList<AllowedTemperatureInterval> Intervals { get; }
    internal bool Allows(TemperatureDecisionBucket bucket);
}

internal readonly struct FetchRequestTopologyVersion : IEquatable<FetchRequestTopologyVersion>
{
    internal long Value { get; }
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
}

internal sealed class TemperaturePartitionDefinition
{
    internal int DefinitionId { get; }
    internal IReadOnlyList<int> SortedDecisionEndpointsKelvin { get; }
    internal int Classify(TemperatureDecisionBucket bucket);
}

internal readonly struct TemperatureEligibilityClassKey :
    IEquatable<TemperatureEligibilityClassKey>, IComparable<TemperatureEligibilityClassKey>
{
    internal int PartitionDefinitionId { get; }
    internal int IntervalOrdinal { get; }
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
    internal TemperaturePartitionDefinition CreatePartitionForApplicableTags(
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
    internal void AddFetchRequest(
        int parentWorldId,
        IReadOnlyList<Tag> requestedTags,
        bool hasTemperatureLimit,
        DeliveryTemperatureConstraint constraint);
    internal FetchTemperatureEligibilitySnapshot Build();
    internal void Discard();
}

internal sealed class PickupTemperatureGroupingSession
{
    internal void Begin(
        DeliveryTemperatureGameSession session,
        int parentWorldId,
        ActiveTemperatureConstraintSnapshot constraints,
        FetchTemperatureEligibilitySnapshot? eligibilitySnapshot,
        WorldParentTopologySnapshot worldTopology);
    internal TemperatureEligibilityClassKey Classify(
        PickupTagIdentity tagIdentity,
        IReadOnlyList<Tag> applicableRequestedTags,
        bool hasPrimaryElement,
        float temperatureKelvin);
    internal void Complete();
    internal void Discard();
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
    DirectChoreComparison
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
        FastTrackVerifiedMember semanticMember,
        out MemberInfo member);
}

internal sealed class FastTrackCompatibilityReport
{
    internal string? AssemblyIdentity { get; }
    internal Version? AssemblyVersion { get; }
    internal string? AssemblySha256 { get; }
    internal FastTrackFeatureCompatibility GetFeature(FastTrackFeature feature);
}

internal sealed class FastTrackRuntimeInspectionInput
{
    internal bool IsFastTrackEnabledForActiveContent { get; }
    internal Assembly? FastTrackAssembly { get; }
    internal IReadOnlyList<RuntimePatchDescriptor> ActivePatches { get; }
}

internal sealed class RuntimePatchDescriptor
{
    internal MethodBase TargetMethod { get; }
    internal MethodInfo PatchMethod { get; }
    internal string HarmonyOwner { get; }
    internal int Priority { get; }
}

internal sealed class FastTrackCompatibilityInspector
{
    internal FastTrackCompatibilityReport Inspect(
        FastTrackRuntimeInspectionInput inspectionInput);
}

internal enum FastTrackWorldInventoryUpdateKind
{
    CompleteWorld,
    SingleResourceTag
}

internal sealed class FastTrackWorldInventoryPublicationResult
{
    internal FastTrackWorldInventoryUpdateKind UpdateKind { get; }
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
    internal void BeginSingleResourceTagUpdate(
        GameSessionGeneration gameSessionGeneration,
        WorldInventoryCollectionGeneration collectionGeneration,
        bool coverageRequired,
        IReadOnlyCollection<Tag> presentResourceTags);
    internal void BeginResourceTag(Tag resourceTag);
    internal void AddPickup(
        bool hasPrimaryElement,
        float temperatureKelvin,
        float amount);
    internal void CompleteResourceTag();
    internal FastTrackWorldInventoryPublicationResult Complete();
    internal void Discard();
}

internal enum InventoryImplementationPath
{
    None,
    Klei,
    FastTrack
}

internal enum PickupGroupingImplementationPath
{
    Klei,
    FastTrack
}

internal sealed class DeliveryTemperaturePatchActivationPlan
{
    internal bool InstallTemperatureStatusReplacement { get; }
    internal InventoryImplementationPath InventoryImplementationPath { get; }
    internal PickupGroupingImplementationPath PickupGroupingImplementationPath { get; }
    internal bool InstallFastTrackDirectChoreComparison { get; }
    internal bool HasReleaseBlockingIncompatibility { get; }

    internal static DeliveryTemperaturePatchActivationPlan Create(
        bool statusTemperatureAccountingEnabled,
        FastTrackCompatibilityReport fastTrackCompatibility,
        bool kleiInventoryFallbackVerified,
        bool kleiPickupFallbackVerified);
}
```

## Test Naming, Seeds, and Reference Rules

- Test method format: `Operation_WhenCondition_ExpectedOutcome`.
- Exhaustive temperature tests iterate `TemperatureDecisionBucket.UnderflowOrdinal` through `OverflowOrdinal` inclusive.
- Use fixed seeds and print the seed plus generated operation index in assertion messages:
  - constraint registry operations: `0x51A7E`;
  - interval normalization: `0x1A7E2A1`;
  - amount series: `0xA60A17`;
  - combined fetch eligibility: `0xFE7C4`;
  - lifecycle/concurrency schedules: `0x5E5510`.
- `Tests/ReferenceModels/ReferenceTemperatureEligibility.cs` must implement direct loops independently. It must not call production normalization, bucket classification, prefix summation, partition classification, or interval merging.
- Ordinary unit tests assert counts, equality, generations, completeness, and allocation structure. Do not use wall-clock thresholds in the unit suite.
- Every exception assertion verifies the semantic exception type and a message naming the violated invariant; do not pin punctuation or full stack traces.

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

Build through the repository-local pipeline when an integration adapter first touches game/Harmony types:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- build --mod mods/delivery-temperature-limit-supercooled
```

Expected build success includes one printed exact build-result JSON path. Do not install that Gate A–C build.

## Commit Protocol for Every Meaningful Chunk

For every task commit:

1. Run `git status --short` and confirm unrelated paths remain unstaged.
2. Run targeted `git diff --check -- <exact task paths>`.
3. Load and follow `C:\Users\maksy\.agents\skills\committing-to-git\SKILL.md` in full.
4. Invoke its `workflow prepare` in `actual`/`paths` mode with every exact whole path listed by the task, `--evidence reuse`, `--basis authored-current-task`, and only the task's stated allowed Conventional Commit type.
5. Show the helper's `displayText` verbatim and obtain explicit authorization for that exact snapshot and exact message.
6. Without reading or changing any artifact between prepare and commit, invoke `workflow commit` with the opaque transaction, the exact approved transport-safe subject, and `--verification required`.
7. Parse and report the helper's JSON result. If the commit exists but a later gate fails or the outcome is unknown, use the skill's recovery procedure; never repeat the commit blindly.
8. Do not push.

Every subject below begins with an uppercase description as required by the commit workflow.

---

### Task 0: Preflight, Configuration Approval, and Baseline Boundaries

**Files:**
- Read: `docs/specs/2026-08-29-delivery-temperature-limit-performance-rewrite-design.md`
- Read: `docs/plans/2026-08-29-delivery-temperature-limit-performance-rewrite.md`
- Inspect: `mods/delivery-temperature-limit-supercooled/Source/*.cs`
- Inspect: `mods/delivery-temperature-limit-supercooled/Tests/*`
- Inspect: `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`
- Do not modify any file in this task.

**Interfaces:**
- Consumes: approved architecture and current working tree.
- Produces: a verified clean implementation scope, exact configuration authorization, and baseline focused command evidence.

- [ ] **Step 1: Re-read repository instructions and both approved documents completely**

Record any contradiction before proceeding. The specification wins over this plan only when it is more specific; a real contradiction requires user resolution.

- [ ] **Step 2: Inspect the working tree without changing it**

Run:

```text
git status --short
```

Expected: identify every pre-existing modification/untracked path, including but not limited to the two paths recorded in Global Constraints. Treat all as user-owned.

- [ ] **Step 3: Obtain exact configuration approval**

Present the two exact changes in the Configuration Approval Dossier. Do not edit either file until the user explicitly approves those exact settings and impacts.

Expected: approval names both `Tests/DeliveryTemperatureLimit.Tests.csproj` and `oni-mod-pipeline.toml`. Approval for one does not authorize the other.

- [ ] **Step 4: Confirm the current focused tests before changing the harness**

Run:

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore -- --filter "FullyQualifiedName~BuildingsEligibilityTests"
```

Expected: PASS. If restore assets are absent, run the locked restore command once, then rerun. Do not run installed-game or profiler validation.

- [ ] **Step 5: Confirm the local pipeline entrypoint without running the deep campaign**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- diagnose --mod mods/delivery-temperature-limit-supercooled
```

Expected: successful environment diagnosis naming the installed ONI managed directory. If tool restore assets are absent, perform a locked restore of the tool project as a separate command. Do not change configuration to work around discovery.

- [ ] **Step 6: Record the big-bang boundary in the implementation log**

Use the task conversation/status update, not a repository file, to state:

```text
Gate A–C builds are compile/test artifacts only and must not be installed.
The first installable build is the Gate D coordinated activation.
```

There is no commit for Task 0.

---

### Task 1: Canonical Delivery Constraint and Temperature Decision Buckets

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/DeliveryTemperatureBounds.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/DeliveryTemperatureConstraint.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/TemperatureDecisionBucket.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperatureConstraintTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperatureDecisionBucketTests.cs`

**Interfaces:**
- Consumes: the approved project wildcard and exact existing serialized semantics.
- Produces: `DeliveryTemperatureBounds`, `DeliveryTemperatureConstraint`, and `TemperatureDecisionBucket` exactly as declared in the contract registry.

- [ ] **Step 1: Add the approved production-domain compile wildcard**

Insert exactly the XML from the Configuration Approval Dossier. Do not reformat or alter any other project property. Run locked restore and verify `packages.lock.json` remains byte-unchanged.

- [ ] **Step 2: Write failing constraint characterization tests**

Create tests covering disabled, normalized, enabled-empty, boundaries, and truncation. Include this core:

```csharp
[TestMethod]
public void Allows_WhenTemperatureIsInsideInclusiveExclusiveBounds_ReturnsExpectedDecision()
{
    var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(10, 20);

    Assert.IsFalse(constraint.Allows(9.999f));  // truncates to 9
    Assert.IsTrue(constraint.Allows(10.0f));
    Assert.IsTrue(constraint.Allows(19.999f)); // truncates to 19
    Assert.IsFalse(constraint.Allows(20.0f));
}

[TestMethod]
public void FromSerializedLimits_WhenEnabledMinimumIsNotBelowMaximum_PreservesEmptyConstraint()
{
    var constraint = DeliveryTemperatureConstraint.FromSerializedLimits(100, 100);

    Assert.IsTrue(constraint.IsEnabled);
    Assert.IsTrue(constraint.IsEmpty);
    Assert.IsFalse(constraint.Allows(100.0f));
}
```

Also add exact tests named:

- `FromSerializedLimits_WhenHighIsZero_ReturnsDisabledConstraint`
- `FromSerializedLimits_WhenValuesExceedBounds_ClampsBothValues`
- `FromSerializedLimits_WhenValuesAreNegative_ClampsBothValuesToZero`
- `Allows_WhenConstraintIsDisabled_ReturnsTrueForUnderflowAndOverflow`
- `Allows_WhenTemperatureHasNegativeFraction_TruncatesTowardZero`
- `Allows_WhenMaximumIs5000_Rejects5000AndOverflow`

- [ ] **Step 3: Write failing decision-bucket tests**

Include:

```csharp
[DataRow(-1.0f, TemperatureDecisionBucket.UnderflowOrdinal)]
[DataRow(-0.999f, 1)]
[DataRow(0.0f, 1)]
[DataRow(273.15f, 274)]
[DataRow(4999.999f, 5000)]
[DataRow(5000.0f, TemperatureDecisionBucket.OverflowOrdinal)]
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

Add exhaustive round-trip coverage for integer Kelvin `0..4999` and tests that every truncated integer below zero shares underflow while every integer at/above 5000 shares overflow.

- [ ] **Step 4: Run both test classes and observe the intended red**

Run the focused command once with `DeliveryTemperatureConstraintTests`, then once with `TemperatureDecisionBucketTests`.

Expected: compilation fails because the three production types do not exist. Any project XML, lockfile, or unrelated compilation failure must be fixed before implementation.

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

- [ ] **Step 6: Implement the fixed 5,002-class bucket mapping**

Use ordinal mapping:

```csharp
if (truncatedKelvin < DeliveryTemperatureBounds.MinimumSupportedKelvin)
    return new TemperatureDecisionBucket(UnderflowOrdinal);
if (truncatedKelvin >= DeliveryTemperatureBounds.MaximumSupportedKelvinExclusive)
    return new TemperatureDecisionBucket(OverflowOrdinal);
return new TemperatureDecisionBucket(truncatedKelvin + 1);
```

Comment why negative Celsius is not negative Kelvin and why `-0.999 K` truncates into the zero-Kelvin class.

- [ ] **Step 7: Run the focused tests green and refactor**

Expected: both classes PASS. Confirm equality/hash/compare implementations are value-based and allocation-free. Run `git diff --check` on the six task paths.

- [ ] **Step 8: Perform the chunk shim and naming scan**

Run:

```text
rg -n "TemperatureIndexData|getTemperatureIndexData|Helper|Utils" mods/delivery-temperature-limit-supercooled/Source/Domain mods/delivery-temperature-limit-supercooled/Tests/Domain
```

Expected: no new legacy reference, generic utility name, or incomplete marker. Existing legacy references outside the new directories are expected until Gate D.

- [ ] **Step 9: Prepare and commit the exact chunk**

Use the Commit Protocol with the six task paths, allowed type `perf`, and exact subject:

```text
perf: Define canonical delivery temperature semantics
```

---

### Task 2: Immutable Constraint Registry and Endpoint Reference Counts

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/TemperatureConstraintGeneration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/TemperatureConstraintRegistration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/ActiveTemperatureConstraintSnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/TemperatureConstraintRegistry.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperatureConstraintRegistryTests.cs`

**Interfaces:**
- Consumes: canonical constraints and bounds from Task 1.
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
- `CaptureSnapshot_WhenCallerMutatesReturnedView_CannotMutateRegistryState` (the API should expose read-only arrays/views that cannot be cast back to mutable owned arrays).

- [ ] **Step 2: Add deterministic randomized reference-model test**

Using seed `0x51A7E`, execute 50,000 register/replace/remove operations across component IDs `0..2047`. Maintain a test-only dictionary and rebuild expected endpoints by direct loops after each sampled operation. Every 97 operations assert counts, generation increments, sorted endpoints, and exact registered constraints.

- [ ] **Step 3: Run the class and observe red**

Expected: compilation fails for missing registry types.

- [ ] **Step 4: Implement token ownership and O(1) mutation**

Use:

```csharp
private readonly Dictionary<int, RegistryEntry> entriesByComponentInstanceId;
private readonly int[] endpointReferenceCounts =
    new int[DeliveryTemperatureBounds.MaximumSupportedKelvinExclusive + 1];
private long nextRegistrationSequence;
private long generation;
private ActiveTemperatureConstraintSnapshot publishedSnapshot;
```

Registration sequence zero is invalid. Increment with checked semantics; exhaustion throws a named `InvalidOperationException` and never reuses a token. Endpoint increments/decrements occur only for enabled, nonempty constraints. Guard underflow of a reference count with an invariant exception.

- [ ] **Step 5: Eagerly publish immutable snapshots on changed mutations**

Reconstruct sorted endpoints by scanning the fixed 5,001-entry reference array, not by sorting all components. Copy registered constraints into a deterministic component-instance-ID order. Publish the new reference only after all fields are complete and while the registry owns its private lock.

Return the already-published reference from `CaptureSnapshot`; it must not acquire another service or cause deferred work.

- [ ] **Step 6: Run focused tests and randomized test green**

Expected: all registry tests PASS. Confirm identical operations preserve object reference equality of the published snapshot as evidence that they are true no-ops.

- [ ] **Step 7: Inspect bounded complexity and comments**

Verify the only per-mutation bounded scan is the fixed endpoint array plus snapshot copy; no LINQ `Sort`, `Distinct`, `allLimits` scan, lazy flag, or worker callback exists. Comments must identify token ownership and endpoint-count invariants.

- [ ] **Step 8: Prepare and commit**

Use the five task paths, allowed type `perf`, and exact subject:

```text
perf: Publish immutable active temperature constraints
```

---

### Task 3: Owned TemperatureLimit Component Index

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/TemperatureLimitComponentIndex.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/TemperatureConstraints/TemperatureLimitRegistration.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/GameStubs.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperatureLimitComponentIndexTests.cs`

**Interfaces:**
- Consumes: `DeliveryTemperatureConstraint` and `TemperatureConstraintRegistration`.
- Produces: O(1) thread-safe GameObject-instance lookup with remove-if-owned semantics; `TemperatureLimitRegistration` later composes index and registry ownership.

- [ ] **Step 1: Extend the test stub semantically**

Replace the empty test `TemperatureLimit` stub with a sealed stub that carries a test label only:

```csharp
public sealed class TemperatureLimit
{
    public TemperatureLimit(string diagnosticLabel) =>
        DiagnosticLabel = diagnosticLabel;

    public string DiagnosticLabel { get; }
}
```

Do not add Unity lifecycle behavior to the stub; the index must not depend on it.

- [ ] **Step 2: Write failing ownership and lookup tests**

Include:

```csharp
[TestMethod]
public void TryRemove_WhenGameObjectIdWasReused_DoesNotRemoveNewOwner()
{
    var index = new TemperatureLimitComponentIndex();
    var oldRegistration = Registration(componentId: 1, sequence: 10);
    var newRegistration = Registration(componentId: 2, sequence: 11);

    Assert.IsTrue(index.TryRegister(77, new TemperatureLimit("old"), oldRegistration, Constraint(10, 20)));
    Assert.IsTrue(index.TryRegister(77, new TemperatureLimit("new"), newRegistration, Constraint(30, 40)));

    Assert.IsFalse(index.TryRemove(77, oldRegistration));
    Assert.IsTrue(index.TryGetComponent(77, out var component));
    Assert.AreEqual("new", component.DiagnosticLabel);
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

Use `ConcurrentDictionary<int, TemperatureLimitComponentEntry>` because reads can occur from FastTrack-related paths. Each entry is one immutable object containing component reference, registration token, and immutable constraint. Use `TryUpdate`/`TryRemove(KeyValuePair<...>)` loops so stale ownership cannot remove a replacement.

Do not expose the dictionary, return mutable entries, or call Unity from the index.

- [ ] **Step 5: Run tests green and inspect allocations**

Expected: all index tests PASS. Ordinary successful reads must perform no allocation. Replacement may allocate one immutable entry; it occurs only on configuration change.

- [ ] **Step 6: Prepare and commit**

Use the four task paths, allowed type `perf`, and exact subject:

```text
perf: Add owned temperature component lookup
```

---

### Task 4: Game-Session Ownership, Registration Coordination, and Diagnostic Limiting

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/GameSessionGeneration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/RetainedCollectionLimits.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/SessionDiagnosticLimiter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperatureGameSessionHost.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/SessionDiagnosticLimiterTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperatureGameSessionTests.cs`

**Interfaces:**
- Consumes: constraint registry and component index.
- Produces: one atomically published game session, composite component registration, stale-session rejection, idempotent two-phase shutdown, and per-session diagnostic rate limiting. Later tasks extend the same session with their completed world/fetch services; do not add null or `object` placeholders for types that do not yet exist.

- [ ] **Step 1: Write failing host lifecycle tests**

Include:

```csharp
[TestMethod]
public void EnsureGameSession_WhenGameIdentityChanges_DetachesAndInvalidatesOldSession()
{
    DeliveryTemperatureGameSessionHost.ResetForTests();
    var oldSession = DeliveryTemperatureGameSessionHost.EnsureGameSession(100);

    var newSession = DeliveryTemperatureGameSessionHost.EnsureGameSession(200);

    Assert.AreNotSame(oldSession, newSession);
    Assert.IsFalse(oldSession.IsAcceptingPublications);
    Assert.IsTrue(newSession.IsAcceptingPublications);
    Assert.AreNotEqual(oldSession.Generation, newSession.Generation);
}
```

`ResetForTests` is `internal` and exists only to isolate static host tests; production code must not call it.

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

Use `Interlocked.Increment` for nonzero game-session generations, `Volatile.Read` to capture the current session, and `Interlocked.CompareExchange`/`Exchange` for publication and detachment. Store only the integer `Game` instance identity, never a Unity object, in the pure host.

`EnsureGameSession` must stop a different old session before publishing the replacement. A worker holding the old object can then mutate only an inactive, detached session whose publication methods reject it.

- [ ] **Step 5: Implement composite component registration transaction**

Sequence mutation so a partially failed registration is rolled back in the same session. Never hold both service locks. Add a comment explaining the short observable ordering window and why fetch snapshot generation validation prevents mixed publication.

`TemperatureLimitRegistration` includes session generation, GameObject ID, and registry token. Every replace/remove validates all three before mutation.

- [ ] **Step 6: Add named retained collection limits**

Use these initial limits and comments explaining that Task 27 verifies them under profiling:

```csharp
internal const int MaximumRetainedPickupClassificationCount = 16384;
internal const int MaximumRetainedFastTrackGroupingKeyCount = 8192;
internal const int MaximumRetainedFetchEligibilityEntryCount = 4096;
internal const int MaximumRetainedWorldResourceTagCount = 4096;
```

Do not call them cache sizes. Each consumer later replaces its variable-capacity collection after exceeding its relevant limit.

- [ ] **Step 7: Run green, then run lifecycle schedule stress**

Use seed `0x5E5510` to run 10,000 deterministic ensure/capture/detach/complete/register/remove operations. Assert no old generation removes or publishes into the current session. This is invariant testing, not wall-clock benchmarking.

- [ ] **Step 8: Scan for static mutable gameplay collections**

Within `Source/Domain`, only the host's atomic current-session reference and monotonic generation source may be mutable static state. `SessionDiagnosticLimiter` belongs to a session instance.

- [ ] **Step 9: Prepare and commit**

Use the seven task paths, allowed type `perf`, and exact subject:

```text
perf: Scope temperature state to game sessions
```

---

### Task 5: Immutable World-to-Parent Topology

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldTopology/WorldParentTopologyVersion.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldTopology/WorldParentTopologyChange.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldTopology/WorldParentTopologySnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldTopology/WorldParentTopologyCatalog.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/WorldParentTopologyCatalogTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperatureGameSessionTests.cs`

**Interfaces:**
- Consumes: `GameSessionGeneration` and the session lifetime from Task 4.
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

- [ ] **Step 2: Run red**

Expected: missing topology types and session property.

- [ ] **Step 3: Implement immutable snapshot publication**

Keep one private dictionary behind one private lock. On an effective mutation, copy it into a new snapshot and build a parent-to-sorted-member array map before publication. Repeated identical registration returns the existing snapshot reference.

Do not infer a parent for unknown worlds. Do not normalize an explicit self-parent into another sentinel.

- [ ] **Step 4: Extend `DeliveryTemperatureGameSession` with the completed catalog**

Construct it with the session generation, expose it through `WorldParentTopology`, and clear its owned mutable dictionary in `ReleaseOwnedState`. Session shutdown must leave already-captured snapshots usable but detached from future publication.

- [ ] **Step 5: Run topology and amended session tests green**

Expected: both classes PASS. The concurrent reader test must use barriers and assert whole snapshot states, not a timing deadline.

- [ ] **Step 6: Review world semantic names and locking**

Confirm every integer parameter/property says `worldId` or `parentWorldId`; no bare `id`, `index`, or `worldMap` survives. Confirm snapshot construction calls no other domain service while locked.

- [ ] **Step 7: Prepare and commit**

Use the seven task paths, allowed type `perf`, and exact subject:

```text
perf: Publish immutable parent world topology
```

---

### Task 6: Sparse Temperature Amount Accumulator and Prefix-Summed Series

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/TemperatureAmountAccumulator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/TemperatureAmountSeries.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperatureAmountAccumulatorTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperatureAmountSeriesTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceTemperatureEligibility.cs`

**Interfaces:**
- Consumes: `TemperatureDecisionBucket` and `DeliveryTemperatureConstraint`.
- Produces: O(1) stamped accumulation over fixed 5,002-class arrays and immutable sparse prefix-sum range queries.

- [ ] **Step 1: Write failing accumulator tests**

Include:

```csharp
[TestMethod]
public void BeginResourceTag_WhenPreviousTagTouchedFewBuckets_DoesNotCarryAmountsForward()
{
    var accumulator = new TemperatureAmountAccumulator();
    accumulator.BeginResourceTag();
    accumulator.Add(10.0f, 4.0f);
    _ = accumulator.BuildSeries();

    accumulator.BeginResourceTag();
    accumulator.Add(20.0f, 3.0f);
    var second = accumulator.BuildSeries();

    Assert.AreEqual(1, second.OccupiedBucketCount);
    Assert.AreEqual(3.0f, second.TotalAmount);
}
```

Add exact tests:

- `Add_WhenSeveralAmountsShareBucket_SumsThem`
- `Add_WhenAmountIsZero_DoesNotTouchBucket`
- `Add_WhenAmountsCancelToZero_OmitsBucketFromSeries`
- `Add_WhenTemperatureIsUnderflow_UsesUnderflowBucket`
- `Add_WhenTemperatureIsOverflow_UsesOverflowBucket`
- `BeginResourceTag_WhenStampWraps_PerformsOneSafeFullReset`
- `BuildSeries_WhenTouchedBucketsWereUnordered_SortsByBucketOrdinal`
- `BuildSeries_WhenCalledWithoutBegin_ThrowsInvalidOperationException`
- `Add_WhenCalledAfterBuildWithoutNewBegin_ThrowsInvalidOperationException`

Expose an internal constructor/test seam that initializes the stamp to `int.MaxValue` solely to test wraparound without billions of calls. Production callers use the parameterless constructor.

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
        (5000.0f, 13.0f));

    Assert.AreEqual(
        12.0f,
        series.GetAmountAllowedBy(Constraint(10, 20)));
}
```

Add exact tests:

- `GetAmountAllowedBy_WhenConstraintIsDisabled_ReturnsTotalIncludingUnderflowAndOverflow`
- `GetAmountAllowedBy_WhenConstraintIsEmpty_ReturnsZero`
- `GetAmountAllowedBy_WhenNoBucketOccupied_ReturnsZero`
- `GetAmountAllowedBy_WhenMaximumIs5000_ExcludesOverflow`
- `GetAmountAllowedBy_WhenMinimumIsZero_ExcludesUnderflow`
- `PublishedArrays_WhenSourceBuffersAreReused_DoNotChange`

- [ ] **Step 3: Add deterministic reference comparison**

With seed `0xA60A17`, generate 10,000 sparse series with bucket counts `0..256`, duplicate additions, cancellation, and random normalized constraints. Compare `GetAmountAllowedBy` with `ReferenceTemperatureEligibility.SumAllowedAmounts`, which directly iterates original `(temperature, amount)` pairs and calls its own explicit truncation/comparison.

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

Add comments showing the ordinal mapping used for `[minimum, maximum)` and why underflow/overflow are excluded from every enabled valid constraint.

- [ ] **Step 7: Run all tests green and inspect representation**

Expected: both focused classes and randomized reference comparison PASS. Verify no dictionary, LINQ iterator, or per-`BeginResourceTag` array allocation exists.

- [ ] **Step 8: Prepare and commit**

Use the five task paths, allowed type `perf`, and exact subject:

```text
perf: Add sparse temperature amount series
```

---

### Task 7: Immutable Complete-World, Coverage, and Single-Resource Inventory Publications

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/WorldInventoryCollectionGeneration.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/CompleteWorldResourceTemperatureAmounts.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/WorldResourceTagCoverage.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/WorldResourceTemperatureSeriesPublication.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/CompleteWorldResourceTemperatureAmountsBuilder.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/GameStubs.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/WorldResourceTemperaturePublicationTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/CompleteWorldResourceTemperatureAmountsBuilderTests.cs`

**Interfaces:**
- Consumes: sparse accumulator/series and `RetainedCollectionLimits.MaximumRetainedWorldResourceTagCount`.
- Produces: three non-interchangeable immutable contracts: one complete world map, one complete present-tag coverage set, and one complete temperature series for exactly one resource tag.

- [ ] **Step 1: Add the production-shaped `Tag` test stub**

Define only the value behavior required by production domain files:

```csharp
public readonly struct Tag : IEquatable<Tag>
{
    public Tag(string value) => Value = value;
    public string Value { get; }
    public bool Equals(Tag other) => StringComparer.Ordinal.Equals(Value, other.Value);
    public override bool Equals(object? obj) => obj is Tag other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
}
```

Do not add game-only conversions, implicit operators, or members unused by the linked pure-domain sources.

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
    builder.AddPickup(300.0f, 10.0f);
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

Inject an internal retained-tag limit of four for the high-water test; do not manufacture 4,097 test tags.

- [ ] **Step 7: Run the builder tests red**

Run the focused command with `CLASS_NAME=CompleteWorldResourceTemperatureAmountsBuilderTests`.

Expected: missing complete-world builder and publication types. Confirm the failures are not caused by the earlier coverage types.

- [ ] **Step 8: Implement the explicit complete-world builder state machine**

Use named states `Idle`, `BuildingWorld`, `BuildingResourceTag`, and `Completed`. `Build` copies the tag-to-series mapping into `CompleteWorldResourceTemperatureAmounts`; the publication never exposes the reusable dictionary. `Discard` returns to `Idle` and clears all candidate references after any exception path.

When retained entry count exceeds the configured limit, replace the mutable dictionary instance instead of only calling `Clear`. Expose dictionary identity only through an `internal` diagnostic consumed by the test assembly; do not add public diagnostic surface.

- [ ] **Step 9: Run both publication classes green and inspect allocation ownership**

Run `WorldResourceTemperaturePublicationTests`, then `CompleteWorldResourceTemperatureAmountsBuilderTests`, as separate commands. Build candidate A, reuse the builder for candidate B, and verify candidate A remains byte-for-byte equivalent through its semantic accessors.

Expected: both classes pass. Confirm no publication exposes mutable arrays, dictionaries, or source collections and no type uses the ambiguous unqualified term prohibited by Global Constraints.

- [ ] **Step 10: Prepare and commit**

Use the eight task paths, allowed type `perf`, and exact subject:

```text
perf: Add explicit world inventory publications
```

---

### Task 8: Preaggregated Parent-World Temperature Inventory Catalog

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/WorldTemperatureInventoryCatalog.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/TemperatureAmountSeries.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/WorldTemperatureInventoryCatalogTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperatureAmountSeriesTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperatureGameSessionTests.cs`

**Interfaces:**
- Consumes: complete-world publications, FastTrack coverage and single-tag publications, world-parent changes, constraints, and session ownership.
- Produces: per-parent/tag immutable aggregate series, O(1) completeness lookup followed by O(log occupied buckets) amount lookup, and session-owned cleanup without `WorldContainer` enumeration.

- [ ] **Step 1: Write failing complete-world and coverage completeness tests**

Include this test exactly:

```csharp
[TestMethod]
public void TryGetAvailableAmount_WhenCoverageContainsTagButSeriesHasNotArrived_ReturnsFalse()
{
    var catalog = new WorldTemperatureInventoryCatalog();
    catalog.RegisterWorld(worldId: 1, parentWorldId: 1);
    var generation = new WorldInventoryCollectionGeneration(9);
    catalog.PublishWorldResourceTagCoverage(
        1,
        WorldResourceTagCoverage.Create(generation, new[] { new Tag("Iron") }));

    var complete = catalog.TryGetAvailableAmount(
        1,
        new Tag("Iron"),
        Constraint(250, 350),
        generation,
        out _);

    Assert.IsFalse(complete);
}
```

Add exact tests:

- `TryGetAvailableAmount_WhenEveryMemberHasCompleteWorldPublication_SumsParentAndChildAmounts`
- `TryGetAvailableAmount_WhenTagIsAbsentFromCompleteWorld_TreatsContributionAsKnownZero`
- `TryGetAvailableAmount_WhenEveryCoverageExcludesTag_ReturnsCompleteZero`
- `TryGetAvailableAmount_WhenOneMemberCoverageIsMissing_ReturnsFalse`
- `TryGetAvailableAmount_WhenCoverageContainsTagAndCurrentSeriesExists_ReturnsAmount`
- `TryGetAvailableAmount_WhenOnePresentMemberSeriesIsPending_ReturnsFalseRatherThanZero`
- `PublishCompleteWorldResourceAmounts_WhenWorldIsUnknown_ReturnsFalse`
- `PublishWorldResourceTagCoverage_WhenGenerationIsOlder_RejectsLatePublication`
- `PublishWorldResourceTemperatureSeries_WhenNoCurrentCoverageOrCompletePublicationExists_ReturnsFalse`
- `PublishWorldResourceTagCoverage_AfterCompletePublicationForSameGeneration_RejectsSemanticDowngrade`
- `PublishCompleteWorldResourceAmounts_AfterCoveragePublication_UpgradesToCompleteWorldState`
- `TryGetWorldResourceTagCoverageRequirement_WhenWorldIsUnknown_ReturnsFalse`
- `TryGetWorldResourceTagCoverageRequirement_WhenGenerationHasNoCoverage_ReturnsRequired`
- `TryGetWorldResourceTagCoverageRequirement_AfterCoverageOrCompletePublication_ReturnsNotRequired`

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
- `TryGetAvailableAmount_WhenConstraintIsEmpty_ReturnsCompleteZeroWithoutSeriesSearch`
- `TryGetAvailableAmount_WhenConstraintIsDisabled_ReturnsFalseBecauseCallerMustBypassTemperatureReplacement`
- `ConcurrentReadDuringSingleTagPublication_ObservesWholeOldOrWholeNewTagAggregate`

Use internal diagnostics to count aggregate rebuilds. The diagnostic must be compiled only as an `internal` member; no production behavior may branch on test mode.

- [ ] **Step 3: Write failing sparse series-combination tests**

Add `TemperatureAmountSeries.Combine` tests for empty inputs, overlapping buckets, disjoint buckets, underflow/overflow, source immutability, and `Combine_WhenSameSeriesAppearsTwice_CountsTwoListedContributions`. Each input-list entry semantically represents one member-world contribution even when two entries reference the same immutable object. The implementation must merge sorted sparse bucket arrays and must not expand every aggregate rebuild to 5,002 entries.

- [ ] **Step 4: Run all three affected classes red**

Run `WorldTemperatureInventoryCatalogTests`, `TemperatureAmountSeriesTests`, and `DeliveryTemperatureGameSessionTests` separately.

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

An aggregate is queryable only if the parent member-set version and expected collection generation match, every member has current coverage, and its pending-present-world count is zero. If every member has coverage and no member reports the requested tag, return a complete zero without constructing a zero-filled series. Never infer zero from a missing world publication, missing coverage, or pending present tag.

- [ ] **Step 6: Implement publication-specific aggregate rebuilds**

For `PublishCompleteWorldResourceAmounts`, compute the union of the old and replacement tag sets and rebuild only those parent/tag entries. For `PublishWorldResourceTemperatureSeries`, rebuild exactly one parent/tag entry. For coverage replacement, update completeness only for tags whose presence changed plus currently pending tags; do not rebuild an unrelated amount series.

Capture immutable member series and catalog versions under the lock, release it, call `TemperatureAmountSeries.Combine` outside the lock, then reacquire and publish only if registration/publication versions remain unchanged. Retry a bounded number of times or leave the aggregate incomplete for the next authoritative publication; never spin indefinitely. Never acquire or call `WorldParentTopologyCatalog` while holding the inventory lock.

- [ ] **Step 7: Extend game-session ownership**

Construct and expose `WorldTemperatureInventory`. When topology changes, the game adapter calls topology and inventory services sequentially after releasing each service's lock. `ReleaseOwnedState` invokes `ClearForGameSession` exactly once; late detached-session publications return `false` and retain no candidate reference.

- [ ] **Step 8: Run green and deterministic mixed-publication stress**

Using seed `0x5E5510`, execute 10,000 operations covering world registration, complete publication, coverage publication, single-tag replacement, new tag publication, coverage replacement, removal, and reparenting. Compare every query with a test-only direct reference sum over the currently registered publication states.

The reference model must independently implement the three-proof rule; it must not call catalog completeness methods. Assertion messages include seed and operation index.

- [ ] **Step 9: Verify fallback and complexity contracts**

Assert that `TryGetAvailableAmount` returns `false` for every incomplete case and that callers ignore `availableAmount` on `false`. Comments must tell the status adapter to preserve ONI's incoming `fetchable` unchanged. Use diagnostics to prove a single-tag update rebuilds one tag, does not enumerate `WorldContainer`, and does not construct a complete-world map.

- [ ] **Step 10: Prepare and commit**

Use the six task paths, allowed type `perf`, and exact subject:

```text
perf: Preaggregate incremental world temperature inventory
```

---

### Task 9: Normalized Storage Temperature Interval Sets

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/AllowedTemperatureInterval.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/AllowedTemperatureIntervalSet.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/AllowedTemperatureIntervalSetTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceTemperatureEligibility.cs`

**Interfaces:**
- Consumes: canonical constraints and decision buckets.
- Produces: immutable `AllowsNoTemperature`, `AllowsEveryTemperature`, or sorted merged inclusive-low/exclusive-high intervals with O(log interval count) lookup.

- [ ] **Step 1: Add the factory to the contract**

Implement and use this exact construction seam:

```csharp
internal static AllowedTemperatureIntervalSet FromDestinations(
    bool hasUnconstrainedDestination,
    IReadOnlyList<DeliveryTemperatureConstraint> constrainedDestinations);
```

A missing or disabled `TemperatureLimit` is represented by `hasUnconstrainedDestination = true`; do not insert a fake `[0,5000)` constraint.

- [ ] **Step 2: Write failing state and normalization tests**

Include:

```csharp
[TestMethod]
public void FromDestinations_WhenIntervalsOverlapOrTouch_MergesThem()
{
    var set = AllowedTemperatureIntervalSet.FromDestinations(
        hasUnconstrainedDestination: false,
        [Constraint(10, 20), Constraint(15, 30), Constraint(30, 40)]);

    CollectionAssert.AreEqual(
        new[] { new AllowedTemperatureInterval(10, 40) },
        set.Intervals.ToArray());
}
```

Add exact tests:

- `FromDestinations_WhenNoDestinationContributes_ReturnsAllowsNoTemperature`
- `FromDestinations_WhenUnconstrainedDestinationExists_ReturnsAllowsEveryTemperature`
- `FromDestinations_WhenConstraintIsDisabled_TreatsItAsUnconstrained`
- `FromDestinations_WhenConstraintIsEmpty_IgnoresIt`
- `FromDestinations_WhenIntervalsDuplicate_CollapsesThem`
- `FromDestinations_WhenIntervalsAreDisjoint_SortsThem`
- `Allows_WhenBucketIsAtInclusiveMinimum_ReturnsTrue`
- `Allows_WhenBucketIsAtExclusiveMaximum_ReturnsFalse`
- `Allows_WhenBucketIsUnderflow_ReturnsFalseUnlessAllowsEvery`
- `Allows_WhenBucketIsOverflow_ReturnsFalseUnlessAllowsEvery`
- `PublishedIntervals_WhenInputListChanges_RemainImmutable`

- [ ] **Step 3: Add exhaustive and randomized reference tests**

For each generated destination set, compare interval membership for all 5,002 buckets with direct “any destination allows” evaluation from `ReferenceTemperatureEligibility`. Use seed `0x1A7E2A1`, 5,000 destination sets, duplicates, adjacency, disabled, empty, zero, and 5000 boundaries.

- [ ] **Step 4: Run red**

Expected: missing interval types/factory.

- [ ] **Step 5: Implement sort-and-merge normalization**

Return singleton immutable instances for allows-none and allows-every. For finite constraints, copy valid intervals, sort by minimum then maximum, and merge when `next.MinimumInclusiveKelvin <= current.MaximumExclusiveKelvin`. That comparison intentionally merges adjacency.

Membership uses binary search against interval minima/maxima. Do not allocate a 5,002-entry membership array and do not retain the input list.

- [ ] **Step 6: Run green and inspect minimal representation**

Expected: all interval and reference tests PASS. Assert `AllowsEveryTemperature` carries no interval array and empty constraints do not contribute endpoints.

- [ ] **Step 7: Prepare and commit**

Use the four task paths, allowed type `perf`, and exact subject:

```text
perf: Normalize storage temperature eligibility
```

---

### Task 10: Scoped Temperature Partition Definitions and Class Keys

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/PickupTagIdentity.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/TemperaturePartitionDefinition.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/TemperatureEligibilityClassKey.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/PickupTemperaturePartitionCatalog.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/TemperaturePartitionDefinitionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceTemperatureEligibility.cs`

**Interfaces:**
- Consumes: decision buckets and relevant constraint endpoints.
- Produces: minimal behavior-equivalence partitions, snapshot-local definition IDs, exact fallback/missing-element classes, and structural sharing for identical endpoint unions.

- [ ] **Step 1: Fix reserved definition/class constants in tests**

Use these exact semantic constants:

```csharp
internal const int ExactTemperatureDecisionDefinitionId = -1;
internal const int NoTemperatureDistinctionDefinitionId = 0;
internal const int FirstOptimizedDefinitionId = 1;
internal const int MissingPrimaryElementOrdinal = TemperatureDecisionBucket.BucketCount;
```

An exact fallback temperature uses definition `-1` and its decision-bucket ordinal. A missing primary element uses definition `-1`, ordinal `5002`. No-temperature-distinction uses definition `0`, ordinal `0`. Optimized snapshot definitions use positive IDs unique within that snapshot.

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
    Assert.AreEqual(2, definition.Classify(Bucket(5000)));
}
```

Add exact tests:

- `Create_WhenEndpointsAreUnsortedAndDuplicated_NormalizesThem`
- `Create_WhenEndpointIsZero_SeparatesUnderflowFromZero`
- `Create_WhenEndpointIs5000_Separates4999FromOverflow`
- `Create_WhenNoEndpoints_ReturnsNoTemperatureDistinction`
- `Classify_WhenInputIsEveryDecisionBucket_ReturnsMonotonicOrdinals`
- `TemperatureEligibilityClassKey_WhenOrdinalsMatchButDefinitionsDiffer_IsNotEqual`
- `TemperatureEligibilityClassKey_WhenDefinitionAndOrdinalMatch_IsEqual`
- `ExactFallback_WhenPrimaryElementIsMissing_UsesDedicatedMissingOrdinal`
- `PickupTagIdentity_WhenHashesMatchButPrefabTagsDiffer_IsNotEqual`

- [ ] **Step 3: Write equivalence and minimal-fragmentation proof tests**

For constraint set `[10,20)` and `[30,40)`, iterate every pair of the 5,002 decision buckets:

- if partition classes are equal, direct allow/deny vectors across both constraints must be identical;
- if direct vectors are identical and no endpoint lies between the buckets, their partition classes must be equal.

Implement the allow/deny vector in the independent reference model, not via `TemperaturePartitionDefinition`.

- [ ] **Step 4: Write structural-sharing catalog tests**

Add tests that identical normalized endpoint unions reuse one definition instance/ID, different endpoint unions receive different positive IDs, and unions across applicable tags include each endpoint once without mutating per-tag definitions.

- [ ] **Step 5: Run red**

Expected: missing partition/key/catalog types.

- [ ] **Step 6: Implement upper-bound classification**

Classification ordinal equals the number of endpoints less than or equal to the decision temperature. Treat underflow as below zero and overflow as at/above 5000. Use binary upper-bound search; do not linearly scan endpoints in the per-pickup path.

Create copies of endpoint inputs. Assign positive IDs deterministically in first-normalized-definition encounter order inside one catalog build.

- [ ] **Step 7: Implement structural union caching**

The catalog stores immutable endpoints by `(parentWorldId, requestedTag)`. `CreatePartitionForApplicableTags` merges sorted arrays into one normalized endpoint sequence, then interns that exact sequence to a shared definition. It never unions endpoints from a different parent world.

- [ ] **Step 8: Run green and exhaustive proof tests**

Expected: every boundary, equality, structural sharing, and 5,002-class proof test PASS.

- [ ] **Step 9: Prepare and commit**

Use the six task paths, allowed type `perf`, and exact subject:

```text
perf: Define scoped pickup temperature partitions
```

---

### Task 11: Combined Fetch Eligibility Builder and Version-Validated Publication

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/FetchRequestTopologyVersion.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/FetchRequestTopologyTracker.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/FetchTemperatureEligibilitySnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/FetchTemperatureEligibilityBuilder.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperatureGameSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FetchRequestTopologyTrackerTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FetchTemperatureEligibilityBuilderTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperatureGameSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceTemperatureEligibility.cs`

**Interfaces:**
- Consumes: active constraints, component lookup generation, world topology, interval sets, and partition catalog.
- Produces: one immutable storage-plus-pickup snapshot stamped with all four validity dimensions and session-side rejection of stale candidates.

- [ ] **Step 1: Write failing topology-version tests**

Test initial version, one increment per effective change, monotonic ordering, and checked exhaustion. Repeated calls represent actual topology events and always increment; event adapters are responsible for suppressing known no-op callbacks.

- [ ] **Step 2: Write failing builder behavior tests**

Include:

```csharp
[TestMethod]
public void AddFetchRequest_WhenTagHasConstrainedAndUnconstrainedDestinations_StorageAllowsEveryButPickupKeepsConstraintEndpoints()
{
    var builder = BeginBuilder();
    var iron = new Tag("Iron");
    builder.AddFetchRequest(1, [iron], true, Constraint(10, 20));
    builder.AddFetchRequest(1, [iron], false, default);

    var snapshot = builder.Build();

    Assert.IsTrue(snapshot.TryGetStorageEligibility(1, iron, out var storage));
    Assert.IsTrue(storage.AllowsEveryTemperature);
    CollectionAssert.AreEqual(
        new[] { 10, 20 },
        snapshot.CreatePartitionForApplicableTags(1, [iron])
            .SortedDecisionEndpointsKelvin.ToArray());
}
```

Add exact tests:

- `AddFetchRequest_WhenConstraintIsDisabled_ContributesUnconstrainedStorageAndNoEndpoints`
- `AddFetchRequest_WhenConstraintIsEmpty_ContributesAllowsNoneAndNoEndpoints`
- `AddFetchRequest_WhenConstraintIsNonEmpty_ContributesIntervalAndBothEndpoints`
- `AddFetchRequest_WhenTagsRepeat_DeduplicatesPerChore`
- `AddFetchRequest_WhenSameTagExistsInDifferentParents_DoesNotCrossContaminate`
- `Build_WhenNoFetchRequests_PublishesCompleteEmptySnapshot`
- `Build_WhenCalledBeforeBegin_ThrowsInvalidOperationException`
- `Build_WhenCalledTwice_ThrowsInvalidOperationException`
- `Discard_WhenEnumerationThrows_DropsAllCandidateReferences`
- `Builder_WhenPriorEntryCountExceedsHighWater_ReplacesMutableMaps`
- `Snapshot_WhenBuilderIsReused_RemainsImmutable`
- `CreatePartitionForApplicableTags_WhenPickupMatchesSeveralTags_UnionsEveryApplicableEndpoint`
- `CreatePartitionForApplicableTags_WhenPickupMatchesNoRequestedTag_ReturnsNoTemperatureDistinction`

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

Build storage interval sets and the partition catalog from the same fully traversed entry set, then stamp one snapshot with captured generations/versions.

- [ ] **Step 6: Extend the game session with fetch services**

Add `FetchRequestTopology`, an atomically read current `FetchTemperatureEligibilitySnapshot`, and `TryPublishFetchTemperatureEligibility`. Registration changes that alter effective constraints call `RecordEffectiveChange` after registry/index mutation completes. World add/remove/reparent session methods update world topology and inventory first, then record one fetch topology change after both locks are released.

Also expose the current `WorldInventoryCollectionGeneration`. Increment it on an enabled-count transition from zero to nonzero, keep it unchanged for constraint edits while the enabled count remains nonzero because fixed decision-bucket inventory is constraint-independent, clear temperature inventory on a nonzero-to-zero transition, and increment again on the next zero-to-nonzero transition. A world added while active must establish the proof appropriate to the selected inventory implementation—complete-world publication for the Klei inventory update path, or coverage plus required present-tag series for the FastTrack inventory update path—before its parent/tag becomes complete. While the enabled count is zero, inventory adapters must decline to open an accumulator/builder session.

Candidate publication captures the current active constraint snapshot and current world snapshot once, compares every stamp, and uses `Volatile.Write` only after all comparisons pass.

- [ ] **Step 7: Add deterministic combined reference comparison**

Using seed `0xFE7C4`, generate 2,000 topologies with `1..8` parents, `1..32` tags, and `0..256` fetch requests. For all 5,002 decision buckets:

- compare storage interval results with direct “any destination allows” evaluation;
- compare partition equivalence vectors with every relevant constrained destination;
- assert unrelated parent/tag endpoints never appear; and
- assert multi-tag unions contain exactly the endpoints from matched requested tags.

- [ ] **Step 8: Run all affected tests green**

Expected: tracker, builder, session, and reference tests PASS. Verify candidate build performs no Unity call and publication never merges candidate dictionaries into live state.

- [ ] **Step 9: Prepare and commit**

Use the nine task paths, allowed type `perf`, and exact subject:

```text
perf: Build combined fetch temperature eligibility
```

---

### Task 12: Per-Update Pickup Grouping Session and Exact Fallback

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/PickupTemperatureGroupingSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/PickupTemperatureGroupingSessionTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/CrossDomainTemperatureEligibilityTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceTemperatureEligibility.cs`

**Interfaces:**
- Consumes: one captured game session, active constraint snapshot, optional combined snapshot, resolved optional parent world, and applicable requested tags.
- Produces: stable full classification per pickup for one update, exact fallback for every unverifiable state, and bounded reusable per-update dictionaries.

- [ ] **Step 1: Amend the final session signatures before writing tests**

Use these exact signatures, replacing the earlier abbreviated declaration in the contract registry:

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

- [ ] **Step 3: Write exhaustive cross-domain correctness invariant**

For each generated `(parent, PickupTagIdentity, applicable tags)` and each pair of the 5,002 decision buckets, evaluate every relevant destination constraint directly. Assert:

```text
same TemperatureEligibilityClassKey
    => identical allow/deny result for every relevant constraint

identical result vector with no intervening relevant endpoint
    => same optimized TemperatureEligibilityClassKey
```

Run the property once with a current optimized snapshot and once with a stale snapshot. Under stale fallback, only identical exact buckets may share a temperature class.

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

Do not re-read the host or a snapshot during `Classify`. Cache the full `TemperatureEligibilityClassKey` by pickup instance ID and cache applicable-tag endpoint unions by the complete `PickupTagIdentity` plus an immutable normalized applicable-tag key.

- [ ] **Step 7: Implement cleanup and high-water replacement**

`Complete` and `Discard` clear captured session/snapshot references. If the pickup-classification dictionary exceeded `MaximumRetainedPickupClassificationCount`, replace it. Never retain a `Pickupable`, `GameObject`, `Navigator`, or Unity component in the pure grouping session.

- [ ] **Step 8: Run green and the exhaustive suite**

Expected: grouping and cross-domain tests PASS. Search the grouping implementation and verify it contains no `TemperatureLimit.getTemperatureIndexData`, global endpoint list, Unity type, or `ClusterManager` call.

- [ ] **Step 9: Prepare and commit**

Use the four task paths, allowed type `perf`, and exact subject:

```text
perf: Add exact fallback pickup grouping
```

---

### Task 13: Collision-Free FastTrack Grouping-Key Allocation

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FastTrack/FastTrackPickupGroupingKeyAllocator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FastTrackPickupGroupingKeyAllocatorTests.cs`

**Interfaces:**
- Consumes: complete `TemperatureEligibilityClassKey` values from Task 12.
- Produces: update-local one-to-one mapping from `(originalTagBitsHash, temperatureClass)` to FastTrack's required integer key.

- [ ] **Step 1: Write failing allocation tests**

Include:

```csharp
[TestMethod]
public void GetOrAllocate_WhenOriginalHashMatchesButTemperatureClassDiffers_ReturnsDifferentIntegers()
{
    var allocator = new FastTrackPickupGroupingKeyAllocator();
    allocator.Begin(temperatureGroupingIsActive: true);

    var first = allocator.GetOrAllocate(123, new TemperatureEligibilityClassKey(7, 1));
    var second = allocator.GetOrAllocate(123, new TemperatureEligibilityClassKey(7, 2));

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

Use an internal constructor that starts the next allocated integer at `int.MaxValue` for the exhaustion test. Normal production allocation starts at zero; because every candidate uses the allocator while active, allocated values cannot collide with an unallocated raw key in that update.

- [ ] **Step 2: Add deterministic uniqueness stress**

Generate 100,000 composite inputs deliberately reusing only 16 original hashes and 257 class keys. Maintain a reference dictionary and assert equality iff the full composite is equal.

- [ ] **Step 3: Run red**

Expected: missing allocator.

- [ ] **Step 4: Implement checked sequential allocation**

Use a dictionary keyed by an immutable composite struct. Allocate a new integer with checked increment only for unseen composites. Do not hash-mix the original value, temperature ordinal, or definition ID into the returned key.

Inactive mode returns the exact original hash and must leave retained entry count at zero.

- [ ] **Step 5: Run green and inspect the old collision expression boundary**

Expected: all allocator tests PASS. The new domain directory must not contain `(num << 6)`, `(num << 16)`, SDBM commentary, or “extremely unlikely collision” reasoning.

- [ ] **Step 6: Prepare and commit**

Use the two task paths, allowed type `perf`, and exact subject:

```text
perf: Allocate collision-free FastTrack pickup keys
```

---

### Task 14: Patch Target and Unique-Anchor Contract Verification

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/PatchContractViolationException.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/PatchContractVerifier.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: reflection metadata and adapter-supplied instruction predicates; it has no Harmony or game compile-time dependency.
- Produces: exact method signature verification and zero/one/multiple anchor enforcement for every later adapter.

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
var method = PatchContractVerifier.RequireInstanceMethod(
    typeof(MethodFixture),
    "Target",
    typeof(bool),
    [typeof(int), typeof(string)]);
```

- [ ] **Step 2: Write failing unique-anchor tests**

Use a small list of test instruction labels:

```csharp
[TestMethod]
public void RequireSingleMatch_WhenTwoInstructionsMatch_ThrowsWithMatchCount()
{
    var exception = Assert.ThrowsException<PatchContractViolationException>(() =>
        PatchContractVerifier.RequireSingleMatch(
            ["load", "anchor", "anchor", "return"],
            instruction => instruction == "anchor",
            "Fixture.Target anchor"));

    StringAssert.Contains(exception.Message, "2");
    StringAssert.Contains(exception.Message, "Fixture.Target anchor");
}
```

Add zero, one, and predicate-exception cases. Predicate failure must be wrapped with the contract name and original exception as `InnerException`.

- [ ] **Step 3: Run red**

Expected: missing verifier and exception.

- [ ] **Step 4: Implement reflection-only verification**

`RequireInstanceMethod` examines declared methods with explicit public/nonpublic instance binding flags, exact method name, exact return type, exact ordered parameter types, and non-generic status. It never selects “first method by name.”

`RequireSingleMatch<T>` makes one pass, records match count/index, and throws unless exactly one matches. It does not interpret `ToString()` as a semantic signature; later Harmony predicates inspect opcode and typed `MethodInfo`/`FieldInfo` operands.

- [ ] **Step 5: Run tests green and validate linked test source**

Expected: `PatchContractVerifierTests` PASS under the approved test-project links. Verify the two production files import only `System`, `System.Collections.Generic`, and `System.Reflection` namespaces actually required.

- [ ] **Step 6: Prepare and commit**

Use the three task paths, allowed type `refactor`, and exact subject:

```text
refactor: Verify Harmony patch contracts explicitly
```

---

### Task 15: Inactive Game and World Lifecycle Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/DeliveryTemperatureLifecyclePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/WorldParentTopologyPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperatureGameSession.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperatureGameSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: verified installed methods `Game.OnLoadLevel()`, `Game.DestroyInstances()`, `ClusterManager.RegisterWorldContainer(WorldContainer)`, `ClusterManager.UnregisterWorldContainer(WorldContainer)`, and `WorldContainer.SetParentIdx(int)`.
- Produces: manual-patch methods for order-independent session start, two-phase shutdown, world add/remove/reparent, and one session operation per effective world change.

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
apply returned WorldParentTopologyChange to inventory
release inventory lock
record one fetch topology change if HasChanged
```

Reject calls after `StopAcceptingPublications`. Add comments explaining why old/new parent invalidation is based on the returned change rather than a fresh mutable world lookup.

- [ ] **Step 4: Write and run failing lifecycle target-contract tests**

Using reflection fixtures shaped like the installed game, add exact tests for `Game.OnLoadLevel()`, `Game.DestroyInstances()`, `ClusterManager.RegisterWorldContainer(WorldContainer)`, `ClusterManager.UnregisterWorldContainer(WorldContainer)`, and `WorldContainer.SetParentIdx(int)`. Each test asserts full declaring type, static/instance form, return type, and parameter types. Mutations with an overload-only match or changed return type must fail with `PatchContractViolationException`.

Run `PatchContractVerifierTests`. Expected: the new lifecycle target-resolution assertions fail because the adapter resolution methods do not exist.

- [ ] **Step 5: Implement inactive lifecycle adapter methods**

Do **not** add `[HarmonyPatch]`, `[HarmonyPrefix]`, `[HarmonyPostfix]`, or `[HarmonyFinalizer]` attributes in Gate C files. Do not call these adapters from `Mod` yet.

Provide methods with manual-patch-compatible signatures:

```csharp
internal static void GameOnLoadLevelPrefix(Game __instance);
internal static void GameDestroyInstancesPrefix(
    Game __instance,
    out DeliveryTemperatureGameSession __state);
internal static Exception GameDestroyInstancesFinalizer(
    Exception __exception,
    DeliveryTemperatureGameSession __state);
```

The prefix detaches/stops before ONI destroys objects. The finalizer completes release and returns the original exception unchanged.

- [ ] **Step 6: Implement inactive world topology adapter methods**

Provide:

```csharp
internal static void RegisterWorldContainerPostfix(WorldContainer worldContainer);
internal static void UnregisterWorldContainerPrefix(WorldContainer worldContainer);
internal static void SetParentIdxPostfix(WorldContainer __instance);
```

Read `id` and resulting `ParentWorldId` only on the main thread, then pass integers to the session. Unknown/invalid world IDs produce one rate-limited diagnostic and no guessed mapping.

- [ ] **Step 7: Add target-resolution methods using `PatchContractVerifier`**

Each adapter exposes an `internal static MethodInfo Resolve...Target()` with the exact declaring type, return type, and parameter list. Do not use name-only `AccessTools.Method` resolution.

- [ ] **Step 8: Run domain, patch-contract, and production build tests**

Run `DeliveryTemperatureGameSessionTests`, `PatchContractVerifierTests`, then the focused pipeline build command as separate commands.

Expected: PASS/build success. Do not install the build. Inspect the built assembly metadata or source to confirm these new classes contain no Harmony patch-discovery attributes.

- [ ] **Step 9: Prepare and commit**

Use the five task paths, allowed type `perf`, and exact subject:

```text
perf: Add session and world lifecycle adapters
```

---

### Task 16: Inactive Klei World Inventory and Status Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/WorldInventory/StatusTemperatureAvailability.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/KleiWorldInventoryTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/StatusAvailabilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/StatusTemperatureAvailabilityTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/WorldTemperatureInventoryCatalogTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: complete-world builder/catalog contract, current collection generation, component constraints, and topology snapshot.
- Produces: exception-safe Klei `WorldInventory.Update` enumeration bracketing and exact status `fetchable` replacement, still inactive until Gate D.

- [ ] **Step 1: Write failing status formula characterization tests**

Use the exact current behavior:

```csharp
[TestMethod]
public void CalculateFetchable_WhenEligibleTotalAndRemainingAreKnown_PreservesExistingFormula()
{
    Assert.AreEqual(
        14.0f,
        StatusTemperatureAvailability.CalculateFetchable(
            eligibleTotal: 7.0f,
            remaining: 20.0f));
    Assert.AreEqual(
        10.0f,
        StatusTemperatureAvailability.CalculateFetchable(
            eligibleTotal: 7.0f,
            remaining: 3.0f));
}
```

Add:

- `ShouldTryReplacement_WhenOriginalStorageAndFetchableAreBelowMinimum_ReturnsFalse`
- `ShouldTryReplacement_WhenOriginalAmountsMeetMinimum_ReturnsTrue`
- `CalculateFetchable_WhenEligibleTotalIsZero_ReturnsZero`
- `CalculateFetchable_WhenRemainingIsNegative_PreservesMathfMinEquivalent` (characterize actual accepted inputs; do not clamp silently).

- [ ] **Step 2: Add catalog-to-status behavior tests**

Test the adapter decision through a pure method taking catalog query result:

- disabled/no component leaves the original `fetchable` unchanged;
- incomplete catalog leaves it unchanged;
- complete catalog applies exact formula;
- enabled-empty constraint applies complete zero without scanning;
- original early-insufficient condition avoids catalog query.

- [ ] **Step 3: Run red**

Expected: missing formula/decision type.

- [ ] **Step 4: Implement the pure status calculation**

Keep it free of Unity and `Mathf`; `Math.Min(float,float)` preserves the required arithmetic. The Harmony hook passes by reference only after the pure method reports a complete replacement.

- [ ] **Step 5: Write and run failing Klei inventory/status patch-contract tests**

Add emitted/captured instruction fixtures for the installed `WorldInventory.Update` and `FetchListStatusItemUpdater.Render200ms` shapes. Require unique semantic anchors for tag start, filtered pickup contribution, tag completion, status early-insufficient branch, and the exact `fetchable` assignment point. Add zero-anchor, duplicate-anchor, wrong-`TotalAmount` getter, and reordered-status-branch mutations.

Run `PatchContractVerifierTests`. Expected: new assertions fail because the Klei inventory/status target and transpiler contract methods are absent.

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

When the catalog returns `false`, leave the incoming `fetchable` exactly unchanged. When true, assign the characterized formula. Do not enumerate `ClusterManager.Instance.WorldContainers`.

- [ ] **Step 8: Keep status installation conditional by construction**

These adapter methods contain no option lookup in per-update code. Task 23's installer omits all Klei and FastTrack inventory/status patches when `Options.Instance.CheckTemperatureForStatusItems` is false.

- [ ] **Step 9: Run focused tests and production build**

Run status tests, catalog tests, and `PatchContractVerifierTests` separately before the pipeline build. Expected: tests pass and pipeline build succeeds. Do not install. Verify new adapters have no patch-discovery attributes, no static world/tag amount dictionary, no FastTrack reflection, and no ambiguous content-mode terminology.

- [ ] **Step 10: Prepare and commit**

Use the six task paths, allowed type `perf`, and exact subject:

```text
perf: Add sparse status inventory adapters
```

---

### Task 17: Inactive Authoritative Fetch Traversal and Sweep Eligibility Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/ClearableTemperatureEligibility.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/FetchTemperatureEligibilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/ClearableTemperatureEligibilityTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FetchTemperatureEligibilityBuilderTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: `GlobalChoreProvider.fetchMap` traversal, combined builder/snapshot, component index, topology snapshot, and fetch topology tracker.
- Produces: one exception-safe combined snapshot candidate per authoritative traversal, topology version hooks, and conservative sweep decisions; inactive until Gate D.

- [ ] **Step 1: Write failing conservative sweep-decision tests**

Pin exact decision order:

```csharp
[TestMethod]
public void Evaluate_WhenOriginalResultIsTrueButSnapshotIsStale_ReturnsFalse()
{
    Assert.IsFalse(ClearableTemperatureEligibility.Evaluate(
        originalHasDestination: true,
        hasPrimaryElement: true,
        hasCurrentEligibility: false,
        currentEligibilityAllowsPickup: false));
}
```

Add:

- `Evaluate_WhenOriginalResultIsFalse_RemainsFalse`
- `Evaluate_WhenNoEnabledConstraints_ReturnsOriginalResult`
- `Evaluate_WhenPrimaryElementIsMissing_ReturnsFalse`
- `Evaluate_WhenTopologyIsUnresolved_ReturnsFalse`
- `Evaluate_WhenCurrentIntervalsAllowBucket_ReturnsTrue`
- `Evaluate_WhenCurrentIntervalsRejectBucket_ReturnsFalse`

Make parameters semantic—prefer a small immutable input value over four ambiguous booleans in production.

- [ ] **Step 2: Run red**

Expected: missing sweep decision type.

- [ ] **Step 3: Write failing authoritative-traversal and event target-contract tests**

Add exact reflection/instruction fixtures for all three topology event methods, `GlobalChoreProvider.UpdateStorageFetchableBits`, and `GlobalChoreProvider.ClearableHasDestination`. Require unique typed anchors for parent-section start and selected `FetchChore` traversal. Add mutations for overload ambiguity, a second fetch-map traversal anchor, a missing selected-chore anchor, and a changed `OnTagsChanged` parameter.

- [ ] **Step 4: Run domain and patch-contract tests red**

Run `ClearableTemperatureEligibilityTests` and `PatchContractVerifierTests` separately. Expected: the pure decision type and inactive adapter contract entry points are missing.

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

For each chore, copy/read requested tags during the main-thread traversal, resolve the destination GameObject ID and immutable constraint through the component index, and call `AddFetchRequest`. Postfix builds and attempts one combined publication. Finalizer discards on exception or after a rejected candidate.

Do not traverse `fetchMap` a second time for pickup partitions.

- [ ] **Step 7: Implement inactive `ClearableHasDestination` postfix**

Capture the session and snapshots once. Preserve original false and zero-active bypass. Missing primary, unresolved parent, missing tag, or any stale version produces conservative false. Current interval membership uses one canonical `TemperatureDecisionBucket`.

- [ ] **Step 8: Verify target and anchor contracts**

Resolve exact installed signatures. Require each semantic insertion count explicitly; a zero or duplicate match throws `PatchContractViolationException`. No anchor may depend solely on local number or `operand.ToString()` text.

- [ ] **Step 9: Run focused tests and production build**

Run clearable tests, builder tests, and `PatchContractVerifierTests` separately. Expected: tests pass and pipeline build succeeds. Do not install. Verify new code has no `HashSet<Tag>[]`, global band, or synchronous rebuild in setter/event hooks.

- [ ] **Step 10: Prepare and commit**

Use the five task paths, allowed type `perf`, and exact subject:

```text
perf: Capture authoritative fetch temperature eligibility
```

---

### Task 18: Inactive Klei Pickup Grouping Adapter

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/ThreadConfinedSessionSlot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/KleiPickupTemperatureGroupingPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/ThreadConfinedSessionSlotTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/FetchTemperatureEligibilitySnapshot.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FetchTemperatureEligibilityBuilderTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/PickupTemperatureGroupingSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: `FetchManager.FetchablesByPrefabId.UpdatePickups(Navigator,int)`, its private comparator, current combined snapshot, requested tags by parent, and pickup grouping session.
- Produces: one capture-once update context, applicable-tag union once per base identity, comparator/suppression agreement, and exception-safe nested state; inactive until Gate D.

- [ ] **Step 1: Add requested-tag enumeration to the snapshot contract**

Implement:

```csharp
internal IReadOnlyList<Tag> GetRequestedTags(int parentWorldId);
```

Return a deterministic immutable list of tags present in that parent section. Unknown parent returns an empty immutable list. This allows the adapter to evaluate `KPrefabID.HasTag` once per distinct `PickupTagIdentity`, cache applicable tags, and avoid scanning requested tags for every pickup.

- [ ] **Step 2: Write failing thread-confined nesting tests**

`ThreadConfinedSessionSlot<T>` supports explicit push/pop tokens:

- `Enter_WhenEmpty_SetsCurrent`
- `Enter_WhenNested_SavesPreviousAndSetsNested`
- `Exit_WhenNested_RestoresPrevious`
- `Exit_WhenTokenIsStale_ThrowsLifecycleViolation`
- `DiscardAll_AfterException_ClearsReferences`
- `Enter_WhenGameSessionGenerationChanges_DiscardsOldThreadStateBeforeUse`

Do not use `AsyncLocal`; FastTrack/ONI worker identity is thread-based and the runtime target is `net48`.

- [ ] **Step 3: Add applicable-tag caching tests**

Extend grouping tests to count the callback used to identify applicable requested tags. For 10,000 pickups with the same complete `PickupTagIdentity`, assert it runs once in the update. When `PrefabTag` differs despite equal original hash, assert it runs separately.

- [ ] **Step 4: Run red**

Expected: missing slot and requested-tag API.

- [ ] **Step 5: Write and run failing Klei pickup-path patch-contract tests**

Add exact fixtures for `FetchManager.FetchablesByPrefabId.UpdatePickups(Navigator,int)`, private `PickupComparerIncludingPriority.Compare`, and duplicate suppression. Require one comparator extension anchor and one suppression extension anchor that both consume the same full semantic key. Add mutations for changed candidate type, duplicate compare anchor, missing suppression anchor, and an installed method shape that would require an unverified Unity/native call from a worker.

Run `PatchContractVerifierTests`. Expected: new Klei pickup target/anchor assertions fail because the adapter entry points are absent.

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

Preserve every original comparator field/order. After original equality through priority/tag grouping, compare `PartitionDefinitionId`, then `IntervalOrdinal`. Duplicate suppression uses the same cached full key under the same update context.

Missing/stale/unresolved cases classify through exact buckets; zero enabled constraints add no temperature comparison.

- [ ] **Step 9: Verify target/anchor contracts and build**

Require exact signatures for `UpdatePickups` and private `PickupComparerIncludingPriority.Compare`. Require unique structural anchors for the comparator insertion and suppression insertion. Run affected domain tests, then pipeline build. Do not install.

- [ ] **Step 10: Prepare and commit**

Use the seven task paths, allowed type `perf`, and exact subject:

```text
perf: Add scoped Klei pickup grouping adapter
```

---

### Task 19: Inactive Direct Eligibility and Fetch-Coalescing Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FetchEligibility/FetchChoreConstraintCompatibility.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/DirectFetchTemperatureEligibilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FetchChoreConstraintCompatibilityTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

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

Run `PatchContractVerifierTests`. Expected: new direct-adapter contract tests fail because target/anchor methods are absent.

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

Run containment tests and `PatchContractVerifierTests` separately. Expected: tests pass and pipeline build succeeds. Do not install. Review generated code paths for boxing, LINQ, tuples allocated on the heap, or repeated component lookup.

- [ ] **Step 8: Prepare and commit**

Use the four task paths, allowed type `perf`, and exact subject:

```text
perf: Centralize direct fetch temperature checks
```

---

### Task 20: FastTrack Feature Compatibility Inspection

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrack/FastTrackFeatureCompatibilityState.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrack/FastTrackCompatibilityReport.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrack/FastTrackCompatibilityInspector.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrack/FastTrackCompatibilityInspectorTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrack/FastTrackReflectionEmitFixture.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: enabled-mod evidence supplied by `Mod.OnAllModsLoaded`, loaded assembly metadata, Harmony patch-owner snapshots converted to reflection-only descriptors, and `PatchContractVerifier`.
- Produces: one immutable compatibility report that independently classifies FastTrack world inventory, pickup grouping, and direct chore comparison as `ModNotLoaded`, `ReplacementInactive`, `Ready`, or `Incompatible`.

- [ ] **Step 1: Declare the exact reflection-only compatibility contract in failing tests**

The test-visible contract is:

```csharp
internal enum FastTrackFeature
{
    WorldInventory,
    PickupGrouping,
    DirectChoreComparison
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
    internal string? AssemblySha256 { get; }
    internal FastTrackFeatureCompatibility GetFeature(FastTrackFeature feature);
}

internal sealed class FastTrackCompatibilityInspector
{
    internal FastTrackCompatibilityReport Inspect(
        FastTrackRuntimeInspectionInput inspectionInput);
}
```

`FastTrackFeatureCompatibility` must expose the feature, state, verified reflected method handles needed by its adapter, and one semantic failure code/message only when incompatible. It must not expose Harmony types. `FastTrackRuntimeInspectionInput` contains the enabled-for-active-content evidence, optional assembly, and immutable reflected active-patch descriptors prepared by the installer.

Add exact tests:

- `Inspect_WhenFastTrackModIsNotLoaded_ClassifiesEveryFeatureAsModNotLoaded`
- `Inspect_WhenAssemblyIsLoadedButWorldInventoryReplacementIsInactive_ClassifiesWorldInventoryAsReplacementInactive`
- `Inspect_WhenAssemblyIsLoadedButPickupPrefixIsNotActive_ClassifiesPickupGroupingAsReplacementInactive`
- `Inspect_WhenFeaturesHaveDifferentActivationStates_ClassifiesEachIndependently`
- `GetFeature_WhenFeatureValueIsUnknown_ThrowsArgumentOutOfRangeException`

- [ ] **Step 2: Build emitted FastTrack contract fixtures without adding a dependency**

`FastTrackReflectionEmitFixture` uses `System.Reflection.Emit` to create an in-memory assembly with the exact expected full type names, private fields, method names, signatures, and minimal typed IL shapes. Provide explicit fixture methods that remove one field, change one signature, duplicate one semantic anchor, or change `PickupTagKey.Equals` semantics.

Do not compile source text with an external compiler and do not add a mock package. Fixture method names must state the mutation, for example `CreateWithRunUpdateMissingSingleTagBranch`. Active-patch descriptors identify the exact emitted prefix method, target method, Harmony owner string, and priority using reflection-only values.

- [ ] **Step 3: Run compatibility tests red**

Run `FastTrackCompatibilityInspectorTests`.

Expected: missing report/inspector/state types. If fixture construction fails first, correct the fixture until the intended missing-production-type failure is reached.

- [ ] **Step 4: Implement exact assembly and feature activation inspection**

The inspector must verify, by full name and exact signature, the installed equivalents of at least:

- `PeterHan.FastTrack.UIPatches.BackgroundWorldInventory.RunUpdate`;
- `BackgroundWorldInventory.SumTotal`;
- the fields that distinguish the first complete update and identify `WorldInventory`/`WorldContainer`;
- the active FastTrack prefix replacing `WorldInventory.Update`;
- `PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate.BeforeUpdatePickups`;
- nested `PickupTagDict.AddItem` and `PickupTagKey` constructor/equality shape; and
- FastTrack's direct chore-comparison target used by the current mod.

Treat the installed assembly and actual active Harmony ownership as authoritative. Current upstream source is evidence for expected semantics, not permission to accept a different installed body. Assembly presence alone can produce only `ReplacementInactive`, never `Ready`.

The world-inventory `Ready` contract must prove the two behavioral branches: first update iterates all inventory entries; later updates select one entry through `updateIndex`. It must also prove that removing a pickupable does not remove the dictionary key. If the latter cannot be proved, classify world inventory as `Incompatible` because a one-time coverage set could become false.

- [ ] **Step 5: Write and run mutation tests for every required contract**

Add exact tests:

- `Inspect_WhenWorldInventoryRunUpdateSignatureChanges_ClassifiesOnlyWorldInventoryAsIncompatible`
- `Inspect_WhenRunUpdateNoLongerHasCompleteAndSingleTagBranches_ClassifiesWorldInventoryAsIncompatible`
- `Inspect_WhenRemovedFetchableCanDeleteTagKey_ClassifiesWorldInventoryAsIncompatible`
- `Inspect_WhenPickupTagKeyEqualityUsesMoreThanAllocatedHash_ClassifiesPickupGroupingAsIncompatibleUntilAdapterIsRedesigned`
- `Inspect_WhenAddItemConstructorAnchorIsMissing_ClassifiesPickupGroupingAsIncompatible`
- `Inspect_WhenAddItemConstructorAnchorIsDuplicated_ClassifiesPickupGroupingAsIncompatible`
- `Inspect_WhenHarmonyOwnerDoesNotMatchFastTrack_ClassifiesReplacementAsInactiveRatherThanClaimingReady`
- `Inspect_WhenDirectComparatorContractChanges_ClassifiesOnlyDirectChoreComparisonAsIncompatible`

Expected: each mutation affects only the named feature. A broad catch that marks every feature incompatible is not acceptable.

- [ ] **Step 6: Compute and cache diagnostic identity once**

For an enabled assembly, read `AssemblyName.FullName`, `AssemblyName.Version`, and the SHA-256 digest of the assembly file once during inspection. Use the .NET Framework 4.8-compatible `SHA256.Create().ComputeHash(stream)` pattern, dispose both objects, and normalize the digest to uppercase hexadecimal; do not use a newer BCL-only convenience API. If the assembly is dynamic or has no readable location, record a semantic `DigestUnavailable` failure for an active feature; do not recompute per update or silently omit identity from an incompatibility diagnostic.

Tests use a temporary fixture file and assert the exact digest. Dispose every stream deterministically and share the immutable report thereafter.

- [ ] **Step 7: Run green, then build the production mod**

Run `FastTrackCompatibilityInspectorTests` and `PatchContractVerifierTests` separately, then run the pipeline `build` command. Do not install.

Inspect the new production files and confirm they reference only BCL reflection/IO types plus reflection-only patch descriptors. They must not reference FastTrack, Harmony, Klei, Unity, or PLib types at compile time.

- [ ] **Step 8: Prepare and commit**

Use the six task paths, allowed type `feat`, and exact subject:

```text
feat: Verify active FastTrack feature contracts
```

---

### Task 21: Inactive FastTrack Incremental World Inventory Adapter

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FastTrack/FastTrackWorldInventoryUpdateKind.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FastTrack/FastTrackWorldInventoryPublicationResult.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/FastTrack/FastTrackWorldInventoryPublicationSession.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrack/FastTrackWorldInventoryTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FastTrackWorldInventoryPublicationSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrack/FastTrackCompatibilityInspectorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: a `Ready` world-inventory compatibility feature, current game session and collection generation, coverage-requirement query, sparse accumulator, and all three inventory publication contracts.
- Produces: exception-safe complete-world publication for FastTrack's actual first full update and exactly one single-resource-tag publication for each later update, still inactive until Gate D.

- [ ] **Step 1: Write failing publication-session mode tests**

`FastTrackWorldInventoryPublicationSession` must expose explicit `BeginCompleteWorldUpdate` and `BeginSingleResourceTagUpdate` methods; a boolean `isFull` parameter is forbidden. Add exact tests:

- `BeginCompleteWorldUpdate_WhenTwoTagsComplete_ProducesOneCompleteWorldPublication`
- `BeginSingleResourceTagUpdate_WhenOneTagCompletes_ProducesOneSeriesPublication`
- `BeginSingleResourceTagUpdate_WhenSecondTagBegins_ThrowsLifecycleViolation`
- `BeginSingleResourceTagUpdate_WhenCoverageIsRequired_ProducesCoverageAndSeries`
- `BeginSingleResourceTagUpdate_WhenCoverageIsCurrent_ProducesOnlySeries`
- `BeginSingleResourceTagUpdate_WhenInventoryHasNoTags_ProducesEmptyCoverageWithoutSeries`
- `AddPickup_WhenPrimaryElementIsMissing_DoesNotAddTemperatureAmount`
- `Complete_WhenResourceTagIsStillOpen_ThrowsLifecycleViolation`
- `Discard_AfterException_ReleasesCoverageTagsAndAccumulatorReferences`
- `Begin_WhenGameSessionGenerationChanges_DiscardsRetainedOldSessionState`

The result has three explicit optional members—complete world, coverage, and single-tag series—and validates that complete-world and single-tag members can never coexist. Do not call it `Data` or use a tuple whose positions could be interchanged.

- [ ] **Step 2: Run the publication-session tests red**

Expected: missing FastTrack update kind, publication result, and session. Confirm existing Task 7/8 publication tests remain green before implementation.

- [ ] **Step 3: Implement the minimal session over canonical domain types**

Reuse `CompleteWorldResourceTemperatureAmountsBuilder` for complete mode and `TemperatureAmountAccumulator` for single-tag mode. Coverage mode copies only the supplied dictionary keys through `WorldResourceTagCoverage.Create`; it never visits the key's pickupable set.

The session must contain no alternative constraint, temperature-bucket, or availability implementation. Comments explain why complete and incremental modes are separate and why a present coverage tag without a series remains incomplete.

- [ ] **Step 4: Run the session green and prove incremental isolation**

Add an internal diagnostic that counts completed resource tags. Assert complete mode reports two for the fixture and incremental mode reports exactly one. Mutate an unrelated tag's fixture collection and prove the incremental result does not contain or retain it.

Expected: session tests pass with no complete-world dictionary allocation in the incremental mode.

- [ ] **Step 5: Add failing installed-shape anchor tests**

Using the emitted fixtures from Task 20, require unique typed anchors for:

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
- otherwise call `TryGetWorldResourceTagCoverageRequirement` once;
- when coverage is required, enumerate `WorldInventory.Inventory.Keys` exactly once and copy only keys into the publication session; and
- enter a thread-confined nested context tied to game-session generation.

The `SumTotal` hook records the cached `PrimaryElement.Temperature` and `TotalAmount` only at FastTrack's already-filtered contribution point. A missing `PrimaryElement` is skipped, matching the characterized Klei status path. No `GetComponent`, `ClusterManager`, world enumeration, constraint lookup, or logging is permitted in that loop.

At successful postfix, publish in this order: coverage, then single-tag series; or the one complete-world publication. If coverage succeeds and a concurrent generation change rejects the series, the tag remains pending, which is safe. A finalizer always discards/restores thread state and returns the original exception. It never publishes a partially accumulated tag.

- [ ] **Step 7: Prove there is no per-update complete-world reconstruction**

Extend fixtures so `RunUpdate` executes one full branch followed by three incremental invocations. Assert publication callbacks receive one complete world and then exactly three single-tag series. Count key enumeration and pickup accumulation:

- coverage keys are enumerated once for a generation that begins after FastTrack's original full update;
- each incremental invocation accumulates only FastTrack's selected tag;
- unrelated tag series are not combined or copied; and
- a second invocation in the same generation does not repeat coverage enumeration.

This is a structural test, not a wall-clock benchmark. Also assert DeliveryTemperatureLimit installs no `BackgroundInventoryUpdater.StartUpdateAll` world-discovery prefix in the normal `Ready` path. FastTrack's own upstream per-update `WorldContainers` scan is outside this mod's scheduling ownership; this rewrite removes the mod's former duplicate setup scan and must not add another.

- [ ] **Step 8: Run focused tests and production build**

Run `FastTrackWorldInventoryPublicationSessionTests`, `FastTrackCompatibilityInspectorTests`, `WorldTemperatureInventoryCatalogTests`, and `PatchContractVerifierTests` separately. Run pipeline `build`; do not install.

Expected: all pass, and the build contains the inactive patch class without any patch-discovery attribute. Review all worker code against the captured-field rule; if safe access to the cached primary element cannot be proved for the installed ONI/FastTrack build, classify this feature incompatible and use the Task 23 fallback rather than weakening the rule.

- [ ] **Step 9: Prepare and commit**

Use the seven task paths, allowed type `perf`, and exact subject:

```text
perf: Preserve FastTrack incremental inventory updates
```

---

### Task 22: Inactive FastTrack Pickup Grouping and Direct Eligibility Adapters

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrack/FastTrackPickupTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrack/FastTrackDirectFetchTemperaturePatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrack/FastTrackPickupTemperaturePatchContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrack/FastTrackFallbackContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/FastTrackPickupGroupingKeyAllocatorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Domain/PickupTemperatureGroupingSessionTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrack/FastTrackCompatibilityInspectorTests.cs`

**Interfaces:**
- Consumes: `Ready` pickup/direct FastTrack feature reports, canonical grouping session, collision-free key allocator, direct constraint checks, and verified Klei pickup-path fallback.
- Produces: collision-free FastTrack grouping with exact lifecycle cleanup, canonical direct chore comparison, and a tested safe fallback contract; inactive until Gate D.

- [ ] **Step 1: Write failing full-key allocation integration tests**

Exercise the allocator through a pure representation of `PickupTagDict.AddItem` and add exact tests:

- `Allocate_WhenOriginalHashesDifferAndTemperatureClassMatches_ReturnsDifferentKeys`
- `Allocate_WhenOriginalHashMatchesAndTemperatureClassesDiffer_ReturnsDifferentKeys`
- `Allocate_WhenCompositeIdentityRepeats_ReturnsSameKey`
- `Allocate_WhenPrimaryElementIsMissing_UsesAReservedDistinctTemperatureClass`
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

For each candidate, form `PickupTagIdentity` from the original tag-bits hash plus verified prefab tag. Resolve applicable requested tags once per identity and cache the result for the update. Read temperature through the verified cached `PrimaryElement` reference only; never call `GetComponent` or query mutable world topology from the worker. If that cached-read safety contract is not verified, mark the adapter incompatible and route to the Klei pickup grouping path.

Every candidate passes the complete composite identity to the allocator while grouping is active, including missing-primary-element candidates. The transpiler replaces only the constructor argument; it does not modify the fetchable, candidate, `KPrefabID`, or FastTrack dictionary implementation.

- [ ] **Step 5: Implement exception-safe completion and retained-capacity release**

Postfix completes both sessions. Finalizer discards both and restores any nested prior context while preserving the original exception. After `MaximumRetainedFastTrackGroupingKeyCount` is exceeded, the allocator replaces its variable dictionary before the next session. Add an exact test with an injected limit of four.

- [ ] **Step 6: Implement the inactive direct FastTrack chore adapter**

Patch the exact installed FastTrack chore comparator target only when `DirectChoreComparison` is `Ready`. Preserve an existing false result, resolve the destination through the component index, bypass disabled/missing constraints, preserve characterized missing-primary behavior, and call `DeliveryTemperatureConstraint.Allows` once. No alternative boundary calculation, global snapshot reconstruction, or per-call reflection is permitted.

- [ ] **Step 7: Write and implement the safe pickup fallback contract**

Preferred fallback patches FastTrack's exact `BeforeUpdatePickups` guard so the guard returns `true` without executing its replacement body, causing Klei `UpdatePickups` to run. The fallback is valid only after verifying:

- the method is the active FastTrack prefix on the exact Klei target;
- forcing `true` reaches the original Klei body;
- the Klei pickup grouping adapter can be installed completely; and
- no second active prefix will still suppress or replace the original.

If guard interception is impossible, exact-prefix removal is permitted only for the verified FastTrack owner/method pair and only before gameplay starts. Never unpatch by owner wildcard and never remove another mod's patch. `FastTrackFallbackContractTests` cover wrong owner, extra replacement prefix, missing guard, duplicate guard, and successful fallback.

If neither strategy can be proved, throw `PatchContractViolationException` during coordinated activation with one rate-limited diagnostic containing feature, assembly identity, version, SHA-256, failed contract, and attempted fallback. Do not permit temperature-unaware FastTrack collapsing to run.

- [ ] **Step 8: Run focused tests and production build**

Run both new FastTrack test classes, allocator tests, grouping-session tests, and compatibility-inspector tests separately. Run pipeline `build`; do not install.

Expected: all pass. Verify there is no per-candidate reflection, option lookup, assembly lookup, logging, complete snapshot build, original-hash mutation, or unbounded dictionary retention.

- [ ] **Step 9: Prepare and commit**

Use the seven task paths, allowed type `perf`, and exact subject:

```text
perf: Add collision-free FastTrack pickup adapters
```

---

### Task 23: Coordinated Big-Bang Runtime Activation and Legacy Removal

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/InventoryImplementationPath.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/PickupGroupingImplementationPath.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Domain/Runtime/DeliveryTemperaturePatchActivationPlan.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/DeliveryTemperaturePatchInstaller.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/Patching/CodeInstructionFactory.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimit.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Mod.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Buildings.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Construction.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Widget.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SideScreen.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Options.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/Strings.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/Limits.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/Patch.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/PatchFastTrack.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/StatusItems.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/Harmony.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/DeliveryTemperaturePatchActivationPlanTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/IntentionalRuntimeContract.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ModBuildContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/PublicAssemblySurface.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/BuildingsEligibilityTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Patching/PatchContractVerifierTests.cs`

**Interfaces:**
- Consumes: every Gate A–C module and compatibility report.
- Produces: the first installable build, with exactly one canonical runtime algorithm, selected Klei/FastTrack adapters, intentional public surface, and no obsolete temperature-index subsystem.

- [ ] **Step 1: Write failing implementation-path selection tests**

`DeliveryTemperaturePatchActivationPlan.Create` takes the startup status option plus the three FastTrack feature states. Base-game versus Spaced Out content mode is deliberately not an input because adapter selection is orthogonal to content mode.

Add exact tests:

- `Create_WhenFastTrackIsNotLoaded_SelectsKleiInventoryAndPickupPaths`
- `Create_WhenFastTrackIsLoadedButReplacementsAreInactive_SelectsKleiInventoryAndPickupPaths`
- `Create_WhenFastTrackWorldInventoryIsReady_SelectsFastTrackInventoryPath`
- `Create_WhenFastTrackPickupGroupingIsReady_SelectsFastTrackPickupPath`
- `Create_WhenStatusOptionIsDisabled_SelectsNoInventoryOrStatusInstrumentation`
- `Create_WhenStatusOptionIsDisabledAndFastTrackWorldInventoryIsIncompatible_DoesNotBlockAnUnusedStatusFeature`
- `Create_WhenPickupFeatureIsIncompatibleAndKleiFallbackIsVerified_SelectsKleiPickupPath`
- `Create_WhenPickupFeatureIsIncompatibleAndFallbackIsUnverified_ThrowsPatchContractViolation`
- `Create_WhenInventoryFeatureIsIncompatibleAndKleiFallbackIsVerified_SelectsKleiInventoryPath`
- `Create_WhenInventoryFeatureIsIncompatibleAndFallbackIsUnverified_DisablesStatusReplacementAndReportsReleaseBlockingIncompatibility`
- `Create_WhenDirectComparatorIsInactive_OmitsOnlyFastTrackDirectAdapter`

Use explicit enum members `Klei` and `FastTrack`; do not use booleans or the prohibited ambiguous terminology.

- [ ] **Step 2: Run selection tests red, then implement the immutable activation plan**

Expected red: missing path and activation-plan types. Implement only the selection matrix and rerun green before editing `Mod.cs`.

The activation plan lists exact patch groups, not individual booleans scattered across the installer. Its constructor validates impossible combinations, such as selecting FastTrack inventory when the status option is disabled or when compatibility is not `Ready`.

- [ ] **Step 3: Write failing curated runtime/serialization contract tests**

Replace whole-assembly equality with `IntentionalRuntimeContract`. Assert that the only intentionally public or nested-public types declared by this assembly are:

```text
DeliveryTemperatureLimit.Mod
DeliveryTemperatureLimit.Options
DeliveryTemperatureLimit.TemperatureLimit
STRINGS.TEMPERATURELIMIT
```

Permit the existing semantically accurate `TemperatureLimit` component operations: `MinValue`, `MaxValue`, `IsDisabled`, `LowLimit`, `HighLimit`, `Get(GameObject)`, `CopySettings`, `SetLowLimit`, `SetHighLimit`, `Disable`, and `AllowedByTemperature`. Permit the required `Mod`, `Options`, and localization members with their current signatures. `TemperatureLimitWidget`, `TemperatureLimitSideScreen`, every patch class, every domain type, and every compatibility type must be internal.

Add metadata assertions that `TemperatureLimit` still contains private `int lowLimit` and `int highLimit` with both `[KSerialization.Serialize]` and `[UnityEngine.SerializeField]`. Assert the absence of nested `TemperatureIndexData` and `getTemperatureIndexData` by exact metadata name.

- [ ] **Step 4: Implement the new `TemperatureLimit` component over the game session**

Preserve serialized fields, constants, player-facing operations, callbacks, and copy-settings behavior. Normalize setters before comparison. If the normalized value is unchanged, return without registry generation change. Otherwise update the component fields and atomically replace the session registration.

`OnSpawn` obtains a `TemperatureLimitRegistration` from the current `DeliveryTemperatureGameSession`. `OnCleanUp` removes that exact token idempotently. `Get(GameObject)` resolves through `TemperatureLimitComponentIndex` using instance ID and returns `null` for no current session or no registration. No static component dictionary, all-limit list, dirty flag, lazy rebuild, or load-level collection clear is permitted.

Add comments at the serialized fields and lifecycle token explaining save identity and stale-callback rejection. Do not add a compatibility facade for removed index members.

- [ ] **Step 5: Convert UI, construction, and building callers to semantic component operations**

Update each caller to use the new component methods and canonical constraints without recreating clamp/boundary logic. Make patch classes internal. Preserve exact player-facing option/default/copy/UI behavior and existing tests. No file may call a removed global index or create a parallel lookup dictionary.

- [ ] **Step 6: Implement two-phase patch installation**

`DeliveryTemperaturePatchInstaller` first resolves and verifies every exact target, signature, typed anchor, Harmony owner, path selection, and fallback needed by the immutable activation plan. It applies nothing during this verification phase.

Before claiming that a Klei implementation path is authoritative, inspect the actual prefix topology on its target. An unrelated postfix or non-skipping observer is permitted only when its semantics do not replace the method. An unknown prefix capable of suppressing/replacing `WorldInventory.Update` or `UpdatePickups` makes Klei authority unproved; fail activation for the affected required behavior instead of treating “not FastTrack” as “Klei.” Extend `PatchContractVerifierTests` with `VerifyKleiAuthority_WhenUnknownSkippingPrefixIsActive_ReturnsFalse`, `VerifyKleiAuthority_WhenOnlyObserverPostfixIsActive_ReturnsTrue`, and `VerifyKleiAuthority_WhenFastTrackAssemblyExistsButReplacementIsInactive_ReturnsTrue`.

Only after all required contracts pass may it apply patch groups. Record every exact `(target, patch method)` installed by this mod. If application throws, remove only methods recorded for this attempt and rethrow the semantic contract violation. Never call broad `UnpatchAll`, never unpatch another owner, and never continue after a partially installed required group.

Always-on groups include lifecycle, topology, authoritative fetch snapshot, direct eligibility, construction/building/UI behavior, and selected pickup path. Status-enabled plans install the shared status hook plus exactly one inventory publication path. Status-disabled plans install neither Klei nor FastTrack inventory/status instrumentation and allocate no catalog buffers merely for status.

- [ ] **Step 7: Replace automatic discovery with explicit startup sequencing**

`Mod.OnLoad` retains PLib initialization, localization, and options registration, then installs only groups whose contracts do not depend on the complete loaded-mod topology. `Mod.OnAllModsLoaded` builds one FastTrack compatibility report from enabled-for-active-content mods and actual Harmony ownership, creates one activation plan, preverifies remaining groups, and installs them once.

Do not call blanket `PatchAll`. Guard duplicate callbacks with an installer state machine that distinguishes `NotStarted`, `Verifying`, `Installed`, and `Failed`; a second successful call is an idempotent no-op, while reentry during verification or after failure throws a diagnostic lifecycle violation.

- [ ] **Step 8: Prove the Klei/FastTrack inventory fallback is complete or release-blocking**

For an incompatible active FastTrack inventory replacement, a Klei inventory fallback is valid only if the installer can neutralize the exact FastTrack `WorldInventory.Update` replacement and its background scheduling entry point before gameplay, verify that no other replacement suppresses Klei enumeration, and install the complete Klei inventory adapter. If all conditions hold, select the Klei inventory update path.

If they do not all hold, omit the temperature status replacement so existing ONI availability remains unchanged, emit one diagnostic, and mark the activation report release-blocking. Do not combine partial FastTrack deltas with a Klei complete-world candidate, do not run two inventory enumerations, and do not claim the status option is functioning. Task 27 acceptance must fail until a supported adapter or proved fallback exists.

- [ ] **Step 9: Delete the obsolete implementation in the same change**

After every caller compiles against the new services, delete the five listed legacy files. Do not leave forwarding types, type aliases, obsolete wrappers, unused Harmony entry points, commented-out code, or conditional compilation that can restore the old global path.

Run `rg` for exact removed symbols and semantic equivalents. A match in the approved design/plan explaining removal is allowed; a production or test fixture implementation match is not.

- [ ] **Step 10: Run focused activation and build-contract tests**

Run, separately:

- `DeliveryTemperaturePatchActivationPlanTests`;
- `DeliveryTemperatureGameSessionTests`;
- `TemperatureLimitComponentIndexTests`;
- `BuildingsEligibilityTests`;
- `ModBuildContractTests`;
- both FastTrack contract classes; and
- `PatchContractVerifierTests`.

Then run pipeline `build`. This is the first build permitted to be installed later, but do not install it yet. Expected: all focused tests pass, the merged DLL contains only the curated public contract, and no obsolete source is compiled.

- [ ] **Step 11: Review the big-bang boundary before commit**

Inspect every task path and confirm:

- every active patch reads the new session/domain services;
- no build can execute the old and new eligibility models together;
- the status-off selection installs no inventory instrumentation;
- Klei implementation paths contain no FastTrack compatibility work;
- content mode is not used to select implementation path;
- comments document every non-obvious ownership and fallback invariant; and
- all deleted responsibilities have a named new owner.

- [ ] **Step 12: Prepare and commit**

Use every exact path listed by this task, allowed types `feat` and `refactor`, and exact subject:

```text
feat: Activate scoped delivery temperature runtime
```

---

### Task 24: Exhaustive Architecture, Correctness, and Performance-Shape Contracts

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Architecture/NoShimArchitectureContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Architecture/ImplementationTerminologyContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Architecture/PerformanceArchitectureContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/Domain/CrossDomainTemperatureEligibilityTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceWorldTemperatureInventory.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ReferenceModels/ReferenceTemperatureEligibility.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/ModBuildContractTests.cs`

**Interfaces:**
- Consumes: the fully activated Gate D implementation.
- Produces: automated proof that removed architecture cannot silently return, all canonical representations agree over 5,002 classes, and hot-path structural invariants remain enforced.

- [ ] **Step 1: Write and run the no-shim architecture tests red**

Scan production syntax/metadata, not documentation prose, and assert absence of:

- `TemperatureIndexData` and `getTemperatureIndexData`;
- `allLimits`, `limitsDirty`, and `UpdateIndexes`;
- `storageFetchableTagsPerTemperatureIndex`;
- a dense `(Tag, temperature index)` status dictionary;
- the deleted patch/status type names;
- a public domain/patch/FastTrack type outside `IntentionalRuntimeContract`; and
- any type forwarding, `[Obsolete]` forwarding member, alias, wrapper, or facade preserving a removed symbol.

Expected red should identify any residue left by Task 23. Remove residue in the relevant production owner; do not weaken the test with an allowlist unless the user approves a full shim-exception dossier.

- [ ] **Step 2: Enforce semantic terminology mechanically**

`ImplementationTerminologyContractTests` scans production `.cs`, test `.cs`, acceptance-check IDs/titles/actions/expected text, and commit-subject constants in this plan's implementation section. It rejects the unqualified ambiguous word prohibited by Global Constraints while allowing the specification/plan sentences that explicitly define or ban that word.

Also reject type identifiers containing `NonVanilla` or beginning with the ambiguous term. The failure reports exact file and line. Do not scan `.git`, generated artifacts, third-party DLLs, or user-owned `screenshot-guidance.md`.

- [ ] **Step 3: Write performance-shape metadata and diagnostic tests**

Add exact tests:

- `ConstraintReadPath_WhenInspected_DoesNotCallSortDistinctOrRegistryRebuild`
- `StatusQueryPath_WhenInspected_DoesNotReferenceClusterManagerOrWorldContainers`
- `KleiInventoryPublication_WhenOneUpdateRuns_EnumeratesEachContributingPickupableOnce`
- `FastTrackIncrementalPublication_WhenOneTagRuns_DoesNotConstructCompleteWorldPublication`
- `FastTrackIncrementalPublication_WhenOneTagRuns_RebuildsOneParentTagAggregate`
- `DirectEligibilityPath_AfterWarmup_AllocatesNoManagedObjectInPureHarness`
- `PickupComparator_WhenOneComparisonRuns_CapturesNoNewSnapshotAndAllocatesNoCollection`
- `StatusOptionDisabled_WhenActivationPlanIsInspected_ContainsNoInventoryOrStatusPatchGroup`
- `RetainedCollections_WhenHighWaterLimitWasExceeded_ReplaceVariableCapacityStorage`

Use metadata call inspection and deterministic diagnostic counters for structure. Use `GC.GetAllocatedBytesForCurrentThread` only in the pure single-thread harness after warm-up; repeat enough operations to avoid timer dependence and assert exactly zero delta for the direct method. Ordinary tests must not assert elapsed milliseconds.

- [ ] **Step 4: Write exhaustive cross-domain equivalence tests**

For every decision bucket ordinal `0..5001`, compare direct `DeliveryTemperatureConstraint.Allows`, normalized interval membership, pickup partition classification/equivalence, sparse amount-series queries, and the independent reference model. Include missing-primary-element as a separate case, not ordinal 0.

For every pair of constraints drawn from endpoints `{0, 1, 273, 274, 4999, 5000}` plus disabled and empty cases, prove:

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

Expected: all tests pass with zero skipped tests unless a test is explicitly platform-inapplicable and states why in its assertion output. Any failure returns to a focused red-green-refactor correction and its own meaningful commit before rerunning this full command.

- [ ] **Step 7: Run production build and source audits**

Run pipeline `build`, `rg` removed-symbol audits, `rg` ambiguous-terminology audits, and `git diff --check` as separate commands. Do not install.

Expected: build succeeds; structural tests and searches agree; no generated build artifact is staged.

- [ ] **Step 8: Prepare and commit**

Use the seven test paths, allowed type `test`, and exact subject:

```text
test: Enforce temperature performance architecture
```

---

### Task 25: Approved ONI Mod Pipeline Acceptance Contract

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Tests/PipelineAcceptanceProfileTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`

**Interfaces:**
- Consumes: the exact configuration approval obtained in Task 0 and the complete Gate D behavior.
- Produces: required digest-bound acceptance checks for every content-mode/implementation-path combination and final profiling evidence.

- [ ] **Step 1: Revalidate exact configuration authorization before editing**

Compare the current profile with the Configuration Approval Dossier byte-for-byte for the proposed append. If the user approved a different set, if another edit changed the insertion context, or if approval did not explicitly cover all four matrix cases and remaining checks, stop and request renewed exact approval. Do not infer configuration authority from approval of this plan.

- [ ] **Step 2: Write failing profile-contract tests**

`PipelineAcceptanceProfileTests` loads the TOML as UTF-8 text without adding a TOML package and asserts each exact new ID occurs once, has `required = true`, and contains its distinguishing content-mode and implementation-path terminology. It also rejects the earlier ambiguous IDs and the false statement that every FastTrack background publication is a complete world.

Run the focused class. Expected: failure listing the missing approved IDs.

- [ ] **Step 3: Append exactly the approved acceptance blocks**

Append the complete TOML block from the Configuration Approval Dossier. Do not reformat existing checks, change ordering/settings elsewhere, change metadata/build/package/test declarations, or edit pipeline source. Preserve UTF-8 and the repository's existing line-ending policy.

- [ ] **Step 4: Run profile tests and pipeline validation green**

Run `PipelineAcceptanceProfileTests`, then:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- validate --mod mods/delivery-temperature-limit-supercooled
```

Expected: test and validation pass, and validation recognizes every required check. Inspect the diff to confirm it is append-only apart from the new test file.

- [ ] **Step 5: Prepare and commit**

Use the two task paths, allowed type `test`, and exact subject:

```text
test: Require large-colony acceptance matrix
```

---

### Task 26: Final Automated Pipeline Gate

**Files:**
- Inspect only; do not modify source, configuration, tests, candidate artifacts, or installations unless a failure begins a new focused TDD correction chunk.

**Interfaces:**
- Consumes: a clean committed Gate D implementation and approved acceptance profile.
- Produces: fresh automated evidence and exact build/test paths suitable for release-candidate preparation.

- [ ] **Step 1: Verify committed source and preserve unrelated work**

Run `git status --short`. Every contributing implementation/configuration/test change must already be committed; user-owned unrelated paths may remain untracked only if the pipeline's relevant-source rules permit them. If candidate preparation would reject a contributing untracked file, resolve ownership with the user rather than deleting or committing it implicitly.

- [ ] **Step 2: Run locked restore and the full mod suite**

Run as separate commands:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --locked-mode
```

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
```

Expected: restore makes no lockfile change; every required test passes. Record the total/passed/failed/skipped counts from fresh output.

- [ ] **Step 3: Run repository-local environment diagnosis**

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- diagnose --mod mods/delivery-temperature-limit-supercooled
```

Expected: the intended game, managed-assembly, user-data, Dev/Local, SDK, and artifact paths resolve. Do not alter configuration to hide a diagnostic.

- [ ] **Step 4: Validate, build, and test through the pipeline**

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

Expected: all commands succeed. Retain the exact printed `build-result.json` and automated-test evidence directory. Never substitute a path selected by timestamp or a source-root DLL.

- [ ] **Step 5: Treat any failure as a new TDD correction, not a pipeline workaround**

For a failure, identify the smallest owning behavior, add or refine a focused failing test, implement the correction, run focused and complete tests, prepare a meaningful commit with exact authorization, and restart Task 26 from Step 1. Do not edit generated evidence, relax acceptance, change warnings, or bypass locked restore.

There is no commit for a successful Task 26 because it creates evidence only.

---

### Task 27: One Final Exact-Candidate Deep Validation Campaign

**Files:**
- Read: `docs/guides/preparing-oni-mod-releases.md`
- Read: candidate-generated acceptance plan and build provenance.
- Write only through ONI Mod Pipeline's immutable candidate, installation receipt, acceptance recorder, and verification outputs. Do not edit candidate files manually.

**Interfaces:**
- Consumes: the fully committed implementation and fresh Task 26 evidence.
- Produces: one immutable exact candidate tested across all required gameplay/performance/lifecycle cases and a deterministic release-readiness result. It does not upload or publish the mod.

- [ ] **Step 1: Prepare one immutable candidate from clean committed inputs**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- prepare-release --mod mods/delivery-temperature-limit-supercooled
```

Expected: success prints one exact candidate directory, content digest, and `awaiting-acceptance` state. Record them exactly. Preparation reruns locked build/tests; do not replace its evidence with Task 26 output.

- [ ] **Step 2: Install that exact candidate once to the guarded Local target**

First follow the guide's duplicate-copy checklist: the subscribed Workshop copy and competing Dev/Local copies must be disabled manually; the pipeline does not change subscriptions or enabled-mod state. Then run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- install --candidate <exact-candidate-directory> --target local
```

Replace the angle-bracket token with the exact Step 1 path. Expected: candidate verification succeeds, one ownership-guarded Local installation and candidate receipt are created, and the installed runtime hashes match the candidate. Never reinstall or edit this candidate after a receipt exists.

- [ ] **Step 3: Record the fixed test environment before starting ONI**

Record in acceptance notes:

- exact candidate path and digest;
- ONI build and all enabled DLC/content packs;
- base-game or Spaced Out content mode for each run;
- enabled mod IDs and versions;
- FastTrack assembly identity, version, options, Harmony feature states, and SHA-256 for FastTrack runs;
- large-colony save identity;
- simulation speed, warm-up duration, measurement duration, and profiler tool/version; and
- active destinations, enabled/empty/disabled constraints, distinct endpoints, pickupables, requested tags, authoritative worlds, parent groups, and rocket interiors.

Use the same candidate bytes in every combination. Changing source, DLLs, config files, candidate files, or profiler instrumentation that alters runtime code invalidates the campaign and requires a new candidate.

- [ ] **Step 4: Execute the four independent content/path combinations**

Run and record separately:

1. base-game content mode + Klei inventory and pickup grouping paths;
2. base-game content mode + verified FastTrack inventory and pickup grouping paths;
3. Spaced Out content mode + Klei inventory and pickup grouping paths; and
4. Spaced Out content mode + verified FastTrack inventory and pickup grouping paths.

Verify actual Harmony ownership/activation in each run; do not infer path from installed DLL presence. If a topology scenario is structurally inapplicable to base-game content mode, mark only that sub-scenario inapplicable with the authoritative reason and still run every applicable behavior. Do not collapse the four combinations into “modded/unmodded.”

- [ ] **Step 5: Run correctness scenarios in every applicable combination**

Exercise:

- storage, sweeping, construction, fetch coalescing, and direct delivery at inclusive-low/exclusive-high boundaries;
- underflow-adjacent, `0 K`, `4999 K`, `5000 K`, overflow, disabled, and enabled-empty constraints;
- multi-tag pickupables and destinations with overlapping/disjoint constraints;
- side-screen editing, clearing, copy settings, and save persistence;
- current-parent-only status totals across every authoritative member world;
- world registration, removal, and supported parent reassignment/rocket-interior lifecycle;
- active constraint edits while fetch and inventory updates are running; and
- missing/stale snapshot windows, confirming subsystem-specific conservative behavior rather than fabricated eligibility or zero availability.

Compare corresponding Klei and FastTrack implementation-path eligibility outcomes for equivalent content-mode scenarios. The implementation mechanism may differ; the gameplay decision may not.

- [ ] **Step 6: Run status-enabled and status-disabled restart-separated scenarios**

With status accounting enabled, verify complete Klei publications and FastTrack coverage/pending/single-tag convergence. With the option disabled, restart ONI as required and prove through Harmony ownership and profiler traces that no Klei/FastTrack inventory temperature hook, coverage scan, status accumulator, amount-series publication, or catalog query runs.

Direct delivery temperature checks must remain active in status-disabled mode. Re-enabling the option requires another restart before subsequent status-enabled evidence.

- [ ] **Step 7: Profile the fixed large-colony intervals**

After a recorded warm-up, capture equal fixed intervals at the same simulation speed for each combination. Collect CPU samples, managed allocation samples, generation-zero collection counts, retained collection capacities, lock contention, and relevant call counts.

Pass requires all structural budgets below:

- constraint reads perform no sort, endpoint rebuild, or global-limit scan;
- Klei inventory accumulation adds at most one mod observation per Klei-contributing pickupable and performs no second pickupable enumeration;
- a FastTrack steady-state inventory invocation publishes at most one resource tag and performs no unrelated complete-world reconstruction;
- status queries perform no `WorldContainer` enumeration and no scan proportional to all registered worlds times all temperature bands;
- direct eligibility allocates zero after warm-up;
- pickup comparison performs no snapshot capture, reflection, log, or collection allocation per comparison;
- fixed arrays remain exactly 5,001 endpoint counts and 5,002 decision-bucket slots per retained accumulator;
- variable collections exceeding named high-water limits are replaced at the documented safe boundary;
- no mod-attributed warning storm, unbounded retained growth, or unexplained lock contention occurs; and
- any mod-attributed frame/tick cost at or above either 1% of sampled CPU or 0.5 ms in a 200 ms status update is isolated, explained as unavoidable input-proportional work, and explicitly accepted by the user or corrected before release.

These are profiler-review gates, not flaky unit-test timing assertions. Record FastTrack's own `BackgroundInventoryUpdater.StartUpdateAll` world scan separately from DeliveryTemperatureLimit attribution. If it is material, document it as an upstream FastTrack opportunity with evidence; do not patch a third party's general scheduling policy as part of this rewrite.

- [ ] **Step 8: Prove FastTrack publication behavior directly**

In both FastTrack combinations, record counters/traces showing:

- whether the observed first update was a true complete update;
- when a new mod collection generation required one key-only coverage enumeration per world;
- coverage excluded tags becoming known zero;
- coverage-present tags remaining incomplete until their series arrived;
- each later `RunUpdate` selecting and publishing one resource tag;
- a newly observed tag becoming present/current atomically on its first series publication; and
- no per-update assembly inspection, option reflection, all-tag rebuild, or all-world aggregation.

If actual installed behavior differs from the verified contract, stop. Preserve logs, mark acceptance failed, and begin a new specification/TDD/commit/candidate cycle; do not patch the immutable candidate.

- [ ] **Step 9: Execute lifecycle and failure diagnostics**

Load colony A, return to main menu, load colony B, reload B, and repeat across the four content-mode/implementation-path combinations. Confirm old-session worker publications are rejected, state/capacity does not cross sessions semantically, cleanup is idempotent, and diagnostics are rate-limited.

Inspect `Player.log` after each run for DeliveryTemperatureLimit, Harmony, FastTrack, lifecycle, worker, and unhandled exceptions. A release-blocking compatibility state, fallback activation not present in the recorded plan, relevant exception, or repeated warning fails acceptance.

- [ ] **Step 10: Handle any failure immutably**

Record the check as failed and preserve candidate/evidence. Add a focused failing automated test, implement the smallest correction, run the focused/full/pipeline gates, commit the meaningful chunk after exact authorization, and prepare a new candidate with a new run ID. Never edit/delete acceptance evidence, reinstall the failed candidate, replace candidate bytes, or change a result to passed.

- [ ] **Step 11: Record acceptance once and verify release readiness**

Only after every required check was genuinely executed, run in an interactive terminal:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- record-acceptance --candidate <exact-candidate-directory> --tester <tester-display-name>
```

Enter factual `passed`/`failed` results and notes; do not pre-author answers. Then run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- verify-release --candidate <exact-candidate-directory>
```

Expected: verification reports the ready state and exact summary/checklist paths. This plan does not authorize ONI Uploader use, Workshop publication, Git push, or release upload.

There is no source commit for a successful Task 27 because candidate evidence is generated and immutable.

---

### Task 28: Final Evidence Review and Handoff

**Files:**
- Inspect only. Do not modify committed source or immutable candidate evidence.

**Interfaces:**
- Consumes: fresh Task 26 automated output and Task 27 verified candidate evidence.
- Produces: a precise completion report with no unsupported performance claim.

- [ ] **Step 1: Load and follow verification-before-completion instructions**

Read `C:\Users\maksy\.agents\skills\verification-before-completion\SKILL.md` completely. Apply it to every claim about tests, build, compatibility, performance, or release readiness.

- [ ] **Step 2: Re-read the approved specification acceptance criteria**

Map every numbered criterion to fresh automated output, an acceptance result, profiler evidence, source/metadata inspection, or exact candidate verification. A criterion without evidence is incomplete; do not infer it from a neighboring check.

- [ ] **Step 3: Verify repository and candidate identity one final time**

Run `git status --short` and `verify-release` again as separate commands. Confirm unrelated user-owned paths remain untouched, every contributing change is committed, the installed receipt still matches the exact candidate, and no evidence/candidate byte changed after acceptance.

- [ ] **Step 4: Report outcome, evidence, and residual limits**

Lead with pass/fail. Include:

- exact candidate path and digest;
- commit IDs for every meaningful implementation chunk;
- automated test totals and pipeline evidence paths;
- all four content/path matrix results;
- recorded FastTrack identity/digest and per-feature state;
- measured CPU/allocation/GC/retention/lock findings;
- confirmation that no unqualified ambiguous terminology or shim remains;
- confirmation that the Klei inventory update path pays no FastTrack delta/coverage overhead;
- confirmation that the FastTrack inventory update path does not reconstruct complete worlds for steady-state tag updates; and
- any unavoidable ONI input-proportional cost or inapplicable scenario with its exact reason.

Do not say “as fast as possible” as an absolute theorem. The defensible completion claim is that the approved avoidable scaling mechanisms were removed, every remaining material mod-attributed cost was profiled and explained, and no further mitigation was identified within the preserved behavior and verified patch contracts.

There is no commit or push in Task 28.
