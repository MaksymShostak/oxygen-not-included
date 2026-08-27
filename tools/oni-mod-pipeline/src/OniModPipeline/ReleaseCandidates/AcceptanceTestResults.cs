using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

[JsonConverter(typeof(AcceptanceOutcomeJsonConverter))]
internal enum AcceptanceOutcome
{
    Passed,
    Failed
}

internal sealed record AcceptanceCheckResult(
    string Id,
    string Title,
    string Setup,
    string Action,
    string Expected,
    AcceptanceOutcome Outcome,
    string? Note);

internal sealed record AcceptanceTestResults(
    int SchemaVersion,
    string Tester,
    DateTimeOffset RecordedAtUtc,
    string ContentDigest,
    string AcceptancePlanSha256,
    IReadOnlyList<AcceptanceCheckResult> Checks);

internal sealed record AcceptanceRecordingResult(
    string ResultsPath,
    string StaticId,
    string Version,
    string ContentDigest,
    DateTimeOffset RecordedAtUtc,
    bool AllChecksPassed) : IFormattable
{
    public override string ToString() =>
        ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Acceptance results: {ResultsPath}{Environment.NewLine}" +
        $"Static ID: {StaticId}{Environment.NewLine}" +
        $"Version: {Version}{Environment.NewLine}" +
        $"Content digest: {ContentDigest}{Environment.NewLine}" +
        $"Recorded at (UTC): {RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
        $"All checks passed: {AllChecksPassed.ToString().ToLowerInvariant()}";
}

internal sealed class AcceptanceOutcomeJsonConverter :
    JsonConverter<AcceptanceOutcome>
{
    public override AcceptanceOutcome Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "passed" => AcceptanceOutcome.Passed,
                "failed" => AcceptanceOutcome.Failed,
                _ => throw new JsonException(
                    "Unknown acceptance-check outcome.")
            }
            : throw new JsonException(
                "An acceptance-check outcome must be a string.");

    public override void Write(
        Utf8JsonWriter writer,
        AcceptanceOutcome value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            AcceptanceOutcome.Passed => "passed",
            AcceptanceOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                null)
        });
}
