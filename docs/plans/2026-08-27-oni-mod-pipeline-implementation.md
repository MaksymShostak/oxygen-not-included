# ONI Mod Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local-first .NET 10 CLI that takes any conventionally packaged ONI mod from validated source through a fully tested, hashed, upload-ready Workshop release candidate, while keeping the authenticated ONI Uploader's final Publish action human-only.

**Architecture:** One `OniModPipeline` executable contains focused internal modules for profiles, discovery, build/test execution, packaging, listing rendering, content integrity, local installation, and release-candidate lifecycle. Generic behavior is driven by a strict versioned `oni-mod-pipeline.toml`; Delivery Temperature Limit contributes only declarative facts, ordinary C# regression tests, and human acceptance cases. Every mutating operation stages beneath a validated root, every release-content byte is hashed, and installation plus acceptance evidence is bound to the canonical release-content digest.

**Tech Stack:** .NET SDK 10.0.400; C# on `net10.0` for the pipeline; SDK-style `net48` for the ONI mod; System.CommandLine 2.0.11; Tomlyn 2.10.1; YamlDotNet 18.1.0; MSTest.Sdk 4.3.3; `System.Text.Json`; `System.Security.Cryptography`; Git and `dotnet` invoked without a shell.

**Spec:** `docs/specs/2026-08-27-oni-mod-pipeline-design.md`

## Global Constraints

- The pipeline SDK is exactly `10.0.400`, with `rollForward` equal to `latestPatch` and `allowPrerelease` equal to `false`.
- The pipeline targets `net10.0`; Delivery Temperature Limit remains SDK-style `net48`.
- The normal workflow runs locally on Windows, macOS, and Linux and requires no hosted CI, container runtime, PowerShell, batch, or Bash.
- The generic pipeline contains no Delivery Temperature Limit gameplay concepts; mod-specific facts live in `oni-mod-pipeline.toml`, the mod test project, and acceptance declarations.
- No compatibility shim remains under `build.sh`, `deploy_mod_locally.bat`, or either `.Tests.ps1` name.
- `mod_info.yaml` is the release-version source. Build, test, validation, and release preparation never rewrite tracked source.
- Restore is lock-file based. Release workflows invoke `dotnet restore --locked-mode`; dependency updates use an explicit reviewed `--force-evaluate` operation.
- The mod project relies on the SDK's implicit .NET Framework reference assemblies; Klei, Unity, Harmony, FMOD, and other game assemblies remain explicit non-copy-local references.
- All process arguments use `ProcessStartInfo.ArgumentList`; no shell command string, arbitrary hook, or `UseShellExecute=true` is permitted.
- Package sources and destinations are explicit. Packaging starts from an empty staging directory, rejects links and collisions, and proves closure before promotion.
- Tracked Workshop text is UTF-8 without BOM, LF-only, and ends with exactly one LF. Generated `description.bbcode` and `change-notes.bbcode` are UTF-8 without BOM, CRLF-only, and end with exactly one CRLF.
- The Steam description limit is 8,000 UTF-8 bytes. V1 also applies an 8,000-byte conservative ceiling to change notes.
- `workshop-content/` is the only Update Data directory. Listing artifacts and release evidence are never copied into it.
- Release content and prepared identity evidence are immutable. Installation and acceptance receipts are write-once. Verification may replace only the three derived readiness documents atomically.
- Candidate acceptance is valid only for an installed candidate with the same content digest and immutable acceptance-plan hash.
- `verify-release` is deterministic and non-interactive for unchanged candidate state.
- No command is named `publish`, handles Steam credentials, automates the Uploader UI, calls SteamCMD, or claims publication.
- The tracked root `DeliveryTemperatureLimit.dll` remains untouched as the migration baseline; deleting it requires a separate future decision.
- Repository configuration safety applies throughout. Before any task edits a project, solution, lock, SDK, build, test, deployment, or policy file, pause for explicit approval of the exact file and setting described in this plan.
- Every commit step requires fresh explicit authorization for the staged snapshot and exact message. Pushing requires a separate explicit authorization. Plan approval alone authorizes neither.
- Preserve unrelated untracked files, including `AGENTS.md` and `mods/delivery-temperature-limit-supercooled/screenshot-guidance.md`.

The package pins were checked on 2026-08-27 against their primary distribution/documentation pages: [System.CommandLine 2.0.11](https://www.nuget.org/packages/System.CommandLine/2.0.11), [Tomlyn 2.10.1](https://www.nuget.org/packages/Tomlyn/2.10.1), [YamlDotNet 18.1.0](https://www.nuget.org/packages/YamlDotNet/18.1.0), and [MSTest.Sdk 4.3.3](https://www.nuget.org/packages/MSTest.Sdk/4.3.3). Microsoft's current guidance recommends `MSTest.Sdk`; its default Microsoft.Testing.Platform profile includes TRX support, invoked with `--report-trx` after the `dotnet test` argument separator: [MSTest SDK setup](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-getting-started), [MTP test reports](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-test-reports).

---

## Delivery Shape and Review Gates

The work is one integrated product, but it has four reviewer gates. A gate must be green before work that removes its predecessor begins.

| Gate | Independently testable deliverable | Legacy behavior still available? |
|---|---|---|
| A — Generic foundation | Strict profile validation, environment diagnosis, and portable CLI/test skeleton | Yes |
| B — Build parity | C# regressions, isolated locked build, modernized SDK project, and public-surface/package parity | Yes |
| C — Candidate lifecycle | Allowlisted content, CRLF listing handoff, hashes, evidence, guarded install, and digest-bound acceptance | Yes |
| D — Migration completion | Full real-mod rehearsal passes; shell workflows are deleted; README names one supported path | No |

If a gate fails, repair it in place and rerun its commands. Do not delete or bypass the legacy path merely to advance the checklist.

Two specification details are reconciled explicitly rather than left to implementer interpretation:

- Sections 10.3 and 17.2 require the profile to name intended merge inputs, while the illustrative TOML omitted a field. Schema v1 therefore includes `[build].merge-inputs` as an array of assembly simple names; this mod declares `PLib`.
- The legacy local deployment copied `Preview.png` beside runtime files, while Sections 13, 16, and 17 define preview as a separate Uploader listing field. Parity treats moving that unchanged image to `workshop-listing/preview.png` as the approved architecture, not as a runtime inventory regression.

## Configuration Approval Dossier

Before Task 1, request one explicit configuration approval covering only the following exact intentions. If any value changes during implementation, present the delta and obtain a new approval before editing.

| File | Exact setting or operation | Impact |
|---|---|---|
| `global.json` | Add SDK `10.0.400`, `latestPatch`, stable-only | Pins the local toolchain feature band |
| `tools/oni-mod-pipeline/OniModPipeline.slnx` | Add the production and generic test projects | Defines the tool build/test entry point |
| `tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj` | Add `net10.0` executable/tool settings, nullable and implicit usings, warnings-as-errors, lock-file generation, and the three pinned production packages | Defines runtime and dependency policy |
| `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj` | Add `MSTest.Sdk/4.3.3`, `net10.0`, a project reference, and lock-file generation | Defines generic test execution |
| Tool `packages.lock.json` files | Generate and commit exact closures | Enables locked restore |
| `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml` | Add schema-v1 mod facts, allowlist, tests, listing inputs, and acceptance checks | Connects this mod to the generic CLI |
| `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj` | Add `MSTest.Sdk/4.3.3`, linked production source, stubs, and lock-file generation | Replaces dynamic PowerShell compilation |
| Mod test `packages.lock.json` | Generate and commit exact closure | Enables locked regression restore |
| `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj` | Retain `net48`; remove legacy output/configuration/framework/version declarations; rename `GameFolder`; require pipeline build properties; lock restore; privatize ILRepack build assets; explicitly import the merge target | Completes SDK-style modernization without retargeting ONI code |
| `mods/delivery-temperature-limit-supercooled/Source/ILRepack.targets` | Replace source-root output and the PowerShell metadata target with an isolated `MergeModAssembly` target | Eliminates source mutation and shell execution |
| `mods/delivery-temperature-limit-supercooled/Source/Directory.Build.props` | Delete after the required-property diagnostic is proven | Removes machine-specific fallback configuration |
| `mods/delivery-temperature-limit-supercooled/Source/packages.lock.json` | Generate and commit the mod build closure | Enables locked mod restore |
| Legacy `.ps1`, `.bat`, and `.sh` files listed in Task 19 | Delete only after Gates A–C and parity checks pass | Removes all supported shell-based ONI workflows |

## File and Module Map

All production types are `internal` unless an ecosystem entry point requires otherwise. `Properties/AssemblyInfo.cs` grants test access; the tool remains one assembly.

```text
global.json
tools/oni-mod-pipeline/
  OniModPipeline.slnx
  src/OniModPipeline/
    OniModPipeline.csproj
    Program.cs
    Properties/AssemblyInfo.cs
    Cli/
      CliApplication.cs
      CommandOptions.cs
      PipelineServices.cs
    Diagnostics/
      Diagnostic.cs
      DiagnosticCatalog.cs
      DiagnosticRenderer.cs
      DiagnosticSeverity.cs
      OperationResult.cs
      OutputFormat.cs
      PipelineExitCode.cs
    Processes/
      ExternalProcessRunner.cs
      IExternalProcessRunner.cs
      ProcessRequest.cs
      ProcessResult.cs
    ModProfiles/
      AcceptanceCheckProfile.cs
      BuildProfile.cs
      ContainedPathResolver.cs
      LocalInstallProfile.cs
      ModProfile.cs
      ModProfileLoader.cs
      ModProfileLocator.cs
      ModProfileValidator.cs
      OniMetadata.cs
      OniMetadataReader.cs
      PackageFileMapping.cs
      TestProjectProfile.cs
      WorkshopListingProfile.cs
    EnvironmentDiscovery/
      EnvironmentDiscoveryRequest.cs
      EnvironmentDiscoveryService.cs
      EnvironmentVariableSource.cs
      GameInstallationCandidateSource.cs
      PipelineEnvironment.cs
      SteamLibraryCatalog.cs
    SourceControl/
      GitRepositoryInspector.cs
      RelevantSourceSet.cs
      SourceSnapshot.cs
    ModBuild/
      BuildRequest.cs
      BuildResult.cs
      MsBuildPropertyArgument.cs
      ModBuilder.cs
    ModTest/
      AutomatedTestResult.cs
      AutomatedTestRunner.cs
    ContentIntegrity/
      CanonicalContentManifestSerializer.cs
      ContentArea.cs
      ContentHasher.cs
      ContentRole.cs
      FileDigest.cs
      ReleaseContentEntry.cs
      ReleaseContentManifest.cs
    WorkshopListing/
      BbCodeValidator.cs
      ListingTextRenderer.cs
      ListingTextReport.cs
      PreviewImageInspector.cs
      WorkshopListingAssembler.cs
      WorkshopListingValidator.cs
    WorkshopContent/
      WorkshopContentAssembler.cs
      WorkshopContentValidator.cs
    ReleaseCandidates/
      AcceptanceRecorder.cs
      AcceptanceTestPlan.cs
      AcceptanceTestResults.cs
      BuildProvenance.cs
      CandidateLayout.cs
      IAcceptanceConsole.cs
      ReleaseCandidatePreparer.cs
      ReleaseCandidateState.cs
      ReleaseCandidateVerifier.cs
      ReleaseReadinessReport.cs
      ReleaseSummaryRenderer.cs
      RunIdFactory.cs
      UploaderChecklistRenderer.cs
    ModInstallation/
      InstallTarget.cs
      InstallationReceipt.cs
      ModInstaller.cs
      OwnershipMarker.cs
    Serialization/
      Utf8ArtifactWriter.cs
  tests/OniModPipeline.Tests/
    OniModPipeline.Tests.csproj
    Cli/
    ContentIntegrity/
    Diagnostics/
    EnvironmentDiscovery/
    Fixtures/
    ModBuild/
    ModInstallation/
    ModProfiles/
    ModTest/
    Processes/
    ReleaseCandidates/
    SourceControl/
    WorkshopContent/
    WorkshopListing/

mods/delivery-temperature-limit-supercooled/
  oni-mod-pipeline.toml
  STEAM_CHANGE_NOTES.bbcode
  Source/
    DeliveryTemperatureLimit.csproj
    ILRepack.targets
    packages.lock.json
  Tests/
    DeliveryTemperatureLimit.Tests.csproj
    BuildingsEligibilityTests.cs
    DotnetProcess.cs
    GameStubs.cs
    ModBuildContractTests.cs
    PublicAssemblySurface.cs
    TemporaryDirectory.cs
    packages.lock.json
```

Documentation delivered at Gate D lives in `README.md` and `docs/guides/oni-mod-development-workflow.md`.

`Directory.Build.props`, both PowerShell tests, `Source/build.sh`, and `scripts/deploy_mod_locally.bat` appear in the map only as Task 19 deletions. Do not create replacement wrappers.

## Cross-Task Contract Registry

Later tasks must use these names and signatures exactly unless a reviewer approves a coordinated plan revision.

```csharp
internal enum PipelineExitCode
{
    Success = 0,
    InvalidInput = 2,
    EnvironmentUnavailable = 3,
    BuildOrTestFailed = 4,
    InstallationFailed = 5,
    ReleaseNotReady = 6,
    InternalFailure = 10
}

internal enum OutputFormat { Human, Json }

internal sealed record Diagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string Summary,
    string Evidence,
    string NextAction);

internal sealed record OperationResult<T>(
    T? Value,
    IReadOnlyList<Diagnostic> Diagnostics,
    PipelineExitCode ExitCode)
{
    internal bool IsSuccess => ExitCode == PipelineExitCode.Success;
}

internal sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables);

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal interface IExternalProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

internal sealed record EnvironmentDiscoveryRequest(
    string? GameDirectory,
    string? UserDataDirectory,
    string? ArtifactsDirectory);

internal sealed record PipelineEnvironment(
    string GameDirectory,
    string OniManagedAssemblyDirectory,
    string UserDataDirectory,
    string DevelopmentModsDirectory,
    string LocalModsDirectory,
    string ArtifactsDirectory,
    string DotnetSdkVersion,
    string OperatingSystem,
    string Architecture);

internal sealed record BuildRequest(
    ModProfile Profile,
    PipelineEnvironment Environment,
    string Configuration,
    string RunRoot,
    string ReleaseVersion,
    string SourceCommit);

internal sealed record AssemblyVersionInfo(
    string AssemblyVersion,
    string? FileVersion,
    string? InformationalVersion);

internal sealed record BuildResult(
    string RunRoot,
    string? PrimaryOutputPath,
    IReadOnlyList<FileDigest> Inputs,
    IReadOnlyList<FileDigest> Outputs,
    IReadOnlyList<FileDigest> MergeInputs,
    IReadOnlyList<FileDigest> GameReferences,
    string SourceCommit,
    string ReleaseVersion,
    string DotnetSdkVersion,
    IReadOnlyList<string> StructuredBuildArguments,
    AssemblyVersionInfo? PrimaryAssemblyVersion,
    bool SourceBytesUnchanged);

internal enum ContentArea { WorkshopContent, WorkshopListing }
internal enum ContentRole { Runtime, Description, ChangeNotes, Preview }

internal sealed record FileDigest(string Path, long ByteLength, string Sha256);

internal sealed record ReleaseContentEntry(
    ContentArea ContentArea,
    string RelativePath,
    long ByteLength,
    string Sha256,
    ContentRole Role);

internal sealed record ReleaseContentManifest(
    int SchemaVersion,
    IReadOnlyList<ReleaseContentEntry> Entries,
    string ContentDigest);
```

JSON uses camel-case property names. Enum values are serialized as lower-case kebab-case strings through an explicit converter, not by relying on `Enum.ToString()`.

---

### Task 1: Pin the Toolchain and Establish a Smoke-Tested CLI Shell

**Files:**
- Create: `global.json`
- Create: `tools/oni-mod-pipeline/OniModPipeline.slnx`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Program.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Properties/AssemblyInfo.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/CliApplicationTests.cs`
- Create: tool production and test `packages.lock.json` files through restore

**Interfaces:**
- Consumes: none
- Produces: `CliApplication.CreateRootCommand()` and `CliApplication.InvokeAsync(string[], CancellationToken)`; a locked `OniModPipeline.slnx` build/test entry point

- [ ] **Step 1: Obtain the configuration approval described above**

Present the exact `global.json`, solution, and two project-file changes below. Stop until the user explicitly approves them.

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>oni-mod-pipeline</ToolCommandName>
    <PackageId>MaksymShostak.OniModPipeline</PackageId>
    <Version>0.1.0</Version>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.11" />
    <PackageReference Include="Tomlyn" Version="2.10.1" />
    <PackageReference Include="YamlDotNet" Version="18.1.0" />
  </ItemGroup>
</Project>
```

```xml
<Project Sdk="MSTest.Sdk/4.3.3">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\OniModPipeline\OniModPipeline.csproj" />
  </ItemGroup>
</Project>
```

After `dotnet new sln --name OniModPipeline --output tools/oni-mod-pipeline` and the two explicit `dotnet sln ... add ...` operations, the solution must contain exactly:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/OniModPipeline/OniModPipeline.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Create only the approved configuration scaffold and restore it**

Create `global.json`, the two project files, the `.slnx`, and source/test directories. Add the production project under solution folder `src` and the test project under solution folder `tests`. Create `Program.cs` containing only `return 0;` so the executable project is compilable, but do not create `CliApplication` yet.

Run:

```text
dotnet restore tools/oni-mod-pipeline/OniModPipeline.slnx --force-evaluate
```

Expected: both `packages.lock.json` files are created and restore succeeds.

- [ ] **Step 3: Write the failing CLI identity test**

```csharp
using MaksymShostak.OniModPipeline.Cli;

namespace MaksymShostak.OniModPipeline.Tests.Cli;

[TestClass]
public sealed class CliApplicationTests
{
    [TestMethod]
    public void CreateRootCommand_WhenCalled_DescribesTheLocalPipeline()
    {
        var command = CliApplication.CreateRootCommand();

        Assert.AreEqual(
            "Prepare tested ONI mod release candidates for manual Workshop upload.",
            command.Description);
    }
}
```

- [ ] **Step 4: Run the focused test and confirm the red state**

Run from the repository root:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~CliApplicationTests"
```

Expected: compilation fails because `CliApplication` does not exist.

- [ ] **Step 5: Create the minimal executable**

Implement:

```csharp
// Program.cs
using MaksymShostak.OniModPipeline.Cli;

return await CliApplication.InvokeAsync(args, CancellationToken.None);
```

```csharp
// Cli/CliApplication.cs
using System.CommandLine;

namespace MaksymShostak.OniModPipeline.Cli;

internal static class CliApplication
{
    internal static RootCommand CreateRootCommand() =>
        new("Prepare tested ONI mod release candidates for manual Workshop upload.");

    internal static Task<int> InvokeAsync(
        string[] args,
        CancellationToken cancellationToken) =>
        CreateRootCommand().Parse(args).InvokeAsync(cancellationToken: cancellationToken);
}
```

```csharp
// Properties/AssemblyInfo.cs
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OniModPipeline.Tests")]
```

- [ ] **Step 6: Verify locked restore**

Run:

```text
dotnet restore tools/oni-mod-pipeline/OniModPipeline.slnx --locked-mode
```

Expected: success without changing either lock file.

- [ ] **Step 7: Run the smoke test and executable help**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: `CliApplicationTests` passes.

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- --help
```

Expected: exit `0`; output contains `oni-mod-pipeline` and the exact description from the test.

- [ ] **Step 8: Review and commit the foundation**

Inspect `git diff --check`, `git diff --stat`, the project files, and both lock files. Obtain explicit authorization, then create:

```text
build: establish locked .NET 10 pipeline foundation
```

Do not push.

### Task 2: Establish Stable Diagnostics, Results, and Output Rendering

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/Diagnostic.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/DiagnosticCatalog.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/DiagnosticRenderer.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/DiagnosticSeverity.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/OperationResult.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/OutputFormat.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/PipelineExitCode.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Diagnostics/DiagnosticRendererTests.cs`

**Interfaces:**
- Consumes: none
- Produces: `Diagnostic`, `OperationResult<T>`, `PipelineExitCode`, and `DiagnosticRenderer.Render<T>(OperationResult<T>, OutputFormat, TextWriter, TextWriter)` for every later command

- [ ] **Step 1: Write failing exit-code and JSON-shape tests**

```csharp
[TestMethod]
public void Render_WhenInvalidProfileUsesJson_WritesStableMachineReadableFields()
{
    var diagnostic = DiagnosticCatalog.UnsupportedSchemaVersion(2, "profile.toml");
    var result = new OperationResult<object>(
        null,
        [diagnostic],
        PipelineExitCode.InvalidInput);
    using var output = new StringWriter(CultureInfo.InvariantCulture);
    using var error = new StringWriter(CultureInfo.InvariantCulture);

    var exitCode = DiagnosticRenderer.Render(result, OutputFormat.Json, output, error);

    Assert.AreEqual(2, exitCode);
    StringAssert.Contains(output.ToString(), "\"id\": \"ONIP1001\"");
    StringAssert.Contains(output.ToString(), "\"exitCode\": 2");
    Assert.AreEqual(string.Empty, error.ToString());
}

[TestMethod]
public void Render_WhenHumanFailure_WritesRemedyToStandardError()
{
    var result = new OperationResult<object>(
        null,
        [DiagnosticCatalog.UnsupportedSchemaVersion(2, "profile.toml")],
        PipelineExitCode.InvalidInput);
    using var output = new StringWriter(CultureInfo.InvariantCulture);
    using var error = new StringWriter(CultureInfo.InvariantCulture);

    var exitCode = DiagnosticRenderer.Render(result, OutputFormat.Human, output, error);

    Assert.AreEqual(2, exitCode);
    Assert.AreEqual(string.Empty, output.ToString());
    StringAssert.Contains(error.ToString(), "ONIP1001");
    StringAssert.Contains(error.ToString(), "Use schema-version = 1");
}

[TestMethod]
public async Task InvokeAsync_WhenCommandCannotBeParsed_ReturnsInvalidInputExitCode()
{
    var exitCode = await CliApplication.InvokeAsync(["--not-a-real-option"], CancellationToken.None);

    Assert.AreEqual(2, exitCode);
}
```

- [ ] **Step 2: Run the focused test and confirm it fails**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~DiagnosticRendererTests"
```

Expected: compilation fails because the diagnostics types do not exist.

- [ ] **Step 3: Implement the diagnostic value types and catalog**

Use the contract-registry definitions and these stable IDs:

```csharp
internal static class DiagnosticIds
{
    internal const string UnsupportedSchemaVersion = "ONIP1001";
    internal const string UnknownProfileKey = "ONIP1002";
    internal const string UnsafeProfilePath = "ONIP1003";
    internal const string DuplicatePackageDestination = "ONIP1004";
    internal const string InvalidOniMetadata = "ONIP1005";
    internal const string InvalidWorkshopListing = "ONIP1006";
    internal const string ProfileNotFoundOrAmbiguous = "ONIP1007";
    internal const string DeclaredInputMissing = "ONIP1008";
    internal const string MissingDotnetSdk = "ONIP2001";
    internal const string AmbiguousGameInstallation = "ONIP2002";
    internal const string MissingGameAssembly = "ONIP2003";
    internal const string MissingUserDataDirectory = "ONIP2004";
    internal const string DuplicateInstalledMod = "ONIP2005";
    internal const string RestoreFailed = "ONIP3001";
    internal const string BuildFailed = "ONIP3002";
    internal const string SourceChangedDuringBuild = "ONIP3003";
    internal const string BuildOutputMissing = "ONIP3004";
    internal const string AutomatedTestFailed = "ONIP3005";
    internal const string UnownedInstallDestination = "ONIP4001";
    internal const string InstalledContentMismatch = "ONIP4002";
    internal const string InstallationReceiptExists = "ONIP4003";
    internal const string DirtyReleaseInput = "ONIP5001";
    internal const string CandidateManifestMismatch = "ONIP5002";
    internal const string AcceptanceDigestMismatch = "ONIP5003";
    internal const string RequiredAcceptanceMissing = "ONIP5004";
    internal const string InvalidUploaderRepresentation = "ONIP5005";
    internal const string ReleaseNotReady = "ONIP5006";
    internal const string CandidateAlreadyExists = "ONIP5007";
    internal const string AcceptanceRequiresInteractiveTerminal = "ONIP5008";
    internal const string UnexpectedFailure = "ONIP9001";
    internal const string CleanupFailed = "ONIP9002";
}
```

`DiagnosticCatalog` constructs full immutable diagnostics. Do not scatter literal IDs or remedy wording through command handlers.

- [ ] **Step 4: Implement deterministic human and JSON rendering**

`DiagnosticRenderer` writes successful human values to stdout, human failures to stderr, and one JSON document to stdout for JSON mode. JSON must use `System.Text.Json`, camel-case fields, `WriteIndented = true`, no ANSI escapes, UTF-8/LF when written to files, and invariant numeric formatting.

Update `CliApplication.InvokeAsync` to parse first, print each `ParseError.Message` to stderr, and return `PipelineExitCode.InvalidInput` when `ParseResult.Errors` is non-empty. Invoke the parsed action asynchronously only when parsing succeeds. A top-level catch for an unexpected exception emits a stable internal-failure diagnostic and returns `10`; it does not convert cancellation into success.

- [ ] **Step 5: Run diagnostics and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass.

- [ ] **Step 6: Review and commit diagnostics**

After explicit authorization, create:

```text
feat: add stable pipeline diagnostics and exit codes
```

### Task 3: Load Strict Versioned Profiles and ONI Metadata

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/AcceptanceCheckProfile.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/BuildProfile.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/LocalInstallProfile.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ModProfile.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ModProfileLoader.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ModProfileLocator.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/OniMetadata.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/OniMetadataReader.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/PackageFileMapping.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/TestProjectProfile.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/WorkshopListingProfile.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModProfiles/ModProfileLoaderTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModProfiles/ModProfileLocatorTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModProfiles/OniMetadataReaderTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Fixtures/TemporaryDirectory.cs`

**Interfaces:**
- Consumes: `OperationResult<T>` and `DiagnosticCatalog`
- Produces: `ModProfileLocator.Locate(string startPath)`, `ModProfileLoader.Load(string manifestPath)`, and `OniMetadataReader.Read(ModProfile)`

```csharp
internal sealed record ModProfile(
    int SchemaVersion,
    string ManifestPath,
    string ModRoot,
    string ModYamlPath,
    string ModInfoYamlPath,
    BuildProfile? Build,
    IReadOnlyList<PackageFileMapping> PackageFiles,
    WorkshopListingProfile WorkshopListing,
    LocalInstallProfile LocalInstall,
    IReadOnlyList<TestProjectProfile> TestProjects,
    IReadOnlyList<AcceptanceCheckProfile> AcceptanceChecks);

internal sealed record BuildProfile(
    string EntryPoint,
    string Configuration,
    string GameManagedDirectoryProperty,
    string PrimaryOutput,
    IReadOnlyList<string> MergeInputs);

internal sealed record PackageFileMapping(string Source, string Destination);
internal sealed record TestProjectProfile(string Id, string Path, bool Required);
internal sealed record LocalInstallProfile(string DirectoryName);

internal sealed record WorkshopListingProfile(
    string Description,
    string ChangeNotes,
    string Preview,
    IReadOnlyList<string> ModTypes,
    IReadOnlyList<string> DlcCompatibility,
    int DescriptionByteLimit,
    int ChangeNotesByteLimit);

internal sealed record AcceptanceCheckProfile(
    string Id,
    string Title,
    bool Required,
    string Setup,
    string Action,
    string Expected);

internal sealed record OniMetadata(
    string StaticId,
    string Title,
    string Description,
    string SupportedContent,
    int MinimumSupportedBuild,
    string Version,
    int ApiVersion);
```

- [ ] **Step 1: Write failing profile discovery and strict-schema tests**

Cover all of these named cases:

```csharp
[TestMethod]
public void Locate_WhenStartedBelowOneManifest_ReturnsThatManifest()

[TestMethod]
public void Locate_WhenTwoCandidateManifestsAreReachable_ReturnsAmbiguityDiagnostic()

[TestMethod]
public void Load_WhenSchemaVersionIsTwo_ReturnsOnip1001()

[TestMethod]
public void Load_WhenTopLevelKeyIsMisspelled_ReturnsOnip1002()

[TestMethod]
public void Load_WhenNestedKeyIsUnknown_ReturnsOnip1002WithFullKeyPath()

[TestMethod]
public void Read_WhenYamlContainsAllRequiredScalars_ReturnsTypedMetadata()

[TestMethod]
public void Read_WhenYamlContainsAdditionalOniOwnedKey_StillReturnsRequiredMetadata()

[TestMethod]
public void Read_WhenYamlHasMultipleDocuments_ReturnsOnip1005()
```

Both missing and ambiguous profile discovery use `ONIP1007` with different evidence/remedy text; a declared path that resolves safely but does not exist uses `ONIP1008`.

Use `TemporaryDirectory` backed by `Path.GetTempPath()` and a unique GUID. Its `Dispose` method must resolve the path, prove it remains a strict descendant of the captured temporary root, and only then delete it recursively.

- [ ] **Step 2: Run the profile tests and confirm the red state**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ModProfiles"
```

Expected: compilation fails for the missing profile types.

- [ ] **Step 3: Implement upward profile discovery**

`ModProfileLocator` accepts either a manifest path, a mod directory, or a descendant path. It walks parents only until the Git worktree root when available, otherwise to the filesystem root. Zero matches returns an actionable missing-profile diagnostic; more than one possible manifest from the explicit input is an ambiguity error. Never select a sibling mod by recency or directory enumeration order.

- [ ] **Step 4: Parse TOML into an untyped model and reject unknown keys before mapping**

Use Tomlyn's `TomlTable` representation. Validate allowed keys at every level before constructing records:

```csharp
private static readonly IReadOnlyDictionary<string, ISet<string>> AllowedKeys =
    new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
    {
        [""] = new HashSet<string>(["schema-version", "mod", "build", "package-files", "workshop-listing", "local-install", "test-projects", "acceptance-checks"], StringComparer.Ordinal),
        ["mod"] = new HashSet<string>(["mod-yaml", "mod-info-yaml"], StringComparer.Ordinal),
        ["build"] = new HashSet<string>(["entry-point", "configuration", "game-managed-directory-property", "primary-output", "merge-inputs"], StringComparer.Ordinal),
        ["package-files[]"] = new HashSet<string>(["source", "destination"], StringComparer.Ordinal),
        ["workshop-listing"] = new HashSet<string>(["description", "change-notes", "preview", "mod-types", "dlc-compatibility", "description-byte-limit", "change-notes-byte-limit"], StringComparer.Ordinal),
        ["local-install"] = new HashSet<string>(["directory-name"], StringComparer.Ordinal),
        ["test-projects[]"] = new HashSet<string>(["id", "path", "required"], StringComparer.Ordinal),
        ["acceptance-checks[]"] = new HashSet<string>(["id", "title", "required", "setup", "action", "expected"], StringComparer.Ordinal)
    };
```

Require `schema-version = 1`. Preserve declared strings exactly; semantic normalization belongs in Task 4.
When either optional listing byte-limit key is absent, map it to `8000`; when present, require an integer from `1` through `8000`. A profile may lower but not raise the platform ceiling.

- [ ] **Step 5: Parse ONI YAML as a constrained mapping**

Use `YamlStream`/`YamlMappingNode`, not permissive POCO deserialization. Require one document, a mapping root, and the known required scalar keys used by each file. Reject aliases, custom tags, duplicate keys, extra documents, missing required keys, integer overflow in interpreted integers, and non-scalar values for interpreted fields. Preserve forward compatibility by leaving additional ONI-owned keys uninterpreted rather than treating them as profile-schema errors. Do not rewrite YAML.

- [ ] **Step 6: Run profile tests and locked solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all profile, metadata, and existing tests pass.

- [ ] **Step 7: Review and commit strict profile loading**

After explicit authorization, create:

```text
feat: load strict versioned ONI mod profiles
```

### Task 4: Enforce Path Containment and Profile Semantics

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ContainedPathResolver.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ModProfileValidator.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModProfiles/ContainedPathResolverTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModProfiles/ModProfileValidatorTests.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ModProfileLoader.cs`

**Interfaces:**
- Consumes: `ModProfile`, `OniMetadata`, `OperationResult<T>`
- Produces: `ContainedPathResolver.ResolveExistingFile(string root, string declaredPath)` and `ModProfileValidator.Validate(ModProfile, OniMetadata)`

- [ ] **Step 1: Write failing containment tests**

```csharp
[DataTestMethod]
[DataRow("../outside.txt")]
[DataRow("..\\outside.txt")]
[DataRow("/absolute.txt")]
[DataRow("C:\\absolute.txt")]
public void ResolveExistingFile_WhenPathEscapesRoot_ReturnsOnip1003(string declaredPath)

[TestMethod]
public void ResolveExistingFile_WhenAncestorIsSymbolicLink_ReturnsOnip1003()

[TestMethod]
public void ResolveExistingFile_WhenContainedRegularFile_ReturnsCanonicalAbsolutePath()
```

On Windows, add a junction/reparse-point case when the test process has permission; if creating it is unavailable, test the same branch through an internal `FileAttributes` seam rather than marking the required behavior skipped.

- [ ] **Step 2: Write failing semantic-validation tests**

Cover duplicate package destinations after `/` normalization, Unicode-NFC collisions, portable case-insensitive collisions, duplicate test/check IDs, non-kebab-case IDs, missing required listing inputs, invalid mod-type/DLC identifiers, empty package mappings, missing root metadata destinations, and build profiles whose primary output is not sourced from `{build-output}`.

- [ ] **Step 3: Run the focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ContainedPathResolverTests"
```

Expected: compilation fails because `ContainedPathResolver` does not exist.

- [ ] **Step 4: Implement canonical containment**

The resolver must:

1. reject empty, rooted, NUL-containing, or drive-relative declarations;
2. combine with the canonical root and call `Path.GetFullPath`;
3. use `Path.GetRelativePath` to prove the result is neither rooted nor `..` nor beneath `..`;
4. walk every existing path segment from root to leaf and reject `FileAttributes.ReparsePoint` or a non-null `LinkTarget`;
5. require the requested file/directory kind; and
6. return the absolute path without changing filename casing.

- [ ] **Step 5: Implement semantic profile validation**

Use ordinal comparison for identifiers and portable collision keys of `relativePath.Normalize(FormC).ToUpperInvariant()`. The only v1 listing identifiers are:

```text
mod-types: language, worldgen, new-features, tweaks, ui
dlc-compatibility: base-game, spaced-out, frosty-planet-pack, bionic-booster-pack, prehistoric-planet-pack, aquatic-planet-pack
```

Require a non-empty title/description, a static ID matching `^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$`, positive API/minimum-build integers, and a release version accepted by `System.Version.TryParse` with two through four nonnegative components no greater than `65534`. This validates ONI/.NET-compatible syntax without choosing semantic-versus-calendar versioning. Require `mod.yaml`, `mod_info.yaml`, and every declared package source/test/listing source to resolve beneath the mod root. Content-only profiles may omit `[build]`; build profiles require one project/solution entry point, a `{build-output}` primary output, a managed-directory property matching `^[A-Za-z_][A-Za-z0-9_]*$`, and unique merge-input assembly simple names matching `^[A-Za-z_][A-Za-z0-9_.-]*$` without a `.dll` suffix. The configured project receives its chosen managed-directory property plus the generic `OniMergedModOutputPath` contract; a project that cannot accept the latter is not conventionally buildable under schema v1.

- [ ] **Step 6: Run all profile tests**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ModProfiles"
```

Expected: all profile tests pass, including link and collision cases.

- [ ] **Step 7: Review and commit path safety**

After explicit authorization, create:

```text
feat: validate portable ONI profile paths and semantics
```

### Task 5: Add Shell-Free External Process Execution

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Processes/IExternalProcessRunner.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Processes/ProcessRequest.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Processes/ProcessResult.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Processes/ExternalProcessRunner.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Processes/ExternalProcessRunnerTests.cs`

**Interfaces:**
- Consumes: the process contracts in the registry
- Produces: `ExternalProcessRunner.RunAsync(ProcessRequest, CancellationToken)` for Git, restore, build, and test modules

- [ ] **Step 1: Write failing argument-boundary and cancellation tests**

Create a tiny fixture executable project under the test temporary directory that serializes its received arguments. Verify an argument containing spaces, quotes, an ampersand, a dollar sign, and a semicolon arrives as one literal argument. Add a cancellation test whose child waits on standard input and must be killed with its process tree.

```csharp
[TestMethod]
public async Task RunAsync_WhenArgumentContainsShellCharacters_PreservesOneLiteralArgument()

[TestMethod]
public async Task RunAsync_WhenCancelled_KillsProcessTreeAndThrowsOperationCancelledException()
```

- [ ] **Step 2: Run the focused test and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ExternalProcessRunnerTests"
```

Expected: compilation fails because `ExternalProcessRunner` does not exist.

- [ ] **Step 3: Implement process execution without a shell**

Construct `ProcessStartInfo` with:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = request.FileName,
    WorkingDirectory = request.WorkingDirectory,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    RedirectStandardInput = false,
    CreateNoWindow = true
};

foreach (var argument in request.Arguments)
    startInfo.ArgumentList.Add(argument);

foreach (var pair in request.EnvironmentVariables)
    startInfo.Environment[pair.Key] = pair.Value;
```

Read stdout/stderr concurrently, register cancellation to `Kill(entireProcessTree: true)`, await process exit, then return exact captured text and exit code. Never log environment-variable values globally; only the explicitly structured build arguments enter provenance.

- [ ] **Step 4: Run process and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass on the current Windows host; the test code contains no OS-specific shell executable.

- [ ] **Step 5: Review and commit process execution**

After explicit authorization, create:

```text
feat: execute pipeline processes without a shell
```

### Task 6: Record Scoped Git Provenance and Detect Source Mutation

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/SourceControl/GitRepositoryInspector.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/SourceControl/RelevantSourceSet.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/SourceControl/SourceSnapshot.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/SourceControl/GitRepositoryInspectorTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/SourceControl/RelevantSourceSetTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/SourceControl/SourceSnapshotTests.cs`

**Interfaces:**
- Consumes: `ModProfile`, `FileDigest`, and `IExternalProcessRunner`
- Produces: scoped Git provenance and exact before/after source snapshots

```csharp
internal sealed record GitProvenance(
    string WorktreeRoot,
    string Commit,
    IReadOnlyList<string> ContributingPaths,
    IReadOnlyList<string> DirtyPaths)
{
    internal bool IsClean => DirtyPaths.Count == 0;
}

internal sealed record SourceSnapshot(IReadOnlyList<FileDigest> Files)
{
    internal static SourceSnapshot Capture(IReadOnlyList<string> absolutePaths);
    internal static SourceSnapshot CaptureTree(string absoluteRoot);
    internal IReadOnlyList<string> ChangedPathsComparedWith(SourceSnapshot later);
}
```

- [ ] **Step 1: Write failing scoped-cleanliness tests**

Create temporary Git repositories through `ExternalProcessRunner`, configuring a local test identity only inside the temporary repository. Cover:

```csharp
[TestMethod]
public async Task InspectAsync_WhenContributingFileIsModified_ReportsThatFileDirty()

[TestMethod]
public async Task InspectAsync_WhenUnrelatedFileIsModified_RemainsCleanForReleaseScope()

[TestMethod]
public async Task InspectAsync_WhenContributingFileIsUntracked_ReportsThatFileDirty()

[TestMethod]
public async Task InspectAsync_WhenPathIsOutsideWorktree_ReturnsOnip5001()
```

- [ ] **Step 2: Write failing source-snapshot tests**

Verify byte changes, additions, removals, and unchanged bytes with a changed timestamp. The comparison is content-based; timestamps are not evidence.

- [ ] **Step 3: Run the focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~SourceControl"
```

Expected: compilation fails for the missing source-control types.

- [ ] **Step 4: Implement relevant input enumeration**

`RelevantSourceSet` includes the manifest, both ONI metadata files, the declared build entry point, every tracked file beneath its project directory except `bin/` and `obj/`, every package source, every Workshop source, and every declared test project plus its compile inputs. For a pipeline executable built from the same worktree, it also includes `global.json`, `OniModPipeline.slnx`, both tool project files, lock files, and all tool `.cs` files. Normalize to worktree-relative `/` paths, sort ordinally, and deduplicate.

- [ ] **Step 5: Implement Git inspection with literal argument boundaries**

Invoke these commands as separate `ProcessRequest` values:

```text
git rev-parse --show-toplevel
git rev-parse HEAD
git status --porcelain=v1 -z --untracked-files=all
git ls-files -z
```

Parse all NUL-delimited status output, including rename/copy records, then intersect it with the normalized contributing-path set. Use the complete NUL-delimited tracked-file list to prove every contributing source is committed. This avoids command-line-length limits while still allowing unrelated dirty files. No global Git configuration is read or changed by production code.

- [ ] **Step 6: Implement SHA-256 source snapshots**

Hash exact file bytes with `SHA256.HashData(Stream)`. Store canonical absolute paths in memory and worktree-relative paths in evidence. `CaptureTree` enumerates every regular file beneath the mod root without following links, including pre-existing untracked files and old `bin`/`obj` contents, so additions and removals are observable. Before restore, capture both the contributing input set and the complete mod-root tree; after build, recapture them and fail with `ONIP3003` if any bytes, additions, or removals differ.

- [ ] **Step 7: Run source-control and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass; the unrelated-dirty-file test proves release scope is narrow.

- [ ] **Step 8: Review and commit provenance support**

After explicit authorization, create:

```text
feat: capture scoped source provenance
```

### Task 7: Discover the Local ONI Environment Portably

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/EnvironmentDiscoveryRequest.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/EnvironmentDiscoveryService.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/EnvironmentVariableSource.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/GameInstallationCandidateSource.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/PipelineEnvironment.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/SteamLibraryCatalog.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/EnvironmentDiscovery/EnvironmentDiscoveryServiceTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/EnvironmentDiscovery/SteamLibraryCatalogTests.cs`

**Interfaces:**
- Consumes: `EnvironmentDiscoveryRequest`, `ModProfile`, diagnostics, and process execution
- Produces: `EnvironmentDiscoveryService.DiscoverAsync(ModProfile, EnvironmentDiscoveryRequest, CancellationToken)` returning `OperationResult<PipelineEnvironment>`

- [ ] **Step 1: Write failing precedence and ambiguity tests**

```csharp
[TestMethod]
public async Task DiscoverAsync_WhenCliGameDirectoryIsValid_UsesItBeforeEnvironmentAndDiscovery()

[TestMethod]
public async Task DiscoverAsync_WhenEnvironmentOverrideIsValid_UsesItBeforeAutomaticDiscovery()

[TestMethod]
public async Task DiscoverAsync_WhenTwoAutomaticGameDirectoriesAreValid_ReturnsOnip2002()

[TestMethod]
public async Task DiscoverAsync_WhenManagedAnchorIsMissing_ReturnsOnip2003()

[TestMethod]
public async Task DiscoverAsync_WhenArtifactOverrideIsRelative_ReturnsInvalidInput()
```

Also test that an explicit invalid override fails immediately and does not silently fall through to automatic discovery.

- [ ] **Step 2: Write platform-path table tests**

Assert the candidate source emits these native user-data conventions:

```text
Windows: <MyDocuments>/Klei/OxygenNotIncluded
macOS:   <home>/Library/Application Support/unity.Klei.Oxygen Not Included
Linux:   <home>/.config/unity3d/Klei/Oxygen Not Included
```

Derive `mods/Dev` and `mods/Local` with those exact case-sensitive leaf names. Windows matching remains case-insensitive; macOS/Linux path construction preserves case.

- [ ] **Step 3: Run the discovery tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~EnvironmentDiscovery"
```

Expected: compilation fails because the discovery service does not exist.

- [ ] **Step 4: Implement precedence and validation**

Use this order independently for each purpose:

```text
explicit CLI option
documented environment variable
platform automatic candidates
actionable failure
```

The variables are `ONI_GAME_DIRECTORY`, `ONI_USER_DATA_DIRECTORY`, and `ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY`. From a selected game root, probe `OxygenNotIncluded_Data/Managed` on Windows/Linux and `OxygenNotIncluded.app/Contents/Resources/Data/Managed` on macOS. Validate the derived managed directory by requiring `Assembly-CSharp.dll` and `0Harmony.dll`. Later build evidence enumerates the complete resolved compile-reference set.

- [ ] **Step 5: Implement Steam-library candidate discovery**

Read Steam registry/install metadata on Windows and `steamapps/libraryfolders.vdf` under conventional Steam roots. Parse quoted VDF keys and paths without invoking Steam. Probe each library for `steamapps/common/OxygenNotIncluded`; also accept explicit Epic/non-Steam locations through overrides. On Linux, include native Steam roots and Proton's `steamapps/compatdata/457140/pfx/drive_c/users/steamuser/Documents/Klei/OxygenNotIncluded` as a user-data candidate tied to the matching library.

- [ ] **Step 6: Derive the artifact root safely**

If overridden, require an absolute path. Otherwise use `<git-worktree-root>/artifacts` when the mod is in Git and `<mod-root>/artifacts` otherwise. Reject a filesystem root, home directory, Documents root, ONI user-data root, or Steam library root as the artifact directory.

- [ ] **Step 7: Verify the selected SDK**

Invoke `dotnet --version`; require a stable `10.0.4xx` version and record the exact value. Do not download or install an SDK automatically.

- [ ] **Step 8: Run discovery and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass without relying on the current machine's actual ONI installation in unit tests.

- [ ] **Step 9: Review and commit environment discovery**

After explicit authorization, create:

```text
feat: discover portable local ONI environments
```

### Task 8: Wire Read-Only `diagnose` and `validate` Commands

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CommandOptions.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/DiagnoseCommandTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/ValidateCommandTests.cs`

**Interfaces:**
- Consumes: profile loading/validation, metadata, environment discovery, Git inspection, and diagnostics
- Produces: `diagnose` and `validate [--for-release]`, with common options `--mod`, `--game-directory`, `--user-data-directory`, `--artifacts-directory`, and `--format`

```csharp
internal sealed record PipelineServices(
    ModProfileLocator ProfileLocator,
    ModProfileLoader ProfileLoader,
    ModProfileValidator ProfileValidator,
    OniMetadataReader MetadataReader,
    EnvironmentDiscoveryService EnvironmentDiscovery,
    GitRepositoryInspector GitRepositoryInspector);
```

- [ ] **Step 1: Write failing command-boundary tests**

Use temporary mod fixtures and injected services. Cover:

```csharp
[TestMethod]
public async Task Diagnose_WhenEnvironmentIsValid_PrintsResolvedPathsWithoutCreatingArtifacts()

[TestMethod]
public async Task Validate_WhenDevelopmentInputIsDirty_SucceedsWithoutForRelease()

[TestMethod]
public async Task Validate_WhenContributingInputIsDirtyAndForRelease_ReturnsExitCodeSix()

[TestMethod]
public async Task Validate_WhenJsonRequested_WritesOneJsonDocumentWithoutAnsi()
```

Take a recursive file-byte snapshot before each command and prove it is unchanged afterward.

- [ ] **Step 2: Run CLI tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~DiagnoseCommandTests"
```

Expected: `diagnose` is not registered.

- [ ] **Step 3: Build one composition root**

`CliApplication.CreateDefaultServices()` constructs concrete `System.IO` modules and one `ExternalProcessRunner`. Tests call `CreateRootCommand(PipelineServices)` with controlled dependencies. Command actions only translate options into requests, call a module, render its result, and return the module's exit code.

- [ ] **Step 4: Register exact command options**

```text
oni-mod-pipeline diagnose --mod <path> [environment overrides] [--format human|json]
oni-mod-pipeline validate --mod <path> [environment overrides] [--for-release] [--format human|json]
```

`--mod` defaults to the current directory for discovery. Environment options are optional and accept one path. `--format` defaults to `human`; unknown values are command-parse errors with exit `2`.

- [ ] **Step 5: Implement read-only reports**

`diagnose` reports SDK, OS/architecture, worktree/mod roots, selected game and managed paths, user-data path, Dev/Local targets, artifact root, game build metadata if readable, and Uploader presence as optional information. `validate` performs profile, metadata, path, listing-source, reference-anchor, and declaration checks. `--for-release` additionally enforces committed clean contributing paths.

- [ ] **Step 6: Run side-effect and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass; fixture byte snapshots are identical before and after both commands.

- [ ] **Step 7: Manually exercise help without an ONI install**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- diagnose --help
```

Expected: exit `0`; all five common options are documented; no artifacts directory is created.

- [ ] **Step 8: Review Gate A and commit**

Confirm strict schema, path safety, scoped release cleanliness, and portable discovery are independently green. After explicit authorization, create:

```text
feat: add read-only ONI diagnose and validate commands
```

### Task 9: Add the Delivery Temperature Limit Profile and C# Eligibility Regression

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml`
- Create: `mods/delivery-temperature-limit-supercooled/STEAM_CHANGE_NOTES.bbcode`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameStubs.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/BuildingsEligibilityTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/packages.lock.json` through restore
- Retain: `mods/delivery-temperature-limit-supercooled/Tests/BuildingsEligibility.Tests.ps1` until Task 19

**Interfaces:**
- Consumes: schema-v1 profile contract and existing private `Buildings_Patch.IsEligible(IBuildingConfig, GameObject)`
- Produces: a valid real-mod profile and the required test-project ID `delivery-temperature-limit-regressions`

- [ ] **Step 1: Obtain exact configuration approval for the profile and test project**

This approval is required even if Task 1's files were approved separately. The test project is:

```xml
<Project Sdk="MSTest.Sdk/4.3.3">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>annotations</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\Source\Buildings.cs" Link="Production\Buildings.cs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the schema-v1 profile**

Create exactly this portable profile:

```toml
schema-version = 1

[mod]
mod-yaml = "mod.yaml"
mod-info-yaml = "mod_info.yaml"

[build]
entry-point = "Source/DeliveryTemperatureLimit.csproj"
configuration = "Release"
game-managed-directory-property = "OniManagedAssemblyDirectory"
primary-output = "{build-output}/DeliveryTemperatureLimit.dll"
merge-inputs = ["PLib"]

[[package-files]]
source = "mod.yaml"
destination = "mod.yaml"

[[package-files]]
source = "mod_info.yaml"
destination = "mod_info.yaml"

[[package-files]]
source = "{build-output}/DeliveryTemperatureLimit.dll"
destination = "DeliveryTemperatureLimit.dll"

[workshop-listing]
description = "STEAM_DESCRIPTION.bbcode"
change-notes = "STEAM_CHANGE_NOTES.bbcode"
preview = "Preview.png"
mod-types = ["new-features", "tweaks", "ui"]
dlc-compatibility = [
  "base-game",
  "spaced-out",
  "frosty-planet-pack",
  "bionic-booster-pack",
  "prehistoric-planet-pack",
  "aquatic-planet-pack"
]

[local-install]
directory-name = "DeliveryTemperatureLimit"

[[test-projects]]
id = "delivery-temperature-limit-regressions"
path = "Tests/DeliveryTemperatureLimit.Tests.csproj"
required = true

[[acceptance-checks]]
id = "storage-bin-temperature-filter"
title = "Storage Bin rejects out-of-range deliveries"
required = true
setup = "Configure a Storage Bin with a bounded safe temperature range and make one out-of-range material plus one in-range control available."
action = "Allow Duplicants to generate and perform storage errands for both materials."
expected = "No errand delivers the out-of-range material to the configured bin, while the in-range control remains deliverable."

[[acceptance-checks]]
id = "storage-tile-rocket-temperature-filter"
title = "Storage Tile aboard a rocket rejects out-of-range deliveries"
required = true
setup = "Load the release-test colony, configure a rocket-interior Storage Tile with a bounded safe temperature range, expose out-of-range and in-range control materials, and provide a competing valid destination if errand generation requires it."
action = "Allow Duplicants to generate and perform rocket-interior storage errands for both materials."
expected = "Duplicants refuse the out-of-range Storage Tile delivery and the in-range control remains deliverable."

[[acceptance-checks]]
id = "construction-temperature-filter"
title = "Optional construction limits reject out-of-range building materials"
required = true
setup = "Enable Apply Limits to Construction Materials, place a blueprint with an applicable temperature limit, and make out-of-range plus in-range construction materials available."
action = "Allow Duplicants to generate construction supply errands, then repeat after disabling the option."
expected = "The enabled option blocks out-of-range construction delivery without blocking the in-range control; disabling the option restores ordinary construction delivery behavior."

[[acceptance-checks]]
id = "temperature-side-screen-editing"
title = "Temperature side-screen fields edit and clear without freezing"
required = true
setup = "Select an eligible storage building with no active limits and open the Delivery Temperature Limit side screen."
action = "Enter a high limit first, edit both limits, then press Del to clear the fields."
expected = "Values update coherently, the interface remains responsive, and clearing both fields disables the limit."

[[acceptance-checks]]
id = "temperature-side-screen-keyboard"
title = "Temperature side screen does not capture camera controls"
required = true
setup = "Open the Delivery Temperature Limit side screen on an eligible building."
action = "Use the normal keyboard camera controls while the panel is selected and while a numeric field has focus."
expected = "Camera controls work according to ONI's normal focus rules and the panel does not leave keyboard input locked."

[[acceptance-checks]]
id = "save-load-temperature-limits"
title = "Configured limits survive save and load"
required = true
setup = "Configure distinct limits on a Storage Bin and a rocket Storage Tile, then save the colony."
action = "Return to the main menu, reload the save, and inspect both buildings."
expected = "Both buildings retain their configured limits and continue to apply them to delivery errands."

[[acceptance-checks]]
id = "delivery-temperature-log-review"
title = "Acceptance run produces no relevant game-log exceptions"
required = true
setup = "Complete the storage, construction, UI, and save-load acceptance scenarios in one game session."
action = "Exit the game and inspect Player.log for DeliveryTemperatureLimit, Harmony patch, Unity lifecycle, and unhandled exception messages."
expected = "The log contains the mod's expected initialization messages and no new relevant errors or exceptions."

[[acceptance-checks]]
id = "workshop-description-uploader-line-structure"
title = "Generated Workshop description preserves line structure in the ONI Uploader"
required = true
setup = "Open the candidate workshop-listing/description.bbcode in current Windows Notepad, open the authenticated ONI Uploader Edit Mod form, and leave every update checkbox disabled."
action = "Copy all text from Notepad, paste it into Description, inspect the structure, record the Notepad and Uploader versions in the result note, then cancel the form without publishing."
expected = "Paragraphs, blank lines, ---, headings, and [list] blocks remain on separate lines, and no Workshop update is submitted."
```

- [ ] **Step 3: Add reviewed current change notes**

Use exactly this LF source text with one final LF:

```bbcode
[h1]Storage Tiles Check the Thermometer[/h1]

Storage Tiles—including the ones aboard rockets—now respect delivery temperature limits. Duplicants will no longer tuck volcano-fresh cargo under the floor and stamp it “properly stored.”

Thanks to [url=https://steamcommunity.com/id/shylion]ShyLion[/url] for the suggestion before the next mission became an orbital sauna.
```

- [ ] **Step 4: Write the C# eligibility regression first**

Replace dynamic source concatenation with these normal C# declarations in `GameStubs.cs`:

```csharp
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type) { }
        public HarmonyPatch(string methodName) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute { }

    public sealed class HarmonyMethod
    {
        public HarmonyMethod(System.Reflection.MethodInfo method) { }
    }

    public sealed class Harmony
    {
        public void Patch(
            System.Reflection.MethodInfo original,
            HarmonyMethod? prefix = null,
            HarmonyMethod? postfix = null) { }
    }

    public static class AccessTools
    {
        public static System.Reflection.MethodInfo? Method(Type type, string name) => null;
        public static System.Reflection.FieldInfo? Field(Type type, string name) => null;
    }
}

namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly Dictionary<Type, object> components = new();

        public T? GetComponent<T>() where T : class =>
            components.TryGetValue(typeof(T), out var component) ? (T)component : null;

        public T AddComponent<T>() where T : class, new()
        {
            var component = new T();
            components[typeof(T)] = component;
            return component;
        }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) { }
    }
}

public interface IBuildingConfig { }
public sealed class StorageTileConfig : IBuildingConfig { }

public sealed class BuildingConfigManager
{
    public static BuildingConfigManager Instance { get; } = new();
    public void ConfigurePost() { }
}

public sealed class BuildingDef
{
    public UnityEngine.GameObject? BuildingComplete { get; set; }
    public UnityEngine.GameObject? BuildingUnderConstruction { get; set; }
}

public sealed class TemperatureLimit { }
public sealed class ManualDeliveryKG { }
public sealed class Storage { public bool allowUIItemRemoval { get; set; } }
public sealed class StorageLocker { }
public sealed class ObjectDispenser { }
public sealed class SolidConduitInbox { }
public sealed class BottleEmptier { }
public sealed class CreatureFeeder { }
public sealed class RationBox { }
public sealed class Refrigerator { }
```

Write the test:

```csharp
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests;

[TestClass]
public sealed class BuildingsEligibilityTests
{
    [TestMethod]
    public void IsEligible_WhenConfigIsStorageTile_ReturnsTrue()
    {
        var method = typeof(Buildings_Patch).GetMethod(
            "IsEligible",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "Buildings_Patch.IsEligible must remain discoverable.");

        var result = method.Invoke(
            null,
            [new StorageTileConfig(), new UnityEngine.GameObject()]);

        Assert.AreEqual(true, result);
    }
}
```

- [ ] **Step 5: Restore and run the new regression**

Run:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --force-evaluate
```

Expected: restore succeeds and creates `Tests/packages.lock.json`.

Run:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --locked-mode
```

Expected: success without lock-file changes.

Run:

```text
dotnet test --project mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
```

Expected: `IsEligible_WhenConfigIsStorageTile_ReturnsTrue` passes.

- [ ] **Step 6: Validate the real profile**

Run `oni-mod-pipeline validate` with explicit local ONI paths if automatic discovery is ambiguous:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- validate --mod mods/delivery-temperature-limit-supercooled
```

Expected: success; profile paths resolve beneath the mod root; the change-note source is non-empty; both root metadata destinations are declared.

- [ ] **Step 7: Review and commit the mod adapter layer**

Confirm no generic production type mentions Storage Tiles, rockets, temperature limits, or this mod's static ID. After explicit authorization, create:

```text
test: define Delivery Temperature Limit pipeline profile
```

### Task 10: Build in Isolation and Run Declared Tests with TRX Evidence

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/Serialization/Utf8ArtifactWriter.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModBuild/BuildRequest.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModBuild/BuildResult.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModBuild/MsBuildPropertyArgument.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModBuild/ModBuilder.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModTest/AutomatedTestResult.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModTest/AutomatedTestRunner.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Serialization/Utf8ArtifactWriterTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModBuild/ModBuilderTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModBuild/MsBuildPropertyArgumentTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModTest/AutomatedTestRunnerTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/BuildCommandTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/TestCommandTests.cs`

**Interfaces:**
- Consumes: profile/environment/source snapshots, `IExternalProcessRunner`, diagnostics, and `FileDigest`
- Produces: `ModBuilder.BuildAsync(BuildRequest, CancellationToken)`, `AutomatedTestRunner.RunAsync(ModProfile, string resultsRoot, CancellationToken)`, `build-result.json`, and CLI `build`/`test`

```csharp
internal sealed record AutomatedTestResult(
    string TestProjectId,
    string ProjectPath,
    string TrxPath,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Passed);
```

- [ ] **Step 1: Write failing artifact-writer tests**

Prove `Utf8ArtifactWriter.WriteJsonAtomicallyAsync` and `WriteLfTextAtomicallyAsync` emit UTF-8 without BOM, LF-only, exactly one final LF, and replace only the named derived file. A failed serialization must leave the existing destination unchanged.

- [ ] **Step 2: Write failing build-contract tests with a recording process runner**

```csharp
[TestMethod]
public async Task BuildAsync_WhenProfileHasBuild_RestoresLockedThenBuildsWithoutShell()

[TestMethod]
public async Task BuildAsync_WhenSourceBytesChange_ReturnsOnip3003()

[TestMethod]
public async Task BuildAsync_WhenPrimaryOutputIsMissing_ReturnsOnip3004()

[TestMethod]
public async Task BuildAsync_WhenPrimaryOutputIsManagedAssembly_RecordsThreeVersionMeanings()

[TestMethod]
public async Task BuildAsync_WhenProfileIsContentOnly_ProducesEmptySuccessfulBuildResult()
```

Assert the exact structured build properties, not a joined command string.

`MsBuildPropertyArgument.Create(name, value)` requires the approved MSBuild property-name regex, rejects NUL/control characters and literal double quotes in values, and returns one `-p:Name="Value"` token. Test spaces, semicolons, equals signs, Unicode, and trailing directory separators so MSBuild receives each path as one property value without shell quoting or multi-property injection.

- [ ] **Step 3: Write failing test-runner evidence tests**

Cover unique required IDs, missing projects, failing processes, missing TRX files, and exact TRX filenames. A required project producing exit `0` but no TRX is `ONIP3005`.

- [ ] **Step 4: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ModBuilderTests"
```

Expected: compilation fails because `ModBuilder` does not exist.

- [ ] **Step 5: Implement isolated locked build orchestration**

For a build profile, invoke two processes in order:

```text
dotnet restore <entry-point> --locked-mode -p:<game-managed-directory-property>=<managed-directory> -p:BaseIntermediateOutputPath=<run-root>/obj/$(MSBuildProjectName)/ -p:MSBuildProjectExtensionsPath=<run-root>/obj/$(MSBuildProjectName)/
dotnet build <entry-point> --no-restore --configuration <configuration> -p:<game-managed-directory-property>=<managed-directory> -p:OniMergedModOutputPath=<run-root>/output/<primary-file> -p:BaseOutputPath=<run-root>/bin/$(MSBuildProjectName)/ -p:BaseIntermediateOutputPath=<run-root>/obj/$(MSBuildProjectName)/ -p:MSBuildProjectExtensionsPath=<run-root>/obj/$(MSBuildProjectName)/ -p:Version=<mod-info-version> -p:InformationalVersion=<mod-info-version>+<short-commit> -p:Deterministic=true -p:ContinuousIntegrationBuild=true -p:PathMap=<worktree-root>=/_/
```

Every token is one `ArgumentList` entry; `$(MSBuildProjectName)` is passed literally for per-project expansion by MSBuild, never interpreted by a shell. Use identical intermediate-path properties for restore and build so `project.assets.json` remains isolated and discoverable. Capture source bytes before restore and after build. Record the contributing input hashes in `BuildResult.Inputs`, resolve declared outputs beneath the run root, hash them, and write `build-result.json` using the centralized artifact writer. For content-only profiles, create the run root and successful result without invoking `dotnet`.

For a managed primary DLL, use `PEReader`/`MetadataReader` without loading the assembly into the pipeline process. Read `AssemblyDefinition.Version` plus the fixed string values of `AssemblyFileVersionAttribute` and `AssemblyInformationalVersionAttribute` into `AssemblyVersionInfo`. Require the informational version to start with the validated `mod_info.yaml` version; record all three meanings rather than treating them as interchangeable. Content-only builds store `null`.

- [ ] **Step 6: Enumerate actual game references for provenance**

After restore and before build, invoke:

```text
dotnet msbuild <entry-point> -nologo -target:ResolveReferences -getItem:ReferencePath,ReferenceCopyLocalPaths -p:Configuration=<configuration> -p:<game-managed-directory-property>=<managed-directory> -p:OniMergedModOutputPath=<run-root>/output/<primary-file> -p:BaseIntermediateOutputPath=<run-root>/obj/$(MSBuildProjectName)/ -p:MSBuildProjectExtensionsPath=<run-root>/obj/$(MSBuildProjectName)/
```

Parse the SDK's JSON output. Retain `ReferencePath` items beneath the managed-assembly directory as game references. Resolve every declared merge-input simple name to exactly one `ReferenceCopyLocalPaths` item, hash it into `BuildResult.MergeInputs`, and reject missing, duplicate, or undeclared merge inputs. Sort both sets by normalized filename. Do not copy game references or loose merge inputs into Workshop content.

- [ ] **Step 7: Implement declared MSTest execution**

For every profile test project, first run locked restore. Then invoke Microsoft.Testing.Platform through:

```text
dotnet test --project <test-project> --no-restore --configuration Release --results-directory <results-root> -- --report-trx --report-trx-filename <test-project-id>.trx
```

Set `ONI_MANAGED_ASSEMBLY_DIRECTORY` and `ONI_MOD_PIPELINE_REPOSITORY_ROOT` only on the child test process so mod integration tests can locate real references and source without committed machine paths. Delete no pre-existing result directory: require the run-specific result root to be absent, create it, then require exactly the intended `.trx` per declared project. Capture stdout/stderr in `AutomatedTestResult` but never place logs in Workshop content.

- [ ] **Step 8: Register `build` and `test` commands**

```text
oni-mod-pipeline build --mod <path> [environment overrides] [--configuration Release] [--format human|json]
oni-mod-pipeline test --mod <path> [environment overrides] [--format human|json]
```

`build` prints the exact `build-result.json` path. `test` prints the exact automated-test-results directory. Neither selects a previous run, installs, packages, or mutates metadata.

- [ ] **Step 9: Run generic integration tests**

Build a minimal temporary `net10.0` fixture twice under different run roots, including one run-root name containing spaces and a semicolon. Assert MSBuild receives the literal paths, outputs remain beneath each root, and fixture source bytes do not change.

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass.

- [ ] **Step 10: Run the declared real-mod test through the CLI**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Expected: exit `0`; the run contains `automated-test-results/delivery-temperature-limit-regressions.trx`.

- [ ] **Step 11: Review and commit build/test orchestration**

After explicit authorization, create:

```text
feat: build ONI mods in isolation and capture TRX evidence
```

### Task 11: Capture the Legacy Contract and Complete SDK-Style Mod Modernization

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Tests/DotnetProcess.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/TemporaryDirectory.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/PublicAssemblySurface.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/ModBuildContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/ILRepack.targets`
- Create: `mods/delivery-temperature-limit-supercooled/Source/packages.lock.json` through restore
- Delete: `mods/delivery-temperature-limit-supercooled/Source/Directory.Build.props` only after the new required-property diagnostic passes
- Retain: `mods/delivery-temperature-limit-supercooled/DeliveryTemperatureLimit.dll` as the public-surface baseline

**Interfaces:**
- Consumes: pipeline build properties `OniManagedAssemblyDirectory` and `OniMergedModOutputPath`; standard SDK properties `Version`, `InformationalVersion`, `BaseOutputPath`, and `BaseIntermediateOutputPath`
- Produces: one merged `net48` DLL at the requested artifact path, no source mutation, a locked mod dependency closure, and parity tests against the tracked legacy DLL

- [ ] **Step 1: Obtain exact approval for the project, target, lock file, and props-file deletion**

Explain that the target remains `net48`, SDK-generated compile items/assembly attributes remain enabled, no language/nullable policy is being added to the mod, and the only output selected for Workshop packaging is the explicit merged DLL. Stop until approved.

- [ ] **Step 2: Write the failing source-mutation integration test before editing MSBuild**

```csharp
[TestMethod]
public async Task Build_WhenPipelinePropertiesAreProvided_DoesNotChangeModInfoBytes()
{
    var repositoryRoot = RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT");
    var managedDirectory = RequiredEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY");
    var modRoot = Path.Combine(repositoryRoot, "mods", "delivery-temperature-limit-supercooled");
    var project = Path.Combine(modRoot, "Source", "DeliveryTemperatureLimit.csproj");
    var modInfo = Path.Combine(modRoot, "mod_info.yaml");
    using var output = new TemporaryDirectory();
    var baseOutputPath = Path.Combine(output.Path, "bin", "$(MSBuildProjectName)") + Path.DirectorySeparatorChar;
    var baseIntermediateOutputPath = Path.Combine(output.Path, "obj", "$(MSBuildProjectName)") + Path.DirectorySeparatorChar;
    var before = await File.ReadAllBytesAsync(modInfo);

    var restore = await DotnetProcess.RunAsync(
        repositoryRoot,
        [
            "restore", project, "--locked-mode",
            $"-p:OniManagedAssemblyDirectory={managedDirectory}",
            $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
            $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
        ]);
    Assert.AreEqual(0, restore.ExitCode, restore.StandardError);

    var result = await DotnetProcess.RunAsync(
        repositoryRoot,
        [
            "build", project, "--no-restore", "--configuration", "Release",
            $"-p:OniManagedAssemblyDirectory={managedDirectory}",
            $"-p:OniMergedModOutputPath={Path.Combine(output.Path, "DeliveryTemperatureLimit.dll")}",
            $"-p:BaseOutputPath={baseOutputPath}",
            $"-p:BaseIntermediateOutputPath={baseIntermediateOutputPath}",
            $"-p:MSBuildProjectExtensionsPath={baseIntermediateOutputPath}"
        ]);

    Assert.AreEqual(0, result.ExitCode, result.StandardError);
    CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(modInfo));
    Assert.IsTrue(File.Exists(Path.Combine(output.Path, "DeliveryTemperatureLimit.dll")));
}
```

`DotnetProcess` is a test-only copy of the shell-free `ProcessStartInfo.ArgumentList` pattern, not a script or generic process abstraction. The current project is expected not to satisfy the explicit merged-output assertion.

Implement its complete surface as:

```csharp
internal sealed record DotnetProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class DotnetProcess
{
    internal static async Task<DotnetProcessResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start(), "dotnet process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var registration = cancellationToken.Register(
            () =>
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            });
        await process.WaitForExitAsync(cancellationToken);
        return new(process.ExitCode, await standardOutput, await standardError);
    }
}
```

Use this contained temporary-directory fixture:

```csharp
internal sealed class TemporaryDirectory : IDisposable
{
    private readonly string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());

    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(tempRoot, $"oni-mod-pipeline-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        var resolved = System.IO.Path.GetFullPath(Path);
        var relative = System.IO.Path.GetRelativePath(tempRoot, resolved);
        var escapes = System.IO.Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith($"..{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        if (escapes)
            throw new InvalidOperationException($"Refusing to delete temporary path outside {tempRoot}: {resolved}");
        if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }
}
```

- [ ] **Step 3: Add public-surface and deterministic-binary tests**

`PublicAssemblySurface.Read(string assemblyPath)` uses `PEReader` and `MetadataReader` to return ordinally sorted strings for every public/nested-public type and its public/protected constructors, methods, fields, properties, events, generic arity, parameter count, and signature blob. It excludes module IDs, timestamps, assembly/file/informational version attributes, and method bodies.

```csharp
[TestMethod]
public async Task ModernizedBuild_WhenComparedWithTrackedLegacyDll_PreservesPublicSurface()

[TestMethod]
public async Task ModernizedBuild_WhenBuiltTwiceFromSameInputs_ProducesSameMergedDllHash()
```

Both tests build into two unique temporary roots. The first compares against tracked `DeliveryTemperatureLimit.dll`; the second compares SHA-256 hashes. If the second test exposes unavoidable ILRepack nondeterminism, stop at Gate B and obtain approval for a documented non-blocking provenance limitation before weakening the assertion.

- [ ] **Step 4: Run the new build-contract tests and observe the red state**

Run through the pipeline so the two required environment variables are supplied:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Expected: the Storage Tile regression passes, while at least the explicit isolated-merge assertion fails under the legacy project/target contract.

- [ ] **Step 5: Replace the project file with the concise approved contract**

The completed project contains this structure; retain the existing list of non-framework references, changing every `HintPath` prefix to `$(OniManagedAssemblyDirectory)` and retaining `<Private>false</Private>`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyTitle>Delivery Temperature Limit (Supercooled)</AssemblyTitle>
    <Authors>MaksymShostak</Authors>
    <Company>MaksymShostak</Company>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="PLib" Version="4.24.0">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="ILRepack.Lib.MSBuild.Task" Version="2.0.34">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Reference Include="0Harmony">
      <HintPath>$(OniManagedAssemblyDirectory)/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(OniManagedAssemblyDirectory)/Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp-firstpass">
      <HintPath>$(OniManagedAssemblyDirectory)/Assembly-CSharp-firstpass.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UI">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.UI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Unity.TextMeshPro">
      <HintPath>$(OniManagedAssemblyDirectory)/Unity.TextMeshPro.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIModule">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.UIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.InputLegacyModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.ImageConversionModule">
      <HintPath>$(OniManagedAssemblyDirectory)/UnityEngine.ImageConversionModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Newtonsoft.Json">
      <HintPath>$(OniManagedAssemblyDirectory)/Newtonsoft.Json.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="FMODUnity">
      <HintPath>$(OniManagedAssemblyDirectory)/FMODUnity.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <Target Name="ValidateOniBuildProperties" BeforeTargets="ResolveReferences">
    <Error Condition="'$(OniManagedAssemblyDirectory)' == ''"
           Text="OniManagedAssemblyDirectory is required. Run this build through oni-mod-pipeline or pass the installed ONI Managed directory explicitly." />
    <Error Condition="!Exists('$(OniManagedAssemblyDirectory)/Assembly-CSharp.dll')"
           Text="OniManagedAssemblyDirectory does not contain Assembly-CSharp.dll: $(OniManagedAssemblyDirectory)" />
    <Error Condition="'$(OniMergedModOutputPath)' == ''"
           Text="OniMergedModOutputPath is required. Run this build through oni-mod-pipeline or pass an isolated merged output path explicitly." />
  </Target>

  <Import Project="ILRepack.targets" />
</Project>
```

This retains each current explicit game reference verbatim except for the approved property rename. It removes all six explicit base-framework references, `OutputPath`, both `Append*ToOutputPath` flags, both conditional configuration groups, and the literal `AssemblyVersion`.

- [ ] **Step 6: Replace the merge target and delete the metadata target**

```xml
<Project>
  <Target Name="MergeModAssembly"
          AfterTargets="Build"
          DependsOnTargets="ValidateOniBuildProperties"
          Condition="'$(DesignTimeBuild)' != 'true'">
    <PropertyGroup>
      <_ResolvedMergedModOutputPath>$([System.IO.Path]::GetFullPath('$(OniMergedModOutputPath)'))</_ResolvedMergedModOutputPath>
    </PropertyGroup>
    <ItemGroup>
      <_OniMergeInput Include="$(TargetPath)" />
      <_OniMergeInput Include="$(TargetDir)PLib.dll" />
    </ItemGroup>
    <Error Condition="!Exists('$(TargetPath)')" Text="Primary mod assembly was not built: $(TargetPath)" />
    <Error Condition="!Exists('$(TargetDir)PLib.dll')" Text="Declared PLib merge input was not restored to $(TargetDir)PLib.dll." />
    <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(_ResolvedMergedModOutputPath)'))" />
    <ILRepack Parallel="true"
              Internalize="true"
              InputAssemblies="@(_OniMergeInput)"
              TargetKind="Dll"
              OutputFile="$(_ResolvedMergedModOutputPath)"
              LibraryPath="$(OniManagedAssemblyDirectory)" />
    <Error Condition="!Exists('$(_ResolvedMergedModOutputPath)')" Text="ILRepack did not create $(_ResolvedMergedModOutputPath)." />
  </Target>
</Project>
```

There is no XML namespace, `Exec`, PowerShell, `UpdateModInfoVersion`, source-root output, or `GameFolder` reference.

- [ ] **Step 7: Remove the machine-specific fallback after proving the diagnostic**

Run a build without `OniManagedAssemblyDirectory` and verify it fails with the exact actionable error from `ValidateOniBuildProperties`. Then delete `Source/Directory.Build.props`. Re-run the same negative check and verify the diagnostic remains identical.

- [ ] **Step 8: Generate and lock the mod dependency closure**

Run with the discovered managed path:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj --force-evaluate -p:OniManagedAssemblyDirectory=<absolute-managed-directory> -p:BaseIntermediateOutputPath=<absolute-artifacts-directory>/migration/mod-restore/obj/ -p:MSBuildProjectExtensionsPath=<absolute-artifacts-directory>/migration/mod-restore/obj/
```

Expected: `Source/packages.lock.json` is created and includes the implicit .NET Framework reference-assemblies dependency plus the exact PLib/ILRepack closure.

Run:

```text
dotnet restore mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimit.csproj --locked-mode -p:OniManagedAssemblyDirectory=<absolute-managed-directory> -p:BaseIntermediateOutputPath=<absolute-artifacts-directory>/migration/mod-restore/obj/ -p:MSBuildProjectExtensionsPath=<absolute-artifacts-directory>/migration/mod-restore/obj/
```

Expected: success without lock-file changes.

- [ ] **Step 9: Run the full C# regression and parity suite**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Expected: Storage Tile eligibility, source non-mutation, public-surface parity, and repeatable merged-DLL hash all pass; a deterministic TRX is produced.

- [ ] **Step 10: Build the real mod through the pipeline and inspect its result**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- build --mod mods/delivery-temperature-limit-supercooled --configuration Release
```

Expected: the merged DLL exists only below `artifacts/builds/MaksymShostak.DeliveryTemperatureLimit/<run-id>/`; `build-result.json` records version `2026.8.26`; `git diff -- mods/delivery-temperature-limit-supercooled/mod_info.yaml` is empty; no new root DLL is written.

- [ ] **Step 11: Review Gate B and commit modernization**

Review the evaluated `TargetFramework=net48`, explicit reference closure, public surface, package inventory expectation (`mod.yaml`, `mod_info.yaml`, merged DLL), and the intentional relocation of `Preview.png` from the legacy local-copy folder to Workshop listing. After explicit authorization, create:

```text
build: modernize the net48 ONI mod project
```

### Task 12: Implement Canonical Content Hashing and Manifest Serialization

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/CanonicalContentManifestSerializer.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/ContentArea.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/ContentHasher.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/ContentRole.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/FileDigest.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/ReleaseContentEntry.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ContentIntegrity/ReleaseContentManifest.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ContentIntegrity/CanonicalContentManifestSerializerTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ContentIntegrity/ContentHasherTests.cs`

**Interfaces:**
- Consumes: release-content paths and roles
- Produces: `ContentHasher.HashFileAsync`, `CanonicalContentManifestSerializer.Serialize`, and `ReleaseContentManifest` with the canonical digest

```csharp
internal static class CanonicalContentManifestSerializer
{
    internal static byte[] Serialize(IReadOnlyList<ReleaseContentEntry> entries);
}

internal sealed class ContentHasher
{
    internal Task<ReleaseContentManifest> CreateManifestAsync(
        string releaseContentRoot,
        IReadOnlyList<(string AbsolutePath, ContentArea Area, ContentRole Role)> files,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write the cross-platform golden-vector test**

```csharp
[TestMethod]
public void Serialize_WhenGivenGoldenEntries_ProducesSpecifiedDigest()
{
    ReleaseContentEntry[] entries =
    [
        new(ContentArea.WorkshopContent, "mod.yaml", 42,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            ContentRole.Runtime),
        new(ContentArea.WorkshopListing, "description.bbcode", 17,
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            ContentRole.Description)
    ];

    var bytes = CanonicalContentManifestSerializer.Serialize(entries);
    var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    Assert.AreEqual(
        "c599e8d8dd6d307064e20c85381dfe17a1c1b340b688bb3a3cbab40741a2b8ca",
        digest);
}
```

- [ ] **Step 2: Write failing collision and sorting tests**

Cover ordinal ordering independent of current culture, `/` separators on Windows, NFC normalization, rejection of NUL/LF in paths, portable case collisions, Unicode-normalization collisions, lowercase 64-hex hashes, and byte lengths above `Int32.MaxValue` represented as invariant `long` values.

- [ ] **Step 3: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ContentIntegrity"
```

Expected: compilation fails because canonical serialization does not exist.

- [ ] **Step 4: Implement the exact canonical byte stream**

Write ASCII/UTF-8 bytes directly to a `MemoryStream`:

```text
oni-release-content-manifest-v1\n
<content-area>\0<normalized-relative-path>\0<byte-length>\0<sha256>\0<role>\n
```

Map enum values explicitly to `workshop-content`, `workshop-listing`, `runtime`, `description`, `change-notes`, and `preview`. Sort first by normalized relative path ordinally and then, only to break an equal-path tie across the two areas, by mapped content-area string ordinally. The JSON manifest contains the same ordered entries plus `schemaVersion: 1` and the digest; its own bytes are not recursively included.

- [ ] **Step 5: Implement streamed file hashing**

Open files read-only with sequential-scan intent, hash through `SHA256`, record `long` byte length from the same stream, and lowercase with `Convert.ToHexString(...).ToLowerInvariant()`. Reject files whose resolved path leaves the declared release-content root or is a link/reparse point.

- [ ] **Step 6: Run content-integrity and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass and the golden digest matches exactly on Windows.

- [ ] **Step 7: Review and commit canonical integrity support**

After explicit authorization, create:

```text
feat: define canonical ONI release content hashes
```

### Task 13: Validate and Render Workshop Listing Handoff Artifacts

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/BbCodeValidator.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/ListingTextRenderer.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/ListingTextReport.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/PreviewImageInspector.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/WorkshopListingAssembler.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/WorkshopListingValidator.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/WorkshopListing/BbCodeValidatorTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/WorkshopListing/ListingTextRendererTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/WorkshopListing/PreviewImageInspectorTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/WorkshopListing/WorkshopListingAssemblerTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Fixtures/workshop-description-structure.bbcode`

**Interfaces:**
- Consumes: `WorkshopListingProfile`, `ContentHasher`, and an empty candidate listing directory
- Produces: `WorkshopListingValidator.ValidateAsync(...)`, `WorkshopListingAssembler.AssembleAsync(...)`, `description.bbcode`, `change-notes.bbcode`, normalized preview, and representation reports

```csharp
internal sealed record ListingTextReport(
    string Encoding,
    bool HasBom,
    string LineEndings,
    int LogicalLineCount,
    int LineBreakCount,
    int BlankLineCount,
    long Utf8ByteCount,
    string LogicalContentSha256,
    string ArtifactSha256);
```

- [ ] **Step 1: Add the structural fixture and failing renderer tests**

The LF fixture must contain a paragraph, two consecutive blank lines, `---`, Unicode text and an emoji, `[h1]`, `[list]`, two `[*]` items, a `[url=https://example.invalid]` link, and exactly one final LF.

```csharp
[DataTestMethod]
[DataRow("lf")]
[DataRow("crlf")]
[DataRow("cr")]
[DataRow("mixed")]
public void Render_WhenInputUsesAnyLineBreakStyle_PreservesOneLogicalDocument(string variant)

[TestMethod]
public void Render_WhenCalledTwice_IsLogicallyIdempotent()

[TestMethod]
public void Render_WhenArtifactIsWritten_UsesNoBomAndOnlyCrLfWithOneFinalCrLf()
```

- [ ] **Step 2: Write failing validation tests**

Cover source CR/CRLF rejection, absent/multiple final LF, empty notes, the four reserved whole-file values `TODO`, `TBD`, `CHANGEME`, and `ONI_MOD_PIPELINE_CHANGE_NOTES_REQUIRED` (case-insensitive after trimming), 8,001 UTF-8 bytes, 8,000 UTF-8 bytes, Markdown links, unsupported URL schemes, unbalanced/nonnested paired BBCode, and valid `[*]` items inside `[list]`.

- [ ] **Step 3: Write failing image-signature tests**

Use minimal byte fixtures for PNG (`89 50 4E 47 0D 0A 1A 0A`), JPEG (`FF D8 FF`), GIF87a, and GIF89a. Require extension/signature agreement, normalize `.jpeg` to candidate `.jpg`, reject other extensions, preserve bytes exactly, and never call `System.Drawing` or transcode.

- [ ] **Step 4: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~WorkshopListing"
```

Expected: compilation fails because listing modules do not exist.

- [ ] **Step 5: Implement logical-line normalization and CRLF emission**

Parse `\r\n`, lone `\r`, and lone `\n` into logical line boundaries without chained replacement. Remove all terminal empty line breaks, append exactly one logical terminator, compute the logical digest over UTF-8 LF bytes, then emit each boundary as CRLF using `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)`. Define `LogicalLineCount` as `LineBreakCount + 1`, including the terminal empty segment implied by the required final newline; define `BlankLineCount` over content segments and exclude that terminal segment. Re-read emitted bytes and prove there is no BOM, no lone CR/LF, and exactly one final CRLF.

- [ ] **Step 6: Implement conservative BBCode validation**

Check balanced proper nesting for `b`, `i`, `u`, `strike`, `spoiler`, `h1`, `h2`, `h3`, `url`, `list`, and `quote`. Treat `[*]` as a list item token, and accept literal `---`. Only recognized tag names participate in nesting, so bracketed display-name text such as `[sd] QooLiO` is preserved literally. Reject Markdown `[label](url)` syntax with `ONIP1006`. Accept only `https` and `http` URL schemes in v1.

- [ ] **Step 7: Assemble listing artifacts outside Workshop content**

Require an empty target directory. Render `description.bbcode` and `change-notes.bbcode`; copy the preview bytes under a lower-case validated extension. Produce reports in memory for later provenance; do not place evidence beside listing artifacts.

- [ ] **Step 8: Complete read-only listing validation**

Wire `WorkshopListingValidator` into `validate` so the command applies source LF/final-newline rules, BBCode checks, UTF-8 byte ceilings, placeholder rejection, and preview signature/extension checks without creating candidate files. Preserve the same diagnostics between standalone validation and release assembly.

Map schema identifiers to current Uploader labels through one ordinal dictionary used by validation and summaries: `language` → `language`, `worldgen` → `worldgen`, `new-features` → `new features`, `tweaks` → `tweaks`, `ui` → `ui`; `base-game` → `Base Game`, `spaced-out` → `Spaced Out!`, `frosty-planet-pack` → `The Frosty Planet Pack`, `bionic-booster-pack` → `The Bionic Booster Pack`, `prehistoric-planet-pack` → `The Prehistoric Planet Pack`, and `aquatic-planet-pack` → `The Aquatic Planet Pack`. Unknown identifiers fail instead of being displayed verbatim.

- [ ] **Step 9: Test the real source representation**

Load `STEAM_DESCRIPTION.bbcode` and assert its current logical line count is `54`, its source is LF-only, and its generated artifact has `53` CRLF pairs with no lone line ending. Assert source/artifact logical SHA-256 values match.

- [ ] **Step 10: Run listing and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass; no clipboard or desktop API has been introduced.

- [ ] **Step 11: Review and commit Uploader-compatible listing output**

After explicit authorization, create:

```text
feat: render Uploader-safe Workshop listing artifacts
```

### Task 14: Assemble and Prove the Workshop Content Allowlist

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopContent/WorkshopContentAssembler.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/WorkshopContent/WorkshopContentValidator.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/WorkshopContent/WorkshopContentAssemblerTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/WorkshopContent/WorkshopContentValidatorTests.cs`

**Interfaces:**
- Consumes: `ModProfile.PackageFiles`, `BuildResult`, and a new empty staging root
- Produces: `WorkshopContentAssembler.AssembleAsync(ModProfile, BuildResult, string stagingRoot, CancellationToken)` returning the exact runtime `FileDigest` set

- [ ] **Step 1: Write failing allowlist tests**

Cover one file mapping, one declared contained directory mapping, `{build-output}` expansion, duplicate destinations, source links, destination collisions, zero-byte primary DLL, missing root metadata, and a stale undeclared file already present in staging.

- [ ] **Step 2: Write failing forbidden-content tests**

Reject case-insensitively:

```text
0Harmony.dll
Assembly-CSharp.dll
Assembly-CSharp-firstpass.dll
Unity*.dll
FMOD*.dll
Newtonsoft.Json.dll
PLib.dll
```

Also reject `.cs`, `.csproj`, `.sln`, `.slnx`, `.ps1`, `.bat`, `.sh`, `.pdb`, lock files, logs, `bin`, `obj`, `Tests`, and `release-evidence` unless a future schema revision supplies an explicitly reviewed runtime role. PLib must be merged into the primary DLL rather than copied loose.

- [ ] **Step 3: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~WorkshopContent"
```

Expected: compilation fails because the assembler does not exist.

- [ ] **Step 4: Implement copy-to-empty staging**

Resolve every source through `ContainedPathResolver`; `{build-output}` sources must also match a hashed `BuildResult.Outputs` entry. Map destination separators to `/` for validation, then to the host separator for writing. Copy files with `FileMode.CreateNew`. Directory mappings recursively enumerate regular files only, sorted by normalized relative path, and remain beneath their one declared subtree.

- [ ] **Step 5: Prove staging closure after copy**

Walk the completed staging tree without following links. Compare its normalized path set one-for-one with the expanded declared mapping set. Require `mod.yaml`, `mod_info.yaml`, and the non-empty primary assembly at the root. Hash actual staged bytes and verify build-sourced hashes equal `BuildResult`.

- [ ] **Step 6: Test the Delivery Temperature Limit inventory**

Using a synthetic successful `BuildResult`, assert the completed tree is exactly:

```text
DeliveryTemperatureLimit.dll
mod.yaml
mod_info.yaml
```

`Preview.png`, screenshots, BBCode, source, tests, scripts, and release evidence must be absent. This is the intentional listing/runtime separation; it supersedes the legacy batch script's convenience copy of `Preview.png`.

- [ ] **Step 7: Run content and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass and staging closure is exact.

- [ ] **Step 8: Review and commit allowlisted packaging**

After explicit authorization, create:

```text
feat: assemble closed Workshop content packages
```

### Task 15: Prepare All-or-Nothing Release Candidates and Identity Evidence

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/AcceptanceTestPlan.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/BuildProvenance.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/CandidateLayout.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseCandidatePreparer.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseCandidateState.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseReadinessReport.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseSummaryRenderer.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/RunIdFactory.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/UploaderChecklistRenderer.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/CandidateLayoutTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/ReleaseCandidatePreparerTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/RunIdFactoryTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/PrepareReleaseCommandTests.cs`

**Interfaces:**
- Consumes: validated release-clean profile/environment, locked build, declared tests, content/listing assemblers, canonical manifest, provenance, and artifact writer
- Produces: `ReleaseCandidatePreparer.PrepareAsync(...)`, CLI `prepare-release`, and one complete `awaiting-acceptance` candidate or no final candidate

```csharp
internal enum ReleaseCandidateState
{
    AwaitingAcceptance,
    AcceptanceFailed,
    ReadyForUpload,
    VerificationFailed
}

internal sealed record PreparedReleaseCandidate(
    string CandidateDirectory,
    CandidateLayout Layout,
    ReleaseContentManifest ContentManifest,
    BuildProvenance Provenance,
    ReleaseCandidateState State);
```

- [ ] **Step 1: Write failing run-ID and layout tests**

```csharp
[TestMethod]
public void Create_WhenGivenUtcAndEightBytes_UsesSortableCollisionResistantFormat()
{
    var id = RunIdFactory.Create(
        new DateTimeOffset(2026, 8, 27, 14, 3, 2, TimeSpan.Zero).AddTicks(1234567),
        Convert.FromHexString("0123456789abcdef"));

    Assert.AreEqual("20260827T140302.1234567Z-0123456789abcdef", id);
}
```

`CandidateLayout` must derive every path beneath:

```text
<artifacts>/release-candidates/<static-id>/<version>/<run-id>/
```

and expose exact properties for both content directories and every evidence file named in the specification.

Inject the BCL `TimeProvider` into preparer/installer/acceptance constructors; production uses `TimeProvider.System` and tests use a fixed provider. Production run IDs obtain exactly eight random bytes from `RandomNumberGenerator.GetBytes(8)` and pass them with `TimeProvider.GetUtcNow()` to the pure `RunIdFactory.Create` method.

- [ ] **Step 2: Write failing all-or-nothing preparation tests**

Cover a failing restore, failing test, listing validation error, packaging error, hash error, evidence write error, and final rename collision (`ONIP5007`). In every case, assert no final run-ID directory exists and both unique staging/work siblings are removed. Also assert cleanup failure is secondary `ONIP9002` and never replaces the primary one.

- [ ] **Step 3: Write a successful candidate-contract test**

With faked build/test modules and real temporary filesystem modules, require exactly:

```text
workshop-content/
workshop-listing/
release-evidence/release-readiness-report.json
release-evidence/release-content-manifest.json
release-evidence/build-provenance.json
release-evidence/automated-test-results/<id>.trx
release-evidence/acceptance-test-plan.json
release-evidence/release-summary.md
release-evidence/uploader-checklist.md
```

Assert `installation-receipt.json` and `acceptance-test-results.json` are absent immediately after preparation.

- [ ] **Step 4: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ReleaseCandidatePreparerTests"
```

Expected: compilation fails because release preparation does not exist.

- [ ] **Step 5: Implement safe candidate layout and staging**

Validate `<static-id>` and `<version>` as single filesystem segments. Create a candidate sibling named `.<run-id>.staging-<guid>` and a separate transient build sibling named `.<run-id>.work-<guid>` beneath the version directory. Resolve both and the final path beneath the selected artifact root before any creation or cleanup. Require the final path to be absent. The staging sibling contains only the final three top-level candidate directories; restore/build intermediates and `build-result.json` remain under the work sibling. Delete the validated work sibling after content/evidence assembly, then promote with `Directory.Move(staging, final)` only after all checks succeed.

- [ ] **Step 6: Orchestrate release preparation in fail-closed order**

Perform exactly:

1. locate/load/validate profile and ONI metadata;
2. discover environment;
3. construct the relevant source set and require committed clean inputs;
4. create the candidate staging and transient work layouts;
5. run locked Release build into the transient work directory;
6. run all required tests into staged `automated-test-results`;
7. assemble `workshop-content/` and `workshop-listing/` into empty directories;
8. hash both content areas and write `release-content-manifest.json`;
9. write immutable build provenance and acceptance plan;
10. write derived awaiting-acceptance readiness, summary, and checklist files;
11. re-hash every immutable prepared file and re-check contributing-source cleanliness;
12. delete the validated transient work directory; and
13. atomically promote the candidate staging directory.

No caught failure is converted into a partial candidate.

- [ ] **Step 7: Write exact build provenance**

`build-provenance.json` records schema version, pipeline informational version/executable SHA-256, static ID/version, commit and scoped cleanliness, one UTC preparation time, OS/architecture, exact SDK, target framework/configuration, hash of each lock file, game-build metadata, game reference hashes, structured build arguments, primary/merge input/output hashes, assembly/file/informational versions, listing representation reports, acceptance-plan hash, and release-content digest. Replace personal absolute prefixes with named roots such as `${WORKTREE}`, `${GAME}`, and `${ARTIFACTS}` in evidence fields that do not require an actionable absolute path.

- [ ] **Step 8: Render the immutable acceptance plan**

Copy every profile acceptance check's `id`, `title`, `required`, `setup`, `action`, and `expected` fields in declared order. Add candidate static ID, version, content digest, preparation time, and schema version. Write once; record its SHA-256 in provenance.

- [ ] **Step 9: Render the initial derived documents**

The summary includes exact candidate content/listing paths, digest, build/test status, tags/DLC selections, and `awaiting-acceptance`. The checklist disables publication until state becomes `ready-for-upload`; it already names the exact Update Data, description, change-note, and preview paths. Render those two first, hash them, then render the initial readiness report last with `state: awaiting-acceptance`, their evidence-index entries, and missing installation/acceptance evidence as blocking conditions.

- [ ] **Step 10: Register `prepare-release`**

```text
oni-mod-pipeline prepare-release --mod <path> [environment overrides] [--format human|json]
```

There is no dirty bypass, skip-test option, candidate reuse, overwrite, publish option, or interactive prompt. Success prints the absolute candidate path, content digest, and `awaiting-acceptance` state.

- [ ] **Step 11: Run candidate and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass; failure tests leave no final candidate.

- [ ] **Step 12: Review and commit candidate preparation**

After explicit authorization, create:

```text
feat: prepare immutable ONI release candidates
```

### Task 16: Install Exact Builds and Candidates with Ownership Guards

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModInstallation/InstallTarget.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModInstallation/InstallationReceipt.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModInstallation/ModInstaller.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ModInstallation/OwnershipMarker.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ModInstallation/ModInstallerTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/InstallCommandTests.cs`

**Interfaces:**
- Consumes: a manifest-verified candidate or explicit `BuildResult` plus profile; `PipelineEnvironment`; `ContentHasher`
- Produces: guarded `Dev`/`Local` installation and, for candidates only, write-once `installation-receipt.json`

```csharp
internal enum InstallTarget { Dev, Local }

internal sealed record OwnershipMarker(
    int SchemaVersion,
    string StaticId,
    string ManagedDirectoryName,
    string InstalledContentDigest);

internal sealed record InstallationReceipt(
    int SchemaVersion,
    string StaticId,
    string Version,
    string ContentDigest,
    InstallTarget Target,
    string AbsoluteTargetPath,
    DateTimeOffset InstalledAtUtc,
    bool InstalledFilesVerified);
```

- [ ] **Step 1: Write failing destination-ownership tests**

```csharp
[TestMethod]
public async Task InstallAsync_WhenDestinationExistsWithoutMarker_ReturnsOnip4001AndPreservesBytes()

[TestMethod]
public async Task InstallAsync_WhenMarkerStaticIdDiffers_ReturnsOnip4001AndPreservesBytes()

[TestMethod]
public async Task InstallAsync_WhenOwnedDestinationExists_ReplacesItThroughVerifiedSiblingStaging()

[TestMethod]
public async Task InstallAsync_WhenSwapFails_RestoresOwnedPreviousInstallation()
```

Also assert the installer refuses a destination equal to the mods root, user-data root, Documents/home root, or any ancestor of those paths.

- [ ] **Step 2: Write failing candidate-receipt tests**

Cover digest mismatch before copy, tampering during copy, existing receipt, installed-byte mismatch, and success. A development `BuildResult` install must never create a candidate receipt.

- [ ] **Step 3: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ModInstallerTests"
```

Expected: compilation fails because `ModInstaller` does not exist.

- [ ] **Step 4: Resolve and guard the exact install target**

Derive `<user-data>/mods/Dev/<directory-name>` or `<user-data>/mods/Local/<directory-name>`. Require the destination to be a strict descendant of the selected target root. If it exists, read `.oni-mod-pipeline-owner.json` and require schema `1`, matching static ID, and matching managed directory name. V1 has no force, adopt, or arbitrary destination switch.

For a development build result, reload the explicitly supplied mod profile and recompute every `BuildResult.Inputs` and `BuildResult.Outputs` hash before assembling its runtime package. Any changed/missing input or output invalidates that build result; never install an implicit latest build.

- [ ] **Step 5: Stage, verify, swap, and recover**

Create one sibling staging directory under the target root, copy only manifest-verified runtime content, write the ownership marker, and re-hash installed runtime bytes excluding the marker. For an owned replacement, move the old directory to a unique sibling backup, move staging to the final path, verify again, then delete only that validated backup. On failure after moving the old directory, remove only the validated new destination and move the backup back. Every recursive operation rechecks containment immediately before execution.

- [ ] **Step 6: Write candidate receipt exactly once**

After candidate bytes are live and reverified, atomically create `release-evidence/installation-receipt.json` with `FileMode.CreateNew`. If the receipt already exists, fail before touching the install destination. A candidate may have one acceptance installation only.

- [ ] **Step 7: Warn about duplicate subscribed copies without mutating Steam state**

Read `mods/Steam/*/mod.yaml` beneath the selected user-data directory and compare `staticID`. Emit non-blocking warning `ONIP2005` naming any duplicate. Never edit subscriptions, mod enablement files, or Steam directories.

- [ ] **Step 8: Register exact install forms**

```text
oni-mod-pipeline install --candidate <directory> --target dev [environment overrides]
oni-mod-pipeline install --candidate <directory> --target local [environment overrides]
oni-mod-pipeline install --mod <path> --build-result <build-result.json> --target dev [environment overrides]
oni-mod-pipeline install --mod <path> --build-result <build-result.json> --target local [environment overrides]
```

Enforce candidate versus mod/build-result mutual exclusivity at parse time. Never infer a latest artifact.

- [ ] **Step 9: Run installation and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass, including rollback and unowned-directory preservation.

- [ ] **Step 10: Compare development-install inventory with the legacy script**

Install a real build result into a temporary explicit user-data root. Confirm the runtime inventory contains the same mod metadata and DLL as the legacy deployment while `Preview.png` is deliberately absent because it is a listing field. Do not touch the developer's actual ONI directory in this comparison.

- [ ] **Step 11: Review and commit guarded installation**

After explicit authorization, create:

```text
feat: install exact ONI artifacts with ownership guards
```

### Task 17: Record Digest-Bound Human Acceptance Exactly Once

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/AcceptanceRecorder.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/AcceptanceTestResults.cs`
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/IAcceptanceConsole.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/AcceptanceRecorderTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/RecordAcceptanceCommandTests.cs`

**Interfaces:**
- Consumes: immutable acceptance plan, content manifest/digest, installation receipt, live installed bytes, and interactive tester input
- Produces: write-once `acceptance-test-results.json` and derived `AcceptanceFailed`/acceptance-passed evidence

```csharp
internal enum AcceptanceOutcome { Passed, Failed }

internal sealed record AcceptanceCheckResult(
    string Id,
    string Title,
    string Setup,
    string Action,
    string Expected,
    AcceptanceOutcome Outcome,
    string? Note);

internal sealed record AcceptanceTestResults(
    int SchemaVersion,
    string Tester,
    DateTimeOffset RecordedAtUtc,
    string ContentDigest,
    string AcceptancePlanSha256,
    IReadOnlyList<AcceptanceCheckResult> Checks);

internal interface IAcceptanceConsole
{
    bool IsInteractive { get; }
    void WriteLine(string value);
    string ReadRequired(string prompt);
    AcceptanceOutcome ReadOutcome(string prompt);
    string? ReadOptional(string prompt);
}
```

- [ ] **Step 1: Write failing evidence-precondition tests**

Cover non-interactive input (`ONIP5008`), missing receipt, receipt digest mismatch, current content digest mismatch, installed-byte mismatch, acceptance-plan hash mismatch, empty tester, and an existing results file. Each must fail before prompting or modifying evidence.

- [ ] **Step 2: Write failing passed/failed recording tests**

Use a fake interactive console. A required check accepts only `passed` or `failed`, never skipped. Verify every result copies the immutable setup/action/expected text, a failed result is preserved, and a second invocation cannot replace it.

- [ ] **Step 3: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~AcceptanceRecorderTests"
```

Expected: compilation fails because `AcceptanceRecorder` does not exist.

- [ ] **Step 4: Verify candidate and live installation before prompting**

Recompute all release-content hashes, verify manifest bytes/digest, verify the receipt, then hash the live install files named by `workshop-content/` while ignoring only `.oni-mod-pipeline-owner.json`. Require exact path, size, and hash equality. Re-hash `acceptance-test-plan.json` and compare provenance.

- [ ] **Step 5: Record one complete interactive attestation**

Print candidate ID/version/digest before the checks. Read tester from `--tester` or prompt; trim outer whitespace and reject empty. For each check, print title, setup, action, and expected observation, then read outcome and optional note. Write the complete result atomically with `FileMode.CreateNew` only after all checks have responses.

- [ ] **Step 6: Register `record-acceptance`**

```text
oni-mod-pipeline record-acceptance --candidate <directory> [--tester <display-name>]
```

This command has no JSON result-import mode in v1. A failed check returns exit `6` after preserving the results file; all passed checks return `0` but the candidate is not yet ready until `verify-release` succeeds.

- [ ] **Step 7: Run acceptance and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass; repeat and tamper cases cannot overwrite evidence.

- [ ] **Step 8: Review and commit acceptance recording**

After explicit authorization, create:

```text
feat: bind ONI acceptance results to installed candidate bytes
```

### Task 18: Verify Readiness and Render the Human Uploader Handoff

**Files:**
- Create: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseCandidateVerifier.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseReadinessReport.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/ReleaseSummaryRenderer.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/ReleaseCandidates/UploaderChecklistRenderer.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/PipelineServices.cs`
- Modify: `tools/oni-mod-pipeline/src/OniModPipeline/Cli/CliApplication.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/ReleaseCandidateVerifierTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/ReleaseSummaryRendererTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/UploaderChecklistRendererTests.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/ReleaseCandidates/CandidateFixture.cs`
- Create: `tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/VerifyReleaseCommandTests.cs`

**Interfaces:**
- Consumes: the entire candidate contract and current release-content bytes
- Produces: `ReleaseCandidateVerifier.VerifyAsync(string candidateDirectory, CancellationToken)`, deterministic derived evidence, and CLI `verify-release`

- [ ] **Step 1: Write failing state-derivation tests**

Cover awaiting acceptance, failed required acceptance, all-passed acceptance, missing TRX, failing TRX outcome, dirty/mismatched provenance, changed content byte, added undeclared content file, changed acceptance plan, changed install receipt, and invalid CRLF representation. Assert exact state and blocking diagnostic IDs.

Add `VerifyAsync_WhenObservedContentTamperIsLaterRestored_RemainsVerificationFailed`: after one verification records an immutable content/evidence breach, restoring the bytes does not make that run ID eligible again.

- [ ] **Step 2: Write the tamper-invalidation test**

```csharp
[TestMethod]
public async Task VerifyAsync_WhenReadyCandidateContentIsTampered_ReturnsOnip5002AndNeverReady()
{
    var candidate = await CandidateFixture.CreateReadyAsync();
    await File.AppendAllTextAsync(
        candidate.Layout.DescriptionPath,
        "tamper",
        new UTF8Encoding(false));

    var result = await candidate.Verifier.VerifyAsync(candidate.Root, CancellationToken.None);

    Assert.IsFalse(result.IsSuccess);
    Assert.AreEqual(PipelineExitCode.ReleaseNotReady, result.ExitCode);
    Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "ONIP5002"));
}
```

- [ ] **Step 3: Write the repeatability test**

Verify a ready fixture twice. Compare exact bytes and SHA-256 for `release-readiness-report.json`, `release-summary.md`, and `uploader-checklist.md` after each run. They must be identical. The verifier must not insert a fresh current time.

- [ ] **Step 4: Run focused tests and confirm failure**

Run:

```text
dotnet test --project tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj --no-restore -- --filter "FullyQualifiedName~ReleaseCandidateVerifierTests"
```

Expected: compilation fails because the verifier does not exist.

- [ ] **Step 5: Implement fail-closed verification**

Recompute and validate:

1. candidate layout and absence of unknown files in immutable areas;
2. every release-content entry/hash/size/role and the canonical digest;
3. each artifact's recomputed logical digest against the source logical digest frozen in provenance, plus CRLF byte rules;
4. build provenance, lock/reference/output hashes, and scoped commit identity;
5. required TRX existence and passed outcome;
6. immutable acceptance-plan hash;
7. installation receipt digest and live installed bytes;
8. acceptance results, required passes, plan hash, and content digest; and
9. evidence index hash/length for every evidence file except the readiness report itself.

Derive state from evidence. Never trust a previously serialized `state` value. The one permitted carry-forward is a verifier-authored `irreversibleInvalidation` reason from the prior readiness report after an observed immutable content/evidence mismatch; preserve that reason on every later verification so the candidate cannot return to ready. Missing, malformed, or manually edited derived evidence fails closed. Ordinary `awaiting-acceptance` blockers do not set irreversible invalidation.

Parse each TRX with `XDocument` using its declared namespace. Require one `ResultSummary` whose `outcome` is `Completed`, one `Counters` element with `executed > 0`, and zero `failed`, `error`, `timeout`, and `aborted` counts; malformed or inconsistent counters are build/test evidence failures.

- [ ] **Step 6: Render the final readiness report as the evidence index**

Use the preparation/install/acceptance event times already present in immutable or write-once evidence. Sort checks and evidence paths ordinally. Record byte length and SHA-256 for build provenance, content manifest, every TRX, acceptance plan/results, receipt, summary, and checklist; exclude only `release-readiness-report.json` to avoid recursion.

- [ ] **Step 7: Render the final release summary**

Include static ID, title, version, commit, content digest, SDK/game build, automated test table, acceptance table, warnings, exact absolute Update Data directory, exact description/change-note/preview paths, preview format/size, mod types, DLC selections, and `ready-for-upload`. State plainly that Steam publication has not occurred.

- [ ] **Step 8: Render the exact Uploader checklist**

Require the human to confirm:

```text
[ ] Candidate state is ready-for-upload.
[ ] Update Data points exactly to <candidate>/workshop-content.
[ ] The displayed data path is not the mutable Dev/Local test directory.
[ ] Description comes from <candidate>/workshop-listing/description.bbcode.
[ ] Paragraphs, blank lines, ---, headings, and [list] blocks remain separate after paste.
[ ] Change notes come from <candidate>/workshop-listing/change-notes.bbcode.
[ ] Preview comes from the exact generated preview path.
[ ] Title, mod types, tags, and DLC compatibility match release-summary.md.
[ ] The final form has been reviewed immediately before Publish.
```

End with: `Publish is a deliberate authenticated human action. ONI Mod Pipeline does not perform or record it.`

- [ ] **Step 9: Atomically replace only derived evidence**

Render summary and checklist first from the fully derived in-memory verification result, hash those rendered bytes for the readiness evidence index, then render the readiness report last; none of the first two documents includes the readiness report's hash, so there is no cycle. Write each derived file to a unique same-directory temporary file, flush and close it, then replace the named destination. If any write fails, preserve prior derived files where possible, report verification failure, and never touch immutable content/evidence.

- [ ] **Step 10: Register `verify-release`**

```text
oni-mod-pipeline verify-release --candidate <directory> [--format human|json]
```

Success is only `ready-for-upload` with exit `0`. Every other state returns exit `6`. The command is non-interactive and has no Uploader/open/publish side effect.

- [ ] **Step 11: Run verifier and solution tests**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Expected: all tests pass, including exact repeatability and tamper invalidation.

- [ ] **Step 12: Review Gate C and commit lifecycle completion**

Confirm candidates stop at `ready-for-upload`, no Steam SDK/credential/UI code exists, and evidence never appears beneath `workshop-content`. After explicit authorization, create:

```text
feat: verify upload-ready ONI release candidates
```

### Task 19: Remove Superseded Shell Workflows and Document One Supported Path

**Files:**
- Delete: `mods/delivery-temperature-limit-supercooled/Source/build.sh`
- Delete: `mods/delivery-temperature-limit-supercooled/scripts/deploy_mod_locally.bat`
- Delete: `mods/delivery-temperature-limit-supercooled/Tests/BuildingsEligibility.Tests.ps1`
- Delete: `mods/delivery-temperature-limit-supercooled/Tests/ModInfoVersion.Tests.ps1`
- Modify: `README.md`
- Create: `docs/guides/oni-mod-development-workflow.md`

**Interfaces:**
- Consumes: all green Gates A–C and their replacement commands
- Produces: one documented shell-free local workflow and no old-command shims

- [ ] **Step 1: Reconfirm exact deletion approval**

Show the four paths, their replacements, and fresh passing evidence:

| Deleted path | Proven replacement |
|---|---|
| `Source/build.sh` | `oni-mod-pipeline build` / `prepare-release` plus locked isolated MSBuild |
| `scripts/deploy_mod_locally.bat` | guarded `oni-mod-pipeline install` |
| `Tests/BuildingsEligibility.Tests.ps1` | `BuildingsEligibilityTests.cs` in declared MSTest project |
| `Tests/ModInfoVersion.Tests.ps1` | `ModBuildContractTests.cs` plus generic no-source-mutation checks |

Stop until the deletion is explicitly approved if it was not included in the current exact configuration authorization.

- [ ] **Step 2: Run all replacements before deleting predecessors**

Run:

```text
dotnet restore tools/oni-mod-pipeline/OniModPipeline.slnx --locked-mode
```

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- build --mod mods/delivery-temperature-limit-supercooled
```

Expected: all succeed; metadata/source byte snapshots remain unchanged.

- [ ] **Step 3: Delete exactly the four approved legacy files**

Use targeted file deletion. Do not delete `Source/.gitignore`, the tracked baseline DLL, `clean.py`, or unrelated user files. Report that Git can recover the deleted scripts until the deletion commit is made.

- [ ] **Step 4: Rewrite README's development and release sections**

Document prerequisites, profile discovery, environment overrides, and this one normal path:

```text
oni-mod-pipeline diagnose
oni-mod-pipeline validate
oni-mod-pipeline test
oni-mod-pipeline prepare-release
oni-mod-pipeline install --candidate <path> --target local
# perform in-game acceptance
oni-mod-pipeline record-acceptance --candidate <path>
oni-mod-pipeline verify-release --candidate <path>
# inspect release-summary.md and uploader-checklist.md
# publish manually with the authenticated ONI Uploader
```

State that versions and `STEAM_CHANGE_NOTES.bbcode` are deliberate reviewed source edits made before preparation; build never increments or rewrites them.

- [ ] **Step 5: Add the detailed generic workflow guide**

`docs/guides/oni-mod-development-workflow.md` must cover:

1. onboarding a new mod with schema-v1 profile examples;
2. local diagnose/validate/build/test/install iteration;
3. version and Workshop-text review;
4. release cleanliness and locked dependency maintenance;
5. candidate preparation and content/evidence separation;
6. local duplicate-mod precautions and log review;
7. digest-bound acceptance and write-once failure semantics;
8. deterministic verification and tamper recovery by creating a new candidate;
9. CRLF description/change-note copy workflow;
10. the exact human Uploader checklist and stop-before-Publish boundary; and
11. troubleshooting by diagnostic ID and artifact path.

Do not mention deleted scripts as alternatives.

- [ ] **Step 6: Prove no supported shell workflow remains**

Run:

```text
rg -n --hidden --glob !.git/** --glob !docs/specs/** --glob !docs/plans/** "powershell|pwsh|deploy_mod_locally|build\.sh|\.Tests\.ps1" README.md mods tools docs/guides
```

Expected: no matches in supported workflow/source/test files. Historical architecture and implementation-plan references are intentionally excluded from this assertion.

- [ ] **Step 7: Run the complete automated suite after deletion**

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Expected: all tests pass without any deleted file.

- [ ] **Step 8: Review and commit workflow migration**

After explicit authorization, create:

```text
docs: establish the automated ONI mod workflow
```

The commit includes the four deletions and both documentation changes so no commit presents undocumented or duplicate supported workflows.

### Task 20: Rehearse the Real Release Lifecycle Through the Human Uploader Gate

**Files:**
- Generated only beneath ignored `artifacts/`: one release candidate and its lifecycle evidence
- Modify tracked files only if the rehearsal exposes a defect; any repair returns to its owning task and repeats all dependent checks

**Interfaces:**
- Consumes: the clean committed implementation, current installed ONI game, current Windows Notepad, current authenticated Klei ONI Uploader, and the Delivery Temperature Limit acceptance plan
- Produces: one verified `ready-for-upload` candidate; no Steam publication

- [ ] **Step 1: Verify a clean attributable release scope**

Run:

```text
git status --short --branch
```

Expected: no tracked changes in the mod, profile, tests, tool, lock files, or release documentation. Unrelated untracked files outside the contributing set do not block preparation.

- [ ] **Step 2: Restore and run all automated tests from locked dependencies**

Run:

```text
dotnet restore tools/oni-mod-pipeline/OniModPipeline.slnx --locked-mode
```

Run:

```text
dotnet test --solution tools/oni-mod-pipeline/OniModPipeline.slnx --no-restore
```

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- test --mod mods/delivery-temperature-limit-supercooled
```

Expected: all generic and mod tests pass; TRX evidence is produced for the declared project.

- [ ] **Step 3: Diagnose and validate for release**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- diagnose --mod mods/delivery-temperature-limit-supercooled
```

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- validate --mod mods/delivery-temperature-limit-supercooled --for-release
```

Expected: exact SDK/game/user-data/artifact paths are shown; release validation succeeds without source changes.

- [ ] **Step 4: Prepare a fresh candidate**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- prepare-release --mod mods/delivery-temperature-limit-supercooled
```

Expected: one new candidate is reported in `awaiting-acceptance`; content consists of exactly three runtime files plus the three separately staged listing artifacts; all content hashes verify.

- [ ] **Step 5: Inspect the candidate before installing**

Open `release-summary.md`, `release-content-manifest.json`, and `uploader-checklist.md`. Confirm version, commit, content digest, automated test result, Uploader tags/DLC selections, and exact paths. Do not edit the candidate.

- [ ] **Step 6: Prepare the game for an unambiguous local test**

Disable the subscribed Steam copy of the same static ID in ONI. If the target Local directory is hand-maintained and unowned, move it aside manually rather than asking the pipeline to adopt or erase it. Exit ONI before installation.

- [ ] **Step 7: Install the exact candidate**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- install --candidate <candidate-directory> --target local
```

Expected: exact installed hashes verify; one installation receipt is created; the candidate digest remains unchanged.

- [ ] **Step 8: Execute every in-game acceptance check**

Use the candidate's immutable plan. For the Storage Tile rocket case:

1. load the designated release-test colony;
2. configure a Storage Tile aboard a rocket with a bounded safe temperature range;
3. expose one out-of-range material and one in-range control material;
4. provide a competing valid delivery target if errand generation requires it;
5. observe that Dupes refuse the invalid Storage Tile delivery;
6. observe that the in-range control remains deliverable; and
7. review `Player.log` for relevant exceptions.

Also execute the Storage Bin, construction-option, side-screen edit, keyboard/camera, save/load, and log checks exactly as rendered.

- [ ] **Step 9: Test the actual Uploader newline seam without publishing**

Open the generated candidate `workshop-listing/description.bbcode` in current Windows Notepad, select/copy its text, open the authenticated ONI Uploader's Edit Mod form, and paste into Description while every update checkbox remains disabled. Confirm paragraphs, blank lines, `---`, headings, and `[list]` blocks remain on separate lines. Record Notepad and Uploader versions in the check note. Cancel the form; do not click Publish.

- [ ] **Step 10: Record the acceptance results**

Run:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- record-acceptance --candidate <candidate-directory> --tester <display-name>
```

Enter each observed result truthfully. If any required check fails, preserve that failed candidate, fix the underlying issue in a new commit, and start again with a new candidate/run ID.

- [ ] **Step 11: Verify upload readiness twice**

Run twice:

```text
dotnet run --project tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj --no-restore -- verify-release --candidate <candidate-directory>
```

Expected both times: exit `0`, `ready-for-upload`, identical derived evidence hashes, unchanged content digest, and exact paths for Update Data, description, change notes, and preview.

- [ ] **Step 12: Stop at the deliberate human approval boundary**

The pipeline work is complete when `uploader-checklist.md` is satisfied and the directory is ready to select in the Uploader. Do not automate, simulate, or click Publish. The authenticated account holder performs the final visual review and publication as a separate deliberate action.

- [ ] **Step 13: Confirm the rehearsal did not change tracked source**

Run:

```text
git status --short --branch
```

Expected: only ignored `artifacts/` and the managed local ONI install changed. There is deliberately no rehearsal commit when tracked files remain unchanged.

---

## Final Verification Matrix

| Requirement | Primary task/test evidence |
|---|---|
| Local .NET 10 execution | Tasks 1, 7, 8 |
| Generic profile plus mod-specific layer | Tasks 3, 4, 9 |
| Strict safe paths and clean scope | Tasks 4, 6, 15 |
| SDK-style `net48` modernization | Task 11 |
| Locked restore | Tasks 1, 9, 10, 11, 20 |
| No source mutation | Tasks 6, 10, 11, 20 |
| C# replacement regressions | Tasks 9, 11, 19 |
| Isolated build and exact test evidence | Task 10 |
| Explicit runtime allowlist | Task 14 |
| CRLF direct-copy artifact | Tasks 13, 20 |
| Canonical hashes and digest | Task 12 |
| All-or-nothing candidate | Task 15 |
| Guarded Dev/Local install | Task 16 |
| Digest-bound human acceptance | Task 17 |
| Tamper-invalidated, repeatable readiness | Task 18 |
| No PowerShell/batch/Bash shims | Task 19 |
| Storage Tile aboard rocket acceptance | Task 20 |
| Human-only authenticated publication | Tasks 18, 20 |

## Expected Commit Sequence

Each line is a proposed review boundary, not standing authorization:

```text
build: establish locked .NET 10 pipeline foundation
feat: add stable pipeline diagnostics and exit codes
feat: load strict versioned ONI mod profiles
feat: validate portable ONI profile paths and semantics
feat: execute pipeline processes without a shell
feat: capture scoped source provenance
feat: discover portable local ONI environments
feat: add read-only ONI diagnose and validate commands
test: define Delivery Temperature Limit pipeline profile
feat: build ONI mods in isolation and capture TRX evidence
build: modernize the net48 ONI mod project
feat: define canonical ONI release content hashes
feat: render Uploader-safe Workshop listing artifacts
feat: assemble closed Workshop content packages
feat: prepare immutable ONI release candidates
feat: install exact ONI artifacts with ownership guards
feat: bind ONI acceptance results to installed candidate bytes
feat: verify upload-ready ONI release candidates
docs: establish the automated ONI mod workflow
```

Before every commit, inspect the exact staged file list and diff, run the task's named checks, and request explicit authorization. Never batch unrelated user-owned changes into these commits.
