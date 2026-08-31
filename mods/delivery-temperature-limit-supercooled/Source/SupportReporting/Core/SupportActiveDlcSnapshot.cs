#nullable enable

using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    internal sealed class SupportActiveDlcSnapshot
    {
        private SupportActiveDlcSnapshot(
            string state,
            IEnumerable<string> ids,
            string? unavailableReason)
        {
            State = SupportReportCollections.RequireNonBlank(
                state,
                nameof(state));
            Ids = SupportReportCollections.CopyStrings(ids, nameof(ids));
            UnavailableReason = unavailableReason;
        }

        public string State { get; }

        public IReadOnlyList<string> Ids { get; }

        public string? UnavailableReason { get; }

        internal static SupportActiveDlcSnapshot Available(
            IEnumerable<string> ids) =>
            new SupportActiveDlcSnapshot(
                SupportReportLimits.AvailableState,
                ids,
                null);

        internal static SupportActiveDlcSnapshot Unavailable(
            string reason) =>
            new SupportActiveDlcSnapshot(
                SupportReportLimits.UnavailableState,
                Array.Empty<string>(),
                SupportReportCollections.RequireNonBlank(
                    reason,
                    nameof(reason)));
    }

    internal static class SupportActiveDlcCapture
    {
        private const string UnavailableReason =
            "Active DLC identifiers were unavailable during report generation.";

        internal static SupportActiveDlcSnapshot Capture(
            Func<IEnumerable<string>?> readActiveDlcIds,
            ICollection<string> warnings)
        {
            if (readActiveDlcIds == null)
            {
                throw new ArgumentNullException(nameof(readActiveDlcIds));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            try
            {
                IEnumerable<string>? observedIds = readActiveDlcIds();
                if (observedIds == null)
                {
                    return CreateUnavailableSnapshot(warnings);
                }

                var normalizedIds = new List<string>();
                foreach (string observedId in observedIds)
                {
                    if (!string.IsNullOrWhiteSpace(observedId))
                    {
                        normalizedIds.Add(observedId);
                    }
                }

                normalizedIds.Sort(StringComparer.Ordinal);
                return SupportActiveDlcSnapshot.Available(normalizedIds);
            }
            catch (Exception)
            {
                return CreateUnavailableSnapshot(warnings);
            }
        }

        private static SupportActiveDlcSnapshot CreateUnavailableSnapshot(
            ICollection<string> warnings)
        {
            warnings.Add(UnavailableReason);
            return SupportActiveDlcSnapshot.Unavailable(UnavailableReason);
        }
    }
}
