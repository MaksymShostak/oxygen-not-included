namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackSupportedAssemblyBuildCatalogTests
{
    private const string DigestA =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";
    private const string DigestB =
        "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B";

    [TestMethod]
    public void BuildIdentity_WhenDigestUsesLowercase_StoresCanonicalUppercaseHexadecimal()
    {
        var identity = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            "d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add");

        Assert.AreEqual(DigestA, identity.AssemblySha256);
    }

    [TestMethod]
    public void BuildIdentity_WhenFileVersionIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new FastTrackAssemblyBuildIdentity(
                fileVersion: null!,
                DigestA));
    }

    [TestMethod]
    public void BuildIdentity_WhenDigestIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new FastTrackAssemblyBuildIdentity(
                new Version(0, 18, 4, 0),
                assemblySha256: null!));
    }

    [TestMethod]
    public void BuildIdentity_WhenDigestShapeIsInvalid_ThrowsArgumentException()
    {
        string[] invalidDigests =
        {
            string.Empty,
            new string('A', 63),
            new string('A', 65),
            new string('G', 64)
        };

        foreach (string invalidDigest in invalidDigests)
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new FastTrackAssemblyBuildIdentity(
                    new Version(0, 18, 4, 0),
                    invalidDigest),
                $"Digest '{invalidDigest}' should have been rejected.");
        }
    }

    [TestMethod]
    public void BuildIdentity_WhenValuesAreCanonicalEquivalent_HasValueEqualityAndEqualHashCode()
    {
        var uppercase = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA);
        var lowercase = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA.ToLowerInvariant());

        Assert.AreEqual(uppercase, lowercase);
        Assert.IsTrue(uppercase.Equals(lowercase));
        Assert.AreEqual(uppercase.GetHashCode(), lowercase.GetHashCode());
    }

    [TestMethod]
    public void BuildIdentity_WhenVersionOrDigestDiffers_IsNotEqual()
    {
        var baseline = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA);
        var differentVersion = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 5, 0),
            DigestA);
        var differentDigest = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestB);

        Assert.AreNotEqual(baseline, differentVersion);
        Assert.AreNotEqual(baseline, differentDigest);
        Assert.IsFalse(baseline.Equals(null));
    }

    [TestMethod]
    public void Catalog_ConstructorCopiesInputAndSortsByVersionThenDigest()
    {
        var earlierHigherDigest = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA);
        var earlierLowerDigest = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestB);
        var laterBuild = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 5, 0),
            DigestB);
        var source = new List<FastTrackAssemblyBuildIdentity>
        {
            laterBuild,
            earlierHigherDigest,
            earlierLowerDigest
        };

        var catalog = new FastTrackSupportedAssemblyBuildCatalog(source);
        source.Clear();

        CollectionAssert.AreEqual(
            new[]
            {
                earlierLowerDigest,
                earlierHigherDigest,
                laterBuild
            },
            catalog.Builds.ToArray());
        var exposedBuilds =
            (ICollection<FastTrackAssemblyBuildIdentity>)catalog.Builds;
        Assert.IsTrue(exposedBuilds.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            exposedBuilds.Add(laterBuild));
    }

    [TestMethod]
    public void Catalog_WhenBuildSequenceIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new FastTrackSupportedAssemblyBuildCatalog(builds: null!));
    }

    [TestMethod]
    public void Catalog_WhenBuildSequenceContainsNull_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new FastTrackSupportedAssemblyBuildCatalog(
                new FastTrackAssemblyBuildIdentity[] { null! }));
    }

    [TestMethod]
    public void Catalog_WhenCanonicalIdentityRepeats_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new FastTrackSupportedAssemblyBuildCatalog(new[]
            {
                new FastTrackAssemblyBuildIdentity(
                    new Version(0, 18, 4, 0),
                    DigestA),
                new FastTrackAssemblyBuildIdentity(
                    new Version(0, 18, 4, 0),
                    DigestA.ToLowerInvariant())
            }));
    }

    [TestMethod]
    public void Catalog_Contains_RequiresVersionAndDigestFromTheSameDeclaredBuild()
    {
        var first = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA);
        var second = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 5, 0),
            DigestB);
        var catalog = new FastTrackSupportedAssemblyBuildCatalog(
            new[] { first, second });

        Assert.IsTrue(catalog.Contains(
            first.FileVersion,
            DigestA.ToLowerInvariant()));
        Assert.IsFalse(catalog.Contains(first.FileVersion, DigestB));
        Assert.IsFalse(catalog.Contains(second.FileVersion, DigestA));
    }

    [TestMethod]
    public void Catalog_Contains_WhenObservedDigestIsMalformed_ReturnsFalse()
    {
        var catalog = new FastTrackSupportedAssemblyBuildCatalog(new[]
        {
            new FastTrackAssemblyBuildIdentity(
                new Version(0, 18, 4, 0),
                DigestA)
        });
        string?[] malformedDigests =
        {
            null,
            string.Empty,
            new string('A', 63),
            new string('A', 65),
            new string('G', 64)
        };

        foreach (string? malformedDigest in malformedDigests)
        {
            Assert.IsFalse(catalog.Contains(
                new Version(0, 18, 4, 0),
                malformedDigest!));
        }
    }

    [TestMethod]
    public void Declared_WhenRead_ContainsExactlyTheTwoVerifiedFastTrackBuilds()
    {
        IReadOnlyList<FastTrackAssemblyBuildIdentity> builds =
            FastTrackSupportedAssemblyBuildCatalog.Declared.Builds;

        Assert.HasCount(2, builds);
        Assert.AreEqual(new Version(0, 18, 4, 0), builds[0].FileVersion);
        Assert.AreEqual(DigestA, builds[0].AssemblySha256);
        Assert.AreEqual(new Version(0, 18, 5, 0), builds[1].FileVersion);
        Assert.AreEqual(DigestB, builds[1].AssemblySha256);
        Assert.IsTrue(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[0].FileVersion,
            DigestA));
        Assert.IsTrue(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[1].FileVersion,
            DigestB));
        Assert.IsFalse(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[0].FileVersion,
            DigestB));
        Assert.IsFalse(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[1].FileVersion,
            DigestA));
    }
}
