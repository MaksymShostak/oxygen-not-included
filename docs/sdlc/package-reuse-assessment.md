# ONI reuse and rights decisions

The accepted handoff selected reuse of Universal Ontology's working SDLC.
Source commit, hashes and the retained MIT notice are in .sdlc/UPSTREAM.json.
The source's adapted TDD skill retains its own MIT licence/provenance.

| Requirement | Reused owner and compared alternative | Residual ONI work |
|---|---|---|
| SDLC state, schemas, verification and PR linkage | Existing tested Python controls vs a JavaScript rewrite | Keep controls and .venv; npm exposes routine commands without a new state engine |
| Transactional setup | Existing set_up_mcp_servers.py configuration transaction vs extraction/reimplementation | Import its tested functions; no MCP/AWS/product installer entry point or invocation |
| Local skills | Existing native layout and local activation vs external refresh | Empty external lock accepted only for explicit local-only activation; preserve unrelated skills |
| Check selection | Existing native Git pathspec/diff selector vs duplicate path/glob parsing or an Actions-only filter | ONI path sets and native Git working-tree/untracked queries; local and CI share the same paths |
| Tests | Existing unittest/Jest preservation suite vs conversion to another runner | Latest selected Jest/yaml; tests of actual ONI boundary changes |
| Issue/PR governance | Native GitHub forms, read-only trusted-base linkage and CODEOWNERS vs a new workflow service | Keep player forms, one PR template, personal owner and actual main protections |
| Dependencies | Native Dependabot vs a second update bot | npm/pip/Actions batching plus NuGet updates; no auto-merge or global tool install |
| Mod lifecycle | Existing ONI Mod Pipeline vs replacement SDLC build/deploy commands | Delegate validate/build/test and exact artifacts to the native pipeline |

Primary registry/runtime and licence references are in
[toolchain-selection.md](toolchain-selection.md). The root's npm package is private
tooling under MIT; dependencies are development-only. The future converter retains
its planned AGPL-3.0-only subpackage boundary. No restricted runtime is copied into
the SDLC package and no registry package is published by adoption.

## DCG owner decision

The source record reports Max's prior authorization to use DCG, followed by the
operator's 0.14.1 installation. The ONI handoff explicitly says to preserve that
installation and its protections. The [retained upstream identity](../../.sdlc/dcg/UPSTREAM.json)
and upstream licence contain a non-standard OpenAI/Anthropic rider; do not call it
plain MIT or infer an author-issued licence exception. No binary is redistributed
or new operator configuration activated here. ONI observations and limits are in
[adoption.md](adoption.md#command-safety).

## Consumer validation

jsonschema owns JSON Schema, tomllib owns TOML, safe YAML owns metadata parsing,
and Git owns change-path matching. Cross-record intent/freshness checks remain
repository policy. Native Codex hooks and roles were refreshed against
[hooks](https://learn.chatgpt.com/docs/hooks) and
[subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents).
Parseability is not hook trust, model compliance, GitHub permission or human approval.
