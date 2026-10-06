namespace MaksymShostak.OniModPipeline.Readme;

internal interface IReadmeCanonicalizer
{
    Task<byte[]> CanonicalizeAsync(string repositoryRoot, string readmePath, byte[] candidate, CancellationToken cancellationToken);
}
