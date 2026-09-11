# Packaged README consumer evidence

This is an interim local-tarball qualification, not registry adoption or release
acceptance. The implementation is uncommitted on `feature/bbcode-readme-consumer`,
based on `c4e681d719544eb5f2747bcc1b1cbac59bc6b959`.

## Retained inputs and results

The local evidence directory is
`artifacts/readme-consumer/2026-09-11-local-tarball/`. It retains the frozen
32-file implementation snapshot and hashes, tracked patch, installed npm lock,
real-consumer command results, original/final README, and GitHub-rendered HTML.
Artifacts are ignored local evidence and must accompany any later handoff.

- Package: `steam-community-bbcode@1.0.0-rc.1`, installed by native npm with
  scripts disabled, from the retained local tarball.
- Archive SHA-256:
  `ca394c268c130123d7d935216fe8bf13f2c6c7e52853f763daf6ba666c9cea8d`.
- Archive SRI:
  `sha512-ULCFRtu6sWNEisZ3V983suxp8wf/5EnPcNYl59OP0wzflfRsjmajwfrLklN6IZjbGfY0Q2WvfIGu1zvGyVMS/A==`.
- Generated description SHA-256:
  `8bc87a8a4ae00fc21bbe0e816c12f9ccb40e08b69a83fe280ec67b46bd9b627c`.
- Synchronized README SHA-256:
  `5b24d0fe3531d5a67bee0c15a48cc8b5ce462bf31b2efd8194df70e5c32c4270`.

The actual pipeline invoked the installed package's public CLI. A separate mod
copy and native installation with spaces in both paths passed five scenarios:
read-only drift (exit 6), synchronization (exit 0), fresh check (exit 0), unchanged
repeat (exit 0), and unsupported-input rejection (exit 2 with unchanged README).
The fixture independently asserts manual prefix/suffix preservation and visible
credit text. Original owner-checkout files were not edited.

Native listing validation first rejected missing final newlines; those were
corrected in the isolated listing sources. Strict conversion then rejected the
literal `[sd]` and `[Fixed]` credits. Native `[noparse]` markup preserves those
visible names without weakening conversion diagnostics. Successful real-mod
conversion reports an empty diagnostics array.

The owner authorized GitHub `POST /markdown` in GFM mode for this README.
The returned HTML was inspected for text, headings, lists, credit links and images.
A local browser preview of that exact fragment with an explicit UTF-8 wrapper
confirmed Unicode, image display and document structure. This uses GitHub's
renderer with browser-default presentation, not the complete GitHub page theme.
Human acceptance is not inferred from this inspection.

## Verification and review

- Native pipeline suite: 368 passed, zero failed or skipped.
- Jest: 73 passed across five suites.
- Native locked restore, profile/environment validation and mod build passed.
- Broader mod tests: 911 passed, four failed, 38 skipped (953 total).
- Generated SDLC configuration was explicitly approved, generated, and passed
  `npm run check:sdlc`; this does not assert host hook loading or trust.
- Independent correctness review cleared its two findings after the concurrency
  decision and actual `validate --for-release` stale-README regression test.

The four broader failures concern unchanged baseline contracts: an expected mod
version of `2026.9.5` versus metadata `2026.9.8`; two UI tests requiring the absent
`DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH`; and an ASCII-only change-notes
assertion rejecting existing degree symbols. The listing diff adds only final
newlines and native literal-tag escaping, not those degree symbols. These are
source-supported baseline comparisons, not a separately executed baseline suite.
Exact emitted build and TRX paths are in `evidence.json`.

The native security inventory included 16 of the frozen 32 paths, omitting ten
tests and six documentation/listing paths. No scan was launched. The owner then
directed **no review** instead of accepting an alternative security assessment.
No security-clearance claim is made. Inventory evidence is retained separately
under `security-precheck/` in the local evidence directory.

## Remaining release gates

Registry installation and repeat qualification remain pending; no provisional
local archive dependency was added to the repository's npm manifest or lock.
The full affected verification remains unsuccessful because of the broader mod
test failures. The current local result does not qualify a refreshed hosted RC,
authorize publication, or establish Workshop/in-game acceptance.

The accepted optimistic concurrency limitation is documented separately in
[the owner decision](2026-09-11-bbcode-readme-concurrency-decision.md).
