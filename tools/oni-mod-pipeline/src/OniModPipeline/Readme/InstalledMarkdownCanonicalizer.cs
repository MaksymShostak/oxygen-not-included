using System.Text;
using System.Text.Json;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Readme;

/// <summary>Format through the public checker and supply explicit alternatives for owned compatibility badges.</summary>
internal sealed class InstalledMarkdownCanonicalizer(IExternalProcessRunner runner) : IReadmeCanonicalizer
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private const string PackageName = "@hadden-industries/markdown-quality";
    private const string Version = "1.0.2";
    private const string BadgeBase = "https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/";
    private static readonly Dictionary<string, string> BadgeAlternatives = new(StringComparer.Ordinal)
    {
        ["VanillaYes.png"] = "Base game supported",
        ["Dlc1Yes.png"] = "Spaced Out! supported",
        ["Dlc2Yes.png"] = "The Frosty Planet Pack supported",
        ["Dlc3Yes.png"] = "The Bionic Booster Pack supported",
        ["Dlc4Yes.png"] = "The Prehistoric Planet Pack supported",
        ["Dlc5Yes.png"] = "The Aquatic Planet Pack supported"
    };

    public async Task<byte[]> CanonicalizeAsync(string repositoryRoot, string readmePath, byte[] candidate, CancellationToken cancellationToken)
    {
        try
        {
            return await CanonicalizeCoreAsync(repositoryRoot, readmePath, candidate, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("Installed Markdown metadata or output violated its public contract; README was not replaced.", exception);
        }
    }

    private async Task<byte[]> CanonicalizeCoreAsync(string repositoryRoot, string readmePath, byte[] candidate, CancellationToken cancellationToken)
    {
        if (candidate.Length > 2_097_152) throw new InvalidDataException("README exceeds the native document limit.");
        var manifestPath = ContainedPathResolver.ResolveExistingFile(repositoryRoot, "tooling/markdown/node_modules/@hadden-industries/markdown-quality/package.json");
        if (!manifestPath.IsSuccess) throw new InvalidDataException("Install the locked tooling/markdown graph before README synchronization.");
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath.Value!, cancellationToken).ConfigureAwait(false));
        var metadata = manifest.RootElement;
        if (metadata.GetProperty("name").GetString() != PackageName || metadata.GetProperty("version").GetString() != Version)
            throw new InvalidDataException("README normalization requires the approved Markdown package tuple.");
        var bin = ContainedPathResolver.ResolveExistingFile(Path.GetDirectoryName(manifestPath.Value!)!, metadata.GetProperty("bin").GetProperty("markdown-quality").GetString()!);
        if (!bin.IsSuccess) throw new InvalidDataException("The public Markdown bin is missing or unsafe.");
        var temporary = Path.Combine(Path.GetDirectoryName(readmePath)!, $"oni-readme-{Guid.NewGuid():N}.md");
        var relative = Path.GetRelativePath(repositoryRoot, temporary).Replace('\\', '/');
        var owned = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            var node = await runner.RunAsync(new ProcessRequest("node", ["--version"], repositoryRoot, new Dictionary<string, string>()), timeout.Token).ConfigureAwait(false);
            if (node.ExitCode != 0 || !System.Version.TryParse(node.StandardOutput.Trim().TrimStart('v'), out var nodeVersion) || nodeVersion.Major != 24 || nodeVersion < new System.Version(24, 21, 0))
                throw new InvalidDataException("Markdown tooling requires supported Node >=24.21.0 <25.");
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                owned = true;
                await file.WriteAsync(candidate, timeout.Token).ConfigureAwait(false);
            }
            using var observed = await RunAsync("check").ConfigureAwait(false);
            var repaired = AddBadgeAlternatives(candidate, observed.RootElement.GetProperty("diagnostics"));
            await File.WriteAllBytesAsync(temporary, repaired, timeout.Token).ConfigureAwait(false);
            using var formatted = await RunAsync("format").ConfigureAwait(false);
            using var checkedResult = await RunAsync("check").ConfigureAwait(false);
            if (formatted.RootElement.GetProperty("exitCode").GetInt32() != 0 || checkedResult.RootElement.GetProperty("exitCode").GetInt32() != 0)
                throw new InvalidDataException("Canonical README has unresolved Markdown findings.");
            var output = await File.ReadAllBytesAsync(temporary, timeout.Token).ConfigureAwait(false);
            if (output.Length > 2_097_152) throw new InvalidDataException("Canonical README exceeds the document limit.");
            return output;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidDataException("Markdown normalization timed out; README was not replaced.", exception);
        }
        finally
        {
            if (owned) File.Delete(temporary);
        }

        async Task<JsonDocument> RunAsync(string mode)
        {
            var result = await runner.RunAsync(new ProcessRequest("node", [bin.Value!, mode, "--root", repositoryRoot,
                "--files-json", JsonSerializer.Serialize(new[] { relative }), "--json"], repositoryRoot,
                new Dictionary<string, string>()), timeout.Token).ConfigureAwait(false);
            if (result.StandardOutput.Length > 8_388_608 || result.ExitCode is not (0 or 1))
                throw new InvalidDataException($"Markdown {mode} failed or refused preservation; README was not replaced.");
            var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("operation").GetString() != mode ||
                root.GetProperty("package").GetProperty("name").GetString() != PackageName ||
                root.GetProperty("package").GetProperty("version").GetString() != Version ||
                root.GetProperty("exitCode").GetInt32() != result.ExitCode ||
                root.GetProperty("outcome").GetString() != (result.ExitCode == 0 ? "clean" : "findings") ||
                root.GetProperty("errors").GetArrayLength() != 0 || result.ExitCode == 0 && root.GetProperty("unprocessed").GetArrayLength() != 0 ||
                root.GetProperty("selection").GetProperty("mode").GetString() != "explicit" ||
                !root.GetProperty("selection").GetProperty("files").EnumerateArray().Select(item => item.GetString()).SequenceEqual([relative]))
            {
                document.Dispose();
                throw new InvalidDataException("Markdown result does not prove complete processing of the staged README.");
            }
            return document;
        }
    }

    internal static byte[] AddBadgeAlternatives(byte[] candidate, JsonElement diagnostics)
    {
        var text = Utf8.GetString(candidate);
        var edits = new Dictionary<int, string>();
        foreach (var diagnostic in diagnostics.EnumerateArray())
        {
            var rule = diagnostic.GetProperty("rule").GetString();
            if (rule != "markdown/require-alt-text") continue;
            var offset = 0;
            var line = diagnostic.GetProperty("line").GetInt32();
            var column = diagnostic.GetProperty("column").GetInt32();
            if (line < 1 || column < 1) throw new InvalidDataException("Invalid native presentation coordinates.");
            for (var number = 1; number < line; number++)
            {
                var newline = text.IndexOf('\n', offset);
                if (newline < 0) throw new InvalidDataException("Native presentation line is outside the README.");
                offset = newline + 1;
            }
            var lineEnd = text.IndexOf('\n', offset);
            if (lineEnd < 0) lineEnd = text.Length;
            offset += column - 1;
            if (offset >= lineEnd || !ReadmeDescriptionBlock.ContainsContentOffset(candidate, offset))
                throw new InvalidDataException("Presentation repair cannot change authored README content.");
            var match = BadgeAlternatives.SingleOrDefault(item => text.AsSpan(offset).StartsWith($"![]({BadgeBase}{item.Key})", StringComparison.Ordinal));
            if (match.Key is null) throw new InvalidDataException("An image requires an authored alternative; no label is guessed.");
            if (!edits.TryAdd(offset + 2, match.Value))
                throw new InvalidDataException("Native presentation diagnostics repeat an image coordinate.");
        }
        foreach (var edit in edits.OrderByDescending(item => item.Key)) text = text.Insert(edit.Key, edit.Value);
        return Utf8.GetBytes(text);
    }
}
