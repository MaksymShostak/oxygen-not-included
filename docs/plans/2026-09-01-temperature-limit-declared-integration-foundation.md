# Temperature Limit Declared Integration Foundation Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in dependency order. Follow the repository's test-driven-development and formal review gates, and use the checkboxes (`- [ ]`) to track progress.

**Goal:** Replace FastTrack-shaped selection and reporting with a provider-neutral, compile-time-declared capability model while preserving every currently verified Klei/FastTrack behavior.

**Architecture:** Pure immutable identifiers, declarations, contributions, outcomes, and a deterministic selector live under `GameplayActivation/Core/ExternalModIntegration`. Klei and FastTrack preparation produce complete contributions containing resolved patch bindings plus exact authority requirements. `DeliveryTemperatureRuntimePatchPlan` consumes only the selected generic map. Schema version 2 projects the same generic outcomes without provider-name branches.

**Tech Stack:** C# 8-compatible production source; MSTest; BCL reflection; existing Harmony contract binding types; existing FastTrack `0.18.4.0` inert binary fixture; Newtonsoft.Json through the production build only.

**Spec:** `docs/specs/2026-08-31-temperature-limit-lifecycle-contained-activation-design.md`, especially sections 10, 19, 20, and 21.

## Global Constraints

- Execute after reviewing `docs/plans/2026-09-01-temperature-limit-safe-activation-program.md`.
- Preserve all existing compatibility states and structural checks in `FastTrackCompatibilityInspector`.
- Match FastTrack only when one active Klei mod has exact static ID `PeterHan.FastTrack` and that same mod supplies exactly one loaded assembly with simple name `FastTrack`.
- Catalog order controls deterministic inspection/report order only. It never breaks an ownership conflict.
- Do not scan `AppDomain` for adapter types or accept assemblies based only on simple name.
- Do not add Blueprints Expanded to the production catalog.
- The only configuration edit in this plan is the already approved test-project linked-source item.

---

## File and Responsibility Map

| File | Responsibility |
|---|---|
| `Source/GameplayActivation/Core/ExternalModIntegration/ValidatedIntegrationIdentifier.cs` | One shared lowercase-ASCII-kebab validator |
| `.../DeclaredModIntegrationId.cs` | Stable integration identity value |
| `.../RuntimeCapabilityId.cs` | Stable capability identity value and owned constants |
| `.../RuntimePatchGroupId.cs` | Provider-neutral stable audit identity for one concrete prepared patch group |
| `.../ExternalModIntegrationStates.cs` | The four exact state dimensions from the spec |
| `.../RuntimeCapabilityDefinition.cs` | Criticality, Klei baseline, optional atomic bundle |
| `.../RuntimeAuthorityRequirement.cs` | Exact target, permitted owner, and required replacement evidence |
| `.../ActiveHarmonyPrefixDescriptor.cs` | Provider-neutral copied prefix authority evidence |
| `.../PreparedRuntimeAuthorityContribution.cs` | One immutable provider contribution with resolved bindings |
| `.../PreparedRuntimeAuthorityInspection.cs` | One inspector's immutable generic outcome plus contributions |
| `.../ExternalModIntegrationOutcome.cs` | Bounded sanitized generic diagnostic projection |
| `.../DeclaredModIntegrationDescriptor.cs` | Exact static-ID/assembly contract and declared capabilities |
| `.../DeclaredModIntegrationCatalog.cs` | Explicit ordered declaration set with uniqueness checks |
| `.../RuntimePatchCapabilitySelector.cs` | Deterministic per-capability selection and bundle validation |
| `Source/GameplayActivation/Core/ExternalModIntegration/LoadedModInspectionContext.cs` | Short-lived copied authoritative topology facts; no retained Klei object |
| `Source/GameplayActivation/Core/ExternalModIntegration/DeclaredExternalModIntegrationPreparation.cs` | Executes only catalog-declared inspectors |
| `Source/GameplayActivation/Core/ExternalModIntegration/DeclaredIntegrationPreparationResult.cs` | Immutable ordered contributions and external integration outcomes |
| `Source/FastTrackCompatibility/FeatureContractVerification/FastTrackRuntimeAuthorityIntegrationInspector.cs` | Projects the existing FastTrack deep module into generic contributions/outcomes |
| `Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs` | Consumes the generic selection and exposes generic authority verification |
| `Source/SupportReporting/Core/SupportReportDocument.cs` | Schema-v2 generic external-integration snapshots |

## Cross-Task Interfaces

```csharp
DeclaredIntegrationPreparationResult DeclaredExternalModIntegrationPreparation.Prepare(
    DeclaredModIntegrationCatalog catalog,
    LoadedModInspectionContext context,
    IReadOnlyList<IRuntimeAuthorityIntegrationInspector> runtimeInspectors,
    IReadOnlyList<IAdditiveInteroperabilityInspector> additiveInspectors);

RuntimePatchCapabilitySelection RuntimePatchCapabilitySelector.Select(
    IReadOnlyList<RuntimeCapabilityDefinition> definitions,
    IReadOnlyList<PreparedRuntimeAuthorityContribution> contributions,
    IReadOnlyList<ExternalModIntegrationOutcome> outcomes);

DeliveryTemperatureRuntimePatchPlan DeliveryTemperatureRuntimePatchPlan.Create(
    bool checkTemperatureForStatusItems,
    RuntimePatchCapabilitySelection capabilitySelection);
```

The preparation result feeds the selector; the selection feeds the runtime plan; the same generic outcomes feed `SupportRuntimeSnapshot`. No later task unwraps a provider-specific result.

## Task 1: Link the Pure Production Directory into the Existing Test Project

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/LinkedProductionSourceBoundaryContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj`

- [ ] Add this exact entry to the existing `ApprovedCompileIncludes` array before changing the project file:

```csharp
@"..\Source\GameplayActivation\Core\**\*.cs",
```

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~LinkedProductionSourceBoundaryContractTests
```

Expected red: `TestProject_WhenCompileLinksAreInspected_UsesExactApprovedProductionBoundary` reports that the approved include is missing from the project.

- [ ] Add exactly this item to the existing production-source `ItemGroup`:

```xml
<Compile Include="..\Source\GameplayActivation\Core\**\*.cs"
         Link="Production\GameplayActivation\Core\%(RecursiveDir)%(Filename)%(Extension)" />
```

- [ ] Run the same focused command.

Expected green: all `LinkedProductionSourceBoundaryContractTests` pass.

- [ ] Inspect `git diff -- mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj` and confirm this is the only project-file delta.

## Task 2: Add Validated Provider-Neutral Identities and State Dimensions

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/ValidatedIntegrationIdentifier.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/DeclaredModIntegrationId.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/RuntimeCapabilityId.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/RuntimePatchGroupId.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/ExternalModIntegrationStates.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/IntegrationIdentifierTests.cs`

- [ ] Write parameterized tests covering empty, whitespace, uppercase, underscore, period, leading/trailing hyphen, repeated hyphen, more than 64 characters, valid single segment, and valid multi-segment values. Assert ordinal equality and hash behavior for the integration, capability, and patch-group identity values.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~IntegrationIdentifierTests
```

Expected red: the identity types do not exist.

- [ ] Implement one validator used by both values. Its acceptance rule must be explicit rather than culture-sensitive:

```csharp
internal static string RequireKebabCase(string value, string parameterName)
{
    if (value == null)
    {
        throw new ArgumentNullException(parameterName);
    }

    if (value.Length == 0 || value.Length > 64 ||
        value[0] == '-' || value[value.Length - 1] == '-')
    {
        throw new ArgumentException(
            "An integration identifier must contain 1-64 lowercase ASCII kebab-case characters.",
            parameterName);
    }

    bool previousWasHyphen = false;
    for (int index = 0; index < value.Length; index++)
    {
        char character = value[index];
        bool isHyphen = character == '-';
        bool isLowerAscii = character >= 'a' && character <= 'z';
        bool isDigit = character >= '0' && character <= '9';
        if ((!isHyphen && !isLowerAscii && !isDigit) ||
            (isHyphen && previousWasHyphen))
        {
            throw new ArgumentException(
                "An integration identifier must contain 1-64 lowercase ASCII kebab-case characters.",
                parameterName);
        }

        previousWasHyphen = isHyphen;
    }

    return value;
}
```

- [ ] Define exactly these capability constants:

```csharp
internal static readonly RuntimeCapabilityId WorldInventoryTemperaturePublication =
    new RuntimeCapabilityId("world-inventory-temperature-publication");
internal static readonly RuntimeCapabilityId PickupTemperatureGrouping =
    new RuntimeCapabilityId("pickup-temperature-grouping");
internal static readonly RuntimeCapabilityId DirectDeliveryEligibility =
    new RuntimeCapabilityId("direct-delivery-eligibility");
internal static readonly RuntimeCapabilityId TemperatureStatusAvailability =
    new RuntimeCapabilityId("temperature-status-availability");
```

- [ ] Define the exact enums approved by the spec: `DeclaredModMatchState`, `RuntimeAuthorityObservation`, `IntegrationContractState`, and `IntegrationCapabilityDisposition`, without Boolean aliases. Add `ExternalModIntegrationCategory` with only `ExclusiveRuntimeAuthority` and `AdditiveInteroperability`; built-in Klei baselines are capability candidates, not external-mod categories.

- [ ] Run the focused tests again.

Expected green: all identifier and state tests pass.

## Task 3: Model Immutable Declarations, Contributions, Authority Evidence, and Outcomes

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/RuntimeCapabilityDefinition.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/RuntimeAuthorityRequirement.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/ActiveHarmonyPrefixDescriptor.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/PreparedRuntimeAuthorityContribution.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/PreparedRuntimeAuthorityInspection.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/ExternalModIntegrationOutcome.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/DeclaredModIntegrationDescriptor.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/DeclaredIntegrationModelTests.cs`

- [ ] Test defensive copying, non-null elements, bounded display names and diagnostics, required upstream evidence reference, exact accepted static IDs/assembly names, duplicate patch identities, invalid owner evidence, and category ordering. Prove that changing an input list after construction does not mutate the object.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~DeclaredIntegrationModelTests
```

Expected red: the immutable model is absent.

- [ ] Implement authority requirements around exact reflection identity:

```csharp
internal enum RuntimeAuthorityRequirementKind
{
    KleiOriginal,
    ExactOwnedReplacement
}

internal sealed class RuntimeAuthorityRequirement
{
    internal RuntimeAuthorityRequirement(
        MethodBase targetMethod,
        RuntimeAuthorityRequirementKind kind,
        string? requiredHarmonyOwner,
        MethodInfo? requiredPrefixMethod,
        IEnumerable<string> permittedSkippingPrefixOwners)
    {
        TargetMethod = targetMethod ?? throw new ArgumentNullException(nameof(targetMethod));
        Kind = kind;
        RequiredHarmonyOwner = requiredHarmonyOwner;
        RequiredPrefixMethod = requiredPrefixMethod;
        PermittedSkippingPrefixOwners = CopyDistinctOwners(permittedSkippingPrefixOwners);
        ValidateReplacementEvidence();
    }

    internal MethodBase TargetMethod { get; }
    internal RuntimeAuthorityRequirementKind Kind { get; }
    internal string? RequiredHarmonyOwner { get; }
    internal MethodInfo? RequiredPrefixMethod { get; }
    internal IReadOnlyList<string> PermittedSkippingPrefixOwners { get; }
}
```

- [ ] Make `PreparedRuntimeAuthorityContribution` carry one integration ID, one capability ID, one or more stable `RuntimePatchGroupId` audit values, one authority observation, immutable verified `HarmonyPatchContractBinding` values, exact `RuntimeAuthorityRequirement` values, and a stable bounded diagnostic code/message. It must reject `DoesNotOwn` plus non-empty bindings and reject `OwnsCompatible` without a complete contribution.

- [ ] Make `ExternalModIntegrationOutcome` contain only allowlisted scalar facts and immutable capability outcome values. Do not retain `Assembly`, `KMod.Mod`, `Harmony`, exception, stack trace, or filesystem path objects.

- [ ] Run the focused tests again.

Expected green: all model invariants pass.

## Task 4: Build an Explicit Catalog and a Deterministic Capability Selector

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/DeclaredModIntegrationCatalog.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/RuntimePatchCapabilitySelection.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/RuntimePatchCapabilitySelector.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/DeclaredModIntegrationCatalogTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/RuntimePatchCapabilitySelectorTests.cs`

- [ ] Add catalog tests for duplicate integration ID, duplicate exact static ID, duplicate capability declaration inside one descriptor, stable insertion order, and a valid FastTrack-only catalog.

- [ ] Add selector tests for Klei fallback, one compatible owner, incompatible required owner, unavailable required ownership, optional omission, two compatible owners, two claimed owners where one is incompatible, undeclared outcome rejection, an atomic bundle with one mixed member, and a valid all-Klei/all-external bundle.

- [ ] Add a synthetic second authority named `synthetic-runtime-authority` that owns `pickup-temperature-grouping`. Assert selection succeeds without a selector source branch or FastTrack type.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~DeclaredModIntegrationCatalogTests|FullyQualifiedName~RuntimePatchCapabilitySelectorTests"
```

Expected red: selection and catalog APIs do not exist.

- [ ] Implement the selection loop in capability-definition order:

```csharp
foreach (RuntimeCapabilityDefinition definition in definitions)
{
    IReadOnlyList<PreparedRuntimeAuthorityContribution> claims =
        FindClaims(definition.Id, contributions);

    if (claims.Count == 0)
    {
        SelectBaselineOrOmit(definition, selected, outcomes);
        continue;
    }

    if (claims.Count != 1)
    {
        throw RuntimeCapabilitySelectionException.ConflictingOwners(
            definition.Id,
            claims);
    }

    SelectSingleClaim(definition, claims[0], selected, outcomes);
}

ValidateAtomicBundles(definitions, selected);
```

- [ ] Ensure `FindClaims` treats `OwnsCompatible`, `OwnsIncompatible`, and `OwnershipUnavailable` as ownership claims. Never choose Klei after an external integration claims a required capability but cannot prove a compatible contribution.

- [ ] Ensure the selector never consults catalog position or loaded-mod order when more than one integration claims one exclusive capability.

- [ ] Run the focused tests again.

Expected green: every deterministic selection and bundle case passes.

## Task 5: Add Narrow Inspection Contracts and Authoritative Loaded-Mod Matching

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/LoadedModInspectionContext.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/IRuntimeAuthorityIntegrationInspector.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/IAdditiveInteroperabilityInspector.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/DeclaredExternalModIntegrationPreparation.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/DeclaredIntegrationPreparationResult.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/RuntimePatchInstallation/DeclaredExternalModIntegrationPreparationTests.cs`

- [ ] Test exact static-ID match, assembly tied to the same active mod entry, inactive mod ignored, duplicate matching active entries ambiguous, duplicate same-name assemblies ambiguous, unknown mod ignored, runtime inspector exception converted to its declaration's inspection-unavailable outcome, and additive inspector exception isolated from runtime selection.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~DeclaredExternalModIntegrationPreparationTests
```

Expected red: the declared preparation layer is absent.

- [ ] Define the narrow contracts exactly around declared descriptors:

```csharp
internal interface IRuntimeAuthorityIntegrationInspector
{
    DeclaredModIntegrationId IntegrationId { get; }

    PreparedRuntimeAuthorityInspection Inspect(
        DeclaredModIntegrationDescriptor descriptor,
        LoadedModInspectionContext context);
}

internal interface IAdditiveInteroperabilityInspector
{
    DeclaredModIntegrationId IntegrationId { get; }

    ExternalModIntegrationOutcome Inspect(
        DeclaredModIntegrationDescriptor descriptor,
        LoadedModInspectionContext context);
}
```

- [ ] Do not add `IModCompatibilityProvider`, runtime type scanning, `Assembly.Load`, or provider lookup by class name.

- [ ] Build `LoadedModInspectionContext` from immutable `LoadedModCandidate` values containing active state, exact static ID, and BCL `Assembly` references plus copied active-Harmony descriptors. A thin runtime adapter creates those values only from the `IReadOnlyList<KMod.Mod>` received by `OnAllModsLoaded`. Keep the context short-lived and absent from support-document types.

- [ ] Run the focused tests again.

Expected green: only declared, exact identities are inspected and additive faults remain isolated.

## Task 6: Project Existing FastTrack Verification Through the Generic Boundary

**Files:**

- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackRuntimeAuthorityIntegrationInspector.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/IFastTrackRuntimePatchContributionBuilder.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/FastTrackRuntimePatchContributionBuilder.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/ActiveHarmonyPatchDescriptor.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackCompatibilityInspector.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifier.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCompatibilityInspectorTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCoherentActivationContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackReflectionEmitFixture.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlanTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/HarmonyTranspilerInfrastructure/HarmonyPatchContractVerifierTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/FastTrackCompatibility/FeatureContractVerification/FastTrackLoadedGameInspectionInput.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/Fixtures/ThirdParty/FastTrack/0.18.4.0/README.md`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackRuntimeAuthorityIntegrationInspectorTests.cs`

- [ ] Add projection tests for all existing FastTrack feature states. Assert this exact mapping:

| Existing feature state | Authority | Contract | Required disposition | Optional disposition |
|---|---|---|---|---|
| `ModNotLoaded` | `DoesNotOwn` | `NotEvaluated` | `NotApplicable` | `NotApplicable` |
| `ReplacementInactive` | `DoesNotOwn` | `NotEvaluated` | `NotApplicable` | `NotApplicable` |
| `Ready` | `OwnsCompatible` | `Compatible` | `Selected` | `Selected` |
| `Incompatible` | `OwnsIncompatible` | `Incompatible` | `ActivationBlocking` | `Unavailable` |

- [ ] Assert an exact active mod with static ID `PeterHan.FastTrack` but no same-entry `FastTrack` assembly is `Matched` with inspection unavailable; a `FastTrack` assembly supplied by a different mod does not satisfy identity.

- [ ] Re-obtain the official `FastTrack.zip` named by the existing fixture README, require its already recorded SHA-256 `8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D`, inspect its packaged mod metadata read-only, and record the exact observed static ID plus archive member path in the README. The expected value is `PeterHan.FastTrack`; if the digest or packaged value differs, stop instead of changing the accepted identity by inference.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter FullyQualifiedName~FastTrackRuntimeAuthorityIntegrationInspectorTests
```

Expected red: no generic FastTrack adapter exists.

- [ ] Implement the adapter by invoking the existing `FastTrackCompatibilityInspector` once, then projecting each feature. Inject the narrow `IFastTrackRuntimePatchContributionBuilder` so linked tests can supply resolved BCL-only bindings without compiling game adapters; the production builder remains in `RuntimePatchInstallation`. Reuse all current verified members, file identity state, failure codes, and bounded structural messages.

- [ ] Replace the provider-folder `ActiveHarmonyPatchDescriptor` with the core `ActiveHarmonyPrefixDescriptor` everywhere, then delete the old file. The new name reflects that selected-authority proof intentionally inspects skipping prefixes; it remains a BCL-only immutable copy rather than concrete Harmony metadata.

- [ ] Move FastTrack patch binding preparation behind this adapter or an adapter-owned builder so each `Ready` contribution is already complete. Do not let the generic selector call `GetFeature(FastTrackFeature...)`.

- [ ] Construct the one production descriptor with:

```csharp
new DeclaredModIntegrationDescriptor(
    new DeclaredModIntegrationId("fast-track"),
    "Fast Track",
    new[] { ExternalModIntegrationCategory.ExclusiveRuntimeAuthority },
    new[] { "PeterHan.FastTrack" },
    new[] { "FastTrack" },
    "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
    new[]
    {
        RuntimeCapabilityId.WorldInventoryTemperaturePublication,
        RuntimeCapabilityId.PickupTemperatureGrouping,
        RuntimeCapabilityId.DirectDeliveryEligibility
    });
```

- [ ] Keep `temperature-status-availability` owned by Temperature Limit as an optional dependent capability whose availability follows the selected world-inventory contribution; do not claim FastTrack owns the status UI itself.

- [ ] Run the focused adapter tests and the existing `FastTrackCompatibilityInspectorTests`.

Expected green: legacy structural coverage remains green and generic outcomes preserve all distinctions.

## Task 7: Make the Runtime Patch Plan Consume Only the Generic Selection

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlan.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchInstaller.cs`
- Delete: `mods/delivery-temperature-limit-supercooled/Source/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchGroup.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/RuntimePatchInstallation/DeliveryTemperatureRuntimePatchPlanTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/FastTrackCompatibility/FastTrackCoherentActivationContractTests.cs`

- [ ] Rewrite plan tests first so `Create` receives a `RuntimePatchCapabilitySelection`. Preserve Klei-only, each independent FastTrack-ready feature, status-only degradation, required incompatibility, selected-authority change, an unknown noninterfering mod, and an undeclared skipping-prefix owner that must fail generic authority proof without creating an integration outcome.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~DeliveryTemperatureRuntimePatchPlanTests|FullyQualifiedName~FastTrackCoherentActivationContractTests"
```

Expected red: the plan still requires `FastTrackCompatibilityReport`.

- [ ] Change the plan boundary to this shape:

```csharp
internal static DeliveryTemperatureRuntimePatchPlan Create(
    bool checkTemperatureForStatusItems,
    RuntimePatchCapabilitySelection capabilitySelection)
```

- [ ] Store only immutable selected generic contributions, ordered verified bindings, stable generic patch-group audit IDs, exact generic authority requirements, generic outcomes, and the optional status diagnostic. Remove the `fastTrackCompatibility` field and all `FastTrackFeature`/`FastTrackCompatibilityReport` access. Delete the now-unused provider-named patch-group enum; leave its existing conditional test-project link unchanged so this plan introduces no additional configuration delta.

- [ ] Replace `VerifyFastTrackAuthority` and `VerifyKleiAuthorityForMatchingTargets` branches with one generic loop over exact `RuntimeAuthorityRequirement` values. For `ExactOwnedReplacement`, require the exact target, method, owner, and permitted-owner set. For `KleiOriginal`, reject every unpermitted Boolean skipping prefix.

- [ ] Run the focused tests again.

Expected green: all previous topology semantics pass through generic selection.

## Task 8: Replace the Singular FastTrack Report Shape with Schema Version 2

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportLimits.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportDocument.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Source/SupportReporting/Core/SupportReportSummaryRenderer.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportDocumentJsonContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportReportSummaryRendererTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/SupportReporting/SupportJsonReportSizeLimiterTests.cs`

- [ ] Change JSON contract tests first. Require `schemaVersion: 2`, `runtime.externalModIntegrations`, zero/one/multiple integration arrays, deterministic order, bounded capability arrays, and absence of `runtime.fastTrack` anywhere in JSON.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~SupportReportDocumentJsonContractTests|FullyQualifiedName~SupportReportSummaryRendererTests|FullyQualifiedName~SupportJsonReportSizeLimiterTests"
```

Expected red: schema version and runtime shape are still FastTrack-specific.

- [ ] Replace `SupportFastTrackSnapshot` and `SupportFastTrackFeatureSnapshot` with:

```csharp
internal sealed class SupportExternalModIntegrationSnapshot
{
    internal string IntegrationId { get; }
    internal string DisplayName { get; }
    internal IReadOnlyList<string> Categories { get; }
    internal string MatchState { get; }
    internal SupportReportFact AssemblyIdentity { get; }
    internal SupportReportFact AssemblyVersion { get; }
    internal SupportReportFact FileVersion { get; }
    internal SupportReportFact AssemblySha256 { get; }
    internal IReadOnlyList<SupportExternalModCapabilitySnapshot> Capabilities { get; }
    internal IReadOnlyList<SupportDiagnosticSnapshot> Diagnostics { get; }
}

internal sealed class SupportExternalModCapabilitySnapshot
{
    internal string CapabilityId { get; }
    internal string AuthorityObservation { get; }
    internal string ContractState { get; }
    internal string Disposition { get; }
    internal string? DiagnosticCode { get; }
    internal string? DiagnosticMessage { get; }
}
```

- [ ] Enforce existing collection/message/JSON limits at construction and final serialization. Render summaries by display name and generic capability state; do not add `if (IntegrationId == "fast-track")`.

- [ ] Run the focused tests again.

Expected green: schema-v2 projection is deterministic, bounded, and provider-neutral.

## Task 9: Add Architecture and Extension Proofs

**Files:**

- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/LinkedProductionSourceBoundaryContractTests.cs`
- Modify: `mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureAssemblyContracts/ImplementationTerminologyContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Source/GameplayActivation/Core/ExternalModIntegration/DeclaredStaticMethodProtocol.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/ExternalIntegrationExtensionContractTests.cs`
- Create: `mods/delivery-temperature-limit-supercooled/Tests/GameplayActivation/ExternalModIntegration/DeclaredStaticMethodProtocolTests.cs`

- [ ] Add a compiled/source boundary asserting `GameplayActivation/Core`, `RuntimePatchCapabilitySelector`, and `DeliveryTemperatureRuntimePatchPlan` contain none of these tokens: `FastTrackCompatibilityReport`, `FastTrackFeature`, `PeterHan.FastTrack`, `BlueprintsV2`, `KMod.Mod`, `HarmonyLib`, `UnityEngine`.

- [ ] Add the synthetic runtime-authority and synthetic additive inspectors. Prove the former supplies a capability without selector/schema edits and the latter reports `Ready`, `Unavailable`, and contained throw outcomes while contributing zero Harmony bindings.

- [ ] Add a BCL-only declared static-method protocol verifier that receives an explicit endpoint type and exact method descriptors; it never scans arbitrary assemblies. With reflection-emitted top-level public static fixtures, verify exact getter/setter/ID names, return types, parameter types, static/public/top-level requirements, missing member, overload ambiguity, renamed parameter type, wrong by-reference shape, and changed return type.

- [ ] Assert the production catalog contains exactly `fast-track` at this milestone and does not contain `blueprints-expanded`.

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore --filter "FullyQualifiedName~ExternalIntegrationExtensionContractTests|FullyQualifiedName~DeclaredStaticMethodProtocolTests|FullyQualifiedName~LinkedProductionSourceBoundaryContractTests|FullyQualifiedName~ImplementationTerminologyContractTests"
```

Expected green: core boundaries are provider-neutral and the additive seam is proven without a production claim.

## Task 10: Milestone Verification and Gated Commit

- [ ] Run:

```powershell
dotnet test mods/delivery-temperature-limit-supercooled/Tests/DeliveryTemperatureLimit.Tests.csproj --no-restore
git diff --check
```

Expected: all tests pass; zero skipped/inconclusive; no whitespace errors.

- [ ] Run the forbidden-configuration check from the program plan.

Expected: only the approved test-project linked-source item differs.

- [ ] State `Implementation complete; /review pending` for this milestone and ask the user to invoke built-in `/review` over the declared-integration, runtime-plan, FastTrack-adapter, schema-v2, approved test-project, and associated test changes. Resolve or explicitly defer every confirmed P0-P2 finding and rerun the affected focused/full gates.

- [ ] Show `git status --short` and `git diff --stat`. Stage only this plan's intended files after separating pre-existing user-owned changes.

- [ ] If and only if the user explicitly authorizes this exact staged snapshot, load `committing-to-git` and create:

```text
refactor(temperature-limit): generalize external mod integration selection

Introduce compile-time declared integration identities and deterministic
capability selection, project FastTrack through a provider-neutral adapter,
and publish schema-v2 generic integration diagnostics without claiming
Blueprints Expanded support.
```

- [ ] Do not push.
