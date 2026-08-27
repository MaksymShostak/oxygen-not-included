using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModInstallation;
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
        rootCommand.Subcommands.Add(CreateInstallCommand(services));
        rootCommand.Subcommands.Add(CreateRecordAcceptanceCommand(services));
        rootCommand.Subcommands.Add(CreateVerifyReleaseCommand(services));
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
            ModInstaller.CreateDefault(),
            AcceptanceRecorder.CreateDefault(new SystemAcceptanceConsole()),
            ReleaseCandidateVerifier.CreateDefault(),
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

    private static Command CreateInstallCommand(PipelineServices services)
    {
        var options = new CommandOptions(defaultModToCurrentDirectory: false);
        var candidateOption = new Option<string?>("--candidate")
        {
            Description = "Exact manifest-verified release-candidate directory to install."
        };
        var buildResultOption = new Option<string?>("--build-result")
        {
            Description =
                "Exact build-result.json to install together with the explicit --mod profile."
        };
        var targetOption = new Option<string?>("--target")
        {
            Description = "Guarded ONI installation target: dev or local."
        };
        AddExplicitPathValidator(candidateOption);
        AddExplicitPathValidator(options.ModOption);
        AddExplicitPathValidator(buildResultOption);
        targetOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (value is not null && value is not ("dev" or "local"))
            {
                result.AddError("Option '--target' accepts only 'dev' or 'local'.");
            }
        });

        var command = new Command(
            "install",
            "Install one exact candidate or explicit development build with ownership guards.");
        options.AddTo(command);
        command.Options.Add(candidateOption);
        command.Options.Add(buildResultOption);
        command.Options.Add(targetOption);
        command.Validators.Add(result =>
        {
            var candidate = result.GetValue(candidateOption);
            var mod = result.GetValue(options.ModOption);
            var buildResult = result.GetValue(buildResultOption);
            var candidateForm = candidate is not null &&
                mod is null &&
                buildResult is null;
            var developmentForm = candidate is null &&
                mod is not null &&
                buildResult is not null;
            if (!candidateForm && !developmentForm)
            {
                result.AddError(
                    "Command 'install' requires exactly one source form: --candidate, or --mod together with --build-result.");
            }

            if (result.GetValue(targetOption) is null)
            {
                result.AddError("Command 'install' requires '--target dev' or '--target local'.");
            }
        });
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await InstallAsync(
                services,
                options.GetOptionalModPath(parseResult),
                parseResult.GetValue(candidateOption),
                parseResult.GetValue(buildResultOption),
                ParseInstallTarget(parseResult.GetValue(targetOption)!),
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

    private static Command CreateRecordAcceptanceCommand(PipelineServices services)
    {
        var candidateOption = new Option<string?>("--candidate")
        {
            Description =
                "Exact installed release-candidate directory whose human acceptance will be recorded."
        };
        var testerOption = new Option<string?>("--tester")
        {
            Description =
                "Tester display name; when omitted, the interactive recorder prompts for it."
        };
        AddExplicitPathValidator(candidateOption);
        testerOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                result.AddError("Option '--tester' requires a nonempty display name.");
            }
        });

        var command = new Command(
            "record-acceptance",
            "Record one interactive, write-once attestation for exact installed candidate bytes.");
        command.Options.Add(candidateOption);
        command.Options.Add(testerOption);
        command.Validators.Add(result =>
        {
            if (result.GetValue(candidateOption) is null)
            {
                result.AddError("Command 'record-acceptance' requires '--candidate'.");
            }
        });
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await services.AcceptanceRecorder.RecordAsync(
                Path.GetFullPath(parseResult.GetValue(candidateOption)!),
                parseResult.GetValue(testerOption),
                cancellationToken).ConfigureAwait(false);
            return DiagnosticRenderer.Render(
                result,
                OutputFormat.Human,
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error);
        });
        return command;
    }

    private static Command CreateVerifyReleaseCommand(PipelineServices services)
    {
        var candidateOption = new Option<string?>("--candidate")
        {
            Description =
                "Exact release-candidate directory whose upload readiness will be verified."
        };
        var formatOption = new Option<string>("--format")
        {
            Description = "Output format: human or json.",
            DefaultValueFactory = _ => "human"
        };
        AddExplicitPathValidator(candidateOption);
        formatOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!string.Equals(value, "human", StringComparison.Ordinal) &&
                !string.Equals(value, "json", StringComparison.Ordinal))
            {
                result.AddError("Option '--format' accepts only 'human' or 'json'.");
            }
        });

        var command = new Command(
            "verify-release",
            "Deterministically verify one candidate for deliberate manual ONI Uploader handoff.");
        command.Options.Add(candidateOption);
        command.Options.Add(formatOption);
        command.Validators.Add(result =>
        {
            if (result.GetValue(candidateOption) is null)
            {
                result.AddError("Command 'verify-release' requires '--candidate'.");
            }
        });
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var result = await services.ReleaseCandidateVerifier.VerifyAsync(
                Path.GetFullPath(parseResult.GetValue(candidateOption)!),
                cancellationToken).ConfigureAwait(false);
            var format = string.Equals(
                parseResult.GetValue(formatOption),
                "json",
                StringComparison.Ordinal)
                ? OutputFormat.Json
                : OutputFormat.Human;
            return DiagnosticRenderer.Render(
                result,
                format,
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

    private static async Task<OperationResult<ModInstallationResult>> InstallAsync(
        PipelineServices services,
        string? modPath,
        string? candidateDirectory,
        string? buildResultPath,
        InstallTarget target,
        EnvironmentDiscoveryRequest environmentRequest,
        CancellationToken cancellationToken)
    {
        if (candidateDirectory is not null)
        {
            var resolvedCandidateDirectory = Path.GetFullPath(candidateDirectory);
            var environmentResult = await services.EnvironmentDiscovery.DiscoverAsync(
                resolvedCandidateDirectory,
                environmentRequest,
                cancellationToken).ConfigureAwait(false);
            if (!environmentResult.IsSuccess)
            {
                return ConvertFailure<PipelineEnvironment, ModInstallationResult>(
                    environmentResult);
            }

            return await services.ModInstaller.InstallCandidateAsync(
                resolvedCandidateDirectory,
                target,
                environmentResult.Value!,
                cancellationToken).ConfigureAwait(false);
        }

        var contextResult = await ResolveReadOnlyContextAsync(
            services,
            modPath!,
            environmentRequest,
            cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ConvertFailure<ReadOnlyContext, ModInstallationResult>(contextResult);
        }

        var context = contextResult.Value!;
        return await services.ModInstaller.InstallBuildAsync(
            context.Profile,
            context.Metadata,
            Path.GetFullPath(buildResultPath!),
            target,
            context.Environment,
            cancellationToken).ConfigureAwait(false);
    }

    private static InstallTarget ParseInstallTarget(string value) =>
        value switch
        {
            "dev" => InstallTarget.Dev,
            "local" => InstallTarget.Local,
            _ => throw new InvalidOperationException(
                "The validated installation target was not canonical.")
        };

    private static void AddExplicitPathValidator(Option<string?> option)
    {
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (value is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError($"Option '{option.Name}' requires a nonempty path.");
                return;
            }

            try
            {
                _ = Path.GetFullPath(value);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException)
            {
                result.AddError(
                    $"Option '{option.Name}' requires a valid path: {exception.Message}");
            }
        });
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
