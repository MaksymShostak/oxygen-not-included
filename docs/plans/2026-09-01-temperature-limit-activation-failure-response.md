# Temperature Limit Activation Failure Response Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in dependency order. Follow the repository's test-driven-development and formal review gates, and use the checkboxes (`- [ ]`) to track progress.

**Goal:** Turn one contained activation failure into one bounded, path-redacted local standard report and one appropriate three-action warning without performing any external or control-state action until the player explicitly chooses it.

**Architecture:** The activation core retains only sanitized immutable failure values. Support schema version 2 receives availability-aware settings and a generic activation snapshot. A pure one-shot response coordinator chooses verified-containment versus restart-recommended copy and delegates report creation/dialog display through ports. Thin Klei/Unity adapters write locally, show the installed three-action dialog overload, and execute folder/issue/quit actions only from callbacks.

**Tech Stack:** Existing support-report core and writer; Newtonsoft.Json production serialization; MSTest fakes; Klei `KMod.Manager.Dialog`; Unity `Application.OpenURL`/`Application.Quit`; existing bounded GitHub issue URL builder.

**Spec:** `docs/specs/2026-08-31-temperature-limit-lifecycle-contained-activation-design.md`, especially sections 14-16, 20, 23, and 24.

## Global Constraints

- Execute after the pure activation core plan is green.
- Automatic activation-failure reporting always uses standard privacy: it never reads `Player.log`.
- Automatic report creation never copies to the clipboard, opens a folder/browser, exits the game, uploads data, or invokes the manual success presenter.
- The player warning is attempted once per process failure outcome. A dialog failure is ancillary and cannot change the retained activation failure.
- Report/presentation/action faults become bounded secondary diagnostics. They never replace the activation primary failure.
- Preserve the existing explicitly user-invoked standard and extended support-report options unless a schema adaptation is required.
- Direct unit tests compile only production files already linked by the approved core/support globs. Tests of Klei/Unity shells use source, installed metadata, or an exact pipeline-provenance assembly; do not add another project-file link.

---

## File and Responsibility Map

| File | Responsibility |
|---|---|
| `SupportReporting/KleiIntegration/KleiSupportPathRedactorFactory.cs` | Shared known-root redactor construction |
| `SupportReporting/Core/SupportGameplayActivationFailureFactory.cs` | Convert raw caught exception to bounded redacted core detail |
| `SupportReporting/Core/SupportGameplayActivationSnapshot.cs` | Allowlisted activation/report projection |
| `SupportReporting/Core/ActivationFailureReportResult.cs` | Available/unavailable local report result |
| `GameplayActivation/Core/FailureResponse/ActivationFailureResponsePlan.cs` | Exact player copy/action choice |
| `GameplayActivation/Core/FailureResponse/GameplayActivationFailureResponseOutcome.cs` | Immutable report and warning attempt outcomes, separate from the failure record |
| `GameplayActivation/Core/FailureResponse/ActivationFailureResponseCoordinator.cs` | Report once, warn once, no automatic actions |
| `GameplayActivation/KleiIntegration/KleiActivationFailureReportWriter.cs` | Standard report snapshot plus atomic local write |
| `GameplayActivation/KleiIntegration/KleiActivationFailurePresenter.cs` | Three-action Klei dialog and user callbacks |

Response diagnostics use these unique stable IDs: `DTL-ACTIVATION-REPORT-FAILED`, `DTL-ACTIVATION-DIALOG-FAILED`, `DTL-ACTIVATION-REPORT-FOLDER-ACTION-FAILED`, `DTL-ACTIVATION-ISSUE-ACTION-FAILED`, and `DTL-ACTIVATION-QUIT-ACTION-FAILED`.

## Cross-Task Interfaces

```csharp
ActivationFailureReportResult IActivationFailureReportWriter.TryWrite(
    GameplayActivationFailureRecord failureRecord);
void IActivationFailurePresenter.Present(ActivationFailureResponsePlan plan);
GameplayActivationFailureResponseOutcome ActivationFailureResponseCoordinator.TryRespond(
    GameplayActivationFailureRecord failureRecord);
```

The response coordinator calls the writer at most once, builds one response plan from the returned availability result and compensation status, then calls the presenter at most once. Neither response result is written back into the immutable failure record.

## Task 1: Produce Bounded Path-Redacted Failure Details at the Catch Boundary

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/KleiSupportPathRedactorFactory.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/KleiSupportReportSnapshotReader.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportGameplayActivationFailureFactory.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportGameplayActivationFailureFactoryTests.cs`

- [ ] Extract the existing known-root redactor construction from `KleiSupportReportSnapshotReader` without changing its current diagnostic sanitation behavior.

- [ ] Test Windows and Unix path shapes in primary messages, nested inner-exception messages, mod directory, persistent-data directory, user profile, and an unrelated non-path message. Assert type names remain, paths are replaced with stable tokens, stack traces are absent, and every retained string meets the core bound.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~SupportGameplayActivationFailureFactoryTests
```

Expected red: the production failure factory does not exist.

- [ ] Implement `IGameplayActivationFailureFactory.Create` using the shared redactor and the core bounds:

```csharp
public GameplayActivationFailureDetail Create(
    string diagnosticId,
    GameplayActivationFailureStage stage,
    Exception exception,
    GameplayPatchRegistrationIdentity? patchIdentity)
{
    if (exception == null)
    {
        throw new ArgumentNullException(nameof(exception));
    }

    RedactedSupportText redactedMessage = pathRedactor.Redact(
        SupportDiagnosticBuffer.BoundMessageForReport(exception.Message));
    return new GameplayActivationFailureDetail(
        diagnosticId,
        stage,
        exception.GetType().FullName ?? exception.GetType().Name,
        redactedMessage.Content,
        patchIdentity?.CreateDiagnosticSnapshot());
}
```

- [ ] Do not put the raw `Exception` or `StackTrace` into `GameplayActivationFailureDetail`.

- [ ] Run the focused tests and existing `SupportPathRedactorTests`.

Expected green: retained failures are bounded and path-redacted without regressing manual report sanitation.

## Task 2: Add Availability-Aware Settings and Activation State to Schema Version 2

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportGameplayActivationSnapshot.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportDocument.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/KleiSupportReportSnapshotReader.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportDocumentTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportGameplayActivationSnapshotTests.cs`

- [ ] Add JSON/document tests for available settings, unavailable settings, every activation state, every compensation status, no failed binding, one failed binding, multiple secondary details, deterministic order, and bounded strings.

- [ ] Assert the reader consumes an already captured `SettingsSnapshotResult`; no report-generation path may access `DeliveryTemperatureLimitOptions.Instance` or `GameUtil.temperatureUnit`.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~SupportReportDocumentTests|FullyQualifiedName~SupportGameplayActivationSnapshotTests"
```

Expected red: the document still assumes settings are always available and has no activation projection.

- [ ] Model settings availability explicitly:

```csharp
internal sealed class SupportActivationSettingsSnapshot
{
    internal string State { get; }
    internal string? UnavailableReason { get; }
    internal bool? CheckTemperatureForStatusItems { get; }
    internal bool? UnderConstructionLimit { get; }
    internal int? MaxConstructionTemperature { get; }
    internal int? MinConstructionTemperature { get; }
}
```

- [ ] Keep the current UI display temperature unit as a separate availability-aware `temperatureLimit.displayTemperatureUnit` support fact. It is environment/display context, not one of the activation option values.

- [ ] Model activation with allowlisted values only:

```csharp
internal sealed class SupportGameplayActivationSnapshot
{
    internal string State { get; }
    internal string? CompensationStatus { get; }
    internal string? DiagnosticId { get; }
    internal string? FailureStage { get; }
    internal SupportPatchRegistrationIdentitySnapshot? FailedBinding { get; }
    internal SupportActivationFailureDetailSnapshot? PrimaryFailure { get; }
    internal IReadOnlyList<SupportActivationFailureDetailSnapshot> SecondaryFailures { get; }
}
```

- [ ] Replace the current independent options access in `KleiSupportReportSnapshotReader.CreateDocument` with the supplied settings result. Manual reports use the one process activation snapshot; if activation has not captured settings, publish an explicit unavailable fact rather than forcing the lazy.

- [ ] Run the focused tests again.

Expected green: both success and failure reports represent settings/activation without another options read.

## Task 3: Add a Separate Automatic Local Report Path

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/ActivationFailureReportResult.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/AtomicSupportReportFileStore.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/FailureResponse/IActivationFailureReportWriter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/KleiIntegration/KleiActivationFailureReportWriter.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/DeliveryTemperatureSupportReporter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/AtomicSupportReportFileStoreTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/KleiIntegration/KleiActivationFailureReportWriterSourceContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportingSourceBoundaryTests.cs`

- [ ] Test `AtomicSupportReportFileStore` with a successful write, serializer callback throw, unwritable/invalid directory, existing final path, and final move failure. Assert no owned temporary file remains after any failure. Source-contract the Klei writer to exactly one local-store call, one attempt result, no extended-report kind, no clipboard/folder/browser/dialog/quit call, and no manual presenter call.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~AtomicSupportReportFileStoreTests|FullyQualifiedName~KleiActivationFailureReportWriterSourceContractTests|FullyQualifiedName~SupportReportingSourceBoundaryTests"
```

Expected red: automatic activation failure currently has no separate report result.

- [ ] Reuse `SupportReportKind.Standard` exactly; do not add a third report kind. Keep player-log inclusion limited to the explicit manual extended flow:

```csharp
private static bool IncludesPlayerLog(SupportReportKind kind) =>
    kind == SupportReportKind.ExtendedPlayerLog;
```

The optional schema-v2 `activationFailure` object, not a new `reportKind`, distinguishes the automatic artifact.

- [ ] Return data instead of presenting it:

```csharp
internal sealed class ActivationFailureReportResult
{
    internal ActivationFailureReportState State { get; }
    internal string? ReportId { get; }
    internal string? FinalPath { get; }
    internal SupportIssueUrl IssueUrl { get; }
    internal string? FailureDiagnosticId { get; }
}
```

`ActivationFailureReportState` has exactly `Available` and `Unavailable`; an available result requires nonblank report ID/final path, while an unavailable result requires its stable failure diagnostic ID and never invents a path.

- [ ] `KleiActivationFailureReportWriter.TryWrite` must catch its own report failures, record a bounded ancillary diagnostic, and return an unavailable result containing a safe issue URL. It must not call `SupportReportPlayerPresenter.PresentSuccess` or `PresentFailure`.

- [ ] Refactor `SupportReportJsonFileWriter` to delegate only the rooted-directory-independent create-new/write/flush/atomic-move/owned-temp cleanup mechanics to `AtomicSupportReportFileStore`; keep Unity path resolution and Newtonsoft serialization in the Klei integration layer.

- [ ] Keep the existing manual report actions user-triggered and behaviorally separate.

- [ ] Run the focused tests again.

Expected green: automatic creation is local-only and returns a complete availability result.

## Task 4: Implement Pure Response Selection and One-Shot Coordination

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/FailureResponse/ActivationFailureResponsePlan.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/FailureResponse/GameplayActivationFailureResponseOutcome.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/FailureResponse/IActivationFailurePresenter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/FailureResponse/ActivationFailureResponseCoordinator.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/FailureResponse/ActivationFailureResponseCoordinatorTests.cs`

- [ ] Test `NotRequired` and `VerifiedComplete` select `Continue Without Limits`; `Incomplete` and `VerificationUnavailable` select `Exit Game` plus restart recommendation. Assert the five response diagnostic IDs above are ordinally unique from every activation-core ID.

- [ ] Test report-available and report-unavailable copy, stable diagnostic ID inclusion, report ID or unavailable marker, exact three action labels, writer throw containment, presenter throw containment, repeated response call, and concurrent response calls. Assert the immutable `GameplayActivationFailureResponseOutcome` records report availability and warning attempted/displayed/unavailable facts without mutating or being inserted into `GameplayActivationFailureRecord`.

- [ ] Assert fake clipboard/folder/browser/quit actions remain at zero during response coordination.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~ActivationFailureResponseCoordinatorTests
```

Expected red: response selection and one-shot behavior do not exist.

- [ ] Implement the verified-containment copy exactly around these meaning-bearing strings:

```text
Title: Temperature Limit couldn't activate
Consequence: Temperature limits are not enforced for this launch.
Primary: Continue Without Limits
Secondary: Open Report Folder
Third: Report Issue
```

- [ ] Implement the unverified/incomplete copy exactly around:

```text
Title: Temperature Limit couldn't activate
Consequence: Temperature limits are not enforced, and Temperature Limit could not verify that all of its Harmony registrations were removed.
Recommendation: Restart ONI before loading a colony.
Primary: Exit Game
Secondary: Open Report Folder
Third: Report Issue
```

- [ ] Make the continue callback a true no-op. The pure coordinator calls the report writer once, derives one plan, attempts the presenter once, and marks response attempted even if either ancillary port throws.

- [ ] Run the focused tests again.

Expected green: copy/action selection is precise and one-shot.

## Task 5: Wire the Installed Klei Three-Action Dialog Without Automatic Actions

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/KleiIntegration/KleiActivationFailurePresenter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/KleiIntegration/KleiActivationFailurePlayerActions.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/KleiActivationFailurePresentationContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/KleiIntegration/KleiActivationFailurePresenterSourceContractTests.cs`

- [ ] Add an installed-assembly reflection test that selects exactly one `KMod.Manager.Dialog` overload with title/body plus three string/action pairs. Assert every callback parameter accepts `System.Action` and the method returns `void`.

- [ ] Add a source/IL test asserting presentation contains one dialog call, no action invocation before that call, no clipboard reference, and exactly one `Application.Quit` call site behind the primary callback for restart-recommended plans.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~KleiActivationFailurePresentationContractTests|FullyQualifiedName~KleiActivationFailurePresenterSourceContractTests"
```

Expected red: the concrete presenter is absent.

- [ ] Call the verified installed overload with all text/action pairs:

```csharp
KMod.Manager.Dialog(
    null,
    plan.Title,
    plan.Body,
    plan.PrimaryActionLabel,
    () => TryUserAction(plan.PrimaryAction),
    plan.SecondaryActionLabel,
    () => TryUserAction(plan.SecondaryAction),
    plan.ThirdActionLabel,
    () => TryUserAction(plan.ThirdAction));
```

- [ ] Map actions only inside callbacks: no-op continue; `Application.Quit()` for exit; `Application.OpenURL` with the known report-directory URI for folder; `Application.OpenURL` with the bounded GitHub issue URL for issue.

- [ ] Catch every action exception, record a bounded ancillary diagnostic, and return. Do not redisplay the warning or alter activation state.

- [ ] Run the focused tests again.

Expected green: metadata and source boundaries prove the three explicit user actions.

## Task 6: Cover Privacy, Bounds, and Failure-Message Acceptance

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportDocumentTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportIssueUrlBuilderTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportJsonReportSizeLimiterTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/Core/FailureResponse/ActivationFailureMessageAcceptanceTests.cs`

- [ ] Add maximum-count/message/total-JSON tests for activation primary/secondary failures and external-integration outcomes. Prove deterministic truncation retains the primary failure, compensation status, stable diagnostic ID, and report availability.

- [ ] Add path-shaped sentinel strings and assert they are absent from serialized JSON, issue URLs, and player copy.

- [ ] Assert player copy names Temperature Limit, says it could not activate, says limits are not enforced for this launch, distinguishes compensation uncertainty, never says uploaded, and discloses report creation failure.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~SupportReportDocumentTests|FullyQualifiedName~SupportIssueUrlBuilderTests|FullyQualifiedName~SupportJsonReportSizeLimiterTests|FullyQualifiedName~ActivationFailureMessageAcceptanceTests"
```

Expected green: privacy allowlists, bounds, and player semantics all pass.

## Task 7: Milestone Verification and Gated Commit

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~SupportReporting|FullyQualifiedName~ActivationFailure"
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
git diff --check
```

Expected: all focused and complete tests pass with zero skipped/inconclusive; no whitespace errors.

- [ ] Confirm automatic response tests observed zero clipboard/folder/browser/quit calls before simulated user callbacks.

- [ ] State `Implementation complete; /review pending` for this milestone and ask the user to invoke built-in `/review` over the activation-failure support schema, atomic local store, response core, Klei/Unity shells, and their tests. Resolve or explicitly defer every confirmed P0-P2 finding and rerun the affected focused/full gates.

- [ ] Show `git status --short` and `git diff --stat`. Stage only this plan's intended files after separating user-owned pre-existing edits.

- [ ] If and only if the user explicitly authorizes this exact staged snapshot, load `committing-to-git` and create:

```text
feat(temperature-limit): add contained activation failure response

Project bounded activation evidence into schema-v2 local reports and show one
compensation-aware warning whose folder, issue, and exit actions run only after
the player explicitly selects them.
```

- [ ] Do not push.
