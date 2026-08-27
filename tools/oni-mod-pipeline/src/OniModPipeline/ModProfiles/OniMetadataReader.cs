using MaksymShostak.OniModPipeline.Diagnostics;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal static class OniMetadataReader
{
    internal static OperationResult<OniMetadata> Read(ModProfile profile)
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

    private static string ResolveMetadataPath(string modRoot, string declaredPath)
    {
        if (string.IsNullOrWhiteSpace(declaredPath) || Path.IsPathRooted(declaredPath))
        {
            throw new MetadataReadException(DiagnosticCatalog.UnsafeProfilePath(
                modRoot,
                string.IsNullOrWhiteSpace(declaredPath) ? "<empty>" : declaredPath,
                "the declaration must be a nonempty relative path."));
        }

        string resolvedRoot;
        string resolvedPath;
        try
        {
            resolvedRoot = Path.GetFullPath(modRoot);
            resolvedPath = Path.GetFullPath(Path.Combine(resolvedRoot, declaredPath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new MetadataReadException(DiagnosticCatalog.UnsafeProfilePath(
                modRoot,
                declaredPath,
                "the declaration is not a valid filesystem path."));
        }

        var relativePath = Path.GetRelativePath(resolvedRoot, resolvedPath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new MetadataReadException(DiagnosticCatalog.UnsafeProfilePath(
                resolvedRoot,
                declaredPath,
                $"it resolves outside the mod root to '{resolvedPath}'."));
        }

        if (!File.Exists(resolvedPath))
        {
            throw new MetadataReadException(DiagnosticCatalog.DeclaredInputMissing(
                declaredPath,
                resolvedPath));
        }

        return resolvedPath;
    }

    private static YamlMappingNode ReadMapping(string path)
    {
        var stream = new YamlStream();
        try
        {
            using var reader = File.OpenText(path);
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
