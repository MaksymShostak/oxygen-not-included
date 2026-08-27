using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal sealed record AcceptanceTestPlanCheck(
    string Id,
    string Title,
    bool Required,
    string Setup,
    string Action,
    string Expected);

internal sealed record AcceptanceTestPlan(
    int SchemaVersion,
    string StaticId,
    string Version,
    string ContentDigest,
    DateTimeOffset PreparedAtUtc,
    IReadOnlyList<AcceptanceTestPlanCheck> Checks)
{
    internal static AcceptanceTestPlan Create(
        ModProfile profile,
        OniMetadata metadata,
        string contentDigest,
        DateTimeOffset preparedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDigest);

        return new AcceptanceTestPlan(
            1,
            metadata.StaticId,
            metadata.Version,
            contentDigest,
            preparedAtUtc.ToUniversalTime(),
            profile.AcceptanceChecks
                .Select(check => new AcceptanceTestPlanCheck(
                    check.Id,
                    check.Title,
                    check.Required,
                    check.Setup,
                    check.Action,
                    check.Expected))
                .ToArray());
    }
}
