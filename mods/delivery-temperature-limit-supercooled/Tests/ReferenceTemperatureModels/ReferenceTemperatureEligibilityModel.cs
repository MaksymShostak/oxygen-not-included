namespace DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

internal readonly struct ReferenceTemperatureAmount
{
    internal ReferenceTemperatureAmount(float temperatureKelvin, float amount)
    {
        TemperatureKelvin = temperatureKelvin;
        Amount = amount;
    }

    internal float TemperatureKelvin { get; }

    internal float Amount { get; }
}

internal static class ReferenceTemperatureEligibilityModel
{
    internal static float SumAllowedAmounts(
        IReadOnlyList<ReferenceTemperatureAmount> sourceTemperatureAmounts,
        DeliveryTemperatureConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(sourceTemperatureAmounts);

        var allowedAmount = 0.0f;
        foreach (var sourceTemperatureAmount in sourceTemperatureAmounts)
        {
            // Deliberately duplicate the serialized-domain decision instead of
            // calling DeliveryTemperatureConstraint.Allows or using production
            // buckets. This model is an independent oracle for truncation toward
            // zero and the inclusive-minimum/exclusive-maximum comparison.
            var temperatureIsAllowed = !constraint.IsEnabled;
            if (constraint.IsEnabled && !constraint.IsEmpty)
            {
                var truncatedKelvin =
                    (int)sourceTemperatureAmount.TemperatureKelvin;
                temperatureIsAllowed =
                    constraint.MinimumInclusiveKelvin <= truncatedKelvin &&
                    truncatedKelvin < constraint.MaximumExclusiveKelvin;
            }

            if (temperatureIsAllowed)
            {
                allowedAmount += sourceTemperatureAmount.Amount;
            }
        }

        return allowedAmount;
    }
}
