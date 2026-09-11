using System.Security.Cryptography;
using System.Text.Json;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Readme;

internal sealed record ReadmeSynchronization(
    string ReadmePath, bool HasDrift, bool Changed, string DescriptionSha256,
    string ReadmeSha256, JsonElement ConversionDiagnostics, string ConverterStandardError);

internal sealed class ReadmeSynchronizer(InstalledBbcodeConverter converter)
{
    internal async Task<ReadmeSynchronization> SynchronizeAsync(
        string repositoryRoot, ReadmeProfile profile, RenderedListingText description,
        string packageDirectory, bool check, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = ContainedPathResolver.ResolveExistingFile(repositoryRoot, profile.RepositoryPath);
        if (!resolved.IsSuccess)
        {
            throw new InvalidDataException("The README must be an existing regular file inside the repository without linked ancestors.");
        }

        var readmePath = resolved.Value!;
        var original = await File.ReadAllBytesAsync(readmePath, cancellationToken).ConfigureAwait(false);
        // Reject an invalid ownership boundary before launching the converter.
        ReadmeDescriptionBlock.Replace(original, string.Empty);
        var generatedPath = Path.Combine(Path.GetTempPath(), $"oni-readme-{Guid.NewGuid():N}.bbcode");
        var ownsGeneratedFile = false;
        var ownsReplacementFile = false;
        string? replacementPath = null;
        try
        {
            await using (var generated = new FileStream(generatedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsGeneratedFile = true;
                await generated.WriteAsync(description.Bytes, cancellationToken).ConfigureAwait(false);
            }

            var conversion = await converter.ConvertAsync(packageDirectory, generatedPath, cancellationToken).ConfigureAwait(false);
            var replacement = ReadmeDescriptionBlock.Replace(original, conversion.Markdown);
            var drift = !original.AsSpan().SequenceEqual(replacement);
            if (!check && drift)
            {
                replacementPath = Path.Combine(Path.GetDirectoryName(readmePath)!, $".oni-readme-{Guid.NewGuid():N}.tmp");
                await using var output = new FileStream(replacementPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                ownsReplacementFile = true;
                await output.WriteAsync(replacement, cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            var currentPath = ContainedPathResolver.ResolveExistingFile(repositoryRoot, profile.RepositoryPath);
            if (!currentPath.IsSuccess)
            {
                throw new IOException("The README path changed during conversion.");
            }
            var currentBytes = await File.ReadAllBytesAsync(readmePath, cancellationToken).ConfigureAwait(false);
            if (!original.AsSpan().SequenceEqual(currentBytes))
            {
                throw new IOException("The README changed during conversion; synchronization did not overwrite it.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (replacementPath is not null)
            {
                File.Move(replacementPath, readmePath, overwrite: true);
                replacementPath = null;
                ownsReplacementFile = false;
            }

            return new ReadmeSynchronization(readmePath, drift, !check && drift,
                Convert.ToHexStringLower(SHA256.HashData(description.Bytes)),
                Convert.ToHexStringLower(SHA256.HashData(check ? original : replacement)),
                conversion.Diagnostics, conversion.StandardError);
        }
        finally
        {
            if (ownsGeneratedFile) File.Delete(generatedPath);
            if (ownsReplacementFile && replacementPath is not null)
            {
                File.Delete(replacementPath);
            }
        }
    }
}
