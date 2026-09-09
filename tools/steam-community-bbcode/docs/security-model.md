# Security model

The library transforms caller-supplied strings into source syntax, Steam MDAST, GFM, or a documented partial Steam result.
It does not own authentication, authorization, a server, a filesystem namespace or a remote renderer.
Its [security policy](../SECURITY.md) states the required boundary and reporting path.

## Text and target structure

The forward parser uses Chevrotain's lexer/grammar, retaining original source and UTF-16 positions.
Issued parse results are immutable and recognized by identity; an arbitrary caller-created object cannot masquerade as a prepared parse result.
The semantic stage builds native MDAST nodes and narrow Steam extensions.
Unknown or unqualified constructs retain complete source with diagnostics.

GFM lowering uses `mdast-util-to-markdown` and the native GFM extension.
Literal Markdown punctuation and HTML-shaped source remain text.
Code and noparse content stay opaque until target serialization.
Spoilers can emit only the fixed `<details>`, `<summary>Spoiler</summary>` and `</details>` wrapper; content is serialized separately as MDAST.
Source attributes are never interpolated into it.

Reverse conversion consumes native GFM syntax independently of the optional GitHub post-parse autolinker.
Its literal encoder protects each opening bracket inside a separate noparse region, including input containing `[/noparse]`.
Unsupported nodes retain their original Markdown spans and are explicitly accounted for.
Code containing a Steam code closer is preserved as literal source instead of being placed inside an escapable code region.

## Resources and I/O

The URL policy uses WHATWG URL parsing after rejecting controls and backslashes.
Forward links allow HTTP/HTTPS, relative references and `mailto:`; images do not allow `mailto:`. Reverse resources require safe absolute URLs and reject Steam delimiter/quote characters.
Resource destinations are never fetched or verified for existence.
Guide-local image identifiers remain unresolved without a URL.

CLI file paths are explicit operator arguments.
Source text does not become a path or command.
The CLI reads a file or stdin, validates UTF-8, writes stdout and stderr, and returns an exit status.
A caller that offers it as a service must enforce its own authorization for filesystem arguments.

## Resource limits and their scope

| Dimension | Forward default | Reverse default | Enforcement |
| --- | --- | --- | --- |
| UTF-8 input bytes | 1,048,576 | 1,048,576 | Before parsing; CLI additionally bounds acquisition |
| Nesting depth | 128 | 128 | Forward during grammar descent; reverse after native GFM parsing |
| Node count | 100,000 source nodes | 100,000 native MDAST nodes | Forward during grammar traversal; reverse after native GFM parsing |
| Attribute bytes | 65,536 per construct | Not applicable | Forward source header check |
| UTF-8 output bytes | No separate output quota | 8,388,608 | Reverse rendered subtree assembly |

Limits must be positive safe integers; nesting overrides cannot exceed 256.
Forward node counting excludes grammar bookkeeping and attribute tokens.
Reverse counting includes the root at depth zero.
A violated conversion limit returns input-scoped error diagnostics and no partial target.
CLI acquisition failures return status 2 without a conversion result.

These controls do not bound all tokenizer/parser allocations, total process memory, concurrent work or elapsed time.
Reverse parsing precedes its structural limits.
Forward autolink diagnostics apply the native GFM tree transforms to an isolated copy of the canonical target tree, without reparsing serialized Markdown.
Large caller-selected limits expand the workload.
No worker isolation, cancellation deadline or streaming-conversion claim is made.

The performance qualification runs hostile opaque text and adjacent formatting in subprocesses with a fixed five-second regression budget.
It checks complete output and diagnostics, not only termination.
That fixture budget does not become a deadline enforced by the synchronous API or a general native-parser memory bound.

## Evidence and outstanding assurance

Maintained tests cover unsafe URLs, controls, malformed attributes, opaque-region delimiters, nested/list/table structure, immutable parse results, limits and real CLI byte streams.
Seeded properties complement independently authored examples; round trips alone are not treated as proof.
Runtime coverage and mutation qualification are described in [testing](testing.md).

CodeQL, dependency review, locked licence-text review, supply-chain provenance and independent security assessment are separate gates.
A passing deterministic check does not attest an unexecuted gate.
Remote renderer evidence is scoped to the actual endpoint and fixture used; local conversion does not establish live Steam acceptance or visual equivalence on every GitHub surface.
