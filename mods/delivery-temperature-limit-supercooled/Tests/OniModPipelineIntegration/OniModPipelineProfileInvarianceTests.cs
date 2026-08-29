using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

internal sealed record OniModPipelinePackageFileContract(
    string Source,
    string Destination);

internal sealed record OniModPipelineProfileContract(
    string BuildEntryPoint,
    string BuildConfiguration,
    string ManagedDirectoryProperty,
    string PrimaryOutput,
    IReadOnlyList<string> MergeInputs,
    IReadOnlyList<OniModPipelinePackageFileContract> PackageFiles,
    string LocalInstallDirectory,
    IReadOnlyList<string> RequiredTestProjectPaths);

internal static partial class OniModPipelineProfileContractReader
{
    internal static OniModPipelineProfileContract Read(string profilePath)
    {
        var lines = File.ReadAllLines(profilePath);
        var section = string.Empty;
        var buildValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var packageFiles = new List<OniModPipelinePackageFileContract>();
        var localInstallValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var requiredTestProjectPaths = new List<string>();
        string? pendingPackageSource = null;
        string? pendingTestProjectPath = null;
        var pendingTestProjectRequired = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                if (section == "package-files" && pendingPackageSource is not null)
                {
                    throw new InvalidDataException(
                        "A package-files table ended without a destination.");
                }

                if (section == "test-projects"
                    && pendingTestProjectPath is not null
                    && pendingTestProjectRequired)
                {
                    requiredTestProjectPaths.Add(pendingTestProjectPath);
                }

                section = line.Trim('[', ']');
                pendingPackageSource = null;
                pendingTestProjectPath = null;
                pendingTestProjectRequired = false;
                continue;
            }

            var assignment = AssignmentPattern().Match(line);
            if (!assignment.Success)
            {
                continue;
            }

            var key = assignment.Groups["key"].Value;
            var rawValue = assignment.Groups["value"].Value.Trim();
            switch (section)
            {
                case "build":
                    buildValues.Add(key, rawValue);
                    break;
                case "package-files" when key == "source":
                    pendingPackageSource = ReadQuotedValue(rawValue);
                    break;
                case "package-files" when key == "destination":
                    if (pendingPackageSource is null)
                    {
                        throw new InvalidDataException(
                            "A package destination appeared before its source.");
                    }

                    packageFiles.Add(new(
                        pendingPackageSource,
                        ReadQuotedValue(rawValue)));
                    pendingPackageSource = null;
                    break;
                case "local-install":
                    localInstallValues.Add(key, ReadQuotedValue(rawValue));
                    break;
                case "test-projects" when key == "path":
                    pendingTestProjectPath = ReadQuotedValue(rawValue);
                    break;
                case "test-projects" when key == "required":
                    pendingTestProjectRequired = bool.Parse(rawValue);
                    break;
            }
        }

        if (section == "test-projects"
            && pendingTestProjectPath is not null
            && pendingTestProjectRequired)
        {
            requiredTestProjectPaths.Add(pendingTestProjectPath);
        }

        return new(
            ReadQuotedBuildValue(buildValues, "entry-point"),
            ReadQuotedBuildValue(buildValues, "configuration"),
            ReadQuotedBuildValue(buildValues, "game-managed-directory-property"),
            ReadQuotedBuildValue(buildValues, "primary-output"),
            ReadQuotedArray(buildValues["merge-inputs"]),
            packageFiles,
            localInstallValues["directory-name"],
            requiredTestProjectPaths);
    }

    private static string ReadQuotedBuildValue(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        ReadQuotedValue(values[key]);

    private static string ReadQuotedValue(string rawValue)
    {
        var match = QuotedValuePattern().Match(rawValue);
        if (!match.Success)
        {
            throw new InvalidDataException(
                $"Expected one TOML basic string, observed: {rawValue}");
        }

        return match.Groups["value"].Value;
    }

    private static IReadOnlyList<string> ReadQuotedArray(string rawValue)
    {
        var match = QuotedArrayPattern().Match(rawValue);
        if (!match.Success)
        {
            throw new InvalidDataException(
                $"Expected one single-line TOML string array, observed: {rawValue}");
        }

        var body = match.Groups["body"].Value;
        return QuotedArrayItemPattern()
            .Matches(body)
            .Select(item => item.Groups["value"].Value)
            .ToArray();
    }

    [GeneratedRegex(
        @"^(?<key>[A-Za-z0-9-]+)\s*=\s*(?<value>.+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AssignmentPattern();

    [GeneratedRegex(
        "^\\\"(?<value>[^\\\"]*)\\\"$",
        RegexOptions.CultureInvariant)]
    private static partial Regex QuotedValuePattern();

    [GeneratedRegex(
        @"^\[(?<body>.*)\]$",
        RegexOptions.CultureInvariant)]
    private static partial Regex QuotedArrayPattern();

    [GeneratedRegex(
        "\\\"(?<value>[^\\\"]*)\\\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex QuotedArrayItemPattern();
}

[TestClass]
public sealed class OniModPipelineProfileInvarianceTests
{
    private const int ExpectedProfileByteLength = 5413;
    private const string ExpectedProfileSha256 =
        "5A03C7656F75B539B226C1CD6FF231D85C7DE200E701B5274751F09F00739AFD";

    [TestMethod]
    public async Task ProfileBytes_WhenComparedWithTaskZeroEvidence_AreUnchanged()
    {
        var profilePath = ProfilePath();
        var bytes = await File.ReadAllBytesAsync(profilePath);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));

        Assert.AreEqual(
            ExpectedProfileByteLength,
            bytes.Length,
            $"oni-mod-pipeline.toml byte length changed at {profilePath}.");
        Assert.AreEqual(
            ExpectedProfileSha256,
            digest,
            $"oni-mod-pipeline.toml bytes changed at {profilePath}.");
    }

    [TestMethod]
    public void Profile_WhenParsed_RetainsAuthoritativeDevelopmentPipelineContract()
    {
        var profile = OniModPipelineProfileContractReader.Read(ProfilePath());

        Assert.AreEqual(
            "Source/DeliveryTemperatureLimit.csproj",
            profile.BuildEntryPoint);
        Assert.AreEqual("Release", profile.BuildConfiguration);
        Assert.AreEqual(
            "OniManagedAssemblyDirectory",
            profile.ManagedDirectoryProperty);
        Assert.AreEqual(
            "{build-output}/DeliveryTemperatureLimit.dll",
            profile.PrimaryOutput);
        CollectionAssert.AreEqual(new[] { "PLib" }, profile.MergeInputs.ToArray());
        Assert.AreEqual("DeliveryTemperatureLimit", profile.LocalInstallDirectory);
        CollectionAssert.AreEqual(
            new[] { "Tests/DeliveryTemperatureLimit.Tests.csproj" },
            profile.RequiredTestProjectPaths.ToArray());
    }

    private static string ProfilePath() =>
        Path.Combine(
            RequiredEnvironmentVariable("ONI_MOD_PIPELINE_REPOSITORY_ROOT"),
            "mods",
            "delivery-temperature-limit-supercooled",
            "oni-mod-pipeline.toml");

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }
}
