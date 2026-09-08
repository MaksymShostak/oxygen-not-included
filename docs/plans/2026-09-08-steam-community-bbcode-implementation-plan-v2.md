# Implementation plan for a maintainable Steam Community BBCode ↔ GFM package

> **Document version:** 2.0 — 7 September 2026. This is the implementation-plan version, not an npm package release.  
> **Amendment:** JavaScript ES modules with checked JSDoc and generated TypeScript declarations replace TypeScript-first runtime authoring.  
> **Scope preservation:** This is the full original plan, amended in place—not a summary or replacement plan. All original sections, architectural decisions, semantic mappings, API contracts, coverage obligations, test/security/licensing requirements and research lineage are retained; the authoring/build changes and their consequences are explicitly recorded.  
> **Research baseline:** Original research claims remain dated 7 September 2026. Newly checked sources relate to this language/tooling amendment; unrelated package/specification claims have not been silently refreshed or rewritten. The revision bundle includes the exact recovered original report text, a portable v1, a unified diff, a reversible edit ledger and preservation checks.

## Executive recommendation

The recommended implementation is an **ESM-only Node.js package authored in JavaScript, with comprehensive JSDoc contracts, mandatory static type checking and generated TypeScript declarations, built around existing AST infrastructure**, with the Steam-specific work restricted to the part for which no established standard exists: defining Steam's dialect, interpreting Steam-specific BBCode semantics, and lowering those semantics into MDAST.

The package should provisionally be called **`steam-community-bbcode`**. That name is preferable to `steamify`, `steam-markup`, or `steam-workshop-bbcode` because it says exactly which language family is being handled without suggesting that the package is an official Valve product, and it leaves room for the documented differences between Workshop items, guides, discussions, reviews and other Steam Community surfaces. The **default dialect profile should be `workshop-item`**, because that is the immediate use case. I found no indexed npm package with the exact proposed name, but that must not be treated as proof that the name is available: npm notes that its search index can lag newly published packages, so the name should be verified directly with the registry immediately before reservation/publication. [^S01]

The primary architecture should be:

```text
Steam Community BBCode source
        │
        ▼
generic BBCode parser
preferably @bbob/parser
        │
        ▼
generic BBCode parse tree
        │
        ▼
Steam dialect normaliser
        │
        ▼
extended MDAST
        │
        ├───────────────► diagnostics / provenance / fidelity report
        │
        ▼
GFM lowering pass
        │
        ▼
standard GFM-compatible MDAST
        │
        ▼
mdast-util-to-markdown + mdast-util-gfm
        │
        ▼
GitHub-Flavoured Markdown
```

The secondary, explicitly incomplete route should be:

```text
GFM
 │
 ▼
micromark-extension-gfm
 + mdast-util-from-markdown
 + mdast-util-gfm
 │
 ▼
standard MDAST
 │
 ▼
supported-subset Steam renderer
 │
 ▼
Steam Community BBCode
 + explicit unsupported-feature diagnostics
```

This is substantially preferable to a regex converter. MDAST is a published syntax-tree specification implementing the unist model and already defines the Markdown concepts we need—headings, emphasis, strong text, links, images, lists, blockquotes, code, inline code, thematic breaks and HTML—with recognised extensions for GFM features such as deletion and tables. [^S02] The unified ecosystem already provides low-level utilities for parsing Markdown into MDAST and serialising MDAST back to Markdown, and its maintainers specifically recommend `mdast-util-to-markdown` when an application is manipulating syntax trees directly. [^S03] `mdast-util-gfm` supplies the GFM-specific parsing and serialisation extensions for autolinks, strikethrough, tables, task lists and related syntax. [^S04]

For the BBCode side, **BBob should be the first parser we attempt to reuse rather than writing a parser from scratch**. BBob is a JavaScript BBCode parser that produces an AST; importantly for Steam, it supports an allow-list of recognised tags and a `contextFreeTags` mode which deliberately stops parsing markup inside constructs such as `[code]`. That is almost exactly the lexical behaviour needed for Steam's `[code]` and `[noparse]` regions. [^S05] It is MIT-licensed and describes itself as a pure-JavaScript parser with no dependencies. [^S05]

There should nevertheless be a **hard parser-acceptance gate** before BBob becomes an architectural dependency. It must prove correct handling of Steam's unusual `[*]` list-item boundary syntax, opaque `[noparse]`, opaque `[code]`, attribute formats such as Steam's quote and preview tags, malformed/unclosed input, unknown tags and nesting. If a particular Steam construct needs a small adapter around BBob, write the adapter. If fundamental parse semantics cannot be made correct without effectively reimplementing BBob, reject it and choose another generic parser or parser toolkit. The project should not contort its language model merely to retain one dependency.

The first public stable release should make one strong promise:

> **Every Steam Community BBCode construct in the project's versioned provenance registry is recognised and has a tested, documented conversion policy. No known source construct may disappear, accidentally become Markdown syntax, remain as unexplained raw BBCode, or suffer semantic loss without a machine-readable diagnostic.**

That is deliberately not the same as claiming that every Steam semantic can be expressed perfectly in GFM. GFM simply has no native equivalent for some Steam constructs. “Complete BBCode coverage” should mean **complete recognition and accounted-for conversion**, with fidelity classified per construct; it must never mean inventing a false one-to-one correspondence.

This is particularly important in light of the Steamify evaluation. Steamify 2.0.1 happened to pass all 16 checks against the current relatively simple Workshop description, but only **6 of 17 additional formatting probes** passed; the failures included nested ordered lists, bare URL tags, inline lists, multiline bold, underline, spoilers, tables, `noparse`, literal Markdown emphasis characters, and inline/block code containing backticks. The important conclusion is that unmodified Steamify 2.0.1 is unsuitable for an **unrestricted formatting-preservation contract**, not that every use of Steamify fails. [^H01] That precise failure corpus should become a permanent regression suite for the new package.

The release should therefore be able to demonstrate, rather than merely advertise, that it is better:

| Criterion | Stable-release requirement |
|---|---|
| Steam source syntax coverage | Every construct in the versioned Steam registry has fixtures, AST expectations and conversion tests |
| Silent semantic loss | **Zero** known cases |
| Steamify regression corpus | All 17 additional cases correctly preserved where GFM/GitHub can represent the semantic, otherwise intentionally lowered with an explicit fidelity diagnostic |
| Existing real description | All 16 existing checks continue to pass |
| Regex substitution as parser | None |
| Structured intermediate representation | Extended MDAST |
| Source lineage | Machine-readable provenance on every construct |
| GFM generation | Established MDAST/GFM serialisers rather than hand-written Markdown escaping |
| Unknown Steam syntax | Preserved and diagnosed, never silently discarded |
| GFM → Steam claim | Explicitly partial; no “full GFM support” marketing |
| Security | Static analysis, dependency review, fuzz/property testing, resource limits and release provenance |
| Release provenance | npm Trusted Publishing/OIDC with provenance |
| Licence | `AGPL-3.0-only` |

That is the foundation from which it could credibly become the de-facto package. The repository should **not use “best”, “complete”, “de-facto” or “100% compatible” as marketing claims until the generated conformance evidence supports the relevant statement**.

## Specification lineage and the coverage model

The most important discovery is that there is **no single sufficiently complete formal Steam BBCode specification**. Valve's Workshop-specific formatting-help page is authoritative, but incomplete as a language specification.

The actual Valve Workshop-item help page currently documents `[h1]`, `[h2]`, `[h3]`, `[b]`, `[u]`, `[i]`, `[strike]`, `[spoiler]`, `[noparse]`, `[hr]`, `[url]`, plain-URL widget behaviour, `[list]`, `[olist]`, `[*]`, `[quote=author]` and `[code]`. It explicitly demonstrates that `[noparse]` prevents inner tags from being interpreted. [^S06] Valve's formatting help for some other Community contexts additionally documents tables, including `[table]`, `[tr]`, `[th]`, `[td]`, `noborder=1` and `equalcells=1`. [^S07]

The long-running community-maintained *Comprehensive Formatting Help* guide is therefore necessary secondary evidence. It explicitly exists to catalogue all Steam markup tags, parameters, context differences and parsing problems, and observes that Valve's own help is inaccurate/inconsistent across Steam surfaces. [^S08] Particularly relevant to this project, its current UGC-description/summary matrix records a broader set than Valve's Workshop help page: headings, bold, underline, italic, strike, spoiler, `noparse`, rule, URL, `previewyoutube`, list, ordered list, quote, code, table, image and video, alongside renderer behaviours such as bracket expansion and word filtering. Guide sections expose yet further constructs such as `previewimg`, `previewicon` and `screenshot`. [^S08]

The same guide records undocumented or unusual forms including `[pullquote]`, `[p]`, `[img]`, `[video ...]`, `[previewimg=...]`, `[screenshot=...]` and `[previewyoutube=...]`. [^S08] It also contains recent community reports that `[video]` may no longer work reliably, illustrating why “known syntax” and “currently rendered by Steam” must be separate concepts. [^S08]

This leads to a better specification design than simply hard-coding a tag array.

The repository should contain a **version-controlled, machine-readable dialect registry**, for example:

```text
spec/
  sources.json
  constructs.json
  profiles.json
  schema/
    sources.schema.json
    constructs.schema.json
    profiles.schema.json
```

`constructs.json` should express facts such as:

```json
{
  "id": "steam.bbcode.spoiler",
  "syntaxKind": "pairedTag",
  "tagName": "spoiler",
  "contentModel": "phrasing",
  "provenance": [
    {
      "sourceId": "valve.workshop-item-formatting-help",
      "status": "documented"
    },
    {
      "sourceId": "community.comprehensive-formatting-help",
      "status": "observed"
    }
  ],
  "profiles": {
    "workshop-item": "documented",
    "ugc-description": "observed",
    "guide-section": "observed"
  },
  "gfmMapping": {
    "fidelity": "target-specific",
    "strategy": "github-collapsible-section"
  }
}
```

The precise names matter. I would use the following vocabulary:

| Name | Meaning |
|---|---|
| `SteamDialectProfile` | A particular Steam rendering/input context |
| `SteamConstructDefinition` | One syntax or renderer construct with provenance and conversion policy |
| `SteamConstructStatus` | `documented`, `communityObserved`, `historical`, `disputed`, `unsupported` |
| `SteamSyntaxKind` | `pairedTag`, `boundaryTag`, `opaqueTag`, `urlEmbed`, `rendererTransformation` |
| `SteamContentModel` | `phrasing`, `flow`, `listItems`, `tableRows`, `tableCells`, `opaqueText` |
| `ConversionFidelity` | `exact`, `equivalent`, `approximate`, `lossy`, `unsupported` |
| `ConversionDiagnostic` | Structured information explaining anything non-exact |
| `ConversionResult<T>` | Converted value plus diagnostics and coverage information |
| `SourceSpan` | Source start/end information where the parser can provide it |
| `ConstructId` | Stable semantic identifier such as `steam.bbcode.noparse` |

There should be **no catch-all type called `Tag`, `Element`, `Thing`, `NodeData`, `ConvertOptions` or `Converter` where a more exact name exists**.

The profile registry might initially contain:

```js
/**
 * @typedef {
 *   'workshop-item' |
 *   'ugc-description' |
 *   'guide-section' |
 *   'discussion' |
 *   'review' |
 *   'announcement'
 * } SteamDialectProfileId
 */
export {}
```

The primary support commitment for the first stable release should be:

```text
all known Steam Community syntax
        │
        ├── recognised by the parser
        ├── provenance known
        ├── represented or classified
        └── conversion policy tested

workshop-item / UGC description
        │
        └── highest-fidelity supported source profile
```

This distinction prevents a future contributor from seeing, for example, `[previewimg]` and mistakenly declaring it unsupported merely because it does not occur in Workshop-item help. Steam's syntax genuinely differs by surface. The community guide's support matrix provides good evidence of precisely that phenomenon. [^S08]

It is also important not to misclassify Steam renderer behaviours as BBCode. Plain YouTube, Steam Store and Workshop URLs are documented by Valve as being automatically turned into widgets, even though they are not bracketed tags. [^S06] Emoticon expansion, bracket expansion and word filtering are likewise renderer behaviours in the community catalogue rather than ordinary paired-tag syntax. [^S08] They therefore belong in the same provenance registry but with:

```json
{
  "syntaxKind": "rendererTransformation"
}
```

rather than pretending that they are AST tag nodes.

The generated documentation should then distinguish four concepts that other converters frequently blur:

**Parsing coverage** asks whether source syntax is recognised.

**Steam-profile support** asks where Steam is believed to recognise that syntax.

**Target representability** asks whether GFM/GitHub has an equivalent semantic.

**Conversion fidelity** asks how closely the chosen target representation preserves it.

That model lets the project honestly say “100% registry coverage” without falsely saying “100% identical rendering”.

The registry itself should be validated against JSON Schema during CI, and a generator should produce `docs/steam-support-matrix.md` and machine-readable `coverage.json` from it. Documentation must not contain a separately maintained tag list; otherwise the documentation and implementation will eventually diverge.

## Package architecture and API design

The implementation should be deliberately layered so that **the generic parser, Steam semantics, MDAST transformation and GFM rendering cannot become entangled**.

A recommended source tree is:

```text
src/
  index.js

  steam/
    dialect-profile.js
    construct-definition.js
    construct-registry.js
    parse-steam-bbcode.js
    normalise-steam-parse-tree.js
    validate-steam-structure.js

  mdast/
    steam-mdast-nodes.js
    steam-bbcode-to-mdast.js
    lower-steam-mdast-to-gfm.js

  gfm/
    serialise-gfm.js
    parse-gfm.js
    gfm-to-steam-bbcode.js

  diagnostics/
    conversion-diagnostic.js
    diagnostic-code.js
    conversion-result.js

  security/
    resource-limits.js
    safe-url.js

  cli/
    main.js

spec/
test/
docs/
scripts/

types/                     # generated .d.ts and .d.ts.map; not authored
tsconfig.base.json
tsconfig.json              # check authored JavaScript; no emit
tsconfig.declarations.json # emit declarations only
```

The crucial rule is: **do not invent a parallel generic document AST**.

MDAST already represents the overwhelming majority of the semantic document structure and is expressly designed to be extended. [^S02] The package should therefore use normal MDAST nodes wherever semantics line up:

```text
[h1]       -> Heading(depth=1)
[h2]       -> Heading(depth=2)
[h3]       -> Heading(depth=3)
[b]        -> Strong
[i]        -> Emphasis
[strike]   -> Delete
[url]      -> Link
[img]      -> Image
[list]     -> List(ordered=false)
[olist]    -> List(ordered=true)
[*]        -> ListItem
[quote]    -> Blockquote, where no metadata exists
[code]     -> Code
[hr]       -> ThematicBreak
[table]    -> Table
[tr]       -> TableRow
[th]/[td]  -> TableCell
[p]        -> Paragraph
```

MDAST itself defines the standard document nodes, while its GFM extensions include `Delete`, `Table`, `TableRow` and `TableCell`. [^S02]

Only genuinely Steam-specific semantics should produce extension nodes. For example:

```js
/**
 * @typedef {import('unist').Parent} Parent
 * @typedef {import('unist').Literal} Literal
 * @typedef {import('mdast').PhrasingContent} PhrasingContent
 * @typedef {import('mdast').BlockContent} BlockContent
 */

/**
 * @typedef {Parent & {
 *   type: 'steamUnderline'
 *   children: PhrasingContent[]
 * }} SteamUnderline
 */

/**
 * @typedef {Parent & {
 *   type: 'steamSpoiler'
 *   children: PhrasingContent[]
 * }} SteamSpoiler
 */

/**
 * @typedef {Literal & {
 *   type: 'steamNoParse'
 *   value: string
 * }} SteamNoParse
 */

/**
 * @typedef {Parent & {
 *   type: 'steamAttributedBlockquote'
 *   author?: string
 *   steamCommentId?: string
 *   children: BlockContent[]
 * }} SteamAttributedBlockquote
 */

/**
 * @typedef {Parent & {
 *   type: 'steamEmbeddedMedia'
 *   mediaKind: 'youtube' | 'video' | 'steamStore' | 'steamUgc' | 'other'
 *   source: string
 *   children: PhrasingContent[]
 * }} SteamEmbeddedMedia
 */

/**
 * @typedef {Parent & {
 *   type: 'steamPreviewImage'
 *   source: string
 *   alt?: string
 *   size?: 'thumb' | 'full' | 'original'
 *   alignment?: 'left' | 'right' | 'inline'
 *   children: PhrasingContent[]
 * }} SteamPreviewImage
 */

/**
 * @typedef {Parent & {
 *   type: 'steamPullQuote'
 *   children: BlockContent[]
 * }} SteamPullQuote
 */

export {}
```

That is an **extended MDAST**, not a competing AST.

I would avoid names such as `SteamAst` because they obscure which abstraction is involved. There are really two different trees:

```text
BBob parse tree
    = syntactic representation of source BBCode

Extended MDAST
    = semantic document representation
```

Calling both “Steam AST” would invite architectural confusion.

### Parser boundary

`@bbob/parser` should be isolated behind exactly one module:

```js
parseSteamBbcodeSyntax(source, options)
```

Nothing outside that adapter should import BBob types.

That gives us the ability to replace BBob later without changing the package's public API. BBob is attractive because it already supports AST parsing, tag allow-lists, escaped tags and context-free parsing. [^S05] In particular:

```js
contextFreeTags: ['noparse', 'code']
```

is conceptually the correct starting point for Steam because Valve explicitly defines `[noparse]` as suppressing tag parsing, while `[code]` preserves fixed-width text and spacing. [^S06]

Before adoption, an architectural test suite must establish how BBob treats:

```text
[*]
[quote=author]
[quote=author;commentId]
[table noborder=1 equalcells=1]
[video mp4=... poster=...]
[previewimg=420;sizeFull,floatLeft;example.png]
[code][b]literal[/b][/code]
[noparse][b]literal[/b][/noparse]
```

If `[*]` requires a small Steam normalisation pass, that is acceptable. Writing a complete recursive BBCode parser solely because one boundary token is unusual would not be.

### Runtime dependency policy

The runtime should remain deliberately small:

```json
{
  "dependencies": {
    "@bbob/parser": "...",
    "@types/mdast": "...",
    "@types/unist": "...",
    "mdast-util-from-markdown": "...",
    "mdast-util-gfm": "...",
    "mdast-util-to-markdown": "...",
    "micromark-extension-gfm": "..."
  }
}
```

The exact compatible versions should be pinned through `package-lock.json` at implementation time rather than copied from this research report.

There is no need for `remark`, `unified`, an HTML parser, an HTML-to-Markdown converter or a templating engine in the primary conversion path. `mdast-util-to-markdown` is specifically the lower-level serializer intended for applications manipulating trees directly, and `mdast-util-gfm` extends it for GFM. [^S04] The reverse parser can use `mdast-util-from-markdown` with `micromark-extension-gfm` and the corresponding GFM MDAST extension. [^S04]

This also means **escaping Markdown must not be implemented manually**. The Steamify result in which literal `*literal*` accidentally became emphasis is exactly the kind of defect a proper Markdown AST serializer is intended to avoid. [^H01]

The package should be ESM-only. The current MDAST GFM utilities are themselves ESM-oriented, and trying to create a dual CJS/ESM distribution would add complexity without helping the chosen dependency graph. [^S04]

As of 7 September 2026, Node 22 and 24 are LTS lines, Node 26 is Current, and Node 20 has reached EOL. Node's own release guidance recommends production use of Active or Maintenance LTS versions. [^S09] I therefore recommend:

```json
{
  "type": "module",
  "engines": {
    "node": ">=22"
  }
}
```

with CI on Node 22, Node 24 and Node 26. That avoids beginning a new package with an already-EOL runtime while also testing the current release line. [^S09]

### JavaScript authoring, checked contracts and declaration generation — v2 amendment

**Decision:** author the library, CLI, runtime tests and maintenance scripts in ordinary JavaScript ES modules. Use comprehensive JSDoc annotations and TypeScript's JavaScript checker as mandatory development tooling. Generate the public `.d.ts` files from the checked JavaScript. Do not hand-maintain a parallel TypeScript implementation or a duplicate public declaration model.

This replaces the original **TypeScript-first authoring choice**, not the requirement for mechanically checked contracts. The BBob acceptance gate, extended-MDAST architecture, construct registry, conversion semantics, package name, primary/partial direction distinction and all original assurance requirements remain in force.

#### Rationale and limits of the decision

TypeScript officially supports checking JavaScript through `allowJs`/`checkJs` and supports declaration-only generation from JavaScript and JSDoc. These capabilities let this package keep executable JavaScript source while supplying checked contracts and declarations to consumers.[^V2-01][^V2-02]

There is a directly relevant ecosystem precedent: `mdast-util-to-markdown` uses JavaScript with JSDoc type imports and runs TypeScript/type-coverage checks; `unified` also implements its processor in annotated JavaScript. This is evidence of a suitable established approach, not a claim that every modern Node package must use it.[^V2-03][^V2-04][^V2-05][^V2-06]

TypeScript source can express complicated type relationships more concisely. Checked JavaScript also has different inference rules, including more permissive treatment of some unannotated objects and parameters. Consequently, merely enabling `checkJs` is insufficient: annotate module boundaries, AST construction points, options, diagnostics and parser state deliberately, and test that invalid uses are rejected.[^V2-07]

**Static exhaustiveness is not specification completeness.** A type model can omit a Steam construct and still type-check perfectly. The provenance review, independently defined fixtures, renderer assertions, security testing and comparative conformance evidence elsewhere in this plan remain separate release obligations. Likewise, a checked URL string is not a validated URL, and a well-typed AST is not proof that a transformation preserved meaning. Do not count a type-check pass as a security or conformance-test pass.

#### Authored files and type ownership

All executable project source is `.js` with explicit relative `.js` import specifiers. `typescript`, linting tools, declaration-test tooling and internal-only platform type packages are development dependencies. There is no runtime TypeScript compiler, loader, transpiler, or consumer build step in the published npm package.

Reuse `mdast` and `unist` definitions rather than recreating them. JSDoc may use imported types, discriminated unions, generics and `@satisfies`; the TypeScript-supported JSDoc subset is the contract syntax for this project.[^V2-08] For example:

```js
/**
 * Create a literal mdast text node without interpreting Markdown punctuation.
 *
 * The serializer, not this constructor, owns context-sensitive escaping.
 * Do not turn source asterisks or backticks into formatting at this boundary.
 *
 * @param {string} value
 *   Literal text obtained from the Steam syntax/semantic layer.
 * @returns {import('mdast').Text}
 *   The equivalent literal text node.
 */
export function createTextNode(value) {
  return {type: 'text', value}
}
```

Keep each project-owned contract beside the module that owns it, using `@typedef`, `@property`, `@template`, `@param` and `@returns` as appropriate. Preserve the semantic names and all fields illustrated elsewhere in this plan. A generated declaration may expose a type alias rather than the original illustrative `interface`; that authoring distinction must not change the permitted node shapes, required properties, optionality, readonly contracts or public function signatures.

The registry remains the source of construct facts. Do not introduce a second handwritten construct-ID or profile-ID catalogue merely to obtain types. Where literal unions or handler-key declarations need generation, derive those narrow artefacts from the same validated registry with an established schema/type-generation tool where suitable. Do not build a new general-purpose schema compiler for this amendment. Test registry-to-handler completeness independently of the generated types. The original profile-union example is a projection of the registry, not a second source of truth.

**Declaration dependencies need special care.** When public generated declarations import `mdast` or `unist` types, declare the corresponding type packages, such as `@types/mdast` and `@types/unist`, in `dependencies`, not only in `devDependencies`. They provide declaration files, not a runtime type-checker. Do not rely on accidental transitive hoisting. The TypeScript publishing guidance explicitly requires consumers to receive declaration dependencies; `mdast-util-to-markdown` follows that pattern.[^V2-09][^V2-04] Internal-only `@types/node` can remain a development dependency when it is not referenced by the public declaration graph.

The parser adapter must remain the only module aware of BBob's vendor-specific types. Do not leak them through generated declarations. Extend the existing mdast/unist model narrowly; do not replace it with an untyped generic tree, cast every extension to a standard node, or add an unrestricted `any`/index-signature escape hatch to make checks pass. Validate nested extension-node contracts and their lowering to standard GFM MDAST with type-level and runtime fixtures.

#### Checker and declaration-build configuration

Use separate checking and declaration-emission configurations. The following are proposed project configurations, not evidence that the converter has already been implemented. Pin and test the compiler and type-definition versions during the initial tooling gate.

`tsconfig.base.json`:

```json
{
  "compilerOptions": {
    "allowJs": true,
    "checkJs": true,
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "noPropertyAccessFromIndexSignature": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true,
    "skipLibCheck": false,
    "target": "ES2022",
    "lib": ["ES2022"],
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "types": ["node"]
  }
}
```

`tsconfig.json` — checks authored JavaScript without emitting JavaScript or declarations:

```json
{
  "extends": "./tsconfig.base.json",
  "compilerOptions": {
    "noEmit": true
  },
  "include": [
    "src/**/*.js",
    "scripts/**/*.js",
    "test/**/*.js"
  ],
  "exclude": ["node_modules", "types", "coverage"]
}
```

`tsconfig.declarations.json` — emits declarations and declaration maps from library/CLI source only:

```json
{
  "extends": "./tsconfig.base.json",
  "compilerOptions": {
    "noEmit": false,
    "declaration": true,
    "declarationMap": true,
    "emitDeclarationOnly": true,
    "noEmitOnError": true,
    "rootDir": "src",
    "outDir": "types"
  },
  "include": ["src/**/*.js"],
  "exclude": ["node_modules", "test", "scripts", "coverage"]
}
```

Use an appropriate Node type-definition version for the oldest supported runtime when checking executable source, then run the unchanged Node runtime matrix. Maintain a separate consumer-fixture configuration for `.mts`/`.ts` interoperability tests; those files test TypeScript consumers and are not executable package implementation. Neither declaration checking nor the compiler's syntax target replaces tests on the supported Node versions.

Document the effective compiler configuration and exact tool versions in CI artefacts. Do not weaken project-wide checking to accommodate a poorly typed dependency: isolate the affected boundary, validate it at runtime where required, and record any narrowly scoped exception with a regression case and a removal condition. An unexplained `@ts-ignore`, blanket `@ts-nocheck`, or broad `any` cast is not an acceptable implementation of a contract. Negative tests may use justified `@ts-expect-error` assertions; CI must also detect when the expected error disappears.

#### JavaScript distribution and declaration-aware npm exports

Publish the authored JavaScript directly, retaining its comments. A JavaScript-to-JavaScript build, bundler and minifier are not required for this package. `npm run build` now means generation/verification of declarations and other declared generated artefacts, not transpilation of executable source.

A packaging projection is:

```json
{
  "name": "steam-community-bbcode",
  "type": "module",
  "license": "AGPL-3.0-only",
  "engines": {
    "node": ">=22"
  },
  "main": "./src/index.js",
  "types": "./types/index.d.ts",
  "exports": {
    ".": {
      "types": "./types/index.d.ts",
      "import": "./src/index.js"
    }
  },
  "bin": {
    "steam-community-bbcode": "./src/cli/main.js"
  },
  "files": [
    "src/",
    "types/",
    "spec/",
    "README.md",
    "LICENSE",
    "SECURITY.md",
    "CHANGELOG.md"
  ],
  "scripts": {
    "typecheck": "tsc --project tsconfig.json",
    "clean:types": "node scripts/clean-generated-types.js",
    "build:types": "tsc --project tsconfig.declarations.json",
    "build": "npm run clean:types && npm run build:types"
  }
}
```

This is a focused metadata example, not a complete ready-to-publish manifest: retain the original dependency graph, source-registry/documentation generation, test scripts, repository metadata and release controls elsewhere in the plan. Add declaration dependencies as described above. Resolve the provisional name and compatible package versions at implementation/release time.

The cleanup script must use Node filesystem APIs, operate only on the known generated declaration directory, and be cross-platform. The CLI must have a valid Node shebang and be exercised through the installed npm binary on the supported operating systems. Exports should expose only the supported public API; new subpath exports need their own declaration and compatibility tests.

The original source-map requirement is fulfilled by **shipped JavaScript source plus declaration maps pointing to that shipped source**. No `.js.map` is needed for JavaScript that has not been transformed. If a future approved build transforms JavaScript, require the corresponding executable source maps as well. Verify every shipped declaration-map source path against the actual tarball. This preserves inspectability and source navigation rather than adding meaningless maps to unchanged code.

Generate declarations in the publisher's controlled build. Do not add install-time compilation, `ts-node`, `tsx`, or a TypeScript runtime dependency. Test the packed tarball rather than relying on source-tree imports or symlinked workspace dependencies. Publish the exact tested artefact under the existing trusted-publishing/provenance controls.

#### Type-contract TDD and acceptance gates

The type contract is part of TDD, not a documentation step after implementation. Before implementing an exported function or AST variant, add a positive use case and the applicable negative-use cases. Then add the smallest checked JavaScript implementation that makes those contract tests and the corresponding runtime/conformance tests pass.

| Gate | Required evidence |
|---|---|
| Public contract coverage | Every export, input option, result, diagnostic and public AST extension has a documented checked contract. |
| Invalid-use rejection | Fixtures reject wrong argument types, misspelled option/profile identifiers, malformed AST shapes, invalid discriminants and forbidden readonly mutations. |
| Exhaustive handling | Closed project-owned unions have explicit exhaustiveness checks or a checked complete handler map; adding a variant makes an incomplete handler fail. |
| Runtime boundaries | JavaScript callers, malformed source and external data still receive the original runtime validation and diagnostics. Types are not treated as input validation. |
| Declaration generation | A clean build emits `.d.ts` and `.d.ts.map` from the JavaScript with no handwritten duplicate API, stale files, unresolved imports or leaked BBob types. |
| JavaScript consumer | A fresh npm installation imports and runs the API/CLI without a compiler, loader, project source checkout or consumer build. |
| TypeScript consumer | A fresh fixture resolves the installed package's exports and declarations under the documented supported compiler configurations; positive and negative API tests pass. |
| Dependency placement | Consumer tests do not depend on this repository's development dependencies or accidental hoisting. |
| Repeatability | Two clean declaration builds using the same inputs/toolchain produce identical declaration artefacts; review declaration diffs as API changes. |
| Original quality gates | All existing conformance, regression, property, mutation, security, licensing, provenance and documentation gates still pass. |

Extend the test layout rather than replacing the existing suite:

```text
test/
  type-contracts/             # checked-JavaScript positive/negative fixtures
  package-consumers/
    javascript/              # installed tarball, no compiler required at runtime
    typescript/              # generated declarations and API compatibility
scripts/
  clean-generated-types.js
  verify-generated-types.js
  test-package-consumers.js
docs/
  javascript-and-types.md
  decisions/
    0001-javascript-with-checked-jsdoc.md
```

Select a maintained declaration-test/type-coverage tool during the tooling gate rather than building one. `tsd` or another appropriate existing checker harness can support generated declaration tests. Type coverage must have an explicitly reviewed scope and exception policy; a coverage percentage must not be confused with dialect coverage. These additions complement, and do not lower, the numerical runtime coverage and mutation gates already specified.

Add `npm run test:types`, `npm run types:check`, and `npm run test:package` to the release checklist. Run `test:types` against generated declarations, `types:check` against clean/repeatable generation, and `test:package` against the real packed artefact. Capture their actual supported compiler/runtime versions in the results. Do not claim the plan amendment itself has passed the future converter's tests.

#### Documentation and maintenance consequences

`CONTRIBUTING.md` and `docs/javascript-and-types.md` must explain why a JavaScript repository contains `tsconfig` files, how its JSDoc contracts are checked, how declarations are generated, where generated files live, and why contributors must not edit them manually. JavaScript examples are the default in README, API and CLI documentation. Label TypeScript declaration projections and consumer-only examples explicitly.

Generate API documentation from the same JSDoc contracts with tooling that supports the project's imported and generic types; prove that support with a documentation fixture before adopting the tool. Preserve all original comment obligations about parser recovery, opaque regions, escaping, fidelity policy, source lineage and trust boundaries. The authoring change is from TSDoc on `.ts` implementation to checked JSDoc on `.js` implementation, not from rich documentation to terse or optional comments.

The decision record should capture alternatives, the ecosystem precedent, type-checking limitations, dependency placement, public API compatibility and the absence of consumer compilation. Reconsider TypeScript source only through an explicit later ADR supported by maintenance evidence; do not silently introduce a mixed runtime implementation or a second type model.

### Public API

The public functions should say precisely what they do. The following is the **generated `.d.ts` API projection**, retained to state the signatures precisely; implement the functions in `.js` modules with equivalent JSDoc `@param` and `@returns` contracts. Do not maintain this declaration projection by hand as a second API source:

```typescript
export function parseSteamCommunityBbcode(
  source: string,
  options?: ParseSteamCommunityBbcodeOptions
): SteamBbcodeParseResult

export function steamCommunityBbcodeToMdast(
  source: string | SteamBbcodeParseResult,
  options?: SteamBbcodeToMdastOptions
): ConversionResult<SteamMdastRoot>

export function steamCommunityBbcodeToGfm(
  source: string,
  options?: SteamBbcodeToGfmOptions
): ConversionResult<string>

export function gfmToSteamCommunityBbcode(
  source: string,
  options?: GfmToSteamBbcodeOptions
): PartialConversionResult<string>
```

I prefer these admittedly long names to ambiguous APIs such as:

```js
parse()
convert()
toMarkdown()
fromMarkdown()
```

For a package whose whole value proposition is dialect precision, ambiguity in the public vocabulary would be self-defeating.

`ConversionResult<T>` should look approximately like:

```js
/**
 * @template T
 * @typedef {{
 *   value: T
 *   diagnostics: readonly ConversionDiagnostic[]
 *   coverage: ConversionCoverage
 * }} ConversionResult
 */

/**
 * @typedef {{
 *   code: DiagnosticCode
 *   severity: 'info' | 'warning' | 'error'
 *   constructId: ConstructId
 *   fidelity: ConversionFidelity
 *   message: string
 *   sourceSpan?: SourceSpan
 * }} ConversionDiagnostic
 */

export {}
```

That gives build pipelines something better than console warnings.

For example:

```json
{
  "code": "STEAM_VIDEO_EMBED_LOWERED_TO_LINK",
  "severity": "warning",
  "constructId": "steam.bbcode.video",
  "fidelity": "lossy",
  "message": "GitHub Markdown cannot preserve Steam video-widget behaviour; emitted a link."
}
```

The CLI should expose that same model:

```text
steam-community-bbcode to-gfm STEAM_DESCRIPTION.bbcode
steam-community-bbcode to-gfm --fail-on=lossy STEAM_DESCRIPTION.bbcode
steam-community-bbcode to-steam README.md
steam-community-bbcode coverage --format=json
```

`to-steam` must be labelled **partial** in `--help`, the README and API documentation. It must never silently ignore unsupported GFM nodes.

## Conversion semantics and complete source-side coverage

The semantic layer is where the new package should decisively outperform Steamify.

Steamify's failure pattern strongly suggests that direct textual rewriting is the wrong abstraction: nested ordered lists lost hierarchy, literal asterisks became Markdown emphasis, `noparse` contents were still converted, tables and several tags fell through unchanged, and backticks inside code confused the generated Markdown delimiters. [^H01] An AST pipeline addresses each class of error structurally rather than with more regular expressions.

The source registry should assign every construct a **specific target strategy and fidelity classification**.

| Steam source | Intermediate representation | GFM/GitHub strategy | Expected fidelity |
|---|---|---|---|
| `h1`, `h2`, `h3` | MDAST `Heading` | `#`, `##`, `###` | Exact |
| `b` | `Strong` | strong emphasis | Exact |
| `i` | `Emphasis` | emphasis | Exact |
| `strike` | GFM `Delete` | `~~…~~` | Exact |
| `u` | `SteamUnderline` | verified GitHub HTML representation or diagnosed plain-text fallback | Target-dependent |
| `spoiler` | `SteamSpoiler` | GitHub collapsible section where structurally valid | Equivalent/approximate |
| `noparse` | `SteamNoParse` | literal MDAST text, safely escaped by serializer | Exact visible text |
| `hr` | `ThematicBreak` | thematic break | Exact |
| attributed `url` | `Link` | Markdown link | Exact |
| bare `[url]…[/url]` | `Link` | Markdown link/autolink | Exact |
| `list` | `List(false)` | unordered list | Exact |
| `olist` | `List(true)` | ordered list | Exact |
| `[*]` | `ListItem` | list item | Exact |
| nested lists | nested MDAST lists | nested GFM lists | Exact |
| `quote` | `Blockquote` | blockquote | Exact |
| attributed quote | `SteamAttributedBlockquote` | attribution + blockquote | Equivalent; comment metadata may be lost |
| `code` | `Code`/protected literal | serializer-selected safe fence | Exact content |
| `table` | GFM `Table` | GFM table when representable | Exact/equivalent |
| table layout attributes | metadata | omit with diagnostic where GFM has no equivalent | Lossy styling |
| `img` | `Image` | GFM image | Equivalent |
| `previewimg` / `screenshot` | `SteamPreviewImage` | image; record lost Steam sizing/alignment | Approximate |
| `previewyoutube` | `SteamEmbeddedMedia` | link or documented GitHub-compatible representation | Approximate |
| `video` | `SteamEmbeddedMedia` | link/fallback | Lossy |
| `p` | `Paragraph` | paragraph | Exact |
| `pullquote` | `SteamPullQuote` | blockquote-style lowering | Approximate |
| obsolete `color` | historical extension | preserve text, diagnostic | Lossy |
| unknown BBCode | unknown source construct | preserve literal source + diagnostic | No silent loss |

GitHub officially documents `<details>`/`<summary>` as a supported way to produce collapsible Markdown sections, including Markdown content inside the details block. [^S10] That makes it a reasonable GitHub-targeted spoiler representation for block-level spoilers. It is not semantically identical to every possible inline Steam spoiler, so an inline spoiler that must change flow should receive `fidelity: "approximate"` rather than being falsely labelled exact.

Underline should be treated similarly: do **not** assume an HTML tag is acceptable merely because another converter uses it. The implementation should put candidate `<u>`/`<ins>` representations through the GitHub-rendering conformance suite before declaring them supported. The BUTR Markdown-to-Steam project itself recognises that ordinary Markdown has no underline equivalent and uses HTML as the possible bridge in the opposite direction, reinforcing that this is a target-extension issue rather than a native Markdown construct. [^S11]

Tables illustrate why MDAST is so valuable. GFM has a recognised table AST with `Table`, `TableRow` and `TableCell` and existing parsing/serialisation utilities. [^S12] There is therefore no justification for Steam tables to remain raw BBCode, as happened in the Steamify probe. [^H01] Basic rectangular Steam tables should become normal GFM tables. A pathological Steam table containing block structures that GFM table cells cannot faithfully represent should produce an explicit diagnostic and an intentional fallback rather than malformed Markdown.

`noparse` requires special attention. This input:

```text
[noparse][b]*literal*[/b][/noparse]
```

must not become:

```markdown
**\*literal\***
```

because Steam says the inner `[b]` itself is literal text. Valve explicitly demonstrates this suppression behaviour. [^S06] Semantically, the intermediate node should therefore be:

```js
{
  type: 'steamNoParse',
  value: '[b]*literal*[/b]'
}
```

and only during GFM lowering should that become a normal text node. The established MDAST serializer then decides the correct Markdown escaping. That fixes both of Steamify's related defect classes: interpreting tags inside `noparse`, and failing to escape source Markdown metacharacters. [^H01]

Code needs the same structural treatment. Do not generate fences manually with a hard-coded triple backtick. Construct an MDAST `Code` node and let the established Markdown serializer choose safe syntax. `mdast-util-to-markdown` is specifically designed to serialise MDAST while accounting for Markdown syntax rules. [^S03] This should eliminate Steamify's failures for inline backticks and fenced-code contents. [^H01]

Nested lists must remain nested AST children:

```js
{
  type: 'list',
  ordered: true,
  children: [
    {
      type: 'listItem',
      children: [
        {
          type: 'paragraph',
          children: [{type: 'text', value: 'Parent'}]
        },
        {
          type: 'list',
          ordered: true,
          children: [
            {
              type: 'listItem',
              children: [
                {
                  type: 'paragraph',
                  children: [{type: 'text', value: 'Child'}]
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

At no point should list depth be represented by manually prepending spaces to a string.

Plain URL widgets should also be treated explicitly. Valve documents automatic widgets for plain YouTube, Store and UGC URLs. [^S06] The source pass may annotate such text as a Steam renderer feature without turning it into a fictional BBCode tag. When targeting GitHub, the converter can produce a normal GFM link/autolink but should report that Steam's widget presentation is not preserved.

### Unknown and future constructs

A de-facto tool must age well.

Unknown input such as:

```text
[futuretag foo=bar]content[/futuretag]
```

must never become just:

```text
content
```

because that destroys evidence.

The parser should produce something equivalent to:

```js
/**
 * @typedef {{
 *   type: 'unknownSteamBbcode'
 *   tagName: string
 *   rawAttributes: string
 *   rawSource: string
 * }} UnknownSteamBbcode
 */

export {}
```

and emit:

```text
STEAM_UNKNOWN_CONSTRUCT
```

A strict conversion can then fail; a permissive conversion can preserve it literally.

That design is much safer than having a table of recognised substitutions and silently stripping everything else.

### The intentionally partial reverse path

The GFM → Steam route should use the **same MDAST semantic layer**, but the contract must be narrower.

`mdast-util-from-markdown`, `micromark-extension-gfm` and `mdast-util-gfm` already provide a standard GFM-to-MDAST path, so we should reuse it rather than parsing Markdown ourselves. [^S04] The renderer can then support high-confidence mappings such as headings 1–3, strong, emphasis, deletion, links, images, lists, blockquotes, code and basic tables.

The API should make partiality impossible to miss:

```js
/**
 * @template T
 * @typedef {ConversionResult<T> & {
 *   unsupportedSourceNodes: readonly UnsupportedGfmFeatureDiagnostic[]
 * }} PartialConversionResult
 */

export {}
```

Examples of GFM concepts that should not automatically be advertised as fully reversible include task-list state, arbitrary raw HTML, footnotes, heading levels beyond the Steam profile, and target-specific GitHub constructs. MDAST's GFM ecosystem itself encompasses features that Steam does not have direct equivalents for. [^S04]

A README claim should therefore say:

> “Steam Community BBCode → GFM is the package's conformance-tested primary conversion direction. GFM → Steam Community BBCode is a documented supported subset and reports unsupported or lossy nodes.”

That is both more truthful and more useful than a nominal “bidirectional” claim.

## Test-driven development and proof of superiority

The strongest differentiator should not be clever implementation; it should be **observable evidence**.

Every implementation change should begin with a fixture and an expected semantic outcome. The specification registry, tests and documentation should be coupled so that adding a construct to one without the others fails CI.

For each `SteamConstructDefinition`, CI should require at least:

```text
canonical positive fixture
nested/context fixture
literal/escaping fixture where applicable
malformed-input fixture
expected Steam-aware MDAST
expected GFM semantic structure
expected diagnostics
documentation entry
provenance entry
```

The repository could encode that relationship directly:

```js
/**
 * @typedef {{
 *   constructId: ConstructId
 *   profile: SteamDialectProfileId
 *   source: string
 *   expectedMdast: Root
 *   expectedFidelity: ConversionFidelity
 *   expectedDiagnostics: readonly DiagnosticCode[]
 * }} ConstructConformanceCase
 */

export {}
```

Then `coverage.json` should be **generated from executed tests**, not manually declared.

A simplified generated record might be:

```json
{
  "construct": "steam.bbcode.noparse",
  "profiles": ["workshop-item", "ugc-description"],
  "provenance": [
    "valve.workshop-item-formatting-help"
  ],
  "tests": {
    "canonical": "pass",
    "nested": "pass",
    "escaping": "pass",
    "malformed": "pass"
  },
  "gfm": {
    "fidelity": "exact",
    "test": "pass"
  }
}
```

The stable-release gate should require **every construct row to be complete**. A percentage such as “94% Steam tags supported” should not be considered sufficient for a 1.0 release.

### Structural assertions, not string snapshots alone

Tests should not merely assert:

```js
expect(output).toMatchSnapshot()
```

because syntactically different Markdown can be semantically equivalent, while plausible-looking Markdown can render with the wrong structure.

The stronger test is:

```text
Steam source
    ↓
our converter
    ↓
generated GFM
    ↓
official GFM-compatible MDAST parser
    ↓
normalised semantic MDAST
    ↓
compare against expected target tree
```

Because the MDAST GFM extensions are explicitly intended to parse and serialise GFM tables, strikethrough, task lists and other extensions, they provide an appropriate independent round-trip check of the generated structure. [^S04]

For selected cases, a scheduled conformance workflow should also call GitHub's own Markdown rendering API and compare normalised target HTML. GitHub provides a REST endpoint that renders Markdown to HTML; this should remain a **secondary live-renderer test**, not a dependency of the converter or ordinary offline unit tests. [^S13]

That lets tests distinguish:

```text
GFM-spec structural correctness
```

from:

```text
github.com presentation behaviour
```

which is exactly the distinction needed for constructs such as spoilers and underline.

### Steamify regression suite

The attached handoff should be treated as a seed corpus, not anecdotal history. It establishes two useful baselines:

```text
real current description:
Steamify 2.0.1 = 16 / 16

additional adversarial cases:
Steamify 2.0.1 = 6 / 17
```

and identifies eleven concrete failure classes. [^H01]

The new test suite should contain independent minimal reproductions of all eleven:

```text
nested ordered list
bare URL tag
inline list
multiline bold
underline
spoiler
table
noparse
literal Markdown emphasis characters
inline code containing backticks
block code containing backtick fences
```

plus the six successful Steamify cases so that the new implementation does not regress functionality Steamify already handled. [^H01]

Do not simply copy the output expectations from Steamify. Each case should have an expected **semantic tree** derived from the source construct's specification.

The existing real description can also be retained as an integration fixture **only if its copyright/redistribution status permits inclusion in the public AGPL repository**. Otherwise retain its 16 structural assertions and replace the content with independently written synthetic material.

### Adversarial and property testing

A structured parser is still parser software and should be tested as such.

Property-based testing should generate combinations of:

```text
arbitrary Unicode
Markdown metacharacters
brackets
backticks
CRLF / LF
empty elements
deeply nested tags
mixed list depths
unknown tags
missing closing tags
unexpected closing tags
large attributes
quotes and semicolons in attributes
opaque-region contents
```

A library such as `fast-check` is appropriate as a development dependency; the important requirement is the property model, not that specific package.

Useful invariants include:

```text
converter never throws for arbitrary bounded UTF-8 text
```

unless explicitly invoked in strict-parse mode;

```text
plain source text survives as equivalent rendered text
```

for characters not carrying Steam semantics;

```text
text inside noparse is never interpreted as Steam markup
```

and:

```text
unknown constructs are never silently discarded
```

as well as:

```text
serialising an output MDAST tree and parsing the resulting GFM
preserves its semantic projection
```

where GFM supports that tree.

Mutation testing should be added once the core transforms exist. A high line-coverage number does not prove that mapping tests catch incorrect operators, omitted branches or reversed conditions; mutation testing is particularly valuable for a converter full of small semantic mapping decisions.

I would make the quality gates approximately:

```text
statement coverage    >= 98%
line coverage         >= 98%
branch coverage       >= 95%
function coverage     >= 98%
mutation score        >= 90%
construct registry    100% accounted for
known silent loss       0
```

The specific generic code-coverage percentages are project policy rather than a semantic guarantee; **construct coverage and mutation results are the more important release criteria**.

### Comparative conformance harness

To make “better than alternatives” verifiable, put a reproducible comparison harness in the repository:

```text
comparison/
  corpus/
  adapters/
    steamify/
    generic-bbcode-to-markdown/
    steam-editor-tools/
    ...
  expected/
  results/
  README.md
```

Alternatives should be tested only for capabilities they actually claim.

The currently visible landscape reinforces why this approach has room to become a reference implementation. The generic Node package `bbcode-to-markdown` is an older generic BBCode converter rather than a Steam dialect implementation; its published description lists a simple conversion API and multiple dependencies. [^S14] `Steam Editor Tools` has a much stronger structured architecture—its Python implementation explicitly uses an intermediate `Document` model and claims comprehensive Steam-format support—but its primary documented flow is into Steam BBCode and it is a Python package, not a focused Node Steam→GFM library. [^S15] `Converter.MarkdownToBBCode` is a .NET tool whose Steam support is primarily Markdown/HTML → Steam BBCode, i.e. the opposite direction from this project's primary contract. [^S11] Other Steam editors and generators focus on authoring or Markdown→Steam rather than a conformance-tested Steam→GFM AST conversion. [^S16]

That makes `Steam Editor Tools` especially useful as an architectural comparator rather than something to dismiss: its authors independently reached the conclusion that structured intermediate data is preferable to regex reconstruction and explicitly contrast that with a regex-based Steam converter. [^S15] Our Node package should carry that principle further by using the existing MDAST ecosystem instead of inventing its own generic document schema.

The comparison report should publish raw test results, exact dependency versions and hashes where possible, and classify every outcome:

```text
PASS_EXACT
PASS_EQUIVALENT
PASS_DIAGNOSED_LOSS
FAIL_WRONG_STRUCTURE
FAIL_SILENT_LOSS
FAIL_RAW_SOURCE_MARKUP
FAIL_EXCEPTION
NOT_SUPPORTED_BY_PROJECT
```

That classification is much more informative than a percentage alone.

The release criterion should be **Pareto-style dominance rather than a contrived weighted score**:

* no alternative may outperform the candidate on documented Steam-source construct coverage without that gap being explicitly explained;
* every Steamify regression supported by GFM/GitHub must be fixed;
* unavoidable target loss must be detected rather than silent;
* the entire evidence set must be reproducible.

The project can then publish a statement such as:

> “Release 1.0 passes every construct in conformance corpus X; Steamify 2.0.1 passes Y under the same applicable Steam→GFM cases.”

That is a defensible claim. “We are the best Steam converter” is not.

## Security, licensing, documentation and package release

Although this is “only” a text converter, it processes potentially hostile text, URLs and deeply nested structures, so the security model should be explicit.

The core package should be **pure transformation code**:

```text
no HTTP requests
no Steam authentication
no GitHub authentication
no filesystem access in library functions
no shell execution
no dynamic JavaScript evaluation
no loading remote images
no automatic URL fetching
```

Only the CLI should read stdin/files and write stdout/files.

The parser should have configurable resource limits such as:

```js
/**
 * @typedef {{
 *   maxInputBytes: number
 *   maxNestingDepth: number
 *   maxNodeCount: number
 *   maxAttributeBytes: number
 * }} SteamParseResourceLimits
 */

export {}
```

Defaults should be generous enough not to confuse Steam's current UI limits with a universal library limitation. The community catalogue itself records different character limits across Steam surfaces and warns that some limits may be inaccurate, so hard-coding “Workshop is 8,000 characters, therefore reject 8,001” into a general parser would be wrong. [^S08]

URLs must not be fetched as part of conversion. A URL node should be treated as untrusted data. When a target representation requires raw HTML, attributes must be escaped and URL schemes explicitly considered. MDAST's own security documentation warns that MDAST can contain HTML and that unsafe transformation of untrusted HTML can create XSS problems. [^S02] For that reason, arbitrary Steam source attributes should never be copied into generated HTML.

The security test corpus should include:

```text
javascript: URLs
data: URLs
HTML-shaped source text
quotes inside URLs
NUL and control characters
very deep nesting
very large numbers of sibling tags
pathological unmatched brackets
Unicode confusables
malformed attributes
enormous noparse/code bodies
```

and the project should have a `SECURITY.md` explaining the trust boundary and vulnerability-reporting procedure.

CI should include GitHub CodeQL for JavaScript/TypeScript; GitHub provides dedicated JavaScript/TypeScript security query suites. [^S17] Dependency Review should be a required pull-request check because GitHub can identify newly introduced dependency versions and associated vulnerabilities before merge. [^S18] Dependabot security/version updates should complement, not replace, human review. GitHub's current supply-chain tooling explicitly distinguishes dependency review from ongoing Dependabot alerts and updates. [^S19]

Actions workflows should declare minimum `permissions` and pin third-party Actions to full commit SHAs. GitHub's own hardening guidance explains that movable action tags can be redirected whereas a full commit SHA fixes the reviewed code. [^S20]

### AGPL-3.0-only

The project licence field must be:

```json
{
  "license": "AGPL-3.0-only"
}
```

not:

```text
AGPL-3.0
AGPL-3.0+
AGPL-3.0-or-later
GPL-3.0
```

SPDX explicitly distinguishes `AGPL-3.0-only` from `AGPL-3.0-or-later`. [^S21] The full AGPL v3 text should be present as `LICENSE`, source files should carry an appropriate copyright/licence notice or SPDX identifier, and documentation should state clearly that the package is AGPL **version 3 only**. Section 13 is the AGPL's remote-network-interaction provision for modified network-accessible versions. [^S22]

The direct dependency licences must be reviewed before adoption and on every release. BBob currently identifies itself as MIT-licensed, as does `mdast-util-gfm`. [^S05] That is encouraging, but the repository should generate a dependency-licence report from the actual locked dependency graph rather than assuming today's graph will remain unchanged.

Licence compatibility deserves an explicit pre-1.0 legal review, particularly because the intended project licence is strong copyleft. This implementation plan is not a substitute for legal advice.

### Documentation as a first-class deliverable

The documentation layout should be:

```text
README.md
LICENSE
SECURITY.md
CONTRIBUTING.md
CHANGELOG.md
CODE_OF_CONDUCT.md

docs/
  getting-started.md
  cli.md
  api.md
  javascript-and-types.md
  decisions/
    0001-javascript-with-checked-jsdoc.md
  conversion-semantics.md
  diagnostics.md
  steam-support-matrix.md        # generated
  specification-lineage.md
  gfm-to-steam-limitations.md
  security-model.md
  architecture.md
  comparison.md                  # generated results
  releasing.md

spec/
  sources.json
  constructs.json
  profiles.json
```

The README should answer, near the top:

```text
What does it convert?
Which direction is complete?
What does "complete" mean?
Which Steam surface is the default?
What happens when GFM cannot express a Steam construct?
Does it make network requests?
Which Node versions are supported?
What licence applies?
```

A minimal API example should look like:

```js
import {steamCommunityBbcodeToGfm} from 'steam-community-bbcode'

const result = steamCommunityBbcodeToGfm(source, {
  profile: 'workshop-item'
})

console.log(result.value)

for (const diagnostic of result.diagnostics) {
  console.error(
    diagnostic.severity,
    diagnostic.code,
    diagnostic.message
  )
}
```

For deterministic repository generation:

```js
const result = steamCommunityBbcodeToGfm(source, {
  profile: 'workshop-item',
  failOnFidelityBelow: 'equivalent'
})
```

This is a much better integration contract than asking users to remember “don't use tables”, “don't put a backtick in code”, or similar limitations.

`docs/conversion-semantics.md` must contain examples for **every construct**, but the support table itself should be generated from the registry so the prose cannot claim support that tests do not provide.

The attached Steamify handoff should inform a migration section explaining the relevant behavioural differences without exaggerating the evidence: Steamify 2.0.1 successfully converted the current simple description but failed many ordinary additional constructs under the preservation contract. [^H01]

### Developer comments

The source should be unusually well-commented, but comments must explain semantics and design decisions, not narrate syntax.

Every exported function/type should have **checked JSDoc** covering its contract and semantics, such as the following signature-and-comment sketch. The omitted implementation is to be supplied by the TDD milestones, and referenced types must be imported through JSDoc from their owning `.js` modules:

```js
/**
 * Converts Steam Community BBCode into GitHub-Flavoured Markdown.
 *
 * The input is parsed according to the selected Steam dialect profile.
 * Known constructs that cannot be represented exactly in the target are
 * reported through `ConversionResult.diagnostics`; they are never silently
 * discarded.
 *
 * This function performs no network or filesystem I/O.
 *
 * @param {string} source
 *   Source BBCode; never evaluated or fetched.
 * @param {SteamBbcodeToGfmOptions} [options]
 *   Dialect profile, fidelity policy and bounded-resource settings.
 * @returns {ConversionResult<string>}
 *   Generated GFM with structured diagnostics and coverage information.
 *
 * @see steam.bbcode.noparse
 * @see docs/conversion-semantics.md
 */
export function steamCommunityBbcodeToGfm(source, options) {
  // ...
}
```

A non-obvious algorithm should explain **why**:

```js
// `noparse` is lowered only after Steam syntax interpretation.
// Converting its payload recursively would incorrectly turn source such as
// `[noparse][b]literal[/b][/noparse]` into Markdown strong emphasis.
// See construct `steam.bbcode.noparse`.
```

Do not write:

```js
// Loop over children.
for (const child of children) {
```

because that contributes no information.

Stable construct IDs in comments are preferable to scattering Steam URLs through the implementation. `sources.json` should resolve those IDs to provenance, allowing links to change without rewriting code.

### npm release

The package should publish the authored JavaScript, generated declarations, declaration maps pointing to the shipped JavaScript source, specifications needed for provenance, README and licence—but not test artefacts, local comparison environments or arbitrary repository files. The original source-map intent—inspectability and source navigation—is retained: untransformed JavaScript needs no executable `.js.map`; any future approved JavaScript transformation must publish its corresponding source maps as well.

Before publication:

```text
npm test
npm run test:conformance
npm run test:property
npm run test:mutation
npm run lint
npm run typecheck
npm run security
npm run build
npm run test:types
npm run types:check
npm run docs:check
npm run coverage:check
npm pack --dry-run
npm run test:package
```

The release workflow should use **npm Trusted Publishing through GitHub Actions OIDC rather than a long-lived npm automation token**. npm currently generates provenance attestations automatically for public packages published through trusted publishing from a public GitHub repository. [^S23] Consumers and maintainers can use `npm audit signatures` to verify registry signatures and package provenance attestations. [^S24]

That is an especially good fit for an AGPL package intended to make transparency part of its value proposition.

## Implementation sequence and stable-release definition

The implementation should proceed by **proof gates**, not by building the whole converter and testing it afterwards.

The first repository state should contain only the project charter, `AGPL-3.0-only` licensing, architecture decision records, JavaScript/Node configuration, mandatory JSDoc checking and declaration-generation configuration, test runner, CI, initial source registry and the Steamify regression corpus. This makes TDD literal: failing conformance tests exist before the corresponding converter implementation.

Before parser work, the initial tooling gate must demonstrate checked-JavaScript positive and negative contract fixtures, clean declaration-only generation and an installed-package smoke test. This is an additional gate, not a replacement for any conversion fixture or parser-acceptance requirement.

The next gate should be the **BBob feasibility spike**. No conversion code should be built around BBob until executable tests establish:

```text
ordinary nested paired tags
Steam [*] item boundaries
ordered and unordered nested lists
opaque code
opaque noparse
Steam quote attributes
table attributes
preview-style semicolon attributes
unknown tags
unclosed tags
mismatched tags
Unicode
CRLF/LF
```

BBob already offers AST parsing, recognised-tag restrictions and context-free inner parsing, so it is a strong candidate rather than an arbitrary new dependency. [^S05] The output of this gate is an ADR: either “BBob accepted”, “BBob accepted with adapter X”, or “BBob rejected because requirement Y is impossible without replacing its parser semantics”.

The following gate is **source semantic normalisation**. Every recognised syntax node becomes either standard MDAST or a narrowly defined Steam MDAST extension. At this stage there is still no Markdown output. Tests compare source BBCode directly with semantic trees.

Then comes **GFM lowering**. One Steam extension at a time receives a documented target policy and a test. This stage should aggressively reuse `mdast-util-to-markdown` and `mdast-util-gfm`, because the unified ecosystem already solves Markdown syntax serialisation and GFM extension handling. [^S04]

Only after the Steam→GFM path reaches complete registry coverage should the **partial GFM→Steam renderer** be added. It should consume MDAST from the standard GFM parser rather than sharing ad-hoc regexes with the forward converter. [^S25] Its feature matrix should be independently generated; forward coverage must not imply reverse coverage.

The next gate is **differential validation**. Run the common corpus against every reasonably automatable alternative and publish the raw results. The most serious architectural comparator, Steam Editor Tools, already demonstrates the value of an intermediate structured document representation, but is Python-based and oriented primarily around producing Steam BBCode. [^S15] Generic BBCode-to-Markdown packages do not model Steam's context-specific dialect. [^S14] Markdown-to-Steam tools address primarily the reverse problem. [^S11] The new project should therefore prove its niche rather than merely assert one.

The penultimate gate is **security and supply-chain review**: fuzz/property tests, resource-exhaustion tests, URL handling review, CodeQL, dependency review, locked licence report, Action-SHA review and a manual review of all raw-HTML generation. GitHub provides CodeQL analysis for JavaScript/TypeScript and dependency-review mechanisms suitable for those CI gates. [^S17]

The final release candidate should not be called `1.0.0` until all of the following are true:

| Stable-release condition | Required state |
|---|---|
| Official Workshop constructs | Every currently documented construct represented in registry and tested |
| Community-discovered constructs | Every construct in the chosen community lineage classified and tested |
| Context-specific constructs | Profile membership explicit |
| Historical/disputed constructs | Preserved/classified; no false “currently supported” assertion |
| Steamify current-description baseline | 16/16 preserved |
| Steamify additional regression corpus | All 17 accounted for with correct output or explicit target-fidelity handling |
| Silent text deletion | Zero known cases |
| Silent construct deletion | Zero known cases |
| Literal Markdown metacharacters | Escaped through structural serialisation |
| `noparse` | Opaque semantics verified |
| Code containing delimiters | Verified |
| Nested ordered/unordered lists | Structural tests verified |
| Tables | Structural GFM test verified |
| Unknown syntax | Preserved plus diagnostic |
| MDAST conformance | Standard nodes used wherever an existing node is semantically correct |
| Custom AST surface | Only Steam-specific MDAST extensions |
| Forward conversion | Complete against registry |
| Reverse conversion | Explicit supported-subset matrix; no complete-GFM claim |
| Documentation | Generated coverage table matches executable tests |
| API documentation | Every exported symbol documented |
| Implementation authoring | JavaScript ESM; no TypeScript runtime implementation or consumer compilation |
| Checked contracts | Strict JSDoc checking and positive/negative contract tests passing |
| Generated declarations | Clean `.d.ts`/declaration-map generation; no handwritten duplicate API |
| Installed consumers | JavaScript runtime and TypeScript declaration tests pass against the packed artefact |
| Security | CodeQL/dependency/fuzz/resource-limit gates passing |
| Dependency licences | Reviewed from locked graph |
| Licence | `AGPL-3.0-only` throughout |
| Node support | Node 22/24/26 CI passing |
| npm artefact | `npm pack` contents reviewed |
| Supply-chain provenance | Trusted Publishing/OIDC configured |
| Reproducible comparison | Published alongside release |

At that point, a release-quality claim can be concrete:

> **`steam-community-bbcode` is an AST-based Steam Community BBCode converter for Node.js whose Steam→GFM support is defined by a versioned, source-provenanced construct registry. Every registered construct has an executable conversion policy, and any unavoidable semantic degradation is explicitly reported. GFM→Steam support is intentionally a documented subset.**

That is a much stronger proposition than Steamify's effective model of “convert the cases the implementation happens to recognise”. The attached probe demonstrates exactly why this matters: Steamify succeeded on the current easy input but failed eleven of seventeen broader preservation cases, including several that an AST pipeline should handle naturally. [^H01]

More importantly, the architecture makes future claims **falsifiable**. When Steam changes, a maintainer updates a provenance record and fixture. When GitHub rendering changes, a conformance test detects it. When somebody reports a missing Steam tag, CI refuses to regard it as supported until parsing, semantics, target conversion, documentation and provenance all agree.

That—not a larger collection of replacements—is what would give the package a realistic route to becoming the de-facto Steam Community BBCode → GitHub-Flavoured Markdown implementation.

## References retained from the original plan

The following references were recovered from the original report’s citation metadata. Their inclusion preserves its research lineage; this language/toolchain amendment does not claim to have re-verified every original source. Tracking parameters have been removed from URLs.

[^S01]: [Searching for and choosing packages to download | npm Docs](https://docs.npmjs.com/searching-for-and-choosing-packages-to-download/). Cited in the original plan dated 7 September 2026.

[^S02]: [GitHub - syntax-tree/mdast: Markdown Abstract Syntax Tree format · GitHub](https://github.com/syntax-tree/mdast). Cited in the original plan dated 7 September 2026.

[^S03]: [GitHub - syntax-tree/mdast-util-to-markdown: mdast utility to serialize markdown · GitHub](https://github.com/syntax-tree/mdast-util-to-markdown). Cited in the original plan dated 7 September 2026.

[^S04]: [GitHub - syntax-tree/mdast-util-gfm: mdast extension to parse and serialize GFM (GitHub Flavored Markdown) · GitHub](https://github.com/syntax-tree/mdast-util-gfm). Cited in the original plan dated 7 September 2026.

[^S05]: [GitHub - JiLiZART/BBob: ⚡️Blazing fast js bbcode parser, that transforms and parses bbcode to AST and transform it to HTML, React, Vue with plugin support in pure javascript, no dependencies · GitHub](https://github.com/jilizart/bbob). Cited in the original plan dated 7 September 2026.

[^S06]: [Text Formatting](https://steamcommunity.com/comment/WorkshopItem/formattinghelp). Cited in the original plan dated 7 September 2026.

[^S07]: [Text Formatting](https://steamcommunity.com/comment/Recommendation/formattinghelp). Cited in the original plan dated 7 September 2026.

[^S08]: [Steam Community :: Guide :: Comprehensive Formatting Help](https://steamcommunity.com/sharedfiles/filedetails?id=2807121939). Cited in the original plan dated 7 September 2026.

[^S09]: [Node.js — Node.js Releases](https://nodejs.org/en/about/previous-releases). Cited in the original plan dated 7 September 2026.

[^S10]: [Organizing information with collapsed sections - GitHub Docs](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/organizing-information-with-collapsed-sections?apiVersion=2022-11-28). Cited in the original plan dated 7 September 2026.

[^S11]: [GitHub - BUTR/Converter.MarkdownToBBCode: Converts Markdown and HTML into NexusMods and Steam BBCode flavor · GitHub](https://github.com/BUTR/Converter.MarkdownToBBCode). Cited in the original plan dated 7 September 2026.

[^S12]: [GitHub - syntax-tree/mdast-util-gfm-table: mdast extension to parse and serialize GFM tables · GitHub](https://github.com/syntax-tree/mdast-util-gfm-table). Cited in the original plan dated 7 September 2026.

[^S13]: [REST API endpoints for Markdown - GitHub Docs](https://docs.github.com/en/rest/markdown/markdown). Cited in the original plan dated 7 September 2026.

[^S14]: [Bbcode-to-markdown NPM | npm.io](https://npm.io/package/bbcode-to-markdown). Cited in the original plan dated 7 September 2026.

[^S15]: [GitHub - cainmagi/steam-editor-tools: This package offers editor tools helping users write Steam guides and reviews. Support Steam information queries, image editing tools, and BBCode text processing tools. · GitHub](https://github.com/cainmagi/steam-editor-tools). Cited in the original plan dated 7 September 2026.

[^S16]: [GitHub - bijx/Steam-Markup-Generator: A lightweight rich-text editor that enables you to format text using Steam's custom markup tags, providing a live preview of the formatted content. · GitHub](https://github.com/bijx/Steam-Markup-Generator). Cited in the original plan dated 7 September 2026.

[^S17]: [JavaScript and TypeScript queries for CodeQL analysis - GitHub Docs](https://docs.github.com/en/code-security/reference/code-scanning/codeql/codeql-queries/javascript-typescript-built-in-queries). Cited in the original plan dated 7 September 2026.

[^S18]: [Dependency review - GitHub Docs](https://docs.github.com/en/code-security/concepts/supply-chain-security/dependency-review). Cited in the original plan dated 7 September 2026.

[^S19]: [Supply chain security - GitHub Docs](https://docs.github.com/en/code-security/concepts/supply-chain-security). Cited in the original plan dated 7 September 2026.

[^S20]: [Protecting against security threats - GitHub Docs](https://docs.github.com/en/code-security/tutorials/secure-your-organization/protect-against-threats). Cited in the original plan dated 7 September 2026.

[^S21]: [GNU Affero General Public License v3.0 only | Software Package Data Exchange (SPDX)](https://spdx.org/licenses/preview/AGPL-3.0-only.html). Cited in the original plan dated 7 September 2026.

[^S22]: [GNU Affero General Public License - GNU Project - Free Software Foundation](https://www.gnu.org/licenses/agpl-3.0.html.en). Cited in the original plan dated 7 September 2026.

[^S23]: [Trusted publishing for npm packages | npm Docs](https://docs.npmjs.com/trusted-publishers/). Cited in the original plan dated 7 September 2026.

[^S24]: [npm-audit | npm Docs](https://docs.npmjs.com/cli/audit/). Cited in the original plan dated 7 September 2026.

[^S25]: [GitHub - syntax-tree/mdast-util-from-markdown: mdast utility to parse markdown · GitHub](https://github.com/syntax-tree/mdast-util-from-markdown). Cited in the original plan dated 7 September 2026.

[^H01]: User-supplied **2026-09-07-steamify-unsuitability-handoff.md**, dated 7 September 2026. The exact supplied file is retained under `evidence/` in the revision bundle. These are user-supplied empirical findings, not newly rerun benchmarks.


## References for the v2 JavaScript amendment

The following primary sources were checked for the amendment on 7 September 2026. They supplement rather than replace the original plan's references. Configuration, test gates and distribution choices above are project recommendations; their presence is not a claim that the converter has been implemented.

[^V2-01]: TypeScript, [TSConfig: `checkJs`](https://www.typescriptlang.org/tsconfig/checkJs.html).

[^V2-02]: TypeScript, [Creating `.d.ts` files from `.js` files](https://www.typescriptlang.org/docs/handbook/declaration-files/dts-from-js.html).

[^V2-03]: `mdast-util-to-markdown`, [JavaScript serializer implementation](https://raw.githubusercontent.com/syntax-tree/mdast-util-to-markdown/main/lib/index.js).

[^V2-04]: `mdast-util-to-markdown`, [package manifest: dependencies, JavaScript/ESM distribution and type-checking scripts](https://raw.githubusercontent.com/syntax-tree/mdast-util-to-markdown/main/package.json).

[^V2-05]: `unified`, [JavaScript processor implementation](https://raw.githubusercontent.com/unifiedjs/unified/main/lib/index.js).

[^V2-06]: `unified`, [package manifest and development tooling](https://raw.githubusercontent.com/unifiedjs/unified/main/package.json).

[^V2-07]: TypeScript, [Type checking JavaScript files](https://www.typescriptlang.org/docs/handbook/type-checking-javascript-files.html).

[^V2-08]: TypeScript, [JSDoc reference](https://www.typescriptlang.org/docs/handbook/jsdoc-supported-types.html).

[^V2-09]: TypeScript, [Publishing declaration files: declaration dependencies and package metadata](https://www.typescriptlang.org/docs/handbook/declaration-files/publishing.html).


## Revision record and preservation scope

Version 2 replaces TypeScript-first executable source with JavaScript ES modules, checked JSDoc and generated declarations. It amends the executive recommendation, source layout, code examples, documentation/comment requirements, dependency placement, build/release workflow and authoring-specific acceptance gates. The public API signature projection is explicitly identified as generated declaration output rather than a TypeScript implementation.

No original construct, semantic mapping, original test category, numerical quality threshold, security control, licensing requirement, alternative-comparison obligation, source reference or original implementation milestone has been removed. New type-contract and installed-consumer gates are additive. Unrelated original wording has deliberately been retained rather than silently reconciled or corrected during a language/toolchain amendment.

The accompanying revision bundle contains the complete recovered original final-report text, a portable-reference v1, this v2, `v1-to-v2.patch`, `amendment-ledger.json`, `preservation-checks.json`, `verify-revision.mjs`, and the supplied Steamify handoff. The original report's prose and code are archived separately from citation-format normalization. Exact text recovery and reversible edit replay demonstrate preservation outside the identified edit regions; they do not constitute a formal proof of converter correctness, semantic equivalence of all code sketches, or completeness of Steam's external specification.
