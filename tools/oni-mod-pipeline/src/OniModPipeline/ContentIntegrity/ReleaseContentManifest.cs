namespace MaksymShostak.OniModPipeline.ContentIntegrity;

internal sealed record ReleaseContentManifest(
    int SchemaVersion,
    IReadOnlyList<ReleaseContentEntry> Entries,
    string ContentDigest);
