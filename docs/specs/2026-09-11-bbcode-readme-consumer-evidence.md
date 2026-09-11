# Packaged README consumer evidence

The initial record below describes local-tarball qualification before the
implementation was committed. It was based on
`c4e681d719544eb5f2747bcc1b1cbac59bc6b959`. The registry qualification update below
records the subsequent published-package checks; full mod-release acceptance
remains separate.

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

The registry qualification below completes the installed-package boundary. No
provisional local archive dependency was added to the npm manifest or lock.
The full affected verification remains unsuccessful because of the broader mod
test failures. The current local result does not qualify a refreshed hosted RC,
authorize publication, or establish Workshop/in-game acceptance.

The accepted optimistic concurrency limitation is documented separately in
[the owner decision](2026-09-11-bbcode-readme-concurrency-decision.md).

## Registry qualification update — 11 September 2026

The committed implementation is `2fc2ddd2645d7d6e15da800e26b74b5c26c652ef`.
The standalone hosted release run `34577578709` passed its runtime matrix,
mutation, SDLC, CodeQL and archive qualification on
`521088ef851ef2e3da0220b135fed64d5e441ae2`.
Owner-authenticated npm bootstrap published `steam-community-bbcode@1.0.0-rc.1`
under `next`; no GitHub OIDC provenance is claimed for this first publication.

- Registry and hosted archive SHA-256:
  `3f32c0f0f944f02fc9001a3f6be72ec26e2d97f51c16e3c5f1e78ea3d29bd7e3`.
- Registry and installed-lock integrity:
  `sha512-um9+xQK5FuwZ+i6JW6SQZ/O/WudLDCGq1wa95JQR12KfprrrS3gmHcAypaUpiNuy7gXlnusvCy0+lnwD5b+AWQ==`.
- Exact-coordinate installed JavaScript, CLI and declaration consumers passed,
  together with native registry signatures and production audit.
- The actual ONI `sync-readme --check` command passed against an isolated
  registry installation, then again through normal repository package discovery:
  no drift, no writes, no diagnostics, and the same description/README hashes
  recorded above.
- The owner approved the exact development dependency and native npm 12.0.2 lock
  update. All 318 existing lock entries retained their versions.

The destination retains `artifacts/registry-bootstrap-34577578709/`, including
`bootstrap-registry-report.json` and `consumer-report.json`; ONI retains its
isolated lock under `artifacts/registry-readme-consumer-34577578709/`.
The dependency commit and hosted integration checks are separate from these
observed registry results. The four broader mod-test failures above remain
outstanding; this record does not assert Workshop or in-game acceptance.
