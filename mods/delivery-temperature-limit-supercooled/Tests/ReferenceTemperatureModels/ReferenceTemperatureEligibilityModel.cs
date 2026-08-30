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
    internal static float GetRepresentativeTemperatureKelvin(
        int decisionBucketOrdinal)
    {
        if (decisionBucketOrdinal <
                TemperatureDecisionBucket.BelowMinimumKelvinOrdinal ||
            decisionBucketOrdinal >
                TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decisionBucketOrdinal));
        }

        return GetRepresentativeTruncatedKelvin(decisionBucketOrdinal);
    }

    internal static int GetRepresentativeTruncatedKelvin(
        int decisionBucketOrdinal)
    {
        if (decisionBucketOrdinal ==
            TemperatureDecisionBucket.BelowMinimumKelvinOrdinal)
        {
            return OniStorableTemperatureBounds.MinimumTemperatureKelvin - 1;
        }

        if (decisionBucketOrdinal ==
            TemperatureDecisionBucket.AtOrAboveMaximumKelvinOrdinal)
        {
            return OniStorableTemperatureBounds.MaximumTemperatureKelvin;
        }

        if (decisionBucketOrdinal <
                TemperatureDecisionBucket.FirstIntegerKelvinOrdinal ||
            decisionBucketOrdinal >
                TemperatureDecisionBucket.HighestIntegerKelvinOrdinal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decisionBucketOrdinal));
        }

        return decisionBucketOrdinal -
            TemperatureDecisionBucket.FirstIntegerKelvinOrdinal;
    }

    internal static bool AllowanceVectorsAreEqual(
        IReadOnlyList<bool> first,
        IReadOnlyList<bool> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var allowanceIndex = 0;
             allowanceIndex < first.Count;
             allowanceIndex++)
        {
            if (first[allowanceIndex] != second[allowanceIndex])
            {
                return false;
            }
        }

        return true;
    }

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
            allowances[constraintIndex] = AllowsTemperature(
                destinationConstraint,
                temperatureKelvin);
        }

        return allowances;
    }

    /// <summary>
    /// Independently evaluates one normalized constraint without invoking the
    /// production constraint, interval, bucket, partition, or series operation.
    /// </summary>
    internal static bool AllowsTemperature(
        DeliveryTemperatureConstraint constraint,
        float temperatureKelvin)
    {
        if (!constraint.IsEnabled)
        {
            return true;
        }

        if (constraint.IsEmpty)
        {
            return false;
        }

        // The compatibility behavior is truncation toward zero followed by an
        // inclusive-minimum/exclusive-maximum comparison. Repeat it here so the
        // oracle remains independent from all production decision domains.
        var truncatedKelvin = (int)temperatureKelvin;
        return constraint.MinimumInclusiveKelvin <= truncatedKelvin &&
            truncatedKelvin < constraint.MaximumExclusiveKelvin;
    }

    internal static bool AnyDestinationAllowsTemperature(
        IReadOnlyList<DeliveryTemperatureConstraint> destinationConstraints,
        float temperatureKelvin)
    {
        ArgumentNullException.ThrowIfNull(destinationConstraints);

        foreach (var destinationConstraint in destinationConstraints)
        {
            if (AllowsTemperature(destinationConstraint, temperatureKelvin))
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
            if (AllowsTemperature(
                    constraint,
                    sourceTemperatureAmount.TemperatureKelvin))
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
