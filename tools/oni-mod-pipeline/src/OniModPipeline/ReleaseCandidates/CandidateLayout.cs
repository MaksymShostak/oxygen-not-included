using System.Text.RegularExpressions;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal sealed record CandidateLayout
{
    private static readonly char[] PortableInvalidFileNameCharacters =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> WindowsReservedDeviceNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
            "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly Regex RunIdPattern = new(
        "^[0-9]{8}T[0-9]{6}\\.[0-9]{7}Z-[0-9a-f]{16}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex GuidSuffixPattern = new(
        "^[0-9a-f]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private CandidateLayout(
        string artifactsDirectory,
        string staticId,
        string version,
        string runId)
    {
        ArtifactsDirectory = artifactsDirectory;
        StaticId = staticId;
        Version = version;
        RunId = runId;
        ReleaseCandidatesDirectory = Path.Combine(
            artifactsDirectory,
            "release-candidates");
        StaticIdDirectory = Path.Combine(ReleaseCandidatesDirectory, staticId);
        VersionDirectory = Path.Combine(StaticIdDirectory, version);
        CandidateDirectory = Path.Combine(VersionDirectory, runId);
        WorkshopContentDirectory = Path.Combine(CandidateDirectory, "workshop-content");
        WorkshopListingDirectory = Path.Combine(CandidateDirectory, "workshop-listing");
        DescriptionPath = Path.Combine(WorkshopListingDirectory, "description.bbcode");
        ChangeNotesPath = Path.Combine(WorkshopListingDirectory, "change-notes.bbcode");
        ReleaseEvidenceDirectory = Path.Combine(CandidateDirectory, "release-evidence");
        ReleaseReadinessReportPath = Path.Combine(
            ReleaseEvidenceDirectory,
            "release-readiness-report.json");
        ReleaseContentManifestPath = Path.Combine(
            ReleaseEvidenceDirectory,
            "release-content-manifest.json");
        BuildProvenancePath = Path.Combine(
            ReleaseEvidenceDirectory,
            "build-provenance.json");
        AutomatedTestResultsDirectory = Path.Combine(
            ReleaseEvidenceDirectory,
            "automated-test-results");
        AcceptanceTestPlanPath = Path.Combine(
            ReleaseEvidenceDirectory,
            "acceptance-test-plan.json");
        ReleaseSummaryPath = Path.Combine(ReleaseEvidenceDirectory, "release-summary.md");
        UploaderChecklistPath = Path.Combine(
            ReleaseEvidenceDirectory,
            "uploader-checklist.md");
        InstallationReceiptPath = Path.Combine(
            ReleaseEvidenceDirectory,
            "installation-receipt.json");
        AcceptanceTestResultsPath = Path.Combine(
            ReleaseEvidenceDirectory,
            "acceptance-test-results.json");
    }

    public string ArtifactsDirectory { get; }

    public string StaticId { get; }

    public string Version { get; }

    public string RunId { get; }

    public string ReleaseCandidatesDirectory { get; }

    public string StaticIdDirectory { get; }

    public string VersionDirectory { get; }

    public string CandidateDirectory { get; }

    public string WorkshopContentDirectory { get; }

    public string WorkshopListingDirectory { get; }

    public string DescriptionPath { get; }

    public string ChangeNotesPath { get; }

    public string ReleaseEvidenceDirectory { get; }

    public string ReleaseReadinessReportPath { get; }

    public string ReleaseContentManifestPath { get; }

    public string BuildProvenancePath { get; }

    public string AutomatedTestResultsDirectory { get; }

    public string AcceptanceTestPlanPath { get; }

    public string ReleaseSummaryPath { get; }

    public string UploaderChecklistPath { get; }

    public string InstallationReceiptPath { get; }

    public string AcceptanceTestResultsPath { get; }

    internal static CandidateLayout Create(
        string artifactsDirectory,
        string staticId,
        string version,
        string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsDirectory);
        ValidateSegment(staticId, nameof(staticId));
        ValidateSegment(version, nameof(version));
        if (string.IsNullOrWhiteSpace(runId) || !RunIdPattern.IsMatch(runId))
        {
            throw new ArgumentException(
                "A release run ID must use the canonical UTC timestamp and 16-hex suffix format.",
                nameof(runId));
        }

        var root = Path.GetFullPath(artifactsDirectory);
        var layout = new CandidateLayout(root, staticId, version, runId);
        EnsureStrictDescendant(root, layout.CandidateDirectory);
        return layout;
    }

    internal static CandidateLayout FromCandidateDirectory(string candidateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateDirectory);
        var candidate = new DirectoryInfo(Path.GetFullPath(candidateDirectory));
        var version = candidate.Parent;
        var staticId = version?.Parent;
        var releaseCandidates = staticId?.Parent;
        var artifacts = releaseCandidates?.Parent;
        if (version is null ||
            staticId is null ||
            releaseCandidates is null ||
            artifacts is null ||
            !string.Equals(
                releaseCandidates.Name,
                "release-candidates",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A candidate must use the exact artifacts/release-candidates/<static-id>/<version>/<run-id> hierarchy.",
                nameof(candidateDirectory));
        }

        var layout = Create(
            artifacts.FullName,
            staticId.Name,
            version.Name,
            candidate.Name);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(layout.CandidateDirectory, candidate.FullName, comparison))
        {
            throw new ArgumentException(
                "The candidate directory does not resolve to its canonical layout path.",
                nameof(candidateDirectory));
        }

        return layout;
    }

    internal string CreateTransientSiblingPath(string kind, Guid suffix)
    {
        if (kind is not ("staging" or "work"))
        {
            throw new ArgumentException(
                "A candidate transient sibling kind must be 'staging' or 'work'.",
                nameof(kind));
        }

        var path = Path.GetFullPath(Path.Combine(
            VersionDirectory,
            $".{RunId}.{kind}-{suffix:N}"));
        EnsureStrictDescendant(VersionDirectory, path);
        return path;
    }

    internal bool IsOwnedTransientSibling(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var resolved = Path.GetFullPath(path);
        if (!string.Equals(
            Path.GetDirectoryName(resolved),
            VersionDirectory,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            return false;
        }

        var name = Path.GetFileName(resolved);
        foreach (var kind in new[] { "staging", "work" })
        {
            var prefix = $".{RunId}.{kind}-";
            if (name.StartsWith(prefix, StringComparison.Ordinal) &&
                GuidSuffixPattern.IsMatch(name[prefix.Length..]))
            {
                return true;
            }
        }

        return false;
    }

    internal string GetStagingPath(string stagingDirectory, string finalPath)
    {
        if (!IsOwnedTransientSibling(stagingDirectory) ||
            !IsStrictDescendant(CandidateDirectory, finalPath))
        {
            throw new InvalidOperationException(
                "Candidate staging path translation requires owned final and staging paths.");
        }

        var relativePath = Path.GetRelativePath(CandidateDirectory, finalPath);
        var translated = Path.GetFullPath(Path.Combine(stagingDirectory, relativePath));
        EnsureStrictDescendant(stagingDirectory, translated);
        return translated;
    }

    internal string GetFinalPath(string stagingDirectory, string stagedPath)
    {
        if (!IsOwnedTransientSibling(stagingDirectory) ||
            !IsStrictDescendant(stagingDirectory, stagedPath))
        {
            throw new InvalidOperationException(
                "Candidate final path translation requires owned staging and staged paths.");
        }

        var relativePath = Path.GetRelativePath(stagingDirectory, stagedPath);
        var translated = Path.GetFullPath(Path.Combine(CandidateDirectory, relativePath));
        EnsureStrictDescendant(CandidateDirectory, translated);
        return translated;
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.IndexOfAny(PortableInvalidFileNameCharacters) >= 0 ||
            value.EndsWith(' ') ||
            value.EndsWith('.') ||
            WindowsReservedDeviceNames.Contains(
                value.Split('.', 2, StringSplitOptions.None)[0]) ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Release identity values must be one portable, nonempty filesystem segment.",
                parameterName);
        }
    }

    private static bool IsStrictDescendant(string root, string path)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(root),
            Path.GetFullPath(path));
        return relative != "." &&
            !Path.IsPathRooted(relative) &&
            relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static void EnsureStrictDescendant(string root, string path)
    {
        if (!IsStrictDescendant(root, path))
        {
            throw new InvalidOperationException(
                $"Release candidate path '{path}' must remain beneath '{root}'.");
        }
    }
}
