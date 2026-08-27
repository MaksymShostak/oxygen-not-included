using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DeliveryTemperatureLimit.Tests;

internal static class PublicAssemblySurface
{
    internal static IReadOnlyList<string> Read(string assemblyPath)
    {
        using var stream = new FileStream(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var surface = new List<string>();

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            if (!IsPublic(type.Attributes))
            {
                continue;
            }

            var typeName = GetTypeName(metadata, typeHandle);
            surface.Add(
                $"type|{typeName}|arity={type.GetGenericParameters().Count}|kind={GetTypeKind(type.Attributes)}");
            AddMethods(metadata, type, typeName, surface);
            AddFields(metadata, type, typeName, surface);
            AddProperties(metadata, type, typeName, surface);
            AddEvents(metadata, type, typeName, surface);
        }

        return surface.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void AddMethods(
        MetadataReader metadata,
        TypeDefinition type,
        string typeName,
        ICollection<string> surface)
    {
        foreach (var handle in type.GetMethods())
        {
            var method = metadata.GetMethodDefinition(handle);
            if (!IsVisible(method.Attributes & MethodAttributes.MemberAccessMask))
            {
                continue;
            }

            var name = metadata.GetString(method.Name);
            var memberKind = name is ".ctor" or ".cctor" ? "constructor" : "method";
            var signature = Convert.ToHexString(metadata.GetBlobBytes(method.Signature));
            surface.Add(
                $"{memberKind}|{typeName}|{name}|access={GetAccess(method.Attributes & MethodAttributes.MemberAccessMask)}|arity={method.GetGenericParameters().Count}|parameters={CountParameters(metadata, method)}|signature={signature}");
        }
    }

    private static void AddFields(
        MetadataReader metadata,
        TypeDefinition type,
        string typeName,
        ICollection<string> surface)
    {
        foreach (var handle in type.GetFields())
        {
            var field = metadata.GetFieldDefinition(handle);
            if (!IsVisible(field.Attributes & FieldAttributes.FieldAccessMask))
            {
                continue;
            }

            var signature = Convert.ToHexString(metadata.GetBlobBytes(field.Signature));
            surface.Add(
                $"field|{typeName}|{metadata.GetString(field.Name)}|access={GetAccess(field.Attributes & FieldAttributes.FieldAccessMask)}|signature={signature}");
        }
    }

    private static void AddProperties(
        MetadataReader metadata,
        TypeDefinition type,
        string typeName,
        ICollection<string> surface)
    {
        foreach (var handle in type.GetProperties())
        {
            var property = metadata.GetPropertyDefinition(handle);
            var accessors = property.GetAccessors();
            var visibleAccessor = FirstVisibleMethod(
                metadata,
                accessors.Getter,
                accessors.Setter,
                accessors.Others);
            if (visibleAccessor.IsNil)
            {
                continue;
            }

            var method = metadata.GetMethodDefinition(visibleAccessor);
            var parameterCount = !accessors.Getter.IsNil
                ? CountParameters(metadata, metadata.GetMethodDefinition(accessors.Getter))
                : Math.Max(0, CountParameters(metadata, method) - 1);
            var signature = Convert.ToHexString(metadata.GetBlobBytes(property.Signature));
            surface.Add(
                $"property|{typeName}|{metadata.GetString(property.Name)}|access={GetAccess(method.Attributes & MethodAttributes.MemberAccessMask)}|parameters={parameterCount}|signature={signature}");
        }
    }

    private static void AddEvents(
        MetadataReader metadata,
        TypeDefinition type,
        string typeName,
        ICollection<string> surface)
    {
        foreach (var handle in type.GetEvents())
        {
            var eventDefinition = metadata.GetEventDefinition(handle);
            var accessors = eventDefinition.GetAccessors();
            var visibleAccessor = FirstVisibleMethod(
                metadata,
                accessors.Adder,
                accessors.Remover,
                accessors.Raiser,
                accessors.Others);
            if (visibleAccessor.IsNil)
            {
                continue;
            }

            var method = metadata.GetMethodDefinition(visibleAccessor);
            surface.Add(
                $"event|{typeName}|{metadata.GetString(eventDefinition.Name)}|access={GetAccess(method.Attributes & MethodAttributes.MemberAccessMask)}|type={GetEntityName(metadata, eventDefinition.Type)}");
        }
    }

    private static MethodDefinitionHandle FirstVisibleMethod(
        MetadataReader metadata,
        IEnumerable<MethodDefinitionHandle> handles)
    {
        foreach (var handle in handles)
        {
            if (!handle.IsNil && IsVisible(
                metadata.GetMethodDefinition(handle).Attributes &
                MethodAttributes.MemberAccessMask))
            {
                return handle;
            }
        }

        return default;
    }

    private static MethodDefinitionHandle FirstVisibleMethod(
        MetadataReader metadata,
        MethodDefinitionHandle first,
        MethodDefinitionHandle second,
        IEnumerable<MethodDefinitionHandle> others) =>
        FirstVisibleMethod(metadata, new[] { first, second }.Concat(others));

    private static MethodDefinitionHandle FirstVisibleMethod(
        MetadataReader metadata,
        MethodDefinitionHandle first,
        MethodDefinitionHandle second,
        MethodDefinitionHandle third,
        IEnumerable<MethodDefinitionHandle> others) =>
        FirstVisibleMethod(metadata, new[] { first, second, third }.Concat(others));

    private static int CountParameters(
        MetadataReader metadata,
        MethodDefinition method) =>
        method.GetParameters().Count(handle =>
            metadata.GetParameter(handle).SequenceNumber > 0);

    private static string GetTypeName(
        MetadataReader metadata,
        TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        var name = metadata.GetString(type.Name);
        var declaringType = type.GetDeclaringType();
        if (!declaringType.IsNil)
        {
            return $"{GetTypeName(metadata, declaringType)}+{name}";
        }

        var typeNamespace = metadata.GetString(type.Namespace);
        return string.IsNullOrEmpty(typeNamespace)
            ? name
            : $"{typeNamespace}.{name}";
    }

    private static string GetEntityName(MetadataReader metadata, EntityHandle handle) =>
        handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeName(
                metadata,
                (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => GetTypeReferenceName(
                metadata,
                (TypeReferenceHandle)handle),
            HandleKind.TypeSpecification =>
                $"spec:{Convert.ToHexString(metadata.GetBlobBytes(metadata.GetTypeSpecification((TypeSpecificationHandle)handle).Signature))}",
            _ => $"{handle.Kind}"
        };

    private static string GetTypeReferenceName(
        MetadataReader metadata,
        TypeReferenceHandle handle)
    {
        var type = metadata.GetTypeReference(handle);
        var name = metadata.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return $"{GetTypeReferenceName(metadata, (TypeReferenceHandle)type.ResolutionScope)}+{name}";
        }

        var typeNamespace = metadata.GetString(type.Namespace);
        return string.IsNullOrEmpty(typeNamespace)
            ? name
            : $"{typeNamespace}.{name}";
    }

    private static bool IsPublic(TypeAttributes attributes) =>
        (attributes & TypeAttributes.VisibilityMask) is
            TypeAttributes.Public or TypeAttributes.NestedPublic;

    private static bool IsVisible(MethodAttributes attributes) =>
        attributes is MethodAttributes.Public or
            MethodAttributes.Family or
            MethodAttributes.FamORAssem or
            MethodAttributes.FamANDAssem;

    private static bool IsVisible(FieldAttributes attributes) =>
        attributes is FieldAttributes.Public or
            FieldAttributes.Family or
            FieldAttributes.FamORAssem or
            FieldAttributes.FamANDAssem;

    private static string GetAccess(MethodAttributes attributes) =>
        attributes.ToString();

    private static string GetAccess(FieldAttributes attributes) =>
        attributes.ToString();

    private static string GetTypeKind(TypeAttributes attributes) =>
        (attributes & TypeAttributes.Interface) != 0
            ? "interface"
            : (attributes & TypeAttributes.Sealed) != 0
                ? "sealed"
                : (attributes & TypeAttributes.Abstract) != 0
                    ? "abstract"
                    : "class";
}
