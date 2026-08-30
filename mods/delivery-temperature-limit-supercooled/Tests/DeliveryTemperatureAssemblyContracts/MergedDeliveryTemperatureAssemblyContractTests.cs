using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class MergedDeliveryTemperatureAssemblyContractTests
{
    private const string DeterministicBuildFixtureReleaseVersion = "2026.8.26";
    private const string DeterministicTestSourceCommit =
        "0123456789abcdef0123456789abcdef01234567";
    private const string FastTrackFixtureSha256 =
        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD";

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ProvenanceBoundAssemblyCases
    {
        get
        {
            foreach (DeliveryTemperatureArtifactContractCase contractCase in
                     DeliveryTemperatureArtifactContractCaseProvider
                         .ResolveForCurrentEnvironment())
            {
                yield return
                [
                    contractCase.ContractRowName,
                    contractCase.Kind.ToString(),
                    contractCase.AssemblyPath,
                    contractCase.AssemblySha256,
                    contractCase.AssemblyByteLength,
                    contractCase.ExpectedFileVersion,
                    contractCase.SourceCommit,
                    contractCase.ExpectedTargetFrameworkName
                ];
            }
        }
    }

    public static string FormatProvenanceBoundAssemblyCaseName(
        MethodInfo methodInfo,
        object[] data) =>
        $"{methodInfo.Name} ({data[0]})";

    [TestMethod]
    [DynamicData(
        nameof(ProvenanceBoundAssemblyCases),
        DynamicDataDisplayName = nameof(FormatProvenanceBoundAssemblyCaseName))]
    public async Task ProvenanceBoundAssembly_WhenContractRowIsEvaluated_HasRecordedRuntimeContract(
        string contractRow,
        string artifactKind,
        string assemblyPath,
        string expectedSha256,
        long expectedByteLength,
        string expectedFileVersion,
        string expectedSourceCommit,
        string expectedTargetFrameworkName)
    {
        TestContext.WriteLine($"Artifact contract row: {contractRow}");
        Assert.AreEqual(
            artifactKind,
            contractRow,
            "The display name and semantic artifact kind must remain identical.");

        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        Assert.IsTrue(
            File.Exists(assemblyPath),
            $"The {contractRow} assembly does not exist at {assemblyPath}.");
        var observedSha256 = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
        var observedByteLength = new FileInfo(assemblyPath).Length;
        var versionInformation = FileVersionInfo.GetVersionInfo(assemblyPath);
        var observedFileVersion = new Version(
            versionInformation.FileMajorPart,
            versionInformation.FileMinorPart,
            versionInformation.FileBuildPart,
            versionInformation.FilePrivatePart).ToString();

        Assert.AreEqual(
            expectedSha256,
            observedSha256,
            $"{contractRow} digest changed at {assemblyPath}.");
        Assert.AreEqual(
            expectedByteLength,
            observedByteLength,
            $"{contractRow} byte length changed at {assemblyPath}.");
        Assert.AreEqual(expectedFileVersion, observedFileVersion);
        await AssertGitObjectExistsAsync(repositoryRoot, expectedSourceCommit);
        Assert.AreEqual(
            expectedTargetFrameworkName,
            ReadTargetFrameworkName(assemblyPath),
            $"{contractRow} has the wrong TargetFrameworkAttribute.");
        AssertKnownFrameworkConflictRootsAreNotReferenced(assemblyPath);

        if (!string.Equals(
                artifactKind,
                nameof(DeliveryTemperatureArtifactContractKind.PublishedBaseline),
                StringComparison.Ordinal))
        {
            // The published DLL is deliberately retained as a behavioral and
            // metadata control. Only provenance-bound new artifacts are
            // required to expose the completed big-bang runtime architecture.
            IntentionalRuntimeContractTests.AssertMergedAssembly(assemblyPath);
            NoShimArchitectureContractTests.AssertMergedAssembly(assemblyPath);
            AssertPlibIsMergedRatherThanExternallyReferenced(assemblyPath);
            await AssertFastTrackFixtureIsNotContainedAsync(
                repositoryRoot,
                assemblyPath);
        }
    }

    [TestMethod]
    public async Task Build_WhenPipelinePropertiesAreProvided_DoesNotChangeModInfoBytes()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        var modRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled");
        var projectPath = Path.Combine(
            modRoot,
            "Source",
            "DeliveryTemperatureLimit.csproj");
        var modInfoPath = Path.Combine(modRoot, "mod_info.yaml");
        using var output = new PipelineTestTemporaryDirectory();
        var paths = CreateIsolatedBuildPaths(output.Path);
        var before = await File.ReadAllBytesAsync(modInfoPath);

        var restore = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "restore",
                projectPath,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}"
            ]);
        Assert.AreEqual(0, restore.ExitCode, restore.FormatEvidence());

        var mergedAssemblyPath = Path.Combine(
            output.Path,
            "DeliveryTemperatureLimit.dll");
        var build = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "build",
                projectPath,
                "--no-restore",
                "--configuration",
                "Release",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={mergedAssemblyPath}",
                $"-p:BaseOutputPath={paths.BaseOutputPath}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}"
            ]);

        Assert.AreEqual(0, build.ExitCode, build.FormatEvidence());
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(modInfoPath));
        Assert.IsTrue(File.Exists(mergedAssemblyPath));
        AssertKnownFrameworkConflictRootsAreNotReferenced(mergedAssemblyPath);
        IntentionalRuntimeContractTests.AssertMergedAssembly(mergedAssemblyPath);
        NoShimArchitectureContractTests.AssertMergedAssembly(mergedAssemblyPath);
    }

    [TestMethod]
    public async Task Build_WhenBuiltTwiceFromSameInputs_ProducesSameMergedAssemblyHash()
    {
        var repositoryRoot = RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = RequiredEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY");
        using var firstOutput = new PipelineTestTemporaryDirectory();
        using var secondOutput = new PipelineTestTemporaryDirectory();

        var firstAssembly = await BuildAssemblyAsync(
            repositoryRoot,
            managedDirectory,
            firstOutput);
        var secondAssembly = await BuildAssemblyAsync(
            repositoryRoot,
            managedDirectory,
            secondOutput);
        var firstHash = SHA256.HashData(await File.ReadAllBytesAsync(firstAssembly));
        var secondHash = SHA256.HashData(await File.ReadAllBytesAsync(secondAssembly));

        CollectionAssert.AreEqual(firstHash, secondHash);
    }

    private static async Task<string> BuildAssemblyAsync(
        string repositoryRoot,
        string managedDirectory,
        PipelineTestTemporaryDirectory output)
    {
        var projectPath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Source",
            "DeliveryTemperatureLimit.csproj");
        var mergedAssemblyPath = Path.Combine(
            output.Path,
            "DeliveryTemperatureLimit.dll");
        var paths = CreateIsolatedBuildPaths(output.Path);
        var restore = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "restore",
                projectPath,
                "--locked-mode",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}"
            ]);
        Assert.AreEqual(0, restore.ExitCode, restore.FormatEvidence());

        var build = await DotnetCommandRunner.RunAsync(
            repositoryRoot,
            [
                "build",
                projectPath,
                "--no-restore",
                "--configuration",
                "Release",
                $"-p:OniManagedAssemblyDirectory={managedDirectory}",
                $"-p:OniMergedModOutputPath={mergedAssemblyPath}",
                $"-p:BaseOutputPath={paths.BaseOutputPath}",
                $"-p:BaseIntermediateOutputPath={paths.BaseIntermediateOutputPath}",
                $"-p:MSBuildProjectExtensionsPath={paths.BaseIntermediateOutputPath}",
                $"-p:Version={DeterministicBuildFixtureReleaseVersion}",
                "-p:InformationalVersion=" +
                    $"{DeterministicBuildFixtureReleaseVersion}+" +
                    DeterministicTestSourceCommit[..12],
                "-p:Deterministic=true",
                "-p:ContinuousIntegrationBuild=true",
                $"-p:PathMap=\"{CreateDeterministicPathMap(output.Path, repositoryRoot)}\""
            ]);
        Assert.AreEqual(0, build.ExitCode, build.FormatEvidence());
        Assert.IsTrue(File.Exists(mergedAssemblyPath));
        AssertKnownFrameworkConflictRootsAreNotReferenced(mergedAssemblyPath);
        IntentionalRuntimeContractTests.AssertMergedAssembly(mergedAssemblyPath);
        NoShimArchitectureContractTests.AssertMergedAssembly(mergedAssemblyPath);
        return mergedAssemblyPath;
    }

    private static (string BaseOutputPath, string BaseIntermediateOutputPath)
        CreateIsolatedBuildPaths(string outputRoot)
    {
        var baseOutputPath = Path.Combine(
                outputRoot,
                "bin",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        var baseIntermediateOutputPath = Path.Combine(
                outputRoot,
                "obj",
                "$(MSBuildProjectName)") +
            Path.DirectorySeparatorChar;
        return (baseOutputPath, baseIntermediateOutputPath);
    }

    private static string CreateDeterministicPathMap(
        string outputRoot,
        string repositoryRoot) =>
        $"{EscapePathMapComponent(Path.GetFullPath(outputRoot))}=/_build/," +
        $"{EscapePathMapComponent(Path.GetFullPath(repositoryRoot))}=/_/";

    private static string EscapePathMapComponent(string value) =>
        value
            .Replace("=", "==", StringComparison.Ordinal)
            .Replace(",", ",,", StringComparison.Ordinal);

    private static void AssertKnownFrameworkConflictRootsAreNotReferenced(
        string assemblyPath)
    {
        var prohibitedReferences = new[]
        {
            "System.IO.Compression",
            "System.Net.Http"
        };
        var references = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyReferences(assemblyPath)
            .Select(reference => reference.Name)
            .ToArray();

        foreach (var prohibitedReference in prohibitedReferences)
        {
            CollectionAssert.DoesNotContain(
                references,
                prohibitedReference,
                $"{assemblyPath} must not directly reference {prohibitedReference}.");
        }
    }

    private static void AssertPlibIsMergedRatherThanExternallyReferenced(
        string assemblyPath)
    {
        // These two types are directly consumed by the mod and are stable,
        // semantically meaningful witnesses that the configured PLib input was
        // merged. A resource/string search would be too weak because it could
        // pass for an unresolvable external PLib reference.
        string[] requiredMergedPlibTypes =
        [
            "PeterHan.PLib.Core.PUtil",
            "PeterHan.PLib.Options.POptions"
        ];
        foreach (string requiredType in requiredMergedPlibTypes)
        {
            Assert.IsTrue(
                DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                    assemblyPath,
                    requiredType),
                $"Merged assembly {assemblyPath} is missing required PLib " +
                $"type {requiredType}.");
        }

        string[] references = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyReferences(assemblyPath)
            .Select(reference => reference.Name)
            .ToArray();
        CollectionAssert.DoesNotContain(
            references,
            "PLib",
            "PLib must be merged and must not remain a runtime package dependency.");
    }

    private static async Task AssertFastTrackFixtureIsNotContainedAsync(
        string repositoryRoot,
        string assemblyPath)
    {
        string fixturePath = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Tests",
            "Fixtures",
            "ThirdParty",
            "FastTrack",
            "0.18.4.0",
            "FastTrack.dll");
        byte[] fixtureBytes = await File.ReadAllBytesAsync(fixturePath);
        Assert.AreEqual(
            FastTrackFixtureSha256,
            Convert.ToHexString(SHA256.HashData(fixtureBytes)),
            "The FastTrack exclusion check must remain bound to the reviewed " +
            "0.18.4.0 static-contract fixture.");

        byte[] assemblyBytes = await File.ReadAllBytesAsync(assemblyPath);
        Assert.AreEqual(
            -1,
            assemblyBytes.AsSpan().IndexOf(fixtureBytes),
            $"Merged assembly {assemblyPath} contains the complete FastTrack " +
            "fixture byte sequence.");
        AssertEncodedTextIsAbsent(
            assemblyBytes,
            FastTrackFixtureSha256,
            assemblyPath);
        AssertEncodedTextIsAbsent(
            assemblyBytes,
            FastTrackFixtureSha256.ToLowerInvariant(),
            assemblyPath);

        string[] assemblyReferences = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyReferences(assemblyPath)
            .Select(reference => reference.Name)
            .ToArray();
        Assert.IsFalse(
            assemblyReferences.Any(reference => reference.Contains(
                "FastTrack",
                StringComparison.OrdinalIgnoreCase)),
            $"Merged assembly {assemblyPath} retains a FastTrack compile/runtime " +
            "assembly reference.");
        Assert.IsFalse(
            ReadManifestResourceNames(assemblyPath).Any(resourceName =>
                resourceName.Contains(
                    "FastTrack",
                    StringComparison.OrdinalIgnoreCase)),
            $"Merged assembly {assemblyPath} embeds a FastTrack-named resource.");
    }

    private static void AssertEncodedTextIsAbsent(
        byte[] assemblyBytes,
        string prohibitedText,
        string assemblyPath)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(prohibitedText);
        byte[] utf16Bytes = Encoding.Unicode.GetBytes(prohibitedText);
        Assert.AreEqual(
            -1,
            assemblyBytes.AsSpan().IndexOf(utf8Bytes),
            $"Merged assembly {assemblyPath} contains prohibited UTF-8 text " +
            $"'{prohibitedText}'.");
        Assert.AreEqual(
            -1,
            assemblyBytes.AsSpan().IndexOf(utf16Bytes),
            $"Merged assembly {assemblyPath} contains prohibited UTF-16 text " +
            $"'{prohibitedText}'.");
    }

    private static string ReadTargetFrameworkName(string assemblyPath)
    {
        using var stream = new FileStream(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var peReader = new PEReader(stream);
        MetadataReader metadata = peReader.GetMetadataReader();
        var observedFrameworkNames = new List<string>();
        foreach (CustomAttributeHandle handle in
                 metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attribute = metadata.GetCustomAttribute(handle);
            if (!IsTargetFrameworkAttribute(metadata, attribute.Constructor))
            {
                continue;
            }

            BlobReader value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() != 1)
            {
                throw new InvalidDataException(
                    $"TargetFrameworkAttribute in {assemblyPath} has an " +
                    "invalid custom-attribute prolog.");
            }

            string? frameworkName = value.ReadSerializedString();
            if (string.IsNullOrWhiteSpace(frameworkName))
            {
                throw new InvalidDataException(
                    $"TargetFrameworkAttribute in {assemblyPath} has no " +
                    "framework name.");
            }

            observedFrameworkNames.Add(frameworkName);
        }

        Assert.HasCount(
            1,
            observedFrameworkNames,
            $"Assembly {assemblyPath} must declare exactly one " +
            "TargetFrameworkAttribute.");
        return observedFrameworkNames[0];
    }

    private static bool IsTargetFrameworkAttribute(
        MetadataReader metadata,
        EntityHandle constructor)
    {
        EntityHandle declaringType = constructor.Kind switch
        {
            HandleKind.MemberReference => metadata
                .GetMemberReference((MemberReferenceHandle)constructor)
                .Parent,
            HandleKind.MethodDefinition => metadata
                .GetMethodDefinition((MethodDefinitionHandle)constructor)
                .GetDeclaringType(),
            _ => default
        };

        return declaringType.Kind switch
        {
            HandleKind.TypeReference => IsTargetFrameworkTypeReference(
                metadata,
                (TypeReferenceHandle)declaringType),
            HandleKind.TypeDefinition => IsTargetFrameworkTypeDefinition(
                metadata,
                (TypeDefinitionHandle)declaringType),
            _ => false
        };
    }

    private static bool IsTargetFrameworkTypeReference(
        MetadataReader metadata,
        TypeReferenceHandle handle)
    {
        TypeReference type = metadata.GetTypeReference(handle);
        return string.Equals(
                metadata.GetString(type.Namespace),
                "System.Runtime.Versioning",
                StringComparison.Ordinal) &&
            string.Equals(
                metadata.GetString(type.Name),
                "TargetFrameworkAttribute",
                StringComparison.Ordinal);
    }

    private static bool IsTargetFrameworkTypeDefinition(
        MetadataReader metadata,
        TypeDefinitionHandle handle)
    {
        TypeDefinition type = metadata.GetTypeDefinition(handle);
        return string.Equals(
                metadata.GetString(type.Namespace),
                "System.Runtime.Versioning",
                StringComparison.Ordinal) &&
            string.Equals(
                metadata.GetString(type.Name),
                "TargetFrameworkAttribute",
                StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReadManifestResourceNames(
        string assemblyPath)
    {
        using var stream = new FileStream(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var peReader = new PEReader(stream);
        MetadataReader metadata = peReader.GetMetadataReader();
        return metadata.ManifestResources
            .Select(handle => metadata.GetString(
                metadata.GetManifestResource(handle).Name))
            .ToArray();
    }

    private static async Task AssertGitObjectExistsAsync(
        string repositoryRoot,
        string objectId)
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
        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"{objectId}^{{commit}}");

        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start(), "git cat-file did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"Recorded source commit {objectId} is unavailable. " +
            $"Standard output: {await standardOutput}. Standard error: {await standardError}.");
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(value),
            $"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value;
    }
}
