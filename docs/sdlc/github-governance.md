# ONI GitHub governance

R0/R1 may use an accepted task or normal PR brief. Ordinary R2/R3 work requires a
previously accepted baseline. The single lower-case pull_request_template.md
declares seven metadata fields: Change issue, Accepted baseline, Risk class,
Acceptance IDs implemented, Baseline-only, New functionality and Software selection.
Keep the player bug/feature forms; engineering SDLC forms are additional options.

## Trusted policy and bootstrap

SDLC PR linkage uses pull_request_target with read-only contents/issues/PR access.
It checks out only the event's trusted base SHA, reads candidate baseline blobs
through the GitHub API, and never checks out or runs candidate code. Its tests cover
schema/risk/state, immutable baseline versions, renamed paths and head/base freshness.
Reference presence is not acceptance or research quality.

The owner explicitly authorized this bootstrap merge. The initial base lacks the
policy, so this PR cannot prove its own trusted-base check. Establish that check on
a subsequent real PR before requiring it; do not fabricate a successful bootstrap
result, execute head policy with privileged credentials, or create a synthetic pilot.
Re-read live accepted intent before merge; Issue edits are not an atomic transaction
with old PR checks.

## Checks and permissions

SDLC controls runs its matrix only when native Git selects affected control inputs.
The stable SDLC controls gate fails on scope errors or failed selected jobs.
The native pipeline suite runs on Windows only for pipeline inputs and the shared
.NET configuration; it needs no game installation. Mod/game acceptance remains local.
CodeQL analyzes Actions, C#, JavaScript and Python; static findings are separate
from native Codex Security and behavior/runtime verification.

CODEOWNERS names @MaksymShostak, the personal repository owner. It neither grants
access nor supplies an independent reviewer. GitHub prohibits author self-approval.
At preflight main had no protection or required status checks. Preserve actual
settings and report readback; no protection bypass or branch deletion is authorized.

The label helper explicitly targets MaksymShostak/oxygen-not-included so the
upstream remote cannot receive bootstrap writes. Label setup is separate from
development setup. Issue title/body edits invalidate accepted lifecycle labels
without erasing historical evidence.

Dependabot proposes native npm/pip/Actions updates in one daily group, and NuGet
updates for the existing .NET projects. Review versions, licences, scope and tests;
grouping is not acceptance or permission to merge. Existing Windows/ONI toolchains
retain their consumer-owned lockfiles.

The imported security procedure grants no authority for live scans, host artifact
access or publication. Use the actual accepted scope and current native consumer.

## Accepted-plan linkage and actual execution evidence

The committed-plan route supplements the existing Issue snapshot route. Use the
unchanged accepted UTF-8 Markdown file under docs/plans/ already present as a
regular file in the trusted PR base. Add one `Baseline acceptance:` field identifying
the inspectable owner decision. `Acceptance IDs implemented:` is then a nonempty
JSON array of distinct exact lines occurring once in that plan. These references
link evidence; they do not authenticate acceptance or prove implementation.

The native reader acquires pinned commit/tree/blob objects, checks regular file
mode and object identity, bounds transport and decodes strictly. Candidate code
is never executed by the trusted metadata check. Preserve complete change and
rename coverage, exact base/head identities and movement detection. An unchanged
plan in Git is not permission to change its accepted scope.

Use `npm run setup:sdlc:github -- --check-issue-readiness` for the read-only Issue
availability prerequisite. It targets this repository's explicit origin and
installs no labels. Disabled Issues blocks the Issue route, not the supported
plan route; settings changes retain their separate authority. Label installation
without that option remains a separately authorized operation.

Read the actual event, checked-out policy revision, candidate/merge revision,
run/attempt and relevant job results. A rerun of an older event does not select
new policy. Required skipped or missing checks remain gaps. Retain a failing
trusted-policy run while diagnosing its boundary; do not execute candidate policy
with privileged credentials, change acceptance metadata to manufacture a pass,
or bypass branch protection.
