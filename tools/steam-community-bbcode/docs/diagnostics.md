# Diagnostics and fidelity

Diagnostics explain parsing problems, rejected input, literal preservation and target presentation changes.
A successful function call does not imply lossless conversion.
Inspect diagnostics and occurrence outcomes before adopting output.

| Fidelity | Meaning |
| --- | --- |
| `exact` | The selected construct has an exact target mapping in the qualified contract |
| `equivalent` | Target syntax differs while the qualified semantics are retained |
| `approximate` | A documented target approximation retains the useful content |
| `lossy` | Content is retained but a documented source meaning or presentation is lost |
| `unsupported` | The source cannot be faithfully mapped under the current policy and is preserved or rejected explicitly |

For example, underline becomes text with `STEAM_UNDERLINE_LOWERED_TO_TEXT`; a block spoiler becomes a details block with `STEAM_SPOILER_LOWERED_TO_DETAILS`.
A guide image without a resolvable URL stays source with `STEAM_GUIDE_IMAGE_UNRESOLVED`.
Unknown standalone bracket labels remain literal and receive `STEAM_UNKNOWN_CONSTRUCT`; they do not acquire a missing-closer error merely because they use square brackets.
Recognized malformed tags are still diagnosed.

Each conversion diagnostic carries `code`, `severity`, `fidelity` and `message`.
`code` belongs to the generated declaration's closed `DiagnosticCode` union; messages are human explanations, not identifiers to parse.
A `sourceSpan` is present when a meaningful source location is available.

| Scope | Additional identity | Interpretation |
| --- | --- | --- |
| `construct` | `constructId` | A Steam construct occurrence, including explicit unknown syntax |
| `input` | None | A whole-input limit or target-renderer concern |
| `gfm-node` | `nodeType` | A reverse-conversion source node outside the qualified subset |

The low-level parser has its own closed parser-code union and structural diagnostics; conversion turns those into the richer outcome contract.
Codes for input/attribute/node/depth limits are stable distinct identifiers, not vendor exception messages.
A configured resource-limit failure produces error severity and an empty target, never a silently truncated document.

`GFM_RENDERER_AUTOLINK_POSSIBLE` concerns an optional second pass on some GitHub surfaces.
The serialized Markdown retains literal syntax, but that renderer may add links.
The check counts additional links; it is not a general renderer equivalence proof, remote fetch or sanitization service.

The CLI can enforce a fidelity threshold with `--fail-on=approximate`, `lossy` or `unsupported`; conversion errors always fail.
It retains output on a fidelity failure for inspection, so callers must check exit status.
See the [CLI contract](../README.md#command-line).
