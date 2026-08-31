# Temperature Limit Support Reporting and Community Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Temperature Limit players a local, privacy-conscious, one-file support reporter and give the repository concise issue, support, contribution, and pull-request paths that consume its output.

**Architecture:** A Unity/Klei-free `SupportReporting/Core` module owns immutable report contracts, bounded diagnostics, redaction, log selection, summary rendering, file naming, and fixed-origin issue URL construction. Thin ONI adapters capture loaded game/mod facts, serialize through the already referenced Newtonsoft.Json assembly, write a unique local report, and present the folder/browser result through the existing PLib options surface. The existing runtime patch planner remains the sole owner of patch/FastTrack facts and publishes a read-only support snapshot rather than allowing the reporter to repeat compatibility inspection.

**Tech Stack:** C# 8 and .NET Standard 2.1 for the game-loaded assembly; MSTest SDK 4.3.3 on .NET 10 for tests; Harmony 2; PLib 4.24 options; Newtonsoft.Json supplied by ONI; GitHub issue-form YAML; repository-local ONI Mod Pipeline.

**Spec:** `docs/specs/2026-08-31-temperature-limit-support-reporting-and-community-design.md`

## Global Constraints

- Production and every linked production source remain C# 8-compatible and target `netstandard2.1`; tests remain `net10.0` with the existing SDK/default language version.
- Add no package, package reference, project property, lockfile entry, test project, CI workflow, or pipeline-profile setting. The sole assembly-reference change is the approved non-copy-local `UnityEngine.IMGUIModule` game assembly required for clipboard access.
- The only test-project configuration change is the exact approved `SupportReporting/Core/**/*.cs` linked-compile item in Task 1.
- Apart from the approved `UnityEngine.IMGUIModule` reference in `Source/DeliveryTemperatureLimit.csproj`, do not modify that project file, either `packages.lock.json`, `global.json`, `oni-mod-pipeline.toml`, `mod.yaml`, `mod_info.yaml`, `STEAM_CHANGE_NOTES.bbcode`, or release/version metadata.
- Standard reports never read `Player.log`; extended reports read at most the most recent 6 MiB of raw log data and keep final JSON below 12 MiB.
- Retain at most 128 distinct diagnostics, 2,048 characters per diagnostic message, and 512 active-mod entries; disclose omitted/truncated data.
- Keep the complete prefilled issue URL at or below 1,800 characters and allow only the fixed `https://github.com/MaksymShostak/oxygen-not-included/issues/new` origin and `temperature-limit-bug.yml` template.
- Never collect or serialize an absolute path, user/account name, Steam user ID, IP/network information, environment variables, save/save metadata, screenshot, crash dump, or third-party configuration in a standard report.
- An extended log receives best-effort replacement of known user-profile, ONI persistent-data, and discovered installation-root prefixes before serialization; never rewrite the source log.
- All generation remains local. Add no HTTP client, telemetry, authentication, background upload, automatic issue submission, or persistent cross-report installation identifier.
- Preserve the two pre-existing user-owned untracked paths, `AGENTS.md` and `mods/delivery-temperature-limit-supercooled/screenshot-guidance.md`, without staging, editing, deleting, or restoring them.
- The user authorized the exact configuration, repository-policy, public-surface, and GitHub metadata changes in the spec. Any broader configuration or remote change stops for new exact approval.
- The user authorized only the already-created design-spec commit. Do not commit implementation changes until the formal review gate has completed and the user separately authorizes the exact implementation commit.
- After implementation and applicable verification, state exactly `Implementation complete; /review pending`, direct the user to `/review` → **Review uncommitted changes**, and ask the reviewer to ignore the two pre-existing untracked paths. Resolve or explicitly defer confirmed P0–P2 findings before any completion claim or implementation commit.

---

## File Structure

### Pure production core linked into tests

- `Source/SupportReporting/Core/SupportReportLimits.cs` — schema number, report/log/diagnostic/mod/URL ceilings, fixed GitHub origin/template, and stable unavailable marker.
- `Source/SupportReporting/Core/SupportReportKind.cs` — the exact standard and extended report modes.
- `Source/SupportReporting/Core/SupportReportDocument.cs` — immutable report root and section/value objects consumed by Newtonsoft.Json.
- `Source/SupportReporting/Core/SupportDiagnosticBuffer.cs` — thread-safe code-keyed diagnostic aggregation with deterministic bounded snapshots.
- `Source/SupportReporting/Core/SupportPathRedactor.cs` — longest-prefix-first replacement of explicitly supplied sensitive paths.
- `Source/SupportReporting/Core/SupportLogExcerptBuilder.cs` — recent-tail stream selection, UTF-8 decoding, redaction, JSON-escaped-size limiting, and truncation evidence.
- `Source/SupportReporting/Core/SupportJsonReportSizeLimiter.cs` — final serialized-size enforcement that can retain a smaller newest-log suffix while preserving an immutable report and disclosing the additional truncation.
- `Source/SupportReporting/Core/SupportReportSummaryRenderer.cs` — compact Markdown summary shared by clipboard and issue prefill.
- `Source/SupportReporting/Core/SupportIssueUrlBuilder.cs` — fixed-origin, percent-encoded, length-bounded issue-form URL construction.
- `Source/SupportReporting/Core/SupportReportFileName.cs` — stable UTC/report-ID JSON filename.

### ONI integration, compiled only into production

- `Source/SupportReporting/KleiIntegration/KleiSupportReportSnapshotReader.cs` — maps Klei/Unity/mod/options facts into core report inputs without retaining paths.
- `Source/SupportReporting/KleiIntegration/SupportReportJsonFileWriter.cs` — Newtonsoft serialization plus same-directory temporary write and unique atomic promotion.
- `Source/SupportReporting/KleiIntegration/SupportReportPlayerPresenter.cs` — clipboard, local-directory URI, fixed GitHub URL, and `KMod.Manager.Dialog` feedback.
- `Source/SupportReporting/KleiIntegration/DeliveryTemperatureSupportReporter.cs` — the one static internal facade for initialization, loaded-mod publication, diagnostics, standard/extended actions, and failure containment.

### Tests

- `Tests/SupportReporting/SupportReportDocumentTests.cs`
- `Tests/SupportReporting/SupportDiagnosticBufferTests.cs`
- `Tests/SupportReporting/SupportPathRedactorTests.cs`
- `Tests/SupportReporting/SupportLogExcerptBuilderTests.cs`
- `Tests/SupportReporting/SupportReportSummaryRendererTests.cs`
- `Tests/SupportReporting/SupportIssueUrlBuilderTests.cs`
- `Tests/SupportReporting/SupportReportFileNameTests.cs`
- `Tests/SupportReporting/SupportReportingSourceBoundaryTests.cs`
- Existing runtime-plan and intentional-public-surface suites are extended in their current files rather than duplicated.

### Existing source and public documentation

- Modify `Source/DeliveryTemperatureLimitMod.cs` for early reporter initialization, loaded-mod publication, and fail-closed diagnostic capture.
- Modify `Source/DeliveryTemperatureLimitOptions.cs` for exactly two non-persisted PLib `Action<object>` properties.
- Modify `Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs` to map its already-owned plan/FastTrack evidence into a support snapshot.
- Modify `Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs` to expose its current installation snapshot without reinspection.
- Route the existing noteworthy `Debug.Log*` integration/compatibility sites through the reporter's bounded diagnostic entry point without changing their severity or text semantics.
- Modify `Tests/DeliveryTemperatureAssemblyContracts/IntentionalRuntimeContractTests.cs` for the two intentional public option members and their non-serialized contract.
- Modify `Tests/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlanTests.cs` for support snapshot mapping.
- Create `CONTRIBUTING.md`, `SUPPORT.md`, both issue forms, issue-template configuration, and the pull-request template.
- Modify `README.md` and `mods/delivery-temperature-limit-supercooled/STEAM_DESCRIPTION.bbcode` with the real support route.

---

### Task 1: Link and establish the pure report contract

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportLimits.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportKind.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportDocument.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportDocumentTests.cs`

**Interfaces:**
- Produces: `SupportReportLimits`, `SupportReportKind`, `SupportReportFact`, `SupportReportDocument`, `SupportReportGameSnapshot`, `SupportReportTemperatureLimitSnapshot`, `SupportRuntimeSnapshot`, `SupportFastTrackSnapshot`, `SupportFastTrackFeatureSnapshot`, `SupportActiveModSnapshot`, `SupportDiagnosticSnapshot`, `SupportPlayerLogSnapshot`, `SupportGenerationSnapshot`, and `SupportPrivacySnapshot`.
- Every produced type is `internal`; DTO properties may be public for Newtonsoft serialization because the declaring types remain internal.
- Later tasks construct only through validating constructors/static factories; mutable public setters are forbidden.

- [ ] **Step 1: Add the exact approved linked-source item**

Add this single element to the existing first `ItemGroup` in `DeliveryTemperatureLimit.Tests.csproj`:

```xml
<Compile Include="..\Source\SupportReporting\Core\**\*.cs"
         Link="Production\SupportReporting\Core\%(RecursiveDir)%(Filename)%(Extension)" />
```

Do not change any other XML node.

- [ ] **Step 2: Write the failing contract tests**

Create tests that require schema `1`, exact enum values, explicit available/unavailable facts, defensive copies, deterministic collection order, and standard-report absence of a player log. The first test has this shape:

```csharp
[TestMethod]
public void Constructor_WhenStandardFactsAreSupplied_PreservesSchemaAndExplicitAvailability()
{
    var document = SupportReportDocumentFixture.Create(
        SupportReportKind.Standard,
        playerLog: null);

    Assert.AreEqual(1, document.SchemaVersion);
    Assert.AreEqual("standard", document.ReportKind);
    Assert.AreEqual("available", document.Game.Build.State);
    Assert.AreEqual("744825", document.Game.Build.Value);
    Assert.AreEqual("unavailable", document.Game.GameVersion.State);
    Assert.IsNull(document.PlayerLog);
}
```

The fixture is private test code in the same file and constructs every required section with explicit sample values; it is not production scaffolding.

- [ ] **Step 3: Run the focused test and observe red**

Run:

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --filter FullyQualifiedName~SupportReportDocumentTests
```

Expected: compilation fails because the `SupportReporting/Core` contract types do not exist.

- [ ] **Step 4: Implement the immutable contract**

Define the exact policy constants:

```csharp
internal static class SupportReportLimits
{
    internal const int SchemaVersion = 1;
    internal const int MaximumDistinctDiagnostics = 128;
    internal const int MaximumDiagnosticMessageCharacters = 2048;
    internal const int MaximumActiveMods = 512;
    internal const int MaximumRawPlayerLogBytes = 6 * 1024 * 1024;
    internal const int MaximumEscapedPlayerLogBytes = 10 * 1024 * 1024;
    internal const int MaximumReportBytes = 12 * 1024 * 1024;
    internal const int MaximumIssueUrlCharacters = 1800;
    internal const string BugIssueOrigin =
        "https://github.com/MaksymShostak/oxygen-not-included/issues/new";
    internal const string BugIssueTemplate = "temperature-limit-bug.yml";
}
```

Define report kind and explicit facts:

```csharp
internal enum SupportReportKind
{
    Standard,
    ExtendedPlayerLog
}

internal sealed class SupportReportFact
{
    private SupportReportFact(string state, string? value, string? reason)
    {
        State = state;
        Value = value;
        Reason = reason;
    }

    public string State { get; }
    public string? Value { get; }
    public string? Reason { get; }

    internal static SupportReportFact Available(string value) =>
        new SupportReportFact(
            "available",
            value ?? throw new ArgumentNullException(nameof(value)),
            null);

    internal static SupportReportFact Unavailable(string reason) =>
        new SupportReportFact(
            "unavailable",
            null,
            string.IsNullOrWhiteSpace(reason)
                ? throw new ArgumentException(
                    "An unavailable fact requires a reason.",
                    nameof(reason))
                : reason);
}
```

`SupportReportDocument` maps `Standard` to `standard` and `ExtendedPlayerLog` to `extended-player-log`, validates a non-empty report ID, requires a UTC timestamp, copies every incoming list into `ReadOnlyCollection<T>`, and rejects a player log for `Standard` or a missing player log for `ExtendedPlayerLog`.

- [ ] **Step 5: Run focused tests and observe green**

Run the Task 1 filter again. Expected: all `SupportReportDocumentTests` pass.

- [ ] **Step 6: Record the checkpoint without committing**

Inspect only the Task 1 paths with `git diff --` and retain the passing test output. Do not stage or commit; the repository's formal `/review` gate applies to the complete implementation.

---

### Task 2: Add bounded structured diagnostics

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportDiagnosticBuffer.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportDiagnosticBufferTests.cs`

**Interfaces:**
- Consumes: `SupportReportLimits.MaximumDistinctDiagnostics`, `MaximumDiagnosticMessageCharacters`, and `SupportDiagnosticSnapshot`.
- Produces:

```csharp
internal enum SupportDiagnosticSeverity { Information, Warning, Error }

internal sealed class SupportDiagnosticBuffer
{
    internal void Record(
        string code,
        SupportDiagnosticSeverity severity,
        string message,
        DateTimeOffset occurredAtUtc,
        Exception? exception = null);

    internal IReadOnlyList<SupportDiagnosticSnapshot> CaptureSnapshot();
    internal int OmittedDistinctDiagnosticCount { get; }
}
```

- [ ] **Step 1: Write failing aggregation and ceiling tests**

Cover:

- two events with the same ordinal code become one snapshot with repeat count `2`, first/last timestamps, and the latest bounded message;
- 2,049-character messages become exactly 2,048 characters with a trailing truncation marker included within that limit;
- the 129th distinct code is omitted and increments `OmittedDistinctDiagnosticCount`;
- later repeats for one of the first 128 codes still update it after the buffer is full;
- snapshots retain first-seen code order; and
- parallel records for one code produce the exact total count without corrupting the snapshot.

Representative assertion:

```csharp
Assert.AreEqual(2, snapshot[0].RepeatCount);
Assert.AreEqual(first, snapshot[0].FirstOccurredAtUtc);
Assert.AreEqual(second, snapshot[0].LastOccurredAtUtc);
```

- [ ] **Step 2: Run focused tests and observe red**

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --filter FullyQualifiedName~SupportDiagnosticBufferTests
```

Expected: compilation fails because `SupportDiagnosticBuffer` is absent.

- [ ] **Step 3: Implement one lock-owned code-indexed buffer**

Use one private synchronization object, `Dictionary<string, MutableDiagnostic>` for ordinal lookup, and `List<string>` for first-seen order. Validate that codes are nonblank, timestamps have offset zero, and messages are non-null. Store exception type full name and bounded exception message, never `Exception.ToString()` or a raw stack containing paths.

- [ ] **Step 4: Run focused tests and observe green**

Run the Task 2 filter. Expected: all diagnostic buffer tests pass.

- [ ] **Step 5: Record the checkpoint without committing**

Inspect Task 2 paths and retain the test output; do not stage or commit.

---

### Task 3: Redact known paths and build a bounded recent log excerpt

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportPathRedactor.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportLogExcerptBuilder.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportPathRedactorTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportLogExcerptBuilderTests.cs`

**Interfaces:**

```csharp
internal sealed class SupportPathRedactionRule
{
    internal SupportPathRedactionRule(string pathPrefix, string placeholder);
    internal string PathPrefix { get; }
    internal string Placeholder { get; }
}

internal sealed class SupportPathRedactor
{
    internal SupportPathRedactor(
        IEnumerable<SupportPathRedactionRule> rules,
        StringComparison comparison);

    internal RedactedSupportText Redact(string text);
}

internal sealed class SupportLogExcerptBuilder
{
    internal SupportPlayerLogSnapshot Create(
        Stream seekableLog,
        string sourceState,
        SupportPathRedactor redactor);
}
```

- [ ] **Step 1: Write failing redaction tests**

Require longest-prefix-first replacement, deterministic placeholder order, ordinal/ordinal-ignore-case behavior selected by the caller, no replacement of unrelated substrings, and rejection of blank prefixes/placeholders. Include a non-ASCII profile path fixture such as `C:\Users\Максим\Documents`.

- [ ] **Step 2: Write failing log-tail tests**

Use `MemoryStream` fixtures to prove:

- a short UTF-8 log is preserved;
- a log over 6 MiB keeps the most recent raw bytes and reports original/included counts;
- a tail that starts inside a multi-byte character drops only the invalid leading replacement character;
- known paths are replaced;
- content composed of JSON-expensive backslashes, quotes, and control characters is further shortened until its escaped UTF-8 representation is at most 10 MiB; and
- `Truncated` is true if either raw-tail or escaped-size truncation occurred.

- [ ] **Step 3: Run both filters and observe red**

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --filter "FullyQualifiedName~SupportPathRedactorTests|FullyQualifiedName~SupportLogExcerptBuilderTests"
```

Expected: missing-type compilation failures.

- [ ] **Step 4: Implement deterministic redaction**

Copy and sort rules by descending `PathPrefix.Length`, then ordinal placeholder. Reject duplicate prefixes under the selected comparison. `Redact` returns content plus only the placeholders actually used; it never mutates input or reads environment state.

- [ ] **Step 5: Implement seekable recent-tail selection**

Require a readable, seekable stream. Seek to `max(0, Length - MaximumRawPlayerLogBytes)`, read exactly the remaining bounded bytes, decode with non-throwing UTF-8, trim one leading U+FFFD only when raw-tail truncation occurred, redact, then retain the newest characters whose exact JSON-escaped UTF-8 byte count is within `MaximumEscapedPlayerLogBytes`. Count escapes for quote, backslash, control characters, and UTF-8 bytes explicitly; do not serialize repeatedly to estimate size.

- [ ] **Step 6: Run focused tests and observe green**

Run the Task 3 filter. Expected: all redaction and excerpt tests pass.

- [ ] **Step 7: Record the checkpoint without committing**

Inspect Task 3 paths and retain the test output; do not stage or commit.

---

### Task 4: Render the compact summary, fixed issue URL, and safe filename

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportSummaryRenderer.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportIssueUrlBuilder.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportFileName.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportSummaryRendererTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportIssueUrlBuilderTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportFileNameTests.cs`

**Interfaces:**

```csharp
internal static class SupportReportSummaryRenderer
{
    internal static string Render(
        SupportReportDocument document,
        string reportFileName);
}

internal static class SupportIssueUrlBuilder
{
    internal static SupportIssueUrl Create(string diagnosticSummary);
}

internal sealed class SupportIssueUrl
{
    internal string Value { get; }
    internal bool SummaryWasShortened { get; }
}

internal static class SupportReportFileName
{
    internal static string Create(DateTimeOffset generatedAtUtc, Guid reportId);
}
```

- [ ] **Step 1: Write failing summary tests**

Assert an exact compact Markdown result containing only report ID, filename, ONI build/branch, Temperature Limit version, platform, DLC IDs, FastTrack high-level state, and `Player.log` inclusion. Assert that active mod titles and diagnostic messages never appear.

- [ ] **Step 2: Write failing URL tests**

Assert exact HTTPS origin/path, query parameters `template=temperature-limit-bug.yml` and `diagnostics=<encoded summary>`, percent encoding for `&`, `#`, Unicode, and CR/LF, maximum total length 1,800, deterministic shortening, and rejection of a null summary. Parse the result with `Uri` and assert no host/path can be influenced by summary content.

- [ ] **Step 3: Write failing filename tests**

For `2026-08-31T07:08:09.123Z` and GUID `00112233-4455-6677-8899-aabbccddeeff`, require:

```text
temperature-limit-support-20260831T070809123Z-00112233.json
```

Reject non-UTC timestamps and `Guid.Empty`.

- [ ] **Step 4: Run all Task 4 filters and observe red**

Use a single `dotnet test` filter joining the three test class names. Expected: missing-type failures.

- [ ] **Step 5: Implement exact rendering and bounded URL creation**

Build the query from constants only. If the encoded URL is too long, binary-search the longest summary prefix that fits, append a fixed `… [summary shortened; see attached report]` marker, and return `SummaryWasShortened = true`. Do not truncate the fixed origin, template, parameter name, or marker.

- [ ] **Step 6: Run Task 4 tests and observe green**

Expected: all summary, URL, and filename tests pass.

- [ ] **Step 7: Record the checkpoint without committing**

Inspect Task 4 paths and retain the test output; do not stage or commit.

---

### Task 5: Publish runtime patch and FastTrack evidence from the existing owner

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlanTests.cs`

**Interfaces:**
- Consumes: `SupportRuntimeSnapshot`, `SupportFastTrackSnapshot`, `SupportFastTrackFeatureSnapshot` from Task 1.
- Produces:

```csharp
// DeliveryTemperatureRuntimePatchPlan
internal SupportRuntimeSnapshot CreateSupportReportSnapshot(
    string installationState);

// DeliveryTemperatureRuntimePatchInstaller
internal static SupportRuntimeSnapshot CaptureSupportReportSnapshot();
```

- [ ] **Step 1: Write failing patch-plan snapshot tests**

Extend the existing plan fixture for FastTrack absent, replacement inactive, ready `0.18.4.0`, status-only incompatible, and delivery-feature incompatible states. Assert ordered patch-group names exactly match `OrderedPatchGroups`; assembly identity/version/file version/SHA-256 and each feature state/failure code/message map from the original `FastTrackCompatibilityReport`; verified reflected `MemberInfo` objects are not serialized.

- [ ] **Step 2: Run the focused existing suite and observe red**

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --filter FullyQualifiedName~DeliveryTemperatureRuntimePatchPlanTests
```

Expected: compilation fails because `CreateSupportReportSnapshot` is absent.

- [ ] **Step 3: Implement the plan-owned mapping**

Iterate the three exact `FastTrackFeature` enum values, map `Feature`, `State`, optional `FailureCode`, and a diagnostic-buffer-bounded `FailureMessage`, and copy ordered patch group names with `ToString()`. Reuse the report already held by the plan. Do not call Harmony, reflection discovery, file hashing, or `FastTrackCompatibilityInspector`.

- [ ] **Step 4: Expose installer state under the existing lock**

`CaptureSupportReportSnapshot` acquires `InstallationSynchronization`. When `installedPatchPlan` is present, it returns `CreateSupportReportSnapshot(runtimeInstallerState.ToString())`. Otherwise it returns `SupportRuntimeSnapshot.Unavailable(runtimeInstallerState.ToString(), "No verified runtime patch plan was published.")`. It performs no runtime mutation.

- [ ] **Step 5: Run focused tests and observe green**

Run the Task 5 filter. Expected: all runtime patch-plan tests pass.

- [ ] **Step 6: Record the checkpoint without committing**

Inspect the two source files and one test file; do not stage or commit.

---

### Task 6: Capture ONI facts, serialize one local JSON file, and present it

**Files:**
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportJsonReportSizeLimiter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/KleiSupportReportSnapshotReader.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/SupportReportJsonFileWriter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/SupportReportPlayerPresenter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/KleiIntegration/DeliveryTemperatureSupportReporter.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportingSourceBoundaryTests.cs`

**Interfaces:**

```csharp
internal static class DeliveryTemperatureSupportReporter
{
    internal static void Initialize(KMod.Mod currentMod, Assembly assembly);
    internal static void PublishLoadedMods(IReadOnlyList<KMod.Mod> loadedMods);
    internal static void Record(
        string code,
        SupportDiagnosticSeverity severity,
        string message,
        Exception? exception = null);
    internal static void CreateStandardReport();
    internal static void CreateExtendedReport();
}
```

`KleiSupportReportSnapshotReader` accepts immutable captured current-mod/loaded-mod state and returns a `SupportReportDocument`. `SupportReportJsonFileWriter.Write` returns the final absolute path for presentation but no absolute path is placed inside the document. `SupportReportPlayerPresenter.PresentSuccess` receives the final path, compact summary, and issue URL; `PresentFailure` receives a player-safe message plus the exception for `Player.log` only.

- [ ] **Step 1: Write failing source-boundary tests**

The tests read the new production source boundary and assert:

- no `HttpClient`, `WebClient`, `WebRequest`, socket, GitHub token, upload, telemetry, save-file, environment-variable enumeration, or third-party config APIs;
- `KleiSupportReportSnapshotReader` consumes `KMod.Mod.title`, `staticID`, `packagedModInfo.version`, `IsActive()`, and loaded assembly names/versions, but never serializes `path`, `file_source`, or an absolute label path;
- `Application.consoleLogPath` is accessed only for `ExtendedPlayerLog`;
- report destination begins from `Application.persistentDataPath` and fixed child segments `DeliveryTemperatureLimit` and `support-reports`;
- serialization uses `JsonConvert.SerializeObject(..., Formatting.Indented)` and UTF-8 without BOM;
- presentation uses `GUIUtility.systemCopyBuffer`, `Application.OpenURL`, and `KMod.Manager.Dialog`; and
- the top-level action catches every exception and never rethrows into PLib.

- [ ] **Step 2: Run the source-boundary filter and observe red**

Expected: failures because the integration files do not exist.

- [ ] **Step 3: Implement sanitized Klei/mod capture**

Capture current mod identity during `Initialize`. During `PublishLoadedMods`, preserve list order and take at most 512 active entries. For every entry, store title, static ID, packaged version if present, active loaded assembly simple-name/version pairs, and a source-kind string derived from the supported label/distribution field only; never retain or hand the core a path. Record the omitted count.

Game facts come from `KleiVersion.ChangeList`, `KleiVersion.BuildBranch`, Unity application version/Unity version/platform, process architecture, current culture name, and active DLC IDs through current `DlcManager` APIs. Each optional read is isolated: an unavailable optional fact adds a generation warning instead of aborting the report.

- [ ] **Step 4: Implement standard/extended report assembly**

Use a new `Guid` and `DateTimeOffset.UtcNow`. Read `DeliveryTemperatureLimitOptions.Instance`, `DeliveryTemperatureRuntimePatchInstaller.CaptureSupportReportSnapshot()`, loaded-mod snapshot, and diagnostic snapshot. For extended mode only, open `Application.consoleLogPath` read-only with shared read/write access and build rules for the user profile, persistent-data path, and discovered installation root. If the log is missing/unreadable, produce an extended document with player-log state `unavailable` and a generation warning rather than failing the entire report.

- [ ] **Step 5: Implement adaptive JSON sizing and durable writing**

Serialize the immutable document before opening the temporary file. If an extended
report reaches `MaximumReportBytes`, serialize a copy with empty `Player.log`
content and the additional-truncation warning to measure exact non-log overhead,
then retain the newest log suffix that leaves the final UTF-8 JSON strictly below
the limit. Mark the copied log as truncated and disclose the further shortening;
if the empty-log document itself cannot fit, fail without writing a partial
report. Create the fixed support directory and write the bounded JSON to a
same-directory `<final-name>.tmp-<guid>` using `new UTF8Encoding(false)`. Flush
the text writer, call `FileStream.Flush(flushToDisk: true)`, close, defensively
recheck the byte length, then move to the unique final filename. On any failure,
delete only the exact task-owned temporary file after verifying its parent is the
fixed support directory; never overwrite or delete an existing final report.

- [ ] **Step 6: Implement player presentation**

Copy the compact summary, call `Application.OpenURL(new Uri(reportDirectory).AbsoluteUri)`, then open the fixed issue URL. Show success through `KMod.Manager.Dialog(null, "Temperature Limit support report created", messageWithPathAndReviewReminder)`. Catch and record folder/browser failures separately so the existing report remains successful. On generation failure, call `KMod.Manager.Dialog` with the direct bug-form URL and log the exception through `Debug.LogError`.

- [ ] **Step 7: Run source-boundary and all SupportReporting tests**

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --filter FullyQualifiedName~SupportReporting
```

Expected: all support-reporting core and source-boundary tests pass.

- [ ] **Step 8: Build production to validate actual Klei APIs**

Run the repository-local pipeline build command from the mod directory:

```text
oni-mod-pipeline build
```

Expected: a successful isolated build. Any incorrect assumed KMod/DLC/Unity member is corrected to the installed build's supported public member, with the same data-minimization contract and no reflection workaround unless direct API absence is proven and the spec is amended.

- [ ] **Step 9: Record the checkpoint without committing**

Inspect Task 6 paths and retain test/build evidence; do not stage or commit.

---

### Task 7: Wire the PLib actions, early lifecycle, public surface, and noteworthy diagnostics

**Files:**
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitOptions.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitMod.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfigurator.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitUserInterface/TemperatureLimitSideScreen.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/InventoryUpdateAdapters/FastTrackWorldInventoryTemperaturePatches.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/IntentionalRuntimeContractTests.cs`

**Interfaces:**
- Consumes the static reporter facade from Task 6.
- Adds exactly two public option members and no public type:

```csharp
[Option(
    "Create Support Report",
    "Creates a local diagnostic report, copies a summary, and opens the GitHub bug form. Player.log is not read.",
    "Support")]
[JsonIgnore]
public Action<object> CreateSupportReport =>
    _ => DeliveryTemperatureSupportReporter.CreateStandardReport();

[Option(
    "Create Extended Support Report",
    "Creates the same local report and includes a bounded, best-effort-redacted copy of the current Player.log. Review it before uploading.",
    "Support")]
[JsonIgnore]
public Action<object> CreateExtendedSupportReport =>
    _ => DeliveryTemperatureSupportReporter.CreateExtendedReport();
```

- [ ] **Step 1: Extend the intentional public-surface test first**

Add both property/member names to the intentional arrays. Add assertions that each property is read-only `System.Action<object>` (the exact type PLib 4.24 maps to a button), has `[JsonIgnore]`, has `[Option]`, and lacks `[JsonProperty]`. Retain the exact four existing serialized option keys and defaults.

- [ ] **Step 2: Run the intentional contract filter and observe red**

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --filter FullyQualifiedName~IntentionalRuntimeContractTests
```

Expected: failure because the approved action members are absent.

- [ ] **Step 3: Add the two action properties**

Add only the snippet in this task. Do not remove class-level `[RestartRequired]`, change persisted properties, add a support setting, or introduce another options type.

- [ ] **Step 4: Initialize before risky patch work and publish mods before compatibility work**

Immediately after `base.OnLoad(harmony)`, call `DeliveryTemperatureSupportReporter.Initialize(mod, assembly)`. Wrap each existing installer call with `try/catch`; record the stable code and exception, then `throw` to preserve fail-closed startup:

```csharp
try
{
    DeliveryTemperatureRuntimePatchInstaller
        .InstallLoadedModTopologyIndependentPatches(harmony);
}
catch (Exception exception)
{
    DeliveryTemperatureSupportReporter.Record(
        "DTL-PATCH-TOPOLOGY-INDEPENDENT-FAILED",
        SupportDiagnosticSeverity.Error,
        "Loaded-mod-independent patch installation failed.",
        exception);
    throw;
}
```

`OnAllModsLoaded` calls `PublishLoadedMods(loadedMods)` before the equivalent topology-dependent wrapper.

- [ ] **Step 5: Route existing noteworthy messages through the recorder**

Use stable codes while preserving current Unity log severity/message:

- `DTL-PREFAB-CONFIGURATION-SKIPPED`
- `DTL-PREFAB-CONFIGURATION-COMPLETE`
- `DTL-SIDE-SCREEN-REGISTRATION-FAILED`
- `DTL-STATUS-COMPATIBILITY-DEGRADED`
- `DTL-GAME-LOAD-AUTHORITY-REJECTED`
- `DTL-FASTTRACK-INVENTORY-PUBLICATION-SKIPPED`

`DeliveryTemperatureSupportReporter.Record` itself mirrors the event to `Debug.Log`, `Debug.LogWarning`, or `Debug.LogError`, so replace the original call rather than logging twice. Do not route exceptions thrown only as programmer/domain guards.

- [ ] **Step 6: Run intentional, runtime, and support tests**

Run filters for `IntentionalRuntimeContractTests`, `DeliveryTemperatureRuntimePatchPlanTests`, and `SupportReporting`. Expected: all pass.

- [ ] **Step 7: Run a production build and inspect the merged public surface**

Run `oni-mod-pipeline build`, then the registered test command in Task 9. Expected: the merged assembly has the same four public types and only the two newly approved option member names.

- [ ] **Step 8: Record the checkpoint without committing**

Inspect Task 7 paths; do not stage or commit.

---

### Task 8: Publish concise community-health and player guidance

**Files:**
- Create: `CONTRIBUTING.md`
- Create: `SUPPORT.md`
- Create: `.github/ISSUE_TEMPLATE/temperature-limit-bug.yml`
- Create: `.github/ISSUE_TEMPLATE/temperature-limit-feature.yml`
- Create: `.github/ISSUE_TEMPLATE/config.yml`
- Create: `.github/pull_request_template.md`
- Modify: `README.md`
- Modify: `mods/delivery-temperature-limit-supercooled/STEAM_DESCRIPTION.bbcode`

**Interfaces:**
- The bug form's `diagnostics` ID must exactly match `SupportIssueUrlBuilder`.
- Existing remote labels `bug` and `enhancement` were verified before planning and are the only labels named by the forms.
- Every support URL targets `MaksymShostak/oxygen-not-included`; no guessed Workshop homepage is introduced.

- [ ] **Step 1: Create the exact bug form**

```yaml
name: Temperature Limit bug
description: Report behavior that is incorrect, broken, or unexpectedly slow.
title: ""
labels:
  - bug
body:
  - type: markdown
    attributes:
      value: |
        Thanks for helping improve Delivery Temperature Limit (Supercooled).
        The fastest route is **Mods → Delivery Temperature Limit → Options → Create Support Report**. Review the generated file before attaching it. If the mod cannot load, attach the current `Player.log` if available. See [SUPPORT.md](../../SUPPORT.md) for privacy and fallback details.
  - type: textarea
    id: observed
    attributes:
      label: What happened?
      description: Describe the incorrect behavior or error in plain language.
      placeholder: The Storage Bin accepted material hotter than its configured maximum.
    validations:
      required: true
  - type: textarea
    id: reproduction
    attributes:
      label: What were you doing when it happened?
      description: Give the shortest sequence or situation that lets us see the problem.
      placeholder: Configure the bin to 20–30 °C, make 40 °C material available, then allow storage errands.
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: What did you expect?
      description: Tell us what Temperature Limit should have done instead.
    validations:
      required: true
  - type: textarea
    id: diagnostics
    attributes:
      label: Generated diagnostic summary
      description: This is filled automatically when the in-game reporter opens the form. Leave it blank if the reporter was unavailable.
    validations:
      required: false
  - type: upload
    id: files
    attributes:
      label: Support report, log, or screenshot
      description: Drag the generated JSON report here. For a startup failure, a current Player.log is also useful. Review files before uploading because GitHub issues are public.
    validations:
      required: false
      accept: ".json,.log,.txt,.zip,.png,.jpg,.jpeg"
  - type: textarea
    id: context
    attributes:
      label: Anything else?
      description: Add any context that is not already captured above.
    validations:
      required: false
```

- [ ] **Step 2: Create the exact feature form**

```yaml
name: Temperature Limit feature idea
description: Suggest an improvement grounded in a player problem or use case.
title: ""
labels:
  - enhancement
body:
  - type: markdown
    attributes:
      value: |
        Describe the player problem first. You do not need to design the implementation or write formal acceptance criteria.
  - type: textarea
    id: problem
    attributes:
      label: What player problem would this solve?
      description: Explain the limitation, repetitive task, or confusing behavior you encounter.
    validations:
      required: true
  - type: textarea
    id: desired-experience
    attributes:
      label: What would a better experience look like?
      description: Describe the outcome from the player's perspective.
    validations:
      required: true
  - type: textarea
    id: example
    attributes:
      label: Example situation
      description: Give a colony, building, material, or workflow example if one helps.
    validations:
      required: false
  - type: textarea
    id: workaround
    attributes:
      label: Current workaround
      description: Tell us how you handle this today, if at all.
    validations:
      required: false
  - type: textarea
    id: suggestion
    attributes:
      label: Optional suggested behavior or additional context
      description: Share an idea, screenshot, comparison, or related mod if useful.
    validations:
      required: false
```

- [ ] **Step 3: Create the exact issue-template configuration**

`.github/ISSUE_TEMPLATE/config.yml` contains only:

```yaml
blank_issues_enabled: false
```

- [ ] **Step 4: Create the pull-request template**

```markdown
## Related issue

Closes #

## Why

Describe the player or contributor problem this change solves.

## What changed

Summarize the focused implementation.

## Verification

- [ ] Relevant focused automated tests pass.
- [ ] `oni-mod-pipeline validate` passes.
- [ ] `oni-mod-pipeline build` passes.
- [ ] `oni-mod-pipeline test` passes.
- [ ] Relevant manual ONI scenarios were exercised, or are marked not applicable below.

Manual scenarios and evidence:

## Impact review

- Compatibility with ONI content/DLC modes and other mods:
- Save/persistence impact:
- Performance/allocation impact:
- UI/localization/documentation impact:
- Screenshots or recordings, when relevant:

## Known limitations

State any known limitation, or write `None`.
```

- [ ] **Step 5: Create concise `CONTRIBUTING.md`**

Use these exact headings and content responsibilities:

```markdown
# Contributing

Thanks for helping improve Delivery Temperature Limit (Supercooled).

## Choose the right route

- For a player-visible bug, use the Temperature Limit bug form and the automated report flow in SUPPORT.md.
- For a feature idea, use the feature form and describe the player problem and desired experience.
- For a code or documentation change, open or link an issue before substantial work so scope and compatibility expectations are visible.

## Set up and validate a checkout

Follow the existing getting-started and development guides. ONI Mod Pipeline is the repository's supported build, test, install, and release path.

Run these commands from `mods/delivery-temperature-limit-supercooled`:

```text
oni-mod-pipeline diagnose
oni-mod-pipeline validate
oni-mod-pipeline build
oni-mod-pipeline test
```

`build` prints one exact `build-result.json` path. Paste that printed path when PowerShell prompts, then install that named result:

```powershell
$buildResultPath = Read-Host 'Paste the exact build-result.json path printed by build'
oni-mod-pipeline install --mod . --build-result $buildResultPath --target dev
```

Never select a result by timestamp, directory ordering, or a "latest" convention. Build again after source changes instead of reusing or editing an older result.

## Make a focused change

Keep each change focused on one agreed player or maintainer outcome. Preserve unrelated working-tree changes, add or update a failing test before behavior code when practical, and keep production source compatible with the repository's C# 8 ceiling. Discuss dependency, build, test, formatting, CI, repository-policy, release-process, or other configuration changes in the linked issue before editing those files.

## Prepare a pull request

Link the issue, explain the player or maintainer rationale, summarize the focused change, and include fresh automated test evidence plus relevant in-game ONI evidence. Explicitly describe compatibility, performance/allocation, save/persistence, UI, localization, and documentation impact, writing `None` where a category does not apply. Release changes must follow [Preparing ONI mod releases](docs/guides/preparing-oni-mod-releases.md).
```

- [ ] **Step 6: Create player-focused `SUPPORT.md`**

Use exact sections: `Fastest reporting path`, `Standard versus extended reports`, `What is collected`, `What is not collected`, `If the reporter cannot run`, `Before restarting after a crash`, and `Public attachment privacy`. State the two action labels exactly, explain that files stay local until attached, list the standard exclusions from the spec, link the bug form directly, and link Klei's current log guidance. Do not direct users to a nonexistent Discord, email, discussion, or Workshop support thread.

- [ ] **Step 7: Update README and Workshop listing source**

Insert before `Development and release workflow` in `README.md`:

```markdown
## Support and contributing

Use the in-game mod options to create a local support report without manually finding game versions, enabled DLCs, settings, or active mods. The standard report does not read `Player.log`; the clearly labeled extended report includes a bounded, best-effort-redacted copy for harder failures. Nothing is uploaded automatically.

- [Report a bug](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml)
- [Suggest a feature](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-feature.yml)
- [Support and privacy details](SUPPORT.md)
- [Contributing](CONTRIBUTING.md)
```

Add a matching concise `[h1]Support and bug reports[/h1]` section to `STEAM_DESCRIPTION.bbcode` that tells players to open the mod's Options, choose one of the two report actions, review the local JSON, and attach it to the full GitHub bug-form URL. Do not edit the change-notes file.

- [ ] **Step 8: Validate the policy files structurally**

Use `rg` to verify unique field IDs and exact labels, and validate YAML with the repository's available parser or a read-only `ruby -e`/PowerShell YAML facility only if already installed. Do not add a validator dependency. Open each GitHub form through its repository URL only after files are present remotely; local verification relies on schema review and GitHub's documented field contract.

- [ ] **Step 9: Record the checkpoint without committing**

Inspect all Task 8 paths. Confirm no generated report, log, save, build output, or private path was added. Do not stage or commit.

---

### Task 9: Run complete automated verification and enter the formal review gate

**Files:**
- No new intended files.
- Inspect all task-owned source, tests, docs, and policy files.

**Interfaces:**
- Consumes every previous task.
- Produces fresh validation/build/test evidence and the required user-visible review handoff.

- [ ] **Step 1: Run the self-contained support-reporting tests directly**

```text
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --configuration Release --filter FullyQualifiedName~SupportReporting
```

Expected: all support-reporting core and source-boundary tests pass with zero
warnings treated as errors. Do not treat a raw, unfiltered `dotnet test` as a
valid standalone command: the pipeline-integration tests require repository and
ONI managed-assembly environment variables supplied by `oni-mod-pipeline`.
Step 4 is the authoritative complete-project test run.

- [ ] **Step 2: Run repository-local pipeline validation**

From `mods/delivery-temperature-limit-supercooled`:

```text
oni-mod-pipeline validate
```

Expected: exit `0` and no error diagnostics.

- [ ] **Step 3: Run a fresh production build**

```text
oni-mod-pipeline build
```

Expected: exit `0`; retain the exact emitted `build-result.json` path for later Dev installation.

- [ ] **Step 4: Run the registered pipeline tests**

```text
oni-mod-pipeline test
```

Expected: exit `0`; the required `delivery-temperature-limit-regressions` project passes.

- [ ] **Step 5: Inspect the complete diff and workspace boundaries**

Run `git status --short`, `git diff --check`, and targeted `git diff -- <task paths>`. Confirm:

- only approved task files changed;
- `AGENTS.md` and `screenshot-guidance.md` remain untracked and untouched;
- no report/log/build output is tracked;
- no configuration beyond the exact test compile item, approved non-copy-local `UnityEngine.IMGUIModule` reference, and approved community-policy files changed; and
- no secret, personal path, token, or generated user data appears in the diff.

- [ ] **Step 6: Announce and pause for built-in review**

Send exactly:

```text
Implementation complete; /review pending
```

Tell the user to invoke `/review`, select **Review uncommitted changes**, and scope the review to the Temperature Limit support-reporting/community implementation while ignoring the pre-existing untracked `AGENTS.md` and `mods/delivery-temperature-limit-supercooled/screenshot-guidance.md`.

Do not claim completion, mutate GitHub metadata, install into ONI user data, commit, or push while review is pending.

- [ ] **Step 7: Resolve the review result**

For every confirmed P0–P2 finding, add a focused failing test when behavioral, implement the smallest correction, rerun the focused test plus all Task 9 commands, and request another `/review` if the correction is non-trivial. A finding may remain only if the user explicitly defers it. P3 findings are handled when useful and in scope; record any intentionally unaddressed P3 without calling the implementation perfect.

---

### Task 10: Perform approved remote metadata and manual player-flow verification after review

**Files:**
- No additional repository file expected.
- External state: GitHub repository description/topics; ONI Dev mod installation only.

**Interfaces:**
- Consumes a review-cleared diff and the exact fresh build result from Task 9.
- Produces verified GitHub About metadata and manual player-flow evidence.

- [ ] **Step 1: Re-read GitHub metadata before mutation**

The GitHub MCP server was authenticated but had no repository-metadata read/write tool and repository search did not return this fork; that unavailability and the CLI fallback were already disclosed. Recheck the current state with:

```text
gh repo view MaksymShostak/oxygen-not-included --json description,homepageUrl,repositoryTopics,visibility,defaultBranchRef,isFork,hasIssuesEnabled,hasDiscussionsEnabled,hasWikiEnabled
```

Stop if description or topics no longer match the planning baseline (empty/no topics) unless the desired final values are already exact; never overwrite a concurrent user change.

- [ ] **Step 2: Apply only the approved description and topics**

```text
gh repo edit MaksymShostak/oxygen-not-included --description "Optimized Oxygen Not Included mod for setting minimum and maximum temperatures on materials delivered to storage, buildings, and construction." --add-topic c-sharp --add-topic dotnet --add-topic game-mod --add-topic harmony --add-topic oni-mod --add-topic oxygen-not-included --add-topic plib --add-topic steam-workshop --add-topic temperature-control --add-topic unity
```

Do not pass any homepage, feature, visibility, branch, template, merge, or fork-related flag.

- [ ] **Step 3: Verify the exact remote result**

Repeat the Task 10 Step 1 query. Require the exact description, exactly the ten approved topics, empty homepage, Issues enabled, Discussions disabled, Wiki unchanged, public visibility, `main`, and fork status unchanged.

- [ ] **Step 4: Install the exact fresh build to Dev**

From the mod directory, pass the exact build result emitted by Task 9:

```powershell
$supportReportBuildResultPath = Read-Host 'Paste the exact build-result.json path printed by Task 9'
oni-mod-pipeline install --mod . --build-result $supportReportBuildResultPath --target dev
```

Before running `install`, confirm that the pasted value is exactly the path printed by Task 9. Never infer a latest run.

- [ ] **Step 5: Execute the manual ONI matrix without submitting an issue**

Verify in the current public ONI build:

1. Both support actions render in the mod's PLib Options under Support.
2. Standard action creates one JSON file, excludes `playerLog`, copies a summary, reveals the folder, opens the correct form, and prefills `diagnostics`.
3. Extended action warns through its label/tooltip, includes a bounded/redacted current log or an explicit unavailable state, and remains below 12 MiB.
4. The JSON contains build/branch, mod version, active DLC IDs, settings, active mods/load order, selected patch groups, FastTrack state, diagnostics, generation warnings, and privacy disclosure.
5. Neither report contains an absolute user/profile/install path after redaction inspection.
6. Closing the issue form submits nothing; delete no generated report automatically.
7. Repeat snapshot checks with FastTrack absent and present if both environments are available; otherwise record the absent manual case and rely on automated compatibility fixtures for the other.

- [ ] **Step 6: Re-run verification if manual testing changes code**

Any code correction returns to a focused red/green cycle, all Task 9 commands, and another formal `/review` for non-trivial changes. Do not treat a manual correction as exempt from review.

- [ ] **Step 7: Request separate implementation commit authorization**

After review clearance, remote verification, applicable manual evidence, and a final diff/status summary, propose the exact implementation commit message and scope through the `committing-to-git` skill. Do not create that commit until the user approves its exact message/snapshot. Do not push without a separate explicit authorization for the exact commit/ref.

---

## Final Acceptance Mapping

- Spec goals 1–3 and acceptance 1–6: Tasks 4, 6, 7, 8, and 10.
- Allowlisted data, privacy, redaction, no network/upload, and bounded size: Tasks 1–3, 6, and 9.
- Existing runtime owner/no reinspection: Task 5.
- Fail-closed lifecycle and non-crashing report actions: Tasks 6–7.
- Intentional two-member public-surface change and no new public type: Task 7.
- Community files, exact labels, forms, contributor/support routing: Task 8.
- No unapproved packages/projects/pipeline/version/config changes: Global Constraints plus Task 9 diff audit.
- Exact GitHub description/topics and unchanged remaining metadata: Task 10.
- Fresh automated evidence, manual player flow, formal `/review`, and separate commit/push authorization: Tasks 9–10.
