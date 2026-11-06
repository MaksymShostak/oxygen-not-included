using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;
public sealed class ExternalIntegrationExtensionContractTests
{
    private static readonly DeclaredModIntegrationId SyntheticRuntimeId =
        new("synthetic-runtime");
    private static readonly RuntimeCapabilityId SyntheticRuntimeCapabilityId =
        new("synthetic-runtime-capability");
    private static readonly DeclaredModIntegrationId SyntheticAdditiveId =
        new("synthetic-additive");
    private static readonly RuntimeCapabilityId SyntheticAdditiveCapabilityId =
        new("synthetic-settings-transfer");

    [Test]
    public async Task RuntimeAuthorityExtension_WhenDeclaredAndCompatible_SuppliesCapabilityWithoutSelectorBranch()
    {
        DeclaredModIntegrationDescriptor descriptor = RuntimeDescriptor();
        LoadedModInspectionContext context = Context(
            "Synthetic.Runtime",
            "SyntheticRuntimeExtension");
        DeclaredIntegrationPreparationResult preparation =
            DeclaredExternalModIntegrationPreparation.Prepare(
                new DeclaredModIntegrationCatalog(new[] { descriptor }),
                context,
                new IRuntimeAuthorityIntegrationInspector[]
                {
                    new SyntheticRuntimeAuthorityInspector()
                },
                Array.Empty<IAdditiveInteroperabilityInspector>());
        RuntimeCapabilityDefinition definition =
            new(
                SyntheticRuntimeCapabilityId,
                RuntimeCapabilityCriticality.Required,
                () => CreateKleiBaselineContribution(),
                atomicBundleId: null);

        RuntimePatchCapabilitySelection selection =
            RuntimePatchCapabilitySelector.Select(
                new[] { definition },
                preparation.RuntimeAuthorityContributions,
                preparation.ExternalModIntegrationOutcomes);

        RuntimeCapabilitySelectionEntry selected =
            selection.GetCapabilitySelection(SyntheticRuntimeCapabilityId);
        await Assert.That(selected.HasSelectedContribution).IsTrue();
        PreparedRuntimeAuthorityContribution selectedContribution =
            selected.PrepareSelectedContribution();
        await Assert.That(selectedContribution.ImplementationIdentity).IsEqualTo(RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(SyntheticRuntimeId));
        await Assert.That(selectedContribution.PatchGroupIds[0].Value).IsEqualTo("synthetic-runtime-patches");
        await Assert.That(selection.ExternalModIntegrationOutcomes[0]
                .Capabilities[0]
                .Disposition).IsEqualTo(IntegrationCapabilityDisposition.Selected);
    }

    [Test]
    public async Task AdditiveExtension_WhenReady_ReportsCapabilityWithoutHarmonyContribution()
    {
        DeclaredIntegrationPreparationResult preparation = PrepareAdditive(
            new SyntheticAdditiveInspector(
                IntegrationContractState.Compatible,
                IntegrationCapabilityDisposition.Ready));

        await Assert.That(preparation.RuntimeAuthorityContributions).Count().IsEqualTo(0);
        ExternalModIntegrationCapabilityOutcome capability = preparation
            .ExternalModIntegrationOutcomes[0]
            .Capabilities[0];
        await Assert.That(capability.ContractState).IsEqualTo(IntegrationContractState.Compatible);
        await Assert.That(capability.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Ready);
    }

    [Test]
    public async Task AdditiveExtension_WhenUnavailable_ReportsBoundedFailureWithoutHarmonyContribution()
    {
        DeclaredIntegrationPreparationResult preparation = PrepareAdditive(
            new SyntheticAdditiveInspector(
                IntegrationContractState.VerificationUnavailable,
                IntegrationCapabilityDisposition.Unavailable));

        await Assert.That(preparation.RuntimeAuthorityContributions).Count().IsEqualTo(0);
        ExternalModIntegrationCapabilityOutcome capability = preparation
            .ExternalModIntegrationOutcomes[0]
            .Capabilities[0];
        await Assert.That(capability.ContractState).IsEqualTo(IntegrationContractState.VerificationUnavailable);
        await Assert.That(capability.Disposition).IsEqualTo(IntegrationCapabilityDisposition.Unavailable);
        await Assert.That(capability.DiagnosticCode).IsEqualTo("synthetic-additive-unavailable");
    }

    [Test]
    public async Task AdditiveExtension_WhenInspectorThrows_ContainsFailureWithoutHarmonyContribution()
    {
        DeclaredIntegrationPreparationResult preparation = PrepareAdditive(
            new ThrowingSyntheticAdditiveInspector());

        await Assert.That(preparation.RuntimeAuthorityContributions).Count().IsEqualTo(0);
        ExternalModIntegrationOutcome outcome =
            preparation.ExternalModIntegrationOutcomes[0];
        await Assert.That(outcome.MatchState).IsEqualTo(DeclaredModMatchState.InspectionUnavailable);
        await Assert.That(outcome.Capabilities[0].ContractState).IsEqualTo(IntegrationContractState.VerificationUnavailable);
        await Assert.That(outcome.Capabilities[0].DiagnosticCode).IsEqualTo("additive-integration-inspection-unavailable");
    }

    [Test]
    public async Task ProductionCatalog_WhenCompositionSourceIsInspected_DeclaresOnlyFastTrackAtThisMilestone()
    {
        string repositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT") ??
            throw new InvalidOperationException(
                "ONI_MOD_PIPELINE_REPOSITORY_ROOT is required.");
        string installerSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchInstaller.cs"));
        const string descriptorReference = ".DeclaredIntegrationDescriptor";

        await Assert.That(CountOrdinalOccurrences(installerSource, descriptorReference)).IsEqualTo(1).Because("Production composition must name exactly one compile-time " +
            "integration descriptor at this milestone.");
        await Assert.That(installerSource).Contains("FastTrackRuntimeAuthorityIntegrationInspector");
        await Assert.That(installerSource.Contains(
                "blueprints-expanded",
                StringComparison.Ordinal)).IsFalse().Because("The extension proof must not advertise production Blueprints " +
            "Expanded support.");
    }

    private static DeclaredIntegrationPreparationResult PrepareAdditive(
        IAdditiveInteroperabilityInspector inspector)
    {
        DeclaredModIntegrationDescriptor descriptor = AdditiveDescriptor();
        return DeclaredExternalModIntegrationPreparation.Prepare(
            new DeclaredModIntegrationCatalog(new[] { descriptor }),
            Context("Synthetic.Additive", "SyntheticAdditiveExtension"),
            Array.Empty<IRuntimeAuthorityIntegrationInspector>(),
            new[] { inspector });
    }

    private static DeclaredModIntegrationDescriptor RuntimeDescriptor() =>
        new(
            SyntheticRuntimeId,
            "Synthetic Runtime Integration",
            new[] { "Synthetic.Runtime" },
            new[] { "SyntheticRuntimeExtension" },
            "https://example.invalid/synthetic-runtime-evidence",
            new[]
            {
                new DeclaredModIntegrationCapability(
                    SyntheticRuntimeCapabilityId,
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority)
            });

    private static DeclaredModIntegrationDescriptor AdditiveDescriptor() =>
        new(
            SyntheticAdditiveId,
            "Synthetic Additive Integration",
            new[] { "Synthetic.Additive" },
            new[] { "SyntheticAdditiveExtension" },
            "https://example.invalid/synthetic-additive-evidence",
            new[]
            {
                new DeclaredModIntegrationCapability(
                    SyntheticAdditiveCapabilityId,
                    ExternalModIntegrationCategory.AdditiveInteroperability)
            });

    private static LoadedModInspectionContext Context(
        string staticId,
        string assemblySimpleName) =>
        new(
            new[]
            {
                new LoadedModCandidate(
                    isActive: true,
                    staticId,
                    new[] { DynamicAssembly(assemblySimpleName) })
            },
            Array.Empty<ActiveHarmonyPrefixDescriptor>());

    private static Assembly DynamicAssembly(string simpleName) =>
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(simpleName),
            AssemblyBuilderAccess.Run);

    private static PreparedRuntimeAuthorityContribution
        CreateKleiBaselineContribution()
    {
        MethodInfo target = RequireMethod(nameof(SyntheticRuntimeTarget));
        return new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity.KleiBaseline,
            SyntheticRuntimeCapabilityId,
            new[] { new RuntimePatchGroupId("klei-synthetic-runtime") },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    target,
                    RequireMethod(nameof(PreparedPostfix)),
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

    private static PreparedRuntimeAuthorityContribution
        CreateSyntheticRuntimeContribution()
    {
        MethodInfo target = RequireMethod(nameof(SyntheticRuntimeTarget));
        MethodInfo requiredPrefix = RequireMethod(
            nameof(SyntheticRuntimeSkippingPrefix));
        return new PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity
                .ForDeclaredExternalIntegration(SyntheticRuntimeId),
            SyntheticRuntimeCapabilityId,
            new[] { new RuntimePatchGroupId("synthetic-runtime-patches") },
            RuntimeAuthorityObservation.OwnsCompatible,
            new[]
            {
                new HarmonyPatchContractBinding(
                    target,
                    RequireMethod(nameof(PreparedPostfix)),
                    HarmonyPatchContractKind.Postfix)
            },
            new[]
            {
                new RuntimeAuthorityRequirement(
                    target,
                    RuntimeAuthorityRequirementKind.ExactOwnedReplacement,
                    "Synthetic.Runtime.Owner",
                    requiredPrefix,
                    new[] { "Synthetic.Runtime.Owner" })
            },
            diagnosticCode: null,
            diagnosticMessage: null);
    }

    private static MethodInfo RequireMethod(string methodName) =>
        typeof(ExternalIntegrationExtensionContractTests).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Missing fixture method: " + methodName);

    private static int CountOrdinalOccurrences(string value, string fragment)
    {
        int count = 0;
        int searchIndex = 0;
        while ((searchIndex = value.IndexOf(
                   fragment,
                   searchIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += fragment.Length;
        }

        return count;
    }

    private static void SyntheticRuntimeTarget()
    {
    }

    private static bool SyntheticRuntimeSkippingPrefix() => false;

    private static void PreparedPostfix()
    {
    }

    private sealed class SyntheticRuntimeAuthorityInspector :
        IRuntimeAuthorityIntegrationInspector
    {
        public DeclaredModIntegrationId IntegrationId => SyntheticRuntimeId;

        public PreparedRuntimeAuthorityInspection Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context)
        {
            var outcome = new ExternalModIntegrationOutcome(
                descriptor.IntegrationId,
                descriptor.DisplayName,
                descriptor.Categories,
                DeclaredModMatchState.Matched,
                "SyntheticRuntimeExtension, Version=1.0.0.0",
                "1.0.0.0",
                "1.0.0.0",
                new string('A', 64),
                new[]
                {
                    new ExternalModIntegrationCapabilityOutcome(
                        SyntheticRuntimeCapabilityId,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority,
                        RuntimeAuthorityObservation.OwnsCompatible,
                        IntegrationContractState.Compatible,
                        IntegrationCapabilityDisposition.Ready,
                        diagnosticCode: null,
                        diagnosticMessage: null)
                },
                Array.Empty<ExternalModIntegrationDiagnostic>());
            return new PreparedRuntimeAuthorityInspection(
                outcome,
                new[] { CreateSyntheticRuntimeContribution() });
        }
    }

    private sealed class SyntheticAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        private readonly IntegrationContractState contractState;
        private readonly IntegrationCapabilityDisposition disposition;

        internal SyntheticAdditiveInspector(
            IntegrationContractState contractState,
            IntegrationCapabilityDisposition disposition)
        {
            this.contractState = contractState;
            this.disposition = disposition;
        }

        public DeclaredModIntegrationId IntegrationId => SyntheticAdditiveId;

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context)
        {
            bool unavailable = disposition ==
                IntegrationCapabilityDisposition.Unavailable;
            return new ExternalModIntegrationOutcome(
                descriptor.IntegrationId,
                descriptor.DisplayName,
                descriptor.Categories,
                DeclaredModMatchState.Matched,
                "SyntheticAdditiveExtension, Version=1.0.0.0",
                "1.0.0.0",
                "1.0.0.0",
                new string('B', 64),
                new[]
                {
                    new ExternalModIntegrationCapabilityOutcome(
                        SyntheticAdditiveCapabilityId,
                        ExternalModIntegrationCategory.AdditiveInteroperability,
                        RuntimeAuthorityObservation.DoesNotOwn,
                        contractState,
                        disposition,
                        unavailable ? "synthetic-additive-unavailable" : null,
                        unavailable
                            ? "The synthetic additive contract was unavailable."
                            : null)
                },
                unavailable
                    ? new[]
                    {
                        new ExternalModIntegrationDiagnostic(
                            "synthetic-additive-unavailable",
                            "The synthetic additive contract was unavailable.")
                    }
                    : Array.Empty<ExternalModIntegrationDiagnostic>());
        }
    }

    private sealed class ThrowingSyntheticAdditiveInspector :
        IAdditiveInteroperabilityInspector
    {
        public DeclaredModIntegrationId IntegrationId => SyntheticAdditiveId;

        public ExternalModIntegrationOutcome Inspect(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context) =>
            throw new InvalidOperationException("Synthetic inspector failure.");
    }
}
