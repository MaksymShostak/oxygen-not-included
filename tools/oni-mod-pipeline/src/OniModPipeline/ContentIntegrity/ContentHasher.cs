using System.Security.Cryptography;

namespace MaksymShostak.OniModPipeline.ContentIntegrity;

internal sealed class ContentHasher
{
    private const int StreamBufferSize = 64 * 1024;

    private readonly Func<string, FileAttributes> readAttributes;
    private readonly Func<string, string?> readLinkTarget;

    internal ContentHasher()
        : this(File.GetAttributes, ReadLinkTarget)
    {
    }

    internal ContentHasher(
        Func<string, FileAttributes> readAttributes,
        Func<string, string?> readLinkTarget)
    {
        ArgumentNullException.ThrowIfNull(readAttributes);
        ArgumentNullException.ThrowIfNull(readLinkTarget);
        this.readAttributes = readAttributes;
        this.readLinkTarget = readLinkTarget;
    }

    internal async Task<FileDigest> HashFileAsync(
        string absolutePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolvedPath = ResolveAbsolutePath(absolutePath, nameof(absolutePath));
        EnsureRegularNonLinkFile(resolvedPath);

        await using var stream = new FileStream(
            resolvedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha256 = SHA256.Create();
        var hash = await sha256
            .ComputeHashAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        var byteLength = stream.Position;
        EnsureRegularNonLinkFile(resolvedPath);

        return new FileDigest(
            resolvedPath,
            byteLength,
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    internal async Task<ReleaseContentManifest> CreateManifestAsync(
        string releaseContentRoot,
        IReadOnlyList<(string AbsolutePath, ContentArea Area, ContentRole Role)> files,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseContentRoot);
        ArgumentNullException.ThrowIfNull(files);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedRoot = Path.GetFullPath(releaseContentRoot);
        EnsureDirectoryExistsAndIsNotLink(resolvedRoot, "release content root");

        var entries = new List<ReleaseContentEntry>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedPath = ResolveAbsolutePath(file.AbsolutePath, "files.AbsolutePath");
            EnsureStrictDescendant(resolvedRoot, resolvedPath, "release content root");

            string areaName;
            try
            {
                areaName = file.Area.ToCanonicalName();
                _ = file.Role.ToCanonicalName();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException(
                    "A release content file has an unknown area or role.",
                    exception);
            }

            var areaRoot = Path.Combine(resolvedRoot, areaName);
            EnsureStrictDescendant(areaRoot, resolvedPath, $"'{areaName}' area");
            EnsureContainedRegularFile(resolvedRoot, resolvedPath);

            var relativePath = Path
                .GetRelativePath(areaRoot, resolvedPath)
                .Replace('\\', '/');
            var digest = await HashFileAsync(resolvedPath, cancellationToken)
                .ConfigureAwait(false);
            entries.Add(new ReleaseContentEntry(
                file.Area,
                relativePath,
                digest.ByteLength,
                digest.Sha256,
                file.Role));
        }

        var canonicalEntries = CanonicalContentManifestSerializer.Canonicalize(entries);
        var canonicalBytes = CanonicalContentManifestSerializer.Serialize(canonicalEntries);
        var contentDigest = Convert
            .ToHexString(SHA256.HashData(canonicalBytes))
            .ToLowerInvariant();
        return new ReleaseContentManifest(1, canonicalEntries, contentDigest);
    }

    private static string ResolveAbsolutePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "A release content file path must be absolute.",
                parameterName);
        }

        return Path.GetFullPath(path);
    }

    private void EnsureContainedRegularFile(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var currentPath = root;
        for (var index = -1; index < segments.Length; index++)
        {
            if (index >= 0)
            {
                currentPath = Path.Combine(currentPath, segments[index]);
            }

            var isLeaf = index == segments.Length - 1;
            if (isLeaf)
            {
                EnsureRegularNonLinkFile(currentPath);
                continue;
            }

            EnsureDirectoryExistsAndIsNotLink(currentPath, "release content path ancestor");
        }
    }

    private void EnsureRegularNonLinkFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Release content file '{path}' does not exist.",
                path);
        }

        var attributes = ReadAttributes(path);
        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new InvalidOperationException(
                $"Release content path '{path}' must be a regular file.");
        }

        EnsureNotLink(path, attributes);
    }

    private void EnsureDirectoryExistsAndIsNotLink(string path, string description)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"The {description} '{path}' does not exist as a directory.");
        }

        var attributes = ReadAttributes(path);
        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException(
                $"The {description} '{path}' must be a directory.");
        }

        EnsureNotLink(path, attributes);
    }

    private FileAttributes ReadAttributes(string path)
    {
        try
        {
            return readAttributes(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Release content path '{path}' could not be inspected.",
                exception);
        }
    }

    private void EnsureNotLink(string path, FileAttributes attributes)
    {
        string? linkTarget;
        try
        {
            linkTarget = readLinkTarget(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Release content path '{path}' could not be inspected for links.",
                exception);
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 || linkTarget is not null)
        {
            throw new InvalidOperationException(
                $"Release content path '{path}' must not be a symbolic link or reparse point.");
        }
    }

    private static void EnsureStrictDescendant(
        string root,
        string path,
        string description)
    {
        var relativePath = Path.GetRelativePath(root, path);
        if (relativePath == "." ||
            Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            relativePath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Release content file '{path}' must remain beneath the {description} '{root}'.");
        }
    }

    private static string? ReadLinkTarget(string path)
    {
        FileSystemInfo entry = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        return entry.LinkTarget;
    }
}
