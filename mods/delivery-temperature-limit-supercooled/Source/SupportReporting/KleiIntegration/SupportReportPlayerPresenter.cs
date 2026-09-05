#nullable enable

using System;
using System.IO;
using UnityEngine;
using Text = STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS;

namespace DeliveryTemperatureLimit
{
    /// <summary>UI-thread report state; all external actions require their own click.</summary>
    internal static class SupportReportPlayerPresenter
    {
        private static string? lastPath;
        private static string? lastSummary;
        internal static bool HasReport => lastPath != null;
        internal static string Message { get; private set; } = "";

        internal static void PresentSuccess(string finalReportPath, string compactSummary)
        {
            if (string.IsNullOrWhiteSpace(finalReportPath))
                throw new ArgumentException("A report path is required.", nameof(finalReportPath));
            if (compactSummary == null) throw new ArgumentNullException(nameof(compactSummary));
            lastPath = finalReportPath;
            lastSummary = compactSummary;
            Message = Text.STATUS_REPORT_CREATED + "\n" + finalReportPath;
        }

        internal static void PresentFailure(string playerSafeMessage, Exception exception)
        {
            // Leave the last successful report available after a subsequent failure.
            Message = playerSafeMessage;
            DeliveryTemperatureOptionsStore.Log("Local support report creation failed.", exception);
        }

        internal static void OpenLastReportFolder() => Run(() =>
        {
            if (lastPath == null) { Message = Text.STATUS_NO_REPORT; return; }
            if (!File.Exists(lastPath)) throw new FileNotFoundException("Report is not accessible.", lastPath);
            Application.OpenURL(new Uri(Path.GetDirectoryName(lastPath) ??
                throw new InvalidOperationException("Report has no parent directory.")).AbsoluteUri);
        });

        internal static void CopyLastReportSummary() => Run(() =>
        {
            if (lastSummary == null) { Message = Text.STATUS_NO_REPORT; return; }
            GUIUtility.systemCopyBuffer = lastSummary;
            Message = Text.STATUS_SUMMARY_COPIED;
        });

        internal static void OpenIssueForm() => Run(() => Application.OpenURL(
            SupportReportLimits.BugIssueOrigin + "?template=" +
            Uri.EscapeDataString(SupportReportLimits.BugIssueTemplate)));

        private static void Run(System.Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                Message = Text.STATUS_ACTION_FAILED;
                DeliveryTemperatureOptionsStore.Log("Explicit support action failed.", ex);
            }
        }
    }
}
