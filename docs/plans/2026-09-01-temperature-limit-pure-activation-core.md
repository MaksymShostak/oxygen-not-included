# Temperature Limit Pure Activation Core Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in dependency order. Follow the repository's test-driven-development and formal review gates, and use the checkboxes (`- [ ]`) to track progress.

**Goal:** Implement and exhaustively test the framework-independent one-attempt state machine that makes the complete selected gameplay patch set active or leaves every Temperature Limit callback inert.

**Architecture:** A fresh-instance `GameplayActivationCoordinator` owns preparation, complete baseline observation, ordered registration, post-registration audit, reverse-order compensation, immutable failure publication, and idempotency. A separate `GameplayActivationGate` publishes the state through volatile primitive reads. Concrete Harmony, Klei, Unity, disk, and UI work remain behind ports or outside this directory.

**Tech Stack:** C# 8-compatible BCL; `System.Reflection`; immutable defensive copies; MSTest fakes; linked production source from the existing test project.

**Spec:** `docs/specs/2026-08-31-temperature-limit-lifecycle-contained-activation-design.md`, especially sections 6-9, 11-14, 17-18, and 21-23.

## Global Constraints

- Execute after the declared-integration foundation plan is green.
- Production files under `GameplayActivation/Core` may reference only BCL types, the existing pure Harmony contract binding/patch-kind types, and the provider-neutral integration values from the preceding plan.
- Do not reference Klei, Unity, PLib, `HarmonyLib`, `DeliveryTemperatureLimitOptions`, concrete reporters, or concrete presenters.
- Do not add a production reset API. Every test creates fresh gate/coordinator/fake instances.
- Never call a port while holding the coordinator's synchronization lock.
- The primary activation failure is immutable once captured. Compensation and response failures are secondary evidence only.

---

## File and Responsibility Map

| File | Responsibility |
|---|---|
| `GameplayActivationState.cs` | Exact six-state process model |
| `PatchCompensationStatus.cs` | Exact four-way compensation classification |
| `GameplayActivationFailureStage.cs` | Stable activation boundary classification |
| `ActivationSettingsSnapshot.cs` | One immutable settings snapshot |
| `SettingsSnapshotResult.cs` | Available/unavailable result without lazy retry |
| `GameplayPatchRegistrationIdentity.cs` | Exact target, patch method, kind, owner identity |
| `GameplayPatchObservation.cs` | Availability-aware registry observation |
| `GameplayPatchAttemptJournal.cs` | Append-before-call ordered identities |
| `PreparedGameplayActivation.cs` | Immutable settings, selection, outcomes, ordered registrations |
| `GameplayActivationFailureRecord.cs` | Sanitized primary detail, secondary details, failed identity, compensation |
| `GameplayActivationOutcome.cs` | Terminal/idempotent/re-entry request result |
| `IGameplayActivationPreparation.cs` | Complete cold preparation port |
| `IGameplayPatchRegistry.cs` | Register, observe, and remove exact identity port |
| `IGameplayActivationFailureFactory.cs` | Converts exceptions into bounded path-redacted details |
| `IGameplayActivationClock.cs` | Deterministic UTC occurrence time port |
| `GameplayActivationGate.cs` | Volatile read-mostly process state |
| `GameplayActivationCoordinator.cs` | The only state-transition and compensation policy owner |

## Cross-Task Interfaces

```csharp
PreparedGameplayActivation IGameplayActivationPreparation.Prepare();
GameplayPatchObservation IGameplayPatchRegistry.Observe(
    GameplayPatchRegistrationIdentity identity);
void IGameplayPatchRegistry.Register(GameplayPatchRegistrationIdentity identity);
void IGameplayPatchRegistry.Remove(GameplayPatchRegistrationIdentity identity);

GameplayActivationOutcome GameplayActivationCoordinator.TryActivate(
    IGameplayActivationPreparation preparation,
    IGameplayPatchRegistry registry);
GameplayActivationOutcome GameplayActivationCoordinator.RecordPrerequisiteFailure(
    GameplayActivationFailureDetail primaryFailure);
bool GameplayActivationCoordinator.TryGetTerminalOutcome(
    out GameplayActivationOutcome outcome);
```

The coordinator is constructed once with its gate, failure factory, and clock. Preparation and registry instances belong to one attempt; no port is retained after a failed terminal outcome.

The implementation uses this stable activation diagnostic registry and tests it for ordinal uniqueness:

| Boundary | Stable diagnostic ID |
|---|---|
| Framework or PLib prerequisite | `DTL-ACTIVATION-INITIALIZATION-FAILED` |
| Settings capture | `DTL-ACTIVATION-SETTINGS-UNAVAILABLE` |
| Declared integration inspection | `DTL-ACTIVATION-INTEGRATION-INSPECTION-FAILED` |
| Capability selection/authority proof | `DTL-ACTIVATION-AUTHORITY-SELECTION-FAILED` |
| Target/member resolution | `DTL-ACTIVATION-TARGET-RESOLUTION-FAILED` |
| Transpiler preflight | `DTL-ACTIVATION-TRANSPILER-PREFLIGHT-FAILED` |
| Harmony argument binding | `DTL-ACTIVATION-ARGUMENT-BINDING-FAILED` |
| Inactive-route verification | `DTL-ACTIVATION-INACTIVE-CONTRACT-FAILED` |
| Baseline observation | `DTL-ACTIVATION-BASELINE-FAILED` |
| Register call | `DTL-ACTIVATION-REGISTRATION-FAILED` |
| Per-call/final complete audit | `DTL-ACTIVATION-REGISTRATION-AUDIT-FAILED` |
| Re-entry | `DTL-ACTIVATION-REENTRY-DETECTED` |
| Exact-method removal fault | `DTL-ACTIVATION-COMPENSATION-REMOVE-FAILED` |
| Known residual registration | `DTL-ACTIVATION-COMPENSATION-INCOMPLETE` |
| Unavailable final observation | `DTL-ACTIVATION-COMPENSATION-UNVERIFIED` |
| Last-chance loaded-mod lifecycle boundary | `DTL-ACTIVATION-LIFECYCLE-BOUNDARY-FAILED` |

## Task 1: Add the Exact State, Compensation, Settings, and Failure Value Types

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationState.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/PatchCompensationStatus.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationFailureStage.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ActivationSettingsSnapshot.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/SettingsSnapshotResult.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationFailureRecord.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/ActivationValueTypeTests.cs`

- [ ] Write tests for exact enum names/order, the complete stable diagnostic table above and its ordinal uniqueness, available/unavailable settings exclusivity, immutable settings values, bounded diagnostic IDs/messages, defensive-copy secondary failures, and rejection of a failure record without a primary detail.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~ActivationValueTypeTests
```

Expected red: the activation values do not exist.

- [ ] Define the exact enums:

```csharp
internal enum GameplayActivationState
{
    NotStarted,
    Preparing,
    Installing,
    Active,
    Compensating,
    Failed
}

internal enum PatchCompensationStatus
{
    NotRequired,
    VerifiedComplete,
    Incomplete,
    VerificationUnavailable
}
```

- [ ] Capture exactly the four current restart-required option values used by gameplay configuration:

```csharp
internal sealed class ActivationSettingsSnapshot
{
    internal ActivationSettingsSnapshot(
        bool checkTemperatureForStatusItems,
        bool underConstructionLimit,
        int maxConstructionTemperature,
        int minConstructionTemperature)
    {
        CheckTemperatureForStatusItems = checkTemperatureForStatusItems;
        UnderConstructionLimit = underConstructionLimit;
        MaxConstructionTemperature = maxConstructionTemperature;
        MinConstructionTemperature = minConstructionTemperature;
    }

    internal bool CheckTemperatureForStatusItems { get; }
    internal bool UnderConstructionLimit { get; }
    internal int MaxConstructionTemperature { get; }
    internal int MinConstructionTemperature { get; }
}
```

- [ ] Make `SettingsSnapshotResult.Available(snapshot)` and `SettingsSnapshotResult.Unavailable(detail)` mutually exclusive. An unavailable result retains no delegate that could retry option access.

- [ ] Implement `GameplayActivationFailureDetail` as bounded scalar data: stable diagnostic ID, stage, exception type name, sanitized message. Set one explicit maximum for every string and reject path-redaction tokens only in tests of the production factory later; the core does not inspect filesystem state.

- [ ] Make `GameplayActivationFailureRecord` contain UTC occurrence time, terminal `Failed` state, compensation status, primary detail, optional failed registration diagnostic snapshot, attempted/compensated counts, the retained `SettingsSnapshotResult`, selected per-capability authorities and generic integration outcomes when available, and bounded secondary diagnostic details. It must retain no loaded object, mutable Harmony collection, raw path, colony identity, or exception graph.

- [ ] Run the focused tests again.

Expected green: exact states and immutable settings/failure values pass.

## Task 2: Add Exact Registration Identity, Observation, and Append-Before-Call Journal

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayPatchRegistrationIdentity.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayPatchObservation.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayPatchAttemptJournal.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayPatchIdentityAndJournalTests.cs`

- [ ] Test identity equality across target, patch method, kind, and ordinal owner; inequality when any one differs; journal insertion order; reverse enumeration; duplicate exact identity rejection; same target/patch method rejection even when the requested patch kind differs; and immutable snapshots after later updates.

- [ ] Test all observation states: absent, exact only, same patch method under another owner, same patch method under another kind, exact plus any conflicting registration, and unavailable.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayPatchIdentityAndJournalTests
```

Expected red: identity and journal types are absent.

- [ ] Implement the observation enum and safety properties:

```csharp
internal enum GameplayPatchObservationState
{
    Absent,
    ExactRegistrationPresent,
    SamePatchMethodPresentUnderAnotherOwner,
    SamePatchMethodPresentUnderAnotherKind,
    ExactAndConflictingRegistrationPresent,
    Unavailable
}

internal sealed class GameplayPatchObservation
{
    internal GameplayPatchObservationState State { get; }

    internal bool IsProvenAbsent =>
        State == GameplayPatchObservationState.Absent;

    internal bool IsExactPostRegistrationProof =>
        State == GameplayPatchObservationState.ExactRegistrationPresent;

    internal bool AnyMatchingPatchMethodMayRemain =>
        State == GameplayPatchObservationState.ExactRegistrationPresent ||
        State == GameplayPatchObservationState.SamePatchMethodPresentUnderAnotherOwner ||
        State == GameplayPatchObservationState.SamePatchMethodPresentUnderAnotherKind ||
        State == GameplayPatchObservationState.ExactAndConflictingRegistrationPresent;
}
```

- [ ] Make the journal's only append method named `RecordAttemptBeforeRegistration`. Each entry records the ordered binding index, exact identity, proved-absent baseline, whether the register call returned, the post-call observation when available, and bounded diagnostic IDs. Later facts replace the indexed immutable entry with a new immutable value; previously captured journal snapshots never change. The coordinator task must append immediately before the registry port.

- [ ] Run the focused tests again.

Expected green: identity and observation semantics are deterministic.

## Task 3: Define the Pure Ports and Immutable Prepared Activation

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/IGameplayActivationPreparation.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/IGameplayPatchRegistry.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/IGameplayActivationFailureFactory.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/IGameplayActivationClock.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/PreparedGameplayActivation.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationOutcome.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/PreparedGameplayActivationTests.cs`

- [ ] Test nulls, empty registration plans, duplicate registration identities, immutable list copies, unavailable settings rejection, and retention of generic integration outcomes.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~PreparedGameplayActivationTests
```

Expected red: the prepared object and ports are absent.

- [ ] Use these narrow port shapes:

```csharp
internal interface IGameplayActivationPreparation
{
    PreparedGameplayActivation Prepare();
}

internal interface IGameplayPatchRegistry
{
    GameplayPatchObservation Observe(GameplayPatchRegistrationIdentity identity);
    void Register(GameplayPatchRegistrationIdentity identity);
    void Remove(GameplayPatchRegistrationIdentity identity);
}

internal interface IGameplayActivationFailureFactory
{
    GameplayActivationFailureDetail Create(
        string diagnosticId,
        GameplayActivationFailureStage stage,
        Exception exception,
        GameplayPatchRegistrationIdentity? patchIdentity);
}

internal interface IGameplayActivationClock
{
    DateTimeOffset UtcNow { get; }
}
```

- [ ] Construct the coordinator once from the process gate and failure factory, and supply attempt-specific ports only to the activation call:

```csharp
internal GameplayActivationCoordinator(
    GameplayActivationGate gate,
    IGameplayActivationFailureFactory failureFactory,
    IGameplayActivationClock clock)

internal GameplayActivationOutcome TryActivate(
    IGameplayActivationPreparation preparation,
    IGameplayPatchRegistry registry)

internal bool TryGetTerminalOutcome(
    out GameplayActivationOutcome outcome)
```

`IGameplayActivationClock.UtcNow` is read once when the primary failure becomes known. This permits framework failure to be retained before authoritative loaded-mod topology and a concrete Harmony registry exist while keeping occurrence-time tests deterministic.

- [ ] `PreparedGameplayActivation` must contain the available settings snapshot, provider-neutral capability selection, generic integration outcomes, ordered exact registration identities, and immutable runtime plan. It may not contain a `Harmony`, `KMod.Mod`, Unity object, or reporter.

- [ ] `GameplayActivationOutcome` must distinguish `Activated`, `AlreadyActive`, `Failed`, `AlreadyFailed`, and `ReentryRejected`. Only activated/already-active outcomes may expose an active plan.

- [ ] Run the focused tests again.

Expected green: prepared data is immutable and ports are narrow.

## Task 4: Implement Safe Gate Publication and the Successful Activation Path

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationGate.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationCoordinator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayActivationCoordinatorSuccessTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayActivationTestDoubles.cs`

- [ ] Build deterministic fakes that record every call and can return observations per identity. Do not add test branches to production.

- [ ] Test the exact successful trace:

```text
Prepare
ObserveBaseline(0..n-1)
RecordAttempt(0), Register(0), ObserveExact(0)
...
RecordAttempt(n-1), Register(n-1), ObserveExact(n-1)
ObserveCompleteAudit(0..n-1)
PublishPlan
PublishActive
```

- [ ] Assert the gate remains inactive at every fake callback and becomes active only after the final exact observation and plan publication.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayActivationCoordinatorSuccessTests
```

Expected red: no coordinator exists.

- [ ] Implement volatile primitive publication:

```csharp
internal sealed class GameplayActivationGate
{
    private int state = (int)GameplayActivationState.NotStarted;

    internal GameplayActivationState State =>
        (GameplayActivationState)Volatile.Read(ref state);

    internal bool IsActive => State == GameplayActivationState.Active;

    internal void Publish(GameplayActivationState value) =>
        Volatile.Write(ref state, (int)value);
}
```

- [ ] Keep `Publish` inaccessible from patch callbacks; only the coordinator/process owner receives the mutable gate instance. Patch callbacks later receive an `IsActive` read facade.

- [ ] Implement complete baseline proof before the first mutation. Every identity must observe `Absent`; any other state is a preparation failure with `NotRequired`.

- [ ] Implement the registration loop in this exact order:

```csharp
for (int index = 0; index < prepared.Registrations.Count; index++)
{
    GameplayPatchRegistrationIdentity identity = prepared.Registrations[index];
    journal.RecordAttemptBeforeRegistration(index, identity, baseline[index]);
    registry.Register(identity);
    journal.RecordRegistrationReturned(index);
    GameplayPatchObservation observation = registry.Observe(identity);
    journal.RecordPostRegistrationObservation(index, observation);
    if (!observation.IsExactPostRegistrationProof)
    {
        throw new GameplayPatchAuditException(identity, observation.State);
    }

    ThrowIfReentryWasObserved();
}
```

- [ ] After the last per-call observation, observe every planned identity again as one complete audit. Any absent, wrong-owner, wrong-kind, duplicate, or unavailable result starts compensation. Publish the immutable runtime plan first and `Active` last under the coordinator lock only after this audit succeeds.

- [ ] Run the focused tests again.

Expected green: the success trace and publication checkpoints pass.

## Task 5: Handle Pre-Mutation Failures and Terminal Idempotency

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationCoordinator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayActivationCoordinatorPreparationFailureTests.cs`

- [ ] Add tests for preparation throw, settings unavailable, duplicate identity, baseline present under expected owner, baseline present under another owner, baseline observation unavailable, and framework prerequisite failure recorded before `TryActivate`.

- [ ] For each case assert zero `Register`/`Remove` calls, terminal `Failed`, inactive gate, `NotRequired`, one immutable primary failure, and repeated call returns `AlreadyFailed` without port calls.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayActivationCoordinatorPreparationFailureTests
```

Expected red: at least the framework/prerequisite and baseline cases fail.

- [ ] Add `RecordPrerequisiteFailure` that accepts one already-sanitized failure detail only while `NotStarted`, constructs an unavailable settings result using that same stable diagnostic ID because settings were never captured, publishes `Failed`, and returns the retained outcome. A second call must return the original record unchanged.

- [ ] Convert every exception through `IGameplayActivationFailureFactory`; never store the raw exception in the retained record.

- [ ] Run the focused tests again.

Expected green: all pre-mutation failures are terminal, inert, and mutation-free.

## Task 6: Journal Before Registration and Compensate Every Attempted Identity

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationCoordinator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayActivationCoordinatorRegistrationFaultTests.cs`

- [ ] Parameterize over every binding index in a three-binding plan and inject both faults:

  - registry throws before applying binding `n`;
  - registry marks binding `n` present and then throws.

- [ ] Assert binding `n` is journaled in both cases, later bindings are untouched, every journaled identity receives a reverse-order removal attempt, and the primary diagnostic remains the registration failure.

- [ ] Add per-call and final-complete-audit faults: observe throws, returns absent, wrong-owner state, or exact-plus-other-owner state. Assert all enter compensation and never publish active.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayActivationCoordinatorRegistrationFaultTests
```

Expected red: compensation is incomplete or the current journal misses an after-mutation throw.

- [ ] Before the first removal, publish `Compensating` under the synchronization lock so all gate readers are inactive.

- [ ] Compensation must continue after every removal exception:

```csharp
for (int index = journal.Count - 1; index >= 0; index--)
{
    GameplayPatchRegistrationIdentity identity = journal[index];
    try
    {
        registry.Remove(identity);
    }
    catch (Exception exception)
    {
        secondaryFailures.Add(failureFactory.Create(
            "DTL-ACTIVATION-COMPENSATION-REMOVE-FAILED",
            GameplayActivationFailureStage.Compensation,
            exception,
            identity));
    }
}
```

- [ ] Do not throw from `TryActivate`. Return the contained failure outcome after final observation and `Failed` publication.

- [ ] Run the focused tests again.

Expected green: every before/after fault is caught by the pre-call journal and fully iterated compensation.

## Task 7: Classify Final Compensation from Observation, Not Removal Return Values

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationCoordinator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayActivationCoordinatorCompensationTests.cs`

- [ ] Test these exact matrices across multiple identities:

| Removal behavior | Final observations | Expected status |
|---|---|---|
| all return | all absent | `VerifiedComplete` |
| one throws before removal | one exact remains | `Incomplete` |
| one removes then throws | all absent | `VerifiedComplete` |
| all return | one same method under another owner | `Incomplete` |
| all return | one same method under another kind | `Incomplete` |
| all return | one exact plus any conflicting registration | `Incomplete` |
| all return | one unavailable, no known present | `VerificationUnavailable` |
| mixed | one known present and one unavailable | `Incomplete` |

- [ ] Assert every final observation is attempted even after an earlier observation throws. Assert observation faults are retained as secondary details and never replace the primary registration failure.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayActivationCoordinatorCompensationTests
```

Expected red: final classification is not yet complete.

- [ ] Implement this precedence:

```csharp
PatchCompensationStatus status = anyMatchingPatchMethodMayRemain
    ? PatchCompensationStatus.Incomplete
    : anyObservationUnavailable
        ? PatchCompensationStatus.VerificationUnavailable
        : PatchCompensationStatus.VerifiedComplete;
```

- [ ] Publish the final immutable failure record and `Failed` under the same lock. Clear transient mutable references after publication; retain only immutable outcome data.

- [ ] Run the focused tests again.

Expected green: conclusive absence can overrule a post-removal throw, while known residual state always wins over unavailable metadata.

## Task 8: Make Re-entry and Concurrent Reads Deterministic

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/GameplayActivationCoordinator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/GameplayActivationCoordinatorConcurrencyTests.cs`

- [ ] Add fake callbacks that re-enter during preparation, baseline observation, registration, post-registration observation, removal, and final observation. Assert one attempt, inner `ReentryRejected`, outer contained failure, and no parallel registry sequence.

- [ ] Add a worker that reads the gate at barriers for every transition. Assert inactive for `NotStarted`, `Preparing`, `Installing`, `Compensating`, and `Failed`; active only for `Active`.

- [ ] Add repeated-callback tests after `Active` and after `Failed` and assert no duplicate preparation, registration, reporting signal, or warning signal is produced by the core.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayActivationCoordinatorConcurrencyTests
```

Expected red: re-entry is not coherently attached to the original attempt.

- [ ] At entry, use a short lock only to inspect state and mark an in-progress attempt. A caller seeing `Preparing`, `Installing`, or `Compensating` sets `reentryObserved = true` under the same lock and returns `ReentryRejected` immediately.

- [ ] The original attempt checks that flag after every external port call and again in the same critical section that would publish `Active`. Re-entry during compensation is retained as secondary evidence but does not start a second compensation pass.

- [ ] Run the focused tests again.

Expected green: no deadlock, no parallel attempt, and safe publication at every barrier.

## Task 9: Prove a Gate-Aware Additive Endpoint Without Shipping a Mod-Specific Endpoint

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/GateAwareAdditiveInteroperabilityEndpoint.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/GateAwareAdditiveInteroperabilityEndpointTests.cs`

- [ ] Define an internal generic `GateAwareAdditiveInteroperabilityEndpoint<TSource, TPayload, TParsed>` that receives the central gate, stable ID, getter, complete payload parser, one atomic applier, and bounded diagnostic sink.

- [ ] Test every non-`Active` gate state: the getter returns no data, the setter performs no parse/apply, no externally triggered exception escapes, and the stable ID remains available. Drive each transient state through a fresh coordinator/fake barrier rather than exposing a gate setter to production consumers.

- [ ] While active, parse the complete synthetic payload before exactly one apply call. Unknown version, missing key, wrong token kind, invalid range, parser throw, and applier throw must produce no partial application and one bounded diagnostic.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GateAwareAdditiveInteroperabilityEndpointTests
```

Expected red: the reusable endpoint policy does not exist.

- [ ] Implement the endpoint without Harmony, Klei, Unity, Newtonsoft, or a provider name. The setter calls the atomic applier only after the parser returns a complete valid `TParsed`; both getter/setter boundaries catch `Exception`, emit one bounded diagnostic, and return an inert result.

- [ ] Keep the generic endpoint internal and do not add public top-level `Blueprints_GetData`, `Blueprints_SetData`, or `Blueprints_ID` methods in production. The reflection-emitted convention fixture from the preceding plan proves extension mechanics only.

- [ ] Run the focused tests again.

Expected green: generic additive lifecycle, exception containment, and atomic-apply policy work across every gate state without a production compatibility claim.

## Task 10: Enforce the Framework-Independent Boundary

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/LinkedProductionSourceBoundaryContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/NoShimArchitectureContractTests.cs`

- [ ] Add a source boundary over every file beneath `Source/GameplayActivation/Core` rejecting `HarmonyLib`, `KMod`, `UnityEngine`, `PeterHan.PLib`, `DeliveryTemperatureLimitOptions`, `DeliveryTemperatureSupportReporter`, `SupportReportPlayerPresenter`, `Reset`, and preprocessor-based test forks.

- [ ] Add reflection checks that the merged production assembly will expose no public activation-core type or production reset method.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~LinkedProductionSourceBoundaryContractTests|FullyQualifiedName~NoShimArchitectureContractTests"
```

Expected green: the core remains pure, internal, and reset-free.

## Task 11: Milestone Verification and Gated Commit

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~GameplayActivation
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
git diff --check
```

Expected: every activation and complete-suite test passes with zero skipped/inconclusive; no whitespace errors.

- [ ] State `Implementation complete; /review pending` for this milestone and ask the user to invoke built-in `/review` over `Source/GameplayActivation/Core` plus its linked production tests and boundary-contract edits. Resolve or explicitly defer every confirmed P0-P2 finding and rerun the affected focused/full gates.

- [ ] Show `git status --short` and `git diff --stat`. Stage only this plan's intended files after separating user-owned pre-existing edits.

- [ ] If and only if the user explicitly authorizes this exact staged snapshot, load `committing-to-git` and create:

```text
feat(temperature-limit): add all-or-inert activation core

Add a one-attempt state machine with safe gate publication, single settings
capture, append-before-call patch journaling, complete post-registration audit,
and observation-based compensation that preserves the primary failure.
```

- [ ] Do not push.
