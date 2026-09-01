#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Declares which independent inspection boundary is authoritative for one
    /// integration capability. One capability cannot cross category boundaries.
    /// </summary>
    internal sealed class DeclaredModIntegrationCapability
    {
        internal DeclaredModIntegrationCapability(
            RuntimeCapabilityId capabilityId,
            ExternalModIntegrationCategory category)
        {
            ExternalModIntegrationModelValidation.RequireCapabilityId(
                capabilityId,
                nameof(capabilityId));
            CapabilityId = capabilityId;
            Category = ExternalModIntegrationModelValidation.RequireDefinedEnum(
                category,
                nameof(category));
        }

        internal RuntimeCapabilityId CapabilityId { get; }

        internal ExternalModIntegrationCategory Category { get; }
    }

    /// <summary>
    /// Compile-time declaration of one intentionally supported external-mod
    /// integration and the exact Klei/assembly identities that may satisfy it.
    /// </summary>
    internal sealed class DeclaredModIntegrationDescriptor
    {
        internal DeclaredModIntegrationDescriptor(
            DeclaredModIntegrationId integrationId,
            string displayName,
            IEnumerable<string> acceptedStaticIds,
            IEnumerable<string> acceptedAssemblySimpleNames,
            string upstreamEvidenceReference,
            IEnumerable<DeclaredModIntegrationCapability>
                declaredCapabilities)
        {
            ExternalModIntegrationModelValidation.RequireIntegrationId(
                integrationId,
                nameof(integrationId));
            IntegrationId = integrationId;
            DisplayName = ExternalModIntegrationModelValidation
                .RequireDisplayName(displayName, nameof(displayName));
            AcceptedStaticIds = CopyExactIdentities(
                acceptedStaticIds,
                nameof(acceptedStaticIds),
                false);
            AcceptedAssemblySimpleNames = CopyExactIdentities(
                acceptedAssemblySimpleNames,
                nameof(acceptedAssemblySimpleNames),
                true);
            UpstreamEvidenceReference = RequireUpstreamEvidenceReference(
                upstreamEvidenceReference);
            DeclaredCapabilities = CopyDeclaredCapabilities(
                declaredCapabilities);
            Categories = ProjectCategories(DeclaredCapabilities);
            DeclaredCapabilityIds = ProjectCapabilityIds(DeclaredCapabilities);
        }

        internal DeclaredModIntegrationId IntegrationId { get; }

        internal string DisplayName { get; }

        internal IReadOnlyList<ExternalModIntegrationCategory> Categories { get; }

        internal IReadOnlyList<string> AcceptedStaticIds { get; }

        internal IReadOnlyList<string> AcceptedAssemblySimpleNames { get; }

        internal string UpstreamEvidenceReference { get; }

        internal IReadOnlyList<DeclaredModIntegrationCapability>
            DeclaredCapabilities { get; }

        internal IReadOnlyList<RuntimeCapabilityId> DeclaredCapabilityIds { get; }

        internal IReadOnlyList<RuntimeCapabilityId> GetDeclaredCapabilityIds(
            ExternalModIntegrationCategory category)
        {
            ExternalModIntegrationModelValidation.RequireDefinedEnum(
                category,
                nameof(category));
            var capabilityIds = new List<RuntimeCapabilityId>();
            for (int index = 0; index < DeclaredCapabilities.Count; index++)
            {
                DeclaredModIntegrationCapability declaration =
                    DeclaredCapabilities[index];
                if (declaration.Category == category)
                {
                    capabilityIds.Add(declaration.CapabilityId);
                }
            }

            return new ReadOnlyCollection<RuntimeCapabilityId>(capabilityIds);
        }

        private static IReadOnlyList<string> CopyExactIdentities(
            IEnumerable<string> identities,
            string parameterName,
            bool requireAssemblySimpleName)
        {
            if (identities == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copied = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string identity in identities)
            {
                if (identity == null)
                {
                    throw new ArgumentException(
                        "A declaration cannot contain a null accepted identity.",
                        parameterName);
                }

                string exactIdentity = ExternalModIntegrationModelValidation
                    .RequireExactBoundedText(
                        identity,
                        parameterName,
                        256,
                        requireAssemblySimpleName
                            ? "An accepted assembly simple name"
                            : "An accepted Klei static ID");
                if (requireAssemblySimpleName &&
                    (exactIdentity.IndexOf(',') >= 0 ||
                     exactIdentity.IndexOf('/') >= 0 ||
                     exactIdentity.IndexOf('\\') >= 0 ||
                     exactIdentity.EndsWith(
                         ".dll",
                         StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException(
                        "An accepted assembly identity must be an exact simple " +
                        "name, not a display name, path, file name, or qualified " +
                        "assembly identity.",
                        parameterName);
                }

                if (!seen.Add(exactIdentity))
                {
                    throw new ArgumentException(
                        "A declaration cannot repeat an accepted exact identity.",
                        parameterName);
                }

                copied.Add(exactIdentity);
            }

            if (copied.Count == 0)
            {
                throw new ArgumentException(
                    "A declared integration requires at least one accepted exact " +
                    "identity.",
                    parameterName);
            }

            return new ReadOnlyCollection<string>(copied);
        }

        private static string RequireUpstreamEvidenceReference(string value)
        {
            string boundedReference = ExternalModIntegrationModelValidation
                .RequireExactBoundedText(
                    value,
                    nameof(value),
                    1024,
                    "An upstream evidence reference");
            Uri? uri;
            if (!Uri.TryCreate(boundedReference, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException(
                    "An upstream evidence reference must be an absolute HTTP or " +
                    "HTTPS URI.",
                    nameof(value));
            }

            return boundedReference;
        }

        private static IReadOnlyList<DeclaredModIntegrationCapability>
            CopyDeclaredCapabilities(
                IEnumerable<DeclaredModIntegrationCapability>
                    declaredCapabilities)
        {
            if (declaredCapabilities == null)
            {
                throw new ArgumentNullException(nameof(declaredCapabilities));
            }

            var copied = new List<DeclaredModIntegrationCapability>();
            var seen = new HashSet<RuntimeCapabilityId>();
            foreach (DeclaredModIntegrationCapability declaration in
                     declaredCapabilities)
            {
                if (declaration == null)
                {
                    throw new ArgumentException(
                        "A declared integration capability cannot be null.",
                        nameof(declaredCapabilities));
                }

                if (!seen.Add(declaration.CapabilityId))
                {
                    throw new ArgumentException(
                        "A declaration cannot repeat or assign one capability " +
                        "identity to more than one inspection category.",
                        nameof(declaredCapabilities));
                }

                copied.Add(declaration);
            }

            if (copied.Count == 0)
            {
                throw new ArgumentException(
                    "A declared integration requires at least one capability.",
                    nameof(declaredCapabilities));
            }

            return new ReadOnlyCollection<DeclaredModIntegrationCapability>(
                copied);
        }

        private static IReadOnlyList<ExternalModIntegrationCategory>
            ProjectCategories(
                IReadOnlyList<DeclaredModIntegrationCapability>
                    declaredCapabilities)
        {
            var categories = new HashSet<ExternalModIntegrationCategory>();
            for (int index = 0; index < declaredCapabilities.Count; index++)
            {
                categories.Add(declaredCapabilities[index].Category);
            }

            return ExternalModIntegrationModelValidation.CopyCategories(
                categories,
                nameof(declaredCapabilities));
        }

        private static IReadOnlyList<RuntimeCapabilityId> ProjectCapabilityIds(
            IReadOnlyList<DeclaredModIntegrationCapability>
                declaredCapabilities)
        {
            var capabilityIds = new List<RuntimeCapabilityId>(
                declaredCapabilities.Count);
            for (int index = 0; index < declaredCapabilities.Count; index++)
            {
                capabilityIds.Add(declaredCapabilities[index].CapabilityId);
            }

            return new ReadOnlyCollection<RuntimeCapabilityId>(capabilityIds);
        }
    }
}
