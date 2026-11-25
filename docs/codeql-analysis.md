# CodeQL analysis with game references

C# analysis uses CodeQL's manual build mode and the same ONI/Unity DLLs as the supported local game installation.
The compiler resolves the real game APIs, and CodeQL extracts the compiled source and TUnit-generated code.
Actions and Python continue to use analysis without a build.

## Reference ownership

`tools/ci/oni-references.json` owns the exact private NuGet package version, archive length, archive SHA-256, and manifest SHA-256.
The package contains the original managed DLLs and a manifest with the length and SHA-256 of each DLL.
It contains no game assets, saves, logs, credentials, or machine paths.
No game DLL or package is committed to the public repository or uploaded as an Actions artifact.

The catalog selects the current private package.
The installed runtime checks independently read the Klei changelist and compare the full DLL inventory with the pinned manifest.
The package preserves the complete managed DLL directory so assembly dependencies can resolve without substitutes.

## Prepare and publish the private package

Start a new intake with the changelist measured from the installed game.

```powershell
$managed = "C:/Program Files (x86)/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed"
$changeList = Read-Host "Installed Klei changelist"
$intake = New-Item -ItemType Directory -Path ("artifacts/private-intake-" + [guid]::NewGuid().ToString("N"))
$catalog = Join-Path $intake.FullName "oni-references.json"
python tools/ci/oni_references.py pack --managed-directory $managed --version "$changeList.0.0" --output-directory $intake.FullName --catalog $catalog
python tools/ci/oni_references.py verify-installed --managed-directory $managed --catalog $catalog
```

This copies the DLLs and writes a deterministic archive and candidate catalog in the intake directory.
It refuses to overwrite an existing package.
Verify the archive with `acquire --archive` using that candidate catalog.
After reviewing the measured hashes, promote the catalog to `tools/ci/oni-references.json` and run the [build-floor projection](development-dependencies.md#update-game-and-mod-dependencies).
Keep the verified archive privately in its intake directory or copy it into the local private-package store for future publication.

After publication is authorized, publish the archive named by the selected package ID and version to `https://nuget.pkg.github.com/MaksymShostak/index.json` using a publishing credential with `write:packages`.
The initial `744825.0.0` publication and download used the existing GitHub CLI OAuth login with package scopes.

```powershell
gh auth refresh --hostname github.com --scopes read:packages,write:packages
```

GitHub's registry documentation describes classic personal access tokens as another authentication route.
Keep publishing credentials in the local credential store or transient process environment; CI does not need them.
New packages default to private visibility.
This package deliberately has no repository metadata, preventing automatic inheritance of the public source repository's access permissions.

In the package's settings, retain **Private** visibility and grant `MaksymShostak/oxygen-not-included` **Read** under **Manage Actions access**.
Keep human access independent of the public repository.
Do not enable inherited public repository permissions.
See GitHub's [NuGet authentication guidance](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry) and [package access controls](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility).
GitHub CLI documents [OAuth scope expansion](https://cli.github.com/manual/gh_auth_refresh).

## Analysis flow

The C# reusable workflow validates the selected revision and trusted event before acquiring references.
Only the C# analysis job receives `packages: read`; its repository-scoped `GITHUB_TOKEN` is passed only to the acquisition step.
There is no persistent personal access token in Actions.
Foreign pull requests remain ineligible for privileged CodeQL analysis under the existing CI policy.

Acquisition uses a temporary native NuGet `PackageDownload` project with an exact version and only the private reference feed.
The package is data, not an imported MSBuild dependency.
Credentials exist only in the restore subprocess environment.
The archive and manifest hashes, exact inventory, and every DLL are checked before the reference directory is exposed to the build.
Missing access or changed bytes fail the job.

After CodeQL initialization, `tools/ci/codeql_build.py` discovers every tracked C# project using Git.
It performs locked restores followed by fresh Release builds with shared compilation disabled.
The mod build receives the verified managed directory and an isolated merged output path.
Build outputs stay outside the source projects, and the original local game installation is never modified.
All references are verified again after compilation.
The build records its SDK, commands, project inventory, source commit, and reference identities in local build evidence.

CodeQL analyzes the resulting database and uploads its normal analysis results.
Database upload is explicitly disabled so the database derived from private game assemblies stays on the ephemeral runner.
The existing completion receipt is returned only after successful acquisition, compilation, and analysis.
No unresolved game API is replaced by a stub, excluded, or hidden through diagnostic suppression.

## Local verification

Use the verified intake directory from the preparation step.
For a previously retained package, set `$intake` to the directory that contains its archive.

```powershell
$pin = (Get-Content -Raw tools/ci/oni-references.json | ConvertFrom-Json).package
$archive = Join-Path $intake.FullName "$($pin.id).$($pin.version).nupkg"
$run = Join-Path "artifacts" ("reference-check-" + [guid]::NewGuid().ToString("N"))
python tools/ci/oni_references.py acquire --archive $archive --output-directory "$run/references"
python tools/ci/codeql_build.py --managed-directory "$run/references/Managed" --output-directory "$run/build"
python -m unittest discover -s tests -p "test_*.py"
python tools/ci/action_versions.py --check
```

Each acquisition and build needs a new output directory.
To verify extraction as well as compilation, run the build helper as the command for `codeql database create --language=csharp`, then analyze the database with `codeql/csharp-queries:codeql-suites/csharp-code-scanning.qls`.
A local analysis proves the local candidate; hosted activation additionally requires the private package publication, Actions read access, and a successful run of the published workflow revision.
