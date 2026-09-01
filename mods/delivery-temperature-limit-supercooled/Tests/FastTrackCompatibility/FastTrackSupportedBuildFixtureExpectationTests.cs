namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackSupportedBuildFixtureExpectationTests
{
    [TestMethod]
    public void DeclaredFixtures_WhenComparedWithSupportedBuildCatalog_HaveExactIdentityClosure()
    {
        FastTrackAssemblyBuildIdentity[] supportedBuilds =
            FastTrackSupportedAssemblyBuildCatalog.Declared.Builds.ToArray();
        FastTrackAssemblyBuildIdentity[] preservedBuilds =
            FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures
                .Select(fixture => fixture.AssemblyBuildIdentity)
                .ToArray();

        CollectionAssert.AreEqual(supportedBuilds, preservedBuilds);
    }

    [TestMethod]
    public void CopiedFixtureTree_WhenEnumerated_ContainsOnlyDeclaredSupportedBuildArtifacts()
    {
        string fixtureRoot = RequireCopiedFixtureRoot();
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

        CollectionAssert.AreEqual(
            expectedRelativePaths,
            actualRelativePaths,
            "The preserved fixture tree must remain a closed set of exact " +
            "supported DLL evidence and its human provenance/license notes.");
    }

    private static string RequireCopiedFixtureRoot()
    {
        string fixtureRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "ThirdParty",
            "FastTrack"));
        Assert.IsTrue(
            Directory.Exists(fixtureRoot),
            "The preserved FastTrack fixtures must be copied as " +
            "non-reference test data by DeliveryTemperatureLimit.Tests.csproj.");
        return fixtureRoot;
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/');
}
