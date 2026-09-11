# ONI SDLC bootstrap handoff

Continue the user-authorized SDLC adoption in this existing worktree. Read its AGENTS.md first and recheck Git state.

## Workspace and source

- Implementation worktree: C:\Users\maksy\GitHub\oxygen-not-included-worktrees\sdlc-bootstrap
- Branch: chore/oni-sdlc-bootstrap
- Starting commit: 1fb1351c6991838d91c7747404985892868e1e04
- Main checkout: C:\Users\maksy\GitHub\oxygen-not-included
- Origin: https://github.com/MaksymShostak/oxygen-not-included.git
- Upstream remote also exists: https://github.com/llunak/oni-deliverytemperaturelimit.git. Do not publish this work there.
- SDLC reference checkout: C:\Users\maksy\GitHub\universal-ontology
- SDLC reference commit: b3984ffbfe9b38cca7bd4570aeb3f5bc0fa6f20e
- Reference SDLC: 1.0.0, pre-release, deployed in Universal Ontology. That deployment evidence does not establish ONI host acceptance.

The ONI main checkout was clean and matched origin/main at the last check. The plan and AGENTS.md are now committed. The worktree was created successfully using Git worktree add. No SDLC implementation, dependencies, configuration edits, commits, pushes or GitHub writes have happened in it. This handoff is the only new file.

Universal Ontology now contains the unrelated, user-owned untracked file docs/specs/2026-09-08-mcp-improvement-change-dossier.md. Preserve it. Do not modify Universal Ontology as part of this adoption.

## Accepted sequence and authority

The user requested the following SDLC adoption:

Embed the working Universal Ontology SDLC into ONI, with adjustments, in this worktree; verify and merge it into main.

The user explicitly prioritised a consolidated, proportionate bootstrap: land the already-working, non-conflicting SDLC components together, then resolve real ONI edge cases. Do not repeat the prolonged Universal Ontology adoption, invent a distribution framework, or run a synthetic adoption pilot.

The latest explicit approvals were:

> I pre-approve all configuration changes as they are required by the plan and/or SDLC implementation.
> Similarly pre-approve commits and pushes to the new SDLC worktree for ONI.

These approvals supersede repeated exact-configuration and commit/push confirmation requests within that scope. Still produce reviewable diffs, use the committing-to-git skill, preserve unrelated work, use signed commits, verify signatures and the exact published branch, and report results. Do not treat the old read-only discovery restriction as current.

The accepted sequence authorizes the SDLC merge; follow the repository's actual GitHub protections. No force pushes, protection bypasses, branch deletion, purchases, global trust/ACL changes or unrelated publication is authorized. Global tool/plugin changes and permission-sensitive live operations remain separate. The standing no-subagents instruction has not been lifted for this adoption; earlier narrowly scoped reviewer exceptions are spent.

The user requested switching conversations so this work proceeds in an ONI-rooted task. Do not create another bootstrap worktree.

## Established technical findings

- ONI already owns a .NET 10 toolchain and ONI Mod Pipeline. global.json selects SDK 10.0.400, latestPatch, no prereleases, Microsoft.Testing.Platform.
- The pipeline is tools/oni-mod-pipeline/OniModPipeline.slnx. Its tests are tools/oni-mod-pipeline/tests/OniModPipeline.Tests. Use existing commands and contracts; read the getting-started and development guides before executing lifecycle commands.
- The root has no package.json, .venv, .codex or .agents directory at the starting commit. Keep the mod pipeline and its package identities intact.
- ONI has existing Issue Forms and a lower-case .github/pull_request_template.md. Merge conventions and keep one discoverable PR template. No tracked Actions workflows were found at discovery.
- .gitignore currently ignores packages/ for NuGet.
- Root LICENSE is MIT with an ONI/Klei notice. Preserve it.
- No custom core.hooksPath or active non-sample .git/hooks files were found before worktree creation. Recheck before changing shared Git configuration: linked worktrees share repository configuration by default.
- Universal Ontology scripts/sdlc.py uses _repository.derive_repo_from_script(__file__). Its scripts/runRepositoryPython.js selects its own repository root and .venv. Calling those source scripts by absolute path would operate on Universal Ontology, not ONI. Copy/adapt the needed controls into the consuming repository.
- scripts/set_up_sdlc.py and scripts/set_up_agent_skills.py reuse the proven configuration transaction utilities inside scripts/set_up_mcp_servers.py. Resolve this real dependency deliberately; do not accidentally install or configure ontology/AWS/MCP services. Avoid a speculative rewrite merely to remove an internal import.
- Keep the working Python controls where justified, expose routine commands through appropriate npm entry points, and use an ONI-local virtual environment. Do not share Universal Ontology's .venv or product dependencies.
- Keep generic guidance distinct from ONI-specific verification commands and release boundaries. Preserve source licences, provenance and the sole adapted TDD procedure. All six SDLC skills are available on demand; do not turn them into six mandatory ceremonies.

## Useful source files already located

Read these directly from the reference checkout; do not repeat broad discovery:

- AGENTS.md, REVIEW.md; docs/sdlc/howto.md, engineering-principles.md, proportional-workflow.md, codex-security.md, command-safety.md, dcg-acceptance.md, temporary-artefacts.md, github-governance.md and adoption.md.
- .sdlc/skills/ (six selected skills, including the complete adapted test-driven-development and its licence/provenance/references).
- .sdlc/codex/ (configuration, eight roles, rules and Stop-hook source).
- .sdlc/pipeline-policy.json, verification.json, skill-policies.json, schemas/ and dcg/.
- scripts/sdlc.py, _sdlc_state.py, sdlc_stop_gate.py, validate_sdlc_pr.py, bootstrap_github_sdlc.py, probe_dcg_hook_protocol.py, _commands.py, _repository.py.
- scripts/set_up_sdlc.py, set_up_agent_skills.py, set_up_mcp_servers.py, runRepositoryPython.js, setUpDevelopmentEnvironment.js, configureGitHooks.js and selectPullRequestChecks.js.
- tests/sdlc/, tests/test_set_up_agent_skills.py, tests/test_set_up_mcp_servers.py and the focused JavaScript setup/PR-control tests.
- .github/workflows/sdlc-control-tests.yml, sdlc-pr.yml, sdlc-issue-acceptance.yml, codeql.yml; .github/dependabot.yml, CODEOWNERS and Issue/PR templates.

Reference versions currently declared: Node 24.20.0, Python 3.14.7, npm 12.0.2; requirements-sdlc.txt pins jsonschema 4.26.0, PyYAML 6.0.3, rfc3339-validator 0.1.4 and rfc3986-validator 0.1.1. Refresh relevant primary-source/version/rights facts proportionately before selecting dependencies. Do not import the ontology package.json/lockfile or its product dependencies wholesale.

Reference setup installs npm dependencies with scripts disabled, creates .venv, installs Python requirements, activates local SDLC files, and checks for AWS CLI. Remove the irrelevant product requirements/AWS check from the ONI adaptation. Setup is not permission to run a live security scan or publish.

The reference SDLC PR linkage workflow uses pull_request_target with read-only permissions and executes only trusted default-branch policy; do not check out or execute candidate code there. Bootstrap the policy on main before claiming this check ran. GitHub required checks/settings need their own truthful activation/readback. ONI is a personal repository; do not copy Hadden-Industries team ownership entries.

## Verification and proportionality

Reuse existing passing preservation tests for imported controls; do not manufacture RED for already-working code. Use test-first checks for changed executable behavior. Keep source hashes and record ONI-specific adaptations.

Use affected scopes: SDLC edits run control/setup tests; mod/pipeline edits run the relevant existing .NET/pipeline checks. Broad integration changes can widen coverage. Do not run every ontology, every gameplay scenario or an expensive security scan merely because the source SDLC had those commands.

Native Codex Security remains the security workflow. The installed 0.1.23 plugin's Windows scan-directory artifact access defect has a qualified, narrow procedure in docs/sdlc/codex-security.md; use its bounded review/troubleshooting budgets, native inventory checks and explicit host-artifact authority. Do not repeat old diagnostic scans or call old Universal Ontology evidence an ONI pass.

DCG 0.14.1 and the custom hard-reset pack are already installed on this host. User-level trust/control installation is distinct from repository files. Preserve all existing protections and qualify relevant worktree behavior without destructive probes or bypasses. The earlier esbuild parent-directory access repair was successfully qualified; do not reopen that completed investigation without a new failure.

## Next action

Proceed with the consolidated bootstrap under the user's pre-approvals. Do not stop for another generic configuration manifest approval. Draft the concrete adaptation, implement it in this worktree, run appropriate existing/control/integration checks, resolve actual failures, and deliver through a reviewed signed commit/PR/merge. Ask only for a genuinely missing authority or decision.
