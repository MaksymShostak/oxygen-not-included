# API

Import the four named functions from `steam-community-bbcode`.
The package ships JavaScript ESM and generated declarations; no runtime compiler is required.
Contracts are generated from the checked source, not maintained as a second set of handwritten declarations.
See [JavaScript and types](javascript-and-types.md).

The [generated package-root reference](reference/index-1.md) documents signatures, options and result types from checked JSDoc.
Its [module index](reference/index.md) also includes source-owned supporting types; those source modules are internal and are not additional package import entry points.

| Function | Input | Result |
| --- | --- | --- |
| `parseSteamCommunityBbcode(source, options?)` | A BBCode string | Immutable source syntax, original source, profile and parser diagnostics |
| `steamCommunityBbcodeToMdast(input, options?)` | A BBCode string or a parser-issued result | Steam-aware MDAST, diagnostics and observed construct coverage |
| `steamCommunityBbcodeToGfm(source, options?)` | A BBCode string | Serialized GFM, diagnostics and observed construct coverage |
| `gfmToSteamCommunityBbcode(source, options?)` | A Markdown string | Partial Steam output, diagnostics, unsupported source nodes and reverse node accounting |

All functions run synchronously and perform no document-triggered I/O.
Wrong JavaScript argument types, unknown options and invalid limits throw `TypeError` or `RangeError`.
Resource exhaustion within a configured conversion bound returns an input-scoped error and an empty target; malformed or unsupported syntax normally remains literal with diagnostics.
Inspect the result before adopting it.

## Options and source syntax

Forward options accept `profile` and `resourceLimits`.
The default profile is `workshop-item`; valid profiles and their source evidence are in the [generated matrix](steam-support-matrix.md).
A profile records the selected source context; it does not assert that a live Steam surface renders every known tag.

Prepared parse results retain their profile and limits.
MDAST conversion rejects a different profile, an explicit resource-limit override or a forged parse object.
To change parsing options, pass the original source string and parse again.
The GFM function accepts a string, not a prepared parse result.

Source syntax keeps `rawSource`, complete attributes and source spans.
Spans use one-based line/column coordinates, zero-based UTF-16 offsets and exclusive ends.
Resource byte limits use UTF-8 instead; the two dimensions are deliberately distinct.

Reverse options accept only `resourceLimits`.
Reverse conversion targets the qualified Workshop subset and has no source-profile option.
The full limit table and enforcement stages are in the [security model](security-model.md).

## Conversion results

Every conversion result has `value`, `diagnostics` and `coverage`.
`value` is a Steam-aware MDAST root for semantic conversion, or a string for either text output.
Standard MDAST nodes represent standard semantics.
Narrow Steam extensions retain underline, spoiler, attributed quote, pull quote, color, noparse and media/image metadata until target policy is applied. Native mdast/unist declaration contracts remain available to consumers.

GFM lowering removes empty presentation wrappers, combines equivalent adjacent or nested styles, and carries styles across paragraph boundaries.
Native GFM serialization preserves whitespace within deletion using character references.
These target normalizations leave the immutable source syntax and Steam MDAST unchanged.
Only ASCII layout whitespace separates structural table rows/cells; non-ASCII whitespace there remains diagnosed source content.

Forward `coverage.constructs` records occurrences with construct identity, fidelity and source span.
Repeated constructs have repeated outcomes.
It is not a percentage of the registry.
`contextOnlyPolicies` records remote effects that cannot be observed from this input and does not invent occurrences for them.

Reverse results also expose `unsupportedSourceNodes`.
Their `coverage.direction` is `gfm-to-steam`; `coverage.sourceNodes` accounts for actual native GFM nodes and their spans independently of forward registry coverage.
Preserving an unsupported parent accounts for its descendants as unsupported, without separately rendering them as active Steam markup.
See [reverse limitations](gfm-to-steam-limitations.md).

The package-root type exports document option, result, fidelity, diagnostic, source-span, syntax-node and Steam-MDAST extension contracts.
Read their generated declarations through an editor or `types/index.d.ts`; the source-owned comments explain the same boundaries.
[Diagnostics](diagnostics.md) describes outcome use.
