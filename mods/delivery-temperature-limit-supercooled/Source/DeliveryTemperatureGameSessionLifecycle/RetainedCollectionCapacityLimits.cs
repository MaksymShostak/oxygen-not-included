#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reviewed upper bounds for retaining reusable variable-capacity collections
    /// after an operation completes. They never limit the workload processed by an
    /// invocation: a consumer must process every entry, then replace only an
    /// oversized reusable backing collection at its documented safe boundary.
    /// </summary>
    internal static class RetainedCollectionCapacityLimits
    {
        // These deliberately generous powers of two suit community-mod late-game
        // colonies while preventing one pathological operation from pinning its
        // peak capacity for the rest of the process lifetime. Task 25 verifies each
        // retention transition structurally; Task 28 permits a simple indicative
        // manual observation without turning these values into benchmark targets.
        internal const int MaximumRetainedPickupClassificationCount = 16384;
        internal const int MaximumRetainedFastTrackGroupingKeyCount = 8192;
        internal const int MaximumRetainedFetchEligibilityEntryCount = 4096;
        internal const int MaximumRetainedWorldResourceTagCount = 4096;
    }
}
