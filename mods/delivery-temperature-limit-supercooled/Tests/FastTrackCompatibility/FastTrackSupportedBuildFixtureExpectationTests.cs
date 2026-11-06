namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackSupportedBuildFixtureExpectationTests
{
    [Test]
    public async Task DeclaredFixtures_WhenComparedWithSupportedBuildCatalog_HaveExactIdentityClosure()
    {
        FastTrackAssemblyBuildIdentity[] supportedBuilds =
            FastTrackSupportedAssemblyBuildCatalog.Declared.Builds.ToArray();
        FastTrackAssemblyBuildIdentity[] preservedBuilds =
            FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures
                .Select(fixture => fixture.AssemblyBuildIdentity)
                .ToArray();

        await Assert.That(preservedBuilds).IsEquivalentTo(supportedBuilds, TUnit.Assertions.Enums.CollectionOrdering.Matching);
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

        await Assert.That(actualRelativePaths).IsEquivalentTo(expectedRelativePaths, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("The preserved fixture tree must remain a closed set of exact " +
            "supported DLL evidence and its human provenance/license notes.");
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
