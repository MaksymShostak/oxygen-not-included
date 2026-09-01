using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

/// <summary>
/// Reads every admitted preserved FastTrack DLL as portable-executable data.
/// Fixtures are never loaded, referenced, or executed, so missing ONI/Unity
/// dependencies cannot affect these static compatibility assertions.
/// </summary>
[TestClass]
public sealed class PreservedFastTrackAssemblyContractTests
{
    public sealed class SupportedBuildFixtureCase
    {
        internal SupportedBuildFixtureCase(
            string assemblyPath,
            FastTrackSupportedBuildFixtureExpectation expectation)
        {
            AssemblyPath = assemblyPath;
            Expectation = expectation;
        }

        internal string AssemblyPath { get; }

        internal FastTrackSupportedBuildFixtureExpectation Expectation
        {
            get;
        }
    }

    public static IEnumerable<object[]> SupportedPreservedBuildCases
    {
        get
        {
            string fixtureRoot = RequireCopiedFixtureRoot();
            foreach (FastTrackSupportedBuildFixtureExpectation expectation in
                     FastTrackSupportedBuildFixtureExpectation.DeclaredFixtures)
            {
                string assemblyPath = Path.GetFullPath(Path.Combine(
                    fixtureRoot,
                    expectation.RelativeFixtureDirectoryPath,
                    "FastTrack.dll"));
                yield return new object[]
                {
                    new SupportedBuildFixtureCase(assemblyPath, expectation)
                };
            }
        }
    }

    public static string FormatSupportedPreservedBuildCaseName(
        MethodInfo methodInfo,
        object[] data)
    {
        var contractCase = (SupportedBuildFixtureCase)data[0];
        FastTrackAssemblyBuildIdentity identity =
            contractCase.Expectation.AssemblyBuildIdentity;
        return $"{methodInfo.Name} ({identity.FileVersion}, " +
            "sha256-" +
            identity.AssemblySha256.Substring(0, 12) +
            ")";
    }

    [TestMethod]
    [DynamicData(
        nameof(SupportedPreservedBuildCases),
        DynamicDataDisplayName = nameof(FormatSupportedPreservedBuildCaseName))]
    public void PreservedFixture_AssemblyMetadataExactlyMatchesExpectedBuild(
        SupportedBuildFixtureCase contractCase)
    {
        FastTrackSupportedBuildFixtureExpectation expectation =
            contractCase.Expectation;
        Assert.AreEqual(
            expectation.AssemblyBuildIdentity.AssemblySha256,
            ComputeUppercaseSha256(contractCase.AssemblyPath));
        using var fixture = new FastTrackPortableExecutableFixture(
            contractCase.AssemblyPath);

        AssemblyDefinition assemblyDefinition =
            fixture.MetadataReader.GetAssemblyDefinition();
        ModuleDefinition moduleDefinition =
            fixture.MetadataReader.GetModuleDefinition();

        Assert.AreEqual(
            expectation.ExpectedAssemblyName,
            fixture.MetadataReader.GetString(assemblyDefinition.Name));
        Assert.AreEqual(
            expectation.ExpectedAssemblyVersion,
            assemblyDefinition.Version);
        Assert.AreEqual(
            expectation.AssemblyBuildIdentity.FileVersion.ToString(),
            fixture.RequireAssemblyFileVersionAttributeValue());
        Assert.AreEqual(
            expectation.ExpectedModuleVersionId,
            fixture.MetadataReader.GetGuid(moduleDefinition.Mvid));
    }

    [TestMethod]
    [DynamicData(
        nameof(SupportedPreservedBuildCases),
        DynamicDataDisplayName = nameof(FormatSupportedPreservedBuildCaseName))]
    public void PreservedFixture_WorldInventoryContractMatchesDeclaredPresenceAndSemantics(
        SupportedBuildFixtureCase contractCase)
    {
        FastTrackSupportedBuildFixtureExpectation expectation =
            contractCase.Expectation;
        using var fixture = new FastTrackPortableExecutableFixture(
            contractCase.AssemblyPath);
        if (!expectation.WorldInventoryReplacementIsPresent)
        {
            Assert.IsFalse(fixture.TryFindType(
                "PeterHan.FastTrack.UIPatches.BackgroundWorldInventory",
                out _));
            return;
        }

        TypeDefinitionHandle backgroundInventory = fixture.RequireType(
            "PeterHan.FastTrack.UIPatches.BackgroundWorldInventory");
        FieldDefinitionHandle firstUpdate = fixture.RequireField(
            backgroundInventory,
            "firstUpdate",
            isStatic: false,
            FieldAttributes.Private,
            "System.Boolean");
        FieldDefinitionHandle updateIndex = fixture.RequireField(
            backgroundInventory,
            "updateIndex",
            isStatic: false,
            FieldAttributes.Private,
            "System.Int32");
        fixture.RequireField(
            backgroundInventory,
            "worldContainer",
            isStatic: false,
            FieldAttributes.Private,
            "WorldContainer");
        fixture.RequireField(
            backgroundInventory,
            "worldInventory",
            isStatic: false,
            FieldAttributes.Private,
            "WorldInventory");
        MethodDefinitionHandle sumTotal = fixture.RequireMethod(
            backgroundInventory,
            "SumTotal",
            isStatic: true,
            MethodAttributes.Private,
            "System.Single",
            "System.Collections.Generic.IEnumerable`1[Pickupable]",
            "System.Int32");
        MethodDefinitionHandle runUpdate = fixture.RequireMethod(
            backgroundInventory,
            "RunUpdate",
            isStatic: false,
            MethodAttributes.Assembly,
            "System.Void");
        TypeDefinitionHandle replacementPatch = fixture.RequireType(
            "PeterHan.FastTrack.UIPatches." +
            "WorldInventory_UpdateReplace_Patch");
        fixture.RequireMethod(
            replacementPatch,
            "Prefix",
            isStatic: true,
            MethodAttributes.Assembly,
            "System.Boolean",
            "WorldInventory");
        TypeDefinitionHandle removalPatch = fixture.RequireType(
            "PeterHan.FastTrack.UIPatches." +
            "WorldInventory_OnRemovedFetchable_Patch");
        MethodDefinitionHandle removedFetchablePrefix = fixture.RequireMethod(
            removalPatch,
            "Prefix",
            isStatic: true,
            MethodAttributes.Assembly,
            "System.Boolean",
            "WorldInventory",
            "System.Object");

        IReadOnlyList<MetadataIlInstruction> runUpdateInstructions =
            fixture.Decode(runUpdate);
        RequireSingleFieldLoadIndex(
            fixture,
            runUpdateInstructions,
            "WorldInventory",
            "Inventory");
        Assert.AreEqual(
            2,
            CountTokenInstructions(
                runUpdateInstructions,
                sumTotal,
                IsCallInstruction));
        Assert.IsGreaterThanOrEqualTo(
            1,
            CountTokenInstructions(
                runUpdateInstructions,
                firstUpdate,
                instruction => instruction.OpCode == OpCodes.Ldfld));
        Assert.IsGreaterThanOrEqualTo(
            1,
            CountTokenInstructions(
                runUpdateInstructions,
                updateIndex,
                instruction => instruction.OpCode == OpCodes.Ldfld));
        Assert.IsGreaterThanOrEqualTo(
            2,
            CountTokenInstructions(
                runUpdateInstructions,
                updateIndex,
                instruction => instruction.OpCode == OpCodes.Stfld));
        Assert.IsTrue(runUpdateInstructions.Any(instruction =>
            instruction.OpCode.FlowControl == FlowControl.Cond_Branch));
        int firstUpdateReadIndex = runUpdateInstructions
            .Select((instruction, index) => (instruction, index))
            .Single(candidate =>
                candidate.instruction.OpCode == OpCodes.Ldfld &&
                candidate.instruction.MetadataToken ==
                MetadataTokens.GetToken(firstUpdate))
            .index;
        Assert.IsTrue(
            firstUpdateReadIndex + 1 < runUpdateInstructions.Count,
            "The firstUpdate field read must be followed by its conditional branch.");
        MetadataIlInstruction firstUpdateBranch =
            runUpdateInstructions[firstUpdateReadIndex + 1];
        Assert.IsTrue(
            firstUpdateBranch.OpCode == OpCodes.Brfalse ||
            firstUpdateBranch.OpCode == OpCodes.Brfalse_S,
            "FastTrack's false branch must select the later single-tag path.");
        Assert.IsNotNull(firstUpdateBranch.BranchTargetOffset);
        int singleResourceTagBranchOffset =
            firstUpdateBranch.BranchTargetOffset.Value;
        Assert.AreEqual(
            1,
            runUpdateInstructions.Count(instruction =>
                instruction.Offset > firstUpdateBranch.Offset &&
                instruction.Offset < singleResourceTagBranchOffset &&
                IsCallInstruction(instruction) &&
                instruction.MetadataToken == MetadataTokens.GetToken(sumTotal)));
        Assert.AreEqual(
            1,
            runUpdateInstructions.Count(instruction =>
                instruction.Offset >= singleResourceTagBranchOffset &&
                IsCallInstruction(instruction) &&
                instruction.MetadataToken == MetadataTokens.GetToken(sumTotal)));

        IReadOnlyList<int> resourceTagGetterIndices = FindCallIndices(
            fixture,
            runUpdateInstructions,
            "System.Collections.Generic.KeyValuePair`2[Tag,",
            "get_Key");
        IReadOnlyList<int> pickupableSetGetterIndices = FindCallIndices(
            fixture,
            runUpdateInstructions,
            "System.Collections.Generic.KeyValuePair`2[Tag,",
            "get_Value");
        IReadOnlyList<int> sumTotalCallIndices =
            runUpdateInstructions
                .Select((instruction, index) => (instruction, index))
                .Where(candidate =>
                    IsCallInstruction(candidate.instruction) &&
                    candidate.instruction.MetadataToken ==
                        MetadataTokens.GetToken(sumTotal))
                .Select(candidate => candidate.index)
                .ToArray();
        IReadOnlyList<int> accessibleAmountSetterIndices = FindCallIndices(
            fixture,
            runUpdateInstructions,
            "System.Collections.Generic.Dictionary`2[Tag,System.Single]",
            "set_Item");
        Assert.AreEqual(2, resourceTagGetterIndices.Count);
        Assert.AreEqual(2, pickupableSetGetterIndices.Count);
        Assert.AreEqual(2, sumTotalCallIndices.Count);
        Assert.AreEqual(2, accessibleAmountSetterIndices.Count);
        for (var publicationIndex = 0;
             publicationIndex < sumTotalCallIndices.Count;
             publicationIndex++)
        {
            Assert.IsTrue(
                resourceTagGetterIndices[publicationIndex] <
                    pickupableSetGetterIndices[publicationIndex] &&
                pickupableSetGetterIndices[publicationIndex] <
                    sumTotalCallIndices[publicationIndex] &&
                sumTotalCallIndices[publicationIndex] <
                    accessibleAmountSetterIndices[publicationIndex],
                "Each complete or incremental RunUpdate branch must load one " +
                "typed resource tag and its pickupable set before SumTotal, then " +
                "write that same branch's accessible amount.");
        }

        IReadOnlyList<MetadataIlInstruction> sumTotalInstructions =
            fixture.Decode(sumTotal);
        int getCellIndex = RequireSingleCallIndex(
            fixture,
            sumTotalInstructions,
            "Workable",
            "GetCell");
        int validCellIndex = RequireSingleCallIndex(
            fixture,
            sumTotalInstructions,
            "Grid",
            "IsValidCell");
        int storedPrivateFilterIndex = RequireSingleCallIndex(
            fixture,
            sumTotalInstructions,
            "KPrefabID",
            "HasTag");
        int totalAmountGetterIndex = RequireSingleCallIndex(
            fixture,
            sumTotalInstructions,
            "Pickupable",
            "get_TotalAmount");
        Assert.IsTrue(
            getCellIndex < validCellIndex &&
            validCellIndex < storedPrivateFilterIndex &&
            storedPrivateFilterIndex < totalAmountGetterIndex);
        Assert.IsTrue(
            sumTotalInstructions
                .Skip(getCellIndex + 1)
                .Take(totalAmountGetterIndex - getCellIndex - 1)
                .Count(instruction =>
                    instruction.OpCode.FlowControl ==
                        FlowControl.Cond_Branch) >= 2,
            "The TotalAmount contribution must remain after FastTrack's cell, " +
            "world, and StoredPrivate filter branches.");
        Assert.IsTrue(totalAmountGetterIndex + 1 < sumTotalInstructions.Count);
        Assert.AreEqual(
            OpCodes.Add,
            sumTotalInstructions[totalAmountGetterIndex + 1].OpCode);

        IReadOnlyList<MetadataIlInstruction> removalInstructions =
            fixture.Decode(removedFetchablePrefix);
        int pickupSetRemovalCount = 0;
        int inventoryDictionaryKeyRemovalCount = 0;
        foreach (MetadataIlInstruction instruction in removalInstructions)
        {
            if (!IsCallInstruction(instruction) ||
                !instruction.MetadataToken.HasValue)
            {
                continue;
            }

            MetadataMemberIdentity member = fixture.ResolveMemberIdentity(
                instruction.MetadataToken.Value);
            if (!string.Equals(member.Name, "Remove", StringComparison.Ordinal))
            {
                continue;
            }

            if (member.DeclaringTypeName.StartsWith(
                    "System.Collections.Generic.HashSet`1[Pickupable]",
                    StringComparison.Ordinal))
            {
                pickupSetRemovalCount++;
            }
            else if (member.DeclaringTypeName.StartsWith(
                         "System.Collections.Generic.Dictionary`2[Tag,",
                         StringComparison.Ordinal) ||
                     member.DeclaringTypeName.StartsWith(
                         "System.Collections.Generic.IDictionary`2[Tag,",
                         StringComparison.Ordinal))
            {
                inventoryDictionaryKeyRemovalCount++;
            }
        }

        Assert.IsGreaterThanOrEqualTo(1, pickupSetRemovalCount);
        Assert.AreEqual(0, inventoryDictionaryKeyRemovalCount);
    }

    [TestMethod]
    [DynamicData(
        nameof(SupportedPreservedBuildCases),
        DynamicDataDisplayName = nameof(FormatSupportedPreservedBuildCaseName))]
    public void PreservedFixture_PickupGroupingContractMatchesDeclaredPresenceAndSemantics(
        SupportedBuildFixtureCase contractCase)
    {
        FastTrackSupportedBuildFixtureExpectation expectation =
            contractCase.Expectation;
        using var fixture = new FastTrackPortableExecutableFixture(
            contractCase.AssemblyPath);
        if (!expectation.PickupGroupingReplacementIsPresent)
        {
            Assert.IsFalse(fixture.TryFindType(
                "PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate",
                out _));
            return;
        }

        TypeDefinitionHandle fastUpdate = fixture.RequireType(
            "PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate");
        fixture.RequireMethod(
            fastUpdate,
            "BeforeUpdatePickups",
            isStatic: true,
            MethodAttributes.Assembly,
            "System.Boolean",
            "FetchManager+FetchablesByPrefabId",
            "Navigator",
            "System.Int32");
        TypeDefinitionHandle pickupTagKey = fixture.RequireNestedType(
            fastUpdate,
            "PickupTagKey",
            TypeAttributes.NestedAssembly);
        FieldDefinitionHandle hashField = fixture.RequireField(
            pickupTagKey,
            "Hash",
            isStatic: false,
            FieldAttributes.Assembly,
            "System.Int32");
        FieldDefinitionHandle identityField = fixture.RequireField(
            pickupTagKey,
            "ID",
            isStatic: false,
            FieldAttributes.Assembly,
            "KPrefabID");
        MethodDefinitionHandle keyConstructor = fixture.RequireMethod(
            pickupTagKey,
            ".ctor",
            isStatic: false,
            MethodAttributes.Public,
            "System.Void",
            "System.Int32",
            "KPrefabID");
        MethodDefinitionHandle typedEquality = fixture.RequireMethod(
            pickupTagKey,
            "Equals",
            isStatic: false,
            MethodAttributes.Public,
            "System.Boolean",
            "PeterHan.FastTrack.GamePatches." +
            "FetchManagerFastUpdate+PickupTagKey");
        TypeDefinitionHandle pickupTagDictionary = fixture.RequireNestedType(
            fastUpdate,
            "PickupTagDict",
            TypeAttributes.NestedPrivate);
        MethodDefinitionHandle addItem = fixture.RequireMethod(
            pickupTagDictionary,
            "AddItem",
            isStatic: false,
            MethodAttributes.Public,
            "System.Void",
            "FetchManager+Fetchable&",
            "System.Int32");

        IReadOnlyList<MetadataIlInstruction> equalityInstructions =
            fixture.Decode(typedEquality);
        Assert.AreEqual(
            2,
            CountTokenInstructions(
                equalityInstructions,
                hashField,
                instruction => instruction.OpCode == OpCodes.Ldfld));
        Assert.AreEqual(
            0,
            CountTokenInstructions(
                equalityInstructions,
                identityField,
                instruction => instruction.OpCode == OpCodes.Ldfld));

        IReadOnlyList<MetadataIlInstruction> addItemInstructions =
            fixture.Decode(addItem);
        Assert.AreEqual(
            1,
            CountTokenInstructions(
                addItemInstructions,
                keyConstructor,
                IsCallInstruction));
        int keyConstructorToken = MetadataTokens.GetToken(keyConstructor);
        int keyConstructorInstructionIndex =
            Enumerable.Range(0, addItemInstructions.Count).Single(index =>
                IsCallInstruction(addItemInstructions[index]) &&
                addItemInstructions[index].MetadataToken ==
                    keyConstructorToken);
        Assert.IsGreaterThanOrEqualTo(4, keyConstructorInstructionIndex);
        Assert.IsTrue(IsLocalAddressLoad(
            addItemInstructions[keyConstructorInstructionIndex - 4]));
        Assert.IsTrue(IsLocalValueLoad(
            addItemInstructions[keyConstructorInstructionIndex - 3]));
        Assert.IsTrue(IsLocalValueLoad(
            addItemInstructions[keyConstructorInstructionIndex - 2]));
        MetadataIlInstruction prefabIdentityLoad =
            addItemInstructions[keyConstructorInstructionIndex - 1];
        Assert.AreEqual(OpCodes.Ldfld, prefabIdentityLoad.OpCode);
        Assert.IsTrue(prefabIdentityLoad.MetadataToken.HasValue);
        MetadataMemberIdentity prefabIdentityField =
            fixture.ResolveMemberIdentity(
                prefabIdentityLoad.MetadataToken.Value);
        Assert.AreEqual("Pickupable", prefabIdentityField.DeclaringTypeName);
        Assert.AreEqual("KPrefabID", prefabIdentityField.Name);
    }

    [TestMethod]
    [DynamicData(
        nameof(SupportedPreservedBuildCases),
        DynamicDataDisplayName = nameof(FormatSupportedPreservedBuildCaseName))]
    public void PreservedFixture_DirectDeliveryReplacementMatchesDeclaredPresence(
        SupportedBuildFixtureCase contractCase)
    {
        FastTrackSupportedBuildFixtureExpectation expectation =
            contractCase.Expectation;
        using var fixture = new FastTrackPortableExecutableFixture(
            contractCase.AssemblyPath);

        Assert.AreEqual(
            expectation.DirectDeliveryReplacementIsPresent,
            fixture.TryFindType(
                "PeterHan.FastTrack.GamePatches.ChoreComparator",
                out _));
        Assert.AreEqual(
            expectation.DirectDeliveryReplacementIsPresent,
            fixture.TryFindType(
                "PeterHan.FastTrack.GamePatches.ChorePatches+" +
                "GlobalChoreProvider_CollectChores_Patch",
                out _));
    }

    [TestMethod]
    [DynamicData(
        nameof(SupportedPreservedBuildCases),
        DynamicDataDisplayName = nameof(FormatSupportedPreservedBuildCaseName))]
    public void TestProject_CopiesPreservedFixtureAsDataWithoutLoadingItsPhysicalAssembly(
        SupportedBuildFixtureCase contractCase)
    {
        Assert.IsFalse(
            IsPhysicalAssemblyWithFixtureSimpleNameLoaded(
                contractCase.AssemblyPath));
    }

    [TestMethod]
    public void PhysicalAssemblyNameDetection_WhenDynamicAssemblySimpleNameMatches_IgnoresIt()
    {
        Assembly loadedDynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("FastTrack"),
            AssemblyBuilderAccess.Run);

        Assert.IsFalse(ContainsPhysicalAssemblyWithSimpleName(
            new[] { loadedDynamicAssembly },
            "FastTrack"));
    }

    [TestMethod]
    public void PhysicalAssemblyNameDetection_WhenLoadedAssemblySimpleNameMatches_ReportsIt()
    {
        Assembly loadedPhysicalAssembly =
            typeof(PreservedFastTrackAssemblyContractTests).Assembly;
        string loadedAssemblySimpleName =
            loadedPhysicalAssembly.GetName().Name!;

        Assert.IsFalse(loadedPhysicalAssembly.IsDynamic);
        Assert.IsTrue(ContainsPhysicalAssemblyWithSimpleName(
            new[] { loadedPhysicalAssembly },
            loadedAssemblySimpleName));
    }

    private static int CountTokenInstructions<THandle>(
        IReadOnlyList<MetadataIlInstruction> instructions,
        THandle expectedHandle,
        Func<MetadataIlInstruction, bool> instructionContract)
        where THandle : struct
    {
        int expectedToken = expectedHandle switch
        {
            MethodDefinitionHandle method => MetadataTokens.GetToken(method),
            FieldDefinitionHandle field => MetadataTokens.GetToken(field),
            _ => throw new ArgumentException(
                "Only method and field handles can be IL member anchors.",
                nameof(expectedHandle))
        };
        return instructions.Count(instruction =>
            instructionContract(instruction) &&
            instruction.MetadataToken == expectedToken);
    }

    private static bool IsCallInstruction(MetadataIlInstruction instruction) =>
        instruction.OpCode == OpCodes.Call ||
        instruction.OpCode == OpCodes.Callvirt ||
        instruction.OpCode == OpCodes.Newobj;

    private static bool IsLocalAddressLoad(
        MetadataIlInstruction instruction) =>
        instruction.OpCode == OpCodes.Ldloca ||
        instruction.OpCode == OpCodes.Ldloca_S;

    private static bool IsLocalValueLoad(
        MetadataIlInstruction instruction) =>
        instruction.OpCode == OpCodes.Ldloc ||
        instruction.OpCode == OpCodes.Ldloc_S ||
        instruction.OpCode == OpCodes.Ldloc_0 ||
        instruction.OpCode == OpCodes.Ldloc_1 ||
        instruction.OpCode == OpCodes.Ldloc_2 ||
        instruction.OpCode == OpCodes.Ldloc_3;

    private static IReadOnlyList<int> FindCallIndices(
        FastTrackPortableExecutableFixture fixture,
        IReadOnlyList<MetadataIlInstruction> instructions,
        string declaringTypeNamePrefix,
        string methodName)
    {
        var matchingIndices = new List<int>();
        for (var instructionIndex = 0;
             instructionIndex < instructions.Count;
             instructionIndex++)
        {
            MetadataIlInstruction instruction = instructions[instructionIndex];
            if (!IsCallInstruction(instruction) ||
                !instruction.MetadataToken.HasValue)
            {
                continue;
            }

            MetadataMemberIdentity member = fixture.ResolveMemberIdentity(
                instruction.MetadataToken.Value);
            if (member.DeclaringTypeName.StartsWith(
                    declaringTypeNamePrefix,
                    StringComparison.Ordinal) &&
                string.Equals(
                    member.Name,
                    methodName,
                    StringComparison.Ordinal))
            {
                matchingIndices.Add(instructionIndex);
            }
        }

        return matchingIndices.AsReadOnly();
    }

    private static int RequireSingleCallIndex(
        FastTrackPortableExecutableFixture fixture,
        IReadOnlyList<MetadataIlInstruction> instructions,
        string declaringTypeNamePrefix,
        string methodName)
    {
        IReadOnlyList<int> matchingIndices = FindCallIndices(
            fixture,
            instructions,
            declaringTypeNamePrefix,
            methodName);
        Assert.AreEqual(
            1,
            matchingIndices.Count,
            $"Expected exactly one {declaringTypeNamePrefix}.{methodName} call. " +
            $"Observed calls: {DescribeCalls(fixture, instructions)}");
        return matchingIndices[0];
    }

    private static int RequireSingleFieldLoadIndex(
        FastTrackPortableExecutableFixture fixture,
        IReadOnlyList<MetadataIlInstruction> instructions,
        string declaringTypeName,
        string fieldName)
    {
        var matchingIndices = new List<int>();
        for (var instructionIndex = 0;
             instructionIndex < instructions.Count;
             instructionIndex++)
        {
            MetadataIlInstruction instruction = instructions[instructionIndex];
            if (instruction.OpCode != OpCodes.Ldfld ||
                !instruction.MetadataToken.HasValue)
            {
                continue;
            }

            MetadataMemberIdentity member = fixture.ResolveMemberIdentity(
                instruction.MetadataToken.Value);
            if (string.Equals(
                    member.DeclaringTypeName,
                    declaringTypeName,
                    StringComparison.Ordinal) &&
                string.Equals(member.Name, fieldName, StringComparison.Ordinal))
            {
                matchingIndices.Add(instructionIndex);
            }
        }

        Assert.AreEqual(
            1,
            matchingIndices.Count,
            $"Expected exactly one {declaringTypeName}.{fieldName} field load.");
        return matchingIndices[0];
    }

    private static string DescribeCalls(
        FastTrackPortableExecutableFixture fixture,
        IReadOnlyList<MetadataIlInstruction> instructions)
    {
        var callIdentities = new List<string>();
        foreach (MetadataIlInstruction instruction in instructions)
        {
            if (!IsCallInstruction(instruction) ||
                !instruction.MetadataToken.HasValue)
            {
                continue;
            }

            MetadataMemberIdentity member = fixture.ResolveMemberIdentity(
                instruction.MetadataToken.Value);
            callIdentities.Add(member.DeclaringTypeName + "." + member.Name);
        }

        return string.Join(", ", callIdentities);
    }

    private static string RequireCopiedFixtureRoot()
    {
        string fixtureRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "ThirdParty",
            "FastTrack"));
        Assert.IsTrue(
            Directory.Exists(fixtureRoot),
            "The preserved FastTrack fixture catalog must be copied as " +
            "non-reference test data by DeliveryTemperatureLimit.Tests.csproj.");
        return fixtureRoot;
    }

    private static bool IsPhysicalAssemblyWithFixtureSimpleNameLoaded(
        string fixturePath)
    {
        string expectedAssemblyName;
        using (var fixture = new FastTrackPortableExecutableFixture(fixturePath))
        {
            AssemblyDefinition assemblyDefinition =
                fixture.MetadataReader.GetAssemblyDefinition();
            expectedAssemblyName = fixture.MetadataReader.GetString(
                assemblyDefinition.Name);
        }

        return ContainsPhysicalAssemblyWithSimpleName(
            AppDomain.CurrentDomain.GetAssemblies(),
            expectedAssemblyName);
    }

    private static bool ContainsPhysicalAssemblyWithSimpleName(
        IEnumerable<Assembly> loadedAssemblies,
        string expectedAssemblySimpleName) =>
        loadedAssemblies.Any(assembly =>
            !assembly.IsDynamic &&
            string.Equals(
                assembly.GetName().Name,
                expectedAssemblySimpleName,
                StringComparison.Ordinal));

    private static string ComputeUppercaseSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using SHA256 algorithm = SHA256.Create();
        return string.Concat(
            algorithm.ComputeHash(stream).Select(value => value.ToString("X2")));
    }

    private sealed class FastTrackPortableExecutableFixture : IDisposable
    {
        private static readonly OpCode[] SingleByteOpCodes =
            BuildOpCodeLookup(multiByte: false);
        private static readonly OpCode[] MultiByteOpCodes =
            BuildOpCodeLookup(multiByte: true);

        private readonly FileStream assemblyStream;
        private readonly PEReader portableExecutableReader;
        private readonly MetadataTypeNameProvider typeNameProvider = new();

        internal FastTrackPortableExecutableFixture(string filePath)
        {
            assemblyStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            portableExecutableReader = new PEReader(assemblyStream);
            Assert.IsTrue(portableExecutableReader.HasMetadata);
            MetadataReader = portableExecutableReader.GetMetadataReader();
        }

        internal MetadataReader MetadataReader { get; }

        internal string RequireAssemblyFileVersionAttributeValue()
        {
            AssemblyDefinition assembly = MetadataReader.GetAssemblyDefinition();
            foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
            {
                CustomAttribute attribute = MetadataReader.GetCustomAttribute(handle);
                if (!string.Equals(
                        GetAttributeTypeName(attribute.Constructor),
                        "System.Reflection.AssemblyFileVersionAttribute",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                BlobReader valueReader = MetadataReader.GetBlobReader(attribute.Value);
                Assert.AreEqual(1, valueReader.ReadUInt16());
                string? value = valueReader.ReadSerializedString();
                Assert.IsNotNull(value);
                return value;
            }

            Assert.Fail("FastTrack fixture has no AssemblyFileVersionAttribute.");
            return string.Empty;
        }

        internal bool TryFindType(
            string fullTypeName,
            out TypeDefinitionHandle typeHandle)
        {
            foreach (TypeDefinitionHandle candidateHandle in
                     MetadataReader.TypeDefinitions)
            {
                if (string.Equals(
                        GetTypeDefinitionName(candidateHandle),
                        fullTypeName,
                        StringComparison.Ordinal))
                {
                    typeHandle = candidateHandle;
                    return true;
                }
            }

            typeHandle = default;
            return false;
        }

        internal TypeDefinitionHandle RequireType(string fullTypeName)
        {
            Assert.IsTrue(
                TryFindType(fullTypeName, out TypeDefinitionHandle handle),
                $"FastTrack fixture requires exact type '{fullTypeName}'.");
            return handle;
        }

        internal TypeDefinitionHandle RequireNestedType(
            TypeDefinitionHandle declaringTypeHandle,
            string nestedTypeName,
            TypeAttributes expectedVisibility)
        {
            TypeDefinition declaringType =
                MetadataReader.GetTypeDefinition(declaringTypeHandle);
            var matches = new List<TypeDefinitionHandle>();
            foreach (TypeDefinitionHandle nestedHandle in
                     declaringType.GetNestedTypes())
            {
                TypeDefinition nestedType =
                    MetadataReader.GetTypeDefinition(nestedHandle);
                if (string.Equals(
                        MetadataReader.GetString(nestedType.Name),
                        nestedTypeName,
                        StringComparison.Ordinal) &&
                    (nestedType.Attributes & TypeAttributes.VisibilityMask) ==
                    expectedVisibility)
                {
                    matches.Add(nestedHandle);
                }
            }

            Assert.HasCount(
                1,
                matches,
                $"Expected exactly one nested type '{nestedTypeName}'.");
            return matches[0];
        }

        internal FieldDefinitionHandle RequireField(
            TypeDefinitionHandle declaringTypeHandle,
            string fieldName,
            bool isStatic,
            FieldAttributes expectedVisibility,
            string fieldTypeName)
        {
            TypeDefinition declaringType =
                MetadataReader.GetTypeDefinition(declaringTypeHandle);
            var matches = new List<FieldDefinitionHandle>();
            foreach (FieldDefinitionHandle fieldHandle in declaringType.GetFields())
            {
                FieldDefinition field =
                    MetadataReader.GetFieldDefinition(fieldHandle);
                string decodedType = field.DecodeSignature(
                    typeNameProvider,
                    genericContext: null);
                if (string.Equals(
                        MetadataReader.GetString(field.Name),
                        fieldName,
                        StringComparison.Ordinal) &&
                    field.Attributes.HasFlag(FieldAttributes.Static) == isStatic &&
                    (field.Attributes & FieldAttributes.FieldAccessMask) ==
                    expectedVisibility &&
                    string.Equals(
                        decodedType,
                        fieldTypeName,
                        StringComparison.Ordinal))
                {
                    matches.Add(fieldHandle);
                }
            }

            Assert.HasCount(
                1,
                matches,
                $"Expected exactly one field '{fieldName}' of type " +
                $"'{fieldTypeName}'.");
            return matches[0];
        }

        internal MethodDefinitionHandle RequireMethod(
            TypeDefinitionHandle declaringTypeHandle,
            string methodName,
            bool isStatic,
            MethodAttributes expectedVisibility,
            string returnTypeName,
            params string[] parameterTypeNames)
        {
            TypeDefinition declaringType =
                MetadataReader.GetTypeDefinition(declaringTypeHandle);
            var matches = new List<MethodDefinitionHandle>();
            foreach (MethodDefinitionHandle methodHandle in
                     declaringType.GetMethods())
            {
                MethodDefinition method =
                    MetadataReader.GetMethodDefinition(methodHandle);
                MethodSignature<string> signature = method.DecodeSignature(
                    typeNameProvider,
                    genericContext: null);
                if (string.Equals(
                        MetadataReader.GetString(method.Name),
                        methodName,
                        StringComparison.Ordinal) &&
                    method.Attributes.HasFlag(MethodAttributes.Static) == isStatic &&
                    (method.Attributes & MethodAttributes.MemberAccessMask) ==
                    expectedVisibility &&
                    string.Equals(
                        signature.ReturnType,
                        returnTypeName,
                        StringComparison.Ordinal) &&
                    signature.ParameterTypes.SequenceEqual(
                        parameterTypeNames,
                        StringComparer.Ordinal))
                {
                    matches.Add(methodHandle);
                }
            }

            Assert.HasCount(
                1,
                matches,
                $"Expected exactly one method '{methodName}' with the declared " +
                "signature.");
            return matches[0];
        }

        internal IReadOnlyList<MetadataIlInstruction> Decode(
            MethodDefinitionHandle methodHandle)
        {
            MethodDefinition method =
                MetadataReader.GetMethodDefinition(methodHandle);
            Assert.AreNotEqual(0, method.RelativeVirtualAddress);
            MethodBodyBlock body = portableExecutableReader.GetMethodBody(
                method.RelativeVirtualAddress);
            byte[]? decodedBytes = body.GetILBytes();
            if (decodedBytes is null)
            {
                throw new InvalidDataException(
                    "The verified FastTrack method has no decodable IL body.");
            }

            byte[] bytes = decodedBytes;
            var instructions = new List<MetadataIlInstruction>();
            var byteIndex = 0;
            while (byteIndex < bytes.Length)
            {
                int offset = byteIndex;
                OpCode opCode = ReadOpCode(bytes, ref byteIndex);
                int? metadataToken = null;
                int? branchTargetOffset = null;
                int operandByteCount;
                switch (opCode.OperandType)
                {
                    case OperandType.InlineNone:
                        operandByteCount = 0;
                        break;
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        operandByteCount = 1;
                        break;
                    case OperandType.ShortInlineBrTarget:
                        RequireRemainingBytes(bytes, byteIndex, 1);
                        branchTargetOffset =
                            byteIndex +
                            1 +
                            unchecked((sbyte)bytes[byteIndex]);
                        operandByteCount = 1;
                        break;
                    case OperandType.InlineVar:
                        operandByteCount = 2;
                        break;
                    case OperandType.InlineI:
                    case OperandType.ShortInlineR:
                        operandByteCount = 4;
                        break;
                    case OperandType.InlineBrTarget:
                        RequireRemainingBytes(bytes, byteIndex, 4);
                        branchTargetOffset =
                            byteIndex +
                            4 +
                            ReadInt32(bytes, byteIndex);
                        operandByteCount = 4;
                        break;
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineSig:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                        RequireRemainingBytes(bytes, byteIndex, 4);
                        metadataToken = ReadInt32(bytes, byteIndex);
                        operandByteCount = 4;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        operandByteCount = 8;
                        break;
                    case OperandType.InlineSwitch:
                        RequireRemainingBytes(bytes, byteIndex, 4);
                        int targetCount = ReadInt32(bytes, byteIndex);
                        Assert.IsGreaterThanOrEqualTo(0, targetCount);
                        operandByteCount = checked(4 + (targetCount * 4));
                        break;
                    default:
                        Assert.Fail($"Unknown IL operand type {opCode.OperandType}.");
                        operandByteCount = 0;
                        break;
                }

                RequireRemainingBytes(bytes, byteIndex, operandByteCount);
                instructions.Add(new MetadataIlInstruction(
                    offset,
                    opCode,
                    metadataToken,
                    branchTargetOffset));
                byteIndex += operandByteCount;
            }

            return instructions.AsReadOnly();
        }

        internal MetadataMemberIdentity ResolveMemberIdentity(int metadataToken)
        {
            EntityHandle handle = MetadataTokens.EntityHandle(metadataToken);
            switch (handle.Kind)
            {
                case HandleKind.MemberReference:
                    MemberReference member = MetadataReader.GetMemberReference(
                        (MemberReferenceHandle)handle);
                    return new MetadataMemberIdentity(
                        GetTypeName(member.Parent),
                        MetadataReader.GetString(member.Name));
                case HandleKind.MethodDefinition:
                    MethodDefinitionHandle methodHandle =
                        (MethodDefinitionHandle)handle;
                    MethodDefinition method =
                        MetadataReader.GetMethodDefinition(methodHandle);
                    return new MetadataMemberIdentity(
                        GetTypeDefinitionName(method.GetDeclaringType()),
                        MetadataReader.GetString(method.Name));
                case HandleKind.FieldDefinition:
                    FieldDefinitionHandle fieldHandle =
                        (FieldDefinitionHandle)handle;
                    FieldDefinition field =
                        MetadataReader.GetFieldDefinition(fieldHandle);
                    return new MetadataMemberIdentity(
                        GetTypeDefinitionName(field.GetDeclaringType()),
                        MetadataReader.GetString(field.Name));
                case HandleKind.MethodSpecification:
                    MethodSpecification specification =
                        MetadataReader.GetMethodSpecification(
                            (MethodSpecificationHandle)handle);
                    return ResolveMemberIdentity(
                        MetadataTokens.GetToken(specification.Method));
                default:
                    Assert.Fail(
                        $"Metadata token 0x{metadataToken:X8} is not a supported " +
                        "method or field identity.");
                    return default;
            }
        }

        public void Dispose()
        {
            portableExecutableReader.Dispose();
            assemblyStream.Dispose();
        }

        private string GetAttributeTypeName(EntityHandle constructorHandle)
        {
            switch (constructorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    MemberReference reference =
                        MetadataReader.GetMemberReference(
                            (MemberReferenceHandle)constructorHandle);
                    return GetTypeName(reference.Parent);
                case HandleKind.MethodDefinition:
                    MethodDefinition definition =
                        MetadataReader.GetMethodDefinition(
                            (MethodDefinitionHandle)constructorHandle);
                    return GetTypeDefinitionName(definition.GetDeclaringType());
                default:
                    return string.Empty;
            }
        }

        private string GetTypeName(EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return GetTypeDefinitionName((TypeDefinitionHandle)handle);
                case HandleKind.TypeReference:
                    return GetTypeReferenceName((TypeReferenceHandle)handle);
                case HandleKind.TypeSpecification:
                    TypeSpecification specification =
                        MetadataReader.GetTypeSpecification(
                            (TypeSpecificationHandle)handle);
                    return specification.DecodeSignature(
                        typeNameProvider,
                        genericContext: null);
                default:
                    return string.Empty;
            }
        }

        private string GetTypeDefinitionName(TypeDefinitionHandle handle)
        {
            TypeDefinition type = MetadataReader.GetTypeDefinition(handle);
            string name = MetadataReader.GetString(type.Name);
            TypeDefinitionHandle declaringType = type.GetDeclaringType();
            if (!declaringType.IsNil)
            {
                return GetTypeDefinitionName(declaringType) + "+" + name;
            }

            string typeNamespace = MetadataReader.GetString(type.Namespace);
            return string.IsNullOrEmpty(typeNamespace)
                ? name
                : typeNamespace + "." + name;
        }

        private string GetTypeReferenceName(TypeReferenceHandle handle)
        {
            TypeReference type = MetadataReader.GetTypeReference(handle);
            string name = MetadataReader.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return GetTypeReferenceName(
                    (TypeReferenceHandle)type.ResolutionScope) +
                    "+" +
                    name;
            }

            string typeNamespace = MetadataReader.GetString(type.Namespace);
            return string.IsNullOrEmpty(typeNamespace)
                ? name
                : typeNamespace + "." + name;
        }

        private static OpCode ReadOpCode(
            byte[] bytes,
            ref int byteIndex)
        {
            byte firstByte = bytes[byteIndex++];
            OpCode opCode;
            if (firstByte == 0xFE)
            {
                RequireRemainingBytes(bytes, byteIndex, 1);
                opCode = MultiByteOpCodes[bytes[byteIndex++]];
            }
            else
            {
                opCode = SingleByteOpCodes[firstByte];
            }

            Assert.AreNotEqual(0, opCode.Size, "Encountered an unknown IL opcode.");
            return opCode;
        }

        private static int ReadInt32(
            byte[] bytes,
            int byteIndex) =>
            bytes[byteIndex] |
            (bytes[byteIndex + 1] << 8) |
            (bytes[byteIndex + 2] << 16) |
            (bytes[byteIndex + 3] << 24);

        private static void RequireRemainingBytes(
            byte[] bytes,
            int byteIndex,
            int requiredByteCount)
        {
            Assert.IsGreaterThanOrEqualTo(0, requiredByteCount);
            Assert.IsTrue(
                byteIndex <= bytes.Length - requiredByteCount,
                "Encountered a truncated IL operand.");
        }

        private static OpCode[] BuildOpCodeLookup(bool multiByte)
        {
            var lookup = new OpCode[256];
            foreach (FieldInfo field in typeof(OpCodes).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is not OpCode opCode || opCode.Size == 0)
                {
                    continue;
                }

                ushort encodedValue = unchecked((ushort)opCode.Value);
                bool isMultiByte = (encodedValue & 0xFF00) == 0xFE00;
                if (isMultiByte == multiByte)
                {
                    lookup[encodedValue & 0xFF] = opCode;
                }
            }

            return lookup;
        }

        private sealed class MetadataTypeNameProvider :
            ISignatureTypeProvider<string, object?>
        {
            public string GetArrayType(string elementType, ArrayShape shape) =>
                elementType +
                "[" +
                new string(',', Math.Max(0, shape.Rank - 1)) +
                "]";

            public string GetByReferenceType(string elementType) =>
                elementType + "&";

            public string GetFunctionPointerType(
                MethodSignature<string> signature) =>
                "method-pointer";

            public string GetGenericInstantiation(
                string genericType,
                ImmutableArray<string> typeArguments) =>
                genericType +
                "[" +
                string.Join(",", typeArguments) +
                "]";

            public string GetGenericMethodParameter(
                object? genericContext,
                int index) =>
                "!!" + index;

            public string GetGenericTypeParameter(
                object? genericContext,
                int index) =>
                "!" + index;

            public string GetModifiedType(
                string modifier,
                string unmodifiedType,
                bool isRequired) =>
                unmodifiedType;

            public string GetPinnedType(string elementType) => elementType;

            public string GetPointerType(string elementType) => elementType + "*";

            public string GetPrimitiveType(PrimitiveTypeCode typeCode) =>
                typeCode switch
                {
                    PrimitiveTypeCode.Boolean => "System.Boolean",
                    PrimitiveTypeCode.Byte => "System.Byte",
                    PrimitiveTypeCode.Char => "System.Char",
                    PrimitiveTypeCode.Double => "System.Double",
                    PrimitiveTypeCode.Int16 => "System.Int16",
                    PrimitiveTypeCode.Int32 => "System.Int32",
                    PrimitiveTypeCode.Int64 => "System.Int64",
                    PrimitiveTypeCode.IntPtr => "System.IntPtr",
                    PrimitiveTypeCode.Object => "System.Object",
                    PrimitiveTypeCode.SByte => "System.SByte",
                    PrimitiveTypeCode.Single => "System.Single",
                    PrimitiveTypeCode.String => "System.String",
                    PrimitiveTypeCode.TypedReference => "System.TypedReference",
                    PrimitiveTypeCode.UInt16 => "System.UInt16",
                    PrimitiveTypeCode.UInt32 => "System.UInt32",
                    PrimitiveTypeCode.UInt64 => "System.UInt64",
                    PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                    PrimitiveTypeCode.Void => "System.Void",
                    _ => typeCode.ToString()
                };

            public string GetSZArrayType(string elementType) => elementType + "[]";

            public string GetTypeFromDefinition(
                MetadataReader reader,
                TypeDefinitionHandle handle,
                byte rawTypeKind) =>
                GetDefinitionName(reader, handle);

            public string GetTypeFromReference(
                MetadataReader reader,
                TypeReferenceHandle handle,
                byte rawTypeKind) =>
                GetReferenceName(reader, handle);

            public string GetTypeFromSpecification(
                MetadataReader reader,
                object? genericContext,
                TypeSpecificationHandle handle,
                byte rawTypeKind) =>
                reader.GetTypeSpecification(handle).DecodeSignature(
                    this,
                    genericContext);

            private static string GetDefinitionName(
                MetadataReader reader,
                TypeDefinitionHandle handle)
            {
                TypeDefinition type = reader.GetTypeDefinition(handle);
                string name = reader.GetString(type.Name);
                TypeDefinitionHandle declaringType = type.GetDeclaringType();
                if (!declaringType.IsNil)
                {
                    return GetDefinitionName(reader, declaringType) + "+" + name;
                }

                string typeNamespace = reader.GetString(type.Namespace);
                return string.IsNullOrEmpty(typeNamespace)
                    ? name
                    : typeNamespace + "." + name;
            }

            private static string GetReferenceName(
                MetadataReader reader,
                TypeReferenceHandle handle)
            {
                TypeReference type = reader.GetTypeReference(handle);
                string name = reader.GetString(type.Name);
                if (type.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    return GetReferenceName(
                        reader,
                        (TypeReferenceHandle)type.ResolutionScope) +
                        "+" +
                        name;
                }

                string typeNamespace = reader.GetString(type.Namespace);
                return string.IsNullOrEmpty(typeNamespace)
                    ? name
                    : typeNamespace + "." + name;
            }
        }
    }

    private readonly record struct MetadataIlInstruction(
        int Offset,
        OpCode OpCode,
        int? MetadataToken,
        int? BranchTargetOffset);

    private readonly record struct MetadataMemberIdentity(
        string DeclaringTypeName,
        string Name);
}
