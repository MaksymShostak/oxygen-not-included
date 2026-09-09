# Releasing

The release implementation reuses owlapi's candidate, checksum and public-registry verification code.
The exact source revision and retained controls are recorded in [software selection](software-selection.md).
The package is still a private development package at version 1.0.0; that number does not identify a public release.

## Qualify a candidate

From this package's source checkout with the locked development tools installed, run:

```sh
npm run check
npm run release:pack
```

The second command prints a new directory under `artifacts/release-candidates/`.
It builds declarations with TS 7, packs through native npm and installs that archive into a temporary consumer.
The existing authored JavaScript, CLI, declaration contracts, licence and package-boundary checks run against the installed package.
Native npm audits the actual installed production graph with the same low-severity floor as dependency review and emits its CycloneDX SBOM.
The candidate retains the archive, native pack inventory, consumer report, audit, SBOM, Git revision and dirty-state marker, archive SHA-256/SHA-512 integrity, and `SHA256SUMS`.

Use `npm run release:pack -- --output artifacts/my-candidate` to choose an empty directory.
The empty-directory requirement prevents mixing files from different attempts; it does not impose an immutable-release policy.
Keep the printed archive with its evidence through publication and registry verification.
Candidate qualification does not resolve the outstanding renderer, availability or release-review obligations in [testing](testing.md).

## Release acceptance

Prepare the frozen candidate with an agreed public identity and version.
Confirm every registered construct has an executed policy, source evidence and documented example, including the partial reverse limitations and current comparison results.
Complete independent R2 verification and applicable security review against the actual candidate.
The accepted plan and execution record retain the complete gates.

Require current Node 22/24/26 checks on Windows and Linux, CodeQL and dependency review, meaningful property/resource checks and review of generated HTML paths. Run `npm run test:mutation` for the configured library and CLI profiles, each requiring at least 90%, or retain equivalent results for unchanged source and tests. Preserve failures, survivors and native timeout/error counts; the numerical score does not establish semantic correctness or security approval.
Local package checks cannot substitute for remote CI or independent review.

Review the native archive allowlist and actual installed files, declaration maps pointing to shipped source, exact full dependency licence terms and retained notices.
Keep AGPL-3.0-only explicit and exclude tests, comparison environments, credentials and local artifacts from the distribution.
The release production SBOM supplements the existing main and isolated tooling inventories; SBOM metadata is not legal clearance.
The independent pre-release legal review remains required by the accepted plan.
Keep the original comparison and qualification evidence with the approved release.

## Publish an approved release

The repository workflow **Steam Community BBCode release** has a manual `publish` input, defaulting to false.
Its default run qualifies and retains a candidate without publishing.
Before publication, approve and commit the actual public package version and metadata, complete the release obligations, and configure npm's trusted publisher for `MaksymShostak/oxygen-not-included`, workflow `steam-community-bbcode-release.yml`, environment `npm-release`.
The binding must allow direct `npm publish`; a binding limited to staged publishing cannot authorize this workflow.
If npm requires an initial package before that binding can be created, the owner must arrange that first publication separately.
No bootstrap token or credential is stored by this implementation.

Dispatch the reviewed revision with `publish: true` and the intended distribution tag, such as `next` for a release candidate or `latest` for an approved stable release.
The private development flag and a dirty candidate prevent publication.
The publisher downloads the same run's artifact by ID, checks its native artifact digest and `SHA256SUMS`, and publishes its exact archive with native npm OIDC/provenance.
Only that job receives OIDC permission, and it executes no checked-out project code or package lifecycle scripts.
The workflow does not create an immutable GitHub release or impose a single distribution channel.

## Verify the registry

A separate job without publication credentials fetches the exact public version and requested tag.
It verifies that the registry archive has the retained SHA-256 and published SHA-512 integrity, then installs the exact coordinate with a fresh npm cache.
The installed lockfile must bind the package to the same archive integrity before the existing API, CLI and TS 7 consumers run.
Native `npm audit signatures` verifies available registry signatures and provenance, and native production audit runs again.
Other distribution tags may coexist.

To repeat this read-only verification from a qualified source checkout:

```sh
npm run release:verify-registry -- --candidate artifacts/my-candidate --tag next --output artifacts/registry-verification
```

Retain the registry verification artifact with the candidate.
If publishing succeeds but verification fails, diagnose or rerun the verification job against that same candidate; do not republish the version to recover a verification failure.
Live trusted-publisher configuration and a successful public-registry run are required before claiming operational publication.
