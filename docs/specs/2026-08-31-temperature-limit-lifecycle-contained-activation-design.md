# Delivery Temperature Limit: Lifecycle-Contained Activation Design

- **Status:** Revised written specification pending user review
- **Date:** 2026-08-31
- **Mod:** Delivery Temperature Limit (Supercooled)
- **Primary user:** An ONI player whose mod set or current game build is incompatible with Temperature Limit
- **Primary reliability objective:** A Temperature Limit activation failure must not terminate ONI's mod-loading lifecycle
- **Gameplay invariant:** Temperature Limit is either completely active or behaviorally inert
- **Recovery model:** Patch-registration compensation with explicit verification, never an atomicity claim
- **Diagnostic model:** One bounded, path-redacted local report attempt and one player warning attempt
- **Development model:** Source-grounded contracts, deterministic fault injection, focused TDD, the repository-local ONI Mod Pipeline, and the formal `/review` gate

## 1. Decision

Replace the current two-phase gameplay patch installation with one lifecycle-contained, process-lifetime activation attempt in `OnAllModsLoaded`.

`OnLoad` performs only contained framework, localization, options, and diagnostic initialization. It applies no gameplay Harmony patch. `OnAllModsLoaded` captures the active mod topology, evaluates a compile-time catalog of explicitly declared external-mod integrations, captures the settings used for activation, selects exactly one implementation for each exclusive runtime capability, resolves every target, verifies every transpiler and Harmony argument binding, and constructs the complete immutable patch set before the first Harmony mutation.

The activation attempt publishes `Active` only after every planned registration has been applied and observed. If any activation-critical operation fails, the mod makes every provisional callback inert, compensates registrations attributable to that attempt, verifies the result as far as Harmony permits, records an immutable failure, and returns normally from the Klei lifecycle callback. A failed activation is not retried during the current ONI process.

The user is told explicitly that Temperature Limit did not activate and that its limits are not being enforced. The response differs according to whether compensation was verified:

1. `NotRequired` or `VerifiedComplete` permits **Continue Without Limits**, **Open Report Folder**, or **Report Issue**.
2. `Incomplete` or `VerificationUnavailable` does not claim graceful degradation. It recommends restarting before loading a colony and offers **Exit Game**, **Open Report Folder**, or **Report Issue**.

No activation failure changes a Temperature Limit setting, Klei's mod-enabled state, or colony-save data. The only intentional persistent artifact is the local diagnostic report.

## 2. Why the current behavior is unacceptable

The current implementation applies seven gameplay patches during `OnLoad`, applies the topology-dependent set during `OnAllModsLoaded`, and catches installation exceptions only to record and rethrow them. Inspection of the installed ONI assembly established that Klei's surrounding mod-load path does not provide a containment boundary that makes this rethrow harmless. An incompatible target or Harmony binding can therefore escape the mod lifecycle and terminate game startup.

The current installation helper also records a registration only after `Harmony.Patch` returns. If Harmony mutates its registration state and then throws, that registration is absent from the removal list. If exact removal itself throws, the secondary exception can replace the original installation exception. Finally, the existing state and comments describe the behavior as a transaction with exact rollback even though Harmony mutation is not an ACID transaction and its compensating operation can fail.

This design addresses those failures together. Catching the final exception without preventing partial behavior would merely trade a startup crash for an untrustworthy half-active mod.

## 3. Source-grounded terminology

### 3.1 Lifecycle-contained activation failure

A **lifecycle-contained activation failure** is a failure during Temperature Limit's initialization or gameplay activation for which:

- a managed exception caught by the mod's Klei lifecycle boundary is not deliberately rethrown;
- the gameplay activation state becomes terminal `Failed` for the current process;
- the all-or-inert gameplay invariant remains in force;
- patch-registration compensation is attempted when Harmony mutation may have begun; and
- ancillary diagnostics and player notification are attempted without becoming new activation-critical failures.

This is a scoped guarantee. It does not claim that the mod can recover from conditions that prevent managed exception handling or prevent the lifecycle handler itself from executing, such as process termination or unrecoverable runtime failure.

### 3.2 Activation-critical operation

An **activation-critical operation** is an operation whose success is required before Temperature Limit may enforce gameplay behavior. These operations are:

- required base lifecycle initialization;
- PLib initialization, localization registration, and options registration;
- active loaded-mod topology validation;
- one-time activation settings capture;
- declared external-mod identity inspection needed to establish ownership of an exclusive runtime capability;
- exclusive runtime-authority selection for every required capability;
- verification of every selected exclusive runtime integration contract;
- target and member resolution;
- selected-owner verification;
- transpiler preflight;
- Harmony argument-binding verification;
- patch registration and post-registration observation; and
- publication of the immutable runtime plan and `Active` state.

Failure of any activation-critical operation prevents activation.

### 3.3 Ancillary operation

An **ancillary operation** improves diagnosis or presentation but is not required to enforce temperature limits correctly. These operations include:

- initialization of the support reporter;
- retention and mirroring of diagnostic events;
- publication of a sanitized loaded-mod support snapshot;
- inspection and availability reporting for additive interoperability protocols;
- local diagnostic-report generation;
- display of the player-facing warning;
- opening the report directory;
- opening the GitHub issue form; and
- executing the user-selected quit action.

Each ancillary operation has its own containment boundary. Its failure is recorded where possible and must not escape through `OnLoad`, `OnAllModsLoaded`, or a player action.

### 3.4 Patch-registration compensation

**Patch-registration compensation** is the explicit attempt to remove registrations attributable to a failed activation attempt. It is not called rollback, is not described as atomic, and does not imply that Harmony or the process has returned to its exact earlier state.

Microsoft's compensating-transaction guidance is relevant to the distinction: compensating work is application-specific, needs sufficient progress information, and can itself fail. Harmony likewise exposes patch metadata and exact-patch removal but does not provide a transactional installation primitive.

### 3.5 Graceful degradation

The player-facing phrase **graceful degradation** is permitted only when compensation is `NotRequired` or `VerifiedComplete` and the gameplay gate is inactive. It is not used for `Incomplete` or `VerificationUnavailable`, because those outcomes lack verified containment of the registration state.

### 3.6 Stable diagnostic identifier and player-facing summary

A **stable diagnostic identifier** is a bounded, nonlocalized code suitable for reports, issue search, and support communication. A **player-facing summary** is localized or readable prose that explains the consequence and next action without exposing raw stack traces, local paths, or internal implementation detail.

### 3.7 Declared external-mod integration

A **declared external-mod integration** is a compile-time Temperature Limit declaration for one identified third-party mod and one or more narrow capabilities. The declaration includes stable identity evidence, compatibility contracts, failure policy, diagnostics, fixtures, and acceptance tests. An installed but undeclared mod remains an ordinary co-resident mod; Temperature Limit makes no compatibility claim about it.

Two integration categories are intentionally distinct:

- An **exclusive runtime-authority integration** exists when another mod replaces or suppresses an ONI execution seam that Temperature Limit must intercept. Exactly one implementation may own each affected semantic capability. FastTrack is the first declared integration of this category.
- An **additive interoperability integration** exchanges optional data or behavior through an explicit protocol without taking ownership of Temperature Limit's core delivery semantics. A future Blueprints Expanded settings-transfer adapter would be in this category.

An additive integration that cannot be verified is described as **capability unavailable**. It is not described as graceful degradation and, by itself, does not make Temperature Limit's core enforcement inactive. An exclusive integration that actively owns a required runtime seam but cannot be verified is activation-critical because selecting the Klei implementation would not restore authority over the suppressed seam.

## 4. Goals

The implementation must:

1. Prevent a caught Temperature Limit initialization or activation exception from being deliberately rethrown through either Klei lifecycle callback.
2. Apply no gameplay Harmony patch during `OnLoad`.
3. Verify the complete selected per-capability runtime patch set, including every declared external runtime-authority contract and Harmony argument binding, before the first registration.
4. Make provisional and residual patch behavior inert until and unless the complete activation becomes `Active`.
5. Detect a registration that may have occurred even when `Harmony.Patch` throws.
6. Compensate only registrations unambiguously attributable to the current activation attempt.
7. Preserve the primary activation exception when compensation or diagnostics also fail.
8. Classify compensation honestly as `NotRequired`, `VerifiedComplete`, `Incomplete`, or `VerificationUnavailable`.
9. Avoid retries against a process whose activation has failed.
10. Create one best-effort local diagnostic report without requiring the player to find versions, mod topology, settings, or paths manually.
11. Tell the player unambiguously that limits are not enforced for the launch.
12. Make folder, issue, and quit actions user-initiated.
13. Test every activation and compensation boundary through deterministic fault injection.
14. Exercise the Klei baseline, every declared exclusive runtime integration, and representative additive integration fixtures against current real contracts.
15. Permit a later mod that implements an existing capability to be added through a declaration, a narrow adapter, fixtures, tests, and report projection without adding provider-specific branches to the activation coordinator or changing the report schema.
16. Preserve the current production target, package closure, pipeline profile, mod metadata, and Workshop metadata.

## 5. Non-goals

This implementation will not:

- silently choose a fallback implementation after an active owner of an exclusive capability fails verification;
- apply a subset of the verified gameplay plan;
- retry activation later in the same ONI process;
- unpatch another Harmony owner's methods;
- use `UnpatchAll`, owner-wide removal, or `HarmonyPatchType.All`;
- claim transactional atomicity or restoration of the complete process state;
- classify failures through a broad `IsFatalException` helper;
- automatically upload a report, open a browser, open a folder, copy to the clipboard, or read `Player.log` after activation failure;
- disable the mod persistently or modify a colony save;
- add a shipped fault-injection switch, environment variable, option, or debug command;
- discover arbitrary third-party mods or Temperature Limit integration adapters by runtime naming convention;
- introduce one broad `IModCompatibilityProvider` interface that forces runtime patch replacement and additive protocols into the same lifecycle or failure policy;
- add a compile-time reference to a third-party mod assembly;
- claim Blueprints Expanded support before its identity, protocol, payload, gate behavior, fixtures, and two-mod smoke test have passed;
- add a package, new test project, pipeline-profile setting, or CI workflow;
- change `oni-mod-pipeline.toml`, a package lockfile, `mod.yaml`, `mod_info.yaml`, or Workshop metadata; or
- create a commit, push, release, or GitHub issue without the separately required authorization.

## 6. Process-lifetime state model

### 6.1 Gameplay activation state

The exact enum is:

```text
GameplayActivationState
  NotStarted
  Preparing
  Installing
  Active
  Compensating
  Failed
```

The allowed transition graph is:

```text
NotStarted ──framework/prerequisite failure──────────────▶ Failed
     │
     └──activation attempt──▶ Preparing
                                  │
                                  ├──preparation failure──▶ Failed
                                  │
                                  └──complete verification──▶ Installing
                                                                  │
                                                                  ├──complete registration audit──▶ Active
                                                                  │
                                                                  └──installation uncertainty/failure──▶ Compensating──▶ Failed
```

`Failed` is terminal until the current ONI process exits. A repeated call in `Active` is an idempotent success. A repeated call in `Failed` returns the existing failure outcome without preparation, Harmony mutation, reporting, or a second warning. Re-entry while `Preparing`, `Installing`, or `Compensating` is an activation-critical failure of the original attempt; it never starts a parallel attempt.

Successful framework initialization leaves the state at `NotStarted`; it establishes a separate prerequisite-complete fact. This avoids describing the mod as preparing gameplay patches before loaded-mod topology exists.

### 6.2 Publication and concurrency

State transitions and activation-owned references are coordinated under one synchronization boundary. The read-mostly gameplay gate uses a safe publication mechanism such as `Volatile.Read`/`Volatile.Write` on a primitive state representation.

The transition to `Active` is the final publication step, after:

- the immutable runtime patch plan is assigned;
- the Harmony owner and patch registry are assigned;
- every registration has passed the complete post-install audit; and
- any game-load authority data needed by callbacks is initialized.

The transition from `Installing` to `Compensating` occurs before the first removal attempt. Background callbacks, including callbacks reached through a declared runtime-authority integration such as FastTrack, therefore observe inactive behavior throughout compensation even when a registration remains physically present.

The production process-lifetime owner has no reset API. Tests exercise fresh coordinator instances rather than adding a production reset seam.

## 7. The all-or-inert gameplay invariant

The governing invariant is:

> While `GameplayActivationState != Active`, every provisional or residual Temperature Limit gameplay patch is behaviorally neutral.

This is stronger than requiring compensation to succeed. It protects the player while compensation is underway and when exact registration state cannot be verified.

The invariant is implemented through a small process-lifetime gameplay gate plus existing game-session ownership:

1. The central game-session host cannot create or expose a Temperature Limit session unless the activation gate is `Active`.
2. `TryStartAuthorizedGameSession` returns `false` without throwing whenever activation is not `Active`.
3. Patch callbacks that can mutate prefabs, UI, lifecycle state, or other data before consulting the session host receive an explicit inactive-state guard.
4. Prefixes with `__state` publish their existing inactive/default state and return without changing target behavior.
5. Postfixes treat an inactive/default `__state` as a no-op.
6. Helpers invoked by transpiled IL check the gate or obtain an activation-authorized game session before changing eligibility, grouping, publication, or status behavior.
7. A binding inventory test assigns every patch entry point one reviewed inactive-behavior route. Adding an unclassified binding fails the test.

The transpiler method itself runs during registration and may throw during preflight or IL generation. The invariant concerns the runtime behavior emitted into the target method; tests must therefore inspect the injected helper route, not merely look for a gate in the transpiler method.

## 8. Lifecycle integration

### 8.1 `OnLoad`

`OnLoad` performs no gameplay Harmony mutation. Its activation-critical sequence is:

1. call `base.OnLoad(harmony)`;
2. initialize the support reporter as an ancillary operation;
3. initialize PLib;
4. register localization; and
5. register the PLib options type.

The whole override has a last-chance managed exception boundary. A framework or registration exception records a pre-activation failure with compensation `NotRequired`, transitions the process activation owner to `Failed`, and returns normally. The response may be deferred because the mod-management UI may not yet be able to display a warning reliably.

The ancillary reporter initializer keeps its own existing containment boundary. Its failure does not prevent gameplay activation, but it may make the later automatic report unavailable.

### 8.2 `OnAllModsLoaded`

`OnAllModsLoaded` performs this ordered sequence inside its last-chance boundary:

1. call `base.OnAllModsLoaded(harmony, loadedMods)`;
2. publish the sanitized loaded-mod support snapshot as an ancillary operation;
3. if initialization previously failed, skip activation and present the retained failure once;
4. otherwise perform the one activation attempt using the authoritative `loadedMods` argument;
5. if activation fails, attempt the automatic local report once; and
6. attempt the appropriate player warning once.

The sanitized support snapshot is never substituted for the authoritative topology input. Failure to publish that ancillary snapshot does not erase the actual loaded-mod list or force activation failure.

The lifecycle boundary does not contain `throw;`, does not wrap and throw another exception, and does not call a helper whose contract is to rethrow. Its final fallback makes a best-effort state transition and log entry, then returns.

### 8.3 Scope of the containment promise

The implementation catches managed `Exception` at the lifecycle boundary because the requirement is to avoid deliberately propagating failures already delivered to that boundary. It does not maintain a speculative catch-filter taxonomy. If an exception is caught, it is not deliberately rethrown. Conditions that prevent the catch or its handler from running remain outside the promise.

## 9. One late preparation phase

All seven currently topology-independent patches move from `OnLoad` into the late activation set:

1. temperature-limited delivery-target prefab configuration;
2. material-selection panel prefab initialization;
3. material-selection panel configuration;
4. building-definition instantiation;
5. building-definition post-processing;
6. details-screen prefab initialization; and
7. Complex Fabricator side-screen show handling.

The late preparation phase then adds the immutable ordered runtime groups selected by `DeliveryTemperatureRuntimePatchPlan`:

- game-session lifecycle;
- world-parent topology;
- authoritative fetch-temperature eligibility;
- the selected world-inventory temperature-publication authority;
- optional temperature-status availability;
- the selected pickup-temperature-grouping authority; and
- the selected direct-delivery-eligibility authority.

Klei supplies the built-in baseline implementation. A declared external integration may replace one or more capability implementations only through the provider-neutral selection described in section 10; the runtime plan does not branch on a mod name.

Preparation performs, in order:

1. validate the Harmony owner;
2. validate the loaded-mod argument;
3. construct one short-lived `LoadedModInspectionContext` from the authoritative loaded-mod list, relevant loaded assemblies, and copied active Harmony prefix descriptors;
4. evaluate every entry in the compile-time `DeclaredModIntegrationCatalog` against that context, containing each additive inspector failure as an unavailable capability outcome;
5. let each matched adapter inspect its exact active-mod, assembly, file, owner, member, signature, protocol, and IL contracts as applicable;
6. select exactly one implementation for each exclusive runtime capability and validate every declared atomic capability bundle;
7. retain additive interoperability outcomes without allowing them to contribute to the gameplay Harmony transaction;
8. capture `SettingsSnapshotResult` once;
9. create the immutable patch plan from the selected runtime contributions and available settings snapshot;
10. verify selected authority;
11. resolve every target and patch method;
12. preflight every transpiler;
13. verify every Harmony argument binding through `HarmonyPatchContractBindingVerifier`;
14. verify patch identity uniqueness needed by exact compensation;
15. verify that every binding has an inactive-behavior contract;
16. observe one complete pre-mutation baseline and prove that every planned target/patch-method pair is absent under every owner; and
17. produce one immutable `PreparedGameplayActivation` containing the runtime selection and generic integration outcomes.

No Harmony mutation is permitted before all seventeen operations succeed. The existing uncommitted Harmony argument-binding verifier is retained and becomes part of this complete preparation gate rather than being replaced. The inspection context is never serialized or retained in the failure record; only bounded, sanitized facts are projected into support reporting.

## 10. Declared external-mod integration architecture

### 10.1 Categories and failure policy

The shared architecture distinguishes semantic ownership instead of treating every compatible mod as a FastTrack-shaped patch replacement:

| Category | Meaning | Activation consequence | First concrete case |
|---|---|---|---|
| Built-in baseline | Temperature Limit patches the authoritative Klei seam | Selected when no declared external integration owns that capability | Klei delivery and inventory methods |
| Exclusive runtime authority | An active external mod replaces or suppresses an ONI seam | A required owned capability must verify and select exactly one implementation; incompatibility or ambiguous ownership blocks activation | FastTrack |
| Additive interoperability | An external mod exchanges optional data through a bounded protocol without owning core delivery semantics | Failure is isolated and reported as capability unavailable; core enforcement may still become active | A future Blueprints Expanded settings-transfer integration |

Capability criticality is owned by Temperature Limit's capability definition, not chosen by a third-party adapter. A required delivery-correctness capability cannot be downgraded to optional by its integration. An optional capability can be omitted only through an `Unavailable` `RuntimeCapabilitySelectionEntry` carrying a validated stable diagnostic identifier and bounded message. That generic selection outcome exists even when no external integration is present; the selector does not fabricate an external-mod outcome to explain an ownerless omission.

### 10.2 Provider-neutral core contracts

The reusable core consists of small immutable types rather than a dynamic plugin framework:

- `DeclaredModIntegrationCatalog` is the compile-time ordered list of integrations Temperature Limit intentionally recognizes. Its order makes inspection and reporting deterministic but grants no selection priority. Adding an integration requires an explicit catalog declaration; arbitrary assemblies are not discovered as Temperature Limit providers.
- `DeclaredModIntegrationDescriptor` contains a stable integration ID, display name, exact mod-identity contract, upstream evidence reference, and immutable `DeclaredModIntegrationCapability` values. Each value assigns one capability to exactly one inspection category; the descriptor derives its ordered category and capability projections from those assignments, so a capability cannot ambiguously cross the runtime-authority and additive boundaries. It contains no Harmony or third-party mod object.
- `LoadedModInspectionContext` is a short-lived preparation-layer input built from the authoritative Klei callback data. It may carry reflection identities and copied Harmony ownership descriptors, but it is not part of the pure core, support schema, or retained failure record.
- `RuntimeCapabilityId` is a validated stable identifier value, not a mod-specific enum. Temperature Limit owns constants such as `world-inventory-temperature-publication`, `pickup-temperature-grouping`, and `direct-delivery-eligibility`.
- `RuntimeAuthorityImplementationIdentity` distinguishes the built-in Klei baseline from a declared external integration structurally. Equality includes that origin discriminator, so a colliding textual integration ID cannot make a mixed atomic bundle appear coherent.
- `RuntimeCapabilityDefinition` states whether the capability is required for core enforcement, supplies the Klei baseline contribution when one exists, and may name an atomic bundle whose members must be selected coherently. A baseline contribution must identify the built-in Klei implementation and may contain only `KleiOriginal` authority requirements.
- `PreparedRuntimeAuthorityContribution` contains one implementation identity, one capability ID, its authority observation, immutable patch bindings or patch groups when compatible, permitted owner evidence, and bounded diagnostics.
- `RuntimeCapabilitySelectionEntry` is constructed either from one compatible capability-matching contribution or as an explicitly diagnosed optional omission. Its factories prevent selected, omitted, and diagnostic state from contradicting one another.
- `RuntimePatchCapabilitySelection` is the immutable selected map consumed by `DeliveryTemperatureRuntimePatchPlan`.
- `ExternalModIntegrationOutcome` is the provider-neutral, sanitized projection retained for diagnostics and reporting. Every capability outcome retains its exact `ExternalModIntegrationCategory`, so later runtime selection can preserve additive-only outcomes without weakening validation of undefined exclusive-runtime capabilities.

Stable integration and capability identifiers use lowercase ASCII kebab case and are validated for non-blank bounded length and uniqueness when the catalog is constructed. Player-facing display names are separate and may be localized; they never serve as identity keys.

Integration mechanics remain behind two narrow contracts:

- `IRuntimeAuthorityIntegrationInspector` inspects a declared replacement and returns runtime-authority contributions. It cannot mutate Harmony.
- `IAdditiveInteroperabilityInspector` verifies an optional protocol and returns an availability outcome. It cannot contribute to the gameplay Harmony transaction.

When one declared integration participates in both categories, authoritative identity matching occurs once and each category-specific inspector runs independently over only its assigned capabilities. Preparation validates complete ordered output for each category, contains either inspector's failure within that category, and merges the results into one ordered integration outcome. If additive output conflicts with already-validated runtime facts or reuses any runtime diagnostic code with a different message—whether the diagnostic is attached to one capability or to the integration outcome—only the additive category becomes unavailable; its invalid facts never abort or replace the valid runtime contribution. The preparation-owned `additive-integration-outcome-conflict` diagnostic code is reserved at the inspector boundary, making the contained fallback deterministic and collision-free. A runtime-authority observation other than `DoesNotOwn` always has a matching prepared contribution. An additive capability always reports `DoesNotOwn`; a matched compatible protocol is `Ready`, a matched incompatible or unverifiable protocol is `Unavailable`, and an inspection-unavailable protocol is verification-unavailable and `Unavailable`. It can never be `Selected` or `ActivationBlocking` as runtime authority.

There is deliberately no broad `IModCompatibilityProvider`. Identity detection, runtime patch authority, and additive data exchange have different invariants and failure policies; merging them would expose a shallow interface whose callers still need provider-specific type tests and branches.

The current `FastTrackCompatibilityInspector` remains a FastTrack-specific deep module. A thin FastTrack adapter projects its results into `PreparedRuntimeAuthorityContribution` and `ExternalModIntegrationOutcome`. `FastTrackLoadedGameInspectionInput`, FastTrack reflection details, and FastTrack patch classes remain behind that boundary. Conversely, `DeliveryTemperatureRuntimePatchPlan` accepts `RuntimePatchCapabilitySelection` and no longer depends on `FastTrackCompatibilityReport` or constructs a FastTrack-specific support-report object.

### 10.3 Deterministic capability selection

Selection is data-driven but not priority-driven:

1. Build the Klei baseline candidate for each capability defined by Temperature Limit.
2. Evaluate only catalog-declared integration identities against the authoritative loaded-mod context.
3. Ask each matched runtime-authority adapter whether the external mod does not own, compatibly owns, incompatibly owns, or cannot conclusively determine ownership of each declared capability.
4. If no external integration owns a capability, select its Klei baseline.
5. If exactly one integration compatibly owns it, select that prepared contribution.
6. If an integration owns it but is incompatible or ownership inspection is unavailable, fail activation for a required capability; for an optional capability, retain an explicitly diagnosed unavailable selection entry and publish the matching external capability as unavailable.
7. If more than one declared integration claims exclusive ownership, reject the conflict rather than choosing by catalog order, load order, or an undocumented priority.
8. Validate each declared atomic bundle after per-capability selection using the origin-qualified implementation identity, not a bare textual integration ID. A bundle may prohibit a mixed implementation when its members share one correctness invariant. If bundle validation rejects the provisional selections, reproject every externally reported member of that bundle as `ActivationBlocking` with the stable `mixed-runtime-capability-bundle` diagnostic; a failure report must never retain a `Selected` claim that the selector rejected.

The provider-neutral states are explicit:

```text
DeclaredModMatchState
  NotMatched
  Matched
  Ambiguous
  InspectionUnavailable

RuntimeAuthorityObservation
  DoesNotOwn
  OwnsCompatible
  OwnsIncompatible
  OwnershipUnavailable

IntegrationContractState
  NotEvaluated
  Compatible
  Incompatible
  VerificationUnavailable

IntegrationCapabilityDisposition
  NotApplicable
  Selected
  Ready
  Unavailable
  ActivationBlocking
```

These dimensions are not collapsed into a single `Compatible` Boolean. For example, an enabled FastTrack mod can be `Matched` while a disabled FastTrack feature `DoesNotOwn` its capability; an additive protocol can be `Compatible` and `Ready` without ever being `Selected` as runtime authority. For a discovery-driven protocol, `Ready` means Temperature Limit's endpoint is present, verified, and ready to be discovered; it does not claim that a downstream registry mutation was observed unless that integration has an explicit, order-independent observation contract.

There is therefore exactly one selected implementation per exclusive capability, not one global implementation family. Independent capabilities may legitimately select different integrations, while one semantic capability is never assembled from Klei and external fragments. Adding a new mod that supplies an existing capability adds a catalog entry and adapter; it does not add a mod-name branch to the coordinator or selector.

### 10.4 Identity and contract declaration requirements

Each declared integration must state and test:

- stable Temperature Limit integration ID and player-facing display name;
- exact Klei mod static ID or explicitly accepted IDs;
- expected assembly simple names and the rule connecting those assemblies to the active mod entry;
- version, file-version, digest, Harmony-owner, member, signature, IL, or protocol requirements that are semantically necessary for that integration;
- every capability it can own or expose, assigned to exactly one inspection category;
- whether any capabilities form an atomic bundle;
- bounded stable diagnostic identifiers for absence, incompatibility, ambiguity, and unavailable inspection;
- fixture provenance and the upstream version or revision against which the contract was verified; and
- capability-specific unit, reflection, boundary, report, and smoke-test evidence.

The common identity layer answers only whether the declared mod and candidate assemblies are present. Provider-specific structural verification remains in the provider adapter because different mods expose fundamentally different contracts. Temperature Limit does not load or execute third-party fixture assemblies as trusted code and does not take a compile-time reference on another mod's DLL.

An undeclared or unknown mod is not rejected merely for being present. It appears in the ordinary sanitized active-mod inventory, but it receives no compatibility state and cannot be selected as a runtime implementation. If its Harmony registrations suppress or ambiguously alter a required selected seam, the generic selected-owner verification rejects that authority conflict before mutation; Temperature Limit still does not guess an adapter or claim support for it.

### 10.5 Lifecycle and containment rules

Runtime-authority inspection and contribution construction are pure preparation work and complete before the first Harmony mutation. Provider-specific callbacks and emitted helpers obey the same central `GameplayActivationState == Active` gate as Klei callbacks.

Additive interoperability follows a separate boundary:

- a discoverable endpoint may exist before activation, but its getter returns no data and its setter performs no mutation unless the central gameplay gate is `Active`;
- input is schema-checked and canonicalized through Temperature Limit's existing domain API rather than written directly into private fields;
- exceptions at an externally callable boundary are contained, bounded, and reported without escaping into the calling mod;
- inspection or invocation failure marks only that additive capability unavailable; and
- an additive adapter cannot smuggle Harmony registrations into the gameplay activation transaction.

If a future additive integration requires registration into mutable third-party state or requires its own Harmony patches, it needs an explicitly designed idempotent activation unit with its own gate, observation, and compensation policy. It is not silently treated as part of either the core Harmony transaction or the ancillary reporter.

### 10.6 Generic support-report projection

Schema version 2 replaces the singular `runtime.fastTrack` projection with a bounded ordered `runtime.externalModIntegrations` collection. Each item contains only allowlisted facts:

- stable integration ID and display name;
- ordered integration categories when one mod has more than one declared category;
- declared-mod match state;
- availability-aware assembly identity, assembly version, file version, and digest when that integration declares those facts relevant;
- ordered capability outcomes with capability ID, contract state, selection disposition, stable failure identifier, and bounded player-safe message; and
- no third-party object, raw path, stack trace, or unbounded protocol payload.

FastTrack is rendered as one item in this collection. Later declared integrations use the same schema and renderer. Unknown mods remain solely in `activeMods`; their presence does not create a false compatibility claim. `selectedPatchGroups` remains the audit of the concrete runtime plan.

### 10.7 Blueprints Expanded as an extension proof

Blueprints Expanded, Workshop item `3468585385`, is the concrete proof that the seam must support more than runtime patch replacements. Its published feature set includes storing building settings, applying them to new or existing buildings, and preconfiguring planned buildings. The upstream source inspected at commit `76057aef0640ab6877d3537a7d2d11e4b86faf39` documents and discovers this opt-in convention on a non-nested type:

```text
public static JObject Blueprints_GetData(GameObject source)
public static void Blueprints_SetData(GameObject source, JObject data)
public static string Blueprints_ID() // optional stable data key
```

The upstream scanner walks loaded non-dynamic assemblies, locates the getter and setter by exact parameter types, creates delegates, and registers them. This is Blueprints Expanded's declared protocol; it does not justify Temperature Limit scanning arbitrary assemblies for its own adapters.

Blueprints Expanded invokes that scan from its own `OnAllModsLoaded`. Temperature Limit's endpoint is compile-time present as soon as its assembly is loaded, so discovery does not depend on which mod's callback runs first. The future adapter must not reflect into Blueprints Expanded's internal registry or label registration as observed when callback order makes that unknowable; the getter and setter remain gate-aware when Blueprints invokes their retained delegates later.

A later `BlueprintsExpandedSettingsTransferIntegration` therefore belongs behind the additive interoperability contract, not inside `DeliveryTemperatureRuntimePatchPlan`. Its intended endpoint must:

- expose the exact public static methods on a top-level Temperature Limit type so the published scanner can discover them;
- return `null` and make the setter a no-op while Temperature Limit is not `Active`;
- make `Blueprints_ID` return the same stable data key in every activation state because the scanner invokes it during registration, while a key such as `DeliveryTemperatureLimit.BuildingSettings` carries a separate integer payload schema version;
- round-trip only allowlisted canonical integer-Kelvin bounds, including the explicit disabled representation, for a `GameObject` that actually owns a Temperature Limit component; localized display units and global defaults are not payload data;
- reject an unknown schema, wrong token type, missing component, or out-of-range value without partially applying data;
- parse and validate the complete payload before applying it once through a dedicated atomic `TemperatureLimit` domain method built on the existing canonicalization path; two sequential setter calls and direct private-field writes are forbidden because either could expose a partially applied pair; and
- contain every externally triggered exception and publish a bounded diagnostic rather than throwing through Blueprints Expanded.

This activation change does not advertise or ship Blueprints Expanded support. Before that support can be declared publicly, its packaged static ID and assembly identity must be verified, the exact protocol and payload must be covered by inert or reflection-emitted fixtures, old/unknown payload behavior must be tested, and a two-mod development smoke test must prove capture, placement, application to an existing building, planned-building preconfiguration where applicable, and inactive-gate behavior.

### 10.8 Extension acceptance template

A later declared integration is complete only when it adds:

1. one explicit catalog descriptor;
2. one narrow category-specific adapter;
3. exact identity and contract fixtures with provenance;
4. per-capability compatible, incompatible, absent, inactive, and inspection-unavailable tests as applicable;
5. generic report-projection tests;
6. inactive-gate and external-exception-containment tests for every callable endpoint;
7. combination tests with every already declared integration that can overlap the same capability;
8. a representative installed-mod smoke test; and
9. public compatibility documentation only after all preceding evidence passes.

Adding a mod that implements an existing capability must not require a new activation state, compensation status, report-schema shape, or provider-specific condition in `GameplayActivationCoordinator`. A genuinely new Temperature Limit capability may add a new capability definition, but it still uses the same selection and reporting machinery.

## 11. Settings capture

The activation path creates exactly one discriminated outcome:

```text
SettingsSnapshotResult
  Available(ActivationSettingsSnapshot)
  Unavailable(stableDiagnosticIdentifier)
```

`ActivationSettingsSnapshot` is immutable and contains exactly the option values used to select or configure the runtime plan. The reporter consumes this outcome; it never accesses `DeliveryTemperatureLimitOptions.Instance` independently after activation preparation.

This avoids repeated access to a failed `Lazy<T>` factory, whose exception is cached and rethrown by .NET. It also makes the report semantically precise: it reports activation settings, not an unlabelled claim about possibly changed current UI values.

If settings capture is unavailable, activation fails during `Preparing`, compensation is `NotRequired`, and the settings result is retained for reporting. A report renders the settings as unavailable with the stable diagnostic identifier rather than substituting default values.

## 12. Patch registration and observation

### 12.1 Registration identity

`GameplayPatchRegistrationIdentity` contains:

- original target `MethodBase`;
- `HarmonyPatchContractKind`;
- patch `MethodInfo`; and
- the exact Harmony owner string.

Identity equality is exact reflection identity plus ordinal owner equality.

### 12.2 Baseline proof

Before the activation state can leave `Preparing`, the adapter reads the appropriate Harmony patch-kind collections for the complete planned set and proves that each target and patch method is absent under every owner. Preparation also proves that the planned set does not use the same patch method more than once on a target across patch kinds.

If any baseline identity cannot be observed or an ambiguous existing registration is found, activation fails in `Preparing` without entering any `Harmony.Patch` call, and compensation is `NotRequired`. This complete baseline proof is what makes later `Unpatch(original, patchMethod)` attributable to this attempt even though that exact-method API does not accept an owner argument.

### 12.3 Attempt journal

Immediately after baseline proof and immediately before calling `Harmony.Patch`, the coordinator appends an immutable attempt-journal entry. The entry records:

- registration identity;
- ordered binding index;
- baseline absence;
- whether `Harmony.Patch` returned;
- the post-call observation outcome; and
- any bounded stable diagnostic identifiers produced for the step.

Journaling before the mutation call closes the partial-registration hole: if `Harmony.Patch` registers the method and then throws, the identity remains eligible for exact compensation even when the first post-fault observation also fails.

### 12.4 Post-registration audit

After each successful patch call, the adapter verifies that the exact identity is present in the correct patch-kind collection under the Temperature Limit owner. After the final binding, a complete audit verifies every planned identity again. Only the complete audit permits `Installing -> Active`.

An absent identity, wrong kind, wrong owner, duplicate identity, or unavailable observation is an installation failure and starts compensation.

## 13. Patch-registration compensation

Compensation operates over every journal entry whose mutation call was entered, in reverse application order. It never operates over a globally discovered owner set and never removes a foreign method.

For each entry, the coordinator:

1. attempts `Unpatch(original, patchMethod)`;
2. records but does not propagate an exception;
3. continues with all remaining entries; and
4. performs a complete post-compensation observation after all attempts.

The original activation-critical exception remains the primary cause in `GameplayActivationFailureRecord`. Compensation exceptions and observation failures are secondary bounded diagnostics.

The exact enum is:

```text
PatchCompensationStatus
  NotRequired
  VerifiedComplete
  Incomplete
  VerificationUnavailable
```

Classification is deterministic:

- `NotRequired`: no `Harmony.Patch` call was entered during the failed attempt.
- `VerifiedComplete`: at least one patch call was entered and every attempted identity is verified absent after compensation.
- `Incomplete`: at least one attempted identity is verified still present. This classification takes precedence over simultaneous unavailable observations because a residual is known.
- `VerificationUnavailable`: no identity is known to remain, but at least one attempted identity cannot be observed conclusively.

An unpatch exception followed by conclusive absence can therefore yield `VerifiedComplete`, while preserving the unpatch exception as a secondary diagnostic. The status describes the verified final registration condition, not whether every compensation call returned normally.

## 14. Immutable failure record

`GameplayActivationFailureRecord` is created once and contains only bounded or availability-aware values:

- stable primary diagnostic identifier;
- failure stage;
- UTC occurrence time;
- terminal activation state;
- patch-compensation status;
- primary exception type and path-redacted bounded message;
- optional failed registration identity rendered without filesystem paths;
- attempted and compensated registration counts;
- selected per-capability runtime authorities and bounded declared-integration outcomes when preparation reached that point;
- `SettingsSnapshotResult`;
- bounded secondary diagnostic identifiers.

The record does not retain arbitrary loaded objects, mutable Harmony collections, a colony identity, local paths, or an unbounded exception graph. Full exception text may still be mirrored best-effort to `Player.log`, but the local diagnostic report uses the bounded, sanitized representation.

Automatic report and warning outcomes are retained separately in an immutable
`GameplayActivationFailureResponseOutcome`. The report consumes the failure
record, so placing its own later creation outcome back into that record would
create a temporal cycle and make the claimed immutability false.

Suggested stable identifiers include:

- `DTL-ACTIVATION-INITIALIZATION-FAILED`;
- `DTL-ACTIVATION-PREPARATION-FAILED`;
- `DTL-ACTIVATION-REGISTRATION-FAILED`;
- `DTL-ACTIVATION-COMPENSATION-INCOMPLETE`;
- `DTL-ACTIVATION-COMPENSATION-UNVERIFIED`;
- `DTL-ACTIVATION-REPORT-FAILED`; and
- `DTL-ACTIVATION-DIALOG-FAILED`.

The detailed implementation plan will assign one identifier to each concrete stage and test uniqueness and stability.

## 15. Automatic local diagnostic report

### 15.1 Separate activation-failure flow

The existing manual support-report actions remain explicit user flows. Their success presenter currently copies a summary, opens the report folder, and opens the issue form; that presenter is not reused for automatic activation failures.

The failure handler instead calls one idempotent `TryCreateActivationFailureReport` operation. It:

- consumes the immutable failure record and already captured support snapshots;
- consumes `SettingsSnapshotResult` without touching the options `Lazy<T>` again;
- creates a standard support-report document augmented with the activation
  failure; it does not introduce a third report kind;
- creates no extended log snapshot;
- reads no `Player.log`;
- sanitizes diagnostic messages through the existing path redactor;
- applies the existing count, message, and total JSON-size limits;
- writes through the existing unique temporary-file and durable-promotion path; and
- returns an availability-aware report outcome rather than throwing to the lifecycle boundary.

### 15.2 Schema evolution

Because version 1 requires concrete settings values and has no activation-failure record, the report schema advances to version 2 instead of overloading existing fields dishonestly.

Version 2 adds:

- availability-aware `temperatureLimit.activationSettings`;
- `runtime.gameplayActivationState`;
- `runtime.patchCompensationStatus`;
- bounded ordered `runtime.externalModIntegrations`, replacing the singular version-1 `runtime.fastTrack` shape; and
- an optional bounded `activationFailure` object.

The automatic activation-failure artifact uses `reportKind: "standard"` and
contains no `playerLog`. Manual standard and extended reports also use schema
version 2 and label the captured values as activation settings. Existing
version-1 files remain readable artifacts; the mod does not migrate or rewrite
them.

### 15.3 Persistence boundary

The report is an intentional persistent diagnostic artifact. The failure handler makes no persistent control-state change: it does not alter options, Klei's enabled-mod registry, a save, or a retry marker.

Nothing is uploaded automatically. The report stays local until the player chooses to attach it to an issue.

## 16. Player warning and actions

The installed ONI build exposes one `KMod.Manager.Dialog` overload with three text/action pairs, so the approved recovery choices require no new UI dependency.

### 16.1 Verified containment

For `NotRequired` or `VerifiedComplete`:

- **Title:** `Temperature Limit couldn't activate`
- **Consequence:** Temperature limits are not enforced for this launch.
- **Primary action:** `Continue Without Limits`
- **Secondary action:** `Open Report Folder`
- **Third action:** `Report Issue`

The continue callback deliberately performs no control-state change; selecting it merely dismisses the warning. The phrase graceful degradation may be used in internal documentation for this verified outcome, but the player text remains direct and concrete.

### 16.2 Unverified or incomplete registration state

For `Incomplete` or `VerificationUnavailable`:

- **Title:** `Temperature Limit couldn't activate`
- **Consequence:** Temperature limits are not enforced, and Temperature Limit could not verify that all of its Harmony registrations were removed.
- **Recommendation:** Restart ONI before loading a colony.
- **Primary action:** `Exit Game`
- **Secondary action:** `Open Report Folder`
- **Third action:** `Report Issue`

The exit callback uses Unity's player-application quit operation only after the player chooses it. If the action fails, the failure is contained and recorded; the dialog text has already supplied the manual restart recommendation.

### 16.3 Action boundaries

No folder or browser operation occurs before the corresponding user action. `Report Issue` opens a bounded GitHub issue-form URL containing the stable diagnostic identifier, report ID or unavailable state, compensation status, and a short player-facing summary. It does not copy data to the clipboard or submit the issue.

If report creation failed, the warning says so and the folder action targets only the known support-report directory where possible. The issue action remains available with the report outcome marked unavailable.

The warning is attempted once. A dialog failure is recorded best-effort and is not retried in a loop.

## 17. Testing architecture

### 17.1 Pure activation core

`Source/GameplayActivation/Core` contains the framework-independent state machine, identity/value types, attempt journal, compensation classifier, and interfaces needed by the coordinator. It may use BCL reflection types and the existing pure Harmony contract-kind/binding types, but it cannot reference Unity, Klei, PLib, or concrete Harmony API types.

The existing MSTest project links this exact production directory. Tests create fresh coordinator instances and deterministic fake preparation, registry, observation, report, and presentation ports. No production reset API or conditional test branch is added.

### 17.2 Concrete Harmony adapter

A narrow production adapter maps verified bindings to:

- manual `Harmony.Patch` with exactly one patch kind populated;
- `Harmony.GetPatchInfo` and the matching kind collection; and
- exact `harmony.Unpatch(original, patchMethod)`.

The adapter contains no compatibility selection or state policy. Source and merged-assembly contracts verify this limited mapping.

The test process additionally loads the installed, digest-pinned Harmony 2.4.2 assembly through reflection and patches dynamically emitted fixture methods. This avoids adding a compile reference that would conflict with the test project's deliberately minimal Harmony type fixtures while still exercising the real installed API and metadata behavior.

### 17.3 Lifecycle and presentation shells

Unity/Klei integration remains a thin shell. Pure tests exercise response selection and one-shot behavior through ports. Installed-assembly metadata tests verify the real Klei lifecycle and three-action dialog signatures. Compiled IL/source-boundary tests verify that lifecycle catch paths do not contain a rethrow and that `OnLoad` has no gameplay patch call.

### 17.4 Inactive-behavior inventory

Every prepared patch binding is mapped to one tested inactive-behavior route:

- direct activation-gate guard;
- activation-authorized game-session lookup;
- inert `__state` propagation; or
- transpiled call to a gate/session-aware helper.

The inventory must cover the union of the Klei baseline, every selected declared runtime-authority contribution, and the seven moved late patches. A missing or duplicate identity fails the contract test. A source-boundary test also proves that the gameplay activation core and `DeliveryTemperatureRuntimePatchPlan` do not reference `FastTrackCompatibilityReport`, `FastTrackFeature`, or another provider-specific result type.

## 18. Deterministic fault-injection matrix

The test suite injects controlled faults at these boundaries:

| Boundary | Injected fault | Required evidence |
|---|---|---|
| Framework initialization | Base lifecycle, PLib, localization, or option registration throws | No lifecycle exception escapes; no patch call; `Failed`; `NotRequired`; response deferred or attempted once |
| Loaded-mod support publication | Sanitized snapshot capture throws | Authoritative topology remains available to preparation; ancillary failure cannot escape |
| Settings capture | Options `Lazy<T>` throws | One unavailable settings result retained; no second access; no patch call |
| Klei target resolution | Missing, renamed, ambiguous, or wrong-signature member | Complete preparation rejected before mutation |
| Declared identity matching | Missing, duplicate, ambiguous, or mismatched static ID/assembly ownership | Only the affected declaration receives the exact outcome; no name-based guess or arbitrary assembly scan |
| Exclusive capability selection | Active owner is incompatible, inspection is unavailable, two integrations claim ownership, or an atomic bundle is mixed | Required capability blocks before mutation; optional capability is explicitly unavailable; no priority or Klei fallback |
| FastTrack adapter | Wrong active-mod identity, file identity, owner, member, signature, or IL | Provider-neutral selector receives an exact incompatible outcome; no speculative or mixed-capability fallback; no patch call |
| Additive integration inspection | Protocol is absent or changed, the inspector throws, or its category output conflicts with validated runtime facts or diagnostics | Capability is unavailable with a stable diagnostic; no gameplay patch is contributed; otherwise valid runtime contributions and core activation continue |
| Undeclared co-resident mod | Unknown noninterfering mod and assembly are present | It remains only in the sanitized active-mod inventory and cannot be selected as a capability implementation |
| Undeclared authority conflict | Unknown Harmony owner suppresses or ambiguously changes a required selected seam | Generic selected-owner verification rejects the topology before mutation without inventing an adapter or compatibility claim |
| Harmony argument binding | Renamed target argument, wrong by-reference shape, invalid special injection, or wrong overload | Verifier rejects complete set before mutation |
| Transpiler preflight | Missing or ambiguous anchor, changed local/member, or emitted-call mismatch | No patch call |
| Registration before mutation | Registry throws before binding `n` is applied | Entry `n` absent; earlier attempts compensated; later bindings untouched |
| Registration after mutation | Registry registers binding `n` and then throws | Pre-call journal retains `n`; exact compensation attempted |
| Post-registration observation | Observation throws, reports absent, or reports wrong owner/kind | Activation never becomes `Active`; compensation begins |
| Every binding index | Before/after fault at every binding in Klei and all selected declared runtime-authority plans | No first/middle/last-only coverage gap |
| Foreign registration | Different Harmony owner patches same target with a different patch method | Foreign identity survives Temperature Limit compensation |
| Baseline ambiguity | Same target/patch method already exists | Complete preparation fails with no patch call and `NotRequired` rather than risking foreign removal |
| Compensation before removal | Exact unpatch throws without removing | Remaining entries still attempted; final status reflects observation |
| Compensation after removal | Removal succeeds and adapter then throws | Conclusive absence may still produce `VerifiedComplete`; secondary fault retained |
| Residual registration | Exact method deliberately remains | `Incomplete`; gameplay gate inactive; restart recommendation |
| Observation unavailable | Final metadata read throws | `VerificationUnavailable` unless a known residual makes status `Incomplete` |
| Report capture/write | Snapshot, redaction, serialization, directory, or file operation throws | Lifecycle returns; report marked unavailable; no automatic external action |
| Dialog display | Klei dialog throws | Lifecycle returns; best-effort diagnostic only |
| User action | Folder, issue, or quit operation throws | Action failure contained without altering primary failure |
| Re-entry | Call during preparation, installation, or compensation | One attempt only; original attempt fails coherently |
| Repeated callback | Call after `Active` or `Failed` | Idempotent outcome; no duplicate mutation, report, or warning |
| Publication timing | Worker reads gate at every transition checkpoint | Inactive through preparation, installation, and compensation; active only after complete audit |
| Additive endpoint while inactive | External getter or setter is invoked in every non-`Active` state | Getter returns no data; setter is inert; no exception crosses the external-mod boundary |
| Additive payload | Unknown version, missing key, wrong token type, invalid range, or setter failure | No partial application; existing canonical settings remain authoritative; bounded diagnostic only |

Fault injection is test orchestration, not a shipped runtime feature.

## 19. Declared external-mod, Klei, and Harmony compatibility coverage

The current installed ONI assembly and Harmony assembly remain digest-pinned inputs. The existing FastTrack `0.18.4.0` binary remains inert fixture evidence and is never executed as trusted test code. A reflection-emitted additive fixture models a convention-based settings-transfer consumer without making a production support claim for that consumer.

Coverage must prove:

1. The Klei-only topology produces one complete verified patch set before mutation.
2. The supported FastTrack topology produces provider-neutral contributions and one coherent selected set before mutation.
3. FastTrack absent, inactive replacement, fully compatible, status-only incompatible, and delivery-feature incompatible states remain distinct behind the FastTrack adapter.
4. A synthetic second runtime-authority adapter can supply an existing capability without a coordinator change, report-schema change, or provider-specific selector branch.
5. Conflicting owners and invalid atomic bundles are rejected deterministically without catalog-order or load-order precedence.
6. A synthetic additive adapter can be compatible, unavailable, or throwing without changing the core activation state or contributing a gameplay Harmony binding.
7. Catalog declarations are explicit, duplicate integration/capability identities fail validation, and undeclared mods cannot affect selection.
8. Reflection-emitted fixtures reject renamed parameters, overload ambiguity, missing members, changed generic/by-reference/return types, changed owner topology, and changed additive-protocol signatures.
9. Every patch entry point passes `HarmonyPatchContractBindingVerifier` against the exact selected target.
10. An incompatibility never combines Klei and an external implementation within one semantic capability.
11. A fault at every selected binding index produces a contained, inert failure.
12. Real Harmony metadata identifies owner and patch method as assumed by the adapter.
13. Exact removal of Temperature Limit's patch method preserves another owner's method on the same target.
14. The published Blueprints Expanded convention remains an extension proof only; tests and public metadata do not label it supported until its dedicated acceptance gate is complete.

## 20. Support-report schema and privacy tests

Schema-version-2 tests must cover:

- available and unavailable activation settings;
- every activation state and compensation status;
- zero, one, and multiple generic external-mod integration outcomes;
- FastTrack rendered through the generic integration collection rather than a singular schema member;
- absent, inactive, selected, incompatible, and inspection-unavailable capability projections;
- an activation-triggered standard report with and without a failed binding
  identity;
- path redaction in primary and secondary exception messages;
- count and message bounds;
- total JSON-size enforcement;
- absence of `playerLog` for activation-triggered standard reports;
- deterministic property/list order where the existing serializer contract requires it;
- report-write failure without temporary-file leakage;
- no clipboard, folder, or browser operation during automatic creation; and
- user-initiated folder and issue operations only.

The privacy allowlist remains the governing model. No arbitrary exception object, loaded-mod object, Harmony object, or filesystem path enters the document model.

## 21. Approved configuration amendment

The only configuration change is the user-approved addition to the existing production-source `ItemGroup` in:

`mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`

```xml
<Compile Include="..\Source\GameplayActivation\Core\**\*.cs"
         Link="Production\GameplayActivation\Core\%(RecursiveDir)%(Filename)%(Extension)" />
```

Impact:

- the existing required MSTest project compiles the exact pure production activation core;
- deterministic fault-injection tests execute production state and compensation policy;
- no package or assembly reference is added;
- no production build setting or packaged output changes;
- no lockfile changes;
- no second test project is created; and
- the existing pipeline automatically runs the tests.

`oni-mod-pipeline.toml` remains byte-for-byte unchanged.

## 22. Expected implementation boundaries

The detailed implementation plan may refine filenames while preserving these semantic boundaries:

```text
Source/
  GameplayActivation/
    Core/
      GameplayActivationState
      PatchCompensationStatus
      SettingsSnapshotResult
      GameplayPatchRegistrationIdentity
      GameplayActivationFailureRecord
      GameplayActivationCoordinator
      ExternalModIntegration/
        DeclaredModIntegrationCatalog
        RuntimeCapabilityId and definitions
        PreparedRuntimeAuthorityContribution
        RuntimePatchCapabilitySelection
        ExternalModIntegrationOutcome
      pure ports and immutable outcomes
    HarmonyIntegration/
      concrete Harmony registration/observation adapter
    KleiIntegration/
      lifecycle failure response and player warning shell
  RuntimePatchInstallation/
    provider-neutral target resolution, capability selection, and composition root
  FastTrackCompatibility/
    FastTrack-specific identity, reflection, patch, and projection adapter
  ExternalModIntegrations/
    future additive adapters such as Blueprints Expanded; absent until declared
  SupportReporting/
    schema-version-2 generic integration projection and activation-failure report path
```

`DeliveryTemperatureRuntimePatchInstaller` remains the cold composition root for runtime target resolution and patch-plan construction, but it no longer owns an untestable private state machine, an overclaimed rollback helper, or FastTrack-specific selection policy. The composition root constructs the explicit catalog; the coordinator and runtime plan consume only provider-neutral outcomes. The existing Harmony binding work is integrated into preparation.

## 23. Release acceptance

The change cannot be called complete until all of the following pass:

1. Focused pure coordinator tests.
2. Provider-neutral catalog, capability selector, atomic-bundle, conflict, and duplicate-declaration tests.
3. Synthetic second-runtime-authority and additive-integration extension tests that require no coordinator or report-schema branch.
4. The complete deterministic fault-injection matrix.
5. Every-binding-index before/after-mutation coverage for Klei and every selected declared runtime-authority plan.
6. Settings single-capture and unavailable-report tests.
7. Inactive-behavior inventory and additive-endpoint gate coverage.
8. Installed ONI and Harmony metadata contracts.
9. FastTrack binary and reflection-emitted compatibility contracts.
10. Reflection-emitted convention-based additive-protocol contracts.
11. Real-Harmony registration, metadata, foreign-owner preservation, and exact-removal tests.
12. Schema-version-2 generic-integration, path-redaction, bound, and automatic-presentation tests.
13. Source-boundary proof that the coordinator, selector, and runtime plan contain no FastTrack-specific result dependency or provider-name condition.
14. Compiled lifecycle exception-region/no-rethrow contracts.
15. `oni-mod-pipeline build`.
16. `oni-mod-pipeline test --mod mods/delivery-temperature-limit-supercooled`.
17. A development installation smoke test with FastTrack absent.
18. A development installation smoke test with the supported FastTrack topology active.
19. Confirmation that no activation warning appears on either successful smoke path and Temperature Limit behavior remains functional.
20. The repository's formal `/review` over all uncommitted changes.
21. Resolution or explicit deferral of every confirmed P0-P2 review finding.

A release candidate is not prepared while the workspace contains unrelated or unreviewed relevant changes. Commit and push remain separately authorized operations.

## 24. Failure-message acceptance

Player-facing copy must:

- name Temperature Limit;
- say it could not activate rather than saying merely that an error occurred;
- say that limits are not enforced for the launch;
- distinguish verified from incomplete/unavailable compensation without technical jargon;
- recommend restart before colony load when registration state is not verified;
- never display a raw path or stack trace;
- never imply that a report was uploaded;
- disclose when local report creation failed; and
- keep the stable diagnostic identifier available for support.

Tests assert meaning-bearing fragments and action selection rather than locking punctuation that localization may legitimately change.

## 25. Supersession of earlier design language

This specification supersedes earlier Temperature Limit design statements that:

- require gameplay patches during `OnLoad`;
- require an installation exception to be rethrown from a Klei lifecycle callback;
- describe runtime installation as fail-closed without distinguishing lifecycle containment from process termination;
- call Harmony removal exact rollback or a transaction; or
- imply that report generation may access `DeliveryTemperatureLimitOptions.Instance` independently of activation settings capture.

Earlier documents remain historical records and are not rewritten wholesale. The active support-reporting specification receives a narrow supersession note and schema-version-2 clarification so its runtime integration and report contract do not contradict this design.

## 26. Evidence and references

The design is grounded in the following primary or authoritative sources and locally verified runtime evidence:

- [Harmony basics: patch inspection and specific unpatching](https://harmony.pardeike.net/articles/basics.html)
- [Harmony API: `Harmony.Patch` and patched-method inspection](https://harmony.pardeike.net/api/HarmonyLib.Harmony.html)
- [Harmony `Patches` metadata collections and owners](https://harmony.pardeike.net/api/HarmonyLib.Patches.html)
- [Microsoft Compensating Transaction pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction)
- [Microsoft .NET exception best practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Microsoft `Lazy<T>` thread-safety and exception caching](https://learn.microsoft.com/en-us/dotnet/framework/performance/lazy-initialization)
- [Unity `Application.Quit`](https://docs.unity3d.com/ScriptReference/Application.Quit.html)
- [NASA software fault-injection reference](https://ntrs.nasa.gov/api/citations/20000120144/downloads/20000120144.pdf)
- [Blueprints Expanded Workshop feature description](https://steamcommunity.com/sharedfiles/filedetails/?id=3468585385)
- [Blueprints Expanded settings-transfer convention and scanner source at inspected revision](https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/blob/76057aef0640ab6877d3537a7d2d11e4b86faf39/BlueprintsV2/BlueprintsV2/ModAPI/API_Methods.cs)
- [Blueprints Expanded `OnAllModsLoaded` registration point at inspected revision](https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/blob/76057aef0640ab6877d3537a7d2d11e4b86faf39/BlueprintsV2/Mod.cs)
- [Blueprints Expanded upstream repository and MIT license](https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods)
- installed ONI `Assembly-CSharp.dll`, changelist `744825`, including the inspected `KMod.Manager.Dialog` signature and Klei mod-load control flow;
- installed `0Harmony.dll` version `2.4.2.0` and its repository-pinned digest contract; and
- the repository's inert FastTrack `0.18.4.0` fixture and reflection-emitted compatibility fixtures.

## 27. Summary

Temperature Limit will perform one completely prepared late activation, publish gameplay behavior only after every registration is verified, and become process-lifetime inert if activation cannot complete. Any potentially applied registration is journaled before mutation, compensated exactly, and classified by observed final state. External-mod support is declared explicitly and selected per semantic capability: FastTrack is one runtime-authority adapter, while future protocols such as Blueprints Expanded settings transfer use a separate additive boundary. Provider-specific types do not enter the coordinator, runtime plan, or report schema. The primary failure is retained even when compensation or diagnostics fail. Players receive one accurate warning and one local report attempt without automatic external actions or persistent control-state changes. Deterministic fault injection, real Harmony metadata exercises, declared-integration fixtures, compiled lifecycle inspection, the existing pipeline, and formal review form the release gate.
