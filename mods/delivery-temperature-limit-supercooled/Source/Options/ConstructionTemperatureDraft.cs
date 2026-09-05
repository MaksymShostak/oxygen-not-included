#nullable enable

using System;
using System.Globalization;

namespace DeliveryTemperatureLimit
{
    internal enum ConstructionRangeError
    {
        None,
        LowerNumber,
        UpperNumber,
        LowerRange,
        UpperRange,
        EmptyRange
    }

    /// <summary>
    /// Pure draft validation. Retaining an unchanged display string retains its
    /// original Kelvin value. Opening the screen or saving an unrelated checkbox
    /// never runs a display value back through temperature conversion.
    /// </summary>
    internal sealed class ConstructionTemperatureDraft
    {
        private readonly Func<int, float> toKelvin;
        private readonly Func<int, string> format;
        private string originalLower = "";
        private string originalUpper = "";
        private int originalLowerKelvin;
        private int originalUpperKelvin;

        internal ConstructionTemperatureDraft(
            int lowerKelvin, int upperKelvin,
            Func<int, float> toKelvin, Func<int, string> format)
        {
            this.toKelvin = toKelvin ?? throw new ArgumentNullException(nameof(toKelvin));
            this.format = format ?? throw new ArgumentNullException(nameof(format));
            Reset(lowerKelvin, upperKelvin);
        }

        internal string LowerText { get; set; } = "";
        internal string UpperText { get; set; } = "";
        internal bool IsDirty => LowerText != originalLower || UpperText != originalUpper;

        internal void Reset(int lowerKelvin, int upperKelvin)
        {
            originalLowerKelvin = lowerKelvin;
            originalUpperKelvin = upperKelvin;
            LowerText = originalLower = format(lowerKelvin);
            UpperText = originalUpper = format(upperKelvin);
        }

        internal void Revert()
        {
            LowerText = originalLower;
            UpperText = originalUpper;
        }

        internal ConstructionRangeError Validate(out int lower, out int upper)
        {
            lower = upper = 0;
            ConstructionRangeError error = Parse(
                LowerText, originalLower, originalLowerKelvin, true, out lower);
            if (error != ConstructionRangeError.None)
                return error;
            error = Parse(UpperText, originalUpper, originalUpperKelvin, false, out upper);
            if (error != ConstructionRangeError.None)
                return error;
            // Construction defaults must describe a nonempty range. This does not
            // alter the existing building-widget policy or simulation predicate.
            return lower < upper ? ConstructionRangeError.None :
                ConstructionRangeError.EmptyRange;
        }

        private ConstructionRangeError Parse(
            string text, string originalText, int originalKelvin,
            bool lower, out int kelvin)
        {
            kelvin = originalKelvin;
            if (text != originalText)
            {
                if (!int.TryParse(text, NumberStyles.Integer,
                        CultureInfo.CurrentCulture, out int displayed) &&
                    !int.TryParse(text, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out displayed))
                    return lower ? ConstructionRangeError.LowerNumber :
                        ConstructionRangeError.UpperNumber;

                // Check before narrowing: an overflowing conversion must not be
                // clamped into an apparently valid boundary.
                double rounded = Math.Round(toKelvin(displayed));
                if (double.IsNaN(rounded) || double.IsInfinity(rounded) ||
                    rounded < OniStorableTemperatureBounds.MinimumTemperatureKelvin ||
                    rounded > OniStorableTemperatureBounds.MaximumTemperatureKelvin)
                    return lower ? ConstructionRangeError.LowerRange :
                        ConstructionRangeError.UpperRange;
                kelvin = (int)rounded;
            }
            if (kelvin < OniStorableTemperatureBounds.MinimumTemperatureKelvin ||
                kelvin > OniStorableTemperatureBounds.MaximumTemperatureKelvin)
                return lower ? ConstructionRangeError.LowerRange :
                    ConstructionRangeError.UpperRange;
            return ConstructionRangeError.None;
        }
    }
}
