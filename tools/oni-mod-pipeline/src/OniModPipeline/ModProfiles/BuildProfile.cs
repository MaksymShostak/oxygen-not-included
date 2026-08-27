namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed record BuildProfile(
    string EntryPoint,
    string Configuration,
    string GameManagedDirectoryProperty,
    string PrimaryOutput,
    IReadOnlyList<string> MergeInputs);
