<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Comparative conversion evidence

This benchmark executes unmodified published tools through their documented APIs.
It compares 37 Steam-to-GFM cases and 13 GFM-to-Steam cases against independently
authored semantic trees. The forward corpus includes four ordinary controls and
33 construct cases, many deliberately containing literal Markdown/HTML-shaped
text. These are conformance probes, not a representative workload or a performance
benchmark. Several failures can have the same underlying cause.

The complete inputs, expectations, raw outputs, stderr, consumer trees, capability
decisions, classifications, exact versions and input/package identities are in
[results/windows-x64.json](results/windows-x64.json). Regenerate with the command
below. The result date is an execution timestamp, not a release designation.

## Scope and interpretation

| Provider | Version | Applicable direction and evidence |
| --- | --- | --- |
| Candidate | Private development package 1.0.0 | Forward registry policies and the explicitly partial reverse subset |
| [Steamify](https://github.com/pivoshenko/steamify) | 2.0.1 | Both directions; README supported-feature lists |
| [bbcode-to-markdown](https://github.com/akhoury/bbcode-to-markdown) | 1.0.3 | Generic forward conversion; installed `bbcodejs.BUILTIN` and `newBBCodeTags` registrations |
| [Steam Editor Tools](https://github.com/cainmagi/steam-editor-tools) | 0.5.1 | `DocumentParser.parse_markdown` followed by `BBCodeRenderer.render` |
| [BUTR Markdown converter](https://github.com/BUTR/Converter.MarkdownToBBCode) | 1.0.0.29 | Native Markdown-to-Steam CLI, default options |

All four direct comparator packages carry MIT licences. Installed licence texts
and package metadata were inspected; their identities are retained. The isolated
Python graph also contains BSD, PSF, MIT-CMU and MPL-2.0 dependencies. Native
installations retain their notices. None of these benchmark dependencies enters
the published runtime package or the repository's main Python environment.
The old generic Node dependency graph is used only for comparison; this record
does not qualify its dependencies for production adoption.

The locked Node comparison graph uses the maintainer's unmodified `form-data`
2.5.6 v2 backport through a native override beneath Request 2.88.2.
The obsolete `~2.3.2` range otherwise retains two vulnerable multipart releases.
This declared dependency-selection change preserves all 250 outputs, statuses,
semantic trees and diagnostics against the immediately preceding native run.
Provider source remains unchanged; the report retains the new manifest/lock hashes.
The graph also selects unmodified `nwmatcher` 1.4.4 beneath `jsdom-nogyp` 0.8.3,
fixing GHSA-6394-6h9h-cfjg without changing the comparator's public API.
Request also selects unmodified `qs` 6.16.0, fixing GHSA-6rw7-vpxm-498p and
GHSA-4mjr-xmp4-gh2g. All 250 observations remain identical after this repair.
Eight moderate affected-package audit entries remain within the assessed fixed-corpus
tooling boundary, with no high or critical entries in the refreshed npm audit.

`PASS_EXACT` means the output matches an authored target spelling;
`PASS_EQUIVALENT` means its parsed semantics match. Neither is a stronger semantic
result. `PASS_DIAGNOSED_LOSS` additionally requires a per-input target-loss or
fallback diagnostic. A warning cannot excuse a wrong output tree. Unsupported
directions and unclaimed constructs are `NOT_SUPPORTED_BY_PROJECT`, not failures.
The classifier separately retains wrong structure, silent loss, unconverted
source markup and execution exceptions.

The candidate meets all 50 authored cases. That includes diagnosed fallbacks;
it does not mean every Steam feature has a lossless GFM equivalent. No alternative
exceeds the candidate on this selected documented Steam-source corpus. This is
not a global dominance claim: the reverse subset intentionally preserves features
such as inline code, heading levels 4–6 and meaningful image alt text that other
projects choose to transform. Those alternative capabilities remain relevant.

The raw results show these distinct issues or policy differences:

- Steamify and the generic converter pass ordinary formatting controls. Literal
  asterisks and HTML-shaped text in the construct corpus become GFM formatting or
  HTML. Steamify also leaves compact lists/quotes unconverted. Both split a code
  block containing its chosen closing fence.
- Steamify explicitly passes Steam-only underline, spoiler, noparse and table
  markup through in its forward direction. Its reverse feature list does not
  claim tables. These cases are excluded from its applicable conversion counts.
- The generic converter registers content-only underline, colour and noparse
  handlers. Its behaviour differs from our loss-accounting and opacity contracts;
  it has no complete Steam-dialect claim. Its generated table lacks the GFM
  delimiter row in this corpus.
- Steam Editor Tools' Markdown parser drops the empty-alt image fixture on the
  direct document/renderer path. Its wider image and authoring APIs are outside
  this benchmark.
- Steam Editor Tools and BUTR put line breaks inside code delimiters. Our Steam
  consumer preserves those as code payload, producing a tree mismatch. Whether
  Steam's live renderer visibly normalizes that padding remains unverified.
- BUTR documents that literal BBCode is deliberately passed through and
  interpreted. Its literal-text mismatch is an intentional policy difference.
  Steamify and Steam Editor Tools also leave this fixture active.

Forward results use the native GFM syntax parser with its supported extension
composition. Reverse results use the candidate Steam consumer, followed by that
native GFM parser, against authored expected trees. This dependency limits the
independence of reverse classifications: inspect raw output before reporting an
upstream defect. These are not browser screenshots, Steam renderer results,
security findings, or evidence of complete coverage of the other projects.

All 33 historical Steamify obligations remain independently exercised by
`test/steamify-description-regression.test.js` (16) and
`test/steamify-probes-regression.test.js` (17). The archived outputs and their hash
are identified in the result. They are historical comparator evidence; the
current common-corpus executions above are recorded separately.

## Reproduction

Run from the repository root on Windows x64, with the checkout's Python 3.14
`.venv`, Node 24.20.0/npm 12.0.2, and .NET 10 runtime installed. The recorded
Python lock is platform-specific. The converter's ordinary documented dependency
setup must already be complete.

```powershell
npm --prefix tools/steam-community-bbcode/comparison/node ci --ignore-scripts --no-audit --no-fund
.venv/Scripts/python.exe -m pip install --only-binary=:all: --require-hashes --target tools/steam-community-bbcode/artifacts/comparison/python -r tools/steam-community-bbcode/comparison/python/requirements-windows-py314.txt
dotnet tool install Converter.MarkdownToBBCodeSteam.Tool --tool-path tools/steam-community-bbcode/artifacts/comparison/dotnet --version 1.0.0.29 --configfile tools/steam-community-bbcode/comparison/dotnet/NuGet.Config
npm --prefix tools/steam-community-bbcode run comparison:run
npm --prefix tools/steam-community-bbcode test
```

Install into empty isolated targets. Reuse an existing matching installation;
do not mix a new resolution with old target contents. Native .NET installation
may still read host NuGet configuration before applying the explicit sources;
it required narrowly elevated execution in this sandbox. No global setting,
permission, tool, or root SDK version was changed.

The npm command runs public APIs in separate processes, with a 30-second timeout
and 8-MiB output cap per external case. Python uses child-only `PYTHONPATH` and
native text streams configured to preserve newlines. The .NET CLI receives an
actual UTF-8 file through `-i`, avoiding its path-or-text ambiguity. No shim,
vendor patch, monkeypatch, alternate checker or checker suppression is involved.
These execution bounds are benchmark bounds, not library guarantees.

Maintained source/locks/results belong to the converter package. Ignored
`artifacts/comparison` installations, native resolver reports and exact CLI input
files remain task-owned reproduction inputs for independent review. Reassess
them after that review; ordinary package checks do not need these installations.
Raw execution logs and classifier RED/GREEN evidence remain under
`.sdlc/runtime/converter/comparison-*.log` until the task's review gates complete.
