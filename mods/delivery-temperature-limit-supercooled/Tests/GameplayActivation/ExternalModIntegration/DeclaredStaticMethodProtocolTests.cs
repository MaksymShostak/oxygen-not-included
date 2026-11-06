using System.Reflection;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit.Tests.GameplayActivation.ExternalModIntegration;
public sealed class DeclaredStaticMethodProtocolTests
{
    [Test]
    public async Task Verify_WhenEndpointMatchesExactProtocol_ReturnsMethodsInDeclarationOrder()
    {
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            Getter(),
            Setter(),
            Identifier());

        IReadOnlyList<MethodInfo> verifiedMethods =
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors());

        await Assert.That(verifiedMethods.Select(method => method.Name).ToArray()).IsEquivalentTo(new[]
            {
                "Blueprints_GetData",
                "Blueprints_SetData",
                "Blueprints_ID"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Verify_WhenRequiredMethodIsMissing_RejectsEndpoint()
    {
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            Getter(),
            Setter());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Verify_WhenDeclaredNameIsOverloaded_RejectsAmbiguousEndpoint()
    {
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            Getter(),
            new EmittedMethod(
                "Blueprints_GetData",
                typeof(ProtocolJsonObject),
                new[] { typeof(RenamedProtocolGameObject) }),
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Verify_WhenReturnTypeChanges_RejectsEndpoint()
    {
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            new EmittedMethod(
                "Blueprints_GetData",
                typeof(string),
                new[] { typeof(ProtocolGameObject) }),
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Verify_WhenParameterTypeIdentityChanges_RejectsEndpoint()
    {
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            new EmittedMethod(
                "Blueprints_GetData",
                typeof(ProtocolJsonObject),
                new[] { typeof(RenamedProtocolGameObject) }),
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Verify_WhenParameterByReferenceShapeChanges_RejectsEndpoint()
    {
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            new EmittedMethod(
                "Blueprints_GetData",
                typeof(ProtocolJsonObject),
                new[] { typeof(ProtocolGameObject).MakeByRefType() }),
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [Arguments(false, true, DisplayName = "non-public method")]
    [Arguments(true, false, DisplayName = "instance method")]
    public async Task Verify_WhenMethodIsNotPublicStatic_RejectsEndpoint(
        bool isPublicMethod,
        bool isStaticMethod)
    {
        EmittedMethod invalidGetter = Getter() with
        {
            IsPublic = isPublicMethod,
            IsStatic = isStaticMethod
        };
        Type endpointType = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: true,
            invalidGetter,
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Verify_WhenEndpointTypeIsNotPublicStaticTopLevel_RejectsEndpoint()
    {
        Type internalEndpoint = CreateTopLevelEndpoint(
            isPublic: false,
            isStaticType: true,
            Getter(),
            Setter(),
            Identifier());
        Type instanceEndpoint = CreateTopLevelEndpoint(
            isPublic: true,
            isStaticType: false,
            Getter(),
            Setter(),
            Identifier());
        Type nestedEndpoint = CreateNestedEndpoint(
            Getter(),
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                internalEndpoint,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                instanceEndpoint,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                nestedEndpoint,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Verify_WhenEndpointTypeIsClosedGenericStaticType_RejectsEndpoint()
    {
        Type endpointType = CreateClosedGenericTopLevelEndpoint(
            Getter(),
            Setter(),
            Identifier());

        await Assert.That(() =>
            DeclaredStaticMethodProtocol.Verify(
                endpointType,
                ProtocolDescriptors())).ThrowsExactly<InvalidOperationException>();
    }

    private static IReadOnlyList<DeclaredStaticMethodDescriptor>
        ProtocolDescriptors() =>
        new[]
        {
            new DeclaredStaticMethodDescriptor(
                "Blueprints_GetData",
                typeof(ProtocolJsonObject),
                new[] { typeof(ProtocolGameObject) }),
            new DeclaredStaticMethodDescriptor(
                "Blueprints_SetData",
                typeof(void),
                new[]
                {
                    typeof(ProtocolGameObject),
                    typeof(ProtocolJsonObject)
                }),
            new DeclaredStaticMethodDescriptor(
                "Blueprints_ID",
                typeof(string),
                Type.EmptyTypes)
        };

    private static EmittedMethod Getter() =>
        new(
            "Blueprints_GetData",
            typeof(ProtocolJsonObject),
            new[] { typeof(ProtocolGameObject) });

    private static EmittedMethod Setter() =>
        new(
            "Blueprints_SetData",
            typeof(void),
            new[]
            {
                typeof(ProtocolGameObject),
                typeof(ProtocolJsonObject)
            });

    private static EmittedMethod Identifier() =>
        new("Blueprints_ID", typeof(string), Type.EmptyTypes);

    private static Type CreateTopLevelEndpoint(
        bool isPublic,
        bool isStaticType,
        params EmittedMethod[] methods)
    {
        ModuleBuilder module = CreateDynamicModule();
        TypeAttributes visibility = isPublic
            ? TypeAttributes.Public
            : TypeAttributes.NotPublic;
        TypeAttributes shape = isStaticType
            ? TypeAttributes.Abstract | TypeAttributes.Sealed
            : TypeAttributes.Sealed;
        TypeBuilder endpoint = module.DefineType(
            "SyntheticProtocolEndpoint" + Guid.NewGuid().ToString("N"),
            visibility | shape | TypeAttributes.Class);
        DefineMethods(endpoint, methods);
        return endpoint.CreateType()!;
    }

    private static Type CreateNestedEndpoint(params EmittedMethod[] methods)
    {
        ModuleBuilder module = CreateDynamicModule();
        TypeBuilder outer = module.DefineType(
            "SyntheticProtocolContainer" + Guid.NewGuid().ToString("N"),
            TypeAttributes.Public | TypeAttributes.Class);
        TypeBuilder endpoint = outer.DefineNestedType(
            "NestedEndpoint",
            TypeAttributes.NestedPublic |
                TypeAttributes.Abstract |
                TypeAttributes.Sealed |
                TypeAttributes.Class);
        DefineMethods(endpoint, methods);
        _ = outer.CreateType();
        return endpoint.CreateType()!;
    }

    private static Type CreateClosedGenericTopLevelEndpoint(
        params EmittedMethod[] methods)
    {
        ModuleBuilder module = CreateDynamicModule();
        TypeBuilder endpoint = module.DefineType(
            "SyntheticGenericProtocolEndpoint" +
                Guid.NewGuid().ToString("N"),
            TypeAttributes.Public |
                TypeAttributes.Abstract |
                TypeAttributes.Sealed |
                TypeAttributes.Class);
        _ = endpoint.DefineGenericParameters("TValue");
        DefineMethods(endpoint, methods);
        Type openEndpointType = endpoint.CreateType()!;
        return openEndpointType.MakeGenericType(typeof(int));
    }

    private static ModuleBuilder CreateDynamicModule()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(
                "DeclaredStaticMethodProtocolFixture" +
                Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule("Main");
    }

    private static void DefineMethods(
        TypeBuilder endpoint,
        IEnumerable<EmittedMethod> methods)
    {
        foreach (EmittedMethod method in methods)
        {
            MethodAttributes attributes = MethodAttributes.HideBySig;
            attributes |= method.IsPublic
                ? MethodAttributes.Public
                : MethodAttributes.Private;
            if (method.IsStatic)
            {
                attributes |= MethodAttributes.Static;
            }

            MethodBuilder methodBuilder = endpoint.DefineMethod(
                method.Name,
                attributes,
                method.ReturnType,
                method.ParameterTypes);
            ILGenerator il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Newobj, typeof(NotImplementedException)
                .GetConstructor(Type.EmptyTypes)!);
            il.Emit(OpCodes.Throw);
        }
    }

    private sealed class ProtocolGameObject
    {
    }

    private sealed class RenamedProtocolGameObject
    {
    }

    private sealed class ProtocolJsonObject
    {
    }

    private sealed record EmittedMethod(
        string Name,
        Type ReturnType,
        Type[] ParameterTypes)
    {
        internal bool IsPublic { get; init; } = true;

        internal bool IsStatic { get; init; } = true;
    }
}
