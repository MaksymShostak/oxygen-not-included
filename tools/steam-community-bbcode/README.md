# steam-community-bbcode

This AGPL-3.0-only subpackage implements Steam Community BBCode parsing, MDAST conversion and GFM serialization.
All 39 registry entries have executed conversion policies: 37 observable source constructs and two context-only renderer rules.
The [generated matrix](docs/steam-support-matrix.md) distinguishes preserved, approximated and unsupported semantics.
Stable release qualification remains unfinished; the private `1.0.0` development label does not attest readiness.

The [accepted implementation plan](../../docs/plans/2026-09-08-steam-community-bbcode-implementation-plan-v2.md) (source checkout) defines an ESM JavaScript package with checked JSDoc, generated declarations, source-provenanced Steam constructs, MDAST semantics and native GFM serialization.
Steam-to-GFM is the primary direction; GFM-to-Steam implements an explicit subset.
The default source profile is `workshop-item`.
The library performs no network or filesystem I/O.
Unavoidable loss must receive structured diagnostics.

This directory is a separate licensing boundary.
The repository root MIT/Klei licence and .NET/ONI Mod Pipeline are unchanged.
See [LICENSE](LICENSE) for AGPL version 3 only.
Dependency notices retain their original terms.
Publication and pre-release legal review are separate gates.

Use npm 12.0.2; `npm --version` should report that exact version.
Install this directory with `npm ci --ignore-scripts`, and install the approved coverage environment with `npm --prefix tooling/type-coverage ci --ignore-scripts`.
Install the separate approved docs environment with `npm --prefix tooling/api-docs ci --ignore-scripts`.
TypeScript 7 owns primary checking, declarations and consumers; the two TS 6 environments have only their documented metric and API-generation roles.
For source-checkout documentation checks, use the repository's Python 3.14.7 `.venv` (created by root `npm run setup:development`).
From the repository root, install the development-only formatter:

```text
node scripts/runRepositoryPython.js -m pip install --require-hashes --only-binary=:all: -r tools/steam-community-bbcode/tooling/prose/requirements.txt
```

`npm run docs:format` in this package applies semantic sentence line breaks to authored guides.
`npm run docs:format:check` checks them without writing.
Run `npm run check` here, or `npm run check:converter` at the repository root.
Chevrotain 13.2.0 now supplies the qualified grammar foundation.
Source parsing qualification is separate from the converter API and conformance requirements.

Historical Steamify evidence and independently defined regression contracts live under `test/fixtures/steamify/`.
All 16 current-description obligations and 17 additional cases pass the new converter's accounted-for preservation expectations.
`qualify:parser` checks the selected native grammar foundation separately.

```js
import {steamCommunityBbcodeToGfm} from 'steam-community-bbcode';

const result = steamCommunityBbcodeToGfm('[b]Literal *text*[/b]');
console.log(result.value);
console.log(result.diagnostics);
```

`parseSteamCommunityBbcode` returns immutable source syntax, positions and parsing diagnostics.
`steamCommunityBbcodeToMdast` accepts a source string or a result issued by that parser.
Prepared results retain their profile and parsing limits; changing those requires parsing the source string again.
MDAST preserves Steam extensions until GFM lowering applies the target policy.
Per-input `coverage` describes observed construct outcomes, not completion of the source registry.
Its `contextOnlyPolicies` separately identifies remote effects whose occurrences cannot be inferred from input.
Those entries do not count as observed constructs.

Conformance executes canonical, nested, literal-text and malformed contexts for each source case across all six profiles.
Profile membership records source evidence; these passes do not establish current Steam rendering on every surface or support for every parameter variant.
Some GitHub surfaces additionally autolink escaped text; `GFM_RENDERER_AUTOLINK_POSSIBLE` reports that possibility separately from GFM syntax fidelity.

The CLI, comparative corpus, seeded properties and resource contracts have local execution evidence.
Runtime coverage and mutation results are recorded with their assessed source snapshots in [testing](docs/testing.md).
Independent security/release review and the complete remote Node/OS matrix remain separate gates.
See [testing](docs/testing.md) for exact scope and retained evidence.

Start with [getting started](docs/getting-started.md), the [API](docs/api.md), [CLI](docs/cli.md), [per-construct examples](docs/conversion-semantics.md) and [diagnostics](docs/diagnostics.md).
Node 22/24/26 are the configured CI lines; current local results use Node 24.20.0 on Windows x64.
The [security model](docs/security-model.md) states resource and HTML boundaries, and [SECURITY.md](SECURITY.md) provides private reporting.
[Comparison](docs/comparison.md) and [release qualification](docs/releasing.md) distinguish recorded results from outstanding acceptance.

The forward URL policy uses native WHATWG parsing, permits HTTP/HTTPS and relative references (plus `mailto:` for links), and rejects control characters and backslashes before normalization.
Rejected destinations remain literal source with diagnostics.
The package never fetches destinations.
Unknown and currently unmapped constructs also retain their complete source with explicit diagnostics.

`gfmToSteamCommunityBbcode` is intentionally partial.
Its [independent support matrix](docs/steam-support-matrix.md#partial-gfm-to-steam) records mappings and literal preservation for unsupported GFM.
Check `unsupportedSourceNodes` and `diagnostics` before using its output.
Reverse resources must be safe absolute URLs: a relative Markdown destination has no qualified document base in Steam.
Images with meaningful alternative text, metadata and other unsupported nodes retain their complete Markdown source as literal text.

```js
import {gfmToSteamCommunityBbcode} from 'steam-community-bbcode';

const partial = gfmToSteamCommunityBbcode('## Heading\n\n**Text**');
console.log(partial.value);
console.log(partial.unsupportedSourceNodes);
```

Reverse `resourceLimits` default to 1 MiB of UTF-8 input, 100,000 native MDAST nodes, depth 128 and 8 MiB of UTF-8 output.
The root counts as one node at depth zero.
Input bytes are checked before native parsing; node/depth limits apply after native parsing and before rendering.
Output limits are checked as rendered subtrees are assembled.
Exceeding a limit returns an input-scoped error and an empty target, never a truncated document.
These bounds are not a parser timeout or a limit on the native parser's intermediate allocations.
Caller limits must be positive safe integers; supported renderer depth cannot exceed 256.

## Command line

The installed package exposes `steam-community-bbcode`.
From this source checkout, use `npm run cli -- COMMAND ...`.
The CLI executes the same public converters:

```text
steam-community-bbcode to-gfm STEAM_DESCRIPTION.bbcode
steam-community-bbcode to-gfm --fail-on=lossy STEAM_DESCRIPTION.bbcode
steam-community-bbcode to-gfm --format=json --profile=workshop-item description.bbcode
steam-community-bbcode to-steam README.md
steam-community-bbcode coverage --format=json
```

`to-steam` is **partial**.
Unsupported GFM is preserved as literal source and reported; inspect `unsupportedSourceNodes` in JSON output.
Omit the input path or use `-` to read stdin.
Use `--` before an input filename that starts with `-`.
Input is UTF-8; malformed byte sequences fail instead of becoming replacement characters.
The CLI enforces the default 1-MiB input bound while reading, with `--max-input-bytes=N` for an explicit positive safe-integer override.

Stdout contains only converted text, or the public result object with `--format=json`.
Stderr contains one JSON object per diagnostic.
`--fail-on` accepts `none` (default), `approximate`, `lossy` or `unsupported`, and rejects the chosen fidelity or worse.
Exit status is 0 for acceptance, 1 for conversion or fidelity failure, and 2 for argument, I/O or encoding failure.
Fidelity failures retain output for inspection; check the exit status before adopting it.
No output-file overwrite option is provided.
`--profile` applies to `to-gfm` only.

The `coverage` command prints the shipped **conformance** report.
It does not measure runtime code coverage or run a Steam renderer.
`--help` describes the commands and partial reverse contract.
Archive consumer checks exercise the actual installed npm binary and shipped report without network resolution.
