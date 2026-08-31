#nullable enable

using System;
using System.Globalization;

namespace DeliveryTemperatureLimit
{
    internal static class SupportReportFileName
    {
        internal static string Create(
            DateTimeOffset generatedAtUtc,
            Guid reportId)
        {
            if (generatedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The report filename timestamp must use the UTC offset.",
                    nameof(generatedAtUtc));
            }

            if (reportId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A report filename requires a non-empty report ID.",
                    nameof(reportId));
            }

            return "temperature-limit-support-" +
                generatedAtUtc.ToString(
                    "yyyyMMdd'T'HHmmssfff'Z'",
                    CultureInfo.InvariantCulture) +
                "-" +
                reportId.ToString("N").Substring(0, 8) +
                ".json";
        }
    }
}
