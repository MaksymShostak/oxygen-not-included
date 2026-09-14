#nullable enable

using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>One-way failure transition with a single pending player warning.</summary>
    internal sealed class RuntimeFailureState
    {
        private int state;

        internal bool HasFailed => Volatile.Read(ref state) != 0;
        internal bool HasPendingWarning => Volatile.Read(ref state) == 1;

        internal bool TryRecordFailure() => Interlocked.CompareExchange(ref state, 1, 0) == 0;

        internal bool TryTakeWarning() => Interlocked.CompareExchange(ref state, 2, 1) == 1;
    }
}
