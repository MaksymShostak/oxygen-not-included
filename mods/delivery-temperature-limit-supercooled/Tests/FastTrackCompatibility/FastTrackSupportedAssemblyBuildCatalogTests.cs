namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackSupportedAssemblyBuildCatalogTests
{
    private const string DigestA =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";
    private const string DigestB =
        "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B";

    [Test]
    public async Task BuildIdentity_WhenDigestUsesLowercase_StoresCanonicalUppercaseHexadecimal()
    {
        var identity = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            "d291c0d58379b77b4a60fb6d386b3783e4061e5c620def93502ae984cd657add");

        await Assert.That(identity.AssemblySha256).IsEqualTo(DigestA);
    }

    [Test]
    public async Task BuildIdentity_WhenFileVersionIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            new FastTrackAssemblyBuildIdentity(
                fileVersion: null!,
                DigestA)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task BuildIdentity_WhenDigestIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            new FastTrackAssemblyBuildIdentity(
                new Version(0, 18, 4, 0),
                assemblySha256: null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task BuildIdentity_WhenDigestShapeIsInvalid_ThrowsArgumentException()
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
            await Assert.That(() =>
                new FastTrackAssemblyBuildIdentity(
                    new Version(0, 18, 4, 0),
                    invalidDigest)).ThrowsExactly<ArgumentException>().Because($"Digest '{invalidDigest}' should have been rejected.");
        }
    }

    [Test]
    public async Task BuildIdentity_WhenValuesAreCanonicalEquivalent_HasValueEqualityAndEqualHashCode()
    {
        var uppercase = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA);
        var lowercase = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA.ToLowerInvariant());

        await Assert.That(lowercase).IsEqualTo(uppercase);
        await Assert.That(uppercase.Equals(lowercase)).IsTrue();
        await Assert.That(lowercase.GetHashCode()).IsEqualTo(uppercase.GetHashCode());
    }

    [Test]
    public async Task BuildIdentity_WhenVersionOrDigestDiffers_IsNotEqual()
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

        await Assert.That(differentVersion).IsNotEqualTo(baseline);
        await Assert.That(differentDigest).IsNotEqualTo(baseline);
        await Assert.That(baseline.Equals(null)).IsFalse();
    }

    [Test]
    public async Task Catalog_ConstructorCopiesInputAndSortsByVersionThenDigest()
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

        await Assert.That(catalog.Builds.ToArray()).IsEquivalentTo(new[]
            {
                earlierLowerDigest,
                earlierHigherDigest,
                laterBuild
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var exposedBuilds =
            (ICollection<FastTrackAssemblyBuildIdentity>)catalog.Builds;
        await Assert.That(exposedBuilds.IsReadOnly).IsTrue();
        await Assert.That(() =>
            exposedBuilds.Add(laterBuild)).ThrowsExactly<NotSupportedException>();
    }

    [Test]
    public async Task Catalog_WhenBuildSequenceIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
            new FastTrackSupportedAssemblyBuildCatalog(builds: null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Catalog_WhenBuildSequenceContainsNull_ThrowsArgumentException()
    {
        await Assert.That(() =>
            new FastTrackSupportedAssemblyBuildCatalog(
                new FastTrackAssemblyBuildIdentity[] { null! })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Catalog_WhenCanonicalIdentityRepeats_ThrowsArgumentException()
    {
        await Assert.That(() =>
            new FastTrackSupportedAssemblyBuildCatalog(new[]
            {
                new FastTrackAssemblyBuildIdentity(
                    new Version(0, 18, 4, 0),
                    DigestA),
                new FastTrackAssemblyBuildIdentity(
                    new Version(0, 18, 4, 0),
                    DigestA.ToLowerInvariant())
            })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Catalog_Contains_RequiresVersionAndDigestFromTheSameDeclaredBuild()
    {
        var first = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 4, 0),
            DigestA);
        var second = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 5, 0),
            DigestB);
        var catalog = new FastTrackSupportedAssemblyBuildCatalog(
            new[] { first, second });

        await Assert.That(catalog.Contains(
            first.FileVersion,
            DigestA.ToLowerInvariant())).IsTrue();
        await Assert.That(catalog.Contains(first.FileVersion, DigestB)).IsFalse();
        await Assert.That(catalog.Contains(second.FileVersion, DigestA)).IsFalse();
    }

    [Test]
    public async Task Catalog_Contains_WhenObservedDigestIsMalformed_ReturnsFalse()
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
            await Assert.That(catalog.Contains(
                new Version(0, 18, 4, 0),
                malformedDigest!)).IsFalse();
        }
    }

    [Test]
    public async Task Declared_WhenRead_ContainsExactlyTheTwoVerifiedFastTrackBuilds()
    {
        IReadOnlyList<FastTrackAssemblyBuildIdentity> builds =
            FastTrackSupportedAssemblyBuildCatalog.Declared.Builds;

        await Assert.That(builds).Count().IsEqualTo(2);
        await Assert.That(builds[0].FileVersion).IsEqualTo(new Version(0, 18, 4, 0));
        await Assert.That(builds[0].AssemblySha256).IsEqualTo(DigestA);
        await Assert.That(builds[1].FileVersion).IsEqualTo(new Version(0, 18, 5, 0));
        await Assert.That(builds[1].AssemblySha256).IsEqualTo(DigestB);
        await Assert.That(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[0].FileVersion,
            DigestA)).IsTrue();
        await Assert.That(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[1].FileVersion,
            DigestB)).IsTrue();
        await Assert.That(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[0].FileVersion,
            DigestB)).IsFalse();
        await Assert.That(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(
            builds[1].FileVersion,
            DigestA)).IsFalse();
    }
}
