#nullable enable

using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

internal enum PipelineProvenanceBoundAssemblyKind
{
    ExactPipelineBuild,
    ExactReleaseCandidate
}

/// <summary>
/// Exact assembly plus the pipeline evidence and package inventory that bind it.
/// </summary>
internal sealed record PipelineProvenanceBoundAssembly(
    PipelineProvenanceBoundAssemblyKind Kind,
    string AssemblyPath,
    string SourceCommit,
    string AssemblySha256,
    long AssemblyByteLength,
    string EvidencePath,
    IReadOnlyList<string> PackageRelativePaths,
    string? PackageDirectoryPath,
    string RecordedFileVersion,
    string RecordedTargetFrameworkName);

internal enum DeliveryTemperatureArtifactContractKind
{
    PublishedBaseline,
    ExactPipelineBuild,
    ExactReleaseCandidate
}

/// <summary>
/// One immutable data row shared by the merged-assembly and package-boundary
/// contracts. Keeping the semantic fields named here prevents either consumer
/// from depending on brittle object-array indexes maintained by the other.
/// </summary>
internal sealed record DeliveryTemperatureArtifactContractCase(
    DeliveryTemperatureArtifactContractKind Kind,
    string ContractRowName,
    string AssemblyPath,
    string AssemblySha256,
    long AssemblyByteLength,
    string SourceCommit,
    string ExpectedTargetFrameworkName,
    string ExpectedFileVersion,
    string EvidencePath,
    IReadOnlyList<string> PackageRelativePaths,
    string? PackageDirectoryPath);

/// <summary>
/// Constructs the complete artifact-contract matrix for the current process.
/// The published baseline is unconditional. Exact pipeline artifacts are
/// optional only when their corresponding environment variable is absent; an
/// invalid supplied value propagates as a semantic binding failure.
/// </summary>
internal static class DeliveryTemperatureArtifactContractCaseProvider
{
    private const string PublishedBaselineSourceCommit =
        "5f7bf43aa823bbb4771936b058c6d573484b6d91";
    private const string PublishedBaselineSha256 =
        "02A14F2E123F42BDD87847C15AB434DAFC8A4D4BC92B465F9DCD367364BF465E";
    private const long PublishedBaselineByteLength = 376320;
    private const string PublishedBaselineFileVersion = "2026.8.26.0";
    private const string PublishedBaselineTargetFrameworkName =
        ".NETFramework,Version=v4.8";

    private static readonly string[] ExactPackageRelativePaths =
    [
        "mod.yaml",
        "mod_info.yaml",
        "DeliveryTemperatureLimit.dll"
    ];

    internal static IReadOnlyList<DeliveryTemperatureArtifactContractCase>
        ResolveForCurrentEnvironment()
    {
        var locator = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment();
        var contractCases = new List<DeliveryTemperatureArtifactContractCase>
        {
            new(
                DeliveryTemperatureArtifactContractKind.PublishedBaseline,
                nameof(DeliveryTemperatureArtifactContractKind.PublishedBaseline),
                Path.Combine(
                    locator.RepositoryRoot,
                    "mods",
                    "delivery-temperature-limit-supercooled",
                    "DeliveryTemperatureLimit.dll"),
                PublishedBaselineSha256,
                PublishedBaselineByteLength,
                PublishedBaselineSourceCommit,
                PublishedBaselineTargetFrameworkName,
                PublishedBaselineFileVersion,
                EvidencePath: "tracked-published-baseline",
                Array.AsReadOnly(ExactPackageRelativePaths.ToArray()),
                PackageDirectoryPath: null)
        };

        foreach (PipelineProvenanceBoundAssembly assembly in
                 locator.ProbeExactPipelineBuildDataRows())
        {
            contractCases.Add(CreateExactArtifactCase(assembly));
        }

        foreach (PipelineProvenanceBoundAssembly assembly in
                 locator.ProbeExactReleaseCandidateDataRows())
        {
            contractCases.Add(CreateExactArtifactCase(assembly));
        }

        return contractCases.AsReadOnly();
    }

    private static DeliveryTemperatureArtifactContractCase
        CreateExactArtifactCase(PipelineProvenanceBoundAssembly assembly)
    {
        DeliveryTemperatureArtifactContractKind contractKind = assembly.Kind switch
        {
            PipelineProvenanceBoundAssemblyKind.ExactPipelineBuild =>
                DeliveryTemperatureArtifactContractKind.ExactPipelineBuild,
            PipelineProvenanceBoundAssemblyKind.ExactReleaseCandidate =>
                DeliveryTemperatureArtifactContractKind.ExactReleaseCandidate,
            _ => throw new InvalidOperationException(
                $"Unsupported provenance-bound assembly kind {assembly.Kind}.")
        };

        return new DeliveryTemperatureArtifactContractCase(
            contractKind,
            contractKind.ToString(),
            assembly.AssemblyPath,
            // Pipeline JSON uses canonical lowercase hex while the assembly
            // contract uses Convert.ToHexString's canonical uppercase form.
            // Normalize at this representation boundary; casing has no digest
            // semantics and must not create a false artifact mismatch.
            assembly.AssemblySha256.ToUpperInvariant(),
            assembly.AssemblyByteLength,
            assembly.SourceCommit,
            assembly.RecordedTargetFrameworkName,
            assembly.RecordedFileVersion,
            assembly.EvidencePath,
            assembly.PackageRelativePaths,
            assembly.PackageDirectoryPath);
    }
}

internal sealed class PipelineProvenanceBindingException : Exception
{
    internal PipelineProvenanceBindingException(string message)
        : base(message)
    {
    }

    internal PipelineProvenanceBindingException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Resolves only explicitly named ONI mod pipeline evidence. It never searches
/// build/candidate directories, chooses a newest artifact, or falls back to the
/// tracked published DLL.
/// </summary>
internal sealed class PipelineProvenanceBoundAssemblyLocator
{
    internal const string BuildResultPathVariable =
        "DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH";
    internal const string ReleaseCandidateDirectoryVariable =
        "DELIVERY_TEMPERATURE_LIMIT_RELEASE_CANDIDATE_DIRECTORY";

    private const string ArtifactsDirectoryVariable =
        "ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY";
    private const string RepositoryRootVariable =
        "ONI_MOD_PIPELINE_REPOSITORY_ROOT";
    private const string DeliveryTemperatureStaticId =
        "MaksymShostak.DeliveryTemperatureLimit";
    private const string DeliveryTemperatureAssemblyFileName =
        "DeliveryTemperatureLimit.dll";
    private const string RequiredTargetFrameworkMoniker = "netstandard2.1";

    private static readonly string[] ExactPackageRelativePaths =
    [
        "mod.yaml",
        "mod_info.yaml",
        DeliveryTemperatureAssemblyFileName
    ];

    private static readonly Regex BuildRunIdPattern = new(
        "^[0-9]{8}T[0-9]{13}Z-[0-9a-f]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex CandidateRunIdPattern = new(
        "^[0-9]{8}T[0-9]{6}\\.[0-9]{7}Z-[0-9a-f]{16}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly string repositoryRoot;
    private readonly string artifactsRoot;
    private readonly string expectedStaticId;
    private readonly string expectedRepositoryCommit;
    private readonly Func<string, string?> readEnvironmentVariable;

    internal string RepositoryRoot => repositoryRoot;

    internal PipelineProvenanceBoundAssemblyLocator(
        string repositoryRoot,
        string artifactsRoot,
        string expectedStaticId,
        string expectedRepositoryCommit,
        Func<string, string?> readEnvironmentVariable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedStaticId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRepositoryCommit);
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        if (!IsLowercaseHex(expectedRepositoryCommit, expectedLength: 40))
        {
            throw new ArgumentException(
                "The expected repository commit must be lowercase 40-hex.",
                nameof(expectedRepositoryCommit));
        }

        this.repositoryRoot = CanonicalizeExistingPath(
            repositoryRoot,
            ExpectedPathKind.Directory);
        this.artifactsRoot = CanonicalizeExistingPath(
            artifactsRoot,
            ExpectedPathKind.Directory);
        this.expectedStaticId = expectedStaticId;
        this.expectedRepositoryCommit = expectedRepositoryCommit;
        this.readEnvironmentVariable = readEnvironmentVariable;
    }

    internal static PipelineProvenanceBoundAssemblyLocator
        CreateForCurrentPipelineEnvironment()
    {
        string repositoryRoot =
            Environment.GetEnvironmentVariable(RepositoryRootVariable) ??
            ResolveRepositoryRootFromTestLocation();
        string artifactsRoot =
            Environment.GetEnvironmentVariable(ArtifactsDirectoryVariable) ??
            Path.Combine(repositoryRoot, "artifacts");
        return new PipelineProvenanceBoundAssemblyLocator(
            repositoryRoot,
            artifactsRoot,
            DeliveryTemperatureStaticId,
            ReadCurrentRepositoryCommit(repositoryRoot),
            Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// Returns no row only when no build evidence was supplied. A nonblank but
    /// invalid value is resolved immediately and therefore fails discovery.
    /// </summary>
    internal IReadOnlyList<PipelineProvenanceBoundAssembly>
        ProbeExactPipelineBuildDataRows()
    {
        string? suppliedPath = readEnvironmentVariable(BuildResultPathVariable);
        return string.IsNullOrWhiteSpace(suppliedPath)
            ? Array.Empty<PipelineProvenanceBoundAssembly>()
            : [ResolveRequiredPipelineBuild()];
    }

    internal PipelineProvenanceBoundAssembly ResolveRequiredPipelineBuild()
    {
        string? suppliedPath = readEnvironmentVariable(BuildResultPathVariable);
        if (string.IsNullOrWhiteSpace(suppliedPath))
        {
            throw new PipelineProvenanceBindingException(
                $"Required exact pipeline build environment variable " +
                $"{BuildResultPathVariable} is absent or whitespace.");
        }

        try
        {
            return ResolvePipelineBuildCore(suppliedPath);
        }
        catch (PipelineProvenanceBindingException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            JsonException or FormatException or ArgumentException or
            InvalidOperationException)
        {
            throw InvalidBuild(
                suppliedPath,
                "the build evidence could not be parsed or validated",
                exception);
        }
    }

    /// <summary>
    /// Returns no row only when no candidate evidence was supplied. It never
    /// discovers or ranks candidate directories.
    /// </summary>
    internal IReadOnlyList<PipelineProvenanceBoundAssembly>
        ProbeExactReleaseCandidateDataRows()
    {
        string? suppliedDirectory = readEnvironmentVariable(
            ReleaseCandidateDirectoryVariable);
        return string.IsNullOrWhiteSpace(suppliedDirectory)
            ? Array.Empty<PipelineProvenanceBoundAssembly>()
            : [ResolveRequiredReleaseCandidate()];
    }

    internal PipelineProvenanceBoundAssembly ResolveRequiredReleaseCandidate()
    {
        string? suppliedDirectory = readEnvironmentVariable(
            ReleaseCandidateDirectoryVariable);
        if (string.IsNullOrWhiteSpace(suppliedDirectory))
        {
            throw new PipelineProvenanceBindingException(
                $"Required exact release candidate environment variable " +
                $"{ReleaseCandidateDirectoryVariable} is absent or whitespace.");
        }

        try
        {
            return ResolveReleaseCandidateCore(suppliedDirectory);
        }
        catch (PipelineProvenanceBindingException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            JsonException or FormatException or ArgumentException or
            InvalidOperationException)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "the candidate evidence could not be parsed or validated",
                exception);
        }
    }

    private PipelineProvenanceBoundAssembly ResolvePipelineBuildCore(
        string suppliedPath)
    {
        if (!Path.IsPathFullyQualified(suppliedPath))
        {
            throw InvalidBuild(
                suppliedPath,
                "the supplied path must be fully qualified");
        }

        if (!string.Equals(
                Path.GetFileName(suppliedPath),
                "build-result.json",
                PathComparison))
        {
            throw InvalidBuild(
                suppliedPath,
                "the supplied file name must be exactly build-result.json");
        }

        string canonicalBuildResultPath;
        try
        {
            canonicalBuildResultPath = CanonicalizeExistingPath(
                suppliedPath,
                ExpectedPathKind.File);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException)
        {
            throw InvalidBuild(
                suppliedPath,
                "the supplied build-result.json does not resolve to one " +
                "existing canonical file",
                exception);
        }

        RequireDescendant(
            artifactsRoot,
            canonicalBuildResultPath,
            () => InvalidBuild(
                suppliedPath,
                $"the build result must remain beneath pipeline artifacts " +
                $"root '{artifactsRoot}'"));
        string[] relativeSegments = SplitRelativePath(
            artifactsRoot,
            canonicalBuildResultPath);
        if (relativeSegments.Length != 4 ||
            !string.Equals(relativeSegments[0], "builds", PathComparison) ||
            !string.Equals(relativeSegments[1], expectedStaticId, PathComparison) ||
            !BuildRunIdPattern.IsMatch(relativeSegments[2]) ||
            !string.Equals(
                relativeSegments[3],
                "build-result.json",
                PathComparison))
        {
            throw InvalidBuild(
                suppliedPath,
                "the file must use artifacts/builds/<static-id>/<pipeline-run-id>/" +
                "build-result.json for the expected mod static ID");
        }

        using JsonDocument buildResultDocument = JsonDocument.Parse(
            File.ReadAllBytes(canonicalBuildResultPath));
        JsonElement buildResult = buildResultDocument.RootElement;
        RequireObject(buildResult, "build result", suppliedPath, isCandidate: false);

        string recordedRunRoot = RequireString(
            buildResult,
            "runRoot",
            suppliedPath,
            isCandidate: false);
        string canonicalRunRoot = CanonicalizeBuildPath(
            recordedRunRoot,
            ExpectedPathKind.Directory,
            suppliedPath,
            "recorded runRoot");
        string expectedRunRoot = Path.GetDirectoryName(
            canonicalBuildResultPath)!;
        if (!PathsEqual(canonicalRunRoot, expectedRunRoot))
        {
            throw InvalidBuild(
                suppliedPath,
                $"recorded runRoot '{recordedRunRoot}' does not equal the exact " +
                $"build-result parent '{expectedRunRoot}'");
        }

        string sourceCommit = RequireString(
            buildResult,
            "sourceCommit",
            suppliedPath,
            isCandidate: false);
        if (!string.Equals(
                sourceCommit,
                expectedRepositoryCommit,
                StringComparison.Ordinal))
        {
            throw InvalidBuild(
                suppliedPath,
                $"sourceCommit '{sourceCommit}' does not match current commit " +
                $"'{expectedRepositoryCommit}'");
        }

        if (!RequireBoolean(
                buildResult,
                "sourceBytesUnchanged",
                suppliedPath,
                isCandidate: false))
        {
            throw InvalidBuild(
                suppliedPath,
                "sourceBytesUnchanged must be true");
        }

        string recordedPrimaryOutputPath = RequireString(
            buildResult,
            "primaryOutputPath",
            suppliedPath,
            isCandidate: false);
        string canonicalPrimaryOutputPath = CanonicalizeBuildPath(
            recordedPrimaryOutputPath,
            ExpectedPathKind.File,
            suppliedPath,
            "primary output");
        string canonicalBuildOutputDirectory = CanonicalizeBuildPath(
            Path.Combine(canonicalRunRoot, "output"),
            ExpectedPathKind.Directory,
            suppliedPath,
            "declared build output directory");
        RequireDescendant(
            canonicalBuildOutputDirectory,
            canonicalPrimaryOutputPath,
            () => InvalidBuild(
                suppliedPath,
                "primary output must remain beneath the run's output directory"));
        if (!string.Equals(
                Path.GetFileName(canonicalPrimaryOutputPath),
                DeliveryTemperatureAssemblyFileName,
                PathComparison))
        {
            throw InvalidBuild(
                suppliedPath,
                $"primary output must be {DeliveryTemperatureAssemblyFileName}");
        }

        JsonElement outputArray = RequireArray(
            buildResult,
            "outputs",
            suppliedPath,
            isCandidate: false);
        RecordedFileDigest? primaryOutputDigest = null;
        var observedOutputPaths = new HashSet<string>(PathComparer);
        foreach (JsonElement outputElement in outputArray.EnumerateArray())
        {
            RecordedFileDigest outputDigest = ReadRecordedFileDigest(
                outputElement,
                suppliedPath,
                isCandidate: false,
                "build output");
            string canonicalOutputPath = CanonicalizeBuildPath(
                outputDigest.Path,
                ExpectedPathKind.File,
                suppliedPath,
                "recorded output");
            RequireDescendant(
                canonicalBuildOutputDirectory,
                canonicalOutputPath,
                () => InvalidBuild(
                    suppliedPath,
                    $"recorded output '{outputDigest.Path}' is outside the " +
                    "run's output directory"));
            if (!observedOutputPaths.Add(canonicalOutputPath))
            {
                throw InvalidBuild(
                    suppliedPath,
                    $"recorded output '{outputDigest.Path}' is duplicated");
            }

            if (PathsEqual(
                    canonicalOutputPath,
                    canonicalPrimaryOutputPath))
            {
                if (primaryOutputDigest is not null)
                {
                    throw InvalidBuild(
                        suppliedPath,
                        "outputs contains duplicate primary-output evidence");
                }

                primaryOutputDigest = outputDigest;
            }
        }

        if (observedOutputPaths.Count != 1)
        {
            throw InvalidBuild(
                suppliedPath,
                "outputs must contain exactly the merged " +
                $"{DeliveryTemperatureAssemblyFileName}; sidecars and " +
                "additional build outputs are outside the package contract");
        }

        if (primaryOutputDigest is null)
        {
            throw InvalidBuild(
                suppliedPath,
                "outputs does not declare the primary output");
        }

        VerifyFileDigest(
            canonicalPrimaryOutputPath,
            primaryOutputDigest,
            () => InvalidBuild(
                suppliedPath,
                "primary output length or SHA-256 differs from outputs evidence"));

        JsonElement inputArray = RequireArray(
            buildResult,
            "inputs",
            suppliedPath,
            isCandidate: false);
        var observedInputPaths = new HashSet<string>(PathComparer);
        foreach (JsonElement inputElement in inputArray.EnumerateArray())
        {
            RecordedFileDigest inputDigest = ReadRecordedFileDigest(
                inputElement,
                suppliedPath,
                isCandidate: false,
                "build input");
            string canonicalInputPath = CanonicalizeBuildPath(
                inputDigest.Path,
                ExpectedPathKind.File,
                suppliedPath,
                "build input");
            RequireDescendant(
                repositoryRoot,
                canonicalInputPath,
                () => InvalidBuild(
                    suppliedPath,
                    $"build input '{inputDigest.Path}' is outside current " +
                    "repository root"));
            if (!observedInputPaths.Add(canonicalInputPath))
            {
                throw InvalidBuild(
                    suppliedPath,
                    $"build input '{inputDigest.Path}' is duplicated");
            }

            VerifyFileDigest(
                canonicalInputPath,
                inputDigest,
                () => InvalidBuild(
                    suppliedPath,
                    $"current input '{inputDigest.Path}' differs from the " +
                    "recorded working-tree fingerprint"));
        }

        if (observedInputPaths.Count == 0)
        {
            throw InvalidBuild(
                suppliedPath,
                "inputs must contain the nonempty working-tree fingerprint " +
                "used by the pipeline build");
        }

        string recordedTargetFrameworkMoniker = RequireString(
            buildResult,
            "primaryAssemblyTargetFrameworkMoniker",
            suppliedPath,
            isCandidate: false);
        if (!string.Equals(
                recordedTargetFrameworkMoniker,
                RequiredTargetFrameworkMoniker,
                StringComparison.Ordinal))
        {
            throw InvalidBuild(
                suppliedPath,
                "primaryAssemblyTargetFrameworkMoniker must be " +
                RequiredTargetFrameworkMoniker);
        }

        // Keep the CLR TargetFrameworkAttribute name distinct from the short
        // SDK target-framework moniker. The merged-assembly contract compares
        // this exact recorded name with the exact artifact's metadata.
        string recordedTargetFrameworkName = RequireString(
            buildResult,
            "primaryAssemblyTargetFrameworkName",
            suppliedPath,
            isCandidate: false);
        string recordedFileVersion = RequireCanonicalFourComponentVersion(
            RequireObjectProperty(
                buildResult,
                "primaryAssemblyVersion",
                suppliedPath,
                isCandidate: false),
            "fileVersion",
            suppliedPath,
            isCandidate: false);

        return new PipelineProvenanceBoundAssembly(
            PipelineProvenanceBoundAssemblyKind.ExactPipelineBuild,
            canonicalPrimaryOutputPath,
            sourceCommit,
            primaryOutputDigest.Sha256,
            primaryOutputDigest.ByteLength,
            canonicalBuildResultPath,
            Array.AsReadOnly(ExactPackageRelativePaths.ToArray()),
            PackageDirectoryPath: null,
            recordedFileVersion,
            recordedTargetFrameworkName);
    }

    private PipelineProvenanceBoundAssembly ResolveReleaseCandidateCore(
        string suppliedDirectory)
    {
        if (!Path.IsPathFullyQualified(suppliedDirectory))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "the supplied candidate directory must be fully qualified");
        }

        string canonicalCandidateDirectory;
        try
        {
            canonicalCandidateDirectory = CanonicalizeExistingPath(
                suppliedDirectory,
                ExpectedPathKind.Directory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "the supplied candidate does not resolve to one existing " +
                "canonical directory",
                exception);
        }

        RequireDescendant(
            artifactsRoot,
            canonicalCandidateDirectory,
            () => InvalidCandidate(
                suppliedDirectory,
                $"the candidate must remain beneath pipeline artifacts root " +
                $"'{artifactsRoot}'"));
        string[] relativeSegments = SplitRelativePath(
            artifactsRoot,
            canonicalCandidateDirectory);
        if (relativeSegments.Length != 4 ||
            !string.Equals(
                relativeSegments[0],
                "release-candidates",
                PathComparison) ||
            !string.Equals(relativeSegments[1], expectedStaticId, PathComparison) ||
            string.IsNullOrWhiteSpace(relativeSegments[2]) ||
            !CandidateRunIdPattern.IsMatch(relativeSegments[3]))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "the directory must use artifacts/release-candidates/<static-id>/" +
                "<version>/<pipeline-run-id> for the expected mod static ID");
        }

        string workshopContentDirectory = CanonicalizeCandidatePath(
            Path.Combine(canonicalCandidateDirectory, "workshop-content"),
            ExpectedPathKind.Directory,
            suppliedDirectory,
            "workshop-content directory");
        string workshopListingDirectory = CanonicalizeCandidatePath(
            Path.Combine(canonicalCandidateDirectory, "workshop-listing"),
            ExpectedPathKind.Directory,
            suppliedDirectory,
            "workshop-listing directory");
        string releaseEvidenceDirectory = CanonicalizeCandidatePath(
            Path.Combine(canonicalCandidateDirectory, "release-evidence"),
            ExpectedPathKind.Directory,
            suppliedDirectory,
            "release-evidence directory");
        string manifestPath = CanonicalizeCandidatePath(
            Path.Combine(
                releaseEvidenceDirectory,
                "release-content-manifest.json"),
            ExpectedPathKind.File,
            suppliedDirectory,
            "release-content manifest");
        string provenancePath = CanonicalizeCandidatePath(
            Path.Combine(releaseEvidenceDirectory, "build-provenance.json"),
            ExpectedPathKind.File,
            suppliedDirectory,
            "build provenance");

        using JsonDocument manifestDocument = JsonDocument.Parse(
            File.ReadAllBytes(manifestPath));
        JsonElement manifest = manifestDocument.RootElement;
        RequireObject(manifest, "release content manifest", suppliedDirectory, true);
        if (RequireInt32(
                manifest,
                "schemaVersion",
                suppliedDirectory,
                isCandidate: true) != 1)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "release-content-manifest.json schemaVersion must be 1");
        }

        JsonElement manifestEntries = RequireArray(
            manifest,
            "entries",
            suppliedDirectory,
            isCandidate: true);
        var entries = new List<ReleaseContentEvidenceEntry>();
        var manifestPathIdentities = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement entryElement in manifestEntries.EnumerateArray())
        {
            ReleaseContentEvidenceEntry entry = ReadReleaseContentEntry(
                entryElement,
                suppliedDirectory);
            string identity = entry.ContentArea + "\0" + entry.RelativePath;
            if (!manifestPathIdentities.Add(identity))
            {
                throw InvalidCandidate(
                    suppliedDirectory,
                    $"manifest contains a portable duplicate at " +
                    $"{entry.ContentArea}/{entry.RelativePath}");
            }

            string contentRoot = entry.ContentArea == "workshop-content"
                ? workshopContentDirectory
                : workshopListingDirectory;
            string manifestFilePath = CanonicalizeCandidatePath(
                Path.Combine(
                    contentRoot,
                    entry.RelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)),
                ExpectedPathKind.File,
                suppliedDirectory,
                $"manifest entry {entry.ContentArea}/{entry.RelativePath}");
            RequireDescendant(
                contentRoot,
                manifestFilePath,
                () => InvalidCandidate(
                    suppliedDirectory,
                    $"manifest entry '{entry.RelativePath}' escapes " +
                    $"{entry.ContentArea}"));
            VerifyFileDigest(
                manifestFilePath,
                new RecordedFileDigest(
                    manifestFilePath,
                    entry.ByteLength,
                    entry.Sha256),
                () => InvalidCandidate(
                    suppliedDirectory,
                    $"manifest entry {entry.ContentArea}/{entry.RelativePath} " +
                    "has a changed length or SHA-256"));
            entries.Add(entry);
        }

        AssertExactManifestInventory(
            workshopContentDirectory,
            "workshop-content",
            entries,
            suppliedDirectory);
        AssertExactManifestInventory(
            workshopListingDirectory,
            "workshop-listing",
            entries,
            suppliedDirectory);

        string recordedContentDigest = RequireLowercaseSha256(
            RequireString(
                manifest,
                "contentDigest",
                suppliedDirectory,
                isCandidate: true),
            suppliedDirectory,
            isCandidate: true,
            "manifest contentDigest");
        string calculatedContentDigest = CalculateReleaseContentDigest(entries);
        if (!string.Equals(
                recordedContentDigest,
                calculatedContentDigest,
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "release-content-manifest.json contentDigest does not match " +
                "its canonical entries");
        }

        ReleaseContentEvidenceEntry[] packageEntries = entries
            .Where(entry => entry.ContentArea == "workshop-content")
            .OrderBy(entry =>
                Array.IndexOf(ExactPackageRelativePaths, entry.RelativePath))
            .ToArray();
        if (packageEntries.Length != ExactPackageRelativePaths.Length ||
            !packageEntries.Select(entry => entry.RelativePath)
                .SequenceEqual(ExactPackageRelativePaths, StringComparer.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "workshop-content inventory must contain exactly mod.yaml, " +
                "mod_info.yaml, and DeliveryTemperatureLimit.dll");
        }

        ReleaseContentEvidenceEntry assemblyEntry = packageEntries.Single(
            entry => entry.RelativePath == DeliveryTemperatureAssemblyFileName);
        if (!string.Equals(assemblyEntry.Role, "runtime", StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "DeliveryTemperatureLimit.dll must have runtime manifest role");
        }

        string candidateAssemblyPath = CanonicalizeCandidatePath(
            Path.Combine(
                workshopContentDirectory,
                DeliveryTemperatureAssemblyFileName),
            ExpectedPathKind.File,
            suppliedDirectory,
            "candidate assembly");

        using JsonDocument provenanceDocument = JsonDocument.Parse(
            File.ReadAllBytes(provenancePath));
        JsonElement provenance = provenanceDocument.RootElement;
        RequireObject(provenance, "build provenance", suppliedDirectory, true);
        if (RequireInt32(
                provenance,
                "schemaVersion",
                suppliedDirectory,
                isCandidate: true) != 1 ||
            RequireInt32(
                provenance,
                "profileSchemaVersion",
                suppliedDirectory,
                isCandidate: true) != 1)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "build-provenance.json schema versions must both be 1");
        }

        string provenanceStaticId = RequireString(
            provenance,
            "staticId",
            suppliedDirectory,
            isCandidate: true);
        if (!string.Equals(
                provenanceStaticId,
                expectedStaticId,
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"provenance staticId '{provenanceStaticId}' does not match " +
                $"'{expectedStaticId}'");
        }

        string provenanceVersion = RequireString(
            provenance,
            "version",
            suppliedDirectory,
            isCandidate: true);
        if (!string.Equals(
                provenanceVersion,
                relativeSegments[2],
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"provenance version '{provenanceVersion}' does not match " +
                $"candidate hierarchy version '{relativeSegments[2]}'");
        }

        string sourceCommit = RequireString(
            provenance,
            "repositoryCommit",
            suppliedDirectory,
            isCandidate: true);
        if (!string.Equals(
                sourceCommit,
                expectedRepositoryCommit,
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"provenance repositoryCommit '{sourceCommit}' does not match " +
                $"current commit '{expectedRepositoryCommit}'");
        }

        if (!RequireBoolean(
                provenance,
                "relevantPathsClean",
                suppliedDirectory,
                isCandidate: true) ||
            !RequireBoolean(
                provenance,
                "sourceBytesUnchanged",
                suppliedDirectory,
                isCandidate: true))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "candidate provenance requires relevantPathsClean and " +
                "sourceBytesUnchanged to be true");
        }

        string targetFramework = RequireString(
            provenance,
            "targetFramework",
            suppliedDirectory,
            isCandidate: true);
        if (!string.Equals(
                targetFramework,
                RequiredTargetFrameworkMoniker,
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "candidate provenance targetFramework must be " +
                RequiredTargetFrameworkMoniker);
        }

        string recordedTargetFrameworkName = RequireString(
            provenance,
            "primaryAssemblyTargetFrameworkName",
            suppliedDirectory,
            isCandidate: true);
        string recordedFileVersion = RequireCanonicalFourComponentVersion(
            RequireObjectProperty(
                provenance,
                "primaryAssemblyVersion",
                suppliedDirectory,
                isCandidate: true),
            "fileVersion",
            suppliedDirectory,
            isCandidate: true);

        if (!string.Equals(
                RequireString(
                    provenance,
                    "configuration",
                    suppliedDirectory,
                    isCandidate: true),
                "Release",
                StringComparison.Ordinal) ||
            !string.Equals(
                RequireString(
                    provenance,
                    "artifactsDirectory",
                    suppliedDirectory,
                    isCandidate: true),
                "${ARTIFACTS}",
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "candidate provenance must record Release configuration and " +
                "the canonical ${ARTIFACTS} root token");
        }

        string provenanceContentDigest = RequireLowercaseSha256(
            RequireString(
                provenance,
                "releaseContentDigest",
                suppliedDirectory,
                isCandidate: true),
            suppliedDirectory,
            isCandidate: true,
            "provenance releaseContentDigest");
        if (!string.Equals(
                provenanceContentDigest,
                recordedContentDigest,
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "provenance releaseContentDigest does not match the manifest");
        }

        JsonElement primaryOutputElement = RequireObjectProperty(
            provenance,
            "primaryOutput",
            suppliedDirectory,
            isCandidate: true);
        RecordedFileDigest primaryOutputDigest = ReadRecordedFileDigest(
            primaryOutputElement,
            suppliedDirectory,
            isCandidate: true,
            "provenance primary output");
        if (!primaryOutputDigest.Path.StartsWith(
                "${ARTIFACTS}/",
                StringComparison.Ordinal) ||
            !primaryOutputDigest.Path.EndsWith(
                $"/{DeliveryTemperatureAssemblyFileName}",
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "provenance primary output must be the canonical artifacts-" +
                $"rooted {DeliveryTemperatureAssemblyFileName} path");
        }

        if (primaryOutputDigest.ByteLength != assemblyEntry.ByteLength ||
            !string.Equals(
                primaryOutputDigest.Sha256,
                assemblyEntry.Sha256,
                StringComparison.Ordinal))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "provenance primary output digest does not match packaged " +
                "DeliveryTemperatureLimit.dll");
        }

        JsonElement buildOutputs = RequireArray(
            provenance,
            "buildOutputs",
            suppliedDirectory,
            isCandidate: true);
        int matchingBuildOutputCount = 0;
        var observedBuildOutputPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement buildOutputElement in buildOutputs.EnumerateArray())
        {
            RecordedFileDigest buildOutput = ReadRecordedFileDigest(
                buildOutputElement,
                suppliedDirectory,
                isCandidate: true,
                "provenance build output");
            if (!observedBuildOutputPaths.Add(buildOutput.Path))
            {
                throw InvalidCandidate(
                    suppliedDirectory,
                    $"provenance build output path '{buildOutput.Path}' is " +
                    "duplicated");
            }

            if (buildOutput == primaryOutputDigest)
            {
                matchingBuildOutputCount++;
            }
        }

        if (matchingBuildOutputCount != 1)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "provenance buildOutputs must contain the primary output " +
                "digest exactly once");
        }

        VerifyFileDigest(
            candidateAssemblyPath,
            primaryOutputDigest,
            () => InvalidCandidate(
                suppliedDirectory,
                "candidate DLL length or SHA-256 differs from primary-output " +
                "provenance"));

        return new PipelineProvenanceBoundAssembly(
            PipelineProvenanceBoundAssemblyKind.ExactReleaseCandidate,
            candidateAssemblyPath,
            sourceCommit,
            assemblyEntry.Sha256,
            assemblyEntry.ByteLength,
            canonicalCandidateDirectory,
            Array.AsReadOnly(ExactPackageRelativePaths.ToArray()),
            workshopContentDirectory,
            recordedFileVersion,
            recordedTargetFrameworkName);
    }

    private static ReleaseContentEvidenceEntry ReadReleaseContentEntry(
        JsonElement entry,
        string suppliedDirectory)
    {
        RequireObject(entry, "manifest entry", suppliedDirectory, true);
        string contentArea = RequireString(
            entry,
            "contentArea",
            suppliedDirectory,
            isCandidate: true);
        if (contentArea is not ("workshop-content" or "workshop-listing"))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"manifest content area '{contentArea}' is unknown");
        }

        string relativePath = NormalizeManifestRelativePath(
            RequireString(
                entry,
                "relativePath",
                suppliedDirectory,
                isCandidate: true),
            suppliedDirectory);
        long byteLength = RequireInt64(
            entry,
            "byteLength",
            suppliedDirectory,
            isCandidate: true);
        if (byteLength < 0)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"manifest entry '{relativePath}' has negative length");
        }

        string sha256 = RequireLowercaseSha256(
            RequireString(
                entry,
                "sha256",
                suppliedDirectory,
                isCandidate: true),
            suppliedDirectory,
            isCandidate: true,
            $"manifest entry '{relativePath}' SHA-256");
        string role = RequireString(
            entry,
            "role",
            suppliedDirectory,
            isCandidate: true);
        if (role is not (
            "runtime" or "description" or "change-notes" or "preview"))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"manifest role '{role}' is unknown");
        }

        return new(contentArea, relativePath, byteLength, sha256, role);
    }

    private static string NormalizeManifestRelativePath(
        string relativePath,
        string suppliedDirectory)
    {
        if (string.IsNullOrEmpty(relativePath) ||
            relativePath.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            throw InvalidCandidate(
                suppliedDirectory,
                "manifest relative paths must contain portable nonempty text");
        }

        string normalized = relativePath
            .Replace('\\', '/')
            .Normalize(NormalizationForm.FormC);
        if (normalized[0] == '/' ||
            normalized.Length >= 2 &&
            char.IsAsciiLetter(normalized[0]) &&
            normalized[1] == ':')
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"manifest path '{relativePath}' must be relative");
        }

        string[] segments = normalized.Split('/');
        if (segments.Any(segment =>
                segment.Length == 0 || segment is "." or ".."))
        {
            throw InvalidCandidate(
                suppliedDirectory,
                $"manifest path '{relativePath}' contains empty or traversal " +
                "segments");
        }

        return normalized;
    }

    private static void AssertExactManifestInventory(
        string contentDirectory,
        string contentArea,
        IReadOnlyList<ReleaseContentEvidenceEntry> manifestEntries,
        string suppliedDirectory)
    {
        var declaredRelativePaths = manifestEntries
            .Where(entry => entry.ContentArea == contentArea)
            .Select(entry => entry.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualRelativePaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        // Enumeration is confined to the one explicitly supplied candidate. It
        // verifies inventory and is never used to discover or rank artifacts.
        foreach (string enumeratedPath in Directory.EnumerateFiles(
                     contentDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string canonicalPath = CanonicalizeExistingPath(
                enumeratedPath,
                ExpectedPathKind.File);
            RequireDescendant(
                contentDirectory,
                canonicalPath,
                () => InvalidCandidate(
                    suppliedDirectory,
                    $"enumerated {contentArea} file escapes its content root"));
            string relativePath = Path.GetRelativePath(
                    contentDirectory,
                    canonicalPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            actualRelativePaths.Add(relativePath);
        }

        if (!actualRelativePaths.SetEquals(declaredRelativePaths))
        {
            string undeclared = string.Join(
                ", ",
                actualRelativePaths.Except(
                    declaredRelativePaths,
                    StringComparer.OrdinalIgnoreCase));
            string missing = string.Join(
                ", ",
                declaredRelativePaths.Except(
                    actualRelativePaths,
                    StringComparer.OrdinalIgnoreCase));
            throw InvalidCandidate(
                suppliedDirectory,
                $"{contentArea} inventory differs from the manifest; " +
                $"undeclared=[{undeclared}], missing=[{missing}]");
        }
    }

    private static string CalculateReleaseContentDigest(
        IReadOnlyList<ReleaseContentEvidenceEntry> entries)
    {
        var canonicalText = new StringBuilder(
            "oni-release-content-manifest-v1\n");
        foreach (ReleaseContentEvidenceEntry entry in entries
                     .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
                     .ThenBy(entry => entry.ContentArea, StringComparer.Ordinal))
        {
            canonicalText.Append(entry.ContentArea);
            canonicalText.Append('\0');
            canonicalText.Append(entry.RelativePath);
            canonicalText.Append('\0');
            canonicalText.Append(
                entry.ByteLength.ToString(CultureInfo.InvariantCulture));
            canonicalText.Append('\0');
            canonicalText.Append(entry.Sha256);
            canonicalText.Append('\0');
            canonicalText.Append(entry.Role);
            canonicalText.Append('\n');
        }

        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonicalText.ToString())))
            .ToLowerInvariant();
    }

    private string CanonicalizeBuildPath(
        string path,
        ExpectedPathKind expectedPathKind,
        string suppliedBuildResultPath,
        string semanticName)
    {
        try
        {
            return CanonicalizeExistingPath(path, expectedPathKind);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException)
        {
            throw InvalidBuild(
                suppliedBuildResultPath,
                $"{semanticName} '{path}' is not one canonical existing " +
                expectedPathKind.ToString().ToLowerInvariant(),
                exception);
        }
    }

    private static string CanonicalizeCandidatePath(
        string path,
        ExpectedPathKind expectedPathKind,
        string suppliedCandidateDirectory,
        string semanticName)
    {
        try
        {
            return CanonicalizeExistingPath(path, expectedPathKind);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException)
        {
            throw InvalidCandidate(
                suppliedCandidateDirectory,
                $"{semanticName} '{path}' is not one canonical existing " +
                expectedPathKind.ToString().ToLowerInvariant(),
                exception);
        }
    }

    /// <summary>
    /// Walks each existing path segment and resolves every reparse target before
    /// containment is evaluated. This prevents textual prefix checks from
    /// accepting a link that leaves the diagnosed artifacts root.
    /// </summary>
    private static string CanonicalizeExistingPath(
        string path,
        ExpectedPathKind expectedPathKind)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "Path must be fully qualified.",
                nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        string pathRoot = Path.GetPathRoot(fullPath) ??
            throw new ArgumentException("Path has no filesystem root.", nameof(path));
        string currentPath = new DirectoryInfo(pathRoot).FullName;
        string relativePath = Path.GetRelativePath(pathRoot, fullPath);
        string[] segments = relativePath == "."
            ? Array.Empty<string>()
            : relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

        for (int segmentIndex = 0;
             segmentIndex < segments.Length;
             segmentIndex++)
        {
            string candidatePath = Path.Combine(
                currentPath,
                segments[segmentIndex]);
            bool isDirectory = Directory.Exists(candidatePath);
            bool isFile = File.Exists(candidatePath);
            if (!isDirectory && !isFile)
            {
                throw new FileNotFoundException(
                    $"Path segment does not exist: {candidatePath}",
                    candidatePath);
            }

            if (segmentIndex < segments.Length - 1 && !isDirectory)
            {
                throw new IOException(
                    $"Intermediate path segment is not a directory: " +
                    $"{candidatePath}");
            }

            FileSystemInfo fileSystemInfo = isDirectory
                ? new DirectoryInfo(candidatePath)
                : new FileInfo(candidatePath);
            fileSystemInfo.Refresh();
            if ((fileSystemInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                FileSystemInfo resolvedTarget =
                    fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true) ??
                    throw new IOException(
                        $"Reparse path has no resolvable target: {candidatePath}");
                currentPath = Path.GetFullPath(resolvedTarget.FullName);
            }
            else
            {
                currentPath = Path.GetFullPath(fileSystemInfo.FullName);
            }
        }

        bool finalIsDirectory = Directory.Exists(currentPath);
        bool finalIsFile = File.Exists(currentPath);
        if (expectedPathKind == ExpectedPathKind.Directory && !finalIsDirectory ||
            expectedPathKind == ExpectedPathKind.File && !finalIsFile)
        {
            throw new IOException(
                $"Canonical path is not an existing {expectedPathKind}: " +
                $"{currentPath}");
        }

        return currentPath;
    }

    private static string[] SplitRelativePath(string root, string descendant) =>
        Path.GetRelativePath(root, descendant).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

    private static void RequireDescendant(
        string root,
        string candidate,
        Func<PipelineProvenanceBindingException> createException)
    {
        string relative = Path.GetRelativePath(
            Path.GetFullPath(root),
            Path.GetFullPath(candidate));
        if (relative == "." ||
            Path.IsPathRooted(relative) ||
            relative == ".." ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            relative.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw createException();
        }
    }

    private static void VerifyFileDigest(
        string filePath,
        RecordedFileDigest expected,
        Func<PipelineProvenanceBindingException> createException)
    {
        FileInfo file = new(filePath);
        if (file.Length != expected.ByteLength ||
            !string.Equals(
                CalculateFileSha256(filePath),
                expected.Sha256,
                StringComparison.Ordinal))
        {
            throw createException();
        }
    }

    private static string CalculateFileSha256(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static RecordedFileDigest ReadRecordedFileDigest(
        JsonElement element,
        string suppliedEvidencePath,
        bool isCandidate,
        string semanticName)
    {
        RequireObject(
            element,
            semanticName,
            suppliedEvidencePath,
            isCandidate);
        string path = RequireString(
            element,
            "path",
            suppliedEvidencePath,
            isCandidate);
        long byteLength = RequireInt64(
            element,
            "byteLength",
            suppliedEvidencePath,
            isCandidate);
        if (byteLength < 0)
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"{semanticName} byteLength cannot be negative");
        }

        string sha256 = RequireLowercaseSha256(
            RequireString(
                element,
                "sha256",
                suppliedEvidencePath,
                isCandidate),
            suppliedEvidencePath,
            isCandidate,
            $"{semanticName} SHA-256");
        return new(path, byteLength, sha256);
    }

    private static string RequireLowercaseSha256(
        string value,
        string suppliedEvidencePath,
        bool isCandidate,
        string semanticName)
    {
        if (!IsLowercaseHex(value, expectedLength: 64))
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"{semanticName} must be lowercase 64-hex");
        }

        return value;
    }

    private static bool IsLowercaseHex(string value, int expectedLength) =>
        value.Length == expectedLength &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string RequireString(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required string property '{propertyName}' is missing or invalid");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required string property '{propertyName}' is blank");
        }

        return value;
    }

    private static string RequireCanonicalFourComponentVersion(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        string value = RequireString(
            parent,
            propertyName,
            suppliedEvidencePath,
            isCandidate);
        if (!Version.TryParse(value, out Version? version) ||
            version.Build < 0 ||
            version.Revision < 0 ||
            !string.Equals(
                version.ToString(4),
                value,
                StringComparison.Ordinal))
        {
            // FileVersionInfo exposes four numeric components. Requiring the
            // same canonical representation here prevents equivalent-looking
            // but textually ambiguous evidence from reaching the byte-level
            // assembly contract.
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required version property '{propertyName}' must use its " +
                "canonical four-component numeric form");
        }

        return value;
    }

    private static bool RequireBoolean(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required Boolean property '{propertyName}' is missing or invalid");
        }

        return property.GetBoolean();
    }

    private static int RequireInt32(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out int value))
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required Int32 property '{propertyName}' is missing or invalid");
        }

        return value;
    }

    private static long RequireInt64(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt64(out long value))
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required Int64 property '{propertyName}' is missing or invalid");
        }

        return value;
    }

    private static JsonElement RequireArray(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required array property '{propertyName}' is missing or invalid");
        }

        return property;
    }

    private static JsonElement RequireObjectProperty(
        JsonElement parent,
        string propertyName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"required object property '{propertyName}' is missing or invalid");
        }

        return property;
    }

    private static void RequireObject(
        JsonElement value,
        string semanticName,
        string suppliedEvidencePath,
        bool isCandidate)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw InvalidEvidence(
                suppliedEvidencePath,
                isCandidate,
                $"{semanticName} must be a JSON object");
        }
    }

    private static PipelineProvenanceBindingException InvalidEvidence(
        string suppliedEvidencePath,
        bool isCandidate,
        string reason) =>
        isCandidate
            ? InvalidCandidate(suppliedEvidencePath, reason)
            : InvalidBuild(suppliedEvidencePath, reason);

    private static PipelineProvenanceBindingException InvalidBuild(
        string suppliedPath,
        string reason,
        Exception? innerException = null)
    {
        string message =
            $"Invalid exact pipeline build result '{suppliedPath}': {reason}.";
        return innerException is null
            ? new PipelineProvenanceBindingException(message)
            : new PipelineProvenanceBindingException(message, innerException);
    }

    private static PipelineProvenanceBindingException InvalidCandidate(
        string suppliedDirectory,
        string reason,
        Exception? innerException = null)
    {
        string message =
            $"Invalid exact release candidate '{suppliedDirectory}': {reason}.";
        return innerException is null
            ? new PipelineProvenanceBindingException(message)
            : new PipelineProvenanceBindingException(message, innerException);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static string ResolveRepositoryRootFromTestLocation()
    {
        DirectoryInfo? candidate = new(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(
                    candidate.FullName,
                    "mods",
                    "delivery-temperature-limit-supercooled",
                    "oni-mod-pipeline.toml")))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        throw new PipelineProvenanceBindingException(
            $"{RepositoryRootVariable} was not supplied and the repository " +
            $"root could not be resolved from {AppContext.BaseDirectory}.");
    }

    private static string ReadCurrentRepositoryCommit(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new PipelineProvenanceBindingException(
                "git rev-parse could not start while binding pipeline evidence.");
        }

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        string commit = standardOutput.Trim();
        if (process.ExitCode != 0 ||
            !IsLowercaseHex(commit, expectedLength: 40))
        {
            throw new PipelineProvenanceBindingException(
                $"Current repository commit could not be resolved. " +
                $"git exit={process.ExitCode}; stderr={standardError}");
        }

        return commit;
    }

    private enum ExpectedPathKind
    {
        File,
        Directory
    }

    private sealed record RecordedFileDigest(
        string Path,
        long ByteLength,
        string Sha256);

    private sealed record ReleaseContentEvidenceEntry(
        string ContentArea,
        string RelativePath,
        long ByteLength,
        string Sha256,
        string Role);
}

[TestClass]
public sealed class PipelineProvenanceBoundAssemblyLocatorTests
{
    private const string ExpectedStaticId =
        "MaksymShostak.DeliveryTemperatureLimit";
    private const string ExpectedRepositoryCommit =
        "0123456789abcdef0123456789abcdef01234567";
    private const string FixtureReleaseVersion = "2026.8.30";
    private const string FixtureAssemblyFileVersion = "2026.8.30.0";
    private const string FixtureTargetFrameworkName =
        ".NETStandard,Version=v2.1";

    [TestMethod]
    public void BuildDataRowProbe_WhenVariableIsMissing_YieldsNoRowWhileRequiredResolverRejectsAbsence()
    {
        using var fixture = new ProvenanceBindingFixture();
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(buildResultPath: null, candidateDirectory: null);

        Assert.IsEmpty(locator.ProbeExactPipelineBuildDataRows().ToArray());
        PipelineProvenanceBindingException exception =
            Assert.ThrowsExactly<PipelineProvenanceBindingException>(
                locator.ResolveRequiredPipelineBuild);
        StringAssert.Contains(
            exception.Message,
            PipelineProvenanceBoundAssemblyLocator.BuildResultPathVariable);
    }

    [TestMethod]
    public void BuildDataRowProbe_WhenSuppliedValueIsInvalid_ThrowsInsteadOfSuppressingRow()
    {
        using var fixture = new ProvenanceBindingFixture();
        string invalidSuppliedPath = Path.Combine(
            fixture.RepositoryRoot,
            "README.md");
        File.WriteAllText(invalidSuppliedPath, "not a build result");
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(invalidSuppliedPath, candidateDirectory: null);

        PipelineProvenanceBindingException exception =
            Assert.ThrowsExactly<PipelineProvenanceBindingException>(
                () => locator.ProbeExactPipelineBuildDataRows().ToArray());
        StringAssert.Contains(exception.Message, invalidSuppliedPath);
        StringAssert.Contains(exception.Message, "build-result.json");
    }

    [TestMethod]
    public void RequiredBuildResolver_WhenExactBuildResultIsValid_ReturnsProvenanceBoundAssembly()
    {
        using var fixture = new ProvenanceBindingFixture();
        string buildResultPath = fixture.CreateValidBuildResult();
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(buildResultPath, candidateDirectory: null);

        PipelineProvenanceBoundAssembly assembly =
            locator.ResolveRequiredPipelineBuild();

        Assert.AreEqual(
            PipelineProvenanceBoundAssemblyKind.ExactPipelineBuild,
            assembly.Kind);
        Assert.AreEqual(fixture.BuildAssemblyPath, assembly.AssemblyPath);
        Assert.AreEqual(ExpectedRepositoryCommit, assembly.SourceCommit);
        Assert.AreEqual(
            FixtureAssemblyFileVersion,
            assembly.RecordedFileVersion);
        Assert.AreEqual(
            FixtureTargetFrameworkName,
            assembly.RecordedTargetFrameworkName);
        CollectionAssert.AreEqual(
            new[]
            {
                "mod.yaml",
                "mod_info.yaml",
                "DeliveryTemperatureLimit.dll"
            },
            assembly.PackageRelativePaths.ToArray());
        Assert.HasCount(1, locator.ProbeExactPipelineBuildDataRows().ToArray());
    }

    [TestMethod]
    [DataRow(BuildResultMutation.WrongSourceCommit)]
    [DataRow(BuildResultMutation.ChangedSourceInput)]
    [DataRow(BuildResultMutation.ChangedPrimaryOutput)]
    [DataRow(BuildResultMutation.PrimaryOutputOutsideBuildOutput)]
    [DataRow(BuildResultMutation.MismatchedStaticIdRunRoot)]
    [DataRow(BuildResultMutation.AdditionalBuildOutput)]
    [DataRow(BuildResultMutation.EmptyInputFingerprint)]
    [DataRow(BuildResultMutation.MissingPrimaryAssemblyFileVersion)]
    [DataRow(BuildResultMutation.NonCanonicalPrimaryAssemblyFileVersion)]
    [DataRow(BuildResultMutation.MissingPrimaryAssemblyTargetFrameworkName)]
    [DataRow(BuildResultMutation.WrongPrimaryAssemblyTargetFrameworkMoniker)]
    public void RequiredBuildResolver_WhenBuildEvidenceIsMutated_RejectsBinding(
        BuildResultMutation mutation)
    {
        using var fixture = new ProvenanceBindingFixture();
        string buildResultPath = fixture.CreateValidBuildResult();
        fixture.ApplyBuildResultMutation(mutation);
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(buildResultPath, candidateDirectory: null);

        PipelineProvenanceBindingException exception =
            Assert.ThrowsExactly<PipelineProvenanceBindingException>(
                locator.ResolveRequiredPipelineBuild);
        StringAssert.Contains(exception.Message, buildResultPath);
    }

    [TestMethod]
    public void ReleaseCandidateDataRowProbe_WhenVariableIsWhitespace_YieldsNoRowWhileRequiredResolverRejectsAbsence()
    {
        using var fixture = new ProvenanceBindingFixture();
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(
                buildResultPath: null,
                candidateDirectory: "   ");

        Assert.IsEmpty(
            locator.ProbeExactReleaseCandidateDataRows().ToArray());
        PipelineProvenanceBindingException exception =
            Assert.ThrowsExactly<PipelineProvenanceBindingException>(
                locator.ResolveRequiredReleaseCandidate);
        StringAssert.Contains(
            exception.Message,
            PipelineProvenanceBoundAssemblyLocator
                .ReleaseCandidateDirectoryVariable);
    }

    [TestMethod]
    public void RequiredReleaseCandidateResolver_WhenManifestAndProvenanceAreValid_ReturnsExactPackagedAssembly()
    {
        using var fixture = new ProvenanceBindingFixture();
        string candidateDirectory = fixture.CreateValidReleaseCandidate();
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(
                buildResultPath: null,
                candidateDirectory);

        PipelineProvenanceBoundAssembly assembly =
            locator.ResolveRequiredReleaseCandidate();

        Assert.AreEqual(
            PipelineProvenanceBoundAssemblyKind.ExactReleaseCandidate,
            assembly.Kind);
        Assert.AreEqual(fixture.CandidateAssemblyPath, assembly.AssemblyPath);
        Assert.AreEqual(ExpectedRepositoryCommit, assembly.SourceCommit);
        Assert.AreEqual(
            FixtureAssemblyFileVersion,
            assembly.RecordedFileVersion);
        Assert.AreEqual(
            FixtureTargetFrameworkName,
            assembly.RecordedTargetFrameworkName);
        CollectionAssert.AreEqual(
            new[]
            {
                "mod.yaml",
                "mod_info.yaml",
                "DeliveryTemperatureLimit.dll"
            },
            assembly.PackageRelativePaths.ToArray());
        Assert.HasCount(
            1,
            locator.ProbeExactReleaseCandidateDataRows().ToArray());
    }

    [TestMethod]
    [DataRow(ReleaseCandidateMutation.ManifestPathEscape)]
    [DataRow(ReleaseCandidateMutation.WrongStaticId)]
    [DataRow(ReleaseCandidateMutation.ChangedCandidateAssembly)]
    [DataRow(ReleaseCandidateMutation.ChangedPrimaryOutputDigest)]
    [DataRow(ReleaseCandidateMutation.MismatchedReleaseContentDigest)]
    [DataRow(ReleaseCandidateMutation.UndeclaredPackageFile)]
    [DataRow(ReleaseCandidateMutation.PrimaryOutputPathMismatch)]
    [DataRow(ReleaseCandidateMutation.CandidateVersionMismatch)]
    [DataRow(ReleaseCandidateMutation.MissingPrimaryAssemblyFileVersion)]
    [DataRow(ReleaseCandidateMutation.NonCanonicalPrimaryAssemblyFileVersion)]
    [DataRow(ReleaseCandidateMutation.MissingPrimaryAssemblyTargetFrameworkName)]
    [DataRow(ReleaseCandidateMutation.WrongTargetFrameworkMoniker)]
    public void RequiredReleaseCandidateResolver_WhenCandidateBindingIsMutated_RejectsEvidence(
        ReleaseCandidateMutation mutation)
    {
        using var fixture = new ProvenanceBindingFixture();
        string candidateDirectory = fixture.CreateValidReleaseCandidate();
        fixture.ApplyReleaseCandidateMutation(mutation);
        PipelineProvenanceBoundAssemblyLocator locator =
            fixture.CreateLocator(
                buildResultPath: null,
                candidateDirectory);

        PipelineProvenanceBindingException exception =
            Assert.ThrowsExactly<PipelineProvenanceBindingException>(
                locator.ResolveRequiredReleaseCandidate);
        StringAssert.Contains(exception.Message, candidateDirectory);
    }

    public enum BuildResultMutation
    {
        WrongSourceCommit,
        ChangedSourceInput,
        ChangedPrimaryOutput,
        PrimaryOutputOutsideBuildOutput,
        MismatchedStaticIdRunRoot,
        AdditionalBuildOutput,
        EmptyInputFingerprint,
        MissingPrimaryAssemblyFileVersion,
        NonCanonicalPrimaryAssemblyFileVersion,
        MissingPrimaryAssemblyTargetFrameworkName,
        WrongPrimaryAssemblyTargetFrameworkMoniker
    }

    public enum ReleaseCandidateMutation
    {
        ManifestPathEscape,
        WrongStaticId,
        ChangedCandidateAssembly,
        ChangedPrimaryOutputDigest,
        MismatchedReleaseContentDigest,
        UndeclaredPackageFile,
        PrimaryOutputPathMismatch,
        CandidateVersionMismatch,
        MissingPrimaryAssemblyFileVersion,
        NonCanonicalPrimaryAssemblyFileVersion,
        MissingPrimaryAssemblyTargetFrameworkName,
        WrongTargetFrameworkMoniker
    }

    private sealed class ProvenanceBindingFixture : IDisposable
    {
        private const string BuildRunId =
            "20260830T1200000000000Z-0123456789abcdef0123456789abcdef";
        private const string CandidateRunId =
            "20260830T120000.0000000Z-0123456789abcdef";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly PipelineTestTemporaryDirectory temporaryDirectory =
            new();
        private string? buildResultPath;
        private string? buildSourceInputPath;
        private string? releaseContentManifestPath;
        private string? buildProvenancePath;
        private IReadOnlyList<FixtureReleaseContentEntry>? releaseEntries;

        internal ProvenanceBindingFixture()
        {
            RepositoryRoot = Path.Combine(
                temporaryDirectory.Path,
                "repository-under-test");
            ArtifactsRoot = Path.Combine(RepositoryRoot, "artifacts");
            Directory.CreateDirectory(RepositoryRoot);
            Directory.CreateDirectory(ArtifactsRoot);
        }

        internal string RepositoryRoot { get; }

        internal string ArtifactsRoot { get; }

        internal string BuildAssemblyPath { get; private set; } = string.Empty;

        internal string CandidateAssemblyPath { get; private set; } =
            string.Empty;

        internal PipelineProvenanceBoundAssemblyLocator CreateLocator(
            string? buildResultPath,
            string? candidateDirectory)
        {
            var environment = new Dictionary<string, string?>(
                StringComparer.Ordinal)
            {
                [PipelineProvenanceBoundAssemblyLocator
                    .BuildResultPathVariable] = buildResultPath,
                [PipelineProvenanceBoundAssemblyLocator
                    .ReleaseCandidateDirectoryVariable] = candidateDirectory
            };
            return new PipelineProvenanceBoundAssemblyLocator(
                RepositoryRoot,
                ArtifactsRoot,
                ExpectedStaticId,
                ExpectedRepositoryCommit,
                variableName => environment.TryGetValue(
                    variableName,
                    out string? value)
                        ? value
                        : null);
        }

        internal string CreateValidBuildResult()
        {
            string runRoot = Path.Combine(
                ArtifactsRoot,
                "builds",
                ExpectedStaticId,
                BuildRunId);
            string buildOutputDirectory = Path.Combine(runRoot, "output");
            Directory.CreateDirectory(buildOutputDirectory);
            BuildAssemblyPath = Path.Combine(
                buildOutputDirectory,
                "DeliveryTemperatureLimit.dll");
            File.WriteAllBytes(BuildAssemblyPath, [1, 3, 5, 7, 9]);

            buildSourceInputPath = Path.Combine(
                RepositoryRoot,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source",
                "FixtureSource.cs");
            Directory.CreateDirectory(
                Path.GetDirectoryName(buildSourceInputPath)!);
            File.WriteAllText(
                buildSourceInputPath,
                "internal sealed class FixtureSource { }\n");
            buildResultPath = Path.Combine(runRoot, "build-result.json");
            WriteJson(
                buildResultPath,
                new
                {
                    runRoot,
                    primaryOutputPath = BuildAssemblyPath,
                    inputs = new[] { Digest(buildSourceInputPath) },
                    outputs = new[] { Digest(BuildAssemblyPath) },
                    mergeInputs = Array.Empty<object>(),
                    gameReferences = Array.Empty<object>(),
                    sourceCommit = ExpectedRepositoryCommit,
                    releaseVersion = FixtureReleaseVersion,
                    dotnetSdkVersion = "10.0.400",
                    structuredBuildArguments = Array.Empty<string>(),
                    primaryAssemblyVersion = new
                    {
                        assemblyVersion = FixtureAssemblyFileVersion,
                        fileVersion = FixtureAssemblyFileVersion,
                        informationalVersion =
                            FixtureReleaseVersion +
                            "+0123456789ab.0123456789abcdef"
                    },
                    primaryAssemblyTargetFrameworkMoniker = "netstandard2.1",
                    primaryAssemblyTargetFrameworkName =
                        FixtureTargetFrameworkName,
                    sourceBytesUnchanged = true
                });
            return buildResultPath;
        }

        internal void ApplyBuildResultMutation(BuildResultMutation mutation)
        {
            Assert.IsNotNull(buildResultPath);
            Assert.IsNotNull(buildSourceInputPath);
            JsonObject document = ReadJsonObject(buildResultPath);
            switch (mutation)
            {
                case BuildResultMutation.WrongSourceCommit:
                    document["sourceCommit"] = new string('f', 40);
                    break;
                case BuildResultMutation.ChangedSourceInput:
                    File.AppendAllText(buildSourceInputPath, "// changed\n");
                    break;
                case BuildResultMutation.ChangedPrimaryOutput:
                    File.AppendAllText(BuildAssemblyPath, "changed");
                    break;
                case BuildResultMutation.PrimaryOutputOutsideBuildOutput:
                    document["primaryOutputPath"] = buildSourceInputPath;
                    break;
                case BuildResultMutation.MismatchedStaticIdRunRoot:
                    document["runRoot"] = Path.Combine(
                        ArtifactsRoot,
                        "builds",
                        "Another.Mod",
                        BuildRunId);
                    break;
                case BuildResultMutation.AdditionalBuildOutput:
                    string sidecarPath = Path.Combine(
                        Path.GetDirectoryName(BuildAssemblyPath)!,
                        "DeliveryTemperatureLimit.pdb");
                    File.WriteAllBytes(sidecarPath, [2, 4, 6, 8]);
                    document["outputs"]!.AsArray().Add(
                        JsonSerializer.SerializeToNode(
                            Digest(sidecarPath),
                            JsonOptions));
                    break;
                case BuildResultMutation.EmptyInputFingerprint:
                    document["inputs"] = new JsonArray();
                    break;
                case BuildResultMutation.MissingPrimaryAssemblyFileVersion:
                    document["primaryAssemblyVersion"]!
                        .AsObject()
                        .Remove("fileVersion");
                    break;
                case BuildResultMutation
                    .NonCanonicalPrimaryAssemblyFileVersion:
                    document["primaryAssemblyVersion"]!["fileVersion"] =
                        "2026.8.30";
                    break;
                case BuildResultMutation
                    .MissingPrimaryAssemblyTargetFrameworkName:
                    document.Remove("primaryAssemblyTargetFrameworkName");
                    break;
                case BuildResultMutation
                    .WrongPrimaryAssemblyTargetFrameworkMoniker:
                    document["primaryAssemblyTargetFrameworkMoniker"] = "net48";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            WriteJsonNode(buildResultPath, document);
        }

        internal string CreateValidReleaseCandidate()
        {
            string candidateDirectory = Path.Combine(
                ArtifactsRoot,
                "release-candidates",
                ExpectedStaticId,
                FixtureReleaseVersion,
                CandidateRunId);
            string workshopContentDirectory = Path.Combine(
                candidateDirectory,
                "workshop-content");
            string workshopListingDirectory = Path.Combine(
                candidateDirectory,
                "workshop-listing");
            string releaseEvidenceDirectory = Path.Combine(
                candidateDirectory,
                "release-evidence");
            Directory.CreateDirectory(workshopContentDirectory);
            Directory.CreateDirectory(workshopListingDirectory);
            Directory.CreateDirectory(releaseEvidenceDirectory);

            CandidateAssemblyPath = Path.Combine(
                workshopContentDirectory,
                "DeliveryTemperatureLimit.dll");
            File.WriteAllBytes(CandidateAssemblyPath, [2, 4, 6, 8, 10]);
            File.WriteAllText(
                Path.Combine(workshopContentDirectory, "mod.yaml"),
                "staticID: MaksymShostak.DeliveryTemperatureLimit\n");
            File.WriteAllText(
                Path.Combine(workshopContentDirectory, "mod_info.yaml"),
                "minimumSupportedBuild: 744825\n");
            File.WriteAllText(
                Path.Combine(workshopListingDirectory, "description.bbcode"),
                "Description\n");
            File.WriteAllText(
                Path.Combine(workshopListingDirectory, "change-notes.bbcode"),
                "Changes\n");
            File.WriteAllBytes(
                Path.Combine(workshopListingDirectory, "preview.png"),
                [0x89, 0x50, 0x4E, 0x47]);

            releaseEntries =
            [
                ManifestEntry(
                    "workshop-content",
                    "DeliveryTemperatureLimit.dll",
                    CandidateAssemblyPath,
                    "runtime"),
                ManifestEntry(
                    "workshop-content",
                    "mod.yaml",
                    Path.Combine(workshopContentDirectory, "mod.yaml"),
                    "runtime"),
                ManifestEntry(
                    "workshop-content",
                    "mod_info.yaml",
                    Path.Combine(workshopContentDirectory, "mod_info.yaml"),
                    "runtime"),
                ManifestEntry(
                    "workshop-listing",
                    "description.bbcode",
                    Path.Combine(
                        workshopListingDirectory,
                        "description.bbcode"),
                    "description"),
                ManifestEntry(
                    "workshop-listing",
                    "change-notes.bbcode",
                    Path.Combine(
                        workshopListingDirectory,
                        "change-notes.bbcode"),
                    "change-notes"),
                ManifestEntry(
                    "workshop-listing",
                    "preview.png",
                    Path.Combine(workshopListingDirectory, "preview.png"),
                    "preview")
            ];
            string releaseContentDigest =
                CalculateReleaseContentDigest(releaseEntries);
            releaseContentManifestPath = Path.Combine(
                releaseEvidenceDirectory,
                "release-content-manifest.json");
            WriteJson(
                releaseContentManifestPath,
                new
                {
                    schemaVersion = 1,
                    entries = releaseEntries,
                    contentDigest = releaseContentDigest
                });

            object primaryOutput = DigestWithPath(
                "${ARTIFACTS}/release-candidates/work/output/" +
                "DeliveryTemperatureLimit.dll",
                CandidateAssemblyPath);
            buildProvenancePath = Path.Combine(
                releaseEvidenceDirectory,
                "build-provenance.json");
            WriteJson(
                buildProvenancePath,
                new
                {
                    schemaVersion = 1,
                    profileSchemaVersion = 1,
                    staticId = ExpectedStaticId,
                    version = FixtureReleaseVersion,
                    repositoryCommit = ExpectedRepositoryCommit,
                    relevantPathsClean = true,
                    targetFramework = "netstandard2.1",
                    primaryAssemblyTargetFrameworkName =
                        FixtureTargetFrameworkName,
                    configuration = "Release",
                    artifactsDirectory = "${ARTIFACTS}",
                    buildOutputs = new[] { primaryOutput },
                    primaryOutput,
                    primaryAssemblyVersion = new
                    {
                        assemblyVersion = FixtureAssemblyFileVersion,
                        fileVersion = FixtureAssemblyFileVersion,
                        informationalVersion =
                            FixtureReleaseVersion +
                            "+0123456789ab.0123456789abcdef"
                    },
                    sourceBytesUnchanged = true,
                    releaseContentDigest
                });
            return candidateDirectory;
        }

        internal void ApplyReleaseCandidateMutation(
            ReleaseCandidateMutation mutation)
        {
            Assert.IsNotNull(releaseContentManifestPath);
            Assert.IsNotNull(buildProvenancePath);
            switch (mutation)
            {
                case ReleaseCandidateMutation.ManifestPathEscape:
                {
                    JsonObject manifest = ReadJsonObject(
                        releaseContentManifestPath);
                    JsonArray entries = manifest["entries"]!.AsArray();
                    entries[0]!["relativePath"] = "../escaped.dll";
                    WriteJsonNode(releaseContentManifestPath, manifest);
                    break;
                }
                case ReleaseCandidateMutation.WrongStaticId:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["staticId"] = "Another.Mod";
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation.ChangedCandidateAssembly:
                    File.AppendAllText(CandidateAssemblyPath, "changed");
                    break;
                case ReleaseCandidateMutation.ChangedPrimaryOutputDigest:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["primaryOutput"]!["sha256"] =
                        new string('0', 64);
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation.MismatchedReleaseContentDigest:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["releaseContentDigest"] = new string('0', 64);
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation.UndeclaredPackageFile:
                    File.WriteAllText(
                        Path.Combine(
                            Path.GetDirectoryName(CandidateAssemblyPath)!,
                            "undeclared-sidecar.dll"),
                        "undeclared");
                    break;
                case ReleaseCandidateMutation.PrimaryOutputPathMismatch:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["primaryOutput"]!["path"] =
                        "${ARTIFACTS}/another-output/DeliveryTemperatureLimit.dll";
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation.CandidateVersionMismatch:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["version"] = "2099.1.1";
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation
                    .MissingPrimaryAssemblyFileVersion:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["primaryAssemblyVersion"]!
                        .AsObject()
                        .Remove("fileVersion");
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation
                    .NonCanonicalPrimaryAssemblyFileVersion:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["primaryAssemblyVersion"]!["fileVersion"] =
                        "2026.8.30";
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation
                    .MissingPrimaryAssemblyTargetFrameworkName:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance.Remove("primaryAssemblyTargetFrameworkName");
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                case ReleaseCandidateMutation.WrongTargetFrameworkMoniker:
                {
                    JsonObject provenance = ReadJsonObject(buildProvenancePath);
                    provenance["targetFramework"] = "net48";
                    WriteJsonNode(buildProvenancePath, provenance);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        }

        public void Dispose() => temporaryDirectory.Dispose();

        private static FixtureReleaseContentEntry ManifestEntry(
            string contentArea,
            string relativePath,
            string sourcePath,
            string role)
        {
            FileInfo source = new(sourcePath);
            return new(
                contentArea,
                relativePath,
                source.Length,
                Sha256(sourcePath),
                role);
        }

        private static object Digest(string path) =>
            DigestWithPath(path, path);

        private static object DigestWithPath(
            string recordedPath,
            string sourcePath)
        {
            FileInfo source = new(sourcePath);
            return new
            {
                path = recordedPath,
                byteLength = source.Length,
                sha256 = Sha256(sourcePath)
            };
        }

        private static string Sha256(string path) =>
            Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(path)))
                .ToLowerInvariant();

        private static string CalculateReleaseContentDigest(
            IReadOnlyList<FixtureReleaseContentEntry> entries)
        {
            var canonicalText = new StringBuilder(
                "oni-release-content-manifest-v1\n");
            foreach (FixtureReleaseContentEntry entry in entries
                         .OrderBy(entry =>
                             entry.RelativePath,
                             StringComparer.Ordinal)
                         .ThenBy(entry =>
                             entry.ContentArea,
                             StringComparer.Ordinal))
            {
                canonicalText.Append(entry.ContentArea);
                canonicalText.Append('\0');
                canonicalText.Append(entry.RelativePath);
                canonicalText.Append('\0');
                canonicalText.Append(
                    entry.ByteLength.ToString(CultureInfo.InvariantCulture));
                canonicalText.Append('\0');
                canonicalText.Append(entry.Sha256);
                canonicalText.Append('\0');
                canonicalText.Append(entry.Role);
                canonicalText.Append('\n');
            }

            return Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(canonicalText.ToString())))
                .ToLowerInvariant();
        }

        private static JsonObject ReadJsonObject(string path) =>
            JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        private static void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(value, JsonOptions));
        }

        private static void WriteJsonNode(string path, JsonNode value) =>
            File.WriteAllText(path, value.ToJsonString(JsonOptions));
    }

    private sealed record FixtureReleaseContentEntry(
        string ContentArea,
        string RelativePath,
        long ByteLength,
        string Sha256,
        string Role);
}
