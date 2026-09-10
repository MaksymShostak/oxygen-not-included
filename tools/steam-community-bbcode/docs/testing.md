# Testing and qualification

`npm run docs:api:check` qualifies imported generic JSDoc through the isolated native TypeDoc model and Markdown renderer, then checks all ten generated reference files for freshness.
`npm run docs:api` explicitly regenerates them.
Primary TS 7 checking and the strict 100% metric cover its orchestration script as well as the runtime, tests and other maintenance scripts.

Run commands from this package's source checkout with the locked dependencies installed.
`npm run check` combines specification projections, runtime coverage, executed conformance, TypeScript 7 checking, 100% typed-reference coverage, reproducible declarations, declaration contracts and actual npm-installed API/CLI consumers.
The repository entry point is `npm run check:converter`.
Native ESLint recommended rules also run without inline configuration or warning allowances.

On 9 September 2026 the full package check passed locally on Windows x64 under Node 22.23.2 and 26.8.1, supplementing the existing Node 24.20.0 full verification.
The additional runtimes were unmodified official executables checked against their published SHA-256 lists, with npm 12.0.2 and the same locked dependency graphs.
Each invocation used the selected runtime for its child processes as well.
These runs do not establish Linux compatibility or successful GitHub CodeQL and dependency-review jobs.

| Command | Contract |
| --- | --- |
| `npm test` | Focused runtime examples and seeded properties |
| `npm run qualify:parser` | Native grammar foundation and resource contracts |
| `npm run qualify:performance` | Bounded subprocess regression for opaque text and adjacent formatting |
| `npm run test:coverage` | Both suites under c8/V8, including unloaded runtime source |
| `npm run conformance:check` | Generated forward/reverse policy report matches executed cases |
| `npm run docs:check` | Construct examples and package-local documentation links |
| `npm run docs:format:check` | Native semantic line formatting of authored package guides, without writing |
| `npm run qualify:prose` | Sentence boundaries, literal content, idempotence, native check behavior and generated-file exclusions |
| `npm run test:mutation` | Sequential Stryker library and real-process CLI qualification |
| `npm run test:package` | Packed source, declarations, maps, executable and fresh consumers |

Runtime coverage requires at least 98% statements, lines and functions and 95% branches.
Three JSDoc-only modules with `export {}` are excluded from runtime measurement because c8 assigns them fictitious unloaded functions.
Their contracts remain subject to primary type/declaration checks.
Executable diagnostic arrays are counted even when unloaded.
See `.c8rc.json` for the exact scope.

Both mutation profiles require 90%; neither excludes surviving mutants or uses checker suppressions.
CI requires the dependency-review job to succeed before starting the six ordinary converter jobs.
The full library and CLI profiles then run in separate parallel Linux jobs only after every ordinary check passes.
The dependency-review action runs only for affected pull requests; its job remains eligible on push and manual runs so those events can reach the test stages.
The existing Ubuntu/Node 24 qualification check requires dependency review and both matrices to succeed, and explicitly fails if an upstream stage fails or is skipped.
Superseded PR runs are cancelled within the same workflow and PR; main and manual runs remain independent.
The first remote run spent about 40 minutes on library mutation testing and ten minutes on CLI mutation testing, compared with 40 seconds for ordinary converter checks.
Parallel jobs reduce the expected critical path without reducing mutation scope; runner availability still affects elapsed time.
Incremental mutation-result caching is not enabled: Stryker's command runner cannot detect CLI test changes, and environment and dependency changes require separate invalidation.
The library uses Stryker's maintained TAP runner with `node:test` and per-file coverage.
The CLI uses its built-in command runner with real child processes, because in-process TAP coverage cannot establish coverage inside a spawned CLI.
CLI mutation coverage analysis is disabled by that runner's contract, not inferred from the library score.

Stryker runs disposable sandboxes under `artifacts/`, never in place.
Native Node ancestor resolution supplies the package's installed dependencies without extra node_modules links.
Runtime mutation sandboxes omit compiler configurations: Stryker 10's config rewriter depends on an API removed in TypeScript 7, and the JavaScript runner does not consume those files.
Primary checking and declaration generation still use the real TypeScript 7 configurations outside the sandbox.
TypeScript 6.0.3 remains confined to the two approved metric and docs packages; neither supplies a compiler to a mutation sandbox or primary checking.

Reports live under `artifacts/runtime-coverage`, `artifacts/mutation-library` and `artifacts/mutation-cli`.
Retain original failures and classify survivors before changing tests.
A timeout can count as detected in Stryker's score; it is not an assertion failure.
Host execution may be necessary on Windows where a restricted process sandbox denies Stryker's cleanup of its own workers.
This does not require changing filesystem permissions, command guards or installed vendor code.

Runtime-error mutants are reported separately and excluded by Stryker's native score calculation; they are not assertion kills.
Mutated native grammar setup or module initialization can fail before an assertion runs.
Preserve the native reasons and exact counts in the execution record alongside survivors and uncovered mutants.
The unmutated suite passes; an invalid instrumented grammar is not a production failure or proof that an assertion detects a fault.

Scores measure the configured code and tests.
They do not prove complete Steam syntax, rendering fidelity, security approval or stable-release readiness.
The shipped `coverage.json` is a separate **conformance** report, not runtime coverage.
Current numerical results and retained logs belong to the [execution record](../../../docs/plans/2026-09-08-steam-community-bbcode-execution.md).

The [release workflow](releasing.md) reuses the same installed API, CLI and TS 7 declaration consumers for both retained archives and exact public coordinates.
`npm run release:pack` also retains native production audit, CycloneDX inventory and archive integrity.
Registry acceptance checks cover coexistence of release channels and rejection of wrong coordinates, tag targets, archive bytes, origins and SHA-512 integrity.
The independent public installation and native signature/provenance audit require an actual authorized publication; local fixture checks do not attest those remote operations.
