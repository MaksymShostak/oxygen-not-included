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
        string repository = Environment.GetEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")!;
        string source = Path.Combine(repository, "mods", "delivery-temperature-limit-supercooled", "Tests", "Fixtures", "ThirdParty", "FastTrack");
        string[] expectedDirectories = expectedRelativePaths.SelectMany(path =>
            new[] { path.Split('/')[0], path[..path.LastIndexOf('/')] }).Distinct().ToArray();
        foreach (var root in new[] { source, fixtureRoot })
        {
            string Relative(string path) => NormalizeRelativePath(Path.GetRelativePath(root, path));
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(Relative).ToArray();
            await Assert.That(files).IsEquivalentTo(expectedRelativePaths).Because("Unexpected files in " + root);
            if (root == source)
            {
                var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).Select(Relative).ToArray();
                await Assert.That(directories).IsEquivalentTo(expectedDirectories).Because("Unexpected source fixture directory");
            }
        }
    }

    [Test]
    public async Task WorkshopLimitationIdentifiesTheMeasuredBetaInBothListingInputs()
    {
        string repository = Environment.GetEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT")!;
        string mod = Path.Combine(repository, "mods", "delivery-temperature-limit-supercooled");
        string measured = FastTrackSupportedAssemblyBuildCatalog.SelectedBuild.FileVersion.ToString();
        var versions = new System.Text.RegularExpressions.Regex(@"\bFast Track Beta (?<version>[0-9]+(?:\.[0-9]+)+)(?![\w]|\.[0-9])");
        foreach (string file in new[] { "STEAM_DESCRIPTION.bbcode", "STEAM_CHANGE_NOTES.bbcode" })
        {
            string listing = await File.ReadAllTextAsync(Path.Combine(mod, file));
            var recorded = versions.Matches(listing).Select(match => match.Groups["version"].Value).Distinct().ToArray();
            await Assert.That(recorded).IsEquivalentTo([measured]).Because(file);
        }
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
