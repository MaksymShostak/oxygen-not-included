using System.Text.Json;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Readme;

internal sealed record BbcodeConversion(string Markdown, JsonElement Diagnostics, string StandardError);

internal sealed class InstalledBbcodeConverter(IExternalProcessRunner processRunner)
{
    internal async Task<BbcodeConversion> ConvertAsync(
        string packageDirectory, string generatedDescriptionPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifestPath = ContainedPathResolver.ResolveExistingFile(packageDirectory, "package.json");
        if (!manifestPath.IsSuccess)
        {
            throw new InvalidDataException("The installed converter package manifest is missing or unsafe.");
        }

        try
        {
            using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath.Value!, cancellationToken).ConfigureAwait(false));
            var root = manifest.RootElement;
            if (root.GetProperty("name").GetString() != "steam-community-bbcode")
            {
                throw new InvalidDataException("The installed package must be steam-community-bbcode.");
            }

            var binDeclaration = root.GetProperty("bin");
            var bin = binDeclaration.ValueKind == JsonValueKind.String
                ? binDeclaration.GetString()
                : binDeclaration.GetProperty("steam-community-bbcode").GetString();
            var executable = ContainedPathResolver.ResolveExistingFile(packageDirectory, bin ?? string.Empty);
            if (!executable.IsSuccess)
            {
                throw new InvalidDataException("The declared public converter bin must be an existing regular file inside the installed package.");
            }

            var result = await processRunner.RunAsync(new ProcessRequest(
                "node",
                [executable.Value!, "to-gfm", "--format=json", "--profile=workshop-item", "--fail-on=lossy", generatedDescriptionPath],
                Path.GetFullPath(packageDirectory),
                new Dictionary<string, string>()), cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new InvalidDataException($"Converter exited with {result.ExitCode}: {result.StandardError}");
            }

            using var output = JsonDocument.Parse(result.StandardOutput);
            var value = output.RootElement.GetProperty("value").GetString();
            var diagnostics = output.RootElement.GetProperty("diagnostics");
            if (value is null || diagnostics.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Converter output requires a string value and diagnostics array.");
            }

            foreach (var diagnostic in diagnostics.EnumerateArray())
            {
                var fidelity = diagnostic.GetProperty("fidelity").GetString();
                var severity = diagnostic.GetProperty("severity").GetString();
                if (fidelity is not ("exact" or "approximate") || severity is not ("info" or "warning") ||
                    string.IsNullOrWhiteSpace(diagnostic.GetProperty("code").GetString()) ||
                    diagnostic.GetProperty("message").ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Converter output contains a rejected or malformed diagnostic.");
                }
            }

            return new BbcodeConversion(value, diagnostics.Clone(), result.StandardError);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("The converter manifest or conversion result does not satisfy its public JSON contract.", exception);
        }
    }
}
