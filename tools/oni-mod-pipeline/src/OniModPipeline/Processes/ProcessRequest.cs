namespace MaksymShostak.OniModPipeline.Processes;

internal sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string? StandardInput = null,
    int? OutputLimitCharacters = null);
