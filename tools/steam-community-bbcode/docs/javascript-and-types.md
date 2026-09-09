# JavaScript and checked contracts

Executable source, CLI code, tests and maintenance scripts use JavaScript ES modules and JSDoc.
TypeScript 7.0.2 checks authored JavaScript and generates the public declaration files; consumers execute the shipped JavaScript directly.

`tsconfig.json` checks source, scripts and runtime tests without emission.
`tsconfig.declarations.json` emits `.d.ts` and `.d.ts.map` from `src/` into the ignored `types/` directory.
Edit the owning JavaScript contract, never the generated declaration.
Declaration maps must resolve to the source included in the tarball.

`npm run test:types` checks positive and negative consumer fixtures against freshly generated declarations.
`npm run types:check` compares two clean declaration builds.
`npm run test:package` packs with npm 12, installs the actual archive into an isolated temporary consumer, and checks ESM loading and declaration resolution.
The installed JavaScript smoke executes parsing, MDAST interpretation and GFM serialization and the partial reverse API, including diagnosed fallbacks.
Declaration fixtures cover the public APIs and nested/media extension contracts.
The consumer check also invokes the installed npm binary with offline resolution for help, stdin conversion and the shipped conformance report.
CLI source has a native Node shebang and requires no compiler or loader at execution time.

`npm run type-coverage` uses the approved isolated TypeScript 6.0.3 environment.
It measures the parent project's authored JavaScript at 100%, with no exclusions or suppressions beyond the declared generated/comparison-directory boundary.
TypeScript 7 remains authoritative for primary checking and declaration emission.
The extra typing metric does not establish Steam syntax or semantic coverage.

`npm run docs:api` uses unmodified TypeDoc 0.28.20 and TypeScript 6.0.3 only in `tooling/api-docs`, under the separate approved docs qualification.
Native `typedoc-plugin-markdown` 4.13.0 renders the reference.
A maintained imported generic fixture checks native type identities and rendered comments; the check compares generated Markdown bytes with the committed reference.

The docs orchestration script has its own strict config because TypeDoc's native API declarations require the standard DOM WebAssembly types.
The parent's TS 7 executable checks it through `typecheck:api-docs`; DOM globals do not enter runtime or consumer checking.
The isolated metric also requires 100% for that script.
No TS 6 compiler generates the package declarations or checks consumers.

Public declarations reuse mdast/unist contracts.
Their definition packages are runtime dependencies so fresh consumers receive them; the compiler and internal Node definitions remain development-only.
Parser-vendor types must remain inside the syntax boundary and must not escape through public API declarations.
