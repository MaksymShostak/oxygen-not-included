using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace MaksymShostak.OniModPipeline.SourceControl;

internal sealed record RelevantSourceSet(
    string WorktreeRoot,
    IReadOnlyList<string> AbsolutePaths,
    IReadOnlyList<string> WorktreeRelativePaths)
{
    private const string BuildOutputPrefix = "{build-output}/";

    internal static async Task<OperationResult<RelevantSourceSet>> CreateAsync(
        ModProfile profile,
        string worktreeRoot,
        IReadOnlyList<string> observedWorktreePaths,
        string? pipelineExecutablePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreeRoot);
        ArgumentNullException.ThrowIfNull(observedWorktreePaths);

        try
        {
            var builder = new SourceSetBuilder(worktreeRoot);
            var observedAbsolutePaths = observedWorktreePaths
                .Select(builder.ResolveWorktreeRelativePath)
                .ToArray();

            if (profile.Build is not null || profile.TestProjects.Count > 0)
            {
                builder.AddOptionalControlFile("Directory.Packages.props", observedWorktreePaths);
                builder.AddOptionalControlFile("global.json", observedWorktreePaths);
                builder.AddOptionalControlFile("tools/ci/oni_references.py", observedWorktreePaths);
                builder.AddOptionalControlFile("tools/ci/oni-references.json", observedWorktreePaths);
                builder.AddOptionalControlFile("tests/TUnitProjectDefaults.cs", observedWorktreePaths);
                builder.AddOptionalControlFile("tests/TUnitProject.props", observedWorktreePaths);
            }

            builder.AddAbsolutePath(profile.ManifestPath);
            builder.AddDeclaredFile(profile.ModRoot, profile.ModYamlPath);
            builder.AddDeclaredFile(profile.ModRoot, profile.ModInfoYamlPath);
            if (profile.Catalogs is { } catalogs)
            {
                builder.AddDeclaredFileOrTree(profile.ModRoot, catalogs.Directory, observedAbsolutePaths);
                builder.AddDeclaredFile(profile.ModRoot, catalogs.OptionsSource);
            }

            if (profile.Build is { } build)
            {
                var buildEntryPoint = builder.ResolveDeclaredPath(
                    profile.ModRoot,
                    build.EntryPoint);
                builder.AddAbsolutePath(buildEntryPoint);
                await builder.AddProjectTreeAsync(
                    buildEntryPoint,
                    observedAbsolutePaths, cancellationToken).ConfigureAwait(false);
            }

            foreach (var mapping in profile.PackageFiles)
            {
                if (mapping.Source.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.AddDeclaredFileOrTree(profile.ModRoot, mapping.Source, observedAbsolutePaths);
            }

            builder.AddDeclaredFile(profile.ModRoot, profile.WorkshopListing.Description);
            builder.AddDeclaredFile(profile.ModRoot, profile.WorkshopListing.ChangeNotes);
            builder.AddDeclaredFile(profile.ModRoot, profile.WorkshopListing.Preview);
            if (profile.Readme is not null)
            {
                builder.AddDeclaredFile(worktreeRoot, profile.Readme.RepositoryPath);
                builder.AddDeclaredFile(worktreeRoot, "package.json");
                builder.AddDeclaredFile(worktreeRoot, "package-lock.json");
                builder.AddOptionalControlFile(".markdown-quality.json", observedWorktreePaths);
                builder.AddOptionalControlFile(".markdown-quality-execution.json", observedWorktreePaths);
                builder.AddDeclaredFile(worktreeRoot, ".node-version");
                builder.AddDeclaredFile(worktreeRoot, ".python-version");
                builder.AddDeclaredFile(worktreeRoot, "tools/readme/canonicalize.mjs");
                builder.AddDeclaredFile(worktreeRoot, "tools/readme/compatibility-badges.mjs");
                builder.AddDeclaredFile(worktreeRoot, "tools/readme/render-description.mjs");
            }

            foreach (var testProject in profile.TestProjects)
            {
                var testProjectPath = builder.ResolveDeclaredPath(
                    profile.ModRoot,
                    testProject.Path);
                builder.AddAbsolutePath(testProjectPath);
                await builder.AddProjectTreeAsync(
                    testProjectPath,
                    observedAbsolutePaths, cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception) when (exception is SolutionException or InvalidDataException or System.Xml.XmlException)
        {
            return new(null, [DiagnosticCatalog.GitProvenanceUnavailable(exception.Message)], PipelineExitCode.ReleaseNotReady);
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

        // Missing optional policy is a valid default. Retain tracked deletions
        // so Git status still refuses a control removed during preparation.
        internal void AddOptionalControlFile(string relativePath, IReadOnlyList<string> observedPaths)
        {
            var path = ResolveDeclaredPath(WorktreeRoot, relativePath);
            if (File.Exists(path) || Directory.Exists(path)) AddDeclaredFile(WorktreeRoot, relativePath);
            else if (observedPaths.Contains(relativePath, StringComparer.Ordinal)) AddAbsolutePath(path);
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

        internal void AddDeclaredFileOrTree(string root, string declaredPath, IReadOnlyList<string> observedPaths)
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

            var directory = directoryResult.Value!;
            foreach (var path in EnumerateRegularFiles(directory))
                AddAbsolutePath(path);
            // Deleted files no longer exist on disk, but remain inputs to the selected directory mapping.
            foreach (var path in observedPaths)
                if (TryGetDescendantRelativePath(directory, path, out _)) AddAbsolutePath(path);
        }

        internal async Task AddProjectTreeAsync(string entryPoint, IReadOnlyList<string> observedPaths, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string directory = Path.GetDirectoryName(entryPoint)!;
            bool solution = SourceExtension(entryPoint) is null;
            foreach (var path in observedPaths)
            {
                if (!TryGetDescendantRelativePath(directory, path, out var relative)) continue;
                if (solution ? HasBuildDirectorySegment(relative) : IsTopLevelBuildOutput(relative)) continue;
                AddAbsolutePath(path);
            }

            string? input = ExistingContainedFile(entryPoint);
            if (input is null) return;
            if (!solution)
            {
                AddSdkProjectInputs(input);
                return;
            }

            var serializer = SolutionSerializers.GetSerializerByMoniker(input)
                ?? throw new InvalidDataException($"Unsupported solution format at '{input}'.");
            var model = await serializer.OpenAsync(input, token).ConfigureAwait(false);
            foreach (var project in model.SolutionProjects)
            {
                token.ThrowIfCancellationRequested();
                string member = ResolveDeclaredPath(directory, project.FilePath);
                AddAbsolutePath(member);
                string? existing = ExistingContainedFile(member);
                if (existing is not null && SourceExtension(existing) is not null)
                    AddSdkProjectInputs(existing);
            }
        }

        // Validate the whole path from the operator's checkout before a directory is enumerated.
        // Missing tracked files still participate in Git status even though they cannot be read.
        private string? ExistingContainedFile(string path)
        {
            var verified = ContainedPathResolver.ResolveExistingFile(WorktreeRoot, GetWorktreeRelativePath(path));
            if (verified.IsSuccess) return verified.Value!;
            var diagnostic = verified.Diagnostics.Single();
            if (diagnostic.Id == DiagnosticIds.DeclaredInputMissing) return null;
            throw new SourceSetException(diagnostic, verified.ExitCode);
        }

        private void AddSdkProjectInputs(string project)
        {
            string root = Path.GetDirectoryName(project)!;
            var inputs = SdkProjectInputs.Read(project, SourceExtension(project)!);
            bool IncludeDirectory(string path) => !Path.GetFileName(path).StartsWith('.') &&
                !IsTopLevelBuildOutput(Path.GetRelativePath(root, path));
            bool IncludeFile(string path) => inputs.IncludesDefault(Path.GetRelativePath(root, path));
            foreach (var path in EnumerateRegularFiles(root, IncludeDirectory, IncludeFile))
                AddAbsolutePath(path);
            foreach (var pattern in inputs.DeclaredItems)
                AddDeclaredProjectItems(root, pattern);
        }

        private void AddDeclaredProjectItems(string projectDirectory, SdkProjectInputs.Item item)
        {
            var (prefix, glob) = BindPattern(projectDirectory, item.Include);
            var exclusions = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var exclude in item.Excludes)
            {
                var (excludeRoot, tail) = BindPattern(projectDirectory, exclude);
                string relative = Path.GetRelativePath(WorktreeRoot, excludeRoot).Replace('\\', '/');
                exclusions.AddInclude(tail is null ? relative : (relative == "." ? tail : relative + "/" + tail));
            }
            bool Excluded(string path) => exclusions.Match(Path.GetRelativePath(WorktreeRoot, path).Replace('\\', '/')).HasMatches;
            if (glob is null)
            {
                if (!Excluded(prefix) && ExistingContainedFile(prefix) is { } file) AddAbsolutePath(file);
                return;
            }

            // Validate the non-wildcard prefix before enumerating anything on disk.
            if (Path.GetRelativePath(WorktreeRoot, prefix) != ".")
            {
                var verified = ContainedPathResolver.ResolveExistingDirectory(WorktreeRoot, GetWorktreeRelativePath(prefix));
                if (!verified.IsSuccess)
                {
                    var diagnostic = verified.Diagnostics.Single();
                    if (diagnostic.Id == DiagnosticIds.DeclaredInputMissing) return;
                    throw new SourceSetException(diagnostic, verified.ExitCode);
                }
            }
            var includes = new Matcher(StringComparison.OrdinalIgnoreCase).AddInclude(glob);
            bool Matches(string path) => includes.Match(Path.GetRelativePath(prefix, path).Replace('\\', '/')).HasMatches && !Excluded(path);
            foreach (var path in EnumerateRegularFiles(prefix, includeFile: Matches)) AddAbsolutePath(path);
        }

        private (string Path, string? Glob) BindPattern(string projectDirectory, string pattern)
        {
            int wildcard = pattern.IndexOfAny(['*', '?']);
            if (wildcard < 0) return (ResolveDeclaredPath(projectDirectory, pattern), null);
            int slash = pattern.LastIndexOf('/', wildcard);
            string prefix = slash < 0 ? "." : pattern[..(slash + 1)];
            var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(projectDirectory, prefix)));
            if (Path.GetRelativePath(WorktreeRoot, directory) != ".") EnsureInsideWorktree(directory);
            return (directory, pattern[(slash + 1)..]);
        }

        private static string? SourceExtension(string project) => Path.GetExtension(project).ToLowerInvariant() switch
        {
            ".csproj" => ".cs",
            ".vbproj" => ".vb",
            ".fsproj" => ".fs",
            _ => null
        };

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
            AddAbsolutePath(Path.Combine(WorktreeRoot, "Directory.Packages.props"));
            AddAbsolutePath(Path.Combine(WorktreeRoot, "tools", "ci", "oni_references.py"));
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

            foreach (var sourcePath in EnumerateRegularFiles(toolRoot,
                directory => !IsBuildDirectoryName(Path.GetFileName(directory)),
                path => Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".py"))
                AddAbsolutePath(sourcePath);
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

        private static bool IsBuildDirectoryName(string name) =>
            name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase);

        private static bool IsTopLevelBuildOutput(string relativePath)
        {
            int end = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            return IsBuildDirectoryName(end < 0 ? relativePath : relativePath[..end]);
        }

        private static bool HasBuildDirectorySegment(string relativePath) =>
            relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(IsBuildDirectoryName);

        private static IEnumerable<string> EnumerateRegularFiles(string root, Func<string, bool>? includeDirectory = null,
            Func<string, bool>? includeFile = null)
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
                    bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                    if ((isDirectory ? includeDirectory : includeFile)?.Invoke(entry) == false) continue;
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new SourceSetException(DiagnosticCatalog.UnsafeProfilePath(root, entry,
                            "a contributing path is a symbolic link or reparse point"), PipelineExitCode.InvalidInput);

                    if (isDirectory)
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
