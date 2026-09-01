#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeliveryTemperatureLimit.Tests.ExternalModCompatibility;

/// <summary>
/// Non-operational serialized-shape stub for a future explicit decision to
/// preserve an evaluated but unsupported external-mod build.
/// </summary>
internal sealed class DeferredExternalModFixtureIncompatibilityDecisionDocument
{
    [JsonPropertyName("schemaVersion")]
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("assemblySha256")]
    [JsonRequired]
    public string AssemblySha256 { get; init; } = null!;

    [JsonPropertyName("evaluatedAtUtc")]
    [JsonRequired]
    public DateTimeOffset EvaluatedAtUtc { get; init; }

    [JsonPropertyName("failureCodes")]
    [JsonRequired]
    public IReadOnlyList<string> FailureCodes { get; init; } = null!;

    [JsonPropertyName("summary")]
    [JsonRequired]
    public string Summary { get; init; } = null!;
}
