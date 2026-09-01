#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Identifies which declared visibility boundary a runtime contract requires.
    /// </summary>
    internal enum DeclaredMemberVisibility
    {
        Public,
        NonPublic
    }

    /// <summary>
    /// Identifies whether a reflected field belongs to an instance or its type.
    /// </summary>
    internal enum FieldStorageKind
    {
        Instance,
        Static
    }

    /// <summary>
    /// Resolves reflection and semantic transpiler anchors by exact contract.
    /// Every operation rejects both absence and ambiguity so an upstream game or
    /// mod change cannot silently redirect a patch to a merely similar member.
    /// </summary>
    internal static class HarmonyPatchContractVerifier
    {
        internal static MethodInfo RequireInstanceMethod(
            Type declaringType,
            string methodName,
            DeclaredMemberVisibility visibility,
            Type returnType,
            IReadOnlyList<Type> orderedParameterTypes) =>
            RequireMethod(
                declaringType,
                methodName,
                visibility,
                BindingFlags.Instance,
                "instance",
                returnType,
                orderedParameterTypes);

        internal static MethodInfo RequireStaticMethod(
            Type declaringType,
            string methodName,
            DeclaredMemberVisibility visibility,
            Type returnType,
            IReadOnlyList<Type> orderedParameterTypes) =>
            RequireMethod(
                declaringType,
                methodName,
                visibility,
                BindingFlags.Static,
                "static",
                returnType,
                orderedParameterTypes);

        internal static ConstructorInfo RequireConstructor(
            Type declaringType,
            DeclaredMemberVisibility visibility,
            IReadOnlyList<Type> orderedParameterTypes)
        {
            ValidateDeclaringType(declaringType);
            ValidateOrderedParameterTypes(orderedParameterTypes);
            BindingFlags bindingFlags =
                BindingFlags.DeclaredOnly |
                BindingFlags.Instance |
                GetVisibilityBindingFlag(visibility);
            ConstructorInfo[] constructors =
                declaringType.GetConstructors(bindingFlags);
            string contractName =
                GetTypeDisplayName(declaringType) +
                " declared constructor";

            return RequireSingleMatch(
                constructors,
                candidate => ParametersMatchExactly(
                    candidate.GetParameters(),
                    orderedParameterTypes),
                contractName);
        }

        internal static FieldInfo RequireField(
            Type declaringType,
            string fieldName,
            DeclaredMemberVisibility visibility,
            FieldStorageKind storageKind,
            Type fieldType)
        {
            ValidateDeclaringType(declaringType);
            ValidateMemberName(fieldName, nameof(fieldName));
            if (fieldType == null)
            {
                throw new ArgumentNullException(nameof(fieldType));
            }

            BindingFlags storageBindingFlag =
                GetStorageBindingFlag(storageKind);
            BindingFlags bindingFlags =
                BindingFlags.DeclaredOnly |
                storageBindingFlag |
                GetVisibilityBindingFlag(visibility);
            FieldInfo[] fields = declaringType.GetFields(bindingFlags);
            string contractName =
                GetTypeDisplayName(declaringType) +
                "." +
                fieldName +
                " declared " +
                GetStorageDisplayName(storageKind) +
                " field";

            return RequireSingleMatch(
                fields,
                candidate =>
                    string.Equals(
                        candidate.Name,
                        fieldName,
                        StringComparison.Ordinal) &&
                    candidate.FieldType == fieldType,
                contractName);
        }

        internal static Type RequireNestedType(
            Type declaringType,
            string nestedTypeName,
            DeclaredMemberVisibility visibility)
        {
            ValidateDeclaringType(declaringType);
            ValidateMemberName(nestedTypeName, nameof(nestedTypeName));
            BindingFlags bindingFlags =
                BindingFlags.DeclaredOnly |
                GetVisibilityBindingFlag(visibility);
            Type[] nestedTypes = declaringType.GetNestedTypes(bindingFlags);
            string contractName =
                GetTypeDisplayName(declaringType) +
                "." +
                nestedTypeName +
                " declared nested type";

            return RequireSingleMatch(
                nestedTypes,
                candidate => string.Equals(
                    candidate.Name,
                    nestedTypeName,
                    StringComparison.Ordinal),
                contractName);
        }

        internal static T RequireSingleMatch<T>(
            IReadOnlyList<T> candidates,
            Func<T, bool> semanticMatch,
            string semanticAnchorName)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (semanticMatch == null)
            {
                throw new ArgumentNullException(nameof(semanticMatch));
            }

            ValidateMemberName(
                semanticAnchorName,
                nameof(semanticAnchorName));

            int matchingCandidateCount = 0;
            int matchingCandidateIndex = -1;
            for (var candidateIndex = 0;
                 candidateIndex < candidates.Count;
                 candidateIndex++)
            {
                bool isMatch;
                try
                {
                    // Evaluate each predicate exactly once. Continuing after a
                    // second match records the complete ambiguity count for a
                    // useful installation-time diagnostic.
                    isMatch = semanticMatch(candidates[candidateIndex]);
                }
                catch (Exception exception)
                {
                    throw new HarmonyPatchContractViolationException(
                        "Harmony patch contract '" +
                        semanticAnchorName +
                        "' could not evaluate candidate index " +
                        candidateIndex +
                        ".",
                        exception);
                }

                if (isMatch)
                {
                    matchingCandidateCount++;
                    matchingCandidateIndex = candidateIndex;
                }
            }

            if (matchingCandidateCount != 1)
            {
                throw new HarmonyPatchContractViolationException(
                    "Harmony patch contract '" +
                    semanticAnchorName +
                    "' requires exactly one match, but found " +
                    matchingCandidateCount +
                    ".");
            }

            return candidates[matchingCandidateIndex];
        }

        internal static bool VerifyKleiAuthority(
            MethodBase targetMethod,
            IReadOnlyList<ActiveHarmonyPrefixDescriptor> activePatches,
            IReadOnlyCollection<string> permittedSkippingPrefixOwners)
        {
            if (targetMethod == null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            if (activePatches == null)
            {
                throw new ArgumentNullException(nameof(activePatches));
            }

            if (permittedSkippingPrefixOwners == null)
            {
                throw new ArgumentNullException(
                    nameof(permittedSkippingPrefixOwners));
            }

            for (var patchIndex = 0;
                 patchIndex < activePatches.Count;
                 patchIndex++)
            {
                ActiveHarmonyPrefixDescriptor activePatch =
                    activePatches[patchIndex];
                if (activePatch == null)
                {
                    throw new ArgumentException(
                        "An active Harmony patch descriptor cannot be null.",
                        nameof(activePatches));
                }

                if (!Equals(activePatch.TargetMethod, targetMethod) ||
                    activePatch.PrefixMethod.ReturnType != typeof(bool))
                {
                    // The installer supplies active prefixes only. A prefix can
                    // suppress the original Klei body only when it targets this
                    // exact method and returns bool; priority alone proves no
                    // authority and is deliberately ignored here.
                    continue;
                }

                if (!ContainsExactOwner(
                        permittedSkippingPrefixOwners,
                        activePatch.HarmonyOwner))
                {
                    return false;
                }
            }

            return true;
        }

        private static MethodInfo RequireMethod(
            Type declaringType,
            string methodName,
            DeclaredMemberVisibility visibility,
            BindingFlags storageBindingFlag,
            string storageDisplayName,
            Type returnType,
            IReadOnlyList<Type> orderedParameterTypes)
        {
            ValidateDeclaringType(declaringType);
            ValidateMemberName(methodName, nameof(methodName));
            if (returnType == null)
            {
                throw new ArgumentNullException(nameof(returnType));
            }

            ValidateOrderedParameterTypes(orderedParameterTypes);
            BindingFlags bindingFlags =
                BindingFlags.DeclaredOnly |
                storageBindingFlag |
                GetVisibilityBindingFlag(visibility);
            MethodInfo[] methods = declaringType.GetMethods(bindingFlags);
            string contractName =
                GetTypeDisplayName(declaringType) +
                "." +
                methodName +
                " declared " +
                storageDisplayName +
                " method";

            return RequireSingleMatch(
                methods,
                candidate =>
                    string.Equals(
                        candidate.Name,
                        methodName,
                        StringComparison.Ordinal) &&
                    !candidate.IsGenericMethod &&
                    candidate.ReturnType == returnType &&
                    ParametersMatchExactly(
                        candidate.GetParameters(),
                        orderedParameterTypes),
                contractName);
        }

        private static bool ParametersMatchExactly(
            IReadOnlyList<ParameterInfo> actualParameters,
            IReadOnlyList<Type> expectedParameterTypes)
        {
            if (actualParameters.Count != expectedParameterTypes.Count)
            {
                return false;
            }

            for (var parameterIndex = 0;
                 parameterIndex < actualParameters.Count;
                 parameterIndex++)
            {
                if (actualParameters[parameterIndex].ParameterType !=
                    expectedParameterTypes[parameterIndex])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsExactOwner(
            IReadOnlyCollection<string> permittedOwners,
            string candidateOwner)
        {
            foreach (string permittedOwner in permittedOwners)
            {
                if (string.Equals(
                    permittedOwner,
                    candidateOwner,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static BindingFlags GetVisibilityBindingFlag(
            DeclaredMemberVisibility visibility)
        {
            switch (visibility)
            {
                case DeclaredMemberVisibility.Public:
                    return BindingFlags.Public;
                case DeclaredMemberVisibility.NonPublic:
                    return BindingFlags.NonPublic;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(visibility),
                        visibility,
                        "Unknown declared-member visibility.");
            }
        }

        private static BindingFlags GetStorageBindingFlag(
            FieldStorageKind storageKind)
        {
            switch (storageKind)
            {
                case FieldStorageKind.Instance:
                    return BindingFlags.Instance;
                case FieldStorageKind.Static:
                    return BindingFlags.Static;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(storageKind),
                        storageKind,
                        "Unknown field storage kind.");
            }
        }

        private static string GetStorageDisplayName(
            FieldStorageKind storageKind)
        {
            switch (storageKind)
            {
                case FieldStorageKind.Instance:
                    return "instance";
                case FieldStorageKind.Static:
                    return "static";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(storageKind),
                        storageKind,
                        "Unknown field storage kind.");
            }
        }

        private static void ValidateDeclaringType(Type declaringType)
        {
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
        }

        private static void ValidateOrderedParameterTypes(
            IReadOnlyList<Type> orderedParameterTypes)
        {
            if (orderedParameterTypes == null)
            {
                throw new ArgumentNullException(nameof(orderedParameterTypes));
            }

            for (var parameterIndex = 0;
                 parameterIndex < orderedParameterTypes.Count;
                 parameterIndex++)
            {
                if (orderedParameterTypes[parameterIndex] == null)
                {
                    throw new ArgumentException(
                        "An expected parameter type cannot be null at index " +
                        parameterIndex +
                        ".",
                        nameof(orderedParameterTypes));
                }
            }
        }

        private static void ValidateMemberName(
            string memberName,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                throw new ArgumentException(
                    "A reflection contract name cannot be blank.",
                    parameterName);
            }
        }

        private static string GetTypeDisplayName(Type type) =>
            type.FullName ?? type.Name;
    }
}
