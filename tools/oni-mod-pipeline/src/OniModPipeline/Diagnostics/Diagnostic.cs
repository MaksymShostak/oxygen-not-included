namespace MaksymShostak.OniModPipeline.Diagnostics;

internal sealed record Diagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string Summary,
    string Evidence,
    string NextAction);
