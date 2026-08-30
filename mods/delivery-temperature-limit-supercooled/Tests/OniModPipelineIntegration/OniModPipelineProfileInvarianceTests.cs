using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

internal sealed record OniModPipelinePackageFileContract(
    string Source,
    string Destination);

internal sealed record OniModPipelineTestProjectContract(
    string Id,
    string Path,
    bool Required);

internal sealed record OniModPipelineProfileContract(
    string BuildEntryPoint,
    string BuildConfiguration,
    string ManagedDirectoryProperty,
    string PrimaryOutput,
    IReadOnlyList<string> MergeInputs,
    IReadOnlyList<OniModPipelinePackageFileContract> PackageFiles,
    string LocalInstallDirectory,
    IReadOnlyList<OniModPipelineTestProjectContract> TestProjects,
    IReadOnlyList<string> RequiredAcceptanceCheckIds);

internal static partial class OniModPipelineProfileContractReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static OniModPipelineProfileContract Read(string profilePath) =>
        Read(File.ReadAllBytes(profilePath));

    internal static OniModPipelineProfileContract Read(byte[] profileBytes)
    {
        ArgumentNullException.ThrowIfNull(profileBytes);
        string profileText;
        try
        {
            profileText = StrictUtf8.GetString(profileBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The ONI mod pipeline profile must contain strict UTF-8 text.",
                exception);
        }

        var lines = profileText.Split('\n');
        var section = string.Empty;
        var buildValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var packageFiles = new List<OniModPipelinePackageFileContract>();
        var localInstallValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var testProjects = new List<OniModPipelineTestProjectContract>();
        var requiredAcceptanceCheckIds = new List<string>();
        string? pendingPackageSource = null;
        string? pendingTestProjectId = null;
        string? pendingTestProjectPath = null;
        var pendingTestProjectRequired = false;
        string? pendingAcceptanceCheckId = null;
        var pendingAcceptanceCheckRequired = false;

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

                if (section == "test-projects")
                {
                    AddPendingTestProject(
                        testProjects,
                        pendingTestProjectId,
                        pendingTestProjectPath,
                        pendingTestProjectRequired);
                }

                if (section == "acceptance-checks")
                {
                    AddPendingAcceptanceCheck(
                        requiredAcceptanceCheckIds,
                        pendingAcceptanceCheckId,
                        pendingAcceptanceCheckRequired);
                }

                section = line.Trim('[', ']');
                pendingPackageSource = null;
                pendingTestProjectId = null;
                pendingTestProjectPath = null;
                pendingTestProjectRequired = false;
                pendingAcceptanceCheckId = null;
                pendingAcceptanceCheckRequired = false;
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
                case "test-projects" when key == "id":
                    pendingTestProjectId = ReadQuotedValue(rawValue);
                    break;
                case "test-projects" when key == "path":
                    pendingTestProjectPath = ReadQuotedValue(rawValue);
                    break;
                case "test-projects" when key == "required":
                    pendingTestProjectRequired = bool.Parse(rawValue);
                    break;
                case "acceptance-checks" when key == "id":
                    pendingAcceptanceCheckId = ReadQuotedValue(rawValue);
                    break;
                case "acceptance-checks" when key == "required":
                    pendingAcceptanceCheckRequired = bool.Parse(rawValue);
                    break;
            }
        }

        if (section == "test-projects")
        {
            AddPendingTestProject(
                testProjects,
                pendingTestProjectId,
                pendingTestProjectPath,
                pendingTestProjectRequired);
        }

        if (section == "acceptance-checks")
        {
            AddPendingAcceptanceCheck(
                requiredAcceptanceCheckIds,
                pendingAcceptanceCheckId,
                pendingAcceptanceCheckRequired);
        }

        return new(
            ReadQuotedBuildValue(buildValues, "entry-point"),
            ReadQuotedBuildValue(buildValues, "configuration"),
            ReadQuotedBuildValue(buildValues, "game-managed-directory-property"),
            ReadQuotedBuildValue(buildValues, "primary-output"),
            ReadQuotedArray(buildValues["merge-inputs"]),
            packageFiles,
            localInstallValues["directory-name"],
            testProjects,
            requiredAcceptanceCheckIds);
    }

    private static void AddPendingTestProject(
        ICollection<OniModPipelineTestProjectContract> testProjects,
        string? id,
        string? path,
        bool required)
    {
        if (id is null || path is null)
        {
            throw new InvalidDataException(
                "Every test-projects table must declare both id and path.");
        }

        testProjects.Add(new(id, path, required));
    }

    private static void AddPendingAcceptanceCheck(
        ICollection<string> requiredAcceptanceCheckIds,
        string? id,
        bool required)
    {
        if (id is null)
        {
            throw new InvalidDataException(
                "Every acceptance-checks table must declare an id.");
        }

        if (required)
        {
            requiredAcceptanceCheckIds.Add(id);
        }
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
    private const string TaskZeroEvidenceCommit =
        "fb5729cd28f2922b39d2fca3979818e219dee871";
    private const int ExpectedProfileByteLength = 5413;
    private const string ExpectedProfileSha256 =
        "5A03C7656F75B539B226C1CD6FF231D85C7DE200E701B5274751F09F00739AFD";

    [TestMethod]
    public async Task ProfileBytes_WhenComparedWithTaskZeroEvidence_AreUnchanged()
    {
        var profilePath = ProfilePath();
        var bytes = await File.ReadAllBytesAsync(profilePath);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        var taskZeroBytes = await ReadTaskZeroProfileBytesAsync();

        Assert.AreEqual(
            ExpectedProfileByteLength,
            taskZeroBytes.Length,
            $"Task 0 profile evidence at {TaskZeroEvidenceCommit} has an " +
            "unexpected byte length.");
        Assert.AreEqual(
            ExpectedProfileSha256,
            Convert.ToHexString(SHA256.HashData(taskZeroBytes)),
            $"Task 0 profile evidence at {TaskZeroEvidenceCommit} has an " +
            "unexpected digest.");

        Assert.AreEqual(
            ExpectedProfileByteLength,
            bytes.Length,
            $"oni-mod-pipeline.toml byte length changed at {profilePath}.");
        Assert.AreEqual(
            ExpectedProfileSha256,
            digest,
            $"oni-mod-pipeline.toml bytes changed at {profilePath}.");
        CollectionAssert.AreEqual(
            taskZeroBytes,
            bytes,
            $"oni-mod-pipeline.toml byte sequence differs from Task 0 commit " +
            $"{TaskZeroEvidenceCommit} at {profilePath}.");
    }

    [TestMethod]
    public void Profile_WhenParsed_RetainsAuthoritativeDevelopmentPipelineContract()
    {
        byte[] semanticInputCopy = File.ReadAllBytes(ProfilePath()).ToArray();
        var profile = OniModPipelineProfileContractReader.Read(
            semanticInputCopy);

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
        CollectionAssert.AreEqual(
            new[]
            {
                new OniModPipelinePackageFileContract("mod.yaml", "mod.yaml"),
                new OniModPipelinePackageFileContract(
                    "mod_info.yaml",
                    "mod_info.yaml"),
                new OniModPipelinePackageFileContract(
                    "{build-output}/DeliveryTemperatureLimit.dll",
                    "DeliveryTemperatureLimit.dll")
            },
            profile.PackageFiles.ToArray());
        Assert.AreEqual("DeliveryTemperatureLimit", profile.LocalInstallDirectory);
        CollectionAssert.AreEqual(
            new[]
            {
                new OniModPipelineTestProjectContract(
                    "delivery-temperature-limit-regressions",
                    "Tests/DeliveryTemperatureLimit.Tests.csproj",
                    Required: true)
            },
            profile.TestProjects.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "storage-bin-temperature-filter",
                "storage-tile-rocket-temperature-filter",
                "construction-temperature-filter",
                "temperature-side-screen-editing",
                "temperature-side-screen-keyboard",
                "save-load-temperature-limits",
                "delivery-temperature-log-review",
                "workshop-description-uploader-line-structure"
            },
            profile.RequiredAcceptanceCheckIds.ToArray());
    }

    private static async Task<byte[]> ReadTaskZeroProfileBytesAsync()
    {
        string repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("blob");
        startInfo.ArgumentList.Add(
            TaskZeroEvidenceCommit +
            ":mods/delivery-temperature-limit-supercooled/" +
            "oni-mod-pipeline.toml");

        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start(), "git cat-file did not start.");
        await using var profileBytes = new MemoryStream();
        Task copyOutput = process.StandardOutput.BaseStream.CopyToAsync(
            profileBytes);
        Task<string> readError = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(copyOutput, process.WaitForExitAsync());
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"Task 0 profile evidence could not be read from commit " +
            $"{TaskZeroEvidenceCommit}. Standard error: {await readError}");
        return profileBytes.ToArray();
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
