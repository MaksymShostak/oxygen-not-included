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

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateSignatureChanged() =>
        Create(FastTrackContractMutation.RunUpdateSignatureChanged);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateMissingSingleTagBranch() =>
        Create(FastTrackContractMutation.RunUpdateMissingSingleTagBranch);

    internal static FastTrackEmittedAssembly
        CreateWithRunUpdateTotalsInCompleteBranchOnly() =>
        Create(FastTrackContractMutation.RunUpdateTotalsInCompleteBranchOnly);

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
        CreateWithDirectComparatorContractChanged() =>
        Create(FastTrackContractMutation.DirectComparatorSignatureChanged);

    private static FastTrackEmittedAssembly Create(
        FastTrackContractMutation mutation)
    {
        var assemblyName = new AssemblyName(
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
            new ActiveHarmonyPatchDescriptor(
                worldInventoryContract.WorldInventoryUpdateTarget,
                worldInventoryContract.WorldInventoryReplacementPrefix,
                FastTrackHarmonyOwner,
                HarmonyNormalPriority),
            new ActiveHarmonyPatchDescriptor(
                pickupGroupingContract.UpdatePickupsTarget,
                pickupGroupingContract.BeforeUpdatePickupsPrefix,
                FastTrackHarmonyOwner,
                HarmonyNormalPriority),
            new ActiveHarmonyPatchDescriptor(
                directDeliveryContract.GlobalChoreCollectionTarget,
                directDeliveryContract.GlobalChoreCollectionPrefix,
                FastTrackHarmonyOwner,
                HarmonyNormalPriority));
    }

    private static EmittedGameContractTypes DefineGameContractTypes(
        ModuleBuilder moduleBuilder)
    {
        Type tagType = DefineValueType(moduleBuilder, "Tag");
        Type pickupableType = DefineReferenceType(moduleBuilder, "Pickupable");
        Type prefabIdentityType = DefineReferenceType(moduleBuilder, "KPrefabID");
        Type navigatorType = DefineReferenceType(moduleBuilder, "Navigator");
        Type worldContainerType = DefineReferenceType(
            moduleBuilder,
            "WorldContainer");

        TypeBuilder worldInventoryBuilder = moduleBuilder.DefineType(
            "WorldInventory",
            TypeAttributes.Public | TypeAttributes.Class);
        DefineDefaultConstructor(worldInventoryBuilder);
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
            navigatorType,
            worldContainerType,
            worldInventoryType,
            worldInventoryType.GetMethod("Update")!,
            fetchablesByPrefabIdType,
            fetchablesByPrefabIdType.GetMethod("UpdatePickups")!,
            fetchableType,
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
        Type pickupableSequenceType = typeof(IEnumerable<>).MakeGenericType(
            gameTypes.PickupableType);

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
        backgroundInventoryBuilder.DefineField(
            "worldInventory",
            gameTypes.WorldInventoryType,
            FieldAttributes.Private);
        DefineDefaultConstructor(backgroundInventoryBuilder);

        MethodBuilder sumTotalBuilder = backgroundInventoryBuilder.DefineMethod(
            "SumTotal",
            MethodAttributes.Private | MethodAttributes.Static,
            typeof(float),
            new[] { pickupableSequenceType, typeof(int) });
        ILGenerator sumTotalGenerator = sumTotalBuilder.GetILGenerator();
        sumTotalGenerator.Emit(OpCodes.Ldc_R4, 0.0f);
        sumTotalGenerator.Emit(OpCodes.Ret);

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
            sumTotalBuilder,
            mutation == FastTrackContractMutation.RunUpdateMissingSingleTagBranch,
            mutation ==
                FastTrackContractMutation.RunUpdateTotalsInCompleteBranchOnly,
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

        _ = accessibleAmountDictionaryType;
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
        MethodInfo sumTotalMethod,
        bool omitSingleTagBranch,
        bool putBothTotalsInCompleteBranch,
        Type returnType)
    {
        Label singleTagBranch = generator.DefineLabel();
        Label exit = generator.DefineLabel();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldfld, firstUpdateField);
        generator.Emit(OpCodes.Brfalse_S, singleTagBranch);

        EmitSumTotalCall(generator, sumTotalMethod);
        generator.Emit(OpCodes.Pop);
        if (putBothTotalsInCompleteBranch)
        {
            EmitSumTotalCall(generator, sumTotalMethod);
            generator.Emit(OpCodes.Pop);
        }

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldc_I4_1);
        generator.Emit(OpCodes.Stfld, updateIndexField);
        generator.Emit(OpCodes.Br_S, exit);

        generator.MarkLabel(singleTagBranch);
        if (!omitSingleTagBranch)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, updateIndexField);
            generator.Emit(OpCodes.Pop);
            if (!putBothTotalsInCompleteBranch)
            {
                EmitSumTotalCall(generator, sumTotalMethod);
                generator.Emit(OpCodes.Pop);
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

    private static void EmitSumTotalCall(
        ILGenerator generator,
        MethodInfo sumTotalMethod)
    {
        generator.Emit(OpCodes.Ldnull);
        generator.Emit(OpCodes.Ldc_I4_0);
        generator.Emit(OpCodes.Call, sumTotalMethod);
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
        ConstructorBuilder keyConstructor = keyBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new[] { typeof(int), gameTypes.PrefabIdentityType });
        ILGenerator keyConstructorGenerator = keyConstructor.GetILGenerator();
        keyConstructorGenerator.Emit(OpCodes.Ldarg_0);
        keyConstructorGenerator.Emit(OpCodes.Ldarg_1);
        keyConstructorGenerator.Emit(OpCodes.Stfld, hashField);
        keyConstructorGenerator.Emit(OpCodes.Ldarg_0);
        keyConstructorGenerator.Emit(OpCodes.Ldarg_2);
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
            new[] { gameTypes.FetchableType.MakeByRefType(), typeof(int) });
        ILGenerator addItemGenerator = addItemBuilder.GetILGenerator();
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
            addItemGenerator.Emit(OpCodes.Ldc_I4_0);
            addItemGenerator.Emit(OpCodes.Ldnull);
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
        comparatorGenerator.Emit(OpCodes.Ldc_I4_1);
        comparatorGenerator.Emit(OpCodes.Ret);
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

    private static void DefineDefaultConstructor(TypeBuilder typeBuilder)
    {
        ConstructorBuilder constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);
        ILGenerator generator = constructor.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        generator.Emit(OpCodes.Ret);
    }

    private enum FastTrackContractMutation
    {
        None,
        RunUpdateSignatureChanged,
        RunUpdateMissingSingleTagBranch,
        RunUpdateTotalsInCompleteBranchOnly,
        RemovedFetchableDeletesTagKey,
        PickupTagKeyEqualityUsesAllocatedIdentity,
        AddItemConstructorAnchorMissing,
        AddItemConstructorAnchorDuplicated,
        DirectComparatorSignatureChanged
    }

    private sealed record EmittedGameContractTypes(
        Type TagType,
        Type PickupableType,
        Type PrefabIdentityType,
        Type NavigatorType,
        Type WorldContainerType,
        Type WorldInventoryType,
        MethodInfo WorldInventoryUpdateTarget,
        Type FetchablesByPrefabIdType,
        MethodInfo UpdatePickupsTarget,
        Type FetchableType,
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
        ActiveHarmonyPatchDescriptor worldInventoryReplacement,
        ActiveHarmonyPatchDescriptor pickupGroupingReplacement,
        ActiveHarmonyPatchDescriptor directDeliveryEligibilityReplacement)
    {
        Assembly = assembly;
        WorldInventoryReplacement = worldInventoryReplacement;
        PickupGroupingReplacement = pickupGroupingReplacement;
        DirectDeliveryEligibilityReplacement =
            directDeliveryEligibilityReplacement;
    }

    internal Assembly Assembly { get; }

    internal ActiveHarmonyPatchDescriptor WorldInventoryReplacement { get; }

    internal ActiveHarmonyPatchDescriptor PickupGroupingReplacement { get; }

    internal ActiveHarmonyPatchDescriptor
        DirectDeliveryEligibilityReplacement { get; }

    internal IReadOnlyList<ActiveHarmonyPatchDescriptor> AllReplacements =>
        new[]
        {
            WorldInventoryReplacement,
            PickupGroupingReplacement,
            DirectDeliveryEligibilityReplacement
        };

    internal ActiveHarmonyPatchDescriptor WithHarmonyOwner(
        ActiveHarmonyPatchDescriptor descriptor,
        string harmonyOwner) =>
        new(
            descriptor.TargetMethod,
            descriptor.PatchMethod,
            harmonyOwner,
            descriptor.Priority);
}
