#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable, discriminated result of exactly one FastTrack world-inventory
    /// publication session.
    /// </summary>
    /// <remarks>
    /// The explicit kind prevents callers from interpreting an accidental
    /// combination of nullable payloads. Complete-world evidence can never coexist
    /// with incremental evidence, while coverage and a single-tag series coexist
    /// only in the one result kind that names both.
    /// </remarks>
    internal sealed class FastTrackWorldInventoryPublicationResult
    {
        private readonly CompleteWorldResourceTemperatureAmounts?
            completeWorldResourceTemperatureAmounts;
        private readonly WorldResourceTagCoverage? worldResourceTagCoverage;
        private readonly WorldResourceTemperatureSeriesPublication?
            worldResourceTemperatureSeriesPublication;

        private FastTrackWorldInventoryPublicationResult(
            FastTrackWorldInventoryPublicationKind kind,
            CompleteWorldResourceTemperatureAmounts?
                completeWorldResourceTemperatureAmounts,
            WorldResourceTagCoverage? worldResourceTagCoverage,
            WorldResourceTemperatureSeriesPublication?
                worldResourceTemperatureSeriesPublication)
        {
            bool hasCompleteWorldAmounts =
                completeWorldResourceTemperatureAmounts != null;
            bool hasCoverage = worldResourceTagCoverage != null;
            bool hasResourceTemperatureSeries =
                worldResourceTemperatureSeriesPublication.HasValue;
            bool payloadsMatchKind;
            switch (kind)
            {
                case FastTrackWorldInventoryPublicationKind
                    .CompleteWorldAmounts:
                    payloadsMatchKind = hasCompleteWorldAmounts &&
                        !hasCoverage &&
                        !hasResourceTemperatureSeries;
                    break;

                case FastTrackWorldInventoryPublicationKind
                    .ResourceTagCoverageAndTemperatureSeries:
                    payloadsMatchKind = !hasCompleteWorldAmounts &&
                        hasCoverage &&
                        hasResourceTemperatureSeries;
                    break;

                case FastTrackWorldInventoryPublicationKind
                    .ResourceTemperatureSeries:
                    payloadsMatchKind = !hasCompleteWorldAmounts &&
                        !hasCoverage &&
                        hasResourceTemperatureSeries;
                    break;

                case FastTrackWorldInventoryPublicationKind
                    .ResourceTagCoverageOnly:
                    payloadsMatchKind = !hasCompleteWorldAmounts &&
                        hasCoverage &&
                        !hasResourceTemperatureSeries;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unknown FastTrack world-inventory publication kind.");
            }

            if (!payloadsMatchKind)
            {
                throw new ArgumentException(
                    "The FastTrack world-inventory publication kind does not " +
                    "match its complete-world, coverage, and single-tag payloads.",
                    nameof(kind));
            }

            Kind = kind;
            this.completeWorldResourceTemperatureAmounts =
                completeWorldResourceTemperatureAmounts;
            this.worldResourceTagCoverage = worldResourceTagCoverage;
            this.worldResourceTemperatureSeriesPublication =
                worldResourceTemperatureSeriesPublication;
        }

        internal FastTrackWorldInventoryPublicationKind Kind { get; }

        internal static FastTrackWorldInventoryPublicationResult
            ForCompleteWorldAmounts(
                CompleteWorldResourceTemperatureAmounts completeWorldAmounts)
        {
            if (completeWorldAmounts == null)
            {
                throw new ArgumentNullException(nameof(completeWorldAmounts));
            }

            return new FastTrackWorldInventoryPublicationResult(
                FastTrackWorldInventoryPublicationKind.CompleteWorldAmounts,
                completeWorldAmounts,
                worldResourceTagCoverage: null,
                worldResourceTemperatureSeriesPublication: null);
        }

        internal static FastTrackWorldInventoryPublicationResult
            ForResourceTagCoverageAndTemperatureSeries(
                WorldResourceTagCoverage resourceTagCoverage,
                WorldResourceTemperatureSeriesPublication
                    resourceTemperatureSeries)
        {
            if (resourceTagCoverage == null)
            {
                throw new ArgumentNullException(nameof(resourceTagCoverage));
            }

            return new FastTrackWorldInventoryPublicationResult(
                FastTrackWorldInventoryPublicationKind
                    .ResourceTagCoverageAndTemperatureSeries,
                completeWorldResourceTemperatureAmounts: null,
                resourceTagCoverage,
                resourceTemperatureSeries);
        }

        internal static FastTrackWorldInventoryPublicationResult
            ForResourceTemperatureSeries(
                WorldResourceTemperatureSeriesPublication
                    resourceTemperatureSeries) =>
            new FastTrackWorldInventoryPublicationResult(
                FastTrackWorldInventoryPublicationKind
                    .ResourceTemperatureSeries,
                completeWorldResourceTemperatureAmounts: null,
                worldResourceTagCoverage: null,
                resourceTemperatureSeries);

        internal static FastTrackWorldInventoryPublicationResult
            ForResourceTagCoverageOnly(
                WorldResourceTagCoverage resourceTagCoverage)
        {
            if (resourceTagCoverage == null)
            {
                throw new ArgumentNullException(nameof(resourceTagCoverage));
            }

            return new FastTrackWorldInventoryPublicationResult(
                FastTrackWorldInventoryPublicationKind.ResourceTagCoverageOnly,
                completeWorldResourceTemperatureAmounts: null,
                resourceTagCoverage,
                worldResourceTemperatureSeriesPublication: null);
        }

        internal bool TryGetCompleteWorldResourceTemperatureAmounts(
            out CompleteWorldResourceTemperatureAmounts completeWorldAmounts)
        {
            if (completeWorldResourceTemperatureAmounts != null)
            {
                completeWorldAmounts =
                    completeWorldResourceTemperatureAmounts;
                return true;
            }

            completeWorldAmounts = null!;
            return false;
        }

        internal bool TryGetWorldResourceTagCoverage(
            out WorldResourceTagCoverage resourceTagCoverage)
        {
            if (worldResourceTagCoverage != null)
            {
                resourceTagCoverage = worldResourceTagCoverage;
                return true;
            }

            resourceTagCoverage = null!;
            return false;
        }

        internal bool TryGetWorldResourceTemperatureSeriesPublication(
            out WorldResourceTemperatureSeriesPublication
                resourceTemperatureSeries)
        {
            if (worldResourceTemperatureSeriesPublication.HasValue)
            {
                resourceTemperatureSeries =
                    worldResourceTemperatureSeriesPublication.Value;
                return true;
            }

            resourceTemperatureSeries =
                default(WorldResourceTemperatureSeriesPublication);
            return false;
        }
    }
}
