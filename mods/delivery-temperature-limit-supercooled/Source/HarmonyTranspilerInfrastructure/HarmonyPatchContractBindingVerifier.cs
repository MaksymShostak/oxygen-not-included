#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Verifies Harmony argument bindings before the installer mutates any
    /// target method.
    /// </summary>
    /// <remarks>
    /// The accepted binding surface is explicit and fail-closed. A new Harmony
    /// injection form must acquire verifier coverage before this mod can use it.
    /// </remarks>
    internal static class HarmonyPatchContractBindingVerifier
    {
        private const string HarmonyArgumentAttributeFullName =
            "HarmonyLib.HarmonyArgument";

        private static readonly HashSet<string> SpecialParameterNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "__instance",
                "__originalMethod",
                "__args",
                "__result",
                "__resultRef",
                "__state",
                "__exception",
                "__runOriginal"
            };

        internal static VerifiedBindings VerifyAll(
            IReadOnlyList<HarmonyPatchContractBinding> bindings) =>
            VerifiedBindings.Create(bindings);

        private static void VerifyCore(
            IReadOnlyList<HarmonyPatchContractBinding> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            for (var bindingIndex = 0;
                 bindingIndex < bindings.Count;
                 bindingIndex++)
            {
                HarmonyPatchContractBinding binding =
                    bindings[bindingIndex] ??
                    throw new ArgumentException(
                        "A Harmony patch binding cannot be null at index " +
                        bindingIndex + ".",
                        nameof(bindings));
                VerifyPatchMethodShape(binding);
                VerifyTargetArgumentBindings(binding);
            }

            VerifySharedStateBindings(bindings);
        }

        private static void VerifyPatchMethodShape(
            HarmonyPatchContractBinding binding)
        {
            MethodInfo patchMethod = binding.PatchMethod;
            if (!patchMethod.IsStatic)
            {
                throw BindingViolation(
                    binding,
                    "patch method must be static");
            }

            switch (binding.PatchKind)
            {
                case HarmonyPatchContractKind.Prefix:
                    if (patchMethod.ReturnType != typeof(void) &&
                        patchMethod.ReturnType != typeof(bool))
                    {
                        throw BindingViolation(
                            binding,
                            "Prefix return type must be System.Void or " +
                            "System.Boolean, but was " +
                            GetTypeDisplayName(patchMethod.ReturnType));
                    }

                    break;
                case HarmonyPatchContractKind.Postfix:
                    VerifyPostfixReturn(binding);
                    break;
                case HarmonyPatchContractKind.Transpiler:
                    VerifyTranspilerShape(binding);
                    break;
                case HarmonyPatchContractKind.Finalizer:
                    if (patchMethod.ReturnType != typeof(void) &&
                        !typeof(Exception).IsAssignableFrom(
                            patchMethod.ReturnType))
                    {
                        throw BindingViolation(
                            binding,
                            "Finalizer return type must be System.Void or an " +
                            "Exception type, but was " +
                            GetTypeDisplayName(patchMethod.ReturnType));
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(binding.PatchKind),
                        binding.PatchKind,
                        "Unknown Harmony patch contract kind.");
            }
        }

        private static void VerifyPostfixReturn(
            HarmonyPatchContractBinding binding)
        {
            MethodInfo patchMethod = binding.PatchMethod;
            if (patchMethod.ReturnType == typeof(void))
            {
                return;
            }

            ParameterInfo[] patchParameters = patchMethod.GetParameters();
            Type targetReturnType = GetTargetReturnType(binding.TargetMethod);
            if (patchParameters.Length == 0 ||
                patchParameters[0].ParameterType != patchMethod.ReturnType ||
                patchMethod.ReturnType != targetReturnType)
            {
                throw BindingViolation(
                    binding,
                    "Postfix return type " +
                    GetTypeDisplayName(patchMethod.ReturnType) +
                    " is not a valid pass-through return for target type " +
                    GetTypeDisplayName(targetReturnType));
            }
        }

        private static void VerifyTranspilerShape(
            HarmonyPatchContractBinding binding)
        {
            MethodInfo patchMethod = binding.PatchMethod;
            if (!IsCodeInstructionSequence(patchMethod.ReturnType))
            {
                throw BindingViolation(
                    binding,
                    "Transpiler return type must be " +
                    "IEnumerable<HarmonyLib.CodeInstruction>, but was " +
                    GetTypeDisplayName(patchMethod.ReturnType));
            }

            ParameterInfo[] patchParameters = patchMethod.GetParameters();
            var instructionSequenceCount = 0;
            for (var parameterIndex = 0;
                 parameterIndex < patchParameters.Length;
                 parameterIndex++)
            {
                ParameterInfo patchParameter = patchParameters[parameterIndex];
                Type parameterType = patchParameter.ParameterType;
                if (IsCodeInstructionSequence(parameterType))
                {
                    instructionSequenceCount++;
                    continue;
                }

                if (parameterType == typeof(ILGenerator) ||
                    parameterType == typeof(MethodBase))
                {
                    continue;
                }

                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has unsupported transpiler parameter type " +
                    GetTypeDisplayName(parameterType));
            }

            if (instructionSequenceCount != 1)
            {
                throw BindingViolation(
                    binding,
                    "Transpiler must declare exactly one " +
                    "IEnumerable<HarmonyLib.CodeInstruction> parameter, but " +
                    "declared " +
                    instructionSequenceCount);
            }
        }

        private static void VerifyTargetArgumentBindings(
            HarmonyPatchContractBinding binding)
        {
            if (binding.PatchKind == HarmonyPatchContractKind.Transpiler)
            {
                return;
            }

            ParameterInfo[] targetParameters =
                binding.TargetMethod.GetParameters();
            ParameterInfo[] patchParameters =
                binding.PatchMethod.GetParameters();
            int firstInjectedParameterIndex =
                binding.PatchKind == HarmonyPatchContractKind.Postfix &&
                binding.PatchMethod.ReturnType != typeof(void)
                    ? 1
                    : 0;
            for (var patchParameterIndex = firstInjectedParameterIndex;
                 patchParameterIndex < patchParameters.Length;
                 patchParameterIndex++)
            {
                ParameterInfo patchParameter =
                    patchParameters[patchParameterIndex];
                string? patchParameterName = patchParameter.Name;
                if (string.IsNullOrEmpty(patchParameterName))
                {
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "has no metadata name");
                }

                string effectiveParameterName = GetEffectiveParameterName(
                    patchParameter);
                if (SpecialParameterNames.Contains(effectiveParameterName))
                {
                    VerifySpecialInjection(
                        binding,
                        patchParameter,
                        effectiveParameterName);
                    continue;
                }

                if (effectiveParameterName.StartsWith(
                        "___",
                        StringComparison.Ordinal))
                {
                    VerifyFieldInjection(
                        binding,
                        patchParameter,
                        effectiveParameterName.Substring(3));
                    continue;
                }

                int targetParameterIndex = ResolveTargetParameterIndex(
                    binding,
                    patchParameter,
                    targetParameters);
                if (targetParameterIndex < 0)
                {
                    continue;
                }

                VerifyTargetArgumentType(
                    binding,
                    patchParameter,
                    targetParameters[targetParameterIndex]);
            }
        }

        private static int ResolveTargetParameterIndex(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            IReadOnlyList<ParameterInfo> targetParameters)
        {
            CustomAttributeData? argumentAttribute =
                GetHarmonyArgumentAttribute(patchParameter);
            if (argumentAttribute != null)
            {
                CustomAttributeTypedArgument mapping =
                    argumentAttribute.ConstructorArguments[0];
                if (mapping.ArgumentType == typeof(int))
                {
                    return RequireTargetParameterIndex(
                        binding,
                        patchParameter,
                        targetParameters,
                        (int)mapping.Value!);
                }

                return RequireTargetParameterName(
                    binding,
                    patchParameter,
                    targetParameters,
                    (string?)mapping.Value);
            }

            string patchParameterName = GetEffectiveParameterName(
                patchParameter);
            if (patchParameterName.StartsWith("__", StringComparison.Ordinal))
            {
                string indexText = patchParameterName.Substring(2);
                if (!int.TryParse(indexText, out int targetParameterIndex))
                {
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "does not contain a valid positional index");
                }

                return RequireTargetParameterIndex(
                    binding,
                    patchParameter,
                    targetParameters,
                    targetParameterIndex);
            }

            return RequireTargetParameterName(
                binding,
                patchParameter,
                targetParameters,
                patchParameterName);
        }

        private static void VerifySpecialInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            string injectionName)
        {
            switch (injectionName)
            {
                case "__instance":
                    VerifyInstanceInjection(binding, patchParameter);
                    return;
                case "__originalMethod":
                    VerifyExactNonByRefInjection(
                        binding,
                        patchParameter,
                        typeof(MethodBase),
                        allowAssignableReceiver: true);
                    return;
                case "__args":
                    VerifyExactNonByRefInjection(
                        binding,
                        patchParameter,
                        typeof(object[]),
                        allowAssignableReceiver: false);
                    return;
                case "__result":
                    VerifyResultInjection(binding, patchParameter);
                    return;
                case "__resultRef":
                    VerifyResultReferenceInjection(binding, patchParameter);
                    return;
                case "__state":
                    return;
                case "__exception":
                    VerifyExceptionInjection(binding, patchParameter);
                    return;
                case "__runOriginal":
                    VerifyExactNonByRefInjection(
                        binding,
                        patchParameter,
                        typeof(bool),
                        allowAssignableReceiver: false);
                    return;
                default:
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "uses an unknown special injection '" +
                        injectionName +
                        "'");
            }
        }

        private static void VerifyInstanceInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter)
        {
            if (binding.TargetMethod.IsStatic)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "cannot inject __instance for a static target");
            }

            Type declaringType = binding.TargetMethod.DeclaringType ??
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "cannot inject __instance because the target has no " +
                    "declaring type");
            Type patchType = GetElementType(patchParameter.ParameterType);
            if (patchParameter.ParameterType.IsByRef)
            {
                if (patchType != declaringType)
                {
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "has by-reference instance type " +
                        GetTypeDisplayName(patchType) +
                        ", but the target instance type is " +
                        GetTypeDisplayName(declaringType));
                }

                return;
            }

            if (declaringType.IsValueType &&
                patchType != declaringType &&
                patchType != typeof(object))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has instance type " +
                    GetTypeDisplayName(patchType) +
                    ", but Harmony can box value-type instance " +
                    GetTypeDisplayName(declaringType) +
                    " only as System.Object");
            }

            if (!patchType.IsAssignableFrom(declaringType))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has instance type " +
                    GetTypeDisplayName(patchType) +
                    ", which cannot receive target instance type " +
                    GetTypeDisplayName(declaringType));
            }
        }

        private static void VerifyResultInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter)
        {
            Type returnType = GetTargetReturnType(binding.TargetMethod);
            if (returnType == typeof(void))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "cannot inject __result for a void target");
            }

            if (returnType.IsByRef)
            {
                if (patchParameter.ParameterType != returnType)
                {
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "must use the exact by-reference return type " +
                        GetTypeDisplayName(returnType));
                }

                return;
            }

            Type returnElementType = GetElementType(returnType);
            Type patchElementType = GetElementType(
                patchParameter.ParameterType);
            if (patchParameter.ParameterType.IsByRef)
            {
                if (patchElementType != returnElementType &&
                    !(returnElementType.IsValueType &&
                      patchElementType == typeof(object)))
                {
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "has by-reference result type " +
                        GetTypeDisplayName(patchElementType) +
                        ", but the target returns " +
                        GetTypeDisplayName(returnElementType));
                }

                return;
            }

            if (returnElementType.IsValueType &&
                patchElementType != returnElementType &&
                patchElementType != typeof(object))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has result type " +
                    GetTypeDisplayName(patchElementType) +
                    ", but Harmony can box value-type result " +
                    GetTypeDisplayName(returnElementType) +
                    " only as System.Object");
            }

            if (!patchElementType.IsAssignableFrom(returnElementType))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has result type " +
                    GetTypeDisplayName(patchElementType) +
                    ", which cannot receive target return type " +
                    GetTypeDisplayName(returnElementType));
            }
        }

        private static void VerifyResultReferenceInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter)
        {
            Type returnType = GetTargetReturnType(binding.TargetMethod);
            if (!returnType.IsByRef)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "__resultRef requires a by-reference return target");
            }

            Type patchType = patchParameter.ParameterType;
            Type patchElementType = GetElementType(patchType);
            Type returnElementType = GetElementType(returnType);
            if (!patchType.IsByRef ||
                !patchElementType.IsGenericType ||
                !string.Equals(
                    patchElementType.GetGenericTypeDefinition().FullName,
                    "HarmonyLib.RefResult`1",
                    StringComparison.Ordinal) ||
                patchElementType.GetGenericArguments()[0] != returnElementType)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "__resultRef must be a by-reference " +
                    "HarmonyLib.RefResult<" +
                    GetTypeDisplayName(returnElementType) +
                    ">");
            }
        }

        private static void VerifyExceptionInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter)
        {
            if (binding.PatchKind != HarmonyPatchContractKind.Finalizer)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "__exception is permitted only on a Finalizer contract");
            }

            Type patchType = patchParameter.ParameterType;
            if (patchType.IsByRef ||
                !patchType.IsAssignableFrom(typeof(Exception)))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "__exception must receive System.Exception without " +
                    "by-reference indirection");
            }
        }

        private static void VerifyExactNonByRefInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            Type injectedType,
            bool allowAssignableReceiver)
        {
            Type patchType = patchParameter.ParameterType;
            bool typeMatches = allowAssignableReceiver
                ? patchType.IsAssignableFrom(injectedType)
                : patchType == injectedType;
            if (patchType.IsByRef || !typeMatches)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "must have non-by-reference type " +
                    GetTypeDisplayName(injectedType));
            }
        }

        private static void VerifyFieldInjection(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            string fieldName)
        {
            Type declaringType = binding.TargetMethod.DeclaringType ??
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "cannot inject a field because the target has no " +
                    "declaring type");
            FieldInfo? field = ResolveField(declaringType, fieldName);
            if (field == null)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "does not resolve field '" +
                    fieldName +
                    "' on " +
                    GetTypeDisplayName(declaringType));
            }

            if (!field.IsStatic && binding.TargetMethod.IsStatic)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "cannot inject instance field '" +
                    fieldName +
                    "' for a static target");
            }

            Type patchType = patchParameter.ParameterType;
            Type patchElementType = GetElementType(patchType);
            bool valueTypeWouldRequireBoxing =
                field.FieldType.IsValueType &&
                patchElementType != field.FieldType;
            if ((patchType.IsByRef && patchElementType != field.FieldType) ||
                (!patchType.IsByRef &&
                 (valueTypeWouldRequireBoxing ||
                  !patchElementType.IsAssignableFrom(field.FieldType))))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has field injection type " +
                    GetTypeDisplayName(patchElementType) +
                    ", which cannot receive field '" +
                    fieldName +
                    "' of type " +
                    GetTypeDisplayName(field.FieldType));
            }
        }

        private static FieldInfo? ResolveField(
            Type declaringType,
            string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            if (int.TryParse(fieldName, out int declaredFieldIndex))
            {
                FieldInfo[] declaredFields = declaringType.GetFields(
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                return declaredFieldIndex >= 0 &&
                       declaredFieldIndex < declaredFields.Length
                    ? declaredFields[declaredFieldIndex]
                    : null;
            }

            for (Type? inspectedType = declaringType;
                 inspectedType != null;
                 inspectedType = inspectedType.BaseType)
            {
                FieldInfo? field = inspectedType.GetField(
                    fieldName,
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static void VerifySharedStateBindings(
            IReadOnlyList<HarmonyPatchContractBinding> bindings)
        {
            var stateBindings = new List<StateBinding>();
            for (var bindingIndex = 0;
                 bindingIndex < bindings.Count;
                 bindingIndex++)
            {
                HarmonyPatchContractBinding binding = bindings[bindingIndex];
                ParameterInfo[] patchParameters =
                    binding.PatchMethod.GetParameters();
                for (var parameterIndex = 0;
                     parameterIndex < patchParameters.Length;
                     parameterIndex++)
                {
                    ParameterInfo patchParameter =
                        patchParameters[parameterIndex];
                    if (!string.Equals(
                            GetEffectiveParameterName(patchParameter),
                            "__state",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Type stateType = GetElementType(
                        patchParameter.ParameterType);
                    for (var stateIndex = 0;
                         stateIndex < stateBindings.Count;
                         stateIndex++)
                    {
                        StateBinding previousState =
                            stateBindings[stateIndex];
                        if (!Equals(
                                previousState.Binding.TargetMethod,
                                binding.TargetMethod) ||
                            previousState.Binding.PatchMethod.DeclaringType !=
                            binding.PatchMethod.DeclaringType)
                        {
                            continue;
                        }

                        if (previousState.StateType != stateType)
                        {
                            throw BindingViolation(
                                binding,
                                patchParameter,
                                "uses __state type " +
                                GetTypeDisplayName(stateType) +
                                ", but patch " +
                                GetMethodDisplayName(
                                    previousState.Binding.PatchMethod) +
                                " for the same target and declaring type uses " +
                                GetTypeDisplayName(previousState.StateType));
                        }
                    }

                    stateBindings.Add(new StateBinding(binding, stateType));
                }
            }
        }

        private static int RequireTargetParameterIndex(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            IReadOnlyList<ParameterInfo> targetParameters,
            int targetParameterIndex)
        {
            if (targetParameterIndex < 0 ||
                targetParameterIndex >= targetParameters.Count)
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "does not resolve because target argument index " +
                    targetParameterIndex +
                    " is outside the target signature");
            }

            return targetParameterIndex;
        }

        private static int RequireTargetParameterName(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            IReadOnlyList<ParameterInfo> targetParameters,
            string? targetParameterName)
        {
            for (var targetParameterIndex = 0;
                 targetParameterIndex < targetParameters.Count;
                 targetParameterIndex++)
            {
                if (string.Equals(
                        targetParameters[targetParameterIndex].Name,
                        targetParameterName,
                        StringComparison.Ordinal))
                {
                    return targetParameterIndex;
                }
            }

            throw BindingViolation(
                binding,
                patchParameter,
                "does not match target argument name '" +
                (targetParameterName ?? "<null>") +
                "'");
        }

        private static void VerifyTargetArgumentType(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            ParameterInfo targetParameter)
        {
            Type patchParameterType = patchParameter.ParameterType;
            Type targetParameterType = targetParameter.ParameterType;
            Type patchElementType = GetElementType(patchParameterType);
            Type targetElementType = GetElementType(targetParameterType);

            if (patchParameterType.IsByRef)
            {
                if (patchElementType != targetElementType)
                {
                    throw BindingViolation(
                        binding,
                        patchParameter,
                        "has by-reference element type " +
                        GetTypeDisplayName(patchElementType) +
                        ", but target argument '" +
                        targetParameter.Name +
                        "' has element type " +
                        GetTypeDisplayName(targetElementType));
                }

                return;
            }

            if (!patchElementType.IsAssignableFrom(targetElementType))
            {
                throw BindingViolation(
                    binding,
                    patchParameter,
                    "has type " +
                    GetTypeDisplayName(patchElementType) +
                    ", which cannot receive target argument '" +
                    targetParameter.Name +
                    "' of type " +
                    GetTypeDisplayName(targetElementType));
            }
        }

        private static CustomAttributeData? GetHarmonyArgumentAttribute(
            ParameterInfo patchParameter)
        {
            IList<CustomAttributeData> attributes =
                patchParameter.GetCustomAttributesData();
            for (var attributeIndex = 0;
                 attributeIndex < attributes.Count;
                 attributeIndex++)
            {
                CustomAttributeData attribute = attributes[attributeIndex];
                if (string.Equals(
                        attribute.AttributeType.FullName,
                        HarmonyArgumentAttributeFullName,
                        StringComparison.Ordinal))
                {
                    if (attribute.ConstructorArguments.Count == 0)
                    {
                        throw new HarmonyPatchContractViolationException(
                            "HarmonyArgument on patch parameter '" +
                            patchParameter.Name +
                            "' has no name or index mapping.");
                    }

                    return attribute;
                }
            }

            return null;
        }

        private static string GetEffectiveParameterName(
            ParameterInfo patchParameter)
        {
            CustomAttributeData? argumentAttribute =
                GetHarmonyArgumentAttribute(patchParameter);
            if (argumentAttribute != null &&
                argumentAttribute.ConstructorArguments[0].ArgumentType ==
                typeof(string))
            {
                return (string?)argumentAttribute.ConstructorArguments[0]
                    .Value ?? patchParameter.Name ?? string.Empty;
            }

            string patchParameterName = patchParameter.Name ?? string.Empty;
            if (patchParameter.Member is MethodInfo patchMethod)
            {
                string? methodMapping = ResolveScopeArgumentMapping(
                    patchMethod.GetCustomAttributesData(),
                    patchParameterName);
                if (methodMapping != null)
                {
                    return methodMapping;
                }

                for (Type? patchType = patchMethod.DeclaringType;
                     patchType != null;
                     patchType = patchType.BaseType)
                {
                    string? typeMapping = ResolveScopeArgumentMapping(
                        patchType.GetCustomAttributesData(),
                        patchParameterName);
                    if (typeMapping != null)
                    {
                        return typeMapping;
                    }
                }
            }

            return patchParameterName;
        }

        private static string? ResolveScopeArgumentMapping(
            IList<CustomAttributeData> attributes,
            string patchParameterName)
        {
            for (var attributeIndex = 0;
                 attributeIndex < attributes.Count;
                 attributeIndex++)
            {
                CustomAttributeData attribute = attributes[attributeIndex];
                if (!string.Equals(
                        attribute.AttributeType.FullName,
                        HarmonyArgumentAttributeFullName,
                        StringComparison.Ordinal) ||
                    attribute.ConstructorArguments.Count < 2)
                {
                    continue;
                }

                CustomAttributeTypedArgument originalName =
                    attribute.ConstructorArguments[0];
                CustomAttributeTypedArgument newName =
                    attribute.ConstructorArguments[1];
                if (originalName.ArgumentType == typeof(string) &&
                    newName.ArgumentType == typeof(string) &&
                    string.Equals(
                        (string?)originalName.Value,
                        patchParameterName,
                        StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty((string?)newName.Value))
                {
                    return (string)newName.Value!;
                }
            }

            return null;
        }

        private static bool IsCodeInstructionSequence(Type type)
        {
            if (IsExactCodeInstructionSequence(type))
            {
                return true;
            }

            Type[] interfaces = type.GetInterfaces();
            for (var interfaceIndex = 0;
                 interfaceIndex < interfaces.Length;
                 interfaceIndex++)
            {
                if (IsExactCodeInstructionSequence(
                        interfaces[interfaceIndex]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactCodeInstructionSequence(Type type) =>
            type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
            string.Equals(
                type.GetGenericArguments()[0].FullName,
                "HarmonyLib.CodeInstruction",
                StringComparison.Ordinal);

        private static Type GetTargetReturnType(MethodBase targetMethod) =>
            targetMethod is MethodInfo targetMethodInfo
                ? targetMethodInfo.ReturnType
                : typeof(void);

        private static Type GetElementType(Type type) =>
            type.IsByRef
                ? type.GetElementType() ??
                    throw new InvalidOperationException(
                        "A by-reference type must expose an element type.")
                : type;

        private static string GetTypeDisplayName(Type type) =>
            type.FullName ?? type.Name;

        private static HarmonyPatchContractViolationException BindingViolation(
            HarmonyPatchContractBinding binding,
            string reason) =>
            new HarmonyPatchContractViolationException(
                "Harmony " +
                binding.PatchKind +
                " binding for patch " +
                GetMethodDisplayName(binding.PatchMethod) +
                " targeting " +
                GetMethodDisplayName(binding.TargetMethod) +
                " was rejected: " +
                reason +
                ".");

        private static HarmonyPatchContractViolationException BindingViolation(
            HarmonyPatchContractBinding binding,
            ParameterInfo patchParameter,
            string reason) =>
            new HarmonyPatchContractViolationException(
                "Harmony " +
                binding.PatchKind +
                " binding for patch " +
                GetMethodDisplayName(binding.PatchMethod) +
                " targeting " +
                GetMethodDisplayName(binding.TargetMethod) +
                " rejected parameter '" +
                (patchParameter.Name ?? "<unnamed>") +
                "': " +
                reason +
                ".");

        private static string GetMethodDisplayName(MethodBase method) =>
            (method.DeclaringType?.FullName ?? "<unknown-type>") +
            "." +
            method.Name;

        private sealed class StateBinding
        {
            internal StateBinding(
                HarmonyPatchContractBinding binding,
                Type stateType)
            {
                Binding = binding;
                StateType = stateType;
            }

            internal HarmonyPatchContractBinding Binding { get; }

            internal Type StateType { get; }
        }

        internal sealed class VerifiedBindings :
            IReadOnlyList<HarmonyPatchContractBinding>
        {
            private readonly HarmonyPatchContractBinding[] bindings;

            private VerifiedBindings(
                IReadOnlyList<HarmonyPatchContractBinding> sourceBindings)
            {
                bindings = new HarmonyPatchContractBinding[
                    sourceBindings.Count];
                for (var bindingIndex = 0;
                     bindingIndex < sourceBindings.Count;
                     bindingIndex++)
                {
                    bindings[bindingIndex] = sourceBindings[bindingIndex];
                }
            }

            internal static VerifiedBindings Create(
                IReadOnlyList<HarmonyPatchContractBinding> sourceBindings)
            {
                if (sourceBindings == null)
                {
                    throw new ArgumentNullException(nameof(sourceBindings));
                }

                var snapshot = new VerifiedBindings(sourceBindings);
                VerifyCore(snapshot);
                return snapshot;
            }

            public int Count => bindings.Length;

            public HarmonyPatchContractBinding this[int index] =>
                bindings[index];

            public IEnumerator<HarmonyPatchContractBinding> GetEnumerator() =>
                ((IEnumerable<HarmonyPatchContractBinding>)bindings)
                    .GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                bindings.GetEnumerator();
        }
    }
}
