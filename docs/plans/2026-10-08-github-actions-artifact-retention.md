# GitHub Actions artifact reduction implementation plan

Date: 8 October 2026.
Status: proposed draft for owner review.
This planning request does not approve implementation or configuration changes.
Decision and integration owner: Maksym Shostak.

## Purpose and governing baseline

Remove GitHub Actions artifact storage as a prerequisite for the current ONI CodeQL completion gate while preserving proof that every selected language analyzed the exact selected source in the same run attempt.
Keep native SARIF as files and keep native CodeQL result upload intact.
Do not weaken security analysis to remove three very small archives.

The inspected local and remote `main` revision is `0572b26b61eda236cdfb5886f0e032cfd1a5fc0b`; the working tree was clean before this draft.
The authenticated GitHub API reports `MaksymShostak/oxygen-not-included` as public, with default branch `main`.
The local repository also has an `upstream` remote; this plan concerns the owner's `origin` only.

Current constraints come from [AGENTS.md](../../AGENTS.md), the [accepted proportionate CI plan](2026-10-06-proportionate-github-actions.md), its [delivery record](../reviews/2026-10-06-proportionate-ci-delivery.md), the [current selector and gate](../../tools/ci/checks.py), and [release preparation](../../tools/oni-mod-pipeline/manual/preparing-releases.md).
The older CI plan/delivery record describes its historical Markdown tooling versions; current installed tooling and locks, rather than those historical version statements, govern new verification.
Requirements, criteria, scenarios and decisions below are proposed amendment records, not assertions of owner acceptance.

Reference inputs and implementation lessons:

| Reference                       | Exact identity and use                                                                                                                                                                                                                                                                                                                                                                                    |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BBCode artifact-retention plan  | `C:\Users\maksy\GitHub\steam-community-bbcode\docs\plans\2026-10-07-github-actions-artifact-retention.md`, SHA-256 `66ffabae4aea8ed5fdfeb6ebf98240ab8359065269332e4dd1131c9a609d6c7e`. Adapt consumer-first analysis, native evidence and exact approval scope.                                                                                                                                           |
| Delivered BBCode implementation | [PR #18](https://github.com/MaksymShostak/steam-community-bbcode/pull/18), source `4cb54e7697471a482668722ead5aa96db0e65682`, normal merge `3c33db288e1eb59032446adb2238ecf9765f0a66`. Reports moved to protected logs; same-run package bytes and slim publication files stayed as files. Reporter review repaired lost siblings, missing-record handling on failure and lost malformed/raw diagnostics. |
| Other producer handoff          | `C:\Users\maksy\Desktop\github-actions-artifact-handoff-2026-10-07.md`, SHA-256 `5016f5cfc0af347ca07cb6863c6a02bd12fb649f9930394fc53fed606c15b52b`; producer candidate `a02e8435646ef1573dfa14c6b4bf87f9fc033ca6`. Same-cohort dependency files moved through bounded native outputs; reports moved to logs; actual release payloads/checkpoints stayed as files.                                         |

The producer's zero-artifact hosted run had a cancelled lane and failed aggregate; its later local pass does not establish complete hosted acceptance.
BBCode's passing implementation/reviews are useful design evidence, not ONI verification.
Do not copy either producer's release windows, compressed lock protocol, private-runner gate or shared package archive into a workflow with a different consumer contract.

HISEW applicability is active/personal for the explicitly selected ONI checkout.
The retained state is `handoff-committed`; another execution requires its own accepted scope.
The existing focused profile runs pipeline tests; affected adds mod validation/build; full adds mod tests with the existing child-only compiler-server settings.
None declares path coverage/input ordering or runs the Python CI contracts automatically.
Do not confuse a full product profile with proof for the changed CodeQL orchestration.
No execution, profile mutation, task adoption, reviewer dispatch or hosted run is part of this planning request.

## Current artifact inventory and actual consumers

Observation time: **7 October 2026, approximately 21:44 UTC** (8 October locally).
The complete Actions inventory returned one page, 27 records reported as non-expired, totaling **110,563,508 bytes**, approximately **105.44 MiB**.
Provider-reported compressed archive bytes are not an account billing statement or a measure of local `artifacts/`.

| Family                                                         | Count |       Bytes | Origin and current role                                                                                                                                                       |
| -------------------------------------------------------------- | ----: | ----------: | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Historical `bbcode-*` reports/mutation bundles                 |    22 | 110,448,571 | September 9-10 runs on the historical `steam-community-bbcode` branch. Approximately 105.33 MiB; current `main` has no BBCode workflow. Existing records expire October 9-10. |
| Historical `sarif-artifact-csharp` and `sarif-artifact-python` |     2 |     114,063 | July 20 run on historical `master`; expiry October 18. These are genuine SARIF files, preserved under the owner's file-retention preference.                                  |
| Current `oni-codeql-<run>-<attempt>-<language>`                |     3 |         874 | One JSON completion record per Actions/C#/Python lane; one-day retention. The parent completion job downloads and validates them before emitting `completion_sha`.            |

The three current records were created by [successful ONI run 37539075048](https://github.com/MaksymShostak/oxygen-not-included/actions/runs/37539075048) at the inspected source.
Actions/Python records are 294/290 compressed bytes and the C# record is 290 bytes.
All ten jobs concluded successfully, including each language, dependency submission and `Required ONI checks`.
Their expiry timestamps are **7 October, 22:13-22:15 UTC**, still in the future at the stated observation time; inventory values must be refreshed before implementation or cleanup.

A later [Dependabot PR run 37610739863](https://github.com/MaksymShostak/oxygen-not-included/actions/runs/37610739863) failed selection and the required gate; its application/documentation/CodeQL/dependency consumers were skipped.
That is not a failed CodeQL transport observation and does not disprove the successful baseline run.
Do not repair that separate PR while implementing this retention amendment without its own accepted scope.

Current tracked authored workflows are only:

| Workflow                                                                         | Existing archive behavior                                                                                                                                                                                                                    | Proposed treatment                                                                                                               |
| -------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| [ONI checks](../../.github/workflows/oni-checks.yml)                             | No direct upload/download. Selects consumers on PR/main push, manual dispatch and Sunday schedule; calls CodeQL and evaluates exact completion identities.                                                                                   | Preserve event selection, product/documentation checks, trust and stable gate. Its called CodeQL becomes artifact-free.          |
| [CodeQL](../../.github/workflows/codeql.yml)                                     | Selected language matrix analyzes source and uploads native results; each successful lane archives a five-field JSON record for one day. `complete` downloads records matching the same run/attempt and rejects missing/stale/extra records. | Replace only completion-record transport with independently named native job outputs. Preserve SARIF/native analysis and upload. |
| [NuGet dependency submission](../../.github/workflows/dependency-submission.yml) | No artifact transfer. Publishes a validated graph with locked restore, selected-source freshness checks and serialized publication.                                                                                                          | Unchanged; dependency submission is an external graph operation, not a report bundle.                                            |

GitHub's workflow API also lists provider CodeQL, Dependency Graph, Automatic Dependency Submission and Dependabot entries, plus the historical BBCode workflow.
Registry presence is not proof that a workflow file exists on current `main` or that an automatic feature is currently enabled.
The exact Git tree confirms the BBCode file is absent; the prior delivery records a separately approved automatic-submission setting change.
This draft neither removes historical branches/workflow entries nor changes those settings.

The only current authored Actions archives are therefore **three tiny but currently necessary same-attempt completion records**.
Removing their upload alone would break the gate; logs/summaries alone are not a native cross-job input.
Most listed storage will expire naturally and is not ongoing report creation by current `main`.
The case for this amendment is removing a storage dependency and unnecessary archive operations, not claiming a major present-day storage leak.

## Selected design and risk route

**Proposed risk class: R2.**
The change alters security-analysis orchestration and completion evidence; a lost language, stale output or wrongly successful aggregate could let protected delivery proceed without selected analysis.
No application, conversion, mod ABI or Steam publishing change is intended.
Reversibility is a reviewed Git revert restoring future receipt upload/download; it cannot restore expired provider records or rerun an original analysis retroactively.

Required lifecycle evidence: exact accepted amendment/configuration scope, native route/execution, CI-contract regression proof, independent orchestration/security review, final native verification with its coverage limits, hosted language/output inventories and native handoff.
Use the current external HISEW evidence destination for personal records, rediscovered before implementation receipts.
Never place personal receipts into immutable ONI release candidates or treat older task authority as a new execution/publication approval.

| Decision | Proposed choice and rationale                                                                                                                                                                                                                                       |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DEC-001  | Remove completion-record upload/download after replacing its consumer contract. Current authored ONI checks then create zero Actions archives, including selected CodeQL runs.                                                                                      |
| DEC-002  | Three explicitly named language call jobs (`analyze-actions`, `analyze-csharp`, `analyze-python`) each expose their own completion output through one native reusable single-language workflow. No matrix output merging or last-completer aggregation.             |
| DEC-003  | Carry the existing five receipt fields as compact single-line JSON, bounded to 4 KiB UTF-8 per language. The gate validates exact selected language set, native job results, source, policy digest, run and attempt before returning the existing `completion_sha`. |
| DEC-004  | Preserve independent selection/recomputation, trusted/same-repository eligibility, CodeQL action pins, `build-mode: none`, `tools: linked`, categories and native security-events upload. SARIF stays as files; no full-SARIF log dump.                             |
| DEC-005  | No historical deletion, branch retirement, workflow disablement, schedule/event/path change, dependency/runtime update, report toggle, alternate storage service or product/release schema migration.                                                               |
| DEC-006  | Use GitHub's native reusable workflows/job outputs, existing Python standard-library JSON/Git validation and the existing test suite. No copied AGPL implementation, new package, transport shim or compressed binary protocol.                                     |

GitHub warns that matrix output name collisions can overwrite earlier values, and reusable matrix outputs select the last successful value.
A single `receipt` output from the existing matrix cannot prove all languages.
[Job outputs](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idoutputs), [reusable workflow outputs](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows#using-outputs-from-a-reusable-workflow) The fixed language call jobs retain independent parallel analysis with no added runner for a wrapper; each invokes one native analysis job, and the existing validator/completer still run.
Do not claim runner-minute savings before measurement.

Native outputs have a 1 MB per-job limit estimated in UTF-16 and may be suppressed when GitHub considers them secret-bearing.
A bounded five-field record is far smaller; a missing/redacted output still fails rather than falling back to logs or another run. [Output limits and behavior](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idoutputs) The workflow/data contract is repository-specific; GitHub owns expression, output and nested-workflow behavior, so hosted proof is necessary.

Logs and summaries do not count toward artifact allowance; cache storage is separate from artifacts/Packages. Visibility, actual billing entitlement and accumulated storage remain separate observations. [Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions) This plan makes no claim that the observed 105.44 MiB consumes a private-repository allowance or that expiration reverses prior accrued usage.
Its current-run sample avoids only **874 archive bytes across three selected languages**; the operational benefit is independence from archive availability/expiry.

The ONI repository is MIT; the reference producer and BBCode implementation have AGPL boundaries.
Reuse their design lessons through an independent standard-library implementation, not literal copied code or invented license clearance.
Preserve existing native tooling and third-party notices.
Any new package/action/runtime adoption requires separate current support/version/rights research and exact approval.
Existing local npm is 12.2.0, while the ONI manifest currently declares npm 12.0.2; do not silently amend that setting or substitute an older cached executable to claim conformance.
Record the actual runtime used and resolve any enforced engine mismatch through a separate exact decision before qualifying implementation.
No npm-policy change is proposed here.

## Proposed requirements and acceptance criteria

| Requirement                                    | Acceptance criterion                                                                                                                                                                                                                                                          |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-001: eliminate archive transport           | AC-001: the authored workflow tree contains no upload/download-artifact step, and changed complete ONI runs with all selected languages have zero Actions artifacts.                                                                                                          |
| REQ-002: preserve complete same-attempt proof  | AC-002: each selected language independently returns the exact five-field record and native successful job result; completion rejects absent, duplicated, unexpected, malformed, oversized, wrong-source/policy/run/attempt or redacted records.                              |
| REQ-003: preserve trust and security semantics | AC-003: unchanged selection/eligibility and per-checkout validation precede analysis; no selected analysis failure/cancellation/skip or evidence failure can return `completion_sha`; native SARIF and upload/category behavior remain unchanged.                             |
| REQ-004: keep diagnosis honest and bounded     | AC-004: completion logs/summaries identify all selected results and every available record, with explicit missing/invalid evidence; one bad record does not hide readable siblings. No untrusted output is interpolated into shell code or interpreted as a workflow command. |
| REQ-005: preserve product/recovery files       | AC-005: local immutable candidates, runtime packages, TRX/test evidence, content manifests, installed acceptance/release receipts, proprietary inputs and historical recovery records are not deleted or transformed.                                                         |
| REQ-006: demonstrate actual delivery           | AC-006: local contracts/review/native product evidence and hosted action results reference their exact candidates; genuine single/subset/all-language paths, skips/failures and zero archives are distinguished from simulations and unobserved scenarios.                    |

## Exact proposed configuration scope

[AGENTS.md](../../AGENTS.md) requires explicit approval for exact configuration changes.
Owner acceptance of a concrete revision of this table is the proposed implementation scope; drafting this document is not that approval.
Likely implementation paths are predictions, not permission to broaden behavior or settings.

| File and setting                                                                       | Proposed exact effect                                                                                                                                                                                                                                                                                              | Existing invariants                                                                                                                                                                                                                                                                                            |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.github/workflows/codeql.yml`: language analysis topology                             | Replace `jobs.analyze` matrix with the three explicit language call jobs, each dependent on successful `validate`, enabled only for that validated selected language, calling `./.github/workflows/codeql-language.yml` with the same plan and one constant language.                                              | Keep current workflow-call plan input, validation job, validated language output, Ubuntu 24.04 runtime and read/security-event permissions. No additional trigger or credentials.                                                                                                                              |
| `.github/workflows/codeql.yml`: `complete.needs`, environment and completion transport | Depend on validator and all three explicit call jobs; retain `if: always()`. Remove artifact download and pass each native result and receipt through independently named environment values to `codeql-complete`. Preserve `complete` job ID and workflow output `completion_sha`.                                | Validator must succeed; selected language results must succeed. Unselected calls are skipped with no receipt. Stable parent `Required ONI checks` still evaluates the returned tested revision.                                                                                                                |
| New `.github/workflows/codeql-language.yml`: native reusable workflow                  | `workflow_call` only, required string inputs `plan` and `language`, one bounded string receipt output. One analysis job checks out/validates the actual selected source and language, runs existing init/analyze actions, then emits the completion record after native analysis success. No upload-artifact step. | Preserve existing checkout and CodeQL pinned action commits, disabled credential persistence, full fetch, 20-minute deadline, Ubuntu 24.04, `build-mode: none`, `tools: linked`, per-language category and native SARIF upload. Use constant action language values from validated enum; no inherited secrets. |

Source changes: `tools/ci/checks.py` changes only `codeql-receipt` production and `codeql-complete` input/validation/diagnosis; `tests/test_ci_checks.py` adds independent positive/negative transport and actual workflow-mutation contracts.
The existing selector operation names remain accurate: a receipt is now transmitted as an output rather than archived as a file.
Do not retain the file-download path as an alias/fallback or introduce a second authority graph.
The existing `policy_sha256` remains SHA-256 of selected committed `tools/ci/checks.py`; this plan does not redefine it as a signed attestation or hash of the entire workflow tree.
Source revision still identifies the committed workflow inputs, and `.github/workflows/**` changes continue to select conservative full checks through the existing classifier.

No change to `oni-checks.yml`, `dependency-submission.yml`, package/lock files, `.markdown-quality.json`, `.gitignore`, `global.json`, mod configuration, CodeQL settings/rules, repository policy, branch protection or HISEW profiles is proposed.
If implementation discovers a necessary setting outside this table, present its exact effect before mutation.
The existing profile gap below is a distinct configuration decision, not implicit authority under workflow acceptance.

## Completion contract and diagnostics

Keep the same exact record fields:

| Field           | Required meaning                                                                               |
| --------------- | ---------------------------------------------------------------------------------------------- |
| `language`      | One of `actions`, `csharp`, `python`, matching the independently named job and selected plan.  |
| `revision`      | Exact selected/tested lowercase full commit SHA.                                               |
| `policy_sha256` | Exact selected policy digest already computed by the native selector.                          |
| `run_id`        | Current GitHub run identity as a string, not an identity borrowed from another run.            |
| `attempt`       | Current GitHub attempt as a string. Do not mix previous successful outputs into a new attempt. |

Before emitting the output, the child uses existing native selection recomputation/validation against its checkout and requires its analysis step's actual successful outcome.
Unknown/unselected languages, ineligible analysis and unsuccessful steps produce no accepted completion record.
The output is compact UTF-8 JSON on one line, bounded to **4 KiB per language**, with no newline, field coercion or optional extra fields.
Producer identifiers must validate to their native fixed formats before writing the environment file.

The completer validates the complete fixed set of three result/receipt slots, then selects the exact required set from the recomputed plan.
Every selected slot requires result `success` and a strict five-field record; reject duplicate JSON member names, unexpected keys, non-object values, unknown language, wrong types or mismatching identity.
Every unselected slot must be skipped without a receipt; an unexpected executed/completed language is a contract failure, not additional invisible qualification.
Missing or redacted outputs fail closed.
No fallback artifact, previous-run search, cached receipt, independent re-analysis or log scraping is permitted.
Native GitHub outputs and job results establish the same workflow execution boundary; neither the policy digest nor output text is independent authentication.

Complete all readable diagnostic observations before raising an aggregate evidence error.
Report every selected slot's native result and record identity; if one JSON record is malformed, preserve safe bounded raw text and still report valid siblings.
Report missing records on both successful-looking and failed language paths; a failed stage is not a reason to omit the missing evidence.
Native Python exceptions/non-JSON errors remain visible, and writing a summary must not overwrite the failed exit status.
The unchanged CodeQL action owns its actual analysis diagnostics and SARIF validation; do not invent a new SARIF parser or restate action success as alert-free code.

Receipt values contain controlled identity fields only; no provider tokens, arbitrary paths, raw SARIF or child transcript belongs in the output.
Pass output text through environment values, not `${{ ... }}` interpolation inside executable shell text.
Summaries list controlled identifiers/results and explicit limitations, with a proposed **16 KiB UTF-8 per completion summary** ceiling.
If showing malformed/raw candidate text, suspend workflow command interpretation with a fresh cryptographically unpredictable token, restore in `finally`, and escape summary markup. [Workflow commands](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands) Do not add a broad reporter that recursively prints runner directories; this repository has no current report bundle that needs that replacement.
Invalid/oversized evidence is an explicit failure, not silent truncation or an archive fallback.

Partial reruns deserve an explicit contract test: previously successful selected jobs can retain older attempt outputs.
Those records cannot satisfy the current-attempt contract.
If selected records span attempts, fail and require rerunning the complete selected CodeQL set plus its completer.
Preserve the existing exact-attempt assurance rather than accepting stale lanes for convenience.
Do not claim that rerunning only a completer is a valid recovery path.

## Quality scenarios and proof

| Scenario                                  | Stimulus and required response                                                                                                                                                                                           |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| QA-001: all languages                     | Full selected run completes Actions/C#/Python in varying orders. Each named output survives independently, the completer returns the selected SHA, the outer required gate succeeds and run artifact inventory is empty. |
| QA-002: one language/subset               | Existing selection chooses Python only or Python+C#. Only those calls execute, others skip without outputs, exact required records pass and no archive is created.                                                       |
| QA-003: missing/failed/cancelled language | One selected call fails, cancels, skips unexpectedly or has suppressed/missing output. No completion SHA is admitted, all available sibling identities remain diagnosable and the outer gate fails.                      |
| QA-004: stale/corrupt records             | Wrong revision/policy/run/attempt, duplicated JSON member, unexpected key/language, 4 KiB overflow or mixed-attempt partial rerun is rejected. Records before and after the invalid slot still appear in diagnostics.    |
| QA-005: ineligible/unselected analysis    | Fork/untrusted eligibility is unchanged; a forged plan or direct unselected/unknown-language child call fails native validation before analysis. Docs-only changes continue to skip CodeQL and need no output.           |
| QA-006: hostile diagnostic text           | Malformed record contains workflow commands, quotes, backticks, HTML or line breaks. It does not become shell code/runner commands or unescaped summary markup; failure remains nonzero.                                 |
| QA-007: interrupted analysis              | A cancelled attempt lacks complete output. Existing logs remain historical, no earlier receipt is substituted and fresh evidence belongs to a new complete selected attempt.                                             |
| QA-008: security/release invariants       | Analysis language/categories/SARIF upload are unchanged; ONI product and documentation checks still pass their existing contracts. Local release candidates and accepted installed bytes are untouched.                  |

## Vertical implementation slices

The owner/integrator owns all slices.
Each slice has a complete observable consumer path, including rejection behavior, before its transport is removed.
Do not land archive deletion as a standalone incomplete change.
SLICE-002 integrates the native output producer/validator from SLICE-001; SLICE-003 qualifies the integrated candidate.
This plan grants no delegation or reviewer-dispatch authority.

| Slice                                                 | Traceability                                                              | Demonstrable outcome and falsifiable proof                                                                                                                                                                                                                                                                                                                                                                                                | Delivery/recovery implication                                                                                                                                                                                         |
| ----------------------------------------------------- | ------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SLICE-001: native bounded completion output           | REQ-002/003/004; AC-002/003/004; QA-003/004/005/006; DEC-003/004/006      | Add the output producer and complete-set validator at the existing Python seam. Exercise actual `GITHUB_OUTPUT` bytes, independent expected identities, duplicate/type/size rejection, all language subsets, malformed middle slot with valid siblings, and native failure/same-attempt enforcement. Existing transport remains until integration; no accepted fallback path is introduced.                                               | Freeze a tested native contract without claiming hosted output behavior. Historical receipt bytes/schemas remain history.                                                                                             |
| SLICE-002: archive-free CodeQL call topology          | REQ-001/002/003/004; AC-001/002/003/004; QA-001..007; DEC-001/002/003/004 | Integrate the three explicit call jobs and one native single-language reusable workflow; remove receipt archive upload/download and directory environment plumbing in the same patch. Mutation tests against actual YAML reject omitted language bindings, weakened result/identity checks, altered trust/permissions/categories, output overwrite topology and reintroduced artifact calls. Selection/gate Git-history tests still pass. | One complete security-consumer path is reviewable/reversible. Keep public gate/workflow output contract stable; language check display topology changes are declared, not hidden.                                     |
| SLICE-003: exact-candidate qualification and delivery | REQ-001..006; AC-001..006; QA-001..008; DEC-001..006                      | Freeze integrated source; complete local CI/Python/native Markdown and route-selected product verification, independent orchestration/security review and hosted all-language/subset/docs-only observations. Read every selected job result/output behavior and zero-artifact inventory; separate hosted failures from local fault injection and pending real scenarios.                                                                  | Use separately authorized commit/push/PR delivery, preserve exact evidence before expiry and complete native HISEW handoff. Observer aborts/reverts on missing analysis, unsafe diagnosis or falsely successful gate. |

Focused command: repository `.venv` Python runs `-m unittest discover -s tests -p test_ci_checks.py`.
Run the broader repository Python suite when the native helper/test surface changes, including existing selector/fork/rename/dependency freshness contracts; Windows-specific skips are not Linux passes.
The existing Linux control job exercises CI contracts when changed control and Python paths are selected.
Tests must validate actual orchestration, not merely compare duplicated expected YAML strings or a helper's own constants.
Use existing native JSON/Git parsers and actual temporary Git histories; fake action results only at the external failure boundary.
GitHub owns nested reusable-workflow/output semantics, so local mutation tests cannot replace hosted execution.

**Verification gap:** current HISEW focused/affected/full profiles execute product pipeline/mod commands, not `tests/test_ci_checks.py`, and have undeclared path coverage.
Before engine-qualified completion, either obtain the engine-supported admission of supplemental CI-contract evidence or obtain exact approval for a profile amendment that includes the necessary CI/Python commands and declares appropriate coverage.
Do not silently modify profiles or manufacture a generic full receipt as CI assurance.
Retain the current full product profile's compiler-server settings and proprietary-game qualification limits; do not rerun unrelated full mod/game checks merely for drafting or commit ceremony.
The accepted route determines final breadth after the coverage gap is resolved.

Independent review receives the frozen patch/tree, source and reference hashes, actual runtime identities, per-language contract, historical successful run and all local negative-case outputs.
Review must examine matrix-output loss, selected skips, nested permissions, current-attempt semantics, shell/output injection and preserved SARIF behavior.
Refresh affected evidence after review fixes; reuse still-valid evidence for unchanged inputs.
Local verification, independent review, hosted CI, native handoff, main integration and release/Steam publication remain separate boundaries.

## Rollout, migration, observability and recovery

Before implementation, accept the exact amendment/configuration scope and resolve native verification coverage.
Recheck checkout ownership, retained user changes, HISEW state and current producer/consumer definitions against this baseline.
A new execution does not adopt an old handoff, and this planning request does not inherit prior BBCode commit/push/merge authorization.

The record content stays the same five-field identity; only transport and consumer input change.
No data backfill or migration of historical completion archives is needed.
No local file-reader shim/fallback remains after integrated cutover; the source change updates producer, consumer and tests together.
Future languages require explicit new named output/result bindings and updated native validator tests; unknown languages fail rather than silently disappearing.

First hosted proof should exercise a trusted control-changing PR that conservatively selects all languages, followed by exact accepted main/full/manual execution under separate delivery/dispatch authority.
Do not add a new dispatch language selector or weaken classification to manufacture subset evidence.
Observe genuine Python/C# subset and documentation-only cases when available; record any still-pending hosted scenario and its local proof honestly.
A failed/cancelled hosted output case is useful only if it actually reached the changed CodeQL path; selection failure is a different boundary.

Required signals: validated selected language set; each named call's native result; per-language source/policy/run/attempt; completer status and emitted SHA; stable outer gate conclusion; native code-scanning result availability; per-run artifact count **zero**.
Capture actual tested checkout SHA separately from PR head when GitHub uses a merge checkout.
Local/hosted evidence references must identify the exact attempt, not only the mutable branch name.
No success claim is allowed for a partial/cancelled aggregate or for provider code-scanning results from an older revision.

The integration owner observes initial actual PR/main usage and natural historical expiry, without scheduling an automation.
Current routine storage reduction is tiny; measure avoided upload/download steps and output reliability rather than promising substantial GB or minute savings.
Logs/summaries follow provider retention; removal of one-day artifacts does not promise a particular log retention window.
Retain needed review/delivery references externally through the configured workflow before provider expiry.

Abort or forward-fix if one selected language vanishes, a job/output is overwritten or suppressed, the gate accepts mixed-attempt data, permissions/category/SARIF behavior changes, diagnostics are unsafe/incomplete, or native/hosted verification fails.
Preserve failed outputs and sibling evidence before fixing.
A reviewed revert restores the original matrix and matching one-day archive transfer, with producer/consumer/tests reverted together and qualification of that candidate.
Reversion restores future behavior; expired original artifacts and lost runner state remain unrecoverable.
Re-executing analysis creates new evidence for a new attempt, never a reconstructed original receipt.

No existing Actions artifact is deleted by this plan.
Historical BBCode bundles expire naturally October 9-10; SARIF remains untouched through its existing expiry.
An explicit owner-approved cleanup could reclaim old records sooner, but it is separate destructive scope and does not undo accrued usage.
The historical BBCode branch must not be merged into ONI main or revived as an ONI package publisher.

Local `artifacts/release-candidates/**` is a separate immutable product/recovery contract: Workshop runtime content, uploader text, manifests, native TRX evidence, source/provenance and installed acceptance/readiness records have real consumers.
Keep those files, hashes and acceptance bindings intact; moving them to logs would break validation/recovery.
Do not print proprietary game binaries, local machine/Steam identities, credentials, full game logs or immutable candidate trees while diagnosing this CI change.
No ONI installation, game run, release candidate preparation, Steam upload, package publication, source retirement or historical branch cleanup is authorized.

## Unknowns, cheap experiments and re-planning

| Question                                                                 | Cheapest discriminating evidence                                                                                                  | Required response                                                                                             |
| ------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| Do named nested outputs preserve every selected language?                | Hosted all-language run with independent source-bound records and actual call results; local fixtures vary completion ordering.   | Fail if a selected output is missing. Revise native topology rather than fall back to last matrix output.     |
| What happens on partial reruns/output suppression?                       | Local same-attempt and empty-output tests; actual hosted rerun/failure when separately authorized and useful.                     | Mixed attempts fail; rerun the complete selected set. State unobserved provider behavior explicitly.          |
| Do external/manual tools consume completion archives?                    | Repository search shows only `codeql-complete`; owner identifies any external reader and its exact record purpose before removal. | Re-plan only that real consumer; no generic retention input.                                                  |
| Does new nested topology affect required statuses?                       | Read existing required-check settings without mutation and verify retained `Required ONI checks`/workflow output identity.        | Any required check-name setting change needs its own exact proposal.                                          |
| Can HISEW admit the relevant Python/workflow evidence?                   | Native profile coverage/input inspection plus CI test discovery.                                                                  | Resolve through supported supplemental evidence or separately approved profile scope before engine assurance. |
| Does current npm declaration prevent reproducible implementation checks? | Actual installed/hosted versions and native engine behavior, without installation/configuration mutation.                         | Resolve incompatibility as a distinct tooling decision; do not silently downgrade/repin.                      |

Re-baseline if current main/workflow selection, fixed language inventory, CodeQL action behavior, completion identity, artifact consumers or required-check settings materially change.
Re-plan for any new report/file consumer, binary cross-job transfer, broader release payload, retention change, model/provider operation, runtime/dependency adoption, event filter, private-runner policy or security-rule/SARIF treatment.
The higher outcome is complete, honest security qualification with the least transport required for its actual consumer; saving bytes cannot justify losing analysis proof.

## Readiness record

This draft supplies current storage attribution, the real completion consumer, exact native-output design/configuration scope, three vertical slices, measurable rejection/hosted proof and bounded recovery.
Implementation prerequisites remain exact owner acceptance, native verification coverage and confirmation of any external completion-file consumer.
Only this plan is created by the current request; CI/configuration, source/product files, historical archives, provider settings and remote state are unchanged.
