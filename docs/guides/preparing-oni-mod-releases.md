# Preparing ONI mod releases

Turn reviewed, committed mod source into one immutable release candidate, bind human acceptance to the exact installed bytes, and prove that the candidate is ready for a deliberate manual ONI Uploader handoff.

[Workflow overview](oni-mod-development-workflow.md) · [Getting started](getting-started-with-oni-mod-pipeline.md) · [Developing ONI mods](developing-oni-mods.md) · [Profile reference](oni-mod-pipeline-profile-reference.md) · [Troubleshooting](troubleshooting-oni-mod-pipeline.md)

> [!WARNING]
> A release candidate is not a mutable staging folder. Never edit its content, repair its hashes, replace its evidence, reinstall it after a receipt exists, or reuse its run ID. Correct tracked source or workflow defects and prepare a new candidate.

## Review every release input

Release preparation begins with source review, not with `prepare-release`. Deliberately inspect and commit every input that contributes to runtime content, Workshop listing content, automated evidence, acceptance evidence, or provenance.

For Delivery Temperature Limit, complete this checklist:

- [ ] Set the intended release version in `mod_info.yaml`.
- [ ] Confirm the title and static ID in `mod.yaml`.
- [ ] Confirm `supportedContent`, `minimumSupportedBuild`, and `APIVersion` in `mod_info.yaml`.
- [ ] Review the stable listing text in `STEAM_DESCRIPTION.bbcode`.
- [ ] Replace `STEAM_CHANGE_NOTES.bbcode` with the reviewed notes for this update.
- [ ] Review `Preview.png`, mod types, and DLC compatibility declared by `oni-mod-pipeline.toml`.
- [ ] Review the package allowlist and the expected runtime inventory.
- [ ] Review every required automated test and human acceptance check.
- [ ] Confirm all project and package lock files represent the intended dependency closure.
- [ ] Commit every contributing source change.

The metadata and listing source files remain authoritative. ONI Mod Pipeline validates, renders, hashes, and records them, but it never derives or increments a version and never rewrites tracked Workshop text.

Tracked BBCode uses BOM-free UTF-8 with LF line endings for stable review. Candidate preparation creates separate CRLF files for direct use with the Windows Uploader.

## Prove the release scope is clean

Inspect the repository before preparation:

```text
git status --short
oni-mod-pipeline validate --mod mods/delivery-temperature-limit-supercooled --for-release
```

Release validation requires every contributing path to be:

- beneath the intended mod or contributing in-worktree pipeline scope;
- tracked by Git;
- present in the selected commit; and
- clean in both the index and working tree.

Contributing paths include the profile, metadata, declared package sources, build entry point, relevant project and lock files, declared tests, Workshop sources, preview, and in-worktree ONI Mod Pipeline source used to prepare the candidate.

Unrelated changes outside that contributing set do not alter the candidate's source identity, but they remain user-owned work and should still be understood. Never discard unrelated changes merely to obtain a clean release scope.

## Run current automated tests

Run the declared test suite before candidate preparation:

```text
oni-mod-pipeline test --mod mods/delivery-temperature-limit-supercooled
```

This gives developers a direct failure path before the all-or-nothing preparation operation. `prepare-release` runs the required locked build and automated tests again and captures its own exact evidence; it does not import an arbitrary earlier development results directory.

## Prepare one immutable candidate

Run:

```text
oni-mod-pipeline prepare-release --mod mods/delivery-temperature-limit-supercooled
```

Preparation performs one all-or-nothing operation:

1. reload and validate the strict profile and ONI metadata;
2. resolve and validate the environment;
3. require clean, committed contributing source;
4. perform locked restore and an isolated Release build;
5. run every declared test project and require successful TRX evidence;
6. assemble only the package allowlist into Workshop runtime content;
7. render the description and change notes into Uploader-compatible CRLF artifacts;
8. copy and validate the declared preview;
9. calculate file hashes and one canonical release-content digest;
10. record build, source, SDK, game-reference, listing, test, and acceptance-plan provenance;
11. render the initial `awaiting-acceptance` summary, checklist, and readiness report; and
12. atomically promote the candidate only after every stage succeeds.

A successful command prints the exact candidate directory, content digest, and state. The directory is created beneath:

```text
<artifacts>/release-candidates/<static-id>/<version>/<run-id>/
```

There is no dirty bypass, skip-test option, candidate reuse option, overwrite option, or publish option.

## Inspect the candidate before installation

The candidate separates uploadable content from lifecycle evidence:

```text
<candidate>/
  workshop-content/
    <only runtime files declared by package-files>
  workshop-listing/
    description.bbcode
    change-notes.bbcode
    preview.<ext>
  release-evidence/
    acceptance-test-plan.json
    automated-test-results/
    build-provenance.json
    release-content-manifest.json
    release-readiness-report.json
    release-summary.md
    uploader-checklist.md
```

Before installing, open these generated files:

- `release-evidence/release-summary.md` for the version, commit, digest, build, tests, tags, DLC compatibility, and absolute handoff paths;
- `release-evidence/release-content-manifest.json` for every content path, byte length, SHA-256, role, and canonical digest;
- `release-evidence/acceptance-test-plan.json` for the immutable human checks; and
- `release-evidence/uploader-checklist.md` for the current lifecycle state and exact final form mappings.

Confirm that the candidate identifies the intended commit and version, contains only the intended runtime and listing files, reports every required automated test as passed, and remains in `awaiting-acceptance` before installation.

## Map candidate areas to the Uploader correctly

| Candidate object | Uploader destination | Rule |
| --- | --- | --- |
| `workshop-content/` | **Update Data** | Select this exact directory. It contains only the runtime package. |
| `workshop-listing/description.bbcode` | **Description** | Open the generated file in current Windows Notepad and copy it directly. |
| `workshop-listing/change-notes.bbcode` | **Change Notes** | Copy the generated CRLF artifact, not the tracked LF source. |
| `workshop-listing/preview.<ext>` | **Preview** | Select the exact validated preview path from the checklist. |
| `release-evidence/` | Nowhere | Never upload lifecycle evidence as mod data or listing content. |

Never use a mutable `mods/Dev` or `mods/Local` installation as **Update Data**. The candidate's `workshop-content` is the digest-bound upload input.

## Prepare ONI for unambiguous acceptance

Before candidate installation:

1. Exit ONI.
2. Identify every subscribed, Dev, or Local copy with the same static ID.
3. In ONI, disable the subscribed Workshop copy and any competing local copy so the candidate will be the only enabled implementation under test.
4. If the intended Local directory already exists and is unowned or hand-maintained, move it aside manually rather than asking the pipeline to adopt or erase it.

ONI Mod Pipeline can detect duplicate subscribed copies and report `ONIP2005`, but it never changes Steam subscriptions, Workshop directories, or ONI's enabled-mod state.

## Install the exact candidate once

Pass the exact directory printed by `prepare-release`:

```text
oni-mod-pipeline install --candidate <exact-candidate-directory> --target local
```

Candidate installation:

- re-verifies the candidate layout, content manifest, and canonical digest;
- stages only `workshop-content` beneath the profile's managed directory name;
- applies the same ownership guard used by development installation;
- verifies the staged and live installed inventories and hashes;
- writes `.oni-mod-pipeline-owner.json` into the managed installation; and
- creates `release-evidence/installation-receipt.json` with create-new semantics.

The receipt records the candidate identity, content digest, target, absolute destination, installation time, and successful installed-file verification. A candidate may receive only one installation receipt. If a receipt already exists, installation fails before changing the destination.

## Perform every human acceptance check

Use the immutable plan at:

```text
<candidate>/release-evidence/acceptance-test-plan.json
```

For every check:

1. follow the recorded setup;
2. perform the recorded action;
3. compare the observed behavior with the recorded expected result;
4. exercise an unchanged control where the check calls for one; and
5. retain a concise factual note useful to future reviewers.

Complete save/load scenarios when declared. After gameplay checks, inspect the current `Player.log` for expected initialization plus relevant Harmony, Unity lifecycle, and unhandled-exception messages.

Automated tests do not substitute for human acceptance. A human may explicitly attest a result based on their own completed observation, but the record must identify that evidence source truthfully rather than implying that the pipeline or another operator performed the check.

## Verify the Windows Uploader text representation

Candidate description and change-note artifacts are BOM-free UTF-8 with CRLF line endings and exactly one final CRLF. Their logical and artifact hashes are recorded in build provenance.

Complete the required representation check without publishing:

1. Open `<candidate>/workshop-listing/description.bbcode` in current Windows Notepad.
2. Open the authenticated ONI Uploader's **Edit Mod** form.
3. Leave every update checkbox disabled.
4. Select all text in Notepad and copy it directly into **Description**.
5. Confirm that paragraphs, blank lines, `---`, headings, and `[list]` blocks remain on separate lines.
6. Record the Notepad and Uploader versions in the acceptance note.
7. Cancel the form without selecting **Publish**.

Do not copy the tracked LF source, pass generated text through another editor or formatter, or save changes into the candidate. Use the same direct-copy rule for generated change notes during the final handoff.

## Record acceptance exactly once

In an interactive terminal, run:

```text
oni-mod-pipeline record-acceptance --candidate <exact-candidate-directory> --tester <display-name>
```

For each immutable check, enter `passed` or `failed` plus an optional factual note. The recorder verifies before and after prompting that:

- candidate content still matches the manifest and canonical digest;
- the acceptance plan still matches its provenance hash;
- the installation receipt belongs to this candidate;
- the live ownership marker identifies the same static ID and digest;
- the installed directory contains exactly the declared runtime inventory plus the ownership marker; and
- every installed runtime file matches the candidate by length and SHA-256.

The command writes `release-evidence/acceptance-test-results.json` with create-new semantics. It never overwrites an existing result.

<details>

<summary>Understand which candidate files can change during the lifecycle</summary>

- Preparation creates immutable content and foundational evidence, plus initial derived readiness documents.
- Candidate installation creates `installation-receipt.json` exactly once.
- Acceptance recording creates `acceptance-test-results.json` exactly once.
- Verification may atomically regenerate only `release-summary.md`, `uploader-checklist.md`, and `release-readiness-report.json` from revalidated inputs.
- No lifecycle command edits `workshop-content`, `workshop-listing`, build provenance, the content manifest, automated TRX results, or the acceptance plan.

</details>

If an answer was entered incorrectly or any check failed, preserve the result. Correct the tracked source, documentation, test setup, or workflow as appropriate, commit the correction, and prepare a new candidate. Never edit or delete acceptance evidence to make an old candidate pass.

## Verify upload readiness deterministically

Run:

```text
oni-mod-pipeline verify-release --candidate <exact-candidate-directory>
```

Verification rechecks the candidate layout, content manifest and digest, build provenance, source-cleanliness evidence, required TRX results, generated listing bytes, preview, installation receipt, ownership marker, live installed inventory and hashes, acceptance-plan hash, and recorded results.

It derives one lifecycle state:

| State | Meaning | Next action |
| --- | --- | --- |
| `awaiting-acceptance` | Installation or acceptance evidence is not complete. | Complete the missing lifecycle step for this exact candidate if its write-once preconditions still permit it. |
| `acceptance-failed` | At least one required acceptance check was recorded as failed. | Preserve the candidate, fix the cause in tracked source or process, and prepare a new candidate. |
| `ready-for-upload` | All immutable content, evidence, installed bytes, and required acceptance checks verify. | Inspect the generated handoff documents and perform the deliberate human Uploader review. |
| `verification-failed` | Evidence or content is missing, inconsistent, unsafe, or tampered. | Preserve the candidate and diagnose the exact failure. An irreversible invalidation cannot be rehabilitated. |

Only `ready-for-upload` exits successfully. Every consistent verification run deterministically regenerates:

- `release-evidence/release-summary.md`;
- `release-evidence/uploader-checklist.md`; and
- `release-evidence/release-readiness-report.json`.

Run verification twice when rehearsing the lifecycle or proving deterministic evidence, and run it again immediately before the final Uploader handoff. The candidate digest and derived evidence hashes must remain unchanged when no input changed.

## Complete the manual Uploader handoff

Open the candidate's generated `uploader-checklist.md` and check each item against the visible authenticated form:

- [ ] Candidate state is `ready-for-upload`.
- [ ] **Update Data** points exactly to the candidate's `workshop-content` directory.
- [ ] The displayed data path is not a mutable Dev/Local installation.
- [ ] **Description** comes from the candidate's generated `description.bbcode`.
- [ ] Paragraphs, blank lines, `---`, headings, and `[list]` blocks remain separate after paste.
- [ ] **Change Notes** comes from the candidate's generated `change-notes.bbcode`.
- [ ] **Preview** comes from the exact generated preview path.
- [ ] Title, mod types, tags, and DLC compatibility match `release-summary.md`.
- [ ] The complete form is reviewed immediately before publication.

> [!IMPORTANT]
> **Publish** is a deliberate authenticated human action. ONI Mod Pipeline does not open, populate, submit, automate, simulate, or record publication. A rehearsal ends by cancelling the form. An actual release ends only when the authenticated account holder independently decides to select **Publish**.

## Recover without corrupting evidence

| Failure point | Preserve | Correct response |
| --- | --- | --- |
| Release validation | Working tree and diagnostics | Review the exact contributing dirty or untracked path, make an intentional source decision, and commit before preparing. |
| Candidate preparation | Failed run artifacts, when present | Fix the tracked build, test, profile, listing, or environment issue and rerun preparation. No partial final candidate is reusable. |
| Candidate inspection | Entire candidate | Correct tracked source and prepare a new candidate. Do not edit candidate content. |
| Candidate installation | Candidate, destination, and diagnostic | Resolve ownership or environment safely. If a receipt already exists, use a new candidate rather than reinstalling. |
| Human acceptance | Candidate, installation receipt, and recorded result | Fix the underlying issue and prepare a new candidate. Do not overwrite the write-once result. |
| Verification | Candidate and readiness report | Diagnose the named inconsistency. Preserve irreversible invalidation and create a new candidate after fixing tracked source or workflow. |
| Uploader review | Candidate and generated handoff documents | Cancel without publishing, correct tracked source, and prepare a new candidate if any candidate input is wrong. |

For diagnostic IDs, exit codes, and evidence locations, continue to [Troubleshooting ONI Mod Pipeline](troubleshooting-oni-mod-pipeline.md).
