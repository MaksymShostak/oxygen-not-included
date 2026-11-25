using MaksymShostak.OniModPipeline.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal sealed class OniMetadataReader
{
    internal OperationResult<OniMetadata> Read(ModProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        try
        {
            var modYamlPath = ResolveMetadataPath(
                profile.ModRoot,
                profile.ModYamlPath);
            var modInfoYamlPath = ResolveMetadataPath(
                profile.ModRoot,
                profile.ModInfoYamlPath);
            var modYaml = ReadMapping(modYamlPath);
            var modInfoYaml = ReadMapping(modInfoYamlPath);

            var metadata = new OniMetadata(
                ReadRequiredScalar(modYaml, "staticID", modYamlPath),
                ReadRequiredScalar(modYaml, "title", modYamlPath),
                ReadRequiredScalar(modYaml, "description", modYamlPath),
                ReadRequiredScalar(modInfoYaml, "supportedContent", modInfoYamlPath),
                ReadRequiredInt(modInfoYaml, "minimumSupportedBuild", modInfoYamlPath),
                ReadRequiredScalar(modInfoYaml, "version", modInfoYamlPath),
                ReadRequiredInt(modInfoYaml, "APIVersion", modInfoYamlPath));

            return new OperationResult<OniMetadata>(
                metadata,
                [],
                PipelineExitCode.Success);
        }
        catch (MetadataReadException exception)
        {
            return new OperationResult<OniMetadata>(
                null,
                [exception.Diagnostic],
                PipelineExitCode.InvalidInput);
        }
    }

    internal OperationResult<string> SynchronizeMinimumBuild(ModProfile profile, string catalogPath, bool write)
    {
        var metadata = Read(profile);
        if (!metadata.IsSuccess) return new(null, metadata.Diagnostics, metadata.ExitCode);
        string path = profile.ModInfoYamlPath;
        try
        {
            using var catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath), new JsonDocumentOptions { AllowDuplicateProperties = false });
            string? version = catalog.RootElement.GetProperty("package").GetProperty("version").GetString();
            if (!System.Version.TryParse(version, out var selected) || selected.Major < 1 ||
                version != selected.Major.ToString(CultureInfo.InvariantCulture) + ".0.0")
                throw InvalidMetadata(catalogPath, "the single-build reference version must be <positive changelist>.0.0");

            path = ResolveMetadataPath(profile.ModRoot, profile.ModInfoYamlPath);
            byte[] original = File.ReadAllBytes(path);
            bool bom = original.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            var utf8 = new UTF8Encoding(bom, true);
            string text = utf8.GetString(original.AsSpan(bom ? Encoding.UTF8.Preamble.Length : 0));
            var mapping = ReadMapping(path, text);
            if (mapping.Children[new YamlScalarNode("minimumSupportedBuild")] is not YamlScalarNode floor ||
                !int.TryParse(floor.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                throw InvalidMetadata(path, "minimumSupportedBuild must have a literal integer value");
            int start = checked((int)floor.Start.Index);
            int end = checked((int)floor.End.Index);
            string token = text[start..end];
            if (token == "'" + floor.Value + "'" || token == "\"" + floor.Value + "\"") { start++; end--; }
            else if (token != floor.Value) throw InvalidMetadata(path, "cannot locate an unambiguous build-floor scalar");
            string expected = selected.Major.ToString(CultureInfo.InvariantCulture);
            if (text[start..end] == expected) return new(path, [], PipelineExitCode.Success);
            if (!write)
                return new(null, [DiagnosticCatalog.InvalidOniMetadata(path,
                    $"minimumSupportedBuild differs from the reference catalog ({expected}). Run sync-build-floor --write.")],
                    PipelineExitCode.ReleaseNotReady);

            string temp = Path.Combine(Path.GetDirectoryName(path)!, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, text[..start] + expected + text[end..], utf8);
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
                    throw InvalidMetadata(path, "metadata changed while projecting the build floor");
                File.Replace(temp, path, null);
            }
            finally { File.Delete(temp); }
            return new(path, [], PipelineExitCode.Success);
        }
        catch (MetadataReadException ex) { return new(null, [ex.Diagnostic], PipelineExitCode.InvalidInput); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException
            or InvalidOperationException or ArgumentException or OverflowException)
        {
            return new(null, [DiagnosticCatalog.InvalidOniMetadata(path, ex.Message)], PipelineExitCode.InvalidInput);
        }
    }

    private static string ResolveMetadataPath(string modRoot, string declaredPath)
    {
        var result = ContainedPathResolver.ResolveExistingFile(modRoot, declaredPath);
        if (!result.IsSuccess)
        {
            throw new MetadataReadException(result.Diagnostics.Single());
        }

        return result.Value!;
    }

    private static YamlMappingNode ReadMapping(string path, string? text = null)
    {
        var stream = new YamlStream();
        try
        {
            using TextReader reader = text is null ? File.OpenText(path) : new StringReader(text);
            stream.Load(reader);
        }
        catch (Exception exception) when (
            exception is YamlException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            throw InvalidMetadata(path, exception.Message);
        }

        if (stream.Documents.Count != 1)
        {
            throw InvalidMetadata(
                path,
                $"expected exactly one YAML document but found {stream.Documents.Count}.");
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode mapping)
        {
            throw InvalidMetadata(path, "the document root must be a mapping.");
        }

        ValidateNode(mapping, path);
        return mapping;
    }

    private static void ValidateNode(YamlNode node, string path)
    {
        if (!node.Anchor.IsEmpty)
        {
            throw InvalidMetadata(path, "anchors and aliases are not allowed.");
        }

        if (!node.Tag.IsEmpty &&
            !node.Tag.Value.StartsWith("tag:yaml.org,2002:", StringComparison.Ordinal))
        {
            throw InvalidMetadata(path, $"custom tag '{node.Tag.Value}' is not allowed.");
        }

        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var pair in mapping.Children)
                {
                    ValidateNode(pair.Key, path);
                    ValidateNode(pair.Value, path);
                }

                break;
            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children)
                {
                    ValidateNode(child, path);
                }

                break;
        }
    }

    private static string ReadRequiredScalar(
        YamlMappingNode mapping,
        string key,
        string path)
    {
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode keyNode ||
                !string.Equals(keyNode.Value, key, StringComparison.Ordinal))
            {
                continue;
            }

            if (pair.Value is not YamlScalarNode valueNode || valueNode.Value is null)
            {
                throw InvalidMetadata(path, $"required field '{key}' must be a scalar.");
            }

            return valueNode.Value;
        }

        throw InvalidMetadata(path, $"required scalar field '{key}' is missing.");
    }

    private static int ReadRequiredInt(
        YamlMappingNode mapping,
        string key,
        string path)
    {
        var value = ReadRequiredScalar(mapping, key, path);
        if (!int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var integer))
        {
            throw InvalidMetadata(
                path,
                $"required field '{key}' must be a 32-bit integer.");
        }

        return integer;
    }

    private static MetadataReadException InvalidMetadata(string path, string reason) =>
        new(DiagnosticCatalog.InvalidOniMetadata(path, reason));

    private sealed class MetadataReadException(Diagnostic diagnostic) : Exception
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }
}
