using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.Cli;

internal sealed record PipelineServices(
    ModProfileLocator ProfileLocator,
    ModProfileLoader ProfileLoader,
    ModProfileValidator ProfileValidator,
    OniMetadataReader MetadataReader,
    EnvironmentDiscoveryService EnvironmentDiscovery,
    GitRepositoryInspector GitRepositoryInspector,
    WorkshopListingValidator WorkshopListingValidator,
    IReleaseCandidatePreparer ReleaseCandidatePreparer,
    IModInstaller ModInstaller,
    IAcceptanceRecorder AcceptanceRecorder,
    IReleaseCandidateVerifier ReleaseCandidateVerifier,
    IExternalProcessRunner ProcessRunner);
