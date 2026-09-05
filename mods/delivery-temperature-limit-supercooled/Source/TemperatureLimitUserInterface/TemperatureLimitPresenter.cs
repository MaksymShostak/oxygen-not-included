#nullable enable

using System;
using System.Globalization;

namespace DeliveryTemperatureLimit
{
    internal enum TemperatureValidationSeverity
    {
        Normal,
        Warning,
        Error
    }

    internal sealed class TemperatureValidationResult
    {
        private TemperatureValidationResult(
            bool isValid,
            TemperatureBounds bounds,
            string message,
            TemperatureValidationSeverity severity)
        {
            IsValid = isValid;
            Bounds = bounds;
            Message = message;
            Severity = severity;
        }

        public bool IsValid { get; }

        public TemperatureBounds Bounds { get; }

        public string Message { get; }

        public TemperatureValidationSeverity Severity { get; }

        public static TemperatureValidationResult Valid(
            TemperatureBounds bounds,
            string message,
            TemperatureValidationSeverity severity) =>
            new TemperatureValidationResult(true, bounds, message, severity);

        public static TemperatureValidationResult Invalid(string message) =>
            new TemperatureValidationResult(
                false,
                TemperatureBounds.Unbounded,
                message,
                TemperatureValidationSeverity.Error);
    }

    /// <summary>
    /// Encapsulates temperature parsing, unit conversion, physical bound checking,
    /// and user-facing status string generation for the delivery temperature editor.
    /// </summary>
    internal static class TemperatureLimitPresenter
    {
        private static readonly string[] KnownUnitSuffixes = new[]
        {
            "°c",
            "c",
            "°f",
            "f",
            "k"
        };

        public static string FormatInputText(int? kelvin)
        {
            if (!kelvin.HasValue)
            {
                return string.Empty;
            }

            return GameUtil.GetFormattedTemperature(
                kelvin.Value,
                GameUtil.TimeSlice.None,
                GameUtil.TemperatureInterpretation.Absolute,
                false,
                true).Trim();
        }

        public static string FormatTemperatureWithUnit(int kelvin) =>
            GameUtil.GetFormattedTemperature(
                kelvin,
                GameUtil.TimeSlice.None,
                GameUtil.TemperatureInterpretation.Absolute,
                true,
                true);

        public static string GetCurrentUnitSuffix() =>
            GameUtil.GetTemperatureUnitSuffix().Trim();

        public static string GetRangeDescription(TemperatureBounds bounds)
        {
            if (bounds.IsUnbounded)
            {
                return STRINGS.TEMPERATURELIMIT.STATUS_DISABLED.ToString();
            }

            if (bounds.LowerKelvin.HasValue && bounds.UpperKelvin.HasValue)
            {
                if (bounds.IsEqualBounds)
                {
                    return STRINGS.TEMPERATURELIMIT.WARNING_EMPTY.ToString();
                }

                return string.Format(
                    STRINGS.TEMPERATURELIMIT.STATUS_RANGE.ToString(),
                    FormatTemperatureWithUnit(bounds.LowerKelvin.Value),
                    FormatTemperatureWithUnit(bounds.UpperKelvin.Value));
            }

            if (bounds.LowerKelvin.HasValue)
            {
                return string.Format(
                    STRINGS.TEMPERATURELIMIT.STATUS_LOW_ONLY.ToString(),
                    FormatTemperatureWithUnit(bounds.LowerKelvin.Value));
            }

            if (bounds.UpperKelvin.HasValue)
            {
                return string.Format(
                    STRINGS.TEMPERATURELIMIT.STATUS_HIGH_ONLY.ToString(),
                    FormatTemperatureWithUnit(bounds.UpperKelvin.Value));
            }

            return STRINGS.TEMPERATURELIMIT.STATUS_DISABLED.ToString();
        }

        public static TemperatureValidationResult ValidateAndParse(
            string? lowText,
            string? highText)
        {
            if (!TryParseEndpoint(lowText, out int? lowKelvin, out string? lowError))
            {
                return TemperatureValidationResult.Invalid(lowError!);
            }

            if (!TryParseEndpoint(highText, out int? highKelvin, out string? highError))
            {
                return TemperatureValidationResult.Invalid(highError!);
            }

            if (lowKelvin.HasValue && highKelvin.HasValue)
            {
                if (lowKelvin.Value > highKelvin.Value)
                {
                    return TemperatureValidationResult.Invalid(
                        STRINGS.TEMPERATURELIMIT.ERROR_REVERSED.ToString());
                }

                if (lowKelvin.Value == highKelvin.Value)
                {
                    TemperatureBounds equalBounds = new TemperatureBounds(lowKelvin, highKelvin);
                    return TemperatureValidationResult.Valid(
                        equalBounds,
                        STRINGS.TEMPERATURELIMIT.WARNING_EMPTY.ToString(),
                        TemperatureValidationSeverity.Warning);
                }

                TemperatureBounds bounded = new TemperatureBounds(lowKelvin, highKelvin);
                return TemperatureValidationResult.Valid(
                    bounded,
                    GetRangeDescription(bounded),
                    TemperatureValidationSeverity.Normal);
            }

            if (lowKelvin.HasValue)
            {
                TemperatureBounds lowOnly = new TemperatureBounds(lowKelvin, null);
                return TemperatureValidationResult.Valid(
                    lowOnly,
                    GetRangeDescription(lowOnly),
                    TemperatureValidationSeverity.Normal);
            }

            if (highKelvin.HasValue)
            {
                TemperatureBounds highOnly = new TemperatureBounds(null, highKelvin);
                return TemperatureValidationResult.Valid(
                    highOnly,
                    GetRangeDescription(highOnly),
                    TemperatureValidationSeverity.Normal);
            }

            return TemperatureValidationResult.Valid(
                TemperatureBounds.Unbounded,
                STRINGS.TEMPERATURELIMIT.STATUS_DISABLED.ToString(),
                TemperatureValidationSeverity.Normal);
        }

        private static bool TryParseEndpoint(
            string? text,
            out int? kelvin,
            out string? errorMessage)
        {
            kelvin = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                kelvin = null;
                return true;
            }

            string normalized = text.Trim();

            string currentSuffix = GetCurrentUnitSuffix();
            if (currentSuffix.Length > 0 &&
                normalized.EndsWith(currentSuffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    0,
                    normalized.Length - currentSuffix.Length).Trim();
            }

            foreach (string knownSuffix in KnownUnitSuffixes)
            {
                if (normalized.EndsWith(knownSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(
                        0,
                        normalized.Length - knownSuffix.Length).Trim();
                    break;
                }
            }

            if (normalized.Length == 0 ||
                normalized == "-" ||
                normalized == "+")
            {
                errorMessage = STRINGS.TEMPERATURELIMIT.ERROR_NUMBER.ToString();
                return false;
            }

            if (!int.TryParse(
                    normalized,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int displayedTemperature) &&
                !int.TryParse(
                    normalized,
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out displayedTemperature))
            {
                errorMessage = STRINGS.TEMPERATURELIMIT.ERROR_NUMBER.ToString();
                return false;
            }

            float convertedKelvin = GameUtil.GetTemperatureConvertedToKelvin(
                displayedTemperature);
            int roundedKelvin = (int)Math.Round(convertedKelvin);

            if (roundedKelvin < OniStorableTemperatureBounds.MinimumTemperatureKelvin ||
                roundedKelvin > OniStorableTemperatureBounds.MaximumTemperatureKelvin)
            {
                errorMessage = string.Format(
                    STRINGS.TEMPERATURELIMIT.ERROR_RANGE.ToString(),
                    FormatTemperatureWithUnit(OniStorableTemperatureBounds.MinimumTemperatureKelvin),
                    FormatTemperatureWithUnit(OniStorableTemperatureBounds.MaximumTemperatureKelvin));
                return false;
            }

            kelvin = roundedKelvin;
            return true;
        }
    }
}
