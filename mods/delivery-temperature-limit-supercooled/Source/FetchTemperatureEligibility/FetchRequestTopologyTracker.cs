#nullable enable

using System;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns the monotonically increasing fetch-request topology version.
    /// </summary>
    internal sealed class FetchRequestTopologyTracker
    {
        private readonly object versionMutationLock = new object();

        // The exact field name is a representation contract for the narrow checked
        // exhaustion test. There is intentionally no injectable counter or limit.
        private long version;

        internal FetchRequestTopologyVersion CaptureVersion() =>
            new FetchRequestTopologyVersion(Volatile.Read(ref version));

        internal FetchRequestTopologyVersion RecordEffectiveChange()
        {
            lock (versionMutationLock)
            {
                if (version == long.MaxValue)
                {
                    throw CreateVersionExhaustedException();
                }

                long nextVersion;
                try
                {
                    nextVersion = checked(version + 1L);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        "The fetch-request topology version is exhausted; the " +
                        "tracker will not wrap or publish a reusable version.",
                        exception);
                }

                if (nextVersion <= 0)
                {
                    throw CreateVersionExhaustedException();
                }

                // Callers invoke this operation only for real topology events.
                // Repeated calls therefore each advance once; event adapters own
                // suppression of callbacks already proven to be semantic no-ops.
                Volatile.Write(ref version, nextVersion);
                return new FetchRequestTopologyVersion(nextVersion);
            }
        }

        private static InvalidOperationException CreateVersionExhaustedException() =>
            new InvalidOperationException(
                "The fetch-request topology version is exhausted; the tracker " +
                "will not wrap or publish a reusable version.");
    }
}
