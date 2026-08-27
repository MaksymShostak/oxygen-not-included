using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.CommandLine;
using System.Globalization;

namespace MaksymShostak.OniModPipeline.Cli;

internal static class CliApplication
{
    private const int MaximumGameBuildMetadataBytes = 64 * 1024;

    internal static RootCommand CreateRootCommand() =>
        CreateRootCommand(CreateDefaultServices());

    internal static RootCommand CreateRootCommand(PipelineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var rootCommand = new RootCommand(
            "Prepare tested ONI mod release candidates for manual Workshop upload.");
        rootCommand.Subcommands.Add(CreateDiagnoseCommand(services));
        rootCommand.Subcommands.Add(CreateValidateCommand(services));
        rootCommand.Subcommands.Add(CreateBuildCommand(services));
        rootCommand.Subcommands.Add(CreateTestCommand(services));
        rootCommand.Subcommands.Add(CreatePrepareReleaseCommand(services));
        return rootCommand;
    }

    internal static PipelineServices CreateDefaultServices()
    {
        var processRunner = new ExternalProcessRunner();
        var candidateSource = GameInstallationCandidateSource.CreateDefault();
        var gitRepositoryInspector = new GitRepositoryInspector(processRunner);
        return new PipelineServices(
            new ModProfileLocator(),
            new ModProfileLoader(),
            new ModProfileValidator(),
            new OniMetadataReader(),
            new EnvironmentDiscoveryService(
                processRunner,
                new EnvironmentVariableSource(),
                candidateSource,
                new SteamLibraryCatalog()),
            gitRepositoryInspector,
            new WorkshopListingValidator(),
            ReleaseCandidatePreparer.CreateDefault(
                processRunner,
                gitRepositoryInspector),
            processRunner);
    }

    internal static async Task<int> InvokeAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        try
        {
            var parseResult = CreateRootCommand().Parse(args);
            if (parseResult.Errors.Count > 0)
            {
                foreach (var parseError in parseResult.Errors)
                {
                    parseResult.InvocationConfiguration.Error.Write(parseError.Message);
                    parseResult.InvocationConfiguration.Error.Write('\n');
                }

                return (int)PipelineExitCode.InvalidInput;
            }

            return await parseResult.InvokeAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var result = new OperationResult<object>(
                null,
                [DiagnosticCatalog.UnexpectedFailure(exception)],
                PipelineExitCode.InternalFailure);

            return DiagnosticRenderer.Render(
                result,
                OutputFormat.Human,
                Console.Out,
                Console.Error);
        }
    }

    private static Command CreateDiagnoseCommand(PipelineServices services)
    {
        var options = new CommandOptions();
        var command = new Command(
            "diagnose",
            "Report the resolved read-only ONI mod development environment.");
        options.AddTo(command);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await DiagnoseAsync(
                services,
                options.GetModPath(parseResult),
                options.GetEnvironmentRequest(parseResult),
                cancellationToken).ConfigureAwait(false);
            return DiagnosticRenderer.Render(
                result,
                options.GetOutputFormat(parseResult),
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error);
        });
        return command;
    }

    private static Command CreateValidateCommand(PipelineServices services)
    {
        var options = new CommandOptions();
        var forReleaseOption = new Option<bool>("--for-release")
        {
            Description =
                "Require every contributing source path to be tracked, committed, and clean."
        };
        var command = new Command(
            "validate",
            "Validate an ONI mod profile, metadata, environment, and declared inputs.");
        options.AddTo(command);
        command.Options.Add(forReleaseOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await ValidateAsync(
                services,
                options.GetModPath(parseResult),
                options.GetEnvironmentRequest(parseResult),
                parseResult.GetValue(forReleaseOption),
                cancellationToken).ConfigureAwait(false);
            return DiagnosticRenderer.Render(
                result,
                options.GetOutputFormat(parseResult),
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error);
        });
        return command;
    }

    private static Command CreateBuildCommand(PipelineServices services)
    {
        var options = new CommandOptions();
        var configurationOption = new Option<string?>("--configuration")
        {
            Description =
                "MSBuild configuration override; defaults to the profile build configuration."
        };
        configurationOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (value is not null &&
                (string.IsNullOrWhiteSpace(value) ||
                 value.Contains('"') ||
                 value.Any(char.IsControl)))
            {
                result.AddError(
                    "Option '--configuration' must be nonempty and contain no control characters or double quotes.");
            }
        });

        var command = new Command(
            "build",
            "Build an ONI mod into a new isolated artifact run.");
        options.AddTo(command);
        command.Options.Add(configurationOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await BuildAsync(
                services,
                options.GetModPath(parseResult),
                options.GetEnvironmentRequest(parseResult),
                parseResult.GetValue(configurationOption),
                cancellationToken).ConfigureAwait(false);
            return DiagnosticRenderer.Render(
                result,
                options.GetOutputFormat(parseResult),
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error);
        });
        return command;
    }

    private static Command CreateTestCommand(PipelineServices services)
    {
        var options = new CommandOptions();
        var command = new Command(
            "test",
            "Run every declared mod test project into a new evidence directory.");
        options.AddTo(command);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await TestAsync(
                services,
                options.GetModPath(parseResult),
                options.GetEnvironmentRequest(parseResult),
                cancellationToken).ConfigureAwait(false);
            return DiagnosticRenderer.Render(
                result,
                options.GetOutputFormat(parseResult),
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error);
        });
        return command;
    }

    private static Command CreatePrepareReleaseCommand(PipelineServices services)
    {
        var options = new CommandOptions();
        var command = new Command(
            "prepare-release",
            "Prepare one immutable awaiting-acceptance ONI release candidate.");
        options.AddTo(command);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await PrepareReleaseAsync(
                services,
                options.GetModPath(parseResult),
                options.GetEnvironmentRequest(parseResult),
                cancellationToken).ConfigureAwait(false);
            return DiagnosticRenderer.Render(
                result,
                options.GetOutputFormat(parseResult),
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error);
        });
        return command;
    }

    private static async Task<OperationResult<DiagnoseReport>> DiagnoseAsync(
        PipelineServices services,
        string modPath,
        EnvironmentDiscoveryRequest environmentRequest,
        CancellationToken cancellationToken)
    {
        var contextResult = await ResolveReadOnlyContextAsync(
            services,
            modPath,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ConvertFailure<ReadOnlyContext, DiagnoseReport>(contextResult);
        }

        var context = contextResult.Value!;
        var provenanceResult = await services.GitRepositoryInspector.InspectAsync(
            context.Profile,
            typeof(CliApplication).Assembly.Location,
            cancellationToken).ConfigureAwait(false);
        var provenance = provenanceResult.IsSuccess ? provenanceResult.Value : null;
        var environment = context.Environment;
        var report = new DiagnoseReport(
            environment.DotnetSdkVersion,
            environment.OperatingSystem,
            environment.Architecture,
            provenance?.WorktreeRoot,
            context.Profile.ModRoot,
            environment.GameDirectory,
            environment.OniManagedAssemblyDirectory,
            environment.UserDataDirectory,
            environment.DevelopmentModsDirectory,
            environment.LocalModsDirectory,
            environment.ArtifactsDirectory,
            TryReadGameBuildMetadata(environment.GameDirectory),
            IsUploaderPresent(environment.GameDirectory));
        return Success(report);
    }

    private static async Task<OperationResult<ValidationReport>> ValidateAsync(
        PipelineServices services,
        string modPath,
        EnvironmentDiscoveryRequest environmentRequest,
        bool forRelease,
        CancellationToken cancellationToken)
    {
        var contextResult = await ResolveReadOnlyContextAsync(
            services,
            modPath,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ConvertFailure<ReadOnlyContext, ValidationReport>(contextResult);
        }

        var context = contextResult.Value!;
        var listingResult = await services.WorkshopListingValidator
            .ValidateAsync(context.Profile, cancellationToken)
            .ConfigureAwait(false);
        if (!listingResult.IsSuccess)
        {
            return ConvertFailure<WorkshopListingValidation, ValidationReport>(listingResult);
        }

        var provenanceResult = await services.GitRepositoryInspector.InspectAsync(
            context.Profile,
            typeof(CliApplication).Assembly.Location,
            cancellationToken).ConfigureAwait(false);
        if (forRelease && !provenanceResult.IsSuccess)
        {
            return ConvertFailure<GitProvenance, ValidationReport>(provenanceResult);
        }

        var provenance = provenanceResult.IsSuccess ? provenanceResult.Value : null;
        if (forRelease && provenance is { IsClean: false })
        {
            var dirtyPaths = string.Join(
                ", ",
                provenance.DirtyPaths.Select(path => $"'{path}'"));
            return new OperationResult<ValidationReport>(
                null,
                [DiagnosticCatalog.DirtyReleaseInput(
                    $"Dirty contributing paths: {dirtyPaths}.")],
                PipelineExitCode.ReleaseNotReady);
        }

        return Success(new ValidationReport(
            forRelease,
            provenance?.IsClean ?? false,
            provenance?.WorktreeRoot,
            provenance?.Commit,
            context.Profile.ModRoot,
            context.Metadata.StaticId,
            context.Metadata.Version,
            context.Environment.DotnetSdkVersion));
    }

    private static async Task<OperationResult<string>> BuildAsync(
        PipelineServices services,
        string modPath,
        EnvironmentDiscoveryRequest environmentRequest,
        string? configuration,
        CancellationToken cancellationToken)
    {
        var contextResult = await ResolveReadOnlyContextAsync(
            services,
            modPath,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ConvertFailure<ReadOnlyContext, string>(contextResult);
        }

        var context = contextResult.Value!;
        var provenanceResult = await services.GitRepositoryInspector.InspectAsync(
            context.Profile,
            typeof(CliApplication).Assembly.Location,
            cancellationToken).ConfigureAwait(false);
        if (!provenanceResult.IsSuccess)
        {
            return ConvertFailure<GitProvenance, string>(provenanceResult);
        }

        var provenance = provenanceResult.Value!;
        var runRoot = CreateRunRoot(
            context.Environment.ArtifactsDirectory,
            "builds",
            context.Metadata.StaticId);
        var selectedConfiguration = configuration ??
            context.Profile.Build?.Configuration ??
            "Release";
        var builder = new ModBuilder(
            services.ProcessRunner,
            new Utf8ArtifactWriter());
        var buildResult = await builder.BuildAsync(
            new BuildRequest(
                context.Profile,
                context.Environment,
                selectedConfiguration,
                runRoot,
                context.Metadata.Version,
                provenance.Commit),
            cancellationToken).ConfigureAwait(false);
        if (!buildResult.IsSuccess)
        {
            return ConvertFailure<BuildResult, string>(buildResult);
        }

        return Success(Path.Combine(runRoot, "build-result.json"));
    }

    private static async Task<OperationResult<string>> TestAsync(
        PipelineServices services,
        string modPath,
        EnvironmentDiscoveryRequest environmentRequest,
        CancellationToken cancellationToken)
    {
        var contextResult = await ResolveReadOnlyContextAsync(
            services,
            modPath,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ConvertFailure<ReadOnlyContext, string>(contextResult);
        }

        var context = contextResult.Value!;
        var provenanceResult = await services.GitRepositoryInspector.InspectAsync(
            context.Profile,
            typeof(CliApplication).Assembly.Location,
            cancellationToken).ConfigureAwait(false);
        if (!provenanceResult.IsSuccess)
        {
            return ConvertFailure<GitProvenance, string>(provenanceResult);
        }

        var provenance = provenanceResult.Value!;
        var runRoot = CreateRunRoot(
            context.Environment.ArtifactsDirectory,
            "tests",
            context.Metadata.StaticId);
        var resultsRoot = Path.Combine(runRoot, "automated-test-results");
        var testRunner = new AutomatedTestRunner(
            services.ProcessRunner,
            context.Environment.OniManagedAssemblyDirectory,
            provenance.WorktreeRoot);
        var testResult = await testRunner.RunAsync(
            context.Profile,
            resultsRoot,
            cancellationToken).ConfigureAwait(false);
        return testResult.IsSuccess
            ? Success(resultsRoot)
            : new OperationResult<string>(
                null,
                testResult.Diagnostics,
                testResult.ExitCode);
    }

    private static async Task<OperationResult<PreparedReleaseCandidate>> PrepareReleaseAsync(
        PipelineServices services,
        string modPath,
        EnvironmentDiscoveryRequest environmentRequest,
        CancellationToken cancellationToken)
    {
        var contextResult = await ResolveReadOnlyContextAsync(
            services,
            modPath,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ConvertFailure<ReadOnlyContext, PreparedReleaseCandidate>(contextResult);
        }

        var context = contextResult.Value!;
        var pipelineExecutablePath = typeof(CliApplication).Assembly.Location;
        var provenanceResult = await services.GitRepositoryInspector.InspectAsync(
            context.Profile,
            pipelineExecutablePath,
            cancellationToken).ConfigureAwait(false);
        if (!provenanceResult.IsSuccess)
        {
            return ConvertFailure<GitProvenance, PreparedReleaseCandidate>(
                provenanceResult);
        }

        var provenance = provenanceResult.Value!;
        if (!provenance.IsClean)
        {
            var dirtyPaths = string.Join(
                ", ",
                provenance.DirtyPaths.Select(path => $"'{path}'"));
            return new OperationResult<PreparedReleaseCandidate>(
                null,
                [DiagnosticCatalog.DirtyReleaseInput(
                    $"Dirty contributing paths: {dirtyPaths}.")],
                PipelineExitCode.ReleaseNotReady);
        }

        return await services.ReleaseCandidatePreparer.PrepareAsync(
            new ReleasePreparationRequest(
                context.Profile,
                context.Metadata,
                context.Environment,
                provenance,
                pipelineExecutablePath,
                TryReadGameBuildMetadata(context.Environment.GameDirectory)),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationResult<ReadOnlyContext>> ResolveReadOnlyContextAsync(
        PipelineServices services,
        string modPath,
        EnvironmentDiscoveryRequest environmentRequest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profilePathResult = services.ProfileLocator.Locate(modPath);
        if (!profilePathResult.IsSuccess)
        {
            return ConvertFailure<string, ReadOnlyContext>(profilePathResult);
        }

        var profileResult = services.ProfileLoader.Load(profilePathResult.Value!);
        if (!profileResult.IsSuccess)
        {
            return ConvertFailure<ModProfile, ReadOnlyContext>(profileResult);
        }

        var profile = profileResult.Value!;
        var metadataResult = services.MetadataReader.Read(profile);
        if (!metadataResult.IsSuccess)
        {
            return ConvertFailure<OniMetadata, ReadOnlyContext>(metadataResult);
        }

        var metadata = metadataResult.Value!;
        var validationResult = services.ProfileValidator.Validate(profile, metadata);
        if (!validationResult.IsSuccess)
        {
            return ConvertFailure<ModProfile, ReadOnlyContext>(validationResult);
        }

        var environmentResult = await services.EnvironmentDiscovery.DiscoverAsync(
            profile,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!environmentResult.IsSuccess)
        {
            return ConvertFailure<PipelineEnvironment, ReadOnlyContext>(environmentResult);
        }

        return Success(new ReadOnlyContext(
            profile,
            metadata,
            environmentResult.Value!));
    }

    private static string? TryReadGameBuildMetadata(string gameDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(gameDirectory, "build.json"),
            Path.Combine(gameDirectory, "OxygenNotIncluded_Data", "build.json"),
            Path.Combine(
                gameDirectory,
                "OxygenNotIncluded_Data",
                "StreamingAssets",
                "build.json"),
            Path.Combine(
                gameDirectory,
                "OxygenNotIncluded.app",
                "Contents",
                "Resources",
                "Data",
                "StreamingAssets",
                "build.json")
        };

        foreach (var path in candidates)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > MaximumGameBuildMetadataBytes)
                {
                    continue;
                }

                var metadata = File.ReadAllText(path).Trim();
                if (metadata.Length > 0)
                {
                    return metadata;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Game build metadata is optional diagnostic information.
            }
        }

        return null;
    }

    private static bool IsUploaderPresent(string gameDirectory) =>
        File.Exists(Path.Combine(gameDirectory, "OniUploader64.exe")) ||
        File.Exists(Path.Combine(gameDirectory, "OniUploader.exe")) ||
        Directory.Exists(Path.Combine(gameDirectory, "OniUploader.app"));

    private static string CreateRunRoot(
        string artifactsDirectory,
        string category,
        string staticId)
    {
        var runId = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}";
        return Path.GetFullPath(Path.Combine(
            artifactsDirectory,
            category,
            staticId,
            runId));
    }

    private static OperationResult<T> Success<T>(T value) =>
        new(value, [], PipelineExitCode.Success);

    private static OperationResult<TOutput> ConvertFailure<TInput, TOutput>(
        OperationResult<TInput> result) =>
        new(default, result.Diagnostics, result.ExitCode);

    private sealed record ReadOnlyContext(
        ModProfile Profile,
        OniMetadata Metadata,
        PipelineEnvironment Environment);
}

internal sealed record DiagnoseReport(
    string DotnetSdkVersion,
    string OperatingSystem,
    string Architecture,
    string? WorktreeRoot,
    string ModRoot,
    string GameDirectory,
    string OniManagedAssemblyDirectory,
    string UserDataDirectory,
    string DevelopmentModsDirectory,
    string LocalModsDirectory,
    string ArtifactsDirectory,
    string? GameBuildMetadata,
    bool UploaderPresent) : IFormattable
{
    public override string ToString() =>
        ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Join(
            Environment.NewLine,
            [
                $".NET SDK version: {DotnetSdkVersion}",
                $"Host operating system: {OperatingSystem}",
                $"Host architecture: {Architecture}",
                $"Git worktree root: {WorktreeRoot ?? "<unavailable>"}",
                $"Mod root: {ModRoot}",
                $"ONI game directory: {GameDirectory}",
                $"ONI managed-assembly directory: {OniManagedAssemblyDirectory}",
                $"ONI user-data directory: {UserDataDirectory}",
                $"ONI Dev mods directory: {DevelopmentModsDirectory}",
                $"ONI Local mods directory: {LocalModsDirectory}",
                $"Pipeline artifacts directory: {ArtifactsDirectory}",
                $"ONI game build metadata: {GameBuildMetadata ?? "<unavailable>"}",
                $"ONI Uploader present: {UploaderPresent.ToString().ToLowerInvariant()}"
            ]);
}

internal sealed record ValidationReport(
    bool ReleaseValidation,
    bool SourceClean,
    string? WorktreeRoot,
    string? Commit,
    string ModRoot,
    string StaticId,
    string Version,
    string DotnetSdkVersion) : IFormattable
{
    public override string ToString() =>
        ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Join(
            Environment.NewLine,
            [
                "Profile valid: true",
                "Environment valid: true",
                $"Release validation: {ReleaseValidation.ToString().ToLowerInvariant()}",
                $"Source clean: {SourceClean.ToString().ToLowerInvariant()}",
                $"Worktree root: {WorktreeRoot ?? "<unavailable>"}",
                $"Commit: {Commit ?? "<unavailable>"}",
                $"Mod root: {ModRoot}",
                $"Static ID: {StaticId}",
                $"Version: {Version}",
                $".NET SDK: {DotnetSdkVersion}"
            ]);
}
