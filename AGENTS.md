# Repository Instructions

## Configuration Safety

- NEVER create, modify, rename, or delete configuration files without the user's explicit approval for the exact change.
- Other approvals do not imply approval to change configuration.
- Configuration files include build, bundler, package, lockfile, lint, formatting, test, CI/CD, container, deployment, hosting, environment, and repository-policy files.
- Before requesting approval, identify the exact file and setting, explain the behavioral and pipeline impact, and propose the smallest change.
- If a task appears to require a configuration change, stop and request approval instead of inferring permission.

# Git Guidance

## Local Workspace Commits & Pushing

- For any request to draft a commit message or commit current workspace changes, you MUST load and follow the `committing-to-git` skill. Unless specified otherwise, use the template for a per-file detailed commit message when drafting a commit message.

- **Explicit User Authorization**:
  - Creating a commit requires explicit user authorization.
  - Pushing requires separate explicit user authorization.
  - A request to push existing commits MUST NOT implicitly authorize staging or committing uncommitted workspace changes.

## Working Tree Safety

Treat all existing working-tree changes as user-owned and potentially valuable.

- Use the current working-tree contents as the authoritative starting point for ordinary file editing.
- Preserve all pre-existing modifications unless the user explicitly requests that they be changed or discarded.
- Treat any change you did not make as deliberate, including content that was present earlier in the session and is now absent. Never restore it, and do not assume a regression, a sync artefact, or a tooling bug; raise it and ask if it materially affects work in progress.
- Edit files directly using minimal, targeted changes.
- Never use `git checkout`, `git restore`, `git reset --hard`, or another Git restoration operation to undo edits made during the current task.
- To undo your own changes, reverse only the specific edits you introduced.
- Use Git primarily to inspect repository state and historical content (`git status`, `git diff`, `git show`) during ordinary editing.
- Execute operations that discard working-tree changes only when the user explicitly requests that destructive operation.

# GitHub Platform Guidance

- Avoid executing destructive Git operations (such as force-pushing to protected branches or deleting remote branches) without explicit, case-by-case approval.

# Repository-owned SDLC

- Read [the SDLC guide](docs/sdlc/howto.md) and, for material work, [the engineering principles](docs/sdlc/engineering-principles.md). The current user-approved task is the authority; reuse recorded approvals within their scope.
- Select the smallest justified R0/R1/R2/R3 route. R0/R1 may use the accepted task or PR brief; ordinary R2/R3 work requires a previously accepted baseline. Do not invent extra Issues, plans or reviewers to fill a template.
- Use the repository-adapted [test-driven-development](.sdlc/skills/test-driven-development/SKILL.md) as the sole implementation procedure. All six SDLC skills are available on demand. Role installation does not authorize delegation against user limits.
- Require semantically precise names, reassessing retained names when responsibility changes. Do not introduce or extend shims without a specific prior user override.
- Before new functionality or material integration, research maintained alternatives and supported configuration/composition/extension. Select current stable or the latest patch of the newest applicable LTS line; inspect exact licences and restrictions. Record the residual custom gap.
- Use consumer-owned parsers, schemas and validators, then independently check the mod/tooling outcome. A passing check or metadata field is not evidence of human acceptance or semantic correctness.
- Use npm entry points for SDLC controls; execute Python through this checkout's `.venv`. Inspect lifecycle side effects before execution. SDLC-only work runs control/setup checks; mod and pipeline work follows the existing [ONI Mod Pipeline](docs/guides/oni-mod-development-workflow.md). Carry exact emitted artifact paths.
- Preserve the .NET toolchain and root MIT/Klei licence. New separately licensed modules need explicit subpackage boundaries. Repository tooling success is not in-game acceptance, Workshop readiness or publication.
- Follow [REVIEW.md](REVIEW.md), native Codex Security when authorized, and existing command protections. Installation, hook trust, live scans, GitHub writes and publication retain their separate authorities.
- Remove only spent task-owned scratch after checking remaining consumers and evidence retention. Read [adoption.md](docs/sdlc/adoption.md) for ONI-specific activation evidence and gaps; do not reuse another repository's deployment as an ONI pass.
