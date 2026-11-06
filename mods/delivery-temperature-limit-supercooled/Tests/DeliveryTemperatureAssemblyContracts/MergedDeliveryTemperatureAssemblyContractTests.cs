using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class MergedDeliveryTemperatureAssemblyContractTests
{
    private const string DeterministicBuildFixtureReleaseVersion = "2026.8.26";
    private const string DeterministicTestSourceCommit =
        "0123456789abcdef0123456789abcdef01234567";

    public static IEnumerable<(string ContractRow, string ArtifactKind,
        string AssemblyPath, string AssemblySha256, long AssemblyByteLength,
        string ExpectedFileVersion, string SourceCommit,
        string ExpectedTargetFrameworkName)> ProvenanceBoundAssemblyCases
    {
        get
        {
            foreach (DeliveryTemperatureArtifactContractCase contractCase in
                     DeliveryTemperatureArtifactContractCaseProvider
                         .ResolveForCurrentEnvironment())
            {
                yield return
                (
                    contractCase.ContractRowName,
                    contractCase.Kind.ToString(),
                    contractCase.AssemblyPath,
                    contractCase.AssemblySha256,
                    contractCase.AssemblyByteLength,
                    contractCase.ExpectedFileVersion,
                    contractCase.SourceCommit,
                    contractCase.ExpectedTargetFrameworkName
                );
            }
        }
    }

    [Test]
    [MethodDataSource(nameof(ProvenanceBoundAssemblyCases))]
    [DisplayName($"{nameof(ProvenanceBoundAssembly_WhenContractRowIsEvaluated_HasRecordedRuntimeContract)} ($contractRow)")]
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
        Console.WriteLine($"Artifact contract row: {contractRow}");
        await Assert.That(contractRow).IsEqualTo(artifactKind).Because("The display name and semantic artifact kind must remain identical.");

        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        await Assert.That(File.Exists(assemblyPath)).IsTrue().Because($"The {contractRow} assembly does not exist at {assemblyPath}.");
        var observedSha256 = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
        var observedByteLength = new FileInfo(assemblyPath).Length;
        var versionInformation = FileVersionInfo.GetVersionInfo(assemblyPath);
        var observedFileVersion = new Version(
            versionInformation.FileMajorPart,
            versionInformation.FileMinorPart,
            versionInformation.FileBuildPart,
            versionInformation.FilePrivatePart).ToString();

        await Assert.That(observedSha256).IsEqualTo(expectedSha256).Because($"{contractRow} digest changed at {assemblyPath}.");
        await Assert.That(observedByteLength).IsEqualTo(expectedByteLength).Because($"{contractRow} byte length changed at {assemblyPath}.");
        await Assert.That(observedFileVersion).IsEqualTo(expectedFileVersion);
        await AssertGitObjectExistsAsync(repositoryRoot, expectedSourceCommit);
        await Assert.That((await ReadTargetFrameworkName(assemblyPath))).IsEqualTo(expectedTargetFrameworkName).Because($"{contractRow} has the wrong TargetFrameworkAttribute.");
        await AssertKnownFrameworkConflictRootsAreNotReferenced(assemblyPath);

        if (!string.Equals(
                artifactKind,
                nameof(DeliveryTemperatureArtifactContractKind.PublishedBaseline),
                StringComparison.Ordinal))
        {
            // The published DLL is deliberately retained as a behavioral and
            // metadata control. Only provenance-bound new artifacts are
            // required to expose the completed big-bang runtime architecture.
            await IntentionalRuntimeContractTests.AssertMergedAssembly(assemblyPath);
            await NoShimArchitectureContractTests.AssertMergedAssembly(assemblyPath);
            await AssertPlibIsMergedRatherThanExternallyReferenced(assemblyPath);
            await AssertSupportedFastTrackFixturesAreNotContainedAsync(
                repositoryRoot,
                assemblyPath);
        }
    }

    [Test]
    public async Task Build_WhenPipelinePropertiesAreProvided_DoesNotChangeModInfoBytes()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = await RequiredEnvironmentVariable(
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
        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.FormatEvidence());

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

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.FormatEvidence());
        await Assert.That(await File.ReadAllBytesAsync(modInfoPath)).IsEquivalentTo(before, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(File.Exists(mergedAssemblyPath)).IsTrue();
        await AssertKnownFrameworkConflictRootsAreNotReferenced(mergedAssemblyPath);
        await IntentionalRuntimeContractTests.AssertMergedAssembly(mergedAssemblyPath);
        await NoShimArchitectureContractTests.AssertMergedAssembly(mergedAssemblyPath);
    }

    [Test]
    public async Task Build_WhenBuiltTwiceFromSameInputs_ProducesSameMergedAssemblyHash()
    {
        var repositoryRoot = await RequiredEnvironmentVariable(
            "ONI_MOD_PIPELINE_REPOSITORY_ROOT");
        var managedDirectory = await RequiredEnvironmentVariable(
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

        await Assert.That(secondHash).IsEquivalentTo(firstHash, TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.FormatEvidence());

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
        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.FormatEvidence());
        await Assert.That(File.Exists(mergedAssemblyPath)).IsTrue();
        await AssertKnownFrameworkConflictRootsAreNotReferenced(mergedAssemblyPath);
        await IntentionalRuntimeContractTests.AssertMergedAssembly(mergedAssemblyPath);
        await NoShimArchitectureContractTests.AssertMergedAssembly(mergedAssemblyPath);
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

    private static async Task AssertKnownFrameworkConflictRootsAreNotReferenced(
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
            await Assert.That(references).DoesNotContain(prohibitedReference).Because($"{assemblyPath} must not directly reference {prohibitedReference}.");
        }
    }

    private static async Task AssertPlibIsMergedRatherThanExternallyReferenced(
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
            await Assert.That(DeliveryTemperatureAssemblyMetadataReader.TypeExists(
                    assemblyPath,
                    requiredType)).IsTrue().Because($"Merged assembly {assemblyPath} is missing required PLib " +
                $"type {requiredType}.");
        }

        string[] references = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyReferences(assemblyPath)
            .Select(reference => reference.Name)
            .ToArray();
        await Assert.That(references).DoesNotContain("PLib").Because("PLib must be merged and must not remain a runtime package dependency.");
    }

    private static async Task AssertSupportedFastTrackFixturesAreNotContainedAsync(
        string repositoryRoot,
        string assemblyPath)
    {
        string fixtureCatalogRoot = Path.Combine(
            repositoryRoot,
            "mods",
            "delivery-temperature-limit-supercooled",
            "Tests",
            "Fixtures",
            "ThirdParty",
            "FastTrack");
        byte[] assemblyBytes = await File.ReadAllBytesAsync(assemblyPath);
        foreach (FastTrackSupportedBuildFixtureExpectation fixtureExpectation in
                 FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures)
        {
            string fixtureAssemblyPath = Path.Combine(
                fixtureCatalogRoot,
                fixtureExpectation.RelativeFixtureDirectoryPath,
                "FastTrack.dll");
            byte[] fixtureAssemblyBytes =
                await File.ReadAllBytesAsync(fixtureAssemblyPath);
            await Assert.That(Convert.ToHexString(SHA256.HashData(fixtureAssemblyBytes))).IsEqualTo(fixtureExpectation.AssemblyBuildIdentity.AssemblySha256).Because("The FastTrack production-exclusion contract must remain " +
                "bound to every exact supported-build fixture.");
            await Assert.That(assemblyBytes.AsSpan().IndexOf(fixtureAssemblyBytes)).IsEqualTo(-1).Because($"Merged assembly {assemblyPath} contains the complete " +
                $"FastTrack {fixtureExpectation.AssemblyBuildIdentity.FileVersion} " +
                "fixture byte sequence.");
        }

        string[] assemblyReferences = DeliveryTemperatureAssemblyMetadataReader
            .ReadAssemblyReferences(assemblyPath)
            .Select(reference => reference.Name)
            .ToArray();
        await Assert.That(assemblyReferences.Any(reference => reference.Contains(
                "FastTrack",
                StringComparison.OrdinalIgnoreCase))).IsFalse().Because($"Merged assembly {assemblyPath} retains a FastTrack compile/runtime " +
            "assembly reference.");
        await Assert.That(ReadManifestResourceNames(assemblyPath).Any(resourceName =>
                resourceName.Contains(
                    "FastTrack",
                    StringComparison.OrdinalIgnoreCase))).IsFalse().Because($"Merged assembly {assemblyPath} embeds a FastTrack-named resource.");
    }

    private static async Task<string> ReadTargetFrameworkName(string assemblyPath)
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

        await Assert.That(observedFrameworkNames).Count().IsEqualTo(1).Because($"Assembly {assemblyPath} must declare exactly one " +
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
        await Assert.That(process.Start()).IsTrue().Because("git cat-file did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Assert.That(process.ExitCode).IsEqualTo(0).Because($"Recorded source commit {objectId} is unavailable. " +
            $"Standard output: {await standardOutput}. Standard error: {await standardError}.");
    }

    private static async Task<string> RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse().Because($"Required environment variable {name} was not provided by oni-mod-pipeline.");
        return value!;
    }
}
