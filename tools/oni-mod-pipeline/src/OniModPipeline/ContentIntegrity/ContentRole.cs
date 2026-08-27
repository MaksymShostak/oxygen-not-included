using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaksymShostak.OniModPipeline.ContentIntegrity;

[JsonConverter(typeof(ContentRoleJsonConverter))]
internal enum ContentRole
{
    Runtime,
    Description,
    ChangeNotes,
    Preview
}

internal static class ContentRoleExtensions
{
    internal static string ToCanonicalName(this ContentRole role) =>
        role switch
        {
            ContentRole.Runtime => "runtime",
            ContentRole.Description => "description",
            ContentRole.ChangeNotes => "change-notes",
            ContentRole.Preview => "preview",
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unknown release content role.")
        };
}

internal sealed class ContentRoleJsonConverter : JsonConverter<ContentRole>
{
    public override ContentRole Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "runtime" => ContentRole.Runtime,
                "description" => ContentRole.Description,
                "change-notes" => ContentRole.ChangeNotes,
                "preview" => ContentRole.Preview,
                _ => throw new JsonException("Unknown release content role.")
            }
            : throw new JsonException("A release content role must be a string.");

    public override void Write(
        Utf8JsonWriter writer,
        ContentRole value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalName());
}
