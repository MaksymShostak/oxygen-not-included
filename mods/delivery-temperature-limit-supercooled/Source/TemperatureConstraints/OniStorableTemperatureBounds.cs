#nullable enable

namespace DeliveryTemperatureLimit
{
    internal static class OniStorableTemperatureBounds
    {
        // Zero Kelvin is the mod's preserved configurable floor.
        internal const int MinimumTemperatureKelvin = 0;

        // ONI release changelist 744825 defines Sim.MaxTemperature as 10000f.
        // PrimaryElement.OnDeserialized and SimMessages.ModifyCell accept the maximum
        // inclusively. OniStorableTemperatureBoundsContractTests force a static review
        // of this compile-time boundary whenever the installed ONI binary changes.
        internal const int MaximumTemperatureKelvin = 10000;
    }
}
