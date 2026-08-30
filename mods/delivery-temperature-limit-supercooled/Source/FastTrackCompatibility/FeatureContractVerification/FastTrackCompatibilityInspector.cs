#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Verifies the independently selectable FastTrack contracts once during
    /// loaded-game setup. It contains no Harmony, FastTrack, Klei, Unity, or PLib
    /// compile-time type dependency; runtime types are accepted only after their
    /// exact names, signatures, ownership, and required IL semantics agree.
    /// </summary>
    internal sealed class FastTrackCompatibilityInspector
    {
        private const string FastTrackHarmonyOwner = "PeterHan.FastTrack";
        private const string BackgroundWorldInventoryTypeName =
            "PeterHan.FastTrack.UIPatches.BackgroundWorldInventory";
        private const string WorldInventoryReplacementPatchTypeName =
            "PeterHan.FastTrack.UIPatches." +
            "WorldInventory_UpdateReplace_Patch";
        private const string WorldInventoryRemovalPatchTypeName =
            "PeterHan.FastTrack.UIPatches." +
            "WorldInventory_OnRemovedFetchable_Patch";
        private const string FetchManagerFastUpdateTypeName =
            "PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate";
        private const string PickupTagKeyNestedTypeName = "PickupTagKey";
        private const string PickupTagDictionaryNestedTypeName = "PickupTagDict";
        private const string ChoreComparatorTypeName =
            "PeterHan.FastTrack.GamePatches.ChoreComparator";
        private const string DirectDeliveryPatchTypeName =
            "PeterHan.FastTrack.GamePatches.ChorePatches+" +
            "GlobalChoreProvider_CollectChores_Patch";

        private static readonly Version SupportedFastTrackFileVersion =
            new Version(0, 18, 4, 0);
        private static readonly OpCode[] SingleByteOpCodes =
            BuildOpCodeLookup(multiByte: false);
        private static readonly OpCode[] MultiByteOpCodes =
            BuildOpCodeLookup(multiByte: true);

        private readonly IFastTrackAssemblyFileIdentityReader
            assemblyFileIdentityReader;

        internal FastTrackCompatibilityInspector(
            IFastTrackAssemblyFileIdentityReader assemblyFileIdentityReader)
        {
            this.assemblyFileIdentityReader = assemblyFileIdentityReader ??
                throw new ArgumentNullException(
                    nameof(assemblyFileIdentityReader));
        }

        internal FastTrackCompatibilityReport Inspect(
            FastTrackLoadedGameInspectionInput inspectionInput)
        {
            if (inspectionInput == null)
            {
                throw new ArgumentNullException(nameof(inspectionInput));
            }

            Assembly? fastTrackAssembly = inspectionInput.FastTrackAssembly;
            if (!inspectionInput.IsFastTrackEnabledForLoadedGame ||
                fastTrackAssembly == null)
            {
                return CreateModNotLoadedReport();
            }

            AssemblyName assemblyName = fastTrackAssembly.GetName();
            // Perform this read once even if every feature later proves inactive.
            // The immutable result is shared by all independently classified
            // features and by diagnostics; no adapter may reopen the file.
            FastTrackAssemblyFileIdentity fileIdentity =
                assemblyFileIdentityReader.Read(fastTrackAssembly);
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes =
                inspectionInput.ActiveHarmonyPrefixes;

            bool worldInventoryIsActive = HasActiveWorldInventoryReplacement(
                fastTrackAssembly,
                activePrefixes);
            bool pickupGroupingIsActive = HasActivePickupGroupingReplacement(
                fastTrackAssembly,
                activePrefixes);
            bool directDeliveryEligibilityIsActive =
                HasActiveDirectDeliveryEligibilityReplacement(
                    fastTrackAssembly,
                    activePrefixes);

            FastTrackFeatureCompatibility worldInventory = ClassifyFeature(
                FastTrackFeature.WorldInventory,
                worldInventoryIsActive,
                fileIdentity,
                () => VerifyWorldInventoryContract(fastTrackAssembly));
            FastTrackFeatureCompatibility pickupGrouping = ClassifyFeature(
                FastTrackFeature.PickupGrouping,
                pickupGroupingIsActive,
                fileIdentity,
                () => VerifyPickupGroupingContract(fastTrackAssembly));
            FastTrackFeatureCompatibility directDeliveryEligibility =
                ClassifyFeature(
                    FastTrackFeature.DirectDeliveryEligibility,
                    directDeliveryEligibilityIsActive,
                    fileIdentity,
                    () => VerifyDirectDeliveryEligibilityContract(
                        fastTrackAssembly));

            return new FastTrackCompatibilityReport(
                assemblyName.FullName,
                assemblyName.Version,
                fileIdentity.ReadState,
                fileIdentity.FileVersion,
                fileIdentity.AssemblySha256,
                worldInventory,
                pickupGrouping,
                directDeliveryEligibility);
        }

        private static FastTrackCompatibilityReport CreateModNotLoadedReport() =>
            new FastTrackCompatibilityReport(
                null,
                null,
                FastTrackAssemblyFileIdentityReadState.NotRead,
                null,
                null,
                FastTrackFeatureCompatibility.ModNotLoaded(
                    FastTrackFeature.WorldInventory),
                FastTrackFeatureCompatibility.ModNotLoaded(
                    FastTrackFeature.PickupGrouping),
                FastTrackFeatureCompatibility.ModNotLoaded(
                    FastTrackFeature.DirectDeliveryEligibility));

        private static FastTrackFeatureCompatibility ClassifyFeature(
            FastTrackFeature feature,
            bool replacementIsActive,
            FastTrackAssemblyFileIdentity fileIdentity,
            Func<IDictionary<FastTrackVerifiedMember, MemberInfo>>
                verifyStructuralContract)
        {
            if (!replacementIsActive)
            {
                return FastTrackFeatureCompatibility.ReplacementInactive(feature);
            }

            if (fileIdentity.ReadState !=
                FastTrackAssemblyFileIdentityReadState.Success)
            {
                return FastTrackFeatureCompatibility.Incompatible(
                    feature,
                    FastTrackFeatureCompatibilityFailureCode
                        .AssemblyFileIdentityUnavailable,
                    "The active " +
                    feature +
                    " FastTrack replacement has no verified physical assembly " +
                    "identity. Reader state: " +
                    fileIdentity.ReadState +
                    ". " +
                    fileIdentity.FailureMessage);
            }

            if (!SupportedFastTrackFileVersion.Equals(fileIdentity.FileVersion))
            {
                return FastTrackFeatureCompatibility.Incompatible(
                    feature,
                    FastTrackFeatureCompatibilityFailureCode
                        .UnsupportedFileVersion,
                    "The active " +
                    feature +
                    " FastTrack replacement requires file version " +
                    SupportedFastTrackFileVersion +
                    " exactly, but the loaded file reports " +
                    fileIdentity.FileVersion +
                    ".");
            }

            try
            {
                return FastTrackFeatureCompatibility.Ready(
                    feature,
                    verifyStructuralContract());
            }
            catch (HarmonyPatchContractViolationException exception)
            {
                return FastTrackFeatureCompatibility.Incompatible(
                    feature,
                    GetStructuralFailureCode(feature),
                    exception.Message);
            }
        }

        private static FastTrackFeatureCompatibilityFailureCode
            GetStructuralFailureCode(FastTrackFeature feature)
        {
            switch (feature)
            {
                case FastTrackFeature.WorldInventory:
                    return FastTrackFeatureCompatibilityFailureCode
                        .WorldInventoryContractViolation;
                case FastTrackFeature.PickupGrouping:
                    return FastTrackFeatureCompatibilityFailureCode
                        .PickupGroupingContractViolation;
                case FastTrackFeature.DirectDeliveryEligibility:
                    return FastTrackFeatureCompatibilityFailureCode
                        .DirectDeliveryEligibilityContractViolation;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(feature),
                        feature,
                        "Unknown FastTrack feature.");
            }
        }

        private static bool HasActiveWorldInventoryReplacement(
            Assembly fastTrackAssembly,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes) =>
            ContainsOwnedPrefix(
                fastTrackAssembly,
                activePrefixes,
                WorldInventoryReplacementPatchTypeName,
                "Prefix",
                IsWorldInventoryUpdateTarget);

        private static bool HasActivePickupGroupingReplacement(
            Assembly fastTrackAssembly,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes) =>
            ContainsOwnedPrefix(
                fastTrackAssembly,
                activePrefixes,
                FetchManagerFastUpdateTypeName,
                "BeforeUpdatePickups",
                IsUpdatePickupsTarget);

        private static bool HasActiveDirectDeliveryEligibilityReplacement(
            Assembly fastTrackAssembly,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes) =>
            ContainsOwnedPrefix(
                fastTrackAssembly,
                activePrefixes,
                DirectDeliveryPatchTypeName,
                "Prefix",
                IsGlobalChoreCollectionTarget);

        private static bool ContainsOwnedPrefix(
            Assembly fastTrackAssembly,
            IReadOnlyList<ActiveHarmonyPatchDescriptor> activePrefixes,
            string patchDeclaringTypeName,
            string patchMethodName,
            Func<MethodBase, bool> targetContract)
        {
            for (var prefixIndex = 0;
                 prefixIndex < activePrefixes.Count;
                 prefixIndex++)
            {
                ActiveHarmonyPatchDescriptor prefix =
                    activePrefixes[prefixIndex];
                Type? patchDeclaringType = prefix.PatchMethod.DeclaringType;
                if (patchDeclaringType != null &&
                    ReferenceEquals(
                        patchDeclaringType.Module,
                        fastTrackAssembly.ManifestModule) &&
                    string.Equals(
                        patchDeclaringType.FullName,
                        patchDeclaringTypeName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        prefix.PatchMethod.Name,
                        patchMethodName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        prefix.HarmonyOwner,
                        FastTrackHarmonyOwner,
                        StringComparison.Ordinal) &&
                    targetContract(prefix.TargetMethod))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWorldInventoryUpdateTarget(MethodBase target) =>
            HasMethodContract(
                target,
                "WorldInventory",
                "Update",
                "System.Void",
                new string[0]);

        private static bool IsUpdatePickupsTarget(MethodBase target) =>
            HasMethodContract(
                target,
                "FetchManager+FetchablesByPrefabId",
                "UpdatePickups",
                "System.Void",
                new[] { "Navigator", "System.Int32" });

        private static bool IsGlobalChoreCollectionTarget(MethodBase target) =>
            HasMethodContract(
                target,
                "GlobalChoreProvider",
                "CollectChores",
                "System.Void",
                new[]
                {
                    "ChoreConsumerState",
                    "System.Collections.Generic.List`1[" +
                    "Chore+Precondition+Context]"
                });

        private static bool HasMethodContract(
            MethodBase method,
            string declaringTypeName,
            string methodName,
            string returnTypeName,
            IReadOnlyList<string> parameterTypeNames)
        {
            Type? declaringType = method.DeclaringType;
            var methodInfo = method as MethodInfo;
            if (declaringType == null || methodInfo == null ||
                !string.Equals(
                    declaringType.FullName,
                    declaringTypeName,
                    StringComparison.Ordinal) ||
                !string.Equals(method.Name, methodName, StringComparison.Ordinal) ||
                !string.Equals(
                    GetStableTypeName(methodInfo.ReturnType),
                    returnTypeName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return ParametersMatch(method.GetParameters(), parameterTypeNames);
        }

        private static IDictionary<FastTrackVerifiedMember, MemberInfo>
            VerifyWorldInventoryContract(Assembly fastTrackAssembly)
        {
            Type backgroundInventoryType = RequireType(
                fastTrackAssembly,
                BackgroundWorldInventoryTypeName);
            FieldInfo firstUpdateField = RequireField(
                backgroundInventoryType,
                "firstUpdate",
                isStatic: false,
                isPublic: false,
                "System.Boolean");
            FieldInfo updateIndexField = RequireField(
                backgroundInventoryType,
                "updateIndex",
                isStatic: false,
                isPublic: false,
                "System.Int32");
            FieldInfo worldContainerField = RequireField(
                backgroundInventoryType,
                "worldContainer",
                isStatic: false,
                isPublic: false,
                "WorldContainer");
            FieldInfo worldInventoryField = RequireField(
                backgroundInventoryType,
                "worldInventory",
                isStatic: false,
                isPublic: false,
                "WorldInventory");
            MethodInfo sumTotal = RequireMethod(
                backgroundInventoryType,
                "SumTotal",
                isStatic: true,
                isPublic: false,
                "System.Single",
                new[]
                {
                    "System.Collections.Generic.IEnumerable`1[Pickupable]",
                    "System.Int32"
                });
            MethodInfo runUpdate = RequireMethod(
                backgroundInventoryType,
                "RunUpdate",
                isStatic: false,
                isPublic: false,
                "System.Void",
                new string[0]);
            Type replacementPatchType = RequireType(
                fastTrackAssembly,
                WorldInventoryReplacementPatchTypeName);
            MethodInfo replacementPrefix = RequireMethod(
                replacementPatchType,
                "Prefix",
                isStatic: true,
                isPublic: false,
                "System.Boolean",
                new[] { "WorldInventory" });
            Type removalPatchType = RequireType(
                fastTrackAssembly,
                WorldInventoryRemovalPatchTypeName);
            MethodInfo removedFetchablePrefix = RequireMethod(
                removalPatchType,
                "Prefix",
                isStatic: true,
                isPublic: false,
                "System.Boolean",
                new[] { "WorldInventory", "System.Object" });

            VerifyCompleteAndSingleResourceTagUpdateBranches(
                runUpdate,
                sumTotal,
                firstUpdateField,
                updateIndexField);
            FieldInfo worldInventoryEntriesField =
                RequireWorldInventoryEntriesFieldAnchor(runUpdate);
            VerifyResourceTagPublicationAnchors(runUpdate, sumTotal);
            VerifyFilteredPickupContributionAnchor(sumTotal);
            VerifyRemovalPreservesInventoryDictionaryKeys(
                removedFetchablePrefix);

            return new Dictionary<FastTrackVerifiedMember, MemberInfo>
            {
                {
                    FastTrackVerifiedMember.BackgroundWorldInventoryRunUpdate,
                    runUpdate
                },
                {
                    FastTrackVerifiedMember.BackgroundWorldInventorySumTotal,
                    sumTotal
                },
                {
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryFirstUpdateField,
                    firstUpdateField
                },
                {
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryUpdateIndexField,
                    updateIndexField
                },
                {
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryWorldContainerField,
                    worldContainerField
                },
                {
                    FastTrackVerifiedMember
                        .BackgroundWorldInventoryWorldInventoryField,
                    worldInventoryField
                },
                {
                    FastTrackVerifiedMember.WorldInventoryInventoryField,
                    worldInventoryEntriesField
                },
                {
                    FastTrackVerifiedMember.WorldInventoryReplacementPrefix,
                    replacementPrefix
                },
                {
                    FastTrackVerifiedMember.WorldInventoryRemovedFetchablePrefix,
                    removedFetchablePrefix
                }
            };
        }

        private static FieldInfo RequireWorldInventoryEntriesFieldAnchor(
            MethodInfo runUpdate)
        {
            IReadOnlyList<DecodedIlInstruction> instructions = Decode(runUpdate);
            FieldInfo? inventoryEntriesField = null;
            int inventoryEntriesFieldLoadCount = 0;
            for (var instructionIndex = 0;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                DecodedIlInstruction instruction =
                    instructions[instructionIndex];
                if (instruction.OpCode != OpCodes.Ldfld ||
                    !instruction.MetadataToken.HasValue)
                {
                    continue;
                }

                var field = ResolveMember(
                    runUpdate,
                    instruction.MetadataToken.Value) as FieldInfo;
                if (field == null ||
                    field.IsStatic ||
                    field.DeclaringType == null ||
                    !string.Equals(
                        GetStableTypeName(field.DeclaringType),
                        "WorldInventory",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        field.Name,
                        "Inventory",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        GetStableTypeName(field.FieldType),
                        "System.Collections.Generic.Dictionary`2[Tag," +
                        "System.Collections.Generic.HashSet`1[Pickupable]]",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                inventoryEntriesFieldLoadCount++;
                inventoryEntriesField = field;
            }

            if (inventoryEntriesFieldLoadCount != 1 ||
                inventoryEntriesField == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack BackgroundWorldInventory.RunUpdate requires " +
                    "exactly one typed WorldInventory.Inventory field anchor.");
            }

            return inventoryEntriesField;
        }

        private static void VerifyCompleteAndSingleResourceTagUpdateBranches(
            MethodInfo runUpdate,
            MethodInfo sumTotal,
            FieldInfo firstUpdateField,
            FieldInfo updateIndexField)
        {
            IReadOnlyList<DecodedIlInstruction> instructions = Decode(runUpdate);
            int firstUpdateReadCount = CountMemberInstructions(
                runUpdate,
                instructions,
                firstUpdateField,
                instruction => instruction.OpCode == OpCodes.Ldfld);
            int updateIndexReadCount = CountMemberInstructions(
                runUpdate,
                instructions,
                updateIndexField,
                instruction => instruction.OpCode == OpCodes.Ldfld);
            int updateIndexWriteCount = CountMemberInstructions(
                runUpdate,
                instructions,
                updateIndexField,
                instruction => instruction.OpCode == OpCodes.Stfld);
            int conditionalBranchCount = 0;
            int firstUpdateBranchInstructionIndex = -1;
            for (var instructionIndex = 0;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                if (instructions[instructionIndex].OpCode.FlowControl ==
                    FlowControl.Cond_Branch)
                {
                    conditionalBranchCount++;
                }

                if (instructionIndex + 1 < instructions.Count &&
                    instructions[instructionIndex].OpCode == OpCodes.Ldfld &&
                    InstructionReferencesMember(
                        runUpdate,
                        instructions[instructionIndex],
                        firstUpdateField) &&
                    instructions[instructionIndex + 1].OpCode.FlowControl ==
                    FlowControl.Cond_Branch)
                {
                    if (firstUpdateBranchInstructionIndex >= 0)
                    {
                        throw new HarmonyPatchContractViolationException(
                            "FastTrack BackgroundWorldInventory.RunUpdate has " +
                            "more than one firstUpdate branch anchor.");
                    }

                    firstUpdateBranchInstructionIndex = instructionIndex + 1;
                }
            }

            if (firstUpdateReadCount != 1 ||
                updateIndexReadCount < 1 || updateIndexWriteCount < 2 ||
                conditionalBranchCount < 1 ||
                firstUpdateBranchInstructionIndex < 0)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack BackgroundWorldInventory.RunUpdate no longer " +
                    "proves distinct complete and single-resource-tag branches " +
                    "selected by firstUpdate/updateIndex.");
            }

            DecodedIlInstruction firstUpdateBranch =
                instructions[firstUpdateBranchInstructionIndex];
            if ((firstUpdateBranch.OpCode != OpCodes.Brfalse &&
                 firstUpdateBranch.OpCode != OpCodes.Brfalse_S) ||
                !firstUpdateBranch.BranchTargetOffset.HasValue ||
                firstUpdateBranch.BranchTargetOffset.Value <=
                firstUpdateBranch.Offset)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack BackgroundWorldInventory.RunUpdate firstUpdate " +
                    "false branch does not have one forward " +
                    "single-resource-tag target.");
            }

            int singleResourceTagBranchOffset =
                firstUpdateBranch.BranchTargetOffset.Value;
            int completeBranchTotalCount = 0;
            int singleResourceTagBranchTotalCount = 0;
            int completeBranchUpdateIndexWriteCount = 0;
            int singleResourceTagBranchUpdateIndexReadCount = 0;
            int singleResourceTagBranchUpdateIndexWriteCount = 0;
            for (var instructionIndex = firstUpdateBranchInstructionIndex + 1;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                DecodedIlInstruction instruction = instructions[instructionIndex];
                bool belongsToSingleResourceTagBranch =
                    instruction.Offset >= singleResourceTagBranchOffset;
                if (IsCallInstruction(instruction) &&
                    InstructionReferencesMember(
                        runUpdate,
                        instruction,
                        sumTotal))
                {
                    if (belongsToSingleResourceTagBranch)
                    {
                        singleResourceTagBranchTotalCount++;
                    }
                    else
                    {
                        completeBranchTotalCount++;
                    }
                }

                if (instruction.OpCode == OpCodes.Ldfld &&
                    InstructionReferencesMember(
                        runUpdate,
                        instruction,
                        updateIndexField) &&
                    belongsToSingleResourceTagBranch)
                {
                    singleResourceTagBranchUpdateIndexReadCount++;
                }

                if (instruction.OpCode == OpCodes.Stfld &&
                    InstructionReferencesMember(
                        runUpdate,
                        instruction,
                        updateIndexField))
                {
                    if (belongsToSingleResourceTagBranch)
                    {
                        singleResourceTagBranchUpdateIndexWriteCount++;
                    }
                    else
                    {
                        completeBranchUpdateIndexWriteCount++;
                    }
                }
            }

            if (completeBranchTotalCount != 1 ||
                singleResourceTagBranchTotalCount != 1 ||
                completeBranchUpdateIndexWriteCount < 1 ||
                singleResourceTagBranchUpdateIndexReadCount < 1 ||
                singleResourceTagBranchUpdateIndexWriteCount < 1)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack BackgroundWorldInventory.RunUpdate must keep one " +
                    "SumTotal/updateIndex publication on opposite sides of the " +
                    "firstUpdate branch for complete and single-resource-tag " +
                    "updates.");
            }
        }

        private static void VerifyRemovalPreservesInventoryDictionaryKeys(
            MethodInfo removedFetchablePrefix)
        {
            IReadOnlyList<DecodedIlInstruction> instructions =
                Decode(removedFetchablePrefix);
            int pickupSetRemovalCount = 0;
            int dictionaryKeyRemovalCount = 0;
            for (var instructionIndex = 0;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                DecodedIlInstruction instruction = instructions[instructionIndex];
                if (!IsCallInstruction(instruction) ||
                    !instruction.MetadataToken.HasValue)
                {
                    continue;
                }

                MemberInfo member = ResolveMember(
                    removedFetchablePrefix,
                    instruction.MetadataToken.Value);
                var method = member as MethodBase;
                if (method == null ||
                    !string.Equals(
                        method.Name,
                        "Remove",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string declaringTypeName =
                    GetStableTypeName(method.DeclaringType!);
                if (declaringTypeName.StartsWith(
                        "System.Collections.Generic.HashSet`1[Pickupable]",
                        StringComparison.Ordinal))
                {
                    pickupSetRemovalCount++;
                }
                else if (declaringTypeName.StartsWith(
                             "System.Collections.Generic.Dictionary`2[Tag,",
                             StringComparison.Ordinal) ||
                         declaringTypeName.StartsWith(
                             "System.Collections.Generic.IDictionary`2[Tag,",
                             StringComparison.Ordinal))
                {
                    dictionaryKeyRemovalCount++;
                }
            }

            if (pickupSetRemovalCount < 1 || dictionaryKeyRemovalCount != 0)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack removed-fetchable handling must remove the " +
                    "pickupable from retained sets without deleting an inventory " +
                    "dictionary key.");
            }
        }

        private static void VerifyResourceTagPublicationAnchors(
            MethodInfo runUpdate,
            MethodInfo sumTotal)
        {
            IReadOnlyList<DecodedIlInstruction> instructions = Decode(runUpdate);
            int keyGetterCount = 0;
            int valueGetterCount = 0;
            int accessibleAmountSetterCount = 0;
            int completePublicationAnchorCount = 0;
            for (var instructionIndex = 0;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                DecodedIlInstruction instruction = instructions[instructionIndex];
                if (CallsMethodWithContract(
                        runUpdate,
                        instruction,
                        "System.Collections.Generic.KeyValuePair`2[Tag,",
                        "get_Key",
                        "Tag",
                        new string[0]))
                {
                    keyGetterCount++;
                }

                if (CallsMethodWithContract(
                        runUpdate,
                        instruction,
                        "System.Collections.Generic.KeyValuePair`2[Tag,",
                        "get_Value",
                        "System.Collections.Generic.HashSet`1[Pickupable]",
                        new string[0]))
                {
                    valueGetterCount++;
                }

                if (CallsMethodWithContract(
                        runUpdate,
                        instruction,
                        "System.Collections.Generic.Dictionary`2[Tag,System.Single]",
                        "set_Item",
                        "System.Void",
                        new[] { "Tag", "System.Single" }))
                {
                    accessibleAmountSetterCount++;
                }

                if (!IsCallInstruction(instruction) ||
                    !InstructionReferencesMember(
                        runUpdate,
                        instruction,
                        sumTotal))
                {
                    continue;
                }

                if (instructionIndex >= 4 &&
                    instructionIndex + 1 < instructions.Count &&
                    CallsMethodWithContract(
                        runUpdate,
                        instructions[instructionIndex - 4],
                        "System.Collections.Generic.KeyValuePair`2[Tag,",
                        "get_Key",
                        "Tag",
                        new string[0]) &&
                    CallsMethodWithContract(
                        runUpdate,
                        instructions[instructionIndex - 2],
                        "System.Collections.Generic.KeyValuePair`2[Tag,",
                        "get_Value",
                        "System.Collections.Generic.HashSet`1[Pickupable]",
                        new string[0]) &&
                    CallsMethodWithContract(
                        runUpdate,
                        instructions[instructionIndex + 1],
                        "System.Collections.Generic.Dictionary`2[Tag,System.Single]",
                        "set_Item",
                        "System.Void",
                        new[] { "Tag", "System.Single" }))
                {
                    completePublicationAnchorCount++;
                }
            }

            if (keyGetterCount != 2 ||
                valueGetterCount != 2 ||
                accessibleAmountSetterCount != 2 ||
                completePublicationAnchorCount != 2)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack BackgroundWorldInventory.RunUpdate requires " +
                    "exactly two typed resource-tag publication anchors, each " +
                    "loading one KeyValuePair key/value before SumTotal and " +
                    "writing that result to accessibleAmounts.");
            }
        }

        private static void VerifyFilteredPickupContributionAnchor(
            MethodInfo sumTotal)
        {
            IReadOnlyList<DecodedIlInstruction> instructions = Decode(sumTotal);
            int getCellIndex = -1;
            int validCellIndex = -1;
            int storedPrivateTagCheckIndex = -1;
            int totalAmountGetterIndex = -1;
            int totalAmountGetterCount = 0;
            for (var instructionIndex = 0;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                DecodedIlInstruction instruction = instructions[instructionIndex];
                if (CallsMethodWithContract(
                        sumTotal,
                        instruction,
                        "Workable",
                        "GetCell",
                        "System.Int32",
                        new string[0]))
                {
                    getCellIndex = SetUniqueAnchorIndex(
                        getCellIndex,
                        instructionIndex,
                        "Workable.GetCell");
                }

                if (CallsMethodWithContract(
                        sumTotal,
                        instruction,
                        "Grid",
                        "IsValidCell",
                        "System.Boolean",
                        new[] { "System.Int32" }))
                {
                    validCellIndex = SetUniqueAnchorIndex(
                        validCellIndex,
                        instructionIndex,
                        "Grid.IsValidCell");
                }

                if (CallsMethodWithContract(
                        sumTotal,
                        instruction,
                        "KPrefabID",
                        "HasTag",
                        "System.Boolean",
                        new[] { "Tag" }))
                {
                    storedPrivateTagCheckIndex = SetUniqueAnchorIndex(
                        storedPrivateTagCheckIndex,
                        instructionIndex,
                        "KPrefabID.HasTag");
                }

                if (CallsMethodWithContract(
                        sumTotal,
                        instruction,
                        "Pickupable",
                        "get_TotalAmount",
                        "System.Single",
                        new string[0]))
                {
                    totalAmountGetterCount++;
                    totalAmountGetterIndex = instructionIndex;
                }
            }

            int conditionalFilterBranchCount = 0;
            if (getCellIndex >= 0 && totalAmountGetterIndex > getCellIndex)
            {
                for (int instructionIndex = getCellIndex + 1;
                     instructionIndex < totalAmountGetterIndex;
                     instructionIndex++)
                {
                    if (instructions[instructionIndex].OpCode.FlowControl ==
                        FlowControl.Cond_Branch)
                    {
                        conditionalFilterBranchCount++;
                    }
                }
            }

            bool contributionShapeMatches =
                totalAmountGetterIndex >= 0 &&
                totalAmountGetterIndex + 1 < instructions.Count &&
                instructions[totalAmountGetterIndex + 1].OpCode == OpCodes.Add;
            if (getCellIndex < 0 ||
                validCellIndex <= getCellIndex ||
                storedPrivateTagCheckIndex <= validCellIndex ||
                totalAmountGetterCount != 1 ||
                totalAmountGetterIndex <= storedPrivateTagCheckIndex ||
                conditionalFilterBranchCount < 2 ||
                !contributionShapeMatches)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack BackgroundWorldInventory.SumTotal requires " +
                    "exactly one filtered Pickupable.TotalAmount contribution " +
                    "anchor after the cell, world, and StoredPrivate filters.");
            }
        }

        private static int SetUniqueAnchorIndex(
            int priorIndex,
            int candidateIndex,
            string semanticAnchor)
        {
            if (priorIndex >= 0)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack method contains more than one " +
                    semanticAnchor +
                    " semantic anchor.");
            }

            return candidateIndex;
        }

        private static bool CallsMethodWithContract(
            MethodBase bodyOwner,
            DecodedIlInstruction instruction,
            string declaringTypeNamePrefix,
            string methodName,
            string returnTypeName,
            string[] parameterTypeNames)
        {
            if (!IsCallInstruction(instruction) ||
                !instruction.MetadataToken.HasValue)
            {
                return false;
            }

            MemberInfo member = ResolveMember(
                bodyOwner,
                instruction.MetadataToken.Value);
            var method = member as MethodInfo;
            return method != null &&
                method.DeclaringType != null &&
                GetStableTypeName(method.DeclaringType).StartsWith(
                    declaringTypeNamePrefix,
                    StringComparison.Ordinal) &&
                string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    GetStableTypeName(method.ReturnType),
                    returnTypeName,
                    StringComparison.Ordinal) &&
                ParametersMatch(method.GetParameters(), parameterTypeNames);
        }

        private static IDictionary<FastTrackVerifiedMember, MemberInfo>
            VerifyPickupGroupingContract(Assembly fastTrackAssembly)
        {
            Type fastUpdateType = RequireType(
                fastTrackAssembly,
                FetchManagerFastUpdateTypeName);
            MethodInfo beforeUpdatePickups = RequireMethod(
                fastUpdateType,
                "BeforeUpdatePickups",
                isStatic: true,
                isPublic: false,
                "System.Boolean",
                new[]
                {
                    "FetchManager+FetchablesByPrefabId",
                    "Navigator",
                    "System.Int32"
                });
            Type keyType = RequireNestedType(
                fastUpdateType,
                PickupTagKeyNestedTypeName,
                isPublic: false);
            FieldInfo hashField = RequireField(
                keyType,
                "Hash",
                isStatic: false,
                isPublic: false,
                "System.Int32");
            FieldInfo allocatedIdentityField = RequireField(
                keyType,
                "ID",
                isStatic: false,
                isPublic: false,
                "KPrefabID");
            ConstructorInfo keyConstructor = RequireConstructor(
                keyType,
                isPublic: true,
                new[] { "System.Int32", "KPrefabID" });
            MethodInfo typedEquality = RequireMethod(
                keyType,
                "Equals",
                isStatic: false,
                isPublic: true,
                "System.Boolean",
                new[] { GetStableTypeName(keyType) });
            Type pickupTagDictionaryType = RequireNestedType(
                fastUpdateType,
                PickupTagDictionaryNestedTypeName,
                isPublic: false);
            MethodInfo addItem = RequireMethod(
                pickupTagDictionaryType,
                "AddItem",
                isStatic: false,
                isPublic: true,
                "System.Void",
                new[] { "FetchManager+Fetchable&", "System.Int32" });

            VerifyPickupTagKeyEqualityUsesOnlyAllocatedHash(
                typedEquality,
                hashField,
                allocatedIdentityField);
            VerifyUniquePickupTagKeyConstructorAnchor(
                addItem,
                keyConstructor);

            return new Dictionary<FastTrackVerifiedMember, MemberInfo>
            {
                {
                    FastTrackVerifiedMember
                        .PickupGroupingBeforeUpdatePickupsPrefix,
                    beforeUpdatePickups
                },
                {
                    FastTrackVerifiedMember.PickupGroupingAddItem,
                    addItem
                },
                {
                    FastTrackVerifiedMember.PickupGroupingKeyConstructor,
                    keyConstructor
                },
                {
                    FastTrackVerifiedMember.PickupGroupingKeyTypedEquality,
                    typedEquality
                }
            };
        }

        private static void VerifyPickupTagKeyEqualityUsesOnlyAllocatedHash(
            MethodInfo typedEquality,
            FieldInfo hashField,
            FieldInfo allocatedIdentityField)
        {
            IReadOnlyList<DecodedIlInstruction> instructions =
                Decode(typedEquality);
            int hashReadCount = CountMemberInstructions(
                typedEquality,
                instructions,
                hashField,
                instruction => instruction.OpCode == OpCodes.Ldfld);
            int identityReadCount = CountMemberInstructions(
                typedEquality,
                instructions,
                allocatedIdentityField,
                instruction => instruction.OpCode == OpCodes.Ldfld);
            if (hashReadCount != 2 || identityReadCount != 0)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack PickupTagKey equality must use only the allocated " +
                    "hash value; consulting ID would make collision-free extended " +
                    "keys semantically ineffective.");
            }
        }

        private static void VerifyUniquePickupTagKeyConstructorAnchor(
            MethodInfo addItem,
            ConstructorInfo keyConstructor)
        {
            IReadOnlyList<DecodedIlInstruction> instructions = Decode(addItem);
            int anchorCount = CountMemberInstructions(
                addItem,
                instructions,
                keyConstructor,
                IsCallInstruction);
            if (anchorCount != 1)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack PickupTagDict.AddItem requires exactly one " +
                    "PickupTagKey constructor anchor, but found " +
                    anchorCount +
                    ".");
            }
        }

        private static IDictionary<FastTrackVerifiedMember, MemberInfo>
            VerifyDirectDeliveryEligibilityContract(Assembly fastTrackAssembly)
        {
            Type comparatorType = RequireType(
                fastTrackAssembly,
                ChoreComparatorTypeName);
            MethodInfo comparator = RequireMethod(
                comparatorType,
                "CheckFetchChore",
                isStatic: false,
                isPublic: false,
                "System.Boolean",
                new[]
                {
                    "Chore+Precondition+Context&",
                    "FetchChore",
                    "ClearableManager+SortedClearable&"
                });
            Type replacementPatchType = RequireType(
                fastTrackAssembly,
                DirectDeliveryPatchTypeName);
            MethodInfo replacementPrefix = RequireMethod(
                replacementPatchType,
                "Prefix",
                isStatic: true,
                isPublic: false,
                "System.Boolean",
                new[]
                {
                    "ChoreConsumerState",
                    "GlobalChoreProvider",
                    "System.Collections.Generic.List`1[" +
                    "Chore+Precondition+Context]"
                });

            return new Dictionary<FastTrackVerifiedMember, MemberInfo>
            {
                {
                    FastTrackVerifiedMember
                        .DirectDeliveryEligibilityComparator,
                    comparator
                },
                {
                    FastTrackVerifiedMember
                        .DirectDeliveryEligibilityReplacementPrefix,
                    replacementPrefix
                }
            };
        }

        private static Type RequireType(Assembly assembly, string fullTypeName)
        {
            Type? type = assembly.GetType(
                fullTypeName,
                throwOnError: false,
                ignoreCase: false);
            if (type == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack contract requires exact type '" +
                    fullTypeName +
                    "'.");
            }

            return type;
        }

        private static Type RequireNestedType(
            Type declaringType,
            string nestedTypeName,
            bool isPublic)
        {
            BindingFlags bindingFlags = BindingFlags.DeclaredOnly |
                (isPublic ? BindingFlags.Public : BindingFlags.NonPublic);
            Type[] nestedTypes = declaringType.GetNestedTypes(bindingFlags);
            return HarmonyPatchContractVerifier.RequireSingleMatch(
                nestedTypes,
                candidate => string.Equals(
                    candidate.Name,
                    nestedTypeName,
                    StringComparison.Ordinal),
                GetStableTypeName(declaringType) +
                "+" +
                nestedTypeName +
                " nested type");
        }

        private static FieldInfo RequireField(
            Type declaringType,
            string fieldName,
            bool isStatic,
            bool isPublic,
            string fieldTypeName)
        {
            FieldInfo[] fields = declaringType.GetFields(
                BindingFlags.DeclaredOnly |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static);
            return HarmonyPatchContractVerifier.RequireSingleMatch(
                fields,
                candidate =>
                    string.Equals(
                        candidate.Name,
                        fieldName,
                        StringComparison.Ordinal) &&
                    candidate.IsStatic == isStatic &&
                    candidate.IsPublic == isPublic &&
                    string.Equals(
                        GetStableTypeName(candidate.FieldType),
                        fieldTypeName,
                        StringComparison.Ordinal),
                GetStableTypeName(declaringType) +
                "." +
                fieldName +
                " field");
        }

        private static MethodInfo RequireMethod(
            Type declaringType,
            string methodName,
            bool isStatic,
            bool isPublic,
            string returnTypeName,
            IReadOnlyList<string> parameterTypeNames)
        {
            MethodInfo[] methods = declaringType.GetMethods(
                BindingFlags.DeclaredOnly |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static);
            return HarmonyPatchContractVerifier.RequireSingleMatch(
                methods,
                candidate =>
                    string.Equals(
                        candidate.Name,
                        methodName,
                        StringComparison.Ordinal) &&
                    candidate.IsStatic == isStatic &&
                    candidate.IsPublic == isPublic &&
                    !candidate.IsGenericMethod &&
                    string.Equals(
                        GetStableTypeName(candidate.ReturnType),
                        returnTypeName,
                        StringComparison.Ordinal) &&
                    ParametersMatch(
                        candidate.GetParameters(),
                        parameterTypeNames),
                GetStableTypeName(declaringType) +
                "." +
                methodName +
                " method");
        }

        private static ConstructorInfo RequireConstructor(
            Type declaringType,
            bool isPublic,
            IReadOnlyList<string> parameterTypeNames)
        {
            ConstructorInfo[] constructors = declaringType.GetConstructors(
                BindingFlags.DeclaredOnly |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);
            return HarmonyPatchContractVerifier.RequireSingleMatch(
                constructors,
                candidate =>
                    candidate.IsPublic == isPublic &&
                    ParametersMatch(
                        candidate.GetParameters(),
                        parameterTypeNames),
                GetStableTypeName(declaringType) +
                " constructor");
        }

        private static bool ParametersMatch(
            IReadOnlyList<ParameterInfo> parameters,
            IReadOnlyList<string> parameterTypeNames)
        {
            if (parameters.Count != parameterTypeNames.Count)
            {
                return false;
            }

            for (var parameterIndex = 0;
                 parameterIndex < parameters.Count;
                 parameterIndex++)
            {
                if (!string.Equals(
                        GetStableTypeName(
                            parameters[parameterIndex].ParameterType),
                        parameterTypeNames[parameterIndex],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetStableTypeName(Type type)
        {
            if (type.IsByRef)
            {
                return GetStableTypeName(type.GetElementType()!) + "&";
            }

            if (type.IsArray)
            {
                return GetStableTypeName(type.GetElementType()!) +
                    (type.GetArrayRank() == 1
                        ? "[]"
                        : "[" + new string(',', type.GetArrayRank() - 1) + "]");
            }

            if (!type.IsGenericType)
            {
                return type.FullName ?? type.Name;
            }

            Type genericDefinition = type.GetGenericTypeDefinition();
            Type[] genericArguments = type.GetGenericArguments();
            var names = new string[genericArguments.Length];
            for (var argumentIndex = 0;
                 argumentIndex < genericArguments.Length;
                 argumentIndex++)
            {
                names[argumentIndex] =
                    GetStableTypeName(genericArguments[argumentIndex]);
            }

            return (genericDefinition.FullName ?? genericDefinition.Name) +
                "[" +
                string.Join(",", names) +
                "]";
        }

        private static int CountMemberInstructions(
            MethodBase bodyOwner,
            IReadOnlyList<DecodedIlInstruction> instructions,
            MemberInfo expectedMember,
            Func<DecodedIlInstruction, bool> instructionContract)
        {
            int matchCount = 0;
            for (var instructionIndex = 0;
                 instructionIndex < instructions.Count;
                 instructionIndex++)
            {
                DecodedIlInstruction instruction = instructions[instructionIndex];
                if (!instructionContract(instruction) ||
                    !instruction.MetadataToken.HasValue)
                {
                    continue;
                }

                if (InstructionReferencesMember(
                        bodyOwner,
                        instruction,
                        expectedMember))
                {
                    matchCount++;
                }
            }

            return matchCount;
        }

        private static bool InstructionReferencesMember(
            MethodBase bodyOwner,
            DecodedIlInstruction instruction,
            MemberInfo expectedMember)
        {
            if (!instruction.MetadataToken.HasValue)
            {
                return false;
            }

            MemberInfo resolvedMember = ResolveMember(
                bodyOwner,
                instruction.MetadataToken.Value);
            return MembersMatch(resolvedMember, expectedMember);
        }

        private static bool MembersMatch(
            MemberInfo candidate,
            MemberInfo expected)
        {
            if (Equals(candidate, expected))
            {
                return true;
            }

            try
            {
                return ReferenceEquals(candidate.Module, expected.Module) &&
                    candidate.MetadataToken == expected.MetadataToken;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static MemberInfo ResolveMember(
            MethodBase bodyOwner,
            int metadataToken)
        {
            try
            {
                Type[]? declaringTypeArguments =
                    bodyOwner.DeclaringType != null &&
                    bodyOwner.DeclaringType.IsGenericType
                        ? bodyOwner.DeclaringType.GetGenericArguments()
                        : null;
                Type[]? methodArguments = bodyOwner.IsGenericMethod
                    ? bodyOwner.GetGenericArguments()
                    : null;
                MemberInfo? member = bodyOwner.Module.ResolveMember(
                    metadataToken,
                    declaringTypeArguments,
                    methodArguments);
                if (member == null)
                {
                    throw new HarmonyPatchContractViolationException(
                        "FastTrack IL metadata token " +
                        metadataToken +
                        " resolved to no member.");
                }

                return member;
            }
            catch (ArgumentException exception)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack IL metadata could not be resolved.",
                    exception);
            }
            catch (BadImageFormatException exception)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack IL metadata is malformed.",
                    exception);
            }
            catch (NotSupportedException exception)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack IL metadata is not supported by the loaded " +
                    "runtime.",
                    exception);
            }
        }

        private static bool IsCallInstruction(
            DecodedIlInstruction instruction) =>
            instruction.OpCode == OpCodes.Call ||
            instruction.OpCode == OpCodes.Callvirt ||
            instruction.OpCode == OpCodes.Newobj;

        private static IReadOnlyList<DecodedIlInstruction> Decode(
            MethodBase method)
        {
            MethodBody? methodBody = method.GetMethodBody();
            byte[]? methodBytes = methodBody?.GetILAsByteArray();
            if (methodBytes == null)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack method '" +
                    method.Name +
                    "' has no inspectable IL body.");
            }

            var instructions = new List<DecodedIlInstruction>();
            var byteIndex = 0;
            while (byteIndex < methodBytes.Length)
            {
                int instructionOffset = byteIndex;
                OpCode opCode = ReadOpCode(methodBytes, ref byteIndex);
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
                        RequireRemainingBytes(methodBytes, byteIndex, 1);
                        branchTargetOffset =
                            byteIndex +
                            1 +
                            unchecked((sbyte)methodBytes[byteIndex]);
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
                        RequireRemainingBytes(methodBytes, byteIndex, 4);
                        branchTargetOffset =
                            byteIndex +
                            4 +
                            BitConverter.ToInt32(methodBytes, byteIndex);
                        operandByteCount = 4;
                        break;
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineSig:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                        RequireRemainingBytes(methodBytes, byteIndex, 4);
                        metadataToken = BitConverter.ToInt32(
                            methodBytes,
                            byteIndex);
                        operandByteCount = 4;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        operandByteCount = 8;
                        break;
                    case OperandType.InlineSwitch:
                        RequireRemainingBytes(methodBytes, byteIndex, 4);
                        int targetCount = BitConverter.ToInt32(
                            methodBytes,
                            byteIndex);
                        if (targetCount < 0)
                        {
                            throw MalformedIl(method.Name);
                        }

                        try
                        {
                            operandByteCount = checked(4 + (targetCount * 4));
                        }
                        catch (OverflowException exception)
                        {
                            throw new HarmonyPatchContractViolationException(
                                "FastTrack method '" +
                                method.Name +
                                "' has an overflowing switch table.",
                                exception);
                        }

                        break;
                    default:
                        throw new HarmonyPatchContractViolationException(
                            "FastTrack method '" +
                            method.Name +
                            "' uses an unknown IL operand type " +
                            opCode.OperandType +
                            ".");
                }

                RequireRemainingBytes(methodBytes, byteIndex, operandByteCount);
                instructions.Add(new DecodedIlInstruction(
                    instructionOffset,
                    opCode,
                    metadataToken,
                    branchTargetOffset));
                byteIndex += operandByteCount;
            }

            return instructions.AsReadOnly();
        }

        private static OpCode ReadOpCode(byte[] methodBytes, ref int byteIndex)
        {
            byte firstByte = methodBytes[byteIndex++];
            if (firstByte != 0xFE)
            {
                return RequireKnownOpCode(
                    SingleByteOpCodes[firstByte],
                    firstByte);
            }

            RequireRemainingBytes(methodBytes, byteIndex, 1);
            byte secondByte = methodBytes[byteIndex++];
            return RequireKnownOpCode(
                MultiByteOpCodes[secondByte],
                0xFE00 | secondByte);
        }

        private static OpCode RequireKnownOpCode(OpCode opCode, int encodedValue)
        {
            if (opCode.Size == 0)
            {
                throw new HarmonyPatchContractViolationException(
                    "FastTrack IL contains unknown opcode 0x" +
                    encodedValue.ToString("X4") +
                    ".");
            }

            return opCode;
        }

        private static void RequireRemainingBytes(
            byte[] methodBytes,
            int byteIndex,
            int requiredByteCount)
        {
            if (requiredByteCount < 0 ||
                byteIndex > methodBytes.Length - requiredByteCount)
            {
                throw MalformedIl("unknown");
            }
        }

        private static HarmonyPatchContractViolationException MalformedIl(
            string methodName) =>
            new HarmonyPatchContractViolationException(
                "FastTrack method '" +
                methodName +
                "' has a truncated or malformed IL body.");

        private static OpCode[] BuildOpCodeLookup(bool multiByte)
        {
            var lookup = new OpCode[256];
            FieldInfo[] fields = typeof(OpCodes).GetFields(
                BindingFlags.Public | BindingFlags.Static);
            for (var fieldIndex = 0;
                 fieldIndex < fields.Length;
                 fieldIndex++)
            {
                object? value = fields[fieldIndex].GetValue(null);
                if (!(value is OpCode opCode) || opCode.Size == 0)
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

        private readonly struct DecodedIlInstruction
        {
            internal DecodedIlInstruction(
                int offset,
                OpCode opCode,
                int? metadataToken,
                int? branchTargetOffset)
            {
                Offset = offset;
                OpCode = opCode;
                MetadataToken = metadataToken;
                BranchTargetOffset = branchTargetOffset;
            }

            internal int Offset { get; }

            internal OpCode OpCode { get; }

            internal int? MetadataToken { get; }

            internal int? BranchTargetOffset { get; }
        }
    }
}
