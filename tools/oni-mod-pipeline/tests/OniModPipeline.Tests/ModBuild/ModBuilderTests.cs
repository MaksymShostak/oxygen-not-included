using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ModBuild;
public sealed class ModBuilderTests
{
    [Test]
    [Arguments("Directory.Packages.props")]
    [Arguments("global.json")]
    [Arguments("tests/TUnitProjectDefaults.cs")]
    [Arguments("tests/TUnitProject.props")]
    public async Task BuildAsync_WithCentralDotnetControls_RecordsTheirDigests(string control)
    {
        using var fixture = new BuildFixture();
        var path = Path.GetFullPath(Path.Combine(fixture.WorktreeRoot, control));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "original\n");
        fixture.ProcessRunner.BuildAction = fixture.WritePrimaryOutput;
        var result = await new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter())
            .BuildAsync(fixture.CreateRequest(), CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Inputs.Any(input => input.Path == path)).IsTrue();
    }

    [Test]
    [Arguments("Directory.Packages.props", "edit")]
    [Arguments("Directory.Packages.props", "create")]
    [Arguments("Directory.Packages.props", "delete")]
    [Arguments("global.json", "edit")]
    [Arguments("global.json", "create")]
    [Arguments("global.json", "delete")]
    [Arguments("tests/TUnitProjectDefaults.cs", "edit")]
    [Arguments("tests/TUnitProjectDefaults.cs", "create")]
    [Arguments("tests/TUnitProjectDefaults.cs", "delete")]
    [Arguments("tests/TUnitProject.props", "edit")]
    [Arguments("tests/TUnitProject.props", "create")]
    [Arguments("tests/TUnitProject.props", "delete")]
    public async Task BuildAsync_WhenCentralDotnetControlsChange_RejectsStaleBuild(string control, string change)
    {
        using var fixture = new BuildFixture();
        var path = Path.GetFullPath(Path.Combine(fixture.WorktreeRoot, control));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (change != "create") File.WriteAllText(path, "original\n");
        fixture.ProcessRunner.BuildAction = request =>
        {
            fixture.WritePrimaryOutput(request);
            if (change == "delete") File.Delete(path);
            else File.WriteAllText(path, "changed during build\n");
        };
        var result = await new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter())
            .BuildAsync(fixture.CreateRequest(), CancellationToken.None);
        await AssertDiagnostic(result, DiagnosticIds.SourceChangedDuringBuild);
        await Assert.That(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json"))).IsFalse();
    }

    [Test]
    public async Task BuildAsync_WhenProfileHasBuild_RestoresLockedThenBuildsWithoutShell()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.BuildAction = fixture.WritePrimaryOutput;
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success).Because(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        Assert.NotNull(result.Value);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(3);
        await Assert.That(fixture.ProcessRunner.Requests.All(request => request.FileName == "dotnet")).IsTrue();
        await Assert.That(fixture.ProcessRunner.Requests[0].Arguments.ToArray()).IsEquivalentTo(fixture.ExpectedRestoreArguments, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.ProcessRunner.Requests[1].Arguments.ToArray()).IsEquivalentTo(fixture.ExpectedResolveReferencesArguments, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.ProcessRunner.Requests[2].Arguments.ToArray()).IsEquivalentTo(fixture.ExpectedBuildArguments, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fixture.ProcessRunner.Requests.All(
            request => request.EnvironmentVariables.Count == 0)).IsTrue();
        await Assert.That(result.Value.StructuredBuildArguments.ToArray()).IsEquivalentTo(fixture.ExpectedBuildArguments, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Value.PrimaryOutputPath).IsEqualTo(fixture.PrimaryOutputPath);
        await Assert.That(result.Value.Outputs.Count).IsEqualTo(1);
        await Assert.That(result.Value.Outputs[0].Path).IsEqualTo(fixture.PrimaryOutputPath);
        await Assert.That(result.Value.MergeInputs.Single().Path).IsEqualTo(fixture.MergeInputPath);
        await Assert.That(result.Value.GameReferences.Single().Path).IsEqualTo(fixture.GameReferencePath);
        await Assert.That(result.Value.Inputs.Any(input => input.Path == fixture.SourcePath)).IsTrue();
        await Assert.That(result.Value.SourceBytesUnchanged).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json"))).IsTrue();
    }

    [Test]
    public async Task BuildAsync_WhenSourceBytesChange_ReturnsOnip3003()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.BuildAction = request =>
        {
            fixture.WritePrimaryOutput(request);
            File.AppendAllText(fixture.SourcePath, "// changed during build\n");
        };
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.SourceChangedDuringBuild);
        await Assert.That(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json"))).IsFalse();
    }

    [Test]
    public async Task BuildAsync_WhenPrimaryOutputIsMissing_ReturnsOnip3004()
    {
        using var fixture = new BuildFixture();
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.BuildOutputMissing);
        await Assert.That(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json"))).IsFalse();
    }

    [Test]
    public async Task BuildAsync_WhenLockedRestoreFails_ReturnsOnip3001()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.RestoreExitCode = 1;
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.RestoreFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BuildAsync_WhenBuildProcessFails_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.BuildExitCode = 1;
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(3);
    }

    [Test]
    public async Task BuildAsync_WhenDeclaredMergeInputIsMissing_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        fixture.SetCopyLocalReferences();
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BuildAsync_WhenCopyLocalReferenceIsUndeclared_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        var undeclared = fixture.CreateReferenceFile("Undeclared.Dependency.dll");
        fixture.SetCopyLocalReferences(fixture.MergeInputPath, undeclared);
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BuildAsync_WhenDeclaredMergeInputResolvesTwice_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        var duplicate = fixture.CreateReferenceFile(
            Path.Combine("duplicate", "PLib.dll"));
        fixture.SetCopyLocalReferences(fixture.MergeInputPath, duplicate);
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BuildAsync_WhenPrimaryOutputIsManagedAssembly_RecordsThreeVersionMeanings()
    {
        using var fixture = new BuildFixture();
        var assembly = typeof(ModBuilderTests).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var releaseVersion = informationalVersion.Split('+')[0];
        fixture.ProcessRunner.BuildAction = request =>
        {
            var destination = fixture.GetPrimaryOutputPath(request);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(assembly.Location, destination);
        };
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(releaseVersion),
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success).Because(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        Assert.NotNull(result.Value?.PrimaryAssemblyVersion);
        await Assert.That(result.Value.PrimaryAssemblyVersion.AssemblyVersion).IsEqualTo(assembly.GetName().Version!.ToString());
        await Assert.That(result.Value.PrimaryAssemblyVersion.FileVersion).IsEqualTo(assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
        await Assert.That(result.Value.PrimaryAssemblyVersion.InformationalVersion).IsEqualTo(informationalVersion);
    }

    [Test]
    public async Task BuildAsync_WhenArtifactReleaseVersionDiffersFromMetadata_RejectsBuild()
    {
        using var fixture = new BuildFixture();
        var assembly = typeof(ModBuilderTests).Assembly;
        var artifactVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
        const string requestedVersion = "65534.65534";
        await Assert.That(artifactVersion).IsNotEqualTo(requestedVersion).Because("Fixture must contain a real version mismatch.");
        fixture.ProcessRunner.BuildAction = request =>
        {
            var destination = fixture.GetPrimaryOutputPath(request);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(assembly.Location, destination);
        };
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(fixture.CreateRequest(requestedVersion), CancellationToken.None);

        await AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        await Assert.That(result.Diagnostics.Any(diagnostic =>
            diagnostic.Evidence.Contains("informational version", StringComparison.Ordinal))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json"))).IsFalse();
    }

    [Test]
    public async Task BuildAsync_WhenPrimaryOutputIsManagedAssembly_RecordsTargetFrameworkMonikerFromArtifactMetadata()
    {
        using var fixture = new BuildFixture();
        var assembly = typeof(ModBuilderTests).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var releaseVersion = informationalVersion.Split('+')[0];
        fixture.ProcessRunner.BuildAction = request =>
        {
            var destination = fixture.GetPrimaryOutputPath(request);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(assembly.Location, destination);
        };
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(releaseVersion),
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success).Because(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        using var buildResultJson = JsonDocument.Parse(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.RunRoot, "build-result.json")));
        await Assert.That(buildResultJson.RootElement.TryGetProperty(
                "primaryAssemblyTargetFrameworkMoniker",
                out var targetFrameworkMoniker)).IsTrue().Because("The build result must carry target-framework evidence read from the exact primary assembly.");
        await Assert.That(targetFrameworkMoniker.GetString()).IsEqualTo("net10.0");
    }

    [Test]
    public async Task BuildAsync_WhenManagedArtifactDeclaresNetStandardFramework_RecordsCanonicalMonikerAndFrameworkName()
    {
        using var fixture = new BuildFixture();
        var assembly = typeof(ModBuilderTests).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var releaseVersion = informationalVersion.Split('+')[0];
        fixture.ProcessRunner.BuildAction = request =>
        {
            var destination = fixture.GetPrimaryOutputPath(request);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(assembly.Location, destination);
            ReplaceUniqueEqualLengthUtf8Sequence(
                destination,
                ".NETCoreApp,Version=v10.0",
                ".NETStandard,Version=v2.1");
        };
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(releaseVersion),
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success).Because(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        using var buildResultJson = JsonDocument.Parse(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.RunRoot, "build-result.json")));
        await Assert.That(buildResultJson.RootElement
                .GetProperty("primaryAssemblyTargetFrameworkMoniker")
                .GetString()).IsEqualTo("netstandard2.1");
        await Assert.That(buildResultJson.RootElement
                .GetProperty("primaryAssemblyTargetFrameworkName")
                .GetString()).IsEqualTo(".NETStandard,Version=v2.1");
    }

    [Test]
    public async Task BuildAsync_WhenProfileIsContentOnly_ProducesEmptySuccessfulBuildResult()
    {
        using var fixture = new BuildFixture(hasBuild: false);
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success);
        Assert.NotNull(result.Value);
        await Assert.That(fixture.ProcessRunner.Requests.Count).IsEqualTo(0);
        await Assert.That(result.Value.PrimaryOutputPath).IsNull();
        await Assert.That(result.Value.Outputs.Count).IsEqualTo(0);
        await Assert.That(result.Value.MergeInputs.Count).IsEqualTo(0);
        await Assert.That(result.Value.GameReferences.Count).IsEqualTo(0);
        await Assert.That(result.Value.StructuredBuildArguments.Count).IsEqualTo(0);
        await Assert.That(result.Value.PrimaryAssemblyVersion).IsNull();
        await Assert.That(result.Value.SourceBytesUnchanged).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json"))).IsTrue();
    }

    [Test]
    public async Task BuildAsync_WhenRunRootsContainSpacesAndDelimiters_ProducesRepeatableAssembliesInIsolation()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var worktreeRoot = temporaryDirectory.GetPath("repository");
        var modRoot = Path.Combine(worktreeRoot, "mods", "fixture");
        var sourceDirectory = Path.Combine(modRoot, "Source");
        var managedDirectory = temporaryDirectory.GetPath("game", "Managed");
        Directory.CreateDirectory(Path.Combine(worktreeRoot, ".git"));
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(managedDirectory);
        WriteCommonProfileInputs(modRoot);
        var projectPath = Path.Combine(sourceDirectory, "Fixture.csproj");
        var sourcePath = Path.Combine(sourceDirectory, "Program.cs");
        var lockPath = Path.Combine(sourceDirectory, "packages.lock.json");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
              </PropertyGroup>
              <Target Name="WritePipelineOutput" AfterTargets="Build">
                <Copy SourceFiles="$(TargetPath)" DestinationFiles="$(OniMergedModOutputPath)" />
              </Target>
            </Project>
            """);
        File.WriteAllText(sourcePath, "public static class FixtureType { }\n");
        File.WriteAllText(
            lockPath,
            """
            {
              "version": 1,
              "dependencies": {
                "net10.0": {}
              }
            }
            """);
        var profile = CreateProfile(modRoot, hasBuild: true, mergeInputs: []);
        var environment = CreateEnvironment(temporaryDirectory, managedDirectory);
        var builder = new ModBuilder(new ExternalProcessRunner(), new Utf8ArtifactWriter());
        var runRoots = new[]
        {
            temporaryDirectory.GetPath("runs", "ordinary"),
            temporaryDirectory.GetPath("runs", "with spaces;comma,value=equals")
        };
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var outputHashes = new List<string>();

        foreach (var runRoot in runRoots)
        {
            var result = await builder.BuildAsync(
                new BuildRequest(
                    profile,
                    environment,
                    "Release",
                    runRoot,
                    "1.2.3",
                    "0123456789abcdef0123456789abcdef01234567"),
                CancellationToken.None);

            await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.Success).Because(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
            Assert.NotNull(result.Value);
            await Assert.That(result.Value.PrimaryOutputPath!.StartsWith(
                Path.GetFullPath(runRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(File.Exists(result.Value.PrimaryOutputPath)).IsTrue();
            outputHashes.Add(Convert.ToHexString(SHA256.HashData(
                await File.ReadAllBytesAsync(result.Value.PrimaryOutputPath))));
            await Assert.That(await File.ReadAllBytesAsync(sourcePath)).IsEquivalentTo(sourceBytes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }

        await Assert.That(outputHashes[1]).IsEqualTo(outputHashes[0]);
        await Assert.That(CountFilesIfDirectoryExists(Path.Combine(sourceDirectory, "bin"))).IsEqualTo(0);
        await Assert.That(CountFilesIfDirectoryExists(Path.Combine(sourceDirectory, "obj"))).IsEqualTo(0);
    }

    private static void ReplaceUniqueEqualLengthUtf8Sequence(
        string filePath,
        string existingValue,
        string replacementValue)
    {
        var existingBytes = Encoding.UTF8.GetBytes(existingValue);
        var replacementBytes = Encoding.UTF8.GetBytes(replacementValue);
        if (replacementBytes.Length != existingBytes.Length)
            Assert.Fail("The metadata fixture replacement must preserve the PE blob length.");

        var fileBytes = File.ReadAllBytes(filePath);
        var matchOffsets = new List<int>();
        for (var offset = 0; offset <= fileBytes.Length - existingBytes.Length; offset++)
        {
            if (fileBytes.AsSpan(offset, existingBytes.Length).SequenceEqual(existingBytes))
            {
                matchOffsets.Add(offset);
            }
        }

        if (matchOffsets.Count != 1)
            Assert.Fail("The managed test artifact must contain exactly one target-framework name payload.");
        replacementBytes.CopyTo(fileBytes.AsSpan(matchOffsets[0], replacementBytes.Length));
        File.WriteAllBytes(filePath, fileBytes);
    }

    private static async Task AssertDiagnostic(
        OperationResult<BuildResult> result,
        string expectedId)
    {
        await Assert.That(result.ExitCode).IsEqualTo(PipelineExitCode.BuildOrTestFailed);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == expectedId)).IsTrue().Because($"Expected {expectedId}; received " +
            string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Id)));
    }

    private static int CountFilesIfDirectoryExists(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count()
            : 0;

    private static PipelineEnvironment CreateEnvironment(
        TemporaryDirectory temporaryDirectory,
        string managedDirectory) =>
        new(
            temporaryDirectory.GetPath("game"),
            managedDirectory,
            temporaryDirectory.GetPath("user-data"),
            temporaryDirectory.GetPath("user-data", "mods", "Dev"),
            temporaryDirectory.GetPath("user-data", "mods", "Local"),
            temporaryDirectory.GetPath("artifacts"),
            "10.0.400",
            "Windows",
            "X64");

    private static ModProfile CreateProfile(
        string modRoot,
        bool hasBuild,
        IReadOnlyList<string> mergeInputs)
    {
        var packageFiles = new List<PackageFileMapping>
        {
            new("mod.yaml", "mod.yaml"),
            new("mod_info.yaml", "mod_info.yaml")
        };
        if (hasBuild)
        {
            packageFiles.Add(new(
                "{build-output}/Fixture.dll",
                "Fixture.dll"));
        }

        return new ModProfile(
            1,
            Path.Combine(modRoot, "oni-mod-pipeline.toml"),
            modRoot,
            "mod.yaml",
            "mod_info.yaml",
            hasBuild
                ? new BuildProfile(
                    "Source/Fixture.csproj",
                    "Release",
                    "OniManagedAssemblyDirectory",
                    "{build-output}/Fixture.dll",
                    mergeInputs)
                : null,
            packageFiles,
            new WorkshopListingProfile(
                "description.bbcode",
                "change-notes.bbcode",
                "preview.png",
                ["tweaks"],
                ["base-game"],
                8000,
                8000),
            new LocalInstallProfile("Fixture"),
            [],
            []);
    }

    private static void WriteCommonProfileInputs(string modRoot)
    {
        Directory.CreateDirectory(modRoot);
        File.WriteAllText(Path.Combine(modRoot, "oni-mod-pipeline.toml"), "schema-version = 1\n");
        File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), "staticID: Example.Fixture\n");
        File.WriteAllText(Path.Combine(modRoot, "mod_info.yaml"), "version: 1.2.3\n");
        File.WriteAllText(Path.Combine(modRoot, "description.bbcode"), "Description\n");
        File.WriteAllText(Path.Combine(modRoot, "change-notes.bbcode"), "Changes\n");
        File.WriteAllBytes(Path.Combine(modRoot, "preview.png"), [0x89, 0x50, 0x4E, 0x47]);
    }

    private sealed class BuildFixture : IDisposable
    {
        private readonly TemporaryDirectory temporaryDirectory = new();

        internal BuildFixture(bool hasBuild = true)
        {
            WorktreeRoot = temporaryDirectory.GetPath("repository");
            ModRoot = Path.Combine(WorktreeRoot, "mods", "fixture");
            Directory.CreateDirectory(Path.Combine(WorktreeRoot, ".git"));
            WriteCommonProfileInputs(ModRoot);
            Directory.CreateDirectory(Path.Combine(ModRoot, "Source"));
            ProjectPath = Path.Combine(ModRoot, "Source", "Fixture.csproj");
            SourcePath = Path.Combine(ModRoot, "Source", "Fixture.cs");
            File.WriteAllText(ProjectPath, "<Project />\n");
            File.WriteAllText(SourcePath, "public static class FixtureType { }\n");
            ManagedDirectory = temporaryDirectory.GetPath("game", "Managed");
            Directory.CreateDirectory(ManagedDirectory);
            GameReferencePath = Path.Combine(ManagedDirectory, "Assembly-CSharp.dll");
            File.WriteAllText(GameReferencePath, "game-reference");
            File.WriteAllText(Path.Combine(ManagedDirectory, "0Harmony.dll"), "harmony");
            MergeInputPath = temporaryDirectory.GetPath("packages", "PLib.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(MergeInputPath)!);
            File.WriteAllText(MergeInputPath, "merge-input");
            Profile = CreateProfile(
                ModRoot,
                hasBuild,
                hasBuild ? ["PLib"] : []);
            Environment = CreateEnvironment(temporaryDirectory, ManagedDirectory);
            RunRoot = temporaryDirectory.GetPath("artifacts", "run with spaces;value");
            PrimaryOutputPath = Path.Combine(RunRoot, "output", "Fixture.dll");
            ProcessRunner = new RecordingBuildProcessRunner(
                CreateReferenceOutput(GameReferencePath, [MergeInputPath]));
        }

        internal string WorktreeRoot { get; }

        internal string ModRoot { get; }

        internal string ProjectPath { get; }

        internal string SourcePath { get; }

        internal string ManagedDirectory { get; }

        internal string GameReferencePath { get; }

        internal string MergeInputPath { get; }

        internal string RunRoot { get; }

        internal string PrimaryOutputPath { get; }

        internal ModProfile Profile { get; }

        internal PipelineEnvironment Environment { get; }

        internal RecordingBuildProcessRunner ProcessRunner { get; }

        internal string[] ExpectedRestoreArguments =>
        [
            "restore",
            ProjectPath,
            "--locked-mode",
            MsBuildPropertyArgument.Create(
                "OniManagedAssemblyDirectory",
                ManagedDirectory),
            MsBuildPropertyArgument.Create(
                "BaseIntermediateOutputPath",
                IntermediatePath),
            MsBuildPropertyArgument.Create(
                "MSBuildProjectExtensionsPath",
                IntermediatePath)
        ];

        internal string[] ExpectedResolveReferencesArguments =>
        [
            "msbuild",
            ProjectPath,
            "-nologo",
            "-target:ResolveReferences",
            "-getItem:ReferencePath,ReferenceCopyLocalPaths",
            MsBuildPropertyArgument.Create("Configuration", "Release"),
            MsBuildPropertyArgument.Create(
                "OniManagedAssemblyDirectory",
                ManagedDirectory),
            MsBuildPropertyArgument.Create(
                "OniMergedModOutputPath",
                PrimaryOutputPath),
            MsBuildPropertyArgument.Create(
                "BaseIntermediateOutputPath",
                IntermediatePath),
            MsBuildPropertyArgument.Create(
                "MSBuildProjectExtensionsPath",
                IntermediatePath)
        ];

        internal string[] ExpectedBuildArguments =>
        [
            "build",
            ProjectPath,
            "--no-restore",
            "--configuration",
            "Release",
            MsBuildPropertyArgument.Create(
                "OniManagedAssemblyDirectory",
                ManagedDirectory),
            MsBuildPropertyArgument.Create(
                "OniMergedModOutputPath",
                PrimaryOutputPath),
            MsBuildPropertyArgument.Create("BaseOutputPath", OutputPath),
            MsBuildPropertyArgument.Create(
                "BaseIntermediateOutputPath",
                IntermediatePath),
            MsBuildPropertyArgument.Create(
                "MSBuildProjectExtensionsPath",
                IntermediatePath),
            MsBuildPropertyArgument.Create("Version", "1.2.3"),
            MsBuildPropertyArgument.Create(
                "InformationalVersion",
                "1.2.3+0123456789ab"),
            MsBuildPropertyArgument.Create("Deterministic", "true"),
            MsBuildPropertyArgument.Create("ContinuousIntegrationBuild", "true"),
            MsBuildPropertyArgument.Create(
                "PathMap",
                $"{RunRoot}=/_build/,{WorktreeRoot}=/_/")
        ];

        private string IntermediatePath =>
            Path.Combine(RunRoot, "obj", "$(MSBuildProjectName)")
                .Replace((char)92, '/') + "/";

        private string OutputPath =>
            Path.Combine(RunRoot, "bin", "$(MSBuildProjectName)")
                .Replace((char)92, '/') + "/";

        internal BuildRequest CreateRequest(string releaseVersion = "1.2.3") =>
            new(
                Profile,
                Environment,
                "Release",
                RunRoot,
                releaseVersion,
                "0123456789abcdef0123456789abcdef01234567");

        internal void WritePrimaryOutput(ProcessRequest request)
        {
            var path = GetPrimaryOutputPath(request);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "compiled-output");
        }

        internal string CreateReferenceFile(string filename)
        {
            var path = Path.Combine(Path.GetDirectoryName(MergeInputPath)!, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, filename);
            return path;
        }

        internal void SetCopyLocalReferences(params string[] paths) =>
            ProcessRunner.ReferenceOutput = CreateReferenceOutput(
                GameReferencePath,
                paths);

        internal string GetPrimaryOutputPath(ProcessRequest request)
        {
            var prefix = "-p:OniMergedModOutputPath=\"";
            var argument = request.Arguments.Single(value =>
                value.StartsWith(prefix, StringComparison.Ordinal));
            return argument[prefix.Length..^1]
                .Replace("%3B", ";", StringComparison.OrdinalIgnoreCase)
                .Replace("%25", "%", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose() => temporaryDirectory.Dispose();

        private static string CreateReferenceOutput(
            string gameReferencePath,
            IReadOnlyList<string> mergeInputPaths) =>
            JsonSerializer.Serialize(new
            {
                Items = new
                {
                    ReferencePath = new[]
                    {
                        new { Identity = gameReferencePath, FullPath = gameReferencePath }
                    },
                    ReferenceCopyLocalPaths = mergeInputPaths.Select(path =>
                        new { Identity = path, FullPath = path })
                }
            });
    }

    private sealed class RecordingBuildProcessRunner : IExternalProcessRunner
    {
        internal RecordingBuildProcessRunner(string referenceOutput)
        {
            ReferenceOutput = referenceOutput;
        }

        internal List<ProcessRequest> Requests { get; } = [];

        internal Action<ProcessRequest>? BuildAction { get; set; }

        internal string ReferenceOutput { get; set; }

        internal int RestoreExitCode { get; set; }

        internal int BuildExitCode { get; set; }

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            var operation = request.Arguments[0];
            if (operation == "build")
            {
                BuildAction?.Invoke(request);
            }

            return Task.FromResult(operation switch
            {
                "restore" => new ProcessResult(
                    RestoreExitCode,
                    "restored",
                    RestoreExitCode == 0 ? string.Empty : "restore failed"),
                "msbuild" => new ProcessResult(0, ReferenceOutput, string.Empty),
                "build" => new ProcessResult(
                    BuildExitCode,
                    "built",
                    BuildExitCode == 0 ? string.Empty : "build failed"),
                _ => new ProcessResult(1, string.Empty, $"Unexpected operation: {operation}")
            });
        }
    }
}
