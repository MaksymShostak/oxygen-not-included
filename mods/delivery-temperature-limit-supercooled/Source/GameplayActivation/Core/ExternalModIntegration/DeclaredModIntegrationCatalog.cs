#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Explicit ordered set of external-mod integrations that Temperature Limit
    /// intentionally recognizes. Order is for inspection and reporting only.
    /// </summary>
    internal sealed class DeclaredModIntegrationCatalog
    {
        private readonly IReadOnlyDictionary<
            DeclaredModIntegrationId,
            DeclaredModIntegrationDescriptor> descriptorsById;

        internal DeclaredModIntegrationCatalog(
            IEnumerable<DeclaredModIntegrationDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            var copied = new List<DeclaredModIntegrationDescriptor>();
            var byId = new Dictionary<
                DeclaredModIntegrationId,
                DeclaredModIntegrationDescriptor>();
            var acceptedStaticIds = new HashSet<string>(StringComparer.Ordinal);
            var acceptedAssemblyNames =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (DeclaredModIntegrationDescriptor descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    throw new ArgumentException(
                        "A declared integration catalog cannot contain null.",
                        nameof(descriptors));
                }

                if (byId.ContainsKey(descriptor.IntegrationId))
                {
                    throw new ArgumentException(
                        "A declared integration catalog cannot repeat an " +
                        "integration identity.",
                        nameof(descriptors));
                }

                for (int index = 0;
                     index < descriptor.AcceptedStaticIds.Count;
                     index++)
                {
                    if (!acceptedStaticIds.Add(
                            descriptor.AcceptedStaticIds[index]))
                    {
                        throw new ArgumentException(
                            "Two declared integrations cannot accept the same " +
                            "exact Klei static ID.",
                            nameof(descriptors));
                    }
                }

                for (int index = 0;
                     index < descriptor.AcceptedAssemblySimpleNames.Count;
                     index++)
                {
                    if (!acceptedAssemblyNames.Add(
                            descriptor.AcceptedAssemblySimpleNames[index]))
                    {
                        throw new ArgumentException(
                            "Two declared integrations cannot accept the same " +
                            "exact assembly simple name.",
                            nameof(descriptors));
                    }
                }

                copied.Add(descriptor);
                byId.Add(descriptor.IntegrationId, descriptor);
            }

            Descriptors = new ReadOnlyCollection<
                DeclaredModIntegrationDescriptor>(copied);
            descriptorsById = new ReadOnlyDictionary<
                DeclaredModIntegrationId,
                DeclaredModIntegrationDescriptor>(byId);
        }

        internal IReadOnlyList<DeclaredModIntegrationDescriptor>
            Descriptors { get; }

        internal bool TryGet(
            DeclaredModIntegrationId integrationId,
            out DeclaredModIntegrationDescriptor? descriptor) =>
            descriptorsById.TryGetValue(integrationId, out descriptor);

        internal DeclaredModIntegrationDescriptor GetRequired(
            DeclaredModIntegrationId integrationId)
        {
            DeclaredModIntegrationDescriptor? descriptor;
            if (!descriptorsById.TryGetValue(integrationId, out descriptor))
            {
                throw new KeyNotFoundException(
                    "The requested external-mod integration is not declared: " +
                    integrationId.Value);
            }

            return descriptor;
        }
    }
}
