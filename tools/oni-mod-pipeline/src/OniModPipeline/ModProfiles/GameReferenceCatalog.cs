using System.Globalization;
using MaksymShostak.OniModPipeline.Catalogs;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal static class GameReferenceCatalog
{
    internal static async Task<OperationResult<int>> ReadSelectedBuildAsync(IExternalProcessRunner runner,
        string repositoryRoot, string? pythonPath, CancellationToken token)
    {
        var catalog = ContainedPathResolver.ResolveExistingFile(repositoryRoot, "tools/ci/oni-references.json");
        if (!catalog.IsSuccess) return new(default, catalog.Diagnostics, catalog.ExitCode);
        try
        {
            // Only the catalog is input from the selected checkout. The executable policy ships with this CLI.
            using var resource = typeof(GameReferenceCatalog).Assembly.GetManifestResourceStream("OniModPipeline.oni_references.py")
                ?? throw new InvalidDataException("The pipeline lacks its embedded game-reference validator.");
            using var reader = new StreamReader(resource);
            string script = await reader.ReadToEndAsync(token);
            var result = await runner.RunAsync(new ProcessRequest(CatalogRunner.ResolvePython(pythonPath),
                ["-I", "-B", "-S", "-", "selected-build", "--catalog", catalog.Value!], AppContext.BaseDirectory,
                new Dictionary<string, string>(), StandardInput: script), token);
            if (result.ExitCode != 0 || !int.TryParse(result.StandardOutput.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int build))
                return new(default, [DiagnosticCatalog.InvalidOniMetadata(catalog.Value!, "Reference selection failed. " + result.StandardError)],
                    PipelineExitCode.InvalidInput);
            return new(build, [], PipelineExitCode.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new(default, [DiagnosticCatalog.InvalidOniMetadata(catalog.Value!, ex.Message)], PipelineExitCode.EnvironmentUnavailable);
        }
    }
}
