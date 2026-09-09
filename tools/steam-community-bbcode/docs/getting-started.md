# Getting started

This package is private and under qualification.
Use a source checkout; there is no published release to install.
Node 22, 24 and 26 are the declared CI lines.
Local evidence currently comes from Node 24.20.0 and npm 12.0.2 on Windows x64; the configured matrix is not itself proof that every combination passes.

From `tools/steam-community-bbcode` in the source checkout:

```text
npm ci --ignore-scripts
npm --prefix tooling/type-coverage ci --ignore-scripts
npm run check
npm run cli -- to-gfm path/to/description.bbcode
```

The first two commands install the locked parent graph and approved isolated type-coverage tool.
They do not run package installation scripts.
The library executes ordinary JavaScript; checking and declaration generation are development steps.
See [JavaScript and types](javascript-and-types.md).

From an application with the packed package installed:

```js
import {steamCommunityBbcodeToGfm} from 'steam-community-bbcode';

const result = steamCommunityBbcodeToGfm('[h2]Guide[/h2][b]Text[/b]');
console.log(result.value);
for (const diagnostic of result.diagnostics) {
  console.error(diagnostic.code, diagnostic.fidelity, diagnostic.message);
}
```

The default profile is `workshop-item`.
Input punctuation stays literal until the Steam parser recognizes a qualified construct.
The native serializer escapes Markdown punctuation.
Unknown syntax and unsupported constructs retain source with diagnostics.
Neither conversion direction fetches images, follows links or reads files; the CLI reads only its selected file or stdin.

Before adopting output, inspect its diagnostics and occurrence coverage.
The [generated matrix](steam-support-matrix.md) describes executed policies, including lossy mappings.
[Reverse conversion](gfm-to-steam-limitations.md) is partial.
Use the [CLI fidelity policy](cli.md) when conversion participates in automation.

This package is [AGPL version 3 only](../LICENSE).
The enclosing repository's different licence does not replace this subpackage licence.
