using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal sealed record RelevantSourceSet(
    string WorktreeRoot,
    IReadOnlyList<string> AbsolutePaths,
    IReadOnlyList<string> WorktreeRelativePaths)
{
    private const string BuildOutputPrefix = "{build-output}/";

    internal static OperationResult<RelevantSourceSet> Create(
        ModProfile profile,
        string worktreeRoot,
        IReadOnlyList<string> trackedWorktreeRelativePaths,
        string? pipelineExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreeRoot);
        ArgumentNullException.ThrowIfNull(trackedWorktreeRelativePaths);

        try
        {
            var builder = new SourceSetBuilder(worktreeRoot);
            var trackedAbsolutePaths = trackedWorktreeRelativePaths
                .Select(builder.ResolveWorktreeRelativePath)
                .ToArray();

            builder.AddAbsolutePath(profile.ManifestPath);
            builder.AddDeclaredFile(profile.ModRoot, profile.ModYamlPath);
            builder.AddDeclaredFile(profile.ModRoot, profile.ModInfoYamlPath);

            if (profile.Build is { } build)
            {
                var buildEntryPoint = builder.ResolveDeclaredPath(
                    profile.ModRoot,
                    build.EntryPoint);
                builder.AddAbsolutePath(buildEntryPoint);
                builder.AddTrackedProjectTree(
                    Path.GetDirectoryName(buildEntryPoint)!,
                    trackedAbsolutePaths);
            }

            foreach (var mapping in profile.PackageFiles)
            {
                if (mapping.Source.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.AddDeclaredFileOrTree(profile.ModRoot, mapping.Source);
            }

            builder.AddDeclaredFile(profile.ModRoot, profile.WorkshopListing.Description);
            builder.AddDeclaredFile(profile.ModRoot, profile.WorkshopListing.ChangeNotes);
            builder.AddDeclaredFile(profile.ModRoot, profile.WorkshopListing.Preview);

            foreach (var testProject in profile.TestProjects)
            {
                var testProjectPath = builder.ResolveDeclaredPath(
                    profile.ModRoot,
                    testProject.Path);
                builder.AddAbsolutePath(testProjectPath);
                builder.AddTrackedProjectTree(
                    Path.GetDirectoryName(testProjectPath)!,
                    trackedAbsolutePaths);
            }

            if (pipelineExecutablePath is not null &&
                builder.IsPipelineExecutableBuiltFromWorktree(pipelineExecutablePath))
            {
                builder.AddPipelineToolSources();
            }

            return new OperationResult<RelevantSourceSet>(
                builder.Build(),
                [],
                PipelineExitCode.Success);
        }
        catch (SourceSetException exception)
        {
            return new OperationResult<RelevantSourceSet>(
                null,
                [exception.Diagnostic],
                exception.ExitCode);
        }
    }

    private sealed class SourceSetBuilder
    {
        private readonly Dictionary<string, string> absolutePathByRelativePath =
            new(StringComparer.Ordinal);

        internal SourceSetBuilder(string worktreeRoot)
        {
            WorktreeRoot = Path.GetFullPath(worktreeRoot);
        }

        private string WorktreeRoot { get; }

        internal string ResolveWorktreeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Length >= 2 && char.IsAsciiLetter(relativePath[0]) && relativePath[1] == ':')
            {
                throw OutsideWorktree(relativePath);
            }

            var platformPath = relativePath
                .Replace((char)92, Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(WorktreeRoot, platformPath));
            EnsureInsideWorktree(absolutePath);
            return absolutePath;
        }

        internal string ResolveDeclaredPath(string root, string declaredPath)
        {
            var platformPath = declaredPath
                .Replace((char)92, Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(Path.GetFullPath(root), platformPath));
            EnsureInsideWorktree(absolutePath);
            return absolutePath;
        }

        internal void AddAbsolutePath(string absolutePath)
        {
            var canonicalPath = Path.GetFullPath(absolutePath);
            var relativePath = GetWorktreeRelativePath(canonicalPath);
            absolutePathByRelativePath.TryAdd(relativePath, canonicalPath);
        }

        internal void AddDeclaredFile(string root, string declaredPath)
        {
            var result = ContainedPathResolver.ResolveExistingFile(root, declaredPath);
            if (!result.IsSuccess)
            {
                throw new SourceSetException(
                    result.Diagnostics.Single(),
                    result.ExitCode);
            }

            AddAbsolutePath(result.Value!);
        }

        internal void AddDeclaredFileOrTree(string root, string declaredPath)
        {
            var fileResult = ContainedPathResolver.ResolveExistingFile(root, declaredPath);
            if (fileResult.IsSuccess)
            {
                AddAbsolutePath(fileResult.Value!);
                return;
            }

            var directoryResult = ContainedPathResolver.ResolveExistingDirectory(root, declaredPath);
            if (!directoryResult.IsSuccess)
            {
                throw new SourceSetException(
                    fileResult.Diagnostics.Single(),
                    fileResult.ExitCode);
            }

            foreach (var path in EnumerateRegularFiles(directoryResult.Value!))
            {
                AddAbsolutePath(path);
            }
        }

        internal void AddTrackedProjectTree(
            string projectDirectory,
            IReadOnlyList<string> trackedAbsolutePaths)
        {
            var canonicalProjectDirectory = Path.GetFullPath(projectDirectory);
            foreach (var trackedPath in trackedAbsolutePaths)
            {
                if (!TryGetDescendantRelativePath(
                    canonicalProjectDirectory,
                    trackedPath,
                    out var projectRelativePath) ||
                    HasBuildDirectorySegment(projectRelativePath))
                {
                    continue;
                }

                AddAbsolutePath(trackedPath);
            }
        }

        internal bool IsPipelineExecutableBuiltFromWorktree(string executablePath)
        {
            if (!TryGetDescendantRelativePath(
                WorktreeRoot,
                Path.GetFullPath(executablePath),
                out var relativePath))
            {
                return false;
            }

            relativePath = relativePath.Replace((char)92, '/');
            return relativePath.StartsWith("tools/oni-mod-pipeline/", StringComparison.Ordinal) &&
                relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
        }

        internal void AddPipelineToolSources()
        {
            var toolRoot = Path.Combine(WorktreeRoot, "tools", "oni-mod-pipeline");
            AddAbsolutePath(Path.Combine(WorktreeRoot, "global.json"));
            AddAbsolutePath(Path.Combine(toolRoot, "OniModPipeline.slnx"));
            AddAbsolutePath(Path.Combine(
                toolRoot,
                "src",
                "OniModPipeline",
                "OniModPipeline.csproj"));
            AddAbsolutePath(Path.Combine(
                toolRoot,
                "src",
                "OniModPipeline",
                "packages.lock.json"));
            AddAbsolutePath(Path.Combine(
                toolRoot,
                "tests",
                "OniModPipeline.Tests",
                "OniModPipeline.Tests.csproj"));
            AddAbsolutePath(Path.Combine(
                toolRoot,
                "tests",
                "OniModPipeline.Tests",
                "packages.lock.json"));

            if (!Directory.Exists(toolRoot))
            {
                return;
            }

            foreach (var sourcePath in EnumerateRegularFiles(toolRoot)
                .Where(path => string.Equals(
                    Path.GetExtension(path),
                    ".cs",
                    StringComparison.OrdinalIgnoreCase)))
            {
                var toolRelativePath = Path.GetRelativePath(toolRoot, sourcePath);
                if (!HasBuildDirectorySegment(toolRelativePath))
                {
                    AddAbsolutePath(sourcePath);
                }
            }
        }

        internal RelevantSourceSet Build()
        {
            var relativePaths = absolutePathByRelativePath.Keys
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var absolutePaths = relativePaths
                .Select(path => absolutePathByRelativePath[path])
                .ToArray();
            return new RelevantSourceSet(WorktreeRoot, absolutePaths, relativePaths);
        }

        private string GetWorktreeRelativePath(string absolutePath)
        {
            EnsureInsideWorktree(absolutePath);
            return Path.GetRelativePath(WorktreeRoot, absolutePath)
                .Replace((char)92, '/');
        }

        private void EnsureInsideWorktree(string absolutePath)
        {
            var relativePath = Path.GetRelativePath(WorktreeRoot, absolutePath);
            if (relativePath == "." ||
                Path.IsPathRooted(relativePath) ||
                relativePath == ".." ||
                relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            {
                throw OutsideWorktree(absolutePath);
            }
        }

        private SourceSetException OutsideWorktree(string path) =>
            new(
                DiagnosticCatalog.DirtyReleaseInput(
                    $"Contributing path '{path}' is outside worktree '{WorktreeRoot}'."),
                PipelineExitCode.ReleaseNotReady);

        private static bool TryGetDescendantRelativePath(
            string root,
            string candidate,
            out string relativePath)
        {
            relativePath = Path.GetRelativePath(root, candidate);
            return relativePath != "." &&
                !Path.IsPathRooted(relativePath) &&
                relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        }

        private static bool HasBuildDirectorySegment(string relativePath) =>
            relativePath
                .Replace((char)92, '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment =>
                    string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<string> EnumerateRegularFiles(string root)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(Path.GetFullPath(root));
            while (pendingDirectories.Count > 0)
            {
                var directory = pendingDirectories.Pop();
                foreach (var entry in Directory
                    .EnumerateFileSystemEntries(directory)
                    .OrderBy(path => path, StringComparer.Ordinal))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pendingDirectories.Push(entry);
                    }
                    else
                    {
                        yield return Path.GetFullPath(entry);
                    }
                }
            }
        }
    }

    private sealed class SourceSetException(
        Diagnostic diagnostic,
        PipelineExitCode exitCode) : Exception(diagnostic.Summary)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;

        internal PipelineExitCode ExitCode { get; } = exitCode;
    }
}
