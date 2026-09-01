#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    internal enum RuntimeAuthorityRequirementKind
    {
        KleiOriginal,
        ExactOwnedReplacement
    }

    /// <summary>
    /// Captures the exact prefix-owner evidence a selected runtime capability
    /// requires before any Temperature Limit registration may begin.
    /// </summary>
    internal sealed class RuntimeAuthorityRequirement
    {
        internal RuntimeAuthorityRequirement(
            MethodBase targetMethod,
            RuntimeAuthorityRequirementKind kind,
            string? requiredHarmonyOwner,
            MethodInfo? requiredPrefixMethod,
            IEnumerable<string> permittedSkippingPrefixOwners)
        {
            TargetMethod = targetMethod ??
                throw new ArgumentNullException(nameof(targetMethod));
            if (!Enum.IsDefined(typeof(RuntimeAuthorityRequirementKind), kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown runtime-authority requirement kind.");
            }

            Kind = kind;
            RequiredHarmonyOwner = requiredHarmonyOwner;
            RequiredPrefixMethod = requiredPrefixMethod;
            PermittedSkippingPrefixOwners = CopyDistinctOwners(
                permittedSkippingPrefixOwners);
            ValidateReplacementEvidence();
        }

        internal MethodBase TargetMethod { get; }

        internal RuntimeAuthorityRequirementKind Kind { get; }

        internal string? RequiredHarmonyOwner { get; }

        internal MethodInfo? RequiredPrefixMethod { get; }

        internal IReadOnlyList<string> PermittedSkippingPrefixOwners { get; }

        private static IReadOnlyList<string> CopyDistinctOwners(
            IEnumerable<string> owners)
        {
            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            var copiedOwners = new List<string>();
            var seenOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (string owner in owners)
            {
                string exactOwner = ExternalModIntegrationModelValidation
                    .RequireExactBoundedText(
                        owner,
                        nameof(owners),
                        256,
                        "A permitted Harmony owner");
                if (seenOwners.Add(exactOwner))
                {
                    copiedOwners.Add(exactOwner);
                }
            }

            return new ReadOnlyCollection<string>(copiedOwners);
        }

        private void ValidateReplacementEvidence()
        {
            if (Kind == RuntimeAuthorityRequirementKind.KleiOriginal)
            {
                if (RequiredHarmonyOwner != null || RequiredPrefixMethod != null)
                {
                    throw new ArgumentException(
                        "A Klei-original requirement cannot claim an exact " +
                        "external replacement.");
                }

                return;
            }

            if (RequiredHarmonyOwner == null)
            {
                throw new ArgumentException(
                    "An exact replacement requirement needs its Harmony owner.",
                    nameof(RequiredHarmonyOwner));
            }

            string requiredOwner = ExternalModIntegrationModelValidation
                .RequireExactBoundedText(
                    RequiredHarmonyOwner,
                    nameof(RequiredHarmonyOwner),
                    256,
                    "An exact replacement Harmony owner");
            if (RequiredPrefixMethod == null)
            {
                throw new ArgumentException(
                    "An exact replacement requirement needs its verified " +
                    "prefix method.",
                    nameof(RequiredPrefixMethod));
            }

            bool ownerIsPermitted = false;
            for (int index = 0;
                 index < PermittedSkippingPrefixOwners.Count;
                 index++)
            {
                if (string.Equals(
                        PermittedSkippingPrefixOwners[index],
                        requiredOwner,
                        StringComparison.Ordinal))
                {
                    ownerIsPermitted = true;
                    break;
                }
            }

            if (!ownerIsPermitted)
            {
                throw new ArgumentException(
                    "The required replacement owner must also be permitted as " +
                    "a skipping-prefix owner.",
                    nameof(PermittedSkippingPrefixOwners));
            }
        }
    }
}
