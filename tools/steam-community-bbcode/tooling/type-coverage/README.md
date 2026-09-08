# Isolated type-coverage tool

This private development package runs the maintained `type-coverage` CLI using
the compiler API in unmodified `typescript@6.0.3`. The user approved this exact
version exception on 2026-09-08. See [the selection decision](../../docs/software-selection.md).

TypeScript 7.0.2 remains mandatory for primary checking, declaration generation,
and installed-consumer tests. Coverage is supplementary; its pass cannot replace
those checks. No package alias, source patch, compatibility shim, or suppression
is used. Reassess at dependency refresh, before release, or when the maintained
tool supports the primary compiler's stable API.

Install here with `npm ci --ignore-scripts`, then use the parent package's
`npm run type-coverage`. The tool's project argument selects the parent authored
source, scripts and runtime tests; it does not check a separate copied source tree.
The native `--not-only-in-cwd` option includes that project from this isolated
tool directory. `--is 100` also rejects an empty result, for which the tool's
`--at-least` comparison alone would otherwise succeed on `NaN`.

This authored tooling package is AGPL-3.0-only. Its dependencies retain their own
licences. It is excluded from the converter tarball.
