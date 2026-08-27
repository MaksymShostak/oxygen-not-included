using MaksymShostak.OniModPipeline.Diagnostics;

namespace MaksymShostak.OniModPipeline.ModProfiles;

internal static class ContainedPathResolver
{
    internal static OperationResult<string> ResolveExistingFile(
        string root,
        string declaredPath) =>
        ResolveExisting(
            root,
            declaredPath,
            ExpectedEntryKind.File,
            File.GetAttributes,
            ReadLinkTarget);

    internal static OperationResult<string> ResolveExistingFile(
        string root,
        string declaredPath,
        Func<string, FileAttributes> readAttributes,
        Func<string, string?> readLinkTarget) =>
        ResolveExisting(
            root,
            declaredPath,
            ExpectedEntryKind.File,
            readAttributes,
            readLinkTarget);

    internal static OperationResult<string> ResolveExistingDirectory(
        string root,
        string declaredPath) =>
        ResolveExisting(
            root,
            declaredPath,
            ExpectedEntryKind.Directory,
            File.GetAttributes,
            ReadLinkTarget);

    private static OperationResult<string> ResolveExisting(
        string root,
        string declaredPath,
        ExpectedEntryKind expectedEntryKind,
        Func<string, FileAttributes> readAttributes,
        Func<string, string?> readLinkTarget)
    {
        ArgumentNullException.ThrowIfNull(readAttributes);
        ArgumentNullException.ThrowIfNull(readLinkTarget);

        var renderedRoot = string.IsNullOrWhiteSpace(root) ? "<empty>" : root;
        var renderedDeclaredPath = string.IsNullOrWhiteSpace(declaredPath)
            ? "<empty>"
            : declaredPath;

        if (string.IsNullOrWhiteSpace(root))
        {
            return Unsafe(
                renderedRoot,
                renderedDeclaredPath,
                "the mod root must be a nonempty directory path.");
        }

        if (string.IsNullOrWhiteSpace(declaredPath))
        {
            return Unsafe(
                renderedRoot,
                renderedDeclaredPath,
                "the declaration must be nonempty.");
        }

        if (declaredPath.Contains('\0'))
        {
            return Unsafe(root, declaredPath, "the declaration contains a NUL character.");
        }

        if (IsPortableRootedPath(declaredPath))
        {
            return Unsafe(root, declaredPath, "the declaration must be relative.");
        }

        string resolvedRoot;
        string resolvedPath;
        try
        {
            resolvedRoot = Path.GetFullPath(root);
            var portableRelativePath = declaredPath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Replace((char)92, Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            resolvedPath = Path.GetFullPath(Path.Combine(resolvedRoot, portableRelativePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unsafe(root, declaredPath, "the declaration is not a valid filesystem path.");
        }

        var relativePath = Path.GetRelativePath(resolvedRoot, resolvedPath);
        if (relativePath == "." || IsEscapingRelativePath(relativePath))
        {
            return Unsafe(
                resolvedRoot,
                declaredPath,
                $"it resolves outside the strict descendant set to '{resolvedPath}'.");
        }

        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var currentPath = resolvedRoot;
        for (var segmentIndex = -1; segmentIndex < segments.Length; segmentIndex++)
        {
            if (segmentIndex >= 0)
            {
                currentPath = Path.Combine(currentPath, segments[segmentIndex]);
            }

            var isDirectory = Directory.Exists(currentPath);
            var isFile = File.Exists(currentPath);
            if (!isDirectory && !isFile)
            {
                return Missing(declaredPath, resolvedPath);
            }

            var isLeaf = segmentIndex == segments.Length - 1;
            if (!isLeaf && !isDirectory)
            {
                return Unsafe(
                    resolvedRoot,
                    declaredPath,
                    $"ancestor '{currentPath}' is not a directory.");
            }

            try
            {
                var attributes = readAttributes(currentPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    readLinkTarget(currentPath) is not null)
                {
                    return Unsafe(
                        resolvedRoot,
                        declaredPath,
                        $"path segment '{currentPath}' is a symbolic link or reparse point.");
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return Unsafe(
                    resolvedRoot,
                    declaredPath,
                    $"path segment '{currentPath}' could not be inspected: {exception.Message}");
            }
        }

        var kindMatches = expectedEntryKind switch
        {
            ExpectedEntryKind.File => File.Exists(resolvedPath),
            ExpectedEntryKind.Directory => Directory.Exists(resolvedPath),
            _ => false
        };
        if (!kindMatches)
        {
            var expectedKind = expectedEntryKind == ExpectedEntryKind.File
                ? "regular file"
                : "directory";
            return new OperationResult<string>(
                null,
                [DiagnosticCatalog.DeclaredInputWrongKind(
                    declaredPath,
                    resolvedPath,
                    expectedKind)],
                PipelineExitCode.InvalidInput);
        }

        return new OperationResult<string>(
            resolvedPath,
            [],
            PipelineExitCode.Success);
    }

    private static bool IsPortableRootedPath(string path) =>
        Path.IsPathRooted(path) ||
        path[0] == '/' ||
        path[0] == (char)92 ||
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static bool IsEscapingRelativePath(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath == ".." ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private static string? ReadLinkTarget(string path)
    {
        FileSystemInfo entry = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        return entry.LinkTarget;
    }

    private static OperationResult<string> Unsafe(
        string root,
        string declaredPath,
        string reason) =>
        new(
            null,
            [DiagnosticCatalog.UnsafeProfilePath(root, declaredPath, reason)],
            PipelineExitCode.InvalidInput);

    private static OperationResult<string> Missing(
        string declaredPath,
        string resolvedPath) =>
        new(
            null,
            [DiagnosticCatalog.DeclaredInputMissing(declaredPath, resolvedPath)],
            PipelineExitCode.InvalidInput);

    private enum ExpectedEntryKind
    {
        File,
        Directory
    }
}
