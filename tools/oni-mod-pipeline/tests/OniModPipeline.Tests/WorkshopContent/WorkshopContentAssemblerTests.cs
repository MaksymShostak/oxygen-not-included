using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using MaksymShostak.OniModPipeline.WorkshopContent;
using System.Security.Cryptography;

namespace MaksymShostak.OniModPipeline.Tests.WorkshopContent;

[TestClass]
public sealed class WorkshopContentAssemblerTests
{
    [TestMethod]
    public async Task AssembleAsync_WhenRealDeliveryProfileIsLoaded_ProducesThreeFileInventory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryRoot = FindRepositoryRoot();
        var modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        var profileResult = new ModProfileLoader().Load(
            Path.Combine(modRoot, "oni-mod-pipeline.toml"));
        Assert.IsTrue(profileResult.IsSuccess);
        var runRoot = temporaryDirectory.GetPath("build-run");
        var primaryOutput = Path.Combine(
            runRoot,
            "output",
            "DeliveryTemperatureLimit.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(primaryOutput)!);
        await File.WriteAllBytesAsync(primaryOutput, [1, 2, 3, 4]);
        var buildResult = new BuildResult(
            runRoot,
            primaryOutput,
            [],
            [CreateDigest(primaryOutput)],
            [],
            [],
            "0123456789abcdef0123456789abcdef01234567",
            "2026.8.26",
            "10.0.400",
            [],
            null,
            true,
            null,
            null);
        var stagingRoot = temporaryDirectory.GetPath("candidate", "workshop-content");
        Directory.CreateDirectory(stagingRoot);

        var result = await new WorkshopContentAssembler().AssembleAsync(
            profileResult.Value!,
            buildResult,
            stagingRoot,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, RenderDiagnostics(result.Diagnostics));
        CollectionAssert.AreEqual(
            new[] { "DeliveryTemperatureLimit.dll", "mod.yaml", "mod_info.yaml" },
            result.Value?
                .Select(file => Path.GetRelativePath(stagingRoot, file.Path)
                    .Replace('\\', '/'))
                .ToArray());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenDeliveryProfileIsValid_ProducesExactRuntimeInventory()
    {
        using var fixture = new WorkshopContentFixture();

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, RenderDiagnostics(result.Diagnostics));
        CollectionAssert.AreEqual(
            new[] { "DeliveryTemperatureLimit.dll", "mod.yaml", "mod_info.yaml" },
            Directory.EnumerateFiles(fixture.StagingRoot)
                .Select(Path.GetFileName)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { "DeliveryTemperatureLimit.dll", "mod.yaml", "mod_info.yaml" },
            result.Value?
                .Select(file => Path.GetRelativePath(fixture.StagingRoot, file.Path)
                    .Replace('\\', '/'))
                .ToArray());
        Assert.IsFalse(File.Exists(Path.Combine(fixture.StagingRoot, "Preview.png")));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.StagingRoot, "Source")));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.StagingRoot, "Tests")));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.StagingRoot, "release-evidence")));
    }

    [TestMethod]
    public async Task AssembleAsync_WhenContainedDirectoryIsDeclared_CopiesOnlyRegularDescendants()
    {
        using var fixture = new WorkshopContentFixture();
        var assets = Path.Combine(fixture.ModRoot, "assets");
        Directory.CreateDirectory(Path.Combine(assets, "nested"));
        File.WriteAllText(Path.Combine(assets, "config.json"), "{}");
        File.WriteAllBytes(Path.Combine(assets, "nested", "data.bin"), [1, 2, 3]);
        fixture.Profile = fixture.Profile with
        {
            PackageFiles =
            [
                .. fixture.Profile.PackageFiles,
                new PackageFileMapping("assets", "assets")
            ]
        };

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, RenderDiagnostics(result.Diagnostics));
        CollectionAssert.AreEqual(
            new[]
            {
                "DeliveryTemperatureLimit.dll",
                "assets/config.json",
                "assets/nested/data.bin",
                "mod.yaml",
                "mod_info.yaml"
            },
            result.Value?
                .Select(file => Path.GetRelativePath(fixture.StagingRoot, file.Path)
                    .Replace('\\', '/'))
                .ToArray());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenBuildOutputIsNotInHashedOutputs_RejectsBeforeCopy()
    {
        using var fixture = new WorkshopContentFixture();
        fixture.BuildResult = fixture.BuildResult with { Outputs = [] };

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Count());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenPrimaryAssemblyIsEmpty_RejectsAndCleansStaging()
    {
        using var fixture = new WorkshopContentFixture(primaryBytes: []);

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("ONIP5002", result.Diagnostics.Single().Id);
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Count());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenDestinationsCollidePortably_RejectsBeforeCopy()
    {
        using var fixture = new WorkshopContentFixture();
        File.WriteAllText(Path.Combine(fixture.ModRoot, "duplicate.yaml"), "duplicate");
        fixture.Profile = fixture.Profile with
        {
            PackageFiles =
            [
                .. fixture.Profile.PackageFiles,
                new PackageFileMapping("duplicate.yaml", "MOD.YAML")
            ]
        };

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Count());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenDestinationEscapesStaging_RejectsBeforeCopy()
    {
        using var fixture = new WorkshopContentFixture();
        fixture.Profile = fixture.Profile with
        {
            PackageFiles =
            [
                .. fixture.Profile.PackageFiles,
                new PackageFileMapping("mod.yaml", "../escape.yaml")
            ]
        };

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Count());
        Assert.IsFalse(File.Exists(Path.Combine(
            Path.GetDirectoryName(fixture.StagingRoot)!,
            "escape.yaml")));
    }

    [TestMethod]
    public async Task AssembleAsync_WhenBuildResultRepeatsOutputPath_RejectsAmbiguousEvidence()
    {
        using var fixture = new WorkshopContentFixture();
        fixture.BuildResult = fixture.BuildResult with
        {
            Outputs = [.. fixture.BuildResult.Outputs, .. fixture.BuildResult.Outputs]
        };

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Count());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenSourceIsReportedAsLink_RejectsBeforeCopy()
    {
        using var fixture = new WorkshopContentFixture();
        var linkedSource = Path.Combine(fixture.ModRoot, "mod.yaml");
        var assembler = new WorkshopContentAssembler(
            new WorkshopContentValidator(),
            new ContentHasher(),
            path => string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(linkedSource),
                StringComparison.OrdinalIgnoreCase)
                ? File.GetAttributes(path) | FileAttributes.ReparsePoint
                : File.GetAttributes(path),
            _ => null);

        var result = await assembler.AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Count());
    }

    [TestMethod]
    public async Task AssembleAsync_WhenStagingContainsStaleFile_RejectsWithoutChangingIt()
    {
        using var fixture = new WorkshopContentFixture();
        var stalePath = Path.Combine(fixture.StagingRoot, "stale.txt");
        File.WriteAllText(stalePath, "keep");

        var result = await new WorkshopContentAssembler().AssembleAsync(
            fixture.Profile,
            fixture.BuildResult,
            fixture.StagingRoot,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("keep", File.ReadAllText(stalePath));
        CollectionAssert.AreEqual(
            new[] { stalePath },
            Directory.EnumerateFiles(fixture.StagingRoot).ToArray());
    }

    private static string RenderDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Evidence));

    private static FileDigest CreateDigest(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new FileDigest(
            Path.GetFullPath(path),
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "docs",
                "plans",
                "2026-08-27-oni-mod-pipeline-implementation.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class WorkshopContentFixture : IDisposable
    {
        private readonly TemporaryDirectory temporaryDirectory = new();

        internal WorkshopContentFixture(byte[]? primaryBytes = null)
        {
            ModRoot = temporaryDirectory.GetPath("mod");
            Directory.CreateDirectory(ModRoot);
            File.WriteAllText(Path.Combine(ModRoot, "mod.yaml"), "title: Example\n");
            File.WriteAllText(Path.Combine(ModRoot, "mod_info.yaml"), "version: 1.0.0\n");
            File.WriteAllBytes(Path.Combine(ModRoot, "Preview.png"), [1, 2, 3]);
            File.WriteAllText(Path.Combine(ModRoot, "STEAM_DESCRIPTION.bbcode"), "Description\n");
            File.WriteAllText(Path.Combine(ModRoot, "STEAM_CHANGE_NOTES.bbcode"), "Changes\n");
            Directory.CreateDirectory(Path.Combine(ModRoot, "Source"));
            File.WriteAllText(Path.Combine(ModRoot, "Source", "Mod.cs"), "class Mod { }");
            Directory.CreateDirectory(Path.Combine(ModRoot, "Tests"));
            File.WriteAllText(Path.Combine(ModRoot, "Tests", "ModTests.cs"), "class Tests { }");

            RunRoot = temporaryDirectory.GetPath("build-run");
            PrimaryOutputPath = Path.Combine(
                RunRoot,
                "output",
                "DeliveryTemperatureLimit.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(PrimaryOutputPath)!);
            File.WriteAllBytes(PrimaryOutputPath, primaryBytes ?? [4, 5, 6]);
            var outputDigest = Digest(PrimaryOutputPath);
            BuildResult = new BuildResult(
                RunRoot,
                PrimaryOutputPath,
                [],
                [outputDigest],
                [],
                [],
                "0123456789abcdef0123456789abcdef01234567",
                "1.0.0",
                "10.0.400",
                [],
                null,
                true,
                null,
                null);
            Profile = new ModProfile(
                1,
                Path.Combine(ModRoot, "oni-mod-pipeline.toml"),
                ModRoot,
                "mod.yaml",
                "mod_info.yaml",
                new BuildProfile(
                    "Source/DeliveryTemperatureLimit.csproj",
                    "Release",
                    "OniManagedAssemblyDirectory",
                    "{build-output}/DeliveryTemperatureLimit.dll",
                    ["PLib"]),
                [
                    new PackageFileMapping("mod.yaml", "mod.yaml"),
                    new PackageFileMapping("mod_info.yaml", "mod_info.yaml"),
                    new PackageFileMapping(
                        "{build-output}/DeliveryTemperatureLimit.dll",
                        "DeliveryTemperatureLimit.dll")
                ],
                new WorkshopListingProfile(
                    "STEAM_DESCRIPTION.bbcode",
                    "STEAM_CHANGE_NOTES.bbcode",
                    "Preview.png",
                    ["tweaks"],
                    ["base-game"],
                    8000,
                    8000),
                new LocalInstallProfile("ExampleMod"),
                [],
                []);
            StagingRoot = temporaryDirectory.GetPath("candidate", "workshop-content");
            Directory.CreateDirectory(StagingRoot);
        }

        internal string ModRoot { get; }

        internal string RunRoot { get; }

        internal string PrimaryOutputPath { get; }

        internal string StagingRoot { get; }

        internal ModProfile Profile { get; set; }

        internal BuildResult BuildResult { get; set; }

        public void Dispose() => temporaryDirectory.Dispose();

        private static FileDigest Digest(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return new FileDigest(
                Path.GetFullPath(path),
                bytes.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }
}
