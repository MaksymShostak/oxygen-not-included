#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reports an active FastTrack delivery replacement whose exact contract was
    /// not verified and therefore cannot be replaced by a speculative Klei path.
    /// </summary>
    internal sealed class FastTrackDeliveryEligibilityCompatibilityException :
        Exception
    {
        internal FastTrackDeliveryEligibilityCompatibilityException(
            string message,
            FastTrackCompatibilityReport compatibilityReport)
            : base(message)
        {
            CompatibilityReport = compatibilityReport ??
                throw new ArgumentNullException(nameof(compatibilityReport));
        }

        internal FastTrackCompatibilityReport CompatibilityReport { get; }
    }
}
