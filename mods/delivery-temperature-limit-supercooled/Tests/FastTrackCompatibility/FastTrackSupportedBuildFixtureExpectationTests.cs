namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackSupportedBuildFixtureExpectationTests
{
    [Test]
    public async Task EveryRuntimeAdmissionMustMatchTheOneMeasuredRelease()
    {
        FastTrackAssemblyBuildIdentity[] supportedBuilds =
            FastTrackSupportedAssemblyBuildCatalog.Declared.Builds.ToArray();
        FastTrackAssemblyBuildIdentity[] preservedBuilds =
            FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures
                .Select(fixture => fixture.AssemblyBuildIdentity)
                .ToArray();

        await Assert.That(preservedBuilds).Count().IsEqualTo(1);
        await Assert.That(supportedBuilds.All(build => build.Equals(preservedBuilds[0]))).IsTrue();
    }

    [Test]
    public async Task CopiedFixtureTree_WhenEnumerated_ContainsOnlyDeclaredSupportedBuildArtifacts()
    {
        string fixtureRoot = await RequireCopiedFixtureRoot();
        string[] expectedRelativePaths =
            FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures
                .SelectMany(fixture => new[]
                {
                    Path.Combine(
                        fixture.RelativeFixtureDirectoryPath,
                        "FastTrack.dll"),
                    Path.Combine(
                        fixture.RelativeFixtureDirectoryPath,
                        "README.md"),
                    Path.Combine(
                        fixture.RelativeFixtureDirectoryPath,
                        "UPSTREAM-LICENSE.txt")
                })
                .Select(NormalizeRelativePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        string[] actualRelativePaths = Directory
            .EnumerateFiles(
                fixtureRoot,
                "*",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(fixtureRoot, path))
            .Select(NormalizeRelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string repository = Environment.GetEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")!;
        string source = Path.Combine(repository, "mods", "delivery-temperature-limit-supercooled", "Tests", "Fixtures", "ThirdParty", "FastTrack");
        var current = FastTrackSupportedAssemblyBuildCatalog.SelectedBuild;
        await Assert.That(Directory.GetDirectories(source).Select(path => Path.GetFileName(path)!).ToArray()).IsEquivalentTo([current.FileVersion.ToString()]);
        await Assert.That(Directory.GetDirectories(Path.Combine(source, current.FileVersion.ToString())).Select(path => Path.GetFileName(path)!).ToArray())
            .IsEquivalentTo(["sha256-" + current.AssemblySha256.ToLowerInvariant()]);
        await Assert.That(actualRelativePaths).IsEquivalentTo(expectedRelativePaths, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("The preserved fixture tree must remain a closed set of exact " +
            "selected DLL evidence and its human provenance/license notes.");
    }

    private static async Task<string> RequireCopiedFixtureRoot()
    {
        string fixtureRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "ThirdParty",
            "FastTrack"));
        await Assert.That(Directory.Exists(fixtureRoot)).IsTrue().Because("The preserved FastTrack fixtures must be copied as " +
            "non-reference test data by DeliveryTemperatureLimit.Tests.csproj.");
        return fixtureRoot;
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/');
}
