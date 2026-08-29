#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Guarded availability result whose amount is legally accessible only when
    /// all required inventory evidence is complete.
    /// </summary>
    internal readonly struct TemperatureConstrainedAmountAvailability
    {
        private readonly float completeAvailableAmount;

        private TemperatureConstrainedAmountAvailability(
            TemperatureConstrainedAmountAvailabilityState state,
            float completeAvailableAmount)
        {
            State = state;
            this.completeAvailableAmount = completeAvailableAmount;
        }

        internal TemperatureConstrainedAmountAvailabilityState State { get; }

        internal static TemperatureConstrainedAmountAvailability
            TemperatureConstraintDisabled() =>
            new TemperatureConstrainedAmountAvailability(
                TemperatureConstrainedAmountAvailabilityState
                    .TemperatureConstraintDisabled,
                0.0f);

        internal static TemperatureConstrainedAmountAvailability
            InventoryIncomplete() =>
            new TemperatureConstrainedAmountAvailability(
                TemperatureConstrainedAmountAvailabilityState
                    .InventoryIncomplete,
                0.0f);

        internal static TemperatureConstrainedAmountAvailability Complete(
            float availableAmount) =>
            new TemperatureConstrainedAmountAvailability(
                TemperatureConstrainedAmountAvailabilityState.Complete,
                availableAmount);

        /// <summary>
        /// Returns true only when the out value is semantically legal to consume.
        /// Status adapters must preserve ONI's incoming availability unchanged for
        /// both false-returning states.
        /// </summary>
        internal bool TryGetCompleteAvailableAmount(out float availableAmount)
        {
            if (State == TemperatureConstrainedAmountAvailabilityState.Complete)
            {
                availableAmount = completeAvailableAmount;
                return true;
            }

            availableAmount = 0.0f;
            return false;
        }
    }
}
