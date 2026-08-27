using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal static class ModProfileLocator
{
    internal const string ManifestFileName = "oni-mod-pipeline.toml";

    internal static OperationResult<string> Locate(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return Failure(DiagnosticCatalog.ProfileNotFound(
                startPath,
                []));
        }

        string resolvedStartPath;
        try
        {
            resolvedStartPath = Path.GetFullPath(startPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(DiagnosticCatalog.ProfileNotFound(
                startPath,
                []));
        }

        if (string.Equals(
            Path.GetFileName(resolvedStartPath),
            ManifestFileName,
            StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(resolvedStartPath)
                ? Success(resolvedStartPath)
                : Failure(DiagnosticCatalog.ProfileNotFound(
                    resolvedStartPath,
                    [resolvedStartPath]));
        }

        string startDirectory;
        if (Directory.Exists(resolvedStartPath))
        {
            startDirectory = resolvedStartPath;
        }
        else if (File.Exists(resolvedStartPath))
        {
            startDirectory = Path.GetDirectoryName(resolvedStartPath)!;
        }
        else
        {
            return Failure(DiagnosticCatalog.ProfileNotFound(
                resolvedStartPath,
                []));
        }

        var searchedPaths = new List<string>();
        var candidatePaths = new List<string>();
        for (var directory = new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidatePath = Path.Combine(directory.FullName, ManifestFileName);
            searchedPaths.Add(candidatePath);
            if (File.Exists(candidatePath))
            {
                candidatePaths.Add(Path.GetFullPath(candidatePath));
            }

            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                break;
            }
        }

        return candidatePaths.Count switch
        {
            0 => Failure(DiagnosticCatalog.ProfileNotFound(resolvedStartPath, searchedPaths)),
            1 => Success(candidatePaths[0]),
            _ => Failure(DiagnosticCatalog.ProfileAmbiguous(resolvedStartPath, candidatePaths))
        };
    }

    private static OperationResult<string> Success(string manifestPath) =>
        new(manifestPath, [], PipelineExitCode.Success);

    private static OperationResult<string> Failure(Diagnostic diagnostic) =>
        new(null, [diagnostic], PipelineExitCode.InvalidInput);
}
