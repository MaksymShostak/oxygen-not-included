# ONI bootstrap verification

Source reference: Universal Ontology b3984ffbfe9b38cca7bd4570aeb3f5bc0fa6f20e.
ONI starting revision: 1fb1351c6991838d91c7747404985892868e1e04.
Environment observed 2026-09-08: Windows, Node 24.20.0, Python 3.14.7,
.NET 10.0.400, DCG 0.14.1. The global npm is 11.19.0; selected npm 12.0.2
is invoked through native npm exec without a global install.

## Test chronology

- Preserved imported tests for already-working controls; no artificial RED.
- New setup test failed because requirements.txt was mandatory.
- New local-only activation test failed because the empty external lock was rejected.
- ONI path-routing tests failed for the lower-case PR template, PR validator,
  mod/.NET inputs and ontology-only check names.
- Local working-tree selector first failed its new export contract, then passed
  staged/untracked selection and ignored-runtime-evidence cases.
- Corrected a Windows test fixture to write canonical LF bytes; the installer
  intentionally publishes LF. Corrected the imported nested ignore fixture to use
  tools/example because ONI's existing packages/ rule intentionally ignores NuGet.
- First passing control results: 92 Python SDLC tests; 82 setup tests with one
  POSIX-only execute-mode test skipped on Windows; JavaScript control and selector suites.
- npm audit reported zero advisories for the selected lock. npm reports a deprecated
  transitive glob package; no override was added to the consumer's dependency graph.

## Qualified local results

`npm exec --yes --package=npm@12.0.2 -- npm run setup:development` completed
using this checkout's dependencies and virtual environment. `npm run check:sdlc`
passed. The generated config also validated against the native Codex 0.153.4
configuration schema; schema validation does not establish host loading.

`npm run sdlc -- verify` selected SDLC and pipeline scopes. Its first run passed
the Python/JavaScript checks, then failed because the sandbox could not read the
existing user NuGet.Config. That failed run is retained. The host-access rerun
passed on 2026-09-08 with unchanged verification inputs:

- 94 Python SDLC tests passed, including origin-specific GitHub destinations.
- 82 Python setup tests ran: 81 passed and one POSIX execute-mode case was skipped.
- 73 JavaScript tests passed in five suites.
- Locked .NET restore passed; all 334 existing pipeline tests passed.
- Whitespace and generated-configuration checks passed.

Runtime records preserve command output, failures, task identity and input digests
under the ignored `.sdlc/runtime/` directory. Final CI results and exact publication
revisions belong to the bootstrap PR and its merge record, rather than a predicted
commit ID in its own tree.

This is control/setup evidence, not in-game qualification, independent review,
native security-scan completion, Stop-hook trust or required-check activation.

## Windows Stop-hook repair, 2026-09-09

The accepted Windows repair handoff was applied to ONI starting at
`83f482ed691e6a05e4fc6868acc198e7e2541c42`, in the existing
`oxygen-not-included-worktrees/sdlc-bootstrap` checkout. This is an R1 control
bugfix using the accepted task as its brief and the repository-adapted TDD loop.
The working tree was clean before this repair. The tested two-file repair was
subsequently applied to the normal checkout and BBCode worktree under the owner's
exact activation approval. BBCode's additional committed plan-baseline tests were
retained by replacing only the hook-test block and fixture prefix. Its existing
working-tree edits, staging and active lifecycle state were preserved.

The regression now invokes the generated Windows command with the raw
outer-quoted `COMSPEC /C` boundary used by
[Codex 0.153.4](https://github.com/openai/codex/blob/rust-v0.153.4/codex-rs/hooks/src/engine/command_runner.rs).
Its real Git fixture has spaces in its path and starts the hook in a nested cwd.
The Node probe reports the actual root, forwarded arguments and JSON stdin, then
returns the requested exit status. Both 0 and 2 are required. This probe replaces
only the downstream Python launcher; shell execution and root resolution are real.
Existing tests separately exercise the actual evidence gate and continuation rule.

`npm run test:sdlc -- -k test_native_hook_preserves_repository_root_arguments_stdin_and_exit_status`
first failed both cases with exit 1: cmd treated `$sdlcRepositoryRoot` as a command
and passed literal `(Join-Path` text to Node. This is behavioural RED before the
renderer edit, not an import or environment failure. After explicitly selecting
`powershell.exe -NoLogo -NoProfile -NonInteractive -Command`, the same test passed.
The PowerShell body, POSIX command, repository Python launcher, gate semantics and
30-second hook timeout are preserved. No dependency, wrapper script or execution
policy override was added.

A separate exact-shell replay from this checkout's `scripts` directory reached
the real `.venv` Python launcher and evidence gate: missing required evidence
returned 2, then `stop_hook_active: true` returned 0 with an explicit incomplete
message. This is command replay evidence, not native host acceptance.

`npm run check:affected -- --base 83f482ed691e6a05e4fc6868acc198e7e2541c42`
passed on Windows with Node 24.20.0, npm 12.0.2 and this checkout's Python 3.14.7:
94 Python SDLC tests; 82 setup tests (81 passed, one POSIX-only case skipped);
73 JavaScript tests in five suites. The selected scope was SDLC controls only.
No mod or .NET pipeline input changed, so no mod/pipeline run was required.

The actual desktop executable and PATH CLI both reported Codex 0.153.4. Its own
generated app-server schemas validated the native discovery, configuration and
acceptance interfaces. Initial discovery found no Stop hook in any ONI context;
the normal checkout had no generated `.codex` files or `.venv`. The initial
`npm run sdlc -- verify` failed on stale generated configuration. An attempted
regeneration was rejected by automatic approval review; that failure is retained.

After the user approved the exact local setup and matching native trust scope,
`npm run setup:development` prepared the normal checkout through its existing
owner. `npm run setup:sdlc -- --configuration-only` regenerated both worktrees.
All three `npm run check:sdlc` invocations passed, and all 13 changed generated
documents matched the approved byte hashes. No package or dependency version,
execution policy, DCG setting or unrelated configuration was changed.

Native `hooks/list` then selected the normal checkout's `.codex/hooks.json` for all
three contexts. The one reviewed key was
`C:\Users\maksy\GitHub\oxygen-not-included\.codex\hooks.json:stop:0:0`, with native
definition hash `sha256:c01c8d754bcfc23761e769c2801d72db35e95a495488febfda56a18a40d6de80`.
The native configuration method trusted only that definition. Fresh readback
confirmed enabled/trusted status in all contexts; unrelated user settings and
the existing DCG hook remained unchanged.

The normal checkout's genuine R1 `npm run sdlc -- verify` passed with 94 SDLC tests,
81 setup passes plus one POSIX-only skip, and 73 JavaScript tests. Its current
evidence was the positive control for a disposable native turn in the still
unverified linked worktree. Native `hook/started` and `hook/completed` events showed
the worktree Stop first **blocked**, then **completed** with an explicit incomplete
warning on continuation. Its feedback named the worktree's Git root. The normal
checkout's current-evidence turn **completed** with no hook warning or failure.
This demonstrates worktree evidence selection despite loading the hook definition
from the normal checkout. Probe prompts allowed text only and prohibited tools,
edits and delegation; their repository and evidence states were unchanged.

The BBCode checkout's own `npm run test:sdlc` passed all 99 tests, retaining its
additional baseline coverage. Its existing unverified task also produced native
**blocked** then **completed** events with an incomplete warning, preserving its
active record and working-tree status. Its active task remains owned by that task;
this repair does not convert its evidence into a product acceptance claim. Consult the
retained native probe records for each context and the final linked-worktree
verification/current-evidence result. No commit, push, merge, CI, mod/in-game or
Workshop qualification is implied by this local activation.

Raw RED/GREEN, affected-check and failed lifecycle logs, source identities, native
inventory/schema, trust readback and exact activation preview are retained under
`.sdlc/runtime/stop-hook-repair/`. Its task-owned native inventory script is retained
with the acceptance harness until the final readback and lifecycle disposition;
spent working copies are removed after their scripts and evidence are retained.
The failed lifecycle record is preserved; it is not converted into a pass.
