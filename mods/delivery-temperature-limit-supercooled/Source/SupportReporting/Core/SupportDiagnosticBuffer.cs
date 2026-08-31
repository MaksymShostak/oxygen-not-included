#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    internal enum SupportDiagnosticSeverity
    {
        Information,
        Warning,
        Error
    }

    internal sealed class SupportDiagnosticBuffer
    {
        private const string TruncationMarker = "… [truncated]";

        private readonly object synchronization = new object();
        private readonly Dictionary<string, MutableDiagnostic> diagnostics =
            new Dictionary<string, MutableDiagnostic>(StringComparer.Ordinal);
        private readonly List<string> firstSeenCodes = new List<string>();
        private readonly HashSet<string> omittedCodes =
            new HashSet<string>(StringComparer.Ordinal);

        internal int OmittedDistinctDiagnosticCount
        {
            get
            {
                lock (synchronization)
                {
                    return omittedCodes.Count;
                }
            }
        }

        internal void Record(
            string code,
            SupportDiagnosticSeverity severity,
            string message,
            DateTimeOffset occurredAtUtc,
            Exception? exception = null)
        {
            string validatedCode = SupportReportCollections.RequireNonBlank(
                code,
                nameof(code));
            if (!Enum.IsDefined(typeof(SupportDiagnosticSeverity), severity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(severity),
                    severity,
                    "Unknown support diagnostic severity.");
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (occurredAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "A diagnostic timestamp must use the UTC offset.",
                    nameof(occurredAtUtc));
            }

            string boundedMessage = BoundMessageForReport(message);
            string? exceptionType = exception?.GetType().FullName;
            string? exceptionMessage = exception == null
                ? null
                : BoundMessageForReport(exception.Message ?? string.Empty);

            lock (synchronization)
            {
                MutableDiagnostic? existing;
                if (diagnostics.TryGetValue(validatedCode, out existing) &&
                    existing != null)
                {
                    existing.Record(
                        severity,
                        boundedMessage,
                        occurredAtUtc,
                        exceptionType,
                        exceptionMessage);
                    return;
                }

                if (diagnostics.Count >=
                    SupportReportLimits.MaximumDistinctDiagnostics)
                {
                    omittedCodes.Add(validatedCode);
                    return;
                }

                diagnostics.Add(
                    validatedCode,
                    new MutableDiagnostic(
                        validatedCode,
                        severity,
                        boundedMessage,
                        occurredAtUtc,
                        exceptionType,
                        exceptionMessage));
                firstSeenCodes.Add(validatedCode);
            }
        }

        internal IReadOnlyList<SupportDiagnosticSnapshot> CaptureSnapshot()
        {
            lock (synchronization)
            {
                var snapshot =
                    new List<SupportDiagnosticSnapshot>(firstSeenCodes.Count);
                for (int index = 0; index < firstSeenCodes.Count; index++)
                {
                    snapshot.Add(
                        diagnostics[firstSeenCodes[index]].CreateSnapshot());
                }

                return new ReadOnlyCollection<SupportDiagnosticSnapshot>(
                    snapshot);
            }
        }

        internal static string BoundMessageForReport(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Length <=
                SupportReportLimits.MaximumDiagnosticMessageCharacters)
            {
                return message;
            }

            int retainedCharacterCount =
                SupportReportLimits.MaximumDiagnosticMessageCharacters -
                TruncationMarker.Length;
            return message.Substring(0, retainedCharacterCount) +
                TruncationMarker;
        }

        private static string GetSeverityName(
            SupportDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SupportDiagnosticSeverity.Information:
                    return "information";
                case SupportDiagnosticSeverity.Warning:
                    return "warning";
                case SupportDiagnosticSeverity.Error:
                    return "error";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(severity),
                        severity,
                        "Unknown support diagnostic severity.");
            }
        }

        private sealed class MutableDiagnostic
        {
            internal MutableDiagnostic(
                string code,
                SupportDiagnosticSeverity severity,
                string message,
                DateTimeOffset occurredAtUtc,
                string? exceptionType,
                string? exceptionMessage)
            {
                Code = code;
                Severity = severity;
                Message = message;
                FirstOccurredAtUtc = occurredAtUtc;
                LastOccurredAtUtc = occurredAtUtc;
                RepeatCount = 1;
                ExceptionType = exceptionType;
                ExceptionMessage = exceptionMessage;
            }

            private string Code { get; }

            private SupportDiagnosticSeverity Severity { get; set; }

            private string Message { get; set; }

            private DateTimeOffset FirstOccurredAtUtc { get; set; }

            private DateTimeOffset LastOccurredAtUtc { get; set; }

            private int RepeatCount { get; set; }

            private string? ExceptionType { get; set; }

            private string? ExceptionMessage { get; set; }

            internal void Record(
                SupportDiagnosticSeverity severity,
                string message,
                DateTimeOffset occurredAtUtc,
                string? exceptionType,
                string? exceptionMessage)
            {
                Severity = severity;
                Message = message;
                if (occurredAtUtc < FirstOccurredAtUtc)
                {
                    FirstOccurredAtUtc = occurredAtUtc;
                }

                if (occurredAtUtc > LastOccurredAtUtc)
                {
                    LastOccurredAtUtc = occurredAtUtc;
                }

                RepeatCount = checked(RepeatCount + 1);
                ExceptionType = exceptionType;
                ExceptionMessage = exceptionMessage;
            }

            internal SupportDiagnosticSnapshot CreateSnapshot() =>
                new SupportDiagnosticSnapshot(
                    Code,
                    GetSeverityName(Severity),
                    FirstOccurredAtUtc,
                    LastOccurredAtUtc,
                    RepeatCount,
                    Message,
                    ExceptionType,
                    ExceptionMessage);
        }
    }
}
