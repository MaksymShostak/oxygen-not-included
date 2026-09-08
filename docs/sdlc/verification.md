# ONI bootstrap verification

Source reference: Universal Ontology b3984ffbfe9b38cca7bd4570aeb3f5bc0fa6f20e.
ONI starting revision: 1fb1351c6991838d91c7747404985892868e1e04.
Environment observed 2026-09-08: Windows, Node 24.20.0, Python 3.14.7,
.NET 10.0.400, DCG 0.14.1. The global npm is 11.19.0; selected npm 12.0.2
is invoked through native npm exec without a global install.

## Test chronology

- Preserved imported tests for already-working controls; no artificial RED.
- New setup test failed because requirements.txt was mandatory.
- New local-only activation test failed because the empty external lock was rejected.
- ONI path-routing tests failed for the lower-case PR template, PR validator,
  mod/.NET inputs and ontology-only check names.
- Local working-tree selector first failed its new export contract, then passed
  staged/untracked selection and ignored-runtime-evidence cases.
- Corrected a Windows test fixture to write canonical LF bytes; the installer
  intentionally publishes LF. Corrected the imported nested ignore fixture to use
  tools/example because ONI's existing packages/ rule intentionally ignores NuGet.
- First passing control results: 92 Python SDLC tests; 82 setup tests with one
  POSIX-only execute-mode test skipped on Windows; JavaScript control and selector suites.
- npm audit reported zero advisories for the selected lock. npm reports a deprecated
  transitive glob package; no override was added to the consumer's dependency graph.

## Qualified local results

`npm exec --yes --package=npm@12.0.2 -- npm run setup:development` completed
using this checkout's dependencies and virtual environment. `npm run check:sdlc`
passed. The generated config also validated against the native Codex 0.153.4
configuration schema; schema validation does not establish host loading.

`npm run sdlc -- verify` selected SDLC and pipeline scopes. Its first run passed
the Python/JavaScript checks, then failed because the sandbox could not read the
existing user NuGet.Config. That failed run is retained. The host-access rerun
passed on 2026-09-08 with unchanged verification inputs:

- 94 Python SDLC tests passed, including origin-specific GitHub destinations.
- 82 Python setup tests ran: 81 passed and one POSIX execute-mode case was skipped.
- 73 JavaScript tests passed in five suites.
- Locked .NET restore passed; all 334 existing pipeline tests passed.
- Whitespace and generated-configuration checks passed.

Runtime records preserve command output, failures, task identity and input digests
under the ignored `.sdlc/runtime/` directory. Final CI results and exact publication
revisions belong to the bootstrap PR and its merge record, rather than a predicted
commit ID in its own tree.

This is control/setup evidence, not in-game qualification, independent review,
native security-scan completion, Stop-hook trust or required-check activation.
