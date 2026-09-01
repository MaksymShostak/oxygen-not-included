#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Executes only inspectors named by the compile-time catalog after exact
    /// static-ID and same-entry assembly matching has succeeded.
    /// </summary>
    internal static class DeclaredExternalModIntegrationPreparation
    {
        private const string AdditiveOutcomeConflictDiagnosticCode =
            "additive-integration-outcome-conflict";
        private const string AdditiveOutcomeConflictDiagnosticMessage =
            "The matched additive interoperability integration reported " +
            "facts inconsistent with another declared inspection category.";

        internal static DeclaredIntegrationPreparationResult Prepare(
            DeclaredModIntegrationCatalog catalog,
            LoadedModInspectionContext context,
            IReadOnlyList<IRuntimeAuthorityIntegrationInspector>
                runtimeInspectors,
            IReadOnlyList<IAdditiveInteroperabilityInspector> additiveInspectors)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            IReadOnlyDictionary<
                DeclaredModIntegrationId,
                IRuntimeAuthorityIntegrationInspector> runtimeById =
                    IndexRuntimeInspectors(runtimeInspectors);
            IReadOnlyDictionary<
                DeclaredModIntegrationId,
                IAdditiveInteroperabilityInspector> additiveById =
                    IndexAdditiveInspectors(additiveInspectors);

            var contributions =
                new List<PreparedRuntimeAuthorityContribution>();
            var outcomes = new List<ExternalModIntegrationOutcome>();
            for (int descriptorIndex = 0;
                 descriptorIndex < catalog.Descriptors.Count;
                 descriptorIndex++)
            {
                DeclaredModIntegrationDescriptor descriptor =
                    catalog.Descriptors[descriptorIndex];
                DeclaredLoadedModMatch match = context.Match(descriptor);
                if (!match.IsInspectable)
                {
                    ExternalModIntegrationOutcome identityOutcome =
                        CreateIdentityOutcome(descriptor, match);
                    AppendUnavailableRuntimeAuthorityContributions(
                        identityOutcome,
                        contributions);
                    outcomes.Add(identityOutcome);
                    continue;
                }

                var categoryOutcomes = new List<
                    ExternalModIntegrationOutcome>(descriptor.Categories.Count);
                int? additiveOutcomeIndex = null;
                if (HasCategory(
                        descriptor,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority))
                {
                    InspectRuntimeAuthorityCategory(
                        descriptor,
                        context,
                        runtimeById,
                        contributions,
                        categoryOutcomes);
                }

                if (HasCategory(
                        descriptor,
                        ExternalModIntegrationCategory
                            .AdditiveInteroperability))
                {
                    additiveOutcomeIndex = categoryOutcomes.Count;
                    InspectAdditiveInteroperabilityCategory(
                        descriptor,
                        context,
                        additiveById,
                        categoryOutcomes);
                }

                outcomes.Add(MergeCategoryOutcomesWithAdditiveContainment(
                    descriptor,
                    categoryOutcomes,
                    additiveOutcomeIndex));
            }

            return new DeclaredIntegrationPreparationResult(
                contributions,
                outcomes);
        }

        private static IReadOnlyDictionary<
            DeclaredModIntegrationId,
            IRuntimeAuthorityIntegrationInspector> IndexRuntimeInspectors(
                IReadOnlyList<IRuntimeAuthorityIntegrationInspector> inspectors)
        {
            if (inspectors == null)
            {
                throw new ArgumentNullException(nameof(inspectors));
            }

            var indexed = new Dictionary<
                DeclaredModIntegrationId,
                IRuntimeAuthorityIntegrationInspector>();
            for (int index = 0; index < inspectors.Count; index++)
            {
                IRuntimeAuthorityIntegrationInspector inspector = inspectors[index];
                if (inspector == null)
                {
                    throw new ArgumentException(
                        "A runtime-authority inspector cannot be null.",
                        nameof(inspectors));
                }

                if (indexed.ContainsKey(inspector.IntegrationId))
                {
                    throw new ArgumentException(
                        "Only one runtime-authority inspector may serve a " +
                        "declared integration.",
                        nameof(inspectors));
                }

                indexed.Add(inspector.IntegrationId, inspector);
            }

            return indexed;
        }

        private static IReadOnlyDictionary<
            DeclaredModIntegrationId,
            IAdditiveInteroperabilityInspector> IndexAdditiveInspectors(
                IReadOnlyList<IAdditiveInteroperabilityInspector> inspectors)
        {
            if (inspectors == null)
            {
                throw new ArgumentNullException(nameof(inspectors));
            }

            var indexed = new Dictionary<
                DeclaredModIntegrationId,
                IAdditiveInteroperabilityInspector>();
            for (int index = 0; index < inspectors.Count; index++)
            {
                IAdditiveInteroperabilityInspector inspector = inspectors[index];
                if (inspector == null)
                {
                    throw new ArgumentException(
                        "An additive interoperability inspector cannot be null.",
                        nameof(inspectors));
                }

                if (indexed.ContainsKey(inspector.IntegrationId))
                {
                    throw new ArgumentException(
                        "Only one additive inspector may serve a declared " +
                        "integration.",
                        nameof(inspectors));
                }

                indexed.Add(inspector.IntegrationId, inspector);
            }

            return indexed;
        }

        private static void InspectRuntimeAuthorityCategory(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context,
            IReadOnlyDictionary<
                DeclaredModIntegrationId,
                IRuntimeAuthorityIntegrationInspector> inspectorsById,
            ICollection<PreparedRuntimeAuthorityContribution> contributions,
            ICollection<ExternalModIntegrationOutcome> categoryOutcomes)
        {
            IRuntimeAuthorityIntegrationInspector? inspector;
            if (!inspectorsById.TryGetValue(
                    descriptor.IntegrationId,
                    out inspector))
            {
                ExternalModIntegrationOutcome unavailableOutcome =
                    CreateCategoryUnavailableOutcome(
                        descriptor,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority,
                        "runtime-integration-inspector-missing",
                        "No runtime-authority inspector was registered for the " +
                        "matched declared integration.");
                AppendUnavailableRuntimeAuthorityContributions(
                    unavailableOutcome,
                    contributions);
                categoryOutcomes.Add(unavailableOutcome);
                return;
            }

            try
            {
                PreparedRuntimeAuthorityInspection inspection =
                    inspector.Inspect(descriptor, context);
                ValidateRuntimeInspection(descriptor, inspection);
                for (int index = 0;
                     index < inspection.Contributions.Count;
                     index++)
                {
                    contributions.Add(inspection.Contributions[index]);
                }

                categoryOutcomes.Add(inspection.Outcome);
            }
            catch (Exception)
            {
                ExternalModIntegrationOutcome unavailableOutcome =
                    CreateCategoryUnavailableOutcome(
                        descriptor,
                        ExternalModIntegrationCategory
                            .ExclusiveRuntimeAuthority,
                        "runtime-integration-inspection-unavailable",
                        "The matched runtime-authority integration could not be " +
                        "inspected safely.");
                AppendUnavailableRuntimeAuthorityContributions(
                    unavailableOutcome,
                    contributions);
                categoryOutcomes.Add(unavailableOutcome);
            }
        }

        private static void InspectAdditiveInteroperabilityCategory(
            DeclaredModIntegrationDescriptor descriptor,
            LoadedModInspectionContext context,
            IReadOnlyDictionary<
                DeclaredModIntegrationId,
                IAdditiveInteroperabilityInspector> inspectorsById,
            ICollection<ExternalModIntegrationOutcome> categoryOutcomes)
        {
            IAdditiveInteroperabilityInspector? inspector;
            if (!inspectorsById.TryGetValue(
                    descriptor.IntegrationId,
                    out inspector))
            {
                categoryOutcomes.Add(CreateCategoryUnavailableOutcome(
                    descriptor,
                    ExternalModIntegrationCategory.AdditiveInteroperability,
                    "additive-integration-inspector-missing",
                    "No additive interoperability inspector was registered for " +
                    "the matched declared integration."));
                return;
            }

            try
            {
                ExternalModIntegrationOutcome outcome =
                    inspector.Inspect(descriptor, context);
                ValidateCategoryOutcome(
                    descriptor,
                    ExternalModIntegrationCategory.AdditiveInteroperability,
                    outcome);
                categoryOutcomes.Add(outcome);
            }
            catch (Exception)
            {
                categoryOutcomes.Add(CreateCategoryUnavailableOutcome(
                    descriptor,
                    ExternalModIntegrationCategory.AdditiveInteroperability,
                    "additive-integration-inspection-unavailable",
                    "The matched additive interoperability integration could " +
                    "not be inspected safely."));
            }
        }

        private static bool HasCategory(
            DeclaredModIntegrationDescriptor descriptor,
            ExternalModIntegrationCategory requiredCategory)
        {
            for (int index = 0; index < descriptor.Categories.Count; index++)
            {
                if (descriptor.Categories[index] == requiredCategory)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateRuntimeInspection(
            DeclaredModIntegrationDescriptor descriptor,
            PreparedRuntimeAuthorityInspection inspection)
        {
            if (inspection == null)
            {
                throw new ArgumentException(
                    "A runtime-authority inspector cannot return null.",
                    nameof(inspection));
            }

            IReadOnlyList<RuntimeCapabilityId> declaredRuntimeCapabilities =
                descriptor.GetDeclaredCapabilityIds(
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority);
            ValidateCategoryOutcome(
                descriptor,
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority,
                inspection.Outcome);

            for (int index = 0; index < inspection.Contributions.Count; index++)
            {
                if (!ContainsCapability(
                        declaredRuntimeCapabilities,
                        inspection.Contributions[index].CapabilityId))
                {
                    throw new ArgumentException(
                        "A runtime inspector contributed a capability its " +
                        "declaration does not name.",
                        nameof(inspection));
                }
            }
        }

        private static void ValidateCategoryOutcome(
            DeclaredModIntegrationDescriptor descriptor,
            ExternalModIntegrationCategory category,
            ExternalModIntegrationOutcome outcome)
        {
            if (outcome == null)
            {
                throw new ArgumentException(
                    "An integration inspector cannot return a null outcome.",
                    nameof(outcome));
            }

            if (!outcome.IntegrationId.Equals(descriptor.IntegrationId))
            {
                throw new ArgumentException(
                    "An inspector outcome must identify its declared integration.",
                        nameof(outcome));
            }

            if (!string.Equals(
                    outcome.DisplayName,
                    descriptor.DisplayName,
                    StringComparison.Ordinal) ||
                !CategorySequencesEqual(
                    outcome.Categories,
                    descriptor.Categories))
            {
                throw new ArgumentException(
                    "An inspector outcome must preserve its declaration's " +
                    "display identity and ordered categories.",
                    nameof(outcome));
            }

            if (outcome.MatchState != DeclaredModMatchState.Matched &&
                outcome.MatchState !=
                    DeclaredModMatchState.InspectionUnavailable)
            {
                throw new ArgumentException(
                    "A category inspector can report only a matched or " +
                    "inspection-unavailable identity.",
                    nameof(outcome));
            }

            IReadOnlyList<RuntimeCapabilityId> expectedCapabilityIds =
                descriptor.GetDeclaredCapabilityIds(category);
            if (outcome.Capabilities.Count != expectedCapabilityIds.Count)
            {
                throw new ArgumentException(
                    "A category inspector must report every capability assigned " +
                    "to its inspection boundary exactly once.",
                    nameof(outcome));
            }

            for (int index = 0; index < expectedCapabilityIds.Count; index++)
            {
                ExternalModIntegrationCapabilityOutcome capabilityOutcome =
                    outcome.Capabilities[index];
                if (!capabilityOutcome.CapabilityId.Equals(
                        expectedCapabilityIds[index]))
                {
                    throw new ArgumentException(
                        "A category inspector must preserve the declaration " +
                        "order of its assigned capabilities.",
                        nameof(outcome));
                }

                if (capabilityOutcome.Category != category)
                {
                    throw new ArgumentException(
                        "A category inspector must preserve each capability's " +
                        "declared inspection category.",
                        nameof(outcome));
                }

                if (category ==
                    ExternalModIntegrationCategory.ExclusiveRuntimeAuthority)
                {
                    ValidateExclusiveRuntimeAuthorityCapabilityState(
                        outcome.MatchState,
                        capabilityOutcome);
                }
                else
                {
                    ValidateAdditiveInteroperabilityCapabilityState(
                        outcome.MatchState,
                        capabilityOutcome);
                }
            }

            ValidateInspectorDoesNotUsePreparationOwnedDiagnosticCode(outcome);
            ValidateDiagnosticCodeMessageConsistency(outcome);
        }

        private static void ValidateExclusiveRuntimeAuthorityCapabilityState(
            DeclaredModMatchState matchState,
            ExternalModIntegrationCapabilityOutcome capabilityOutcome)
        {
            if (matchState == DeclaredModMatchState.InspectionUnavailable)
            {
                if (capabilityOutcome.AuthorityObservation ==
                        RuntimeAuthorityObservation.OwnershipUnavailable &&
                    capabilityOutcome.ContractState ==
                        IntegrationContractState.VerificationUnavailable &&
                    capabilityOutcome.Disposition ==
                        IntegrationCapabilityDisposition.Unavailable)
                {
                    return;
                }

                throw new ArgumentException(
                    "An inspection-unavailable runtime-authority outcome must " +
                    "report unavailable ownership, contract verification, and " +
                    "capability disposition.",
                    nameof(capabilityOutcome));
            }

            IntegrationContractState requiredContractState;
            IntegrationCapabilityDisposition requiredDisposition;
            switch (capabilityOutcome.AuthorityObservation)
            {
                case RuntimeAuthorityObservation.DoesNotOwn:
                    requiredContractState =
                        IntegrationContractState.NotEvaluated;
                    requiredDisposition =
                        IntegrationCapabilityDisposition.NotApplicable;
                    break;
                case RuntimeAuthorityObservation.OwnsCompatible:
                    requiredContractState =
                        IntegrationContractState.Compatible;
                    requiredDisposition =
                        IntegrationCapabilityDisposition.Ready;
                    break;
                case RuntimeAuthorityObservation.OwnsIncompatible:
                    requiredContractState =
                        IntegrationContractState.Incompatible;
                    requiredDisposition =
                        IntegrationCapabilityDisposition.Unavailable;
                    break;
                case RuntimeAuthorityObservation.OwnershipUnavailable:
                    requiredContractState =
                        IntegrationContractState.VerificationUnavailable;
                    requiredDisposition =
                        IntegrationCapabilityDisposition.Unavailable;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(capabilityOutcome),
                        capabilityOutcome.AuthorityObservation,
                        "Unknown runtime-authority observation.");
            }

            if (capabilityOutcome.ContractState != requiredContractState ||
                capabilityOutcome.Disposition != requiredDisposition)
            {
                throw new ArgumentException(
                    "A matched runtime-authority capability must report the " +
                    "contract state and disposition required by its exact " +
                    "authority observation.",
                    nameof(capabilityOutcome));
            }
        }

        private static void ValidateAdditiveInteroperabilityCapabilityState(
            DeclaredModMatchState matchState,
            ExternalModIntegrationCapabilityOutcome capabilityOutcome)
        {
            if (capabilityOutcome.AuthorityObservation !=
                RuntimeAuthorityObservation.DoesNotOwn)
            {
                throw new ArgumentException(
                    "An additive interoperability inspector cannot claim " +
                    "exclusive runtime authority.",
                    nameof(capabilityOutcome));
            }

            bool unavailableInspection =
                matchState == DeclaredModMatchState.InspectionUnavailable &&
                capabilityOutcome.ContractState ==
                    IntegrationContractState.VerificationUnavailable &&
                capabilityOutcome.Disposition ==
                    IntegrationCapabilityDisposition.Unavailable;
            bool readyMatchedProtocol =
                matchState == DeclaredModMatchState.Matched &&
                capabilityOutcome.ContractState ==
                    IntegrationContractState.Compatible &&
                capabilityOutcome.Disposition ==
                    IntegrationCapabilityDisposition.Ready;
            bool unavailableMatchedProtocol =
                matchState == DeclaredModMatchState.Matched &&
                (capabilityOutcome.ContractState ==
                    IntegrationContractState.Incompatible ||
                 capabilityOutcome.ContractState ==
                    IntegrationContractState.VerificationUnavailable) &&
                capabilityOutcome.Disposition ==
                    IntegrationCapabilityDisposition.Unavailable;
            if (!unavailableInspection &&
                !readyMatchedProtocol &&
                !unavailableMatchedProtocol)
            {
                throw new ArgumentException(
                    "An additive interoperability capability must report an " +
                    "exact ready or unavailable contract state and can never " +
                    "be selected as runtime authority.",
                    nameof(capabilityOutcome));
            }
        }

        private static void
            ValidateInspectorDoesNotUsePreparationOwnedDiagnosticCode(
                ExternalModIntegrationOutcome outcome)
        {
            for (int index = 0; index < outcome.Capabilities.Count; index++)
            {
                if (string.Equals(
                        outcome.Capabilities[index].DiagnosticCode,
                        AdditiveOutcomeConflictDiagnosticCode,
                        StringComparison.Ordinal))
                {
                    throw PreparationOwnedDiagnosticCodeException();
                }
            }

            for (int index = 0; index < outcome.Diagnostics.Count; index++)
            {
                if (string.Equals(
                        outcome.Diagnostics[index].Code,
                        AdditiveOutcomeConflictDiagnosticCode,
                        StringComparison.Ordinal))
                {
                    throw PreparationOwnedDiagnosticCodeException();
                }
            }
        }

        private static ArgumentException
            PreparationOwnedDiagnosticCodeException() =>
            new ArgumentException(
                "An integration inspector cannot emit the preparation-owned " +
                "additive outcome-conflict diagnostic code.");

        private static void ValidateDiagnosticCodeMessageConsistency(
            ExternalModIntegrationOutcome outcome)
        {
            var diagnosticMessagesByCode = new Dictionary<string, string>(
                StringComparer.Ordinal);
            for (int index = 0; index < outcome.Capabilities.Count; index++)
            {
                ExternalModIntegrationCapabilityOutcome capabilityOutcome =
                    outcome.Capabilities[index];
                if (capabilityOutcome.DiagnosticCode != null)
                {
                    RegisterDiagnosticFact(
                        diagnosticMessagesByCode,
                        capabilityOutcome.DiagnosticCode,
                        capabilityOutcome.DiagnosticMessage!);
                }
            }

            for (int index = 0; index < outcome.Diagnostics.Count; index++)
            {
                ExternalModIntegrationDiagnostic diagnostic =
                    outcome.Diagnostics[index];
                RegisterDiagnosticFact(
                    diagnosticMessagesByCode,
                    diagnostic.Code,
                    diagnostic.Message);
            }
        }

        private static void AppendUnavailableRuntimeAuthorityContributions(
            ExternalModIntegrationOutcome outcome,
            ICollection<PreparedRuntimeAuthorityContribution> contributions)
        {
            for (int index = 0; index < outcome.Capabilities.Count; index++)
            {
                ExternalModIntegrationCapabilityOutcome capability =
                    outcome.Capabilities[index];
                if (capability.AuthorityObservation ==
                    RuntimeAuthorityObservation.DoesNotOwn)
                {
                    continue;
                }

                if (capability.AuthorityObservation !=
                    RuntimeAuthorityObservation.OwnershipUnavailable)
                {
                    throw new InvalidOperationException(
                        "An unavailable runtime-authority result must report " +
                        "ownership as unavailable for every capability.");
                }

                string diagnosticCode = capability.DiagnosticCode ??
                    throw new InvalidOperationException(
                        "An unavailable runtime-authority result requires a " +
                        "stable capability diagnostic code.");
                string diagnosticMessage = capability.DiagnosticMessage ??
                    throw new InvalidOperationException(
                        "An unavailable runtime-authority result requires a " +
                        "bounded capability diagnostic message.");
                contributions.Add(new PreparedRuntimeAuthorityContribution(
                    RuntimeAuthorityImplementationIdentity
                        .ForDeclaredExternalIntegration(
                            outcome.IntegrationId),
                    capability.CapabilityId,
                    Array.Empty<RuntimePatchGroupId>(),
                    RuntimeAuthorityObservation.OwnershipUnavailable,
                    Array.Empty<HarmonyPatchContractBinding>(),
                    Array.Empty<RuntimeAuthorityRequirement>(),
                    diagnosticCode,
                    diagnosticMessage));
            }
        }

        private static bool CategorySequencesEqual(
            IReadOnlyList<ExternalModIntegrationCategory> left,
            IReadOnlyList<ExternalModIntegrationCategory> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsCapability(
            IReadOnlyList<RuntimeCapabilityId> capabilityIds,
            RuntimeCapabilityId candidate)
        {
            for (int index = 0; index < capabilityIds.Count; index++)
            {
                if (capabilityIds[index].Equals(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static ExternalModIntegrationOutcome CreateIdentityOutcome(
            DeclaredModIntegrationDescriptor descriptor,
            DeclaredLoadedModMatch match)
        {
            bool identityNotMatched =
                match.MatchState == DeclaredModMatchState.NotMatched;
            string? diagnosticCode = identityNotMatched
                ? null
                : match.DiagnosticCode ??
                    "declared-integration-identity-inspection-unavailable";
            string? diagnosticMessage = identityNotMatched
                ? null
                : match.DiagnosticMessage ??
                    "The declared integration identity could not be inspected.";
            var capabilityOutcomes = new List<
                ExternalModIntegrationCapabilityOutcome>(
                    descriptor.DeclaredCapabilities.Count);
            for (int index = 0;
                 index < descriptor.DeclaredCapabilities.Count;
                 index++)
            {
                DeclaredModIntegrationCapability declaration =
                    descriptor.DeclaredCapabilities[index];
                capabilityOutcomes.Add(
                    new ExternalModIntegrationCapabilityOutcome(
                        declaration.CapabilityId,
                        declaration.Category,
                        identityNotMatched
                            ? RuntimeAuthorityObservation.DoesNotOwn
                            : declaration.Category ==
                                ExternalModIntegrationCategory
                                    .ExclusiveRuntimeAuthority
                                ? RuntimeAuthorityObservation
                                    .OwnershipUnavailable
                                : RuntimeAuthorityObservation.DoesNotOwn,
                        identityNotMatched
                            ? IntegrationContractState.NotEvaluated
                            : IntegrationContractState
                                .VerificationUnavailable,
                        identityNotMatched
                            ? IntegrationCapabilityDisposition.NotApplicable
                            : IntegrationCapabilityDisposition.Unavailable,
                        diagnosticCode,
                        diagnosticMessage));
            }

            IReadOnlyList<ExternalModIntegrationDiagnostic> diagnostics =
                diagnosticCode == null
                    ? (IReadOnlyList<ExternalModIntegrationDiagnostic>)
                        Array.Empty<ExternalModIntegrationDiagnostic>()
                    : new[]
                    {
                        new ExternalModIntegrationDiagnostic(
                            diagnosticCode,
                            diagnosticMessage!)
                    };

            return new ExternalModIntegrationOutcome(
                descriptor.IntegrationId,
                descriptor.DisplayName,
                descriptor.Categories,
                match.MatchState,
                null,
                null,
                null,
                null,
                capabilityOutcomes,
                diagnostics);
        }

        private static ExternalModIntegrationOutcome
            CreateCategoryUnavailableOutcome(
                DeclaredModIntegrationDescriptor descriptor,
                ExternalModIntegrationCategory category,
                string diagnosticCode,
                string diagnosticMessage)
        {
            bool runtimeAuthority = category ==
                ExternalModIntegrationCategory.ExclusiveRuntimeAuthority;
            return CreateUniformCategoryOutcome(
                descriptor,
                category,
                DeclaredModMatchState.InspectionUnavailable,
                runtimeAuthority
                    ? RuntimeAuthorityObservation.OwnershipUnavailable
                    : RuntimeAuthorityObservation.DoesNotOwn,
                IntegrationContractState.VerificationUnavailable,
                IntegrationCapabilityDisposition.Unavailable,
                diagnosticCode,
                diagnosticMessage);
        }

        private static ExternalModIntegrationOutcome MergeCategoryOutcomes(
            DeclaredModIntegrationDescriptor descriptor,
            IReadOnlyList<ExternalModIntegrationOutcome> categoryOutcomes)
        {
            if (categoryOutcomes.Count != descriptor.Categories.Count)
            {
                throw new InvalidOperationException(
                    "Declared integration preparation must produce exactly one " +
                    "outcome for every declared inspection category.");
            }

            var capabilitiesById = new Dictionary<
                RuntimeCapabilityId,
                ExternalModIntegrationCapabilityOutcome>();
            var diagnostics = new List<ExternalModIntegrationDiagnostic>();
            var diagnosticMessagesByCode = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var retainedTopLevelDiagnosticCodes = new HashSet<string>(
                StringComparer.Ordinal);
            DeclaredModMatchState mergedMatchState =
                DeclaredModMatchState.Matched;
            string? assemblyIdentity = null;
            string? assemblyVersion = null;
            string? fileVersion = null;
            string? assemblySha256 = null;

            for (int outcomeIndex = 0;
                 outcomeIndex < categoryOutcomes.Count;
                 outcomeIndex++)
            {
                ExternalModIntegrationOutcome outcome =
                    categoryOutcomes[outcomeIndex];
                if (!outcome.IntegrationId.Equals(descriptor.IntegrationId) ||
                    !string.Equals(
                        outcome.DisplayName,
                        descriptor.DisplayName,
                        StringComparison.Ordinal) ||
                    !CategorySequencesEqual(
                        outcome.Categories,
                        descriptor.Categories))
                {
                    throw new InvalidOperationException(
                        "Category outcomes cannot be merged across different " +
                        "declared integration identities.");
                }

                if (outcome.MatchState ==
                    DeclaredModMatchState.InspectionUnavailable)
                {
                    mergedMatchState =
                        DeclaredModMatchState.InspectionUnavailable;
                }
                else if (outcome.MatchState != DeclaredModMatchState.Matched)
                {
                    throw new InvalidOperationException(
                        "An inspectable integration category outcome must be " +
                        "matched or inspection-unavailable before merging.");
                }

                assemblyIdentity = MergeOptionalExactFact(
                    assemblyIdentity,
                    outcome.AssemblyIdentity,
                    "assembly identity");
                assemblyVersion = MergeOptionalExactFact(
                    assemblyVersion,
                    outcome.AssemblyVersion,
                    "assembly version");
                fileVersion = MergeOptionalExactFact(
                    fileVersion,
                    outcome.FileVersion,
                    "file version");
                assemblySha256 = MergeOptionalExactFact(
                    assemblySha256,
                    outcome.AssemblySha256,
                    "assembly SHA-256");

                for (int capabilityIndex = 0;
                     capabilityIndex < outcome.Capabilities.Count;
                     capabilityIndex++)
                {
                    ExternalModIntegrationCapabilityOutcome capability =
                        outcome.Capabilities[capabilityIndex];
                    if (capabilitiesById.ContainsKey(capability.CapabilityId))
                    {
                        throw new InvalidOperationException(
                            "Two category inspectors cannot report the same " +
                            "declared integration capability.");
                    }

                    capabilitiesById.Add(
                        capability.CapabilityId,
                        capability);
                    if (capability.DiagnosticCode != null)
                    {
                        RegisterDiagnosticFact(
                            diagnosticMessagesByCode,
                            capability.DiagnosticCode,
                            capability.DiagnosticMessage!);
                    }
                }

                for (int diagnosticIndex = 0;
                     diagnosticIndex < outcome.Diagnostics.Count;
                     diagnosticIndex++)
                {
                    ExternalModIntegrationDiagnostic diagnostic =
                        outcome.Diagnostics[diagnosticIndex];
                    RegisterDiagnosticFact(
                        diagnosticMessagesByCode,
                        diagnostic.Code,
                        diagnostic.Message);
                    if (retainedTopLevelDiagnosticCodes.Add(diagnostic.Code))
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
            }

            var orderedCapabilities = new List<
                ExternalModIntegrationCapabilityOutcome>(
                    descriptor.DeclaredCapabilityIds.Count);
            for (int index = 0;
                 index < descriptor.DeclaredCapabilityIds.Count;
                 index++)
            {
                RuntimeCapabilityId capabilityId =
                    descriptor.DeclaredCapabilityIds[index];
                ExternalModIntegrationCapabilityOutcome? capability;
                if (!capabilitiesById.TryGetValue(
                        capabilityId,
                        out capability))
                {
                    throw new InvalidOperationException(
                        "Merged category outcomes must report every declared " +
                        "integration capability.");
                }

                orderedCapabilities.Add(capability);
            }

            return new ExternalModIntegrationOutcome(
                descriptor.IntegrationId,
                descriptor.DisplayName,
                descriptor.Categories,
                mergedMatchState,
                assemblyIdentity,
                assemblyVersion,
                fileVersion,
                assemblySha256,
                orderedCapabilities,
                diagnostics);
        }

        private static ExternalModIntegrationOutcome
            MergeCategoryOutcomesWithAdditiveContainment(
                DeclaredModIntegrationDescriptor descriptor,
                List<ExternalModIntegrationOutcome> categoryOutcomes,
                int? additiveOutcomeIndex)
        {
            try
            {
                return MergeCategoryOutcomes(descriptor, categoryOutcomes);
            }
            catch (CategoryOutcomeConflictException)
                when (additiveOutcomeIndex.HasValue)
            {
                categoryOutcomes[additiveOutcomeIndex.Value] =
                    CreateCategoryUnavailableOutcome(
                        descriptor,
                        ExternalModIntegrationCategory
                            .AdditiveInteroperability,
                        AdditiveOutcomeConflictDiagnosticCode,
                        AdditiveOutcomeConflictDiagnosticMessage);
                return MergeCategoryOutcomes(descriptor, categoryOutcomes);
            }
        }

        private static string? MergeOptionalExactFact(
            string? accumulated,
            string? candidate,
            string semanticName)
        {
            if (candidate == null)
            {
                return accumulated;
            }

            if (accumulated == null)
            {
                return candidate;
            }

            if (!string.Equals(
                    accumulated,
                    candidate,
                    StringComparison.Ordinal))
            {
                throw new CategoryOutcomeConflictException(
                    "Category outcomes reported conflicting " +
                    semanticName +
                    " facts.");
            }

            return accumulated;
        }

        private static void RegisterDiagnosticFact(
            IDictionary<string, string> diagnosticMessagesByCode,
            string diagnosticCode,
            string diagnosticMessage)
        {
            string? existingMessage;
            if (diagnosticMessagesByCode.TryGetValue(
                    diagnosticCode,
                    out existingMessage))
            {
                if (!string.Equals(
                        existingMessage,
                        diagnosticMessage,
                        StringComparison.Ordinal))
                {
                    throw new CategoryOutcomeConflictException(
                        "Category outcomes cannot reuse one diagnostic code " +
                        "for different messages.");
                }

                return;
            }

            diagnosticMessagesByCode.Add(
                diagnosticCode,
                diagnosticMessage);
        }

        private sealed class CategoryOutcomeConflictException :
            InvalidOperationException
        {
            internal CategoryOutcomeConflictException(string message)
                : base(message)
            {
            }
        }

        private static ExternalModIntegrationOutcome
            CreateUniformCategoryOutcome(
            DeclaredModIntegrationDescriptor descriptor,
            ExternalModIntegrationCategory category,
            DeclaredModMatchState matchState,
            RuntimeAuthorityObservation authorityObservation,
            IntegrationContractState contractState,
            IntegrationCapabilityDisposition disposition,
            string? diagnosticCode,
            string? diagnosticMessage)
        {
            IReadOnlyList<RuntimeCapabilityId> capabilityIds =
                descriptor.GetDeclaredCapabilityIds(category);
            var capabilities = new List<
                ExternalModIntegrationCapabilityOutcome>(
                    capabilityIds.Count);
            for (int index = 0; index < capabilityIds.Count; index++)
            {
                capabilities.Add(
                    new ExternalModIntegrationCapabilityOutcome(
                        capabilityIds[index],
                        category,
                        authorityObservation,
                        contractState,
                        disposition,
                        diagnosticCode,
                        diagnosticMessage));
            }

            IReadOnlyList<ExternalModIntegrationDiagnostic> diagnostics =
                diagnosticCode == null
                    ? (IReadOnlyList<ExternalModIntegrationDiagnostic>)
                        Array.Empty<ExternalModIntegrationDiagnostic>()
                    : new[]
                    {
                        new ExternalModIntegrationDiagnostic(
                            diagnosticCode,
                            diagnosticMessage!)
                    };

            return new ExternalModIntegrationOutcome(
                descriptor.IntegrationId,
                descriptor.DisplayName,
                descriptor.Categories,
                matchState,
                null,
                null,
                null,
                null,
                capabilities,
                diagnostics);
        }
    }
}
