using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.EnvironmentDiscovery;

[TestClass]
public sealed class EnvironmentDiscoveryServiceTests
{
    [TestMethod]
    public async Task DiscoverAsync_WhenCliGameDirectoryIsValid_UsesItBeforeEnvironmentAndDiscovery()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var cliGameDirectory = CreateValidGame(
            temporaryDirectory.GetPath("cli-game"),
            HostOperatingSystem.Windows);
        var environmentGameDirectory = CreateValidGame(
            temporaryDirectory.GetPath("environment-game"),
            HostOperatingSystem.Windows);
        var automaticSteamRoot = CreateSteamGame(
            temporaryDirectory.GetPath("automatic-steam"),
            HostOperatingSystem.Windows);
        var userDataDirectory = CreateUserData(temporaryDirectory.GetPath("user-data"));
        var artifactsDirectory = temporaryDirectory.GetPath("artifacts");
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            new Dictionary<string, string?>
            {
                [EnvironmentVariableSource.GameDirectoryVariable] = environmentGameDirectory
            },
            [automaticSteamRoot]);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                cliGameDirectory,
                userDataDirectory,
                artifactsDirectory),
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Path.GetFullPath(cliGameDirectory), result.Value?.GameDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenEnvironmentOverrideIsValid_UsesItBeforeAutomaticDiscovery()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var environmentGameDirectory = CreateValidGame(
            temporaryDirectory.GetPath("environment-game"),
            HostOperatingSystem.Windows);
        var automaticSteamRoot = CreateSteamGame(
            temporaryDirectory.GetPath("automatic-steam"),
            HostOperatingSystem.Windows);
        var userDataDirectory = CreateUserData(temporaryDirectory.GetPath("user-data"));
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            new Dictionary<string, string?>
            {
                [EnvironmentVariableSource.GameDirectoryVariable] = environmentGameDirectory
            },
            [automaticSteamRoot]);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                null,
                userDataDirectory,
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(
            Path.GetFullPath(environmentGameDirectory),
            result.Value?.GameDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenEnvironmentUserDataAndArtifactsAreValid_UsesThemBeforeDefaults()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var automaticUserData = CreateUserData(
            temporaryDirectory.GetPath("documents", "Klei", "OxygenNotIncluded"));
        var environmentUserData = CreateUserData(
            temporaryDirectory.GetPath("environment-user-data"));
        var environmentArtifacts = temporaryDirectory.GetPath("environment-artifacts");
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            new Dictionary<string, string?>
            {
                [EnvironmentVariableSource.UserDataDirectoryVariable] = environmentUserData,
                ["ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY"] = environmentArtifacts
            },
            gitWorktreeRoot: temporaryDirectory.GetPath("repository"));

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                null,
                null),
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(
            Path.GetFullPath(environmentUserData),
            result.Value?.UserDataDirectory);
        Assert.AreEqual(
            Path.GetFullPath(environmentArtifacts),
            result.Value?.ArtifactsDirectory);
        Assert.AreNotEqual(
            Path.GetFullPath(automaticUserData),
            result.Value?.UserDataDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenOnlyAbbreviatedPipelineArtifactVariableIsSet_DoesNotTreatItAsAnOverride()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var worktreeRoot = temporaryDirectory.GetPath("repository");
        var abbreviatedArtifacts = temporaryDirectory.GetPath("abbreviated-artifacts");
        var profile = CreateProfile(Path.Combine(worktreeRoot, "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            new Dictionary<string, string?>
            {
                ["ONI_PIPELINE_ARTIFACTS_DIRECTORY"] = abbreviatedArtifacts
            },
            gitWorktreeRoot: worktreeRoot);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                null),
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(worktreeRoot), "artifacts"),
            result.Value?.ArtifactsDirectory);
        Assert.AreNotEqual(
            Path.GetFullPath(abbreviatedArtifacts),
            result.Value?.ArtifactsDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenTwoAutomaticGameDirectoriesAreValid_ReturnsOnip2002()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var firstSteamRoot = CreateSteamGame(
            temporaryDirectory.GetPath("steam-a"),
            HostOperatingSystem.Windows);
        var secondSteamRoot = CreateSteamGame(
            temporaryDirectory.GetPath("steam-b"),
            HostOperatingSystem.Windows);
        CreateUserData(temporaryDirectory.GetPath("documents", "Klei", "OxygenNotIncluded"));
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            environment: null,
            [firstSteamRoot, secondSteamRoot]);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                null,
                null,
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2002", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "steam-a");
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "steam-b");
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenManagedAnchorIsMissing_ReturnsOnip2003()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var gameDirectory = temporaryDirectory.GetPath("game");
        var managedDirectory = GetManagedDirectory(
            gameDirectory,
            HostOperatingSystem.Windows);
        Directory.CreateDirectory(managedDirectory);
        File.WriteAllText(Path.Combine(managedDirectory, "Assembly-CSharp.dll"), "assembly");
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(temporaryDirectory, HostOperatingSystem.Windows);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                gameDirectory,
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2003", result.Diagnostics.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single().Evidence, "0Harmony.dll");
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenArtifactOverrideIsRelative_ReturnsInvalidInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(temporaryDirectory, HostOperatingSystem.Windows);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                "relative-artifacts"),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1003", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenExplicitGameOverrideIsInvalid_DoesNotFallThroughToAutomaticDiscovery()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var automaticSteamRoot = CreateSteamGame(
            temporaryDirectory.GetPath("automatic-steam"),
            HostOperatingSystem.Windows);
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            environment: null,
            [automaticSteamRoot]);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                temporaryDirectory.GetPath("invalid-explicit-game"),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2003", result.Diagnostics.Single().Id);
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenEnvironmentGameOverrideIsInvalid_DoesNotFallThroughToAutomaticDiscovery()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var automaticSteamRoot = CreateSteamGame(
            temporaryDirectory.GetPath("automatic-steam"),
            HostOperatingSystem.Windows);
        var invalidEnvironmentGame = temporaryDirectory.GetPath("invalid-environment-game");
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            new Dictionary<string, string?>
            {
                [EnvironmentVariableSource.GameDirectoryVariable] = invalidEnvironmentGame
            },
            [automaticSteamRoot]);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                null,
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2003", result.Diagnostics.Single().Id);
        StringAssert.Contains(
            result.Diagnostics.Single().Evidence,
            EnvironmentVariableSource.GameDirectoryVariable);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenExplicitUserDataOverrideIsInvalid_DoesNotFallThroughToAutomaticDiscovery()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        CreateUserData(temporaryDirectory.GetPath("documents", "Klei", "OxygenNotIncluded"));
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(temporaryDirectory, HostOperatingSystem.Windows);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                temporaryDirectory.GetPath("missing-explicit-user-data"),
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2004", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenNoUserDataCandidateExists_ReturnsOnip2004()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(temporaryDirectory, HostOperatingSystem.MacOS);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.MacOS),
                null,
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2004", result.Diagnostics.Single().Id);
        StringAssert.Contains(
            result.Diagnostics.Single().Evidence,
            "unity.Klei.Oxygen Not Included");
    }

    [TestMethod]
    public async Task DiscoverAsync_OnLinux_DoesNotPairGameWithProtonDataFromAnotherLibrary()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var gameLibrary = CreateSteamGame(
            temporaryDirectory.GetPath("game-library"),
            HostOperatingSystem.Linux);
        var unrelatedLibrary = temporaryDirectory.GetPath("unrelated-library");
        Directory.CreateDirectory(GetProtonUserDataDirectory(unrelatedLibrary));
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Linux,
            environment: null,
            [gameLibrary, unrelatedLibrary]);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                null,
                null,
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2004", result.Diagnostics.Single().Id);
        Assert.IsFalse(result.Diagnostics.Single().Evidence.Contains(
            GetProtonUserDataDirectory(unrelatedLibrary),
            StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenModIsInGit_DefaultsArtifactsToWorktreeRoot()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var worktreeRoot = temporaryDirectory.GetPath("repository");
        var profile = CreateProfile(Path.Combine(worktreeRoot, "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            gitWorktreeRoot: worktreeRoot);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                null),
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(worktreeRoot), "artifacts"),
            result.Value?.ArtifactsDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenModIsOutsideGit_DefaultsArtifactsToModRoot()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory.GetPath("standalone-mod"));
        var service = CreateService(temporaryDirectory, HostOperatingSystem.Linux);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Linux),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                null),
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(profile.ModRoot), "artifacts"),
            result.Value?.ArtifactsDirectory);
    }

    [TestMethod]
    public async Task DiscoverAsync_WhenArtifactOverrideIsProtectedRoot_ReturnsInvalidInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(temporaryDirectory, HostOperatingSystem.Windows);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                temporaryDirectory.GetPath("home")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.InvalidInput, result.ExitCode);
        Assert.AreEqual("ONIP1003", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    [DataRow("10.0.399")]
    [DataRow("10.0.500")]
    [DataRow("10.0.400-preview.1")]
    public async Task DiscoverAsync_WhenDotnetSdkIsNotStableTenPointZeroFourHundred_ReturnsOnip2001(
        string sdkVersion)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfile(temporaryDirectory.GetPath("repository", "mods", "example"));
        var service = CreateService(
            temporaryDirectory,
            HostOperatingSystem.Windows,
            sdkVersion: sdkVersion);

        var result = await service.DiscoverAsync(
            profile,
            new EnvironmentDiscoveryRequest(
                CreateValidGame(
                    temporaryDirectory.GetPath("game"),
                    HostOperatingSystem.Windows),
                CreateUserData(temporaryDirectory.GetPath("user-data")),
                temporaryDirectory.GetPath("artifacts")),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.EnvironmentUnavailable, result.ExitCode);
        Assert.AreEqual("ONIP2001", result.Diagnostics.Single().Id);
    }

    [TestMethod]
    [DataRow("Windows", "Klei/OxygenNotIncluded")]
    [DataRow(
        "MacOS",
        "Library/Application Support/unity.Klei.Oxygen Not Included")]
    [DataRow(
        "Linux",
        ".config/unity3d/Klei/Oxygen Not Included")]
    public void CandidateSource_UsesNativeUserDataConventionAndExactInstallTargetCase(
        string operatingSystemName,
        string expectedRelativePath)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var operatingSystem = Enum.Parse<HostOperatingSystem>(operatingSystemName);
        var homeDirectory = temporaryDirectory.GetPath("home");
        var documentsDirectory = temporaryDirectory.GetPath("documents");
        var source = new GameInstallationCandidateSource(
            operatingSystem,
            homeDirectory,
            documentsDirectory,
            []);
        var expectedRoot = operatingSystem == HostOperatingSystem.Windows
            ? documentsDirectory
            : homeDirectory;
        var expectedUserData = Path.Combine(
            expectedRoot,
            expectedRelativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.AreEqual(
            Normalize(expectedUserData),
            Normalize(source.NativeUserDataDirectory));
        Assert.AreEqual(
            $"{Normalize(expectedUserData)}/mods/Dev",
            Normalize(source.NativeDevelopmentModsDirectory));
        Assert.AreEqual(
            $"{Normalize(expectedUserData)}/mods/Local",
            Normalize(source.NativeLocalModsDirectory));
    }

    [TestMethod]
    public void CandidateSource_OnLinux_IncludesProtonUserDataForEachSteamLibrary()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var firstLibrary = temporaryDirectory.GetPath("steam-a");
        var secondLibrary = temporaryDirectory.GetPath("steam-b");
        var source = new GameInstallationCandidateSource(
            HostOperatingSystem.Linux,
            temporaryDirectory.GetPath("home"),
            temporaryDirectory.GetPath("documents"),
            []);

        var candidates = source.GetAutomaticUserDataDirectories(
            [firstLibrary, secondLibrary]);

        CollectionAssert.Contains(
            candidates.ToArray(),
            Path.Combine(
                firstLibrary,
                "steamapps",
                "compatdata",
                "457140",
                "pfx",
                "drive_c",
                "users",
                "steamuser",
                "Documents",
                "Klei",
                "OxygenNotIncluded"));
        CollectionAssert.Contains(
            candidates.ToArray(),
            Path.Combine(
                secondLibrary,
                "steamapps",
                "compatdata",
                "457140",
                "pfx",
                "drive_c",
                "users",
                "steamuser",
                "Documents",
                "Klei",
                "OxygenNotIncluded"));
    }

    private static EnvironmentDiscoveryService CreateService(
        TemporaryDirectory temporaryDirectory,
        HostOperatingSystem operatingSystem,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? steamRoots = null,
        string? gitWorktreeRoot = null,
        string sdkVersion = "10.0.400")
    {
        var candidateSource = new GameInstallationCandidateSource(
            operatingSystem,
            temporaryDirectory.GetPath("home"),
            temporaryDirectory.GetPath("documents"),
            steamRoots ?? []);
        var environmentSource = new EnvironmentVariableSource(
            environment ?? new Dictionary<string, string?>());
        return new EnvironmentDiscoveryService(
            new DiscoveryProcessRunner(sdkVersion, gitWorktreeRoot),
            environmentSource,
            candidateSource,
            new SteamLibraryCatalog());
    }

    private static ModProfile CreateProfile(string modRoot)
    {
        Directory.CreateDirectory(modRoot);
        return new ModProfile(
            1,
            Path.Combine(modRoot, "oni-mod-pipeline.toml"),
            modRoot,
            "mod.yaml",
            "mod_info.yaml",
            null,
            [],
            new WorkshopListingProfile(
                "description.bbcode",
                "change-notes.bbcode",
                "preview.png",
                [],
                [],
                8000,
                8000),
            new LocalInstallProfile("Example"),
            [],
            []);
    }

    private static string CreateValidGame(
        string gameDirectory,
        HostOperatingSystem operatingSystem)
    {
        var managedDirectory = GetManagedDirectory(gameDirectory, operatingSystem);
        Directory.CreateDirectory(managedDirectory);
        File.WriteAllText(Path.Combine(managedDirectory, "Assembly-CSharp.dll"), "assembly");
        File.WriteAllText(Path.Combine(managedDirectory, "0Harmony.dll"), "harmony");
        return gameDirectory;
    }

    private static string CreateSteamGame(
        string steamRoot,
        HostOperatingSystem operatingSystem)
    {
        CreateValidGame(
            Path.Combine(
                steamRoot,
                "steamapps",
                "common",
                "OxygenNotIncluded"),
            operatingSystem);
        return steamRoot;
    }

    private static string GetManagedDirectory(
        string gameDirectory,
        HostOperatingSystem operatingSystem) =>
        operatingSystem == HostOperatingSystem.MacOS
            ? Path.Combine(
                gameDirectory,
                "OxygenNotIncluded.app",
                "Contents",
                "Resources",
                "Data",
                "Managed")
            : Path.Combine(gameDirectory, "OxygenNotIncluded_Data", "Managed");

    private static string CreateUserData(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetProtonUserDataDirectory(string steamLibrary) =>
        Path.Combine(
            steamLibrary,
            "steamapps",
            "compatdata",
            "457140",
            "pfx",
            "drive_c",
            "users",
            "steamuser",
            "Documents",
            "Klei",
            "OxygenNotIncluded");

    private static string Normalize(string path) =>
        Path.GetFullPath(path).Replace((char)92, '/').TrimEnd('/');

    private sealed class DiscoveryProcessRunner(
        string sdkVersion,
        string? gitWorktreeRoot) : IExternalProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(request.FileName, "dotnet", StringComparison.Ordinal))
            {
                CollectionAssert.AreEqual(
                    new[] { "--version" },
                    request.Arguments.ToArray());
                return Task.FromResult(new ProcessResult(
                    0,
                    $"{sdkVersion}{Environment.NewLine}",
                    string.Empty));
            }

            Assert.AreEqual("git", request.FileName);
            CollectionAssert.AreEqual(
                new[] { "rev-parse", "--show-toplevel" },
                request.Arguments.ToArray());
            return Task.FromResult(gitWorktreeRoot is null
                ? new ProcessResult(128, string.Empty, "not a Git worktree")
                : new ProcessResult(
                    0,
                    $"{gitWorktreeRoot}{Environment.NewLine}",
                    string.Empty));
        }
    }
}
