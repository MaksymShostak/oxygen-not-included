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

internal sealed class ReferenceFetchTemperatureRequest
{
    private ReferenceFetchTemperatureRequest(
        int parentWorldId,
        IReadOnlyList<Tag> requestedTags,
        bool hasEnabledTemperatureConstraint,
        DeliveryTemperatureConstraint enabledTemperatureConstraint)
    {
        ArgumentNullException.ThrowIfNull(requestedTags);
        if (parentWorldId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parentWorldId));
        }

        if (hasEnabledTemperatureConstraint &&
            !enabledTemperatureConstraint.IsEnabled)
        {
            throw new ArgumentException(
                "A constrained reference fetch request requires an enabled " +
                "temperature constraint.",
                nameof(enabledTemperatureConstraint));
        }

        var copiedRequestedTags = new Tag[requestedTags.Count];
        for (var tagIndex = 0; tagIndex < requestedTags.Count; tagIndex++)
        {
            copiedRequestedTags[tagIndex] = requestedTags[tagIndex];
        }

        ParentWorldId = parentWorldId;
        RequestedTags = Array.AsReadOnly(copiedRequestedTags);
        HasEnabledTemperatureConstraint = hasEnabledTemperatureConstraint;
        EnabledTemperatureConstraint = enabledTemperatureConstraint;
    }

    internal int ParentWorldId { get; }

    internal IReadOnlyList<Tag> RequestedTags { get; }

    internal bool HasEnabledTemperatureConstraint { get; }

    internal DeliveryTemperatureConstraint EnabledTemperatureConstraint { get; }

    internal static ReferenceFetchTemperatureRequest Unconstrained(
        int parentWorldId,
        IReadOnlyList<Tag> requestedTags) =>
        new ReferenceFetchTemperatureRequest(
            parentWorldId,
            requestedTags,
            hasEnabledTemperatureConstraint: false,
            enabledTemperatureConstraint: default);

    internal static ReferenceFetchTemperatureRequest TemperatureConstrained(
        int parentWorldId,
        IReadOnlyList<Tag> requestedTags,
        DeliveryTemperatureConstraint enabledTemperatureConstraint) =>
        new ReferenceFetchTemperatureRequest(
            parentWorldId,
            requestedTags,
            hasEnabledTemperatureConstraint: true,
            enabledTemperatureConstraint);
}

internal static class ReferenceTemperatureEligibilityModel
{
    internal static DeliveryTemperatureConstraint[]
        GetStorageDestinationConstraints(
            IReadOnlyList<ReferenceFetchTemperatureRequest> fetchRequests,
            int parentWorldId,
            Tag requestedTag)
    {
        ArgumentNullException.ThrowIfNull(fetchRequests);
        var destinationConstraints = new List<DeliveryTemperatureConstraint>();
        foreach (var fetchRequest in fetchRequests)
        {
            if (fetchRequest.ParentWorldId != parentWorldId ||
                !ContainsTag(fetchRequest.RequestedTags, requestedTag))
            {
                continue;
            }

            destinationConstraints.Add(
                fetchRequest.HasEnabledTemperatureConstraint
                    ? fetchRequest.EnabledTemperatureConstraint
                    : DeliveryTemperatureConstraint.FromSerializedLimits(0, 0));
        }

        return destinationConstraints.ToArray();
    }

    internal static int[] CreateSortedPickupDecisionEndpointUnion(
        IReadOnlyList<ReferenceFetchTemperatureRequest> fetchRequests,
        int parentWorldId,
        IReadOnlyList<Tag> applicableRequestedTags)
    {
        ArgumentNullException.ThrowIfNull(fetchRequests);
        ArgumentNullException.ThrowIfNull(applicableRequestedTags);
        var applicableTagSet = new HashSet<Tag>(applicableRequestedTags);
        var decisionEndpointsKelvin = new SortedSet<int>();

        foreach (var fetchRequest in fetchRequests)
        {
            if (fetchRequest.ParentWorldId != parentWorldId ||
                !fetchRequest.HasEnabledTemperatureConstraint ||
                fetchRequest.EnabledTemperatureConstraint.IsEmpty ||
                !ContainsAnyTag(
                    fetchRequest.RequestedTags,
                    applicableTagSet))
            {
                continue;
            }

            decisionEndpointsKelvin.Add(
                fetchRequest.EnabledTemperatureConstraint.MinimumInclusiveKelvin);
            decisionEndpointsKelvin.Add(
                fetchRequest.EnabledTemperatureConstraint.MaximumExclusiveKelvin);
        }

        return decisionEndpointsKelvin.ToArray();
    }

    internal static DeliveryTemperatureConstraint[]
        GetApplicablePickupTemperatureConstraints(
            IReadOnlyList<ReferenceFetchTemperatureRequest> fetchRequests,
            int parentWorldId,
            IReadOnlyList<Tag> applicableRequestedTags)
    {
        ArgumentNullException.ThrowIfNull(fetchRequests);
        ArgumentNullException.ThrowIfNull(applicableRequestedTags);
        var applicableTagSet = new HashSet<Tag>(applicableRequestedTags);
        var applicableConstraints = new List<DeliveryTemperatureConstraint>();

        foreach (var fetchRequest in fetchRequests)
        {
            if (fetchRequest.ParentWorldId == parentWorldId &&
                fetchRequest.HasEnabledTemperatureConstraint &&
                ContainsAnyTag(fetchRequest.RequestedTags, applicableTagSet))
            {
                applicableConstraints.Add(
                    fetchRequest.EnabledTemperatureConstraint);
            }
        }

        return applicableConstraints.ToArray();
    }

    internal static Tag[] GetRequestedTagsInFirstEncounterOrder(
        IReadOnlyList<ReferenceFetchTemperatureRequest> fetchRequests,
        int parentWorldId)
    {
        ArgumentNullException.ThrowIfNull(fetchRequests);
        var observedTags = new HashSet<Tag>();
        var orderedTags = new List<Tag>();
        foreach (var fetchRequest in fetchRequests)
        {
            if (fetchRequest.ParentWorldId != parentWorldId)
            {
                continue;
            }

            foreach (var requestedTag in fetchRequest.RequestedTags)
            {
                if (observedTags.Add(requestedTag))
                {
                    orderedTags.Add(requestedTag);
                }
            }
        }

        return orderedTags.ToArray();
    }

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

    private static bool ContainsAnyTag(
        IReadOnlyList<Tag> requestedTags,
        ISet<Tag> candidateTags)
    {
        foreach (var requestedTag in requestedTags)
        {
            if (candidateTags.Contains(requestedTag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTag(
        IReadOnlyList<Tag> requestedTags,
        Tag candidateTag)
    {
        foreach (var requestedTag in requestedTags)
        {
            if (requestedTag.Equals(candidateTag))
            {
                return true;
            }
        }

        return false;
    }
}
