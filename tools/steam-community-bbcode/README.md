# steam-community-bbcode

This AGPL-3.0-only subpackage implements Steam Community BBCode parsing, MDAST
conversion and GFM serialization. All 39 registry entries have executed conversion
policies: 37 observable source constructs and two context-only renderer rules.
The [generated matrix](docs/steam-support-matrix.md) distinguishes preserved,
approximated and unsupported semantics. Stable release qualification remains
unfinished; the private `1.0.0` development label does not attest readiness.

The [accepted implementation plan](../../docs/plans/2026-09-08-steam-community-bbcode-implementation-plan-v2.md)
defines an ESM JavaScript package with checked JSDoc, generated declarations,
source-provenanced Steam constructs, MDAST semantics and native GFM serialization.
Steam-to-GFM is the primary direction; GFM-to-Steam will be an explicit subset.
The default source profile is `workshop-item`. The library performs no
network or filesystem I/O. Unavoidable loss must receive structured diagnostics.

This directory is a separate licensing boundary. The repository root MIT/Klei
licence and .NET/ONI Mod Pipeline are unchanged. See [LICENSE](LICENSE) for AGPL
version 3 only. Dependency notices retain their original terms. Publication and
pre-release legal review are separate gates.

Use npm 12.0.2; `npm --version` should report that exact version.
Install this directory with `npm ci --ignore-scripts`, and install the approved
coverage environment with `npm --prefix tooling/type-coverage ci --ignore-scripts`.
Run `npm run check` here, or `npm run check:converter` at the repository root.
Chevrotain 13.2.0 now supplies the qualified grammar foundation. Source parsing
qualification is separate from the converter API and conformance requirements.

Historical Steamify evidence and independently defined regression contracts live
under `test/fixtures/steamify/`. All 16 current-description obligations and 17
additional cases pass the new converter's accounted-for preservation expectations.
`qualify:parser` checks the selected native grammar foundation separately.

```js
import {steamCommunityBbcodeToGfm} from 'steam-community-bbcode';

const result = steamCommunityBbcodeToGfm('[b]Literal *text*[/b]');
console.log(result.value);
console.log(result.diagnostics);
```

`parseSteamCommunityBbcode` returns immutable source syntax, positions and parsing
diagnostics. `steamCommunityBbcodeToMdast` accepts a source string or a result
issued by that parser. Prepared results retain their profile and parsing limits;
changing those requires parsing the source string again. MDAST preserves Steam
extensions until GFM lowering applies the target policy. Per-input `coverage`
describes observed construct outcomes, not completion of the source registry.
Its `contextOnlyPolicies` separately identifies remote effects whose occurrences
cannot be inferred from input. Those entries do not count as observed constructs.

Conformance executes canonical, nested, literal-text and malformed contexts for
each source case across all six profiles. Profile membership records source
evidence; these passes do not establish current Steam rendering on every surface
or support for every parameter variant. Some GitHub surfaces additionally autolink
escaped text; `GFM_RENDERER_AUTOLINK_POSSIBLE` reports that possibility separately
from GFM syntax fidelity.

The reverse converter, CLI, broader property/resource/mutation qualification,
comparative evaluation and independent security/release review remain later gates.
Runtime test coverage and mutation thresholds have not yet been qualified.

Current URL policy uses native WHATWG parsing, permits HTTP/HTTPS and relative
references (plus `mailto:` for links), and rejects control characters and
backslashes before normalization. Rejected destinations remain literal source with
diagnostics. The package never fetches destinations. Unknown and currently
unmapped constructs also retain their complete source with explicit diagnostics.
