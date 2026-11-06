# Dependency version ownership

Each dependency has one authored version authority:

| Dependency               | Authority                                 | Consumers                                                                                  |
| ------------------------ | ----------------------------------------- | ------------------------------------------------------------------------------------------ |
| npm development packages | Root `package.json`                       | npm-generated `package-lock.json` and `npm ci`                                             |
| npm transitive overrides | Root `package.json` → `overrides`         | Steam Markdown packages reuse direct version declarations; fast-uri has one authored floor |
| npm toolchain            | `package.json` → `packageManager`         | Both Node CI jobs install this selection before `npm ci`                                   |
| Node CI runtime          | `.node-version`                           | `actions/setup-node`; `engines.node` separately describes supported consumers              |
| Python runtime           | `.python-version`                         | The shared Python setup action                                                             |
| uv toolchain             | `.github/actions/setup-python/action.yml` | All Python CI jobs                                                                         |
| .NET SDK and MSTest SDK  | `global.json`                             | All four .NET projects                                                                     |
| NuGet packages           | `Directory.Packages.props`                | Versionless project package references and NuGet-generated locks                           |
| GitHub Actions           | `tools/ci/action-versions.json`           | Literal workflow and composite-action references                                           |

Development package declarations use lower bounds.
Exact resolved versions and integrity hashes remain in the native npm and NuGet lockfiles.
Refresh affected locks with the selected package manager, then verify locked installation and affected consumers.

GitHub requires literal action references.
Their full commit SHAs in workflows and composite actions are checked projections of the catalog, following the single-authority pattern used in the owner's `google` repository.

To update actions, verify the upstream release tag's commit, edit the catalog once, then run:

```powershell
python tools/ci/action_versions.py --write
python tools/ci/action_versions.py --check
```

The checker covers all workflow and composite-action YAML files, including JSON-formatted YAML.
It rejects unregistered dependencies, unused authorities, non-immutable catalog revisions, and projection drift.
CI checks it before selecting dependent jobs.
Dependabot proposals that change literal action references must be reconciled into the catalog and regenerated before they pass this check.

NuGet and MSTest use native central version resolution.
Dependabot includes the repository root to discover these declarations.
SDK-owned implicit test dependencies remain selected by MSTest SDK rather than being duplicated in the central package file.
Native central transitive pinning selects the reviewed DiaSymReader, DependencyModel, and TestPlatform ObjectModel updates for both test projects.
The pipeline includes the root NuGet and SDK declarations in contributing inputs and rejects changes to them during a build.

Markdown Quality's `>=3.0.0` floor intentionally retains the published 3.x line; the registry's older `latest` tag is not a reason to downgrade it.
Steam Community BBCode currently publishes a release candidate, so its existing release-candidate floor is retained until a newer applicable release exists.

Some transitive dependencies remain older than their latest releases: Markdown Quality ships bundled dependencies, and MSTest owns its telemetry dependency graph.
The Steam Community BBCode overrides resolve its older Markdown package pins to the current direct declarations without repeating their version numbers.
The direct dependencies and toolchain selections are current; the entire transitive closure is not.
Those remaining versions require upstream releases or separately reviewed overrides.

The November 6, 2026 investigation confirmed a concrete blocker in the test runner: MSTest's telemetry client calls the three-argument `TelemetryClient.TrackEvent` overload, while the published Application Insights 3.2.0 assemblies expose only one- and two-argument overloads.
See the [MSTest telemetry caller](https://github.com/microsoft/testfx/blob/933377f07ac4bded77e820b0beef7d27e805cd89/src/Platform/Microsoft.Testing.Extensions.Telemetry/AppInsightTelemetryClient.cs) and [Application Insights migration changes](https://github.com/microsoft/ApplicationInsights-dotnet/blob/main/BreakingChanges.md).
A version override alone would leave an incompatible caller.

Markdown Quality's published bundle includes ESLint code that imports Ajv 6's draft-04 schema path and accesses `ajv._opts`.
Updating its bundled Ajv to 8 requires migrating that caller and producing a new upstream package artifact.
[npm's bundled-dependency ownership](https://docs.npmjs.com/cli/v12/configuring-npm/package-json/#bundledependencies) also means that root overrides cannot be treated as proof that shipped bundle contents changed.
A clean isolated npm 12.2.2 installation with a root `minimatch >=11.0.5` override still materialized bundled minimatch 11.0.3 with `inBundle: true`.
That native probe confirmed that a root override did not update the published bundle's contents.

Lib.Harmony.Ref 2.4.3 contains a reference assembly with assembly version 2.4.3.0; the supported ONI runtime contract currently expects Harmony 2.4.2.0.
The owner selected the game-matched Lib.Harmony.Ref 2.4.2; no 2.4.3 compatibility trial was applied.

## Remaining bundled versions observed on November 6, 2026

Native `npm outdated --all --json` reported the following 32 package names inside the published Markdown Quality 3.0.0 bundle after the approved overrides and a fresh locked installation.
It reported no older unbundled npm package copies.
This table records investigation evidence; version authorities remain the manifests described above.

| Bundled package                   | Installed versions | Latest stable |
| --------------------------------- | ------------------ | ------------- |
| `@humanfs/core`                   | 0.19.2             | 0.20.0        |
| `@humanfs/node`                   | 0.16.8             | 0.17.0        |
| `@humanfs/types`                  | 0.15.0             | 0.16.0        |
| `@isaacs/nonrecursive-glob-match` | 1.0.3              | 1.1.0         |
| `@keyv/bigmap`                    | 1.3.1              | 6.1.0         |
| `ajv`                             | 6.15.0             | 8.20.0        |
| `balanced-match`                  | 4.0.4              | 5.0.0         |
| `escape-string-regexp`            | 4.0.0              | 5.0.0         |
| `eslint-visitor-keys`             | 3.4.3              | 5.0.1         |
| `fast-levenshtein`                | 2.0.6              | 3.0.0         |
| `find-up`                         | 5.0.0              | 8.0.0         |
| `hashery`                         | 1.5.1              | 3.0.5         |
| `hookified`                       | 1.15.1, 2.2.0      | 3.1.0         |
| `ignore`                          | 5.3.2              | 7.0.14        |
| `isexe`                           | 2.0.0              | 4.0.0         |
| `json-schema-traverse`            | 0.4.1              | 1.0.0         |
| `keyv`                            | 5.6.0              | 6.1.0         |
| `locate-path`                     | 6.0.0              | 8.0.0         |
| `markdown-table`                  | 3.0.4              | 3.1.0         |
| `mdast-util-gfm-autolink-literal` | 2.0.1              | 2.1.0         |
| `mdast-util-gfm-table`            | 2.0.0              | 2.0.1         |
| `mdast-util-gfm-task-list-item`   | 2.0.0              | 2.0.1         |
| `mdast-util-to-markdown`          | 2.2.1              | 2.2.3         |
| `minimatch`                       | 11.0.3             | 11.0.5        |
| `p-limit`                         | 3.1.0              | 7.3.3         |
| `p-locate`                        | 5.0.0              | 7.0.0         |
| `path-exists`                     | 4.0.0              | 5.0.0         |
| `path-key`                        | 3.1.1              | 4.0.0         |
| `qified`                          | 0.10.1             | 0.13.2        |
| `shebang-regex`                   | 3.0.0              | 4.0.0         |
| `which`                           | 2.0.2              | 7.0.0         |
| `yocto-queue`                     | 0.1.0              | 1.2.2         |

The updated local ONI installation matched both expected runtime hashes.
A fresh merged build and its explicitly bound mod test run passed all 993 tests with no skipped tests, including the five previously failing runtime and build contracts.
