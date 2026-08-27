using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;

[TestClass]
public sealed class RelevantSourceSetTests
{
    [TestMethod]
    public void Create_WhenProfileDeclaresBuildTestsAndAssets_IncludesInputsButExcludesBuildDirectories()
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

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        CollectionAssert.Contains(
            result.Value.WorktreeRelativePaths.ToArray(),
            "mods/example/Source/Code.cs");
        CollectionAssert.Contains(
            result.Value.WorktreeRelativePaths.ToArray(),
            "mods/example/Tests/TestCode.cs");
        CollectionAssert.Contains(
            result.Value.WorktreeRelativePaths.ToArray(),
            "mods/example/asset.txt");
        Assert.IsFalse(result.Value.WorktreeRelativePaths.Any(
            path => path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)));
        CollectionAssert.AreEqual(
            result.Value.WorktreeRelativePaths
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray(),
            result.Value.WorktreeRelativePaths.ToArray());
        Assert.IsFalse(result.Value.WorktreeRelativePaths.Any(path => path.Contains('\\')));
    }

    [TestMethod]
    public void Create_WhenPipelineExecutableIsInsideWorktree_IncludesToolSourcesAndLocks()
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
        WriteFile(Path.Combine(toolRoot, "tests", "OniModPipeline.Tests", "OniModPipeline.Tests.csproj"));
        WriteFile(Path.Combine(toolRoot, "tests", "OniModPipeline.Tests", "packages.lock.json"));
        WriteFile(Path.Combine(toolRoot, "tests", "OniModPipeline.Tests", "SmokeTests.cs"));
        WriteFile(executablePath);

        var result = RelevantSourceSet.Create(
            profile,
            temporaryDirectory.Path,
            [],
            executablePath);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.Contains(result.Value!.WorktreeRelativePaths.ToArray(), "global.json");
        CollectionAssert.Contains(
            result.Value.WorktreeRelativePaths.ToArray(),
            "tools/oni-mod-pipeline/src/OniModPipeline/Program.cs");
        CollectionAssert.Contains(
            result.Value.WorktreeRelativePaths.ToArray(),
            "tools/oni-mod-pipeline/tests/OniModPipeline.Tests/SmokeTests.cs");
        Assert.IsFalse(result.Value.WorktreeRelativePaths.Contains(
            "tools/oni-mod-pipeline/src/OniModPipeline/bin/Debug/net10.0/oni-mod-pipeline.dll",
            StringComparer.Ordinal));
    }

    [TestMethod]
    public void Create_WhenPipelineExecutableIsOutsideWorktree_DoesNotTreatItAsContributingInput()
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

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value!.AbsolutePaths.Contains(
            executablePath,
            StringComparer.OrdinalIgnoreCase));
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
