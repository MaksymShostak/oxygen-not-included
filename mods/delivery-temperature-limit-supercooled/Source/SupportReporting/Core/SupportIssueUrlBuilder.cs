#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    internal sealed class SupportIssueUrl
    {
        internal SupportIssueUrl(
            string value,
            bool summaryWasShortened)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            SummaryWasShortened = summaryWasShortened;
        }

        internal string Value { get; }

        internal bool SummaryWasShortened { get; }
    }

    internal static class SupportIssueUrlBuilder
    {
        private const string ShortenedMarker =
            "… [summary shortened; see attached report]";

        private static readonly string QueryPrefix =
            SupportReportLimits.BugIssueOrigin +
            "?template=" +
            SupportReportLimits.BugIssueTemplate +
            "&diagnostics=";

        internal static SupportIssueUrl Create(string diagnosticSummary)
        {
            if (diagnosticSummary == null)
            {
                throw new ArgumentNullException(nameof(diagnosticSummary));
            }

            string completeValue = CreateValue(diagnosticSummary);
            if (completeValue.Length <=
                SupportReportLimits.MaximumIssueUrlCharacters)
            {
                return new SupportIssueUrl(
                    completeValue,
                    summaryWasShortened: false);
            }

            int low = 0;
            int high = diagnosticSummary.Length;
            while (low < high)
            {
                int candidateLength = low + (high - low + 1) / 2;
                string candidate = CreateShortenedSummary(
                    diagnosticSummary,
                    candidateLength);
                if (CreateValue(candidate).Length <=
                    SupportReportLimits.MaximumIssueUrlCharacters)
                {
                    low = candidateLength;
                }
                else
                {
                    high = candidateLength - 1;
                }
            }

            string shortenedSummary = CreateShortenedSummary(
                diagnosticSummary,
                low);
            string shortenedValue = CreateValue(shortenedSummary);
            if (shortenedValue.Length >
                SupportReportLimits.MaximumIssueUrlCharacters)
            {
                throw new InvalidOperationException(
                    "The fixed support issue URL and shortening marker exceed the configured URL limit.");
            }

            return new SupportIssueUrl(
                shortenedValue,
                summaryWasShortened: true);
        }

        private static string CreateValue(string summary) =>
            QueryPrefix + Uri.EscapeDataString(summary);

        private static string CreateShortenedSummary(
            string summary,
            int prefixLength)
        {
            int safeLength = prefixLength;
            if (safeLength > 0 &&
                safeLength < summary.Length &&
                char.IsHighSurrogate(summary[safeLength - 1]) &&
                char.IsLowSurrogate(summary[safeLength]))
            {
                safeLength--;
            }

            return summary.Substring(0, safeLength) + ShortenedMarker;
        }
    }
}
