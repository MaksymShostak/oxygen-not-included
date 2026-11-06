using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Tests.Fixtures;
using System.Security.Cryptography;

namespace MaksymShostak.OniModPipeline.Tests.ContentIntegrity;
public sealed class ContentHasherTests
{
    [Test]
    public async Task HashFileAsync_WhenFileContainsArbitraryBytes_StreamsLengthAndLowercaseSha256()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("payload.bin");
        byte[] bytes = [0, 1, 2, 127, 128, 254, 255];
        await File.WriteAllBytesAsync(path, bytes);
        var hasher = new ContentHasher();

        var digest = await hasher.HashFileAsync(path, CancellationToken.None);

        await Assert.That(digest.Path).IsEqualTo(Path.GetFullPath(path));
        await Assert.That(digest.ByteLength).IsEqualTo((long)bytes.Length);
        await Assert.That(digest.Sha256).IsEqualTo(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        await Assert.That(digest.Sha256).Matches("^[0-9a-f]{64}$");
    }

    [Test]
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

        await Assert.That(manifest.SchemaVersion).IsEqualTo(1);
        await Assert.That(manifest.Entries.Count).IsEqualTo(2);
        await Assert.That(manifest.Entries[0].RelativePath).IsEqualTo("description.bbcode");
        await Assert.That(manifest.Entries[0].ContentArea).IsEqualTo(ContentArea.WorkshopListing);
        await Assert.That(manifest.Entries[1].RelativePath).IsEqualTo("nested/payload.dll");
        await Assert.That(manifest.Entries[1].ContentArea).IsEqualTo(ContentArea.WorkshopContent);
        var expectedDigest = Convert.ToHexStringLower(
            SHA256.HashData(CanonicalContentManifestSerializer.Serialize(manifest.Entries)));
        await Assert.That(manifest.ContentDigest).IsEqualTo(expectedDigest);
    }

    [Test]
    public async Task CreateManifestAsync_WhenFileLeavesReleaseRoot_RejectsFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var releaseRoot = temporaryDirectory.GetPath("candidate");
        Directory.CreateDirectory(releaseRoot);
        var outside = temporaryDirectory.GetPath("outside.dll");
        await File.WriteAllTextAsync(outside, "outside");
        var hasher = new ContentHasher();

        await Assert.That(async () =>
{await hasher.CreateManifestAsync(
                releaseRoot,
                [(outside, ContentArea.WorkshopContent, ContentRole.Runtime)],
                CancellationToken.None);}).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task CreateManifestAsync_WhenFileIsOutsideDeclaredArea_RejectsRoleBinding()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var releaseRoot = temporaryDirectory.GetPath("candidate");
        var listingDirectory = Path.Combine(releaseRoot, "workshop-listing");
        Directory.CreateDirectory(listingDirectory);
        var description = Path.Combine(listingDirectory, "description.bbcode");
        await File.WriteAllTextAsync(description, "description");
        var hasher = new ContentHasher();

        await Assert.That(async () =>
{await hasher.CreateManifestAsync(
                releaseRoot,
                [(description, ContentArea.WorkshopContent, ContentRole.Description)],
                CancellationToken.None);}).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task CreateManifestAsync_WhenAbsolutePathIsRelative_RejectsFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var hasher = new ContentHasher();

        await Assert.That(async () =>
{await hasher.CreateManifestAsync(
                temporaryDirectory.Path,
                [("relative.bin", ContentArea.WorkshopContent, ContentRole.Runtime)],
                CancellationToken.None);}).ThrowsExactly<ArgumentException>();
    }

    [Test]
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

        await Assert.That(async () =>
{await hasher.CreateManifestAsync(
                releaseRoot,
                [(payload, ContentArea.WorkshopContent, ContentRole.Runtime)],
                CancellationToken.None);}).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task HashFileAsync_WhenLeafIsLink_RejectsBeforeHashing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var payload = temporaryDirectory.GetPath("payload.dll");
        await File.WriteAllTextAsync(payload, "payload");
        var hasher = new ContentHasher(
            path => File.GetAttributes(path) | FileAttributes.ReparsePoint,
            _ => null);

        await Assert.That(async () =>
{await hasher.HashFileAsync(payload, CancellationToken.None);}).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task CreateManifestAsync_WhenCancellationIsRequested_StopsBeforeHashing()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var hasher = new ContentHasher();

        await Assert.That(async () =>
{await hasher.CreateManifestAsync(
                temporaryDirectory.Path,
                [],
                cancellation.Token);}).ThrowsExactly<OperationCanceledException>();
    }
}
