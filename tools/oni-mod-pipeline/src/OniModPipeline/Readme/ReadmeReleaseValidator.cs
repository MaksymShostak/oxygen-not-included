using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Readme;

internal sealed class ReadmeReleaseValidator(ReadmeSynchronizer synchronizer)
{
    internal async Task<OperationResult<ReadmeSynchronization>> ValidateAsync(
        ModProfile profile, string worktreeRoot, CancellationToken cancellationToken)
    {
        if (profile.Readme is null)
            return new OperationResult<ReadmeSynchronization>(null, [], PipelineExitCode.Success);
        try
        {
            var listing = await new WorkshopListingValidator().ValidateAsync(profile, cancellationToken).ConfigureAwait(false);
            if (!listing.IsSuccess)
                return new OperationResult<ReadmeSynchronization>(null, listing.Diagnostics, PipelineExitCode.ReleaseNotReady);
            var result = await synchronizer.SynchronizeAsync(worktreeRoot, profile.Readme, listing.Value!.Description,
                Path.Combine(worktreeRoot, "node_modules", "steam-community-bbcode"), true, cancellationToken).ConfigureAwait(false);
            return result.HasDrift
                ? new OperationResult<ReadmeSynchronization>(result, [DiagnosticCatalog.ReadmeSynchronizationFailed("README description is stale. Run sync-readme, review, and commit the updated release inputs.")], PipelineExitCode.ReleaseNotReady)
                : new OperationResult<ReadmeSynchronization>(result, [], PipelineExitCode.Success);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return new OperationResult<ReadmeSynchronization>(null, [DiagnosticCatalog.ReadmeSynchronizationFailed(exception.Message)], PipelineExitCode.ReleaseNotReady);
        }
    }
}
