# ONI SDLC toolchain selection

Checked 2026-09-08 against native registries, installed metadata and primary sources.
The source selection is retained where still current. This is development tooling;
the .NET mod/pipeline package identities and root MIT/Klei notice are unchanged.

| Consumer | Selection | Basis and rights |
|---|---|---|
| Node.js | 24.20.0 | [Newest LTS patch](https://nodejs.org/en/about/previous-releases); host matches; preserve distribution notices |
| npm | 12.0.2 | [Registry release](https://registry.npmjs.org/npm/12.0.2), Artistic-2.0; invoked with npm exec; global npm unchanged |
| Python | 3.14.7 | [CPython release](https://www.python.org/downloads/release/python-3147/), PSF terms; independent ONI .venv |
| Jest | 30.5.1 | [Registry release](https://registry.npmjs.org/jest/30.5.1), MIT text inspected; preserves source tests without a runner rewrite |
| yaml | 2.9.0 | [Registry release](https://registry.npmjs.org/yaml/2.9.0), ISC text inspected; workflow parser in tests |
| jsonschema | 4.26.0 | [PyPI](https://pypi.org/project/jsonschema/4.26.0/), MIT text inspected; Draft 2020-12 and FormatChecker |
| PyYAML | 6.0.3 | [PyPI](https://pypi.org/project/PyYAML/6.0.3/), MIT; safe native metadata parsing |
| Format providers | rfc3339-validator 0.1.4, rfc3986-validator 0.1.1 | [RFC3339](https://pypi.org/project/rfc3339-validator/0.1.4/) / [RFC3986](https://pypi.org/project/rfc3986-validator/0.1.1/), MIT; activate the schema's date/URI contract |
| .NET | Existing global.json | 10.0.400/latestPatch, no prerelease, Microsoft.Testing.Platform; locked NuGet restore |
| Native host controls | Existing DCG 0.14.1 and exposed Codex Security 0.1.23 | No installation or global configuration change; actual authority/acceptance in adoption.md |

No external Skills CLI is needed for six local skills; no unrelated source product
dependency is imported. Python direct requirements are exact pins; pip resolves the
transitive closure. This is not a hash lock. The installed closure is retained with
local evidence; dependency refresh uses native Dependabot and ordinary review.

GitHub Actions retain the source's commit pins. setup-dotnet v6.0.0 resolves to
a98b56852c35b8e3190ac28c8c2271da59106c68. Native CodeQL covers Actions, C#, JavaScript
and Python using build-mode none. Static analysis does not replace ONI runtime checks.
