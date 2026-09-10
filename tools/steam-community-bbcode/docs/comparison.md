# Comparison evidence

The maintained comparison executes 50 authored cases: 37 forward and 13 reverse.
Five providers yield 250 observations, including unsupported directions.
These cases exercise documented capabilities and literal-text preservation; they are not a representative workload or a performance benchmark.

The source checkout's `comparison/README.md` describes applicability and native reproduction.
`comparison/results/windows-x64.json` retains exact tool versions, inputs, expected trees, stdout/stderr, exceptions, parsed output and classifications.
Those development artifacts are deliberately absent from the packed runtime.
Run `npm run comparison:run` only after installing the isolated comparator graphs described there; ordinary checks do not require those environments.

The current recorded candidate meets all 50 corpus contracts, including explicit preservation where GFM or the qualified Steam subset cannot express a feature.
Steamify 2.0.1, bbcode-to-markdown 1.0.3, Steam Editor Tools 0.5.1 and the BUTR converter 1.0.0.29 are invoked unmodified through documented APIs.
A project is compared only where its capability evidence makes the case applicable.

The isolated legacy Node graph selects unmodified `form-data` 2.5.6 through a native npm override for Request 2.88.2.
This maintainer-provided backport fixes the two advisories raised by remote dependency review while all 250 comparison outputs and semantic results remain unchanged.
The raw report records the changed dependency-selection hashes; no comparator source was patched.
The same isolated graph selects unmodified `nwmatcher` 1.4.4 beneath `jsdom-nogyp` 0.8.3 to address GHSA-6394-6h9h-cfjg from the subsequent dependency-review run.
Request also selects unmodified `qs` 6.16.0 to address GHSA-6rw7-vpxm-498p and GHSA-4mjr-xmp4-gh2g from run 34524391937.
All 250 observations remain identical after this change, including output, diagnostics, consumer trees and classifications.
The provider's public API and comparison corpus remain unchanged.
See [software selection](software-selection.md) for the remaining legacy advisory scope.

Exact output and semantically equivalent output are equally successful results.
Diagnosed loss also requires the authored expected fallback tree: a warning does not excuse incorrect output.
No weighted score or general superiority claim is derived from this selected corpus.
Several failures can share one underlying cause, and a difference from this package's policy is not necessarily an upstream bug.
Alternatives have capabilities outside this package's reverse subset.

Forward outputs use the native GFM parser.
Reverse comparison additionally uses this package's Steam consumer, which limits its independence.
Inspect retained raw output and qualify an independent reproducer before reporting an upstream defect.
The results do not establish live Steam rendering or global coverage of the alternative projects.
