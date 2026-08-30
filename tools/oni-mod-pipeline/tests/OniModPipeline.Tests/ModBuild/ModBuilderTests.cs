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

[TestClass]
public sealed class ModBuilderTests
{
    [TestMethod]
    public async Task BuildAsync_WhenProfileHasBuild_RestoresLockedThenBuildsWithoutShell()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.BuildAction = fixture.WritePrimaryOutput;
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        Assert.AreEqual(
            PipelineExitCode.Success,
            result.ExitCode,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(3, fixture.ProcessRunner.Requests.Count);
        Assert.IsTrue(fixture.ProcessRunner.Requests.All(request => request.FileName == "dotnet"));
        CollectionAssert.AreEqual(
            fixture.ExpectedRestoreArguments,
            fixture.ProcessRunner.Requests[0].Arguments.ToArray());
        CollectionAssert.AreEqual(
            fixture.ExpectedResolveReferencesArguments,
            fixture.ProcessRunner.Requests[1].Arguments.ToArray());
        CollectionAssert.AreEqual(
            fixture.ExpectedBuildArguments,
            fixture.ProcessRunner.Requests[2].Arguments.ToArray());
        Assert.IsTrue(fixture.ProcessRunner.Requests.All(
            request => request.EnvironmentVariables.Count == 0));
        CollectionAssert.AreEqual(
            fixture.ExpectedBuildArguments,
            result.Value.StructuredBuildArguments.ToArray());
        Assert.AreEqual(fixture.PrimaryOutputPath, result.Value.PrimaryOutputPath);
        Assert.AreEqual(1, result.Value.Outputs.Count);
        Assert.AreEqual(fixture.PrimaryOutputPath, result.Value.Outputs[0].Path);
        Assert.AreEqual(fixture.MergeInputPath, result.Value.MergeInputs.Single().Path);
        Assert.AreEqual(fixture.GameReferencePath, result.Value.GameReferences.Single().Path);
        Assert.IsTrue(result.Value.Inputs.Any(input => input.Path == fixture.SourcePath));
        Assert.IsTrue(result.Value.SourceBytesUnchanged);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json")));
    }

    [TestMethod]
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

        AssertDiagnostic(result, DiagnosticIds.SourceChangedDuringBuild);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json")));
    }

    [TestMethod]
    public async Task BuildAsync_WhenPrimaryOutputIsMissing_ReturnsOnip3004()
    {
        using var fixture = new BuildFixture();
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.BuildOutputMissing);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json")));
    }

    [TestMethod]
    public async Task BuildAsync_WhenLockedRestoreFails_ReturnsOnip3001()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.RestoreExitCode = 1;
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.RestoreFailed);
        Assert.AreEqual(1, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task BuildAsync_WhenBuildProcessFails_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        fixture.ProcessRunner.BuildExitCode = 1;
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        Assert.AreEqual(3, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task BuildAsync_WhenDeclaredMergeInputIsMissing_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        fixture.SetCopyLocalReferences();
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        Assert.AreEqual(2, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
    public async Task BuildAsync_WhenCopyLocalReferenceIsUndeclared_ReturnsOnip3002()
    {
        using var fixture = new BuildFixture();
        var undeclared = fixture.CreateReferenceFile("Undeclared.Dependency.dll");
        fixture.SetCopyLocalReferences(fixture.MergeInputPath, undeclared);
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        Assert.AreEqual(2, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
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

        AssertDiagnostic(result, DiagnosticIds.BuildFailed);
        Assert.AreEqual(2, fixture.ProcessRunner.Requests.Count);
    }

    [TestMethod]
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

        Assert.AreEqual(
            PipelineExitCode.Success,
            result.ExitCode,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        Assert.IsNotNull(result.Value?.PrimaryAssemblyVersion);
        Assert.AreEqual(
            assembly.GetName().Version!.ToString(),
            result.Value.PrimaryAssemblyVersion.AssemblyVersion);
        Assert.AreEqual(
            assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
            result.Value.PrimaryAssemblyVersion.FileVersion);
        Assert.AreEqual(
            informationalVersion,
            result.Value.PrimaryAssemblyVersion.InformationalVersion);
    }

    [TestMethod]
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

        Assert.AreEqual(
            PipelineExitCode.Success,
            result.ExitCode,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        using var buildResultJson = JsonDocument.Parse(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.RunRoot, "build-result.json")));
        Assert.IsTrue(
            buildResultJson.RootElement.TryGetProperty(
                "primaryAssemblyTargetFrameworkMoniker",
                out var targetFrameworkMoniker),
            "The build result must carry target-framework evidence read from the exact primary assembly.");
        Assert.AreEqual(
            "net10.0",
            targetFrameworkMoniker.GetString());
    }

    [TestMethod]
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

        Assert.AreEqual(
            PipelineExitCode.Success,
            result.ExitCode,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
        using var buildResultJson = JsonDocument.Parse(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.RunRoot, "build-result.json")));
        Assert.AreEqual(
            "netstandard2.1",
            buildResultJson.RootElement
                .GetProperty("primaryAssemblyTargetFrameworkMoniker")
                .GetString());
        Assert.AreEqual(
            ".NETStandard,Version=v2.1",
            buildResultJson.RootElement
                .GetProperty("primaryAssemblyTargetFrameworkName")
                .GetString());
    }

    [TestMethod]
    public async Task BuildAsync_WhenProfileIsContentOnly_ProducesEmptySuccessfulBuildResult()
    {
        using var fixture = new BuildFixture(hasBuild: false);
        var builder = new ModBuilder(fixture.ProcessRunner, new Utf8ArtifactWriter());

        var result = await builder.BuildAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        Assert.AreEqual(PipelineExitCode.Success, result.ExitCode);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(0, fixture.ProcessRunner.Requests.Count);
        Assert.IsNull(result.Value.PrimaryOutputPath);
        Assert.AreEqual(0, result.Value.Outputs.Count);
        Assert.AreEqual(0, result.Value.MergeInputs.Count);
        Assert.AreEqual(0, result.Value.GameReferences.Count);
        Assert.AreEqual(0, result.Value.StructuredBuildArguments.Count);
        Assert.IsNull(result.Value.PrimaryAssemblyVersion);
        Assert.IsTrue(result.Value.SourceBytesUnchanged);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.RunRoot, "build-result.json")));
    }

    [TestMethod]
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

            Assert.AreEqual(
                PipelineExitCode.Success,
                result.ExitCode,
                string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Evidence)));
            Assert.IsNotNull(result.Value);
            Assert.IsTrue(result.Value.PrimaryOutputPath!.StartsWith(
                Path.GetFullPath(runRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(File.Exists(result.Value.PrimaryOutputPath));
            outputHashes.Add(Convert.ToHexString(SHA256.HashData(
                await File.ReadAllBytesAsync(result.Value.PrimaryOutputPath))));
            CollectionAssert.AreEqual(sourceBytes, await File.ReadAllBytesAsync(sourcePath));
        }

        Assert.AreEqual(outputHashes[0], outputHashes[1]);
        Assert.AreEqual(0, CountFilesIfDirectoryExists(Path.Combine(sourceDirectory, "bin")));
        Assert.AreEqual(0, CountFilesIfDirectoryExists(Path.Combine(sourceDirectory, "obj")));
    }

    private static void ReplaceUniqueEqualLengthUtf8Sequence(
        string filePath,
        string existingValue,
        string replacementValue)
    {
        var existingBytes = Encoding.UTF8.GetBytes(existingValue);
        var replacementBytes = Encoding.UTF8.GetBytes(replacementValue);
        Assert.AreEqual(
            existingBytes.Length,
            replacementBytes.Length,
            "The metadata fixture replacement must preserve the PE blob length.");

        var fileBytes = File.ReadAllBytes(filePath);
        var matchOffsets = new List<int>();
        for (var offset = 0; offset <= fileBytes.Length - existingBytes.Length; offset++)
        {
            if (fileBytes.AsSpan(offset, existingBytes.Length).SequenceEqual(existingBytes))
            {
                matchOffsets.Add(offset);
            }
        }

        Assert.HasCount(
            1,
            matchOffsets,
            "The managed test artifact must contain exactly one target-framework name payload.");
        replacementBytes.CopyTo(fileBytes.AsSpan(matchOffsets[0], replacementBytes.Length));
        File.WriteAllBytes(filePath, fileBytes);
    }

    private static void AssertDiagnostic(
        OperationResult<BuildResult> result,
        string expectedId)
    {
        Assert.AreEqual(PipelineExitCode.BuildOrTestFailed, result.ExitCode);
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == expectedId),
            $"Expected {expectedId}; received " +
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
