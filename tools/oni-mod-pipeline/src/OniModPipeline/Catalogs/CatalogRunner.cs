using System.ComponentModel;
using System.Text.Json;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Catalogs;

internal sealed class CatalogRunner(IExternalProcessRunner processRunner, TimeSpan? timeout = null)
{
    internal async Task<OperationResult<JsonElement?>> RunAsync(
        ModProfile profile, string operation, string? pythonPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (profile.Catalogs is not { } catalogs)
            return operation == "check"
                ? new OperationResult<JsonElement?>(null, [], PipelineExitCode.Success)
                : Failure("The profile has no [catalogs] declaration.", PipelineExitCode.InvalidInput);
        if (operation is not ("check" or "inspect") || catalogs.Template.Contains('/') || catalogs.Template.Contains('\\') ||
            !catalogs.Template.EndsWith(".pot", StringComparison.Ordinal) ||
            !System.Text.RegularExpressions.Regex.IsMatch(catalogs.OptionsContextPrefix, @"^(?:[A-Za-z_][A-Za-z0-9_]*\.)+$"))
            return Failure("Invalid catalog operation, template filename, or context prefix.", PipelineExitCode.InvalidInput);
        string? temporaryDirectory = null;
        try
        {
            var directory = ContainedPathResolver.ResolveExistingDirectory(profile.ModRoot, catalogs.Directory);
            var source = ContainedPathResolver.ResolveExistingFile(profile.ModRoot, catalogs.OptionsSource);
            var template = ContainedPathResolver.ResolveExistingFile(profile.ModRoot, catalogs.Directory + "/" + catalogs.Template);
            if (!directory.IsSuccess || !source.IsSuccess || !template.IsSuccess)
                return Failure("A declared catalog input is missing or unsafe.", PipelineExitCode.InvalidInput);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory.Value!))
                if (Path.GetExtension(path) is ".po" or ".pot" &&
                    !ContainedPathResolver.ResolveExistingFile(directory.Value!, Path.GetFileName(path)).IsSuccess)
                    return Failure($"Unsafe catalog input: {path}", PipelineExitCode.InvalidInput);
            var executable = ResolvePython(pythonPath);
            temporaryDirectory = Directory.CreateTempSubdirectory("oni-catalogs-").FullName;
            var scriptPath = Path.Combine(temporaryDirectory, "catalogs.py");
            var assembly = typeof(CatalogRunner).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".Catalogs.Python.catalogs.py", StringComparison.Ordinal));
            using (var resource = assembly.GetManifestResourceStream(resourceName)!)
            await using (var script = File.Create(scriptPath))
                await resource.CopyToAsync(script, cancellationToken).ConfigureAwait(false);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
            ProcessResult result;
            try
            {
                result = await processRunner.RunAsync(new ProcessRequest(executable,
                    ["-I", "-B", scriptPath, operation, "--catalog-directory", directory.Value!,
                     "--template", catalogs.Template, "--options-source", source.Value!,
                     "--context-prefix", catalogs.OptionsContextPrefix],
                    profile.ModRoot, new Dictionary<string, string>()), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure("Catalog backend exceeded its execution deadline.", PipelineExitCode.EnvironmentUnavailable);
            }
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            var version = root.GetProperty("runtimeVersion");
            if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                root.GetProperty("operation").GetString() != operation ||
                version.ValueKind != JsonValueKind.Array || version.GetArrayLength() != 3 ||
                version[0].GetInt32() != 3 || version[1].GetInt32() < 10)
                throw new InvalidDataException("Backend schema/operation or Python version was rejected (Python 3.10+ required).");
            var success = root.GetProperty("success").GetBoolean();
            var diagnostics = root.GetProperty("diagnostics");
            if (diagnostics.ValueKind != JsonValueKind.Array || diagnostics.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                throw new InvalidDataException("Backend diagnostics must be an array of strings.");
            if (!success && result.ExitCode == 1 && diagnostics.GetArrayLength() > 0)
                return Failure(string.Join("; ", diagnostics.EnumerateArray().Select(item => item.GetString())), PipelineExitCode.InvalidInput);
            var value = root.GetProperty("value");
            if (!success || result.ExitCode != 0 || diagnostics.GetArrayLength() != 0 || value.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Backend returned an inconsistent result (exit {result.ExitCode}): {result.StandardError}");
            if (operation == "check" && (value.GetProperty("keyCount").GetInt32() <= 0 || value.GetProperty("localeCount").GetInt32() <= 0))
                throw new InvalidDataException("Backend counts must be positive integers.");
            if (operation == "inspect")
                foreach (var catalog in value.EnumerateObject())
                {
                    if (Path.GetFileName(catalog.Name) != catalog.Name || Path.GetExtension(catalog.Name) is not (".po" or ".pot") ||
                        catalog.Value.ValueKind != JsonValueKind.Object)
                        throw new InvalidDataException("Inspection must map catalog filenames to entry objects.");
                    foreach (var entry in catalog.Value.EnumerateObject())
                        if (entry.Value.ValueKind != JsonValueKind.Object ||
                            entry.Value.GetProperty("block").ValueKind != JsonValueKind.String ||
                            entry.Value.GetProperty("en").ValueKind != JsonValueKind.String ||
                            entry.Value.GetProperty("translation").ValueKind != JsonValueKind.String)
                            throw new InvalidDataException("Inspection entries require block, en and translation strings.");
                }
            return new OperationResult<JsonElement?>(value.Clone(), [], PipelineExitCode.Success);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception or
            JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {
            return Failure(exception.Message, PipelineExitCode.EnvironmentUnavailable);
        }
        finally
        {
            // The runner returns only after the child has completed, including cancellation.
            if (temporaryDirectory is not null)
            {
                File.Delete(Path.Combine(temporaryDirectory, "catalogs.py"));
                Directory.Delete(temporaryDirectory);
            }
        }
    }

    private static string ResolvePython(string? selected)
    {
        if (selected is not null)
        {
            if (!Path.IsPathFullyQualified(selected) || !File.Exists(selected))
                throw new IOException("--python must name an existing absolute Python executable.");
            return Path.GetFullPath(selected);
        }
        var name = OperatingSystem.IsWindows() ? "python.exe" : "python3";
        foreach (var directory in (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory.Trim('"')))
            {
                var candidate = Path.Combine(directory.Trim('"'), name);
                if (File.Exists(candidate)) return candidate;
            }
        throw new IOException("Python was not found on PATH; select an absolute executable with --python.");
    }

    private static OperationResult<JsonElement?> Failure(string evidence, PipelineExitCode exitCode) => new(
        null, [new Diagnostic("ONIP1010", DiagnosticSeverity.Error,
            "Catalog operation could not be accepted.", evidence,
            "Correct the declared catalog/source inputs or select a supported Python executable, then retry.")], exitCode);
}
