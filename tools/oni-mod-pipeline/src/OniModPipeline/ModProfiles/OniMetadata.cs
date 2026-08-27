namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed record OniMetadata(
    string StaticId,
    string Title,
    string Description,
    string SupportedContent,
    int MinimumSupportedBuild,
    string Version,
    int ApiVersion);
