#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>Report choices belong to the open dialog. A failed attempt cannot reuse an older summary.</summary>
    internal sealed class SupportReportSession
    {
        internal bool IncludeGameLog { get; set; }

        internal bool Create(bool openIssueForm, Func<SupportReportKind, string?> createReport,
            Action<string> openForm)
        {
            string? summary = createReport(IncludeGameLog ? SupportReportKind.ExtendedPlayerLog :
                SupportReportKind.Standard);
            if (summary == null) return false;
            if (openIssueForm) openForm(SupportIssueUrlBuilder.Create(summary).Value);
            return true;
        }
    }
}
