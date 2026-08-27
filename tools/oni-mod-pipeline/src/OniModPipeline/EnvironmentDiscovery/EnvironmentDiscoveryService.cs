using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MaksymShostak.OniModPipeline.EnvironmentDiscovery;

internal sealed partial class EnvironmentDiscoveryService(
    IExternalProcessRunner processRunner,
    EnvironmentVariableSource environmentVariables,
    GameInstallationCandidateSource candidateSource,
    SteamLibraryCatalog steamLibraryCatalog)
{
    private static readonly string[] RequiredManagedAssemblies =
    [
        "Assembly-CSharp.dll",
        "0Harmony.dll"
    ];

    internal async Task<OperationResult<PipelineEnvironment>> DiscoverAsync(
        ModProfile profile,
        EnvironmentDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string>? steamLibraries = null;
        IReadOnlyList<string> GetSteamLibraries() =>
            steamLibraries ??= steamLibraryCatalog.DiscoverLibraries(
                candidateSource.SteamRoots);

        var gameResult = SelectGameDirectory(request.GameDirectory, GetSteamLibraries);
        if (!gameResult.IsSuccess)
        {
            return ConvertFailure<SelectedGame, PipelineEnvironment>(gameResult);
        }

        var userDataResult = SelectUserDataDirectory(
            request.UserDataDirectory,
            gameResult.Value!,
            GetSteamLibraries);
        if (!userDataResult.IsSuccess)
        {
            return ConvertFailure<string, PipelineEnvironment>(userDataResult);
        }

        var artifactsResult = await SelectArtifactsDirectoryAsync(
            profile,
            request.ArtifactsDirectory,
            userDataResult.Value!,
            GetSteamLibraries,
            cancellationToken).ConfigureAwait(false);
        if (!artifactsResult.IsSuccess)
        {
            return ConvertFailure<string, PipelineEnvironment>(artifactsResult);
        }

        var sdkResult = await DiscoverDotnetSdkVersionAsync(
            profile.ModRoot,
            cancellationToken).ConfigureAwait(false);
        if (!sdkResult.IsSuccess)
        {
            return ConvertFailure<string, PipelineEnvironment>(sdkResult);
        }

        var userDataDirectory = userDataResult.Value!;
        return new OperationResult<PipelineEnvironment>(
            new PipelineEnvironment(
                gameResult.Value!.GameDirectory,
                gameResult.Value.ManagedAssemblyDirectory,
                userDataDirectory,
                Path.Combine(userDataDirectory, "mods", "Dev"),
                Path.Combine(userDataDirectory, "mods", "Local"),
                artifactsResult.Value!,
                sdkResult.Value!,
                candidateSource.OperatingSystem.ToString(),
                RuntimeInformation.OSArchitecture.ToString()),
            [],
            PipelineExitCode.Success);
    }

    private OperationResult<SelectedGame> SelectGameDirectory(
        string? explicitDirectory,
        Func<IReadOnlyList<string>> getSteamLibraries)
    {
        if (explicitDirectory is not null)
        {
            return ValidateSelectedGame(explicitDirectory, "explicit --game-directory");
        }

        var environmentDirectory = environmentVariables.Get(
            EnvironmentVariableSource.GameDirectoryVariable);
        if (environmentDirectory is not null)
        {
            return ValidateSelectedGame(
                environmentDirectory,
                EnvironmentVariableSource.GameDirectoryVariable);
        }

        var automaticCandidates = candidateSource.GetAutomaticGameDirectories(
            getSteamLibraries());
        var validGames = automaticCandidates
            .Select(TryResolveGame)
            .OfType<SelectedGame>()
            .DistinctBy(
                game => game.GameDirectory,
                candidateSource.PathComparer)
            .OrderBy(
                game => game.GameDirectory,
                candidateSource.PathComparer)
            .ToArray();
        if (validGames.Length > 1)
        {
            return Failure<SelectedGame>(
                DiagnosticCatalog.AmbiguousGameInstallation(
                    validGames.Select(game => game.GameDirectory).ToArray()),
                PipelineExitCode.EnvironmentUnavailable);
        }

        if (validGames.Length == 1)
        {
            return Success(validGames[0]);
        }

        return Failure<SelectedGame>(
            DiagnosticCatalog.MissingGameAssembly(
                automaticCandidates,
                RequiredManagedAssemblies),
            PipelineExitCode.EnvironmentUnavailable);
    }

    private OperationResult<SelectedGame> ValidateSelectedGame(
        string directory,
        string source)
    {
        var selectedGame = TryResolveGame(directory);
        if (selectedGame is not null)
        {
            return Success(selectedGame);
        }

        return Failure<SelectedGame>(
            DiagnosticCatalog.MissingGameAssembly(
                [$"{source}: {RenderPath(directory)}"],
                RequiredManagedAssemblies),
            PipelineExitCode.EnvironmentUnavailable);
    }

    private SelectedGame? TryResolveGame(string directory)
    {
        if (!TryGetFullPath(directory, out var gameDirectory) ||
            !Directory.Exists(gameDirectory))
        {
            return null;
        }

        var managedDirectory = candidateSource.GetManagedAssemblyDirectory(
            gameDirectory);
        return RequiredManagedAssemblies.All(fileName =>
            File.Exists(Path.Combine(managedDirectory, fileName)))
            ? new SelectedGame(gameDirectory, managedDirectory)
            : null;
    }

    private OperationResult<string> SelectUserDataDirectory(
        string? explicitDirectory,
        SelectedGame selectedGame,
        Func<IReadOnlyList<string>> getSteamLibraries)
    {
        if (explicitDirectory is not null)
        {
            return ValidateSelectedUserData(
                explicitDirectory,
                "explicit --user-data-directory");
        }

        var environmentDirectory = environmentVariables.Get(
            EnvironmentVariableSource.UserDataDirectoryVariable);
        if (environmentDirectory is not null)
        {
            return ValidateSelectedUserData(
                environmentDirectory,
                EnvironmentVariableSource.UserDataDirectoryVariable);
        }

        var matchingSteamLibraries = getSteamLibraries()
            .Where(library => PathsEqual(
                Path.Combine(
                    library,
                    "steamapps",
                    "common",
                    "OxygenNotIncluded"),
                selectedGame.GameDirectory))
            .ToArray();
        var automaticCandidates = candidateSource.GetAutomaticUserDataDirectories(
            matchingSteamLibraries);
        var selectedDirectory = automaticCandidates.FirstOrDefault(IsValidUserDataRoot);
        return selectedDirectory is null
            ? Failure<string>(
                DiagnosticCatalog.MissingUserDataDirectory(automaticCandidates),
                PipelineExitCode.EnvironmentUnavailable)
            : Success(Path.GetFullPath(selectedDirectory));
    }

    private OperationResult<string> ValidateSelectedUserData(
        string directory,
        string source)
    {
        if (TryGetFullPath(directory, out var fullPath) &&
            IsValidUserDataRoot(fullPath))
        {
            return Success(fullPath);
        }

        return Failure<string>(
            DiagnosticCatalog.MissingUserDataDirectory(
                [$"{source}: {RenderPath(directory)}"]),
            PipelineExitCode.EnvironmentUnavailable);
    }

    private bool IsValidUserDataRoot(string directory)
    {
        if (!Directory.Exists(directory) || IsFileSystemRoot(directory))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(directory);
        return !PathsEqual(fullPath, candidateSource.HomeDirectory) &&
            !PathsEqual(fullPath, candidateSource.DocumentsDirectory);
    }

    private async Task<OperationResult<string>> SelectArtifactsDirectoryAsync(
        ModProfile profile,
        string? explicitDirectory,
        string userDataDirectory,
        Func<IReadOnlyList<string>> getSteamLibraries,
        CancellationToken cancellationToken)
    {
        var overrideDirectory = explicitDirectory ?? environmentVariables.Get(
            EnvironmentVariableSource.ArtifactsDirectoryVariable);
        if (overrideDirectory is not null)
        {
            if (!Path.IsPathFullyQualified(overrideDirectory) ||
                !TryGetFullPath(overrideDirectory, out var fullOverride))
            {
                return UnsafeArtifactsDirectory(
                    overrideDirectory,
                    "the override must be an absolute path");
            }

            var unsafeReason = GetUnsafeArtifactsReason(
                fullOverride,
                userDataDirectory,
                getSteamLibraries);
            if (unsafeReason is not null)
            {
                return UnsafeArtifactsDirectory(fullOverride, unsafeReason);
            }

            return Success(fullOverride);
        }

        var gitWorktreeRoot = await TryDiscoverGitWorktreeRootAsync(
            profile.ModRoot,
            cancellationToken).ConfigureAwait(false);
        var artifactParent = gitWorktreeRoot ?? Path.GetFullPath(profile.ModRoot);
        var defaultArtifactsDirectory = Path.Combine(artifactParent, "artifacts");
        var defaultUnsafeReason = GetUnsafeArtifactsReason(
            defaultArtifactsDirectory,
            userDataDirectory,
            getSteamLibraries);
        return defaultUnsafeReason is null
            ? Success(defaultArtifactsDirectory)
            : UnsafeArtifactsDirectory(
                defaultArtifactsDirectory,
                defaultUnsafeReason);
    }

    private string? GetUnsafeArtifactsReason(
        string artifactsDirectory,
        string userDataDirectory,
        Func<IReadOnlyList<string>> getSteamLibraries)
    {
        if (File.Exists(artifactsDirectory))
        {
            return "the selected path is an existing file";
        }

        if (IsFileSystemRoot(artifactsDirectory))
        {
            return "a filesystem root cannot be used as an artifact directory";
        }

        if (PathsEqual(artifactsDirectory, candidateSource.HomeDirectory))
        {
            return "the current user's home directory cannot be used as an artifact directory";
        }

        if (PathsEqual(artifactsDirectory, candidateSource.DocumentsDirectory))
        {
            return "the current user's Documents directory cannot be used as an artifact directory";
        }

        if (PathsEqual(artifactsDirectory, userDataDirectory))
        {
            return "the ONI user-data root cannot be used as an artifact directory";
        }

        if (getSteamLibraries().Any(library => PathsEqual(
            artifactsDirectory,
            library)))
        {
            return "a Steam library root cannot be used as an artifact directory";
        }

        if (Directory.Exists(artifactsDirectory))
        {
            var attributes = File.GetAttributes(artifactsDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                new DirectoryInfo(artifactsDirectory).LinkTarget is not null)
            {
                return "an artifact-directory override cannot be a link or reparse point";
            }
        }

        return null;
    }

    private async Task<string?> TryDiscoverGitWorktreeRootAsync(
        string modRoot,
        CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync(
                new ProcessRequest(
                    "git",
                    ["rev-parse", "--show-toplevel"],
                    Path.GetFullPath(modRoot),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows()
                            ? "NUL"
                            : "/dev/null",
                        ["GIT_CONFIG_NOSYSTEM"] = "1",
                        ["GIT_OPTIONAL_LOCKS"] = "0"
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is Win32Exception or FileNotFoundException)
        {
            return null;
        }

        if (result.ExitCode != 0 ||
            !TryGetFullPath(result.StandardOutput.TrimEnd('\r', '\n'), out var root))
        {
            return null;
        }

        var relativeModPath = Path.GetRelativePath(root, Path.GetFullPath(modRoot));
        return relativeModPath == "." || !IsOutside(relativeModPath)
            ? root
            : null;
    }

    private async Task<OperationResult<string>> DiscoverDotnetSdkVersionAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync(
                new ProcessRequest(
                    "dotnet",
                    ["--version"],
                    Path.GetFullPath(workingDirectory),
                    new Dictionary<string, string>(StringComparer.Ordinal)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is Win32Exception or FileNotFoundException)
        {
            return Failure<string>(
                DiagnosticCatalog.MissingDotnetSdk(exception.Message),
                PipelineExitCode.EnvironmentUnavailable);
        }

        var version = result.StandardOutput.Trim();
        if (result.ExitCode != 0 || !StableDotnetSdkVersion().IsMatch(version))
        {
            var evidence = result.ExitCode == 0
                ? $"dotnet --version returned '{version}'."
                : $"dotnet --version exited {result.ExitCode}: {result.StandardError.Trim()}";
            return Failure<string>(
                DiagnosticCatalog.MissingDotnetSdk(evidence),
                PipelineExitCode.EnvironmentUnavailable);
        }

        return Success(version);
    }

    private bool PathsEqual(string first, string second) =>
        candidateSource.PathComparer.Equals(
            NormalizeIdentity(first),
            NormalizeIdentity(second));

    private static string NormalizeIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private static bool IsFileSystemRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return string.Equals(
            NormalizeIdentity(fullPath),
            Path.GetPathRoot(fullPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFullPath(string? path, out string fullPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                fullPath = string.Empty;
                return false;
            }

            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool IsOutside(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath == ".." ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RenderPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "<empty>" : path;

    private static OperationResult<T> Success<T>(T value) =>
        new(value, [], PipelineExitCode.Success);

    private static OperationResult<T> Failure<T>(
        Diagnostic diagnostic,
        PipelineExitCode exitCode) =>
        new(default, [diagnostic], exitCode);

    private static OperationResult<TOutput> ConvertFailure<TInput, TOutput>(
        OperationResult<TInput> result) =>
        new(default, result.Diagnostics, result.ExitCode);

    private static OperationResult<string> UnsafeArtifactsDirectory(
        string path,
        string reason) =>
        Failure<string>(
            DiagnosticCatalog.UnsafeArtifactsDirectory(path, reason),
            PipelineExitCode.InvalidInput);

    [GeneratedRegex(@"\A10\.0\.4[0-9]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex StableDotnetSdkVersion();

    private sealed record SelectedGame(
        string GameDirectory,
        string ManagedAssemblyDirectory);
}
