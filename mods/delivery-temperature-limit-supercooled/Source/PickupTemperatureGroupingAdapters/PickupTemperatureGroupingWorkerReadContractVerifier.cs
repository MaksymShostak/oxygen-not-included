#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Proves the exact managed-only game reads shared by every pickup-temperature
    /// grouping adapter before any worker-capable Harmony patch is installed.
    /// </summary>
    /// <remarks>
    /// Keeping this proof independent of the Klei and FastTrack adapters prevents
    /// either implementation from depending on the other's patch surface. A
    /// contract change fails coordinated activation; adapters must never replace a
    /// rejected field read with component discovery on a worker thread.
    /// </remarks>
    internal static class PickupTemperatureGroupingWorkerReadContractVerifier
    {
        internal static void VerifySharedManagedReadContracts()
        {
            FieldInfo navigatorAnchorCellField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(Navigator),
                    "AnchorCell",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(int));
            MethodInfo navigatorAnchorCellGetter =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(Navigator),
                    "GetAnchorCell",
                    DeclaredMemberVisibility.Public,
                    typeof(int),
                    Array.Empty<Type>());
            RequireDirectManagedInstanceFieldGetter(
                navigatorAnchorCellGetter,
                navigatorAnchorCellField,
                "Navigator.GetAnchorCell");

            _ = HarmonyPatchContractVerifier.RequireField(
                typeof(Grid),
                "WorldIdx",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Static,
                typeof(byte[]));
            _ = HarmonyPatchContractVerifier.RequireField(
                typeof(Pickupable),
                "KPrefabID",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(KPrefabID));
            _ = HarmonyPatchContractVerifier.RequireField(
                typeof(KPrefabID),
                "InstanceID",
                DeclaredMemberVisibility.Public,
                FieldStorageKind.Instance,
                typeof(int));
            FieldInfo prefabTagField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(KPrefabID),
                    "PrefabTag",
                    DeclaredMemberVisibility.Public,
                    FieldStorageKind.Instance,
                    typeof(Tag));
            FieldInfo additionalTagsField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(KPrefabID),
                    "tags",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(HashSet<Tag>));
            MethodInfo hasTagMethod =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(KPrefabID),
                    "HasTag",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(Tag) });
            RequireManagedKPrefabIdHasTagBody(
                hasTagMethod,
                prefabTagField,
                additionalTagsField);

            FieldInfo primaryElementField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(Pickupable),
                    "primaryElement",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(PrimaryElement));
            MethodInfo primaryElementGetter =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(Pickupable),
                    "get_PrimaryElement",
                    DeclaredMemberVisibility.Public,
                    typeof(PrimaryElement),
                    Array.Empty<Type>());
            RequireDirectManagedInstanceFieldGetter(
                primaryElementGetter,
                primaryElementField,
                "Pickupable.PrimaryElement");

            FieldInfo internalTemperatureField =
                HarmonyPatchContractVerifier.RequireField(
                    typeof(PrimaryElement),
                    "_Temperature",
                    DeclaredMemberVisibility.NonPublic,
                    FieldStorageKind.Instance,
                    typeof(float));
            MethodInfo internalTemperatureGetter =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(PrimaryElement),
                    "get_InternalTemperature",
                    DeclaredMemberVisibility.Public,
                    typeof(float),
                    Array.Empty<Type>());
            RequireDirectManagedInstanceFieldGetter(
                internalTemperatureGetter,
                internalTemperatureField,
                "PrimaryElement.InternalTemperature");
        }

        private static void RequireDirectManagedInstanceFieldGetter(
            MethodInfo getter,
            FieldInfo expectedField,
            string contractName)
        {
            byte[]? body = getter.GetMethodBody()?.GetILAsByteArray();
            if (body == null ||
                body.Length != 7 ||
                body[0] != 0x02 ||
                body[1] != 0x7B ||
                body[6] != 0x2A)
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " is no longer an exact managed instance-field getter.");
            }

            FieldInfo? resolvedField;
            try
            {
                resolvedField = getter.Module.ResolveField(
                    BitConverter.ToInt32(body, 2));
            }
            catch (Exception exception)
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " contains an unresolvable field-token operand.",
                    exception);
            }

            if (!Equals(resolvedField, expectedField))
            {
                throw new HarmonyPatchContractViolationException(
                    contractName +
                    " reads a field other than the reviewed managed field.");
            }
        }

        private static void RequireManagedKPrefabIdHasTagBody(
            MethodInfo hasTagMethod,
            FieldInfo prefabTagField,
            FieldInfo additionalTagsField)
        {
            MethodInfo tagEqualityMethod =
                HarmonyPatchContractVerifier.RequireStaticMethod(
                    typeof(Tag),
                    "op_Equality",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(Tag), typeof(Tag) });
            MethodInfo additionalTagContainsMethod =
                HarmonyPatchContractVerifier.RequireInstanceMethod(
                    typeof(HashSet<Tag>),
                    "Contains",
                    DeclaredMemberVisibility.Public,
                    typeof(bool),
                    new[] { typeof(Tag) });
            byte[]? body = hasTagMethod.GetMethodBody()?.GetILAsByteArray();
            if (body == null ||
                body.Length != 29 ||
                body[0] != 0x02 ||
                body[1] != 0x7B ||
                body[6] != 0x03 ||
                body[7] != 0x28 ||
                body[12] != 0x2D ||
                unchecked((sbyte)body[13]) != 13 ||
                body[14] != 0x02 ||
                body[15] != 0x7B ||
                body[20] != 0x03 ||
                body[21] != 0x6F ||
                body[26] != 0x2A ||
                body[27] != 0x17 ||
                body[28] != 0x2A)
            {
                throw new HarmonyPatchContractViolationException(
                    "KPrefabID.HasTag is no longer the reviewed managed-only " +
                    "prefab/additional-tag membership body.");
            }

            try
            {
                FieldInfo? resolvedPrefabTagField =
                    hasTagMethod.Module.ResolveField(
                        BitConverter.ToInt32(body, 2));
                MethodBase? resolvedTagEqualityMethod =
                    hasTagMethod.Module.ResolveMethod(
                        BitConverter.ToInt32(body, 8));
                FieldInfo? resolvedAdditionalTagsField =
                    hasTagMethod.Module.ResolveField(
                        BitConverter.ToInt32(body, 16));
                MethodBase? resolvedContainsMethod =
                    hasTagMethod.Module.ResolveMethod(
                        BitConverter.ToInt32(body, 22));
                if (!Equals(resolvedPrefabTagField, prefabTagField) ||
                    !Equals(resolvedTagEqualityMethod, tagEqualityMethod) ||
                    !Equals(
                        resolvedAdditionalTagsField,
                        additionalTagsField) ||
                    !Equals(
                        resolvedContainsMethod,
                        additionalTagContainsMethod))
                {
                    throw new HarmonyPatchContractViolationException(
                        "KPrefabID.HasTag no longer reads only the reviewed tag " +
                        "fields and managed membership methods.");
                }
            }
            catch (HarmonyPatchContractViolationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new HarmonyPatchContractViolationException(
                    "KPrefabID.HasTag contains an unresolvable managed-read " +
                    "operand.",
                    exception);
            }
        }
    }
}
