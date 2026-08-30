#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Identifies the exact immutable evidence produced by one instrumented
    /// FastTrack world-inventory update.
    /// </summary>
    internal enum FastTrackWorldInventoryPublicationKind
    {
        CompleteWorldAmounts,
        ResourceTagCoverageAndTemperatureSeries,
        ResourceTemperatureSeries,
        ResourceTagCoverageOnly
    }
}
