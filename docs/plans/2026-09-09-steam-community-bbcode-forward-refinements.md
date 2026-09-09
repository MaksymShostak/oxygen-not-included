# Steam Community BBCode forward refinement implementation plan

**Goal:** Make forward-conversion diagnostics precise and remove redundant layout
whitespace without changing literal content or meaningful document structure.

**Architecture:** Keep the existing source parser, Steam-aware MDAST interpretation,
and native GFM serializer. Correct structural diagnostics at their source. Interpret
layout whitespace while constructing flow content; do not rewrite serialized GFM.

**Tech stack:** Existing checked JavaScript, Chevrotain, MDAST/GFM utilities and
Node test runner. TypeScript 7.0.2 remains the primary checker and declaration/consumer
compiler; unmodified TypeScript 6.0.3 remains confined to type-coverage tooling.

**Baseline:** [Accepted v2 plan](2026-09-08-steam-community-bbcode-implementation-plan-v2.md),
especially its unknown-source preservation, native serialization and conformance
requirements. The committed baseline remains immutable; this document is its
owner-approved implementation amendment, linked from the execution record.

**Authority and route:** On 2026-09-09 Max requested, "Let's update the plan to take
into account your recommendations at this point and proceed to implement" after
reviewing the diagnostic and whitespace recommendations. Continue the existing R2
task, using test-first repair for changed behavior and preservation tests for
already-correct behavior. Execute inline with the repository-adapted
`.sdlc/skills/test-driven-development/SKILL.md`; no delegation or new approval loop.

## Constraints and acceptance

- D1: Unpaired unknown bracket labels retain literal source and one accurate
  `STEAM_UNKNOWN_CONSTRUCT` diagnostic. They do not acquire a missing-closer
  diagnostic merely because their spelling resembles a tag.
- D2: Known unclosed tags, mismatches, incomplete headers and unmatched closing
  tags remain diagnosed. Explicitly paired unknown markup remains preserved with
  an unknown-construct diagnostic; nested recognized tags are not activated within it.
- D3: `[noparse]` stays opaque and produces no diagnostic for its literal labels.
  Source bytes, immutable syntax, source spans, nesting and resource limits remain intact.
- W1: Treat ordinary ASCII spaces/tabs and line endings used to separate flow
  blocks or delimit list items as layout, subject to independent target rendering
  checks. Preserve paragraph separation and list hierarchy. Do not globally trim text.
- W2: Preserve inline separation, meaningful internal newlines, nonbreaking spaces,
  code/noparse payloads and literal fallback for malformed/unknown constructs.
- W3: Leave context-sensitive escaping with `mdast-util-to-markdown`. Backslash
  escapes and numeric character references are valid GFM and must not be removed
  by a cleanup pass. No source-document rewrite is required.
- No new dependency, configuration, public option, shim, checker suppression,
  reverse-conversion change, licence/version change, commit or publication.
- Full converter and existing R2 repository verification remain required. Live
  renderer evidence is separate from syntax checks, visual acceptance and release.

## Evidence and oracle

The committed description at `e51073134c1e306f8bf0f25b8a9fe6be1480b22c`
converted with four warnings for two literal credit labels. The current grammar
already gives an unpaired unknown label no body, while the structural diagnostic
pass still assumes every opening token needs a closer. Small fixtures reproduce
this contradiction independently of the mod description.

Whitespace probes show source separators retained inside paragraph text, e.g.
`"\n\nBody\n\n"`, and list-item values such as `" One\n"`. These values cause the
native serializer to add blank lines and preserve leading spaces as `&#x20;`.
This identifies the interpretation layer as the change point, not a serializer bug.

Use hand-authored expected GFM trees/text, the native GFM consumer, and the existing
opt-in GitHub HTML rendering probe for synthetic fixtures. Rendering establishes
target structure and text, not universal Steam renderer equivalence. Keep the
normalization narrow wherever source significance is uncertain.

Primary references (checked 2026-09-09):

- [Valve formatting examples and noparse](https://steamcommunity.com/comment/WorkshopItem/formattinghelp).
- [GFM paragraphs](https://github.github.com/gfm/#paragraphs),
  [lists](https://github.github.com/gfm/#lists), and
  [character references](https://github.github.com/gfm/#entity-and-numeric-character-references).
- [Native serializer](https://github.com/syntax-tree/mdast-util-to-markdown).
- [CSS whitespace processing](https://drafts.csswg.org/css-text-3/#white-space-processing).

## Task 1: Precise structural diagnostics

Files: create `tools/steam-community-bbcode/test/literal-brackets.test.js`;
modify `tools/steam-community-bbcode/src/steam/parse-steam-bbcode-syntax.js`.
Exercise public parse, interpretation and GFM functions; introduce no exports.

- [x] Add literal-label regressions in prose, emphasis and links, with known-tag,
  paired-unknown, malformed-header and noparse controls. Core expected assertion:

  ```js
  assert.deepEqual(
    steamCommunityBbcodeToGfm('[sd] QooLiO').diagnostics.map(d => d.code),
    ['STEAM_UNKNOWN_CONSTRUCT'],
  );
  ```
- [x] Run `npm --prefix tools/steam-community-bbcode test`;
  retain the expected extra `STEAM_UNCLOSED_TAG` failure under `.sdlc/runtime/converter/`.
- [x] Align missing-closer diagnostics with recognized tag semantics; preserve all
  other structural checks and complete source provenance.
- [x] Run the runtime tests and `npm --prefix tools/steam-community-bbcode run qualify:parser`.

## Task 2: Flow layout and protected content

Files: create `tools/steam-community-bbcode/test/flow-whitespace.test.js`;
modify `tools/steam-community-bbcode/src/mdast/steam-bbcode-to-mdast.js` and
`tools/steam-community-bbcode/scripts/qualify-github-rendering.js` as needed.
Record the policy in `tools/steam-community-bbcode/docs/software-selection.md`.
Keep the source syntax unchanged and target serialization native.

- [x] Add a first regression for block separators and a separate list-delimiter
  regression, observing each intended failure before its implementation. Example:

  ```js
  assert.equal(
    steamCommunityBbcodeToGfm('[h1]Title[/h1]\n\nBody\n\n[hr][/hr]').value,
    '# Title\n\nBody\n\n***\n',
  );
  ```
- [x] Run `npm --prefix tools/steam-community-bbcode test`.
- [x] Implement narrow normalization of proven flow delimiters, keeping protected
  payloads, meaningful interior spacing and source coordinates intact.
- [x] Add preservation controls for LF/CRLF, nested lists, multi-paragraph items,
  inline emphasis/link separation, nonbreaking spaces, malformed fallback and opaque
  payloads. Inspect independently parsed GFM structure as well as emitted text.
- [x] Add and execute a synthetic `flow-whitespace` scenario through the existing
  opt-in rendering script, with expected HTML assertions and retained request/response.
  Run `npm --prefix tools/steam-community-bbcode run qualify:github -- --scenario=flow-whitespace`.
- Final repository gate: run `npm run sdlc -- verify --keep-going` after all
  worktree editing finishes. The machine-owned result in
  `.sdlc/runtime/verification/full.json` must record `passed: true` and identical
  before/after identities. Retain actual failures and refresh only generated
  artifacts whose inputs changed.
- [x] Rerun the committed Workshop description through the modified converter;
  retain output and diagnostic/structure evidence, leaving the source file unchanged.
- [x] Update the execution record with RED/GREEN, final verification and remaining
  release gates. Retain test logs/rendering evidence for review and remove only
  spent scratch owned by this follow-up. Earlier reverse-work evidence remains in use.

Implementation and evidence are recorded in the
[execution record](2026-09-08-steam-community-bbcode-execution.md#forward-refinements-9-september-2026).
The full local check passed with 329 runtime tests, 25 parser tests, 198 conformance
executions, 100% type coverage and installed consumers. Implementation is complete.
An earlier final refresh passed every command, but its worktree fingerprint changed
while another task edited SDLC files. Those edits have since been merged at
`eb58b4c3118b44313de0d8ca78950990641276b7`. The resumed verification output is retained
in `.sdlc/runtime/converter/forward-refinements-resumed-verification.log`; the
machine-owned result above records its outcome and exact input identities.
`forward-refinements-converter-final.log` retains the separate converter check.
The original plan's independent review, cross-platform and release gates remain separate.
