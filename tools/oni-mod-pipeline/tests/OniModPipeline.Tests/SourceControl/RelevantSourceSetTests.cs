using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;
public sealed class RelevantSourceSetTests
{
    [Test]
    [Arguments("Directory.Packages.props")]
    [Arguments("global.json")]
    [Arguments("tests/TUnitProjectDefaults.cs")]
    [Arguments("tests/TUnitProject.props")]
    public async Task Create_WithDotnetProjects_IncludesCentralControlsAndRetainsTrackedDeletions(string control)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        var path = directory.GetPath(control);
        WriteFile(path);
        var present = RelevantSourceSet.Create(profile, directory.Path, [], null);
        await Assert.That(present.IsSuccess).IsTrue();
        await Assert.That(present.Value!.WorktreeRelativePaths.ToArray()).Contains(control);
        File.Delete(path);
        var deleted = RelevantSourceSet.Create(profile, directory.Path, [control], null);
        await Assert.That(deleted.IsSuccess).IsTrue();
        await Assert.That(deleted.Value!.WorktreeRelativePaths.ToArray()).Contains(control);
        var absent = RelevantSourceSet.Create(profile, directory.Path, [], null);
        await Assert.That(absent.IsSuccess).IsTrue();
        await Assert.That(absent.Value!.WorktreeRelativePaths.Contains(control)).IsFalse();
    }

    [Test]
    public async Task Create_WithReadme_IncludesDocumentationAndNpmDeclarations()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory) with { Readme = new ReadmeProfile("README.md") };
        var inputs = new[] { "README.md", "package.json", "package-lock.json", ".markdown-quality.json", ".markdown-quality-execution.json", ".node-version", ".python-version", "tools/readme/canonicalize.mjs", "tools/readme/render-description.mjs", "tools/readme/compatibility-badges.mjs" };
        foreach (var path in inputs) WriteFile(directory.GetPath(path));
        var result = RelevantSourceSet.Create(profile, directory.Path, ["README.md", "package.json", "package-lock.json"], null);
        await Assert.That(result.IsSuccess).IsTrue();
        foreach (var path in inputs)
            await Assert.That(result.Value!.WorktreeRelativePaths.ToArray()).Contains(path);
    }
    [Test]
    public async Task Create_WhenProfileDeclaresBuildTestsAndAssets_IncludesInputsButExcludesBuildDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfileFixture(temporaryDirectory);
        var trackedPaths = Directory
            .EnumerateFiles(temporaryDirectory.Path, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelative(temporaryDirectory.Path, path))
            .ToArray();

        var result = RelevantSourceSet.Create(
            profile,
            temporaryDirectory.Path,
            trackedPaths,
            pipelineExecutablePath: null);

        await Assert.That(result.IsSuccess).IsTrue();
        Assert.NotNull(result.Value);
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("mods/example/Source/Code.cs");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("mods/example/Tests/TestCode.cs");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("mods/example/asset.txt");
        await Assert.That(result.Value.WorktreeRelativePaths.Any(
            path => path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/obj/", StringComparison.OrdinalIgnoreCase))).IsFalse();
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).IsEquivalentTo(result.Value.WorktreeRelativePaths
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Value.WorktreeRelativePaths.Any(path => path.Contains('\\'))).IsFalse();
    }

    [Test]
    public async Task Create_WhenPipelineExecutableIsInsideWorktree_IncludesToolSourcesAndLocks()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var profile = CreateProfileFixture(temporaryDirectory);
        var toolRoot = temporaryDirectory.GetPath("tools", "oni-mod-pipeline");
        var executablePath = Path.Combine(
            toolRoot,
            "src",
            "OniModPipeline",
            "bin",
            "Debug",
            "net10.0",
            "oni-mod-pipeline.dll");
        WriteFile(temporaryDirectory.GetPath("global.json"));
        WriteFile(Path.Combine(toolRoot, "OniModPipeline.slnx"));
        WriteFile(Path.Combine(toolRoot, "src", "OniModPipeline", "OniModPipeline.csproj"));
        WriteFile(Path.Combine(toolRoot, "src", "OniModPipeline", "packages.lock.json"));
        WriteFile(Path.Combine(toolRoot, "src", "OniModPipeline", "Program.cs"));
        WriteFile(Path.Combine(toolRoot, "src", "OniModPipeline", "Catalogs", "Python", "catalogs.py"));
        WriteFile(Path.Combine(toolRoot, "tests", "OniModPipeline.Tests", "OniModPipeline.Tests.csproj"));
        WriteFile(Path.Combine(toolRoot, "tests", "OniModPipeline.Tests", "packages.lock.json"));
        WriteFile(Path.Combine(toolRoot, "tests", "OniModPipeline.Tests", "SmokeTests.cs"));
        WriteFile(executablePath);

        var result = RelevantSourceSet.Create(
            profile,
            temporaryDirectory.Path,
            [],
            executablePath);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.WorktreeRelativePaths.ToArray()).Contains("global.json");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("tools/oni-mod-pipeline/src/OniModPipeline/Catalogs/Python/catalogs.py");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("tools/oni-mod-pipeline/src/OniModPipeline/Program.cs");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("tools/oni-mod-pipeline/tests/OniModPipeline.Tests/SmokeTests.cs");
        await Assert.That(result.Value.WorktreeRelativePaths.Contains(
            "tools/oni-mod-pipeline/src/OniModPipeline/bin/Debug/net10.0/oni-mod-pipeline.dll",
            StringComparer.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Create_WhenPipelineExecutableIsOutsideWorktree_DoesNotTreatItAsContributingInput()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var externalDirectory = new TemporaryDirectory();
        var profile = CreateProfileFixture(temporaryDirectory);
        var executablePath = externalDirectory.GetPath("oni-mod-pipeline.dll");
        WriteFile(executablePath);

        var result = RelevantSourceSet.Create(
            profile,
            temporaryDirectory.Path,
            [],
            executablePath);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.AbsolutePaths.Contains(
            executablePath,
            StringComparer.OrdinalIgnoreCase)).IsFalse();
    }

    [Test]
    [Arguments(".markdown-quality.json")]
    [Arguments(".markdown-quality-execution.json")]
    public async Task Create_WithReadme_AllowsAbsentDefaultsButRetainsTrackedControlDeletions(string control)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory) with { Readme = new ReadmeProfile("README.md") };
        foreach (var input in new[] { "README.md", "package.json", "package-lock.json", ".node-version", ".python-version", "tools/readme/canonicalize.mjs", "tools/readme/render-description.mjs", "tools/readme/compatibility-badges.mjs" })
            WriteFile(directory.GetPath(input));
        var absent = RelevantSourceSet.Create(profile, directory.Path, [], null);
        await Assert.That(absent.IsSuccess).IsTrue();
        await Assert.That(absent.Value!.WorktreeRelativePaths.Contains(control)).IsFalse();
        var deleted = RelevantSourceSet.Create(profile, directory.Path, [control], null);
        await Assert.That(deleted.IsSuccess).IsTrue();
        await Assert.That(deleted.Value!.WorktreeRelativePaths.ToArray()).Contains(control);
    }

    private static ModProfile CreateProfileFixture(TemporaryDirectory temporaryDirectory)
    {
        var modRoot = temporaryDirectory.GetPath("mods", "example");
        WriteFile(Path.Combine(modRoot, "oni-mod-pipeline.toml"));
        WriteFile(Path.Combine(modRoot, "mod.yaml"));
        WriteFile(Path.Combine(modRoot, "mod_info.yaml"));
        WriteFile(Path.Combine(modRoot, "asset.txt"));
        WriteFile(Path.Combine(modRoot, "description.bbcode"));
        WriteFile(Path.Combine(modRoot, "change-notes.bbcode"));
        WriteFile(Path.Combine(modRoot, "preview.png"));
        WriteFile(Path.Combine(modRoot, "Source", "Example.csproj"));
        WriteFile(Path.Combine(modRoot, "Source", "Code.cs"));
        WriteFile(Path.Combine(modRoot, "Source", "bin", "ignored.dll"));
        WriteFile(Path.Combine(modRoot, "Source", "obj", "ignored.g.cs"));
        WriteFile(Path.Combine(modRoot, "Tests", "Example.Tests.csproj"));
        WriteFile(Path.Combine(modRoot, "Tests", "TestCode.cs"));
        WriteFile(Path.Combine(modRoot, "Tests", "obj", "ignored.g.cs"));

        return new ModProfile(
            1,
            Path.Combine(modRoot, "oni-mod-pipeline.toml"),
            modRoot,
            "mod.yaml",
            "mod_info.yaml",
            new BuildProfile(
                "Source/Example.csproj",
                "Release",
                "OniManagedAssemblyDirectory",
                "{build-output}/Example.dll",
                []),
            [
                new PackageFileMapping("mod.yaml", "mod.yaml"),
                new PackageFileMapping("mod_info.yaml", "mod_info.yaml"),
                new PackageFileMapping("asset.txt", "asset.txt"),
                new PackageFileMapping("{build-output}/Example.dll", "Example.dll")
            ],
            new WorkshopListingProfile(
                "description.bbcode",
                "change-notes.bbcode",
                "preview.png",
                ["tweaks"],
                ["base-game"],
                8000,
                8000),
            new LocalInstallProfile("Example"),
            [new TestProjectProfile("example-tests", "Tests/Example.Tests.csproj", true)],
            []);
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "content");
    }

    private static string NormalizeRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace((char)92, '/');
}
