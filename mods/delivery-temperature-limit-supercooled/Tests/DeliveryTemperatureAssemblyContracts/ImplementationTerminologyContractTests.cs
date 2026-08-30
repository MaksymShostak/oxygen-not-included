#nullable enable

using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

/// <summary>
/// Keeps content mode and implementation path as two independent concepts. The
/// forbidden token is assembled at runtime so this contract does not fail on its
/// own diagnostic vocabulary.
/// </summary>
[TestClass]
public sealed partial class ImplementationTerminologyContractTests
{
    private static readonly string AmbiguousUnqualifiedTerm =
        string.Concat("vani", "lla");

    [TestMethod]
    public void ImplementationArtifacts_WhenTerminologyIsInspected_ContainOnlyQualifiedContentAndPathNames()
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
            Assert.IsFalse(
                relativePath.Contains(
                    AmbiguousUnqualifiedTerm,
                    StringComparison.OrdinalIgnoreCase),
                $"Ambiguous content/implementation terminology appears in path {relativePath}.");

            string[] sourceLines = File.ReadAllLines(sourcePath);
            for (int lineIndex = 0; lineIndex < sourceLines.Length; lineIndex++)
            {
                string sourceLine = sourceLines[lineIndex];
                Assert.IsFalse(
                    AmbiguousWordRegex().IsMatch(sourceLine),
                    $"Ambiguous unqualified terminology appears at " +
                    $"{relativePath}:{lineIndex + 1}: {sourceLine.Trim()}");
                Assert.IsFalse(
                    AmbiguousTypeIdentifierRegex().IsMatch(sourceLine),
                    $"Ambiguous type identifier appears at " +
                    $"{relativePath}:{lineIndex + 1}: {sourceLine.Trim()}");
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
