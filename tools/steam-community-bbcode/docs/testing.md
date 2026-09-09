# Testing and qualification

Run commands from this package's source checkout with the locked dependencies installed.
`npm run check` combines specification projections, runtime coverage, executed
conformance, TypeScript 7 checking, 100% typed-reference coverage, reproducible
declarations, declaration contracts and actual npm-installed API/CLI consumers.
The repository entry point is `npm run check:converter`. Native ESLint recommended
rules also run without inline configuration or warning allowances.

| Command | Contract |
| --- | --- |
| `npm test` | Focused runtime examples and seeded properties |
| `npm run qualify:parser` | Native grammar foundation and resource contracts |
| `npm run test:coverage` | Both suites under c8/V8, including unloaded runtime source |
| `npm run conformance:check` | Generated forward/reverse policy report matches executed cases |
| `npm run docs:check` | Construct examples and package-local documentation links |
| `npm run test:mutation` | Sequential Stryker library and real-process CLI qualification |
| `npm run test:package` | Packed source, declarations, maps, executable and fresh consumers |

Runtime coverage requires at least 98% statements, lines and functions and 95%
branches. Three JSDoc-only modules with `export {}` are excluded from runtime
measurement because c8 assigns them fictitious unloaded functions. Their contracts
remain subject to primary type/declaration checks. Executable diagnostic arrays
are counted even when unloaded. See `.c8rc.json` for the exact scope.

Both mutation profiles require 90%; neither excludes surviving mutants or uses
checker suppressions. The library uses Stryker's maintained TAP runner with
`node:test` and per-file coverage. The CLI uses its built-in command runner with
real child processes, because in-process TAP coverage cannot establish coverage
inside a spawned CLI. CLI mutation coverage analysis is disabled by that runner's
contract, not inferred from the library score.

Stryker runs disposable sandboxes under `artifacts/`, never in place. Native Node
ancestor resolution supplies the package's installed dependencies without extra
node_modules links. Runtime mutation sandboxes omit compiler configurations:
Stryker 10's config rewriter depends on an API removed in TypeScript 7, and the
JavaScript runner does not consume those files. Primary checking and declaration
generation still use the real TypeScript 7 configurations outside the sandbox.
TypeScript 6.0.3 remains confined to the approved type-coverage package.

Reports live under `artifacts/runtime-coverage`, `artifacts/mutation-library` and
`artifacts/mutation-cli`. Retain original failures and classify survivors before
changing tests. A timeout can count as detected in Stryker's score; it is not an
assertion failure. Host execution may be necessary on Windows where a restricted
process sandbox denies Stryker's cleanup of its own workers. This does not require
changing filesystem permissions, command guards or installed vendor code.

Runtime-error mutants are reported separately and excluded by Stryker's native
score calculation; they are not assertion kills. In the retained library run,
89 fail in mutated native grammar setup/execution, and 11 interrupt the historical
description test's module setup (including one native hit-count limit). Preserve
those reasons alongside survivors and uncovered mutants. The unmutated suite
passes; an invalid instrumented grammar is not a production failure or proof that
an assertion detects a fault.

Scores measure the configured code and tests. They do not prove complete Steam
syntax, rendering fidelity, security approval or stable-release readiness. The
shipped `coverage.json` is a separate **conformance** report, not runtime coverage.
Current numerical results and retained logs belong to the
[execution record](../../../docs/plans/2026-09-08-steam-community-bbcode-execution.md).
