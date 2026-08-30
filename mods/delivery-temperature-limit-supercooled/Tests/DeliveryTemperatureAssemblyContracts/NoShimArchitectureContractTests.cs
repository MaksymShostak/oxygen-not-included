#nullable enable

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

/// <summary>
/// Makes the specification's authoritative removal registry executable. Keeping
/// every removed identity in one table prevents source, metadata, forwarding,
/// and deleted-file checks from drifting into subtly different shim policies.
/// </summary>
[TestClass]
public sealed class NoShimArchitectureContractTests
{
    private enum RemovedArchitectureCategory
    {
        MemberOrType,
        RepresentationOrBehavior,
        SupersededProductionFile
    }

    private sealed record RemovedMetadataMember(
        string DeclaringTypeName,
        string MemberName);

    private sealed record RemovedArchitectureIdentity(
        string SemanticIdentity,
        RemovedArchitectureCategory Category,
        IReadOnlyList<string> ProductionSourceSymbols,
        IReadOnlyList<string> MetadataTypeNames,
        IReadOnlyList<RemovedMetadataMember> MetadataMembers,
        string? SupersededProductionFileName = null);

    // This is the one executable transcription of specification section 6.2.
    // Do not create a second list in another test; all removal checks below must
    // consume this table so a future exception requires one conspicuous change.
    private static readonly RemovedArchitectureIdentity[] RemovedArchitectureIdentities =
    [
        new(
            "TemperatureLimit.TemperatureIndexData",
            RemovedArchitectureCategory.MemberOrType,
            ["TemperatureIndexData"],
            ["DeliveryTemperatureLimit.TemperatureLimit+TemperatureIndexData"],
            []),
        new(
            "TemperatureLimit.getTemperatureIndexData()",
            RemovedArchitectureCategory.MemberOrType,
            ["getTemperatureIndexData"],
            [],
            [new("DeliveryTemperatureLimit.TemperatureLimit", "getTemperatureIndexData")]),
        new(
            "TemperatureLimit.UpdateIndexes()",
            RemovedArchitectureCategory.MemberOrType,
            ["UpdateIndexes"],
            [],
            [new("DeliveryTemperatureLimit.TemperatureLimit", "UpdateIndexes")]),
        new(
            "allLimits",
            RemovedArchitectureCategory.MemberOrType,
            ["allLimits"],
            [],
            [new("DeliveryTemperatureLimit.TemperatureLimit", "allLimits")]),
        new(
            "limitsDirty",
            RemovedArchitectureCategory.MemberOrType,
            ["limitsDirty"],
            [],
            [new("DeliveryTemperatureLimit.TemperatureLimit", "limitsDirty")]),
        new(
            "storageFetchableTagsPerTemperatureIndex",
            RemovedArchitectureCategory.MemberOrType,
            ["storageFetchableTagsPerTemperatureIndex"],
            [],
            [new("DeliveryTemperatureLimit.FetchManager_Patch", "storageFetchableTagsPerTemperatureIndex")]),
        new(
            "lazy temperature-index rebuilding",
            RemovedArchitectureCategory.RepresentationOrBehavior,
            ["temperatureIndexData", "indexTemperatures", "temperaturesToIndex", "SetDirty"],
            [],
            [
                new("DeliveryTemperatureLimit.TemperatureLimit", "temperatureIndexData"),
                new("DeliveryTemperatureLimit.TemperatureLimit", "SetDirty")
            ]),
        new(
            "global operational dense storage-band model",
            RemovedArchitectureCategory.RepresentationOrBehavior,
            ["TemperatureIndexes", "TemperatureIndexCount", "SafeGetIndex"],
            [],
            [
                new("DeliveryTemperatureLimit.TemperatureLimit+TemperatureIndexData", "TemperatureIndexes"),
                new("DeliveryTemperatureLimit.TemperatureLimit+TemperatureIndexData", "TemperatureIndexCount"),
                new("DeliveryTemperatureLimit.TemperatureLimit+TemperatureIndexData", "SafeGetIndex")
            ]),
        new(
            "dense status amount dictionaries keyed by tag and temperature index",
            RemovedArchitectureCategory.RepresentationOrBehavior,
            ["StatusItemsUpdaterPatch", "AmountByTagIndexDict", "worldAmounts", "updateSums", "sumTotals", "SumTotalData"],
            [
                "DeliveryTemperatureLimit.StatusItemsUpdaterPatch",
                "DeliveryTemperatureLimit.StatusItemsUpdaterPatch+SumTotalData"
            ],
            [
                new("DeliveryTemperatureLimit.StatusItemsUpdaterPatch", "worldAmounts"),
                new("DeliveryTemperatureLimit.StatusItemsUpdaterPatch", "updateSums"),
                new("DeliveryTemperatureLimit.StatusItemsUpdaterPatch+SumTotalData", "sumTotals")
            ]),
        new(
            "FastTrack temperature hash mixing",
            RemovedArchitectureCategory.RepresentationOrBehavior,
            ["FetchManagerFastUpdate_PickupTagDict_Patch", "AddItem_Hook", "Hash.SDBMLower"],
            ["DeliveryTemperatureLimit.FetchManagerFastUpdate_PickupTagDict_Patch"],
            [new("DeliveryTemperatureLimit.FetchManagerFastUpdate_PickupTagDict_Patch", "AddItem_Hook")]),
        RemovedFile("Limits.cs"),
        RemovedFile("Patch.cs"),
        RemovedFile("PatchFastTrack.cs"),
        RemovedFile("StatusItems.cs"),
        RemovedFile("Harmony.cs")
    ];

    [TestMethod]
    public void RemovedArchitectureRegistry_WhenCompletenessIsInspected_CoversEveryAuthoritativeCategory()
    {
        Assert.HasCount(15, RemovedArchitectureIdentities);
        Assert.HasCount(
            6,
            RemovedArchitectureIdentities.Where(identity =>
                identity.Category == RemovedArchitectureCategory.MemberOrType));
        Assert.HasCount(
            4,
            RemovedArchitectureIdentities.Where(identity =>
                identity.Category == RemovedArchitectureCategory.RepresentationOrBehavior));
        Assert.HasCount(
            5,
            RemovedArchitectureIdentities.Where(identity =>
                identity.Category == RemovedArchitectureCategory.SupersededProductionFile));
        Assert.HasCount(
            RemovedArchitectureIdentities.Length,
            RemovedArchitectureIdentities
                .Select(identity => identity.SemanticIdentity)
                .Distinct(StringComparer.Ordinal));
    }

    [TestMethod]
    public void ProductionSources_WhenRemovedIdentitiesAreInspected_ContainNoLegacyArchitecture()
    {
        string sourceRoot = ResolveSourceRoot();
        string[] productionSourcePaths = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (RemovedArchitectureIdentity identity in RemovedArchitectureIdentities)
        {
            if (identity.Category == RemovedArchitectureCategory.SupersededProductionFile)
            {
                string removedPath = Path.Combine(
                    sourceRoot,
                    identity.SupersededProductionFileName!);
                Assert.IsFalse(
                    File.Exists(removedPath),
                    $"Superseded production file returned: {removedPath}");
                continue;
            }

            foreach (string sourceSymbol in identity.ProductionSourceSymbols)
            {
                string? containingPath = productionSourcePaths.FirstOrDefault(path =>
                    File.ReadAllText(path).Contains(sourceSymbol, StringComparison.Ordinal));
                Assert.IsNull(
                    containingPath,
                    $"Removed architecture identity '{identity.SemanticIdentity}' " +
                    $"returned as source symbol '{sourceSymbol}' in {containingPath}.");
            }
        }
    }

    internal static void AssertMergedAssembly(string assemblyPath)
    {
        ManagedMetadataIdentities metadata = ReadManagedMetadataIdentities(assemblyPath);
        foreach (RemovedArchitectureIdentity identity in RemovedArchitectureIdentities)
        {
            foreach (string removedTypeName in identity.MetadataTypeNames)
            {
                Assert.IsFalse(
                    metadata.DefinedTypeNames.Contains(removedTypeName),
                    $"Removed type '{identity.SemanticIdentity}' returned in {assemblyPath} " +
                    $"as {removedTypeName}.");
                Assert.IsFalse(
                    metadata.ExportedTypeNames.Contains(removedTypeName),
                    $"Removed type '{identity.SemanticIdentity}' returned as an exported " +
                    $"forwarder in {assemblyPath}: {removedTypeName}.");
            }

            foreach (RemovedMetadataMember removedMember in identity.MetadataMembers)
            {
                Assert.IsFalse(
                    metadata.DeclaredMembers.Contains(removedMember),
                    $"Removed member '{identity.SemanticIdentity}' returned in {assemblyPath} " +
                    $"as {removedMember.DeclaringTypeName}.{removedMember.MemberName}.");
            }
        }
    }

    private static RemovedArchitectureIdentity RemovedFile(string fileName) =>
        new(
            fileName,
            RemovedArchitectureCategory.SupersededProductionFile,
            [],
            [],
            [],
            fileName);

    private static ManagedMetadataIdentities ReadManagedMetadataIdentities(
        string assemblyPath)
    {
        using var stream = new FileStream(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var portableExecutableReader = new PEReader(stream);
        Assert.IsTrue(
            portableExecutableReader.HasMetadata,
            $"Managed metadata is absent from {assemblyPath}.");
        MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();

        var definedTypeNames = new HashSet<string>(StringComparer.Ordinal);
        var exportedTypeNames = new HashSet<string>(StringComparer.Ordinal);
        var declaredMembers = new HashSet<RemovedMetadataMember>();
        foreach (TypeDefinitionHandle typeHandle in metadataReader.TypeDefinitions)
        {
            TypeDefinition type = metadataReader.GetTypeDefinition(typeHandle);
            string typeName = GetDefinedTypeName(metadataReader, typeHandle);
            definedTypeNames.Add(typeName);
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                declaredMembers.Add(new(
                    typeName,
                    metadataReader.GetString(
                        metadataReader.GetMethodDefinition(methodHandle).Name)));
            }

            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                declaredMembers.Add(new(
                    typeName,
                    metadataReader.GetString(
                        metadataReader.GetFieldDefinition(fieldHandle).Name)));
            }
        }

        foreach (ExportedTypeHandle exportedTypeHandle in metadataReader.ExportedTypes)
        {
            ExportedType exportedType = metadataReader.GetExportedType(exportedTypeHandle);
            string namespaceName = metadataReader.GetString(exportedType.Namespace);
            string simpleName = metadataReader.GetString(exportedType.Name);
            exportedTypeNames.Add(string.IsNullOrEmpty(namespaceName)
                ? simpleName
                : namespaceName + "." + simpleName);
        }

        return new(definedTypeNames, exportedTypeNames, declaredMembers);
    }

    private static string GetDefinedTypeName(
        MetadataReader metadataReader,
        TypeDefinitionHandle typeHandle)
    {
        TypeDefinition type = metadataReader.GetTypeDefinition(typeHandle);
        string simpleName = metadataReader.GetString(type.Name);
        TypeDefinitionHandle declaringTypeHandle = type.GetDeclaringType();
        if (!declaringTypeHandle.IsNil)
        {
            return GetDefinedTypeName(metadataReader, declaringTypeHandle) + "+" + simpleName;
        }

        string namespaceName = metadataReader.GetString(type.Namespace);
        return string.IsNullOrEmpty(namespaceName)
            ? simpleName
            : namespaceName + "." + simpleName;
    }

    private static string ResolveSourceRoot() => Path.Combine(
        ResolveRepositoryRoot(),
        "mods",
        "delivery-temperature-limit-supercooled",
        "Source");

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

    private sealed record ManagedMetadataIdentities(
        IReadOnlySet<string> DefinedTypeNames,
        IReadOnlySet<string> ExportedTypeNames,
        IReadOnlySet<RemovedMetadataMember> DeclaredMembers);
}
