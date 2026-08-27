using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaksymShostak.OniModPipeline.ModInstallation;

[JsonConverter(typeof(InstallTargetJsonConverter))]
internal enum InstallTarget
{
    Dev,
    Local
}

internal static class InstallTargetExtensions
{
    internal static string ToCanonicalName(this InstallTarget target) =>
        target switch
        {
            InstallTarget.Dev => "dev",
            InstallTarget.Local => "local",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

    internal static string ToDirectoryName(this InstallTarget target) =>
        target switch
        {
            InstallTarget.Dev => "Dev",
            InstallTarget.Local => "Local",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
}

internal sealed class InstallTargetJsonConverter : JsonConverter<InstallTarget>
{
    public override InstallTarget Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "dev" => InstallTarget.Dev,
                "local" => InstallTarget.Local,
                _ => throw new JsonException("Unknown ONI mod installation target.")
            }
            : throw new JsonException("An ONI mod installation target must be a string.");

    public override void Write(
        Utf8JsonWriter writer,
        InstallTarget value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToCanonicalName());
}
