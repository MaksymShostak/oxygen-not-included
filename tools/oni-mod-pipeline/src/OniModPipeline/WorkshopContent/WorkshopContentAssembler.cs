using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModProfiles;

namespace MaksymShostak.OniModPipeline.WorkshopContent;

internal sealed class WorkshopContentAssembler
{
    private const string BuildOutputPrefix = "{build-output}/";

    private static readonly StringComparer HostPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly WorkshopContentValidator validator;
    private readonly ContentHasher contentHasher;
    private readonly Func<string, FileAttributes> readAttributes;
    private readonly Func<string, string?> readLinkTarget;

    internal WorkshopContentAssembler()
        : this(
            new WorkshopContentValidator(),
            new ContentHasher(),
            File.GetAttributes,
            ReadLinkTarget)
    {
    }

    internal WorkshopContentAssembler(
        WorkshopContentValidator validator,
        ContentHasher contentHasher,
        Func<string, FileAttributes> readAttributes,
        Func<string, string?> readLinkTarget)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(contentHasher);
        ArgumentNullException.ThrowIfNull(readAttributes);
        ArgumentNullException.ThrowIfNull(readLinkTarget);
        this.validator = validator;
        this.contentHasher = contentHasher;
        this.readAttributes = readAttributes;
        this.readLinkTarget = readLinkTarget;
    }

    internal async Task<OperationResult<IReadOnlyList<FileDigest>>> AssembleAsync(
        ModProfile profile,
        BuildResult buildResult,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(buildResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedStagingRoot = Path.GetFullPath(stagingRoot);
        var targetDiagnostic = ValidateEmptyTarget(resolvedStagingRoot);
        if (targetDiagnostic is not null)
        {
            return Failure(targetDiagnostic);
        }

        OperationResult<IReadOnlyList<PlannedContentFile>> planResult;
        try
        {
            planResult = CreatePlan(profile, buildResult);
        }
        catch (InvalidDataException exception)
        {
            return Failure(DiagnosticCatalog.CandidateManifestMismatch(exception.Message));
        }
        if (!planResult.IsSuccess)
        {
            return ConvertFailure<IReadOnlyList<PlannedContentFile>, IReadOnlyList<FileDigest>>(
                planResult);
        }

        var plan = planResult.Value!;
        var primaryFiles = plan.Where(file => file.IsPrimaryOutput).ToArray();
        if (primaryFiles.Length != 1)
        {
            return Failure(DiagnosticCatalog.CandidateManifestMismatch(
                "Exactly one declared package mapping must select the primary build output."));
        }

        var inventoryResult = validator.ValidateInventory(
            plan.Select(file => file.Destination).ToArray(),
            primaryFiles[0].Destination);
        if (!inventoryResult.IsSuccess)
        {
            return ConvertFailure<IReadOnlyList<string>, IReadOnlyList<FileDigest>>(
                inventoryResult);
        }

        foreach (var file in plan)
        {
            var sourceDigest = await contentHasher
                .HashFileAsync(file.SourcePath, cancellationToken)
                .ConfigureAwait(false);
            if (file.IsPrimaryOutput && sourceDigest.ByteLength == 0)
            {
                return Failure(DiagnosticCatalog.CandidateManifestMismatch(
                    $"Primary assembly '{file.SourcePath}' must not be empty."));
            }

            if (file.ExpectedBuildDigest is not null &&
                !DigestMatches(sourceDigest, file.ExpectedBuildDigest))
            {
                return Failure(DiagnosticCatalog.CandidateManifestMismatch(
                    $"Build-sourced file '{file.SourcePath}' does not match its recorded BuildResult hash and size."));
            }
        }

        var createdFiles = new List<string>(plan.Count);
        var createdDirectories = new List<string>();
        try
        {
            foreach (var file in plan.OrderBy(file => file.Destination, StringComparer.Ordinal))
            {
                var destinationPath = ResolveDestination(
                    resolvedStagingRoot,
                    file.Destination);
                CreateParentDirectories(
                    resolvedStagingRoot,
                    destinationPath,
                    createdDirectories);
                await CopyNewAsync(
                    file.SourcePath,
                    destinationPath,
                    cancellationToken).ConfigureAwait(false);
                createdFiles.Add(destinationPath);
            }

            var actualPathsResult = EnumerateStagedPaths(resolvedStagingRoot);
            if (!actualPathsResult.IsSuccess)
            {
                Cleanup(createdFiles, createdDirectories);
                return ConvertFailure<IReadOnlyList<string>, IReadOnlyList<FileDigest>>(
                    actualPathsResult);
            }

            if (!new HashSet<string>(
                    inventoryResult.Value!,
                    StringComparer.Ordinal).SetEquals(actualPathsResult.Value!))
            {
                Cleanup(createdFiles, createdDirectories);
                return Failure(DiagnosticCatalog.CandidateManifestMismatch(
                    "The completed Workshop content tree does not match the expanded declared mapping set."));
            }

            var digests = new List<FileDigest>(plan.Count);
            foreach (var file in plan.OrderBy(file => file.Destination, StringComparer.Ordinal))
            {
                var destinationPath = ResolveDestination(
                    resolvedStagingRoot,
                    file.Destination);
                var digest = await contentHasher
                    .HashFileAsync(destinationPath, cancellationToken)
                    .ConfigureAwait(false);
                if (file.ExpectedBuildDigest is not null &&
                    !DigestMatches(digest, file.ExpectedBuildDigest))
                {
                    Cleanup(createdFiles, createdDirectories);
                    return Failure(DiagnosticCatalog.CandidateManifestMismatch(
                        $"Staged build output '{file.Destination}' does not match its recorded BuildResult hash and size."));
                }

                digests.Add(digest);
            }

            return new OperationResult<IReadOnlyList<FileDigest>>(
                digests,
                [],
                PipelineExitCode.Success);
        }
        catch
        {
            Cleanup(createdFiles, createdDirectories);
            throw;
        }
    }

    private OperationResult<IReadOnlyList<PlannedContentFile>> CreatePlan(
        ModProfile profile,
        BuildResult buildResult)
    {
        var plan = new List<PlannedContentFile>();
        foreach (var mapping in profile.PackageFiles)
        {
            var portableSource = mapping.Source.Replace('\\', '/');
            if (portableSource.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
            {
                var buildPlanResult = PlanBuildOutput(
                    mapping,
                    portableSource[BuildOutputPrefix.Length..],
                    buildResult);
                if (!buildPlanResult.IsSuccess)
                {
                    return ConvertFailure<PlannedContentFile, IReadOnlyList<PlannedContentFile>>(
                        buildPlanResult);
                }

                plan.Add(buildPlanResult.Value!);
                continue;
            }

            var fileResult = ContainedPathResolver.ResolveExistingFile(
                profile.ModRoot,
                mapping.Source,
                readAttributes,
                readLinkTarget);
            if (fileResult.IsSuccess)
            {
                plan.Add(new PlannedContentFile(
                    fileResult.Value!,
                    WorkshopContentValidator.NormalizeRelativePath(mapping.Destination),
                    null,
                    false));
                continue;
            }

            var directoryResult = ContainedPathResolver.ResolveExistingDirectory(
                profile.ModRoot,
                mapping.Source,
                readAttributes,
                readLinkTarget);
            if (!directoryResult.IsSuccess)
            {
                return new OperationResult<IReadOnlyList<PlannedContentFile>>(
                    null,
                    fileResult.Diagnostics,
                    fileResult.ExitCode);
            }

            var destinationRoot = WorkshopContentValidator.NormalizeRelativePath(
                mapping.Destination);
            var directoryPlanResult = PlanDirectory(
                directoryResult.Value!,
                destinationRoot);
            if (!directoryPlanResult.IsSuccess)
            {
                return directoryPlanResult;
            }

            plan.AddRange(directoryPlanResult.Value!);
        }

        return new OperationResult<IReadOnlyList<PlannedContentFile>>(
            plan,
            [],
            PipelineExitCode.Success);
    }

    private OperationResult<PlannedContentFile> PlanBuildOutput(
        PackageFileMapping mapping,
        string relativeSource,
        BuildResult buildResult)
    {
        if (string.IsNullOrWhiteSpace(relativeSource))
        {
            return Failure<PlannedContentFile>(
                "A {build-output} package source must name one output file.");
        }

        var outputRoot = Path.Combine(Path.GetFullPath(buildResult.RunRoot), "output");
        var sourceResult = ContainedPathResolver.ResolveExistingFile(
            outputRoot,
            relativeSource,
            readAttributes,
            readLinkTarget);
        if (!sourceResult.IsSuccess)
        {
            return new OperationResult<PlannedContentFile>(
                null,
                sourceResult.Diagnostics,
                sourceResult.ExitCode);
        }

        var sourcePath = sourceResult.Value!;
        var matchingOutputs = buildResult.Outputs
            .Where(output =>
                HostPathComparer.Equals(Path.GetFullPath(output.Path), sourcePath))
            .Take(2)
            .ToArray();
        if (matchingOutputs.Length == 0)
        {
            return Failure<PlannedContentFile>(
                $"Build-output package source '{mapping.Source}' is absent from BuildResult.Outputs.");
        }

        if (matchingOutputs.Length > 1)
        {
            return Failure<PlannedContentFile>(
                $"Build-output package source '{mapping.Source}' has ambiguous duplicate BuildResult.Outputs evidence.");
        }

        var expected = matchingOutputs[0];

        var isPrimary = buildResult.PrimaryOutputPath is not null &&
            HostPathComparer.Equals(
                Path.GetFullPath(buildResult.PrimaryOutputPath),
                sourcePath);
        return new OperationResult<PlannedContentFile>(
            new PlannedContentFile(
                sourcePath,
                WorkshopContentValidator.NormalizeRelativePath(mapping.Destination),
                expected,
                isPrimary),
            [],
            PipelineExitCode.Success);
    }

    private OperationResult<IReadOnlyList<PlannedContentFile>> PlanDirectory(
        string sourceRoot,
        string destinationRoot)
    {
        var files = new List<PlannedContentFile>();
        var pending = new Stack<string>();
        pending.Push(sourceRoot);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory
                .EnumerateFileSystemEntries(directory)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                var attributes = readAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    readLinkTarget(entry) is not null)
                {
                    return Failure<IReadOnlyList<PlannedContentFile>>(
                        $"Declared source directory contains linked entry '{entry}'.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourceRoot, entry).Replace('\\', '/');
                files.Add(new PlannedContentFile(
                    Path.GetFullPath(entry),
                    WorkshopContentValidator.NormalizeRelativePath(
                        $"{destinationRoot}/{relativePath}"),
                    null,
                    false));
            }
        }

        if (files.Count == 0)
        {
            return Failure<IReadOnlyList<PlannedContentFile>>(
                $"Declared source directory '{sourceRoot}' contains no regular files.");
        }

        return new OperationResult<IReadOnlyList<PlannedContentFile>>(
            files.OrderBy(file => file.Destination, StringComparer.Ordinal).ToArray(),
            [],
            PipelineExitCode.Success);
    }

    private OperationResult<IReadOnlyList<string>> EnumerateStagedPaths(string root)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = readAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    readLinkTarget(entry) is not null)
                {
                    return Failure<IReadOnlyList<string>>(
                        $"Completed Workshop content contains linked entry '{entry}'.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    files.Add(WorkshopContentValidator.NormalizeRelativePath(
                        Path.GetRelativePath(root, entry)));
                }
            }
        }

        return new OperationResult<IReadOnlyList<string>>(
            files.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            [],
            PipelineExitCode.Success);
    }

    private Diagnostic? ValidateEmptyTarget(string target)
    {
        if (!Directory.Exists(target))
        {
            return DiagnosticCatalog.CandidateManifestMismatch(
                $"Workshop content staging root '{target}' must already exist and be empty.");
        }

        var info = new DirectoryInfo(target);
        var attributes = readAttributes(target);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            readLinkTarget(target) is not null)
        {
            return DiagnosticCatalog.CandidateManifestMismatch(
                $"Workshop content staging root '{target}' must not be a link or reparse point.");
        }

        if (!string.Equals(info.Name, "workshop-content", StringComparison.Ordinal))
        {
            return DiagnosticCatalog.CandidateManifestMismatch(
                "Workshop content staging directory must be named exactly 'workshop-content'.");
        }

        if (Directory.EnumerateFileSystemEntries(target).Any())
        {
            return DiagnosticCatalog.CandidateManifestMismatch(
                $"Workshop content staging root '{target}' must be empty.");
        }

        return null;
    }

    private static string ResolveDestination(string root, string relativePath)
    {
        var destination = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, destination);
        if (relative == "." ||
            Path.IsPathRooted(relative) ||
            relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing Workshop content destination outside '{root}': '{destination}'.");
        }

        return destination;
    }

    private static void CreateParentDirectories(
        string root,
        string destinationPath,
        ICollection<string> createdDirectories)
    {
        var relativeDirectory = Path.GetRelativePath(
            root,
            Path.GetDirectoryName(destinationPath)!);
        if (relativeDirectory == ".")
        {
            return;
        }

        var current = root;
        foreach (var segment in relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                Directory.CreateDirectory(current);
                createdDirectories.Add(current);
            }
        }
    }

    private static async Task CopyNewAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var created = false;
        try
        {
            await using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            created = true;
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (created && File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }

    private static void Cleanup(
        IEnumerable<string> createdFiles,
        IEnumerable<string> createdDirectories)
    {
        foreach (var file in createdFiles.Reverse())
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        foreach (var directory in createdDirectories.Reverse())
        {
            if (Directory.Exists(directory) &&
                !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

    private static bool DigestMatches(FileDigest actual, FileDigest expected) =>
        actual.ByteLength == expected.ByteLength &&
        string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal);

    private static string? ReadLinkTarget(string path)
    {
        FileSystemInfo entry = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        return entry.LinkTarget;
    }

    private static OperationResult<T> Failure<T>(string reason) =>
        new(
            default,
            [DiagnosticCatalog.CandidateManifestMismatch(reason)],
            PipelineExitCode.ReleaseNotReady);

    private static OperationResult<IReadOnlyList<FileDigest>> Failure(
        Diagnostic diagnostic) =>
        new(null, [diagnostic], PipelineExitCode.ReleaseNotReady);

    private static OperationResult<TOutput> ConvertFailure<TInput, TOutput>(
        OperationResult<TInput> result) =>
        new(default, result.Diagnostics, result.ExitCode);

    private sealed record PlannedContentFile(
        string SourcePath,
        string Destination,
        FileDigest? ExpectedBuildDigest,
        bool IsPrimaryOutput);
}
