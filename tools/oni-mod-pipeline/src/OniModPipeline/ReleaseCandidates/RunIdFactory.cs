using System.Globalization;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal static class RunIdFactory
{
    internal static string Create(
        DateTimeOffset timestamp,
        ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length != 8)
        {
            throw new ArgumentException(
                "A release run ID requires exactly eight entropy bytes.",
                nameof(entropy));
        }

        var utc = timestamp.ToUniversalTime();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{utc:yyyyMMdd'T'HHmmss.fffffff'Z'}-{Convert.ToHexString(entropy).ToLowerInvariant()}");
    }
}
