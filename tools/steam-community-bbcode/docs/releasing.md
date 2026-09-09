# Release qualification

This package remains private. Its development `1.0.0` label is not a stable release
claim. Publishing, npm ownership and trusted-publisher setup require separate
owner authorization. Do not remove `private`, invent credentials or publish as a
side effect of checking the package.

Prepare a frozen candidate with an agreed public package identity and version.
Confirm every registered construct has an executed policy, source evidence and
documented example; inspect partial reverse limitations and current comparison
results. Complete independent R2 verification and applicable security review.
The source checkout's accepted plan and execution record retain the full gates.

Run from the package source checkout:

```text
npm run check
npm run test:mutation
npm run build
npm run test:package
npm pack --dry-run --ignore-scripts
npm run --silent sbom
npm run --silent sbom:type-coverage
```

`check` includes registry/doc freshness, runtime coverage, primary TypeScript 7
checking, isolated 100% type coverage, reproducible declarations and installed
consumers. Mutation profiles separately require at least 90%. Preserve failed
runs, surviving mutants, timeout/error counts and scope; scores alone do not
establish semantic correctness or security approval.

Review the native archive allowlist and actual packed files. Ship authored ESM,
generated declarations/maps, source specifications, conformance evidence and
package documentation/notices. Maps must resolve to shipped source. Exclude tests,
local mutation/coverage artifacts, credentials and comparator installations.
Review full direct and transitive licence terms from the exact locked graph;
SBOM metadata is an inventory, not legal clearance. Keep AGPL-3.0-only explicit.

Require current Node 22/24/26 checks on Windows and Linux, CodeQL and dependency
review results, meaningful property/resource tests and review of every generated
HTML path. Check the exact Action commit pins and scoped permissions. Local
Windows results cannot substitute for remote CI or the independent review gates.

Configure the approved npm package to trust the exact GitHub repository, workflow
and any selected environment through npm Trusted Publishing. That is a platform
binding, not something a checked-in workflow can establish by itself. Follow the
[native npm guide](https://docs.npmjs.com/trusted-publishers/) for OIDC publishing
and provenance prerequisites. Use scoped permissions and an owner-approved release
trigger; avoid a long-lived publication token. No publisher binding or publication
workflow has been activated for this private candidate.

After an authorized publication, verify the exact registry version, archive
integrity, installed consumer behavior and provenance. Native
[`npm audit signatures`](https://docs.npmjs.com/cli/v12/commands/npm-audit/) checks
registry signatures and provenance; it is separate from vulnerability auditing.
Retain immutable comparison and qualification evidence with the approved release.
