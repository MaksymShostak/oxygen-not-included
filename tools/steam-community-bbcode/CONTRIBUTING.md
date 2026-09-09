# Contributing

This package is AGPL-3.0-only from its first implementation.
Keep its licence boundary separate from the enclosing MIT/Klei repository and retain dependency notices.
Do not add an "or later" grant.

Follow the [Code of Conduct](CODE_OF_CONDUCT.md) in converter community spaces.
It identifies the private reporting channel, initial moderator and conflict-of-interest process.
The adapted Contributor Covenant policy text retains its CC BY-SA 4.0 licence; converter code remains AGPL-3.0-only.

Follow the repository's accepted baseline and adapted TDD procedure.
Add an independent semantic or type-contract fixture before changing executable behavior.
Use the native JSON Schema consumer for registry structure and the domain reference checks for identities; schema validity is not source evidence or conformance.

Use JavaScript ES modules with checked JSDoc and explicit relative `.js` imports.
See [JavaScript and types](docs/javascript-and-types.md) for the compiler roles and declaration lifecycle.
Public declaration changes are API changes.
Do not patch dependencies, add compatibility shims, suppress checks or relax failing assertions.

Install the three locked development environments described in the README, then run `npm run check`.
Use the enclosing repository's SDLC verification entry point for the complete affected route.
A local pass does not authorize a commit, push, npm release, GitHub protection change or Steam publication.

Edit source JSDoc to change the API reference and run `npm run docs:api`.
`npm run docs:api:check` qualifies imported generics and compares native generated Markdown with the committed reference.
Supporting source modules in that reference explain referenced types; the package exports map still defines its public surface.

Use semantic line breaks for authored package guides: one sentence per source line, with editor soft wrapping for long sentences.
Install the hash-locked native formatter in the checkout's `.venv` as described in the README, then run `npm run docs:format`.
`npm run docs:format:check` enforces the same result without writing and is included in `npm run check` and CI.
The gate covers package-root Markdown and authored `docs/**/*.md`; generated references and the conformance and support projections remain owned by their generators.
Code blocks, tables and explicit Markdown hard breaks retain their meaning.
The formatter uses deterministic sentence detection; review language and unusual abbreviations as normal prose.
