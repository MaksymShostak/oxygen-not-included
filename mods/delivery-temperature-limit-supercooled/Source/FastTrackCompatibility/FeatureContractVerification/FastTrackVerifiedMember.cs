#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Gives every reflected FastTrack member a stable semantic role. Adapters
    /// consume these roles instead of repeating name-based reflection.
    /// </summary>
    internal enum FastTrackVerifiedMember
    {
        BackgroundWorldInventoryRunUpdate,
        BackgroundWorldInventorySumTotal,
        BackgroundWorldInventoryFirstUpdateField,
        BackgroundWorldInventoryUpdateIndexField,
        BackgroundWorldInventoryWorldContainerField,
        BackgroundWorldInventoryWorldInventoryField,
        WorldInventoryReplacementPrefix,
        WorldInventoryRemovedFetchablePrefix,
        PickupGroupingBeforeUpdatePickupsPrefix,
        PickupGroupingAddItem,
        PickupGroupingKeyConstructor,
        PickupGroupingKeyTypedEquality,
        DirectDeliveryEligibilityComparator,
        DirectDeliveryEligibilityReplacementPrefix
    }
}
