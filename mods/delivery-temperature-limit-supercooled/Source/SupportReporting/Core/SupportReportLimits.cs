#nullable enable

namespace DeliveryTemperatureLimit
{
    internal static class SupportReportLimits
    {
        internal const int SchemaVersion = 1;
        internal const int MaximumDistinctDiagnostics = 128;
        internal const int MaximumDiagnosticMessageCharacters = 2048;
        internal const int MaximumActiveMods = 512;
        internal const int MaximumRawPlayerLogBytes = 6 * 1024 * 1024;
        internal const int MaximumEscapedPlayerLogBytes = 10 * 1024 * 1024;
        internal const int MaximumReportBytes = 12 * 1024 * 1024;
        internal const int MaximumIssueUrlCharacters = 1800;
        internal const string BugIssueOrigin =
            "https://github.com/MaksymShostak/oxygen-not-included/issues/new";
        internal const string BugIssueTemplate = "temperature-limit-bug.yml";
        internal const string AvailableState = "available";
        internal const string UnavailableState = "unavailable";
    }
}
