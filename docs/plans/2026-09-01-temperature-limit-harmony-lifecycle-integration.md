# Temperature Limit Harmony Lifecycle Integration Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in dependency order. Follow the repository's test-driven-development and formal review gates, and use the checkboxes (`- [ ]`) to track progress.

**Goal:** Wire the approved provider-neutral preparation, pure activation engine, and failure response into ONI so no gameplay patch is installed during `OnLoad`, every selected binding is registered and audited once during `OnAllModsLoaded`, and every provisional/residual callback is inert unless the central gate is `Active`.

**Architecture:** `DeliveryTemperatureRuntimePatchInstaller` becomes a cold preparation/composition module. `HarmonyGameplayPatchRegistry` is the only concrete Harmony mutation/observation adapter. A process-lifetime runtime owner holds the gate, coordinator, installed plan, and one-shot response services. Klei lifecycle overrides catch every managed exception delivered to them and return normally. Direct callbacks, stateful prefix/postfix/finalizer chains, and transpiled helpers all use one classified inactive route verified against the complete selected binding set.

**Tech Stack:** Harmony 2.4.2; Klei/Unity/PLib runtime APIs; C#/.NET Standard 2.1; MSTest reflection/IL/source contracts; reflection-emitted methods; `oni-mod-pipeline`; two manual development smoke topologies.

**Spec:** `docs/specs/2026-08-31-temperature-limit-lifecycle-contained-activation-design.md`, especially sections 7-9, 12-13, 17-19, and 22-24.

## Global Constraints

- Execute after the declared-integration, pure-core, and failure-response plans are green.
- Retain the exact uncommitted `HarmonyPatchContractBindingVerifier` work and make it a mandatory whole-plan preflight.
- Do not use Harmony attributes or `PatchAll`; every selected binding remains manually resolved, verified, registered, observed, and exactly compensated.
- Do not describe `Harmony.Unpatch(original, patchMethod)` as rollback or a transaction. The user-facing and internal term is patch-registration compensation.
- No gameplay patch callback may read options, discover mods, reflect provider types, or decide capability ownership.
- `Active` is process-terminal success. If game-load authority later changes, reject that game session and leave callbacks session-inert; do not select a fallback on the hot path.
- Do not add runtime fault-injection switches, test-only production branches, or a reset seam.

---

## File and Responsibility Map

| File | Responsibility |
|---|---|
| `GameplayActivation/HarmonyIntegration/HarmonyGameplayPatchRegistry.cs` | Map one exact identity to `Patch`, `GetPatchInfo`, and exact-method `Unpatch` |
| `GameplayActivation/KleiIntegration/DeliveryTemperatureGameplayActivationRuntime.cs` | Process-lifetime composition, gate facade, installed-plan publication, game-load authority |
| `GameplayActivation/KleiIntegration/DeliveryTemperatureActivationSettingsReader.cs` | Exactly one availability-aware gameplay-option capture |
| `GameplayActivation/KleiIntegration/SystemGameplayActivationClock.cs` | Production UTC clock for immutable failure occurrence time |
| `RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs` | Build one complete immutable prepared activation; no state or mutation |
| `RuntimePatchInstallation/InactiveGameplayBehaviorContract.cs` | One reviewed inactive route per selected binding |
| `DeliveryTemperatureLimitMod.cs` | Thin contained `OnLoad` and `OnAllModsLoaded` shells |
| Patch callback classes | Immediate gate/session/default-state behavior |

## Cross-Task Interfaces

```csharp
SettingsSnapshotResult DeliveryTemperatureActivationSettingsReader.Capture();
PreparedGameplayActivation DeliveryTemperatureRuntimePatchInstaller.Prepare(
    IReadOnlyList<KMod.Mod> loadedMods,
    SettingsSnapshotResult settings,
    IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes);
GameplayActivationOutcome DeliveryTemperatureGameplayActivationRuntime.TryActivate(
    Harmony harmony,
    IReadOnlyList<KMod.Mod> loadedMods);
bool DeliveryTemperatureGameplayActivationRuntime.TryCaptureAuthorizedGameSession(
    out DeliveryTemperatureGameSession session);
```

The lifecycle shell supplies authoritative Klei topology only to the runtime owner. The runtime owner builds the short-lived inspection context, cold preparation, and concrete registry, while every gameplay callback sees only the gate/session facade.

## Task 1: Implement the Narrow Concrete Harmony Registry

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/HarmonyIntegration/HarmonyGameplayPatchRegistry.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/HarmonyIntegration/HarmonyGameplayPatchRegistrySourceContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/InstalledHarmonyPatchRegistrySemanticsTests.cs`

- [ ] Add source-contract tests asserting the adapter is the only new type under `GameplayActivation/HarmonyIntegration`, accepts one `Harmony` with exact owner, has one switch over the four existing patch kinds, and contains no FastTrack/Klei selection/report/presentation reference.

- [ ] Add reflection-based tests that load the already digest-pinned installed `0Harmony.dll`, dynamically emit target/patch methods, and invoke real Harmony APIs without a test-project compile reference. Cover prefix, postfix, transpiler, and finalizer registration; exact owner/method/kind observation; exact-method removal; and preservation of another owner's different patch method on the same target.

- [ ] Add a same target/same patch method under a different owner case and record the actual Harmony metadata/removal semantics used by the baseline rule.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~HarmonyGameplayPatchRegistrySourceContractTests|FullyQualifiedName~InstalledHarmonyPatchRegistrySemanticsTests|FullyQualifiedName~CurrentOniRuntimeContractTests"
```

Expected red: the concrete adapter is absent while the installed Harmony digest contract remains green.

- [ ] Implement registration with exactly one populated `HarmonyMethod` slot:

```csharp
switch (identity.PatchKind)
{
    case HarmonyPatchContractKind.Prefix:
        harmony.Patch(identity.TargetMethod, prefix: patch);
        break;
    case HarmonyPatchContractKind.Postfix:
        harmony.Patch(identity.TargetMethod, postfix: patch);
        break;
    case HarmonyPatchContractKind.Transpiler:
        harmony.Patch(identity.TargetMethod, transpiler: patch);
        break;
    case HarmonyPatchContractKind.Finalizer:
        harmony.Patch(identity.TargetMethod, finalizer: patch);
        break;
    default:
        throw new ArgumentOutOfRangeException(nameof(identity.PatchKind));
}
```

- [ ] Implement observation by inspecting all four `Patches` collections for the target, counting the same patch method under the expected owner/kind, other owners, and other kinds, and returning the exact core observation state. Reject duplicate exact registrations, wrong-kind presence, and exact-plus-conflicting presence rather than treating one expected match as success.

- [ ] Implement removal only as:

```csharp
harmony.Unpatch(identity.TargetMethod, identity.PatchMethod);
```

- [ ] Do not call `UnpatchAll`, remove by owner, or touch any different patch method.

- [ ] Run the focused tests again.

Expected green: real installed Harmony behavior matches every adapter assumption.

## Task 2: Capture Settings Once and Turn the Installer into Complete Cold Preparation

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/KleiIntegration/DeliveryTemperatureActivationSettingsReader.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPreparationContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/KleiIntegration/DeliveryTemperatureActivationSettingsReaderSourceContractTests.cs`

- [ ] Add a source contract asserting `DeliveryTemperatureActivationSettingsReader.Capture` is the only activation/report path that accesses `DeliveryTemperatureLimitOptions.Instance`. Existing option-default conversion may read `GameUtil.temperatureUnit`; reporting captures the display unit separately and never labels it an activation option.

- [ ] Add preparation-order tests/fakes proving the full seventeen-step sequence from spec section 9 completes before the registry's first register call. Include the seven currently topology-independent bindings in the same final ordered set.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~DeliveryTemperatureRuntimePatchPreparationContractTests|FullyQualifiedName~DeliveryTemperatureActivationSettingsReaderSourceContractTests"
```

Expected red: options are still re-read and the installer still mutates in two lifecycle phases.

- [ ] Capture options with one lazy access inside one try/catch:

```csharp
internal SettingsSnapshotResult Capture()
{
    try
    {
        DeliveryTemperatureLimitOptions options =
            DeliveryTemperatureLimitOptions.Instance;
        return SettingsSnapshotResult.Available(
            new ActivationSettingsSnapshot(
                options.CheckTemperatureForStatusItems,
                options.UnderConstructionLimit,
                options.MaxConstructionTemperature,
                options.MinConstructionTemperature));
    }
    catch (Exception exception)
    {
        return SettingsSnapshotResult.Unavailable(
            failureFactory.Create(
                "DTL-ACTIVATION-SETTINGS-UNAVAILABLE",
                GameplayActivationFailureStage.SettingsCapture,
                exception,
                null));
    }
}
```

- [ ] Replace `InstallLoadedModTopologyIndependentPatches` and `InstallLoadedModTopologyDependentPatches` with one non-mutating preparation entry:

```csharp
internal static PreparedGameplayActivation Prepare(
    IReadOnlyList<KMod.Mod> loadedMods,
    SettingsSnapshotResult settings,
    IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
```

- [ ] Preserve the exact preparation order: settings; core targets; declared identity matching; FastTrack inspection; generic selection; selected external targets; complete authority verification; every transpiler preflight; every Harmony argument binding; identity uniqueness; inactive-route completeness; one immutable prepared activation. Baseline observation remains the coordinator's last preparation step.

- [ ] Build exact `GameplayPatchRegistrationIdentity` values with `DeliveryTemperatureRuntimePatchInstaller.HarmonyOwner` from the verified binding union. Do not publish them to a static installed field from preparation.

- [ ] Delete the private installer state enum, installed Harmony/plan fields, topology-independent installed flag, `ApplyPreparedPatchesWithExactRollback`, and `RollBackExactInstalledMethods` only after coordinator tests cover their replacement.

- [ ] Run the focused tests and existing runtime-plan/FastTrack suites.

Expected green: preparation is complete, late, immutable, provider-neutral, and mutation-free.

## Task 3: Add the Process-Lifetime Runtime Owner and Gate Facade

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/KleiIntegration/DeliveryTemperatureGameplayActivationRuntime.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/KleiIntegration/SystemGameplayActivationClock.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureGameLoadAuthorityPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCoherentActivationContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/KleiIntegration/DeliveryTemperatureGameplayActivationRuntimeSourceContractTests.cs`

- [ ] Add source/IL tests for one static gate, one coordinator, no reset API, no public API, no direct patch loop, `IsActive` as a gate read only, repeated active/failed behavior, and a nonthrowing inactive `TryStartAuthorizedGameSession` path.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~DeliveryTemperatureGameplayActivationRuntimeSourceContractTests|FullyQualifiedName~FastTrackCoherentActivationContractTests"
```

Expected red: the process runtime owner does not exist.

- [ ] Give the runtime owner these process operations:

```csharp
internal static bool IsActive { get; }
internal static void MarkFrameworkPrerequisitesComplete();
internal static GameplayActivationOutcome RecordFrameworkFailure(Exception exception);
internal static GameplayActivationOutcome RecordLifecycleBoundaryFailure(
    Exception exception);
internal static GameplayActivationOutcome TryActivate(
    Harmony harmony,
    IReadOnlyList<KMod.Mod> loadedMods);
internal static void RespondToFailureOnce(GameplayActivationOutcome outcome);
internal static bool TryStartAuthorizedGameSession(Game game);
internal static bool TryCaptureAuthorizedGameSession(
    out DeliveryTemperatureGameSession session);
internal static SupportRuntimeSnapshot CaptureSupportReportSnapshot();
```

- [ ] Make `TryActivate` call `coordinator.TryGetTerminalOutcome` before settings, topology, preparation, or registry construction. Otherwise construct the concrete registry and cold preparation, stage the attempt's Harmony/registry references while the gate is inactive, then call `coordinator.TryActivate(preparation, registry)`. The coordinator assigns its immutable active prepared activation before its final `Active` publication. Game-load code may read the staged registry plus `coordinator.ActivePreparedActivation` only after `IsActive`; clear staged references on failure. Do not put a concrete publication callback into the pure prepared value.

- [ ] If failure-detail construction itself throws at the last-chance boundary, retain only exception type plus the fixed message `Exception details were unavailable at the lifecycle boundary.` Never fall back to raw `Exception.ToString()` in a report/player value.

- [ ] Move game-load authority ownership here. Its first lines must be:

```csharp
if (game == null || !IsActive)
{
    return false;
}
```

- [ ] Recheck only the selected generic authority requirements once per distinct `Game` identity. On contract violation, metadata/observation exception, or session-publication exception, cache `false`, record a bounded diagnostic, publish no session, select no fallback, and return false. Give `DeliveryTemperatureGameLoadAuthorityPatches.GameOnLoadLevelPrefix` its own last-chance managed boundary so no game-load authority failure escapes into ONI.

- [ ] Run the focused tests again.

Expected green: one process owner mediates every gate/plan/session operation.

## Task 4: Contain Both Klei Lifecycle Overrides and Remove `OnLoad` Gameplay Mutation

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitMod.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/DeliveryTemperatureLifecycleContainmentContractTests.cs`

- [ ] Add compiled IL/source tests asserting `OnLoad` contains no call to `Harmony.Patch`, the registry, or runtime patch preparation; both overrides have an outer managed `Exception` handler; neither handler contains `throw`/`rethrow`; and `OnAllModsLoaded` passes its original `loadedMods` argument to activation.

- [ ] Add a source contract proving support snapshot publication is separate from the authoritative activation argument and response is attempted only for a retained failed outcome.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~DeliveryTemperatureLifecycleContainmentContractTests
```

Expected red: `OnLoad` still installs seven patches and both overrides rethrow.

- [ ] Refactor `OnLoad` to this control shape:

```csharp
public override void OnLoad(Harmony harmony)
{
    try
    {
        base.OnLoad(harmony);
        DeliveryTemperatureSupportReporter.Initialize(mod, assembly);
        PUtil.InitLibrary(false);
        Localization.RegisterForTranslation(typeof(STRINGS.TEMPERATURELIMIT));
        new POptions().RegisterOptions(this, typeof(DeliveryTemperatureLimitOptions));
        DeliveryTemperatureGameplayActivationRuntime
            .MarkFrameworkPrerequisitesComplete();
    }
    catch (Exception exception)
    {
        DeliveryTemperatureGameplayActivationRuntime
            .RecordFrameworkFailure(exception);
    }
}
```

- [ ] Refactor `OnAllModsLoaded` to call base, publish the ancillary sanitized snapshot, activate once with the original list when prerequisites succeeded, respond once on failure, and return. Its last-chance catch calls `RecordLifecycleBoundaryFailure`, attempts the response with that returned outcome, and returns normally. When `OnLoad` already failed, `TryActivate` returns the retained `AlreadyFailed` outcome before settings, topology, or registry construction.

- [ ] Keep `DeliveryTemperatureSupportReporter.Initialize` and `PublishLoadedMods` internally contained so reporter failure cannot block a valid activation.

- [ ] Run the focused tests again.

Expected green: no lifecycle exception is deliberately propagated and `OnLoad` performs no gameplay Harmony mutation.

## Task 5: Gate Game-Session Publication and the Seven Moved Early Callbacks

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionHost.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimit.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfigurator.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/ConstructionMaterialTemperatureLimit.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitUserInterface/TemperatureLimitSideScreen.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureGameSessionLifecycle/DeliveryTemperatureGameSessionTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/InactiveBehavior/EarlyPatchInactiveBehaviorTests.cs`

- [ ] Add `DeliveryTemperatureGameplayActivationRuntime.TryCaptureAuthorizedGameSession`. It returns false before touching the host unless `IsActive`, then delegates to `DeliveryTemperatureGameSessionHost.TryCaptureCurrent`. Change `TemperatureLimit` and every patch callback to use this authorized facade rather than calling `TryCaptureCurrent` directly.

- [ ] Keep session creation reachable only from `TryStartAuthorizedGameSession` after the active gate and selected-authority checks. Add a source contract asserting the runtime owner is the only production caller of `EnsureGameSession` and `TryCaptureCurrent`; detach/final shutdown may still release an already-owned session. This preserves the pure linked session host while proving no non-active production path can create or expose a session.

- [ ] Add direct inactive tests for these seven moved bindings:

  1. `ConfigureTemperatureLimitedDeliveryTargetPrefabsPostfix`
  2. `MaterialSelectionPanelPrefabInitializationPostfix`
  3. `MaterialSelectionPanelConfigurationPostfix`
  4. `BuildingDefinitionInstantiationPostfix`
  5. `BuildingDefinitionPostProcessingPostfix`
  6. `DetailsScreenPrefabInitializationPostfix`
  7. `ComplexFabricatorSideScreenShowPostfix`

- [ ] Assert each returns before component addition, prefab mutation, side-screen registration, layout mutation, options access, or Unity object traversal.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~DeliveryTemperatureGameSessionTests|FullyQualifiedName~EarlyPatchInactiveBehaviorTests"
```

Expected red: session capture is still called directly and early callbacks do not yet consult the activation gate.

- [ ] Put a direct guard before every side effect:

```csharp
if (!DeliveryTemperatureGameplayActivationRuntime.IsActive)
{
    return;
}
```

- [ ] Run the focused tests again.

Expected green: no pre-active prefab/UI/session mutation is reachable.

## Task 6: Gate Every Klei Patch Chain and Every Transpiled Helper

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/DeliveryTemperatureGameSessionShutdownPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/WorldParentTopologyPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiAuthoritativeFetchTemperatureEligibilityPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiWorldInventoryTemperaturePatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiPickupTemperatureGroupingPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/KleiDirectDeliveryEligibilityPatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/KleiImplementationAdapters/TemperatureStatusAvailabilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/InactiveBehavior/KleiPatchInactiveBehaviorTests.cs`

- [ ] Inventory every prefix/postfix/finalizer/transpiler binding prepared by the installer. For direct callbacks, assert inactive calls preserve target arguments/results and create no state. For prefixes with `__state`, assert default/inactive state is assigned before return. For postfix/finalizer pairs, assert default state is a no-op and original exceptions are returned unchanged.

- [ ] For every transpiler, inspect emitted IL and identify the injected helper. Invoke that helper inactive and assert it cannot alter eligibility, grouping, publication, status, or session-owned state.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~KleiPatchInactiveBehaviorTests
```

Expected red: at least direct topology callbacks and one or more helper paths remain active without a session/gate.

- [ ] Use one of these reviewed routes per binding:

  - direct `IsActive` guard before any side effect;
  - `TryCaptureCurrent`/`TryEnsureGameSession` with gate enforcement;
  - default inactive `__state` assigned by the prefix and honored by postfix/finalizer;
  - transpiled call only to a helper that uses one of the preceding routes.

- [ ] Never suppress the target's original behavior while inactive. A Temperature Limit prefix that can return `bool` must return the value that allows the original method unless an active session has made an authorized decision.

- [ ] Run the focused tests plus existing Harmony transpiler contract suites.

Expected green: every Klei binding is behaviorally neutral while inactive and all IL anchors remain verified.

## Task 7: Gate Every FastTrack Patch Chain Through the Same Central State

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/InventoryUpdateAdapters/FastTrackWorldInventoryTemperaturePatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/PickupGroupingAdapters/FastTrackPickupTemperaturePatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/DirectDeliveryEligibilityAdapters/FastTrackDirectDeliveryEligibilityPatches.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/InactiveBehavior/FastTrackPatchInactiveBehaviorTests.cs`

- [ ] Test inactive prefix state, postfix/finalizer cleanup, background-worker helpers, pickup key allocation, direct-delivery helper behavior, and every FastTrack transpiler-emitted call. Assert no worker session, thread slot, group key, resource publication, or eligibility change occurs.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackPatchInactiveBehaviorTests
```

Expected red: FastTrack-specific callbacks rely on session availability but do not all prove the central gate.

- [ ] Add the same central gate/session/default-state routes used by Klei. Do not add a FastTrack-local activation Boolean or bind/discover compatibility from a callback.

- [ ] Ensure bound reflected FastTrack contexts may exist after preparation but cannot be used unless the central gate is active.

- [ ] Run the focused tests plus all existing `FastTrack*PatchContractTests`.

Expected green: external runtime-authority callbacks obey the exact same all-or-inert invariant.

## Task 8: Make Inactive Behavior a Complete Binding-Set Contract

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/InactiveGameplayBehaviorContract.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/InactiveBehavior/PreparedPatchInactiveBehaviorInventoryTests.cs`

- [ ] Define the route enum:

```csharp
internal enum InactiveGameplayBehaviorRoute
{
    DirectActivationGate,
    ActivationAuthorizedGameSession,
    InactiveStatePropagation,
    GateAwareTranspiledHelper
}
```

- [ ] Associate each prepared registration identity with exactly one contract containing route and the exact guarding/helper `MethodInfo`. Verify the guard/helper signature during complete preparation.

- [ ] For Klei-only and every currently selectable FastTrack topology, compare the set of prepared registration identities with the inactive-contract set. Fail on missing identity, extra identity, duplicate identity, or unverifiable route method.

- [ ] Include the seven moved early bindings, fixed lifecycle/topology/fetch bindings, optional status binding, every Klei capability binding, and every selected FastTrack contribution.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~PreparedPatchInactiveBehaviorInventoryTests
```

Expected red: there is no one-to-one mechanically checked inventory.

- [ ] Make the installer refuse to return `PreparedGameplayActivation` until this full set equality and every route-method check pass.

- [ ] Run the focused tests again.

Expected green: adding any future binding without an inactive route fails preparation and tests before mutation.

## Task 9: Exercise Every Selected Binding Index Through the Concrete Adapter

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/HarmonyIntegration/HarmonyGameplayActivationFaultMatrixTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractBindingVerifierTests.cs`

- [ ] Build reflection-emitted selected plans for the complete Klei set and each FastTrack-selected contribution. At every binding index inject before-mutation, after-mutation, absent post-observation, wrong-owner, removal-before-effect, removal-after-effect, residual, and observation-unavailable faults.

- [ ] Assert no first/middle/last shortcut: every index appears in each fault data source, and the data source count equals the exact prepared binding count for that topology.

- [ ] Assert another owner's different method survives every compensation case. Assert same target/patch method baseline ambiguity blocks with `NotRequired` and zero mutation.

- [ ] Re-run the Harmony argument-binding suite across the exact complete selected set, including renamed target argument, wrong by-ref shape, invalid special injection, and overload ambiguity cases.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~HarmonyGameplayActivationFaultMatrixTests|FullyQualifiedName~HarmonyPatchContractBindingVerifierTests"
```

Expected green: concrete registration behavior and all argument bindings pass the exhaustive matrix.

## Task 10: Run Full Static, Build, and Pipeline Gates

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
git diff --check
oni-mod-pipeline diagnose --mod mods/delivery-temperature-limit-supercooled
oni-mod-pipeline validate --mod mods/delivery-temperature-limit-supercooled
oni-mod-pipeline build --mod mods/delivery-temperature-limit-supercooled
oni-mod-pipeline test --mod mods/delivery-temperature-limit-supercooled
```

Expected: every command exits `0`; all tests pass with zero failed/skipped/inconclusive; build prints one new exact `build-result.json`; test prints one exact evidence directory.

- [ ] Retain the exact build-result path printed by this invocation. Do not search artifacts for the newest path or reuse an earlier run.

- [ ] Run the configuration invariance check from the program plan.

Expected: only the approved test-project linked-core item differs; forbidden configuration files are byte-for-byte unchanged.

## Task 11: Install the Exact Development Build and Smoke Test FastTrack Absent

This is an external-state change already within the approved implementation design, but use the guarded pipeline installer and exact build result only. If the user has not authorized installing this new build into the Dev target, stop at this task and request that authority.

- [ ] From the repository root, paste the exact path printed by Task 10:

```powershell
$safeActivationBuildResultPath = Read-Host 'Paste the exact build-result.json path printed by Task 10'
oni-mod-pipeline install --mod mods/delivery-temperature-limit-supercooled --build-result $safeActivationBuildResultPath --target dev
```

- [ ] Disable FastTrack for the smoke topology without deleting or modifying its files. Launch ONI with the Dev Temperature Limit build.

- [ ] Confirm no activation warning appears; a new colony/save reaches gameplay; side-screen limits edit; an out-of-range storage delivery is rejected; an in-range control remains deliverable; and no new lifecycle/Harmony/Temperature Limit exception appears in `Player.log`.

- [ ] Create a manual standard support report from the options action and confirm schema version 2 shows FastTrack `NotMatched`, activation `Active`, no singular `runtime.fastTrack`, and no `playerLog`.

- [ ] Record the ONI build, DLC topology, Temperature Limit assembly version, exact build-result path, and result of each check. Do not attach or upload the report automatically.

## Task 12: Smoke Test the Exact Supported FastTrack Topology

- [ ] Enable an active FastTrack installation whose Klei static ID is `PeterHan.FastTrack`, assembly simple name is `FastTrack`, and file/structural identity satisfies the currently supported `0.18.4.0` contract. If those exact bytes/topology are unavailable or ambiguous, stop and ask the user how to obtain the supported installation; do not substitute a newer/unverified binary or relabel it supported.

- [ ] Launch the same exact Dev Temperature Limit build. Confirm no activation warning; support report shows generic `fast-track` outcomes; selected inventory/pickup paths match the verified feature states; inactive direct-delivery replacement retains the Klei baseline; temperature limits still reject the out-of-range storage delivery; and no relevant exception appears in the log.

- [ ] Exercise one background inventory update and one pickup-grouping cycle so the FastTrack callbacks execute while active. Confirm status availability behaves according to the option and no hot-path compatibility inspection/log spam occurs.

- [ ] Record the FastTrack assembly/file version and SHA-256 reported by the mod, plus every smoke result. This remains best-efforts support for the exact verified artifact, not a release-wide FastTrack promise.

## Task 13: Formal Review Gate, Findings, and Gated Commit

- [ ] Show `git status --short`, `git diff --stat`, the full test/pipeline evidence, both smoke records, and the exact allowed configuration delta.

- [ ] State exactly:

```text
Implementation complete; /review pending
```

- [ ] Ask the user to invoke built-in `/review` over all uncommitted changes in the repository. Do not claim completion and do not commit before that review.

- [ ] Resolve every confirmed P0-P2 finding with focused tests and rerun the affected full gates, or record the user's explicit deferral.

- [ ] Re-run the complete test, pipeline, and configuration-invariance commands after the last review fix.

- [ ] Stage only the reviewed safe-activation implementation snapshot, preserving unrelated user-owned edits outside it.

- [ ] If and only if the user explicitly authorizes this exact staged snapshot after review, load `committing-to-git` and create:

```text
feat(temperature-limit): contain gameplay activation failures

Move all gameplay patches to one late audited activation, compensate exact
attempted registrations on failure, keep every provisional callback inert,
contain Klei lifecycle exceptions, and provide one local diagnostic response.

Verify Klei-only and supported FastTrack topologies through the provider-neutral
capability model and exhaustive Harmony binding/fault contracts.
```

- [ ] Do not push.
