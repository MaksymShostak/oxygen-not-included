using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

/// <summary>
/// Builds small, dependency-free assemblies that preserve only the FastTrack
/// contracts inspected by Delivery Temperature Limit. Every mutation changes one
/// named semantic fact so a passing test cannot be explained by an unrelated
/// emitted-type difference.
/// </summary>
internal static class FastTrackReflectionEmitFixture
{
    private const string FastTrackHarmonyOwner = "PeterHan.FastTrack";
    private const int HarmonyNormalPriority = 400;

    internal static FastTrackEmittedAssembly CreateExpectedContract() =>
        Create(FastTrackContractMutation.None);

    internal static FastTrackEmittedAssembly CreateExpectedContract(
        string assemblySimpleName) =>
        Create(FastTrackContractMutation.None, assemblySimpleName);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateSignatureChanged() =>
        Create(FastTrackContractMutation.RunUpdateSignatureChanged);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateSignatureChanged(string assemblySimpleName) =>
        Create(
            FastTrackContractMutation.RunUpdateSignatureChanged,
            assemblySimpleName);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateMissingSingleTagBranch() =>
        Create(FastTrackContractMutation.RunUpdateMissingSingleTagBranch);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateFirstUpdateBranchReversed() =>
        Create(FastTrackContractMutation.RunUpdateFirstUpdateBranchReversed);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateTotalsInCompleteBranchOnly() =>
        Create(FastTrackContractMutation.RunUpdateTotalsInCompleteBranchOnly);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateResourceTagPublicationAnchorMissing() =>
        Create(FastTrackContractMutation
            .RunUpdateResourceTagPublicationAnchorMissing);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateResourceTagPublicationAnchorDuplicated() =>
        Create(FastTrackContractMutation
            .RunUpdateResourceTagPublicationAnchorDuplicated);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateInventoryFieldAnchorMissing() =>
        Create(FastTrackContractMutation
            .RunUpdateInventoryFieldAnchorMissing);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateInventoryFieldAnchorDuplicated() =>
        Create(FastTrackContractMutation
            .RunUpdateInventoryFieldAnchorDuplicated);

    internal static FastTrackEmittedAssembly
        CreateWithSumTotalFilteredContributionAnchorMissing() =>
        Create(FastTrackContractMutation
            .SumTotalFilteredContributionAnchorMissing);

    internal static FastTrackEmittedAssembly
        CreateWithSumTotalFilteredContributionAnchorDuplicated() =>
        Create(FastTrackContractMutation
            .SumTotalFilteredContributionAnchorDuplicated);

    internal static FastTrackEmittedAssembly
        CreateWithRemovedFetchableDeletingTagKey() =>
        Create(FastTrackContractMutation.RemovedFetchableDeletesTagKey);

    internal static FastTrackEmittedAssembly
        CreateWithPickupTagKeyEqualityUsingAllocatedIdentity() =>
        Create(FastTrackContractMutation.PickupTagKeyEqualityUsesAllocatedIdentity);

    internal static FastTrackEmittedAssembly
        CreateWithAddItemConstructorAnchorMissing() =>
        Create(FastTrackContractMutation.AddItemConstructorAnchorMissing);

    internal static FastTrackEmittedAssembly
        CreateWithAddItemConstructorAnchorDuplicated() =>
        Create(FastTrackContractMutation.AddItemConstructorAnchorDuplicated);

    internal static FastTrackEmittedAssembly
        CreateWithPickupTagKeyConstructorArgumentsReversed() =>
        Create(FastTrackContractMutation
            .PickupTagKeyConstructorArgumentsReversed);

    internal static FastTrackEmittedAssembly
        CreateWithAddItemSignatureChanged() =>
        Create(FastTrackContractMutation.AddItemSignatureChanged);

    internal static FastTrackEmittedAssembly
        CreateWithDirectComparatorContractChanged() =>
        Create(FastTrackContractMutation.DirectComparatorSignatureChanged);

    internal static FastTrackEmittedAssembly
        CreateWithDirectComparatorContractChanged(string assemblySimpleName) =>
        Create(
            FastTrackContractMutation.DirectComparatorSignatureChanged,
            assemblySimpleName);

    internal static FastTrackEmittedAssembly
        CreateWithDirectComparatorSuccessReturnMissing() =>
        Create(FastTrackContractMutation
            .DirectComparatorSuccessReturnMissing);

    internal static FastTrackEmittedAssembly
        CreateWithDirectComparatorSuccessReturnDuplicated() =>
        Create(FastTrackContractMutation
            .DirectComparatorSuccessReturnDuplicated);

    private static FastTrackEmittedAssembly Create(
        FastTrackContractMutation mutation,
        string? assemblySimpleName = null)
    {
        var assemblyName = new AssemblyName(
            assemblySimpleName ??
                $"FastTrack.EmittedContract.{Guid.NewGuid():N}")
        {
            Version = new Version(0, 18, 0, 0)
        };
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.RunAndCollect);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(
            assemblyName.Name!);

        EmittedGameContractTypes gameTypes = DefineGameContractTypes(
            moduleBuilder);
        EmittedWorldInventoryContract worldInventoryContract =
            DefineWorldInventoryContract(moduleBuilder, gameTypes, mutation);
        EmittedPickupGroupingContract pickupGroupingContract =
            DefinePickupGroupingContract(moduleBuilder, gameTypes, mutation);
        EmittedDirectDeliveryContract directDeliveryContract =
            DefineDirectDeliveryContract(moduleBuilder, gameTypes, mutation);

        return new FastTrackEmittedAssembly(
            assemblyBuilder,
            new ActiveHarmonyPrefixDescriptor(
                worldInventoryContract.WorldInventoryUpdateTarget,
                worldInventoryContract.WorldInventoryReplacementPrefix,
                FastTrackHarmonyOwner,
                HarmonyNormalPriority),
            new ActiveHarmonyPrefixDescriptor(
                pickupGroupingContract.UpdatePickupsTarget,
                pickupGroupingContract.BeforeUpdatePickupsPrefix,
                FastTrackHarmonyOwner,
                HarmonyNormalPriority),
            new ActiveHarmonyPrefixDescriptor(
                directDeliveryContract.GlobalChoreCollectionTarget,
                directDeliveryContract.GlobalChoreCollectionPrefix,
                FastTrackHarmonyOwner,
                HarmonyNormalPriority));
    }

    private static EmittedGameContractTypes DefineGameContractTypes(
        ModuleBuilder moduleBuilder)
    {
        Type tagType = DefineValueType(moduleBuilder, "Tag");
        TypeBuilder prefabIdentityBuilder = moduleBuilder.DefineType(
            "KPrefabID",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(prefabIdentityBuilder);
        MethodBuilder prefabIdentityHasTagBuilder =
            prefabIdentityBuilder.DefineMethod(
                "HasTag",
                MethodAttributes.Public,
                typeof(bool),
                new[] { tagType });
        ILGenerator prefabIdentityHasTagGenerator =
            prefabIdentityHasTagBuilder.GetILGenerator();
        prefabIdentityHasTagGenerator.Emit(OpCodes.Ldc_I4_0);
        prefabIdentityHasTagGenerator.Emit(OpCodes.Ret);
        Type prefabIdentityType = prefabIdentityBuilder.CreateType()!;

        TypeBuilder workableBuilder = moduleBuilder.DefineType(
            "Workable",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(workableBuilder);
        MethodBuilder pickupableGetCellBuilder = workableBuilder.DefineMethod(
            "GetCell",
            MethodAttributes.Public,
            typeof(int),
            Type.EmptyTypes);
        ILGenerator pickupableGetCellGenerator =
            pickupableGetCellBuilder.GetILGenerator();
        pickupableGetCellGenerator.Emit(OpCodes.Ldc_I4_0);
        pickupableGetCellGenerator.Emit(OpCodes.Ret);
        Type workableType = workableBuilder.CreateType()!;

        TypeBuilder pickupableBuilder = moduleBuilder.DefineType(
            "Pickupable",
            TypeAttributes.Public | TypeAttributes.Class,
            workableType);
        DefineDefaultConstructor(
            pickupableBuilder,
            workableType.GetConstructor(Type.EmptyTypes)!);
        FieldBuilder pickupablePrefabIdentityField =
            pickupableBuilder.DefineField(
                "KPrefabID",
                prefabIdentityType,
                FieldAttributes.Public);
        MethodBuilder pickupablePrefabIdentityGetter =
            pickupableBuilder.DefineMethod(
                "get_KPrefabID",
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                prefabIdentityType,
                Type.EmptyTypes);
        ILGenerator pickupablePrefabIdentityGenerator =
            pickupablePrefabIdentityGetter.GetILGenerator();
        pickupablePrefabIdentityGenerator.Emit(OpCodes.Ldarg_0);
        pickupablePrefabIdentityGenerator.Emit(
            OpCodes.Ldfld,
            pickupablePrefabIdentityField);
        pickupablePrefabIdentityGenerator.Emit(OpCodes.Ret);
        MethodBuilder pickupableTotalAmountGetter =
            pickupableBuilder.DefineMethod(
                "get_TotalAmount",
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                typeof(float),
                Type.EmptyTypes);
        ILGenerator pickupableTotalAmountGenerator =
            pickupableTotalAmountGetter.GetILGenerator();
        pickupableTotalAmountGenerator.Emit(OpCodes.Ldc_R4, 1.0f);
        pickupableTotalAmountGenerator.Emit(OpCodes.Ret);
        Type pickupableType = pickupableBuilder.CreateType()!;

        TypeBuilder gridBuilder = moduleBuilder.DefineType(
            "Grid",
            TypeAttributes.Public |
            TypeAttributes.Abstract |
            TypeAttributes.Sealed);
        FieldBuilder gridWorldIndicesField = gridBuilder.DefineField(
            "WorldIdx",
            typeof(int[]),
            FieldAttributes.Public | FieldAttributes.Static);
        MethodBuilder gridIsValidCellBuilder = gridBuilder.DefineMethod(
            "IsValidCell",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool),
            new[] { typeof(int) });
        ILGenerator gridIsValidCellGenerator =
            gridIsValidCellBuilder.GetILGenerator();
        gridIsValidCellGenerator.Emit(OpCodes.Ldc_I4_1);
        gridIsValidCellGenerator.Emit(OpCodes.Ret);
        gridBuilder.CreateType();

        TypeBuilder gameTagsBuilder = moduleBuilder.DefineType(
            "GameTags",
            TypeAttributes.Public |
            TypeAttributes.Abstract |
            TypeAttributes.Sealed);
        FieldBuilder storedPrivateTagField = gameTagsBuilder.DefineField(
            "StoredPrivate",
            tagType,
            FieldAttributes.Public | FieldAttributes.Static);
        gameTagsBuilder.CreateType();
        Type navigatorType = DefineReferenceType(moduleBuilder, "Navigator");
        Type worldContainerType = DefineReferenceType(
            moduleBuilder,
            "WorldContainer");

        Type pickupableSetType = typeof(HashSet<>).MakeGenericType(
            pickupableType);
        Type inventoryDictionaryType = typeof(Dictionary<,>).MakeGenericType(
            tagType,
            pickupableSetType);

        TypeBuilder worldInventoryBuilder = moduleBuilder.DefineType(
            "WorldInventory",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(worldInventoryBuilder);
        worldInventoryBuilder.DefineField(
            "Inventory",
            inventoryDictionaryType,
            FieldAttributes.Private);
        MethodBuilder worldInventoryUpdateBuilder = worldInventoryBuilder.DefineMethod(
            "Update",
            MethodAttributes.Public,
            typeof(void),
            Type.EmptyTypes);
        worldInventoryUpdateBuilder.GetILGenerator().Emit(OpCodes.Ret);
        Type worldInventoryType = worldInventoryBuilder.CreateType()!;

        TypeBuilder fetchManagerBuilder = moduleBuilder.DefineType(
            "FetchManager",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(fetchManagerBuilder);
        TypeBuilder fetchablesByPrefabIdBuilder = fetchManagerBuilder.DefineNestedType(
            "FetchablesByPrefabId",
            TypeAttributes.NestedPublic | TypeAttributes.Class);
        DefineDefaultConstructor(fetchablesByPrefabIdBuilder);
        MethodBuilder updatePickupsBuilder = fetchablesByPrefabIdBuilder.DefineMethod(
            "UpdatePickups",
            MethodAttributes.Public,
            typeof(void),
            new[] { navigatorType, typeof(int) });
        updatePickupsBuilder.GetILGenerator().Emit(OpCodes.Ret);
        Type fetchablesByPrefabIdType = fetchablesByPrefabIdBuilder.CreateType()!;
        TypeBuilder fetchableBuilder = fetchManagerBuilder.DefineNestedType(
            "Fetchable",
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.SequentialLayout,
            typeof(ValueType));
        fetchableBuilder.DefineField(
            "tagBitsHash",
            typeof(int),
            FieldAttributes.Public);
        fetchableBuilder.DefineField(
            "pickupable",
            pickupableType,
            FieldAttributes.Public);
        Type fetchableType = fetchableBuilder.CreateType()!;
        fetchManagerBuilder.DefineNestedType(
                "Pickup",
                TypeAttributes.NestedPublic |
                TypeAttributes.Sealed |
                TypeAttributes.SequentialLayout,
                typeof(ValueType))
            .CreateType();
        fetchManagerBuilder.CreateType();

        Type choreConsumerStateType = DefineReferenceType(
            moduleBuilder,
            "ChoreConsumerState");
        Type fetchChoreType = DefineReferenceType(moduleBuilder, "FetchChore");

        TypeBuilder choreBuilder = moduleBuilder.DefineType(
            "Chore",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(choreBuilder);
        TypeBuilder preconditionBuilder = choreBuilder.DefineNestedType(
            "Precondition",
            TypeAttributes.NestedPublic | TypeAttributes.Class);
        DefineDefaultConstructor(preconditionBuilder);
        TypeBuilder contextBuilder = preconditionBuilder.DefineNestedType(
            "Context",
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.SequentialLayout,
            typeof(ValueType));
        Type preconditionContextType = contextBuilder.CreateType()!;
        preconditionBuilder.CreateType();
        choreBuilder.CreateType();

        TypeBuilder clearableManagerBuilder = moduleBuilder.DefineType(
            "ClearableManager",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(clearableManagerBuilder);
        TypeBuilder sortedClearableBuilder = clearableManagerBuilder.DefineNestedType(
            "SortedClearable",
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.SequentialLayout,
            typeof(ValueType));
        sortedClearableBuilder.DefineField(
            "pickupable",
            pickupableType,
            FieldAttributes.Public);
        Type sortedClearableType = sortedClearableBuilder.CreateType()!;
        clearableManagerBuilder.CreateType();

        Type listOfPreconditionContexts = typeof(List<>).MakeGenericType(
            preconditionContextType);
        TypeBuilder globalChoreProviderBuilder = moduleBuilder.DefineType(
            "GlobalChoreProvider",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(globalChoreProviderBuilder);
        MethodBuilder collectChoresBuilder = globalChoreProviderBuilder.DefineMethod(
            "CollectChores",
            MethodAttributes.Public,
            typeof(void),
            new[] { choreConsumerStateType, listOfPreconditionContexts });
        collectChoresBuilder.GetILGenerator().Emit(OpCodes.Ret);
        Type globalChoreProviderType = globalChoreProviderBuilder.CreateType()!;

        return new EmittedGameContractTypes(
            tagType,
            pickupableType,
            prefabIdentityType,
            pickupableGetCellBuilder,
            pickupableTotalAmountGetter,
            pickupablePrefabIdentityGetter,
            prefabIdentityHasTagBuilder,
            gridIsValidCellBuilder,
            gridWorldIndicesField,
            storedPrivateTagField,
            navigatorType,
            worldContainerType,
            worldInventoryType,
            worldInventoryType.GetMethod("Update")!,
            worldInventoryType.GetField(
                "Inventory",
                BindingFlags.Instance | BindingFlags.NonPublic)!,
            fetchablesByPrefabIdType,
            fetchablesByPrefabIdType.GetMethod("UpdatePickups")!,
            fetchableType,
            fetchableType.GetField("tagBitsHash")!,
            fetchableType.GetField("pickupable")!,
            pickupableType.GetField("KPrefabID")!,
            choreConsumerStateType,
            fetchChoreType,
            preconditionContextType,
            sortedClearableType,
            globalChoreProviderType,
            globalChoreProviderType.GetMethod("CollectChores")!);
    }

    private static EmittedWorldInventoryContract DefineWorldInventoryContract(
        ModuleBuilder moduleBuilder,
        EmittedGameContractTypes gameTypes,
        FastTrackContractMutation mutation)
    {
        Type pickupableSetType = typeof(HashSet<>).MakeGenericType(
            gameTypes.PickupableType);
        Type inventoryDictionaryType = typeof(Dictionary<,>).MakeGenericType(
            gameTypes.TagType,
            pickupableSetType);
        Type accessibleAmountDictionaryType = typeof(Dictionary<,>).MakeGenericType(
            gameTypes.TagType,
            typeof(float));
        Type inventoryEntryType = typeof(KeyValuePair<,>).MakeGenericType(
            gameTypes.TagType,
            pickupableSetType);
        Type pickupableSequenceType = typeof(IEnumerable<>).MakeGenericType(
            gameTypes.PickupableType);
        MethodInfo inventoryEntryKeyGetter = inventoryEntryType
            .GetProperty("Key")!
            .GetMethod!;
        MethodInfo inventoryEntryValueGetter = inventoryEntryType
            .GetProperty("Value")!
            .GetMethod!;
        MethodInfo accessibleAmountSetter = accessibleAmountDictionaryType
            .GetProperty("Item")!
            .SetMethod!;

        TypeBuilder backgroundInventoryBuilder = moduleBuilder.DefineType(
            "PeterHan.FastTrack.UIPatches.BackgroundWorldInventory",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
        FieldBuilder firstUpdateField = backgroundInventoryBuilder.DefineField(
            "firstUpdate",
            typeof(bool),
            FieldAttributes.Private);
        FieldBuilder updateIndexField = backgroundInventoryBuilder.DefineField(
            "updateIndex",
            typeof(int),
            FieldAttributes.Private);
        backgroundInventoryBuilder.DefineField(
            "validCount",
            typeof(bool),
            FieldAttributes.Private);
        backgroundInventoryBuilder.DefineField(
            "worldContainer",
            gameTypes.WorldContainerType,
            FieldAttributes.Private);
        FieldBuilder worldInventoryField = backgroundInventoryBuilder.DefineField(
            "worldInventory",
            gameTypes.WorldInventoryType,
            FieldAttributes.Private);
        DefineDefaultConstructor(backgroundInventoryBuilder);

        MethodBuilder sumTotalBuilder = backgroundInventoryBuilder.DefineMethod(
            "SumTotal",
            MethodAttributes.Private | MethodAttributes.Static,
            typeof(float),
            new[] { pickupableSequenceType, typeof(int) });
        EmitSumTotal(
            sumTotalBuilder.GetILGenerator(),
            gameTypes,
            mutation);

        Type runUpdateReturnType =
            mutation == FastTrackContractMutation.RunUpdateSignatureChanged
                ? typeof(bool)
                : typeof(void);
        MethodBuilder runUpdateBuilder = backgroundInventoryBuilder.DefineMethod(
            "RunUpdate",
            MethodAttributes.Assembly,
            runUpdateReturnType,
            Type.EmptyTypes);
        EmitRunUpdate(
            runUpdateBuilder.GetILGenerator(),
            firstUpdateField,
            updateIndexField,
            worldInventoryField,
            gameTypes.WorldInventoryEntriesField,
            sumTotalBuilder,
            accessibleAmountDictionaryType,
            inventoryEntryType,
            inventoryEntryKeyGetter,
            inventoryEntryValueGetter,
            accessibleAmountSetter,
            mutation,
            runUpdateReturnType);
        backgroundInventoryBuilder.CreateType();

        Type worldInventoryReplacementPatchType = DefineWorldInventoryPrefixType(
            moduleBuilder,
            "WorldInventory_UpdateReplace_Patch",
            gameTypes.WorldInventoryType,
            emitRemovedFetchableContract: false,
            inventoryDictionaryType,
            pickupableSetType,
            gameTypes,
            mutation);
        Type removedFetchablePatchType = DefineWorldInventoryPrefixType(
            moduleBuilder,
            "WorldInventory_OnRemovedFetchable_Patch",
            gameTypes.WorldInventoryType,
            emitRemovedFetchableContract: true,
            inventoryDictionaryType,
            pickupableSetType,
            gameTypes,
            mutation);

        return new EmittedWorldInventoryContract(
            gameTypes.WorldInventoryUpdateTarget,
            worldInventoryReplacementPatchType.GetMethod(
                "Prefix",
                BindingFlags.Static | BindingFlags.NonPublic)!,
            removedFetchablePatchType.GetMethod(
                "Prefix",
                BindingFlags.Static | BindingFlags.NonPublic)!);
    }

    private static Type DefineWorldInventoryPrefixType(
        ModuleBuilder moduleBuilder,
        string nestedContractTypeName,
        Type worldInventoryType,
        bool emitRemovedFetchableContract,
        Type inventoryDictionaryType,
        Type pickupableSetType,
        EmittedGameContractTypes gameTypes,
        FastTrackContractMutation mutation)
    {
        TypeBuilder patchBuilder = moduleBuilder.DefineType(
            "PeterHan.FastTrack.UIPatches." + nestedContractTypeName,
            TypeAttributes.Public |
            TypeAttributes.Abstract |
            TypeAttributes.Sealed);
        Type[] parameterTypes = emitRemovedFetchableContract
            ? new[] { worldInventoryType, typeof(object) }
            : new[] { worldInventoryType };
        MethodBuilder prefixBuilder = patchBuilder.DefineMethod(
            "Prefix",
            MethodAttributes.Assembly | MethodAttributes.Static,
            typeof(bool),
            parameterTypes);
        ILGenerator prefixGenerator = prefixBuilder.GetILGenerator();
        if (emitRemovedFetchableContract)
        {
            ConstructorInfo setConstructor = pickupableSetType.GetConstructor(
                Type.EmptyTypes)!;
            MethodInfo setRemoveMethod = pickupableSetType.GetMethod(
                "Remove",
                new[] { gameTypes.PickupableType })!;
            prefixGenerator.Emit(OpCodes.Newobj, setConstructor);
            prefixGenerator.Emit(OpCodes.Ldnull);
            prefixGenerator.Emit(OpCodes.Callvirt, setRemoveMethod);
            prefixGenerator.Emit(OpCodes.Pop);
            if (mutation ==
                FastTrackContractMutation.RemovedFetchableDeletesTagKey)
            {
                ConstructorInfo dictionaryConstructor =
                    inventoryDictionaryType.GetConstructor(Type.EmptyTypes)!;
                MethodInfo dictionaryRemoveMethod = inventoryDictionaryType.GetMethod(
                    "Remove",
                    new[] { gameTypes.TagType })!;
                LocalBuilder dictionary = prefixGenerator.DeclareLocal(
                    inventoryDictionaryType);
                LocalBuilder tag = prefixGenerator.DeclareLocal(gameTypes.TagType);
                prefixGenerator.Emit(OpCodes.Newobj, dictionaryConstructor);
                prefixGenerator.Emit(OpCodes.Stloc, dictionary);
                prefixGenerator.Emit(OpCodes.Ldloc, dictionary);
                prefixGenerator.Emit(OpCodes.Ldloc, tag);
                prefixGenerator.Emit(OpCodes.Callvirt, dictionaryRemoveMethod);
                prefixGenerator.Emit(OpCodes.Pop);
            }
        }

        prefixGenerator.Emit(OpCodes.Ldc_I4_0);
        prefixGenerator.Emit(OpCodes.Ret);
        return patchBuilder.CreateType()!;
    }

    private static void EmitRunUpdate(
        ILGenerator generator,
        FieldInfo firstUpdateField,
        FieldInfo updateIndexField,
        FieldInfo worldInventoryField,
        FieldInfo worldInventoryEntriesField,
        MethodInfo sumTotalMethod,
        Type accessibleAmountDictionaryType,
        Type inventoryEntryType,
        MethodInfo inventoryEntryKeyGetter,
        MethodInfo inventoryEntryValueGetter,
        MethodInfo accessibleAmountSetter,
        FastTrackContractMutation mutation,
        Type returnType)
    {
        LocalBuilder accessibleAmounts =
            generator.DeclareLocal(accessibleAmountDictionaryType);
        LocalBuilder inventoryEntry = generator.DeclareLocal(inventoryEntryType);
        Label singleTagBranch = generator.DefineLabel();
        Label exit = generator.DefineLabel();

        generator.Emit(
            OpCodes.Newobj,
            accessibleAmountDictionaryType.GetConstructor(Type.EmptyTypes)!);
        generator.Emit(OpCodes.Stloc, accessibleAmounts);
        if (mutation !=
            FastTrackContractMutation.RunUpdateInventoryFieldAnchorMissing)
        {
            EmitWorldInventoryEntriesFieldLoad(
                generator,
                worldInventoryField,
                worldInventoryEntriesField);
        }

        if (mutation ==
            FastTrackContractMutation.RunUpdateInventoryFieldAnchorDuplicated)
        {
            EmitWorldInventoryEntriesFieldLoad(
                generator,
                worldInventoryField,
                worldInventoryEntriesField);
        }

        generator.Emit(OpCodes.Ldloca, inventoryEntry);
        generator.Emit(OpCodes.Initobj, inventoryEntryType);
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldfld, firstUpdateField);
        generator.Emit(
            mutation ==
                FastTrackContractMutation.RunUpdateFirstUpdateBranchReversed
                    ? OpCodes.Brtrue_S
                    : OpCodes.Brfalse_S,
            singleTagBranch);

        EmitResourceTagPublication(
            generator,
            accessibleAmounts,
            inventoryEntry,
            inventoryEntryKeyGetter,
            inventoryEntryValueGetter,
            accessibleAmountSetter,
            sumTotalMethod,
            mutation);
        if (mutation ==
            FastTrackContractMutation.RunUpdateTotalsInCompleteBranchOnly)
        {
            EmitResourceTagPublication(
                generator,
                accessibleAmounts,
                inventoryEntry,
                inventoryEntryKeyGetter,
                inventoryEntryValueGetter,
                accessibleAmountSetter,
                sumTotalMethod,
                FastTrackContractMutation.None);
        }

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldc_I4_1);
        generator.Emit(OpCodes.Stfld, updateIndexField);
        generator.Emit(OpCodes.Br_S, exit);

        generator.MarkLabel(singleTagBranch);
        if (mutation !=
            FastTrackContractMutation.RunUpdateMissingSingleTagBranch)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, updateIndexField);
            generator.Emit(OpCodes.Pop);
            if (mutation !=
                FastTrackContractMutation.RunUpdateTotalsInCompleteBranchOnly)
            {
                EmitResourceTagPublication(
                    generator,
                    accessibleAmounts,
                    inventoryEntry,
                    inventoryEntryKeyGetter,
                    inventoryEntryValueGetter,
                    accessibleAmountSetter,
                    sumTotalMethod,
                    FastTrackContractMutation.None);
            }

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldc_I4_2);
            generator.Emit(OpCodes.Stfld, updateIndexField);
        }

        generator.MarkLabel(exit);
        if (returnType == typeof(bool))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
        }

        generator.Emit(OpCodes.Ret);
    }

    private static void EmitWorldInventoryEntriesFieldLoad(
        ILGenerator generator,
        FieldInfo worldInventoryField,
        FieldInfo worldInventoryEntriesField)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldfld, worldInventoryField);
        generator.Emit(OpCodes.Ldfld, worldInventoryEntriesField);
        generator.Emit(OpCodes.Pop);
    }

    private static void EmitResourceTagPublication(
        ILGenerator generator,
        LocalBuilder accessibleAmounts,
        LocalBuilder inventoryEntry,
        MethodInfo inventoryEntryKeyGetter,
        MethodInfo inventoryEntryValueGetter,
        MethodInfo accessibleAmountSetter,
        MethodInfo sumTotalMethod,
        FastTrackContractMutation mutation)
    {
        if (mutation ==
            FastTrackContractMutation
                .RunUpdateResourceTagPublicationAnchorMissing)
        {
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Call, sumTotalMethod);
            generator.Emit(OpCodes.Pop);
            return;
        }

        generator.Emit(OpCodes.Ldloc, accessibleAmounts);
        if (mutation ==
            FastTrackContractMutation
                .RunUpdateResourceTagPublicationAnchorDuplicated)
        {
            generator.Emit(OpCodes.Ldloca, inventoryEntry);
            generator.Emit(OpCodes.Call, inventoryEntryKeyGetter);
            generator.Emit(OpCodes.Pop);
        }

        generator.Emit(OpCodes.Ldloca, inventoryEntry);
        generator.Emit(OpCodes.Call, inventoryEntryKeyGetter);
        generator.Emit(OpCodes.Ldloca, inventoryEntry);
        generator.Emit(OpCodes.Call, inventoryEntryValueGetter);
        generator.Emit(OpCodes.Ldc_I4_0);
        generator.Emit(OpCodes.Call, sumTotalMethod);
        generator.Emit(OpCodes.Callvirt, accessibleAmountSetter);
    }

    private static void EmitSumTotal(
        ILGenerator generator,
        EmittedGameContractTypes gameTypes,
        FastTrackContractMutation mutation)
    {
        LocalBuilder totalAmount = generator.DeclareLocal(typeof(float));
        LocalBuilder pickupable =
            generator.DeclareLocal(gameTypes.PickupableType);
        LocalBuilder cell = generator.DeclareLocal(typeof(int));
        Label returnTotal = generator.DefineLabel();

        generator.Emit(OpCodes.Ldc_R4, 0.0f);
        generator.Emit(OpCodes.Stloc, totalAmount);
        generator.Emit(OpCodes.Ldnull);
        generator.Emit(OpCodes.Stloc, pickupable);
        generator.Emit(OpCodes.Ldloc, pickupable);
        generator.Emit(OpCodes.Brfalse_S, returnTotal);
        generator.Emit(OpCodes.Ldloc, pickupable);
        generator.Emit(OpCodes.Callvirt, gameTypes.PickupableGetCellMethod);
        generator.Emit(OpCodes.Stloc, cell);
        generator.Emit(OpCodes.Ldloc, cell);
        generator.Emit(OpCodes.Call, gameTypes.GridIsValidCellMethod);
        generator.Emit(OpCodes.Brfalse_S, returnTotal);
        generator.Emit(OpCodes.Ldsfld, gameTypes.GridWorldIndicesField);
        generator.Emit(OpCodes.Ldloc, cell);
        generator.Emit(OpCodes.Ldelem_I4);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Bne_Un_S, returnTotal);
        generator.Emit(OpCodes.Ldloc, pickupable);
        generator.Emit(
            OpCodes.Callvirt,
            gameTypes.PickupablePrefabIdentityGetter);
        generator.Emit(OpCodes.Ldsfld, gameTypes.StoredPrivateTagField);
        generator.Emit(
            OpCodes.Callvirt,
            gameTypes.PrefabIdentityHasTagMethod);
        generator.Emit(OpCodes.Brtrue_S, returnTotal);

        if (mutation !=
            FastTrackContractMutation.SumTotalFilteredContributionAnchorMissing)
        {
            EmitFilteredPickupContribution(
                generator,
                totalAmount,
                pickupable,
                gameTypes.PickupableTotalAmountGetter);
        }

        if (mutation ==
            FastTrackContractMutation.SumTotalFilteredContributionAnchorDuplicated)
        {
            EmitFilteredPickupContribution(
                generator,
                totalAmount,
                pickupable,
                gameTypes.PickupableTotalAmountGetter);
        }

        generator.MarkLabel(returnTotal);
        generator.Emit(OpCodes.Ldloc, totalAmount);
        generator.Emit(OpCodes.Ret);
    }

    private static void EmitFilteredPickupContribution(
        ILGenerator generator,
        LocalBuilder totalAmount,
        LocalBuilder pickupable,
        MethodInfo pickupableTotalAmountGetter)
    {
        generator.Emit(OpCodes.Ldloc, totalAmount);
        generator.Emit(OpCodes.Ldloc, pickupable);
        generator.Emit(OpCodes.Callvirt, pickupableTotalAmountGetter);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Stloc, totalAmount);
    }

    private static EmittedPickupGroupingContract DefinePickupGroupingContract(
        ModuleBuilder moduleBuilder,
        EmittedGameContractTypes gameTypes,
        FastTrackContractMutation mutation)
    {
        TypeBuilder fastUpdateBuilder = moduleBuilder.DefineType(
            "PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate",
            TypeAttributes.NotPublic |
            TypeAttributes.Abstract |
            TypeAttributes.Sealed);
        MethodBuilder beforeUpdatePickupsBuilder = fastUpdateBuilder.DefineMethod(
            "BeforeUpdatePickups",
            MethodAttributes.Assembly | MethodAttributes.Static,
            typeof(bool),
            new[]
            {
                gameTypes.FetchablesByPrefabIdType,
                gameTypes.NavigatorType,
                typeof(int)
            });
        ILGenerator beforeUpdateGenerator =
            beforeUpdatePickupsBuilder.GetILGenerator();
        beforeUpdateGenerator.Emit(OpCodes.Ldc_I4_0);
        beforeUpdateGenerator.Emit(OpCodes.Ret);

        TypeBuilder keyBuilder = fastUpdateBuilder.DefineNestedType(
            "PickupTagKey",
            TypeAttributes.NestedAssembly |
            TypeAttributes.Sealed |
            TypeAttributes.SequentialLayout,
            typeof(ValueType));
        FieldBuilder hashField = keyBuilder.DefineField(
            "Hash",
            typeof(int),
            FieldAttributes.Assembly | FieldAttributes.InitOnly);
        FieldBuilder identityField = keyBuilder.DefineField(
            "ID",
            gameTypes.PrefabIdentityType,
            FieldAttributes.Assembly | FieldAttributes.InitOnly);
        bool constructorArgumentsAreReversed = mutation ==
            FastTrackContractMutation
                .PickupTagKeyConstructorArgumentsReversed;
        Type[] keyConstructorParameterTypes = constructorArgumentsAreReversed
            ? new[] { gameTypes.PrefabIdentityType, typeof(int) }
            : new[] { typeof(int), gameTypes.PrefabIdentityType };
        ConstructorBuilder keyConstructor = keyBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            keyConstructorParameterTypes);
        ILGenerator keyConstructorGenerator = keyConstructor.GetILGenerator();
        keyConstructorGenerator.Emit(OpCodes.Ldarg_0);
        keyConstructorGenerator.Emit(
            constructorArgumentsAreReversed ? OpCodes.Ldarg_2 : OpCodes.Ldarg_1);
        keyConstructorGenerator.Emit(OpCodes.Stfld, hashField);
        keyConstructorGenerator.Emit(OpCodes.Ldarg_0);
        keyConstructorGenerator.Emit(
            constructorArgumentsAreReversed ? OpCodes.Ldarg_1 : OpCodes.Ldarg_2);
        keyConstructorGenerator.Emit(OpCodes.Stfld, identityField);
        keyConstructorGenerator.Emit(OpCodes.Ret);

        MethodBuilder typedEqualsBuilder = keyBuilder.DefineMethod(
            "Equals",
            MethodAttributes.Public |
            MethodAttributes.Virtual |
            MethodAttributes.Final,
            typeof(bool),
            new[] { keyBuilder });
        EmitPickupTagKeyEquality(
            typedEqualsBuilder.GetILGenerator(),
            hashField,
            identityField,
            mutation ==
                FastTrackContractMutation.PickupTagKeyEqualityUsesAllocatedIdentity);
        TypeBuilder dictionaryBuilder = fastUpdateBuilder.DefineNestedType(
            "PickupTagDict",
            TypeAttributes.NestedPrivate | TypeAttributes.Sealed);
        DefineDefaultConstructor(dictionaryBuilder);
        MethodBuilder addItemBuilder = dictionaryBuilder.DefineMethod(
            "AddItem",
            MethodAttributes.Public,
            typeof(void),
            mutation == FastTrackContractMutation.AddItemSignatureChanged
                ? new[]
                {
                    gameTypes.FetchableType.MakeByRefType(),
                    typeof(long)
                }
                : new[]
                {
                    gameTypes.FetchableType.MakeByRefType(),
                    typeof(int)
                });
        ILGenerator addItemGenerator = addItemBuilder.GetILGenerator();
        LocalBuilder originalTagBitsHash =
            addItemGenerator.DeclareLocal(typeof(int));
        LocalBuilder pickupable =
            addItemGenerator.DeclareLocal(gameTypes.PickupableType);
        addItemGenerator.Emit(OpCodes.Ldarg_1);
        addItemGenerator.Emit(
            OpCodes.Ldfld,
            gameTypes.FetchableTagBitsHashField);
        addItemGenerator.Emit(OpCodes.Stloc, originalTagBitsHash);
        addItemGenerator.Emit(OpCodes.Ldarg_1);
        addItemGenerator.Emit(
            OpCodes.Ldfld,
            gameTypes.FetchablePickupableField);
        addItemGenerator.Emit(OpCodes.Stloc, pickupable);
        int constructorCallCount = mutation switch
        {
            FastTrackContractMutation.AddItemConstructorAnchorMissing => 0,
            FastTrackContractMutation.AddItemConstructorAnchorDuplicated => 2,
            _ => 1
        };
        for (var callIndex = 0; callIndex < constructorCallCount; callIndex++)
        {
            LocalBuilder keyLocal = addItemGenerator.DeclareLocal(keyBuilder);
            addItemGenerator.Emit(OpCodes.Ldloca, keyLocal);
            if (constructorArgumentsAreReversed)
            {
                addItemGenerator.Emit(OpCodes.Ldloc, pickupable);
                addItemGenerator.Emit(
                    OpCodes.Ldfld,
                    gameTypes.PickupablePrefabIdentityField);
                addItemGenerator.Emit(OpCodes.Ldloc, originalTagBitsHash);
            }
            else
            {
                addItemGenerator.Emit(OpCodes.Ldloc, originalTagBitsHash);
                addItemGenerator.Emit(OpCodes.Ldloc, pickupable);
                addItemGenerator.Emit(
                    OpCodes.Ldfld,
                    gameTypes.PickupablePrefabIdentityField);
            }

            addItemGenerator.Emit(OpCodes.Call, keyConstructor);
        }

        addItemGenerator.Emit(OpCodes.Ret);
        keyBuilder.CreateType();
        Type pickupTagDictionaryType = dictionaryBuilder.CreateType()!;
        Type fastUpdateType = fastUpdateBuilder.CreateType()!;

        return new EmittedPickupGroupingContract(
            gameTypes.UpdatePickupsTarget,
            fastUpdateType.GetMethod(
                "BeforeUpdatePickups",
                BindingFlags.Static | BindingFlags.NonPublic)!,
            pickupTagDictionaryType.GetMethod("AddItem")!);
    }

    private static void EmitPickupTagKeyEquality(
        ILGenerator generator,
        FieldInfo hashField,
        FieldInfo identityField,
        bool compareAllocatedIdentity)
    {
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldfld, hashField);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Ldfld, hashField);
        generator.Emit(OpCodes.Ceq);
        if (compareAllocatedIdentity)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, identityField);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldfld, identityField);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.And);
        }

        generator.Emit(OpCodes.Ret);
    }

    private static EmittedDirectDeliveryContract DefineDirectDeliveryContract(
        ModuleBuilder moduleBuilder,
        EmittedGameContractTypes gameTypes,
        FastTrackContractMutation mutation)
    {
        TypeBuilder comparatorBuilder = moduleBuilder.DefineType(
            "PeterHan.FastTrack.GamePatches.ChoreComparator",
            TypeAttributes.NotPublic | TypeAttributes.Sealed);
        DefineDefaultConstructor(comparatorBuilder);
        Type[] comparatorParameterTypes =
            mutation == FastTrackContractMutation.DirectComparatorSignatureChanged
                ? new[]
                {
                    gameTypes.PreconditionContextType.MakeByRefType(),
                    gameTypes.FetchChoreType,
                    typeof(int)
                }
                : new[]
                {
                    gameTypes.PreconditionContextType.MakeByRefType(),
                    gameTypes.FetchChoreType,
                    gameTypes.SortedClearableType.MakeByRefType()
                };
        MethodBuilder comparatorBuilderMethod = comparatorBuilder.DefineMethod(
            "CheckFetchChore",
            MethodAttributes.Private,
            typeof(bool),
            comparatorParameterTypes);
        ILGenerator comparatorGenerator =
            comparatorBuilderMethod.GetILGenerator();
        if (mutation ==
            FastTrackContractMutation.DirectComparatorSuccessReturnMissing)
        {
            comparatorGenerator.Emit(OpCodes.Ldc_I4_0);
            comparatorGenerator.Emit(OpCodes.Ret);
        }
        else if (mutation ==
                 FastTrackContractMutation
                     .DirectComparatorSuccessReturnDuplicated)
        {
            Label secondSuccessReturn = comparatorGenerator.DefineLabel();
            comparatorGenerator.Emit(OpCodes.Ldarg_2);
            comparatorGenerator.Emit(OpCodes.Brfalse_S, secondSuccessReturn);
            comparatorGenerator.Emit(OpCodes.Ldc_I4_1);
            comparatorGenerator.Emit(OpCodes.Ret);
            comparatorGenerator.MarkLabel(secondSuccessReturn);
            comparatorGenerator.Emit(OpCodes.Ldc_I4_1);
            comparatorGenerator.Emit(OpCodes.Ret);
        }
        else
        {
            comparatorGenerator.Emit(OpCodes.Ldc_I4_1);
            comparatorGenerator.Emit(OpCodes.Ret);
        }
        comparatorBuilder.CreateType();

        TypeBuilder chorePatchesBuilder = moduleBuilder.DefineType(
            "PeterHan.FastTrack.GamePatches.ChorePatches",
            TypeAttributes.NotPublic |
            TypeAttributes.Abstract |
            TypeAttributes.Sealed);
        TypeBuilder globalCollectionPatchBuilder =
            chorePatchesBuilder.DefineNestedType(
                "GlobalChoreProvider_CollectChores_Patch",
                TypeAttributes.NestedAssembly |
                TypeAttributes.Abstract |
                TypeAttributes.Sealed);
        Type listOfContexts = typeof(List<>).MakeGenericType(
            gameTypes.PreconditionContextType);
        MethodBuilder prefixBuilder = globalCollectionPatchBuilder.DefineMethod(
            "Prefix",
            MethodAttributes.Assembly | MethodAttributes.Static,
            typeof(bool),
            new[]
            {
                gameTypes.ChoreConsumerStateType,
                gameTypes.GlobalChoreProviderType,
                listOfContexts
            });
        ILGenerator prefixGenerator = prefixBuilder.GetILGenerator();
        prefixGenerator.Emit(OpCodes.Ldc_I4_0);
        prefixGenerator.Emit(OpCodes.Ret);
        Type globalCollectionPatchType = globalCollectionPatchBuilder.CreateType()!;
        chorePatchesBuilder.CreateType();

        return new EmittedDirectDeliveryContract(
            gameTypes.GlobalChoreCollectionTarget,
            globalCollectionPatchType.GetMethod(
                "Prefix",
                BindingFlags.Static | BindingFlags.NonPublic)!);
    }

    private static Type DefineReferenceType(
        ModuleBuilder moduleBuilder,
        string fullName)
    {
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            fullName,
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(typeBuilder);
        return typeBuilder.CreateType()!;
    }

    private static Type DefineValueType(
        ModuleBuilder moduleBuilder,
        string fullName) =>
        moduleBuilder.DefineType(
                fullName,
                TypeAttributes.Public |
                TypeAttributes.Sealed |
                TypeAttributes.SequentialLayout,
                typeof(ValueType))
            .CreateType()!;

    private static void DefineDefaultConstructor(
        TypeBuilder typeBuilder,
        ConstructorInfo? baseConstructor = null)
    {
        ConstructorBuilder constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);
        ILGenerator generator = constructor.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(
            OpCodes.Call,
            baseConstructor ??
                typeof(object).GetConstructor(Type.EmptyTypes)!);
        generator.Emit(OpCodes.Ret);
    }

    private enum FastTrackContractMutation
    {
        None,
        RunUpdateSignatureChanged,
        RunUpdateMissingSingleTagBranch,
        RunUpdateFirstUpdateBranchReversed,
        RunUpdateTotalsInCompleteBranchOnly,
        RunUpdateResourceTagPublicationAnchorMissing,
        RunUpdateResourceTagPublicationAnchorDuplicated,
        RunUpdateInventoryFieldAnchorMissing,
        RunUpdateInventoryFieldAnchorDuplicated,
        SumTotalFilteredContributionAnchorMissing,
        SumTotalFilteredContributionAnchorDuplicated,
        RemovedFetchableDeletesTagKey,
        PickupTagKeyEqualityUsesAllocatedIdentity,
        AddItemConstructorAnchorMissing,
        AddItemConstructorAnchorDuplicated,
        PickupTagKeyConstructorArgumentsReversed,
        AddItemSignatureChanged,
        DirectComparatorSignatureChanged,
        DirectComparatorSuccessReturnMissing,
        DirectComparatorSuccessReturnDuplicated
    }

    private sealed record EmittedGameContractTypes(
        Type TagType,
        Type PickupableType,
        Type PrefabIdentityType,
        MethodInfo PickupableGetCellMethod,
        MethodInfo PickupableTotalAmountGetter,
        MethodInfo PickupablePrefabIdentityGetter,
        MethodInfo PrefabIdentityHasTagMethod,
        MethodInfo GridIsValidCellMethod,
        FieldInfo GridWorldIndicesField,
        FieldInfo StoredPrivateTagField,
        Type NavigatorType,
        Type WorldContainerType,
        Type WorldInventoryType,
        MethodInfo WorldInventoryUpdateTarget,
        FieldInfo WorldInventoryEntriesField,
        Type FetchablesByPrefabIdType,
        MethodInfo UpdatePickupsTarget,
        Type FetchableType,
        FieldInfo FetchableTagBitsHashField,
        FieldInfo FetchablePickupableField,
        FieldInfo PickupablePrefabIdentityField,
        Type ChoreConsumerStateType,
        Type FetchChoreType,
        Type PreconditionContextType,
        Type SortedClearableType,
        Type GlobalChoreProviderType,
        MethodInfo GlobalChoreCollectionTarget);

    private sealed record EmittedWorldInventoryContract(
        MethodInfo WorldInventoryUpdateTarget,
        MethodInfo WorldInventoryReplacementPrefix,
        MethodInfo RemovedFetchablePrefix);

    private sealed record EmittedPickupGroupingContract(
        MethodInfo UpdatePickupsTarget,
        MethodInfo BeforeUpdatePickupsPrefix,
        MethodInfo AddItemMethod);

    private sealed record EmittedDirectDeliveryContract(
        MethodInfo GlobalChoreCollectionTarget,
        MethodInfo GlobalChoreCollectionPrefix);
}

/// <summary>
/// Exposes the emitted assembly and each independently selectable active
/// FastTrack replacement descriptor to inspector tests.
/// </summary>
internal sealed class FastTrackEmittedAssembly
{
    internal FastTrackEmittedAssembly(
        Assembly assembly,
        ActiveHarmonyPrefixDescriptor worldInventoryReplacement,
        ActiveHarmonyPrefixDescriptor pickupGroupingReplacement,
        ActiveHarmonyPrefixDescriptor directDeliveryEligibilityReplacement)
    {
        Assembly = assembly;
        WorldInventoryReplacement = worldInventoryReplacement;
        PickupGroupingReplacement = pickupGroupingReplacement;
        DirectDeliveryEligibilityReplacement =
            directDeliveryEligibilityReplacement;
    }

    internal Assembly Assembly { get; }

    internal ActiveHarmonyPrefixDescriptor WorldInventoryReplacement { get; }

    internal ActiveHarmonyPrefixDescriptor PickupGroupingReplacement { get; }

    internal ActiveHarmonyPrefixDescriptor
        DirectDeliveryEligibilityReplacement { get; }

    internal IReadOnlyList<ActiveHarmonyPrefixDescriptor> AllReplacements =>
        new[]
        {
            WorldInventoryReplacement,
            PickupGroupingReplacement,
            DirectDeliveryEligibilityReplacement
        };

    internal ActiveHarmonyPrefixDescriptor WithHarmonyOwner(
        ActiveHarmonyPrefixDescriptor descriptor,
        string harmonyOwner) =>
        new(
            descriptor.TargetMethod,
            descriptor.PrefixMethod,
            harmonyOwner,
            descriptor.Priority);
}
