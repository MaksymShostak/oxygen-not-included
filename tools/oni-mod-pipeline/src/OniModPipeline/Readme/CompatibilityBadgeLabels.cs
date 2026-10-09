using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;

namespace MaksymShostak.OniModPipeline.Readme;

/// <summary>Supply domain labels for known compatibility images in the generated description.</summary>
internal static class CompatibilityBadgeLabels
{
    internal static async Task<string> RenderGeneratedDescriptionAsync(string root, string markdown, IExternalProcessRunner runner, CancellationToken token)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(markdown) > 2_097_152)
            throw new InvalidDataException("Generated description exceeds its transport limit.");
        var bridge = ContainedPathResolver.ResolveExistingFile(root, "tooling/markdown/render-description.mjs");
        if (!bridge.IsSuccess) throw new InvalidDataException("The domain description renderer is missing or unsafe.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(new ProcessRequest("node", [bridge.Value!], root,
                new Dictionary<string, string>(), markdown, 2_097_152), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            throw new InvalidDataException("Domain description rendering timed out; README was not replaced.", exception);
        }
        if (result.ExitCode != 0) throw new InvalidDataException("Domain description rendering failed; README was not replaced.");
        return result.StandardOutput;
    }
}
