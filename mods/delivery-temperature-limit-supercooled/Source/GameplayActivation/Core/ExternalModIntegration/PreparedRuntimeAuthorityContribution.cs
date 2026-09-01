#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Complete immutable preparation result for one implementation's claim
    /// over one runtime capability. Compatible claims already own every binding
    /// and exact authority requirement that later installation may consume.
    /// </summary>
    internal sealed class PreparedRuntimeAuthorityContribution
    {
        internal PreparedRuntimeAuthorityContribution(
            RuntimeAuthorityImplementationIdentity implementationIdentity,
            RuntimeCapabilityId capabilityId,
            IEnumerable<RuntimePatchGroupId> patchGroupIds,
            RuntimeAuthorityObservation authorityObservation,
            IEnumerable<HarmonyPatchContractBinding> patchBindings,
            IEnumerable<RuntimeAuthorityRequirement> authorityRequirements,
            string? diagnosticCode,
            string? diagnosticMessage)
        {
            implementationIdentity.Validate(nameof(implementationIdentity));
            ExternalModIntegrationModelValidation.RequireCapabilityId(
                capabilityId,
                nameof(capabilityId));
            if (!Enum.IsDefined(
                    typeof(RuntimeAuthorityObservation),
                    authorityObservation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authorityObservation),
                    authorityObservation,
                    "Unknown runtime-authority observation.");
            }

            ImplementationIdentity = implementationIdentity;
            CapabilityId = capabilityId;
            PatchGroupIds = CopyPatchGroupIds(patchGroupIds);
            AuthorityObservation = authorityObservation;
            PatchBindings = CopyPatchBindings(patchBindings);
            AuthorityRequirements = CopyAuthorityRequirements(
                authorityRequirements);
            ExternalModIntegrationModelValidation.ValidateOptionalDiagnostic(
                diagnosticCode,
                diagnosticMessage,
                nameof(diagnosticCode),
                nameof(diagnosticMessage));
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;

            ValidateAuthorityClaim();
        }

        internal RuntimeAuthorityImplementationIdentity
            ImplementationIdentity { get; }

        internal RuntimeCapabilityId CapabilityId { get; }

        internal IReadOnlyList<RuntimePatchGroupId> PatchGroupIds { get; }

        internal RuntimeAuthorityObservation AuthorityObservation { get; }

        internal IReadOnlyList<HarmonyPatchContractBinding> PatchBindings { get; }

        internal IReadOnlyList<RuntimeAuthorityRequirement>
            AuthorityRequirements { get; }

        internal string? DiagnosticCode { get; }

        internal string? DiagnosticMessage { get; }

        private static IReadOnlyList<RuntimePatchGroupId> CopyPatchGroupIds(
            IEnumerable<RuntimePatchGroupId> patchGroupIds)
        {
            if (patchGroupIds == null)
            {
                throw new ArgumentNullException(nameof(patchGroupIds));
            }

            var copied = new List<RuntimePatchGroupId>();
            var seen = new HashSet<RuntimePatchGroupId>();
            foreach (RuntimePatchGroupId patchGroupId in patchGroupIds)
            {
                if (string.IsNullOrEmpty(patchGroupId.Value))
                {
                    throw new ArgumentException(
                        "A runtime patch group must have a valid identity.",
                        nameof(patchGroupIds));
                }

                if (!seen.Add(patchGroupId))
                {
                    throw new ArgumentException(
                        "A contribution cannot repeat a runtime patch group.",
                        nameof(patchGroupIds));
                }

                copied.Add(patchGroupId);
            }

            return new ReadOnlyCollection<RuntimePatchGroupId>(copied);
        }

        private static IReadOnlyList<HarmonyPatchContractBinding>
            CopyPatchBindings(
                IEnumerable<HarmonyPatchContractBinding> patchBindings)
        {
            if (patchBindings == null)
            {
                throw new ArgumentNullException(nameof(patchBindings));
            }

            var copied = new List<HarmonyPatchContractBinding>();
            var seen = new HashSet<(
                MethodBase TargetMethod,
                MethodInfo PatchMethod,
                HarmonyPatchContractKind PatchKind)>();
            foreach (HarmonyPatchContractBinding binding in patchBindings)
            {
                if (binding == null)
                {
                    throw new ArgumentException(
                        "A prepared patch binding cannot be null.",
                        nameof(patchBindings));
                }

                var identity = (
                    binding.TargetMethod,
                    binding.PatchMethod,
                    binding.PatchKind);
                if (!seen.Add(identity))
                {
                    throw new ArgumentException(
                        "A contribution cannot repeat an exact patch identity.",
                        nameof(patchBindings));
                }

                copied.Add(binding);
            }

            return new ReadOnlyCollection<HarmonyPatchContractBinding>(copied);
        }

        private static IReadOnlyList<RuntimeAuthorityRequirement>
            CopyAuthorityRequirements(
                IEnumerable<RuntimeAuthorityRequirement> requirements)
        {
            if (requirements == null)
            {
                throw new ArgumentNullException(nameof(requirements));
            }

            var copied = new List<RuntimeAuthorityRequirement>();
            var seenTargets = new HashSet<MethodBase>();
            foreach (RuntimeAuthorityRequirement requirement in requirements)
            {
                if (requirement == null)
                {
                    throw new ArgumentException(
                        "A runtime-authority requirement cannot be null.",
                        nameof(requirements));
                }

                if (!seenTargets.Add(requirement.TargetMethod))
                {
                    throw new ArgumentException(
                        "A contribution cannot repeat authority evidence for " +
                        "one target method.",
                        nameof(requirements));
                }

                copied.Add(requirement);
            }

            return new ReadOnlyCollection<RuntimeAuthorityRequirement>(copied);
        }

        private void ValidateAuthorityClaim()
        {
            bool hasPreparedEvidence =
                PatchGroupIds.Count != 0 ||
                PatchBindings.Count != 0 ||
                AuthorityRequirements.Count != 0;

            if (AuthorityObservation == RuntimeAuthorityObservation.OwnsCompatible)
            {
                if (PatchGroupIds.Count == 0 ||
                    PatchBindings.Count == 0 ||
                    AuthorityRequirements.Count == 0)
                {
                    throw new ArgumentException(
                        "A compatible runtime-authority claim requires complete " +
                        "patch-group, binding, and authority evidence.");
                }

                var requiredTargets = new HashSet<MethodBase>();
                for (int index = 0;
                     index < AuthorityRequirements.Count;
                     index++)
                {
                    requiredTargets.Add(
                        AuthorityRequirements[index].TargetMethod);
                }

                for (int index = 0; index < PatchBindings.Count; index++)
                {
                    if (!requiredTargets.Contains(
                            PatchBindings[index].TargetMethod))
                    {
                        throw new ArgumentException(
                            "Every compatible patch binding requires exact " +
                            "authority evidence for its target method.");
                    }
                }

                return;
            }

            if (hasPreparedEvidence)
            {
                throw new ArgumentException(
                    "Only a compatible runtime-authority claim may carry " +
                    "prepared patch evidence.");
            }

            if ((AuthorityObservation ==
                    RuntimeAuthorityObservation.OwnsIncompatible ||
                 AuthorityObservation ==
                    RuntimeAuthorityObservation.OwnershipUnavailable) &&
                DiagnosticCode == null)
            {
                throw new ArgumentException(
                    "An incompatible or unavailable authority claim requires a " +
                    "stable diagnostic code and bounded message.");
            }
        }
    }
}
