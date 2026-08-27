using MaksymShostak.OniModPipeline.Diagnostics;
using System.Text;

namespace MaksymShostak.OniModPipeline.WorkshopContent;

internal sealed class WorkshopContentValidator
{
    private static readonly ISet<string> ForbiddenFileNames = new HashSet<string>(
        [
            "0Harmony.dll",
            "Assembly-CSharp.dll",
            "Assembly-CSharp-firstpass.dll",
            "Newtonsoft.Json.dll",
            "PLib.dll",
            "Preview.png"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly ISet<string> ForbiddenExtensions = new HashSet<string>(
        [
            ".bbcode",
            ".cs",
            ".csproj",
            ".sln",
            ".slnx",
            ".ps1",
            ".bat",
            ".sh",
            ".pdb",
            ".log"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly ISet<string> ForbiddenDirectories = new HashSet<string>(
        ["bin", "obj", "Tests", "release-evidence"],
        StringComparer.OrdinalIgnoreCase);

    internal OperationResult<IReadOnlyList<string>> ValidateInventory(
        IReadOnlyList<string> relativePaths,
        string primaryDestination)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);

        string normalizedPrimary;
        try
        {
            normalizedPrimary = NormalizeRelativePath(primaryDestination);
        }
        catch (InvalidDataException exception)
        {
            return Failure(exception.Message);
        }

        var normalizedPaths = new List<string>(relativePaths.Count);
        var portablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in relativePaths)
        {
            string normalized;
            try
            {
                normalized = NormalizeRelativePath(path);
            }
            catch (InvalidDataException exception)
            {
                return Failure(exception.Message);
            }

            if (!portablePaths.Add(normalized))
            {
                return Failure(
                    $"Workshop content destinations collide portably at '{normalized}'.");
            }

            var forbiddenReason = GetForbiddenReason(normalized);
            if (forbiddenReason is not null)
            {
                return Failure(
                    $"Workshop content path '{normalized}' is forbidden: {forbiddenReason}");
            }

            normalizedPaths.Add(normalized);
        }

        if (!normalizedPaths.Contains("mod.yaml", StringComparer.Ordinal))
        {
            return Failure("Workshop content must contain 'mod.yaml' at its root.");
        }

        if (!normalizedPaths.Contains("mod_info.yaml", StringComparer.Ordinal))
        {
            return Failure("Workshop content must contain 'mod_info.yaml' at its root.");
        }

        if (!portablePaths.Contains(normalizedPrimary))
        {
            return Failure(
                $"Workshop content does not contain the primary assembly destination '{normalizedPrimary}'.");
        }

        if (normalizedPrimary.Contains('/'))
        {
            return Failure(
                $"Primary assembly '{normalizedPrimary}' must be placed at the Workshop content root.");
        }

        if (!string.Equals(
            Path.GetExtension(normalizedPrimary),
            ".dll",
            StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                $"Primary output '{normalizedPrimary}' must be a root-level .dll assembly.");
        }

        return new OperationResult<IReadOnlyList<string>>(
            normalizedPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            [],
            PipelineExitCode.Success);
    }

    internal static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            throw new InvalidDataException(
                "A Workshop content destination must be nonempty and contain no record delimiters.");
        }

        string normalized;
        try
        {
            normalized = path.Replace('\\', '/').Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "A Workshop content destination must contain valid Unicode text.",
                exception);
        }

        if (normalized[0] == '/' ||
            normalized.Length >= 2 &&
            char.IsAsciiLetter(normalized[0]) &&
            normalized[1] == ':')
        {
            throw new InvalidDataException(
                $"Workshop content destination '{normalized}' must be relative.");
        }

        var segments = normalized.Split('/');
        if (segments.Any(segment =>
            segment.Length == 0 || segment == "." || segment == ".."))
        {
            throw new InvalidDataException(
                $"Workshop content destination '{normalized}' must not contain empty or traversal segments.");
        }

        return normalized;
    }

    private static string? GetForbiddenReason(string path)
    {
        var segments = path.Split('/');
        if (segments.Take(segments.Length - 1).Any(ForbiddenDirectories.Contains))
        {
            return "build, test, or release-evidence directories are not runtime content.";
        }

        var fileName = segments[^1];
        if (ForbiddenFileNames.Contains(fileName) ||
            fileName.StartsWith("Unity", StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("FMOD", StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return "game, framework, or merge-only assemblies must not be copied loose.";
        }

        var extension = Path.GetExtension(fileName);
        if (ForbiddenExtensions.Contains(extension))
        {
            return "source, project, script, symbol, and log files are excluded in schema v1.";
        }

        if (fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".lock.json", StringComparison.OrdinalIgnoreCase))
        {
            return "dependency lock files are release evidence, not runtime content.";
        }

        return null;
    }

    private static OperationResult<IReadOnlyList<string>> Failure(string reason) =>
        new(
            null,
            [DiagnosticCatalog.CandidateManifestMismatch(reason)],
            PipelineExitCode.ReleaseNotReady);
}
