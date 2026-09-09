# Security policy

This policy covers the `steam-community-bbcode` Node.js package, its CLI and its development/release tooling.
The package is under qualification and has no stable public release.
Its AGPL-3.0-only boundary is this directory; the repository's `.NET` mods and root licensing have their own contracts.

## Reporting a vulnerability

Use the repository's [private vulnerability report](https://github.com/MaksymShostak/oxygen-not-included/security/advisories/new).
Include the package version or commit, Node version, API or CLI invocation, smallest useful input, observed result and security impact.
Keep sensitive data and exploit details in that private report.
The channel was verified enabled on 9 September 2026.
No response-time or supported-release commitment is implied.

## Trust boundaries and required properties

Treat BBCode, Markdown, URLs, labels, attributes and nested structure as untrusted.
The application calling the library chooses its resource limits.
The CLI operator chooses a file path or stdin; input document text must never select files to read.
The repository-maintained specification and locked tools are trusted build inputs, subject to supply-chain review.

- Library conversion must perform no network requests, filesystem access, shell execution or dynamic evaluation.
  Resource URLs remain data; conversion must not authenticate with Steam/GitHub or fetch images, linked content or metadata.
- Source text must not become executable HTML or active target markup by escaping an opaque region.
  Native MDAST serialization owns Markdown escaping.
  The only generated raw HTML is the fixed spoiler details/summary wrapper; source attributes must not enter that HTML.
- Unsafe resource destinations must remain literal with diagnostics.
  Scheme and control-character handling must not normalize a rejected destination into an active link.
  Reverse conversion must preserve unsupported source without injecting active Steam tags.
- Configured input, attribute, node and depth bounds must fail with explicit diagnostics and an empty conversion result.
  Reverse output overflow must also produce an empty result.
  Invalid JavaScript options must be rejected.
- CLI input must be bounded while reading and decoded as valid UTF-8.
  Errors must have nonzero exit status; output and diagnostics must remain separate.
  A fidelity failure deliberately retains converted output, so adopting it requires checking the exit status.
  The CLI has no output-file overwrite option.
- Build and publication inputs must preserve the lock graph, separate compiler roles, generated-declaration provenance and package allowlist.
  Remote publication and credentials remain separate authorities.

## Findings and limitations

Assess reachable consequences: source-triggered execution or I/O, unsafe active links/HTML, delimiter injection, resource-limit bypass, or compromise of the shipped artifact are reportable concerns.
Severity depends on the actual caller, renderer and deployment.
A diagnosed presentation loss can be an intended conversion policy; it is not evidence that an injection or availability problem is safe.
No finding class is waived by this document.

The default 1-MiB input limit is configurable and does not establish a CPU deadline or bound every allocation in native parsers.
Reverse node/depth checks run after GFM parsing; forward conversion also invokes a native GFM consumer. The [security model](docs/security-model.md) records these limits.
Applications exposed to hostile concurrent requests need their own workload and process controls.

Tests, coverage scores and a local package check do not establish independent security approval, current Steam/GitHub rendering, CI success or release readiness.
Development comparison providers and live renderer qualification are explicit tools, not behavior of the published library.
