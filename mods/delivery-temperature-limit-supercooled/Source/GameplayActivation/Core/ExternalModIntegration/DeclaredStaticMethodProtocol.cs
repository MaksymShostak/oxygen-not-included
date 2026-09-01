#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Exact declared signature of one public static interoperability method.
    /// </summary>
    internal sealed class DeclaredStaticMethodDescriptor
    {
        internal DeclaredStaticMethodDescriptor(
            string methodName,
            Type returnType,
            IEnumerable<Type> parameterTypes)
        {
            MethodName = RequireMethodName(methodName);
            ReturnType = returnType ??
                throw new ArgumentNullException(nameof(returnType));
            ParameterTypes = CopyParameterTypes(parameterTypes);
        }

        internal string MethodName { get; }

        internal Type ReturnType { get; }

        internal IReadOnlyList<Type> ParameterTypes { get; }

        private static string RequireMethodName(string methodName)
        {
            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            if (string.IsNullOrWhiteSpace(methodName) ||
                methodName.Length > 256 ||
                !string.Equals(
                    methodName,
                    methodName.Trim(),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A declared static-method name must be non-blank, bounded, " +
                    "and free of surrounding whitespace.",
                    nameof(methodName));
            }

            return methodName;
        }

        private static IReadOnlyList<Type> CopyParameterTypes(
            IEnumerable<Type> parameterTypes)
        {
            if (parameterTypes == null)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            var copied = new List<Type>();
            foreach (Type parameterType in parameterTypes)
            {
                if (parameterType == null)
                {
                    throw new ArgumentException(
                        "A declared static-method parameter type cannot be null.",
                        nameof(parameterTypes));
                }

                copied.Add(parameterType);
            }

            return new ReadOnlyCollection<Type>(copied);
        }
    }

    /// <summary>
    /// Verifies one explicitly supplied endpoint type against exact declared
    /// public-static method signatures. It never discovers or scans assemblies.
    /// </summary>
    internal static class DeclaredStaticMethodProtocol
    {
        internal static IReadOnlyList<MethodInfo> Verify(
            Type endpointType,
            IEnumerable<DeclaredStaticMethodDescriptor> methodDescriptors)
        {
            if (endpointType == null)
            {
                throw new ArgumentNullException(nameof(endpointType));
            }

            ValidateEndpointType(endpointType);
            IReadOnlyList<DeclaredStaticMethodDescriptor> descriptors =
                CopyMethodDescriptors(methodDescriptors);
            MethodInfo[] declaredMethods = endpointType.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            var verifiedMethods = new List<MethodInfo>(descriptors.Count);
            for (int descriptorIndex = 0;
                 descriptorIndex < descriptors.Count;
                 descriptorIndex++)
            {
                DeclaredStaticMethodDescriptor descriptor =
                    descriptors[descriptorIndex];
                MethodInfo method = FindOnlyDeclaredMethod(
                    endpointType,
                    declaredMethods,
                    descriptor.MethodName);
                VerifyMethodSignature(endpointType, method, descriptor);
                verifiedMethods.Add(method);
            }

            return new ReadOnlyCollection<MethodInfo>(verifiedMethods);
        }

        private static void ValidateEndpointType(Type endpointType)
        {
            if (!endpointType.IsPublic ||
                endpointType.IsNested ||
                !endpointType.IsAbstract ||
                !endpointType.IsSealed ||
                endpointType.IsGenericType)
            {
                throw new InvalidOperationException(
                    "A declared static-method endpoint must be one public, " +
                    "non-generic, top-level static type.");
            }
        }

        private static IReadOnlyList<DeclaredStaticMethodDescriptor>
            CopyMethodDescriptors(
                IEnumerable<DeclaredStaticMethodDescriptor> methodDescriptors)
        {
            if (methodDescriptors == null)
            {
                throw new ArgumentNullException(nameof(methodDescriptors));
            }

            var copied = new List<DeclaredStaticMethodDescriptor>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DeclaredStaticMethodDescriptor descriptor in
                     methodDescriptors)
            {
                if (descriptor == null)
                {
                    throw new ArgumentException(
                        "A declared static-method descriptor cannot be null.",
                        nameof(methodDescriptors));
                }

                if (!names.Add(descriptor.MethodName))
                {
                    throw new ArgumentException(
                        "A static-method protocol cannot declare one method " +
                        "name more than once.",
                        nameof(methodDescriptors));
                }

                copied.Add(descriptor);
            }

            if (copied.Count == 0)
            {
                throw new ArgumentException(
                    "A static-method protocol requires at least one exact " +
                    "method descriptor.",
                    nameof(methodDescriptors));
            }

            return new ReadOnlyCollection<DeclaredStaticMethodDescriptor>(
                copied);
        }

        private static MethodInfo FindOnlyDeclaredMethod(
            Type endpointType,
            IReadOnlyList<MethodInfo> declaredMethods,
            string requiredMethodName)
        {
            MethodInfo? matchingMethod = null;
            for (int methodIndex = 0;
                 methodIndex < declaredMethods.Count;
                 methodIndex++)
            {
                MethodInfo candidate = declaredMethods[methodIndex];
                if (!string.Equals(
                        candidate.Name,
                        requiredMethodName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (matchingMethod != null)
                {
                    throw new InvalidOperationException(
                        "Declared static-method endpoint " +
                        GetTypeDisplayName(endpointType) +
                        " contains more than one method named " +
                        requiredMethodName + ".");
                }

                matchingMethod = candidate;
            }

            return matchingMethod ??
                throw new InvalidOperationException(
                    "Declared static-method endpoint " +
                    GetTypeDisplayName(endpointType) +
                    " does not define required method " +
                    requiredMethodName + ".");
        }

        private static void VerifyMethodSignature(
            Type endpointType,
            MethodInfo method,
            DeclaredStaticMethodDescriptor descriptor)
        {
            if (!method.IsPublic ||
                !method.IsStatic ||
                method.IsGenericMethod)
            {
                throw SignatureMismatch(endpointType, descriptor.MethodName);
            }

            if (!ReferenceEquals(method.ReturnType, descriptor.ReturnType))
            {
                throw SignatureMismatch(endpointType, descriptor.MethodName);
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != descriptor.ParameterTypes.Count)
            {
                throw SignatureMismatch(endpointType, descriptor.MethodName);
            }

            for (int parameterIndex = 0;
                 parameterIndex < parameters.Length;
                 parameterIndex++)
            {
                if (!ReferenceEquals(
                        parameters[parameterIndex].ParameterType,
                        descriptor.ParameterTypes[parameterIndex]))
                {
                    throw SignatureMismatch(
                        endpointType,
                        descriptor.MethodName);
                }
            }
        }

        private static InvalidOperationException SignatureMismatch(
            Type endpointType,
            string methodName) =>
            new InvalidOperationException(
                "Declared static-method endpoint " +
                GetTypeDisplayName(endpointType) +
                " does not expose the exact public static signature for " +
                methodName + ".");

        private static string GetTypeDisplayName(Type type) =>
            type.FullName ?? type.Name;
    }
}
