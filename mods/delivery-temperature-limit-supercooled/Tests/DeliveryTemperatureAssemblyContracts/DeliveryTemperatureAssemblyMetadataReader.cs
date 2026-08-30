using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

internal sealed record AssemblyReferenceContract(string Name, Version Version);

internal sealed record AssemblyInstructionContract(
    int Offset,
    string Operation,
    object? Operand,
    string? ResolvedOperand)
{
    public override string ToString() => ResolvedOperand is null
        ? $"IL_{Offset:X4}: {Operation}" + (Operand is null ? string.Empty : $" {Operand}")
        : $"IL_{Offset:X4}: {Operation} {ResolvedOperand}";
}

internal sealed record AssemblyMethodBodyContract(
    string DeclaringType,
    string MethodName,
    string Signature,
    IReadOnlyList<AssemblyInstructionContract> Instructions)
{
    internal string FormatInstructions() => string.Join(
        Environment.NewLine,
        Instructions.Select(instruction => instruction.ToString()));
}

/// <summary>
/// Reads managed-assembly contracts without loading target assemblies for execution.
/// This is essential for ONI binaries, which are built for Unity's runtime rather than
/// the modern runtime executing this test suite.
/// </summary>
internal static class DeliveryTemperatureAssemblyMetadataReader
{
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

    internal static string ResolveManagedAssemblyPath(
        string managedDirectory,
        string assemblyFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyFileName);
        if (!Path.IsPathRooted(managedDirectory))
        {
            throw new ArgumentException(
                "The ONI Managed directory must be an absolute path.",
                nameof(managedDirectory));
        }

        if (!string.Equals(
            Path.GetFileName(assemblyFileName),
            assemblyFileName,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The assembly name must not contain a directory component.",
                nameof(assemblyFileName));
        }

        var managedDirectoryPath = Path.GetFullPath(managedDirectory);
        if (!Directory.Exists(managedDirectoryPath))
        {
            throw new DirectoryNotFoundException(
                $"The ONI Managed directory does not exist: {managedDirectoryPath}");
        }

        var assemblyPath = Path.GetFullPath(
            Path.Combine(managedDirectoryPath, assemblyFileName));
        var relativePath = Path.GetRelativePath(managedDirectoryPath, assemblyPath);
        var escapesManagedDirectory = Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
        if (escapesManagedDirectory)
        {
            throw new InvalidDataException(
                $"Resolved assembly path escapes the ONI Managed directory: {assemblyPath}");
        }

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"The ONI Managed assembly does not exist: {assemblyPath}",
                assemblyPath);
        }

        return assemblyPath;
    }

    internal static IReadOnlyList<string> ReadPublicSurface(string assemblyPath) =>
        Read(assemblyPath, static (peReader, metadata) =>
        {
            var surface = new List<string>();
            foreach (var typeHandle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(typeHandle);
                if (!IsEffectivelyPublic(metadata, typeHandle))
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
        });

    internal static Version ReadAssemblyVersion(string assemblyPath) =>
        Read(
            assemblyPath,
            static (_, metadata) => metadata.GetAssemblyDefinition().Version);

    internal static IReadOnlyList<AssemblyReferenceContract> ReadAssemblyReferences(
        string assemblyPath) =>
        Read(assemblyPath, static (_, metadata) =>
            metadata.AssemblyReferences
                .Select(handle => metadata.GetAssemblyReference(handle))
                .Select(reference => new AssemblyReferenceContract(
                    metadata.GetString(reference.Name),
                    reference.Version))
                .OrderBy(reference => reference.Name, StringComparer.Ordinal)
                .ToArray());

    internal static object? ReadFieldConstant(
        string assemblyPath,
        string declaringType,
        string fieldName) =>
        Read(assemblyPath, (_, metadata) =>
        {
            var typeHandle = FindType(metadata, declaringType);
            var type = metadata.GetTypeDefinition(typeHandle);
            var matchingFields = type.GetFields()
                .Where(handle => string.Equals(
                    metadata.GetString(metadata.GetFieldDefinition(handle).Name),
                    fieldName,
                    StringComparison.Ordinal))
                .ToArray();
            if (matchingFields.Length != 1)
            {
                throw new InvalidDataException(
                    $"Expected exactly one field {declaringType}.{fieldName}, but found {matchingFields.Length}.");
            }

            var defaultValue = metadata
                .GetFieldDefinition(matchingFields[0])
                .GetDefaultValue();
            if (defaultValue.IsNil)
            {
                throw new InvalidDataException(
                    $"Field {declaringType}.{fieldName} has no metadata constant.");
            }

            return ReadConstant(metadata, metadata.GetConstant(defaultValue));
        });

    internal static IReadOnlyList<AssemblyMethodBodyContract> ReadMethodBodies(
        string assemblyPath,
        string declaringType,
        string methodName) =>
        Read(assemblyPath, (peReader, metadata) =>
        {
            var typeHandle = FindType(metadata, declaringType);
            var type = metadata.GetTypeDefinition(typeHandle);
            var methods = new List<AssemblyMethodBodyContract>();
            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                if (!string.Equals(
                    metadata.GetString(method.Name),
                    methodName,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                var signature = Convert.ToHexString(
                    metadata.GetBlobBytes(method.Signature));
                var instructions = method.RelativeVirtualAddress == 0
                    ? Array.Empty<AssemblyInstructionContract>()
                    : ReadInstructions(
                        peReader.GetMethodBody(method.RelativeVirtualAddress),
                        metadata);
                methods.Add(new(
                    declaringType,
                    methodName,
                    signature,
                    instructions));
            }

            return methods;
        });

    internal static bool TypeExists(
        string assemblyPath,
        string fullName) =>
        Read(assemblyPath, (_, metadata) => metadata.TypeDefinitions.Any(
            handle => string.Equals(
                GetTypeName(metadata, handle),
                fullName,
                StringComparison.Ordinal)));

    internal static bool MethodExists(
        string assemblyPath,
        string declaringType,
        string methodName) =>
        Read(assemblyPath, (_, metadata) =>
        {
            var type = metadata.GetTypeDefinition(
                FindType(metadata, declaringType));
            return type.GetMethods().Any(handle => string.Equals(
                metadata.GetString(
                    metadata.GetMethodDefinition(handle).Name),
                methodName,
                StringComparison.Ordinal));
        });

    internal static void AssertProtectedInstanceVoidMethodWithoutParameters(
        string assemblyPath,
        string declaringType,
        string methodName) =>
        Read(assemblyPath, (_, metadata) =>
        {
            var type = metadata.GetTypeDefinition(
                FindType(metadata, declaringType));
            var matchingMethods = type.GetMethods()
                .Select(handle =>
                    (Handle: handle,
                     Method: metadata.GetMethodDefinition(handle)))
                .Where(candidate => string.Equals(
                    metadata.GetString(candidate.Method.Name),
                    methodName,
                    StringComparison.Ordinal))
                .ToArray();
            if (matchingMethods.Length != 1)
            {
                throw new InvalidDataException(
                    $"Expected exactly one method {declaringType}.{methodName}, " +
                    $"but found {matchingMethods.Length}.");
            }

            MethodDefinition method = matchingMethods[0].Method;
            MethodAttributes memberAccess =
                method.Attributes & MethodAttributes.MemberAccessMask;
            bool isInstance =
                (method.Attributes & MethodAttributes.Static) == 0;
            string signature = Convert.ToHexString(
                metadata.GetBlobBytes(method.Signature));

            // ECMA-335 method signature 20 00 01 means HASTHIS, zero
            // parameters, and ELEMENT_TYPE_VOID. Keeping this assertion here
            // prevents a future test author from accidentally treating any
            // overload named OnLoadLevel as the runtime authority boundary.
            const string protectedInstanceVoidWithoutParametersSignature =
                "200001";
            if (memberAccess != MethodAttributes.Family ||
                !isInstance ||
                !string.Equals(
                    signature,
                    protectedInstanceVoidWithoutParametersSignature,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Method {declaringType}.{methodName} must be protected, " +
                    "instance, return System.Void, and declare no parameters. " +
                    $"Observed attributes={method.Attributes}; " +
                    $"signature={signature}.");
            }

            return true;
        });

    internal static void AssertPrivateSerializedInt32Field(
        string assemblyPath,
        string declaringType,
        string fieldName,
        params string[] requiredAttributeTypeNames) =>
        Read(assemblyPath, (_, metadata) =>
        {
            var type = metadata.GetTypeDefinition(
                FindType(metadata, declaringType));
            var matchingFields = type.GetFields()
                .Select(handle => (Handle: handle, Field: metadata.GetFieldDefinition(handle)))
                .Where(candidate => string.Equals(
                    metadata.GetString(candidate.Field.Name),
                    fieldName,
                    StringComparison.Ordinal))
                .ToArray();
            if (matchingFields.Length != 1)
            {
                throw new InvalidDataException(
                    $"Expected exactly one field {declaringType}.{fieldName}, but found {matchingFields.Length}.");
            }

            var field = matchingFields[0].Field;
            if ((field.Attributes & FieldAttributes.FieldAccessMask) !=
                    FieldAttributes.Private ||
                !Convert.ToHexString(metadata.GetBlobBytes(field.Signature))
                    .Equals("06 08".Replace(" ", string.Empty), StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Field {declaringType}.{fieldName} must be one private System.Int32 field.");
            }

            string[] observedAttributeTypeNames = field.GetCustomAttributes()
                .Select(handle => GetCustomAttributeTypeName(
                    metadata,
                    metadata.GetCustomAttribute(handle)))
                .ToArray();
            foreach (string requiredAttributeTypeName in requiredAttributeTypeNames)
            {
                if (!observedAttributeTypeNames.Contains(
                        requiredAttributeTypeName,
                        StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Field {declaringType}.{fieldName} is missing required attribute {requiredAttributeTypeName}. " +
                        $"Observed: {string.Join(", ", observedAttributeTypeNames)}");
                }
            }

            return true;
        });

    private static T Read<T>(
        string assemblyPath,
        Func<PEReader, MetadataReader, T> read)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentNullException.ThrowIfNull(read);

        using var stream = new FileStream(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            throw new BadImageFormatException(
                $"Managed metadata is absent from {assemblyPath}.");
        }

        return read(peReader, peReader.GetMetadataReader());
    }

    private static TypeDefinitionHandle FindType(
        MetadataReader metadata,
        string fullName)
    {
        var matches = metadata.TypeDefinitions
            .Where(handle => string.Equals(
                GetTypeName(metadata, handle),
                fullName,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                $"Expected exactly one type {fullName}, but found {matches.Length}.");
        }

        return matches[0];
    }

    private static object? ReadConstant(
        MetadataReader metadata,
        Constant constant)
    {
        if (constant.TypeCode == ConstantTypeCode.NullReference)
        {
            return null;
        }

        var value = metadata.GetBlobReader(constant.Value);
        return constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => value.ReadBoolean(),
            ConstantTypeCode.Char => (char)value.ReadUInt16(),
            ConstantTypeCode.SByte => value.ReadSByte(),
            ConstantTypeCode.Byte => value.ReadByte(),
            ConstantTypeCode.Int16 => value.ReadInt16(),
            ConstantTypeCode.UInt16 => value.ReadUInt16(),
            ConstantTypeCode.Int32 => value.ReadInt32(),
            ConstantTypeCode.UInt32 => value.ReadUInt32(),
            ConstantTypeCode.Int64 => value.ReadInt64(),
            ConstantTypeCode.UInt64 => value.ReadUInt64(),
            ConstantTypeCode.Single => value.ReadSingle(),
            ConstantTypeCode.Double => value.ReadDouble(),
            ConstantTypeCode.String => value.ReadUTF16(value.Length),
            _ => throw new InvalidDataException(
                $"Unsupported metadata constant type {constant.TypeCode}.")
        };
    }

    private static IReadOnlyList<AssemblyInstructionContract> ReadInstructions(
        MethodBodyBlock body,
        MetadataReader metadata)
    {
        var reader = body.GetILReader();
        var instructions = new List<AssemblyInstructionContract>();
        while (reader.RemainingBytes > 0)
        {
            var offset = reader.Offset;
            var firstByte = reader.ReadByte();
            var value = firstByte == 0xFE
                ? unchecked((short)(0xFE00 | reader.ReadByte()))
                : (short)firstByte;
            if (!OpCodesByValue.TryGetValue(value, out var operation))
            {
                throw new InvalidDataException(
                    $"Unknown IL operation 0x{unchecked((ushort)value):X4} at IL_{offset:X4}.");
            }

            var (operand, resolvedOperand) = ReadOperand(
                ref reader,
                operation.OperandType,
                metadata);
            instructions.Add(new(
                offset,
                operation.Name ?? $"0x{unchecked((ushort)value):X4}",
                operand,
                resolvedOperand));
        }

        return instructions;
    }

    private static (object? Operand, string? ResolvedOperand) ReadOperand(
        ref BlobReader reader,
        OperandType operandType,
        MetadataReader metadata)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                return (null, null);
            case OperandType.ShortInlineI:
                return (reader.ReadSByte(), null);
            case OperandType.InlineI:
                return (reader.ReadInt32(), null);
            case OperandType.InlineI8:
                return (reader.ReadInt64(), null);
            case OperandType.ShortInlineR:
                return (reader.ReadSingle(), null);
            case OperandType.InlineR:
                return (reader.ReadDouble(), null);
            case OperandType.ShortInlineBrTarget:
                return (reader.ReadSByte(), null);
            case OperandType.InlineBrTarget:
                return (reader.ReadInt32(), null);
            case OperandType.ShortInlineVar:
                return (reader.ReadByte(), null);
            case OperandType.InlineVar:
                return (reader.ReadUInt16(), null);
            case OperandType.InlineSwitch:
            {
                var count = reader.ReadInt32();
                var targets = new int[count];
                for (var index = 0; index < count; index++)
                {
                    targets[index] = reader.ReadInt32();
                }

                return (targets, null);
            }
            case OperandType.InlineString:
            {
                var token = reader.ReadInt32();
                var value = metadata.GetUserString(
                    MetadataTokens.UserStringHandle(token));
                return (token, $"\"{value}\"");
            }
            case OperandType.InlineField:
            case OperandType.InlineMethod:
            case OperandType.InlineSig:
            case OperandType.InlineTok:
            case OperandType.InlineType:
            {
                var token = reader.ReadInt32();
                return (token, ResolveToken(metadata, token));
            }
            default:
                throw new InvalidDataException(
                    $"Unsupported IL operand type {operandType}.");
        }
    }

    private static string ResolveToken(MetadataReader metadata, int token)
    {
        var handle = MetadataTokens.Handle(token);
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeName(
                metadata,
                (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => GetTypeReferenceName(
                metadata,
                (TypeReferenceHandle)handle),
            HandleKind.TypeSpecification =>
                $"type-spec:{Convert.ToHexString(metadata.GetBlobBytes(metadata.GetTypeSpecification((TypeSpecificationHandle)handle).Signature))}",
            HandleKind.MethodDefinition => GetMethodName(
                metadata,
                (MethodDefinitionHandle)handle),
            HandleKind.MemberReference => GetMemberReferenceName(
                metadata,
                (MemberReferenceHandle)handle),
            HandleKind.FieldDefinition => GetFieldName(
                metadata,
                (FieldDefinitionHandle)handle),
            HandleKind.MethodSpecification =>
                $"method-spec:{ResolveToken(metadata, MetadataTokens.GetToken(metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method))}",
            HandleKind.StandaloneSignature =>
                $"signature:{Convert.ToHexString(metadata.GetBlobBytes(metadata.GetStandaloneSignature((StandaloneSignatureHandle)handle).Signature))}",
            _ => $"{handle.Kind}:0x{token:X8}"
        };
    }

    private static string GetCustomAttributeTypeName(
        MetadataReader metadata,
        CustomAttribute attribute)
    {
        EntityHandle constructor = attribute.Constructor;
        return constructor.Kind switch
        {
            HandleKind.MethodDefinition => GetTypeName(
                metadata,
                metadata.GetMethodDefinition(
                    (MethodDefinitionHandle)constructor).GetDeclaringType()),
            HandleKind.MemberReference => GetEntityName(
                metadata,
                metadata.GetMemberReference(
                    (MemberReferenceHandle)constructor).Parent),
            _ => throw new InvalidDataException(
                $"Unsupported custom-attribute constructor kind {constructor.Kind}.")
        };
    }

    private static string GetMethodName(
        MetadataReader metadata,
        MethodDefinitionHandle handle)
    {
        var method = metadata.GetMethodDefinition(handle);
        return $"{GetTypeName(metadata, method.GetDeclaringType())}.{metadata.GetString(method.Name)}";
    }

    private static string GetFieldName(
        MetadataReader metadata,
        FieldDefinitionHandle handle)
    {
        var field = metadata.GetFieldDefinition(handle);
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            if (metadata.GetTypeDefinition(typeHandle).GetFields().Contains(handle))
            {
                return $"{GetTypeName(metadata, typeHandle)}.{metadata.GetString(field.Name)}";
            }
        }

        return metadata.GetString(field.Name);
    }

    private static string GetMemberReferenceName(
        MetadataReader metadata,
        MemberReferenceHandle handle)
    {
        var member = metadata.GetMemberReference(handle);
        return $"{GetEntityName(metadata, member.Parent)}.{metadata.GetString(member.Name)}";
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

    private static bool IsEffectivelyPublic(
        MetadataReader metadata,
        TypeDefinitionHandle typeHandle)
    {
        TypeDefinition type = metadata.GetTypeDefinition(typeHandle);
        TypeAttributes visibility =
            type.Attributes & TypeAttributes.VisibilityMask;
        TypeDefinitionHandle declaringType = type.GetDeclaringType();
        if (declaringType.IsNil)
        {
            return visibility == TypeAttributes.Public;
        }

        // ILRepack internalizes a PLib top-level type but can preserve the
        // NestedPublic bit on its children. Such a child is not externally
        // accessible through an internal declaring type and therefore is not
        // part of the CLR-visible public contract. A genuinely public nested
        // type still requires every declaring type in its chain to be public.
        return visibility == TypeAttributes.NestedPublic &&
            IsEffectivelyPublic(metadata, declaringType);
    }

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
