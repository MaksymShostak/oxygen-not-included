# Packaged BBCode README Consumer Implementation Plan

> For agentic workers: execute the accepted slices sequentially using the repository-adapted .sdlc/skills/test-driven-development/SKILL.md. Repository procedure and the user's delegation limits take precedence over generic planning templates.

**Goal:** Qualify the release candidate through a real ONI Mod Pipeline workflow that synchronizes generated Workshop description GFM into a bounded README region.

**Architecture:** .NET orchestration invokes the installed package's public CLI through the existing process runner. A dedicated README component owns only the declared marked block; release checks remain read-only.

**Tech Stack:** Existing .NET 10/System.CommandLine/System.Text.Json, Node 24.20.0, npm 12.0.2 and steam-community-bbcode 1.0.0-rc.1. No additional parser, shim or NuGet dependency.

**Spec:** ../specs/2026-09-11-bbcode-readme-consumer.md

## Constraints and prerequisites

- Risk R2; accepted design is the owner's migration-conversation approval on 11 September 2026. Commit this baseline before implementation and start the native lifecycle task with its acceptance reference.
- Obtain exact approval for the configuration batch identified in the spec; never record a provisional local archive path in the permanent registry lock.
- Preserve the owner's main-checkout edits, frozen extraction source, root MIT/Klei licence, .NET toolchain and all existing dependency resolutions.
- Use the adapted TDD procedure, retaining actual red/green results. Use native locked .NET restore and npm pipeline test entry points; no test configuration change is required.
- Keep signature/publication evidence separate from installed-consumer and README semantic acceptance.

## Task 1: Profile and bounded README synchronization

**Files:** add ModProfiles/ReadmeProfile.cs and Readme/ReadmeDescriptionBlock.cs under tools/oni-mod-pipeline/src/OniModPipeline; update ModProfiles/ModProfile.cs, ModProfileLoader.cs and ModProfileValidator.cs. Add corresponding profile/block tests under tools/oni-mod-pipeline/tests/OniModPipeline.Tests.

**Consumes:** native profile parsing and a successful conversion value. **Produces:** optional repository-relative README contract and exact replacement/check result.

- [ ] Add failing tests for an optional [readme] table, repository-path and unchanged old profiles.
- [ ] Implement the smallest native profile extension; use optional record state without changing unrelated constructor responsibilities.
- [ ] Add failing tests for one valid marker pair, prefix/suffix byte preservation, newline handling and idempotence.
- [ ] Implement exact bounded replacement; test missing/repeated/reversed markers and unsafe paths. No converter is needed for these pure tests.

## Task 2: Installed CLI and native command

**Files:** add Readme/InstalledBbcodeConverter.cs and Readme/ReadmeSynchronizer.cs; update Cli/CliApplication.cs and Diagnostics/DiagnosticCatalog.cs. Add Readme and Cli/SyncReadmeCommandTests.cs tests.

**Consumes:** generated description bytes, installed package manifest/bin, existing IExternalProcessRunner and README block contract. **Produces:** typed synchronization evidence, diagnostics and status.

- [ ] Establish a failing command test showing sync-readme is absent; confirm it fails for that behavior rather than an unavailable runtime.
- [ ] Reuse listing validation/rendering and create an owned generated description file; compare bytes with native listing assembly in tests.
- [ ] Resolve the declared public bin with name/path checks; invoke Node without a shell and pass arguments as separate native process arguments.
- [ ] Parse successful CLI JSON value and diagnostics. Reject nonzero exits and malformed results before any README write.
- [ ] Implement sync-readme and --check, with optional installed converter-package location for isolated/native installations and default repository node_modules discovery.
- [ ] Test real valid GFM, lossy rejection, missing runtime/package, cancellation, markers and concurrent README changes. Verify failures leave original bytes intact and successful repeat runs are unchanged.

## Task 3: Release freshness and documented opt-in

**Files:** update Cli/CliApplication.cs, ReleaseCandidates/ReleaseCandidatePreparer.cs, SourceControl/RelevantSourceSet.cs and their existing test suites; update README.md, docs/guides/oni-mod-pipeline-profile-reference.md and docs/guides/preparing-oni-mod-releases.md. Apply approved mod profile and npm declaration changes at the authorized stages.

**Consumes:** read-only synchronization check and configured profile. **Produces:** release-not-ready on drift, contributing-source documentation coverage, and a normal development command sequence.

- [ ] Add failing tests for stale README rejection in release validation and preparation; verify no source/candidate mutation.
- [ ] Apply the freshness check at both entry paths and preserve behavior for profiles without [readme].
- [ ] Include the configured README and applicable npm manifests in source cleanliness/fingerprinting without including node_modules.
- [ ] Wrap only the isolated worktree's description region in the approved markers; preserve support/translations/development text.
- [ ] Document synchronization before commit and read-only release checks, installation requirements and structured diagnostic handling.

## Task 4: Real packaged-consumer qualification

**Files:** add consumer acceptance tests/evidence orchestration under the existing pipeline test tree, with durable results in ignored artifacts; update active documentation with actual results only.

**Consumes:** exact retained tarball and a real generated mod description. **Produces:** AC-001 through AC-007 evidence, with registry verification initially pending.

- [ ] Use a clean consumer directory, native npm installation with scripts disabled and no converter source imports. Retain the native installed lock and verify exact archive integrity and package version.
- [ ] Exercise actual pipeline sync-readme using a copy of the current real mod description and a README with preserved manually maintained sections. Hash original inputs and record their source checkout separately from the isolated code revision.
- [ ] Independently inspect expected text, headings, lists, links and images; render with GitHub's GFM surface and retain reviewable output. Do not use converter output as its own golden oracle.
- [ ] Test path-with-spaces execution, repeatability, read-only drift detection and a deliberate unsupported/lossy input that cannot alter README contents.
- [ ] Run native pipeline regression tests and required affected SDLC/mod checks. Obtain independent R2 review for the frozen integration and relevant process/file-write security evidence.
- [ ] After separately authorized bootstrap, install exact registry steam-community-bbcode@1.0.0-rc.1 with native npm, retain URL/SRI, repeat real pipeline acceptance and remove all provisional transport assumptions before merge/adoption.
- [ ] Record acceptance or specific failures. Keep npm/GitHub release publication and source retirement blocked until their separate gates are satisfied.
