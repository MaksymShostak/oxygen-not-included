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

## Independent review repairs (9 September 2026)

Both authorized reviewers finished on signed `52fd7f76aa8aa70146774218cc479496e0837807`
with all 164 changed paths accounted for and no tracked drift. The independent
verifier reproduced five installed-consumer failures despite passing automation:
empty, redundant nested, padded deletion and paragraph-spanning formatting (F1),
and deleted nonbreaking table content (F2). F3 identifies the path classifier's
misleading commitment claim. Preserve the original reports and counterexamples
under `.sdlc/runtime/converter/independent-verification/`.

The separately approved direct security assessment is retained under
`.sdlc/runtime/converter/direct-security-review/`. It identifies mandatory native
diagnostic reparsing as a concrete availability concern (D1), and records legacy
isolated-comparator advisories without claiming exploit reachability (D2). The
native precheck remains incomplete: 70 of 164 paths selected, 94 omitted, no live
scan or native bundle. The direct alternative does not change that status.

Continue R2 against the unchanged accepted v2 baseline. Max's standing instruction
authorizes implementation, detailed signed commits and configuration explicitly
specified here. The supported lifecycle starts `converter-review-repairs` after
the earlier implementation handoff; it does not fabricate a resumed active record.
Use the repository-adapted TDD procedure for repairs. For D1, apply the Codex
Security fix-finding boundary/integration checks using the completed independent
source-to-sink investigation and the same authorized reviewer for verification;
the user's two-reviewer limit excludes additional agent dispatch.

### Repair slices and independent oracles

- F1: keep native Markdown serialization. Normalize empty, redundant nested and
  adjacent identical presentation nodes in target lowering; preserve content and
  other active styles. Represent ordinary paragraph boundaries with native flow
  nodes while retaining formatting on each paragraph. Qualify padded deletion
  through the serializer's supported `unsafe` configuration, including Unicode
  whitespace; no output rewriting, HTML substitute or vendor patch. Authored GFM
  expectations and the actual GFM parser are the oracle. Source syntax, positions,
  per-occurrence accounting and opaque content must remain intact.
- F2: `src/mdast/steam-table-structure.js` must recognize only ASCII space, tab,
  CR and LF as structural layout. Nonbreaking or other substantive separators
  cause existing complete-table preservation and its diagnostic. Include an
  otherwise-valid TH-header/TD-body positive control and both row/cell boundaries.
- F3: rename `is_committed_plan_path` to `is_canonical_plan_path` in
  `scripts/_sdlc_baseline.py`, `scripts/sdlc.py` and `scripts/validate_sdlc_pr.py`.
  Preserve the actual native Git commitment checks and existing control tests;
  add no compatibility alias or name-mirroring test.
- D1: qualify applying the existing GFM extension's documented MDAST transforms
  directly to an isolated target-tree copy. Those transforms already own the
  optional GitHub autolink policy. Avoid reparsing escaped Markdown solely to
  count links. Compare diagnostics against the prior native-parser path on
  authored legitimate and hostile inputs; preserve the caller tree. Before
  implementation, retain the original public API repro and a five-second child
  budget failure for 16,000 opaque repetitions. Recheck that input and adjacent
  formatting, record actual timings and scaling, and make no global CPU-bound
  claim. The selected native transform contract is exported by the existing
  `mdast-util-gfm` and typed by `mdast-util-from-markdown`; no dependency is added.

The five initial behavioral RED reproductions are retained in
`review-regressions-red.log`. Earlier setup/import and invalid-header fixture
attempts remain separately identified, not counted as behavioral evidence.
Run focused Node tests first, then the package checks, control regressions and
fresh repository full verification. Refresh the native mutation profiles whose
inputs changed, preserving earlier reports, and ask the same two reviewers to
verify the signed repair candidate. Passing automation does not erase findings.

### Documentation and mechanical follow-through

The baseline's generated API-reference fixture is not satisfied by declaration
generation or authored `docs/api.md`. Qualify current maintained documentation
tools against imported generic checked JSDoc before adopting one. Native registry
metadata selects TypeDoc 0.28.20, documentation 14.0.3 and JSDoc 4.0.5. TypeDoc's
supported peer range stops at TypeScript 6; the existing TS 6 authorization remains
strictly confined to type coverage. Do not substitute that compiler for docs.

For bounded qualification only, create
`.sdlc/runtime/converter/selection/api-documentation/qualification/package.json`
with `private: true`, `type: "module"`, and exact development dependencies
`documentation: "14.0.3"`, `jsdoc: "4.0.5"`; native npm may create its lockfile.
Install with lifecycle scripts disabled after inspecting selected licence texts.
This isolated fixture changes no maintained dependency, build or CI command and
is removed after its evidence is retained or superseded by a qualified adoption.
Any adopted generator and its exact maintained configuration will be stated here
before implementation. No custom type parser, rewritten JSDoc or checker
suppression may disguise a failed qualification.

Remove only the extra final blank line in the three existing JSON schemas:
`spec/schema/common.schema.json`, `profiles.schema.json`, and `sources.schema.json`
under the converter. JSON values and pipeline behavior remain identical; validate
the native schema consumer and the complete Git range's whitespace afterward.
The private conduct-reporting contact remains an outstanding owner decision;
retain the prepared draft without inventing a contact or adopting placeholders.

For the measured D1 regression, add `qualify:performance` to the converter's
`package.json`: `node --test test/performance/*.test.js`, and invoke it from
`check` after runtime coverage. The dedicated subprocess fixtures have a fixed
five-second budget for 16,000/32,000 opaque repetitions and 40,000 adjacent bold
nodes. They check complete output size/content and diagnostics as well as
termination. This is a bounded regression floor on the supported CI runtimes,
not a public conversion deadline. Mutation runners keep their existing scope;
timing fixtures are not repeated for every mutant. No threshold is weakened.

The serializer's `strikethrough` construct name is registered by the native
`mdast-util-gfm-strikethrough` type augmentation, which the aggregate GFM entry
point does not import in its declarations. Add exact direct runtime/type
dependency `mdast-util-gfm-strikethrough: "2.0.0"` to the converter package and
native lock. This is already the aggregate extension's installed version and the
current stable release; its full MIT text was inspected. A native JSDoc `@import`
loads its declared construct names without runtime code, aliases, a local
augmentation or a type assertion hiding the mismatch. Runtime GFM composition
continues to use the existing aggregate extension.

Max answered "Authorize isolated docs-only qualification" after reviewing the
compiler restriction and failed imported-generic fixtures. This explicitly adds
the docs-only TypeScript 6 scope without changing any primary compiler role.
Qualify TypeDoc 0.28.20 and unmodified TypeScript 6.0.3 in the separate private
`tools/steam-community-bbcode/tooling/api-docs/package.json`, both exact development
dependencies, with the native npm lock and scripts disabled during installation.
Use package version 1.0.0, AGPL-3.0-only, ESM and npm 12.0.2. Its native `typedoc`
script runs the supported CLI. The maintained qualification fixtures live under
`test/documentation-fixtures/` so primary TS 7 checking covers them too. TypeDoc
must retain the imported generic result and its value type in its native JSON
model and rendered reference. An unsupported fixture is not a successful adoption.

The rejected documentation.js 14.0.3 fixture exits zero while dropping the return
type; its native lint reports the imported type parse error and unknown template
tags. JSDoc 4.0.5 exits one on the import type syntax. The identical fixture passes
the actual TypeScript 7 checker. Preserve both failures under the documentation
selection evidence. These generators are not adopted into the converter graph.

The TypeDoc fixture retains `Envelope<T>`, resolves the imported alias to its
definition, and keeps the value property's generic type; native generation exits
zero with warnings treated as errors. The first package-root reference correctly
reports 20 referenced symbols outside its selected entry point. Inspection of
`typedoc-plugin-missing-exports` 4.1.4 found prototype monkey patches, so reject and
remove that qualification dependency. Include the referenced project-owned source
modules through TypeDoc's native entry points instead, without adding runtime
exports or suppressing missing-type diagnostics. Use `typedoc-plugin-markdown` 4.13.0
(MIT) to produce reviewable, package-local Markdown without shipping a second
website's scripts, fonts or copied repository documents. Add both exact development
dependency only to the docs-only package and its native lock.

The qualified docs configuration will select `src/index.js` and the source modules
owning its referenced types, the existing `tsconfig.json`, the Markdown plugin,
source-owned referenced types and exported native contracts, `readme: "none"`,
`disableSources: true` to omit commit-dependent source hyperlinks from repeatable
offline documentation, warnings-as-errors, and native `router: "module"` Markdown
output under `docs/reference/` with `index.md` as entry. TypeDoc's JSON model stays
in `artifacts/api-reference/` for fixture/contract validation. No checker settings,
runtime exports, source comments or type syntax are weakened. Add the actual
generator/fixture checks to the package check after qualifying this configuration;
state their exact npm and CI/dependency-update changes before execution.

The native entry-point qualification passes with zero warnings after documenting
nine source modules. The exposed `SteamUrlWidgetKind` contract now states its six
source-qualified identities and checks the private recognizer against that union;
public metadata no longer depends on the type of an undocumented private function.
The accepted widget identities and executable recognizer are unchanged.

Adopt `tooling/api-docs/typedoc.json` with these native entry points: `src/index.js`,
`src/diagnostics/{conversion-result,diagnostic-codes}.js`,
`src/steam/{registry-identifiers,steam-bbcode-syntax,parse-steam-bbcode}.js`, and
`src/mdast/{steam-mdast-nodes,steam-url-widgets,steam-table-layout}.js`.
Use the options above and `entryFileName: "index"`. Keep the generated root API
module distinct from supporting source-module documentation; the latter does not
make internal modules importable through the package exports map.

Add docs-only native Application orchestration and fixture assertions in
`tooling/api-docs/generate.js`. Its `generate` and `check` npm scripts generate
the native model/Markdown, qualify the imported generic and compare generated
bytes with `docs/reference/`; only explicit generation updates that owned folder.
Extend the ESLint file/command scope to this script. Its native TypeDoc API types
reference WebAssembly through the standard DOM library. Check this script with
the parent's TS 7 executable and a separate `tooling/api-docs/tsconfig.json`
extending the unchanged strict base with `lib: ["ES2022", "DOM"]`, `noEmit: true`
and only `generate.js` included. Keep DOM globals outside runtime/consumer checking.
Add `typecheck:api-docs` to the primary `typecheck` sequence; add `check:api-docs`
to the isolated type-coverage tool and run it from the existing metric sequence
with the same 100% strict threshold. No library checking is skipped.
Add parent `docs:api`, `docs:api:check` and `sbom:api-docs` forwarding scripts.
Run docs freshness/qualification from `check` before ordinary documentation tests;
the packed consumer receives the checked reference through the existing `docs/`
allowlist. No documentation generator is added to the shipped runtime.

In `.github/workflows/steam-community-bbcode.yml`, install the exact docs-only
lock with npm 12 and `--ignore-scripts`, emit its native SBOM, and retain
`artifacts/api-reference/` plus the documentation-fixture output with existing
qualification reports. Add only its package directory to the existing npm
Dependabot group. These extend the existing matrix and dependency review; they
do not change permissions, compiler versions, thresholds or remote protections.

The native `excludeExternals` filter also removed the exported unist `SourceSpan`
and its point fields. A direct public-contract assertion reproduces that failure.
Retain TypeDoc's default external inclusion instead: selected source entry points
still bound the reference, while native contracts used by public exports retain
their actual structure. Raising conversion depth did not address this omission
and is not adopted. Keep the SourceSpan assertion beside the generic fixture.

## Authored documentation line breaks (9 September 2026)

Max requested enforceable semantic line breaking and selected converter-only
authored documentation as the initial scope. Qualify maintained native Markdown
formatting before configuration/adoption. The output must retain paragraphs,
inline code, links, tables, fenced examples, nonbreaking spaces and hard breaks;
an ordinary multi-sentence paragraph must become sentence-separated source.
Generated `docs/reference/`, `docs/conversion-semantics.md` and
`docs/steam-support-matrix.md`, fixtures, vendored material and the immutable
repository baseline are outside the authored-document rewrite scope.

Prettier 3.9.6 preserves wrapping by default but needs a plugin for sentence
boundaries. Inspecting exact `eslint-plugin-sentences-per-line` 0.1.3 and
`prettier-plugin-sentences-per-line` 0.2.4 with `sentences-per-line` 0.5.3 (MIT)
shows capital-letter heuristics and incomplete coverage across inline nodes.
The older textlint sentence rule has no release after 2022. Do not substitute
these limitations for evidence that every authored sentence boundary is enforced.

For bounded qualification, create only the ignored
`.sdlc/runtime/converter/selection/semantic-lines/requirements.txt` with exact
`mdformat==1.0.0`, `mdformat-gfm==1.0.0`, `mdformat-slw==0.4.0` and
`mdformat-sembr==0.2.0` (all MIT). Install their unmodified wheels into the adjacent
ignored `python/` target, using the checkout interpreter and native installer.
Select each plugin explicitly when comparing outcomes, retain raw failures, and
keep mdformat's native rendered-equivalence validation enabled. This qualification
adds no maintained dependency or CI change. State the exact selected integration
after the fixture succeeds; add no custom sentence splitter or vendor patch.

The native 15-case comparison retains every result: slw passes 14 but misses a
sentence whose punctuation is inside emphasis; sembr additionally loses NBSP,
misses sentence-starting code/links and triggers native equivalence failure on
a hard break. Extend only the ignored qualification requirements/target with
current MIT `flowmark==0.8.0` and evaluate the identical authored expectations.
No failing oracle is weakened to accept a formatter.

Flowmark's CLI width zero disables sentence wrapping and collapses NBSP; its
default semantic mode also combines short sentences. Native `Intl.Segmenter`
splits the accepted `e.g. Node` abbreviation case. Neither supplies the required
contract as configured. Before custom composition, qualify current MIT
`snapper-fmt==0.11.0`, published as a standalone native formatter in a wheel with
no Python dependencies. Extend only the ignored qualification requirement and
download its unmodified Windows wheel; run its published CLI on the same corpus.
Its documented Unicode segmentation, abbreviation handling and Markdown-aware
regions directly address the remaining gap. No global installation, MCP server,
editor integration, generated configuration or Git filter is authorized here.

Snapper's native Windows CLI passes all 15 unchanged expectations. Select its
unmodified 0.11.0 release for authored converter Markdown. The residual local
work is file selection and invoking that CLI through the existing checkout
Python entry point; sentence detection, Markdown handling and check/write behavior
remain vendor-owned. Use the configuration/build verification route for this
integration, retaining the failed candidate qualification as counterevidence.

Under the owner's standing approval for explicit implementation-plan configuration,
make these exact changes:

- Add `tools/steam-community-bbcode/tooling/prose/requirements.in` with
  `snapper-fmt==0.11.0` and native uv-generated, hash-locked `requirements.txt`.
  Install wheels additively in this checkout's existing `.venv`; never sync away
  SDLC dependencies. This is a development tool, outside the published package.
- Add package `.snapperrc.toml`: Markdown format, unlimited source width,
  sentence breaks only (`clause_breaks = false`), and no code-language formatter
  configuration. Pass this exact config explicitly to prevent ancestor overrides.
- In the converter `package.json`, add `docs:format`, `docs:format:check` and
  `qualify:prose` npm commands using `scripts/runRepositoryPython.js`. Add the
  native read-only formatting check and its qualification to `check`. Neither
  command formats generated API references, conformance/support projections,
  vendored fixtures or documentation outside this converter.
- In `.github/workflows/steam-community-bbcode.yml`, reuse the repository's pinned
  `actions/setup-python` and `.python-version`; create the checkout `.venv` and
  install only the hash-locked formatter wheel with `--only-binary=:all:` before
  converter checking. Retain its installer report and packaged native SBOM.
- In `.github/dependabot.yml`, extend the existing pip directory list with the
  converter's `tooling/prose` directory so native update proposals cover its pin.

The bounded gate covers package-root Markdown and authored `docs/**/*.md`.
Use native CLI fixtures for bold, inline code/links, versions, abbreviations,
NBSP, explicit hard breaks, quoted sentences, lists, blockquotes and GFM tables;
check idempotence and failure-without-writing on a temporary unformatted document.
Verify scope exclusions with distinct fixture files, then compare real authored
documents before/after with the existing native GFM parser and inspect the diff.
Natural-language sentence detection is deterministic tooling, not proof of
linguistic understanding. Prettier is unnecessary for this selected narrow gate.

The real-file check exposed Snapper's documented render backstop: it can return
the original file after an unsafe reflow and exit zero, while structured diagnostics
still identify fused sentences and arbitrary wraps. Refine the local gate to
consume native `--check --output-format json`: fail on native nonzero exit,
`would_reformat`, or native `fused`/`wrap` diagnostics. Keep `long` advisory under
the selected unlimited-width policy, retain the complete native JSON report, and
leave all vendor safeguards enabled. This is orchestration of native findings,
not a replacement Markdown parser, sentence splitter or compatibility shim.
Add the independently reproduced backstop fixture before changing the gate.
Review exceptional prose manually; never rewrite vendor output or suppress an
unresolved native finding.

### Repair and prose integration results

The six maintained prose qualification tests pass, covering the 15 authored
sentence/literal cases, GFM tables and code comments, both hard-break forms,
idempotence, native read-only rejection, scope exclusions, backstop rejection and
long-sentence acceptance. The actual gate first failed against the existing
wrapping, then passes on all 18 selected authored guides. Native GFM comparison
preserves every document's structure/content after accounting for the deliberate
inline-code marking of `.NET` in two guides and the added tooling explanation.
Keep `prose-before.json`, `prose-gfm-preservation.log`, native JSON and both
backstop RED/GREEN logs under `.sdlc/runtime/converter/review-repairs/`.
Generated reference/projection files were excluded from this prose rewrite.

The combined package check passes on Windows x64 / Node 24.20.0 / npm 12.0.2:
557 tests; runtime statements/lines 99.93%, functions 100%, branches 96.43%; three
bounded-process regressions; native conformance; strict TS 7 primary checks;
100% typed references (15,845/15,845 primary, 305/305 docs orchestration);
58 identical declaration artifacts across two clean builds; and actual packed
JavaScript, type and CLI consumers. Retain
`package-repair-and-prose-green.log` and the emitted package artifact at
`tools/steam-community-bbcode/artifacts/steam-community-bbcode-1.0.0.tgz`.
The earlier combined check correctly failed on the conformance input hash after
the package command changes; native regeneration changes that hash and the already
reviewed deletion-whitespace example, without changing construct outcomes.
Its original failed log remains retained. Native actionlint passes the exact
modified workflow; this is local validation, not execution of the remote matrix.

The first repair mutation run failed at 89.86%. Native-parser cases for adjacent
distinct links and mixed presentation marks then detected real untested faults;
equivalent internal array-guard mutants were not given artificial tests. The native
incremental rerun passes at 90.01% (3,462 killed, eight timeouts, 366 survived,
19 uncovered, 102 runtime errors), taking 5m48s. Of the runtime errors, 89 occur
in mutated native grammar setup and 13 in historical-description module setup;
these are excluded by Stryker's score, not assertion kills. The independent CLI
profile passes at 98.37% (181 killed, three survived, no other statuses), taking
5m52s. Native reports are under the package's `artifacts/mutation-library` and
`artifacts/mutation-cli`; keep their old-candidate and 89.86% predecessors in
`review-repairs/` rather than overwriting the counterevidence.

The three SDLC predicate files are signed in `1f67f55c1ff3784438b9ea368c3173babee413de`;
the API generator/reference foundation is signed in
`18b190e4df77e82a385e5b829f279d78e44b55d2`. Both exact messages, staged trees and
parents matched, with successful SSH signature verification. Commit the remaining
integrated repair/prose paths under the same standing approval, freeze that final
candidate, run the native full lifecycle verification, and obtain focused follow-up
from the same two authorized reviewers. Do not infer those future outcomes here.

The API and prose qualification wheels/sources, failed experiments, original
review consumers and native mutation reports remain task-owned review inputs until
the two reviewers complete the signed repair follow-up. The maintainer record and
permanent fixtures preserve the useful qualification conclusions. Reassess spent
working copies at that checkpoint; original RED logs, native inventories, accepted
baselines and active control state remain retained evidence. The prose wheel's
bundled 268-component SBOM and installer report are retained separately from the
npm inventories; npm audit does not assess that compiled Rust graph.

### Final review follow-up and adjacent presentation repair

The integrated signed candidate is `1e3fdf80faf4fefb3a85641aa1f4bd3fb4a301ec`.
Native full lifecycle verification passed on that exact clean commit with equal
before/after identities. Both authorized reviewers completed their focused delta
reviews in `independent-verification/repair-followup/` and
`direct-security-review/repair-followup/`; retain their original results unchanged.
The independent verifier confirms the original examples, table whitespace,
predicate naming and API/prose tooling qualifications, but finds 58 failing cases
among 512 authored adjacent-style combinations. In particular,
`a[i][b]b[/b][/i][i]c[/i]` silently renders visible `ab**c`.

Continue the same R2 baseline and test-first F1 repair. The native serializer's
documented `emphasis` option can use `_` while `strong` retains `*`, preventing
adjacent mixed marks from joining into ambiguous asterisk runs. An inspectable
scratch probe confirms this option preserves the concrete counterexample through
the actual native GFM parser. It changes target spelling only; source syntax,
visible characters, active marks and fidelity obligations remain unchanged.
Use the existing unmodified dependency and public option, without new handlers,
output rewriting, shims, suppressions or dependency changes. Source:
[native serialization options](https://github.com/syntax-tree/mdast-util-to-markdown#options).

Promote the independently expected character/mark test boundary into maintained
tests, first observing the failure on the current implementation. The scratch
81-case adjacent-pair corpus has 75 passes and six semantic failures, retained in
`review-repairs/adjacent-style-red.log`. Check equivalent nested presentation by
each character's mark set, not incidental nesting order. Exercise the concrete
case through the CLI and actual installed archive. Then run affected checks,
regenerate and inspect conformance projections, native mutation and full lifecycle
verification, sign the scoped change and request bounded follow-up on the same
independent review corpus. Do not weaken the original expected characters/marks.

The direct reviewer confirms the expensive output-reparse cause is repaired:
80,019-byte opaque input takes 177 ms; 160,019-byte opaque and 320,000-byte adjacent
bold inputs finish below one second with complete output and diagnostics. The
950,019-byte opaque probe still exceeds 15 seconds in native serialization before
diagnostics. Preserve that residual availability finding and its raw timeout;
the synchronous API and input quotas do not imply a CPU deadline. Caller process
controls and deployment/security acceptance remain explicit limitations.

Max identified the Hadden-Industries owlapi code of conduct as the reference and
explicitly approved extending `conduct@haddenindustries.com`, his initial moderator
role and the conflict-of-interest rule to this converter. Adopt the prepared
`tools/steam-community-bbcode/CODE_OF_CONDUCT.md` adaptation, link it from
`CONTRIBUTING.md`, and include it in the existing authored-prose scope. Preserve
its Contributor Covenant 3.0 / owlapi attribution and CC BY-SA 4.0 policy-text
licence; converter code and package identity remain AGPL-3.0-only. The adaptation
does not assert owlapi's project-specific corporate appointment, mailbox access
implementation or privacy-notice applicability. This is the exact planned
repository-policy addition under the standing configuration/commit authority.

The maintained adjacent-pair suite reproduced all six semantic failures before
the serializer option changed. After the change, 133 focused formatting/CLI
checks pass. Move the existing equivalent-adjacent-order scenario from incidental
AST-nesting equality to the character/mark oracle: it still requires exactly
`ab`, with both characters bold and italic, regardless of which commuting mark
is the outer node. The prior structural-only failure remains in
`adjacent-style-first-green.log`; no source character or required mark is waived.
Replaying the reviewer's retained 512-case corpus with its unchanged expectations
passes all 512 cases; record its original-byte digest in
`independent-style-corpus-replay.json`. This coordinator replay does not replace
the next independent installed-consumer follow-up.

The full package check passes in `final-adjacent-package-check.log`, including
primary TS 7, 100% typed references (16,098/16,098 primary and 305/305 docs),
58 declaration artifacts across two clean builds and the packed API/CLI case.
Native conformance regeneration changes only the source identity hash and the
italic example's `_` delimiter spelling; construct/fidelity results are unchanged.
The approved code of conduct passes the native prose gate as the nineteenth
authored document. Its original proposed text remains in
`code-of-conduct-owlapi-draft.md`; the older short draft remains historical input.
The previous candidate's mutation JSON is retained in
`review-repairs/previous-mutation-1e3fdf80/` before the final native reruns.

The first final library mutation run reports 89.89%, below the unchanged 90%
gate, and native Windows worker disposal reports `taskkill` access denied.
The status diff isolates six newly surviving mutations to presentation sorting:
the old structural-only oracle killed alternate nesting even when every visible
character and active mark was correct. The native distinct-marker policy removes
the reason for this custom ordering. Under the preservation route, remove the
now-redundant `orderedPresentation` and `presentationOrder` helpers rather than
adding assertions for incidental sort order. Name the remaining responsibility
`coalesceGfmPhrasing` in `gfm-phrasing.js`, update all consumers without an alias,
and recheck the same full character/mark corpus plus existing regressions.
Retain the failed native report and cleanup diagnostics; use the native worker
workflow with sufficient permission for its own child-process disposal, without
disabling checks, changing thresholds or editing vendor code.

That preservation hypothesis was falsified: the unchanged independent corpus
passes only 467/512 cases without ordering. The minimal trigger
`a[strike][b]b[/b][/strike]c` emits literal tildes and loses deletion when source
order is retained. Preserve this result in `no-presentation-order-replay.json`.
Add the semantic fixture before reversing only this task's trial removal; the
whole maintained file has one genuine failure in
`presentation-order-all-styles-red.log`. The first name-filter attempt did not
select its intended test and is not RED evidence. Keep the original ordering
helpers and coordinated names: their residual role protects native deletion
flanking, and the new test detects that real effect without requiring incidental
nesting. The earlier plan to remove/rename them is superseded by this evidence.

### Release qualification continuation — 9 September 2026

The user explicitly directed continued release qualification after the local
implementation handoff. Reuse the accepted v2 baseline and standing detailed
commit/configuration authority; the handoff does not terminate the remaining
accepted release obligations. Native lifecycle task `converter-release-qualification`
uses R2 and the unchanged baseline. Start from signed `281cdc9620ba5d7d6cff82e9ff1ad80284658582`.
Its full local receipt, final 90.04%/98.37% mutation reports and both completed
independent follow-ups remain valid for that candidate, with original failures retained.

- [x] Reproduce the near-limit opaque workload through the public API and isolate
  the remaining native serializer cost. Keep complete literal-output and diagnostic
  expectations, the established five-second subprocess qualification budget and
  the no-shim/no-vendor-patch rule. Evaluate current supported native capabilities
  before selecting a repair; a dependency release-channel or accepted-resource
  contract change needs its actual owner decision.
- [x] Scan the exact Snapper native Cargo SBOM with unmodified OSV-Scanner 2.5.1,
  the current stable release, in task-local scratch. Verify the maintainer's release
  digest and preserve its Apache-2.0 terms. Refresh native npm advisory evidence
  for the main, type-coverage, API-docs and isolated comparator locks. Account for
  every extracted component and actual advisory result, without suppressions.
- [ ] Review the locked dependency licence texts and native package allowlist;
  record concrete redistribution obligations and any unresolved terms. Retain the
  existing compiler boundaries and AGPL-3.0-only package identity.
  Include the exact MIT Microsoft notice from `@types/unist` 3.0.3 in authored
  `docs/third-party-notices.md`, since the generated API copies its descriptions.
  The existing `docs/` archive allowlist and authored-prose selector cover this
  file without a configuration change. Link it from the README and selection record.
- [ ] Validate the prepared Windows/Linux Node 22/24/26, CodeQL and dependency-review
  workflow against the actual candidate. Inspect the exact PR/base policy and prepare
  the signed branch and detailed PR body before requesting the separately required
  remote push/PR action. Authentication through the GitHub connector works; the
  local `gh` credential currently receives HTTP 401. No candidate PR or workflow run
  exists, and this host has no installed WSL distribution or container engine.
- [ ] Finish any authorized repairs with the repository TDD procedure, focused
  consumer checks, required fresh full verification and the already authorized
  independent review roles. Commit exact task-owned files with detailed signed
  messages. Keep release qualification active until its remaining decisions and
  external gates are actually resolved; publication remains a separate action.

Evidence for this continuation is retained under
`.sdlc/runtime/converter/release-qualification/`. Native profiling and controlled
reproductions isolate the remaining serializer availability finding. A minimal
owning-library proposal passes the retained semantic corpus and additional seeded
byte-preservation comparisons in a disposable copy. The installed dependencies
and converter implementation remain unchanged, and the primary near-limit
qualification still fails. The private upstream report is prepared but unsent;
its reproduction and proposed repair stay in the restricted local evidence until
disclosure is authorized. No downstream patch, compiler alias, checker suppression
or resource-contract change has been adopted.

The actual package `check` command passes on Windows x64 with official Node
22.23.2 and 26.8.1 and npm 12.0.2, supplementing the existing Node 24.20.0 full
receipt on `281cdc9620ba5d7d6cff82e9ff1ad80284658582`. Both additional native
executables match the published SHA-256 lists, and only each command's process
environment selects its runtime. Raw logs and acquisition receipts remain in the
release-qualification directory. These local passes do not establish Linux CI.

Native OSV-Scanner 2.5.1 accounts for all six explicit inputs: 268 Snapper Cargo
components and 360 main, 37 type-coverage, 23 API-docs, 108 legacy-comparator and
26 BBob npm dependencies. The refreshed native npm audits report zero issues for
the main, type-coverage, API-docs and BBob graphs. The legacy comparator retains
11 propagated affected-package entries (nine moderate, two critical); this is
not a count of independent vulnerabilities. OSV retains 12 Rust records, including
alias records and three maintenance notices, plus eight Node advisory records.

The same authorized direct R2 reviewer assessed all 31 supplied OSV/npm entries:
28 are not actionable through the assessed authored-prose and fixed-corpus
commands, and three unmaintained-component notices remain open for release review.
The matched neural/model/TLS paths are not entered by the Unicode-only formatter
integration. Arbitrary standalone comparator input remains unqualified; the
benchmark deadline is not a filesystem or network sandbox. Keep these boundaries
and upstream maintenance status visible when remote dependency review runs.
No advisory suppression, dependency downgrade or risk waiver has been introduced.
The native Codex Security inventory remains incomplete; the owner-authorized
direct assessment and this static advisory follow-up are the actual evidence.

The installed notice inventory records exact locked identities, full notice text
and digests, while the earlier full-term inspections remain tied to the unchanged
graphs. The additional shipped Microsoft notice covers copied generated prose;
the package's own licence remains AGPL-3.0-only. Metadata and this engineering
notice review do not replace the baseline's independent pre-release legal review.

Live `origin/main` is `975acf599d06ec3d274c55bac8d1731278ffa153` and already
contains the unchanged accepted plan. Its trusted PR validator still accepts only
Issue snapshot baselines; the Markdown-plan route is introduced by this branch.
The prepared converter draft may run its ordinary matrix and security jobs, but
its trusted-base linkage cannot pass until the separately reviewed baseline-route
change is present on `main`. Do not lower the converter's R2 classification,
rewrite the accepted plan or run candidate policy with privileged credentials.
Remote push/PR creation and any prerequisite merge retain their separate authority.

Keep the continuation's runtime executables, scanner, isolated upstream proposal
and probe programs until their remote-CI or upstream-report consumers finish.
The converter task owns these copies; reassess disposal after the external
qualification/reporting checkpoint while retaining acquisition records, original
failures, review reports and the exact proposal/reproduction as evidence.
No lifecycle handoff is claimed while those release obligations remain open.

### Remote qualification and owlapi publication reuse — 9 September 2026

The owner separately authorized pushing signed `818cdfe508b0c77b7bcefb48e1ac22e47b443f89`
to `origin/steam-community-bbcode` and opening a draft against `main`.
Native signed publication succeeded and [draft PR #4](https://github.com/MaksymShostak/oxygen-not-included/pull/4)
has that exact head, the expected base and 189 changed paths.
Readback through the GitHub connector confirms the exact approved body bytes;
the alternative reader's HTML-escaped quotes are a presentation difference.
The final five-document independent follow-up passes the actual archive notice,
native docs/link checks, all 20 authored prose checks and current full-receipt identity.

All six remote Windows/Linux Node matrix jobs have passed the actual package
check; the Linux Node 24 mutation step remains in progress at this checkpoint.
The trusted-base job reproduces `Invalid baseline path.` as predicted.
Dependency review reports two advisories in the isolated legacy comparator's
`form-data` 2.3.3: GHSA-fjxv-7rqg-78g4 and GHSA-hmw2-7cc7-3qxx.
Original job logs are retained under the release-qualification directory.

Repair that demonstrated dependency selection using the maintainer's native
`v2-backport` release, unmodified `form-data` 2.5.6, rather than changing advisory
policy. The exact planned configuration change is
`tools/steam-community-bbcode/comparison/node/package.json`:
`overrides["request@2.88.2"]["form-data"] = "2.5.6"`, with its native npm lockfile
refresh. Request's obsolete `~2.3.2` constraint prevents selecting that supported
backport automatically. Preserve the original comparator report and lock bytes,
review the exact MIT notice and added dependency terms, rerun native audit and
the common comparison corpus, and require identical provider outcomes.
This changes the comparator's declared dependency resolution, so its refreshed
provenance must report that change. It does not patch any provider source or
introduce an advisory suppression. Other legacy advisories remain separately assessed.

The owner then explicitly directed liberal reuse of the existing Node publication
solution in `C:/Users/maksy/GitHub/owlapi`, with proportionate release controls.
That checkout is clean at `2ac41c94e6630ca47ce110a484ec9af3b0b1f335`; its release
code uses the same AGPL-3.0-only boundary. Assess and adapt its existing candidate
archive, artifact-ID transfer, scoped publication and fresh public-registry
verification code. Preserve source attribution and the exact reuse provenance.
The target integration should publish the already tested archive and verify its
registry bytes and installed consumer, using native npm authentication/provenance.
Do not transplant owlapi's immutable GitHub-release/attestation machinery,
32-shard package-specific qualification or one-prerelease-only registry rules.
No first-publication credential, trusted-publisher binding, public version change
or actual npm publication is implied by this reuse instruction; prepare the exact
workflow and package entry-point changes for local qualification first.

### Publication implementation slice

Continue the accepted R2 release-provenance and installed-consumer obligations,
using the owner's 9 September owlapi-reuse instruction as the slice authority.
Adapt owlapi's `release-artifacts.mjs`, `build-release-candidate.mjs`,
`qualify-public-registry.mjs` and release workflow at the recorded source commit.
Keep attribution in the adapted files and the software-selection record.
The residual custom work is the converter coordinate, existing API/CLI/type
consumer integration and a smaller manual workflow.

The exact configuration changes are:

- Add `release:pack` and `release:verify-registry` npm entry points in
  `tools/steam-community-bbcode/package.json`; preserve its name, private flag,
  development version, dependency graph, compiler roles and existing checks.
- Add `.github/workflows/steam-community-bbcode-release.yml` with manual dispatch,
  a `publish` boolean defaulting to false and a distribution-tag string defaulting
  to `next`. Qualify with the existing Node 24.20.0/npm 12.0.2 and locked tooling.
  Transfer the qualified bundle by its same-run artifact ID, verify native
  artifact digests and SHA256SUMS, and publish its exact archive with provenance.
  Only the publication job receives `id-token: write` and names `npm-release`;
  it executes no checked-out package code. A separate read-only job installs the
  exact public coordinate, verifies registry bytes/integrity and runs native
  signature/provenance auditing and the existing consumer fixture.
  No token secret, automatic tag-triggered publication, immutable GitHub release,
  attestation reconciliation service or sole-dist-tag policy is introduced.

Extend the existing package-consumer script to accept a candidate output
directory or an exact public coordinate, retaining its authored JavaScript,
installed CLI and TS 7 declaration assertions. Release qualification additionally
retains native production audit and SBOM output. Reuse owlapi's small hash and
registry-fact helpers instead of its archive parser or release-state framework.
Native npm packing and actual installed consumers own archive interpretation.

Use preservation evidence for copied capability and existing consumers, and
test-first checks for changed registry acceptance: the requested tag must select
the expected version, other legitimate tags may coexist, archive bytes and SRI
must match, and a wrong coordinate or registry origin must fail.
Run the package's native lint/type/coverage checks, actual candidate construction,
and meaningful tamper controls; inspect the workflow using the repository's
existing YAML consumers. Live OIDC binding and npm publication remain unperformed
  until explicitly authorized, and `private: true` prevents accidental activation.
Existing release blockers remain visible rather than being recast as a pass.

The supplied CodeQL alert #1 was statically triaged against its exact test and
the installed LinkifyIt 6.1.0 implementation. It mistakes `LinkifyIt.match(text)`
for regex-taking `String.match`; the API actually scans text as data.
The result is a high-confidence false positive, with full native alert, instance,
policy and source evidence retained locally. No fixture escaping, API workaround
or query suppression is appropriate. The owner explicitly approved dismissing
only alert #1 as a false positive; authenticated native readback confirms that
state and the recorded explanation. No source or query configuration changed.

The new workflow also requires adding its exact path to `CHECK_INPUTS.converter`
in `scripts/selectPullRequestChecks.js`, so future workflow-only changes run both
SDLC and converter checks. The existing native Git fixture test must fail before
that routing repair and pass afterward; unrelated pipeline/mod routing is retained.

The native lifecycle supports pausing and resuming the existing task, but does
not support replacing its original functionality metadata while work remains.
The attempted new begin was rejected without changing state. The task was resumed
with an explicit decision reference to this new-functionality slice and its
completed software selection, retaining R2 and every pending release obligation.
The original metadata is historical; this record declares the additional scope.
No lifecycle implementation or state file was edited to bypass that limitation.

### Publication qualification evidence

The copied owlapi registry predicate was exercised before adaptation: it accepts
the correct coordinate/bytes and rejects changed bytes, but rejects an existing
`latest` alongside `next`. That actual RED result is retained. The adapted native
Node tests accept independent release channels and reject wrong coordinates,
tag targets, registry origins, archive hashes and SHA-512 integrity, using
independent standard `abc` digest vectors. Hash/checksum helpers retain owlapi's
implementation; native npm and installed consumers retain archive interpretation.

The real candidate builder completes its native build, pack, install and existing
JavaScript/CLI/TS 7 consumer checks. Its fresh installed production graph has zero
native audit findings, and the bundle retains the complete audit and CycloneDX
output. TypeScript 7 checking, ESLint and strict typed-reference coverage pass;
the metric is 16,973/16,973 primary and 305/305 isolated documentation references.
Final verification remains tied to the native full receipt and frozen input
manifest under `.sdlc/runtime`, not to a prose assertion of readiness.

Native YAML 2.9 parses the workflow. Native Git Bash validates and executes the
actual publisher step against the asserted private candidate: SHA256SUMS passes,
then native jq stops the step before `npm publish`. A separate synthetic metadata
control shows the eligibility predicate permits public, clean candidates.
No public archive is created for that control. A one-byte archive mutation is
rejected both by the retained-candidate verifier and independently by native
`sha256sum`. The original candidate remains intact. Workflow-only scope routing
fails the existing real-Git test before the one-path repair and passes all 14
cases afterward; unrelated component routing remains unchanged.

The backport preservation check compares all 250 provider results, including
output bytes, status, applicability, semantic trees and diagnostics. Only
process-specific stderr is excluded from equality and remains in the raw reports.
Immediately after the backport, only its two declared dependency-selection hashes
differed. A subsequent normal comparison run refreshes the report after adding
the publication npm entry points. The selected corpus outcomes remain unchanged.
The full licence inventory for every changed dependency is retained separately
from the earlier frozen inventory; every added/updated package has its MIT notice.

Keep the first signed 818cdfe archive, original failures, candidate/control
bundles, source-reuse RED, native command logs and registry/alert readbacks until
the independent reviews and remote qualification consumers finish.
The converter task owns these artifacts; reassess their disposal after that
checkpoint while retaining the evidence needed for recovery and review.
No live release workflow, trusted-publisher binding or public-registry consumer
run is claimed. The serializer availability finding, trusted-base prerequisite
and remaining release-review obligations continue to block stable release.

### CI mutation duration and bounded parallelism

The owner asked whether job 102408860181 in run 34333962240 was proportionate.
Its retained native log shows 39.56 seconds for ordinary converter checks and
49 minutes 53.67 seconds for mutation testing: 3,958 library mutants in
40 minutes 3 seconds, followed by 184 CLI mutants in 9 minutes 48 seconds.
The library already uses native per-file coverage selection and four workers.
The CLI command runner must run its real-process suite for each mutant.
These are test-sensitivity workloads, not normal conversion latency.

The accepted baseline retains both full 90% mutation gates.
Use the owner's standing approval for configuration changes stated in this
execution plan to change only `.github/workflows/steam-community-bbcode.yml`:

- Run the existing library and CLI mutation npm scripts in independent Linux
  Node 24.20.0 matrix jobs, with the same locked install, four workers, full source
  scope, thresholds and 60-minute job limit. Retain each native report separately.
- Let the six ordinary converter-check jobs finish independently. Give the
  Ubuntu/Node 24 execution job a descriptive ` / checks` suffix; retain its old
  check name on an aggregate job that requires both the ordinary matrix and
  mutation matrix to succeed, including explicit rejection of failure, cancellation
  and skipped dependencies. Preserve every other existing check name.
- Add native workflow concurrency grouped by workflow, event and PR number;
  non-PR events use their unique run ID. Cancel only superseded PR runs, never
  a different PR, a main run or the publication workflow.

This uses supported GitHub Actions matrix, dependency and concurrency behavior;
no new package, action, cache, checker suppression or runner plugin is needed.
Stryker's documented incremental mode cannot detect CLI command-runner test
changes, and it does not independently invalidate dependency/environment changes.
Blind result reuse is therefore outside this bounded improvement.
The existing native TAP runner and current full library/CLI commands are retained.
Primary sources are GitHub's workflow concurrency documentation and Stryker's
TAP-runner and incremental-mode documentation, inspected on 9 September 2026.

Use the repository's configuration-preservation verification route: native YAML
and actionlint validation, preserved npm/configuration inputs, actual Bash gate
execution across dependency-result combinations, and independent bounded review.
The timing estimate is roughly ten minutes less critical-path time when both
mutation jobs can start together, plus early ordinary-check feedback.
Runner queueing and actual post-push timings must be measured before claiming a
real speedup; this change does not reduce the number of mutations executed.
Retain the original run log and original publication verification unchanged.

The publication implementation is signed commit
`771d9607238cc4afc74a091285f0de579c2652cd`.
Its two independent follow-ups report no new blocking findings, verify all
20 frozen paths and the retained archive, and leave live publication unperformed.

Native actionlint 1.7.12 and YAML 2.9 accept the parallel workflow.
An isolated execution of its actual Bash aggregate step accepts both matrices
succeeding and rejects all 24 other combinations of success, failure, cancellation,
skipping and absent results. Preservation checks retain the six runtime/OS entries,
original ordinary commands, triggers, dependency-review job, package/lock files
and both Stryker configurations. The executed gate matrix and original log are
retained under `.sdlc/runtime/converter/release-qualification`.
Startup-only Stryker checks, the native final verification receipt and the two
bounded follow-up reviews are retained there without overwriting prior evidence.
Actual parallel Linux execution and timing remain pending the next authorized push.

### Remaining introduced comparator dependency advisory

The owner authorized pushing `f92f16d` and updating draft PR #4; both operations
succeeded and native readback matches the exact approved head/body.
Run 34342366498 starts the two mutation profiles concurrently.
Its dependency-review job no longer reports `form-data`, but rejects the remaining
`nwmatcher` 1.3.9 selection under GHSA-6394-6h9h-cfjg.
This was among the retained fixed-corpus audit findings; reachability assessment
does not replace the configured introduced-dependency gate.

Select unmodified `nwmatcher` 1.4.4, the latest upstream release and the advisory's
fixed version. The original 12-entry npm archive and its complete Diego Perini MIT
grant/warranty notice were inspected and retained. No dependencies or install
hooks are declared by that package. Keep the comparator provider and its API intact.
The exact configuration change, under the owner's standing plan approval, is to
add `overrides["jsdom-nogyp@0.8.3"].nwmatcher = "1.4.4"` to
`tools/steam-community-bbcode/comparison/node/package.json` and regenerate only its
native lockfile. This overcomes the old parent's `~1.3.1` range; the other jsdom
consumer already permits 1.4.4. Preserve the Request/form-data override.

Use the dependency/configuration-preservation route: retain the failing native
dependency-review log and immediate pre-change graph/results; resolve and install
with scripts disabled; verify native installed paths, version and registry integrity;
rerun all 250 comparison observations and compare outputs, status, applicability,
semantic trees and diagnostics, excluding only retained process-specific stderr.
Run native audit, authored-doc checks, affected repository controls and the two
existing bounded reviewers. Preserve every threshold, primary dependency graph,
compiler role and publication/CI workflow. A new commit and push carry their
existing separate authorities; this record does not claim remote GREEN in advance.

Native resolution changes only the existing `node_modules/nwmatcher` lock entry;
both jsdom consumers resolve 1.4.4. Its archive SRI matches the native lock, and
the full installed MIT notice matches the inspected archive exactly.
The normal comparison run preserves all 250 outputs, applicability decisions,
statuses, semantic trees and diagnostics against the immediate pre-change report.
Only process-specific stderr is excluded and remains retained.
The refreshed native audit has nine moderate entries and no high/critical or
nwmatcher finding; its nonzero exit remains recorded for those legacy findings.
Native prose checks pass. Final affected-control and independent-review evidence
is retained beside the failing remote log; no live dependency-review pass is claimed.

### Local delivery and future repository move — 10 September 2026

The owner directed all further work to remain local on `steam-community-bbcode`
for a future move into a purpose-built repository. Do not push further work,
merge into remote `main`, update the ONI PR, dispatch remote workflows or publish.
This supersedes the earlier remote next steps without undoing completed,
previously authorized pushes. The destination repository is not yet specified.
Defer the ONI trusted-base prerequisite and publisher activation; they do not
advance the newly stated delivery scope.

The pending comparator fix is signed local commit
`88a63e679522cc2356a6ab0ca95be6475e7ccb5f`.
All seven source hashes match the retained review manifest and affected-verification
receipt. The independent verifier's saved `nwmatcher-followup/evidence.json`
records PASS for native resolution, archive identity and all 250 preserved
comparison observations. The direct security follow-up records no new causal
findings. Reuse those completed checks; do not restart reviewers or mutation runs
solely because the allowance interruption delayed the commit.

Use the R0 documentation route to align the existing release guide with this
direction and record the source, tooling, licences and ignored evidence that
must accompany the move. Check authored prose, documentation contracts and the
actual diff. Then use the existing `release:pack` command once from the clean
signed snapshot to retain an installable local development archive with its
native production audit and fresh API, CLI and TS 7 consumer evidence.
That archive is not the complete source-transfer bundle or an approved release.

Native npm metadata still lists `mdast-util-to-markdown` 2.1.2 as latest on
10 September. The retained dense-punctuation availability finding therefore
remains unresolved in the unmodified dependency. Preserve the private upstream
report, reproduction and isolated proposed fix for the future owner; do not
adopt a vendor patch, shim, alias or checker suppression. Renderer acceptance,
independent pre-release legal review and destination-specific release decisions
remain separate obligations. This local continuation changes no runtime or
publication configuration and makes no claim of new remote qualification.
