# Steam Community BBCode execution record

## Baseline binding for review

The technical baseline is the complete [v2 implementation plan](2026-09-08-steam-community-bbcode-implementation-plan-v2.md),
already present in merged commit `1d352b9040005e144a9fc4a1ac7dd628241e7284`.
Its SHA-256 is `d639468a9e1a11b79139371a659b28b6c09a9f9b76128f2c13ac2da7b65e6ac4`.
This record refers to that plan; the original baseline bytes remain unchanged.
The owner-approved [forward refinement amendment](2026-09-09-steam-community-bbcode-forward-refinements.md)
records the subsequent diagnostic and whitespace work authorized on 2026-09-09.

Recorded authority: the [bootstrap handoff](2026-09-08-sdlc-bootstrap-handoff.md#accepted-sequence-and-authority)
authorizes the converter after the SDLC merge and preapproves configuration required
by the plan/SDLC. The owner subsequently requested transfer and resumption in this
checkout in task `01a081ff-0fce-7130-95d5-c1fdbcd8cfaa`, turn
`01a08247-6534-7370-891c-f712d3bd74a8`. These authorize continuation, but do not
explicitly name an accepted R2 baseline representation.

Requested decision: accept the exact committed v2 plan above as the R2 technical
baseline, using the recorded owner decision as its acceptance reference. Preserve
the prior-commit/unchanged-content check. Support this committed-plan representation
directly in the owning SDLC controls instead of manufacturing an Issue snapshot.
The existing Issue-snapshot contract remains supported for actual Issues. The
control currently accepts only Issue-shaped JSON, so this integration must be
tested before routing converter implementation. No R2 route or assurance is waived.

Acceptance: on 2026-09-08 Max replied in this implementation task,
"I accept the plan and baseline". This is the owner decision for the exact plan
and direct committed-plan representation proposed above. The plan remains unchanged.
There is no converter commit, push, npm release, or Steam publication authority.

## Scope and assurance

Purpose: allow maintainers to convert Steam descriptions into GFM while accounting
for every source construct and reporting unavoidable loss. Default to Workshop
items. Keep the partial reverse direction explicit, the library free of I/O, and
the AGPL-3.0-only implementation in `tools/steam-community-bbcode/`. Preserve the
root MIT/Klei licence and the .NET/ONI Mod Pipeline.

Route: R2 for the public converter. Required evidence remains the full plan's
registry/conformance, 33 historical regression checks, parser qualification,
strict JSDoc/generated-declaration and installed-consumer tests, affected repository
checks, independent verification/review, and triggered security assessment. A
local test pass does not establish independent acceptance, Steam rendering, release
qualification, or publication readiness. The no-subagents instruction remains.

The first slice is environment/tooling and dependency qualification. Before baseline
acceptance, work comprised local setup, research, declaration-only contracts,
and isolated qualification fixtures. Converter behavior had not been implemented.
The repository-adapted TDD skill owns implementation. Existing setup uses the
preservation route; dependency spikes use the exploration route; changed executable
behavior requires an observed meaningful RED before implementation.

## Parser decision rule

Reuse BBob only through supported configuration/composition after testing the full
parser gate. The plan's older suggestion of a small adapter does not override
NSH-01. Normalizing a lossless generic tree into Steam semantics is an ongoing
boundary. Rewriting input, patching a vendor parser, or reconstructing information
it discarded to conceal an incompatible grammar is not authorized. Reject an
unsuitable candidate and research another maintained parser/toolkit if necessary.

## Local activation

2026-09-08: starting tree was clean on `steam-community-bbcode` at the revision
above. Inspected setup: locked npm install with lifecycle scripts disabled, local
Python virtual environment, SDLC requirements, and local Codex/skill activation;
no global npm, MCP installation, hook trust, or GitHub mutation.
`npm exec --yes --package=npm@12.0.2 -- npm run setup:development` completed with
Node 24.20.0 and Python 3.14.7. npm reported zero vulnerabilities; pip reported a
version-check warning. Generated configuration still needs its direct check;
native Stop-hook loading/trust remains unverified. The direct `npm run check:sdlc`
subsequently passed.

## Baseline control integration evidence

The owning local/PR controls now accept a committed `docs/plans/*.md` directly.
Git and the GitHub Contents API supply exact blobs; Python's standard JSON parser
handles the PR's list of exact source-line references. No Markdown grammar or
parallel approval service was added. Existing Issue snapshot schemas/labels remain
authoritative for actual Issues. An acceptance reference is inspectable evidence,
not authentication by the helper. Trusted-base policy must be landed separately
before a future converter PR can claim a live linkage pass.

`npm run test:sdlc -- -k plan` first failed on absent plan support (four failures/
errors across five tests), then passed all five after implementation. One Windows
fixture needed explicit LF writes to satisfy the unchanged-byte precondition;
the real committed plan was verified as LF in both index and working tree.

## Tooling contract evidence

The first deliberate compile-time RED used an importable empty package module.
`test:types` reported missing `SteamParseResourceLimits` and `SourceSpan` exports,
plus unused negative assertions. Generated declarations from the JSDoc contracts
then passed the same positive and negative fixtures. This establishes declaration
checking for those types, not runtime resource enforcement or converter behavior.

The native npm 12 pack result is keyed by package name. The initial consumer
harness assumed npm 11's array and failed when run with the selected npm 12. It
now consumes the selected package manager's actual result directly, without a
legacy-shape fallback. Installed consumers subsequently passed with npm 12.0.2.

The user approved the isolated, unmodified TypeScript 6.0.3 coverage-tool role on
2026-09-08; primary checking/emission/consumer compilation remain TypeScript 7.0.2.
The first isolated coverage run counted zero files because the tool defaults to
its current directory. Native `--not-only-in-cwd` selects the parent project's
files; `--is 100` additionally makes an empty/NaN result fail. Explicit `Set<string>`
and the qualification rule's no-argument CST-returning contract removed actual
inferred-any gaps. The initial tooling `check` then passed all six corpus/registry
checks, two toolkit probes, 898/898 typed references, four repeatable declaration
artifacts, positive/negative declaration fixtures and the installed package smoke.
These counts describe the initial slice, not later converter functionality.

## Initial registry and CI

Initial schema/data configuration uses the alternative-verification route: native
Ajv validates JSON Schema, ajv-formats owns date/URI semantics, and independently
authored invalid examples check rejection. Ajv first rejected a draft conditional
schema under strict-required checking; expressing forbidden properties with native
boolean schemas corrected that draft without relaxing validation.

Cross-file identity/reference validation uses test-first: the real validator
accepted duplicate source identities and unresolved provenance (two observed
failures), then rejected them after the domain reference checks were added.
The initial 39-entry registry records intended policies, not executed conformance.
Profile applicability and example variants still require independent semantic
qualification. No renderer or converter pass is inferred from schema validity.

CI configuration runs selected checks on Windows/Linux and Node 22.23.2, 24.20.0,
26.8.1. These patch versions were refreshed from Node's official release index.
Workflow scope routing first missed the converter scope, then was corrected.
The workflow has not run on GitHub; no remote check or protection pass is claimed.

## Native grammar slice

The slice follows the parser-boundary and security sections of the accepted plan.
Two initial tests reached an executable empty syntax boundary and failed on absent
CST/opaque tokens. Native Chevrotain lexical modes and recursive grammar then passed
them. Further independent checks exposed absent malformed-source diagnostics,
resource enforcement and root positions polluted by the explicit EOF sentinel.
The grammar now uses the native top-level trailing-input check, retains unmatched
syntax in its CST, and diagnoses structural defects separately.

Source-byte limits precede lexing; attribute quotas precede parsing; native grammar
actions enforce depth and source-element counts. Defaults are 1 MiB input, 128
levels, 100,000 source elements and 64 KiB per attribute header. The supported
recursive-engine ceiling is 256, which has an executed boundary test. JavaScript
callers receive validation errors for invalid limits. A quota result retains the
input but carries no successful CST. These checks do not yet qualify general CPU,
allocation or release-security behavior.

Chevrotain's native input setter resets parser state, allowing its analyzed grammar
to be reused synchronously. A preservation check verifies earlier CSTs/tokens are
unchanged by later parses. Tests inspect actual nested CST ownership, source-token
ranges, quote/table/video/preview/unknown attributes, case, opaque regions, malformed
input and UTF-8 byte boundaries. fast-check 4.9.0 generated/shrank 3,000 bounded
cases with seed 20260908. The current parser qualification has 25 passing checks.

An independent end-position fixture failed before unist spans were supplied, then
passed for LF, CRLF, CR and surrogate pairs. Native lexer coordinates supply the
range; the syntax boundary maps its inclusive end into the unist exclusive end.
The vfile-location comparison was rejected and removed after its measured worst
case; it is not a runtime dependency. Raw research tarballs are retained under the
task's ignored runtime directory for review, owned by this task, with reassessment
at review/hand-off. No generic position indexer or vendored fix was introduced.

The selected parser is a foundation only. No complete-conformance claim follows
from this gate.

## Public parser and first MDAST slice

The public parser slice used compile-time contract RED followed by behavioral RED
for absent source records. The issued result now contains immutable source syntax,
profile and diagnostics; vendor CSTs/tokens remain internal. A JavaScript negative
case exposed `null` profile coercion, which was removed. The package check passed
10 behavior/registry/lineage tests, 25 grammar checks, 2769/2769 typed references,
12 repeatable declaration artifacts, declaration fixtures and an actual installed
JavaScript parser consumer. This describes the parser milestone only.

The next test-first slice implements the plan's standard-MDAST semantics: literal
source punctuation, headings and nested emphasis. Independently authored expected
MDAST node kinds and visible text are the oracle. Three tests reached the empty
conversion implementation and failed on absent paragraphs/headings and absent
prepared-result validation. Focused command: `npm test` in the package; affected
checks include parser qualification, strict checking, type coverage, declarations
and installed consumers. Final R2 verification remains the repository entry point.

Prepared inputs reuse immutable results issued by the parser; a fabricated or
deserialized tree has not passed its validation/resource boundary. A prepared
input retains its selected profile and cannot receive new parsing limits. This
constraint is checked at runtime and documented in the API. Input-wide failures
use diagnostic scope `input`; construct diagnostics retain registry identities.
This avoids assigning a fictional construct identity to a byte-limit failure.

The first MDAST slice passed all 13 current package tests, strict checking,
3285/3285 typed references, 22 repeatable declaration artifacts and the installed
consumer. Subsequent small test-first slices observed absent extension semantics,
list/block semantics, resource handling and table conversion before implementing
them. Current semantic checks cover nested underline/spoiler/noparse, native list
hierarchy and sibling ownership, exact opaque code, blockquotes, paragraphs,
deletion, nested link formatting/images, seven hostile destinations, nested-link
preservation and rectangular table semantics. Irregular/block-containing tables
and unowned list prose retain full source with diagnostics. These are bounded
fixtures, not the completed historical regression or security gates.

The public GFM boundary first failed its deliberate missing-export type contract.
An executable empty serializer then failed six independently parsed output cases.
Native MDAST/GFM serialization now passes those cases, including fence-bearing code
with exact leading/trailing newlines. The current package runtime suite has 37
passing tests. TypeScript 7 strict checking passes; full package and repository
verification must be refreshed after the remaining implementation slices.

## Historical regression recovery and expanded semantics

The exact 5,780-byte historical description was recovered from retained raw
command output, with SHA-256 identical to `historical-results.json`. No retained
Git revision matched because that description was uncommitted when probed. The
fixture README records the retrieval coordinates and preserves its MIT/Klei
source licence. The temporary recovery script is retained for review under this
task's ignored runtime directory; reassess after fixture lineage review.

All 16 original description checks initially produced ten failures. An isolated
public-parser reproduction also failed: `[Fixed]`/`[sd]` without closing tags owned
later known syntax. The native Chevrotain grammar now distinguishes those literal
unpaired labels from explicitly paired unknown constructs. One token-offset map
supports that grammar predicate without repeated input searches. GFM text values
normalize LF/CRLF at the target boundary while source syntax remains unchanged.
All 16 checks then passed against the hash-verified original, including prose order.

The 17 original probe inputs now each have an independent semantic oracle executed
against the new converter. Sixteen passed initially; the spoiler case failed.
Documented constant `<details>`/`<summary>` HTML now handles block spoilers with an
approximate-fidelity diagnostic. Underline currently uses the plan-authorized
plain-text fallback with explicit loss; no verified underline-HTML claim is made.
All 33 historical cases now pass these accounted-for expectations. This is not
registry-wide conformance or perfect preservation of every presentation feature.

Further observed RED/GREEN slices cover attributed quotes, block spoilers with
lists/quotes, pull quotes, historical color and table layout metadata. Named
attributes use a second small Chevrotain grammar over its existing lexer; malformed
or duplicate assignments do not become a silently truncated map. This is the
ongoing Steam attribute syntax boundary, not a replacement parser engine or shim.
The latest runtime suite has 79 passing tests and TypeScript 7 strict checking
passes. Coverage measurement exposed three untyped `Set` references (99.94%);
the set now explicitly owns string keys. Fresh complete checks remain required.

Diagnostic identifiers are now a closed JSDoc union, and metadata source positions
are readonly. Negative consumer fixtures first failed as unused expectations,
then passed after the owning contracts were tightened. Generated MDAST still has
ordinary editable native positions, copied from the immutable source syntax.

The 79-test milestone then passed the full package check: 25 parser qualification
checks, TypeScript 7 checking, 5935/5935 typed references, 36 repeatable declaration
artifacts, positive/negative declaration fixtures and installed consumers.

An opt-in GitHub renderer probe subsequently sent only synthetic public text to
the documented unauthenticated Markdown endpoint. The returned HTML removed `u`,
retained `ins`, and preserved generated collapsible blocks/tables/deletion while
keeping HTML-shaped source escaped. No source destination was fetched and no
repository or message was published. Raw request/response plus timestamp, request
ID and SHA-256 hashes remain in the package's ignored `artifacts/github-rendering`
directory for independent review and pre-release reassessment. This is live API
evidence, not a browser visual check or human acceptance.
# Media interpretation slice (R2, accepted v2 baseline)

Test-first contract: the community guide's YouTube identifier/layout example
becomes a narrowly typed Steam media node, then a native Markdown link with an
approximate-fidelity diagnostic. Unknown layout, unsafe identifier, surplus
parameters and nonempty unqualified body content retain their complete source.
No network lookup or arbitrary HTML is part of conversion. The independent
target oracle is `mdast-util-from-markdown` plus literal expected URLs; the
source oracle is the guide's authored parameter example. First run through the
repository-selected npm 12 package test entry point discovered 84 tests: 83
passed and the new positive media case failed at the missing media-node
assertion. Existing malformed-media preservation controls already passed.
Generated consumer type fixtures were added before the new variant. Affected
scope is all source interpretation and GFM lowering tests; the full R2 verifier
remains required after the implementation is complete.

YouTube reached 84/84 runtime checks. The video slice then produced one intended
failure (90/91 passed), followed by 91/91, with URL, poster and playback fields
retained in a discriminated media contract. The preview-image slice failed its
two missing-node assertions (97/99 passed) and then reached 99/99. Screenshot
URLs become native images with alt text; unresolved guide IDs preserve the full
source and report `STEAM_GUIDE_IMAGE_UNRESOLVED`. The source does not specify
previewicon parameters or screenshot layout field order sufficiently to claim
those forms; tested literal preservation remains the policy. All authored
media contracts pass primary TS 7 and isolated TS 6 type coverage (6804/6804 at
that point). Generated declarations and negative consumer contracts passed.

Plain URL recognition reused the current stable, unmodified `linkify-it` 6.1.0
after inspecting its archive, declarations and MIT licence. Its qualification
passed before implementation. Six recorded URL-family tests then failed at the
missing native link (101/107 passed); native link metadata and diagnosed GFM
lowering brought the suite to 107/107. Literal punctuation, CRLF/Unicode source
positions, opaque regions, existing link labels and deceptive hosts are checked.
No URL is fetched. Vimeo/Sketchfab recognition is not a claim of profile support
or current Steam rendering. The installed JavaScript smoke now exercises all
three current public functions; its filename reflects that expanded scope.

The subsequent full package check passed 107 runtime tests, 25 parser checks,
7272/7272 type coverage, 42 reproducible declaration artifacts, type contracts
and actual installed JavaScript/type consumers. While GREEN, the grammar's
ambiguous `rule` and `attributes` names became `horizontalRule` and
`tagHeaderTail`, including all source-projection consumers.

Registry qualification found that bracket expansion had incorrectly borrowed a
different parsing-space example. The unreleased draft now classifies that rule
and viewer-dependent word filtering as context-only policies with explicit
reasons. Native schema tests enforce empty source examples only for these
non-observable renderer records; real source constructs still require examples.
Two tests failed before this correction, then passed. Renderer text handling
subsequently exposed its two intended missing-diagnostic failures and reached
112/112: emoticon tokens retain text and positions; clan placeholders do not
become active relative images. Opaque source is excluded from those observations.

The authored registry conformance cases execute canonical, nested, escaping and
malformed contexts through the source API and a native GFM consumer. Initial
failures revealed one draft list-item identifier typo and the important
syntax/renderer distinction documented in software selection. The maintainer's
documentation and an independent live GitHub REST response both justified
separating GFM syntax compilation from its optional renderer transforms. The
original failing log remains `artifacts/conformance-red.log`; no source fixture
was changed to hide the URL issue. A separate test then failed on the missing
renderer warning, before adding a native full-consumer check. Current runtime
suite: 146/146. Primary TS 7 passes. Full package and R2 verification are still
required after the remaining work.

The forward proof gate subsequently passed 312 runtime tests, 25 parser checks,
198 canonical/nested/literal/malformed case-profile executions covering 37 source
constructs and two separately tested context-only policies. The generated JSON
and Markdown reports cover all 39 registry entries. The strict supplementary
checker found eight inferred-any references introduced by native array assertion
narrowing in tests. Checked unknown-array contracts and a non-narrowing runtime
array assertion resolved them: 8977/8977 typed references, 48 reproducible
declaration artifacts and both installed consumers passed with primary TS 7.
The user updated global npm to 12.0.2; direct npm now matches the selected version.

The user then asked whether to commit the forward-conversion milestone. An exact
forward-only commit draft is being prepared; no commit is yet authorized. Newly started
reverse work is preserved with matching file hashes under the ignored task-owned
`.sdlc/runtime/converter/reverse-work` directory and excluded from this checkpoint.
Its latest runtime state was 328/329: the source/tree/output quota test is RED.
Resume that test-first slice after this checkpoint; do not treat it as completed.
Owner: this task. Reassessment/removal trigger: merge its specific source changes
back into the working files and verify the reverse slice, preserving user edits.

The forward-only checkpoint passed a fresh `npm run sdlc -- verify --keep-going`
on 9 September 2026 (local date). All selected repository controls and the full
converter check passed: 312 runtime tests, 25 parser qualification checks, 198
source case/profile executions, 8986/8986 typed references, 48 repeatable
declaration artifacts and installed JavaScript/TypeScript consumers. Raw output
is retained in `.sdlc/runtime/converter/forward-checkpoint-verification.log`; the
repository-owned verifier records its actual input identities in
`.sdlc/runtime/verification/full.json`. This checkpoint is not independent review,
runtime coverage/mutation qualification, cross-platform CI or release acceptance.

## Forward refinements (9 September 2026)

The forward checkpoint was subsequently committed with the owner's explicit
approval as signed local commit `e51073134c1e306f8bf0f25b8a9fe6be1480b22c`.
The discussion after that checkpoint authorized the linked
[forward refinement amendment](2026-09-09-steam-community-bbcode-forward-refinements.md).
This is a bounded repair within the existing R2 route and unchanged v2 baseline.
The prior commit approval does not authorize a further commit or push.

The literal-label tests first failed four cases because the parser reported an
unclosed tag for labels that its grammar already treated as unpaired literal
source. Restricting that diagnostic to recognized tags produced 319/319 runtime
and 25/25 parser passes. Unknown labels still receive one preservation warning;
known malformed tags, unknown paired markup and opaque noparse have separate controls.
Public source spans, visible text, emphasis and link destinations are asserted.

The first whitespace regression failed on `"\n\nBody\n\n"` at the MDAST boundary
(320/321 passed). Flow construction now trims ordinary source text only at explicit
block boundaries and list-item edges. Follow-up list tests failed for LF, CRLF and
CR before the list-edge change. A nonbreaking-space preservation check also failed;
flow layout now recognizes only ASCII space/tab/newline characters. A further RED
showed that nonbreaking prose preceding the first list marker was silently discarded;
that case now uses the existing whole-list preservation diagnostic. Source syntax
remains immutable; original positions, inline and interior spacing, multi-paragraph
items, nested lists, code/noparse and malformed/unknown fallback are preserved.

The existing opt-in GitHub renderer gained a synthetic `flow-whitespace` scenario.
It compares actual output with independently authored expected Markdown through the
same native HTML endpoint. The initial request failed on loose-list paragraph
wrappers and padding. After the change, actual and expected HTML matched exactly,
including two-space padding in protected literal text. Both failing and passing
request/response/provenance sets are retained under package
`artifacts/github-rendering/flow-whitespace-red` and `flow-whitespace`.
This establishes the selected target rendering contract, not universal Steam
renderer equivalence, browser visual acceptance or a release qualification.

Current local results: 329/329 runtime tests, 25/25 parser qualification tests,
198 conformance case/profile executions for 39 registry policies, primary TypeScript
7 checking and consumer contracts, 9679/9679 typed references in the approved isolated
TypeScript 6 coverage tool, 48 reproducible declaration artifacts and installed
JavaScript/TypeScript consumers. The full repository verifier passed. Its first run
correctly rejected the old generated report fingerprint; regeneration changed only
`canonicalLfInputsSha256` in `coverage.json`, leaving all conformance outcomes and
the support matrix unchanged. No fixture expectation or gate was weakened.

The committed mod description was rerun from its exact Git blob, SHA-256
`6200c0a0ad51ae4270cb231eadae9b4192ae9138933e7fa5344c7fa8a65c95ef`.
Its output retains six headings, six images, six links, four lists and 17 list items.
The four lists are tight, redundant blank lines and list-padding entities are gone,
and the two remaining warnings accurately preserve `[sd]` and `[Fixed]`. Source text
was not edited. The 6388-byte output SHA-256 is
`d48b3c8b6da11d7af1778edb7df002e1f82bcfc146da5f3d782eaa6ca3d3f054`.

Evidence is retained under `.sdlc/runtime/converter`: `literal-brackets-*.log`,
`flow-whitespace-*.log`, `forward-refinements-*.log` and the description Markdown/JSON.
The failed `npm exec` startup attempt is explicitly retained as
`flow-whitespace-block-startup-error.log`; it was not counted as behavioral RED.
The supported package test script then produced the actual assertion failure.
The passing full run is `forward-refinements-verification-green.log`; the verifier
refresh after this record update is `forward-refinements-final-verification.log`,
with exact input identities in `.sdlc/runtime/verification/full.json`.

No new dependency, configuration, public interface, checker suppression or shim was
introduced. The combined implementation self-check found no naming/ownership mismatch;
it is not independent review. All new tests exercise public interfaces and native
consumers. This follow-up's local artifacts are retained review evidence owned by
this task, to be reassessed after review under the repository retention policy.
There is no spent follow-up scratch dependency to remove. Earlier parked reverse
work remains an active downstream input. Independent review/security qualification,
cross-platform CI, runtime coverage/mutation gates and release acceptance remain
outstanding under the original plan; this amendment does not claim them complete.

Final-snapshot qualification: every command in the final repository refresh passed,
including converter checks and generated configuration. The verifier nevertheless
correctly recorded `passed: false` because `workspaceFingerprint` changed during
the run. Concurrent, externally owned edits appeared in `scripts/set_up_sdlc.py`
and `tests/sdlc/test_pipeline_controls.py`; Max confirmed another task was still
editing this worktree. Those files were preserved without modification by this task.
The failed freshness result and exact before/after identities are retained in
`forward-refinements-snapshot-changed.log` and `.json`. The separately refreshed
package check is recorded in `forward-refinements-converter-final.log`; it does not
substitute for the repository-wide freshness gate.

On resumption, the concurrent SDLC edits had been merged at
`eb58b4c3118b44313de0d8ca78950990641276b7`; only this refinement's nine paths remained
changed. The final command is `npm run sdlc -- verify --keep-going`, with output
retained in `.sdlc/runtime/converter/forward-refinements-resumed-verification.log`.
Its authoritative outcome and exact before/after input identities are recorded in
`.sdlc/runtime/verification/full.json`. Completion requires `passed: true` and
identical identities; this record is prepared before the run so that documenting
the result does not invalidate the snapshot it describes.

## Partial reverse slice resumed (9 September 2026)

The owner preapproved subsequent detailed per-file commits and configuration
changes explicitly stated in the implementation plan, and requested continuous
execution until a genuine unresolved decision. Forward refinements are recorded
in signed commit `a1602c61f1742e3ca24b8f5284edbbde36cba14f`; its tree, message,
parent and SSH signature were verified. Publication still requires its own authority.

Continue the accepted v2 partial-reverse gate on the existing R2 route, inline
under the repository-adapted TDD skill. Bring back only this task's three parked
new files and merge the reverse public contracts into current source. The saved
tests and implementation retain their previous chronology; the source/tree/output
quota regression is the next RED, not a new claim of test-first authorship.

The maintained native GFM parser and extension remain the selected syntax owners;
their public composition contracts and Valve's Workshop formatting help were
rechecked on 9 September. No dependency or licence change is needed. The custom
gap is the qualified Steam target mapping, literal encoding, diagnostics and
resource accounting. Unsupported GFM must be preserved as literal source, with
its own per-node outcomes; forward registry coverage cannot imply reverse support.

Sequence: reproduce the saved quota RED; enforce validated UTF-8 input/output
and MDAST node/depth bounds; qualify literal and block preservation with independent
expected trees and exact target syntax; generate the reverse subset report from
executed cases; extend checked declarations and actual installed consumers; then
run the package and full repository verification before the next signed checkpoint.
The decisive tests exercise the public function, native GFM tree and Steam syntax
consumer with hand-authored expectations. Source input is never fetched or evaluated.
Focused command: `npm --prefix tools/steam-community-bbcode test`.

The resumed suite reproduced the saved quota failure (345/346 passed). Splitting
the quota scenarios into independently executable cases exposed all four missing
bounds and invalid-option acceptance. After validated GFM-specific limits and
input-scoped failure results were added, 350/350 passed. Node count includes the
root at depth zero; UTF-8 bytes and exact-limit acceptance have separate controls.
Tree bounds run after native parsing and before rendering. Subtree output checks
return an empty target on failure; neither these checks nor the input bound claim
a native-parser timeout or intermediate-allocation ceiling.

Four further RED cases showed unsupported relative links/images and uneven tables
being advertised as equivalent. Native WHATWG absolute-reference checking and a
rectangular-table eligibility check now preserve those sources with diagnostics;
354/354 passed. The checked source API, URL scheme policy and immutable forward
behavior remain intact. The shared diagnostic-code inventory was renamed from
`steamConversionDiagnosticCodes` to `conversionDiagnosticCodes` because it now
owns both directions; its type consumer was updated with it.

The independent reverse corpus executes 33 authored cases spanning 23 native
MDAST node kinds. Supported cases assert exact Steam syntax and hand-authored
semantic trees. Unsupported cases assert complete literal source and absence of
active injected tags; every native source occurrence is matched with reported
coverage exactly once. The existing report generator and shipped report/matrix
carry these results in separate reverse sections, using no forward outcome as
reverse evidence. No package configuration change was needed. Two additional
seeded properties ran 1,000 cases each: literal text through native Markdown
escaping, and bounded arbitrary Markdown. The maximum supported renderer depth
and next-level failure are exercised through the actual source and target parsers.
Current runtime total: 390/390.

The first package check correctly found stale declarations when the new packed
consumer referred to the added export; the normal build regenerated them. A later
strict type-coverage check found two inferred-any references to the result local;
an explicit string JSDoc annotation resolved them without suppressions or aliases.
`reverse-package-green.log` records primary TypeScript 7 checking, 11344/11344
typed references in the isolated authorized TypeScript 6 tool, 54 reproducible
declaration artifacts, positive/negative type contracts and actual installed
JavaScript/TypeScript consumers. All 198 forward case/profile executions and
25 parser qualification tests still pass.

Final repository command: `npm run sdlc -- verify --keep-going`, output retained
in `.sdlc/runtime/converter/reverse-final-verification.log`. The authoritative
result and input identities are `.sdlc/runtime/verification/full.json`; a stable
pass requires `passed: true` and identical before/after identities. This record
is finalized before that run. The prior failures remain in `reverse-resumed-red.log`,
`reverse-limits-red.log`, `reverse-context-red.log`, `reverse-package-check.log`
and `reverse-package-check-after-build.log`; corresponding GREEN and generated
report logs are retained alongside them.

This task owns the retained reverse evidence. The parked pre-limit source remains
a concrete reproduction/review input until independent review is complete; it is
no longer an implementation-restoration dependency. No spent scratch dependency
was introduced. The combined self-check covered changed names, native reuse,
literal preservation, consumers and higher-outcome alignment; it is not independent
review. CLI delivery, comparative evaluation, broader runtime/mutation qualification,
independent review/security, cross-platform CI and release acceptance remain later
plan gates. This checkpoint authorizes no publication.

## Comparative validation slice (R2)

The partial reverse checkpoint is signed commit
`a4d3bb04dbb7d0bc107c97cffb762c120e67ac1e`, with matching tree/message/parent and
verified SSH signature. Its full repository snapshot and a separate package run
starting without generated declarations passed. Continue the accepted comparative
conformance gate using unmodified tools and their documented public interfaces.

The primary registries currently identify Steamify 2.0.1, bbcode-to-markdown 1.0.3,
Steam Editor Tools 0.5.1 and Converter.MarkdownToBBCodeSteam.Tool 1.0.0.29.
Their upstream MIT licence texts and documented conversion directions were read.
Steamify covers both directions, generic bbcode-to-markdown covers forward
conversion, and the other two provide Markdown-to-Steam conversion. The native
Steam Editor Tools parse_markdown/BBCodeRenderer path consumes supplied text;
its unrelated Steam queries and image-processing APIs are outside this slice.
The .NET tool's documented intentional interpretation of literal BBCode must be
reported as a policy difference, not disguised as an undocumented defect.

Implementation/configuration details within the owner's standing plan approval:

- Create `tools/steam-community-bbcode/comparison/node/package.json` as a private
  AGPL-3.0-only benchmark environment with only `bbcode-to-markdown: 1.0.3`, and
  its native npm lockfile. Install with lifecycle scripts disabled; this graph
  must not enter the runtime package.
- Create `comparison/python/requirements-windows-py314.txt` under the same package
  from pip's native resolution report, pinning both Python comparators and their
  transitive versions/hashes for this Windows Python 3.14 qualification. Use the
  checkout's `.venv` interpreter and an isolated ignored `artifacts/comparison/python`
  target, preserving the SDLC environment's installed packages.
- Install the exact .NET comparator with native `dotnet tool install --tool-path`
  under ignored `artifacts/comparison/dotnet`; retain its package/framework evidence.
  Preserve the root .NET toolchain and all global tool settings.
  The host NuGet configuration is unreadable to this task. Add
  `comparison/dotnet/NuGet.Config` with `packageSources/clear` and only
  `https://api.nuget.org/v3/index.json`, then supply it through native `--configfile`.
  This confines benchmark resolution to the public registry without modifying
  host configuration. Native installation still attempted an early host-config
  read; narrowly elevated execution of that same install succeeded. No host file
  or permission was changed.
- Add `comparison:run` to the converter package's npm scripts, invoking
  `node scripts/compare-alternatives.js`. This is an explicit benchmark command;
  ordinary consumers never install or run comparators.
- Maintain checked JavaScript and Python protocol adapters, authored common
  corpus/expectations, classifier regressions and raw results with versions,
  identities, capability applicability and per-case explanations. Test only
  documented directions; retain unavailable/inapplicable states honestly.

Use native parsers for output structure and independently authored expected
semantics. Do not compute expected results from the candidate or patch an
alternative to make it pass. Preserve raw exceptions and differing target policies.
Compare semantic outcomes without a weighted superiority score. Fixture and
classification changes require meaningful RED/GREEN or an explicitly recorded
preservation/qualification route; finish with actual benchmark execution and full
repository verification. Live security review and independent acceptance remain
separate authorities.

Comparative implementation uses the test-first classifier boundary: four authored
classification scenarios failed against the executable empty seam, then passed
after implementation (`comparison-classifier-red.log` / `comparison-classifier-green.log`).
The unchanged external APIs use characterization/qualification: actual installed
tools consume the common authored corpus, with no fabricated RED for third-party
behaviour. Native Python text streams explicitly preserve source/output newlines.
The first execution is retained in `comparison-first-run.log`; ordinary controls
were then added so escaping stress cases cannot be mistaken for every ordinary
conversion failing. Final execution is `comparison-final-run.log`.

The maintained comparison README and raw result explain observed failures,
declared passthrough, intentional literal-BBCode interpretation and the reverse
consumer's unverified code-padding semantics. Exact spelling is not ranked above
equivalent structure. All 33 historical Steamify regression obligations remain
in the normal test suite. The comparison graph is excluded by the existing
package file allowlist and remains isolated from runtime/type-checker selection.

Final repository verification command for this slice:
`npm run sdlc -- verify --keep-going`, captured in
`.sdlc/runtime/converter/comparison-final-verification.log`; the authoritative
pass and matching before/after identities are recorded in
`.sdlc/runtime/verification/full.json`. Comparator installations and exact CLI
inputs remain task-owned independent-review reproduction inputs; no spent
scratch is required by ordinary package checks. Independent review/security,
CLI, runtime/mutation qualification and release gates remain outstanding.

## CLI delivery slice (R2)

Comparative evidence is signed commit `7ca087993dcdb52f06c8af8f0dcaeca88eaa7893`.
Continue the accepted CLI contract with Node's stable native `util.parseArgs`,
filesystem streams and fatal UTF-8 decoding; no additional argument-parser or
compatibility dependency is needed. The Node 24 official util/test documentation
was checked. The residual custom work is command policy and conversion I/O.

Configuration within the owner's standing plan approval: add the package's
`bin.steam-community-bbcode = src/cli.js` and `scripts.cli = node src/cli.js`, then
refresh its native npm lockfile metadata. The existing source allowlist and
declaration configuration already include CLI source. Preserve the runtime
dependencies and both approved TypeScript versions.

Implement `to-gfm`, explicitly partial `to-steam`, and `coverage --format=json`.
Conversion reads a file or stdin (`-` or omitted), writes only the result to
stdout and JSON diagnostics to stderr. `--format=json` emits the public result
model. `--fail-on=approximate|lossy|unsupported` rejects that fidelity or worse;
the default is `none`. Conversion errors always fail. Preserve output for
inspection even on a fidelity failure, and document that consumers must check
the exit status before adopting it. Exit codes: 0 accepted, 1 conversion/fidelity
failure, 2 argument/I/O/encoding failure. No output-file overwrite option is added.
`--profile` applies only to forward conversion. Bound input while reading, with
the existing default byte limit and a validated `--max-input-bytes` override.

Use test-first real child-process scenarios for output, diagnostics, thresholds,
invalid options, file/stdin parity, input bounds and malformed UTF-8. Exercise the
actual npm-installed binary through the existing archive consumer check, with
network-disabled npm exec resolution. Regenerate declarations/reports and finish
with the package and repository verification entry points. This slice does not
qualify Linux execution or publish the package.

CLI RED/GREEN evidence is retained in `cli-red.log`, `cli-first-green.log`,
`cli-contract-red.log` and `cli-contract-green.log`. Closed stdout and stderr
pipes exposed genuine unhandled error paths; native descriptor writes and guarded
diagnostic emission preserve exit code 2 (`cli-pipe-red.log`,
`cli-diagnostic-pipe-red.log`, `cli-final-green.log`). All 23 real-process CLI
scenarios pass. The conversion resource-error case was already satisfied and
was retained as qualification, without manufactured RED.

The packed consumer first failed because no binary was declared, then passed
after the approved manifest/native-lock update (`cli-installed-red.log` and
`cli-installed-green.log`). The actual npm-installed executable handled help,
stdin conversion and the shipped report through offline resolution. The existing
allowlist excludes comparison tools; no extra installation hooks were added.

Final check: `npm run sdlc -- verify --keep-going`, recorded in
`.sdlc/runtime/converter/cli-final-verification-green.log`, with the authoritative full
verification identity in `.sdlc/runtime/verification/full.json`. Test-owned npm
consumer directories were removed by the established bounded cleanup. Retain CLI
RED/GREEN and package logs for independent review. Current declarations and
conformance evidence are regenerated through their normal package commands.

The first full run (`cli-final-verification.log`) found four implicitly broad
references in native iterator return parameters and child-exit promises. Explicit
native generic parameters and `number | null` promise results restored 100% typed
references (12703/12703) without aliases, shims or suppressions. Primary TypeScript
7 checking also passed. The final run follows those annotation changes.

## Runtime coverage and mutation slice (R2)

The CLI checkpoint is signed commit `7fc921225cbb95c0ccc67d975b95908d6d4c7009`.
Its full verification passed with matching identities, 417 runtime tests,
12703/12703 typed references, 56 reproducible declarations and installed API/CLI
consumers. Continue the accepted numerical quality gate without lowering its
98% statement/line/function, 95% branch and 90% mutation thresholds.

Primary registry/documentation research selects c8 12.0.0 (ISC),
@stryker-mutator/core 10.0.0 and @stryker-mutator/tap-runner 10.0.0 (Apache-2.0).
Node's native coverage command does not expose a separate statement threshold;
c8 uses native V8 coverage and maintained Istanbul reporting, including unloaded
source with `all`. Stryker's official TAP runner explicitly supports `node:test`
without changing test frameworks. The proposed dedicated Node runner remains an
unmerged upstream PR; do not use unreleased code or a custom runner shim.

Exact configuration changes covered by the owner's standing plan approval:

- Add those three exact development versions to the converter `package.json`
  and update its native npm lockfile with lifecycle scripts disabled. Inspect
  installed licence texts and retain native notices before execution. Do not
  install a TypeScript checker plugin or another TypeScript compiler.
- Add `.c8rc.json` in the converter package with `all: true`, source `src`,
  inclusion `src/**/*.js`, thresholds statements/lines/functions 98 and branches
  95, `check-coverage: true`, and text/JSON/HTML/LCOV reports under ignored
  `artifacts/runtime-coverage`. Add `test:coverage = c8 node --test test/*.test.js`.
  Replace the ordinary `npm test` invocation within package `check` with this
  superset, retaining `npm test` as the focused runtime entry point.
- Add `stryker.config.json` using the official TAP plugin over existing runtime
  tests plus parser qualification, mutating library `src/**/*.js` except CLI.
  CLI is a separate `stryker.cli.config.json` profile using Stryker's built-in
  command runner and the real CLI subprocess tests. This avoids assuming that
  TAP's in-process coverage can see a spawned CLI. Both profiles enforce 90%.
- Both mutation profiles use native sandboxes (`inPlace: false`), four workers,
  explicit `disableTypeChecks: false`, local JSON/HTML/text reports, and ignored
  task-owned `artifacts/mutation-*` temporary directories. No dashboard upload.
  Non-test comparison installations, generated types and artifact trees are not
  copied into sandboxes. Library TAP coverage is per test file; CLI command
  coverage analysis is off because that runner cannot provide it.
- Add `test:mutation:library`, `test:mutation:cli`, and sequential
  `test:mutation` npm entry points. Qualify both native runners before starting
  full mutation work. Preserve failures and survivors as evidence; do not hide
  survivors, ignore mutants or add coverage/checker suppression comments to pass.

Use coverage/survivors to find meaningful missing contracts, not mirror code.
Strengthen the CLI filename test so its actual relative argument begins with a
hyphen, rather than only the basename of an absolute path. Existing correct
behaviour uses qualification, without manufactured RED; discovered defects use
the repository test-first procedure. A changed behavioural requirement returns
to the accepted baseline authority. Record measured scope and tooling limitations
before claiming the release gate, then run current full verification.

Sources: [c8](https://github.com/bcoe/c8),
[Stryker TAP runner](https://stryker-mutator.io/docs/stryker-js/tap-runner/),
[Stryker configuration](https://stryker-mutator.io/docs/stryker-js/configuration/),
and [unmerged Node runner PR](https://github.com/stryker-mutator/stryker-js/pull/6020).

Initial measurement (`runtime-coverage-initial.log`) was 95.51% lines/statements,
95.91% functions and 90.19% branches. c8 assigned fictitious uncovered runtime
functions to three modules containing only JSDoc and `export {}`. Refine
`.c8rc.json` to exclude exactly `src/diagnostics/conversion-result.js`,
`src/mdast/steam-mdast-nodes.js` and `src/steam/steam-bbcode-syntax.js` from runtime
measurement; their contracts remain checked by TypeScript/declaration consumers.
Do not exclude `diagnostic-codes.js`, which contains actual array construction.
Include parser-qualification tests in `test:coverage` and remove their duplicate
execution from `check`; retain `qualify:parser` as the focused entry point.

The first Stryker dry run found 3750 library mutants, then failed because its
tsconfig sandbox rewriter calls the removed TypeScript compiler API
`parseConfigFileTextToJson` (`mutation-library-dry-run.log`). Installed source
confirms that preprocessing runs only for a tsconfig present in the sandbox file
set. Refine both profiles' supported `ignorePatterns` with `**/tsconfig*.json`:
these runtime JavaScript mutation tests do not consume compiler configuration.
This removes unnecessary compiler-config copying/rewriting, not primary checking.
No vendor modification, fake configuration path, compiler downgrade or type-check
suppression is permitted. Primary checks continue using the real TypeScript 7
configuration before/after mutation. Installed Stryker licences match SHA256
`b40930bbcf80744c86c46a12bc9da056641d722716c378f5659b9e555ef833e1`;
c8's ISC text is `1a9c55b2961f5e3062e181fa55666c44d011fc3cc03ed6d4fa4321c51f2a5ec5`.
Their attribution, notice and patent terms remain intact in native installations.

The next dry run exposed an overly broad sandbox ignore pattern: `comparison`
also matched the maintained `scripts/comparison` test dependency. Anchor all four
non-test directory ignores to the package root (`/artifacts`, `/comparison`,
`/tooling`, `/types`) in both profiles. Retain this setup failure separately;
it is not a behavioral mutation result. The scoped coverage run now passes
line/statement/function thresholds (98.48/98.48/98.94%) and identifies the real
remaining branch gap (92.04%).

The corrected TAP baseline passes all 30 test files, but Stryker's native Windows
worker cleanup is denied inside the restricted process sandbox. The host retry
then exposes Stryker's unrelated recursive node_modules discovery entering a
protected Python bytecode cache. Use the supported `symlinkNodeModules: false`
setting in both profiles: their sandboxes already sit below this package, so
native Node ancestor resolution finds this package's exact installed dependencies.
No symlink, loader override, cache permission change or vendor patch is needed.
Qualify that resolution and worker cleanup in a narrowly scoped host dry run.
Retain all setup failures separately from behavioral mutation evidence.

Coverage qualification will exercise malformed and variant preview-image fields,
quoted resource attributes, optional table layout, JavaScript input rejection and
the existing typed Steam-MDAST lowering boundary. Authored trees at that boundary
must satisfy its native content contracts and use independent expected GFM trees;
they do not imply that every tree shape can be authored in Steam's grammar.

The qualified native TAP and command dry runs complete successfully on the host
(`mutation-library-native-resolution-dry-run.log`, `mutation-cli-dry-run.log`).
Runtime coverage now passes at 98.86% lines/statements, 98.94% functions and
95.59% branches with 479 tests (`runtime-coverage-contracts-green.log`). The first
CLI mutation run kills 163/184 mutants (88.59%); all 21 survivors are retained in
`artifacts/mutation-cli/initial-mutation.json`. Strengthen actual CLI contracts:
nonempty actionable errors, validation before I/O, default coverage formatting,
combined invalid options, the one-byte minimum and overrides above the default
reaching both converters. These already-correct cases pass without manufactured
RED. The invalid extra-file fixture now uses real stdin so ENOENT cannot mask a
removed argument-count check. The test process buffer accommodates the actual
1-MiB code-payload result. The complete run will test these assertions' sensitivity.

## Security documentation and inventory slice (R2)

Continue the accepted pure-transformation security boundary while numerical
mutation qualification runs. Apply the standing approval to create exactly
`tools/steam-community-bbcode/SECURITY.md`, scoped to this separately licensed
package: untrusted text/URLs, trusted caller-selected limits and CLI paths,
no source-triggered I/O/evaluation, structural escaping and opaque preservation,
bounded-input failure semantics, reportability based on actual reachability and
impact, and explicit parser-allocation/timeout limits. No finding exclusions,
severity caps, risk acceptance or scanner bypasses are added. Documentation is
not independent security acceptance. The private GitHub reporting endpoint is
enabled (read-only API check, 9 September 2026); use the repository's native
private advisory form without promising an unapproved response SLA.

Add factual `docs/security-model.md` and `docs/testing.md` covering the accepted
contracts, observed controls and qualification gaps. The policy path and boundary
are already required by the accepted baseline; the security-policy skill's
per-edit approval is covered by the owner's explicit standing configuration
approval. Resolve its effective policy after writing.

For the locked dependency/licence inventory, reuse npm 12.0.2's built-in
`sbom --package-lock-only --offline --ignore-scripts --sbom-format=cyclonedx`.
It supplies the actual lock graph, declared licences and archive hashes without
an extra inventory library or network access. Retain raw output, lock hashes,
native version and exact command. Declared metadata is not licence-text review
or vulnerability/provenance qualification. The first parent-graph output contains
299 components with no missing licence field; it includes platform-specific
optional packages from the lock, not only this host's installed subset.

The revised CLI mutation run passes: 181 killed, three survived, no timeout,
uncovered or error results; score 98.37% (`mutation-cli-contracts.log`). Survivors
14 and 16 change a defensive non-Buffer guard; the supported CLI streams produce
Buffers. Survivor 75 removes `readFile`'s UTF-8 option, yielding a Buffer which the
native descriptor writer emits as the same report bytes. Keep all three in the
denominator; no suppression or contrived stream replacement is introduced.

While inspecting the library's generated data contract, add a runtime projection
test against the validated, authored JSON specification. Existing `spec:check`
compares generated file text; this test independently checks loaded field values,
identities, default profile and collection immutability. Neither source spelling
nor the generator is the expected-value oracle. The current baseline passes.

## CI qualification configuration (R2)

Apply the standing configuration approval to these exact changes:

- Converter `package.json`: add `sbom` and `sbom:type-coverage` commands invoking
  npm's native offline, lock-only CycloneDX output for each approved graph. These
  commands print metadata without writing policy, contacting a registry or running
  installation hooks. Refresh native lock metadata and conformance input identity.
  Add `SECURITY.md` to the existing package-file allowlist so installed consumers
  receive the reporting policy. Expand the existing single-doc entry to `docs/`
  so that policy and API documentation links resolve in the installed package;
  source-only commands remain identified as source-checkout workflows.
- `.github/workflows/steam-community-bbcode.yml`: retain the six existing OS/Node
  combinations and npm 12.0.2. Generate both SBOMs after dependency installation;
  run the two mutation profiles once on Ubuntu/Node 24.20.0, in addition to the
  coverage/type/consumer checks on every matrix entry. Retain coverage, mutation
  and SBOM artifacts for 30 days even after a check fails. Use a 60-minute job
  timeout for the complete mutation job. This does not establish any remote pass.
- Add a pull-request-only dependency-review job in that workflow, selected by the
  existing converter scope command. Use `contents: read`, fail on all severities
  from low upward and runtime/development/unknown scopes. Keep native licence
  reporting, with no invented allowlist or legal-approval claim. Disable PR
  comments explicitly; no write permission or report messages are needed.
- Extend only the existing npm entry in `.github/dependabot.yml` from root-only
  `directory` to `directories` including this package, its approved type-coverage
  package and isolated Node comparison package. Preserve schedule/grouping and
  zero cooldown. These are update proposals, subject to compiler/qualification
  constraints, not automatic dependency acceptance.

Native GitHub release metadata resolves `actions/upload-artifact` v7.0.1 to
`043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` and
`actions/dependency-review-action` v5.0.0 to
`a1d282b36b6f3519aa1f3fc636f609c47dddb294`. Their exact action interfaces use Node 24
and their inspected licence texts are MIT with GitHub attribution retained in
the native Actions distribution. Pin those full commits; reuse the already pinned
checkout/setup-node actions. The existing CodeQL workflow already includes
JavaScript/TypeScript, uses pinned actions and scoped upload permissions; preserve
it and report its fork-PR exclusion as a CI coverage limitation. Branch-protection
adoption and live execution are separate platform actions.

Sources: [native SBOM](https://docs.npmjs.com/cli/v12/commands/npm-sbom/),
[dependency review](https://github.com/actions/dependency-review-action),
[artifact action](https://github.com/actions/upload-artifact).

The initial current full route passes (`quality-initial-full-verification.log`):
483 tests under coverage, 13408/13408 typed references, 56 reproducible declarations
and installed consumers. Its matching workspace fingerprint is
`3e6f05f4efc2e8d46bfb0af885f6bcd783199381e131ada4a3b1224b0b27d985`.
Subsequent test/documentation changes require a fresh final route. The additional
diagnostic contract tests require unique nonempty catalogue identifiers and
nonempty explanations for actual conformance/resource outcomes, including all
three diagnostic scopes. They keep executable diagnostic catalogues in runtime
coverage and test those data contracts without copying their full identifier list.

Native SBOM commands emit parseable JSON for 299 parent-graph components and 37
isolated type-coverage components (`quality-package.cdx.json`,
`quality-type-coverage.cdx.json`). The command guard rejected a separate validation
redirection to `$null`; no command ran. Direct JSON parsing without redirection
succeeded. No guard settings or permissions were changed.

The first complete library mutation run fails the 90% gate: 2209 assertion kills,
8 timeouts, 1345 survivors, 86 uncovered mutants and 102 errors, for 60.77% under
Stryker's native denominator. Preserve its unmodified report as
`artifacts/mutation-library/initial-mutation.json`. The subsequent CLI run passes
at 98.37% (181 kills, three survivors, no timeouts, uncovered mutants or errors).
Its three survivors concern the native stream's Buffer guard and equivalent
file-reading encoding; retain them in the denominator without suppressions.

Qualify missing library contracts using the authoritative registry JSON, emitted
diagnostic and outcome contracts, complete positive/negative URL families, actual
resource validation and the existing text-position module boundary. Include
meaningful media body content, whitespace-only media bodies and mixed image/URL
labels so that preservation cannot silently absorb active markup. Already-correct
behaviour passes without manufactured RED (`mutation-contracts-qualification.log`,
480 tests; TypeScript 7 qualification passes). TypeScript caught missing required
fixture metadata and an over-broad inferred specification type; correct fixtures
and use native property narrowing, with no assertions or suppressions.

Rerun the library gate with native Stryker incremental mode, seeded with an exact
copy of the completed report at `artifacts/mutation-library/incremental.json`.
Use `--incremental --incrementalFile artifacts/mutation-library/incremental.json`;
Stryker owns source/test-change detection and result reuse. No report fields,
mutator sets or thresholds are altered. The CLI implementation/tests remain
unchanged since their passing complete run. Native incremental reuse is described
in [Stryker's official guide](https://stryker-mutator.io/docs/stryker-js/incremental/).

## Documentation and lint completion slice (R2)

Complete the accepted package documentation with API/CLI/getting-started guides,
generated per-construct examples, comparison interpretation and a release guide.
Extend the existing conformance generator to emit `docs/conversion-semantics.md`
from already qualified canonical cases and explicit context-only reasons. Keep
native MDAST parsing/serialization and the authoritative registry; do not add a
second maintained support list. Check source-checkout documentation links through
the native Markdown parser and filesystem URL resolution. These documentation
checks establish local file targets, not remote availability or rendered acceptance.

Exact preapproved configuration changes for this slice:

- Parent `package.json`/native lock: add development-only `eslint: 10.10.0`,
  `@eslint/js: 10.0.1`, `globals: 17.12.0`; add `lint` invoking ESLint on `src`,
  `scripts`, `test` and `eslint.config.js`, with zero warnings permitted. Add
  `docs:check` for the documentation contract tests. Include lint and documentation
  checks in the existing `check` command; CI consumes that command unchanged.
- Create `eslint.config.js` using native flat configuration, the maintained
  recommended JavaScript rules and Node ESM globals. Keep all recommended rules
  enabled, disallow inline rule configuration, and use native `ignoreRestSiblings`
  for intentional property exclusion through object-rest composition. Lint only
  authored/package runtime JavaScript, not installed dependencies, generated
  declarations or isolated external comparators. No formatter, compiler plugin,
  custom parser, vendor patch, alias or checker suppression is introduced.

Registry metadata on 9 September 2026 selects those current stable versions;
their Node requirements include all six accepted CI combinations. Exact published
archives and full MIT licence texts were inspected under
`.sdlc/runtime/converter/selection`; retain OpenJS and Sindre Sorhus notices.
ESLint covers JavaScript mistakes beyond the TypeScript contract check; the
residual local configuration supplies scope and Node globals, with no custom
rules. Install after the running mutation snapshot finishes so its shared native
dependency environment stays unchanged. Sources:
[ESLint configuration](https://eslint.org/docs/latest/use/configure/configuration-files)
and [ESLint setup](https://eslint.org/docs/latest/use/getting-started).

The first lint run reports six findings: five unnecessary bracket escapes and
an explicit C0 control range used deliberately for URL rejection. Preserve all
recommended rules. Remove only redundant character-class escapes and express
the same C0/DEL/C1 set using native Unicode `\p{Control}`. Qualify accepted and
rejected boundary code points before that spelling-only refactor. No check is
disabled. The resource test already passes the original spelling.

The expanded parser fixture finds a behavioral RED: `[hr \t][/HR ]` produces a
separate unmatched closer, unlike other recognized closing tags. The native
ClosingTag token already admits trailing whitespace. Correct the horizontal-rule
gate to require that native token and compare its normalized name, preserving
optional closers and other following syntax. The retained failing assertion is
in `mutation-boundaries-qualification.log`; preserve it as
`horizontal-rule-whitespace-red.log` before running GREEN.

The dependency audit reports `qs@6.15.1` pinned by Stryker's
`typed-rest-client@2.3.1`, rather than introduced by ESLint. The latest supported
2.3.x client still pins that vulnerable release; changing Stryker's client major
or downgrading it would be broader and less justified. Apply this exact native
npm configuration change in the parent `package.json` and lock:
`overrides: {"typed-rest-client@2.3.1": {"qs": "6.16.0"}}`.
Use the real, unmodified current stable qs package. This replaces dependency
resolution only; no alias, wrapper, source patch or audit suppression is added.
Its full BSD-3-Clause terms and exact archive were inspected; retain copyright,
conditions, disclaimer and non-endorsement requirements. Refresh audit/SBOMs and
rerun Stryker on the final graph. Reassess/remove the override when the supported
client itself selects a patched qs release. The runtime converter has no qs edge.
Sources: [upstream advisory](https://github.com/advisories/GHSA-4mjr-xmp4-gh2g)
and [qs changelog](https://github.com/ljharb/qs/blob/main/CHANGELOG.md).

## Final diagnostic boundary qualification (R2)

The fresh library mutation run on the final dependency graph scores 89.90%
(3278 killed, 8 timeouts, 350 survived, 19 uncovered, 100 runtime errors), below
the unchanged 90% gate. Preserve its raw report before rerunning. Surviving
malformed-attribute mutations expose a meaningful missing caller contract:
unterminated quoted attributes must retain their source and identify the missing
header/quote/tag with nonempty explanations. Qualify both quote delimiters and
the exact UTF-8 attribute-size boundary on an unfinished header. Use authored
inputs, public parser results and the existing diagnostic catalogue; do not
assert incidental message wording or expose private grammar internals.

This is characterization of already implemented behavior, not fabricated RED.
Run the focused parser contract, lint and TypeScript 7 checks, then the native
Stryker incremental path using the retained complete report and changed tests.
Also rerun the CLI mutation profile on the current dependency graph. No mutant
exclusion, threshold reduction, compiler substitution or suppression is allowed.

The two added public parser contracts pass, including both quoted-attribute
delimiters and a four-byte attribute accepted exactly at its limit then rejected
at three bytes. Lint and TypeScript 7 checking also pass. The native incremental
run detects one changed test file and reuses 3518 of 3755 results from the complete
fresh graph run; it reruns 237, with original reports retained. This is supported
tool reuse, not manually edited mutation results.

Inspecting the 100 runtime-error reasons distinguishes 89 mutated native-grammar
setup/execution failures from 11 description-test module-setup failures, including
one native hit-count limit. These do not count as assertion kills. Remaining
survivors and uncovered guards stay in the score; some concern native invariants
or equivalent defensive conditions. No impossible parser tree or fake stream is
introduced to manufacture coverage.

The implementation and broader behavior contracts are signed in
`25a24156d473b69feaaa9a59b33a3539f9b4a184`. The fresh pre-commit repository route
passed 518 tests, 99.93% lines/statements, 100% functions, 96.36% branches,
15048/15048 typed references, 56 reproducible declarations and actual installed
API/CLI/TypeScript consumers (`quality-final-full-verification.log`). That older
snapshot is not the final evidence for these additional tests/documentation.

The final locked parent SBOM has 360 components and the isolated tooling SBOM
has 37, with no missing declared licence fields. The refreshed native npm audit
reports zero vulnerabilities (`quality-dependency-audit-final.json`); metadata
does not discharge independent licence/security review. The comparator rerun
retains all 250 observations: this converter accounts for all 50 authored cases
(21 equivalent, 17 diagnosed loss, 12 exact); other classifications are unchanged.

Native actionlint 1.7.12 passes the exact changed workflow
(`actionlint-workflow.log`, exit 0). Its task-local Windows archive was checked
against release metadata and published SHA256 checksums, and its full MIT licence
inspected. The independent YAML 1.2 unique-key parse also passed. Neither is a
remote Actions run. The remaining work is final mutation outcomes, a signed
tooling/documentation commit and fresh repository verification, followed by the
accepted independent-review and release gates.

### Evidence and temporary material disposition

This converter task owns `.sdlc/runtime/converter` and its package `artifacts/`
outputs. Preserve original RED/failed logs, native mutation reports, target HTML,
comparison observations, SBOMs and selection/licence archives for the pending
independent R2/security review. Keep isolated comparator installations as that
review's reproduction inputs; reassess their retention when review completes or
an approved release candidate replaces them. The actionlint binary is a bounded
review reproduction tool with the same reassessment checkpoint.

Permanent parser/resource/diagnostic regressions are promoted into maintained
tests. Successful Stryker sandboxes and packed-consumer temporary directories are
disposed by their native tools; inspect any failed-run sandbox left behind before
removing its task-owned working copy. Retain the code-of-conduct draft solely for
the pending owner-selected private reporting contact, then replace it with the
adopted policy and remove the draft. Active lifecycle and verification records
are control/evidence state and must not be cleared as scratch. No shared cache,
user-owned checkout, comparison evidence or failed report is eligible for blanket
deletion.

The final library mutation result passes at 90.18%: 3288 killed, 8 timeouts,
340 survived, 19 uncovered and 100 runtime errors. The new malformed-attribute
contracts detect ten former survivors. The native incremental run takes 3m48s
(`mutation-library-final-diagnostics.log`); its complete native report remains
`artifacts/mutation-library/mutation.json`. The current-graph full CLI run also
passes at 98.37% (181 killed, three survived, no timeouts/uncovered/errors), taking
5m47s (`mutation-cli-final.log`). Preserve both final reports and earlier failures.

Five failed-run library sandboxes were inspected; three had no reparse points
and two had native node_modules links. DCG rejected permanent recursive deletion.
Following its reversible alternative, they were moved without overwriting to
`.sdlc/runtime/converter/retained-sandboxes/`: `sandbox-0TiYp9`, `sandbox-38pYbg`,
`sandbox-Hg7K0w`, `sandbox-QyVwER` and `sandbox-Tya3rI`. No linked dependency target
was deleted. The optional host process inventory was unavailable (CIM access
denied); the producing exec sessions were already completed. These remain owned
by this task for guarded disposition at the independent-review checkpoint.
Original setup logs and mutation reports stay at their existing locations.
This move changes no source, test, dependency or verification input.

Final automatic verification will run through `npm run sdlc -- verify --keep-going`
after the signed tooling/documentation commit, writing the actual frozen identity
and outcome to `.sdlc/runtime/verification/full.json` and retaining console output
as `.sdlc/runtime/converter/quality-committed-full-verification.log`.
Do not infer that outcome from this prospective command or the earlier pass.

The only missing documentation owner decision is the private conduct-reporting
contact; the concrete draft is retained and has not been adopted. Independent R2
verification/review and applicable native security assessment still need an
authorized independent route under the standing no-subagents instruction. Remote
CI, branch-rule adoption, pre-release legal review, publisher binding and any
push/publication remain separate gates. This candidate makes no stable-release
or in-game/Workshop acceptance claim.
