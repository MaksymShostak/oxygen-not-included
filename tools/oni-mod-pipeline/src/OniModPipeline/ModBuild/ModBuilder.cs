using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.Processes;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.SourceControl;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.ModBuild;

internal sealed class ModBuilder(
    IExternalProcessRunner processRunner,
    Utf8ArtifactWriter artifactWriter)
{
    private const string BuildOutputPrefix = "{build-output}/";

    private sealed record PrimaryManagedAssemblyMetadata(
        AssemblyVersionInfo VersionInfo,
        string? TargetFrameworkMoniker);

    internal async Task<OperationResult<BuildResult>> BuildAsync(
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.Environment);
        cancellationToken.ThrowIfCancellationRequested();

        var runRoot = Path.GetFullPath(request.RunRoot);
        var projectIdentity = request.Profile.Build?.EntryPoint ?? request.Profile.ManifestPath;
        if (File.Exists(runRoot) || Directory.Exists(runRoot))
        {
            return BuildFailure(
                projectIdentity,
                $"Run root '{runRoot}' already exists; isolated builds never reuse or delete previous run state.");
        }

        try
        {
            var before = CaptureContributingInputs(request.Profile);
            Directory.CreateDirectory(runRoot);
            if (request.Profile.Build is null)
            {
                var after = CaptureContributingInputs(request.Profile);
                var changedPaths = before.ChangedPathsComparedWith(after);
                if (changedPaths.Count > 0)
                {
                    return SourceChangedFailure(changedPaths);
                }

                var contentOnlyResult = new BuildResult(
                    runRoot,
                    null,
                    before.Files,
                    [],
                    [],
                    [],
                    request.SourceCommit,
                    request.ReleaseVersion,
                    request.Environment.DotnetSdkVersion,
                    [],
                    null,
                    true,
                    null);
                await WriteResultAsync(contentOnlyResult, cancellationToken)
                    .ConfigureAwait(false);
                return Success(contentOnlyResult);
            }

            var build = request.Profile.Build;
            var projectResult = ContainedPathResolver.ResolveExistingFile(
                request.Profile.ModRoot,
                build.EntryPoint);
            if (!projectResult.IsSuccess)
            {
                return BuildFailure(
                    build.EntryPoint,
                    $"Build entry point '{build.EntryPoint}' is missing or unsafe.");
            }

            var projectPath = projectResult.Value!;
            var outputRoot = Path.Combine(runRoot, "output");
            var primaryOutputPath = ResolveRunOutput(
                outputRoot,
                build.PrimaryOutput[BuildOutputPrefix.Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(primaryOutputPath)!);
            var intermediatePath = CreateMsBuildDirectoryPath(
                runRoot,
                "obj",
                "$(MSBuildProjectName)");
            var baseOutputPath = CreateMsBuildDirectoryPath(
                runRoot,
                "bin",
                "$(MSBuildProjectName)");
            var worktreeRoot = FindWorktreeRoot(request.Profile.ModRoot);

            string[] restoreArguments;
            string[] resolveReferencesArguments;
            string[] buildArguments;
            try
            {
                restoreArguments =
                [
                    "restore",
                    projectPath,
                    "--locked-mode",
                    MsBuildPropertyArgument.Create(
                        build.GameManagedDirectoryProperty,
                        request.Environment.OniManagedAssemblyDirectory),
                    MsBuildPropertyArgument.Create(
                        "BaseIntermediateOutputPath",
                        intermediatePath),
                    MsBuildPropertyArgument.Create(
                        "MSBuildProjectExtensionsPath",
                        intermediatePath)
                ];
                resolveReferencesArguments =
                [
                    "msbuild",
                    projectPath,
                    "-nologo",
                    "-target:ResolveReferences",
                    "-getItem:ReferencePath,ReferenceCopyLocalPaths",
                    MsBuildPropertyArgument.Create(
                        "Configuration",
                        request.Configuration),
                    MsBuildPropertyArgument.Create(
                        build.GameManagedDirectoryProperty,
                        request.Environment.OniManagedAssemblyDirectory),
                    MsBuildPropertyArgument.Create(
                        "OniMergedModOutputPath",
                        primaryOutputPath),
                    MsBuildPropertyArgument.Create(
                        "BaseIntermediateOutputPath",
                        intermediatePath),
                    MsBuildPropertyArgument.Create(
                        "MSBuildProjectExtensionsPath",
                        intermediatePath)
                ];
                buildArguments =
                [
                    "build",
                    projectPath,
                    "--no-restore",
                    "--configuration",
                    request.Configuration,
                    MsBuildPropertyArgument.Create(
                        build.GameManagedDirectoryProperty,
                        request.Environment.OniManagedAssemblyDirectory),
                    MsBuildPropertyArgument.Create(
                        "OniMergedModOutputPath",
                        primaryOutputPath),
                    MsBuildPropertyArgument.Create("BaseOutputPath", baseOutputPath),
                    MsBuildPropertyArgument.Create(
                        "BaseIntermediateOutputPath",
                        intermediatePath),
                    MsBuildPropertyArgument.Create(
                        "MSBuildProjectExtensionsPath",
                        intermediatePath),
                    MsBuildPropertyArgument.Create("Version", request.ReleaseVersion),
                    MsBuildPropertyArgument.Create(
                        "InformationalVersion",
                        $"{request.ReleaseVersion}+{ShortCommit(request.SourceCommit)}"),
                    MsBuildPropertyArgument.Create("Deterministic", "true"),
                    MsBuildPropertyArgument.Create(
                        "ContinuousIntegrationBuild",
                        "true"),
                    MsBuildPropertyArgument.Create(
                        "PathMap",
                        CreateDeterministicPathMap(runRoot, worktreeRoot))
                ];
            }
            catch (ArgumentException exception)
            {
                return BuildFailure(projectPath, exception.Message);
            }

            var restore = await RunDotnetAsync(
                request.Profile.ModRoot,
                restoreArguments,
                cancellationToken).ConfigureAwait(false);
            if (restore.ExitCode != 0)
            {
                return new OperationResult<BuildResult>(
                    null,
                    [DiagnosticCatalog.RestoreFailed(
                        projectPath,
                        ProcessEvidence(restore))],
                    PipelineExitCode.BuildOrTestFailed);
            }

            var references = await RunDotnetAsync(
                request.Profile.ModRoot,
                resolveReferencesArguments,
                cancellationToken).ConfigureAwait(false);
            if (references.ExitCode != 0)
            {
                return BuildFailure(
                    projectPath,
                    $"ResolveReferences failed: {ProcessEvidence(references)}");
            }

            ReferenceInventory referenceInventory;
            try
            {
                referenceInventory = ResolveReferenceInventory(
                    references.StandardOutput,
                    request.Environment.OniManagedAssemblyDirectory,
                    build.MergeInputs);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or JsonException or IOException or
                UnauthorizedAccessException or ArgumentException)
            {
                return BuildFailure(
                    projectPath,
                    $"ResolveReferences evidence is invalid: {exception.Message}");
            }

            var buildProcess = await RunDotnetAsync(
                request.Profile.ModRoot,
                buildArguments,
                cancellationToken).ConfigureAwait(false);
            if (buildProcess.ExitCode != 0)
            {
                return BuildFailure(projectPath, ProcessEvidence(buildProcess));
            }

            var afterBuild = CaptureContributingInputs(request.Profile);
            var sourceChanges = before.ChangedPathsComparedWith(afterBuild);
            if (sourceChanges.Count > 0)
            {
                return SourceChangedFailure(sourceChanges);
            }

            var outputPathsResult = ResolveDeclaredOutputPaths(
                request.Profile,
                outputRoot,
                primaryOutputPath);
            if (!outputPathsResult.IsSuccess)
            {
                return ConvertFailure<IReadOnlyList<string>, BuildResult>(outputPathsResult);
            }

            var outputDigests = outputPathsResult.Value!
                .Select(CaptureFile)
                .OrderBy(digest => Path.GetFileName(digest.Path), StringComparer.Ordinal)
                .ThenBy(digest => digest.Path, PathComparer)
                .ToArray();
            var mergeInputDigests = referenceInventory.MergeInputPaths
                .Select(CaptureFile)
                .ToArray();
            var gameReferenceDigests = referenceInventory.GameReferencePaths
                .Select(CaptureFile)
                .ToArray();
            var primaryAssemblyMetadataResult = ReadPrimaryManagedAssemblyMetadata(
                primaryOutputPath,
                request.ReleaseVersion);
            if (!primaryAssemblyMetadataResult.IsSuccess)
            {
                return ConvertFailure<PrimaryManagedAssemblyMetadata?, BuildResult>(
                    primaryAssemblyMetadataResult);
            }

            var primaryAssemblyMetadata = primaryAssemblyMetadataResult.Value;

            var result = new BuildResult(
                runRoot,
                primaryOutputPath,
                before.Files,
                outputDigests,
                mergeInputDigests,
                gameReferenceDigests,
                request.SourceCommit,
                request.ReleaseVersion,
                request.Environment.DotnetSdkVersion,
                buildArguments,
                primaryAssemblyMetadata?.VersionInfo,
                true,
                primaryAssemblyMetadata?.TargetFrameworkMoniker);
            await WriteResultAsync(result, cancellationToken).ConfigureAwait(false);
            return Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException)
        {
            return BuildFailure(projectIdentity, exception.Message);
        }
    }

    private async Task<ProcessResult> RunDotnetAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await processRunner.RunAsync(
            new ProcessRequest(
                "dotnet",
                arguments,
                workingDirectory,
                EmptyEnvironment),
            cancellationToken).ConfigureAwait(false);

    private async Task WriteResultAsync(
        BuildResult result,
        CancellationToken cancellationToken) =>
        await artifactWriter.WriteJsonAtomicallyAsync(
            Path.Combine(result.RunRoot, "build-result.json"),
            result,
            cancellationToken).ConfigureAwait(false);

    private static SourceSnapshot CaptureContributingInputs(ModProfile profile)
    {
        var paths = new HashSet<string>(PathComparer);
        AddExistingFile(paths, profile.ManifestPath);
        AddDeclaredFile(paths, profile.ModRoot, profile.ModYamlPath);
        AddDeclaredFile(paths, profile.ModRoot, profile.ModInfoYamlPath);

        if (profile.Build is { } build)
        {
            AddDeclaredProjectTree(paths, profile.ModRoot, build.EntryPoint);
        }

        foreach (var packageFile in profile.PackageFiles)
        {
            if (!packageFile.Source.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
            {
                AddDeclaredFileOrTree(paths, profile.ModRoot, packageFile.Source);
            }
        }

        AddDeclaredFile(paths, profile.ModRoot, profile.WorkshopListing.Description);
        AddDeclaredFile(paths, profile.ModRoot, profile.WorkshopListing.ChangeNotes);
        AddDeclaredFile(paths, profile.ModRoot, profile.WorkshopListing.Preview);
        foreach (var testProject in profile.TestProjects)
        {
            AddDeclaredProjectTree(paths, profile.ModRoot, testProject.Path);
        }

        return SourceSnapshot.Capture(paths.ToArray());
    }

    private static void AddDeclaredProjectTree(
        ISet<string> paths,
        string modRoot,
        string declaredProjectPath)
    {
        var project = ContainedPathResolver.ResolveExistingFile(
            modRoot,
            declaredProjectPath);
        if (!project.IsSuccess)
        {
            throw new InvalidDataException(
                $"Declared project '{declaredProjectPath}' is missing or unsafe.");
        }

        AddRegularTree(paths, Path.GetDirectoryName(project.Value!)!);
    }

    private static void AddDeclaredFile(
        ISet<string> paths,
        string modRoot,
        string declaredPath)
    {
        var resolved = ContainedPathResolver.ResolveExistingFile(modRoot, declaredPath);
        if (!resolved.IsSuccess)
        {
            throw new InvalidDataException(
                $"Declared input '{declaredPath}' is missing or unsafe.");
        }

        AddExistingFile(paths, resolved.Value!);
    }

    private static void AddDeclaredFileOrTree(
        ISet<string> paths,
        string modRoot,
        string declaredPath)
    {
        var file = ContainedPathResolver.ResolveExistingFile(modRoot, declaredPath);
        if (file.IsSuccess)
        {
            AddExistingFile(paths, file.Value!);
            return;
        }

        var directory = ContainedPathResolver.ResolveExistingDirectory(modRoot, declaredPath);
        if (!directory.IsSuccess)
        {
            throw new InvalidDataException(
                $"Declared input '{declaredPath}' is missing or unsafe.");
        }

        AddRegularTree(paths, directory.Value!);
    }

    private static void AddRegularTree(ISet<string> paths, string root)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory
                .EnumerateFileSystemEntries(directory)
                .OrderBy(path => path, PathComparer))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    var name = Path.GetFileName(entry);
                    if (!string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Push(entry);
                    }
                }
                else
                {
                    AddExistingFile(paths, entry);
                }
            }
        }
    }

    private static void AddExistingFile(ISet<string> paths, string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Contributing input does not exist.", fullPath);
        }

        var attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            new FileInfo(fullPath).LinkTarget is not null)
        {
            throw new InvalidDataException(
                $"Contributing input '{fullPath}' must not be a link or reparse point.");
        }

        paths.Add(fullPath);
    }

    private static ReferenceInventory ResolveReferenceInventory(
        string json,
        string managedAssemblyDirectory,
        IReadOnlyList<string> declaredMergeInputs)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Items", out var items) ||
            items.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "MSBuild -getItem output does not contain an Items object.");
        }

        var referencePaths = ReadItemPaths(items, "ReferencePath");
        var copyLocalPaths = ReadItemPaths(items, "ReferenceCopyLocalPaths");
        var managedRoot = Path.GetFullPath(managedAssemblyDirectory);
        var gameReferences = referencePaths
            .Where(path => IsStrictDescendant(managedRoot, path))
            .Distinct(PathComparer)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ThenBy(path => path, PathComparer)
            .ToArray();

        var declared = declaredMergeInputs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copyLocalBySimpleName = copyLocalPaths
            .Distinct(PathComparer)
            .GroupBy(
                path => Path.GetFileNameWithoutExtension(path),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var undeclared = copyLocalBySimpleName.Keys
            .Where(name => !declared.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (undeclared.Length > 0)
        {
            throw new InvalidDataException(
                $"ReferenceCopyLocalPaths contains undeclared merge inputs: {string.Join(", ", undeclared)}.");
        }

        var mergeInputs = new List<string>(declaredMergeInputs.Count);
        foreach (var declaredName in declaredMergeInputs)
        {
            if (!copyLocalBySimpleName.TryGetValue(declaredName, out var matches) ||
                matches.Length != 1)
            {
                var matchCount = matches?.Length ?? 0;
                throw new InvalidDataException(
                    $"Declared merge input '{declaredName}' resolved to {matchCount} ReferenceCopyLocalPaths items; exactly one is required.");
            }

            mergeInputs.Add(matches[0]);
        }

        return new ReferenceInventory(
            gameReferences,
            mergeInputs
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ThenBy(path => path, PathComparer)
                .ToArray());
    }

    private static IReadOnlyList<string> ReadItemPaths(
        JsonElement items,
        string itemName)
    {
        if (!items.TryGetProperty(itemName, out var itemArray))
        {
            return [];
        }

        if (itemArray.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"MSBuild item '{itemName}' must be a JSON array.");
        }

        var paths = new List<string>();
        foreach (var item in itemArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"MSBuild item '{itemName}' contains a non-object entry.");
            }

            var path = TryReadNonemptyString(item, "FullPath") ??
                TryReadNonemptyString(item, "Identity");
            if (path is null || !Path.IsPathFullyQualified(path))
            {
                throw new InvalidDataException(
                    $"MSBuild item '{itemName}' does not expose an absolute FullPath or Identity.");
            }

            paths.Add(Path.GetFullPath(path));
        }

        return paths;
    }

    private static string? TryReadNonemptyString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static OperationResult<IReadOnlyList<string>> ResolveDeclaredOutputPaths(
        ModProfile profile,
        string outputRoot,
        string primaryOutputPath)
    {
        var outputPaths = new HashSet<string>(PathComparer)
        {
            Path.GetFullPath(primaryOutputPath)
        };
        foreach (var mapping in profile.PackageFiles)
        {
            if (!mapping.Source.StartsWith(BuildOutputPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var outputPath = ResolveRunOutput(
                outputRoot,
                mapping.Source[BuildOutputPrefix.Length..]);
            if (File.Exists(outputPath))
            {
                outputPaths.Add(outputPath);
                continue;
            }

            if (Directory.Exists(outputPath))
            {
                foreach (var file in EnumerateRegularOutputFiles(outputPath))
                {
                    outputPaths.Add(file);
                }

                continue;
            }

            return new OperationResult<IReadOnlyList<string>>(
                null,
                [DiagnosticCatalog.BuildOutputMissing(outputPath)],
                PipelineExitCode.BuildOrTestFailed);
        }

        foreach (var outputPath in outputPaths)
        {
            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                return new OperationResult<IReadOnlyList<string>>(
                    null,
                    [DiagnosticCatalog.BuildOutputMissing(outputPath)],
                    PipelineExitCode.BuildOrTestFailed);
            }
        }

        return new OperationResult<IReadOnlyList<string>>(
            outputPaths.OrderBy(path => path, PathComparer).ToArray(),
            [],
            PipelineExitCode.Success);
    }

    private static IEnumerable<string> EnumerateRegularOutputFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory
                .EnumerateFileSystemEntries(directory)
                .OrderBy(path => path, PathComparer))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        $"Build output '{entry}' must not be a link or reparse point.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    yield return Path.GetFullPath(entry);
                }
            }
        }
    }

    private static OperationResult<PrimaryManagedAssemblyMetadata?> ReadPrimaryManagedAssemblyMetadata(
        string primaryOutputPath,
        string releaseVersion)
    {
        PrimaryManagedAssemblyMetadata? assemblyMetadata;
        try
        {
            // The compiled artifact is authoritative. Command-line MSBuild
            // properties can change the effective target independently of the
            // project file, so provenance must come from the exact packaged bytes.
            using var stream = new FileStream(
                primaryOutputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return Success<PrimaryManagedAssemblyMetadata?>(null);
            }

            var metadata = peReader.GetMetadataReader();
            if (!metadata.IsAssembly)
            {
                return Success<PrimaryManagedAssemblyMetadata?>(null);
            }

            var assembly = metadata.GetAssemblyDefinition();
            string? fileVersion = null;
            string? informationalVersion = null;
            string? targetFrameworkMoniker = null;
            foreach (var attributeHandle in assembly.GetCustomAttributes())
            {
                var attribute = metadata.GetCustomAttribute(attributeHandle);
                var attributeName = GetAttributeTypeName(metadata, attribute.Constructor);
                if (attributeName == "System.Reflection.AssemblyFileVersionAttribute")
                {
                    fileVersion = ReadFixedStringAttribute(metadata, attribute);
                }
                else if (attributeName ==
                    "System.Reflection.AssemblyInformationalVersionAttribute")
                {
                    informationalVersion = ReadFixedStringAttribute(metadata, attribute);
                }
                else if (attributeName ==
                    "System.Runtime.Versioning.TargetFrameworkAttribute")
                {
                    targetFrameworkMoniker = ReadFixedStringAttribute(
                        metadata,
                        attribute);
                }
            }

            assemblyMetadata = new PrimaryManagedAssemblyMetadata(
                new AssemblyVersionInfo(
                    assembly.Version.ToString(),
                    fileVersion,
                    informationalVersion),
                targetFrameworkMoniker);
        }
        catch (BadImageFormatException)
        {
            return Success<PrimaryManagedAssemblyMetadata?>(null);
        }

        var informationMatches =
            string.Equals(
                assemblyMetadata.VersionInfo.InformationalVersion,
                releaseVersion,
                StringComparison.Ordinal) ||
            assemblyMetadata.VersionInfo.InformationalVersion?.StartsWith(
                $"{releaseVersion}+",
                StringComparison.Ordinal) == true;
        return informationMatches
            ? Success<PrimaryManagedAssemblyMetadata?>(assemblyMetadata)
            : new OperationResult<PrimaryManagedAssemblyMetadata?>(
                null,
                [DiagnosticCatalog.BuildFailed(
                    primaryOutputPath,
                    $"Managed primary output informational version '{assemblyMetadata.VersionInfo.InformationalVersion ?? "<missing>"}' does not begin with validated release version '{releaseVersion}'.")],
                PipelineExitCode.BuildOrTestFailed);
    }

    private static string GetAttributeTypeName(
        MetadataReader metadata,
        EntityHandle constructor)
    {
        EntityHandle typeHandle = constructor.Kind switch
        {
            HandleKind.MemberReference => metadata
                .GetMemberReference((MemberReferenceHandle)constructor)
                .Parent,
            HandleKind.MethodDefinition => metadata
                .GetMethodDefinition((MethodDefinitionHandle)constructor)
                .GetDeclaringType(),
            _ => default
        };

        return typeHandle.Kind switch
        {
            HandleKind.TypeReference => FullTypeName(
                metadata,
                metadata.GetTypeReference((TypeReferenceHandle)typeHandle)),
            HandleKind.TypeDefinition => FullTypeName(
                metadata,
                metadata.GetTypeDefinition((TypeDefinitionHandle)typeHandle)),
            _ => string.Empty
        };
    }

    private static string FullTypeName(MetadataReader metadata, TypeReference type)
    {
        var typeNamespace = metadata.GetString(type.Namespace);
        var name = metadata.GetString(type.Name);
        return string.IsNullOrEmpty(typeNamespace) ? name : $"{typeNamespace}.{name}";
    }

    private static string FullTypeName(MetadataReader metadata, TypeDefinition type)
    {
        var typeNamespace = metadata.GetString(type.Namespace);
        var name = metadata.GetString(type.Name);
        return string.IsNullOrEmpty(typeNamespace) ? name : $"{typeNamespace}.{name}";
    }

    private static string? ReadFixedStringAttribute(
        MetadataReader metadata,
        CustomAttribute attribute)
    {
        var reader = metadata.GetBlobReader(attribute.Value);
        if (reader.ReadUInt16() != 1)
        {
            throw new BadImageFormatException("Custom attribute has an invalid prolog.");
        }

        return reader.ReadSerializedString();
    }

    private static string ResolveRunOutput(string outputRoot, string relativePath)
    {
        var platformPath = relativePath
            .Replace((char)92, Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(outputRoot);
        var output = Path.GetFullPath(Path.Combine(root, platformPath));
        if (!IsStrictDescendant(root, output))
        {
            throw new InvalidDataException(
                $"Declared build output '{relativePath}' escapes output root '{root}'.");
        }

        return output;
    }

    private static string CreateMsBuildDirectoryPath(
        string root,
        params string[] segments)
    {
        var path = segments.Aggregate(
            Path.GetFullPath(root),
            Path.Combine);
        return path.Replace((char)92, '/') + "/";
    }

    private static string CreateDeterministicPathMap(
        string runRoot,
        string worktreeRoot) =>
        $"{EscapePathMapComponent(Path.GetFullPath(runRoot))}=/_build/," +
        $"{EscapePathMapComponent(Path.GetFullPath(worktreeRoot))}=/_/";

    private static string EscapePathMapComponent(string value) =>
        value
            .Replace("=", "==", StringComparison.Ordinal)
            .Replace(",", ",,", StringComparison.Ordinal);

    private static bool IsStrictDescendant(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return relative != "." &&
            !Path.IsPathRooted(relative) &&
            relative != ".." &&
            !relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) &&
            !relative.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal);
    }

    private static string FindWorktreeRoot(string startPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startPath);
    }

    private static string ShortCommit(string sourceCommit) =>
        sourceCommit[..Math.Min(12, sourceCommit.Length)];

    private static FileDigest CaptureFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        var byteLength = stream.Length;
        var hash = SHA256.HashData(stream);
        return new FileDigest(
            fullPath,
            byteLength,
            Convert.ToHexStringLower(hash));
    }

    private static string ProcessEvidence(ProcessResult result)
    {
        var standardError = string.IsNullOrWhiteSpace(result.StandardError)
            ? "<empty>"
            : result.StandardError.Trim();
        var standardOutput = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? "<empty>"
            : result.StandardOutput.Trim();
        return $"Process exited {result.ExitCode}. Standard error: {standardError}. Standard output: {standardOutput}.";
    }

    private static OperationResult<BuildResult> BuildFailure(
        string projectPath,
        string evidence) =>
        new(
            null,
            [DiagnosticCatalog.BuildFailed(projectPath, evidence)],
            PipelineExitCode.BuildOrTestFailed);

    private static OperationResult<BuildResult> SourceChangedFailure(
        IReadOnlyList<string> changedPaths) =>
        new(
            null,
            [DiagnosticCatalog.SourceChangedDuringBuild(changedPaths)],
            PipelineExitCode.BuildOrTestFailed);

    private static OperationResult<T> Success<T>(T value) =>
        new(value, [], PipelineExitCode.Success);

    private static OperationResult<TOutput> ConvertFailure<TInput, TOutput>(
        OperationResult<TInput> result) =>
        new(default, result.Diagnostics, result.ExitCode);

    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironment =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record ReferenceInventory(
        IReadOnlyList<string> GameReferencePaths,
        IReadOnlyList<string> MergeInputPaths);
}
