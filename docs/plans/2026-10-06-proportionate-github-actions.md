# Proportionate GitHub Actions for ONI

Status: implementation plan with design decisions accepted through grilling, 6 October 2026.
The owner requested a comparison with OwlAPI and Universal Ontology, established the `docs/` documentation-only boundary, and settled the decisions below.
This document records those decisions; implementation, remaining exact configuration changes, repository settings, commits and publication retain their separate authority requirements.

Accepted tooling decisions: adopt `@hadden-industries/markdown-quality` at coordinated version `1.0.2`; include `docs/reviews/delivery-temperature-limit-steam-copy-review.md` without a special exclusion; use repository Node compatibility `>=24.21.0`.

The original coordinated selection was `0.1.0-alpha.4`.
The owner approved exact corrective adoption of `1.0.2` after public Windows/Linux qualification and a complete 40-document external ONI check demonstrated that the fenced-code and inline-code preservation defects were resolved.
This supersedes the original tuple without changing third-party dependencies, policy scope or code literals.

The selected package separately supports Node `>=24.21.0 <25`, so its consumers must use a qualified Node 24 runtime.
The repository minimum does not establish qualification of this tool on later Node majors.

Decision owner: Maksym Shostak.
The implementing task owns integration and evidence preparation.
Work should use the existing checkout and preserve unrelated changes.

## Accepted grilling decisions

| Decision                            | Accepted outcome                                                                  | Consequence                                                                                                                                                                                                                    |
| ----------------------------------- | --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Q1 Optimization scope               | Keep the full fine-grained plan                                                   | Deliver Python/`.NET` separation and individual CodeQL language selection as well as docs-only routing and dependency selection.                                                                                               |
| Q2 Markdown coverage                | Cover every repository-authored Markdown document                                 | Include the PR template and mod-test provenance READMEs as well as root documents and `docs/`; application-check selection still follows actual consumers.                                                                     |
| Q3 Snapshot freshness               | Allow a truthful older source SHA when its dependency inputs match current `main` | A later docs-only commit must not discard a dependency update; reject superseded dependency states and control publication ordering.                                                                                           |
| Q4 Activation evidence              | Use bounded qualification with explicit limits                                    | Exercise every family locally; require hosted actual control-change, genuine docs-only, full CodeQL and dependency-publication evidence. Other hosted scenarios remain explicitly pending until genuine changes exercise them. |
| Q5 README producer ownership        | Align `sync-readme` with the selected formatter                                   | Extend pipeline scope and its dependency inventory; prove one canonical result while preserving authored bytes outside the generated block.                                                                                    |
| Q6 Markdown baseline                | Include meaning-preserving format, lint and citation repairs                      | Preserve substantive claims, code literals and historical context; diagnose preservation refusals, never force output or suppress checks, and block documentation-check activation if safe remediation cannot be demonstrated. |
| Q7 README audience structure        | Root repository hub with a mod-local synchronized README                          | Use the approved `mods/delivery-temperature-limit-supercooled/README.md` profile destination; keep detailed contributor recipes in existing guides.                                                                            |
| Q8 Steam compatibility presentation | Retain all six graphical badges                                                   | Supply meaningful Markdown alternatives; no badge removal or lint exception.                                                                                                                                                   |
| Q9 Converter boundary               | Retain the source-faithful converter contract                                     | The proposed image-label and heading-placement extensions were withdrawn; keep the existing package/version and no additional AST dependencies.                                                                                |
| Q10 Residual consumer gap           | Keep the small, consumer-owned badge-alt adapter                                  | Native checker coordinates identify the six known images inside the managed block; explicit labels are supplied, unknown images fail, and no descriptions are guessed.                                                         |

The owner additionally selected Node compatibility `>=24.21.0` and approved the exact profile destination change and formatting-only `AGENTS.md` proposal.
Source heading inspection showed the six description sections belong beneath the mod title: use Steam-supported `[h2]` pairs directly, so the existing converter emits `##` without a custom heading rewrite.
These accepted decisions close the current grilling frontier.
New consequential choices require fresh evidence and the same bounded decision process; do not reopen settled choices as routine implementation questions.

## Outcome and inspected baseline

Run checks whose inputs changed, report the coverage honestly, and preserve a conservative fallback.
`docs/**` is a documentation boundary: changes there must not restore NuGet packages, start the Windows application-test job or select mod/security-analysis consumers.
Documentation changes select their own formatting, lint and local-link controls.
Product, test, dependency and workflow changes outside that boundary must continue to receive their applicable assurance; mixed changes take the union.

The inspected ONI baseline is `4d6c1feb651d1c865d8c75daac8a6e8821e91f9f` on `main`, matching the live remote.
The only file introduced by that commit was `docs/reviews/delivery-temperature-limit-steam-copy-review.md`.

| Observed run                                                                                                                  | Work actually performed                                                                             | Observation                                                                                            |
| ----------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| [ONI pipeline tests, 37396720171](https://github.com/MaksymShostak/oxygen-not-included/actions/runs/37396720171)              | Windows checkout, `.NET` setup, locked restore, pipeline tests, Python unit tests                   | Job lasted 104 seconds; locked restore took 26 seconds and `.NET` tests took 52 seconds. It succeeded. |
| [Automatic Dependency Submission, 37396722440](https://github.com/MaksymShostak/oxygen-not-included/actions/runs/37396722440) | Restored the pipeline source/test projects and mod source/test projects; submitted a NuGet snapshot | Separate job lasted 24 seconds. It succeeded despite no dependency input changing.                     |
| CodeQL                                                                                                                        | No run for this commit                                                                              | Existing Markdown trigger exclusion worked.                                                            |

These are observations from one commit.
Concurrent durations are not additive critical-path savings, and runner time is not a measured bill reduction.

Current hosted CI exercises the pipeline tooling and Python contracts, principally with temporary fixtures.
It does not run the mod's separate automated project, compile the mod against ONI assemblies, validate every current content file, or perform in-game acceptance.
This plan preserves that boundary; a green gate must not claim those additional outcomes.

The current API readback found no protection on `main` and no applicable branch rules.
Recheck before delivery; do not change branch rules as part of this proposal.

ONI currently has no Markdown formatter/linter command, Markdown policy or documentation-quality workflow. `package.json` exposes only the pipeline commands, and `.gitattributes` enforces LF text handling rather than Markdown quality. The documentation consumer below is therefore a proposed addition, not a claim that an existing check is being retained. The owner has selected its package/version and review scope; the exact configuration changes listed below remain subject to approval before implementation.

## What the comparison repositories establish

Both local reference checkouts matched their live `main` refs on inspection.
Their workflow and selector files were read without modification.
OwlAPI has unrelated working-tree documentation changes; those are outside this task and are not treated as delivered CI behavior.

### Universal Ontology: select consumers, then verify completion

Inspected delivered revision: `8efcba8957b91d0eb4a35f8b766ab3c9d5d3f6b6`.

- [The selector](https://github.com/Hadden-Industries/universal-ontology/blob/8efcba8957b91d0eb4a35f8b766ab3c9d5d3f6b6/scripts/selectPullRequestChecks.js) owns an explicit input inventory for documentation, Python, ontology, website and distribution consumers.
  The inventory includes source, tests, fixtures, runners, dependencies and execution controls.
- [PR validation](https://github.com/Hadden-Industries/universal-ontology/blob/8efcba8957b91d0eb4a35f8b766ab3c9d5d3f6b6/.github/workflows/pr-validation.yml) selects jobs before installing their dependencies.
  Its validated plan is tied to the tested revision.
- [The final evaluator](https://github.com/Hadden-Industries/universal-ontology/blob/8efcba8957b91d0eb4a35f8b766ab3c9d5d3f6b6/scripts/evaluatePullRequestChecks.js) rejects missing, failed, cancelled or unexpectedly skipped required consumers, and rejects absent or stale completion identities.
  Reusable consumers verify their internal jobs as well.
- In [PR run 37330210805](https://github.com/Hadden-Industries/universal-ontology/actions/runs/37330210805), development/style/documentation work ran while Node product, ontology, website and distribution jobs were skipped.
  This demonstrates consumer separation; it is not a claim that the run was a pure review-only change.
- Its [CodeQL workflow](https://github.com/Hadden-Industries/universal-ontology/blob/8efcba8957b91d0eb4a35f8b766ab3c9d5d3f6b6/.github/workflows/codeql.yml) selects a language matrix from source and execution inputs.
- Its core `main` push, schedule and manual plans currently select full qualification.
  ONI must deliberately choose a different push policy to address direct documentation commits.

### OwlAPI: reuse proof only after proving equivalence

Inspected delivered revision: `073beefb7805130bc0452472d1a9c801471fbaf1`.

- [CI](https://github.com/Hadden-Industries/owlapi/blob/073beefb7805130bc0452472d1a9c801471fbaf1/.github/workflows/ci.yml) can reuse successful PR qualification after independently proving an equivalent ordinary main merge.
  Receipts bind source, tree, workflow, run, artifact and qualification identities; unavailable proof falls back to full work.
- [The applicability observer](https://github.com/Hadden-Industries/owlapi/blob/073beefb7805130bc0452472d1a9c801471fbaf1/scripts/ci-check-applicability.mjs) is explicitly observation-only.
  Its two narrative exclusions do not authorize skipping Java or WebVOWL; both remain required.
- [Its current input-aware plan](https://github.com/Hadden-Industries/owlapi/blob/073beefb7805130bc0452472d1a9c801471fbaf1/docs/plans/2026-10-03-ci-input-aware-qualification.md) explicitly retains full integration checks and defers omission.
  Do not mistake a proposed optimization for a deployed skip policy.
- [Main run 37342523453](https://github.com/Hadden-Industries/owlapi/actions/runs/37342523453) actually ran all 16 jobs; none was skipped.
  The repository is therefore a useful example of evidence discipline, not proof that every documentation commit is already cheap.

Adopt Universal Ontology's input inventory, revision binding and completion gate.
Adopt OwlAPI's distinction between an optimization decision and proof of successful execution.
Do not port cross-run receipts, artifact reuse, Java build caching or browser matrices into this much smaller ONI workflow.

## Reasoning and selected design

1. **First principles:** a check is relevant when its consumed inputs or execution controls change.
   File extension alone cannot establish that.
   Skipping work does not establish that the skipped behavior passed.
2. **Modern practice:** make selection cheap, keep product checks conditional, retain one stable completion check, limit credentials, and measure the whole workflow rather than just the expensive step.
3. **Authoritative contracts:** GitHub distinguishes skipped workflows from skipped jobs; a required workflow omitted by path filtering can remain pending.
   A final job with `always()` and explicit result evaluation avoids treating a downstream skip as success.
   See [required-check behavior](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks) and [workflow filtering](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow).
4. **Adopted practice:** use the proven consumer/gate pattern from Universal Ontology and conservative evidence semantics from OwlAPI, scaled to ONI's two existing test suites.

### Proposed HISEW risk route

- **Risk class:** R2 for implementation, inferred from changing CI pass semantics, security-analysis selection and dependency-snapshot publication.
  Planning itself is read-only analysis plus this document.
- **Decision owner:** Maksym Shostak accepts the replacement coverage contract and exact configuration/settings changes.
- **Reasoning:** a false-negative selector or permissive final gate could silently admit insufficient assurance.
  The dependency publisher also has a write-capable token.
  Producer/formatter alignment can affect generated release text and must preserve literal meaning and owner-authored bytes.
- **Potential blast radius:** ONI contributors, maintainers and consumers relying on hosted checks and dependency alerts; the two comparison repositories are unaffected.
- **Reversibility:** restore unconditional existing tests and the fixed CodeQL matrix through a forward change.
  Keep automatic dependency submission enabled until its replacement is proven; re-enable it if replacement coverage fails.
- **Principal unknowns:** hosted selector/gate overhead; replacement dependency-graph equivalence and ordering; supported producer/formatter convergence; safe remediation of the historical review's preservation refusal; exact hosted runtime identity and event behavior.
- **Required artifacts:** this design dossier/plan, the tested input inventory and gate contract, one consolidated implementation review, and hosted run/graph readbacks.
  Keep transient evidence under the configured external HISEW evidence root.
- **Required specialist lenses:** CI correctness, the dependency publisher's credential/event boundary, and generated-text/literal preservation.
  One bounded independent review may cover these if competent; no separate staffing structure is needed.
- **Required verification:** focused selection/gate negative tests, native workflow validation, affected pipeline/Python checks once on the frozen control change, and the hosted demonstrations below.
- **Required human approvals:** remaining exact configuration changes and the separate GitHub setting listed below; commits and pushes need their own authority.
  The decision table records direct owner decisions, including the Node minimum; it does not grant blanket approval for undisclosed configuration changes.
- **Maximum sensible autonomy:** research, plan preparation and read-only checks now; implementation after the scope/configuration decisions are accepted.
- **Next lifecycle step:** review the expanded exact configuration surface, authorize implementation, then deliver the slices sequentially and qualify the resulting behavior with the accepted evidence limits.

### Architecture and event policy

Use native Git and Python's standard library for the small repository-specific selection and gate policy.
Place it in `tools/ci/checks.py`, owned by CI and covered by `tests/test_ci_checks.py`.
It must not become a disconnected mod-processing script or a new ONIModPipeline command: selecting GitHub jobs belongs to the CI consumer, while catalog parsing remains owned by the pipeline backend.
First remove the incidental dependency in `ListingTextRendererTests`: locate the repository through the tooling-owned `tools/oni-mod-pipeline/OniModPipeline.slnx` marker, rather than a document under `docs/plans/`.

Use the runner's Python only after checking its supported version and recording it; the selector needs Python 3.10+ and no package installation.
Reuse existing `.NET` setup and `global.json` in the selected Windows worker.
Do not add npm, pip, a third-party path Action or a schema-generation package just to decide applicability.
Node/npm acquisition is limited to selected documentation-quality and real README-producer qualification; ordinary Python-only work does not acquire those graphs. Pipeline test fixtures may continue using controlled process doubles, but those do not prove the real formatter/converter integration.

The core workflow should always start for supported PR and `main` push events.
It has one small Linux selection job, one conditional Windows worker for the existing suites, a conditional Linux documentation-quality job, conditional reusable CodeQL/dependency consumers, and a small Linux completion gate. Keep both suites in one Windows worker initially: step-level flags avoid `.NET` setup/restore when only Python tests apply, without adding another runner matrix.
The completion gate is named `Required ONI checks` and describes selected coverage rather than implying full mod qualification.

Centralize the decision in that one selector.
Convert the existing CodeQL file into a reusable consumer and create the dependency publisher as a reusable consumer; do not give each its own push-triggered selector job.
Move CodeQL's existing weekly cadence into the orchestrator's full route.
Documentation-only events need the Linux control jobs and their selected documentation-quality consumer, rather than application jobs or duplicate selection jobs.
Same-repository relative reusable-workflow calls use the caller's commit, as described in [GitHub's reuse contract](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows).
Each reusable consumer validates the passed decision and revision and verifies its internal completion, including every selected CodeQL matrix language, before returning a completion identity to the outer gate.

Selection produces a compact versioned record containing event, exact base/tested SHAs, policy identity, changed paths, selected checks and reasons.
The worker and gate verify the record against their actual checkout.
These are same-run decisions, not transferable cross-run test receipts.

For PRs compare the event's captured base to the actual tested merge revision and verify the merge parents.
For ordinary pushes compare `event.before` to the checked-out `event.after`.
Use complete native Git NUL-delimited records with renames represented as deletion/addition; include deletions and mode/type changes.
Do not depend on the truncated changed-file API, line-delimited path parsing or commit-message skip tags.

Manual and scheduled core runs select all existing suites, documentation quality and all CodeQL languages, subject to the unchanged analysis event/trust policy. Dependency refresh is selected only on eligible trusted `main` events. Preserve the existing same-repository CodeQL PR restriction as an explicit eligibility decision; do not represent a policy-excluded fork analysis as executed. Unsupported events and identity mismatches fail the gate. An unavailable comparison for an otherwise valid event selects all eligible checks with an explicit fallback reason, or fails if source identity cannot be established. Unknown paths outside `docs/` and changed selection/workflow controls widen coverage.
Unusual files inside `docs/` remain a documentation-boundary validation issue: reject unsupported executable/link entries without running them or turning them into mod inputs. An output limit, timeout or failed Git read must never become an empty successful selection. Bound Git observation to 30 seconds and 4 MiB; an exceeded bound is a fallback/failure diagnostic, not a partial path list.

Cancel superseded PR runs only.
Preserve `main` verification attempts through distinct orchestration concurrency identities.
Serialize the dependency publication section separately, with bounded queueing and freshness checks; unique orchestration groups alone do not order snapshot writes.
An explicitly obsolete dependency state may return a verified supersession disposition, but a newer docs-only commit does not make unchanged dependency inputs obsolete.
Queue overflow, failure or missing disposition cannot become successful dependency completion.
Do not add merge-queue support without an actual queue requirement.

Preserve existing explicit CodeQL categories `/language:actions`, `/language:csharp` and `/language:python` through the reusable-workflow migration.
An omitted language receives no fresh analysis; older findings may remain visible.
The gate and summaries must report selected qualification rather than imply that all language results were refreshed on each event.

## Proposed requirements and observable acceptance

These IDs make the accepted design choices falsifiable.
Implementation and configuration authority remain separate from acceptance of the design.

| Requirement                                   | Acceptance / quality scenario                                                                                                                                                                                                                                                                                                              | Design decision                                                                                                                                                                         |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-001 Avoid irrelevant application work     | AC-001 / QA-001: additions, edits, moves and deletions anywhere under `docs/`, including the former root-marker plan, select no Windows worker, `.NET` restore, application tests, CodeQL or NuGet submission                                                                                                                              | DEC-001: make `docs/` a documentation-only boundary; select its quality consumer on PRs and direct `main` pushes                                                                        |
| REQ-002 Preserve current test coverage        | AC-002 / QA-002: each source, test, fixture, dependency, runner and workflow input selects its consumer; mixed changes take the union                                                                                                                                                                                                      | DEC-002: inventory consumers, not filename extensions; retain the existing test commands                                                                                                |
| REQ-003 Fail closed                           | AC-003 / QA-003: unknown path, missing base, malformed plan, wrong checkout, oversized diff and selector failure cannot produce an empty green gate                                                                                                                                                                                        | DEC-003: full fallback for uncertain relevance, failure for uncertain identity or invalid controls                                                                                      |
| REQ-004 Make completion truthful              | AC-004 / QA-004: selected checks skipped/failed/cancelled, missing completion outputs and stale SHAs fail the final gate; legitimate unselected skips are explained                                                                                                                                                                        | DEC-004: independent evaluation of selection, worker result, selected step completion and revision                                                                                      |
| REQ-005 Avoid irrelevant CodeQL languages     | AC-005 / QA-005: known C#-, Python- and Actions-only cases select their matching categories; weekly/manual runs select all three                                                                                                                                                                                                           | DEC-005: share the input inventory, preserve existing analysis categories and upload policy                                                                                             |
| REQ-006 Preserve dependency visibility        | AC-006 / QA-006: a manifest/lock/build-control change updates all four current NuGet project graphs; a following docs-only commit neither restores them nor discards an otherwise current dependency update; obsolete publication cannot overwrite newer graph state                                                                       | DEC-006: prove a controlled native publisher and serialized dependency-input freshness checks before disabling generated autosubmission; retain truthful source SHAs                    |
| REQ-007 Keep trust boundaries narrow          | AC-007 / QA-007: PR selection/tests have read-only credentials; snapshot writes occur only for trusted `main` events; no PR-produced artifact is executed with write credentials                                                                                                                                                           | DEC-007: separate dependency workflow, job-local permissions, no `pull_request_target` or privileged follow-up                                                                          |
| REQ-008 Demonstrate proportionality           | AC-008 / QA-008: hosted summaries show decisions, checks actually executed and timing; no sustained increase in review-only runner work is accepted as a saving                                                                                                                                                                            | DEC-008: compare docs, Python, pipeline, dependency and mixed scenarios; no fixed claimed latency saving                                                                                |
| REQ-009 Check documentation as documentation  | AC-009 / QA-009: authored Markdown formatting/lint/local-link defects fail the documentation consumer and outer gate; image/target deletions trigger link checking without application tests; all authored Markdown, including both historical reviews, receives the applicable checks; unexplained preservation refusal blocks activation | DEC-009: use owner-selected coordinated version `1.0.2` across the complete authored scope, without a historical-review exclusion; remediate baseline findings while preserving meaning |
| REQ-010 Make producer and checker converge    | AC-010 / QA-010: repeated `sync-readme` and the canonical quality check leave the same generated block; `sync-readme --check` passes after synchronization; authored bytes outside the markers remain unchanged; formatter/converter failures cannot write partial output                                                                  | DEC-010: normalize producer-owned GFM through the selected tool's supported public contract before replacing the block; qualify whole-document context and retain pipeline ownership    |
| REQ-011 State runtime compatibility precisely | AC-011 / QA-011: root Node declaration is `>=24.21.0`, root lock metadata agrees, and actual formatter/producer qualification uses a supported Node 24 runtime with version recorded                                                                                                                                                       | DEC-011: preserve the owner's repository minimum and enforce the selected package's narrower `>=24.21.0 <25` contract at its consumer boundary; do not claim later-major qualification  |

## Input inventory and expected work

The implementation must finish this inventory from readers and commands before activating skips.
The table is the conservative starting point.
`docs/` remains a non-executable documentation boundary: unsupported symlinks, executable replacements or unusual entries there fail documentation validation rather than becoming pipeline/mod inputs.
Unknown inputs elsewhere require conservative full selection.

| Change family                                                                                                         | Existing tests                                                                                                                                                        | CodeQL                                                                                                                      | Dependency submission                                                                     |
| --------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Any change under `docs/`, including plans/specs/reviews/guides, images and local-link targets                         | Documentation quality only; no application tests. Check the authored Markdown scope and local targets; reject unsupported entries as documentation-boundary failures. | None                                                                                                                        | None                                                                                      |
| Authored Markdown outside `docs/`, including root documents, the PR template and mod-test provenance READMEs          | Documentation quality plus any positively identified tooling/fixture/policy consumer; Markdown scope membership does not itself exempt a product input                | Only applicable executable/control categories                                                                               | Only applicable dependency/build inputs                                                   |
| README, Steam BBCode, mod profile/metadata, translations and shipped resources                                        | Conservatively retain pipeline and Python coverage; do not claim these fixture suites validate all changed real content                                               | Matching executable language only, if any                                                                                   | Only if dependency/build inputs also changed                                              |
| `tools/oni-mod-pipeline/src`, tests and fixtures, solution and project inputs                                         | Pipeline tests; Python tests also for embedded `Catalogs/Python/catalogs.py` or other imported Python inputs                                                          | C# for `.NET` inputs; Python for Python inputs; union when shared                                                           | Yes for project/lock/SDK/restore/build controls                                           |
| `clean.py` and its Python tests; catalog benchmark and imported backend/tests                                         | Python tests; pipeline tests for embedded backend or shared contract changes                                                                                          | Python                                                                                                                      | None for ordinary Python source edits                                                     |
| Mod C# source/tests                                                                                                   | Conservatively retain current hosted suites; game-dependent checks remain separate                                                                                    | C#                                                                                                                          | Yes only for dependency/build inputs                                                      |
| `package.json`, `package-lock.json`                                                                                   | Pipeline tests, because README conversion is a supported tooling consumer                                                                                             | Relevant executable consumer categories if the dependency graph can affect them; otherwise documented conservative fallback | Native npm lockfile discovery remains enabled; does not by itself require a NuGet restore |
| `global.json`, `.NET` project/lock files, `.slnx`, `.props`, `.targets`, NuGet config and applicable restore controls | Pipeline tests and affected Python tests                                                                                                                              | C#; wider when controls are shared                                                                                          | Yes                                                                                       |
| CI policy/module/tests, workflow or local Action changes                                                              | Full current test coverage plus focused CI-policy tests and native YAML/expression validation                                                                         | All categories for shared control changes; Actions for isolated known Action inputs                                         | Yes if submission or shared restore controls changed                                      |
| Shared Markdown tool/policy/lock inputs                                                                               | Documentation quality, pipeline tests and real README-producer convergence because the graph/policy now affect producer output; affected runtime/acquisition probes   | Categories justified by executable/control dependencies; shared orchestration changes still widen coverage                  | No NuGet restore solely for the separate npm graph; native npm discovery remains enabled  |
| Any path outside `docs/` not positively classified                                                                    | Full current test coverage                                                                                                                                            | All categories                                                                                                              | Conservative selection on trusted `main`, with a reason                                   |
| Core/CodeQL manual or scheduled qualification                                                                         | Full current test coverage                                                                                                                                            | All categories                                                                                                              | Dependency workflow separately refreshes trusted `main`                                   |

`tests/test_translation_catalogs.py` imports the owned catalog backend and benchmark.
Those imports belong in the Python consumer inventory even if only a C# resource declaration exposes the backend to the pipeline.
`ListingTextRendererTests` currently uses `docs/plans/2026-08-27-oni-mod-pipeline-implementation.md` only as a root sentinel: it does not read that plan's content.
Replace that incidental location marker with the existing tooling solution marker before activating the documentation-only boundary.
Demonstrate that adding, moving or removing documentation does not change the test's root discovery.
Other occurrences of `docs/guide.md` and `docs//README.md` in tests are synthetic strings, not reads of repository documents.

The documentation worker still acquires only the separate Markdown graph.
Q5 makes that graph a shared input of the README producer as well: it is no longer truthful to describe its tool/policy changes as documentation-only.
Documentation text under `docs/` does not become a producer input.
The curated root `README.md` selects documentation quality; the mod-local synchronized README selects documentation and producer assurance.
Other files under `mods/` retain their actual consumers.
Every authored mod Markdown file selects documentation quality on ordinary content edits as well as additions.
README producer assurance covers the pipeline production-source subtree and the real mod preview, alongside the declared description, profile, metadata and tool inputs.
This conservative source boundary includes service initialization, profile loading, listing validation, process execution and source-control discovery used by `sync-readme`; fixture tests alone do not qualify changed real listing inputs.
This change adds producer convergence assurance through the existing game-free `sync-readme --check` seam, not a new general mod-validation API.
`validate` performs game-environment discovery and must not be advertised as game-free hosted coverage.

## Implementation slices and proof

| Slice                                                         | Traceability                                                                 | Demonstrable result and predicted seams                                                                                                                                                    | Proof and delivery implication                                                                                                                                                                                                                                                         |
| ------------------------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SLICE-001 Remove the incidental docs dependency               | REQ/AC/QA-001, 002; DEC-001, 002                                             | Change only `ListingTextRendererTests` root discovery to its existing tooling solution marker                                                                                              | Focused renderer tests and root-discovery evidence with a documentation-free temporary layout; preserve the real BBCode fixture.                                                                                                                                                       |
| SLICE-002 Select and gate the existing suites                 | REQ/AC/QA-001 through 004, 007, 008; DEC-001 through 004, 007, 008           | Add `tools/ci/checks.py` and `tests/test_ci_checks.py`; replace unconditional orchestration with selection, conditional Windows steps and final gate                                       | Temporary Git fixtures and negative gate cases for every family; actual control-change hosted run and the genuine docs-only demonstration after documentation activation. Keep original commands; core schedule/manual forces all suites.                                              |
| SLICE-003 Select CodeQL languages                             | REQ/AC/QA-002, 003, 005, 007; DEC-002, 003, 005, 007                         | Convert `.github/workflows/codeql.yml` into a reusable consumer of the core plan and its selected language matrix; include its completion in the outer gate                                | C#-, Python-, Action-, mixed-, workflow-control-, unknown-input and schedule/manual cases; read back uploaded language categories. Empty matrix omits the consumer explicitly.                                                                                                         |
| SLICE-004 Replace redundant NuGet autosubmission safely       | REQ/AC/QA-003, 006, 007, 008; DEC-003, 006, 007, 008                         | Add `.github/workflows/dependency-submission.yml`; dependency-input selection, bounded serialized publication, locked restore and native graph submission on trusted `main` only           | Qualify all four project graphs, deleted manifests, older-SHA/equal-input publication and obsolete/reordered states while autosubmission remains enabled. Disable the setting only after equivalence and separate approval; prove no dynamic duplicate on the genuine docs-only event. |
| SLICE-005 Align README producer and shared formatter          | REQ/AC/QA-002, 003, 007, 009 through 011; DEC-002, 003, 007, 009 through 011 | Approved shared `tooling/markdown` graph/policy and Node declarations; pipeline-owned normalization seam integrated into `ReadmeSynchronizer`, with release validation using the same seam | Real public converter/formatter acquisition on supported Node 24; repeated synchronization and full-document checks converge; outside-marker bytes remain unchanged; process errors, preservation refusal and cancellation cannot partially write README.                              |
| SLICE-006 Establish complete authored-document quality        | REQ/AC/QA-001, 004, 007 through 011; DEC-001, 004, 007 through 011           | Complete authored policy and meaning-preserving corpus repairs; conditional Linux documentation worker using the trusted checker/policy                                                    | Full corpus plus formatter/lint/link negative cases; preserve historical claims and literals; resolve preservation refusal safely before activation; prove docs-only selection excludes application/security/dependency consumers.                                                     |
| SLICE-007 Qualify the integrated control and operating effect | All requirements and decisions                                               | Final input inventory, workflow syntax/governance checks, summaries and hosted readbacks                                                                                                   | Freeze once, perform one bounded independent review, run affected checks and the accepted hosted minimum, and retain exact commit/run references. Label other hosted scenarios pending; restore unconditional coverage if correctness or net benefit fails.                            |

The slices are sequential because policy, outputs, job conditions, producer normalization and the final gate share contracts.
SLICE-005 establishes the shared graph/policy acquisition and producer seam; SLICE-006 completes corpus remediation and activates the blocking docs consumer.
Root-discovery repair and core selection are independently useful if the dependency setting change is deferred, but do not solve the separate generated NuGet run.
Until the clean baseline, safe preservation and producer convergence are qualified, keep documentation-check activation blocked and explicitly report that ONI has no active Markdown-quality gate.
Do not silently deliver a warning-only or excluded-review substitute.

### Selection and gate test oracles

Exercise actual Git repositories for add/edit/delete, rename across classification boundaries, mode change, symlink, spaces, Unicode and newline-containing paths; use NUL-delimited results.
Cover a large diff beyond GitHub's changed-file listing limits.
Expected consumers must come from the inspected reader inventory, not by importing the selector's expected-path table into the test oracle.

Cover ordinary push ranges, PR merge/base identities, base movement, mixed changes, missing comparison objects, zero-before SHA, unsupported events, malformed event JSON, policy drift, malformed or incomplete outputs and observation bounds.
Bound one supported acquisition of a missing comparison object; if it remains unavailable, use the explicit full fallback.
Do not retry until a favorable path list appears.

The gate must reject missing result keys, selected worker skips, failures/cancellation/timeouts, absent selected step completions, wrong revision and selector failure.
If an unselected consumer unexpectedly runs and fails, preserve the failure.
Selection and completion cannot grant their own exceptions by omitting inconvenient keys.

Use native workflow syntax/expression validation and tests that mutate the actual orchestration to remove `needs`, weaken `if`, tolerate selected failures, widen permissions or omit a gate consumer. If a validator is absent, resolve one maintained native tool and its current release/checksum before implementation; do not replace it with regex-only YAML assurance or add a package graph solely for parsing.

Run the current pipeline suite and Python suite once on the consolidated CI-control candidate, along with the new focused tests.
CodeQL uploaded results, dependency graph state and actual runner/job selection need hosted readback; local fixtures cannot prove them.

Add the dependency-ordering fixtures for A changing dependencies followed by B changing only docs; A and a later dependency-changing C reaching the publisher in either order; identical inputs across distinct SHAs; deleted projects; missing comparison data; and queue overflow/cancellation.
An older SHA is eligible only through positive comparison of the complete dependency-input inventory.
An obsolete state must never be represented as published or satisfy selected completion merely through a claimed supersession flag.
Explain its terminal disposition; missing publication assurance still fails the selected gate.

### Documentation quality adoption

ONI has no existing Markdown formatter or linter to preserve.
The owner selected the shared `@hadden-industries/markdown-quality` consumer at coordinated version `1.0.2`, also adopted by OwlAPI.
Acquire it in a separate `tooling/markdown` graph shared by the documentation worker and README producer; its dependencies do not need to join the root npm graph.

Selected native tool: [`@hadden-industries/markdown-quality`](https://github.com/Hadden-Industries/markdown-quality), coordinated version `1.0.2`, as installed in OwlAPI's isolated `tooling/markdown` graph.
Anonymous public npm metadata confirmed that exact version and integrity `sha512-0IQ5m1R9AgONW6oQPiOjJ8kJqBK4daryAjjx2af2ySxlVbjpzsl3P1yVCKxnssBBRVvIi7zwhwtGOS046sOC7A==`.
Its engine range is Node `>=24.21.0 <25`, and its license is AGPL-3.0-only.
Record the package and native-component notices in the adoption evidence; tooling acquisition is separate from mod packaging.

The public `pilot` tag names alpha.4 on inspection, while `latest` names alpha.2.
The installed package's release notes identify a Linux native-executable packaging failure in alpha.2.
Pin the package and coordinated native components to the owner-selected `1.0.2` tuple and qualify public locked acquisition.
Do not resolve a dist-tag, use an unversioned install or substitute another release during implementation; a version change requires a separate decision.
Package selection is accepted, while installation and corpus qualification remain work to perform.

Use separate `tooling/markdown/package.json` and lockfile, lifecycle scripts disabled and no registry credentials.
The proposed root policy is `.markdown-quality.json`, schema 1 with `authored-gfm@1`, every repository-authored Markdown file (`**/*.md` with explicit operational/generated-directory exclusions), LF layout matching `.gitattributes`, GFM linting and contained local-file/image links. In particular include `.github/pull_request_template.md`, `mods/delivery-temperature-limit-supercooled/Tests/ExternalModCompatibility/DeferredFixtureCatalog/README.md` and both repository-authored FastTrack fixture-build READMEs. Their directory name `ThirdParty` does not make those provenance notes copied upstream material. Inventory actual generated/vendor content before declaring exclusions; none may swallow authored reviews or these notes. Use the native complete-scope check, not changed-file-only assurance. The separate graph is outside the root dependency graph and mod binary packaging, but it is an execution input of `sync-readme` and must be selected accordingly.

Include `docs/reviews/delivery-temperature-limit-steam-copy-review.md` in the same complete formatting, GFM-lint and contained-local-link scope as other authored plans, guides, specs and reviews.
Do not add a file-specific exclusion or a historical-review exception.
Baseline the complete authored corpus before activation and disclose actual findings.
Any necessary formatting or link repairs must preserve the review's substantive text, historical context and citation meaning; review those repairs as ordinary documentation changes.
The owner's inclusion decision supersedes the earlier proposal to preserve formatting bytes through an exclusion.

Read-only, in-memory formatter probes during grilling identified baseline debt; they were not a canonical full-scope check.
The Steam copy review changed and retained six prose findings; this CI plan retained 68; the Python performance review raised `PRESERVATION: Formatting changed parsed meaning or a literal.`
Alpha.4's normalized parsed-structure guard supplies no mismatch location.
Treat that refusal as a preservation blocker, not an ordinary autofix.
Perform one focused diagnosis and a justified correction; repeated unchanged failure stops the attempt and blocks activation.
Do not force generated output, disable preservation checks, exclude the review or substitute another package version.

Repair citations without rewriting historical conclusions.
The selected checker classifies scheme-bearing URLs, including `file:///c:/...`, as external, so its local-target check does not validate machine-local historical links.
Verified source commit `726117481e399fa66cf1a1d521a32f1144195abd` preserves the cited `clean.py`, tests and workflow.
Original `artifacts/options-ui` scripts are absent from tracked history; `3db08d4` first tracks extracted successors with different wrappers and line positions.
Preserve original paths as clearly labelled historical provenance, and distinguish verified historical, first-tracked and current successor references.
Never fabricate a historical source link.
Formatter and lint repairs must preserve substantive claims and code literals; a necessary substantive alteration needs separate review.
Any baseline change to a configuration/policy document such as `AGENTS.md` still requires its exact change approval.

The Linux documentation worker acquires only the approved graph, runs the canonical format/lint/link check and reports failures to the final gate.
It performs no formatting writes in CI and runs no mod tests or CodeQL solely because a document changed.
A deletion or move of a document or badge image selects the complete local-link check even when no Markdown text changed.
Link-target changes outside `docs/` can select documentation checking as well as their own applicable consumers.

Keep tool code, locked acquisition inputs and the approved policy separate from candidate content.
For fork PRs, acquire them from the captured trusted base; inspect candidate documents as data through the native library/CLI with the trusted policy. If a small adapter is necessary to supply that policy, place it in `tools/ci/documentation.mjs`, use the public `runQuality` contract and cover its boundary without reimplementing the checker. Candidate JavaScript configuration, dependency graphs and scripts cannot select the executable or trusted policy. A PR proposing a policy/tool change needs separate control qualification; it cannot self-approve its weakened check.

Bootstrap the selected tool and proposed policy through an approved trusted-main/manual qualification, since the pre-adoption base has no trusted graph.
Before making the docs check blocking, prove public locked acquisition, a clean complete authored baseline including the historical Steam copy review, a formatter defect, a GFM-lint defect, a missing local target and a rejected unsupported entry.
One Linux production consumer plus bounded local Windows qualification is sufficient here; do not copy OwlAPI's six-sample cross-platform observation campaign into ordinary ONI documentation commits.

### README producer convergence and Node compatibility

The generated description in `mods/delivery-temperature-limit-supercooled/README.md` is owned by `sync-readme`; the root README is an authored repository hub.
At the inspected baseline, `ReadmeSynchronizer` compared replacement bytes exactly, and `ReadmeDescriptionBlock` normalized newlines without canonicalizing Markdown.
An in-memory alpha.4 probe changed that generated block.
The selected version has no generated-region policy, and a bounded probe of Prettier ignore directives still changed the block during its native formatting pass.
Whole-README suppression and ad hoc formatting exceptions do not implement the accepted authored-document coverage.

Retain `steam-community-bbcode@1.0.0-rc.1` as the direct converter.
Set the six source section headings to `[h2]`; retain all image tags and URLs.
Under the accepted Q10 override, let the public native Markdown checker locate missing image alternatives and supply the six explicit compatibility labels only inside the owned block.
Refuse unknown images, mismatched coordinates or attempts to change authored content.
Do not add a Markdown parser, a heading transformer, converter-package enrichment options or additional AST dependencies.
Final canonical formatting and checking remain wholly owned by the selected Markdown tool's public contract.

Integrate canonicalization at the pipeline-owned producer seam, before replacement, through the selected tool's supported public CLI/library contract. Keep subprocess handling behind the existing process-runner interface and expose one normalization contract to synchronization and release validation. Do not import package-internal formatter functions used by the diagnostic probes or reproduce formatting rules in C#. Public `runQuality` requires a contained policy pathname, not an inline configuration object; qualify any temporary document staging and policy acquisition explicitly, with bounded input/output and cleanup.
No root-owned document may be rewritten during a check.

Prove the fixed point in whole-README context: generation, normalization, replacement, another `sync-readme --check` and the complete authored quality check agree.
Normalizing a standalone fragment is insufficient without that contextual evidence.
Preserve exact bytes outside the generated marker pair; if canonicalization would alter them, report the conflict for authored baseline repair rather than let synchronization silently rewrite owner content.
Missing tools, unsupported runtimes, malformed output, preservation refusal and cancellation fail without partial writes.
Keep original converter fidelity/diagnostic validation in force.

Change root `package.json` Node compatibility from `24.20.0` to the owner-selected `>=24.21.0`, and synchronize only corresponding root metadata in `package-lock.json`.
Keep npm `12.0.2` and `steam-community-bbcode` `1.0.0-rc.1` unless a separately justified change is approved.
Declare the selected Markdown graph's narrower Node `>=24.21.0 <25` contract.
Use a qualified Node 24 runtime for formatter/producer checks and record the actual version; the repository minimum is not a claim that the Markdown tool supports Node 25+. Document the additional shared-tool acquisition required by `sync-readme`. Root engine/lock, shared graph/policy and producer execution controls now select producer assurance as well as documentation quality.

### Native dependency publisher and qualification gate

GitHub owns the current workflow at `dynamic/dependency-graph/auto-submission`; it cannot be fixed by editing `oni-pipeline-tests.yml`.
Its documentation describes [automatic submission](https://docs.github.com/en/code-security/reference/supply-chain-security/automatic-dependency-submission) and the [repository setting](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/submit-dependencies-automatically).
The observed review-only run establishes this repository's behavior even though the general setting guide describes manifest-triggered runs.

Reuse the same native component-detection Action that the successful generated run used: [actions/component-detection-dependency-submission-action at 98beaea](https://github.com/actions/component-detection-dependency-submission-action/tree/98beaea5a1227f3db476e4c9ca959a0cacd7ac4f).
On inspection, that SHA was also its current `main`; the mirror had no release or tag.
Its Action runs on Node 24 and supports `detectorsCategories`, `correlator`, `snapshot-sha` and `snapshot-ref`; its license is MIT.
Recheck that identity, rights and supported execution at implementation, and qualify changed bytes before adopting a newer revision.
Do not silently substitute the old example version from its README.

Use the NuGet detector category and a single stable repository-owned correlator.
Restore the exact current project inventory with locked mode, including project/build imports, without compiling proprietary game assemblies. Do not reproduce the generated workflow's arbitrary `head -20` discovery limit. Inventory additions/deletions must affect selection, and all project graphs must be represented in the resulting snapshot.

Publish snapshots only from a trusted `main` push, a scheduled default-branch run or an explicitly default-branch manual run.
Restrict `contents: write` to the dependency consumer's caller and submit job; other jobs retain their explicitly scoped permissions.
The [snapshot API](https://docs.github.com/en/rest/dependency-graph/dependency-submission) requires Contents write; do not copy unrelated OIDC permissions from an example.
Use pinned Actions and ephemeral `GITHUB_TOKEN`, with checkout credentials disabled.
Follow [GitHub's PR trust guidance](https://docs.github.com/en/actions/reference/security/securely-using-pull_request_target); never run fork code or a PR artifact with the publisher's credentials.
The reusable publisher must independently enforce event, branch and revision eligibility rather than trusting a caller's boolean.

Refresh on the existing weekly cadence and on demand as well as on dependency/build inputs. A schedule is a best-effort backstop, not a seven-day freshness guarantee: GitHub may delay/drop scheduled runs and disables inactive public-repository schedules after 60 days, as documented for [scheduled events](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule).
Preserve Dependency graph, Dependabot alerts, security updates, version-update proposals and secret scanning.
Only generated automatic dependency submission is proposed for disablement.

Before switching, compare manifests, direct/transitive package identities and relationships from the replacement snapshot with the generated run; verify graph acceptance and how stable correlator ownership handles removed manifests. A passing submission step alone is insufficient. The native API considers the latest submitted snapshot per correlator/detector; a stable correlator does not by itself prove chronological publication or removal of manifests.

In the serialized publication section, capture live `main` and compare its complete dependency-input inventory against the checked-out candidate, including additions, deletions, mode/type changes, imported restore controls and detector/publication controls.
If inputs match, submit the candidate's actual source SHA even when a newer docs-only commit is the tip.
Record the observed tip and comparison identity separately; do not rewrite `snapshot-sha` to imply the old checkout was current.
If dependency inputs differ or their completeness cannot be established, do not publish the obsolete/uncertain state.
Its disposition is explicit and does not itself establish successful selected publication.
Later eligible runs publish the current state; failures require recovery rather than an optimistic receipt.

Use job-level publisher serialization, `queue: max` with cancellation disabled, and recheck freshness after acquiring the publication slot.
[GitHub's concurrency contract](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency) allows up to 100 pending jobs; default single-pending behavior can cancel earlier pending work even with `cancel-in-progress: false`.
Queue order follows arrival at the slot, not commit order.
Therefore queueing must accompany input comparison, not replace it.
Qualify reordered arrivals, later dependency changes during publication, queue overflow and removed manifests; do not claim an atomic check-and-submit transaction or guaranteed completion from GitHub's scheduler.
If freshness/ordering/graph controls cannot be demonstrated, keep autosubmission enabled and report the remaining overhead.

Do not parse or recreate the NuGet graph, write a custom submission API client, or restore old runtime artifacts to manufacture equivalence.
Retain proof externally with the actual commit, workflow/run identity, Action SHA, runtime, manifest inventory and graph observation.

## Configuration surface and remaining approval before implementation

The owner accepted the design decisions, shared producer/tool direction and Node minimum. This inventory makes the concrete configuration delta reviewable; disclose and obtain remaining exact-file approval under `AGENTS.md` before mutation. There have been no configuration changes during planning/grilling.

| File or setting                                                                  | Proposed exact change                                                                                                                                                                                                                                                                                                                                                       | Pipeline effect                                                                                                                                                                                                                                                                                                                                  |
| -------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `.github/workflows/oni-pipeline-tests.yml` to `.github/workflows/oni-checks.yml` | Rename to `ONI checks`; one PR/main-push selector, conditional Windows test commands and real producer convergence on applicable inputs, selected Linux docs worker, reusable CodeQL/dependency consumers, stable completion gate, bounded timeouts, PR-only cancellation and weekly/manual full route (`52 16 * * 0`)                                                      | Docs-only commits use the docs worker and small control jobs; Python-only work avoids `.NET`/Node graph acquisition. Shared producer/tool inputs select convergence assurance. Security events write belongs only to the CodeQL caller; Contents write only to the eligible dependency caller. Recheck old required-check names before renaming. |
| `.github/workflows/codeql.yml`                                                   | Replace its direct push/PR/schedule/manual triggers with `workflow_call`; consume and verify the core plan and language matrix, retain categories, same-repository PR eligibility and pinned existing CodeQL Actions, and verify every selected matrix result                                                                                                               | Avoid duplicate selection jobs and unrelated language analyses. The orchestrator preserves the existing weekly/full scan and main/manual coverage; the reusable analysis job keeps its necessary Security events write permission.                                                                                                               |
| `.github/workflows/dependency-submission.yml`                                    | Create reusable trusted-main NuGet publication with the qualified native Action, locked project inventory, job-level serialized queue (`queue: max`, cancellation disabled), complete input freshness comparison, truthful source SHA and scoped Contents write                                                                                                             | Avoids docs-only restores after the setting switch while retaining eligible older-SHA updates; rejects obsolete/uncertain states and reports queue/publication failures. Caller and consumer independently enforce event eligibility.                                                                                                            |
| `tooling/markdown/package.json` and `tooling/markdown/package-lock.json`         | Create a separate private graph pinned to coordinated version `1.0.2`, declaring Node `>=24.21.0 <25`, lifecycle scripts disabled and recorded integrity/license evidence                                                                                                                                                                                                   | Shared by documentation checks and README production, so graph changes select both consumers. Does not add root dependencies or ship the tool in the mod binary. Exact file creation remains part of implementation approval.                                                                                                                    |
| `.markdown-quality.json`                                                         | Create schema-1 authored-GFM policy covering all repository-authored Markdown, LF layout, lint and contained local links; only inventoried operational/generated-directory exclusions, with no historical-review exclusion. If the public producer contract requires staging, copy this approved policy into the bounded temporary document root without changing its rules | Establishes complete authored quality and the canonical producer policy; changes can affect generated README output and select pipeline assurance. Checks do not rewrite repository documents. Persistent creation and any temporary policy-copy behavior belong in the exact implementation approval.                                           |
| Root `package.json` and `package-lock.json`                                      | Set `engines.node` to owner-selected `>=24.21.0`; synchronize the root lock's corresponding metadata only; retain npm `12.0.2` and converter `1.0.0-rc.1`                                                                                                                                                                                                                   | Aligns the repository minimum with the new tool consumer while preserving existing dependency versions. The Markdown consumer separately enforces its Node 24 range. Inspect the exact engine/metadata delta before mutation; no blanket lock regeneration is approved.                                                                          |
| GitHub Settings: Automatic dependency submission                                 | Change Enabled to Disabled only after replacement graph equivalence and separate explicit approval                                                                                                                                                                                                                                                                          | Removes the generated duplicate. Dependency graph and the other security/update features stay enabled.                                                                                                                                                                                                                                           |

No other configuration change is selected.
In particular this plan does not require changing `global.json`, mod TOML, branch protection, HISEW verification profiles or either comparison repository.
Q5 adds ordinary pipeline source/tests and shared-tool acquisition documentation; Q6 adds authored baseline repairs. Any necessary configuration/policy-document repair or additional setting must be named with its exact delta and approved separately under `AGENTS.md`.

The renamed workflow still provides a normal stable completion check; the required gate is not conditionally omitted.
Do not implement the earlier review-only `paths-ignore` suggestion as a second independent skip policy.

## Rollout, observability and recovery

After implementation/configuration approval, start the R2 execution with this accepted design, complete input inventory and assurance obligations. Recheck applicability before engine mutations. Use the existing checkout and `main` only, preserve user-owned files and use exact path-scoped staging; committing/pushing this plan or implementation is separately authorized.

Consolidate each delivered slice before its review.
At the final candidate, obtain one independent review covering selector/gate correctness, publication trust and producer/literal preservation.
Attempt the previously requested Antigravity Flash/high review once with a bounded timeout; if unavailable or empty, use the authorized Codex fallback. Follow up only on findings and newly affected contracts, with one supported correction/retry per unchanged failure.
Do not repeat broad reviews or full suites merely to obtain a green result.

The first hosted control-change run must execute the full existing suites because orchestration changed, plus real producer convergence on supported Node 24.
The accepted activation minimum is hosted qualification of that actual control change, a genuine docs-only change with the blocking docs consumer, all three CodeQL languages and real dependency publication/graph readback.
One eligible event may support several independent observations.
Where autosubmission has been separately approved for disablement, the genuine docs-only observation must also show absence of the generated duplicate.

Exercise every change family and negative selector/gate case in local Git fixtures, including Python-only, pipeline/embedded-backend, mixed/control, dependency, documentation move/deletion and missing local image target.
Local fixtures do not establish hosted execution of those paths.
Mark remaining hosted Python-only, isolated pipeline, PR/fork, ordering or other scenarios explicitly pending until genuine changes/runs exercise them; these do not postpone activation solely to satisfy the former five-family campaign.
Preserve which event/trust boundaries were actually hosted. Do not manufacture no-op commits, create another branch/worktree or open unapproved trial PRs.
Do not use manual-run success as evidence that PR-required-check event semantics were exercised.

Record selected checks and reasons, tested revision, fallback reason, runtime versions, job/step conclusions, setup/restore durations and total workflow/runner time. On docs-only hosted events, there must be no Windows worker, CodeQL consumer or NuGet restore; compare Linux documentation/control overhead with the 104-second baseline rather than promise an unmeasured target.
Confirm the setting switch separately from the repository commit and confirm the absence of the dynamic duplicate on a subsequent qualifying event.

Keep transient files and detailed run evidence under `C:\Users\maksy\.hi\w\e`; retain a concise final delivery record with actual run links in repository documentation only where useful.
Avoid a permanent evidence service, retained executable artifacts or a new cross-run cache.

Abort selective omission on an incorrect classification, misleading gate result, missing selected execution, graph regression or trust-boundary violation.
Restore unconditional existing tests and all CodeQL languages through a reviewed forward change.
If the dependency replacement fails, re-enable generated autosubmission through an authorized setting change; accepting duplicate work temporarily is preferable to losing graph coverage.
Never repair an active checkout with destructive restoration commands.

Replan when a new mod, reader, test project, generated/shipped document, reusable workflow, dependency source, required check, merge queue, publisher or runtime consumer changes the inventory.
Also reassess if control-plane overhead offsets the avoided work.
Cross-run qualification reuse is a later option only if measured duplicate cost justifies OwlAPI-level equivalence, provenance and recovery controls.

## Remaining evidence gates

The current grilling frontier is closed by the accepted decision table and Node minimum.
Remaining work is evidence and exact-delta execution control: implementation/configuration authority, locked acquisition, safe preservation-refusal remediation, complete authored-corpus qualification, producer/checker convergence, native workflow validator identity, the accepted hosted minimum, dependency graph equivalence/ordering and the separate automatic-submission setting approval.
Record pending hosted scenarios without silently treating them as passed.
An unresolved preservation or producer-compatibility blocker stops documentation activation; it does not authorize a review exclusion, weakened gate or unapproved version substitution.

If inspection or bounded experiments expose a consequential choice that those sources cannot settle, use the requested grilling skill before asking the owner to choose.
Do not use that fallback to ask the owner to determine ordinary path mappings or tool behavior that can be established from source.
