using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaksymShostak.OniModPipeline.ContentIntegrity;

[JsonConverter(typeof(ContentAreaJsonConverter))]
internal enum ContentArea
{
    WorkshopContent,
    WorkshopListing
}

internal static class ContentAreaExtensions
{
    internal static string ToCanonicalName(this ContentArea area) =>
        area switch
        {
            ContentArea.WorkshopContent => "workshop-content",
            ContentArea.WorkshopListing => "workshop-listing",
            _ => throw new ArgumentOutOfRangeException(
                nameof(area),
                area,
                "Unknown release content area.")
        };
}

internal sealed class ContentAreaJsonConverter : JsonConverter<ContentArea>
{
    public override ContentArea Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "workshop-content" => ContentArea.WorkshopContent,
                "workshop-listing" => ContentArea.WorkshopListing,
                _ => throw new JsonException("Unknown release content area.")
            }
            : throw new JsonException("A release content area must be a string.");

    public override void Write(
        Utf8JsonWriter writer,
        ContentArea value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalName());
}
