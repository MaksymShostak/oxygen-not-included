#nullable enable

using System;
using System.Globalization;

namespace DeliveryTemperatureLimit
{
    internal static class OptionsTemperatureUnits
    {
        internal static string Current =>
            GameUtil.temperatureUnit.ToString().ToLowerInvariant();

        internal static string Symbol(string unit) => unit == "celsius" ? "\u00B0C" :
            unit == "fahrenheit" ? "\u00B0F" : unit == "kelvin" ? "K" :
            throw new ArgumentException("Unsupported temperature unit.", nameof(unit));

        internal static float ToKelvin(int value, string unit) =>
            GameUtil.GetTemperatureConvertedToKelvin(value,
                ParseGameUnit(GameUtil.temperatureUnit, unit));

        internal static string Format(int kelvin, string unit) =>
            Math.Round(GameUtil.GetTemperatureConvertedFromKelvin(kelvin,
                ParseGameUnit(GameUtil.temperatureUnit, unit)))
                .ToString(CultureInfo.CurrentCulture);

        private static T ParseGameUnit<T>(T current, string unit) where T : struct
        {
            _ = current; // Infers Klei's enum type without serializing its ordinals.
            _ = Symbol(unit);
            return (T)Enum.Parse(typeof(T), unit, true);
        }
    }
}
