#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeliveryTemperatureLimit.Tests.ExternalModCompatibility;

/// <summary>
/// Non-operational serialized-shape stub reserved for a future generalized
/// external-mod fixture catalog. Current tests neither deserialize nor validate
/// this document.
/// </summary>
internal sealed class DeferredExternalModFixtureProvenanceDocument
{
    [JsonPropertyName("schemaVersion")]
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("integrationId")]
    [JsonRequired]
    public string IntegrationId { get; init; } = null!;

    [JsonPropertyName("upstreamProjectUri")]
    [JsonRequired]
    public string UpstreamProjectUri { get; init; } = null!;

    [JsonPropertyName("candidateSourceUri")]
    [JsonRequired]
    public string CandidateSourceUri { get; init; } = null!;

    [JsonPropertyName("archiveObservation")]
    [JsonRequired]
    public DeferredExternalModArchiveObservationDocument ArchiveObservation
    {
        get;
        init;
    } = null!;

    [JsonPropertyName("assembly")]
    [JsonRequired]
    public DeferredExternalModAssemblyEvidenceDocument Assembly { get; init; } =
        null!;

    [JsonPropertyName("retainedArtifacts")]
    [JsonRequired]
    public IReadOnlyList<DeferredExternalModRetainedArtifactDocument>
        RetainedArtifacts { get; init; } = null!;

    [JsonPropertyName("unavailableFacts")]
    [JsonRequired]
    public IReadOnlyList<string> UnavailableFacts { get; init; } = null!;
}

internal sealed class DeferredExternalModArchiveObservationDocument
{
    [JsonPropertyName("availability")]
    [JsonRequired]
    public string Availability { get; init; } = null!;

    [JsonPropertyName("observedAtUtc")]
    [JsonRequired]
    public DateTimeOffset? ObservedAtUtc { get; init; }

    [JsonPropertyName("sha256")]
    [JsonRequired]
    public string? Sha256 { get; init; }
}

internal sealed class DeferredExternalModAssemblyEvidenceDocument
{
    [JsonPropertyName("fileName")]
    [JsonRequired]
    public string FileName { get; init; } = null!;

    [JsonPropertyName("assemblyName")]
    [JsonRequired]
    public string AssemblyName { get; init; } = null!;

    [JsonPropertyName("assemblyVersion")]
    [JsonRequired]
    public string AssemblyVersion { get; init; } = null!;

    [JsonPropertyName("fileVersion")]
    [JsonRequired]
    public string FileVersion { get; init; } = null!;

    [JsonPropertyName("moduleVersionId")]
    [JsonRequired]
    public string ModuleVersionId { get; init; } = null!;

    [JsonPropertyName("sha256")]
    [JsonRequired]
    public string Sha256 { get; init; } = null!;
}

internal sealed class DeferredExternalModRetainedArtifactDocument
{
    [JsonPropertyName("path")]
    [JsonRequired]
    public string Path { get; init; } = null!;

    [JsonPropertyName("sha256")]
    [JsonRequired]
    public string Sha256 { get; init; } = null!;

    [JsonPropertyName("origin")]
    [JsonRequired]
    public DeferredExternalModArtifactOriginDocument Origin { get; init; } =
        null!;
}

internal sealed class DeferredExternalModArtifactOriginDocument
{
    [JsonPropertyName("kind")]
    [JsonRequired]
    public string Kind { get; init; } = null!;

    [JsonPropertyName("archiveMemberPath")]
    [JsonRequired]
    public string? ArchiveMemberPath { get; init; }

    [JsonPropertyName("sourceRevision")]
    [JsonRequired]
    public string? SourceRevision { get; init; }

    [JsonPropertyName("sourcePath")]
    [JsonRequired]
    public string? SourcePath { get; init; }
}
