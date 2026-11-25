# Delivery Temperature Limit on ONI U60

**Accepted implementation plan, 25 November 2026.**
Target the installed November Quality of Life release, Klei changelist `764025`.
Adopt PLib `4.26.0`.
Qualify FastTrack Beta `0.18.6.0` as the **only maintained FastTrack fixture** and retire both earlier fixtures and their runtime catalog entries in the same change.
Two changes to Klei's fetch code require gameplay repairs even after the mod compiles.
A player must still be able to copy a temperature range, see the right material availability, and watch duplicants deliver only eligible material.
[Klei's release notes](https://store.steampowered.com/news/app/457140/view/714544468141801835)

FastTrack's default update paths reference game members absent from U60.
FastTrack support is best-effort.
A standalone U60 candidate that passes its acceptance checks may advance to release review with any FastTrack limitation disclosed.
A compatibility claim for a specific FastTrack version and settings requires a passing paired in-game run.
Adopt the single-owner dependency rules below as part of each upgrade.
[Version ownership and drift checks](#dependency-versions-have-one-editable-owner)

The owner requested the plan and both upgrades, selected the latest-only fixture policy and a single editable version owner for each dependency, and confirmed the [FastTrack release policy](#accepted-fasttrack-release-policy).
The unpublished `2026.11.25` candidate projects its minimum build from the qualified U60 reference set.
The maintainer approved the configuration edits.
In-game acceptance and the release decision are separate gates.
The event overload alone would not require that change.
The fetch method bodies and the U60 dependency do require fresh qualification.
The previous design and released binaries remain historical evidence.
[Existing game compatibility decision](../specs/2026-08-29-delivery-temperature-limit-performance-rewrite-design.md) · [Existing fixture and runtime policy](../specs/2026-09-01-external-mod-compatibility-evidence-design.md)

**HISEW route.**
Implementation followed the R2 route using the sibling workflow source.
A missed Harmony branch can silently change deliveries or report no available material in existing colonies.
The repo also ships a merged third-party library and pins private game references for CI.
The maintainer owns acceptance of the requirements and the release decision.
The accepted plan was baselined in signed commit `b197fa1`.
Local Codex Review Agent reviews covered the native branch proofs, fixture identity and targeted regression tests.
This plan applies the sibling workflow at `d96ceba3aa1ee93e37bec6850d49d8b898236d68`.
Its source-checkout setup inspection reported `repair-required` because the packaged engine bootstrap was absent.
There is no engine-admitted baseline, review or execution result associated with this plan.
[Planning procedure](https://github.com/Hadden-Industries/software-engineering-workflow/blob/d96ceba3aa1ee93e37bec6850d49d8b898236d68/skills/hadden-industries-plan-software-change/SKILL.md) · [Risk routes](https://github.com/Hadden-Industries/software-engineering-workflow/blob/d96ceba3aa1ee93e37bec6850d49d8b898236d68/method/risk-routes.md)

## Implementation and acceptance record

The local release is `2026.11.25` and the merged `netstandard2.1` assembly reports `2026.11.25.0`.
Before the candidate freeze, 1,010 mod tests passed against an explicitly selected merged build, with no skips.
The pipeline passed 445 tests, including validation with an invalid checkout-side verifier, and the private-reference suite passed 15.
The installed CLI reads the selected catalog as data and uses the verifier embedded in its own assembly.
The subsequent security review identified two ways that Git discovery could execute code from a supplied checkout.
The corrected floor command walks to the nearest checkout marker and rejects intervening directory links.
Commands that need a commit or dirty-file list now read the index and working tree in process with LibGit2Sharp.
All four tracked C# projects compiled against the separately acquired reference pack and its directory was reverified after compilation.
The development installation passed the package checksum check in an isolated ONI user-data directory.
The Workshop listing describes the U60 target and the outstanding FastTrack limit.

Standalone human acceptance requires a copied legacy colony and a new colony, same-colony timings, and the Uploader paste-and-cancel check.
A paired run qualifies a particular FastTrack beta and settings combination.
The standalone candidate can proceed to release review after its own checks pass with that limitation disclosed.
The local Steam cache contained legacy items.
A genuine loose Workshop item and its game-loader result remain outstanding.
The private package has been built locally.
Hosted CI qualification requires separately authorized private package publication and a trusted run.
The candidate manifest, installed receipt and acceptance report carry the identities and readiness of the exact packaged bytes.
[Local release procedure](../../tools/oni-mod-pipeline/manual/preparing-releases.md)

## Evidence for the target

Repository inspection started at clean `main` commit `4255b9c21849a318ea191050a37c14b90f5b1bd0`.
There is one mod, Delivery Temperature Limit (Supercooled), and a separate ONI Mod Pipeline CLI.
These observations identify the inputs a later implementer should reproduce.

- **Game.**
  The installed Windows Steam app `457140` reports Steam build ID `26798728`.
  Klei's assembly constants report changelist `764025`, branch `release` and `Sim.MaxTemperature = 10000f`.
  The Unity player reports `6000.3.17f1`.
  The CI catalog still points at `744825.0.0`, built with Unity `6000.3.5f2`.
  Steam's build ID and Klei's changelist serve different consumers.
  [Current runtime tests](../../mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/CurrentOniRuntimeContractTests.cs) · [Private reference catalog](../../tools/ci/oni-references.json)
- **Assembly fingerprints.**
  Installed SHA-256 values are `96b192e45e044aa591e7143bbe1126d685dd1a195db7ba1632fdf4abd8fe3036` for `Assembly-CSharp.dll`, `ec4dada9437739ec8c3253a8884ba9165a56e80fba97a6494dd42f78148cad89` for `Assembly-CSharp-firstpass.dll` and `aec7446028fe31d00dfecc684e40011c4208bb036e82617b8de32002a8e55b53` for `0Harmony.dll`.
  Harmony's shipped version is still `2.4.2.0`.
  These identify this installation.
  Re-read a changed installation before adopting its hashes.
- **Compiler.**
  Production already targets `netstandard2.1` with SDK-derived C# 8, nullable and warnings-as-errors.
  The CLI and TUnit tests run on `net10.0`.
  An in-memory Roslyn probe used SDK `10.0.404`, all 148 tracked production C# files, the previous compiler reference list redirected to the installed game and locked PLib `4.25.0`.
  It produced one warning promoted to error, CS0618 at `TemperatureLimit.OnPrefabInit`.
  This probe did not run MSBuild resolution, ILRepack, Harmony or the game.
  [Production project](../../mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj) · [Framework and lock contracts](../../mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/ProjectTargetFrameworkContractTests.cs) · [Unity managed plug-in compatibility](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/overview-of-dot-net-in-unity/dotnet-profile-support)
- **Gameplay.**
  Comparing the pinned reference archive with the installed method bodies found a zero-match preflight on `ClearableManager.CollectChores` and a still-matching but incomplete hook on `GlobalChoreProvider.UpdateStorageFetchableBits`.
  The four inventory patterns and the two `FetchAreaChore` extension patterns examined still match statically.
  World topology, grouping and worker-read anchors should be rechecked through the installer.
  Static agreement cannot establish runtime patch ordering or player behavior.
  [Direct delivery adapter](../../mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiDirectDeliveryEligibilityPatches.cs) · [Fetch eligibility adapter](../../mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiAuthoritativeFetchTemperatureEligibilityPatches.cs)

### FastTrack 0.18.6.0

The linked `FastTrackBeta` is a public pre-release dated 26 October 2026.
The current catalog and permanent tests cover `0.18.4.0` and `0.18.5.0`.
The tag can move.
Release asset `677623555`, `FastTrack.zip` (562244 bytes), matched GitHub's SHA-256 `882eac3c39571e921645b892005dc3fb4c8f34b43e532705bec3d95da1ff682a`.
The **inner DLL** has SHA-256 `58dbbb3e347b3e9edbaae7fb9bf706c6ab304ce6d9216a8bffbc4624b4138e1a`, file version `0.18.6.0`, assembly version `0.18.0.0` and MVID `b82e018d-40cf-4df1-b9be-10e86ca3876d`.
The source tag resolves to `38dfc8c9aa2e3263c5bb033a8eafa64c20785485`.
Its declared minimum game build `736649` predates U60.
Compatibility on `764025` needs both the released bytes and an actual loaded game.
[FastTrack release](https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta) · [Tagged source](https://github.com/peterhaneve/ONIMods/tree/38dfc8c9aa2e3263c5bb033a8eafa64c20785485) · [Current production catalog](../../mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackSupportedAssemblyBuildCatalog.cs)

### PLib 4.26.0

The maintainer released PLib `4.26` for U60 on 21 November 2026.
NuGet lists `4.26.0` as the latest stable package at inspection.
Its `netstandard2.1` asset is compatible with the project's framework, declares `Lib.Harmony.Ref` with minimum `2.4.2` and comes from `2faede0016540dd5bd994558333fb1d4e95148d3` under MIT.
The NuGet `lib/netstandard2.1/PLib.dll` has SHA-256 `fb2320301a43eb49dd6cdbafffa37ec8f6b03006653bd0e52157cffb3468c811`.
The standalone GitHub DLL has different bytes.
Use the NuGet asset that the existing Source project restores and merges.
[Maintainer's U60 release](https://github.com/peterhaneve/ONIMods/releases/tag/PLib4.26) · [NuGet package](https://www.nuget.org/packages/PLib/4.26.0) · [Merge target](../../mods/delivery-temperature-limit-supercooled/Source/ILRepack.targets)

## Proposed decisions

- **DEC-01.**
  Use the installed public U60 managed API and game-matched Harmony.
  Qualify the exact changelist before projecting the minimum build and publishing compatibility claims.
- **DEC-02.**
  Own each dependency selection in one place.
  Use the versionless NuGet reference for merged PLib and the shared provider-local identity for optional FastTrack.
  Generate required literals and measure actual artifacts independently.
- **DEC-03.**
  Reuse Klei's per-pair eligibility and per-destination reachability checks.
  Preserve its tag-cache outputs, priorities and mutation order while capturing the extra information the mod needs.
- **DEC-04.**
  Keep exactly the latest released FastTrack fixture.
  Admit an active replacement only for verified bytes and a proven feature on U60.
  Separate default whole-mod compatibility from the result for any narrower settings.
- **DEC-05.**
  Bind release evidence to the hashes of one merged candidate.
  Klei owns Workshop ingestion and recovery, and the maintainer owns publication.

### Accepted FastTrack release policy

On 25 November 2026 the maintainer confirmed that FastTrack is optional and supported on a best-effort basis.
If a failure is confined to FastTrack, the standalone U60 candidate may advance to normal release review after its automated and in-game acceptance checks pass.
Disclose the affected FastTrack version and settings in the Workshop description and change notes, then regenerate the README.
Advertise a specific configuration as compatible once it passes a paired in-game run.
The maintainer retains the separate Workshop publication decision.
[Workshop description](../../mods/delivery-temperature-limit-supercooled/STEAM_DESCRIPTION.bbcode) · [Change notes](../../mods/delivery-temperature-limit-supercooled/STEAM_CHANGE_NOTES.bbcode) · [Release profile](../../mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml)

## Requirements and acceptance

| Requirement                                                        | Observable acceptance and quality scenario                                                                                                                                                                                                                                                        | Slices             |
| ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ |
| REQ-01. Load and copy settings on U60                              | AC-01 / QA-01. A player copies distinct bounds from one bin to another. Only the receiver changes. Copying a disabled source disables the receiver. Repetition, destruction, save and reload preserve the intended state. The merged PLib options and side screen open and accept keyboard edits. | 001, 002, 007      |
| REQ-02. Preserve direct sweep eligibility                          | AC-02 / QA-02. For two pickups of the same prefab at different temperatures and two candidate destinations, an incompatible first pair advances to the next candidate. Eligible pairs still obey native preconditions and priorities.                                                             | 003, 005, 007      |
| REQ-03. Publish complete, world-specific fetch eligibility         | AC-03 / QA-03. Single-tag, multiple-tag, repeated and colliding tag hashes yield the independently expected reachable destinations and temperature intervals. With limits disabled the native result and work path remain intact. Interrupted or stale collections cannot publish partial data.   | 004, 007           |
| REQ-04. Qualify the selected optional FastTrack release            | AC-04 / QA-04. Exactly one real fixture matches the selection. Every admission matches that fixture and qualified settings. If none qualifies, active required replacements are rejected. Absent or inactive replacements select Klei.                                                            | 003, 004, 005, 007 |
| REQ-05. Bind development, CI and release evidence to the same game | AC-05 / QA-05. A fresh locked build produces the declared merged output. The private reference package passes independent acquisition and rejects tampered inputs. The locally installed candidate matches its manifest and passes its recorded acceptance.                                       | 001, 002, 006, 007 |
| REQ-06. Edit each dependency selection once                        | AC-06 / QA-06. In a disposable intake, changing one owner and regenerating derived outputs suffices. Checks reject a stale NuGet lock, mismatched PE, second FastTrack fixture and drifted ONI floor. A clean rerun yields no diff.                                                               | 002, 005, 006      |

## Implementation slices

### SLICE-001. Reproduce the U60 contracts at the consumer

**REQ-01, REQ-05 · DEC-01.**
Start with the installed public game.
Read the branch, changelist, Unity product version, assembly identities and full reference inventory.
Use `diagnose`, `validate` and a fresh pipeline `build` to establish the maintained baseline.
Capture the unedited CS0618 and the actual MSBuild diagnostics.
`KnownOniReferenceConflictContractTests` currently expects `System.IO.Compression` and `System.Net.Http` conflicts.
Reconcile any changed warning with resolved assemblies before changing the expected inventory.
Keep reference-resolution, C# compilation and merged-assembly evidence distinct.
[Build guide](../../tools/oni-mod-pipeline/manual/building-testing-and-installing-mods.md) · [Conflict tests](../../mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/KnownOniReferenceConflictContractTests.cs)

Rebind the exact public-runtime tests and the `OniStorableTemperatureBounds` evidence comment together.
On `764025`, `PrimaryElement.OnDeserialized` compares `10000 >= temperature` on its accepted path and `SimMessages.ModifyCell` compares `temperature <= 10000`.
Bind the literal, operand order and branch destination in each method.
The current test accepts a comparison found elsewhere in the body.
Show that an exclusive bound or reversed branch fails.
Preserve the distinction between a *valid stored* 10000 K and the mod's *exclusive* high limit.
Preserve cast-to-integer comparison, zero-high disabled sentinel and empty enabled ranges.
Validate the identity and range semantics independently of descriptive text.
[Bound owner](../../mods/delivery-temperature-limit-supercooled/Source/TemperatureConstraints/OniStorableTemperatureBounds.cs) · [Bound tests](../../mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/OniStorableTemperatureBoundsContractTests.cs)

Use the observed bodies to write a failing sweep-anchor case and a failing single-tag/repeated-hash behavioral case for the current adapters.
Keep fixture preparation and IL reading within the existing metadata/Harmony test infrastructure.
A maintained build, a method name or a matching opcode window alone cannot close either case.
If Steam has replaced the observed bytes, freeze the new identity and repeat those specific observations before porting the patches.
This discovery increment has no releasable binary.
[Harmony contract tests](../../mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs)

### SLICE-002. Copy settings and open the options screen with PLib 4.26

**REQ-01, REQ-05, REQ-06 · DEC-01, DEC-02.**
Change the sole authored obsolete subscription in `TemperatureLimit.OnPrefabInit`.
Use native `KMonoBehaviour.Subscribe(int, Action<object, object>, object)` for `GameHashes.CopySettings`, a shared callback and the destination `TemperatureLimit` as its registered context.
The first callback argument is the registered receiver.
The second is the source `GameObject`.
Klei's `Storage.OnPrefabInit` uses this exact form and its callback confirms the argument order.
The overload also exists in the `744825` reference archive.
Resolve the source through `TemperatureLimit.Get` and apply `CopySettings` once through its existing normalization and publication path.
Empty, destroyed or unregistered sources leave the destination untouched.
[Component owner](../../mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimit.cs)

The clipped `Unsubscribe` note is resolved by the installed `KMonoBehaviour` metadata.
Its action-based `Unsubscribe(int, Action<object>)` is obsolete.
Native `Unsubscribe(int)` and `Unsubscribe(ref int)` take the returned registration handle.
The current mod has no authored call to the obsolete unsubscribe overload.
Follow the game owner's same-object lifetime, as `Storage` does.
The mod attaches `TemperatureLimit` to prefabs, build panels and construction sites for the lifetime of their game objects.
Its cleanup removes the exact session registration.
Use Klei's object cleanup for the event subscription.
Reopen explicit handle ownership if a supported operation later removes the component while its game object stays alive.
Prove destruction, reload, failed activation and two receivers cannot deliver a callback to a stale component or mutate a later session.
A shared delegate avoids a closure per building.
[Prefab attachment](../../mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfigurator.cs) · [Construction attachments](../../mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/ConstructionMaterialTemperatureLimit.cs)

Adopt NuGet `PLib 4.26.0` through central package management and its production lock.
Static inspection found all of the old public/protected signatures in the new NuGet DLL, including the mod's UI, options and initialization entry points.
The selected package still needs a locked build and in-game exercise.
Preserve the shipped Harmony ABI and the existing ILRepack boundary.
Rebuild using locked restore, then inspect the candidate's target framework, intentional public types, merged PLib and external assembly references.
Demonstrate `PUtil.InitLibrary`, localization registration, options open/save/reopen, blueprint widget, side screen and copy on the merged candidate.
Distinguish PLib's own cell/equipment helpers from the paths this mod actually invokes.
Retain upstream MIT notices under the project's distribution practice.
[Mod entry point](../../mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitMod.cs) · [Central pins](../../Directory.Packages.props) · [Production lock](../../mods/delivery-temperature-limit-supercooled/Source/packages.lock.json) · [Runtime surface tests](../../mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/IntentionalRuntimeContractTests.cs)

**Proof.**
Extend the merged-assembly contracts to distinguish the actual called overload and exercise its behavior with independent source/destination ranges.
For example, copy `[280, 310)` over `[320, 350)`, copy a disabled source, repeat and reload.
Check both buildings and the effective delivery decision.
Equal settings must not advance constraint/fetch generations.
UI failure containment and exact session registration cleanup have existing owners.
This slice restores compilation and the component path.
Full in-game acceptance uses the integrated sweep and fetch repairs below.

### SLICE-003. Filter the concrete sweep candidate on Klei's U60 path

**REQ-02, REQ-04 · DEC-03, DEC-04.**
`ClearableManager.CollectChores` still has the old outer signature.
Its loop now calls `ComputeFetchRange` and `EvaluateFetchRange`.
The former caches possible candidate indices by prefab tag.
The latter has both `SortedClearable.pickupable` and the current `GlobalChoreProvider.Fetch.chore`.
Its native tag checks converge immediately before `Chore.Precondition.Context.Set`, followed by `RunPreconditions`.
The current transpiler searches the outer method, where its typed pickup and eligibility anchors are gone.

Move target resolution, preflight and installation together to the inner per-pair decision.
Bind the actual member signatures, stack types, incoming branches and native next-candidate destination.
Apply the existing `IsPickupAllowedForFetchChore` predicate once after native tag admission and before creating the context.
On rejection continue with the next **candidate for this pickup**.
On acceptance run all original preconditions and keep ordering and first-success behavior.
Retarget labels so both admissible tag paths pass the gate.
Keep temperature out of the tag-index cache because two instances with the same tag can have different temperatures.
Preserve existing direct-fetch, chore-combination and FetchArea vetoes.
[Adapter and existing predicate](../../mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiDirectDeliveryEligibilityPatches.cs) · [Patch installer](../../mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs)

**Proof.**
Exercise an actual U60 instruction stream through the adapter, plus missing, duplicated and redirected anchors.
These must fail preflight before a partial group is installed.
Exercise a cold and a hot item sharing one prefab tag, incompatible A followed by compatible B, both tag paths, no eligible destination, native rejection and no active temperature constraint.
The expected pairs come from assigned temperatures, building ranges and game priority, independently of the rewritten IL.
Verify the next-candidate branch does not skip the whole range or suppress native preconditions.
Keep FastTrack-absent Klei operation independently demonstrable.

### SLICE-004. Collect every eligible destination despite tag-hash deduplication

**REQ-03 · DEC-03.**
`GlobalChoreProvider.UpdateStorageFetchableBits` now shares a pooled set of seen `tagsHash` values across the sorted-world traversal.
A seen hash skips even the type, destination and reachability checks.
An eligible first occurrence uses `Add(tagsFirst)` for one tag or `UnionWith(tags)` for multiple tags, then marks the hash seen.
Today the mod records only after `UnionWith`.
Moving that hook to the join fixes single-tag requests but still loses later destinations with different bounds, unbounded storage, another parent world or colliding hashes.
Preserve Klei's native tag output and separately meet the mod's per-destination requirement.
[Fetch adapter](../../mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiAuthoritativeFetchTemperatureEligibilityPatches.cs) · [Eligibility builder](../../mods/delivery-temperature-limit-supercooled/Source/FetchTemperatureEligibility/FetchTemperatureEligibilityBuilder.cs)

Use the existing traversal, active invocation, parent-world key and snapshot builder.
When collecting, remember whether Klei would skip the hash.
Route those candidates through Klei's own storage/food type, live destination, `Grid.PosToCell(KMonoBehaviour)`, `Workable.GetOffsets(int)` and `MinionGroupProber.IsReachable` checks.
At the successful per-destination boundary, record actual tags, current parent and destination constraint exactly once.
A previously seen hash resumes the native next-chore path before any native tag or seen-set writes.
An unseen hash completes Klei's normal single- or multi-tag writes.
With no enabled constraint, use the original fast path, including its early skip.
Verify stack, labels, branch distances, pool recycling and exception flow on emitted IL.
The tag hash cannot serve as destination or tag-set identity.

Measure the extra native reachability calls on repeated hashes, including any observable side effect.
Compare inactive, active with unique tags, and active with many same-tag destinations across worlds.
Count visited chores, extra probes and copied requested tags separately, alongside native tag output and the independent temperature result.
Reuse the existing generation checks, immutable publication, thread confinement and finalizer discard.
A failed, interrupted or superseded traversal must never publish a partial or cross-session result.
Compare candidate and baseline on the same colony and in the same content mode.
Require zero extra reachability probes with no active constraints, one traversal with active constraints, complete results, and work proportional to the visited chores and their requested tags.
If native probes change game state, a second traversal appears, or there is an obvious candidate-only slowdown, revisit this adapter and compare an existing authoritative per-destination source.
A noisy timing observation alone leaves the structural verdict intact.
Record unmeasured latency as such.
The existing release comparison supplies no numerical latency budget.
Avoid a second unconditional world scan or a persistent cell cache.
[Session and publication owner](../../mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSession.cs) · [Existing performance acceptance](2026-08-29-delivery-temperature-limit-performance-rewrite.md)

**Proof.**
For the same tag use two reachable destinations with disjoint ranges, reverse their order, then add a reachable unbounded destination.
Repeat across parent worlds and with two distinct tag sets forced to share a hash.
Include an unreachable first or later destination, wrong chore type, one/many tags, an empty range and teardown mid-collection.
Compare the native tag set to unpatched U60 on the same inputs.
Compare mod sweep availability/grouping to a hand-enumerated per-world destination table and the independent reference model.
Ensure one-tag requests can actually be swept.
A matching `UnionWith` window or a passing builder test without the game adapter misses this regression.
[Reference eligibility model](../../mods/delivery-temperature-limit-supercooled/Tests/ReferenceTemperatureModels/ReferenceTemperatureEligibilityModel.cs)

**U60 implementation evidence.**
The installed traversal checks each chore at most once.
For an already-seen tag hash the active collector also calls the native group probe for that destination.
On first use or after a move, storage's offset tracker can register or move entries in the solid-change and navigation-change scene-partitioner layers.
The nearby `GlobalChoreProvider.UpdateFetches(Navigator)` applies a specific navigator's automation and path-cost filters, so its results cannot establish colony-wide group reachability.
The native pickup offset worker services pickups rather than these storage destinations.
The adapter therefore uses the group probe on the game thread and skips extra probes when collection is inactive.
For *n* constrained destinations in one parent-world/tag group, publication also sorts intervals and their endpoints in O(*n* log *n*) time.
The installed-instruction tests compare tag contents against unmodified Klei code and check two worlds, disjoint ranges, an unconstrained destination and teardown.
Same-colony timing still requires a live run.

### SLICE-005. Replace the FastTrack fixture and qualify the released beta

**REQ-02, REQ-04, REQ-06 · DEC-02, DEC-04.**
Adopt exactly the `0.18.6.0` DLL identified above as inert test data.
Retire the `0.18.4.0` and `0.18.5.0` fixture directories and their runtime admissions together.
Keep exactly one current fixture under both the passing and failing qualification outcomes.
In the qualification build, exercise the proposed admission for that exact DLL.
A distributable build can admit only the identity and settings that passed the live checks.
Its directory under `Tests/Fixtures/ThirdParty/FastTrack` is `0.18.6.0/sha256-58dbbb3e347b3e9edbaae7fb9bf706c6ab304ce6d9216a8bffbc4624b4138e1a/`.
Retain exactly `FastTrack.dll`, `README.md` with intake observations, and the exact tagged `UPSTREAM-LICENSE.txt` there.
The tagged notice covers MIT-licensed code and contains separate image terms.
The fixture uses the DLL and notice.
Keep the ZIP, third-party artwork and FastTrack runtime out of the mod package.
Recompute the member digest, PE identity and module ID from the release before intake.
[Tagged license](https://github.com/peterhaneve/ONIMods/blob/38dfc8c9aa2e3263c5bb033a8eafa64c20785485/LICENSE) · [Preserved assembly tests](../../mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/PreservedFastTrackAssemblyContractTests.cs)

The feature-level comparison found matching instruction operands for 27 methods across seven inventory and pickup types in the previous and new fixtures.
`ParallelInventory` controls the inventory replacement.
The pickup update can be absent depending on `FastUpdatePickups` and the presence of Efficient Fetch.
The released DLL has no `ChoreComparator` direct-delivery replacement.
On this build use the observed Harmony prefix owners to select inventory and pickup independently, with Klei owning direct delivery.
Audit the dormant `IsGlobalChoreCollectionTarget` predicate.
It describes two arguments while the installed `GlobalChoreProvider.CollectChores` has three.
Identify a conflicting skipping prefix on the real target and fail the required capability safely.
Adding the new arity cannot establish that FastTrack supplies that prefix.
[Feature inspector](../../mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityInspector.cs) · [Runtime authority selection](../../mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs)

**There is counterevidence to default FastTrack support on U60.**
With `PickupOpts` enabled by default, the released `World.LateUpdate` postfix reaches `AsyncBrainGroupUpdater.EndBrainUpdate`.
Its sorting path reads `ClearableManager.SortedClearable.comparer`, a field absent in U60.
Its storage-cell path expects `Workable.GetOffsets(int)` to return `CellOffset[]`, while the installed signature returns `IReadOnlyList<CellOffset>`.
A separate default `FastReachability` path from `Game.Update` reaches the old return type.
These are static findings in the foreign binary.
The maintainer's release predates the game update.
A catalog-only change would obscure the risk.

**Qualification and stop condition.**
Reuse the PE tests for exact metadata, worker-read safety, inventory publication, pickup grouping and absence of a direct replacement.
Prove the runtime selects the same loaded mod entry (`PeterHan.FastTrack`, `FastTrack` assembly), its physical DLL digest, real prefix owners and verified members.
Test absent, present with features inactive, inventory only, pickup only, and both active where the beta allows them.
An inactive replacement leaves the corresponding built-in owner eligible.
An unsupported active optional inventory replacement degrades temperature-aware status.
An unsupported active required pickup or direct-delivery replacement blocks gameplay activation.
Exercise those outcomes with a changed digest, a missing physical identity, an unrecognized owner and synthetic retired identities.
Keep foreign patches intact during rollback.
[Integration inspector](../../mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackRuntimeAuthorityIntegrationInspector.cs) · [Fixture closure tests](../../mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackSupportedBuildFixtureExpectationTests.cs)

Run the exact beta with its defaults in a disposable U60 colony through load, multiple updates, pickup, sweep, inventory and save/reload.
Compare with the same colony with FastTrack absent.
Capture the first foreign stack and selected feature state if it fails.
Repeat an affected step with the same save and no FastTrack to distinguish failure of the game/mod integration from failure inside FastTrack.
A separately evidenced, upstream-supported settings combination can justify **only that combination**.
Before admitting a restricted combination, prove that the runtime inspector can distinguish its effective settings or active prefixes from the failing defaults.
If it cannot, withhold the catalog admission and retain the exact beta as candidate evidence.
If no configuration qualifies, the distributable candidate has zero FastTrack admissions.
Its sole fixture still exercises the exact release's identity and the rejection of an active, unqualified required replacement.
Any change to admissions or listing text after a qualification run produces a fresh candidate and requires verification of the final bytes.
Apply the [accepted FastTrack release policy](#accepted-fasttrack-release-policy) when the paired run fails.
Before freezing a standalone candidate, verify that the Workshop description, change notes and generated README accurately describe the tested configurations and any known limitation.

### SLICE-006. Give CI the same private U60 reference set

**REQ-05, REQ-06 · DEC-01, DEC-02, DEC-05.**
Use `tools/ci/oni_references.py` to pack the original managed directory under a fresh private package version, proposed `MaksymShostak.ONI.GameReferences.764025.0.0`.
Its existing `pack --managed-directory <Managed> --version <selected-version> --output-directory <fresh-package-dir> --catalog <approved-catalog>` operation writes a catalog, by default the repository file.
Generate into a specifically approved destination, inspect the exact byte length, archive SHA-256, manifest SHA-256 and file inventory, then promote the reviewed catalog.
The established private package and the installed directory are separate inputs.
Use `acquire --archive <package> --output-directory <fresh-ref-dir> --catalog <approved-catalog>` to verify the archive, manifest and each DLL.
Compile all tracked projects with `codeql_build.py` against the acquired directory and verify it after compilation.
[Reference packer and validator](../../tools/ci/oni_references.py) · [Manual CodeQL build](../../tools/ci/codeql_build.py) · [Reference security and access model](../codeql-analysis.md)

Prove that a changed DLL, unlisted or duplicate archive entry, or wrong archive digest is refused according to the native validator.
Use the existing `tests/test_ci_codeql_references.py` cases and add a focused regression only for changed version projection or acquisition behavior.
A local archive/build proves the local candidate.
Qualifying hosted C# analysis requires authorized private publication, repository-scoped Actions read access and a successful trusted run against the selected commit.
Keep proprietary game assemblies, packages, derived CodeQL databases, credentials, player logs and saves out of public artifacts.
Retain the prior immutable package for earlier commits.

### SLICE-007. Exercise the exact candidate in game and in the Uploader

**REQ-01 through REQ-06 · DEC-01 through DEC-05.**
Run the mod pipeline using the repository's locked toolchain.
From the repository root the supported npm wrapper is `npm run pipeline -- <command>`.
Use the actual game directory or the documented command-local overrides.
The following are implementation and release checks to run after the relevant approvals.

| Stage                  | Maintained command or operation                                                                                                                                                                                                                 | Required observation                                                                                                                                                                                                                                                                                                                                         |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Development            | `diagnose --mod mods/delivery-temperature-limit-supercooled`, `validate --mod mods/delivery-temperature-limit-supercooled`, `build --mod mods/delivery-temperature-limit-supercooled`, `test --mod mods/delivery-temperature-limit-supercooled` | Selected installed Managed path and fresh build-result identity, locked restore, merged-artifact tests, parseable passing TRX for the declared test project. Use `npm run test:pipeline` for changes to the CLI and `python -m unittest discover -s tests -p "test_ci_codeql_references.py"` for the reference tooling.                                      |
| Local rehearsal        | `install --mod mods/delivery-temperature-limit-supercooled --build-result <exact-build-result.json> --target dev`                                                                                                                               | Verify installed bytes. Exercise the new game paths with the integrated changes and use a copy of a pre-U60 colony.                                                                                                                                                                                                                                          |
| Candidate              | `validate --mod mods/delivery-temperature-limit-supercooled --for-release`, `prepare-release --mod mods/delivery-temperature-limit-supercooled`, `install --candidate <exact-candidate-directory> --target local`                               | After the development rehearsal, set the candidate listing from the measured build, run `sync-readme --mod mods/delivery-temperature-limit-supercooled` and its `--check` form, then prepare from separately authorized, committed inputs. Record exact candidate, build, DLL and manifest digests. Avoid enabling simultaneous local and subscribed copies. |
| Acceptance and handoff | `record-acceptance --candidate <exact-candidate-directory> --tester <name>`, then `verify-release --candidate <exact-candidate-directory>`                                                                                                      | Complete the existing profile checks on the installed candidate, including the Uploader paste-and-cancel check. The pipeline's deliverable is `ready-for-upload`. A separately authorized human owns Workshop Publish. Any subsequent content or listing edit requires a fresh candidate and acceptance.                                                     |

Run the eight existing profile checks: storage bin, rocket-interior storage tile, construction option on/off, side-screen editing, keyboard focus, save/load, log review and Uploader text layout.
Include the copy and paired delivery scenarios from slices 002–004 in those observations.
With status reporting enabled, compare accessible amounts for a known mix of eligible, private-stored and wrong-world items.
Interrupt an inventory update and change worlds to check that stale totals cannot replace the current generation.
Verify stored bounds, disabled settings, public component identity and existing option schema on a copied old save as well as a new colony.
Keep `supportedContent: ALL` claims tied to the exercised modes.
[Mod profile and checks](../../mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml) · [Release procedure](../../tools/oni-mod-pipeline/manual/preparing-releases.md)

Klei owns loading loose SteamUGC content and legacy archives, corruption recovery and the restart dialog.
U60's `KMod.Steam.MakeMod` constructs native directory and ZIP sources, and its DLL loader uses `Assembly.LoadFrom`.
Only legacy content was present in the inspected Steam cache.
Exercise a genuine loose item and a legacy item through the actual loader when available.
Record source format, static ID, assembly identity and activation outcome.
A hand-made local folder proves local install only.
The pipeline already stages a directory with exactly the declared DLL, metadata and translations.
Its duplicate detector checks `mods/Steam/<child>/mod.yaml` as an advisory.
Let that advisory tolerate absent or partially available metadata while ONI recovers.
If recovery leaves a mod disabled, verify through ONI's enabled-mod state and diagnostic output.
A quieter dialog cannot serve as evidence of successful loading.
Keep the current uploader contract until its own input format changes.
[Installer](../../tools/oni-mod-pipeline/src/OniModPipeline/ModInstallation/ModInstaller.cs) · [Workshop assembler](../../tools/oni-mod-pipeline/src/OniModPipeline/WorkshopContent/WorkshopContentAssembler.cs)

## Dependency versions have one editable owner

For each future upgrade, edit the owner below, regenerate any required projections, then measure the resulting artifact.
This follows the owner's practice in other local checkouts.
In `owlapi`, an authored selector drives native manifests, literal workflow references and checked projections.
Dated qualification evidence keeps its historical values.
Its Java comparison also reads a single artifact pin.
In `json-quality`, `assets/release-tools.json` supplies the release identity and platform digest, while the consuming script independently hashes the acquired binary.
ONI already uses central NuGet versions and checked projections for Actions.
Apply those patterns to the affected dependencies.
[OwlAPI ownership guide at the inspected revision](https://github.com/Hadden-Industries/owlapi/blob/8a69f2a35cedbe16f66e98dfd4230bb82b3c8bcd/docs/development/dependency-versions.md) · [OwlAPI reference pin](https://github.com/Hadden-Industries/owlapi/blob/8a69f2a35cedbe16f66e98dfd4230bb82b3c8bcd/util/owlapi-reference/pinned-version.json) · [JSON Quality tool pin](https://github.com/Hadden-Industries/json-quality/blob/34fdd4c19d5383783c116d732f70a995460d0f17/assets/release-tools.json) · [JSON Quality verifier](https://github.com/Hadden-Industries/json-quality/blob/34fdd4c19d5383783c116d732f70a995460d0f17/scripts/release-inventory.js) · [ONI dependency ownership](../development-dependencies.md)

- **PLib.**
  Edit only `Directory.Packages.props` for the version.
  Keep the production `PackageReference` versionless.
  Regenerate `Source/packages.lock.json` through NuGet, then perform a new locked restore.
  Framework tests can read the central declaration, while merge tests measure the resolved input and candidate output.
  A deliberate stale lock must fail.
  A failed merge or a standalone PLib shipped beside the mod must fail separately.
- **FastTrack.**
  Keep one editable `file version + inner DLL SHA-256` pair in the provider-local source that owns `FastTrackSupportedAssemblyBuildCatalog`.
  Give the latest selected build a single identity.
  The production catalog references that identity when admitted and is empty when no configuration qualifies.
  Adapt the historical fixture-equals-admitted-builds test to assert exactly one measured current fixture, an exact match for every admission, and zero admissions for a wholly unqualified release.
  This local distinction preserves offline regression evidence even when live qualification fails.
  Production and TUnit already compile the same provider-local source.
  Replace the independently hardcoded *version pins* in `FastTrackSupportedBuildFixtureExpectation`, the two-build catalog test and active-path test setups with the selected identity or measurements from the sole fixture.
  Enumerate the actual tree, read the PE file-version attribute and MVID, rehash its bytes, and compare with the catalog and the content-addressed directory.
  Compute the directory name from the measured identity on intake.
  The new README records acquisition facts, and test-only expectations still independently describe the required feature semantics.
  Synthetic values exercise equality, rejection and sorting without acquiring another release.
  A changed pin with old bytes, changed bytes with the same version, an orphan directory or a second real fixture must each fail.
  This needs no runtime filesystem access to test assets and no second policy manifest.
- **ONI references and support floor.**
  Use the existing `tools/ci/oni-references.json` as the one current reference selector.
  The packer produces its version and hashes from one reviewed intake.
  Under the current single-build policy, derive the Klei change number from the selected package (`<changelist>.0.0`).
  Project it into the required `mod_info.yaml` scalar in an explicit maintenance step.
  Use the catalog in current-build tests, compare it to `KleiVersion.ChangeList` independently read from the installed DLL, and compare actual managed bytes to the pinned manifest.
  Reuse the packer's canonical manifest construction to establish the full installed-directory digest once when only a local install is available.
  This keeps offline development independent of registry credentials.
  Replace the test requiring a literal changelist in a source comment with the installed-boundary assertion.
  Update the current listing claim from qualified evidence and regenerate the README from its BBCode owner.
  Archived logs, published-baseline tests, sample support reports and dated designs have historical meanings.
  They do not select a dependency.
- **Harmony and Unity.**
  The installed game supplies their runtime versions.
  Verify the selected NuGet Harmony reference against the shipped ABI and the resolved package lock.
  Read the Unity build from the player.
  Introduce no additional manually maintained Unity or Harmony version selector.
  Keep the max-temperature value as a gameplay constraint checked against the engine, independently of version selection.

Where ONI or the listing requires literal text, put the smallest projection/check next to the existing reference maintenance and README owners.
Read the one selected release, verify each intended field in its native document, and change only the owned value.
An ordinary build or check reports drift without rewriting inputs.
An explicit write must preserve unrelated metadata and text, refuse ambiguous or invalid input, and be idempotent on a second run.
For example, an anchored YAML value is refused by `OniMetadataReader` and must still be refused through the new caller.
Plan the cutover rehearsal so changing one dependency selection, replacing its actual artifact and regenerating native outputs is sufficient.
Keep each dependency's owner in its existing deployment boundary.
In `docs/development-dependencies.md`, name the FastTrack owner and the ONI floor projection alongside the existing NuGet guidance so a future upgrade starts at the correct file.
[Klei metadata reader in the pipeline](../../tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/OniMetadataReader.cs) · [README synchronizer](../../tools/oni-mod-pipeline/src/OniModPipeline/Readme/ReadmeSynchronizer.cs)

## Other patch-note impacts

| Upstream change                                                                         | Repository decision and check                                                                                                                                                                                                                                                                                                                                                         |
| --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| .NET Framework compatibility for older mods                                             | Klei expects older Framework mods to continue loading. This mod already uses the recommended .NET Standard target. Verify its one game-loaded artifact and keep CLI/TUnit on their own executable framework.                                                                                                                                                                          |
| KleiStringBuilder replaces GlobalStringBuilderPool                                      | Authored production code does not call the obsolete pool. BCL builders are used for matters such as formatting a FastTrack digest. Keep them scoped to their existing workloads. Reassess if profiling shows an actual hot allocation path.                                                                                                                                           |
| VYaml 1.2 → 1.4 and POI loading                                                         | The mod ships translations and ONI metadata and defines no POI templates. The separate CLI uses YamlDotNet for its own validated metadata. Test packaged metadata with ONI and preserve the CLI's rejection of invalid input.                                                                                                                                                         |
| CellChange moves to EventSystem, cached cells and faster PosToCell                      | Authored code has no CellChangeMonitor subscription to migrate. Klei's new fetch reachability path uses the KMonoBehaviour overload. Preserve it there. Verify the existing native WorldInventory filtering and the mod's generation-bound world/rocket topology. Older PLib contained cell-monitor references in its visualizer, which is a dependency concern covered by slice 002. |
| Harvest outputs and YieldFactor, disease index checks and disease ID-hash serialization | The mod consumes pickup temperature and accessible amount. It defines no harvest tuning, disease serializer or disease-index manipulation. Use actual material and copied-save controls. Existing temperature fields need no data conversion for these engine changes.                                                                                                                |
| CreateSubstance/Anims, asteroid and biome remix fixes, requiredDlcIds                   | No authored substance, POI, world-mixing or subworld-mixing definitions were found. Use existing base/rocket coverage for the mod's separate world-parent behavior.                                                                                                                                                                                                                   |
| Effect descriptors and equipment AdditionalTags overload                                | No authored effect descriptor or equipment definition matched. PLib contains equipment-related code. Exercise the PLib functions the mod actually registers. Keep general Klei equipment migration with its owner.                                                                                                                                                                    |
| ElementConverter operational requirement                                                | The mod filters delivery chores. It does not own converter operation. A converter building can receive an eligible delivery subject to normal ONI preconditions.                                                                                                                                                                                                                      |
| IMGUI developer tools on Linux/macOS, Uploader file-count cap                           | The mod's player-facing side screen and options use game UI/PLib. Smoke-test claimed platforms on those UI paths when available. The pipeline has no corresponding 1,000-file gate to remove. Preserve its package allowlist, path containment and file integrity checks.                                                                                                             |

## Integration, approvals and release decision

The mod maintainer integrates the work.
Start with slice 001.
The event/PLib change, the two Klei adapters and private reference preparation have independent owners once the game identity is fixed.
Coordinate the adapter registrations and shared verifier tests in one integration pass.
FastTrack fixture intake and static comparison can proceed alongside the two Klei repairs.
Standalone acceptance depends on the repaired Klei paths and the merged PLib.
The paired game run also requires the exact FastTrack candidate.
Complete the one-owner version work within the slice that changes each dependency.
Slice 007 consumes the resulting immutable candidate.

The owner approved the following configuration changes during implementation.
The release candidate includes the date version and projected game floor below.

| File                                                                    | Smallest proposed edit and effect                                                                                                                                                                                                                                 |
| ----------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Directory.Packages.props`                                              | Change only the `PLib` selection from `4.25.0` to `4.26.0`. The mod will compile and merge against the U60 library.                                                                                                                                               |
| `mods/delivery-temperature-limit-supercooled/Source/packages.lock.json` | Regenerate the `.NETStandard,Version=v2.1` PLib entry (requested range, resolution and content hash) from NuGet. Verify Harmony resolves to the game-matched `2.4.2`. Present any additional graph change before applying it.                                     |
| `tools/ci/oni-references.json`                                          | Set `package.version` to `764025.0.0` and update `package.byteLength`, `package.sha256` and `manifestSha256` to the reviewed packer's measured values. Retain package ID, owner, schema and private access model. CI will consume the new complete reference set. |
| `mods/delivery-temperature-limit-supercooled/mod_info.yaml`             | Set the release version to `2026.11.25`. Project `minimumSupportedBuild` from `744825` to `764025` after qualification. ONI can then exclude earlier builds. Preserve `APIVersion: 2` and `supportedContent: ALL`.                                                |

Exercise new gameplay cases alongside the existing acceptance records.
Package the candidate DLL from `{build-output}`, the source declared in the mod profile.
The tracked mod-root DLL is the published-baseline input to the assembly tests.
Development installs and checks can use per-command paths.
No persistent environment edit is needed.
Ask separately before a commit, push, private package publication or Workshop publication.

**Release observer and recovery.**
The tester records the game identity, selected FastTrack features or absence, candidate digest, copies of the controlled save inputs and redacted relevant diagnostics.
Use the standard local support report and opt-in log excerpt.
Watch for `DTL-GAMEPLAY-DISABLED`, `DTL-STATUS-COMPATIBILITY-DEGRADED`, Harmony contract failures and upstream FastTrack exceptions as well as incorrect deliveries without exceptions.
A failure in core delivery, saves or a required base acceptance check holds the standalone candidate until the owning slice passes.
A failure confined to FastTrack holds the affected compatibility claim.
Once the limitation is documented, the qualified standalone candidate may advance to release review under the [accepted FastTrack release policy](#accepted-fasttrack-release-policy).
Implementation checkout history holds superseded FastTrack evidence.
Keep the active fixture tree latest-only across retries.
A changed release tag, DLL or game hotfix reopens only the relevant measurements, then requires a fresh integrated candidate.
Recheck the linked FastTrack release before describing a candidate as tested with the latest beta.
Routine tests use the frozen fixture offline.
[Support-report procedure](../../SUPPORT.md)

Rehearse upgrades on copies of pre-update saves.
Keep the originals and the prior candidate/manifest.
A failed local install can be replaced through the existing owned-target procedure with a **verified compatible** package.
An earlier mod paired with an earlier game can be retested on the copied pre-update save.
There is no demonstrated save downgrade or Workshop rollback guarantee for U60.
After a public failure, the maintainer can withhold further distribution or approve a forward fix and repeat candidate acceptance.
Report `ready-for-upload`, actual human publication, hosted CI, native platform checks and default FastTrack compatibility as separate observed outcomes.
[Candidate lifecycle](../../tools/oni-mod-pipeline/manual/development-and-release-workflow.md)
