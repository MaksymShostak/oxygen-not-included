#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Full kind-aware temperature identity used by pickup grouping and ordering.
    /// </summary>
    internal readonly struct TemperatureEligibilityClassKey :
        IEquatable<TemperatureEligibilityClassKey>,
        IComparable<TemperatureEligibilityClassKey>
    {
        private static readonly TemperatureEligibilityClassKey
            NoTemperatureDistinctionInstance =
                new TemperatureEligibilityClassKey(
                    TemperatureEligibilityClassificationKind
                        .NoTemperatureDistinction,
                    partitionDefinitionId: 0,
                    intervalOrdinal: 0,
                    exactTemperatureDecisionBucket: default);
        private static readonly TemperatureEligibilityClassKey
            MissingPrimaryElementInstance =
                new TemperatureEligibilityClassKey(
                    TemperatureEligibilityClassificationKind
                        .MissingPrimaryElement,
                    partitionDefinitionId: 0,
                    intervalOrdinal: 0,
                    exactTemperatureDecisionBucket: default);

        private TemperatureEligibilityClassKey(
            TemperatureEligibilityClassificationKind classificationKind,
            int partitionDefinitionId,
            int intervalOrdinal,
            TemperatureDecisionBucket exactTemperatureDecisionBucket)
        {
            ClassificationKind = classificationKind;
            PartitionDefinitionId = partitionDefinitionId;
            IntervalOrdinal = intervalOrdinal;
            ExactTemperatureDecisionBucket = exactTemperatureDecisionBucket;
        }

        internal TemperatureEligibilityClassificationKind ClassificationKind
        {
            get;
        }

        internal int PartitionDefinitionId { get; }

        internal int IntervalOrdinal { get; }

        internal TemperatureDecisionBucket ExactTemperatureDecisionBucket { get; }

        internal static TemperatureEligibilityClassKey NoTemperatureDistinction() =>
            NoTemperatureDistinctionInstance;

        internal static TemperatureEligibilityClassKey OptimizedPartitionInterval(
            int partitionDefinitionId,
            int intervalOrdinal)
        {
            if (partitionDefinitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(partitionDefinitionId),
                    partitionDefinitionId,
                    "An optimized partition definition ID must be positive.");
            }

            if (intervalOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intervalOrdinal),
                    intervalOrdinal,
                    "An optimized partition interval ordinal cannot be negative.");
            }

            return new TemperatureEligibilityClassKey(
                TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval,
                partitionDefinitionId,
                intervalOrdinal,
                exactTemperatureDecisionBucket: default);
        }

        internal static TemperatureEligibilityClassKey ExactDecisionBucket(
            TemperatureDecisionBucket temperatureDecisionBucket) =>
            new TemperatureEligibilityClassKey(
                TemperatureEligibilityClassificationKind
                    .ExactTemperatureDecisionBucket,
                partitionDefinitionId: 0,
                intervalOrdinal: 0,
                exactTemperatureDecisionBucket: temperatureDecisionBucket);

        internal static TemperatureEligibilityClassKey MissingPrimaryElement() =>
            MissingPrimaryElementInstance;

        public bool Equals(TemperatureEligibilityClassKey other)
        {
            if (ClassificationKind != other.ClassificationKind)
            {
                return false;
            }

            switch (ClassificationKind)
            {
                case TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval:
                    return PartitionDefinitionId == other.PartitionDefinitionId &&
                        IntervalOrdinal == other.IntervalOrdinal;

                case TemperatureEligibilityClassificationKind
                    .ExactTemperatureDecisionBucket:
                    return ExactTemperatureDecisionBucket.Equals(
                        other.ExactTemperatureDecisionBucket);

                case TemperatureEligibilityClassificationKind
                    .NoTemperatureDistinction:
                case TemperatureEligibilityClassificationKind
                    .MissingPrimaryElement:
                    return true;

                default:
                    // No factory can create an unknown kind. Keeping unknown values
                    // unequal prevents a corrupted/default-expanded value from
                    // silently collapsing into another pickup grouping class.
                    return false;
            }
        }

        public override bool Equals(object? obj) =>
            obj is TemperatureEligibilityClassKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)ClassificationKind;
                switch (ClassificationKind)
                {
                    case TemperatureEligibilityClassificationKind
                        .OptimizedPartitionInterval:
                        hashCode = (hashCode * 397) ^ PartitionDefinitionId;
                        return (hashCode * 397) ^ IntervalOrdinal;

                    case TemperatureEligibilityClassificationKind
                        .ExactTemperatureDecisionBucket:
                        return (hashCode * 397) ^
                            ExactTemperatureDecisionBucket.GetHashCode();

                    default:
                        return hashCode;
                }
            }
        }

        public int CompareTo(TemperatureEligibilityClassKey other)
        {
            int kindComparison = ((int)ClassificationKind).CompareTo(
                (int)other.ClassificationKind);
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            switch (ClassificationKind)
            {
                case TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval:
                    int definitionComparison = PartitionDefinitionId.CompareTo(
                        other.PartitionDefinitionId);
                    return definitionComparison != 0
                        ? definitionComparison
                        : IntervalOrdinal.CompareTo(other.IntervalOrdinal);

                case TemperatureEligibilityClassificationKind
                    .ExactTemperatureDecisionBucket:
                    return ExactTemperatureDecisionBucket.CompareTo(
                        other.ExactTemperatureDecisionBucket);

                case TemperatureEligibilityClassificationKind
                    .NoTemperatureDistinction:
                case TemperatureEligibilityClassificationKind
                    .MissingPrimaryElement:
                    return 0;

                default:
                    // Unknown kinds already sort deterministically by their numeric
                    // kind value. Equal unknown values have no sanctioned payload.
                    return 0;
            }
        }
    }
}
