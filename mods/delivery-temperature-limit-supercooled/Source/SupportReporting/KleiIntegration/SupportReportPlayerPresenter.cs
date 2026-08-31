#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    internal static class SupportReportPlayerPresenter
    {
        internal static void PresentSuccess(
            string finalReportPath,
            string compactSummary,
            SupportIssueUrl issueUrl)
        {
            if (string.IsNullOrWhiteSpace(finalReportPath))
            {
                throw new ArgumentException(
                    "A successful report presentation requires its local path.",
                    nameof(finalReportPath));
            }

            if (compactSummary == null)
            {
                throw new ArgumentNullException(nameof(compactSummary));
            }

            if (issueUrl == null)
            {
                throw new ArgumentNullException(nameof(issueUrl));
            }

            TryPresentationStep(
                "DTL-SUPPORT-CLIPBOARD-FAILED",
                "The support summary could not be copied to the clipboard.",
                () => GUIUtility.systemCopyBuffer = compactSummary);
            TryPresentationStep(
                "DTL-SUPPORT-FOLDER-OPEN-FAILED",
                "The support-report folder could not be opened automatically.",
                () =>
                {
                    string reportDirectory = Path.GetDirectoryName(
                            finalReportPath) ??
                        throw new InvalidOperationException(
                            "The generated report path had no parent directory.");
                    Application.OpenURL(
                        new Uri(reportDirectory).AbsoluteUri);
                });
            TryPresentationStep(
                "DTL-SUPPORT-ISSUE-FORM-OPEN-FAILED",
                "The GitHub bug form could not be opened automatically.",
                () => Application.OpenURL(issueUrl.Value));

            TryPresentationStep(
                "DTL-SUPPORT-DIALOG-FAILED",
                "The support-report success dialog could not be displayed.",
                () => KMod.Manager.Dialog(
                    null,
                    "Temperature Limit support report created",
                    "The report remains local until you attach it. Review it " +
                    "before uploading.\n\n" +
                    finalReportPath));
        }

        internal static void PresentFailure(
            string playerSafeMessage,
            Exception exception)
        {
            string safeMessage = string.IsNullOrWhiteSpace(playerSafeMessage)
                ? "The support report could not be created."
                : playerSafeMessage;
            try
            {
                Debug.LogError(
                    "Delivery Temperature Limit support report failed: " +
                    exception);
            }
            catch (Exception)
            {
            }

            try
            {
                KMod.Manager.Dialog(
                    null,
                    "Temperature Limit support report failed",
                    safeMessage +
                    "\n\nYou can still report the problem at:\n" +
                    SupportReportLimits.BugIssueOrigin +
                    "?template=" +
                    SupportReportLimits.BugIssueTemplate);
            }
            catch (Exception)
            {
                // Never propagate a presentation failure through the PLib action.
            }
        }

        private static void TryPresentationStep(
            string diagnosticCode,
            string failureMessage,
            System.Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                DeliveryTemperatureSupportReporter.Record(
                    diagnosticCode,
                    SupportDiagnosticSeverity.Warning,
                    failureMessage,
                    exception);
            }
        }
    }
}
