#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Exact old and new parent identities produced by one catalog operation.
    /// Consumers use these captured values to invalidate both affected aggregates;
    /// they never re-read a mutable topology after the change.
    /// </summary>
    internal readonly struct WorldParentTopologyChange
    {
        internal WorldParentTopologyChange(
            bool hasChanged,
            int worldId,
            int? previousParentWorldId,
            int? currentParentWorldId)
        {
            HasChanged = hasChanged;
            WorldId = worldId;
            PreviousParentWorldId = previousParentWorldId;
            CurrentParentWorldId = currentParentWorldId;
        }

        internal bool HasChanged { get; }

        internal int WorldId { get; }

        internal int? PreviousParentWorldId { get; }

        internal int? CurrentParentWorldId { get; }
    }
}
