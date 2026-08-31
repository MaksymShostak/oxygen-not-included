#nullable enable

using System;
using System.Text;

namespace DeliveryTemperatureLimit
{
    internal static class SupportReportSummaryRenderer
    {
        internal static string Render(
            SupportReportDocument document,
            string reportFileName)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string validatedFileName =
                SupportReportCollections.RequireNonBlank(
                    reportFileName,
                    nameof(reportFileName));
            var summary = new StringBuilder(512);
            summary.Append("### Temperature Limit diagnostics\n\n");
            summary.Append("- Report ID: `");
            summary.Append(document.ReportId);
            summary.Append("`\n");
            summary.Append("- Report file: `");
            summary.Append(validatedFileName);
            summary.Append("`\n");
            summary.Append("- ONI build / branch: `");
            summary.Append(GetFactValue(document.Game.Build));
            summary.Append("` / `");
            summary.Append(GetFactValue(document.Game.Branch));
            summary.Append("`\n");
            summary.Append("- Temperature Limit version: `");
            summary.Append(GetTemperatureLimitVersion(document));
            summary.Append("`\n");
            summary.Append("- Platform: `");
            summary.Append(GetFactValue(document.Game.Platform));
            summary.Append("`\n");
            summary.Append("- DLCs: ");
            AppendDlcIds(summary, document.Game.ActiveDlcs);
            summary.Append('\n');
            summary.Append("- FastTrack: `");
            summary.Append(
                document.Runtime.FastTrack == null
                    ? SupportReportLimits.UnavailableState
                    : document.Runtime.FastTrack.State);
            summary.Append("`\n");
            summary.Append("- Player.log: ");
            summary.Append(GetPlayerLogState(document.PlayerLog));
            return summary.ToString();
        }

        private static string GetFactValue(SupportReportFact fact) =>
            string.Equals(
                    fact.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal)
                ? fact.Value ?? SupportReportLimits.UnavailableState
                : SupportReportLimits.UnavailableState;

        private static string GetTemperatureLimitVersion(
            SupportReportDocument document)
        {
            string packageVersion = GetFactValue(
                document.TemperatureLimit.PackageVersion);
            return !string.Equals(
                    packageVersion,
                    SupportReportLimits.UnavailableState,
                    StringComparison.Ordinal)
                ? packageVersion
                : GetFactValue(document.TemperatureLimit.AssemblyVersion);
        }

        private static void AppendDlcIds(
            StringBuilder summary,
            SupportActiveDlcSnapshot activeDlcs)
        {
            if (string.Equals(
                    activeDlcs.State,
                    SupportReportLimits.UnavailableState,
                    StringComparison.Ordinal))
            {
                summary.Append("unavailable");
                return;
            }

            if (activeDlcs.Ids.Count == 0)
            {
                summary.Append("none");
                return;
            }

            for (int index = 0; index < activeDlcs.Ids.Count; index++)
            {
                if (index > 0)
                {
                    summary.Append(", ");
                }

                summary.Append('`');
                summary.Append(activeDlcs.Ids[index]);
                summary.Append('`');
            }
        }

        private static string GetPlayerLogState(
            SupportPlayerLogSnapshot? playerLog)
        {
            if (playerLog == null)
            {
                return "not included";
            }

            return string.Equals(
                    playerLog.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal)
                ? "included (bounded and best-effort redacted)"
                : "requested but unavailable";
        }
    }
}
