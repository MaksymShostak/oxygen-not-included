namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackSupportedAssemblyBuildCatalogTests
{
    private const string DigestA =
        "EFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEFEF";
    private const string DigestB =
        "CDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCDCD";

    [Test]
    public async Task BuildIdentity_WhenDigestUsesLowercase_StoresCanonicalUppercaseHexadecimal()
    {
        var identity = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            "efefefefefefefefefefefefefefefefefefefefefefefefefefefefefefefef");

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
                new Version(1, 2, 3, 4),
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
                    new Version(1, 2, 3, 4),
                    invalidDigest)).ThrowsExactly<ArgumentException>().Because($"Digest '{invalidDigest}' should have been rejected.");
        }
    }

    [Test]
    public async Task BuildIdentity_WhenValuesAreCanonicalEquivalent_HasValueEqualityAndEqualHashCode()
    {
        var uppercase = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestA);
        var lowercase = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestA.ToLowerInvariant());

        await Assert.That(lowercase).IsEqualTo(uppercase);
        await Assert.That(uppercase.Equals(lowercase)).IsTrue();
        await Assert.That(lowercase.GetHashCode()).IsEqualTo(uppercase.GetHashCode());
    }

    [Test]
    public async Task BuildIdentity_WhenVersionOrDigestDiffers_IsNotEqual()
    {
        var baseline = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestA);
        var differentVersion = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 5),
            DigestA);
        var differentDigest = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestB);

        await Assert.That(differentVersion).IsNotEqualTo(baseline);
        await Assert.That(differentDigest).IsNotEqualTo(baseline);
        await Assert.That(baseline.Equals(null)).IsFalse();
    }

    [Test]
    public async Task Catalog_ConstructorCopiesInputAndSortsByVersionThenDigest()
    {
        var earlierHigherDigest = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestA);
        var earlierLowerDigest = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestB);
        var laterBuild = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 5),
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
                    new Version(1, 2, 3, 4),
                    DigestA),
                new FastTrackAssemblyBuildIdentity(
                    new Version(1, 2, 3, 4),
                    DigestA.ToLowerInvariant())
            })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Catalog_Contains_RequiresVersionAndDigestFromTheSameDeclaredBuild()
    {
        var first = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 4),
            DigestA);
        var second = new FastTrackAssemblyBuildIdentity(
            new Version(1, 2, 3, 5),
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
                new Version(1, 2, 3, 4),
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
                new Version(1, 2, 3, 4),
                malformedDigest!)).IsFalse();
        }
    }

    [Test]
    public async Task DistributableCatalog_WithNoQualifiedConfiguration_DoesNotAdmitTheSelectedRelease()
    {
        var selected = FastTrackSupportedAssemblyBuildCatalog.SelectedBuild;
        await Assert.That(FastTrackSupportedAssemblyBuildCatalog.Declared.Builds).IsEmpty();
        await Assert.That(FastTrackSupportedAssemblyBuildCatalog.Declared.Contains(selected.FileVersion, selected.AssemblySha256)).IsFalse();
    }
}
