#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>Keeps a localized temperature value attached to its unit when wrapping.</summary>
    internal static class TemperatureDisplayText
    {
        public static string WithUnit(string number, string suffix) =>
            number + "\u00a0" + suffix.Trim();
    }
}
