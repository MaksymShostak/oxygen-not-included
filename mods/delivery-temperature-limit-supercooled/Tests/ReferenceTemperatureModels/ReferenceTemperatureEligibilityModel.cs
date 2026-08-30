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
    internal static bool[] EvaluateDestinationConstraintAllowances(
        IReadOnlyList<DeliveryTemperatureConstraint> destinationConstraints,
        float temperatureKelvin)
    {
        ArgumentNullException.ThrowIfNull(destinationConstraints);

        var allowances = new bool[destinationConstraints.Count];
        for (var constraintIndex = 0;
             constraintIndex < destinationConstraints.Count;
             constraintIndex++)
        {
            var destinationConstraint = destinationConstraints[constraintIndex];
            if (!destinationConstraint.IsEnabled)
            {
                allowances[constraintIndex] = true;
                continue;
            }

            if (destinationConstraint.IsEmpty)
            {
                allowances[constraintIndex] = false;
                continue;
            }

            // This remains an independent oracle: it repeats the serialized-domain
            // truncation and comparisons rather than invoking any production
            // constraint, interval-set, or partition-classification operation.
            var truncatedKelvin = (int)temperatureKelvin;
            allowances[constraintIndex] =
                destinationConstraint.MinimumInclusiveKelvin <= truncatedKelvin &&
                truncatedKelvin < destinationConstraint.MaximumExclusiveKelvin;
        }

        return allowances;
    }

    internal static bool AnyDestinationAllowsTemperature(
        IReadOnlyList<DeliveryTemperatureConstraint> destinationConstraints,
        float temperatureKelvin)
    {
        ArgumentNullException.ThrowIfNull(destinationConstraints);

        foreach (var destinationConstraint in destinationConstraints)
        {
            // A disabled destination is the logical unconstrained case. Keep it
            // here in the direct oracle even though production translates it into
            // the separately named includesUnconstrainedDestination fact.
            if (!destinationConstraint.IsEnabled)
            {
                return true;
            }

            if (destinationConstraint.IsEmpty)
            {
                continue;
            }

            // Deliberately duplicate truncation and boundary comparisons instead
            // of calling the production Allows method or interval-set logic.
            var truncatedKelvin = (int)temperatureKelvin;
            if (destinationConstraint.MinimumInclusiveKelvin <=
                    truncatedKelvin &&
                truncatedKelvin <
                    destinationConstraint.MaximumExclusiveKelvin)
            {
                return true;
            }
        }

        return false;
    }

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
