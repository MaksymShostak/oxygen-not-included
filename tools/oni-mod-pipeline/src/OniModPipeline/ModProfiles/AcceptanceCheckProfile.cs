namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed record AcceptanceCheckProfile(
    string Id,
    string Title,
    bool Required,
    string Setup,
    string Action,
    string Expected);
