# ONI repository SDLC

Use the accepted task and the smallest justified R0/R1/R2/R3 route. Read
[engineering principles](engineering-principles.md) for material work,
[REVIEW.md](../../REVIEW.md) for review, and [adoption.md](adoption.md) for
actual ONI activation evidence. SDLC 1.0.0 remains pre-release.

The six local skills are available on demand. The repository-adapted
test-driven-development is the sole implementation procedure. Existing working
controls use preservation tests; changed executable behavior uses test-first checks.
Role files do not override a user's delegation limits.

## Set up controls

Use Node 24.20.0, Python 3.14.7 and npm 12.0.2. A host with another npm can invoke
the selected npm without changing its global installation:

```text
npm exec --yes --package=npm@12.0.2 -- npm run setup:development
npm run check:sdlc
```

Setup installs locked npm development dependencies with scripts disabled, creates
or preserves this checkout's .venv, installs requirements-sdlc.txt, and activates
local Codex policy and six skills. Existing unrelated configuration and skills
are preserved or rejected for review on an ownership conflict.

The source transaction utilities still live in scripts/set_up_mcp_servers.py.
SDLC setup imports those utilities; it never calls that module's MCP installer.
There is no setup:mcp entry point, ontology product dependency, AWS check, external
skill refresh, shared .venv or automatic Git-hook configuration in ONI setup.
The empty native skills-lock.json records that no external skills are selected.

Codex may require review of the exact generated Stop hook in its native hooks UI.
Generation and check:sdlc do not grant trust. Keep the existing user-level DCG hook.
Never substitute a source repository's hook acceptance for this worktree's evidence.

## Start and verify actual work

R0/R1 can use an accepted task/PR brief. Ordinary R2/R3 work needs a previously
accepted baseline. A public converter API therefore requires its accepted baseline
before implementation. Reuse the committed converter plan rather than duplicate it.

```text
npm run sdlc -- --help
npm run sdlc -- begin correct-help --risk R0 --intent-reference accepted-task-reference --purpose "Describe existing behavior accurately" --no-new-functionality
npm run sdlc -- verify
npm run sdlc -- handoff --help
```

New functionality uses --new-functionality and a completed
--software-selection-reference. A reference's presence is not research or approval.

Focused verification checks whitespace and generated Codex configuration. Affected
and full verification execute all automatic checks for components changed since
the task's startingHead, including staged, unstaged and non-ignored untracked input.
The same automatic floor is used for R1 and R2/R3; elevated routes additionally
require their independent verification, baseline and relevant specialist/manual
evidence. A full-profile pass alone does not satisfy those obligations.

| Changed component | Automatic checks |
|---|---|
| SDLC controls/setup/governance | Python SDLC and setup suites; JavaScript control tests |
| ONI Mod Pipeline | Locked .NET restore and pipeline tests |
| Mod input or the pipeline it consumes | Locked pipeline restore; native validate, build and test |
| Plans and ordinary documentation alone | Focused floor; review the actual content |
| Future converter subpackage | Add its actual build/type/test/package commands and scope during the converter tooling gate |

Use `npm run check:affected -- --base <full-commit-SHA>` outside an active task.
No inferred remote branch substitutes for an explicit baseline. Direct entry points
are `npm test`, `npm run test:sdlc`, `npm run test:setup` and
`npm run test:pipeline`. The last needs locked .NET restore first.

## Preserve ONI lifecycle boundaries

For game/mod work use [ONI Mod Pipeline](../guides/oni-mod-development-workflow.md).
Read its [getting-started guide](../guides/getting-started-with-oni-mod-pipeline.md)
before execution. Carry exact emitted build-result.json paths. Automated control
checks do not install a mod, establish runtime/UI behavior, attest acceptance or
publish to Steam. Root MIT/Klei licensing and the existing .NET package identities
remain authoritative. Keep the converter's AGPL-3.0-only code in its own subpackage.

Source/config changes invalidate local verification. Use explicit pause/resume and
handoff; retain failures under .sdlc/runtime. The Stop hook supports an honest
incomplete handoff. Follow [temporary artefact policy](temporary-artefacts.md);
never clear lifecycle state to manufacture completion.

GitHub label setup is the separate remote action `npm run setup:sdlc:github`.
[GitHub governance](github-governance.md) explains trusted-base linkage and activation.
Native security scans, host/plugin installation, trust and product publication
retain their own authority. Evaluate adoption through useful real work.
