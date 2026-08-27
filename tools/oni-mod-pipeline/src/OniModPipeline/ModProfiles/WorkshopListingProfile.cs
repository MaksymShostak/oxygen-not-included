namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed record WorkshopListingProfile(
    string Description,
    string ChangeNotes,
    string Preview,
    IReadOnlyList<string> ModTypes,
    IReadOnlyList<string> DlcCompatibility,
    int DescriptionByteLimit,
    int ChangeNotesByteLimit);
