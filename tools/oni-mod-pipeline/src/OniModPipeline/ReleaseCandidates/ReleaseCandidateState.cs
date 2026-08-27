using MaksymShostak.OniModPipeline.ContentIntegrity;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

[JsonConverter(typeof(ReleaseCandidateStateJsonConverter))]
internal enum ReleaseCandidateState
{
    AwaitingAcceptance,
    AcceptanceFailed,
    ReadyForUpload,
    VerificationFailed
}

internal static class ReleaseCandidateStateExtensions
{
    internal static string ToCanonicalName(this ReleaseCandidateState state) =>
        state switch
        {
            ReleaseCandidateState.AwaitingAcceptance => "awaiting-acceptance",
            ReleaseCandidateState.AcceptanceFailed => "acceptance-failed",
            ReleaseCandidateState.ReadyForUpload => "ready-for-upload",
            ReleaseCandidateState.VerificationFailed => "verification-failed",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
}

internal sealed class ReleaseCandidateStateJsonConverter :
    JsonConverter<ReleaseCandidateState>
{
    public override ReleaseCandidateState Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "awaiting-acceptance" => ReleaseCandidateState.AwaitingAcceptance,
                "acceptance-failed" => ReleaseCandidateState.AcceptanceFailed,
                "ready-for-upload" => ReleaseCandidateState.ReadyForUpload,
                "verification-failed" => ReleaseCandidateState.VerificationFailed,
                _ => throw new JsonException("Unknown release candidate state.")
            }
            : throw new JsonException("A release candidate state must be a string.");

    public override void Write(
        Utf8JsonWriter writer,
        ReleaseCandidateState value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalName());
}

internal sealed record PreparedReleaseCandidate(
    string CandidateDirectory,
    CandidateLayout Layout,
    ReleaseContentManifest ContentManifest,
    BuildProvenance Provenance,
    ReleaseCandidateState State)
{
    public override string ToString() =>
        $"Candidate: {CandidateDirectory}{Environment.NewLine}" +
        $"Content digest: {ContentManifest.ContentDigest}{Environment.NewLine}" +
        $"State: {State.ToCanonicalName()}";
}
