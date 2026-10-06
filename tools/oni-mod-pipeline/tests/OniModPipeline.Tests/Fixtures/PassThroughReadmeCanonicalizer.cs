using MaksymShostak.OniModPipeline.Readme;

namespace MaksymShostak.OniModPipeline.Tests.Fixtures;

// Consumer tests isolate release refusal/ownership; real normalization has separate qualification.
internal sealed class PassThroughReadmeCanonicalizer : IReadmeCanonicalizer
{
    public Task<byte[]> CanonicalizeAsync(string root, string path, byte[] candidate, CancellationToken token) => Task.FromResult(candidate);
}
