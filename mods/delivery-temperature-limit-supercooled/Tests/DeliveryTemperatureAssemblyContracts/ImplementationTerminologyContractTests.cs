#nullable enable

using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed partial class ImplementationTerminologyContractTests
{
    private static readonly string AmbiguousUnqualifiedTerm =
        string.Concat("vani", "lla");

    [Test]
    public async Task ImplementationArtifacts_WhenTerminologyIsInspected_ContainOnlyQualifiedContentAndPathNames()
    {
        string repositoryRoot = ResolveRepositoryRoot();
        string modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        string[] inspectedRoots =
        [
            Path.Combine(modRoot, "Source"),
            Path.Combine(modRoot, "Tests")
        ];
        string[] sourcePaths = inspectedRoots
            .SelectMany(root => Directory.EnumerateFiles(
                root,
                "*.cs",
                SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (string sourcePath in sourcePaths)
        {
            string relativePath = Path.GetRelativePath(modRoot, sourcePath);
            await Assert.That(relativePath.Contains(
                    AmbiguousUnqualifiedTerm,
                    StringComparison.OrdinalIgnoreCase)).IsFalse().Because($"Ambiguous content/implementation terminology appears in path {relativePath}.");

            string[] sourceLines = File.ReadAllLines(sourcePath);
            for (int lineIndex = 0; lineIndex < sourceLines.Length; lineIndex++)
            {
                string sourceLine = sourceLines[lineIndex];
                await Assert.That(AmbiguousWordRegex().IsMatch(sourceLine)).IsFalse().Because($"Ambiguous unqualified terminology appears at " +
                    $"{relativePath}:{lineIndex + 1}: {sourceLine.Trim()}");
                await Assert.That(AmbiguousTypeIdentifierRegex().IsMatch(sourceLine)).IsFalse().Because($"Ambiguous type identifier appears at " +
                    $"{relativePath}:{lineIndex + 1}: {sourceLine.Trim()}");
            }
        }
    }

    [Test]
    public async Task GenericSelectionBoundaries_WhenSourceIsInspected_ContainNoProviderOrGameRuntimeDependencies()
    {
        string repositoryRoot = ResolveRepositoryRoot();
        string sourceRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source");
        string gameplayActivationCore = Path.Combine(
            sourceRoot,
            "GameplayActivation",
            "Core");
        string runtimePlanPath = Path.Combine(
            sourceRoot,
            "RuntimePatchInstallation",
            "DeliveryTemperatureRuntimePatchPlan.cs");
        string[] inspectedPaths = Directory
            .EnumerateFiles(
                gameplayActivationCore,
                "*.cs",
                SearchOption.AllDirectories)
            .Append(runtimePlanPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] forbiddenTokens =
        [
            "FastTrackCompatibilityReport",
            "FastTrackFeature",
            "PeterHan.FastTrack",
            "BlueprintsV2",
            "KMod.Mod",
            "HarmonyLib",
            "UnityEngine"
        ];

        foreach (string inspectedPath in inspectedPaths)
        {
            string source = File.ReadAllText(inspectedPath);
            foreach (string forbiddenToken in forbiddenTokens)
            {
                await Assert.That(source.Contains(forbiddenToken, StringComparison.Ordinal)).IsFalse().Because($"Generic selection boundary {inspectedPath} contains " +
                    $"provider/runtime token '{forbiddenToken}'.");
            }
        }
    }

    private static Regex AmbiguousWordRegex() => new(
        "\\b" + Regex.Escape(AmbiguousUnqualifiedTerm) + "\\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static Regex AmbiguousTypeIdentifierRegex() => new(
        "\\b(?:Non" + Regex.Escape(AmbiguousUnqualifiedTerm) +
        "[A-Za-z0-9_]*|" + Regex.Escape(AmbiguousUnqualifiedTerm) +
        "[A-Za-z0-9_]*)\\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string ResolveRepositoryRoot()
    {
        string? pipelineRepositoryRoot = Environment.GetEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(pipelineRepositoryRoot))
        {
            return pipelineRepositoryRoot;
        }

        DirectoryInfo? candidateDirectory = new(AppContext.BaseDirectory);
        while (candidateDirectory is not null)
        {
            string projectPath = Path.Combine(
                candidateDirectory.FullName,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Tests",
                "DeliveryTemperatureLimit.Tests.csproj");
            if (File.Exists(projectPath))
            {
                return candidateDirectory.FullName;
            }

            candidateDirectory = candidateDirectory.Parent;
        }

        throw new InvalidOperationException(
            "The repository root was not supplied and could not be resolved " +
            $"from {AppContext.BaseDirectory}.");
    }
}
