using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit.Tests.RuntimePatchInstallation;

[TestClass]
public sealed class DeclaredExternalModIntegrationPreparationTests
{
    private static readonly DeclaredModIntegrationId FastTrackId =
        new("fast-track");
    private static readonly DeclaredModIntegrationId SyntheticAdditiveId =
        new("synthetic-additive-protocol");

    [TestMethod]
    public void Prepare_WhenExactStaticIdAndSameEntryAssemblyMatch_InvokesDeclaredInspector()
    {
        Assembly fastTrackAssembly = DynamicAssembly("FastTrack");
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                new[] { fastTrackAssembly }));

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackDescriptor()),
                context,
                new[] { inspector },
                Array.Empty<IAdditiveInteroperabilityInspector>());

        Assert.AreEqual(1, inspector.InvocationCount);
        Assert.AreSame(fastTrackAssembly, inspector.ObservedAssembly);
        Assert.HasCount(1, result.ExternalModIntegrationOutcomes);
        Assert.AreEqual(
            DeclaredModMatchState.Matched,
            result.ExternalModIntegrationOutcomes[0].MatchState);
    }

    [TestMethod]
    public void Prepare_WhenMatchingModIsInactive_DoesNotInvokeInspector()
    {
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                false,
                "PeterHan.FastTrack",
                new[] { DynamicAssembly("FastTrack") }));

        DeclaredIntegrationPreparationResult result = PrepareFastTrack(
            context,
            inspector);

        Assert.AreEqual(0, inspector.InvocationCount);
        Assert.AreEqual(
            DeclaredModMatchState.NotMatched,
            result.ExternalModIntegrationOutcomes[0].MatchState);
        Assert.AreEqual(
            RuntimeAuthorityObservation.DoesNotOwn,
            result.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .AuthorityObservation);
    }

    [TestMethod]
    public void Prepare_WhenTwoActiveEntriesUseAcceptedStaticId_ReportsAmbiguity()
    {
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                new[] { DynamicAssembly("FastTrackOne") }),
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                new[] { DynamicAssembly("FastTrackTwo") }));

        DeclaredIntegrationPreparationResult result = PrepareFastTrack(
            context,
            inspector);

        Assert.AreEqual(0, inspector.InvocationCount);
        Assert.AreEqual(
            DeclaredModMatchState.Ambiguous,
            result.ExternalModIntegrationOutcomes[0].MatchState);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnershipUnavailable,
            result.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .AuthorityObservation);
        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnershipUnavailable,
            result.RuntimeAuthorityContributions[0].AuthorityObservation);
    }

    [TestMethod]
    public void Prepare_WhenSameEntryHasTwoAcceptedNameAssemblies_ReportsAmbiguity()
    {
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                new[]
                {
                    DynamicAssembly("FastTrack"),
                    DynamicAssembly("FastTrack")
                }));

        DeclaredIntegrationPreparationResult result = PrepareFastTrack(
            context,
            inspector);

        Assert.AreEqual(0, inspector.InvocationCount);
        Assert.AreEqual(
            DeclaredModMatchState.Ambiguous,
            result.ExternalModIntegrationOutcomes[0].MatchState);
    }

    [TestMethod]
    public void Prepare_WhenAcceptedAssemblyBelongsToDifferentModEntry_DoesNotLeakAssociation()
    {
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                Array.Empty<Assembly>()),
            new LoadedModCandidate(
                true,
                "Example.UnrelatedMod",
                new[] { DynamicAssembly("FastTrack") }));

        DeclaredIntegrationPreparationResult result = PrepareFastTrack(
            context,
            inspector);

        Assert.AreEqual(0, inspector.InvocationCount);
        Assert.AreEqual(
            DeclaredModMatchState.Matched,
            result.ExternalModIntegrationOutcomes[0].MatchState);
        Assert.AreEqual(
            IntegrationContractState.VerificationUnavailable,
            result.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .ContractState);
        Assert.AreEqual(
            "declared-integration-assembly-missing",
            result.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .DiagnosticCode);
    }

    [TestMethod]
    public void Prepare_WhenUnknownModCoexists_IgnoresItAndUsesDeclaredMatch()
    {
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                true,
                "Example.UnknownMod",
                new[] { DynamicAssembly("UnknownMod") }),
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                new[] { DynamicAssembly("FastTrack") }));

        DeclaredIntegrationPreparationResult result = PrepareFastTrack(
            context,
            inspector);

        Assert.AreEqual(1, inspector.InvocationCount);
        Assert.HasCount(1, result.ExternalModIntegrationOutcomes);
        Assert.AreEqual(FastTrackId, result.ExternalModIntegrationOutcomes[0].IntegrationId);
    }

    [TestMethod]
    public void Prepare_WhenRuntimeInspectorThrows_ConvertsFailureToUnavailableOutcome()
    {
        var inspector = new ThrowingRuntimeInspector(FastTrackId);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { inspector },
                Array.Empty<IAdditiveInteroperabilityInspector>());

        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnershipUnavailable,
            result.RuntimeAuthorityContributions[0].AuthorityObservation);
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            result.ExternalModIntegrationOutcomes[0].MatchState);
        Assert.AreEqual(
            "runtime-integration-inspection-unavailable",
            result.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .DiagnosticCode);
    }

    [TestMethod]
    public void Prepare_WhenRuntimeInspectorOmitsDeclaredCapability_RejectsWholeInspection()
    {
        var inspector = new RecordingRuntimeInspector(FastTrackId);
        DeclaredModIntegrationDescriptor descriptor = FastTrackDescriptor(
            RuntimeCapabilityId.DirectDeliveryEligibility,
            RuntimeCapabilityId.PickupTemperatureGrouping);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(descriptor),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { inspector },
                Array.Empty<IAdditiveInteroperabilityInspector>());

        Assert.HasCount(2, result.RuntimeAuthorityContributions);
        Assert.IsTrue(result.RuntimeAuthorityContributions.All(contribution =>
            contribution.AuthorityObservation ==
                RuntimeAuthorityObservation.OwnershipUnavailable));
        Assert.HasCount(
            2,
            result.ExternalModIntegrationOutcomes[0].Capabilities);
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            result.ExternalModIntegrationOutcomes[0].MatchState);
    }

    [TestMethod]
    public void Prepare_WhenInspectionUnavailableClaimsCompatibleAuthority_ContainsWholeRuntimeInspection()
    {
        var inspector = new PredefinedRuntimeAuthorityInspector(
            FastTrackId,
            DeclaredModMatchState.InspectionUnavailable,
            RuntimeAuthorityObservation.OwnsCompatible,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Ready,
            CompatibleContribution(FastTrackId));

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { inspector },
                Array.Empty<IAdditiveInteroperabilityInspector>());

        AssertRuntimeInspectionWasContained(result);
    }

    [TestMethod]
    public void Prepare_WhenNonOwnerUsesUnavailableContract_ContainsWholeRuntimeInspection()
    {
        var inspector = new PredefinedRuntimeAuthorityInspector(
            FastTrackId,
            DeclaredModMatchState.Matched,
            RuntimeAuthorityObservation.DoesNotOwn,
            IntegrationContractState.VerificationUnavailable,
            IntegrationCapabilityDisposition.Unavailable,
            null,
            "contradictory-non-owner-state",
            "A non-owner cannot report an unavailable authority contract.");

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { inspector },
                Array.Empty<IAdditiveInteroperabilityInspector>());

        AssertRuntimeInspectionWasContained(result);
    }

    [TestMethod]
    public void Prepare_WhenRuntimeDiagnosticsReuseCodeForDifferentMessages_ContainsWholeRuntimeInspection()
    {
        var inspector = new RecordingRuntimeInspector(
            FastTrackId,
            new ExternalModIntegrationDiagnostic(
                "shared-runtime-diagnostic",
                "The runtime integration was inspected."),
            "shared-runtime-diagnostic",
            "The runtime capability was inspected.");

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { inspector },
                Array.Empty<IAdditiveInteroperabilityInspector>());

        AssertRuntimeInspectionWasContained(result);
    }

    [TestMethod]
    public void Prepare_WhenMatchedAdditiveInspectorReportsSelectedCapability_ContainsAdditiveCategory()
    {
        var inspector = new PredefinedAdditiveInteroperabilityInspector(
            SyntheticAdditiveId,
            DeclaredModMatchState.Matched,
            IntegrationContractState.Compatible,
            IntegrationCapabilityDisposition.Selected);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(AdditiveDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "Example.SyntheticAdditive",
                    new[] { DynamicAssembly("SyntheticAdditive") })),
                Array.Empty<IRuntimeAuthorityIntegrationInspector>(),
                new[] { inspector });

        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            outcome.MatchState);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            outcome.Capabilities[0].Disposition);
        Assert.AreEqual(
            "additive-integration-inspection-unavailable",
            outcome.Capabilities[0].DiagnosticCode);
    }

    [TestMethod]
    public void Prepare_WhenRuntimeInspectorUsesReservedAdditiveConflictDiagnostic_ContainsRuntimeCategory()
    {
        var runtimeInspector = new RecordingRuntimeInspector(
            FastTrackId,
            new ExternalModIntegrationDiagnostic(
                "additive-integration-outcome-conflict",
                "A provider cannot redefine a preparation-owned diagnostic."));
        var additiveInspector =
            new ConflictingAssemblyFactAdditiveInspector(FastTrackId);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackRuntimeAndAdditiveDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { runtimeInspector },
                new[] { additiveInspector });

        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            outcome.MatchState);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnershipUnavailable,
            outcome.Capabilities[0].AuthorityObservation);
        Assert.AreEqual(
            "runtime-integration-inspection-unavailable",
            outcome.Capabilities[0].DiagnosticCode);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Ready,
            outcome.Capabilities[1].Disposition);
    }

    [TestMethod]
    public void Prepare_WhenAdditiveInspectorThrows_PreservesValidRuntimeContribution()
    {
        var runtimeInspector = new RecordingRuntimeInspector(FastTrackId);
        var additiveInspector =
            new ThrowingAdditiveInspector(SyntheticAdditiveId);
        LoadedModInspectionContext context = Context(
            new LoadedModCandidate(
                true,
                "PeterHan.FastTrack",
                new[] { DynamicAssembly("FastTrack") }),
            new LoadedModCandidate(
                true,
                "Example.SyntheticAdditive",
                new[] { DynamicAssembly("SyntheticAdditive") }));

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackDescriptor(), AdditiveDescriptor()),
                context,
                new[] { runtimeInspector },
                new[] { additiveInspector });

        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        Assert.AreEqual(
            FastTrackId,
            result.RuntimeAuthorityContributions[0]
                .ImplementationIdentity
                .DeclaredExternalIntegrationId);
        Assert.HasCount(2, result.ExternalModIntegrationOutcomes);
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            result.ExternalModIntegrationOutcomes[1].MatchState);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            result.ExternalModIntegrationOutcomes[1]
                .Capabilities[0]
                .Disposition);
    }

    [TestMethod]
    public void Prepare_WhenOneIntegrationDeclaresBothCategories_InvokesAndMergesBothInspectors()
    {
        var runtimeInspector = new RecordingRuntimeInspector(FastTrackId);
        var additiveInspector = new RecordingAdditiveInspector(FastTrackId);
        DeclaredModIntegrationDescriptor descriptor =
            FastTrackRuntimeAndAdditiveDescriptor();

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(descriptor),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { runtimeInspector },
                new[] { additiveInspector });

        Assert.AreEqual(1, runtimeInspector.InvocationCount);
        Assert.AreEqual(1, additiveInspector.InvocationCount);
        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        Assert.HasCount(1, result.ExternalModIntegrationOutcomes);
        CollectionAssert.AreEqual(
            new[]
            {
                RuntimeCapabilityId.DirectDeliveryEligibility,
                RuntimeCapabilityId.TemperatureStatusAvailability
            },
            result.ExternalModIntegrationOutcomes[0]
                .Capabilities
                .Select(capability => capability.CapabilityId)
                .ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                ExternalModIntegrationCategory.AdditiveInteroperability
            },
            result.ExternalModIntegrationOutcomes[0].Categories.ToArray());
    }

    [TestMethod]
    public void Prepare_WhenAdditiveOutcomeConflictsWithRuntimeFacts_ContainsAdditiveCategory()
    {
        var runtimeInspector = new RecordingRuntimeInspector(FastTrackId);
        var additiveInspector =
            new ConflictingAssemblyFactAdditiveInspector(FastTrackId);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackRuntimeAndAdditiveDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { runtimeInspector },
                new[] { additiveInspector });

        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            outcome.MatchState);
        Assert.AreEqual(
            "FastTrack, Version=0.18.4.0",
            outcome.AssemblyIdentity);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnsCompatible,
            outcome.Capabilities[0].AuthorityObservation);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            outcome.Capabilities[1].Disposition);
        Assert.AreEqual(
            "additive-integration-outcome-conflict",
            outcome.Capabilities[1].DiagnosticCode);
    }

    [TestMethod]
    public void Prepare_WhenAdditiveOutcomeConflictsWithRuntimeDiagnostic_ContainsAdditiveCategory()
    {
        var runtimeInspector = new RecordingRuntimeInspector(
            FastTrackId,
            new ExternalModIntegrationDiagnostic(
                "shared-category-diagnostic",
                "Runtime-authority evidence was inspected."));
        var additiveInspector =
            new ConflictingDiagnosticAdditiveInspector(FastTrackId);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackRuntimeAndAdditiveDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { runtimeInspector },
                new[] { additiveInspector });

        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            outcome.MatchState);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnsCompatible,
            outcome.Capabilities[0].AuthorityObservation);
        Assert.AreEqual(
            "additive-integration-outcome-conflict",
            outcome.Capabilities[1].DiagnosticCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "shared-category-diagnostic",
                "additive-integration-outcome-conflict"
            },
            outcome.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Prepare_WhenAdditiveCapabilityDiagnosticConflictsWithRuntimeCapabilityDiagnostic_ContainsAdditiveCategory()
    {
        var runtimeInspector = new RecordingRuntimeInspector(
            FastTrackId,
            capabilityDiagnosticCode: "shared-capability-diagnostic",
            capabilityDiagnosticMessage:
                "Runtime-authority capability evidence was inspected.");
        var additiveInspector =
            new ConflictingCapabilityDiagnosticAdditiveInspector(FastTrackId);

        DeclaredIntegrationPreparationResult result =
            DeclaredExternalModIntegrationPreparation.Prepare(
                Catalog(FastTrackRuntimeAndAdditiveDescriptor()),
                Context(new LoadedModCandidate(
                    true,
                    "PeterHan.FastTrack",
                    new[] { DynamicAssembly("FastTrack") })),
                new[] { runtimeInspector },
                new[] { additiveInspector });

        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            "shared-capability-diagnostic",
            outcome.Capabilities[0].DiagnosticCode);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            outcome.Capabilities[1].Disposition);
        Assert.AreEqual(
            "additive-integration-outcome-conflict",
            outcome.Capabilities[1].DiagnosticCode);
    }

    private static DeclaredIntegrationPreparationResult PrepareFastTrack(
        LoadedModInspectionContext context,
        RecordingRuntimeInspector inspector) =>
        DeclaredExternalModIntegrationPreparation.Prepare(
            Catalog(FastTrackDescriptor()),
            context,
            new[] { inspector },
            Array.Empty<IAdditiveInteroperabilityInspector>());

    private static void AssertRuntimeInspectionWasContained(
        DeclaredIntegrationPreparationResult result)
    {
        Assert.HasCount(1, result.RuntimeAuthorityContributions);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnershipUnavailable,
            result.RuntimeAuthorityContributions[0].AuthorityObservation);
        ExternalModIntegrationOutcome outcome =
            result.ExternalModIntegrationOutcomes[0];
        Assert.AreEqual(
            DeclaredModMatchState.InspectionUnavailable,
            outcome.MatchState);
        Assert.AreEqual(
            RuntimeAuthorityObservation.OwnershipUnavailable,
            outcome.Capabilities[0].AuthorityObservation);
        Assert.AreEqual(
            IntegrationContractState.VerificationUnavailable,
            outcome.Capabilities[0].ContractState);
        Assert.AreEqual(
            IntegrationCapabilityDisposition.Unavailable,
            outcome.Capabilities[0].Disposition);
        Assert.AreEqual(
            "runtime-integration-inspection-unavailable",
            outcome.Capabilities[0].DiagnosticCode);
    }

    private static LoadedModInspectionContext Context(
        params LoadedModCandidate[] candidates) =>
        new(
            candidates,
            Array.Empty<ActiveHarmonyPrefixDescriptor>());

    private static DeclaredModIntegrationCatalog Catalog(
        params DeclaredModIntegrationDescriptor[] descriptors) =>
        new(descriptors);

    private static DeclaredModIntegrationDescriptor FastTrackDescriptor(
        params RuntimeCapabilityId[] declaredCapabilityIds) =>
        new(
            FastTrackId,
            "Fast Track",
            new[] { "PeterHan.FastTrack" },
            new[] { "FastTrack" },
            "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
            declaredCapabilityIds.Length == 0
                ? new[]
                {
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority)
                }
                : declaredCapabilityIds.Select(capabilityId =>
                    new DeclaredModIntegrationCapability(
                        capabilityId,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority)));

    private static DeclaredModIntegrationDescriptor AdditiveDescriptor() =>
        new(
            SyntheticAdditiveId,
            "Synthetic Additive Protocol",
            new[] { "Example.SyntheticAdditive" },
            new[] { "SyntheticAdditive" },
            "https://example.com/synthetic-additive-protocol",
            new[]
            {
                new DeclaredModIntegrationCapability(
                    RuntimeCapabilityId.TemperatureStatusAvailability,
                    ExternalModIntegrationCategory.AdditiveInteroperability)
            });

    private static DeclaredModIntegrationDescriptor
        FastTrackRuntimeAndAdditiveDescriptor() =>
            new(
                FastTrackId,
                "Fast Track",
                new[] { "PeterHan.FastTrack" },
                new[] { "FastTrack" },
                "https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta",
                new[]
                {
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.DirectDeliveryEligibility,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority),
                    new DeclaredModIntegrationCapability(
                        RuntimeCapabilityId.TemperatureStatusAvailability,
                        ExternalModIntegrationCategory
                            .AdditiveInteroperability)
                });

    private static Assembly DynamicAssembly(string simpleName) =>
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(simpleName),
            AssemblyBuilderAccess.Run);

    private sealed class RecordingRuntimeInspector :
        IRuntimeAuthorityIntegrationInspector
    {
        private readonly ExternalModIntegrationDiagnostic? diagnostic;
        private readonly string? capabilityDiagnosticCode;
        private readonly string? capabilityDiagnosticMessage;

        internal RecordingRuntimeInspector(
            DeclaredModIntegrationId integrationId,
            ExternalModIntegrationDiagnostic? diagnostic = null,
            string? capabilityDiagnosticCode = null,
            string? capabilityDiagnosticMessage = null)
        {
            IntegrationId = integrationId;
            this.diagnostic = diagnostic;
            this.capabilityDiagnosticCode = capabilityDiagnosticCode;
            this.capabilityDiagnosticMessage = capabilityDiagnosticMessage;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        internal int InvocationCount { get; private set; }

        internal Assembly? ObservedAssembly { get; private set; }

        public PreparedRuntimeAuthorityInspection Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context)
        {
            InvocationCount++;
            DeclaredLoadedModMatch match = context.Match(descriptor);
            ObservedAssembly = match.MatchedAssembly;
            PreparedRuntimeAuthorityContribution contribution =
                CompatibleContribution(descriptor.IntegrationId);
            return new PreparedRuntimeAuthorityInspection(
                Outcome(
                    descriptor,
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    DeclaredModMatchState.Matched,
                    RuntimeAuthorityObservation.OwnsCompatible,
                    IntegrationContractState.Compatible,
                    IntegrationCapabilityDisposition.Ready,
                    capabilityDiagnosticCode,
                    capabilityDiagnosticMessage,
                    assemblyIdentity: "FastTrack, Version=0.18.4.0",
                    diagnostics: diagnostic == null
                        ? null
                        : new[] { diagnostic }),
                new[] { contribution });
        }
    }

    private sealed class RecordingAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        internal RecordingAdditiveInspector(
            DeclaredModIntegrationId integrationId)
        {
            IntegrationId = integrationId;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        internal int InvocationCount { get; private set; }

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context)
        {
            InvocationCount++;
            return Outcome(
                descriptor,
                RuntimeCapabilityId.TemperatureStatusAvailability,
                DeclaredModMatchState.Matched,
                RuntimeAuthorityObservation.DoesNotOwn,
                IntegrationContractState.Compatible,
                IntegrationCapabilityDisposition.Ready,
                null,
                null);
        }
    }

    private sealed class PredefinedRuntimeAuthorityInspector :
        IRuntimeAuthorityIntegrationInspector
    {
        private readonly DeclaredModMatchState matchState;
        private readonly RuntimeAuthorityObservation authorityObservation;
        private readonly IntegrationContractState contractState;
        private readonly IntegrationCapabilityDisposition disposition;
        private readonly PreparedRuntimeAuthorityContribution? contribution;
        private readonly string? diagnosticCode;
        private readonly string? diagnosticMessage;

        internal PredefinedRuntimeAuthorityInspector(
            DeclaredModIntegrationId integrationId,
            DeclaredModMatchState matchState,
            RuntimeAuthorityObservation authorityObservation,
            IntegrationContractState contractState,
            IntegrationCapabilityDisposition disposition,
            PreparedRuntimeAuthorityContribution? contribution,
            string? diagnosticCode = null,
            string? diagnosticMessage = null)
        {
            IntegrationId = integrationId;
            this.matchState = matchState;
            this.authorityObservation = authorityObservation;
            this.contractState = contractState;
            this.disposition = disposition;
            this.contribution = contribution;
            this.diagnosticCode = diagnosticCode;
            this.diagnosticMessage = diagnosticMessage;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public PreparedRuntimeAuthorityInspection Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            new(
                Outcome(
                    descriptor,
                    RuntimeCapabilityId.DirectDeliveryEligibility,
                    matchState,
                    authorityObservation,
                    contractState,
                    disposition,
                    diagnosticCode,
                    diagnosticMessage),
                contribution == null
                    ? Array.Empty<PreparedRuntimeAuthorityContribution>()
                    : new[] { contribution });
    }

    private sealed class PredefinedAdditiveInteroperabilityInspector :
        IAdditiveInteroperabilityInspector
    {
        private readonly DeclaredModMatchState matchState;
        private readonly IntegrationContractState contractState;
        private readonly IntegrationCapabilityDisposition disposition;

        internal PredefinedAdditiveInteroperabilityInspector(
            DeclaredModIntegrationId integrationId,
            DeclaredModMatchState matchState,
            IntegrationContractState contractState,
            IntegrationCapabilityDisposition disposition)
        {
            IntegrationId = integrationId;
            this.matchState = matchState;
            this.contractState = contractState;
            this.disposition = disposition;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            Outcome(
                descriptor,
                RuntimeCapabilityId.TemperatureStatusAvailability,
                matchState,
                RuntimeAuthorityObservation.DoesNotOwn,
                contractState,
                disposition,
                null,
                null);
    }

    private sealed class ThrowingRuntimeInspector :
        IRuntimeAuthorityIntegrationInspector
    {
        internal ThrowingRuntimeInspector(DeclaredModIntegrationId integrationId)
        {
            IntegrationId = integrationId;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public PreparedRuntimeAuthorityInspection Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            throw new InvalidOperationException("synthetic runtime inspection failure");
    }

    private sealed class ThrowingAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        internal ThrowingAdditiveInspector(DeclaredModIntegrationId integrationId)
        {
            IntegrationId = integrationId;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            throw new InvalidOperationException("synthetic additive inspection failure");
    }

    private sealed class ConflictingAssemblyFactAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        internal ConflictingAssemblyFactAdditiveInspector(
            DeclaredModIntegrationId integrationId)
        {
            IntegrationId = integrationId;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            Outcome(
                descriptor,
                RuntimeCapabilityId.TemperatureStatusAvailability,
                DeclaredModMatchState.Matched,
                RuntimeAuthorityObservation.DoesNotOwn,
                IntegrationContractState.Compatible,
                IntegrationCapabilityDisposition.Ready,
                null,
                null,
                assemblyIdentity: "FastTrack, Version=0.18.5.0");
    }

    private sealed class ConflictingDiagnosticAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        internal ConflictingDiagnosticAdditiveInspector(
            DeclaredModIntegrationId integrationId)
        {
            IntegrationId = integrationId;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            Outcome(
                descriptor,
                RuntimeCapabilityId.TemperatureStatusAvailability,
                DeclaredModMatchState.Matched,
                RuntimeAuthorityObservation.DoesNotOwn,
                IntegrationContractState.Compatible,
                IntegrationCapabilityDisposition.Ready,
                null,
                null,
                diagnostics: new[]
                {
                    new ExternalModIntegrationDiagnostic(
                        "shared-category-diagnostic",
                        "Additive protocol evidence was inspected.")
                });
    }

    private sealed class ConflictingCapabilityDiagnosticAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        internal ConflictingCapabilityDiagnosticAdditiveInspector(
            DeclaredModIntegrationId integrationId)
        {
            IntegrationId = integrationId;
        }

        public DeclaredModIntegrationId IntegrationId { get; }

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            Outcome(
                descriptor,
                RuntimeCapabilityId.TemperatureStatusAvailability,
                DeclaredModMatchState.Matched,
                RuntimeAuthorityObservation.DoesNotOwn,
                IntegrationContractState.Compatible,
                IntegrationCapabilityDisposition.Ready,
                "shared-capability-diagnostic",
                "Additive capability evidence was inspected.");
    }

    private static PreparedRuntimeAuthorityContribution CompatibleContribution(
        DeclaredModIntegrationId integrationId) =>
        new(
            RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(integrationId),
            RuntimeCapabilityId.DirectDeliveryEligibility,
            new[] { new RuntimePatchGroupId("fast-track-direct-delivery") },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    TargetMethod(),
                    PrefixMethod(),
                    HarmonyPatchContractKind.Prefix)
            },
            new[]
            {
                new RuntimeAuthorityRequirement(
                    TargetMethod(),
                    RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                    "PeterHan.FastTrack",
                    PrefixMethod(),
                    new[] { "PeterHan.FastTrack" })
            },
            null,
            null);

    private static ExternalModIntegrationOutcome Outcome(
        DeclaredModIntegrationDescriptor descriptor,
        RuntimeCapabilityId capabilityId,
        DeclaredModMatchState matchState,
        RuntimeAuthorityObservation observation,
        IntegrationContractState contractState,
        IntegrationCapabilityDisposition disposition,
        string? diagnosticCode,
        string? diagnosticMessage,
        string? assemblyIdentity = null,
        IEnumerable<ExternalModIntegrationDiagnostic>? diagnostics = null) =>
        new(
            descriptor.IntegrationId,
            descriptor.DisplayName,
            descriptor.Categories,
            matchState,
            assemblyIdentity,
            null,
            null,
            null,
            new[]
            {
                new ExternalModIntegrationCapabilityOutcome(
                    capabilityId,
                    RequireDeclaredCapabilityCategory(
                        descriptor,
                        capabilityId),
                    observation,
                    contractState,
                    disposition,
                    diagnosticCode,
                    diagnosticMessage)
            },
            diagnostics ?? Array.Empty<ExternalModIntegrationDiagnostic>());

    private static ExternalModIntegrationCategory
        RequireDeclaredCapabilityCategory(
            DeclaredModIntegrationDescriptor descriptor,
            RuntimeCapabilityId capabilityId)
    {
        for (int index = 0;
             index < descriptor.DeclaredCapabilities.Count;
             index++)
        {
            DeclaredModIntegrationCapability declaration =
                descriptor.DeclaredCapabilities[index];
            if (declaration.CapabilityId.Equals(capabilityId))
            {
                return declaration.Category;
            }
        }

        throw new InvalidOperationException(
            "The test outcome requires a declared capability category.");
    }

    private static MethodInfo TargetMethod() =>
        typeof(InspectionFixture).GetMethod(
            nameof(InspectionFixture.Target),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static MethodInfo PrefixMethod() =>
        typeof(InspectionFixture).GetMethod(
            nameof(InspectionFixture.Prefix),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static class InspectionFixture
    {
        internal static void Target()
        {
        }

        internal static void Prefix()
        {
        }
    }
}
