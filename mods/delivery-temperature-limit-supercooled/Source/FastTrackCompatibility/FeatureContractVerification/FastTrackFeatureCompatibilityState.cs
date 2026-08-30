#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Describes whether one FastTrack replacement participates in the loaded
    /// game and, when active, whether its exact contract is safe to extend.
    /// </summary>
    internal enum FastTrackFeatureCompatibilityState
    {
        ModNotLoaded,
        ReplacementInactive,
        Ready,
        Incompatible
    }
}
