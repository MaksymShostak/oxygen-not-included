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
