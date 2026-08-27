namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal sealed record AutomatedTestEvidence(
    string Id,
    bool Required,
    string ProjectPath,
    string TrxPath,
    int ExitCode,
    bool Passed);

internal sealed record EvidenceIndexEntry(
    string Path,
    long ByteLength,
    string Sha256);

internal sealed record ReleaseBlockingCondition(
    string Id,
    string Summary);

internal sealed record ReleaseReadinessReport(
    int SchemaVersion,
    string StaticId,
    string Version,
    string ContentDigest,
    DateTimeOffset PreparedAtUtc,
    ReleaseCandidateState State,
    bool BuildSucceeded,
    bool AutomatedTestsPassed,
    bool PreparedContentVerified,
    bool RelevantSourcesClean,
    IReadOnlyList<AutomatedTestEvidence> AutomatedTests,
    IReadOnlyList<EvidenceIndexEntry> EvidenceIndex,
    IReadOnlyList<ReleaseBlockingCondition> BlockingConditions,
    string? IrreversibleInvalidation);
