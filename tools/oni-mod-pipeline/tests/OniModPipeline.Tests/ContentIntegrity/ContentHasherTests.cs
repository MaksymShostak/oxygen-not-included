using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Security.Cryptography;

namespace MaksymShostak.OniModPipeline.Tests.ContentIntegrity;

[TestClass]
public sealed class ContentHasherTests
{
    [TestMethod]
    public async Task HashFileAsync_WhenFileContainsArbitraryBytes_StreamsLengthAndLowercaseSha256()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("payload.bin");
        byte[] bytes = [0, 1, 2, 127, 128, 254, 255];
        await File.WriteAllBytesAsync(path, bytes);
        var hasher = new ContentHasher();

        var digest = await hasher.HashFileAsync(path, CancellationToken.None);

        Assert.AreEqual(Path.GetFullPath(path), digest.Path);
        Assert.AreEqual((long)bytes.Length, digest.ByteLength);
        Assert.AreEqual(
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            digest.Sha256);
        StringAssert.Matches(digest.Sha256, new("^[0-9a-f]{64}$"));
    }

    [TestMethod]
    public async Task CreateManifestAsync_WhenFilesSpanBothAreas_UsesAreaRelativeCanonicalEntries()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var releaseRoot = temporaryDirectory.GetPath("candidate");
        var contentDirectory = Path.Combine(releaseRoot, "workshop-content", "nested");
        var listingDirectory = Path.Combine(releaseRoot, "workshop-listing");
        Directory.CreateDirectory(contentDirectory);
        Directory.CreateDirectory(listingDirectory);
        var payload = Path.Combine(contentDirectory, "payload.dll");
        var description = Path.Combine(listingDirectory, "description.bbcode");
        await File.WriteAllBytesAsync(payload, [1, 2, 3]);
        await File.WriteAllTextAsync(description, "description\r\n");
        var hasher = new ContentHasher();

        var manifest = await hasher.CreateManifestAsync(
            releaseRoot,
            [
                (payload, ContentArea.WorkshopContent, ContentRole.Runtime),
                (description, ContentArea.WorkshopListing, ContentRole.Description)
            ],
            CancellationToken.None);

        Assert.AreEqual(1, manifest.SchemaVersion);
        Assert.AreEqual(2, manifest.Entries.Count);
        Assert.AreEqual("description.bbcode", manifest.Entries[0].RelativePath);
        Assert.AreEqual(ContentArea.WorkshopListing, manifest.Entries[0].ContentArea);
        Assert.AreEqual("nested/payload.dll", manifest.Entries[1].RelativePath);
        Assert.AreEqual(ContentArea.WorkshopContent, manifest.Entries[1].ContentArea);
        var expectedDigest = Convert.ToHexStringLower(
            SHA256.HashData(CanonicalContentManifestSerializer.Serialize(manifest.Entries)));
        Assert.AreEqual(expectedDigest, manifest.ContentDigest);
    }

    [TestMethod]
    public async Task CreateManifestAsync_WhenFileLeavesReleaseRoot_RejectsFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var releaseRoot = temporaryDirectory.GetPath("candidate");
        Directory.CreateDirectory(releaseRoot);
        var outside = temporaryDirectory.GetPath("outside.dll");
        await File.WriteAllTextAsync(outside, "outside");
        var hasher = new ContentHasher();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            hasher.CreateManifestAsync(
                releaseRoot,
                [(outside, ContentArea.WorkshopContent, ContentRole.Runtime)],
                CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateManifestAsync_WhenFileIsOutsideDeclaredArea_RejectsRoleBinding()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var releaseRoot = temporaryDirectory.GetPath("candidate");
        var listingDirectory = Path.Combine(releaseRoot, "workshop-listing");
        Directory.CreateDirectory(listingDirectory);
        var description = Path.Combine(listingDirectory, "description.bbcode");
        await File.WriteAllTextAsync(description, "description");
        var hasher = new ContentHasher();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            hasher.CreateManifestAsync(
                releaseRoot,
                [(description, ContentArea.WorkshopContent, ContentRole.Description)],
                CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateManifestAsync_WhenAbsolutePathIsRelative_RejectsFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var hasher = new ContentHasher();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            hasher.CreateManifestAsync(
                temporaryDirectory.Path,
                [("relative.bin", ContentArea.WorkshopContent, ContentRole.Runtime)],
                CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateManifestAsync_WhenAncestorIsReparsePoint_RejectsBeforeHashing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var releaseRoot = temporaryDirectory.GetPath("candidate");
        var contentDirectory = Path.Combine(releaseRoot, "workshop-content");
        Directory.CreateDirectory(contentDirectory);
        var payload = Path.Combine(contentDirectory, "payload.dll");
        await File.WriteAllTextAsync(payload, "payload");
        var hasher = new ContentHasher(
            path => string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(contentDirectory),
                StringComparison.OrdinalIgnoreCase)
                ? File.GetAttributes(path) | FileAttributes.ReparsePoint
                : File.GetAttributes(path),
            _ => null);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            hasher.CreateManifestAsync(
                releaseRoot,
                [(payload, ContentArea.WorkshopContent, ContentRole.Runtime)],
                CancellationToken.None));
    }

    [TestMethod]
    public async Task HashFileAsync_WhenLeafIsLink_RejectsBeforeHashing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var payload = temporaryDirectory.GetPath("payload.dll");
        await File.WriteAllTextAsync(payload, "payload");
        var hasher = new ContentHasher(
            path => File.GetAttributes(path) | FileAttributes.ReparsePoint,
            _ => null);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            hasher.HashFileAsync(payload, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateManifestAsync_WhenCancellationIsRequested_StopsBeforeHashing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var hasher = new ContentHasher();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            hasher.CreateManifestAsync(
                temporaryDirectory.Path,
                [],
                cancellation.Token));
    }
}
