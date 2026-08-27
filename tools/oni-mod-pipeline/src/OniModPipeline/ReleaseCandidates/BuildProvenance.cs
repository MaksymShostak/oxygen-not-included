using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.WorkshopListing;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal sealed record ProvenanceFileDigest(
    string Path,
    long ByteLength,
    string Sha256);

internal sealed record BuildInvocation(
    string Executable,
    IReadOnlyList<string> Arguments);

internal sealed record WorkshopListingProvenance(
    ListingTextReport Description,
    ListingTextReport ChangeNotes,
    PreviewImageInspection Preview,
    IReadOnlyList<string> ModTypeLabels,
    IReadOnlyList<string> DlcLabels);

internal sealed record BuildProvenance(
    int SchemaVersion,
    int ProfileSchemaVersion,
    string PipelineInformationalVersion,
    string PipelineExecutableSha256,
    string StaticId,
    string Title,
    string Version,
    string RepositoryCommit,
    bool RelevantPathsClean,
    IReadOnlyList<string> RelevantSourcePaths,
    DateTimeOffset PreparedAtUtc,
    string OperatingSystem,
    string Architecture,
    string DotnetSdkVersion,
    string TargetFramework,
    string Configuration,
    string WorktreeRoot,
    string GameDirectory,
    string OniManagedAssemblyDirectory,
    string ArtifactsDirectory,
    string? GameBuildMetadata,
    IReadOnlyList<ProvenanceFileDigest> LockFiles,
    string LockedDependencyClosureSha256,
    IReadOnlyList<ProvenanceFileDigest> GameReferences,
    BuildInvocation BuildInvocation,
    IReadOnlyList<ProvenanceFileDigest> BuildInputs,
    IReadOnlyList<ProvenanceFileDigest> MergeInputs,
    IReadOnlyList<ProvenanceFileDigest> BuildOutputs,
    ProvenanceFileDigest? PrimaryOutput,
    AssemblyVersionInfo? PrimaryAssemblyVersion,
    bool SourceBytesUnchanged,
    WorkshopListingProvenance WorkshopListing,
    int AcceptanceCheckCount,
    string AcceptanceTestPlanSha256,
    string ReleaseContentDigest);

internal sealed class EvidencePathMapper
{
    private readonly (string Root, string Token)[] roots;
    private readonly StringComparison comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal EvidencePathMapper(
        string worktreeRoot,
        string gameDirectory,
        string artifactsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsDirectory);

        roots =
        [
            (Path.GetFullPath(artifactsDirectory), "${ARTIFACTS}"),
            (Path.GetFullPath(gameDirectory), "${GAME}"),
            (Path.GetFullPath(worktreeRoot), "${WORKTREE}")
        ];
        roots = roots
            .OrderByDescending(root => root.Root.Length)
            .ToArray();
    }

    internal string MapPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = Path.GetFullPath(path);
        foreach (var (root, token) in roots)
        {
            if (string.Equals(resolved, root, comparison))
            {
                return token;
            }

            var relative = Path.GetRelativePath(root, resolved);
            if (IsStrictDescendant(relative))
            {
                return $"{token}/{relative.Replace((char)92, '/')}";
            }
        }

        return $"${{EXTERNAL}}/{Path.GetFileName(resolved)}";
    }

    internal string MapArgument(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        var mapped = argument;
        foreach (var (root, token) in roots)
        {
            mapped = ReplaceRoot(mapped, root, token);
            var alternateRoot = root.Replace((char)92, '/');
            if (!string.Equals(alternateRoot, root, StringComparison.Ordinal))
            {
                mapped = ReplaceRoot(mapped, alternateRoot, token);
            }
        }

        return mapped.Replace((char)92, '/');
    }

    internal ProvenanceFileDigest MapDigest(
        ContentIntegrity.FileDigest digest) =>
        new(MapPath(digest.Path), digest.ByteLength, digest.Sha256);

    private string ReplaceRoot(string value, string root, string token) =>
        value.Replace(root, token, comparison);

    private static bool IsStrictDescendant(string relativePath) =>
        relativePath != "." &&
        !Path.IsPathRooted(relativePath) &&
        relativePath != ".." &&
        !relativePath.StartsWith(
            $"..{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal) &&
        !relativePath.StartsWith(
            $"..{Path.AltDirectorySeparatorChar}",
            StringComparison.Ordinal);
}
