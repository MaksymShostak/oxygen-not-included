#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reports that installed reflection or Harmony metadata no longer matches
    /// the exact runtime contract required by a Delivery Temperature Limit patch.
    /// </summary>
    internal sealed class HarmonyPatchContractViolationException : Exception
    {
        internal HarmonyPatchContractViolationException(string message)
            : base(message)
        {
        }

        internal HarmonyPatchContractViolationException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
