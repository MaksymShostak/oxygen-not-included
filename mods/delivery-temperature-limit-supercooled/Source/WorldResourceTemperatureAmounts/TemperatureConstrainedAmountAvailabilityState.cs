#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Exhaustive reason that a temperature-constrained availability query either
    /// bypasses replacement, must preserve ONI's value, or supplies a complete
    /// replacement amount.
    /// </summary>
    internal enum TemperatureConstrainedAmountAvailabilityState
    {
        TemperatureConstraintDisabled,
        InventoryIncomplete,
        Complete
    }
}
