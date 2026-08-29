# Delivery Temperature Limit: Large-Colony Performance Rewrite Design

- **Status:** Approved architecture and written specification; implementation plan prepared for adversarial review
- **Date:** 2026-08-29
- **Mod:** Delivery Temperature Limit (Supercooled)
- **Runtime target:** .NET Framework 4.8 inside Oxygen Not Included
- **Development pipeline:** repository-local ONI Mod Pipeline
- **Release strategy:** one coordinated performance rewrite; no partially migrated release
- **Test strategy:** focused TDD throughout; one deep integration and performance campaign after all rewrite chunks are integrated
- **Compatibility strategy:** preserve intentional player/save/runtime contracts; remove accidental implementation surface and compatibility shims

## 1. Executive summary

The current mod avoids several obvious hot-path costs, but its central optimization is global: every enabled delivery-temperature limit contributes endpoints to one process-wide temperature partition, and that partition is then used for every world, tag, storage availability calculation, pickup sort, and status calculation. In a very large colony, the number of active limits, fetch chores, material tags, worlds, rocket interiors, and pickupables can all grow at the same time. The present representation therefore multiplies otherwise independent dimensions and can approach dense `world × tag × temperature-band` work and storage.

The rewrite replaces that model with immutable, scoped, purpose-built representations:

- exact immutable delivery constraints registered by component identity;
- fixed-size endpoint reference counts for cheap constraint updates;
- one canonical 5,002-class temperature decision model covering missing elements, underflow, integer Kelvin `0..4999`, and overflow;
- sparse, prefix-summed temperature amount series for status availability;
- normalized allowed-temperature interval sets for storage destinations;
- tag- and parent-world-specific pickup partitions derived from the same authoritative fetch traversal;
- collision-free FastTrack grouping-key allocation rather than hash mixing;
- immutable snapshots with explicit constraint, fetch-topology, world-topology, and game-session generations;
- correctness-preserving exact-temperature-class fallback whenever an optimized snapshot cannot be proved current; and
- one game-session composition root that owns all mutable state and rejects late work after unload.

The design deliberately does **not** retain `TemperatureLimit.TemperatureIndexData`, `TemperatureLimit.getTemperatureIndexData()`, the global operational-band model, or an exact whole-assembly public-surface compatibility test. No known external consumer uses those members. FastTrack does not call them; this mod only uses them internally while patching FastTrack. Keeping them would require maintaining the obsolete global model or presenting misleading semantics under an old name.

The implementation will be test-driven at every focused change. Each meaningful, internally complete chunk will be committed separately after its focused tests pass and its source remains buildable. The expensive whole-mod pipeline, installed-game, four-way base-game/Spaced-Out and Klei/FastTrack matrix, large-colony, profiler, allocation, garbage-collection, save/load, and lifecycle validation campaign will run once, after every planned fix is integrated.

## 2. Scope and current-state findings

### 2.1 Source areas assessed

The performance investigation covered the behavior and patch seams in:

- `Source/Limits.cs`;
- `Source/Patch.cs`;
- `Source/PatchFastTrack.cs`;
- `Source/StatusItems.cs`;
- `Source/Construction.cs`;
- `Source/Buildings.cs`;
- `Source/Mod.cs`;
- the mod and test project files;
- the repository-local ONI Mod Pipeline profile; and
- the installed game's relevant `Assembly-CSharp.dll` types and method signatures.

The investigation also checked current FastTrack source for its pickup grouping and background world-inventory behavior. Relevant upstream files are:

- [FetchManagerFastUpdate.cs](https://github.com/peterhaneve/ONIMods/blob/main/FastTrack/GamePatches/FetchManagerFastUpdate.cs)
- [BackgroundWorldInventory.cs](https://github.com/peterhaneve/ONIMods/blob/main/FastTrack/UIPatches/BackgroundWorldInventory.cs)
- [FastTrackCompat.cs](https://github.com/peterhaneve/ONIMods/blob/main/FastTrack/FastTrackCompat.cs)

Current upstream source is compatibility evidence, not a permanent binary contract. Final validation records the exact installed FastTrack assembly version and SHA-256 digest.

### 2.2 Existing strengths that remain requirements

The current implementation already recognizes several important performance facts:

- repeated Unity `GetComponent` transitions are expensive, so `TemperatureLimit.Get(GameObject)` uses an instance-ID lookup;
- pickup grouping must distinguish temperatures only where a constraint can make eligibility differ;
- immutable aggregate publication is safer for worker readers than exposing fields updated independently;
- FastTrack background inventory work requires thread-confined intermediate sums;
- status-temperature accounting is entirely disabled when its option is disabled; and
- direct delivery eligibility uses integer truncation and inclusive-low/exclusive-high semantics.

The rewrite preserves those intentions while replacing representations whose scaling or correctness properties are inadequate.

### 2.3 Existing large-colony slowdown mechanisms

| Existing mechanism | Scaling trigger | Consequence in a very large colony |
|---|---|---|
| `allLimits` scan, endpoint sort, `Distinct`, and 5,000-entry inverse-map rebuild | Any changed limit followed by a hot-path read | O(active limits log active limits + 5,000) work is deferred onto whichever thread first asks for temperature data; that thread may be sorting or enumerating pickups. |
| Every setter calls `SetDirty`, including effective no-ops | UI editing, copy settings, construction defaults, load/spawn churn | Avoidable global invalidations and lock acquisitions. |
| One global endpoint partition | Limits unrelated to a pickup's tag or parent world | Every new endpoint can fragment every pickup/status/storage operation, even where the associated destination can never request that pickup. |
| `HashSet<Tag>[]` per world and global band | More endpoints, worlds, and requested tags | Dense arrays of sets, full-set clears, and repeated `UnionWith` across every allowed band. |
| `(Tag, temperatureIndex) -> float` dictionary entries, including zero totals | More tags, worlds, and bands | Memory and publication work approach `world × tag × band`; zero-valued cells consume dictionary entries. |
| Full `updateSums` clear and full-band dictionary write per world/tag | Every Klei `WorldInventory.Update` enumeration or FastTrack background inventory refresh | O(bands) work per tag even when a tag occupies one or two temperature classes. |
| Status query scans every `WorldContainer`, then every allowed band | More worlds/rocket interiors, more limits, 200 ms status refresh | Main-thread work approaches `status items × related worlds × allowed bands`. |
| Worker mutation of mutable per-world dictionaries while the main thread reads them | FastTrack background inventory enabled | The code avoids mutating the outer dictionary on workers but does not provide an immutable publication boundary for each inner dictionary. Partial or concurrently mutated state can be observed. |
| Pickup sort/comparison repeatedly obtains global temperature data | Large pickup lists and O(n log n) sorting | Repeated static access and possible lazy rebuilding inside comparator/suppression hot paths. |
| Global partition applied to every pickup tag | Many constraints with varied endpoints | Pickup grouping fragments far beyond the distinctions relevant to the requested tags in the pickup's parent world. |
| FastTrack temperature index mixed into an existing 32-bit hash | Large and diverse pickup sets | Hash collisions can merge eligibility-distinct pickups because FastTrack's private key equality uses only that integer hash. |
| Warning-only response to a missing/changed transpiler anchor | ONI or FastTrack update | The mod can continue with temperature-unaware optimized code, producing incorrect eligibility rather than a safe fallback. |
| Per-component `OnLoadLevel` clears shared collections | Save reload and Unity callback ordering | One component can erase registrations established by another; stale component/world references may survive other lifecycle paths. |
| World discovery scan in FastTrack `StartUpdateAll` | Every background update | Repeated O(world count) main-thread setup and unbounded lifecycle ambiguity; the source itself contains a TODO suggesting event-driven registration. |

### 2.4 What cannot be optimized away

The mod changes fetch eligibility. It must therefore preserve at least one representative for each class of pickup that can produce a different answer for an active destination constraint. No sound implementation can always collapse every temperature to the original ONI tag-only grouping.

The unavoidable work is bounded by the distinctions that are actually relevant:

- direct checks remain O(1) per candidate;
- status accumulation remains O(number of pickupables enumerated by ONI/FastTrack), because item amounts must be observed somewhere;
- snapshot construction remains O(number of relevant fetch chores and requested tags) when the authoritative traversal runs; and
- pickup grouping remains O(number of pickups being updated), with O(log endpoint count) classification for a scoped partition or O(1) exact fallback classification.

The rewrite's purpose is to avoid multiplying those unavoidable dimensions by every global temperature endpoint, every world, or every tag.

## 3. Goals

The rewrite must:

1. Preserve intentional save, player-visible, option, and delivery semantics.
2. Make hot reads lock-free through immutable snapshot capture.
3. Make component add, update, and remove O(1), excluding bounded snapshot reconstruction.
4. Prevent unrelated tags and worlds from fragmenting pickup temperature partitions.
5. Prevent status memory from scaling as a dense product of worlds, tags, and bands.
6. Replace repeated range summation with sparse prefix-sum queries.
7. Publish only generation-current immutable fetch and inventory data, with explicit complete-world versus single-resource-tag inventory semantics.
8. Reject stale worker publications across constraint changes, topology changes, and game-session changes.
9. Preserve correctness when optimized data is absent or stale.
10. Make FastTrack compatibility an optional adapter around the canonical domain algorithm, never an alternate algorithm.
11. Ensure FastTrack grouping keys cannot collide within an update.
12. Make lifecycle cleanup deterministic, idempotent, and owned by one game-session boundary.
13. Bound retained thread-local and reusable collection capacity.
14. Remove obsolete representations rather than retaining unproved compatibility shims.
15. Provide exhaustive deterministic domain tests across every temperature decision class.
16. Integrate all automated and manual validation through the repository's ONI Mod Pipeline.

## 4. Non-goals

The rewrite will not:

- change the serialized field names or type identity of `TemperatureLimit`;
- change the mod options, defaults, localized player-facing meaning, or supported DLC metadata;
- change the integer truncation rule used by delivery eligibility;
- broaden accepted temperature bounds beyond `0..5000 K`;
- modify ONI's simulation, inventory enumeration cadence, or FastTrack's general scheduling strategy;
- introduce a general-purpose compatibility framework for hypothetical mods;
- expose the new domain model as a public extension API;
- keep the old global band model behind an adapter;
- add a third-party property-testing, benchmarking, or mocking dependency merely for convenience;
- make wall-clock timing assertions part of ordinary unit tests;
- perform expensive installed-game validation after each code chunk; or
- publish or upload a Workshop release.

## 5. Non-negotiable architecture principles

### 5.1 No shims by default

The controlling rule is:

> No shims by default. Do not preserve a legacy type, member, code path, representation, alias, wrapper, fallback implementation, or parallel subsystem merely because it already exists or might hypothetically be consumed. A shim is permitted only when a named and reproducible external consumer would otherwise break, the required legacy semantics are precisely documented, no clean migration path exists, the shim has focused contract tests and an explicit owner, its lifecycle and removal conditions are stated, and the user explicitly approves that specific exception. “Public in the old DLL,” “the current regression test expects it,” and “someone might use it” are insufficient.

Every meaningful implementation chunk ends with a shim scan covering newly introduced aliases, wrappers, legacy branches, duplicated algorithms, obsolete members, and compatibility comments. A candidate shim stops implementation until the exception dossier is written and explicitly approved.

### 5.2 Semantic naming

Names must state the domain concept and its semantics. Required examples include:

- `DeliveryTemperatureConstraint`, not `RangeData`;
- `TemperatureDecisionBucket`, not `Index` where the value represents a behavioral equivalence class;
- `TemperatureConstraintRegistry`, not `LimitManager`;
- `TemperatureLimitComponentIndex`, not `fastMap`;
- `WorldParentTopologySnapshot`, not `worldMap`;
- `TemperatureAmountSeries`, not `sums`;
- `AllowedTemperatureIntervalSet`, not `validRanges`;
- `FetchTemperatureEligibilitySnapshot`, not `fetchData`;
- `PickupTemperaturePartitionCatalog`, not `groupCache`;
- `FastTrackPickupGroupingKeyAllocator`, not `HashHelper`; and
- `DeliveryTemperatureGameSession`, not `GlobalState`.

The implementation must not use `Helper`, `Utils`, `Common`, `Misc`, `DoWork`, or bare `Data` type names unless an external API mandates the name.

The unqualified word **“vanilla” is forbidden** in architecture text, production names, test names, comments, diagnostics, commit messages, and acceptance records because it can mean either ONI without the Spaced Out DLC or Klei's unreplaced implementation. Use the exact term from this table:

| Exact term | Meaning |
|---|---|
| **Base-game content mode** | ONI is running with the Spaced Out DLC disabled. |
| **Spaced Out content mode** | ONI is running with the Spaced Out DLC enabled. |
| **Klei inventory update path** | Klei `WorldInventory.Update` performs the authoritative inventory enumeration because FastTrack's `ParallelInventory` replacement is not active. Other unrelated mods may still be enabled. |
| **FastTrack inventory update path** | The verified FastTrack `ParallelInventory` replacement performs background inventory enumeration. This term does not imply that Spaced Out is enabled. |
| **Klei pickup grouping path** | Klei `FetchManager.FetchablesByPrefabId.UpdatePickups` performs pickup grouping because FastTrack's replacement is not active. |
| **FastTrack pickup grouping path** | The verified FastTrack pickup-update replacement performs grouping. This term does not imply that Spaced Out is enabled. |
| **Klei implementation paths** | Collective term for the Klei inventory update, pickup grouping, and direct-comparison paths when more than one is meant. |
| **FastTrack implementation paths** | Collective term for the independently verified FastTrack inventory, pickup grouping, and direct-comparison replacements when more than one is meant. |

Content mode and implementation path are independent axes. Production and acceptance naming must preserve that distinction, for example `KleiWorldInventoryTemperaturePatches`, `FastTrackWorldInventoryTemperaturePatches`, `BaseGameContentModeAcceptanceTests`, and `SpacedOutContentModeAcceptanceTests`. Names such as `VanillaInventoryAdapter` and `NonVanillaAdapter` are semantically invalid.

### 5.3 Comments explain invariants

Comments are required where they protect non-obvious correctness, lifecycle, threading, Harmony-anchor, truncation, or ownership rules. Comments must not narrate obvious syntax, retain obsolete history, or leave unresolved `TODO` markers in a completed chunk.

### 5.4 Immutable publication and single ownership

Mutable state has exactly one owner. Published state is immutable. Readers capture one reference and use it for a complete logical operation. No reader combines fields from snapshots captured at different times.

### 5.5 Correctness before optimization

An optimized snapshot may be used only when its complete validity can be proved. Missing, stale, incomplete, or unverifiable data invokes the designated correctness-preserving behavior. It never invokes the old global optimization as a compatibility fallback.

## 6. Intentional compatibility contract

### 6.1 Preserve

The rewrite preserves:

- the public `DeliveryTemperatureLimit.TemperatureLimit` component identity required by serialized saves;
- private serialized fields named `lowLimit` and `highLimit`, with their existing integer representation;
- `MinValue = 0` and `MaxValue = 5000` unless separate migration approval is obtained;
- disabled behavior when the normalized high limit is zero;
- inclusive lower and exclusive upper delivery eligibility;
- C# integer truncation toward zero before comparison;
- enabled-but-empty constraints when `lowLimit >= highLimit` and `highLimit > 0`;
- construction-material option behavior;
- status-temperature option behavior;
- copy-settings behavior;
- storage, sweeping, fetch coalescing, and direct delivery decisions;
- Klei/FastTrack implementation-path feature parity in both base-game and Spaced Out content modes; and
- the mod entrypoint and Unity/Klei/PLib/Harmony-required callbacks.

### 6.2 Remove

The rewrite removes:

- `TemperatureLimit.TemperatureIndexData`;
- `TemperatureLimit.getTemperatureIndexData()`;
- `allLimits`;
- `limitsDirty`;
- the lazy `UpdateIndexes()` path;
- the old global operational-band representation;
- `storageFetchableTagsPerTemperatureIndex`;
- dense `(Tag, temperatureIndex) -> float` status dictionaries;
- hash-combined FastTrack temperature keys; and
- any test that demands byte-for-byte or whole-public-surface preservation of accidental implementation members.

### 6.3 Why `TemperatureIndexData` is not retained

`TemperatureIndexData` was introduced as an internal atomic-publication mechanism for this mod's own patches. It was not documented as an extension API. The investigation found:

- no reference in current FastTrack source;
- no reference in FastTrack compatibility code;
- no local installed mod assembly referencing its type or getter other than copies of Delivery Temperature Limit itself; and
- no user or developer documentation promising the type.

Retaining only the type name while changing its semantics would be a misleading shim. Retaining its semantics would force the obsolete global band model to remain alive. The exact public-surface regression test must therefore be replaced by a curated compatibility contract that asserts only intentional runtime and serialization surface.

If a later named external mod is proven to consume the member, implementation stops and prepares the full shim-exception dossier required by section 5.1. It does not pre-emptively restore the type.

## 7. Canonical temperature semantics

### 7.1 `DeliveryTemperatureConstraint`

`DeliveryTemperatureConstraint` is an internal immutable value describing a destination's exact configured behavior:

- `MinimumInclusiveKelvin`;
- `MaximumExclusiveKelvin`;
- `IsEnabled`;
- `IsEmpty`; and
- `Allows(float temperatureKelvin)`.

Normalization clamps both serialized integer fields to `0..5000`. `MaximumExclusiveKelvin == 0` means disabled. An enabled constraint with `MinimumInclusiveKelvin >= MaximumExclusiveKelvin` is empty and rejects every temperature. Empty and disabled are not interchangeable.

`Allows` must apply the exact existing conversion before comparison:

```csharp
int truncatedKelvin = (int)temperatureKelvin;
```

No caller may independently round, floor, clamp, or convert through Celsius.

### 7.2 `TemperatureDecisionBucket`

`TemperatureDecisionBucket` is the one canonical classification used wherever a full optimized partition is unavailable or where amounts need stable integer-temperature identity.

It contains exactly 5,002 material-temperature classes plus a separate missing-primary-element classification at APIs that operate on pickupables:

1. **Underflow:** `truncatedKelvin < 0`.
2. **Integer Kelvin:** one class for each value `0..4999`.
3. **Overflow:** `truncatedKelvin >= 5000`.

The apparent asymmetry is intentional:

- `0 K` through `4999 K` can be distinguished by a valid configured endpoint and therefore require separate classes;
- every truncated value below `0 K` is rejected by every enabled nonempty valid constraint, so those values are behaviorally equivalent;
- every truncated value at or above `5000 K` is rejected by every enabled nonempty valid constraint because the maximum is exclusive and cannot exceed `5000`;
- ordinary negative Celsius temperatures are not negative Kelvin. The physical span from absolute zero through `0 °C` maps to Kelvin buckets `0..273` and is already represented individually; and
- values between `-1 K` and `0 K` truncate to zero under C# rules and therefore belong to the `0 K` class rather than underflow.

`TemperatureDecisionBucket.FromTemperature(float temperatureKelvin)` is the only permitted conversion function. Tests pin `-1.0`, values just above `-1`, negative fractional values truncating to zero, `0`, each endpoint-adjacent case, `4999`, values just below `5000`, `5000`, and overflow.

Missing `PrimaryElement` is not assigned an invented Kelvin value. It receives a distinct pickup eligibility classification so null behavior remains explicit and characterizable.

## 8. Constraint registration and component lookup

### 8.1 `TemperatureConstraintRegistry`

The registry is an internal instance service keyed by Unity component instance ID, but it contains no Unity, Harmony, PLib, or FastTrack type.

It provides:

- O(1) add or replace;
- O(1) remove;
- exact enabled-constraint count;
- exact enabled-nonempty-constraint count;
- a monotonic constraint generation;
- fixed endpoint reference counts for `0..5000`, inclusive; and
- eager immutable `ActiveTemperatureConstraintSnapshot` publication on the mutating thread.

Endpoint counts include only enabled, nonempty constraints because disabled and empty constraints cannot create a temperature eligibility boundary. Empty constraints remain in the snapshot because they still reject every temperature.

Mutation rules are:

- identical repeated registration is an idempotent no-op and does not increment generation;
- changed registration for the same identity replaces atomically, adjusts old/new endpoint counts, increments generation once, and emits a diagnostic in diagnostic builds;
- unknown removal is idempotent and emits a diagnostic only in diagnostic builds;
- no public method exposes the mutable dictionary or endpoint array; and
- callbacks are emitted only after the registry lock is released.

The active snapshot contains immutable constraints and a deterministically reconstructed sorted endpoint array. There is no dirty flag and no worker-triggered rebuild.

### 8.2 `TemperatureLimitComponentIndex`

This internal service maps a `GameObject` instance ID to the corresponding `TemperatureLimit` for direct game-patch checks. Its API uses semantic operations such as register, replace, resolve, and remove-if-owned.

Ownership tokens prevent delayed cleanup from removing a newer component that reuses an instance ID. The index is game-session-scoped and is discarded as a unit at shutdown.

### 8.3 `TemperatureLimit` lifecycle and setters

`TemperatureLimit` remains the serialized Unity component. It will:

- normalize loaded fields once at registration;
- register its immutable constraint and component index entry in `OnSpawn`;
- retain the returned registration token;
- make setter and copy operations no-ops when normalized values do not change;
- replace its registry entry only while its token belongs to the current active session;
- remove only its own registrations in `OnCleanUp`; and
- never rebuild global derived data directly.

The existing per-component `OnLoadLevel` global reset is deleted.

## 9. Game-session lifecycle and concurrency

### 9.1 Composition root

`DeliveryTemperatureGameSessionHost` atomically publishes the current `DeliveryTemperatureGameSession`. The session owns:

- `TemperatureConstraintRegistry`;
- `TemperatureLimitComponentIndex`;
- `WorldParentTopologyCatalog`;
- `WorldTemperatureInventoryCatalog`;
- `FetchRequestTopologyTracker`;
- the current combined fetch eligibility snapshot;
- FastTrack adapter status; and
- rate-limited session diagnostics.

Static Harmony entry points may reach the host. Domain algorithms remain instance-based and testable.

### 9.2 Session generations

Each new session receives a monotonic `GameSessionGeneration`. Registrations, snapshots, and update sessions carry that generation. A candidate publication is accepted only when the target session is active and all captured generations/versions remain current.

This rejects background work that completes after main-menu return, save reload, topology mutation, or constraint mutation.

### 9.3 Start and shutdown

The intended authoritative hooks are:

- `Game.OnLoadLevel()` to ensure the current session;
- `Game.DestroyInstances()` prefix to stop acceptance and atomically detach the session; and
- the corresponding finalizer to release session-owned mutable state even after an exception.

`TemperatureLimit.OnSpawn()` invokes the same idempotent `EnsureGameSession` operation so correctness does not depend on undocumented callback order. This is one lifecycle operation with multiple callers, not a parallel compatibility subsystem.

The implementation must characterize installed-game callback order during the one final deep validation. If `Game.DestroyInstances()` does not cover a supported exit path, implementation stops for an explicit design amendment rather than adding speculative cleanup hooks.

### 9.4 Synchronization rules

No code may hold more than one domain-service lock at a time. Cross-service work captures immutable data from one service, releases its lock, computes outside locks, and then validates/publishes into another service.

No lock is held while:

- calling Unity, Klei, PLib, Harmony, or FastTrack;
- logging;
- sorting or normalizing;
- traversing `fetchMap`;
- invoking another domain service; or
- allocating a large builder.

Snapshot references are published atomically. A reader captures one reference once for the complete operation.

### 9.5 Thread-confined work

Accumulators, builders, and grouping sessions are invocation-confined. FastTrack reusable instances may be `[ThreadStatic]`, but each records its game-session generation and active/nesting state. Harmony finalizers clear or restore state after success and exceptions.

Thread-static fixed arrays are bounded and may remain allocated on worker threads. Variable-size dictionaries are replaced after exceeding a documented high-water threshold. Session shutdown need not—and safely cannot—enumerate other threads' static storage; generation rejection prevents retained buffers from publishing stale state.

## 10. World-parent topology

`WorldParentTopologyCatalog` is main-thread-owned and publishes immutable `WorldParentTopologySnapshot` instances.

The catalog is content-mode neutral. It consumes authoritative `WorldContainer` identities and parent relationships and never branches on an assumption that base-game content mode has exactly one world or that Spaced Out content mode is enabled. Base-game content mode and Spaced Out content mode must both tolerate every world context actually reported by the installed game, including lifecycle events for any supported interior world. Spaced Out commonly makes multi-asteroid and rocket-interior aggregation more visible, but the domain model does not encode DLC-specific topology guesses.

The verified installed-game seams are:

- `ClusterManager.RegisterWorldContainer(WorldContainer)` postfix;
- `ClusterManager.UnregisterWorldContainer(WorldContainer)` prefix; and
- `WorldContainer.SetParentIdx(int)` postfix.

Each effective mapping change increments a topology version exactly once. World removal also removes that world's inventory contribution and invalidates affected parent/tag aggregates. Parent reassignment invalidates both the old and new parent aggregates; data is not blindly transferred between parents.

Worker code resolves world-to-parent relationships only through a captured immutable snapshot. An unresolved world never defaults to parent zero, the active world, or its own raw world ID. Acceptance must exercise all four independent combinations: base-game content mode with the Klei inventory update path, base-game content mode with the FastTrack inventory update path, Spaced Out content mode with the Klei inventory update path, and Spaced Out content mode with the FastTrack inventory update path.

## 11. Sparse status-temperature inventory

### 11.1 `TemperatureAmountAccumulator`

This reusable, thread-confined collector uses fixed arrays for the 5,002 `TemperatureDecisionBucket` values:

- accumulated amount by bucket;
- generation stamp by bucket; and
- touched-bucket indices.

Starting a new tag advances a local stamp. Only touched entries are emitted; no 5,002-entry clear or write occurs per tag. Stamp wraparound performs one explicit full reset and is tested.

The Klei inventory update path uses a main-thread instance. The FastTrack inventory update path uses a thread-static instance with finalizer cleanup and game-session generation checks.

### 11.2 `TemperatureAmountSeries`

Published amounts use a sparse immutable series:

- sorted occupied bucket IDs; and
- cumulative amounts aligned with those IDs.

An inclusive-low/exclusive-high amount query uses two binary searches plus prefix subtraction. Underflow and overflow participation follows the exact constraint semantics. Empty constraints return zero without searching. Disabled constraints do not request temperature-specific replacement.

### 11.3 `WorldTemperatureInventoryCatalog`

The catalog owns:

- the latest contribution for each member world and tag;
- the publication kind and collection generation that prove whether a world/tag contribution is current or known absent;
- a preaggregated parent-world series for each tag;
- the set/generation of member worlds required for completeness; and
- collection/completeness state.

The catalog accepts three deliberately different immutable publication contracts:

- `CompleteWorldResourceTemperatureAmounts` contains every resource tag observed by one complete Klei or FastTrack world enumeration. Successful publication atomically replaces the world's whole contribution. A tag absent from this complete publication is a known zero at that publication point.
- `WorldResourceTagCoverage` contains the complete set of resource-tag keys observed for one world when a FastTrack incremental collection generation begins. Coverage proves only presence or absence; it does not claim that a present tag's temperature series has been refreshed for that generation.
- `WorldResourceTemperatureSeriesPublication` contains one resource tag and its complete immutable `TemperatureAmountSeries` from one FastTrack incremental tag refresh. It never claims that other tags were refreshed.

The Klei inventory update path publishes one `CompleteWorldResourceTemperatureAmounts` after a successful `WorldInventory.Update`. FastTrack's first `BackgroundWorldInventory.RunUpdate` after `OnPrefabInit` also enumerates every tag and may publish the same complete form. A later FastTrack `RunUpdate` refreshes only one tag and must publish only one `WorldResourceTemperatureSeriesPublication`; rebuilding a complete-world candidate on every such invocation is forbidden because it would defeat FastTrack's incremental scheduling.

If temperature inventory collection starts after FastTrack has already completed its one-time full update, the FastTrack adapter performs one key-only coverage enumeration for each registered world at the start of that `WorldInventoryCollectionGeneration`, publishes `WorldResourceTagCoverage`, and thereafter accumulates only the tag FastTrack already selected. The coverage enumeration may inspect dictionary keys but must not enumerate pickupables, compute amounts, or recur on every background update. A world registered during an active generation receives its own one-time coverage publication. A tag-specific publication for a newly observed tag atomically establishes that tag as present and current even if it was absent from the earlier coverage set.

Current FastTrack source adds inventory dictionary keys but does not remove keys when the final pickupable is removed; it retains an empty set that later publishes a zero series. The compatibility inspector must verify the installed binary's equivalent lifecycle contract. If an installed FastTrack build can remove or replace keys without a detectable coverage refresh, the adapter is `Incompatible` until a semantically complete invalidation seam is implemented. The future implementer must not assume stable key coverage merely because the type and method names still exist.

For a complete-world replacement, the catalog diffs the previous and replacement resource-tag sets and rebuilds only affected parent/tag aggregates. For a single-tag publication, it replaces and rebuilds only that world/tag and its affected parent/tag aggregate. A FastTrack single-tag update must never cause unrelated resource tags or worlds to be combined again. Readers never scan all `WorldContainer` instances.

The first enabled constraint starts a new `WorldInventoryCollectionGeneration`. A requested parent/tag result is complete only when every currently registered member world has one of these proofs for the expected generation:

1. a complete-world publication, in which the tag is either present with a current series or absent and therefore known zero; or
2. a complete tag-coverage publication in which the tag is absent and therefore known zero; or
3. a complete tag-coverage publication in which the tag is present, plus a current single-tag series publication.

The catalog returns incomplete when any member world lacks the required proof. It must never interpret “coverage says present but no series has arrived” as zero. Complete-world and tag-specific publications from different worlds need not finish in the same frame. World addition, removal, parent reassignment, coverage replacement, and collection-generation change update only the affected completeness state. Parent membership versions prevent a previously complete aggregate from surviving a topology change unnoticed.

Its semantic API is limited to:

```text
RegisterWorld
PublishCompleteWorldResourceAmounts
PublishWorldResourceTagCoverage
PublishWorldResourceTemperatureSeries
TryGetWorldResourceTagCoverageRequirement
TryGetAvailableAmount
RemoveWorld
ClearForGameSession
```

World registration, removal, parent reassignment, complete replacement, coverage replacement, single-tag replacement, duplicate publication, mixed publication kinds, stale generations, and post-removal late publication all have explicit tests.

### 11.4 Availability replacement states

- Status option disabled: do not install inventory/status hooks and retain no temperature inventory.
- No enabled constraints: bypass all temperature-specific status work.
- First enabled constraint: enter collecting state.
- Required member-world/tag data incomplete: leave ONI's existing availability unchanged.
- Complete current data: replace only the temperature-sensitive availability amount.
- Empty enabled constraint: report no temperature-eligible amount once the status hook confirms it is evaluating that destination; no inventory scan is required.
- Transition to zero enabled constraints: publish bypass state immediately and release high-water variable buffers at the next safe main-thread boundary.

The mod must never manufacture zero merely because optimized inventory data has not arrived.

Once a complete temperature-eligible total is available, the adapter preserves the current status adjustment exactly:

```text
fetchable = eligibleTotal + min(remaining, eligibleTotal)
```

It also preserves the existing early exit when `inStorage + fetchable < minimumAmount`. The rewrite does not reinterpret ONI reservation accounting or silently substitute a different “available” formula; focused characterization tests pin the current hook inputs and output before the old status implementation is deleted.

### 11.5 Klei and FastTrack inventory publication adapters

Both adapters bracket existing authoritative enumeration and share the same accumulator, immutable series, catalog, availability formula, and lifecycle rules. Neither contains an alternate status algorithm.

The Klei inventory update adapter:

1. begins one complete-world builder in the `WorldInventory.Update` prefix;
2. begins and completes one resource-tag accumulator around each existing Klei tag enumeration;
3. adds each pickupable temperature and amount while Klei already enumerates it;
4. publishes one `CompleteWorldResourceTemperatureAmounts` only from a successful postfix; and
5. discards the whole candidate from the finalizer after an exception.

The FastTrack inventory update adapter:

1. is installed only when the exact `ParallelInventory` replacement is verified active for the loaded game;
2. distinguishes FastTrack's first complete update from its later single-tag updates using verified installed-binary state and IL anchors;
3. publishes a complete-world candidate for the first complete update;
4. publishes one generation coverage set when incremental collection begins without a usable complete update;
5. publishes exactly one tag series for each later FastTrack update; and
6. uses thread-confined state plus postfix/finalizer cleanup so an exception cannot publish a partial series.

Compatibility discovery and reflective member binding occur once during installation and are cached. The FastTrack inventory update path performs no per-update assembly scan, option reflection, or compatibility rediscovery. The Klei inventory update path allocates no FastTrack state and pays no coverage/delta bookkeeping. Selecting either implementation path is independent of base-game versus Spaced Out content mode.

## 12. Combined fetch temperature-eligibility snapshot

### 12.1 One authoritative traversal

Storage interval sets and pickup partition endpoints are derived during one successful traversal of `GlobalChoreProvider.fetchMap`. This map contains the authoritative active `FetchChore` topology, including storage and construction fetches.

The resulting immutable `FetchTemperatureEligibilitySnapshot` contains:

- storage eligibility by parent world and requested `Tag`;
- pickup partition definitions by parent world and requested `Tag`;
- the active constraint generation;
- the fetch request topology version;
- the world-parent topology version; and
- the game-session generation.

The old snapshot remains visible until a complete current replacement is atomically published.

### 12.2 `FetchRequestTopologyTracker`

The tracker increments a monotonic version on effective changes observed through:

- `GlobalChoreProvider.AddChore`;
- `GlobalChoreProvider.RemoveChore`;
- `FetchChore.OnTagsChanged`;
- destination constraint change;
- world add/remove; and
- world-parent reassignment.

These hooks only change versions. They do not rebuild fetch snapshots synchronously.

The snapshot builder captures all versions before traversal and publishes only if every version is unchanged afterward. Otherwise it discards the whole candidate.

### 12.3 Tag semantics

Each `FetchChore` contributes its requested tag set. For an enabled, nonempty destination constraint, its low/high endpoints contribute to each requested tag in the destination's parent world. Disabled constraints contribute an unconstrained destination. Empty constraints contribute an allows-nothing destination.

If a pickup satisfies multiple requested tags, its effective partition is the normalized union of endpoint arrays for all applicable tags. This union is scoped to the pickup's resolved parent world.

The grouping identity is `PickupTagIdentity`: ONI's existing base grouping identity plus `PrefabTag` where required by the actual Klei or FastTrack pickup grouping path. The design does not claim `PrefabTag` alone is globally unique when the underlying grouping uses additional tag-bit identity.

## 13. Storage temperature eligibility

### 13.1 `AllowedTemperatureIntervalSet`

The internal immutable type has three explicit states:

- `AllowsNoTemperature`;
- `AllowsEveryTemperature`; or
- sorted, nonoverlapping inclusive-low/exclusive-high intervals.

Normalization rules are exact:

- disabled destination constraint makes the requested tag `AllowsEveryTemperature`;
- enabled empty or reversed interval contributes nothing;
- duplicates collapse;
- overlapping intervals merge;
- adjacent intervals merge because no integer decision class lies between them; and
- once `AllowsEveryTemperature` is established, narrower contributors cannot change it.

Lookup uses binary search. There is no per-band `HashSet<Tag>[]` representation.

### 13.2 Build session and publication

`StorageTemperatureEligibilityBuildSession` exists for one complete `GlobalChoreProvider.UpdateStorageFetchableBits` invocation:

- prefix captures game session, constraints, fetch version, and world topology;
- the world-section hook begins the correct parent-world builder;
- each traversed `FetchChore` contributes its requested tags and destination constraint;
- postfix normalizes and hands its portion to the combined snapshot candidate;
- finalizer discards and clears incomplete state after exceptions.

Only the combined `FetchTemperatureEligibilitySnapshot` is published. Storage eligibility is never published independently with versions that could disagree with pickup partitions.

### 13.3 `ClearableHasDestination`

The decision sequence is:

1. Preserve ONI's existing `false` result immediately.
2. Bypass when there are no enabled constraints.
3. Preserve the existing conservative `false` result when `PrimaryElement` is missing.
4. Resolve parent world through the current immutable topology snapshot.
5. Capture the combined fetch snapshot once.
6. Query only if game-session, constraint, fetch, and world-topology generations are current.
7. If current, test the pickup's exact `TemperatureDecisionBucket` against the tag's interval set.
8. If topology, tag data, or a current snapshot is missing, change the otherwise-true result to `false`; never consult the removed global model.

Focused characterization tests must first pin the existing patch's conservative `false` result for missing primary element and unavailable temperature data. The new implementation preserves that behavior: it may temporarily suppress a sweep destination until the authoritative snapshot catches up, but it must not claim that a temperature-valid destination exists without evidence.

## 14. Klei pickup-path temperature partitions

### 14.1 `TemperaturePartitionDefinition`

An immutable definition contains:

- a stable identifier unique within its containing snapshot;
- sorted relevant endpoints;
- classification into interval ordinals;
- a no-temperature-distinction representation; and
- exact-decision fallback representation.

For an optimized scoped partition, two temperatures share an ordinal only if every active relevant constraint for the same parent world and pickup tag identity gives the same eligibility answer.

The converse is also required: if no relevant constraint distinguishes two decision buckets, they should not be fragmented into different optimized classes. Tests therefore verify both correctness and minimal fragmentation.

### 14.2 `TemperatureEligibilityClassKey`

The key contains:

- partition definition ID; and
- interval ordinal.

Ordinals from different partition definitions are never equal merely because they have the same small integer value.

### 14.3 Per-update grouping session

`FetchManager.FetchablesByPrefabId.UpdatePickups(Navigator, int)` receives a prefix/postfix/finalizer session that captures once:

- the current game session;
- active constraints;
- combined fetch snapshot;
- world-parent topology;
- navigator anchor/current world; and
- whether the optimized snapshot is current.

It resolves the navigator's parent world once at update entry using `Navigator.GetAnchorCell()` and then uses immutable topology/snapshot data for every candidate. Patch verification must characterize whether the installed Klei or FastTrack pickup grouping invocation runs on a worker. Worker-capable code never enumerates Unity objects, performs `GetComponent`, or queries `ClusterManager`. It may read only the exact candidate fields and cached `PrimaryElement` temperature whose installed managed-field access and cross-thread stability were verified before activation; if that proof fails, the affected FastTrack pickup grouping path is incompatible and must use the proved Klei pickup grouping path.

Classification is:

- zero enabled constraints: original no-temperature-distinction grouping;
- current scoped partition: partition definition plus interval ordinal;
- stale, missing, or unresolved scoped partition: exact `TemperatureDecisionBucket` class;
- missing `PrimaryElement`: explicit missing-element class.

The session caches each pickup's full grouping key for that update. Oversized dictionaries are replaced at a documented high-water threshold.

### 14.4 Comparator and suppression semantics

The Klei pickup-path comparator preserves all original ordering fields first, then compares partition definition ID and ordinal. Duplicate suppression compares the complete base grouping identity and `TemperatureEligibilityClassKey` captured under the same session snapshot.

Comparator equality and suppression equality must use the same semantic key. No path may independently recalculate a temperature bucket against a newer snapshot midway through the sort.

## 15. FastTrack pickup adapter

### 15.1 Adapter boundary

FastTrack support is an optional named adapter over the canonical pickup grouping algorithm. It does not own an alternate temperature partition, fallback rule, or constraint representation.

Adapter state is explicit per FastTrack feature:

- `ModNotLoaded`: no enabled FastTrack assembly is part of the loaded game;
- `ReplacementInactive`: FastTrack is loaded but the relevant replacement is not active, so the corresponding Klei inventory update, pickup grouping, or direct-comparison path remains authoritative;
- `Ready`: the relevant replacement is active and every required installed-binary and Harmony contract is verified; or
- `Incompatible`: the relevant replacement is active but at least one required contract cannot be proved.

Inventory, pickup grouping, and direct chore-comparison features are inspected separately. The mere presence of a FastTrack DLL never selects a FastTrack adapter, and one inactive or incompatible feature does not silently change the status of another feature.

### 15.2 Collision-free integer allocation

Current FastTrack constructs a private `PickupTagKey` whose equality depends on a 32-bit integer hash. The existing mod mixes the temperature index into that hash, which cannot prove collision freedom.

`FastTrackPickupGroupingKeyAllocator` instead maps the full composite identity:

```text
(original tag-bits hash, full TemperatureEligibilityClassKey) -> unique int
```

within one update session.

Rules are:

- identical composite keys reuse the same allocated integer;
- different original hashes receive different integers even when temperature classes match;
- different temperature classes receive different integers even when original hashes match;
- every candidate, including a missing-primary-element candidate, uses the allocator while temperature grouping is active;
- the pickup's original `tagBitsHash` field is never changed;
- only the constructor argument used for FastTrack's private dictionary key changes;
- with zero enabled constraints, the original hash is returned and no allocation occurs; and
- integer exhaustion fails explicitly and never wraps, reuses, or silently collapses a class.

### 15.3 Compatibility verification and safe fallback

At `OnAllModsLoaded`, the adapter verifies exactly once:

- expected FastTrack type identity;
- method signatures;
- unique IL anchor;
- required prefix/postfix/finalizer session hooks; and
- availability of the canonical Klei pickup grouping path.

A missing or duplicated anchor marks the adapter `Incompatible`; warning-and-continue is forbidden.

Preferred fallback patches FastTrack's `BeforeUpdatePickups` guard so it requests execution of the original Klei `UpdatePickups` and skips the incompatible replacement body. If that guard cannot be installed, exact-prefix removal is allowed only after verifying Harmony owner and target method metadata. It must never unpatch another mod's prefix.

If neither fallback can be proved, the adapter fails closed with one diagnostic containing FastTrack version, assembly digest, failed contract, and attempted fallback. It must not run temperature-unaware pickup collapsing.

Nested/reentrant FastTrack sessions restore prior state exactly and always clean up through finalizers.

## 16. Direct eligibility and fetch-coalescing patches

The direct correctness patches remain but become thin adapters over the immutable constraint/component services:

- `FetchManager.IsFetchablePickup`;
- `ClearableManager.CollectChores`;
- `FetchAreaChore.StatesInstance.Begin`;
- its candidate `CanReach` delegate;
- FastTrack `ChoreComparator.CheckFetchChore`; and
- `GlobalChoreProvider.ClearableHasDestination`.

Each direct pickup/destination check is O(1), allocation-free in its ordinary path, and performs at most:

- component-index lookup;
- null/disabled check;
- one canonical truncation;
- two integer comparisons.

Fetch-chore coalescing compares immutable constraints with explicitly named containment semantics. It does not compare Unity component reference identity as a substitute for equal configured behavior.

Every transpiler must validate a unique structural anchor. Local-variable numbers and `operand.ToString()` text are not sufficient as the sole anchor. Patch installation has a reflection/IL contract test, explicit Harmony ordering where interaction matters, and a defined failure mode.

## 17. Active-constraint state transitions

On zero to one enabled constraint:

- publish the new active snapshot;
- invalidate the combined fetch snapshot;
- begin status collection when enabled;
- leave ONI availability unchanged until inventory completeness is proved; and
- use exact decision fallback for pickup grouping until a current combined snapshot publishes.

On one to zero enabled constraints:

- publish the no-filter fetch state immediately;
- stop temperature pickup grouping immediately;
- stop status collection immediately;
- preserve ordinary ONI behavior; and
- release high-water variable buffers at the next safe main-thread boundary.

An enabled constraint change increments generation only when its normalized effective value changes. It does not synchronously scan all pickups, chores, storages, or worlds.

An enabled empty constraint counts as active because it affects its destination, but it contributes no temperature endpoints. Consumers may answer “allows none” directly without fragmenting pickup temperatures.

## 18. Defensive failure behavior

| Condition | Required behavior |
|---|---|
| Missing/stale pickup partition | Use exact `TemperatureDecisionBucket` grouping. |
| Incomplete world/tag inventory | Leave ONI's existing availability unchanged. |
| Fetch build sees a changed generation/version | Discard the whole candidate. |
| Unknown parent world | Do not guess; use subsystem-specific conservative behavior. |
| Old-session worker publication | Reject. |
| Duplicate identical registration/publication | Idempotent no-op. |
| Conflicting replacement registration | Replace atomically and emit a diagnostic in diagnostic builds. |
| Unknown removal | Idempotent; diagnostic only in diagnostic builds. |
| FastTrack binary mismatch | Disable only the incompatible replacement and prove the corresponding Klei implementation path. |
| FastTrack key-space exhaustion | Fail explicitly; never collide through wraparound. |
| Harmony update session throws | Finalizer clears/discards all thread-confined state. |
| Status option disabled | Install no status/inventory hooks and allocate no status structures. |

Diagnostics are rate-limited by game session and diagnostic key so a single stale condition cannot create a large-colony log storm.

## 19. Bounded resource policy

The endpoint reference array contains 5,001 integers, approximately 20 KiB. The decision-bucket accumulator uses fixed arrays for 5,002 classes. These bounded arrays may be retained and reused.

Sparse published structures allocate only occupied temperature classes or actual interval endpoints. Variable-capacity dictionaries and lists are reusable below named high-water thresholds and replaced above them after publication/cleanup.

Thresholds must be:

- named for the retained resource;
- justified in comments;
- covered by tests; and
- measured during final profiling.

No arbitrary timer-based eviction, weak-reference cache, global LRU, or background cleanup thread is introduced.

## 20. Test-driven implementation strategy

### 20.1 Red-green-refactor contract

Every behavioral chunk follows:

1. Add the smallest focused test that expresses the next approved requirement.
2. Run the narrowest command that executes that test.
3. Observe failure for the intended missing behavior, not a compilation or fixture accident unless compilation failure is the intended red.
4. Implement the minimum complete production behavior.
5. Run the focused test and its directly affected suite.
6. Refactor names, ownership, comments, and duplication while green.
7. Run all affected focused tests again.
8. Build the source when the chunk touches production integration.
9. Perform the shim scan and incomplete-work scan.
10. Commit the meaningful chunk.

Focused tests are not deferred. Only the expensive deep validation campaign is deferred.

### 20.2 Deterministic exhaustive tests

Tests cover all 5,002 temperature decision classes where the relevant property is finite. Randomized tests use fixed, named seeds and report the seed and generated case on failure. No external property-testing package is required.

Required property families include:

- constraint normalization and boundary behavior;
- interval normalization and membership;
- sparse amount series versus a simple reference summation;
- scoped partition equivalence versus direct constraint evaluation;
- minimal fragmentation converse;
- registry endpoint reference counts under add/replace/remove permutations;
- generation-current publication;
- world registration/removal/reparenting;
- duplicate and late publication;
- Klei pickup-path comparator/suppression agreement;
- FastTrack key uniqueness;
- lifecycle invalidation; and
- buffer high-water replacement.

### 20.3 Test doubles and reference models

Pure-domain tests use small, obviously correct reference implementations local to the test project. Reference models must be named as such and must not share the production normalization or classification implementation, which would make equivalence tests tautological.

Game and FastTrack adapter tests use semantically named stubs or captured IL fixtures. Tests must not rely solely on source text matching when reflection or instruction-shape verification can assert the actual contract.

### 20.4 Deep validation boundary

Only after all rewrite chunks are integrated will the implementation run the complete campaign:

- full mod test project;
- ONI Mod Pipeline `diagnose`;
- ONI Mod Pipeline `validate`;
- ONI Mod Pipeline `build`;
- ONI Mod Pipeline `test`;
- exact build-result installation to the guarded development target;
- base-game content mode with the Klei inventory and pickup grouping paths;
- base-game content mode with the FastTrack inventory and pickup grouping paths;
- Spaced Out content mode with the Klei inventory and pickup grouping paths;
- Spaced Out content mode with the FastTrack inventory and pickup grouping paths;
- option-on/option-off combinations;
- save/load/main-menu/new-load lifecycle matrix;
- large-colony functional scenarios;
- CPU profiling;
- allocation and GC profiling;
- retained-memory/high-water checks;
- Harmony and exception log review; and
- comparison against recorded performance budgets and correctness invariants.

A failure in this campaign is fixed with focused TDD and then the affected final gates are rerun. The rewrite is not released in a partially migrated state.

## 21. ONI Mod Pipeline integration

The repository-local pipeline is the authoritative build, test, package, install, and acceptance path. The profile already declares:

```toml
[[test-projects]]
id = "delivery-temperature-limit-regressions"
path = "Tests/DeliveryTemperatureLimit.Tests.csproj"
required = true
```

The rewrite expands that required project; it does not create an unregistered side test command.

Normal focused TDD uses `dotnet test` filters against the test project. Final integration uses the pipeline from the repository root. If `oni-mod-pipeline` is not installed on `PATH`, the checkout-local equivalent is:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- <command> --mod mods/delivery-temperature-limit-supercooled
```

The exact build-result path printed by `build` is carried into `install`; the implementer must not substitute “latest” or copy the source-root DLL.

The pipeline profile will require additional manual acceptance cases for:

- base-game content mode with Klei inventory/pickup paths under large-colony temperature grouping and status load;
- base-game content mode with verified FastTrack pickup/background-inventory replacements;
- Spaced Out content mode with Klei inventory/pickup paths and authoritative multi-world topology;
- Spaced Out content mode with verified FastTrack pickup/background-inventory replacements and authoritative multi-world topology;
- status option disabled with no temperature instrumentation work;
- multiple asteroid and rocket-interior parent-world aggregation;
- constraint edits while fetch updates are active;
- main-menu/load and save-reload lifecycle cleanup; and
- profiler/allocation evidence tied to the exact installed build result.

These are profile configuration changes and require exact configuration approval before implementation.

## 22. Meaningful chunk and commit policy

The rewrite is one release-level migration but will not be one giant commit.

A meaningful chunk is commit-ready only when:

- it began with a focused failing test;
- the intended behavior is complete and green;
- related focused tests pass;
- production source is buildable when touched;
- naming is semantically correct;
- non-obvious invariants have durable comments;
- no temporary diagnostics, disabled assertions, placeholder branches, unresolved `TODO`, or half-migrated call site remains;
- the shim scan finds no unapproved compatibility layer; and
- the staged snapshot contains only that coherent chunk.

The implementation plan will prescribe a precise commit after each such chunk. Commit messages describe the completed domain outcome, not a vague sequence number.

The user has approved the strategy of committing each meaningful implementation chunk. Repository commit safety still applies: before each commit, inspect the exact staged diff and use the repository's committing workflow. Pushing remains separately authorized and is outside this design.

## 23. Planned source-module shape

The implementation plan may refine filenames while preserving these module boundaries:

```text
Source/
  TemperatureConstraints/
    DeliveryTemperatureConstraint.cs
    TemperatureDecisionBucket.cs
    ActiveTemperatureConstraintSnapshot.cs
    TemperatureConstraintRegistry.cs
    TemperatureLimitComponentIndex.cs
  WorldTopology/
    WorldParentTopologySnapshot.cs
    WorldParentTopologyCatalog.cs
  WorldInventory/
    TemperatureAmountAccumulator.cs
    TemperatureAmountSeries.cs
    CompleteWorldResourceTemperatureAmounts.cs
    WorldResourceTagCoverage.cs
    WorldResourceTemperatureSeriesPublication.cs
    CompleteWorldResourceTemperatureAmountsBuilder.cs
    WorldTemperatureInventoryCatalog.cs
  FetchEligibility/
    AllowedTemperatureIntervalSet.cs
    TemperaturePartitionDefinition.cs
    TemperatureEligibilityClassKey.cs
    PickupTemperaturePartitionCatalog.cs
    FetchRequestTopologyTracker.cs
    FetchTemperatureEligibilitySnapshot.cs
    FetchTemperatureEligibilityBuilder.cs
  Runtime/
    DeliveryTemperatureGameSession.cs
    DeliveryTemperatureGameSessionHost.cs
  Patches/
    DirectFetchEligibilityPatches.cs
    FetchTemperatureSnapshotPatches.cs
    KleiWorldInventoryTemperaturePatches.cs
    KleiPickupTemperatureGroupingPatches.cs
  FastTrack/
    FastTrackFeatureCompatibilityState.cs
    FastTrackCompatibilityReport.cs
    FastTrackCompatibilityInspector.cs
    FastTrackPickupGroupingKeyAllocator.cs
    FastTrackWorldInventoryTemperaturePatches.cs
    FastTrackPickupTemperaturePatches.cs
```

The old `Patch.cs`, `PatchFastTrack.cs`, and `StatusItems.cs` responsibilities should be split rather than becoming parallel legacy entry points. Once call sites migrate, obsolete types are deleted in the same rewrite.

## 24. Configuration approval dossier

This specification approves architecture, not configuration edits. Repository policy requires exact approval before implementation changes either file below.

| Configuration file | Exact intended setting change | Behavioral and pipeline impact | Smallest viable change |
|---|---|---|---|
| `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj` | Add `<Compile Include="..\Source\..." Link="Production\..." />` entries for each new pure-domain production source file required by the tests; add no new package unless a separately approved need is demonstrated | Makes the existing required test project compile and exercise the actual domain implementation under MSTest; preserves the existing pipeline test-project ID | Add only explicit source links needed by tests; retain `net10.0`, MSTest SDK, nullable annotations, warnings-as-errors, and locked restore |
| `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml` | Append specifically named required `[[acceptance-checks]]` entries for the final performance, FastTrack, status-disabled, multi-world, concurrent-edit, and lifecycle scenarios | Makes the final deep manual validation part of the digest-bound pipeline acceptance contract rather than an undocumented checklist | Append acceptance entries only; do not change build, package, listing, installation, or existing acceptance settings |

No change is currently required to `Source/DeliveryTemperatureLimit.csproj` because SDK default item inclusion will compile new production `.cs` files. No package, lockfile, build target, pipeline source, or CI change is approved by this design.

If implementation discovers a need for another configuration edit, it must stop and present the exact file, setting, effect, and smallest alternative before editing.

## 25. Acceptance criteria

The rewrite is complete only when all of the following are true:

1. Intentional serialization, options, and player-visible behaviors remain compatible.
2. `TemperatureIndexData`, its getter, and the global band model are absent.
3. No unapproved shim or parallel legacy algorithm remains.
4. Constraint add/replace/remove is O(1) and effective no-ops do not increment generation.
5. Hot readers never rebuild constraint indexes.
6. Every temperature conversion uses the canonical truncation and decision-bucket implementation.
7. Storage eligibility uses normalized interval sets, not per-band tag sets.
8. Status inventory stores only occupied temperature classes and answers range totals from prefix sums.
9. Status queries do not enumerate every `WorldContainer`.
10. The Klei inventory update path publishes one atomic complete-world contribution without a second pickupable enumeration.
11. The FastTrack inventory update path publishes one atomic complete-world contribution for its actual full update and exactly one atomic resource-tag contribution for each later incremental update; it never rebuilds an unrelated world or tag.
12. FastTrack coverage distinguishes a known-absent resource tag from a present tag whose current series has not arrived; incomplete data never becomes a fabricated zero.
13. Base-game and Spaced Out content modes both derive topology from authoritative registered worlds rather than hard-coded DLC assumptions.
14. No unqualified use of “vanilla” remains in architecture, production names, tests, comments, diagnostics, commit messages, or acceptance records.
15. Pickup partitions are scoped by parent world and requested tags.
16. Unrelated constraints cannot fragment a pickup's optimized partition.
17. Missing/stale pickup partitions use exact decision classes and remain correct.
18. Optimized partition equivalence and minimal fragmentation pass exhaustive tests over all 5,002 classes.
19. The combined fetch snapshot publishes only when every captured generation/version remains current.
20. FastTrack uses collision-free update-local key allocation.
21. A FastTrack mismatch cannot silently run a temperature-unaware replacement path.
22. Worker code reads immutable world topology and does not call Unity or `ClusterManager`.
23. Late old-session publications are rejected.
24. Game-session shutdown and repeated component/world cleanup are idempotent.
25. Fixed reusable buffers are bounded and oversized variable collections are released.
26. Status-disabled mode installs no status/inventory hooks and performs no associated allocations.
27. Every implementation chunk has focused red/green/refactor evidence and a coherent commit.
28. The existing required pipeline test project contains the complete automated suite.
29. The one final pipeline/deep-validation campaign passes against the exact installed build result for all four base-game/Spaced-Out and Klei/FastTrack combinations.
30. Player logs contain no new relevant lifecycle, Harmony, FastTrack, worker, or unhandled exceptions.
31. Final profiler evidence shows no remaining avoidable dense `world × tag × temperature` work, no hot-path constraint rebuild, no global unrelated-tag partition fragmentation, no per-FastTrack-delta complete-world reconstruction, and no unbounded retained collection growth.

## 26. Rejected alternatives

### 26.1 Keep `TemperatureIndexData` as a facade

Rejected because no named consumer was found and a facade would either preserve obsolete global semantics or lie about new scoped semantics.

### 26.2 Use one underflow bucket per negative integer Kelvin

Rejected because valid constraints cannot distinguish any truncated value below `0 K`. Negative Celsius values above absolute zero are already represented by Kelvin buckets `0..273`.

### 26.3 Keep dense arrays because 5,000 is small

Rejected because 5,000 is small only in isolation. Multiplication by tags, worlds, inventory refreshes, status items, and mutable dictionary overhead is precisely the large-colony failure mode.

### 26.4 Rebuild optimized data immediately in every setter

Rejected because a UI or copy-settings operation should not synchronously traverse global fetch topology. Setters publish the new constraint generation; the next authoritative traversal builds a complete snapshot.

### 26.5 Keep stale optimized data because ONI updates repeatedly

Rejected because temporary false merging can suppress the only eligible pickup and is a correctness failure. Exact decision fallback is bounded and correct.

### 26.6 Use a larger or better FastTrack hash

Rejected because no 32-bit hash proves collision freedom. Update-local allocation produces unique integers for actual composite keys.

### 26.7 Maintain separate Klei and FastTrack domain algorithms

Rejected because behavioral drift and duplicate optimization logic would be likely. Both are adapters over one domain model.

### 26.8 Deep-test every intermediate migration state

Rejected by the approved release strategy. Focused TDD validates every chunk; expensive full integration occurs after the complete rewrite is assembled.

### 26.9 Make the rewrite one final commit

Rejected because the user requires a commit after every meaningful complete chunk. The release boundary is big-bang; the history remains incremental and reviewable.

### 26.10 Rebuild a complete world publication after every FastTrack `RunUpdate`

Rejected because current FastTrack performs one complete update after prefab initialization and then refreshes only one resource tag per `RunUpdate`. Reconstructing every world/tag contribution for each later invocation would add precisely the large-colony work FastTrack removes. The adapter publishes complete-world, coverage, and single-tag contracts according to the authoritative enumeration that actually occurred.

## 27. Final decision

Implement one coordinated rewrite based on scoped immutable constraints, sparse temperature amount series, explicit complete-world/coverage/single-tag inventory publications, normalized storage intervals, parent-world/tag pickup partitions, collision-free FastTrack keys, authoritative content-neutral world topology, and game-session generation safety. Preserve intentional saves and gameplay; remove the old global temperature-index subsystem and accidental public surface. Use exact decision-bucket fallback whenever optimized data cannot be proved current. Develop through focused TDD and coherent chunk commits, then run the repository-local ONI Mod Pipeline and the complete four-combination base-game/Spaced-Out and Klei/FastTrack large-colony validation campaign once all fixes are integrated.

No open architecture decisions remain in this specification.
