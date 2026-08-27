namespace MaksymShostak.OniModPipeline.ContentIntegrity;

internal sealed record FileDigest(string Path, long ByteLength, string Sha256);
