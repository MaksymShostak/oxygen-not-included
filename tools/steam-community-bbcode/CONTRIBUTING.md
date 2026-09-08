# Contributing

This package is AGPL-3.0-only from its first implementation. Keep its licence
boundary separate from the enclosing MIT/Klei repository and retain dependency
notices. Do not add an "or later" grant.

Follow the repository's accepted baseline and adapted TDD procedure. Add an
independent semantic or type-contract fixture before changing executable behavior.
Use the native JSON Schema consumer for registry structure and the domain reference
checks for identities; schema validity is not source evidence or conformance.

Use JavaScript ES modules with checked JSDoc and explicit relative `.js` imports.
See [JavaScript and types](docs/javascript-and-types.md) for the compiler roles and
declaration lifecycle. Public declaration changes are API changes. Do not patch
dependencies, add compatibility shims, suppress checks or relax failing assertions.

Install both locked development environments as described in the README, then run
`npm run check`. Use the enclosing repository's SDLC verification entry point for
the complete affected route. A local pass does not authorize a commit, push, npm
release, GitHub protection change or Steam publication.
