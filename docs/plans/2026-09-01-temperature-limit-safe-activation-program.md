# Temperature Limit Safe Activation Program Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in dependency order. Follow the repository's test-driven-development and formal review gates, and use the checkboxes (`- [ ]`) to track progress.

**Goal:** Make Delivery Temperature Limit activate once, late, and atomically; remain behaviorally inert on every contained activation failure; report that failure safely; and make external-mod support extensible without coupling the coordinator to FastTrack or prematurely claiming Blueprints Expanded support.

**Architecture:** Four dependent plans establish provider-neutral capability selection, a framework-independent activation/compensation engine, a separate local failure-response flow, and finally the concrete Harmony/Klei lifecycle wiring plus inactive-behavior proof. The central process-lifetime gate is published `Active` only after every selected registration is observed under the exact owner and kind. Every earlier or failed state is inert even when compensation is incomplete.

**Tech Stack:** C# with nullable reference types; .NET Standard 2.1 production assembly; MSTest on .NET 10; Harmony 2.4.2; Klei/Unity runtime contracts; PLib 4.24.0; Newtonsoft.Json; `oni-mod-pipeline`.

**Spec:** `docs/specs/2026-08-31-temperature-limit-lifecycle-contained-activation-design.md`

## Global Constraints

- Treat the current working tree as authoritative and user-owned. Do not restore or overwrite the existing uncommitted Harmony binding work or any other pre-existing edit.
- The only approved configuration edit is the exact `Compile` item for `Source/GameplayActivation/Core/**/*.cs` in `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj` specified in section 21 of the design.
- Do not change `oni-mod-pipeline.toml`, either `packages.lock.json`, `Source/DeliveryTemperatureLimit.csproj`, `mod.yaml`, `mod_info.yaml`, Workshop metadata, CI, package references, or target frameworks.
- Do not add a second test project or compile-time reference to a third-party mod assembly.
- Retain and integrate `HarmonyPatchContractBinding` and `HarmonyPatchContractBindingVerifier`; do not replace or bypass the argument-binding verification already in the working tree.
- Keep FastTrack identity, reflection, and patch details inside the FastTrack adapter. The capability selector, activation coordinator, runtime patch plan, and schema must not branch on the FastTrack name or consume `FastTrackCompatibilityReport`.
- Treat Blueprints Expanded as an extension proof only. Do not ship a Blueprint endpoint, catalog declaration, compatibility claim, public support statement, or third-party assembly fixture in this program.
- No automatic upload, browser opening, folder opening, clipboard write, game exit, option mutation, save mutation, enabled-mod mutation, or retry marker may happen merely because activation failed.
- A commit requires explicit authorization for the staged snapshot. A push requires separate authorization. Every commit command in the detailed plans is therefore a gated checkpoint, not standing authorization.
- Before any final completion claim or commit/push, state `Implementation complete; /review pending` and ask the user to invoke built-in `/review` over all uncommitted changes. Resolve or explicitly defer every confirmed P0-P2 finding.

---

## Plan Set and Dependency Order

```text
1. Declared integration foundation
   provider-neutral IDs, catalog, selector, FastTrack adapter, schema v2
                          │
                          ▼
2. Pure activation core
   state, gate, settings result, journal, audit, compensation, re-entry
                          │
                          ▼
3. Activation failure response
   immutable projection, standard local report, bounded issue URL, three actions
                          │
                          ▼
4. Harmony/Klei integration and release evidence
   one late activation, concrete registry, all callbacks inert, smoke gates
```

Execute the plans in this exact order:

1. `docs/plans/2026-09-01-temperature-limit-declared-integration-foundation.md`
2. `docs/plans/2026-09-01-temperature-limit-pure-activation-core.md`
3. `docs/plans/2026-09-01-temperature-limit-activation-failure-response.md`
4. `docs/plans/2026-09-01-temperature-limit-harmony-lifecycle-integration.md`

Each plan ends with a green, reviewable repository state. Do not start the next plan while the preceding plan has a failing focused suite.

## Responsibility Map

| Area | Owns | Must not own |
|---|---|---|
| `GameplayActivation/Core/ExternalModIntegration` | Validated identities, capability definitions, deterministic selection, generic outcomes | Klei objects, concrete Harmony API, Unity, FastTrack result types |
| `FastTrackCompatibility` | FastTrack identity matching, structural inspection, exact authority evidence, patch contribution preparation | Coordinator state, generic selection policy, support-document shape |
| `RuntimePatchInstallation` | Cold composition, target resolution, complete preparation, installed authority recheck | An independent private activation state machine, player presentation |
| `GameplayActivation/Core` | State transitions, safe gate publication, attempt journal, post-registration audit, compensation classification | Klei lifecycle calls, Unity UI, disk I/O, concrete Harmony calls |
| `GameplayActivation/HarmonyIntegration` | Register, observe, and remove one exact Harmony binding | Compatibility selection, failure policy, reporting |
| `GameplayActivation/KleiIntegration` | Last-chance lifecycle containment and one-shot response dispatch | Core compensation decisions, provider-specific inspection |
| `SupportReporting/Core` | Schema-v2 immutable allowlisted facts, bounds, summaries, URLs | Runtime object graphs, raw exceptions, arbitrary paths |
| `SupportReporting/KleiIntegration` | Snapshot capture, local atomic write, user-invoked actions | Independent option re-read after activation capture, automatic external action |

## Cross-Plan Invariants

The implementation is acceptable only if all of these statements are mechanically testable and true:

1. `OnLoad` makes zero gameplay Harmony registrations.
2. The authoritative `loadedMods` callback argument, not the sanitized reporting snapshot, drives integration inspection.
3. Settings are accessed once. A failed lazy load is retained as unavailable and never retried during reporting.
4. All targets, provider contracts, transpilers, argument bindings, inactive routes, identities, and the complete pre-mutation baseline are verified before the first `Harmony.Patch` call.
5. The attempt journal records binding `n` before calling the concrete registry for binding `n`.
6. Every successful registry call is followed by exact owner/kind/method observation before activation can continue.
7. Any uncertainty after the first mutation changes the gate to inactive before compensation starts.
8. Compensation attempts every journaled identity in reverse order, preserves foreign patch methods, retains the primary failure, and classifies final observation as `VerifiedComplete`, `Incomplete`, or `VerificationUnavailable`.
9. `Active` is the last success publication; `Failed` is terminal; repeated callbacks do no new work; re-entry creates no parallel attempt.
10. Every selected Klei and declared external-mod patch callback has exactly one reviewed inactive route.
11. Schema version 2 contains a bounded ordered generic external-integration collection and no singular `runtime.fastTrack` field.
12. Automatic failure reporting writes only a standard local report and does not read `Player.log` or perform external actions.
13. Player text distinguishes verified containment from incomplete or unavailable registration state and recommends restart before colony load only for the latter.
14. An undeclared mod cannot become an integration candidate. An unknown Harmony authority conflict may block a required capability, but it never creates a guessed support claim.
15. A future additive integration can report `Ready` or `Unavailable` without contributing gameplay Harmony registrations or changing core activation success.

## Common Red-Green Commands

Run the concrete focused `dotnet test` command printed in the current task after each small production change. Every detailed task names its exact test-class filter and expected red/green behavior; do not broaden to the complete suite until that focused command is green.

At every plan boundary run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
git diff --check
```

Expected: every discovered test passes; zero tests are skipped or inconclusive; `git diff --check` emits no output.

At the final program boundary run:

```powershell
oni-mod-pipeline diagnose --mod mods/delivery-temperature-limit-supercooled
oni-mod-pipeline validate --mod mods/delivery-temperature-limit-supercooled
oni-mod-pipeline build --mod mods/delivery-temperature-limit-supercooled
oni-mod-pipeline test --mod mods/delivery-temperature-limit-supercooled
```

Expected: all commands exit `0`; `build` prints one new exact `build-result.json` path; `test` reports all required tests passed with no skipped or inconclusive result. Preserve the exact path printed by that build; never infer a latest build from the artifact directory.

## Configuration Invariance Check

Before the final review gate, inspect the only allowed project-file delta and require no output for every forbidden configuration file:

```powershell
git diff -- mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj
git diff --exit-code -- mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml mods/delivery-temperature-limit-supercooled/Source/packages.lock.json mods/delivery-temperature-limit-supercooled/Tests/packages.lock.json mods/delivery-temperature-limit-supercooled/mod.yaml mods/delivery-temperature-limit-supercooled/mod_info.yaml
```

Expected: the first command shows only the approved `GameplayActivation/Core/**/*.cs` linked-compile item; the second exits `0` with no diff.

## Commit Checkpoints

Each detailed plan names a conventional commit message. At each checkpoint:

1. Run the task's focused tests and the plan-boundary suite.
2. Show `git status --short` and `git diff --stat` to the user.
3. Stage only the files named by that plan.
4. Stop unless the user explicitly authorizes committing that staged snapshot.
5. If authorized, load and follow the `committing-to-git` skill, verify the index, and create the signed commit requested by that skill.

Do not combine unrelated pre-existing changes into an implementation commit merely because they are present in the working tree.

## Program Completion Gate

After all four plans and both development smoke topologies are complete:

- [ ] Confirm the all-or-inert invariant at every deterministic fault boundary.
- [ ] Confirm Klei-only activation and supported FastTrack activation both reach `Active` without a warning.
- [ ] Confirm a forced failure never escapes either Klei lifecycle callback.
- [ ] Confirm the forced-failure report remains local and contains the stable diagnostic ID and compensation status.
- [ ] Confirm every remaining Temperature Limit registration is behaviorally inert during a forced incomplete-compensation run.
- [ ] Confirm Blueprints Expanded is absent from the catalog, public support claims, and packaged output.
- [ ] Confirm the formal repository review has no unresolved, non-deferred P0-P2 finding.

Only then may the implementation be described as complete. Release preparation, issue creation, repository metadata edits, commits, pushes, and publication remain separate authorized operations.
