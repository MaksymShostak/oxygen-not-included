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
            var bridge = ContainedPathResolver.ResolveExistingFile(repositoryRoot, "tools/readme/canonicalize.mjs");
            if (!bridge.IsSuccess)
                throw new InvalidDataException("The README transport bridge is missing from the checkout.");

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
            if (report.GetProperty("schemaVersion").GetInt32() != 5 || report.GetProperty("operation").GetString() != "format" ||
                report.GetProperty("exitCode").GetInt32() != response.ExitCode ||
                report.GetProperty("operationStatus").GetString() != "completed" ||
                report.GetProperty("writtenPaths").GetArrayLength() != 0 ||
                report.GetProperty("package").GetProperty("name").GetString() != PackageName ||
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
