#nullable enable

using System;
using System.Collections.Generic;
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
            AppendExternalModIntegrations(
                summary,
                document.Runtime.ExternalModIntegrations);
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

        private static void AppendExternalModIntegrations(
            StringBuilder summary,
            IReadOnlyList<SupportExternalModIntegrationSnapshot> integrations)
        {
            summary.Append("- External mod integrations:");
            if (integrations.Count == 0)
            {
                summary.Append(" none\n");
                return;
            }

            summary.Append('\n');
            for (int integrationIndex = 0;
                 integrationIndex < integrations.Count;
                 integrationIndex++)
            {
                SupportExternalModIntegrationSnapshot integration =
                    integrations[integrationIndex];
                summary.Append("  - ");
                summary.Append(integration.DisplayName);
                summary.Append(": match `");
                summary.Append(integration.MatchState);
                summary.Append("`; capabilities: ");
                AppendCapabilityDispositions(
                    summary,
                    integration.Capabilities);
                summary.Append('\n');
            }
        }

        private static void AppendCapabilityDispositions(
            StringBuilder summary,
            IReadOnlyList<SupportExternalModCapabilitySnapshot> capabilities)
        {
            if (capabilities.Count == 0)
            {
                summary.Append("none");
                return;
            }

            for (int capabilityIndex = 0;
                 capabilityIndex < capabilities.Count;
                 capabilityIndex++)
            {
                if (capabilityIndex > 0)
                {
                    summary.Append(", ");
                }

                SupportExternalModCapabilitySnapshot capability =
                    capabilities[capabilityIndex];
                summary.Append('`');
                summary.Append(capability.CapabilityId);
                summary.Append('=');
                summary.Append(capability.Disposition);
                summary.Append('`');
            }
        }
    }
}
