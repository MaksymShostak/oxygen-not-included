using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.Tests.Fixtures;

namespace MaksymShostak.OniModPipeline.Tests.EnvironmentDiscovery;

[TestClass]
public sealed class SteamLibraryCatalogTests
{
    [TestMethod]
    public void DiscoverLibraries_WhenModernVdfContainsQuotedPaths_ReturnsEveryLibraryOnce()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var steamRoot = temporaryDirectory.GetPath("Steam");
        var additionalLibrary = temporaryDirectory.GetPath("Steam Library With Spaces");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(additionalLibrary);
        var escapedRoot = EscapeVdf(steamRoot);
        var escapedLibrary = EscapeVdf(additionalLibrary);
        File.WriteAllText(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n" +
            $"  \"0\"\n  {{\n    \"path\" \"{escapedRoot}\"\n  }}\n" +
            $"  \"1\"\n  {{\n    \"path\" \"{escapedLibrary}\"\n" +
            "    \"apps\"\n    {\n      \"457140\" \"123456\"\n    }\n  }\n}\n");
        var catalog = new SteamLibraryCatalog();

        var libraries = catalog.DiscoverLibraries([steamRoot]);

        CollectionAssert.Contains(libraries.ToArray(), Path.GetFullPath(steamRoot));
        CollectionAssert.Contains(
            libraries.ToArray(),
            Path.GetFullPath(additionalLibrary));
        Assert.AreEqual(2, libraries.Count);
    }

    [TestMethod]
    public void DiscoverLibraries_WhenLegacyVdfContainsNumericPath_ReadsThatLibrary()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var steamRoot = temporaryDirectory.GetPath("Steam");
        var additionalLibrary = temporaryDirectory.GetPath("LegacyLibrary");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(additionalLibrary);
        File.WriteAllText(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            $"\"LibraryFolders\"\n{{\n  \"1\" \"{EscapeVdf(additionalLibrary)}\"\n}}\n");
        var catalog = new SteamLibraryCatalog();

        var libraries = catalog.DiscoverLibraries([steamRoot]);

        CollectionAssert.Contains(
            libraries.ToArray(),
            Path.GetFullPath(additionalLibrary));
    }

    [TestMethod]
    public void DiscoverLibraries_WhenVdfIsMalformed_KeepsTheConventionalSteamRoot()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var steamRoot = temporaryDirectory.GetPath("Steam");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        File.WriteAllText(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\" { \"0\" { \"path\" \"unterminated");
        var catalog = new SteamLibraryCatalog();

        var libraries = catalog.DiscoverLibraries([steamRoot]);

        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(steamRoot) },
            libraries.ToArray());
    }

    [TestMethod]
    public void DiscoverLibraries_WhenConventionalRootsAliasOneDirectory_DeduplicatesPhysicalIdentity()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var firstAlias = temporaryDirectory.GetPath("alias-a");
        var secondAlias = temporaryDirectory.GetPath("alias-b");
        var physicalRoot = temporaryDirectory.GetPath("physical-steam");
        var catalog = new SteamLibraryCatalog(path =>
            string.Equals(path, firstAlias, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, secondAlias, StringComparison.OrdinalIgnoreCase)
                ? physicalRoot
                : path);

        var libraries = catalog.DiscoverLibraries([firstAlias, secondAlias]);

        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(physicalRoot) },
            libraries.ToArray());
    }

    private static string EscapeVdf(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
