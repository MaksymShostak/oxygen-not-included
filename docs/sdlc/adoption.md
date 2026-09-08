# ONI SDLC adoption

Authority: [the accepted bootstrap handoff](../plans/2026-09-08-sdlc-bootstrap-handoff.md)
and Max's current instruction to complete and merge this bootstrap before creating
the converter worktree, with required configuration changes, signed commits and
pushes pre-approved. This is a consolidated repository bootstrap, not a product release.

## Source and adaptation

Imported from Universal Ontology commit
`b3984ffbfe9b38cca7bd4570aeb3f5bc0fa6f20e`. The complete source-file hash list is
[UPSTREAM.json](../../.sdlc/UPSTREAM.json); the upstream MIT notice is retained in
[UPSTREAM-LICENSE](../../.sdlc/UPSTREAM-LICENSE). The adapted TDD skill retains its
own licence and provenance. SOURCE_PACKAGE.json is historical upstream package
identity, not an ONI deployment manifest. Root LICENSE remains unchanged.

Preserved Python controls, schemas, Stop protocol, safe configuration transactions,
six local skills, eight optional roles and the native command/security procedures.
ONI adaptations:
- private npm controls package with Jest/yaml only; no ontology product dependencies;
- .venv setup omits requirements.txt and AWS; local-only activation accepts an empty
  external skill lock without weakening the external refresh preflight;
- native Git selectors use ONI paths and include working-tree/untracked input;
- full means full relevant obligations; mod and pipeline commands keep their native owner;
- one lower-case PR template retains ONI impact questions; player Issue Forms survive;
- CODEOWNERS names the personal repository owner; CI adds Windows pipeline tests and
  C# to native CodeQL; ordinary control checks run on Windows and Linux;
- no source Git pre-commit hook is copied because it validates ontology files.
  Shared core.hooksPath, main's checkout, global trust and external tools are preserved.

The source set_up_mcp_servers.py is retained as the owner of tested transaction
utilities. SDLC code imports those functions only. Its product installers are not
exposed or invoked by ONI setup; preservation tests isolate their external effects.

## Verification and bootstrap review

See [verification.md](verification.md) for actual commands and results and
[SDLC-BOOTSTRAP-01.md](SDLC-BOOTSTRAP-01.md) for completion checkpoints.
The user retained a no-subagents constraint. Bootstrap review is a same-session
diff/principles/security-boundary review plus deterministic tests and GitHub CI.
It is not an independent human or model review or a live Codex Security scan.
The owner's explicit bootstrap merge authority applies only to this adoption.

## Command safety

DCG 0.14.1 was already installed. No binary, operator configuration, pack, allowlist
or user hook is changed here. During this ONI task the native PreToolUse hook
attributed a denial to `core.git:git-alias-semantic-unverified` for a variable-based
read-only Git listing inside the proposed copy command. The command did not run.
A literal read-only listing followed by a checked copy succeeded without changing
the guard. This is a real observation of this desktop dispatch path, not proof of
every destructive spelling, stdin path or worker surface.

The inert protocol assets retain their upstream identities. Any additional host
acceptance uses [dcg-acceptance.md](dcg-acceptance.md). Source-repository acceptance
does not become ONI acceptance. Preserve sandbox restrictions on unproved surfaces.

## Codex Security Windows artifact access

The imported [procedure](codex-security.md) describes the source repository's
bounded investigation of plugin 0.1.23 and upstream issue 43791. Its prior scan IDs
and host-authority decisions are historical evidence, not authorization for a new
ONI scan or host artifact operation. No diagnostic scan is repeated in this bootstrap.
Obtain the actual scan scope/authority when security assessment is requested.

## Activation boundaries

Generated local files, native hook trust, actual model compliance, GitHub workflow
execution and required-check enforcement are separate facts. No model-compliance
pilot is claimed. The Stop hook remains subject to native exact-definition trust.

On 2026-09-08 the generated ONI and source hooks.json files had identical SHA256
`bc8e4ff5ce584afaadca01e12231d701c42d2884cbedeb07afdd3544e9bd7e87`.
The inspected user configuration recorded the source repository's Stop-hook trust,
but no entry for this ONI worktree. This file digest is not the native hook-definition
trust hash. The user reported no pending hook in the desktop app. Setup creates
files; it does not create a desktop approval request. The documented native review
interface is `/hooks` in the CLI launched in the target worktree; project-layer
trust is also required. See [OpenAI's hook documentation](https://developers.openai.com/codex/hooks).
ONI host activation therefore remains unverified, without a fabricated pending
approval or a change to user-level trust. Direct SDLC verification remains usable.

GitHub main was unprotected at preflight. The trusted-base linkage workflow first
needs this bootstrap on main; the bootstrap PR cannot establish its own trusted
policy pass. Preserve the bootstrap distinction and verify linkage on subsequent
real work before making that check required. No protection bypass or branch deletion
is authorized. Repository deployment status remains qualified in PACKAGE_STATUS.json.
