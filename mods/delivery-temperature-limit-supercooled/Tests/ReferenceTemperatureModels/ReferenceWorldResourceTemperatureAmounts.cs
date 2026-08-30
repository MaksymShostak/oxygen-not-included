#nullable enable

namespace DeliveryTemperatureLimit.Tests.ReferenceTemperatureModels;

/// <summary>
/// Test-only immutable input for one resource tag's observed temperature amounts.
/// The constructor copies caller-owned data so later test mutations cannot alter
/// an already-published reference state.
/// </summary>
internal sealed class ReferenceWorldResourceTemperatureAmountSeries
{
    private readonly IReadOnlyList<ReferenceTemperatureAmount>
        temperatureAmounts;

    internal ReferenceWorldResourceTemperatureAmountSeries(
        Tag resourceTag,
        IReadOnlyList<ReferenceTemperatureAmount> temperatureAmounts)
    {
        ArgumentNullException.ThrowIfNull(temperatureAmounts);
        ResourceTag = resourceTag;
        var copiedTemperatureAmounts =
            new ReferenceTemperatureAmount[temperatureAmounts.Count];
        for (int amountIndex = 0;
             amountIndex < temperatureAmounts.Count;
             amountIndex++)
        {
            copiedTemperatureAmounts[amountIndex] =
                temperatureAmounts[amountIndex];
        }

        this.temperatureAmounts =
            Array.AsReadOnly(copiedTemperatureAmounts);
    }

    internal Tag ResourceTag { get; }

    internal IReadOnlyList<ReferenceTemperatureAmount> TemperatureAmounts =>
        temperatureAmounts;
}

internal enum ReferenceWorldResourceTemperatureAmountAvailabilityState
{
    TemperatureConstraintDisabled,
    InventoryIncomplete,
    Complete
}

/// <summary>
/// Test-only guarded query result. Keeping this independent from the production
/// result factories prevents a production-state bug from being duplicated by the
/// oracle through shared construction code.
/// </summary>
internal readonly struct ReferenceWorldResourceTemperatureAmountAvailability
{
    private readonly float completeAmount;

    private ReferenceWorldResourceTemperatureAmountAvailability(
        ReferenceWorldResourceTemperatureAmountAvailabilityState state,
        float completeAmount)
    {
        State = state;
        this.completeAmount = completeAmount;
    }

    internal ReferenceWorldResourceTemperatureAmountAvailabilityState State
    {
        get;
    }

    internal static ReferenceWorldResourceTemperatureAmountAvailability
        TemperatureConstraintDisabled() =>
        new(
            ReferenceWorldResourceTemperatureAmountAvailabilityState
                .TemperatureConstraintDisabled,
            completeAmount: 0.0f);

    internal static ReferenceWorldResourceTemperatureAmountAvailability
        InventoryIncomplete() =>
        new(
            ReferenceWorldResourceTemperatureAmountAvailabilityState
                .InventoryIncomplete,
            completeAmount: 0.0f);

    internal static ReferenceWorldResourceTemperatureAmountAvailability Complete(
        float completeAmount) =>
        new(
            ReferenceWorldResourceTemperatureAmountAvailabilityState.Complete,
            completeAmount);

    internal bool TryGetCompleteAmount(out float amount)
    {
        if (State ==
            ReferenceWorldResourceTemperatureAmountAvailabilityState.Complete)
        {
            amount = completeAmount;
            return true;
        }

        amount = 0.0f;
        return false;
    }
}

/// <summary>
/// Direct test-only reference model for registered worlds, parent membership,
/// publication generations, coverage, and pending resource-tag series.
/// </summary>
/// <remarks>
/// This intentionally does not call production catalog completeness, aggregate,
/// bucket, interval, or sparse-series operations. It instead evaluates the three
/// required proofs directly for every parent member: current coverage, known tag
/// presence, and a current series whenever that tag is present.
/// </remarks>
internal sealed class ReferenceWorldResourceTemperatureAmounts
{
    private readonly Dictionary<int, ReferenceWorldPublication>
        publicationsByWorldId = [];

    internal void RegisterWorld(int worldId, int parentWorldId)
    {
        if (worldId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(worldId));
        }

        if (parentWorldId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parentWorldId));
        }

        if (publicationsByWorldId.TryGetValue(
                worldId,
                out ReferenceWorldPublication? publication))
        {
            // Reparenting preserves the world's current publication. Parent
            // completeness is derived afresh by each direct query.
            publication.ParentWorldId = parentWorldId;
            return;
        }

        publicationsByWorldId.Add(
            worldId,
            new ReferenceWorldPublication(parentWorldId));
    }

    internal void RemoveWorld(int worldId) =>
        publicationsByWorldId.Remove(worldId);

    internal bool TryPublishCompleteWorld(
        int worldId,
        WorldInventoryCollectionGeneration collectionGeneration,
        IReadOnlyList<ReferenceWorldResourceTemperatureAmountSeries>
            resourceTemperatureAmountSeries)
    {
        ArgumentNullException.ThrowIfNull(resourceTemperatureAmountSeries);
        if (!publicationsByWorldId.TryGetValue(
                worldId,
                out ReferenceWorldPublication? publication) ||
            IsOlderThanCurrentPublication(
                collectionGeneration,
                publication))
        {
            return false;
        }

        var replacementPresentResourceTags = new HashSet<Tag>();
        var replacementTemperatureAmountsByResourceTag =
            new Dictionary<Tag, IReadOnlyList<ReferenceTemperatureAmount>>();
        foreach (ReferenceWorldResourceTemperatureAmountSeries resourceSeries in
                 resourceTemperatureAmountSeries)
        {
            if (resourceSeries is null)
            {
                throw new ArgumentException(
                    "A complete-world reference publication cannot contain a " +
                    "null resource series.",
                    nameof(resourceTemperatureAmountSeries));
            }

            if (!replacementPresentResourceTags.Add(resourceSeries.ResourceTag))
            {
                throw new ArgumentException(
                    "A complete-world reference publication cannot contain the " +
                    "same resource tag more than once.",
                    nameof(resourceTemperatureAmountSeries));
            }

            replacementTemperatureAmountsByResourceTag.Add(
                resourceSeries.ResourceTag,
                CopyTemperatureAmounts(resourceSeries.TemperatureAmounts));
        }

        publication.HasPublication = true;
        publication.CollectionGeneration = collectionGeneration;
        publication.PublicationStrength =
            ReferenceWorldPublicationStrength.CompleteWorld;
        publication.PresentResourceTags = replacementPresentResourceTags;
        publication.TemperatureAmountsByResourceTag =
            replacementTemperatureAmountsByResourceTag;
        return true;
    }

    internal bool TryPublishResourceTagCoverage(
        int worldId,
        WorldInventoryCollectionGeneration collectionGeneration,
        IReadOnlyList<Tag> presentResourceTags)
    {
        ArgumentNullException.ThrowIfNull(presentResourceTags);
        if (!publicationsByWorldId.TryGetValue(
                worldId,
                out ReferenceWorldPublication? publication) ||
            IsOlderThanCurrentPublication(
                collectionGeneration,
                publication))
        {
            return false;
        }

        if (publication.HasPublication &&
            publication.CollectionGeneration.Equals(collectionGeneration) &&
            publication.PublicationStrength ==
                ReferenceWorldPublicationStrength.CompleteWorld)
        {
            // Coverage is weaker evidence than the complete map at the same
            // generation and therefore may not downgrade it.
            return false;
        }

        var replacementPresentResourceTags = new HashSet<Tag>();
        foreach (Tag presentResourceTag in presentResourceTags)
        {
            replacementPresentResourceTags.Add(presentResourceTag);
        }

        var retainedTemperatureAmountsByResourceTag =
            new Dictionary<Tag, IReadOnlyList<ReferenceTemperatureAmount>>();
        if (publication.HasPublication &&
            publication.CollectionGeneration.Equals(collectionGeneration))
        {
            // Same-generation coverage can retain already-arrived series only
            // for tags that remain explicitly present. Newly covered tags remain
            // pending until their individual series arrives.
            foreach (KeyValuePair<
                         Tag,
                         IReadOnlyList<ReferenceTemperatureAmount>> entry in
                     publication.TemperatureAmountsByResourceTag)
            {
                if (replacementPresentResourceTags.Contains(entry.Key))
                {
                    retainedTemperatureAmountsByResourceTag.Add(
                        entry.Key,
                        entry.Value);
                }
            }
        }

        publication.HasPublication = true;
        publication.CollectionGeneration = collectionGeneration;
        publication.PublicationStrength =
            ReferenceWorldPublicationStrength.ResourceTagCoverage;
        publication.PresentResourceTags = replacementPresentResourceTags;
        publication.TemperatureAmountsByResourceTag =
            retainedTemperatureAmountsByResourceTag;
        return true;
    }

    internal bool TryPublishResourceTagTemperatureAmounts(
        int worldId,
        WorldInventoryCollectionGeneration collectionGeneration,
        Tag resourceTag,
        IReadOnlyList<ReferenceTemperatureAmount> temperatureAmounts)
    {
        ArgumentNullException.ThrowIfNull(temperatureAmounts);
        if (!publicationsByWorldId.TryGetValue(
                worldId,
                out ReferenceWorldPublication? publication) ||
            !publication.HasPublication ||
            !publication.CollectionGeneration.Equals(collectionGeneration) ||
            publication.PublicationStrength ==
                ReferenceWorldPublicationStrength.NoCoverage)
        {
            return false;
        }

        // Presence extension and series replacement are one semantic operation.
        // The reference state therefore never exposes a manufactured intermediate
        // where a newly published tag is present but still pending.
        publication.PresentResourceTags.Add(resourceTag);
        publication.TemperatureAmountsByResourceTag[resourceTag] =
            CopyTemperatureAmounts(temperatureAmounts);
        return true;
    }

    internal ReferenceWorldResourceTemperatureAmountAvailability
        GetTemperatureConstrainedAmountAvailability(
            int parentWorldId,
            Tag resourceTag,
            DeliveryTemperatureConstraint constraint,
            WorldInventoryCollectionGeneration expectedCollectionGeneration)
    {
        if (!constraint.IsEnabled)
        {
            return ReferenceWorldResourceTemperatureAmountAvailability
                .TemperatureConstraintDisabled();
        }

        if (constraint.IsEmpty)
        {
            // Empty enabled constraints are a complete semantic zero and require
            // no inventory evidence.
            return ReferenceWorldResourceTemperatureAmountAvailability.Complete(
                completeAmount: 0.0f);
        }

        bool foundParentMember = false;
        float completeAmount = 0.0f;
        foreach (ReferenceWorldPublication publication in
                 publicationsByWorldId.Values)
        {
            if (publication.ParentWorldId != parentWorldId)
            {
                continue;
            }

            foundParentMember = true;
            if (!publication.HasPublication ||
                !publication.CollectionGeneration.Equals(
                    expectedCollectionGeneration) ||
                publication.PublicationStrength ==
                    ReferenceWorldPublicationStrength.NoCoverage)
            {
                return ReferenceWorldResourceTemperatureAmountAvailability
                    .InventoryIncomplete();
            }

            if (!publication.PresentResourceTags.Contains(resourceTag))
            {
                // Complete coverage and explicit absence proves a zero
                // contribution for this member world.
                continue;
            }

            if (!publication.TemperatureAmountsByResourceTag.TryGetValue(
                    resourceTag,
                    out IReadOnlyList<ReferenceTemperatureAmount>?
                        memberTemperatureAmounts))
            {
                // Presence without the current tag series is the pending state.
                return ReferenceWorldResourceTemperatureAmountAvailability
                    .InventoryIncomplete();
            }

            completeAmount += SumAllowedAmountsDirectly(
                memberTemperatureAmounts,
                constraint);
        }

        return foundParentMember
            ? ReferenceWorldResourceTemperatureAmountAvailability.Complete(
                completeAmount)
            : ReferenceWorldResourceTemperatureAmountAvailability
                .InventoryIncomplete();
    }

    private static bool IsOlderThanCurrentPublication(
        WorldInventoryCollectionGeneration candidateCollectionGeneration,
        ReferenceWorldPublication publication) =>
        publication.HasPublication &&
        candidateCollectionGeneration.Value <
            publication.CollectionGeneration.Value;

    private static IReadOnlyList<ReferenceTemperatureAmount>
        CopyTemperatureAmounts(
            IReadOnlyList<ReferenceTemperatureAmount> temperatureAmounts)
    {
        var copiedTemperatureAmounts =
            new ReferenceTemperatureAmount[temperatureAmounts.Count];
        for (int amountIndex = 0;
             amountIndex < temperatureAmounts.Count;
             amountIndex++)
        {
            copiedTemperatureAmounts[amountIndex] =
                temperatureAmounts[amountIndex];
        }

        return Array.AsReadOnly(copiedTemperatureAmounts);
    }

    private static float SumAllowedAmountsDirectly(
        IReadOnlyList<ReferenceTemperatureAmount> temperatureAmounts,
        DeliveryTemperatureConstraint constraint)
    {
        float allowedAmount = 0.0f;
        foreach (ReferenceTemperatureAmount temperatureAmount in
                 temperatureAmounts)
        {
            // Repeat truncation and boundary comparisons here instead of calling
            // production constraint/bucket/series operations or even the sibling
            // reference helper. This keeps inventory proof and temperature-sum
            // proof independently executable.
            int truncatedKelvin = (int)temperatureAmount.TemperatureKelvin;
            if (constraint.MinimumInclusiveKelvin <= truncatedKelvin &&
                truncatedKelvin < constraint.MaximumExclusiveKelvin)
            {
                allowedAmount += temperatureAmount.Amount;
            }
        }

        return allowedAmount;
    }

    private enum ReferenceWorldPublicationStrength
    {
        NoCoverage,
        ResourceTagCoverage,
        CompleteWorld
    }

    private sealed class ReferenceWorldPublication
    {
        internal ReferenceWorldPublication(int parentWorldId)
        {
            ParentWorldId = parentWorldId;
            PresentResourceTags = [];
            TemperatureAmountsByResourceTag = [];
        }

        internal int ParentWorldId { get; set; }

        internal bool HasPublication { get; set; }

        internal WorldInventoryCollectionGeneration CollectionGeneration
        {
            get;
            set;
        }

        internal ReferenceWorldPublicationStrength PublicationStrength
        {
            get;
            set;
        }

        internal HashSet<Tag> PresentResourceTags { get; set; }

        internal Dictionary<Tag, IReadOnlyList<ReferenceTemperatureAmount>>
            TemperatureAmountsByResourceTag
        {
            get;
            set;
        }
    }
}
