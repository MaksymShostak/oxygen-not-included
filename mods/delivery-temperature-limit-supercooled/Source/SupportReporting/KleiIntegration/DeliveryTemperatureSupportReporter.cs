#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    internal static class DeliveryTemperatureSupportReporter
    {
        private static readonly object SnapshotSynchronization = new object();
        private static readonly SupportDiagnosticBuffer DiagnosticBuffer =
            new SupportDiagnosticBuffer();

        private static KleiCurrentModSupportSnapshot? currentModSnapshot;
        private static KleiLoadedModsSupportSnapshot loadedModsSnapshot =
            KleiLoadedModsSupportSnapshot.Unpublished();

        internal static void Initialize(
            KMod.Mod currentMod,
            Assembly assembly)
        {
            try
            {
                KleiCurrentModSupportSnapshot captured =
                    KleiSupportReportSnapshotReader.CaptureCurrentMod(
                        currentMod,
                        assembly);
                lock (SnapshotSynchronization)
                {
                    currentModSnapshot = captured;
                }

                Record(
                    "DTL-SUPPORT-REPORTER-INITIALIZED",
                    SupportDiagnosticSeverity.Information,
                    "The local support reporter initialized.");
            }
            catch (Exception exception)
            {
                try
                {
                    Debug.LogError(
                        "Delivery Temperature Limit support reporter initialization failed. " +
                        exception);
                }
                catch (Exception)
                {
                }
            }
        }

        internal static void PublishLoadedMods(
            IReadOnlyList<KMod.Mod> loadedMods)
        {
            try
            {
                KleiLoadedModsSupportSnapshot captured =
                    KleiSupportReportSnapshotReader.CaptureLoadedMods(
                        loadedMods);
                lock (SnapshotSynchronization)
                {
                    loadedModsSnapshot = captured;
                }

                Record(
                    "DTL-SUPPORT-LOADED-MODS-PUBLISHED",
                    SupportDiagnosticSeverity.Information,
                    "The sanitized active loaded-mod snapshot was published.");
            }
            catch (Exception exception)
            {
                Record(
                    "DTL-SUPPORT-LOADED-MODS-FAILED",
                    SupportDiagnosticSeverity.Warning,
                    "The active loaded-mod snapshot could not be published.",
                    exception);
            }
        }

        internal static void Record(
            string code,
            SupportDiagnosticSeverity severity,
            string message,
            Exception? exception = null)
        {
            try
            {
                DiagnosticBuffer.Record(
                    code,
                    severity,
                    message,
                    DateTimeOffset.UtcNow,
                    exception);
            }
            catch (Exception recordingException)
            {
                try
                {
                    Debug.LogError(
                        "Delivery Temperature Limit could not retain support diagnostic '" +
                        code +
                        "': " +
                        recordingException);
                }
                catch (Exception)
                {
                }
            }

            MirrorToPlayerLog(code, severity, message, exception);
        }

        internal static void CreateStandardReport() =>
            CreateReport(SupportReportKind.Standard);

        internal static void CreateExtendedReport() =>
            CreateReport(SupportReportKind.ExtendedPlayerLog);

        private static void CreateReport(SupportReportKind reportKind)
        {
            try
            {
                KleiCurrentModSupportSnapshot currentMod;
                KleiLoadedModsSupportSnapshot loadedMods;
                lock (SnapshotSynchronization)
                {
                    currentMod = currentModSnapshot ??
                        throw new InvalidOperationException(
                            "The support reporter did not capture the current mod identity during startup.");
                    loadedMods = loadedModsSnapshot;
                }

                Guid reportId = Guid.NewGuid();
                DateTimeOffset generatedAtUtc = DateTimeOffset.UtcNow;
                string reportFileName = SupportReportFileName.Create(
                    generatedAtUtc,
                    reportId);
                SupportReportDocument document =
                    KleiSupportReportSnapshotReader.CreateDocument(
                        reportKind,
                        reportId,
                        generatedAtUtc,
                        currentMod,
                        loadedMods,
                        DeliveryTemperatureRuntimePatchInstaller
                            .CaptureSupportReportSnapshot(),
                        DiagnosticBuffer.CaptureSnapshot(),
                        DiagnosticBuffer.OmittedDistinctDiagnosticCount);
                string compactSummary =
                    SupportReportSummaryRenderer.Render(
                        document,
                        reportFileName);
                SupportIssueUrl issueUrl = SupportIssueUrlBuilder.Create(
                    compactSummary);
                if (issueUrl.SummaryWasShortened)
                {
                    document = document.WithIssueSummaryWasShortened();
                }

                string finalPath = SupportReportJsonFileWriter.Write(
                    document,
                    reportFileName);
                SupportReportPlayerPresenter.PresentSuccess(
                    finalPath,
                    compactSummary,
                    issueUrl);
            }
            catch (Exception exception)
            {
                SupportReportPlayerPresenter.PresentFailure(
                    "The report could not be created. No data was uploaded. " +
                    "Player.log contains the full failure for manual reporting.",
                    exception);
            }
        }

        private static void MirrorToPlayerLog(
            string code,
            SupportDiagnosticSeverity severity,
            string message,
            Exception? exception)
        {
            try
            {
                string logMessage =
                    "Delivery Temperature Limit [" + code + "]: " + message;
                if (exception != null)
                {
                    logMessage += " " + exception;
                }

                switch (severity)
                {
                    case SupportDiagnosticSeverity.Information:
                        Debug.Log(logMessage);
                        break;
                    case SupportDiagnosticSeverity.Warning:
                        Debug.LogWarning(logMessage);
                        break;
                    case SupportDiagnosticSeverity.Error:
                        Debug.LogError(logMessage);
                        break;
                    default:
                        Debug.LogError(
                            "Delivery Temperature Limit received an unknown support diagnostic severity for " +
                            code +
                            ".");
                        break;
                }
            }
            catch (Exception)
            {
                // Diagnostics must never interfere with mod behavior.
            }
        }
    }
}
