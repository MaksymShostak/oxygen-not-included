<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Comparative conversion evidence

This benchmark executes unmodified published tools through their documented APIs.
The candidate and three active alternatives yield 200 observations.
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
| [Steam Editor Tools](https://github.com/cainmagi/steam-editor-tools) | 0.5.1 | `DocumentParser.parse_markdown` followed by `BBCodeRenderer.render` |
| [BUTR Markdown converter](https://github.com/BUTR/Converter.MarkdownToBBCode) | 1.0.0.29 | Native Markdown-to-Steam CLI, default options |

All three active comparator packages carry MIT licences. Installed licence texts
and package metadata were inspected; their identities are retained. The isolated
Python graph also contains BSD, PSF, MIT-CMU and MPL-2.0 dependencies. Native
installations retain their notices. None of these benchmark dependencies enters
the published runtime package or the repository's main Python environment.

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

- Steamify passes ordinary formatting controls. Literal
  asterisks and HTML-shaped text in the construct corpus become GFM formatting or
  HTML. Steamify also leaves compact lists/quotes unconverted and splits a code
  block containing its chosen closing fence.
- Steamify explicitly passes Steam-only underline, spoiler, noparse and table
  markup through in its forward direction. Its reverse feature list does not
  claim tables. These cases are excluded from its applicable conversion counts.
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

## Retired comparator evidence

The owner retired `bbcode-to-markdown` 1.0.3 from executable comparison tooling
on 10 September 2026. Its dependency on unsupported Request 2.88.2 blocks native
dependency review with [GHSA-p8p7-x288-28g6](https://github.com/advisories/GHSA-p8p7-x288-28g6),
for which Request has no patched release. Earlier form-data, nwmatcher and qs
repairs did not remove that remaining dependency risk.

The complete preceding five-provider report, including all 250 observations,
is preserved byte-for-byte in
[the historical report](results/historical/windows-x64-2026-09-10.json).
Its SHA-256 is `aafe061b9a4efc4dc1553b6790bc5cc7dad2b21cf17b6a4da87f80dc8fc79c45`.
[Source commit b13825c](https://github.com/MaksymShostak/oxygen-not-included/tree/b13825c099c1ea715917af8cc67eaea6fff067e3/tools/steam-community-bbcode)
retains the exact historical runner, capability policy and dependency locks.
The current command does not install or execute that comparator, and its archived
results are not included in current provider counts or relabelled as fresh runs.
All 200 retained-provider observations and all 50 case definitions remain identical
to that historical report.

The archived generic converter passed ordinary formatting controls but interpreted
literal Markdown/HTML, split a code block containing its chosen fence, used
content-only underline/colour/noparse handlers, and emitted a table without a GFM
delimiter row in this corpus. These remain historical observations, with no claim
that the generic package implemented a complete Steam dialect.

## Reproduction

Run from the repository root on Windows x64, with the checkout's Python 3.14
`.venv`, Node 24.20.0/npm 12.0.2, and .NET 10 runtime installed. The recorded
Python lock is platform-specific. The converter's ordinary documented dependency
setup must already be complete.

```powershell
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
