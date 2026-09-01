using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackRuntimeAuthorityIntegrationInspectorTests
{
    private static readonly Version SupportedFileVersion =
        new(0, 18, 4, 0);

    private const string SupportedDigest =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";

    private const string UnsupportedDigest =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [TestMethod]
    public void DeclaredIntegrationDescriptor_UsesExactSupportedFastTrackIdentityAndCapabilities()
    {
        DeclaredModIntegrationDescriptor descriptor =
            FastTrackRuntimeAuthorityIntegrationInspector
                .DeclaredIntegrationDescriptor;

        Assert.AreEqual("fast-track", descriptor.IntegrationId.Value);
        Assert.AreEqual("Fast Track", descriptor.DisplayName);
        CollectionAssert.AreEqual(
            new[] { "PeterHan.FastTrack" },
            descriptor.AcceptedStaticIds.ToArray());
        CollectionAssert.AreEqual(
            new[] { "FastTrack" },
            descriptor.AcceptedAssemblySimpleNames.ToArray());
        Assert.AreEqual(
            "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
            descriptor.UpstreamEvidenceReference);
        CollectionAssert.AreEqual(
            new[]
            {
                RuntimeCapabilityId.WorldInventoryTemperaturePublication,
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeCapabilityId.DirectDeliveryEligibility
            },
            descriptor.DeclaredCapabilityIds.ToArray());
        Assert.IsTrue(descriptor.DeclaredCapabilities.All(capability =>
            capability.Category ==
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority));
    }

    [TestMethod]
    public void Prepare_WhenFastTrackIsAbsent_ProjectsEveryCapabilityAsNotApplicable()
    {
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());

        DeclaredIntegrationPreparationResult result = Prepare(
            new LoadedModInspectionContext(
                Array.Empty<LoadedModCandidate>(),
                Array.Empty<ActiveHarmonyPrefixDescriptor>()),
            identityReader);

        ExternalModIntegrationOutcome outcome = AssertSingleOutcome(result);
        Assert.AreEqual(DeclaredModMatchState.NotMatched, outcome.MatchState);
        Assert.IsNull(outcome.AssemblyIdentity);
        Assert.IsNull(outcome.AssemblyVersion);
        Assert.IsNull(outcome.FileVersion);
        Assert.IsNull(outcome.AssemblySha256);
        Assert.AreEqual(0, result.RuntimeAuthorityContributions.Count);
        Assert.AreEqual(0, identityReader.ReadCallCount);
        AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
    }

    [TestMethod]
    public void Prepare_WhenFastTrackReplacementsAreInactive_ProjectsEveryCapabilityAsNotApplicable()
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

        ExternalModIntegrationOutcome outcome = AssertSingleOutcome(result);
        Assert.AreEqual(DeclaredModMatchState.Matched, outcome.MatchState);
        Assert.AreEqual(fixture.Assembly.FullName, outcome.AssemblyIdentity);
        Assert.AreEqual("0.18.0.0", outcome.AssemblyVersion);
        Assert.AreEqual("0.18.4.0", outcome.FileVersion);
        Assert.AreEqual(SupportedDigest, outcome.AssemblySha256);
        Assert.AreEqual(0, result.RuntimeAuthorityContributions.Count);
        Assert.AreEqual(1, identityReader.ReadCallCount);
        AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
        AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.NotEvaluated,
            IntegrationCapabilityDisposition.NotApplicable,
            expectedDiagnosticCode: null);
    }

    [TestMethod]
    public void Prepare_WhenEveryFastTrackReplacementIsReady_ProjectsReadyCompleteContributions()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity());

        DeclaredIntegrationPreparationResult result = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            identityReader);

        ExternalModIntegrationOutcome outcome = AssertSingleOutcome(result);
        Assert.AreEqual(DeclaredModMatchState.Matched, outcome.MatchState);
        Assert.AreEqual(SupportedDigest, outcome.AssemblySha256);
        Assert.AreEqual(1, identityReader.ReadCallCount);
        Assert.AreEqual(3, result.RuntimeAuthorityContributions.Count);
        AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            expectedDiagnosticCode: null);
        AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            expectedDiagnosticCode: null);
        AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            expectedDiagnosticCode: null);

        AssertCompleteCompatibleContribution(
            result.RuntimeAuthorityContributions[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication);
        AssertCompleteCompatibleContribution(
            result.RuntimeAuthorityContributions[1],
            RuntimeCapabilityId.PickupTemperatureGrouping);
        AssertCompleteCompatibleContribution(
            result.RuntimeAuthorityContributions[2],
            RuntimeCapabilityId.DirectDeliveryEligibility);
    }

    [TestMethod]
    public void Select_WhenFastTrackDoesNotOwnCapabilities_PreservesNotApplicableOutcomesAndSelectsKleiBaselines()
    {
        DeclaredIntegrationPreparationResult preparation = Prepare(
            new LoadedModInspectionContext(
                Array.Empty<LoadedModCandidate>(),
                Array.Empty<ActiveHarmonyPrefixDescriptor>()),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimePatchCapabilitySelection selection = Select(preparation);

        AssertEveryCapabilityUsesImplementation(
            selection,
            RuntimeAuthorityImplementationKind.KleiBaseline);
        ExternalModIntegrationOutcome outcome =
            AssertSingleFinalOutcome(selection);
        foreach (ExternalModIntegrationCapabilityOutcome capability in
                 outcome.Capabilities)
        {
            Assert.AreEqual(
                IntegrationCapabilityDisposition.NotApplicable,
                capability.Disposition);
        }
    }

    [TestMethod]
    public void Select_WhenEveryFastTrackReplacementIsReady_SelectsEveryExternalContribution()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        DeclaredIntegrationPreparationResult preparation = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimePatchCapabilitySelection selection = Select(preparation);

        AssertEveryCapabilityUsesImplementation(
            selection,
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration);
        ExternalModIntegrationOutcome outcome =
            AssertSingleFinalOutcome(selection);
        foreach (ExternalModIntegrationCapabilityOutcome capability in
                 outcome.Capabilities)
        {
            Assert.AreEqual(
                IntegrationCapabilityDisposition.Selected,
                capability.Disposition);
        }
    }

    [TestMethod]
    public void Select_WhenOptionalWorldInventoryReplacementIsIncompatible_OmitsCapabilityWithoutKleiFallback()
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
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            worldInventory.Disposition);
        Assert.IsNull(worldInventory.SelectedContribution);
        ExternalModIntegrationOutcome outcome =
            AssertSingleFinalOutcome(selection);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            outcome.Capabilities[0].Disposition);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Selected,
            outcome.Capabilities[1].Disposition);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Selected,
            outcome.Capabilities[2].Disposition);
    }

    [TestMethod]
    public void Select_WhenRequiredDirectDeliveryReplacementIsIncompatible_BlocksActivationWithoutKleiFallback()
    {
        FastTrackEmittedAssembly fixture = FastTrackReflectionEmitFixture
            .CreateWithDirectComparatorContractChanged("FastTrack");
        DeclaredIntegrationPreparationResult preparation = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            new RecordingAssemblyFileIdentityReader(
                SuccessfulFileIdentity()));

        RuntimeCapabilitySelectionException exception = Assert.ThrowsExactly<
            RuntimeCapabilitySelectionException>(() => Select(preparation));

        Assert.AreEqual(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            exception.CapabilityId);
        Assert.AreEqual(
            "required-runtime-capability-unavailable",
            exception.DiagnosticCode);
        Assert.AreEqual(1, exception.ExternalModIntegrationOutcomes.Count);
        ExternalModIntegrationOutcome outcome =
            exception.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Selected,
            outcome.Capabilities[0].Disposition);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Selected,
            outcome.Capabilities[1].Disposition);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.ActivationBlocking,
            outcome.Capabilities[2].Disposition);
    }

    [TestMethod]
    public void Prepare_WhenEveryActiveFastTrackReplacementUsesAnUnsupportedBuild_ProjectsRequiredAndOptionalFailurePolicies()
    {
        FastTrackEmittedAssembly fixture =
            FastTrackReflectionEmitFixture.CreateExpectedContract("FastTrack");
        var identityReader = new RecordingAssemblyFileIdentityReader(
            SuccessfulFileIdentity(UnsupportedDigest));

        DeclaredIntegrationPreparationResult result = Prepare(
            CreateMatchedContext(fixture, CopyActivePrefixes(fixture)),
            identityReader);

        ExternalModIntegrationOutcome outcome = AssertSingleOutcome(result);
        Assert.AreEqual(DeclaredModMatchState.Matched, outcome.MatchState);
        Assert.AreEqual(1, identityReader.ReadCallCount);
        Assert.AreEqual(3, result.RuntimeAuthorityContributions.Count);
        AssertCapabilityOutcome(
            outcome.Capabilities[0],
            RuntimeCapabilityId.WorldInventoryTemperaturePublication,
            RuntimeAuthorityObservation.OwnsIncompatible,
            IntegrationContractState.Incompatible,
            IntegrationCapabilityDisposition.Unavailable,
            "fast-track-world-inventory-build-unsupported");
        AssertCapabilityOutcome(
            outcome.Capabilities[1],
            RuntimeCapabilityId.PickupTemperatureGrouping,
            RuntimeAuthorityObservation.OwnsIncompatible,
            IntegrationContractState.Incompatible,
            IntegrationCapabilityDisposition.Unavailable,
            "fast-track-pickup-grouping-build-unsupported");
        AssertCapabilityOutcome(
            outcome.Capabilities[2],
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeAuthorityObservation.OwnsIncompatible,
            IntegrationContractState.Incompatible,
            IntegrationCapabilityDisposition.Unavailable,
            "fast-track-direct-delivery-build-unsupported");
        Assert.AreEqual(3, outcome.Diagnostics.Count);
        Assert.AreEqual(
            "The active WorldInventory FastTrack replacement reports an " +
            "unsupported assembly build. Observed file version: 0.18.4.0; " +
            "observed DLL SHA-256: " + UnsupportedDigest + ". Compatibility " +
            "requires one exact admitted version-and-digest pair.",
            outcome.Capabilities[0].DiagnosticMessage);

        foreach (PreparedRuntimeAuthorityContribution contribution in
                 result.RuntimeAuthorityContributions)
        {
            Assert.AreEqual(
                RuntimeAuthorityObservation.OwnsIncompatible,
                contribution.AuthorityObservation);
            Assert.AreEqual(0, contribution.PatchGroupIds.Count);
            Assert.AreEqual(0, contribution.PatchBindings.Count);
            Assert.AreEqual(0, contribution.AuthorityRequirements.Count);
            Assert.IsNotNull(contribution.DiagnosticCode);
            Assert.IsNotNull(contribution.DiagnosticMessage);
        }
    }

    [TestMethod]
    public void Prepare_WhenExactFastTrackEntryHasNoAssembly_DoesNotBorrowAnotherModsAssembly()
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

        ExternalModIntegrationOutcome outcome = AssertSingleOutcome(result);
        Assert.AreEqual(DeclaredModMatchState.Matched, outcome.MatchState);
        Assert.AreEqual(0, identityReader.ReadCallCount);
        Assert.AreEqual(3, result.RuntimeAuthorityContributions.Count);
        Assert.AreEqual(1, outcome.Diagnostics.Count);
        Assert.AreEqual(
            "declared-integration-assembly-missing",
            outcome.Diagnostics[0].Code);
        foreach (ExternalModIntegrationCapabilityOutcome capability in
                 outcome.Capabilities)
        {
            Assert.AreEqual(
                RuntimeAuthorityObservation.OwnershipUnavailable,
                capability.AuthorityObservation);
            Assert.AreEqual(
                IntegrationContractState.VerificationUnavailable,
                capability.ContractState);
            Assert.AreEqual(
                IntegrationCapabilityDisposition.Unavailable,
                capability.Disposition);
            Assert.AreEqual(
                "declared-integration-assembly-missing",
                capability.DiagnosticCode);
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
                CreateKleiBaselineContribution(
                    RuntimeCapabilityId
                        .WorldInventoryTemperaturePublication),
                atomicBundleId: null),
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.PickupTemperatureGrouping,
                RuntimeCapabilityCriticality.Required,
                CreateKleiBaselineContribution(
                    RuntimeCapabilityId.PickupTemperatureGrouping),
                atomicBundleId: null),
            new RuntimeCapabilityDefinition(
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityCriticality.Required,
                CreateKleiBaselineContribution(
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

    private static void AssertEveryCapabilityUsesImplementation(
        RuntimePatchCapabilitySelection selection,
        RuntimeAuthorityImplementationKind expectedImplementationKind)
    {
        Assert.AreEqual(3, selection.CapabilitySelections.Count);
        foreach (RuntimeCapabilitySelectionEntry capabilitySelection in
                 selection.CapabilitySelections)
        {
            Assert.AreEqual(
                IntegrationCapabilityDisposition.Selected,
                capabilitySelection.Disposition);
            Assert.AreEqual(
                expectedImplementationKind,
                capabilitySelection.SelectedContribution
                    ?.ImplementationIdentity.Kind);
        }
    }

    private static ExternalModIntegrationOutcome AssertSingleFinalOutcome(
        RuntimePatchCapabilitySelection selection)
    {
        Assert.AreEqual(1, selection.ExternalModIntegrationOutcomes.Count);
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

    private static ExternalModIntegrationOutcome AssertSingleOutcome(
        DeclaredIntegrationPreparationResult result)
    {
        Assert.AreEqual(1, result.ExternalModIntegrationOutcomes.Count);
        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual("fast-track", outcome.IntegrationId.Value);
        Assert.AreEqual("Fast Track", outcome.DisplayName);
        Assert.AreEqual(1, outcome.Categories.Count);
        Assert.AreEqual(
            ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
            outcome.Categories[0]);
        Assert.AreEqual(3, outcome.Capabilities.Count);
        return outcome;
    }

    private static void AssertCapabilityOutcome(
        ExternalModIntegrationCapabilityOutcome actual,
        RuntimeCapabilityId expectedCapabilityId,
        RuntimeAuthorityObservation expectedAuthority,
        IntegrationContractState expectedContract,
        IntegrationCapabilityDisposition expectedDisposition,
        string? expectedDiagnosticCode)
    {
        Assert.AreEqual(expectedCapabilityId, actual.CapabilityId);
        Assert.AreEqual(
            ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
            actual.Category);
        Assert.AreEqual(expectedAuthority, actual.AuthorityObservation);
        Assert.AreEqual(expectedContract, actual.ContractState);
        Assert.AreEqual(expectedDisposition, actual.Disposition);
        Assert.AreEqual(expectedDiagnosticCode, actual.DiagnosticCode);
        if (expectedDiagnosticCode == null)
        {
            Assert.IsNull(actual.DiagnosticMessage);
        }
        else
        {
            Assert.IsNotNull(actual.DiagnosticMessage);
        }
    }

    private static void AssertCompleteCompatibleContribution(
        PreparedRuntimeAuthorityContribution contribution,
        RuntimeCapabilityId expectedCapabilityId)
    {
        Assert.AreEqual(expectedCapabilityId, contribution.CapabilityId);
        Assert.AreEqual(
            RuntimeAuthorityImplementationKind.DeclaredExternalIntegration,
            contribution.ImplementationIdentity.Kind);
        Assert.AreEqual(
            "fast-track",
            contribution.ImplementationIdentity.DeclaredExternalIntegrationId
                ?.Value);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnsCompatible,
            contribution.AuthorityObservation);
        Assert.AreEqual(1, contribution.PatchGroupIds.Count);
        Assert.AreEqual(1, contribution.PatchBindings.Count);
        Assert.AreEqual(2, contribution.AuthorityRequirements.Count);
        Assert.IsNull(contribution.DiagnosticCode);
        Assert.IsNull(contribution.DiagnosticMessage);
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

            MethodInfo requiredPrefix = Assert.IsInstanceOfType<MethodInfo>(
                readyFeature.GetVerifiedMember(
                    ReplacementPrefixRole(readyFeature.Feature)));
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
