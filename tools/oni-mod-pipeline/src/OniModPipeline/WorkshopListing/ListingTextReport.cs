namespace MaksymShostak.OniModPipeline.WorkshopListing;

internal sealed record ListingTextReport(
    string Encoding,
    bool HasBom,
    string LineEndings,
    int LogicalLineCount,
    int LineBreakCount,
    int BlankLineCount,
    long Utf8ByteCount,
    string LogicalContentSha256,
    string ArtifactSha256);

internal sealed record RenderedListingText(
    byte[] Bytes,
    ListingTextReport Report);
