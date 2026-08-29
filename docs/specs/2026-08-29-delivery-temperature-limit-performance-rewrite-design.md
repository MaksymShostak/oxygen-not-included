# Delivery Temperature Limit: Large-Colony Performance Rewrite Design

- **Status:** Approved architecture and implementation plan; adversarial grilling and independent validation amendments integrated
- **Date:** 2026-08-29
- **Mod:** Delivery Temperature Limit (Supercooled)
- **Game-loaded runtime target:** .NET Standard 2.1 inside ONI's Unity/Mono runtime
- **Production language ceiling:** C# 8 from the `netstandard2.1` SDK default, including production files linked into tests
- **Test and static-analysis target:** .NET 10 with test-only C# 14
- **Development pipeline:** repository-local ONI Mod Pipeline
- **Release strategy:** one coordinated performance rewrite; no partially migrated release
- **Test strategy:** focused TDD throughout; the pipeline's `validate`, `build`, and `test` gates before every meaningful implementation commit; one modest manual baseline/candidate comparison after all rewrite chunks are integrated
- **Compatibility strategy:** preserve intentional player/save/runtime contracts; remove accidental implementation surface and compatibility shims; provide best-efforts support for the specifically verified FastTrack `0.18.4.0` contract only

## 1. Executive summary

The current mod avoids several obvious hot-path costs, but its central optimization is global: every enabled delivery-temperature limit contributes endpoints to one process-wide temperature partition, and that partition is then used for every world, tag, storage availability calculation, pickup sort, and status calculation. In a very large colony, the number of active limits, fetch chores, material tags, worlds, rocket interiors, and pickupables can all grow at the same time. The present representation therefore multiplies otherwise independent dimensions and can approach dense `world × tag × temperature-band` work and storage.

The rewrite replaces that model with immutable, scoped, purpose-built representations:

- exact immutable delivery constraints registered by token-owned component identity;
- fixed-size endpoint reference counts for cheap constraint updates outside hot readers;
- one canonical `10,002`-bucket temperature decision model covering values below the configurable range, integer Kelvin `0..9999`, and values at or above the maximum configurable exclusive endpoint; missing primary elements remain a separate non-temperature classification;
- sparse, prefix-summed temperature amount series for status availability;
- normalized allowed-temperature interval sets for storage destinations;
- tag- and parent-world-specific endpoint facts derived from the same authoritative fetch traversal, with partition definitions interned only inside one pickup-update grouping session;
- collision-free FastTrack grouping-key allocation rather than hash mixing;
- immutable snapshots with explicit constraint, fetch-topology, world-topology, and game-session generations;
- correctness-preserving exact-temperature-class fallback whenever an optimized snapshot cannot be proved current; and
- one game-session composition root that owns all mutable state and rejects late work after unload.

The design deliberately does **not** retain `TemperatureLimit.TemperatureIndexData`, `TemperatureLimit.getTemperatureIndexData()`, the global operational-band model, or an exact whole-assembly public-surface compatibility test. No known external consumer uses those members. FastTrack does not call them; this mod only uses them internally while patching FastTrack. Keeping them would require maintaining the obsolete global model or presenting misleading semantics under an old name.

The implementation will be test-driven at every focused change. Each meaningful, internally complete chunk will be committed separately only after its focused tests pass and the repository-local pipeline passes `validate`, `build`, and `test`. Final exact-candidate validation is deferred until the complete big-bang rewrite exists, but the production pipeline is not deferred: it is the mandatory integration gate throughout development.

The final game exercise is intentionally modest and indicative. The user will compare the published baseline package with the release candidate using two original late-game colonies: one base-game content-mode colony and one Spaced Out content-mode colony. Each original is preserved and copied into separate baseline-role and candidate-role derivatives, producing exactly four one-pass game sessions. Every other mod, including FastTrack, is disabled. There is no automated game control, FastTrack game run, CPU-profiler campaign, allocation/GC campaign, beta release, or repeated timing series.

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

Current upstream source is compatibility evidence, not a permanent binary contract. No official Workshop-distributed FastTrack DLL could be obtained or proven byte-identical during planning. Static compatibility tests therefore use the latest available GitHub release asset described in section 15, with an explicit best-efforts qualification; the release does not require or perform an in-game FastTrack run.

### 2.2 Evidence baselines and authoritative installed build

The implementation must preserve these planning facts as named static contracts rather than relying on memory:

- published Delivery Temperature Limit source baseline commit: `5f7bf43aa823bbb4771936b058c6d573484b6d91`;
- published baseline assembly version: `2026.8.26.0`;
- published baseline `DeliveryTemperatureLimit.dll` SHA-256: `02A14F2E123F42BDD87847C15AB434DAFC8A4D4BC92B465F9DCD367364BF465E`;
- installed public ONI changelist: `744825`;
- installed ONI build branch: `release`;
- installed Unity version: `6000.3.5f2`;
- installed runtime family: Unity `MonoBleedingEdge`;
- installed `Assembly-CSharp.dll` SHA-256: `A58E04D0FFDF89B86FB28B71AD900625B3B539DB30D67F8C6269F73A9F5AE599`; and
- installed `Sim.MaxTemperature`: exactly `10000f`.

The runtime-target rationale is grounded in Klei's Unity 6 transition discussion: Klei developer EricKlei confirmed that ONI's Unity assemblies require .NET Standard 2.1 features ([Klei forum developer response](https://kleiforums.com/forums/topic/170067-modders-now-face-a-dilemma-with-unity-6/?comment=1854227&do=findComment)). PLib's official build guidance likewise documents `CopyLocalLockFileAssemblies` for a .NET Standard 2.1 ILRepack build ([PLib README](https://github.com/peterhaneve/ONIMods/blob/main/PLib/README.md#usage)). The installed Unity `6000.3.5f2` assemblies are the local build-time authority. External guidance explains the target/build settings; the project and installed-assembly contract tests enforce them for this release.

`KleiVersion.ChangeList` and `KleiVersion.BuildBranch` are the authoritative source for the supported public build. `Sim.MaxTemperature`, the inclusive validity checks in `PrimaryElement.OnDeserialized`, and the matching range check in `SimMessages.ModifyCell` are the authoritative sources for the maximum temperature ONI accepts. Exact `10000 K` is valid ONI state; it is nevertheless rejected by every enabled Delivery Temperature Limit range because this mod deliberately preserves an exclusive high endpoint whose maximum configurable value is `10000`.

The installed element data also disproves the assumption that every applicable material lies below `5000 K`. Examples in `StreamingAssets/elements` include `MoltenCarbon` through `5100 K`, `MoltenNiobium` through `5017 K`, `MoltenTungsten` through `6203 K`, and gas phases whose defaults include `CarbonGas` at `5700 K`, `NiobiumGas` at `5500 K`, and `TungstenGas` at `6500 K`. Because the mod evaluates `Pickupable.PrimaryElement`, its semantics include bottled liquids, canistered gases, and heated debris where ONI represents them as pickupables.

If the installed public ONI changelist, release branch, maximum temperature contract, or referenced method signatures change before release, implementation stops and requests a design/update decision. It must not silently broaden the advertised compatibility range or rewrite the fixture expectation.

### 2.3 Existing strengths that remain requirements

The current implementation already recognizes several important performance facts:

- repeated Unity `GetComponent` transitions are expensive, so `TemperatureLimit.Get(GameObject)` uses an instance-ID lookup;
- pickup grouping must distinguish temperatures only where a constraint can make eligibility differ;
- immutable aggregate publication is safer for worker readers than exposing fields updated independently;
- FastTrack background inventory work requires thread-confined intermediate sums;
- status-temperature accounting is entirely disabled when its option is disabled; and
- direct delivery eligibility uses integer truncation and inclusive-low/exclusive-high semantics.

The rewrite preserves those intentions while replacing representations whose scaling or correctness properties are inadequate.

### 2.4 Existing large-colony slowdown mechanisms

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

### 2.5 What cannot be optimized away

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
3. Make endpoint reference-count mutation O(1); perform any immutable-snapshot reconstruction eagerly on the mutating main thread, never lazily in a pickup, inventory, status, comparator, or other hot reader.
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
15. Provide exhaustive deterministic domain tests across every temperature decision bucket.
16. Compile the game-loaded assembly for `netstandard2.1` with a strict C# 8 production ceiling, compile test-only analysis/fixture code under `net10.0`/C# 14, compile the same linked production sources without post-C#-8 syntax, and statically inspect the actual merged production DLL.
17. Integrate development and release evidence through the existing repository-local ONI Mod Pipeline without modifying its profile.
18. Keep FastTrack discovery and adaptation entirely off the normal Klei implementation paths when FastTrack is absent, disabled, or inactive for the loaded game.

## 4. Non-goals

The rewrite will not:

- change the serialized field names or type identity of `TemperatureLimit`;
- change the mod options, defaults, localized player-facing meaning, or `supportedContent: ALL` metadata;
- change the integer truncation rule used by delivery eligibility;
- expose a configurable endpoint above ONI's current `10000 K` maximum;
- claim compatibility with historical ONI builds or implement a historical-signature compatibility analyzer;
- modify ONI's simulation, inventory enumeration cadence, or FastTrack's general scheduling strategy;
- introduce a general-purpose compatibility framework for hypothetical mods;
- expose the new domain model as a public extension API;
- keep the old global band model behind an adapter;
- add a third-party property-testing, benchmarking, or mocking dependency merely for convenience;
- make wall-clock timing assertions part of ordinary unit tests;
- automate ONI gameplay, run an in-game FastTrack validation, require repeated timing samples, or conduct a CPU/allocation/GC profiling campaign;
- make a concise Markdown performance-result record a release or publishing blocker;
- publish a beta; or
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
- `PickupTemperatureGroupingSession`, not `groupCache`;
- `TemperatureConstraintRegistrationToken`, not `RegistrationData`;
- `GameSessionTemperatureLimitRegistrationToken`, not `TemperatureLimitRegistration`;
- `ClearableDestinationSweepEligibility`, not a generic `EligibilityHelper`;
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

### 5.6 Runtime, tooling, and pipeline boundaries

The game-loaded assembly targets exactly `netstandard2.1`, following current ONI mod-development guidance and the installed game's supported API surface. Its target-framework-derived language ceiling is exactly C# 8, consistent with Microsoft's target-framework/language-version guidance ([C# language version errors and target mapping](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/feature-version-errors)). It must not target .NET 8, .NET 9, .NET 10, or multiple frameworks; it must not ship a modern-runtime sidecar or add a `LangVersion` override. The `net10.0` test project may use its test-only C# 14 default plus `System.Reflection.Metadata` and other framework-provided tooling APIs for static analysis, but a physical production file linked into that project remains C# 8-compatible and may not depend on a newer syntax/API merely because the test compiler accepts it.

The pipeline is authoritative during development, not merely for the eventual release candidate. Direct filtered `dotnet test` commands are permitted for the inner red/green loop, but every meaningful commit boundary requires fresh successful pipeline `validate`, `build`, and `test` runs. The pipeline-declared test project remains the single authoritative automated suite. `oni-mod-pipeline.toml` remains byte-for-byte unchanged.

## 6. Intentional compatibility contract

### 6.1 Preserve

The rewrite preserves:

- the public `DeliveryTemperatureLimit.TemperatureLimit` component identity required by serialized saves;
- private serialized fields named `lowLimit` and `highLimit`, with their existing integer representation;
- public bound members retained for intentional compatibility, with `MinValue = 0` and `MaxValue = OniStorableTemperatureBounds.MaximumTemperatureKelvin` (`10000` for ONI build `744825`);
- disabled behavior when the normalized high limit is zero;
- inclusive lower and exclusive upper delivery eligibility;
- C# integer truncation toward zero before comparison;
- enabled-but-empty constraints when `lowLimit >= highLimit` and `highLimit > 0`;
- construction-material option behavior;
- status-temperature option behavior;
- opt-in JSON property identities/types/defaults and PLib's shared `config.json` location under the unchanged mod assembly/static ID;
- copy-settings behavior;
- storage, sweeping, fetch coalescing, and direct delivery decisions;
- equivalent delivery decisions for a structurally verified FastTrack `0.18.4.0` contract on a best-efforts basis; this is not a promise for other FastTrack releases or proof of the Workshop DLL; and
- the mod entrypoint and Unity/Klei/PLib/Harmony-required callbacks; and
- public `STRINGS.TEMPERATURELIMIT` plus its existing `LABEL`, `RANGE_SEPARATOR`, `TOOLTIP_RANGE`, `TOOLTIP_NOTSET`, and `SIDESCREEN_TITLE` `LocString` fields, whose full names are intentional Klei localization keys.

The localization identifier is a Klei ecosystem contract, not a legacy shim. Its source file is semantically renamed to `DeliveryTemperatureLimitStrings.cs`, but no parallel `DeliveryTemperatureLimitStrings` type or duplicate localization-key tree is introduced.

### 6.2 Authoritative legacy-removal registry

This is the single authoritative removal registry. The implementation plan, coordinated activation, no-shim tests, and final audit consume this registry by reference and must not maintain competing subsets:

- members/types: `TemperatureLimit.TemperatureIndexData`, `TemperatureLimit.getTemperatureIndexData()`, `TemperatureLimit.UpdateIndexes()`, `allLimits`, `limitsDirty`, and `storageFetchableTagsPerTemperatureIndex`;
- representations/behaviors: lazy index rebuilding, the global operational/dense storage band model, dense `(Tag, temperatureIndex) -> float` status dictionaries, and FastTrack temperature hash mixing; and
- superseded production files: `Limits.cs`, `Patch.cs`, `PatchFastTrack.cs`, `StatusItems.cs`, and `Harmony.cs`.

The exact public-surface regression test that demands byte-for-byte or whole-public-surface preservation of accidental implementation members is replaced rather than retained. One executable `RemovedArchitectureIdentities` table in the no-shim contract tests transcribes the registry once; every executable source/metadata/file assertion consumes that table rather than restating its own subset.

### 6.3 Why `TemperatureIndexData` is not retained

`TemperatureIndexData` was introduced as an internal atomic-publication mechanism for this mod's own patches. It was not documented as an extension API. The investigation found:

- no reference in current FastTrack source;
- no reference in FastTrack compatibility code;
- no local installed mod assembly referencing its type or getter other than copies of Delivery Temperature Limit itself; and
- no user or developer documentation promising the type.

Retaining only the type name while changing its semantics would be a misleading shim. Retaining its semantics would force the obsolete global band model to remain alive. The exact public-surface regression test must therefore be replaced by a curated compatibility contract that asserts only intentional runtime and serialization surface.

If a later named external mod is proven to consume the member, implementation stops and prepares the full shim-exception dossier required by section 5.1. It does not pre-emptively restore the type.

## 7. Canonical temperature semantics

### 7.1 `OniStorableTemperatureBounds`

`OniStorableTemperatureBounds` is a pure internal source file shared verbatim by the `netstandard2.1` production project and the `net10.0` test project. Its named constant is:

```csharp
internal const int MinimumTemperatureKelvin = 0;
internal const int MaximumTemperatureKelvin = 10000;
```

The adjacent comment must cite the current binary evidence precisely: ONI release changelist `744825`, `Sim.MaxTemperature == 10000f`, inclusive acceptance by `PrimaryElement.OnDeserialized`, and the corresponding `SimMessages.ModifyCell` range check. The code must not call the game to discover this value during play. `OniStorableTemperatureBoundsContractTests` inspect the installed `Assembly-CSharp.dll` through `System.Reflection.Metadata` and fail with the observed build, field, value, assembly version, and digest if the contract changes.

The constant is a reviewed compile-time compatibility boundary, not an evergreen claim. A future public ONI update that changes it requires a source review and a new release decision.

### 7.2 `DeliveryTemperatureConstraint`

`DeliveryTemperatureConstraint` is an internal immutable value describing a destination's exact configured behavior:

- `MinimumInclusiveKelvin`;
- `MaximumExclusiveKelvin`;
- `IsEnabled`;
- `IsEmpty`; and
- `Allows(float temperatureKelvin)`.

`FromSerializedLimits(int serializedLowLimit, int serializedHighLimit)` independently clamps both serialized integer fields to `0..OniStorableTemperatureBounds.MaximumTemperatureKelvin`, currently `0..10000`, before interpreting state. A normalized high value of zero means disabled and always has `IsEmpty == false`. Only an enabled constraint can be empty; it is empty exactly when its normalized minimum is greater than or equal to its normalized maximum. Empty and disabled are not interchangeable. This ordering deliberately preserves a negative serialized high value as disabled after clamping.

`Allows` must apply the exact existing conversion before comparison:

```csharp
int truncatedKelvin = (int)temperatureKelvin;
```

No caller may independently round, floor, clamp, or convert through Celsius. The upper comparison remains exclusive. Consequently, a storable at exactly `10000 K` is valid ONI state but cannot be accepted by an enabled Delivery Temperature Limit range; a disabled constraint preserves ordinary ONI behavior.

### 7.3 `TemperatureDecisionBucket`

`TemperatureDecisionBucket` is the one canonical classification used wherever a full optimized partition is unavailable or where amounts need stable integer-temperature identity. Its bucket count is defined by the formula:

```text
1 below-range bucket
+ OniStorableTemperatureBounds.MaximumTemperatureKelvin individual integer buckets
+ 1 at-or-above-maximum bucket
= 10,002 buckets for ONI build 744825
```

The buckets are:

1. **`BelowMinimumKelvinOrdinal`:** `truncatedKelvin < OniStorableTemperatureBounds.MinimumTemperatureKelvin`.
2. **Integer Kelvin:** one bucket for each value from `0` through `OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1`, currently `0..9999`.
3. **`AtOrAboveMaximumKelvinOrdinal`:** `truncatedKelvin >= OniStorableTemperatureBounds.MaximumTemperatureKelvin`, currently `>= 10000`.

`FirstIntegerKelvinOrdinal` names the `0 K` ordinal and `HighestIntegerKelvinOrdinal` names the current `9999 K` ordinal. Code and tests must use those ordinal names rather than reusing a Kelvin-value constant merely because its current number happens to match an ordinal.

The apparent asymmetry is intentional:

- each current integer Kelvin value `0..9999` can be distinguished by a valid configured endpoint and therefore requires a separate bucket;
- every truncated value below `0 K` is rejected by every enabled nonempty valid constraint, so those values are behaviorally equivalent;
- every truncated value at or above `10000 K` is rejected by every enabled nonempty valid constraint because the maximum endpoint is exclusive and cannot exceed ONI's reviewed maximum;
- ordinary negative Celsius temperatures are not negative Kelvin. The physical span from absolute zero through `0 °C` maps to integer Kelvin buckets `0..273`; and
- values greater than `-1 K` but less than `0 K` truncate to zero under C# rules and therefore belong to the `0 K` bucket.

`TemperatureDecisionBucket.FromTemperature(float temperatureKelvin)` is the only permitted conversion function. Tests pin `-1.0`, values immediately above `-1`, negative fractional values truncating to zero, `0`, every configured-boundary adjacency, `9999`, values immediately below `10000`, exact `10000`, and values above the maximum. Exhaustive loops derive their upper bound from named constants; they must not repeat the literal `10002` throughout the code.

Missing `PrimaryElement` is not assigned an invented Kelvin value or a synthetic bucket ordinal. APIs operating on pickupables use a distinct `MissingPrimaryElement` classification so null behavior remains explicit and cannot collide with a real temperature.

## 8. Constraint registration and component lookup

### 8.1 `TemperatureConstraintRegistry`

The registry is an internal instance service keyed by Unity component instance ID, but it contains no Unity, Harmony, PLib, or FastTrack type.

It provides:

- O(1) add or replace;
- O(1) remove;
- exact enabled-constraint count;
- exact enabled-nonempty-constraint count;
- a monotonic constraint generation;
- fixed endpoint reference counts for every configurable endpoint from `0` through `OniStorableTemperatureBounds.MaximumTemperatureKelvin`, inclusive; and
- eager immutable `ActiveTemperatureConstraintSnapshot` publication on the mutating thread.

Endpoint counts include only enabled, nonempty constraints because disabled and empty constraints cannot create a temperature eligibility boundary. The snapshot records enabled and enabled-nonempty counts, so consumers can distinguish bypass from an active empty-only state without receiving every registered component constraint.

Mutation rules are:

- identical repeated registration is an idempotent no-op and does not increment generation;
- changed registration for the same identity replaces atomically, adjusts old/new endpoint counts, and increments generation once;
- unknown or stale-token removal is an idempotent no-op and cannot remove a replacement;
- no public method exposes the mutable dictionary or endpoint array; and
- the registry exposes no mutation callback surface; callers coordinate other session services only after the registry operation returns and its private lock has been released.

`TemperatureConstraintRegistrationToken` combines component instance identity with a nonzero registration sequence. Both the registration sequence and constraint generation use checked increments while the registry lock is held; exhaustion throws before a partial mutation and never wraps into a reusable identity.

The active snapshot contains only its immutable generation, enabled count, enabled-nonempty count, and deterministically reconstructed sorted unique endpoint array. It deliberately does not copy a `RegisteredTemperatureConstraint` list: save/load or bulk spawn churn must not turn each changing registration into an O(number of already registered components) snapshot copy. Component-specific state remains registry-owned and is addressed by tokens.

There is no dirty flag and no worker-triggered rebuild. Endpoint-count updates are direct array operations. A parallel fixed membership bitset changes only when an endpoint count crosses zero; snapshot reconstruction enumerates its set bits in ascending word order (currently `157` words), never all `10,001` counters. A genuine mutation that changes counts/generation but not endpoint membership reuses the previous immutable endpoint view and does not perform that scan. No per-pickup, per-tag, per-world, per-status, per-comparator, or per-frame path may scan the complete configurable range. Static architecture tests enforce these rules.

### 8.2 `TemperatureLimitComponentIndex`

This internal service maps a `GameObject` instance ID to the corresponding `TemperatureLimit` for direct game-patch checks. Its API uses semantic operations such as register, replace, resolve, and remove-if-owned.

Ownership tokens prevent delayed cleanup from removing a newer component that reuses an instance ID. The dictionary value is the private sealed immutable `TemperatureLimitComponentIndexEntry`; it is not a generic `Entry`. `TryGetRegisteredComponent` returns the component and its exact registration token from one captured entry so Unity-destroyed cleanup cannot pair an old component with a newer token. Owned removal uses the .NET Standard 2.1-compatible atomic key-and-value `ICollection<KeyValuePair<int, TemperatureLimitComponentIndexEntry>>.Remove(expectedPair)` operation; it never performs a separate owner check followed by key-only removal. The index is game-session-scoped and is discarded as a unit at shutdown. Pure linked tests use the exact identities `global::Tag` and `DeliveryTemperatureLimit.TemperatureLimit`; a member-parity contract permits only members actually consumed by linked production source, so a convenient test-only namespace or richer fake cannot hide coupling.

### 8.3 `TemperatureLimit` lifecycle and setters

`TemperatureLimit` remains the serialized Unity component. It will:

- normalize loaded fields once at registration;
- register its immutable constraint and component index entry in `OnSpawn`;
- retain the returned registration token;
- make setter and copy operations no-ops when normalized values do not change;
- replace its registry entry only while its token belongs to the current active session;
- remove only its own registrations in `OnCleanUp`; and
- never rebuild global derived data directly.

`TemperatureLimit.Get(GameObject gameObject)` first applies Unity's overloaded `gameObject == null` check before reading an instance ID. It obtains the indexed component and exact registration token through one `TryGetRegisteredComponent` read. If that component is Unity-destroyed (`component == null` under Unity's overloaded operator), it uses the paired token to request token-owned stale removal and returns `null`. It never performs separate component/token reads, returns a destroyed Unity object, mistakes `ReferenceEquals` for Unity liveness, or permits a stale cleanup race to remove a newer owner.

The existing per-component `OnLoadLevel` global reset is deleted.

## 9. Game-session lifecycle and concurrency

### 9.1 Composition root

`DeliveryTemperatureGameSessionHost` atomically publishes the current `DeliveryTemperatureGameSession`. The session owns:

- `TemperatureConstraintRegistry`;
- `TemperatureLimitComponentIndex`;
- `WorldParentTopologyCatalog`;
- `WorldResourceTemperatureAmountCatalog`;
- `FetchRequestTopologyTracker`;
- the current combined fetch eligibility snapshot;
- FastTrack adapter status; and
- rate-limited session diagnostics.

Static Harmony entry points may reach the host. Domain algorithms remain instance-based and testable.

### 9.2 Session generations

Each new session receives a monotonic nonzero `GameSessionGeneration`. The lock-free host allocator uses a checked compare/exchange loop; it throws before publication at `long.MaxValue` rather than allowing `Interlocked.Increment` wraparound. Registrations use the semantically exact `GameSessionTemperatureLimitRegistrationToken`; registrations, snapshots, and update sessions carry the session generation. A candidate publication is accepted only when the target session is active and all captured generations/versions remain current.

This rejects background work that completes after main-menu return, save reload, topology mutation, or constraint mutation.

### 9.3 Start and shutdown

The intended authoritative hooks are:

- `Game.OnLoadLevel()` to ensure the current session;
- `Game.DestroyInstances()` prefix to stop acceptance and atomically detach the session; and
- the corresponding finalizer to release session-owned mutable state even after an exception.

`DeliveryTemperatureGameLoadAuthorityPatches` resides under `RuntimePatchInstallation` because game-load authorization spans whichever Klei/FastTrack responsibilities the immutable patch plan selected. Its exact `GameOnLoadLevelPrefix(Game __instance)` calls only `DeliveryTemperatureRuntimePatchInstaller.TryStartAuthorizedGameSession(Game game)`. `DeliveryTemperatureGameSessionShutdownPatches` remains under `KleiImplementationAdapters` and owns only the verified `Game.DestroyInstances` prefix/finalizer. This split prevents an earlier adapter from publishing a session before the cross-path authority composition root has approved the load.

`TemperatureLimit.OnSpawn()` consumes the already authorized session and does not create a bypass around runtime-patch authority verification. Correct callback ordering is provided by the verified `Game.OnLoadLevel` authority patch; a component callback that observes no active session retains no registration and safely no-ops. No other code path may call `DeliveryTemperatureGameSessionHost.EnsureGameSession` for a game load.

The implementation performs a second cold authority check exactly once for each game-load identity at `Game.OnLoadLevel`, after startup patch installation but before publishing the game session. It re-reads active Harmony prefix ownership for every selected Klei/FastTrack replacement responsibility. If another mod changed the required topology after `OnAllModsLoaded`, no Delivery Temperature Limit session is published; installed Delivery Temperature Limit patches remain guarded no-ops, no fallback is selected, no third-party method is unpatched, and one diagnostic is emitted for that rejected game-load identity. A repeated prefix callback for the same load reuses its already determined outcome without repeating reflection; a later distinct load performs one new check. This check never runs in an update, pickup, status, comparator, sweep, or direct-delivery hot path.

Static installed-game contracts must verify the lifecycle signatures before activation. The final candidate's required save/main-menu/reload exercise and `Player.log` review must then provide a simple runtime sanity check that shutdown and re-entry behave coherently. If `Game.DestroyInstances()` does not cover an observed supported exit path, implementation stops for an explicit design amendment rather than adding speculative cleanup hooks.

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

Thread-static fixed arrays are bounded and may remain allocated on worker threads. Variable-size dictionaries are replaced after exceeding documented immutable production constants. Those thresholds are retention policies, never workload caps or injectable test settings: every operation processes all entries, then replaces an oversized reusable collection only at its safe completion/finalizer boundary. Session shutdown need not—and safely cannot—enumerate other threads' static storage; generation rejection prevents retained buffers from publishing stale state.

## 10. World-parent topology

`WorldParentTopologyCatalog` is main-thread-owned and publishes immutable `WorldParentTopologySnapshot` instances.

The catalog is content-mode neutral. It consumes authoritative `WorldContainer` identities and parent relationships and never branches on an assumption that base-game content mode has exactly one world or that Spaced Out content mode is enabled. Base-game content mode and Spaced Out content mode must both tolerate every world context actually reported by the installed game, including lifecycle events for any supported interior world. Spaced Out commonly makes multi-asteroid and rocket-interior aggregation more visible, but the domain model does not encode DLC-specific topology guesses.

The verified installed-game seams are:

- `ClusterManager.RegisterWorldContainer(WorldContainer)` postfix;
- `ClusterManager.UnregisterWorldContainer(WorldContainer)` prefix; and
- `WorldContainer.SetParentIdx(int)` postfix.

Each effective mapping change increments a topology version exactly once. The next version is computed in a checked context before mapping mutation, so exhaustion cannot publish a changed map under a wrapped/reused version. World removal also removes that world's inventory contribution and invalidates affected parent/tag aggregates. Parent reassignment invalidates both the old and new parent aggregates; data is not blindly transferred between parents.

Worker code resolves world-to-parent relationships only through a captured immutable snapshot. An unresolved world never defaults to parent zero, the active world, or its own raw world ID. Automated topology tests cover both base-game and Spaced Out content-mode shapes independently of implementation-path selection. The final manual comparison exercises the Klei implementation paths in one late-game colony of each content mode; FastTrack remains a static compatibility contract for this release.

## 11. Sparse status-temperature inventory

### 11.1 `TemperatureAmountAccumulator`

This reusable, thread-confined collector uses fixed arrays whose length is `TemperatureDecisionBucket.BucketCount` (`10,002` for the reviewed ONI build):

- accumulated amount by bucket;
- generation stamp by bucket; and
- touched-bucket indices.

Starting a new tag advances a local stamp. Only touched entries are emitted; no complete-range clear, scan, or write occurs per tag. Stamp wraparound performs one explicit full reset and is tested. The larger current upper bound therefore adds bounded reusable memory and a vanishingly rare reset cost, but it does not add work for unused buckets during ordinary game updates.

The Klei inventory update path uses a main-thread instance. The FastTrack inventory update path uses a thread-static instance with finalizer cleanup and game-session generation checks.

### 11.2 `TemperatureAmountSeries`

Published amounts use a sparse immutable series:

- sorted occupied bucket IDs; and
- cumulative amounts aligned with those IDs.

An inclusive-low/exclusive-high amount query uses two binary searches plus prefix subtraction. Underflow and overflow participation follows the exact constraint semantics. Empty constraints return zero without searching. Disabled constraints do not request temperature-specific replacement.

### 11.3 `WorldResourceTemperatureAmountCatalog`

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

For a complete-world replacement, the catalog diffs the previous and replacement resource-tag sets and rebuilds only affected parent/tag aggregates. For a single-tag publication, it replaces and rebuilds only that world/tag and its affected parent/tag aggregate. Each triggering mutation performs exactly one optimistic outside-lock combine; if its captured versions are stale at publication, it discards the candidate and relies on the concurrent version-changing mutation's own attempt rather than retrying/spinning. The aggregate remains explicitly incomplete until a current candidate publishes. A FastTrack single-tag update must never cause unrelated resource tags or worlds to be combined again. Readers never scan all `WorldContainer` instances.

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
GetWorldResourceTagCoverageRequirementState
GetTemperatureConstrainedAmountAvailability
RemoveWorld
ClearForGameSession
```

World registration, removal, parent reassignment, complete replacement, coverage replacement, single-tag replacement, duplicate publication, mixed publication kinds, stale generations, and post-removal late publication all have explicit tests.

### 11.4 Availability replacement states

Two explicit result contracts prevent boolean/value ambiguity:

- `WorldResourceTagCoverageRequirementState` is `UnknownWorldOrCollectionGeneration`, `CoverageRequired`, or `CoverageCurrent`.
- `TemperatureConstrainedAmountAvailabilityState` is `TemperatureConstraintDisabled`, `InventoryIncomplete`, or `Complete`.

`TemperatureConstrainedAmountAvailability.TryGetCompleteAvailableAmount` succeeds only for `Complete`. A caller must not inspect its `out` value for either other state. The catalog never represents disabled or incomplete as `false` paired with a potentially meaningful float, and adapters switch exhaustively over the named state.

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
3. calls `AddTemperatureAmount` for each pickupable temperature and amount while Klei already enumerates it;
4. publishes one `CompleteWorldResourceTemperatureAmounts` only from a successful postfix; and
5. discards the whole candidate from the finalizer after an exception.

The FastTrack inventory update adapter:

1. is installed only when the exact `ParallelInventory` replacement is verified active for the loaded game;
2. distinguishes FastTrack's first complete update from its later single-tag updates using verified installed-binary state and IL anchors;
3. publishes a complete-world candidate for the first complete update;
4. publishes one generation coverage set when incremental collection begins without a usable complete update;
5. publishes exactly one tag series for each later FastTrack update; and
6. uses thread-confined state plus postfix/finalizer cleanup so an exception cannot publish a partial series.

`FastTrackWorldInventoryPublicationSession` has three unambiguous entry operations: `BeginCompleteWorldUpdate`, `BeginIncrementalResourceTagUpdateRequiringCoverage`, and `BeginIncrementalResourceTagUpdateWithCurrentCoverage`. It never accepts `isFull`, `needsCoverage`, or a defaultable constraint/result boolean. Its result has a required `FastTrackWorldInventoryPublicationKind`: `CompleteWorldAmounts`, `ResourceTagCoverageAndTemperatureSeries`, `ResourceTemperatureSeries`, or `ResourceTagCoverageOnly`; guarded accessors enforce that the declared kind and payloads agree.

Compatibility discovery and reflective member binding occur once during installation and are cached. The FastTrack inventory update path performs no per-update assembly scan, option reflection, or compatibility rediscovery. The Klei inventory update path allocates no FastTrack state and pays no coverage/delta bookkeeping. Selecting either implementation path is independent of base-game versus Spaced Out content mode.

## 12. Combined fetch temperature-eligibility snapshot

### 12.1 One authoritative traversal

Storage interval sets and pickup partition endpoints are derived during one successful traversal of `GlobalChoreProvider.fetchMap`. This map contains the authoritative active `FetchChore` topology, including storage and construction fetches.

The resulting immutable `FetchTemperatureEligibilitySnapshot` contains:

- storage eligibility by parent world and requested `Tag`;
- sorted unique pickup decision endpoints by parent world and requested `Tag`;
- immutable requested-tag encounter order by parent world;
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

Fetch-topology and world-inventory-collection generations use the same checked-before-mutation rule as the registry/topology versions. Exhaustion tests set only predeclared private fields and prove no state transition or candidate publication occurs; production exposes no injectable sequence or reset API.

The snapshot builder captures all versions before traversal and publishes only if every version is unchanged afterward. Otherwise it discards the whole candidate.

### 12.3 Tag semantics

Each `FetchChore` contributes its requested tag set through one of two semantically distinct builder operations. `AddUnconstrainedFetchRequest` records a missing/disabled destination component. `AddTemperatureConstrainedFetchRequest` requires an enabled constraint; an enabled nonempty constraint contributes its low/high endpoints to each requested tag in the destination's parent world, while an enabled empty constraint contributes an allows-nothing destination without endpoints. Passing a disabled constraint to the constrained operation is a caller contract violation.

If a pickup satisfies multiple requested tags, its effective partition is the normalized union of endpoint arrays for all applicable tags. This union is scoped to the pickup's resolved parent world.

The snapshot is the sole retained owner of per-parent/tag endpoint facts. It can return requested tags and construct one sorted union for explicitly supplied applicable tags, but it does not allocate `TemperaturePartitionDefinition` objects, assign definition IDs, or retain a second partition catalog. Those update-local responsibilities belong only to `PickupTemperatureGroupingSession`.

The grouping identity is `PickupTagIdentity`: ONI's existing base grouping identity plus `PrefabTag` where required by the actual Klei or FastTrack pickup grouping path. The design does not claim `PrefabTag` alone is globally unique when the underlying grouping uses additional tag-bit identity.

## 13. Storage temperature eligibility

### 13.1 `AllowedTemperatureIntervalSet`

The internal immutable type has three explicit states:

- `AllowsNoTemperature`;
- `AllowsEveryTemperature`; or
- sorted, nonoverlapping inclusive-low/exclusive-high intervals.

Normalization rules are exact:

- `CreateFromDestinations(bool includesUnconstrainedDestination, IReadOnlyList<DeliveryTemperatureConstraint> enabledDestinationConstraints)` receives only enabled constraints;
- a missing/disabled destination is recorded only by `includesUnconstrainedDestination`, which makes the requested tag `AllowsEveryTemperature`;
- a disabled entry in `enabledDestinationConstraints` is a caller contract violation rather than an implicit boolean reinterpretation;
- enabled empty or reversed interval contributes nothing;
- duplicates collapse;
- overlapping intervals merge;
- adjacent intervals merge because no integer decision class lies between them; and
- once `AllowsEveryTemperature` is established, narrower contributors cannot change it.

Lookup uses binary search. There is no per-band `HashSet<Tag>[]` representation.

### 13.2 Build session and publication

`FetchTemperatureEligibilityBuilder` exists for one complete `GlobalChoreProvider.UpdateStorageFetchableBits` invocation:

- prefix captures game session, constraints, fetch version, and world topology;
- the world-section hook begins the correct parent-world builder;
- each traversed `FetchChore` contributes its requested tags and destination constraint;
- postfix normalizes and hands its portion to the combined snapshot candidate;
- finalizer discards and clears incomplete state after exceptions.

Only the combined `FetchTemperatureEligibilitySnapshot` is published. Storage eligibility is never published independently with versions that could disagree with pickup partitions.

### 13.3 `ClearableDestinationSweepEligibility`

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

The pure decision receives one `ClearableDestinationSweepEligibilityInput` whose fields name the original destination result, enabled-constraint count, primary-element presence, parent-resolution state, snapshot currency, and current eligibility decision. Its single operation is `AllowsClearing`. It does not expose a four-boolean `Evaluate` helper whose arguments can be transposed.

## 14. Klei pickup-path temperature partitions

### 14.1 `TemperaturePartitionDefinition`

An immutable definition contains:

- a positive identifier supplied by and unique within the containing pickup-update grouping session;
- a nonempty sorted relevant endpoint sequence;
- classification into interval ordinals.

Construction rejects a nonpositive ID or empty normalized endpoint sequence. An empty applicable union is represented by `TemperatureEligibilityClassKey.NoTemperatureDistinction()` rather than a one-class optimized definition carrying meaningless identity.

For an optimized scoped partition, two temperatures share an ordinal only if every active relevant constraint for the same parent world and pickup tag identity gives the same eligibility answer.

The converse is also required: if no relevant constraint distinguishes two decision buckets, they should not be fragmented into different optimized classes. Tests therefore verify both correctness and minimal fragmentation.

### 14.2 `TemperatureEligibilityClassKey`

The key contains:

- a required `TemperatureEligibilityClassificationKind`;
- partition definition ID and interval ordinal only for `OptimizedPartitionInterval`;
- an exact `TemperatureDecisionBucket` only for `ExactTemperatureDecisionBucket`; and
- no invented ordinal/definition ID for `NoTemperatureDistinction` or `MissingPrimaryElement`.

Ordinals from different partition definitions are never equal merely because they have the same small integer value.

### 14.3 Per-update grouping session

`FetchManager.FetchablesByPrefabId.UpdatePickups(Navigator, int)` receives a prefix/postfix/finalizer session that captures once:

- the current game session;
- active constraints;
- combined fetch snapshot;
- world-parent topology;
- navigator anchor/current world; and
- whether the optimized snapshot is current.

It resolves the navigator's parent world once at update entry using `Navigator.GetAnchorCell()` and then uses immutable topology/snapshot data for every candidate. Patch verification must characterize whether the installed Klei or FastTrack pickup grouping invocation runs on a worker. Worker-capable code never enumerates Unity objects, performs `GetComponent`, or queries `ClusterManager`. It may read only the exact candidate fields and cached `PrimaryElement` temperature whose installed managed-field access and cross-thread stability were verified before activation. If that proof fails for an active FastTrack pickup replacement, coordinated Delivery Temperature Limit activation aborts before patching; it must not select a Klei path that FastTrack still suppresses.

Classification is:

- zero enabled constraints: original no-temperature-distinction grouping;
- current scoped partition: partition definition plus interval ordinal;
- stale, missing, or unresolved scoped partition: exact `TemperatureDecisionBucket` class;
- missing `PrimaryElement`: explicit missing-element class.

The session caches each pickup's full grouping key for that update. Oversized dictionaries are replaced at a documented high-water threshold.

For current scoped classification, the grouping session asks the captured snapshot for the applicable endpoint union, interns identical immutable union sequences within that update, and assigns definition IDs in deterministic first-encounter order. Equal unions share one definition during that update; unions and IDs do not survive `Complete`/`Discard`. There is no persistent `PickupTemperaturePartitionCatalog`, second endpoint cache, or definition-ID owner.

### 14.4 Comparator and suppression semantics

The Klei pickup-path comparator preserves all original ordering fields first, then compares the full classification-kind-aware `TemperatureEligibilityClassKey`. Optimized keys compare definition ID and interval ordinal; exact keys compare the exact decision bucket; the two non-temperature kinds remain explicit. Duplicate suppression compares the complete base grouping identity and that same full key captured under the same session snapshot.

Comparator equality and suppression equality must use the same semantic key. No path may independently recalculate a temperature bucket against a newer snapshot midway through the sort.

## 15. FastTrack pickup adapter

### 15.1 Adapter boundary

FastTrack support is an optional named adapter over the canonical pickup grouping algorithm. It does not own an alternate temperature partition, fallback rule, or constraint representation.

This specific compatibility work is justified because FastTrack is aimed at the same very-large-colony audience and actively replaces the exact Klei inventory, pickup-grouping, and delivery-comparison seams this mod must observe. Ignoring an active replacement would not merely omit an optimization: it could bypass temperature eligibility or merge eligibility-distinct pickups. That concrete overlap justifies one narrowly verified adapter. It does not justify a generic compatibility framework, support for unverified releases, or any hot-path cost when FastTrack is absent, disabled, or inactive.

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

### 15.3 Compatibility verification and coherent activation policy

FastTrack discovery has a cold-path gate before any reflective feature inspection:

1. Determine whether FastTrack is present in the loaded mod set.
2. Determine whether it is enabled for the currently loaded game/content configuration.
3. Inspect the relevant FastTrack option and active Harmony replacement ownership.
4. If the mod is absent, disabled, or the relevant replacement is inactive, select the corresponding Klei implementation path and allocate/install no FastTrack adapter state for that feature.
5. Only an active replacement proceeds to structural verification.

Consequently, ordinary Klei implementation paths remain the most direct path. They do not perform a FastTrack dictionary lookup, reflection call, option check, compatibility branch, allocation, or adapter dispatch per pickup, inventory item, status query, or update. The one cold startup presence check is not a colony-scaling cost and must be covered by `FastTrackInactivePathArchitectureContractTests`.

For an active replacement, `OnAllModsLoaded` verifies exactly once:

- supported FastTrack file version `0.18.4.0` on a best-efforts basis;
- expected type identities and assembly identity;
- exact required method and field signatures;
- unique semantic IL anchors;
- active Harmony owner/target relationships; and
- every required prefix/postfix/finalizer session hook.

Physical file identity is isolated behind `IFastTrackAssemblyFileIdentityReader`. The production `FastTrackAssemblyFileIdentityReader` is the only code that reads `Assembly.Location`, file version metadata, or SHA-256 bytes, and it runs exactly once during cold inspection. Its result state is explicit: `Available`, `DynamicAssembly`, `LocationUnavailable`, `FileUnavailable`, or `ReadFailed`. An active feature can be `Ready` only when the production reader reports an available physical file whose file version is exactly `0.18.4.0`; a dynamic or unreadable assembly is incompatible rather than silently trusted.

Emitted in-memory structural fixtures deliberately have no physical identity. Inspector unit tests reach structural branches through a narrowly named test adapter that returns an explicit identity result, while separate tests exercise the production reader against real temporary file bytes and every failure state. The seam separates file I/O from structural inspection; it cannot relax production `Ready` semantics.

The runtime verifier is structural and does not use a DLL hash allowlist. The recorded fixture hash proves which GitHub artifact the tests inspected; it does not authorize an unknown binary merely because a filename/version string matches.

If an active FastTrack mismatch can alter direct delivery eligibility or pickup grouping, coordinated Delivery Temperature Limit activation aborts before installing any of its runtime patch set and throws `FastTrackDeliveryEligibilityCompatibilityException`. The one diagnostic names the FastTrack version, observed digest when available, feature, member/anchor contract, and why continuing would produce temperature-unaware behavior. Warning-and-continue, third-party unpatching, and an unproved Klei fallback are forbidden.

If only the optional status adapter is incompatible while direct delivery eligibility and pickup grouping remain coherent, activation installs the coherent delivery patches, leaves ONI's existing lacks-resources availability behavior unchanged, and emits one rate-limited status-compatibility diagnostic. It must not publish partial or fabricated temperature inventory.

If this mod must roll back after a partial exception during its own installer, it removes only the exact methods it installed under its own Harmony owner. It never unpatches FastTrack or another mod. Nested/reentrant FastTrack sessions restore prior Delivery Temperature Limit state exactly and always clean up through finalizers.

`DeliveryTemperatureRuntimePatchPlan` exposes one immutable `OrderedPatchGroups` sequence of semantically named `DeliveryTemperatureRuntimePatchGroup` values. It has no parallel inventory/pickup path enums, generic `useFastTrack` booleans, or mirrored flags. Construction filters one ordered responsibility list so mutually exclusive Klei/FastTrack groups cannot both be selected; status-disabled plans contain neither inventory nor status groups. Its linked pure `VerifySelectedAuthority(IReadOnlyList<ActiveHarmonyPatchDescriptor>)` operation is the sole owner of the selected-owner decision and throws the semantic patch-contract violation when an installed selected authority no longer matches; runtime glue only converts current Harmony information into immutable descriptors.

At each distinct `Game.OnLoadLevel` load identity, before publishing a game session, the installer performs one cold recheck through that pure plan operation. Static production call-order contracts prove the installer cannot reach `EnsureGameSession` before verification succeeds; emitted-descriptor unit tests execute the owner decision itself. Changed prefix topology publishes no session and leaves the already installed Delivery Temperature Limit hooks as session-guarded no-ops. It does not unpatch, choose a fallback, or recheck in a gameplay hot path.

### 15.4 Static FastTrack fixture provenance

The static contract fixture is the latest available DLL from FastTrack's official GitHub repository release asset, not a proven copy of the Steam Workshop distribution:

- supported file version: `0.18.4.0`;
- assembly version: `0.18.0.0`;
- source revision closest to that artifact: `e24e8f3082a52785e971943a8f1fff8de0ca8dff`;
- release page: `https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta`;
- fixture DLL SHA-256: `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`; and
- downloaded ZIP SHA-256: `8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D`.

The word `Beta` appears only because it is part of the upstream release URL/tag; this mod itself has no beta stage. The fixture directory and README must state that the actual Workshop-distributed DLL could not be found, so compatibility is to this available artifact/version on a best-efforts basis. The test project copies the DLL as a non-reference data item and reads it with `System.Reflection.Metadata`; it never links or executes it. The released Delivery Temperature Limit package must contain no FastTrack fixture bytes or full FastTrack mod package.

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

All adapters share one reflection-only `HarmonyPatchContractVerifier`. Its primitive surface verifies exact declared instance methods, static methods, constructors, fields (including instance/static storage), nested types, and a single semantic instruction match; it also verifies Klei authority against active prefix descriptors and an exact permitted-owner set. Every primitive distinguishes public/nonpublic visibility and rejects zero or multiple exact matches. Adapters may not create one-off name-only reflection helpers.

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
| Conflicting replacement registration | Replace atomically under a new token; a stale token cannot mutate/remove it. |
| Unknown removal | Idempotent no-op. |
| Destroyed Unity `GameObject` or indexed `TemperatureLimit` | Return `null`; token-own the stale removal so a replacement cannot be removed. |
| Registration/session generation exhaustion | Throw before mutation/publication; never wrap or reuse an identity. |
| Active FastTrack direct-eligibility or pickup-grouping mismatch | Abort coherent Delivery Temperature Limit activation before patching with `FastTrackDeliveryEligibilityCompatibilityException`. |
| FastTrack status-only mismatch | Install coherent delivery behavior, omit only the temperature-aware status adapter, preserve ONI's existing availability result, and emit one rate-limited diagnostic. |
| FastTrack key-space exhaustion | Fail explicitly; never collide through wraparound. |
| Harmony update session throws | Finalizer clears/discards all thread-confined state. |
| Status option disabled | Install no status/inventory hooks and allocate no status structures. |
| Selected replacement authority changes before `Game.OnLoadLevel` | Publish no game session; keep installed hooks guarded and perform no unpatch/fallback. |

Diagnostics are rate-limited by game session and diagnostic key so a single stale condition cannot create a large-colony log storm. A pre-session authority rejection has no session generation by design, so its single cold diagnostic is keyed by rejected `Game` instance/load identity plus diagnostic key.

## 19. Bounded resource policy

The endpoint reference array contains `OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1` integers: `10,001` entries and approximately `39.1 KiB` of element storage for ONI build `744825`. A decision-bucket accumulator has `TemperatureDecisionBucket.BucketCount` entries in each amount, generation-stamp, and touched-ordinal array: `10,002` entries and approximately `117.2 KiB` of element storage across `float[]`, `int[]`, and `int[]`, excluding array headers. Moving from the legacy `5000 K` limit therefore adds approximately `19.5 KiB` per endpoint-count array and `58.6 KiB` per retained accumulator. These are small bounded reusable costs, not per-world/tag/pickup allocations.

Sizing to the current ONI bound is not speculative padding: current element definitions include pickupable-relevant phases above `5000 K`, as section 2.2 records. A colony that never observes or configures those temperatures still pays only the fixed allocation above; it does not execute work for those unused indices.

Unused temperature buckets add no ordinary hot-path iteration. Classification remains one truncation plus constant-time bound/ordinal logic. Accumulation touches only observed buckets; published amount series include only occupied buckets; partition definitions include only configured endpoints relevant to the parent world and tag; registry snapshots enumerate set bits rather than all counters; queries use binary search. A complete-range scan is permitted only for the accumulator's explicit generation-stamp wraparound reset and exhaustive tests. Static performance-shape tests reject complete-range loops in per-pickup, per-tag, per-world, comparator, suppression, status-query, recurring update, and ordinary constraint-mutation methods.

Sparse published structures allocate only occupied temperature classes or actual interval endpoints. Variable-capacity dictionaries and lists are reusable below named high-water thresholds and replaced above them after publication/cleanup. The initial immutable policies are:

```text
MaximumRetainedPickupClassificationCount = 16384
MaximumRetainedFastTrackGroupingKeyCount = 8192
MaximumRetainedFetchEligibilityEntryCount = 4096
MaximumRetainedWorldResourceTagCount = 4096
```

Thresholds must be:

- named for the retained resource;
- justified in comments;
- covered by tests at the real threshold, `threshold + 1`, and a larger deterministic lightweight workload without an injected/reduced limit; and
- justified by deterministic structure/capacity tests and, where visible during the manual comparison, recorded as an indicative observation rather than a profiler threshold.

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

Focused tests are not deferred. Only final exact-candidate static and four-session manual validation is deferred.

### 20.2 Deterministic exhaustive tests

Tests cover every `TemperatureDecisionBucket` from `BelowMinimumKelvinOrdinal` through `AtOrAboveMaximumKelvinOrdinal`; for build `744825`, that is `10,002` buckets. Loops derive their limits from production constants and assert the reviewed total once rather than repeating `10002` as an independent bound. Randomized tests use fixed, named seeds and report the seed and generated case on failure. No external property-testing package is required.

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

Game and FastTrack adapter tests use semantically named stubs or captured IL fixtures. The game doubles retain exact identities: `Tag` is `global::Tag`, and the component is `DeliveryTemperatureLimit.TemperatureLimit`. The component stub begins as an empty sealed reference type because linked component-index code only stores and returns identity; tests compare reference identity rather than add a diagnostic label. A parity contract enumerates only members genuinely required by linked production source and rejects aliases, extra convenience members, or namespace drift. Tests must not rely solely on source text matching when reflection or instruction-shape verification can assert the actual contract.

Behavioral methods use `Operation_WhenCondition_ExpectedOutcome`; architecture, identity, publication, and concurrency contracts may use `Subject_WhenCondition_ExpectedOutcome`. Each method has exactly three semantic segments and proves one outcome. Exception tests use MSTest 4 `Assert.ThrowsExactly<T>` plus `Assert.Contains(expectedSubstring, actualMessage)` for the invariant-bearing message. New sequence assertions use `Assert.AreSequenceEqual(expected, actual)`; legacy `CollectionAssert` and removed MSTest assertion APIs are not introduced. These choices follow the current official guidance to use `Assert` and the newer exception/sequence APIs ([MSTest assertions](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests-assertions)).

Private reflection is allowed only for the exact representation contracts predeclared in the implementation plan: checked counter exhaustion, generation-stamp wrap, retained collection identity, unaffected aggregate identity, and key-space exhaustion. Each test verifies its exact field name before use. No diagnostic property, test-only constructor, injectable production limit, conditional production branch, or facade exists merely for testing.

### 20.4 Static runtime and artifact contracts

The required `net10.0` test project statically verifies all of the following without launching ONI:

- installed public ONI build/branch, `Sim.MaxTemperature`, relevant `PrimaryElement`/`SimMessages` semantics, and every Harmony target signature used by the mod;
- production project target `netstandard2.1`, C# 8 syntax ceiling with no `LangVersion` override, test project target `net10.0` with test-only C# 14, locked restore, `CopyLocalLockFileAssemblies=true`, compiler warnings as errors, and the approved staged nullable state;
- the exact two known reference-resolution roots while targeting the current ONI DLLs: `System.IO.Compression` reference `4.1.3.0` versus game `4.2.0.0`, and `System.Net.Http` reference `4.1.2.0` versus game `4.2.0.0`;
- absence of warning suppression, binding redirects, `AutoUnify=false`, direct replacement framework references, or package changes intended to hide those two visible MSBuild warnings;
- the merged `DeliveryTemperatureLimit.dll` directly references neither `System.IO.Compression` nor `System.Net.Http`;
- the merged output contains the intended PLib merge input but the pipeline package contains only `DeliveryTemperatureLimit.dll`, `mod.yaml`, and `mod_info.yaml`—never framework DLLs or an application configuration file;
- serialized and curated public assembly contracts, including the deliberate absence of `TemperatureIndexData` and its getter;
- linked algorithm/session sources invoke no Unity, Klei, Harmony, PLib, or FastTrack APIs, contain no conditional framework branches, belong to the exact production compile graph, compile successfully under the evaluated C# 8 production ceiling, and consume only the exact `global::Tag`/`DeliveryTemperatureLimit.TemperatureLimit` stub identities and parity-verified members;
- the GitHub FastTrack `0.18.4.0` fixture has the recorded identity/digest and satisfies the expected structural contract; and
- the actual pipeline-built merged candidate, not merely a test assembly or copied source, satisfies all architecture/reference/package checks.

The two reference warnings remain visible evidence. `TreatWarningsAsErrors` applies to compiler warnings; implementation must not claim the two MSBuild unification warnings were eliminated. An unexpected third root, a version change, or a new direct merged reference fails the contract and requires review.

Performance-shape contracts are deliberately narrow. Every source/metadata inspection names one exact subject signature and a small permitted-call set, forbidden-call/reference set, and back-edge policy. Binary-search and observed-entry loops are permitted where named; assertions never claim that a directory generically “has no loops” or allowlist a namespace. Semantic counts and immutable-reference identity prove complete-versus-incremental publication scope without production counters.

The already-required pipeline `test` command's printed duration may be recorded once against the planning baseline as non-statistical developer evidence. An order-of-magnitude increase prompts investigation but creates no timing assertion, repeated measurement series, cross-machine claim, or publishing blocker.

### 20.5 Final exact-candidate validation boundary

Only after all fixes are integrated and committed will the implementation perform the final release-candidate validation:

- fresh pipeline `diagnose`, `validate`, `build`, and `test`;
- static baseline-versus-candidate assembly comparison using the published baseline DLL and actual merged candidate;
- exact candidate preparation/installation through the repository-local pipeline;
- separate published-baseline and release-candidate derivatives copied from the same untouched late-game base-game content-mode colony, with all other mods disabled, each run once;
- separate published-baseline and release-candidate derivatives copied from the same untouched late-game Spaced Out content-mode colony, with all other mods disabled, each run once;
- one fixed short settling/observation scenario per content mode, recording displayed colony-cycle progress, errand/UI responsiveness, representative delivery outcomes, status behavior, required save/load behavior, and relevant `Player.log` exceptions; and
- optional concise Markdown recording of those observations when convenient.

The original colony saves are never used as mutable test subjects. Each package receives a separate derivative copied from the same original save state and the same short scenario in each content mode. There are exactly four manual runs—no repetitions for the same candidate—and the comparison is explicitly indicative rather than a statistically controlled benchmark. No FastTrack-enabled game run, automated gameplay, CPU sampling, allocation measurement, GC dissection, or beta stage is required. An inconclusive visible delta and absence of a concise Markdown result record do not block publishing when structural evidence passes and no obvious regression is observed; a functional failure, pipeline failure, static-contract failure, obvious candidate-only slowdown, or relevant new log exception does.

A failure is fixed with focused TDD; the pipeline gates and affected final comparison are then rerun. The rewrite is never released in a partially migrated state.

## 21. ONI Mod Pipeline integration

The repository-local pipeline is the authoritative build, test, package, install, and acceptance path. The profile already declares:

```toml
[[test-projects]]
id = "delivery-temperature-limit-regressions"
path = "Tests/DeliveryTemperatureLimit.Tests.csproj"
required = true
```

The rewrite expands that required project; it does not create an unregistered side test command. Pure production sources are linked into this project from their real locations, so the game-loaded and test assemblies compile the same physical files rather than copies.

Normal focused TDD uses `dotnet test` filters against the test project. Final integration uses the pipeline from the repository root. If `oni-mod-pipeline` is not installed on `PATH`, the checkout-local equivalent is:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- <command> --mod mods/delivery-temperature-limit-supercooled
```

The exact build-result path printed by `build` is carried into static artifact inspection. Release preparation prints a separate exact immutable candidate directory, and that exact directory is carried into candidate installation. The implementer must not substitute “latest,” a different build, or the source-root DLL at either boundary.

Before every meaningful commit—not only the final candidate—the implementer runs, in order, pipeline `validate`, pipeline `build`, and pipeline `test` against the complete current working tree. Filtered direct tests may precede those commands but may not replace them. A pipeline failure belongs to the current chunk and must be fixed before preparing a commit.

The existing `oni-mod-pipeline.toml` is sufficient and remains byte-for-byte unchanged. The two-colony performance comparison is deliberately supplemental release evidence, not a new required profile acceptance check. This avoids turning an optional concise result record into a publishing blocker while still ensuring that the mod itself always passes through the production pipeline.

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

Conventional Commit types remain semantically truthful: runtime-target/harness configuration is `build`; inactive Gate A–C modules/adapters are `refactor`; fixture, architecture, and artifact contracts are `test`; the coordinated runtime activation is `perf`; and a plan/spec-only amendment is `docs`. An anticipated future speedup does not make an inactive module a `perf` commit.

The user has approved the strategy of committing each meaningful implementation chunk. Repository commit safety still applies: before each commit, inspect the exact staged diff and use the repository's committing workflow. Pushing remains separately authorized and is outside this design.

## 23. Planned source-module shape

The following directory names are part of the approved semantic architecture and are not placeholders. The implementation plan defines the complete file list within them. An implementer must not abbreviate them to `Domain`, `Runtime`, `Patches`, `FastTrack`, `WorldTopology`, `WorldInventory`, `Helpers`, or another less precise container without a reviewed plan amendment.

```text
Source/
  DeliveryTemperatureLimitMod.cs
  DeliveryTemperatureLimitOptions.cs
  DeliveryTemperatureLimitStrings.cs
  TemperatureConstraints/
  WorldParentTopology/
  WorldResourceTemperatureAmounts/
  FetchTemperatureEligibility/
  DeliveryTemperatureGameSessionLifecycle/
  RuntimePatchInstallation/
  TemperatureLimitedDeliveryTargets/
  KleiImplementationAdapters/
  FastTrackCompatibility/
    FeatureContractVerification/
    InventoryUpdateAdapters/
    PickupGroupingAdapters/
    DirectDeliveryEligibilityAdapters/
  TemperatureLimitUserInterface/
  HarmonyTranspilerInfrastructure/
```

The tests mirror those semantic production directories and add only these purpose-specific roots:

```text
Tests/
  DeliveryTemperatureAssemblyContracts/
  OniModPipelineIntegration/
  RuntimePatchInstallation/
  ReferenceTemperatureModels/
  TestDoubles/
  Fixtures/
    ThirdParty/
      FastTrack/
        0.18.4.0/
```

The linked test boundary comprises `TemperatureConstraints`, `WorldParentTopology`, `WorldResourceTemperatureAmounts`, `FetchTemperatureEligibility`, `DeliveryTemperatureGameSessionLifecycle`, the exact pure runtime-patch group/plan files, the explicitly named pure FastTrack publication/key-allocation files, and reflection-only contract types under `FastTrackCompatibility/FeatureContractVerification` and `HarmonyTranspilerInfrastructure`. The exact globs/files are enumerated in Task 1; runtime Harmony adapter files are never pulled in by a broad glob. A static boundary test fails if linked algorithm/session files invoke Unity, Klei, Harmony, PLib, or FastTrack APIs, reference those assemblies under the test build, are absent from the production `Compile` graph, contain `#if`/`#elif` framework forks, or add test-only production branches. Evaluated compiler-property tests establish the C# 8/C# 14 split, and the mandatory real production build is the authoritative post-C#-8 syntax rejection mechanism; no brittle keyword blacklist impersonates the compiler. The exact `global::Tag` and `DeliveryTemperatureLimit.TemperatureLimit` identities supplied by `TestDoubles/OniGameTypeStubs.cs` are member-parity boundary contracts, not permission for game-object traversal in domain code.

The old `Patch.cs`, `PatchFastTrack.cs`, `StatusItems.cs`, `Limits.cs`, `Harmony.cs`, vague filenames (`Mod.cs`, `Options.cs`, `Strings.cs`), and their obsolete responsibilities are removed or semantically renamed at coordinated activation. They do not remain as forwarding files, aliases, partial-class facades, or compatibility shims.

## 24. Configuration approval dossier

The grilling recorded explicit approval for the exact staged changes below. Before editing, the implementer must compare the live file with the expected starting content; a changed context or broader delta requires renewed approval.

| Configuration or policy file | Exact approved change | Behavioral and pipeline impact | Defensive limit |
|---|---|---|---|
| `Source/DeliveryTemperatureLimit.csproj` | First implementation chunk: replace `<TargetFramework>net48</TargetFramework>` with `netstandard2.1`; add `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Coordinated activation: add `<Nullable>enable</Nullable>`. | Produces a game-loadable .NET Standard assembly, makes PLib available to the existing merge target, treats compiler warnings as errors, and adopts complete nullable analysis only when legacy files are removed. | Do not add `LangVersion`, multi-targeting, `NoWarn`, binding redirects, `AutoUnify`, direct framework-assembly overrides, a modern-runtime sidecar, or package changes. Every new source file starts with `#nullable enable` before project-wide nullable is activated. |
| `Source/packages.lock.json` | Refresh only the target-framework restore graph required by the `net48` to `netstandard2.1` retarget. | Keeps `--locked-mode` authoritative under the new target. | PLib remains `4.24.0`; ILRepack remains `2.0.34`; no direct or transitive version is deliberately upgraded/downgraded. Any unrelated graph/version change stops the task. |
| `Tests/DeliveryTemperatureLimit.Tests.csproj` | First implementation chunk: add linked compile items for the exact pure/session production roots and reflection-only contract files in the plan. Task 21 only: update the SDK-default FastTrack fixture `None` item with `CopyToOutputDirectory=PreserveNewest`. Retain `net10.0`, MSTest.Sdk `4.3.3`, warnings as errors, and locked restore. Retain `<Nullable>annotations</Nullable>` while the legacy linked `Buildings.cs` remains; replace it with `<Nullable>enable</Nullable>` in coordinated activation after that legacy link/file is removed or rewritten. | Tests the same physical sources under modern tooling, enables static metadata/artifact contracts, and makes the FastTrack DLL available only as inert test data. | No copied production source, FastTrack compile reference/execution, production-output copy, package addition, alternate test project, conditional framework code, or unregistered suite. |
| `mod_info.yaml` | Replace `minimumSupportedBuild: 596100` with `minimumSupportedBuild: 744825`; retain `supportedContent: ALL`, `version: 2026.8.26`, and `APIVersion: 2` until normal release versioning is separately authorized. | States support for the current public ONI build used to compile and verify the rewrite. | Do not add `archived_versions` or claim historical/future build compatibility. If public ONI changes before release, stop. |
| `oni-mod-pipeline.toml` | **No change; byte-for-byte invariant:** 5,413 LF bytes, SHA-256 `5A03C7656F75B539B226C1CD6FF231D85C7DE200E701B5274751F09F00739AFD`. | The existing profile already registers the authoritative build, package, test project, installation, and acceptance paths. | Static contract tests compare its digest/content. Do not append performance checks or modify any setting. |

The known `System.IO.Compression` and `System.Net.Http` reference-resolution warnings are not configuration defects to conceal. The approved policy is to leave them visible and guard their exact roots/versions plus the final merged/package surface through static tests. Downgrading or directly pinning `System.IO.Compression` to `4.1.3.0` was explicitly rejected because it did not remove the game-side `4.2.0.0` reference graph and would make the source project own a framework assembly it does not ship.

If implementation discovers a need for any other configuration, package, lockfile, warning-policy, pipeline, CI, or metadata edit, it must stop and present the exact file, setting, effect, and smallest alternative before editing.

## 25. Acceptance criteria

The rewrite is complete only when all of the following are true:

1. Intentional serialization, options, and player-visible behaviors remain compatible.
2. `TemperatureIndexData`, its getter, and the global band model are absent.
3. No unapproved shim or parallel legacy algorithm remains.
4. Endpoint reference-count mutation is O(1), effective no-ops do not increment generation, immutable reconstruction occurs only on the mutating path, and `ActiveTemperatureConstraintSnapshot` never copies every component registration.
5. Hot readers never rebuild constraint indexes.
6. Every temperature conversion uses the canonical truncation and decision-bucket implementation.
7. Storage eligibility uses normalized interval sets, not per-band tag sets.
8. Status inventory stores only occupied temperature classes and answers range totals from prefix sums.
9. Status queries do not enumerate every `WorldContainer`.
10. The Klei inventory update path publishes one atomic complete-world contribution without a second pickupable enumeration.
11. The FastTrack inventory update path publishes one atomic complete-world contribution for its actual full update and exactly one atomic resource-tag contribution for each later incremental update; it never rebuilds an unrelated world or tag.
12. Explicit coverage/amount result states distinguish unknown/current/required coverage, disabled constraints, incomplete inventory, and complete amounts; a present tag whose current series has not arrived never becomes a fabricated zero.
13. Base-game and Spaced Out content modes both derive topology from authoritative registered worlds rather than hard-coded DLC assumptions.
14. No unqualified use of “vanilla” remains in architecture, production names, tests, comments, diagnostics, commit messages, or acceptance records.
15. Pickup partitions are scoped by parent world and requested tags.
16. Unrelated constraints cannot fragment a pickup's optimized partition.
17. Missing/stale pickup partitions use exact decision classes and remain correct.
18. Optimized partition equivalence and minimal fragmentation pass exhaustive tests over every canonical decision bucket (`10,002` for build `744825`).
19. The combined fetch snapshot publishes only when every captured generation/version remains current.
20. FastTrack uses collision-free update-local key allocation.
21. FastTrack-absent, disabled, and inactive games have no FastTrack hot-path work; an active critical mismatch aborts coherent activation before patching; a status-only mismatch disables only the optional status adapter; physical file identity is read exactly once through the production reader before any feature is `Ready`.
22. Worker code reads immutable world topology and does not call Unity or `ClusterManager`.
23. Late old-session publications are rejected.
24. Game-session shutdown and repeated component/world cleanup are idempotent.
25. Fixed reusable buffers are bounded; unused temperature buckets do not create hot-path scans or sparse publications; oversized variable collections are released after full processing; retention tests use real immutable policy thresholds rather than injected smaller limits.
26. Status-disabled mode installs no status/inventory hooks and performs no associated allocations.
27. Every implementation chunk has focused red/green/refactor evidence, fresh pipeline `validate`/`build`/`test` evidence, and a coherent signed commit prepared from an exactly approved snapshot/message.
28. The existing required pipeline test project contains the complete automated suite by linking the real pure production sources; no copied algorithm or side test project exists.
29. The game-loaded assembly targets exactly `netstandard2.1` and all production/linked production files remain C# 8-compatible; tooling/tests target `net10.0` with test-only C# 14; neither project adds `LangVersion`; the final projects have nullable enabled and compiler warnings as errors.
30. Static contracts prove the current ONI build/max-temperature/method signatures, exact known two-warning reference roots, merged-assembly references, package boundary, intentional serialization/public surface, pipeline-profile invariance, and best-efforts FastTrack `0.18.4.0` fixture contract.
31. The final static baseline/candidate diff passes and exactly four indicative manual sessions complete without a functional failure, relevant exception, or obvious candidate-only slowdown: separate baseline-role and candidate-role derivatives copied from each of the user's untouched late-game base-game and Spaced Out content-mode saves, every other mod disabled. A noisy or inconclusive magnitude of improvement is not itself a failure.
32. Player logs contain no new relevant lifecycle, Harmony, worker, or unhandled exceptions in those candidate runs.
33. The source and static performance-shape evidence show no avoidable dense `world × tag × temperature` work, no hot-path constraint rebuild or complete-range scan, no unrelated-tag partition fragmentation, no per-FastTrack-delta complete-world reconstruction, and no unbounded retained collection growth.
34. Release artifacts are release-version artifacts only; no beta is created, and publishing/uploading still requires separate explicit authorization.
35. Harmony targets use the shared exact member/anchor/authority verifier; a one-time `Game.OnLoadLevel` authority recheck publishes no session after a replacement topology change and never unpatches or falls back.
36. `DeliveryTemperatureRuntimePatchPlan` has one ordered semantic patch-group list and no parallel path enums or boolean mirrors.
37. Test doubles retain exact production identities and member parity; MSTest assertions and three-segment test names follow the approved modern contracts.

## 26. Rejected alternatives

### 26.1 Keep `TemperatureIndexData` as a facade

Rejected because no named consumer was found and a facade would either preserve obsolete global semantics or lie about new scoped semantics.

### 26.2 Use one underflow bucket per negative integer Kelvin

Rejected because valid constraints cannot distinguish any truncated value below `0 K`. Negative Celsius values above absolute zero are already represented by Kelvin buckets `0..273`.

### 26.3 Keep dense arrays because the temperature range is bounded

Rejected for published world/tag data because even a bounded `10,002`-bucket range becomes large when multiplied by tags, worlds, refreshes, status items, and mutable dictionary overhead. Fixed arrays remain appropriate only for the small number of reusable, thread-confined accumulators and endpoint counts whose hot paths touch observed entries rather than scanning the full range.

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

### 26.11 Keep .NET Framework 4.8 or target a modern .NET runtime directly

Both are rejected. `net48` does not follow the current ONI developer guidance selected by the user; .NET 8/9/10 game assemblies are not loadable contracts for ONI's Unity/Mono environment. The correct separation is `netstandard2.1` for the game-loaded DLL and `net10.0` for tests/static tooling.

### 26.12 Multi-target or ship a reflection-metadata sidecar

Rejected as unnecessary complexity and a deployment risk. The pipeline packages one merged game DLL. Static analysis belongs in the development test process, where `System.Reflection.Metadata` is available without imposing it on ONI.

### 26.13 Support old ONI builds through inferred signature compatibility

Rejected because Steam normally updates ONI, offline users can deliberately remain behind, and compatible method signatures alone cannot prove behavioral compatibility. This release deliberately supports the current public build `744825`; it does not claim that an old offline installation is safe.

### 26.14 Hide the two framework-reference warnings

Rejected. Direct assembly pins, binding redirects, `AutoUnify=false`, and downgrading `System.IO.Compression` did not produce a cleaner or more truthful artifact contract. The merged DLL directly references neither disputed assembly, so visible exact-root warnings plus static output/package tests are the safer policy.

### 26.15 Modify the pipeline profile for performance evidence

Rejected because the current pipeline already governs build, test, package, install, and acceptance. The simple two-colony comparison is supplemental and its concise Markdown record is explicitly non-blocking; changing `oni-mod-pipeline.toml` would incorrectly make that record part of the required release contract.

### 26.16 Run automated ONI, a profiler campaign, or repeated benchmarks

Rejected as disproportionate for a community mod. Deterministic structural tests and static baseline/candidate analysis prove the intended performance shape; four simple manual runs provide an understandable indicative check without pretending to be a statistically isolated CPU benchmark.

### 26.17 Copy every registration into each active-constraint snapshot

Rejected because bulk component spawn/save-load churn would make every effective registry mutation copy all prior registrations, producing avoidable quadratic aggregate work. Snapshot consumers require generation, enabled counts, and sorted unique endpoints; component-specific ownership remains in the token-addressed registry.

### 26.18 Retain a standalone pickup partition catalog

Rejected because it would duplicate endpoint ownership and cache invalidation across the combined snapshot and grouping path. `FetchTemperatureEligibilitySnapshot` owns immutable per-parent/tag endpoints; one `PickupTemperatureGroupingSession` owns only its update-local union interning and definition IDs, then releases them.

### 26.19 Encode domain state as booleans paired with defaultable values

Rejected because `complete=false` cannot distinguish disabled constraint, missing world/generation, required coverage, and pending present-tag series, while a default float or constraint remains easy to misuse. Semantically named builder operations and result enums make illegal combinations unrepresentable or contract violations.

### 26.20 Inject smaller production retention limits for tests

Rejected because a test-only policy seam can diverge from the real resource behavior and become an accidental public/internal configuration surface. Tests exercise each actual constant at its boundary with lightweight deterministic values and inspect only predeclared private collection identity after all workload entries are processed.

### 26.21 Treat emitted FastTrack assemblies as physical-version evidence

Rejected because dynamic assemblies have no trustworthy file location, file version, or digest. Emitted fixtures test structural branches through a dedicated identity-result adapter; the production reader and exact `0.18.4.0` physical-file requirement have separate tests and remain mandatory at runtime.

### 26.22 Recheck patch authority in gameplay hot paths

Rejected because replacement topology is a cold lifecycle concern and per-update checks would add reflection/patch-owner work to the very paths being optimized. Authority is verified at installation and exactly once again at `Game.OnLoadLevel` before session publication; gameplay hooks are session-guarded thereafter.

## 27. Final decision

Implement one coordinated rewrite based on scoped immutable constraints, the current ONI `10000 K` storable-temperature bound, sparse temperature amount series, explicit complete-world/coverage/single-tag inventory publications and availability states, normalized storage intervals, snapshot-owned parent-world/tag endpoints, update-local pickup partitions, collision-free FastTrack keys, authoritative content-neutral world topology, checked token/session generations, and game-session safety. Preserve intentional saves and gameplay; remove the old global temperature-index subsystem and accidental public surface. Use exact decision-bucket fallback whenever optimized data cannot be proved current. Compile the game-loaded assembly for `netstandard2.1`/C# 8, link the same C# 8 production files into the `net10.0`/C# 14 tooling project, and keep `oni-mod-pipeline.toml` unchanged. Develop through focused TDD, mandatory pipeline gates, and coherent signed chunk commits. After all fixes are integrated, perform static baseline/candidate analysis and the four approved Klei-path manual runs in derivative base-game and Spaced Out colonies. Treat FastTrack `0.18.4.0` support as a structurally plus physical-identity verified, best-efforts static contract rather than a release-wide in-game validation matrix.

No open architecture decisions remain in this specification.
