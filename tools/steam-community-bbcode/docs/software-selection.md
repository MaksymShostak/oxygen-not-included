# Software selection and qualification

Checked 2026-09-08. Reuses the complete research and alternatives in the
[v2 plan](../../../docs/plans/2026-09-08-steam-community-bbcode-implementation-plan-v2.md).
This refresh changes package/tooling facts; it does not rewrite the source dialect
or declare future conformance and release gates passed.

| Component | Current selected/candidate version | Role and evidence |
|---|---|---|
| Node | 24.20.0 | Repository development LTS; package target remains Node 22/24/26. |
| npm | 12.0.2 | Repository-selected native package manager, invoked with npm exec. |
| TypeScript | 7.0.2 | Primary strict JavaScript checker and declaration emitter; installed type consumer passed. |
| @types/node | 22.20.1 | Latest Node 22 declarations for the oldest supported runtime, as required by the plan. |
| @types/mdast / @types/unist | 4.0.4 / 3.0.3 | Public declaration dependencies, installed without dev dependency hoisting. |
| mdast-util-from-markdown | 2.0.3 | Maintained independent target parser, selected for semantic regression oracles. |
| mdast-util-to-markdown | 2.1.2 | Maintained runtime serializer; owns Markdown escaping and code-fence selection. |
| mdast-util-gfm / micromark-extension-gfm | 3.1.0 / 3.0.0 | Maintained GFM extensions; no custom Markdown grammar. |
| @bbob/parser | 4.4.1 | Rejected for full-parser adoption; 17 syntax/span cases pass but strict declaration checking fails. |
| Chevrotain | 13.2.0 | Selected runtime toolkit after Steam grammar, opaque-body, item-boundary, malformed-input, attribute, limit and strict-checker qualification. |
| Peggy | 5.1.0 | Current MIT parser generator; credible alternative, but requires generated parser/build/source-map work. Not installed. |
| Ajv | 8.20.0 | Native JSON Schema consumer for registry validation; development dependency. |
| ajv-formats | 3.0.1 | Ajv's maintained MIT format implementation; full URI/date validation with its shipped licence inspected. |
| fast-check | 4.9.0 | Maintained MIT property generation, seed replay and shrinking; shipped licence and manifest inspected, installed without lifecycle scripts. |
| vfile-location | 5.0.3 | Considered for unist positions; rejected after a 1 MiB newline-heavy lookup took 5.39 seconds. Native lexer coordinates are already available. |
| type-coverage | 2.30.1 | Fails against TypeScript 7.0.2's absent compiler API; isolated TypeScript 6.0.3 qualification authorized below. |
| tsd | 0.33.0 | Alternative declaration harness; includes @tsd/typescript 5.9. It does not measure authored JavaScript type coverage. Not installed. |

Versions were queried through the authoritative npm registry before locking. The
native package locks retain exact resolved identities/integrities. Tarballs were
downloaded with lifecycle scripts disabled and their actual licence texts inspected.
Parser candidates remain development-only until qualified and selected. Chevrotain
was promoted to a runtime dependency after the grammar qualification described below.

## Rights and boundaries

The package and authored tests/scripts are AGPL-3.0-only. Root MIT/Klei licensing is
unchanged. The full licence is from the SPDX licence-list-data project's
[AGPL-3.0-only text](https://raw.githubusercontent.com/spdx/license-list-data/main/text/AGPL-3.0-only.txt),
SHA-256 `d8a6cc31abc16b6748c7a21f21611f5a1ec33f67d22ca23d7da1c19b95496bee`.
PowerShell/curl TLS retrieval failed with host credential errors; Node's HTTPS
fetch retrieved the complete text. No TLS validation or host setting was disabled.

Direct MDAST, micromark, BBob and definition packages carry MIT permission and
notice obligations. BBob's two locked companion packages have the identical MIT
licence SHA-256 `a2f19878ad19b733e3b97f0118f0282c2ba70e6161076539fa4c23b9158dfe91`.
Chevrotain and TypeScript carry Apache-2.0 terms, including licence/notice retention,
patent terms and modified-file notices if their source is modified. Their source
is not patched or copied into the implementation. Ajv is MIT. type-coverage's
published tarball omits a licence file; the exact version's upstream
[MIT licence](https://raw.githubusercontent.com/plantain-00/type-coverage/v2.30.1/LICENSE)
was inspected. No paid account, hosted service, telemetry integration, or remote
fetching is needed by these transformation libraries. npm/pip dependency acquisition
is development setup only. Exact graph reports and copied dependency notices must
be refreshed for distribution; pre-1.0 legal review remains an explicit plan gate.

## Parser decision

BBob's [supported parser API](https://raw.githubusercontent.com/JiLiZART/BBob/master/packages/bbob-parser/README.md)
and shipped source were inspected. `contextFreeTags`, `caseFreeTags` and
`enableEscapeTags` preserve valid opaque regions. Item boundaries are separate
sibling nodes. Native spans retain unknown syntax and reveal malformed pairs;
the recovery AST itself cannot be treated as a valid Steam semantic tree.
The strict checker fails on `TagNode.toJSON()` and `TagNodeObject` optional
coordinate properties. The [isolated reproduction](../comparison/bbob/README.md)
preserves this result; no ambient declaration, vendor patch or checker suppression
is authorized.

[Chevrotain's maintained parsing DSL](https://chevrotain.io/docs/tutorial/step2_parsing.html)
provides native grammar validation, CSTs and error reporting while keeping grammar
authoring in JavaScript. Its [lexer contracts](https://chevrotain.io/docs/guide/resolving_lexer_errors.html)
provide source positions and explicit lexical modes/pattern capabilities. This
fits the residual custom gap: a Steam dialect grammar and semantic interpretation,
not a replacement parser engine. [Peggy](https://peggyjs.org/documentation.html)
is a maintained alternative, with an additional generated-parser lifecycle. Neither
candidate is a full Steam converter. Subsequent Chevrotain grammar qualification
passed 24 checks, including 3,000 seeded/shrunk bounded-source cases, strict JSDoc,
2003/2003 typed references, native CST nesting/offsets, attribute preservation,
opaque regions, malformed-source diagnostics and runtime resource limits. This
qualifies the parser foundation; MDAST transformation and conformance remain.

The pure `vfile-location@5.0.3` API and shipped MIT licence were inspected. A native
`location(value).toPoint(value.length)` probe on repeated `x\n` took approximately
0.44 ms at 4 KiB, 21.4 ms at 64 KiB and 5389.9 ms at 1 MiB on this host. Its scan
repeats searches across remaining text. It was removed before adoption. The stable
remaining boundary maps Chevrotain's existing inclusive coordinates to unist's
exclusive end position, including the terminal newline case; no source-position
indexer, vendor patch or compatibility fallback is needed.

The selected MDAST serializer and GFM extension were promoted from qualification
dependencies to runtime dependencies after independent target-parser checks passed
for literal punctuation/HTML-shaped text, nested lists, deletion, table cells,
opaque `noparse` and code containing backticks/fences. Public API declarations use
explicit native MDAST child-slot extensions; a generic conditional recursive type
was rejected by the strict checker and replaced with direct native `Omit` contracts.
No checker setting or suppression changed.

[Node's native WHATWG URL API](https://nodejs.org/api/url.html#the-whatwg-url-api)
owns resource URL parsing. It is available across the selected Node matrix and
requires no added dependency. The residual custom responsibility is destination
policy: active schemes, relative-reference preservation, and rejecting controls or
backslashes before normalization. URL parsing never authorizes or performs fetching.

The live [GitHub Markdown endpoint](https://docs.github.com/en/rest/markdown/markdown)
was exercised with synthetic public text, without credentials or repository context.
It removed `<u>`, retained `<ins>`, and retained generated details/summary, strong text,
tables and deletion while escaping HTML-shaped source. `<ins>` denotes insertion;
it is not adopted as an equivalent underline semantic. The default underline policy
therefore remains diagnosed plain-text loss. Evidence request, response and hashes
are retained in `artifacts/github-rendering/` for review, not treated as a permanent
guarantee of GitHub behavior. `qualify:github` is an explicit live command, separate
from offline checks. Collapsed-section syntax is documented by
[GitHub](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/organizing-information-with-collapsed-sections).

## Accepted type coverage decision

Observed: `type-coverage@2.30.1` advertises a TypeScript peer range including 7,
but fails at `ts.SyntaxKind.Unknown` because the API object is unavailable.
This is a tool failure, not RED for converter behavior or a passing coverage result.
[Microsoft's TypeScript 7 release notes](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/)
confirm that 7.0 has no programmatic compiler API and describe parallel use of 6.0
for API-consuming tools. Their recommended alias/compatibility package is not used:
the standing no-shims requirement applies.

Accepted explicit version exception: retain TypeScript **7.0.2** as the
only primary checker, declaration emitter and installed-consumer compiler. Run
`type-coverage@2.30.1` in a separate development tooling package with the actual
unmodified **typescript@6.0.3** package, whose compiler API the tool consumes.
No alias, re-export package, fallback, patch or global installation is involved.
The older compiler's sole role is supplementary coverage measurement; its result
cannot substitute for a TypeScript 7 check. Source syntax unsupported by either
required checker must be resolved explicitly, not suppressed.

Exact approved files/settings:

- `tooling/type-coverage/package.json`: private AGPL-3.0-only tooling package;
  dev dependencies `type-coverage: 2.30.1`, `typescript: 6.0.3` and
  `@types/node: 22.20.1`; corresponding native npm lock.
- Main package manifest: remove the failed direct type-coverage dependency and
  direct its coverage command to that tooling package. Keep TypeScript 7.0.2.
- Coverage scope: authored `src/**/*.js`, `scripts/**/*.js`, and current
  `test/**/*.js`; exclude generated declarations and isolated rejected-candidate
  comparison environments. Required minimum: 100% explicit/inferred types, strict
  mode, with zero exclusions/suppressions. This is a typing metric, not conformance.

On 2026-09-08 the user explicitly authorized unmodified TypeScript 6.0.3 solely
inside this isolated package, retaining 7.0.2 for all primary checking, declaration
generation and consumer tests, with no aliases, shims or checker suppressions.
This is the evidenced owner exception required by VER-01. Reassess when a stable TypeScript API and a maintained
coverage tool support the primary compiler directly, or before dependency refresh/
release. Execution results belong in the execution record; the exception itself
does not turn a failed check into a pass.
# Plain URL recognition qualification (9 September 2026)

The registry resolved `linkify-it` **6.1.0** as the current stable release. Its
published archive contains an ESM build and native declarations; the actual MIT
licence grants reuse subject to retaining its notice. Its `uc.micro` dependency
also carries the MIT notice. No declaration compatibility package is needed.
The native options disable fuzzy links/emails and alternate protocols; no custom
normalizer, regular-expression override, alias or patch is installed. The
qualification exercises Unicode offsets and punctuation boundaries without
interpreting source Markdown. TypeScript 7.0.2 accepts the published declarations.

The existing GFM parser is appropriate for the target and reverse direction, but
would interpret literal source punctuation during URL discovery. Node's WHATWG
URL parser is retained for each discovered destination; it does not itself find
URLs in prose. The residual custom gap is the registry's Steam-specific host/path
classification and its fidelity annotation on native MDAST links. After the
qualification and six public conversion cases passed, native npm 12 promoted
the exact dependency from development qualification to runtime. Install scripts
were disabled; the audit reported zero known vulnerabilities at this check.

Sources: [maintainer API and examples](https://github.com/markdown-it/linkify-it),
published archive `linkify-it-6.1.0.tgz` with integrity
`sha512-wJ/TwpSDTLepCrQoYWYIExIKg5Zchex2Nn5yk2mFnB+6PtdkHtyLx742md9csRjjOnGkKIS/RrbY7l8D6gT9Vw==`.
The archive is retained under `.sdlc/runtime/converter/selection` for review.

## GFM syntax versus additional GitHub autolinking

The native MDAST GFM extension intentionally combines syntax parsing with a
post-parse autolinker used on some GitHub surfaces. Its maintainer documents that
this second pass treats escapes/character references differently. A retained
live Markdown REST probe showed both escapes and references remained plain text,
including actual converted noparse and malformed-image inputs. Accordingly,
conformance uses the public `Extension` composition API to select syntax handlers
without renderer transforms. A separate unchanged full-extension consumer checks
for additional links, producing `GFM_RENDERER_AUTOLINK_POSSIBLE` when appropriate.
No vendor code, checker, fixture payload, string escaping or parser algorithm was
patched. This is a distinction between two documented target contracts, not a
claim that every GitHub surface renders them identically.

The previously qualified `mdast-util-from-markdown` 2.0.3 and
`micromark-extension-gfm` 3.0.0 now reside in runtime dependencies for that check
and the planned reverse direction. The native consumer adds parsing cost to the
forward path; end-to-end resource qualification must include it.

Sources: [maintainer explanation](https://github.com/syntax-tree/mdast-util-gfm-autolink-literal#what-is-this),
[GFM specification](https://github.github.com/gfm/), and the retained synthetic
request/response/provenance in `artifacts/github-rendering/literal-autolinks`.

## Literal labels and flow whitespace (9 September 2026)

The owner-approved [forward refinement plan](../../../docs/plans/2026-09-09-steam-community-bbcode-forward-refinements.md)
corrects two interpretation policies without changing dependencies or the serializer.
Unpaired unknown labels such as `[sd]` retain source and the unknown-construct
diagnostic. A missing-closer diagnostic requires a recognized tag. Known malformed
tags, incomplete headers and unmatched closers remain diagnosed; explicitly paired
unknown constructs retain their whole body without activating nested formatting.
Valve's `[noparse]` continues to express explicitly literal source.

Flow construction removes ordinary ASCII spaces/tabs and LF/CRLF/CR at the edges
of text adjoining an explicit converted block or list-item delimiter. Separators
between blocks and after item markers are layout in this conversion policy.
Document edges without a block/item boundary, interior paragraph separators,
inline spaces, nonbreaking spaces, code/noparse payloads, link labels and literal
fallback are preserved. Only text created from ordinary Steam text is eligible;
the immutable source syntax, source spans and original document are unchanged.
Nonbreaking text before a first list marker requires the existing whole-list
preservation fallback instead of being silently discarded as indentation.

The native serializer correctly encodes literal leading spaces as character
references. It was receiving layout inside paragraph text, including trailing
newlines that made GFM lists loose. A GitHub Markdown REST comparison against an
independently authored target first failed on those extra paragraph wrappers and
then matched after the interpretation change, while protected padding remained
identical. The opt-in command is:

```text
npm run qualify:github -- --scenario=flow-whitespace
```

Actual and expected Markdown, HTML and request provenance are retained under
`artifacts/github-rendering/flow-whitespace`; the initial failing response is
retained under `artifacts/github-rendering/flow-whitespace-red`. This is target
HTML evidence, not visual acceptance or a claim about every Steam renderer.
Backslashes and character references remain owned by the native serializer;
there is no output-string cleanup, global trim or requirement to edit descriptions.

Sources: [Valve formatting help](https://steamcommunity.com/comment/WorkshopItem/formattinghelp),
[GFM paragraphs and lists](https://github.github.com/gfm/),
[native serialization](https://github.com/syntax-tree/mdast-util-to-markdown),
and [CSS whitespace processing](https://drafts.csswg.org/css-text-3/#white-space-processing).
