using System.Globalization;

namespace MaksymShostak.OniModPipeline.ModInstallation;

internal sealed record InstallationReceipt(
    int SchemaVersion,
    string StaticId,
    string Version,
    string ContentDigest,
    InstallTarget Target,
    string AbsoluteTargetPath,
    DateTimeOffset InstalledAtUtc,
    bool InstalledFilesVerified);

internal sealed record ModInstallationResult(
    string StaticId,
    string Version,
    string ContentDigest,
    InstallTarget Target,
    string AbsoluteTargetPath,
    DateTimeOffset InstalledAtUtc,
    bool InstallationReceiptWritten)
{
    public override string ToString() =>
        $"Installed: {AbsoluteTargetPath}{Environment.NewLine}" +
        $"Static ID: {StaticId}{Environment.NewLine}" +
        $"Version: {Version}{Environment.NewLine}" +
        $"Content digest: {ContentDigest}{Environment.NewLine}" +
        $"Target: {Target.ToCanonicalName()}{Environment.NewLine}" +
        $"Installed at (UTC): {InstalledAtUtc.ToString("O", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
        $"Candidate receipt written: {InstallationReceiptWritten.ToString().ToLowerInvariant()}";
}
