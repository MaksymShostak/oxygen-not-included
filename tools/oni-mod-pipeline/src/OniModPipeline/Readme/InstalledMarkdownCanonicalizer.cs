using System.Text.Json;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Readme;

/// <summary>Transport generated README bytes to the installed public logical-document operation.</summary>
internal sealed class InstalledMarkdownCanonicalizer(IExternalProcessRunner runner) : IReadmeCanonicalizer
{
    private const string PackageName = "@hadden-industries/markdown-quality";
    private const int DocumentLimit = 2_097_152;

    public async Task<byte[]> CanonicalizeAsync(string repositoryRoot, string readmePath, byte[] candidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (candidate.Length > DocumentLimit) throw new InvalidDataException("README exceeds the document limit.");
            var bridge = ContainedPathResolver.ResolveExistingFile(repositoryRoot, "tooling/markdown/readme-document.mjs");
            var manifestPath = ContainedPathResolver.ResolveExistingFile(repositoryRoot, "tooling/markdown/node_modules/@hadden-industries/markdown-quality/package.json");
            var lockPath = ContainedPathResolver.ResolveExistingFile(repositoryRoot, "tooling/markdown/package-lock.json");
            if (!bridge.IsSuccess || !manifestPath.IsSuccess || !lockPath.IsSuccess)
                throw new InvalidDataException("Install the locked tooling/markdown graph before README synchronization.");
            using var packageLock = JsonDocument.Parse(await File.ReadAllBytesAsync(lockPath.Value!, cancellationToken).ConfigureAwait(false));
            var version = packageLock.RootElement.GetProperty("packages").GetProperty($"node_modules/{PackageName}").GetProperty("version").GetString();
            using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath.Value!, cancellationToken).ConfigureAwait(false));
            if (string.IsNullOrWhiteSpace(version) || manifest.RootElement.GetProperty("name").GetString() != PackageName ||
                manifest.RootElement.GetProperty("version").GetString() != version)
                throw new InvalidDataException("README normalization requires the locked installed package identity.");

            var relative = Path.GetRelativePath(repositoryRoot, readmePath).Replace('\\', '/');
            var requestId = Guid.NewGuid().ToString("N");
            var request = JsonSerializer.Serialize(new { path = relative, requestId, contentBase64 = Convert.ToBase64String(candidate) });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            ProcessResult response;
            try
            {
                response = await runner.RunAsync(new ProcessRequest("node", [bridge.Value!, repositoryRoot], repositoryRoot,
                    new Dictionary<string, string>(), request, 8_388_608), timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidDataException("Markdown normalization timed out; README was not replaced.", exception);
            }
            if (response.ExitCode != 0 || response.StandardOutput.Length > 8_388_608)
                throw new InvalidDataException("Canonical README has unresolved findings or an operational failure.");
            // The bridge uses the installed semantic validator. Bind its admitted response to this transport request.
            using var document = JsonDocument.Parse(response.StandardOutput);
            var report = document.RootElement;
            var output = report.GetProperty("document");
            if (report.GetProperty("schemaVersion").GetInt32() != 3 || report.GetProperty("operation").GetString() != "format" ||
                report.GetProperty("exitCode").GetInt32() != response.ExitCode ||
                report.GetProperty("package").GetProperty("name").GetString() != PackageName ||
                report.GetProperty("package").GetProperty("version").GetString() != version ||
                output.GetProperty("path").GetString() != relative || output.GetProperty("requestId").GetString() != requestId)
                throw new InvalidDataException("Markdown response does not match the README request.");
            var encoded = output.GetProperty("contentBase64").GetString()!;
            var bytes = Convert.FromBase64String(encoded);
            if (bytes.Length > DocumentLimit || Convert.ToBase64String(bytes) != encoded)
                throw new InvalidDataException("Canonical README violates the bounded byte transport.");
            return bytes;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("Installed Markdown metadata or output violated its public contract; README was not replaced.", exception);
        }
    }
}
