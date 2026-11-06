using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackRuntimeAuthorityIntegrationInspectorTests
{
    private static readonly Version SupportedFileVersion =
        new(0, 18, 4, 0);

    private const string SupportedDigest =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";

    private const string UnsupportedDigest =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Test]
    public async Task DeclaredIntegrationDescriptor_UsesExactSupportedFastTrackIdentityAndCapabilities()
    {
        DeclaredModIntegrationDescriptor descriptor =
            FastTrackRuntimeAuthorityIntegrationInspector
                .DeclaredIntegrationDescriptor;

        await Assert.That(descriptor.IntegrationId.Value).IsEqualTo("fast-track");
        await Assert.That(descriptor.DisplayName).IsEqualTo("Fast Track");
        await Assert.That(descriptor.AcceptedStaticIds.ToArray()).IsEquivalentTo(new[] { "PeterHan.FastTrack" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.AcceptedAssemblySimpleNames.ToArray()).IsEquivalentTo(new[] { "FastTrack" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.UpstreamEvidenceReference).IsEqualTo("https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta");
        await Assert.That(descriptor.DeclaredCapabilityIds.ToArray()).IsEquivalentTo(new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeCapabilityId.DirectDeliveryEligibility
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(descriptor.DeclaredCapabilities.All(capability =>
            capability.Category ==
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority)).IsTrue();
    }

    [Test]
    public async Task Prepare_WhenFastTrackIsAbsent_ProjectsEveryCapabilityAsNotApplicable()
    {
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());

        DeclaredIntegrationPreparationResult result = Prepare(
            new LoadedModInspectionContext(
                Array.Empty<LoadedModCandidate>(),
                Array.Empty<ActiveHarmonyPrefixDescriptor>()),
            identityReader);

        ExternalModIntegrationOutcome outcome = await AssertSingleOutcome(result);
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.NotMatched);
        await Assert.That(outcome.AssemblyIdentity).IsNull();
        await Assert.That(outcome.AssemblyVersion).IsNull();
        await Assert.That(outcome.FileVersion).IsNull();
        await Assert.That(outcome.AssemblySha256).IsNull();
        await Assert.That(result.RuntimeAuthorityContributions.Count).IsEqualTo(0);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(0);
        await AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        await AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        await AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
    }

    [Test]
    public async Task Prepare_WhenFastTrackReplacementsAreInactive_ProjectsEveryCapabilityAsNotApplicable()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(SupportedDigest.ToLowerInvariant()));

        DeclaredIntegrationPreparationResult result = Prepare(
            CreateMatchedContext(
                fixture,
                Array.Empty<ActiveHarmonyPrefixDescriptor>()),
            identityReader);

        ExternalModIntegrationOutcome outcome = await AssertSingleOutcome(result);
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.Matched);
        await Assert.That(outcome.AssemblyIdentity).IsEqualTo(fixture.Assembly.FullName);
        await Assert.That(outcome.AssemblyVersion).IsEqualTo("0.18.0.0");
        await Assert.That(outcome.FileVersion).IsEqualTo("0.18.4.0");
        await Assert.That(outcome.AssemblySha256).IsEqualTo(SupportedDigest);
        await Assert.That(result.RuntimeAuthorityContributions.Count).IsEqualTo(0);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(1);
        await AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        await AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        await AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
    }

    [Test]
    public async Task Prepare_WhenEveryFastTrackReplacementIsReady_ProjectsReadyCompleteContributions()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());

        DeclaredIntegrationPreparationResult result = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            identityReader);

        ExternalModIntegrationOutcome outcome = await AssertSingleOutcome(result);
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.Matched);
        await Assert.That(outcome.AssemblySha256).IsEqualTo(SupportedDigest);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(1);
        await Assert.That(result.RuntimeAuthorityContributions.Count).IsEqualTo(3);
        await AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            expectedDiagnosticCode: null);
        await AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            expectedDiagnosticCode: null);
        await AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            expectedDiagnosticCode: null);

        await AssertCompleteCompatibleContribution(
            result.RuntimeAuthorityContributions[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication);
        await AssertCompleteCompatibleContribution(
            result.RuntimeAuthorityContributions[1],
            RuntimeCapabilityId.PickupTemperatureGrouping);
        await AssertCompleteCompatibleContribution(
            result.RuntimeAuthorityContributions[2],
            RuntimeCapabilityId.DirectDeliveryEligibility);
    }

    [Test]
    public async Task Select_WhenFastTrackDoesNotOwnCapabilities_PreservesNotApplicableOutcomesAndSelectsKleiBaselines()
    {
        DeclaredIntegrationPreparationResult preparation = Prepare(
            new LoadedModInspectionContext(
                Array.Empty<LoadedModCandidate>(),
                Array.Empty<ActiveHarmonyPrefixDescriptor>()),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimePatchCapabilitySelection selection = Select(preparation);

        await AssertEveryCapabilityUsesImplementation(
            selection,
            RuntimeAuthorityImplementationKind.KleiBaseline);
        ExternalModIntegrationOutcome outcome =
            await AssertSingleFinalOutcome(selection);
        foreach (ExternalModIntegrationCapabilityOutcome capability in
                 outcome.Capabilities)
        {
            await Assert.That(capability.Disposition).IsEqualTo(IntegrationCapabilityDisposition.NotApplicable);
        }
    }

    [Test]
    public async Task Select_WhenEveryFastTrackReplacementIsReady_SelectsEveryExternalContribution()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        DeclaredIntegrationPreparationResult preparation = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimePatchCapabilitySelection selection = Select(preparation);

        await AssertEveryCapabilityUsesImplementation(
            selection,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
        ExternalModIntegrationOutcome outcome =
            await AssertSingleFinalOutcome(selection);
        foreach (ExternalModIntegrationCapabilityOutcome capability in
                 outcome.Capabilities)
        {
            await Assert.That(capability.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
        }
    }

    [Test]
    public async Task Select_WhenOptionalWorldInventoryReplacementIsIncompatible_OmitsCapabilityWithoutKleiFallback()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithRunUpdateSignatureChanged("FastTrack");
        DeclaredIntegrationPreparationResult preparation = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimePatchCapabilitySelection selection = Select(preparation);

        RuntimeCapabilitySelectionEntry worldInventory =
            selection.GetCapabilitySelection(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication);
        await Assert.That(worldInventory.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
        await Assert.That(worldInventory.HasSelectedContribution).IsFalse();
        ExternalModIntegrationOutcome outcome =
            await AssertSingleFinalOutcome(selection);
        await Assert.That(outcome.Capabilities[0].Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
        await Assert.That(outcome.Capabilities[1].Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
        await Assert.That(outcome.Capabilities[2].Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
    }

    [Test]
    public async Task Select_WhenRequiredDirectDeliveryReplacementIsIncompatible_BlocksActivationWithoutKleiFallback()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorContractChanged("FastTrack");
        DeclaredIntegrationPreparationResult preparation = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimeCapabilitySelectionException exception = (await Assert.That(() => Select(preparation)).ThrowsExactly<
            RuntimeCapabilitySelectionException>())!;

        await Assert.That(exception.CapabilityId).IsEqualTo(RuntimeCapabilityId.DirectDeliveryEligibility);
        await Assert.That(exception.DiagnosticCode).IsEqualTo("required-runtime-capability-unavailable");
        await Assert.That(exception.ExternalModIntegrationOutcomes.Count).IsEqualTo(1);
        ExternalModIntegrationOutcome outcome =
            exception.ExternalModIntegrationOutcomes[0];
        await Assert.That(outcome.Capabilities[0].Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
        await Assert.That(outcome.Capabilities[1].Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
        await Assert.That(outcome.Capabilities[2].Disposition).IsEqualTo(IntegrationCapabilityDisposition.ActivationBlocking);
    }

    [Test]
    public async Task Prepare_WhenEveryActiveFastTrackReplacementUsesAnUnsupportedBuild_ProjectsRequiredAndOptionalFailurePolicies()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(UnsupportedDigest));

        DeclaredIntegrationPreparationResult result = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            identityReader);

        ExternalModIntegrationOutcome outcome = await AssertSingleOutcome(result);
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.Matched);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(1);
        await Assert.That(result.RuntimeAuthorityContributions.Count).IsEqualTo(3);
        await AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.OwnsIncompatible,
            IntegrationContractState.Incompatible,
            IntegrationCapabilityDisposition.Unavailable,
            "fast-track-world-inventory-build-unsupported");
        await AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.OwnsIncompatible,
            IntegrationContractState.Incompatible,
            IntegrationCapabilityDisposition.Unavailable,
            "fast-track-pickup-grouping-build-unsupported");
        await AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.OwnsIncompatible,
            IntegrationContractState.Incompatible,
            IntegrationCapabilityDisposition.Unavailable,
            "fast-track-direct-delivery-build-unsupported");
        await Assert.That(outcome.Diagnostics.Count).IsEqualTo(3);
        await Assert.That(outcome.Capabilities[0].DiagnosticMessage).IsEqualTo("The active WorldInventory FastTrack replacement reports an " +
            "unsupported assembly build. Observed file version: 0.18.4.0; " +
            "observed DLL SHA-256: " + UnsupportedDigest + ". Compatibility " +
            "requires one exact admitted version-and-digest pair.");

        foreach (PreparedRuntimeAuthorityContribution contribution in
                 result.RuntimeAuthorityContributions)
        {
            await Assert.That(contribution.AuthorityObservation).IsEqualTo(RuntimeAuthorityObservation.OwnsIncompatible);
            await Assert.That(contribution.PatchGroupIds.Count).IsEqualTo(0);
            await Assert.That(contribution.PatchBindings.Count).IsEqualTo(0);
            await Assert.That(contribution.AuthorityRequirements.Count).IsEqualTo(0);
            Assert.NotNull(contribution.DiagnosticCode);
            Assert.NotNull(contribution.DiagnosticMessage);
        }
    }

    [Test]
    public async Task Prepare_WhenExactFastTrackEntryHasNoAssembly_DoesNotBorrowAnotherModsAssembly()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());
        var context = new LoadedModInspectionContext(
            new[]
            {
                new LoadedModCandidate(
                    isActive: true,
                    "PeterHan.FastTrack",
                    Array.Empty<Assembly>()),
                new LoadedModCandidate(
                    isActive: true,
                    "A.Different.Mod",
                    new[] { fixture.Assembly })
            },
            CopyActivePrefixes(fixture));

        DeclaredIntegrationPreparationResult result = Prepare(
            context,
            identityReader);

        ExternalModIntegrationOutcome outcome = await AssertSingleOutcome(result);
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.Matched);
        await Assert.That(identityReader.ReadCallCount).IsEqualTo(0);
        await Assert.That(result.RuntimeAuthorityContributions.Count).IsEqualTo(3);
        await Assert.That(outcome.Diagnostics.Count).IsEqualTo(1);
        await Assert.That(outcome.Diagnostics[0].Code).IsEqualTo("declared-integration-assembly-missing");
        foreach (ExternalModIntegrationCapabilityOutcome capability in
                 outcome.Capabilities)
        {
            await Assert.That(capability.AuthorityObservation).IsEqualTo(RuntimeAuthorityObservation.OwnershipUnavailable);
            await Assert.That(capability.ContractState).IsEqualTo(IntegrationContractState.VerificationUnavailable);
            await Assert.That(capability.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
            await Assert.That(capability.DiagnosticCode).IsEqualTo("declared-integration-assembly-missing");
        }
    }

    private static DeclaredIntegrationPreparationResult Prepare(
        LoadedModInspectionContext context,
        RecordingAssemblyFileIdentityReader identityReader)
    {
        DeclaredModIntegrationDescriptor descriptor =
            FastTrackRuntimeAuthorityIntegrationInspector
                .DeclaredIntegrationDescriptor;
        var integrationInspector =
            new FastTrackRuntimeAuthorityIntegrationInspector(
                new FastTrackCompatibilityInspector(
                    identityReader,
                    CreateSupportedTestCatalog()),
                new CompleteTestRuntimeAuthorityContributionBuilder());
        return DeclaredExternalModIntegrationPreparation.Prepare(
            new DeclaredModIntegrationCatalog(new[] { descriptor }),
            context,
            new IRuntimeAuthorityIntegrationInspector[]
            {
                integrationInspector
            },
            Array.Empty<IAdditiveInteroperabilityInspector>());
    }

    private static RuntimePatchCapabilitySelection Select(
        DeclaredIntegrationPreparationResult preparation) =>
        RuntimePatchCapabilitySelector.Select(
            CreateRuntimeCapabilityDefinitions(),
            preparation.RuntimeAuthorityContributions,
            preparation.ExternalModIntegrationOutcomes);

    private static IReadOnlyList<RuntimeCapabilityDefinition>
        CreateRuntimeCapabilityDefinitions() =>
        new[]
        {
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityCriticality.Optional,
                () => CreateKleiBaselineContribution(
                        RuntimeCapabilityId
                            .WorldInventoryTemperaturePublication),
                atomicBundleId: null),
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeCapabilityCriticality.Required,
                () => CreateKleiBaselineContribution(
                        RuntimeCapabilityId.PickupTemperatureGrouping),
                atomicBundleId: null),
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityCriticality.Required,
                () => CreateKleiBaselineContribution(
                        RuntimeCapabilityId.DirectDeliveryEligibility),
                atomicBundleId: null)
        };

    private static PreparedRuntimeAuthorityContribution
        CreateKleiBaselineContribution(RuntimeCapabilityId capabilityId)
    {
        MethodInfo target = typeof(
            FastTrackRuntimeAuthorityIntegrationInspectorTests).GetMethod(
                nameof(KleiBaselineTarget),
                BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo patch = typeof(
            FastTrackRuntimeAuthorityIntegrationInspectorTests).GetMethod(
                nameof(KleiBaselinePatch),
                BindingFlags.Static | BindingFlags.NonPublic)!;
        return new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity.KleiBaseline,
            capabilityId,
            new[]
            {
                new RuntimePatchGroupId(
                    "test-klei-" + capabilityId.Value)
            },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    target,
                    patch,
                    HarmonyPatchContractKind.Postfix)
            },
            new[]
            {
                new RuntimeAuthorityRequirement(
                    target,
                    RuntimeAuthorityRequirementKind.KleiOriginal,
                    requiredHarmonyOwner: null,
                    requiredPrefixMethod: null,
                    Array.Empty<string>())
            },
            diagnosticCode: null,
            diagnosticMessage: null);
    }

    private static async Task AssertEveryCapabilityUsesImplementation(
        RuntimePatchCapabilitySelection selection,
        RuntimeAuthorityImplementationKind expectedImplementationKind)
    {
        await Assert.That(selection.CapabilitySelections.Count).IsEqualTo(3);
        foreach (RuntimeCapabilitySelectionEntry capabilitySelection in
                 selection.CapabilitySelections)
        {
            await Assert.That(capabilitySelection.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
            await Assert.That(capabilitySelection.PrepareSelectedContribution()
                    .ImplementationIdentity.Kind).IsEqualTo(expectedImplementationKind);
        }
    }

    private static async Task<ExternalModIntegrationOutcome> AssertSingleFinalOutcome(
        RuntimePatchCapabilitySelection selection)
    {
        await Assert.That(selection.ExternalModIntegrationOutcomes.Count).IsEqualTo(1);
        return selection.ExternalModIntegrationOutcomes[0];
    }

    private static void KleiBaselineTarget()
    {
    }

    private static void KleiBaselinePatch()
    {
    }

    private static LoadedModInspectionContext CreateMatchedContext(
        FastTrackEmittedAssembly fixture,
        IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes) =>
        new(
            new[]
            {
                new LoadedModCandidate(
                    isActive: true,
                    "PeterHan.FastTrack",
                    new[] { fixture.Assembly })
            },
            activePrefixes);

    private static IReadOnlyList<ActiveHarmonyPrefixDescriptor>
        CopyActivePrefixes(FastTrackEmittedAssembly fixture) =>
        fixture.AllReplacements
            .Select(replacement => new ActiveHarmonyPrefixDescriptor(
                replacement.TargetMethod,
                replacement.PrefixMethod,
                replacement.HarmonyOwner,
                replacement.Priority))
            .ToArray();

    private static async Task<ExternalModIntegrationOutcome> AssertSingleOutcome(
        DeclaredIntegrationPreparationResult result)
    {
        await Assert.That(result.ExternalModIntegrationOutcomes.Count).IsEqualTo(1);
        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        await Assert.That(outcome.IntegrationId.Value).IsEqualTo("fast-track");
        await Assert.That(outcome.DisplayName).IsEqualTo("Fast Track");
        await Assert.That(outcome.Categories.Count).IsEqualTo(1);
        await Assert.That(outcome.Categories[0]).IsEqualTo(ExternalModIntegrationCategory.ExclusiveRuntimeAuthority);
        await Assert.That(outcome.Capabilities.Count).IsEqualTo(3);
        return outcome;
    }

    private static async Task AssertCapabilityOutcome(
        ExternalModIntegrationCapabilityOutcome actual,
        RuntimeCapabilityId expectedCapabilityId,
        RuntimeAuthorityObservation expectedAuthority,
        IntegrationContractState expectedContract,
        IntegrationCapabilityDisposition expectedDisposition,
        string? expectedDiagnosticCode)
    {
        await Assert.That(actual.CapabilityId).IsEqualTo(expectedCapabilityId);
        await Assert.That(actual.Category).IsEqualTo(ExternalModIntegrationCategory.ExclusiveRuntimeAuthority);
        await Assert.That(actual.AuthorityObservation).IsEqualTo(expectedAuthority);
        await Assert.That(actual.ContractState).IsEqualTo(expectedContract);
        await Assert.That(actual.Disposition).IsEqualTo(expectedDisposition);
        await Assert.That(actual.DiagnosticCode).IsEqualTo(expectedDiagnosticCode);
        if (expectedDiagnosticCode == null)
        {
            await Assert.That(actual.DiagnosticMessage).IsNull();
        }
        else
        {
            Assert.NotNull(actual.DiagnosticMessage);
        }
    }

    private static async Task AssertCompleteCompatibleContribution(
        PreparedRuntimeAuthorityContribution contribution,
        RuntimeCapabilityId expectedCapabilityId)
    {
        await Assert.That(contribution.CapabilityId).IsEqualTo(expectedCapabilityId);
        await Assert.That(contribution.ImplementationIdentity.Kind).IsEqualTo(RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
        await Assert.That(contribution.ImplementationIdentity.DeclaredExternalIntegrationId
                ?.Value).IsEqualTo("fast-track");
        await Assert.That(contribution.AuthorityObservation).IsEqualTo(RuntimeAuthorityObservation.OwnsCompatible);
        await Assert.That(contribution.PatchGroupIds.Count).IsEqualTo(1);
        await Assert.That(contribution.PatchBindings.Count).IsEqualTo(1);
        await Assert.That(contribution.AuthorityRequirements.Count).IsEqualTo(2);
        await Assert.That(contribution.DiagnosticCode).IsNull();
        await Assert.That(contribution.DiagnosticMessage).IsNull();
    }

    private static FastTrackAssemblyFileIdentity SuccessfulFileIdentity(
        string? assemblySha256 = null) =>
        new(
            FastTrackAssemblyFileIdentityReadState.Success,
            SupportedFileVersion,
            assemblySha256 ?? SupportedDigest,
            failureMessage: null);

    private static FastTrackSupportedAssemblyBuildCatalog
        CreateSupportedTestCatalog() =>
        new(new[]
        {
            new FastTrackAssemblyBuildIdentity(
                SupportedFileVersion,
                SupportedDigest)
        });

    private sealed class CompleteTestRuntimeAuthorityContributionBuilder :
        IFastTrackRuntimeAuthorityContributionBuilder
    {
        public PreparedRuntimeAuthorityContribution Build(
            DeclaredModIntegrationId integrationId,
            RuntimeCapabilityId capabilityId,
            FastTrackFeatureCompatibility readyFeature,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePrefixes)
        {
            if (readyFeature.State != FastTrackFeatureCompatibilityState.Ready ||
                readyFeature.Feature != ExpectedFeature(capabilityId))
            {
                throw new InvalidOperationException(
                    "The adapter supplied a feature that does not implement " +
                    "the requested runtime capability.");
            }

            MethodInfo requiredPrefix = readyFeature.GetVerifiedMember(
                    ReplacementPrefixRole(readyFeature.Feature)) as MethodInfo
                ?? throw new InvalidOperationException("The verified replacement prefix must be a method.");
            ActiveHarmonyPrefixDescriptor selectedAuthority = activePrefixes
                .Single(prefix =>
                    Equals(prefix.PrefixMethod, requiredPrefix) &&
                    string.Equals(
                        prefix.HarmonyOwner,
                        "PeterHan.FastTrack",
                        StringComparison.Ordinal));
            MethodInfo target = typeof(
                CompleteTestRuntimeAuthorityContributionBuilder).GetMethod(
                    nameof(PreparedTarget),
                    BindingFlags.Static | BindingFlags.NonPublic)!;
            MethodInfo patch = typeof(
                CompleteTestRuntimeAuthorityContributionBuilder).GetMethod(
                    nameof(PreparedPatch),
                    BindingFlags.Static | BindingFlags.NonPublic)!;
            return new PreparedRuntimeAuthorityContribution(
                RuntimeAuthorityImplementationIdentity
                    .ForDeclaredExternalIntegration(integrationId),
                capabilityId,
                new[]
                {
                    new RuntimePatchGroupId(
                        TestPatchGroupId(capabilityId))
                },
                RuntimeAuthorityObservation.OwnsCompatible,
                new[]
                {
                    new HarmonyPatchContractBinding(
                        target,
                        patch,
                        HarmonyPatchContractKind.Postfix)
                },
                new[]
                {
                    new RuntimeAuthorityRequirement(
                        selectedAuthority.TargetMethod,
                        RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                        "PeterHan.FastTrack",
                        requiredPrefix,
                        new[] { "PeterHan.FastTrack" }),
                    new RuntimeAuthorityRequirement(
                        target,
                        RuntimeAuthorityRequirementKind.KleiOriginal,
                        requiredHarmonyOwner: null,
                        requiredPrefixMethod: null,
                        new[] { "PeterHan.FastTrack" })
                },
                diagnosticCode: null,
                diagnosticMessage: null);
        }

        private static FastTrackFeature ExpectedFeature(
            RuntimeCapabilityId capabilityId)
        {
            if (capabilityId.Equals(
                    RuntimeCapabilityId
                        .WorldInventoryTemperaturePublication))
            {
                return FastTrackFeature.WorldInventory;
            }

            if (capabilityId.Equals(
                    RuntimeCapabilityId.PickupTemperatureGrouping))
            {
                return FastTrackFeature.PickupGrouping;
            }

            if (capabilityId.Equals(
                    RuntimeCapabilityId.DirectDeliveryEligibility))
            {
                return FastTrackFeature.DirectDeliveryEligibility;
            }

            throw new ArgumentOutOfRangeException(nameof(capabilityId));
        }

        private static FastTrackVerifiedMember ReplacementPrefixRole(
            FastTrackFeature feature) =>
            feature switch
            {
                FastTrackFeature.WorldInventory =>
                    FastTrackVerifiedMember.WorldInventoryReplacementPrefix,
                FastTrackFeature.PickupGrouping =>
                    FastTrackVerifiedMember
                        .PickupGroupingBeforeUpdatePickupsPrefix,
                FastTrackFeature.DirectDeliveryEligibility =>
                    FastTrackVerifiedMember
                        .DirectDeliveryEligibilityReplacementPrefix,
                _ => throw new ArgumentOutOfRangeException(nameof(feature))
            };

        private static string TestPatchGroupId(
            RuntimeCapabilityId capabilityId)
        {
            if (capabilityId.Equals(
                    RuntimeCapabilityId
                        .WorldInventoryTemperaturePublication))
            {
                return "test-fast-track-world-inventory";
            }

            if (capabilityId.Equals(
                    RuntimeCapabilityId.PickupTemperatureGrouping))
            {
                return "test-fast-track-pickup-grouping";
            }

            if (capabilityId.Equals(
                    RuntimeCapabilityId.DirectDeliveryEligibility))
            {
                return "test-fast-track-direct-delivery";
            }

            throw new ArgumentOutOfRangeException(nameof(capabilityId));
        }

        private static void PreparedTarget()
        {
        }

        private static void PreparedPatch()
        {
        }
    }

    private sealed class RecordingAssemblyFileIdentityReader :
        IFastTrackAssemblyFileIdentityReader
    {
        private readonly FastTrackAssemblyFileIdentity result;

        internal RecordingAssemblyFileIdentityReader(
            FastTrackAssemblyFileIdentity result)
        {
            this.result = result;
        }

        internal int ReadCallCount { get; private set; }

        public FastTrackAssemblyFileIdentity Read(Assembly fastTrackAssembly)
        {
            _ = fastTrackAssembly ??
                throw new ArgumentNullException(nameof(fastTrackAssembly));
            ReadCallCount++;
            return result;
        }
    }
}
