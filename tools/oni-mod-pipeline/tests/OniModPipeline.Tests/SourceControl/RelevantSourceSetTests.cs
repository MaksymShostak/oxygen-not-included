using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.SourceControl;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.SourceControl;
public sealed class RelevantSourceSetTests
{
    [Test]
    [Arguments("Directory.Packages.props")]
    [Arguments("global.json")]
    [Arguments("tools/ci/oni_references.py")]
    [Arguments("tools/ci/oni-references.json")]
    [Arguments("tests/TUnitProjectDefaults.cs")]
    [Arguments("tests/TUnitProject.props")]
    public async Task Create_WithDotnetProjects_IncludesCentralControlsAndRetainsTrackedDeletions(string control)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        var path = directory.GetPath(control);
        WriteFile(path);
        var present = await RelevantSourceSet.CreateAsync(profile, directory.Path, [], null);
        await Assert.That(present.IsSuccess).IsTrue();
        await Assert.That(present.Value!.WorktreeRelativePaths.ToArray()).Contains(control);
        File.Delete(path);
        var deleted = await RelevantSourceSet.CreateAsync(profile, directory.Path, [control], null);
        await Assert.That(deleted.IsSuccess).IsTrue();
        await Assert.That(deleted.Value!.WorktreeRelativePaths.ToArray()).Contains(control);
        var absent = await RelevantSourceSet.CreateAsync(profile, directory.Path, [], null);
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
        var result = await RelevantSourceSet.CreateAsync(profile, directory.Path, ["README.md", "package.json", "package-lock.json"], null);
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

        var result = await RelevantSourceSet.CreateAsync(
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
        // A content-only mod still relies on the in-worktree pipeline sources.
        var profile = CreateProfileFixture(temporaryDirectory) with { Build = null, TestProjects = [] };
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

        var result = await RelevantSourceSet.CreateAsync(
            profile,
            temporaryDirectory.Path,
            [],
            executablePath);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.WorktreeRelativePaths.ToArray()).Contains("global.json");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("Directory.Packages.props");
        await Assert.That(result.Value.WorktreeRelativePaths.ToArray()).Contains("tools/ci/oni_references.py");
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

        var result = await RelevantSourceSet.CreateAsync(
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
        var absent = await RelevantSourceSet.CreateAsync(profile, directory.Path, [], null);
        await Assert.That(absent.IsSuccess).IsTrue();
        await Assert.That(absent.Value!.WorktreeRelativePaths.Contains(control)).IsFalse();
        var deleted = await RelevantSourceSet.CreateAsync(profile, directory.Path, [control], null);
        await Assert.That(deleted.IsSuccess).IsTrue();
        await Assert.That(deleted.Value!.WorktreeRelativePaths.ToArray()).Contains(control);
    }

    [Test]
    public async Task Inspect_FlagsStagedDeletionsAndNewBuildInputsWithinTheDeclaredProject()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string original = Path.Combine(profile.ModRoot, "Source", "Before.cs");
        File.WriteAllText(original, "// initial source");
        TestGitRepository.CommitAll(directory.Path);
        File.Delete(original);
        using (var repository = new LibGit2Sharp.Repository(directory.Path))
            LibGit2Sharp.Commands.Stage(repository, "mods/example/Source/Before.cs");
        File.WriteAllText(Path.Combine(profile.ModRoot, "Source", "After.cs"), "// new untracked input");

        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(
            ["mods/example/Source/Before.cs", "mods/example/Source/After.cs"]);
    }

    [Test]
    public async Task Inspect_IgnoredSdkInputsAreDirtyButIgnoredNotesAndBuildOutputsAreOutsideTheScope()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        File.WriteAllText(directory.GetPath(".gitignore"), "lib/\nbin/\nobj/\n");
        TestGitRepository.CommitAll(directory.Path);
        WriteFile(Path.Combine(profile.ModRoot, "Source", "lib", "Ignored.cs"));
        WriteFile(Path.Combine(profile.ModRoot, "Tests", "lib", "Messages.resx"));
        WriteFile(Path.Combine(profile.ModRoot, "Source", "lib", "personal.txt"));
        WriteFile(Path.Combine(profile.ModRoot, "Source", "nested", "bin", "StillCompiled.cs"));
        WriteFile(Path.Combine(profile.ModRoot, "Source", "obj", "Generated.cs"));

        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(
            ["mods/example/Source/lib/Ignored.cs", "mods/example/Source/nested/bin/StillCompiled.cs", "mods/example/Tests/lib/Messages.resx"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Inspect_ReportsADeletedMemberOfADeclaredPackageDirectory(bool stageDeletion)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string assets = Path.Combine(profile.ModRoot, "sprites");
        WriteFile(Path.Combine(assets, "retained.png"));
        WriteFile(Path.Combine(assets, "removed.png"));
        profile = profile with { PackageFiles = [.. profile.PackageFiles, new PackageFileMapping("sprites", "sprites")] };
        TestGitRepository.CommitAll(directory.Path);
        File.Delete(Path.Combine(assets, "removed.png"));
        if (stageDeletion)
        {
            using var repository = new LibGit2Sharp.Repository(directory.Path);
            LibGit2Sharp.Commands.Stage(repository, "mods/example/sprites/removed.png");
        }

        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(["mods/example/sprites/removed.png"]);
    }

    [Test]
    [Arguments(".slnx")]
    [Arguments(".sln")]
    public async Task Inspect_UsesDeclaredSolutionMembersForIgnoredCompilerFiles(string format)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        string solution = Path.Combine(source, "Release" + format);
        File.WriteAllText(directory.GetPath(".gitignore"), "lib/\n");
        string document = format == ".slnx" ? """<Solution><Project Path="lib/Member.csproj" /></Solution>""" :
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Member", "lib\Member.csproj", "{68B559FC-18A6-43BE-A1AD-FFE42A115500}"
            EndProject
            Global
            EndGlobal
            """;
        File.WriteAllText(solution, document);
        profile = profile with { Build = profile.Build! with { EntryPoint = "Source/Release" + format } };
        WriteFile(Path.Combine(source, "sibling", "Unlisted.csproj"));
        TestGitRepository.CommitAll(directory.Path);
        WriteFile(Path.Combine(source, "lib", "Member.csproj"));
        WriteFile(Path.Combine(source, "lib", "Member.cs"));
        WriteFile(Path.Combine(source, "sibling", "lib", "Personal.cs"));

        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue().Because(string.Join(" ", result.Diagnostics.Select(d => d.Evidence)));
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(
            ["mods/example/Source/lib/Member.csproj", "mods/example/Source/lib/Member.cs"]);
    }

    [Test]
    public async Task Create_RejectsALinkedSolutionMemberBeforeEnumeratingItsDirectory()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        File.WriteAllText(Path.Combine(source, "Release.slnx"), """<Solution><Project Path="linked/Member.csproj" /></Solution>""");
        WriteFile(directory.GetPath("external", "Member.csproj"));
        profile = profile with { Build = profile.Build! with { EntryPoint = "Source/Release.slnx" } };
        string link = await TestDirectoryLink.CreateAsync(source, "linked", "../../../external");
        try
        {
            var result = await RelevantSourceSet.CreateAsync(profile, directory.Path, ["mods/example/Source/linked/Member.csproj"], null);
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Diagnostics.Single().Id).IsEqualTo("ONIP1003");
        }
        finally { Directory.Delete(link, recursive: false); }
    }

    [Test]
    public async Task Inspect_FSharpTracksItsDeclaredSourceListAndAllowsUnlistedIgnoredFiles()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        File.WriteAllText(directory.GetPath(".gitignore"), "lib/\n");
        File.WriteAllText(Path.Combine(source, "Example.fsproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><Compile Include="lib/Active.fs" /></ItemGroup></Project>""");
        profile = profile with { Build = profile.Build! with { EntryPoint = "Source/Example.fsproj" } };
        TestGitRepository.CommitAll(directory.Path);
        WriteFile(Path.Combine(source, "lib", "Active.fs"));
        WriteFile(Path.Combine(source, "lib", "Personal.fs"));
        WriteFile(Path.Combine(source, "lib", "Unused.fsi"));
        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(["mods/example/Source/lib/Active.fs"]);
    }

    [Test]
    public async Task Inspect_ExplicitCompilerGlobHonorsItsExclusionsIndependentlyOfResources()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        File.WriteAllText(directory.GetPath(".gitignore"), "lib/\n");
        File.WriteAllText(Path.Combine(source, "Example.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
              <ItemGroup>
                <Compile Include="lib/parts/**/*.cs" Exclude="lib/parts/local/**" />
                <Compile Remove="**/*.resx" />
              </ItemGroup>
            </Project>
            """);
        TestGitRepository.CommitAll(directory.Path);
        WriteFile(Path.Combine(source, "lib", "parts", "regular", "Used.cs"));
        WriteFile(Path.Combine(source, "lib", "parts", "local", "Personal.cs"));
        WriteFile(Path.Combine(source, "lib", "Other.cs"));
        WriteFile(Path.Combine(source, "lib", "Strings.resx"));
        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(
            ["mods/example/Source/lib/parts/regular/Used.cs", "mods/example/Source/lib/Strings.resx"]).Because(string.Join(", ", result.Value.DirtyPaths));
    }

    [Test]
    public async Task Inspect_UsesMsBuildSingleCharacterPatternsAndOrderedItemRemovals()
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        File.WriteAllText(directory.GetPath(".gitignore"), "lib/\n");
        File.WriteAllText(Path.Combine(source, "Example.fsproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
              <Compile Include="lib/Part?.fs;lib/*.fsi" />
              <Compile Remove="lib/Drop.fsi;lib/Again.fsi" />
              <Compile Include="lib/Again.fsi" />
            </ItemGroup></Project>
            """);
        profile = profile with { Build = profile.Build! with { EntryPoint = "Source/Example.fsproj" } };
        TestGitRepository.CommitAll(directory.Path);
        foreach (string name in new[] { "Part2.fs", "Part22.fs", "Keep.fsi", "Drop.fsi", "Again.fsi" })
            WriteFile(Path.Combine(source, "lib", name));
        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(
            ["mods/example/Source/lib/Part2.fs", "mods/example/Source/lib/Keep.fsi", "mods/example/Source/lib/Again.fsi"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Inspect_RetainsDeletedInputsOfASolutionMemberOutsideTheSolutionDirectory(bool staged)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string solutionDirectory = Directory.CreateDirectory(Path.Combine(profile.ModRoot, "solutions")).FullName;
        File.WriteAllText(Path.Combine(solutionDirectory, "Release.slnx"), """<Solution><Project Path="../Source/Example.csproj" /></Solution>""");
        profile = profile with { Build = profile.Build! with { EntryPoint = "solutions/Release.slnx" } };
        TestGitRepository.CommitAll(directory.Path);
        File.Delete(Path.Combine(profile.ModRoot, "Source", "Code.cs"));
        if (staged)
        {
            using var repository = new LibGit2Sharp.Repository(directory.Path);
            LibGit2Sharp.Commands.Stage(repository, "mods/example/Source/Code.cs");
        }
        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(["mods/example/Source/Code.cs"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Inspect_RedirectedBuildsOnlyPruneLocalOutputsExcludedByTheProject(bool explicitlyExcluded)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        File.Delete(Path.Combine(source, "obj", "ignored.g.cs"));
        File.WriteAllText(directory.GetPath(".gitignore"), "bin/\nobj/\n");
        var declaration = explicitlyExcluded ?
            """<PropertyGroup><DefaultItemExcludes>$(DefaultItemExcludes);$(MSBuildProjectDirectory)/bin/**;$(MSBuildProjectDirectory)/obj/**</DefaultItemExcludes></PropertyGroup>""" : "";
        File.WriteAllText(Path.Combine(source, "Example.csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\">{declaration}</Project>");
        TestGitRepository.CommitAll(directory.Path);
        WriteFile(Path.Combine(source, "bin", "Unexpected.cs"));
        WriteFile(Path.Combine(source, "obj", "Unexpected.resx"));
        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        string[] expected = explicitlyExcluded ? [] : ["mods/example/Source/bin/Unexpected.cs", "mods/example/Source/obj/Unexpected.resx"];
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Inspect_RetainsDeletedLinkedWildcardInputsEvenWhenTheirDirectoryDisappears(bool staged, bool removeDirectory)
    {
        using var directory = new TemporaryDirectory();
        var profile = CreateProfileFixture(directory);
        string source = Path.Combine(profile.ModRoot, "Source");
        string shared = Directory.CreateDirectory(Path.Combine(profile.ModRoot, "shared", "nested")).FullName;
        File.WriteAllText(Path.Combine(source, "Example.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
              <Compile Include="../shared/**/*.cs" Exclude="../shared/**/Optional.cs" />
            </ItemGroup></Project>
            """);
        WriteFile(Path.Combine(shared, "Removed.cs"));
        WriteFile(Path.Combine(shared, "Optional.cs"));
        TestGitRepository.CommitAll(directory.Path);
        File.Delete(Path.Combine(shared, "Removed.cs"));
        File.Delete(Path.Combine(shared, "Optional.cs"));
        if (removeDirectory)
        {
            Directory.Delete(shared);
            Directory.Delete(Path.GetDirectoryName(shared)!);
        }
        if (staged)
        {
            using var repository = new LibGit2Sharp.Repository(directory.Path);
            LibGit2Sharp.Commands.Stage(repository, "mods/example/shared/*");
        }
        var result = await new GitRepositoryInspector().InspectAsync(profile, null, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DirtyPaths.ToArray()).IsEquivalentTo(["mods/example/shared/nested/Removed.cs"]);
    }

    [Test]
    public async Task OverlappingSdkPatternsPlanOneRecursiveWalkAndKeepDisjointDirectRoots()
    {
        using var directory = new TemporaryDirectory();
        string project = directory.GetPath("Example.fsproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
              <Compile Include="lib/**/*.fs;lib/**/*.fs;lib/nested/*.fs;other/*.fs;other/deep/*.fs;one.fs;one.fs" />
            </ItemGroup></Project>
            """);
        var inputs = SdkProjectInputs.Read(project, ".fs", isolatedOutputs: true);
        await Assert.That(inputs.DeclaredIncludes.Count).IsEqualTo(5);
        var plan = new SdkInputSearchPlan(inputs.DeclaredIncludes, CancellationToken.None);
        await Assert.That(plan.Files.ToArray()).IsEquivalentTo([directory.GetPath("one.fs")]);
        await Assert.That(plan.Directories.Keys.ToArray()).IsEquivalentTo(
            [directory.GetPath("lib"), directory.GetPath("other"), directory.GetPath("other", "deep")]);
        await Assert.That(plan.Directories[directory.GetPath("lib")]).IsTrue();
        await Assert.That(plan.Directories[directory.GetPath("other")]).IsFalse();
        await Assert.That(plan.Covers(directory.GetPath("lib", "missing", "WasHere.fs"), CancellationToken.None)).IsTrue();
        await Assert.That(plan.Covers(directory.GetPath("library", "WasHere.fs"), CancellationToken.None)).IsFalse();
        await Assert.That(plan.Covers(directory.GetPath("other", "unrelated", "WasHere.fs"), CancellationToken.None)).IsFalse();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.That(() => plan.Covers(directory.GetPath("one.fs"), cancelled.Token)).ThrowsExactly<OperationCanceledException>();
        await Assert.That(() => inputs.Includes(directory.GetPath("one.fs"), true, cancelled.Token)).ThrowsExactly<OperationCanceledException>();
        using var duringDiscovery = new CancellationTokenSource();
        IEnumerable<Microsoft.Build.Globbing.MSBuildGlob> InterruptedPatterns()
        {
            yield return inputs.DeclaredIncludes[0];
            duringDiscovery.Cancel();
            yield return inputs.DeclaredIncludes[1];
        }
        await Assert.That(() => new SdkInputSearchPlan(InterruptedPatterns(), duringDiscovery.Token)).ThrowsExactly<OperationCanceledException>();

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
        File.WriteAllText(Path.Combine(modRoot, "Source", "Example.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><DefaultItemExcludes>$(DefaultItemExcludes);bin/**;obj/**</DefaultItemExcludes></PropertyGroup></Project>""");
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
        File.WriteAllText(path, Path.GetExtension(path) is ".csproj" or ".fsproj" or ".vbproj"
            ? """<Project Sdk="Microsoft.NET.Sdk" />""" : "content");
    }

    private static string NormalizeRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace((char)92, '/');
}
