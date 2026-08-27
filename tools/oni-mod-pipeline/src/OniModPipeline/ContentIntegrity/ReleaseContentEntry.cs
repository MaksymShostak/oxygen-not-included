namespace MaksymShostak.OniModPipeline.ContentIntegrity;

internal sealed record ReleaseContentEntry(
    ContentArea ContentArea,
    string RelativePath,
    long ByteLength,
    string Sha256,
    ContentRole Role);
