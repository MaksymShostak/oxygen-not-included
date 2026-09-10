# Software selection and qualification

Checked 2026-09-08.
Reuses the complete research and alternatives in the [v2 plan](../../../docs/plans/2026-09-08-steam-community-bbcode-implementation-plan-v2.md).
This refresh changes package/tooling facts; it does not rewrite the source dialect or declare future conformance and release gates passed.

## Partial reverse continuation (9 September 2026)

The accepted plan's reverse capability continues to use the already qualified `mdast-util-from-markdown` 2.0.3, `mdast-util-gfm` 3.1.0 and `micromark-extension-gfm` 3.0.0.
The native [parser API](https://github.com/syntax-tree/mdast-util-from-markdown) and [GFM extension composition](https://github.com/syntax-tree/mdast-util-gfm) were rechecked on 9 September.
Requirements and the locked dependency graph are unchanged; the existing current-version and MIT notice qualification applies.
No new parser, dependency, compiler alias or vendor modification is introduced.

Those libraries own source syntax and native MDAST positions.
They do not emit Steam syntax or enforce this package's target-fidelity policies.
The residual custom work is the partial Steam renderer, source-scoped diagnostics, literal encoding and resource accounting.
Valve's [Workshop formatting help](https://steamcommunity.com/comment/WorkshopItem/formattinghelp) remains the target syntax reference, including opaque noparse regions.
Splitting each literal opening bracket into its own opaque region prevents an embedded, case-varied noparse closing tag from activating source markup.
No input is fetched.

Reverse mappings have independently authored exact Steam syntax and semantic trees.
Unsupported cases retain their complete original source, and native source nodes are compared independently with every reported occurrence.
The reverse report is generated from those executions in its own `coverage.json.reverse` section and support-matrix section; forward outcomes do not confer reverse support.
Relative destinations lack a qualified target document base and are preserved.
Uneven/aligned tables, metadata, meaningful image alternatives, unsupported heading levels, task state, HTML, inline code and reference syntax remain explicitly partial.

Input bytes are bounded before native parsing.
Native-tree count/depth limits apply before rendering, and output bytes are checked while assembling subtrees.
These bounds do not claim a timeout or limit native parser intermediate memory.
The library returns no partial output on quota failure.
Seeded generated checks exercise arbitrary bounded Markdown and literal delimiter injection; they do not establish universal Steam renderer equivalence or replace independent review.

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

Versions were queried through the authoritative npm registry before locking.
The native package locks retain exact resolved identities/integrities.
Tarballs were downloaded with lifecycle scripts disabled and their actual licence texts inspected.
Parser candidates remain development-only until qualified and selected.
Chevrotain was promoted to a runtime dependency after the grammar qualification described below.

## Rights and boundaries

The package and authored tests/scripts are AGPL-3.0-only. Root MIT/Klei licensing is unchanged.
The full licence is from the SPDX licence-list-data project's [AGPL-3.0-only text](https://raw.githubusercontent.com/spdx/license-list-data/main/text/AGPL-3.0-only.txt), SHA-256 `d8a6cc31abc16b6748c7a21f21611f5a1ec33f67d22ca23d7da1c19b95496bee`.
PowerShell/curl TLS retrieval failed with host credential errors; Node's HTTPS fetch retrieved the complete text.
No TLS validation or host setting was disabled.

Direct MDAST, micromark, BBob and definition packages carry MIT permission and notice obligations.
BBob's two locked companion packages have the identical MIT licence SHA-256 `a2f19878ad19b733e3b97f0118f0282c2ba70e6161076539fa4c23b9158dfe91`.
Chevrotain and TypeScript carry Apache-2.0 terms, including licence/notice retention, patent terms and modified-file notices if their source is modified. Their source is not patched or copied into the implementation. Ajv is MIT. type-coverage's published tarball omits a licence file; the exact version's upstream [MIT licence](https://raw.githubusercontent.com/plantain-00/type-coverage/v2.30.1/LICENSE) was inspected. No paid account, hosted service, telemetry integration, or remote fetching is needed by these transformation libraries. npm/pip dependency acquisition is development setup only.
The explicit [licence compatibility assessment](licensing-review.md) completes the current graph review and records the distribution obligations.
Refresh it when dependency identities, copied material or the packaging boundary change.

## Parser decision

BBob's [supported parser API](https://raw.githubusercontent.com/JiLiZART/BBob/master/packages/bbob-parser/README.md) and shipped source were inspected.
`contextFreeTags`, `caseFreeTags` and `enableEscapeTags` preserve valid opaque regions.
Item boundaries are separate sibling nodes.
Native spans retain unknown syntax and reveal malformed pairs; the recovery AST itself cannot be treated as a valid Steam semantic tree.
The strict checker fails on `TagNode.toJSON()` and `TagNodeObject` optional coordinate properties.
The source-checkout reproduction in `comparison/bbob/README.md` preserves this result; no ambient declaration, vendor patch or checker suppression is authorized.

[Chevrotain's maintained parsing DSL](https://chevrotain.io/docs/tutorial/step2_parsing.html) provides native grammar validation, CSTs and error reporting while keeping grammar authoring in JavaScript.
Its [lexer contracts](https://chevrotain.io/docs/guide/resolving_lexer_errors.html) provide source positions and explicit lexical modes/pattern capabilities. This fits the residual custom gap: a Steam dialect grammar and semantic interpretation, not a replacement parser engine. [Peggy](https://peggyjs.org/documentation.html) is a maintained alternative, with an additional generated-parser lifecycle.
Neither candidate is a full Steam converter.
Subsequent Chevrotain grammar qualification passed 24 checks, including 3,000 seeded/shrunk bounded-source cases, strict JSDoc, 2003/2003 typed references, native CST nesting/offsets, attribute preservation, opaque regions, malformed-source diagnostics and runtime resource limits.
This qualifies the parser foundation; MDAST transformation and conformance remain.

The pure `vfile-location@5.0.3` API and shipped MIT licence were inspected.
A native `location(value).toPoint(value.length)` probe on repeated `x\n` took approximately 0.44 ms at 4 KiB, 21.4 ms at 64 KiB and 5389.9 ms at 1 MiB on this host.
Its scan repeats searches across remaining text.
It was removed before adoption.
The stable remaining boundary maps Chevrotain's existing inclusive coordinates to unist's exclusive end position, including the terminal newline case; no source-position indexer, vendor patch or compatibility fallback is needed.

The selected MDAST serializer and GFM extension were promoted from qualification dependencies to runtime dependencies after independent target-parser checks passed for literal punctuation/HTML-shaped text, nested lists, deletion, table cells, opaque `noparse` and code containing backticks/fences.
Public API declarations use explicit native MDAST child-slot extensions; a generic conditional recursive type was rejected by the strict checker and replaced with direct native `Omit` contracts.
No checker setting or suppression changed.

[Node's native WHATWG URL API](https://nodejs.org/api/url.html#the-whatwg-url-api) owns resource URL parsing.
It is available across the selected Node matrix and requires no added dependency.
The residual custom responsibility is destination policy: active schemes, relative-reference preservation, and rejecting controls or backslashes before normalization.
URL parsing never authorizes or performs fetching.

The live [GitHub Markdown endpoint](https://docs.github.com/en/rest/markdown/markdown) was exercised with synthetic public text, without credentials or repository context.
It removed `<u>`, retained `<ins>`, and retained generated details/summary, strong text, tables and deletion while escaping HTML-shaped source. `<ins>` denotes insertion; it is not adopted as an equivalent underline semantic. The default underline policy therefore remains diagnosed plain-text loss. Evidence request, response and hashes are retained in `artifacts/github-rendering/` for review, not treated as a permanent guarantee of GitHub behavior. `qualify:github` is an explicit live command, separate from offline checks. Collapsed-section syntax is documented by [GitHub](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/organizing-information-with-collapsed-sections).

## Accepted type coverage decision

Observed: `type-coverage@2.30.1` advertises a TypeScript peer range including 7, but fails at `ts.SyntaxKind.Unknown` because the API object is unavailable.
This is a tool failure, not RED for converter behavior or a passing coverage result.
[Microsoft's TypeScript 7 release notes](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/) confirm that 7.0 has no programmatic compiler API and describe parallel use of 6.0 for API-consuming tools.
Their recommended alias/compatibility package is not used: the standing no-shims requirement applies.

Accepted explicit version exception: retain TypeScript **7.0.2** as the only primary checker, declaration emitter and installed-consumer compiler.
Run `type-coverage@2.30.1` in a separate development tooling package with the actual unmodified **typescript@6.0.3** package, whose compiler API the tool consumes.
No alias, re-export package, fallback, patch or global installation is involved.
The older compiler's sole role is supplementary coverage measurement; its result cannot substitute for a TypeScript 7 check.
Source syntax unsupported by either required checker must be resolved explicitly, not suppressed.

Exact approved files/settings:

- `tooling/type-coverage/package.json`: private AGPL-3.0-only tooling package; dev dependencies `type-coverage: 2.30.1`, `typescript: 6.0.3` and `@types/node: 22.20.1`; corresponding native npm lock.
- Main package manifest: remove the failed direct type-coverage dependency and direct its coverage command to that tooling package.
  Keep TypeScript 7.0.2.
- Coverage scope: authored `src/**/*.js`, `scripts/**/*.js`, and current `test/**/*.js`; exclude generated declarations and isolated rejected-candidate comparison environments.
  Required minimum: 100% explicit/inferred types, strict mode, with zero exclusions/suppressions.
  This is a typing metric, not conformance.

On 2026-09-08 the user explicitly authorized unmodified TypeScript 6.0.3 solely inside this isolated package, retaining 7.0.2 for all primary checking, declaration generation and consumer tests, with no aliases, shims or checker suppressions.
This is the evidenced owner exception required by VER-01.
Reassess when a stable TypeScript API and a maintained coverage tool support the primary compiler directly, or before dependency refresh/ release.
Execution results belong in the execution record; the exception itself does not turn a failed check into a pass.
# Plain URL recognition qualification (9 September 2026)

The registry resolved `linkify-it` **6.1.0** as the current stable release.
Its published archive contains an ESM build and native declarations; the actual MIT licence grants reuse subject to retaining its notice.
Its `uc.micro` dependency also carries the MIT notice.
No declaration compatibility package is needed.
The native options disable fuzzy links/emails and alternate protocols; no custom normalizer, regular-expression override, alias or patch is installed.
The qualification exercises Unicode offsets and punctuation boundaries without interpreting source Markdown.
TypeScript 7.0.2 accepts the published declarations.

The existing GFM parser is appropriate for the target and reverse direction, but would interpret literal source punctuation during URL discovery.
Node's WHATWG URL parser is retained for each discovered destination; it does not itself find URLs in prose.
The residual custom gap is the registry's Steam-specific host/path classification and its fidelity annotation on native MDAST links.
After the qualification and six public conversion cases passed, native npm 12 promoted the exact dependency from development qualification to runtime.
Install scripts were disabled; the audit reported zero known vulnerabilities at this check.

Sources: [maintainer API and examples](https://github.com/markdown-it/linkify-it), published archive `linkify-it-6.1.0.tgz` with integrity `sha512-wJ/TwpSDTLepCrQoYWYIExIKg5Zchex2Nn5yk2mFnB+6PtdkHtyLx742md9csRjjOnGkKIS/RrbY7l8D6gT9Vw==`.
The archive is retained under `.sdlc/runtime/converter/selection` for review.

## GFM syntax versus additional GitHub autolinking

The native MDAST GFM extension intentionally combines syntax parsing with a post-parse autolinker used on some GitHub surfaces.
Its maintainer documents that this second pass treats escapes/character references differently.
A retained live Markdown REST probe showed both escapes and references remained plain text, including actual converted noparse and malformed-image inputs.
Accordingly, conformance uses the public `Extension` composition API to select syntax handlers without renderer transforms.
A separate unchanged full-extension consumer checks for additional links, producing `GFM_RENDERER_AUTOLINK_POSSIBLE` when appropriate.
No vendor code, checker, fixture payload, string escaping or parser algorithm was patched.
This is a distinction between two documented target contracts, not a claim that every GitHub surface renders them identically.

The previously qualified `mdast-util-from-markdown` 2.0.3 and `micromark-extension-gfm` 3.0.0 now reside in runtime dependencies for that check and the planned reverse direction.
The native consumer adds parsing cost to the forward path; end-to-end resource qualification must include it.

Sources: [maintainer explanation](https://github.com/syntax-tree/mdast-util-gfm-autolink-literal#what-is-this), [GFM specification](https://github.github.com/gfm/), and the retained synthetic request/response/provenance in `artifacts/github-rendering/literal-autolinks`.

## Literal labels and flow whitespace (9 September 2026)

The owner-approved [forward refinement plan](../../../docs/plans/2026-09-09-steam-community-bbcode-forward-refinements.md) corrects two interpretation policies without changing dependencies or the serializer.
Unpaired unknown labels such as `[sd]` retain source and the unknown-construct diagnostic.
A missing-closer diagnostic requires a recognized tag.
Known malformed tags, incomplete headers and unmatched closers remain diagnosed; explicitly paired unknown constructs retain their whole body without activating nested formatting.
Valve's `[noparse]` continues to express explicitly literal source.

Flow construction removes ordinary ASCII spaces/tabs and LF/CRLF/CR at the edges of text adjoining an explicit converted block or list-item delimiter. Separators between blocks and after item markers are layout in this conversion policy. Document edges without a block/item boundary, interior paragraph separators, inline spaces, nonbreaking spaces, code/noparse payloads, link labels and literal fallback are preserved.
Only text created from ordinary Steam text is eligible; the immutable source syntax, source spans and original document are unchanged.
Nonbreaking text before a first list marker requires the existing whole-list preservation fallback instead of being silently discarded as indentation.

The native serializer correctly encodes literal leading spaces as character references.
It was receiving layout inside paragraph text, including trailing newlines that made GFM lists loose.
A GitHub Markdown REST comparison against an independently authored target first failed on those extra paragraph wrappers and then matched after the interpretation change, while protected padding remained identical.
The opt-in command is:

```text
npm run qualify:github -- --scenario=flow-whitespace
```

Actual and expected Markdown, HTML and request provenance are retained under `artifacts/github-rendering/flow-whitespace`; the initial failing response is retained under `artifacts/github-rendering/flow-whitespace-red`.
This is target HTML evidence, not visual acceptance or a claim about every Steam renderer.
Backslashes and character references remain owned by the native serializer; there is no output-string cleanup, global trim or requirement to edit descriptions.

Sources: [Valve formatting help](https://steamcommunity.com/comment/WorkshopItem/formattinghelp), [GFM paragraphs and lists](https://github.github.com/gfm/), [native serialization](https://github.com/syntax-tree/mdast-util-to-markdown), and [CSS whitespace processing](https://drafts.csswg.org/css-text-3/#white-space-processing).

## Native CLI composition (9 September 2026)

Node 24.20.0 supplies stable `util.parseArgs`, filesystem streams, descriptor writes and fatal UTF-8 decoding.
These supported interfaces cover the command's argument and I/O needs without another parser package. The remaining code selects the public converter, validates command-specific options, applies fidelity policy and emits its result. Native descriptor writes make closed-pipe errors observable at the CLI's I/O boundary and finish output before exit.

The existing default byte bound is enforced during input reads, before conversion; this is additional CLI acquisition protection, not a claim that native parser intermediate allocations are bounded.
The package manifest adds only its native npm binary and a source-checkout command.
TypeScript versions, runtime libraries, root licensing and the `.NET` toolchain are unchanged.

Sources: [Node util.parseArgs](https://nodejs.org/docs/latest-v24.x/api/util.html#utilparseargsconfig) and [Node filesystem APIs](https://nodejs.org/docs/latest-v24.x/api/fs.html).

## Runtime, mutation and supply-chain qualification (9 September 2026)

Use c8 12.0.0 (ISC) for native V8 coverage and Stryker core/TAP runner 10.0.0 (Apache-2.0) for executable JavaScript mutation.
Exact installed licence texts were inspected; native installations retain attribution, notices and patent terms. c8's licence SHA256 is `1a9c55b2961f5e3062e181fa55666c44d011fc3cc03ed6d4fa4321c51f2a5ec5`; both Stryker licence texts match `b40930bbcf80744c86c46a12bc9da056641d722716c378f5659b9e555ef833e1`.

The maintained TAP runner consumes the existing `node:test` suite.
A separate built-in command runner exercises the actual CLI child process.
The proposed native Node runner remains unmerged and was not adopted.
Stryker sandboxes omit unconsumed compiler configuration because its config rewriter uses a removed TypeScript API; primary TypeScript 7 checking remains unchanged.
Native Node ancestor resolution avoids unnecessary node_modules links.
No vendor patch, alternate compiler, alias, shim or checker suppression is introduced.
Windows host execution only accommodates native cleanup of the tool's own workers.

Runtime thresholds remain 98% statements/lines/functions and 95% branches; mutation profiles each require 90%.
Three modules containing only JSDoc and `export {}` have no executable behavior and are excluded from runtime coverage; their type/declaration contracts remain checked. Executable registry/diagnostic data remains measured.
See [testing](testing.md) for commands and limitations.

Native npm 12 CycloneDX SBOM output describes each actual lock graph without installation scripts or a second dependency resolver.
The initial quality graph has 299 components; its runtime-only view has 66, all declaring MIT or Apache-2.0.
The isolated type-coverage graph has 37.
Development graphs additionally declare ISC, BSD-3-Clause, BlueOak-1.0.0, 0BSD and CC-BY-4.0.
Inspected full texts include the BlueOak tools, tslib's 0BSD terms and caniuse-lite's CC-BY-4.0 data licence.
These counts describe that retained snapshot, before subsequent lint tooling.
Metadata is not licence-text clearance, a vulnerability scan or signature proof.
Recheck the exact final graph for a release and retain all native notices.

CI composes the existing converter matrix with native dependency review 5.0.0 and artifact upload 7.0.1.
Exact full-commit pins and action interfaces were checked against GitHub release metadata; both action packages carry MIT terms.
No custom security scanner, Actions wrapper or platform-protection change is introduced.
Remote results and independent security acceptance remain outstanding.

Sources: [c8](https://github.com/bcoe/c8), [Stryker TAP](https://stryker-mutator.io/docs/stryker-js/tap-runner/), [Stryker configuration](https://stryker-mutator.io/docs/stryker-js/configuration/), [npm SBOM](https://docs.npmjs.com/cli/v12/commands/npm-sbom/), [dependency review](https://github.com/actions/dependency-review-action), [artifact upload](https://github.com/actions/upload-artifact).

## Native JavaScript linting (9 September 2026)

The registry selects ESLint 10.10.0, @eslint/js 10.0.1 and globals 17.12.0 as current stable.
Their exact published archives and full MIT licence texts were inspected, retaining OpenJS and Sindre Sorhus notices.
Their Node requirements cover the accepted CI matrix.
These are development dependencies only.

Use native flat configuration and all maintained recommended JavaScript rules, with Node ESM globals and intentional object-rest exclusion through the supported `ignoreRestSiblings` setting.
TypeScript remains responsible for checked JSDoc and consumer declarations.
No TypeScript-aware ESLint parser or second compiler is needed.
The custom gap is package scope/configuration, not new lint rules.
Inline configuration is disallowed and warnings fail the command.

Sources: [ESLint configuration](https://eslint.org/docs/latest/use/configure/configuration-files) and [ESLint setup](https://eslint.org/docs/latest/use/getting-started).

## Final locked graph and workflow validation (9 September 2026)

The final parent SBOM contains 360 dependency components; the isolated type-coverage graph contains 37.
Every component has declared licence metadata.
The additional BSD-2-Clause lint/parser packages retain their full source and binary redistribution conditions and disclaimers.
Full installed texts were inspected for eslint-scope, espree, esrecurse, estraverse, esutils and uri-js; esrecurse carries its terms in the source header.
This inspection and the SBOM are evidence for the subsequent [licence compatibility assessment](licensing-review.md); they are not themselves compatibility decisions.

Stryker's typed-rest-client 2.3.1 pins qs 6.15.1, for which the native npm audit reported moderate advisories.
The latest supported client patch still selects that release.
A narrowly scoped native npm override selects unmodified qs 6.16.0 for that client only.
Its exact BSD-3-Clause licence was inspected.
No alias, shim, vendor patch, audit suppression or runtime dependency is introduced.
The refreshed audit reports zero advisories on this locked graph.
Reassess the override when the supported client itself selects a patched qs version.

Native actionlint 1.7.12 validates the changed GitHub Actions workflow.
The official Windows archive's SHA256 matches both release metadata and the published checksum list: `6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9`.
Its exact MIT licence was inspected.
The task-local executable requires no Go installation or project dependency.
Its clean result checks workflow syntax and Actions expressions; it does not execute the remote matrix or adopt branch rules.

Sources: [qs advisory](https://github.com/advisories/GHSA-4mjr-xmp4-gh2g), [native npm overrides](https://docs.npmjs.com/cli/v12/configuring-npm/package-json#overrides), and [actionlint release](https://github.com/rhysd/actionlint/releases/tag/v1.7.12).

## Review repairs and API documentation (9 September 2026)

Native MDAST serialization remains responsible for Markdown delimiters and escaping.
Target phrasing runs remove empty/idempotent presentation wrappers, combine equivalent adjacent marks and carry formatting across paragraphs before serialization.
The supported serializer `unsafe` option preserves deletion whitespace.
Exact MIT `mdast-util-gfm-strikethrough` 2.0.0 is now a direct type dependency so its native construct-name augmentation is available to checked JSDoc; the existing aggregate GFM extension still owns runtime composition.

The public `gfmFromMarkdown()` transforms run on a copied intended target tree to count additional renderer links.
This removes the mandatory parse of serialized output which the direct assessment measured at 14 seconds for an 80,019-byte opaque fixture.
Source tag boundaries are coalesced before native transformation, and installed/native-parser fixtures verify link diagnostics and formatting.
The five-second subprocess regression checks complete output at two opaque sizes and 40,000 adjacent bold nodes.
It does not establish a public CPU deadline or bound every native allocation.
The custom gap is target normalization and diagnostic accounting, not a new Markdown parser or URL recognizer.

TypeDoc 0.28.20 (Apache-2.0) with unmodified TypeScript 6.0.3 (Apache-2.0) passes the accepted imported-generic JSDoc fixture.
Max explicitly authorized this additional compiler role in the separate private `tooling/api-docs` package.
TypeScript 7.0.2 still owns primary checks, declaration generation and consumers.
The docs script's native API declarations require standard DOM WebAssembly types; only its separate primary TS 7 config includes that library.
Runtime checking does not acquire browser globals, and no library checks are suppressed.

Native `typedoc-plugin-markdown` 4.13.0 (MIT) renders ten package-local reference files through TypeDoc's supported output, theme and router extension points.
Exact full licence texts were inspected.
Native entry points include the public module and source-owned referenced contracts; the package exports remain closed.
Exported native unist contracts retain their point fields; TypeDoc's external filter is deliberately unused because it removes those public declarations.
Missing references and warnings fail generation.
Source hyperlinks are omitted with native `disableSources`, avoiding commit-dependent output and unpublished Git links.
The fixture asserts native generic identities and rendered descriptions; freshness compares unmodified generated Markdown bytes, with no output rewriting.

Rejected alternatives remain documented in the retained qualification evidence: documentation 14.0.3 (ISC, with bundled third-party notices) drops the imported return type despite a zero build exit, and its lint rejects the syntax; JSDoc 4.0.5 (Apache-2.0) rejects it outright.
The identical fixture passes TS 7.
`typedoc-plugin-missing-exports` 4.1.4 (MIT) patches TypeDoc prototypes, so it was removed from the qualification graph under the no-shims rule.
No rejected tool or monkey patch is installed in the adopted dependency graphs.

The separate docs SBOM records 23 dependencies and its native npm audit reports zero advisories.
Its declared licence set adds Python-2.0 through unmodified argparse, alongside MIT, Apache-2.0, BSD-2-Clause, BlueOak-1.0.0 and ISC.
The full argparse historical/PSF terms, entities redistribution terms and native syntax highlighter notices were inspected and remain with their installed packages.
The generated SourceSpan descriptions originate in MIT `@types/unist` 3.0.3; its complete Microsoft notice is also retained in the shipped [third-party notices](third-party-notices.md).
The subsequent [licence compatibility assessment](licensing-review.md) applies the inspected terms to the actual distribution boundary.

Sources: [TypeDoc JSDoc support](https://typedoc.org/documents/Doc_Comments.JSDoc_Support.html), [native entry points](https://typedoc.org/documents/Options.Input.html), [TypeDoc plugins](https://typedoc.org/documents/Plugins.html), [documentation.js](https://github.com/documentationjs/documentation), and [TypeScript JSDoc](https://www.typescriptlang.org/docs/handbook/jsdoc-supported-types.html).

## Authored documentation formatting (9 September 2026)

Select unmodified Snapper (`snapper-fmt` 0.11.0, MIT) for semantic line breaks.
The exact published wheel licence was inspected; the hash-locked wheel is installed only as a development tool in the checkout `.venv`, outside the npm distribution.
Its bundled CycloneDX inventory lists 268 Rust components, including permissive licences, Unicode-3.0 and MPL-2.0.
Preserve the native inventory and installed notices; the formatter is neither modified nor redistributed in this package.
The subsequent [licence compatibility assessment](licensing-review.md) records this separate, unmodified development-tool boundary.

Use its native Markdown parser and deterministic sentence segmentation, with unlimited source width and no clause, code, neural or Pandoc formatting options.
The package's explicit `.snapperrc.toml` prevents ancestor configuration from changing these settings.
No Prettier plugin, custom sentence parser or vendor patch is necessary.
The local Python script only selects authored package guides and invokes the installed CLI; existing repository tooling selects the interpreter.

Qualification covers 15 authored sentence/literal cases plus GFM tables, code comments, both hard-break forms, repeated formatting and native check failure without writes.
Package-root Markdown and authored `docs/**/*.md` are enforced.
Generated API references, conformance and support projections are excluded from this formatter.
Both local `check` and the configured CI matrix run the same gate.
This deterministic convention improves source diffs; it does not certify linguistic sentence boundaries or enforce a universal Markdown line-width rule.

The selected native formatter preserves an original document when its rendering safeguard rejects a reflow.
Its zero exit alone therefore cannot enforce this policy: the repository gate also consumes native JSON `would_reformat` and `fused`/`wrap` findings, retaining the complete report at `artifacts/prose/check.json`.
Line-length advice remains advisory.
The leading-dot `.NET` platform token is marked as inline code in the two affected guides so native sentence punctuation handling cannot split that identifier.
The unfixed plain-text reproduction remains an executable rejection test; no native guard or diagnostic is suppressed.

Prettier 3.9.6's `proseWrap: preserve` preserves existing breaks without enforcing them.
Source inspection of published `prettier-plugin-sentences-per-line` 0.2.4 and `eslint-plugin-sentences-per-line` 0.1.3 found gaps across inline Markdown nodes.
The native 15-case mdformat comparison found `mdformat-slw` 0.4.0 passed 14 but missed a wholly bold sentence boundary.
`mdformat-sembr` 0.2.0 also changed NBSP and failed native equivalence on a hard break.
Flowmark 0.8.0's width-zero mode disables sentence wrapping and collapses NBSP; its normal semantic mode combines short sentences.
Native `Intl.Segmenter` splits the `e.g. Node` fixture.
None of these rejected qualification tools enter the adopted dependency graphs.

Sources: [Snapper 0.11.0](https://github.com/TurtleTech-ehf/snapper/tree/v0.11.0), [PyPI distribution](https://pypi.org/project/snapper-fmt/0.11.0/), [Prettier prose wrapping](https://prettier.io/docs/options#prose-wrap), [semantic line breaks](https://sembr.org/), [mdformat-slw](https://github.com/KyleKing/mdformat-slw), and [Flowmark](https://github.com/jlevy/flowmark).

## Release tooling reuse (9 September 2026)

The owner explicitly selected the existing owlapi publication implementation for reuse.
The source is `Hadden-Industries/owlapi` commit `2ac41c94e6630ca47ce110a484ec9af3b0b1f335`, inspected in the clean local checkout.
Adapt its `scripts/release-artifacts.mjs`, `scripts/build-release-candidate.mjs` and `scripts/qualify-public-registry.mjs` within this package's matching AGPL-3.0-only boundary, retaining attribution.
Its native npm archive, checksum, registry-integrity and fresh-consumer code already supplies the required capability.
Use the same workflow design of separate qualification, publication and verification jobs with the exact same-run artifact ID.
The residual integration is this package's coordinate and existing API, CLI and declaration consumers.
The root workflow is a repository-native integration; root MIT/Klei licensing and the `.NET` toolchain remain unchanged.

Use the already qualified Node 24.20.0 and npm 12.0.2.
Native npm 12 pack JSON is the contract; no older npm output compatibility branch is needed.
New `actions/download-artifact` 8.0.1 is the current stable release, pinned to `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c`; its full GitHub MIT notice was inspected.
The other workflow actions and locked tooling reuse the exact versions already qualified by converter CI.
OIDC authorization and public registry signature/provenance validation remain native npm responsibilities.
No new runtime or development npm dependency is introduced.

Omit owlapi's immutable GitHub-release reconciliation, custom archive parsers, package-specific shards and requirement that `next` be the only distribution tag.
The requested tag must identify the release, while other valid release channels may coexist.
Retain one tested archive, production SBOM/audit, same-run artifact integrity, scoped publication credentials and a fresh installed public consumer.
The source checkout's current bootstrap workflow is implementation evidence, not proof of an operational trusted-publishing configuration in this repository.
The package remains private during qualification; public metadata, first-publication authority and the exact npm trusted-publisher binding are separate owner actions.
New npm bindings can restrict direct publishing, so activation must explicitly authorize the workflow's `npm publish` operation.

Sources: [owlapi source revision](https://github.com/Hadden-Industries/owlapi/tree/2ac41c94e6630ca47ce110a484ec9af3b0b1f335), [npm trusted publishing](https://docs.npmjs.com/trusted-publishers/), [npm provenance](https://docs.npmjs.com/generating-provenance-statements/), and [download-artifact 8.0.1](https://github.com/actions/download-artifact/releases/tag/v8.0.1).

## Legacy comparator dependency repair (9 September 2026)

Remote dependency review identified two vulnerabilities in the isolated legacy comparator's `form-data` 2.3.3.
Select the maintainer's unmodified `v2-backport` release 2.5.6 through native npm `overrides["request@2.88.2"]["form-data"]`.
Request 2.88.2's obsolete `~2.3.2` constraint otherwise prevents selecting that API-compatible maintained backport.
The exact package and added dependencies' full MIT notices were inspected before execution.
All 250 provider outputs, classifications, semantic trees and diagnostics are unchanged against the preserved immediately preceding comparison.
The refreshed report records the changed manifest and lockfile hashes.
The form-data-only qualification audit reported ten moderate affected-package entries and no high or critical entries; it did not report either `form-data` advisory.
Those remaining legacy risks retain their separately assessed fixed-corpus tooling boundary.
No provider source, advisory threshold or suppression changed.

Source: [form-data 2.5.6 backport](https://github.com/form-data/form-data/releases/tag/v2.5.6).

The subsequent dependency-review run identified [GHSA-6394-6h9h-cfjg](https://github.com/advisories/GHSA-6394-6h9h-cfjg) in `nwmatcher` 1.3.9.
The graph now selects unmodified 1.4.4, both the advisory's fixed version and the current latest npm release, beneath `jsdom-nogyp` 0.8.3.
The old parent's `~1.3.1` range requires a native parent-scoped npm override; the other jsdom consumer already allows this release.
The exact npm archive contains 12 entries, no declared dependencies or install hooks, and Diego Perini's full MIT grant and warranty disclaimer, inspected before adoption.
The [upstream security repair](https://github.com/dperini/nwmatcher/commit/9dcc2b039beeabd18327a5ebaa537625872e16f0) changes the selector regular expressions.
The provider API, fixed-corpus scope, primary package graph and existing form-data backport remain unchanged.
The refreshed native audit has nine moderate affected-package entries, no high or critical entries and no nwmatcher finding; the remaining legacy risks retain their assessed tooling boundary.
