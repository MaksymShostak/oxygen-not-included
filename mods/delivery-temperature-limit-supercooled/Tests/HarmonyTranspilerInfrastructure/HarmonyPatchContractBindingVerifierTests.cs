using System.Reflection;
using HarmonyLib;

namespace DeliveryTemperatureLimit.Tests.HarmonyTranspilerInfrastructure;
public sealed class HarmonyPatchContractBindingVerifierTests
{
    [Test]
    public async Task NamedTargetArgument_WhenMetadataNameChanges_IsRejectedDuringPreparation()
    {
        MethodInfo targetMethod = RequireMethod(
            nameof(UpdatePickupsWithRenamedNavigator));
        MethodInfo patchMethod = RequireMethod(
            nameof(UpdatePickupsPrefixWithStaleNavigatorName));

        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                HarmonyPatchContractBindingVerifier.VerifyAll(
                    [
                        new HarmonyPatchContractBinding(
                            targetMethod,
                            patchMethod,
                            HarmonyPatchContractKind.Prefix)
                    ])).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("navigator");
        await Assert.That(exception.Message).Contains(nameof(UpdatePickupsWithRenamedNavigator));
        await Assert.That(exception.Message).Contains(nameof(UpdatePickupsPrefixWithStaleNavigatorName));
    }

    [Test]
    public async Task PositionalTargetArgument_WhenIndexIsOutsideTarget_IsRejectedDuringPreparation()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(UpdatePickupsWithRenamedNavigator),
                    nameof(UpdatePickupsPrefixWithOutOfRangePosition))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__2");
        await Assert.That(exception.Message).Contains("index 2");
    }

    [Test]
    public void HarmonyArgumentIndex_WhenTargetMetadataNameDiffers_BindsByPosition()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithHarmonyArgumentIndex));
    }

    [Test]
    public void HarmonyArgumentName_WhenPatchParameterIsRenamed_BindsOriginalName()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithHarmonyArgumentName));
    }

    [Test]
    public void HarmonyArgumentMethodMapping_WhenPatchParameterIsRenamed_BindsOriginalName()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithMethodHarmonyArgument));
    }

    [Test]
    public void HarmonyArgumentTypeMapping_WhenPatchParameterIsRenamed_BindsOriginalName()
    {
        HarmonyPatchContractBindingVerifier.VerifyAll(
            [
                new HarmonyPatchContractBinding(
                    RequireMethod(nameof(UpdatePickupsWithRenamedNavigator)),
                    RequireTypeMappedPatchMethod(),
                    HarmonyPatchContractKind.Prefix)
            ]);
    }

    [Test]
    public async Task HarmonyArgumentIndex_WhenIndexIsOutsideTarget_IsRejectedWithIndexDiagnostic()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(UpdatePickupsWithRenamedNavigator),
                    nameof(UpdatePickupsPrefixWithOutOfRangeHarmonyArgument))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("index 4");
    }

    [Test]
    public void PositionalTargetArgument_WhenMetadataNameChanges_BindsVerifiedPosition()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithVerifiedPosition));
    }

    [Test]
    public async Task NamedTargetArgument_WhenPatchTypeCannotReceiveTargetType_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithIncompatibleWorkerType))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("worker");
        await Assert.That(exception.Message).Contains("System.Int32");
        await Assert.That(exception.Message).Contains("System.String");
    }

    [Test]
    public async Task NamedTargetArgument_WhenPatchByRefTypeDiffers_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithIncompatibleByRefWorkerType))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("worker");
        await Assert.That(exception.Message).Contains("by-reference");
    }

    [Test]
    public async Task InstanceInjection_WhenTargetIsStatic_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInstance))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__instance");
        await Assert.That(exception.Message).Contains("static target");
    }

    [Test]
    public async Task InstanceInjection_WhenStructWouldNeedInterfaceBoxing_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    RequireStructTargetMethod(),
                    RequireMethod(nameof(PrefixWithStructInterfaceInstance)),
                    HarmonyPatchContractKind.Prefix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__instance");
        await Assert.That(exception.Message).Contains(nameof(StructTargetFixture));
    }

    [Test]
    public void InstanceInjection_WhenStructIsReceivedAsObject_IsAccepted()
    {
        VerifySingle(
            RequireStructTargetMethod(),
            RequireMethod(nameof(PrefixWithBoxedStructInstance)),
            HarmonyPatchContractKind.Prefix);
    }

    [Test]
    public async Task ResultInjection_WhenTargetReturnsVoid_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PostfixWithResult),
                    HarmonyPatchContractKind.Postfix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__result");
        await Assert.That(exception.Message).Contains("void target");
    }

    [Test]
    public async Task ResultInjection_WhenPatchTypeCannotReceiveReturnType_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerNumber),
                    nameof(PostfixWithIncompatibleResult),
                    HarmonyPatchContractKind.Postfix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__result");
        await Assert.That(exception.Message).Contains("System.Int32");
        await Assert.That(exception.Message).Contains("System.String");
    }

    [Test]
    public async Task ResultInjection_WhenValueTypeWouldNeedInterfaceBoxing_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetReturningResultStruct),
                    nameof(PostfixWithResultInterface),
                    HarmonyPatchContractKind.Postfix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__result");
        await Assert.That(exception.Message).Contains(nameof(ResultStructFixture));
    }

    [Test]
    public void ResultInjection_WhenValueTypeIsReceivedAsObject_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningResultStruct),
            nameof(PostfixWithBoxedResult),
            HarmonyPatchContractKind.Postfix);
    }

    [Test]
    public async Task FieldInjection_WhenFieldDoesNotExist_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    RequireFieldTargetMethod(),
                    RequireMethod(nameof(PrefixWithMissingField)),
                    HarmonyPatchContractKind.Prefix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("___missing");
        await Assert.That(exception.Message).Contains("field 'missing'");
    }

    [Test]
    public async Task FieldInjection_WhenPatchTypeCannotReceiveFieldType_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    RequireFieldTargetMethod(),
                    RequireMethod(nameof(PrefixWithIncompatibleFieldType)),
                    HarmonyPatchContractKind.Prefix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("___counter");
        await Assert.That(exception.Message).Contains("System.Int32");
        await Assert.That(exception.Message).Contains("System.String");
    }

    [Test]
    public async Task FieldInjection_WhenValueTypeWouldNeedBoxing_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    RequireFieldTargetMethod(),
                    RequireMethod(nameof(PrefixWithBoxedFieldType)),
                    HarmonyPatchContractKind.Prefix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("___counter");
        await Assert.That(exception.Message).Contains("System.Int32");
        await Assert.That(exception.Message).Contains("System.Object");
    }

    [Test]
    public async Task ArgsInjection_WhenPatchTypeIsNotObjectArray_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInvalidArgsArray))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__args");
        await Assert.That(exception.Message).Contains("System.Object[]");
    }

    [Test]
    public async Task RunOriginalInjection_WhenPatchTypeIsNotBoolean_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInvalidRunOriginal))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__runOriginal");
        await Assert.That(exception.Message).Contains("System.Boolean");
    }

    [Test]
    public async Task OriginalMethodInjection_WhenPatchTypeCannotReceiveMethodBase_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInvalidOriginalMethod))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__originalMethod");
        await Assert.That(exception.Message).Contains("System.Reflection.MethodBase");
    }

    [Test]
    public async Task ExceptionInjection_WhenPatchIsNotFinalizer_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithException))).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__exception");
        await Assert.That(exception.Message).Contains("Finalizer");
    }

    [Test]
    public async Task ResultReferenceInjection_WhenTargetDoesNotReturnByReference_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerNumber),
                    nameof(PostfixWithResultReference),
                    HarmonyPatchContractKind.Postfix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__resultRef");
        await Assert.That(exception.Message).Contains("by-reference return");
    }

    [Test]
    public void ResultReferenceInjection_WhenReferenceTypesMatch_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningWorkerReference),
            nameof(PostfixWithValidResultReference),
            HarmonyPatchContractKind.Postfix);
    }

    [Test]
    public async Task ResultInjection_WhenRefReturnIsRequestedByValue_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerReference),
                    nameof(PostfixWithByValueRefReturnResult),
                    HarmonyPatchContractKind.Postfix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__result");
        await Assert.That(exception.Message).Contains("by-reference return");
    }

    [Test]
    public void ResultInjection_WhenRefReturnIsRequestedByReference_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningWorkerReference),
            nameof(PostfixWithByReferenceRefReturnResult),
            HarmonyPatchContractKind.Postfix);
    }

    [Test]
    public async Task ResultReferenceInjection_WhenReferenceElementTypeDiffers_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerReference),
                    nameof(PostfixWithWrongResultReference),
                    HarmonyPatchContractKind.Postfix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__resultRef");
        await Assert.That(exception.Message).Contains("System.Int32");
    }

    [Test]
    public async Task SharedState_WhenPatchTypesDiffer_IsRejectedBeforeMutation()
    {
        MethodInfo targetMethod = RequireMethod(nameof(TargetWithWorkerNumber));

        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                HarmonyPatchContractBindingVerifier.VerifyAll(
                    [
                        new HarmonyPatchContractBinding(
                            targetMethod,
                            RequireMethod(nameof(PrefixWithIntegerState)),
                            HarmonyPatchContractKind.Prefix),
                        new HarmonyPatchContractBinding(
                            targetMethod,
                            RequireMethod(nameof(PostfixWithStringState)),
                            HarmonyPatchContractKind.Postfix)
                    ])).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("__state");
        await Assert.That(exception.Message).Contains("System.Int32");
        await Assert.That(exception.Message).Contains("System.String");
    }

    [Test]
    public async Task PatchMethod_WhenItIsNotStatic_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    RequireMethod(nameof(TargetWithWorkerNumber)),
                    RequireInstancePatchMethod(),
                    HarmonyPatchContractKind.Prefix)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("must be static");
    }

    [Test]
    [Arguments(nameof(PrefixWithInvalidReturn), 0)]
    [Arguments(nameof(PostfixWithInvalidReturn), 1)]
    [Arguments(nameof(FinalizerWithInvalidReturn), 3)]
    public async Task PatchReturn_WhenKindContractIsInvalid_IsRejected(
        string patchMethodName,
        int patchKindValue)
    {
        var patchKind = (HarmonyPatchContractKind)patchKindValue;
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerNumber),
                    patchMethodName,
                    patchKind)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("return type");
    }

    [Test]
    public void PassThroughPostfix_WhenFirstParameterHasArbitraryName_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningWorkerNumber),
            nameof(ValidPassThroughPostfix),
            HarmonyPatchContractKind.Postfix);
    }

    [Test]
    public async Task Transpiler_WhenReturnTypeIsNotInstructionSequence_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(TranspilerWithVoidReturn),
                    HarmonyPatchContractKind.Transpiler)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("Transpiler");
        await Assert.That(exception.Message).Contains("IEnumerable");
    }

    [Test]
    public async Task Transpiler_WhenParameterTypeIsUnsupported_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(TranspilerWithUnsupportedParameter),
                    HarmonyPatchContractKind.Transpiler)).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("instructions");
        await Assert.That(exception.Message).Contains("unsupported transpiler parameter");
    }

    [Test]
    public async Task VerifyAll_WhenSourceListChanges_ReturnsImmutableVerifiedSnapshot()
    {
        var binding = new HarmonyPatchContractBinding(
            RequireMethod(nameof(UpdatePickupsWithRenamedNavigator)),
            RequireMethod(nameof(UpdatePickupsPrefixWithVerifiedPosition)),
            HarmonyPatchContractKind.Prefix);
        var sourceBindings = new List<HarmonyPatchContractBinding>
        {
            binding
        };

        var verifiedBindings =
            HarmonyPatchContractBindingVerifier.VerifyAll(sourceBindings);
        sourceBindings.Clear();

        await Assert.That(verifiedBindings.Count).IsEqualTo(1);
        await Assert.That(verifiedBindings[0]).IsSameReferenceAs(binding);
    }

    [Test]
    public void ValidSpecialInjections_WhenVerified_AreAccepted()
    {
        VerifySingle(
            RequireFieldTargetMethod(),
            RequireMethod(nameof(PrefixWithValidInstance)),
            HarmonyPatchContractKind.Prefix);
        VerifySingle(
            nameof(TargetReturningWorkerNumber),
            nameof(PostfixWithValidResult),
            HarmonyPatchContractKind.Postfix);
        VerifySingle(
            nameof(TargetWithWorkerNumber),
            nameof(PrefixWithValidArgsArray));
        VerifySingle(
            nameof(TargetWithWorkerNumber),
            nameof(PrefixWithValidRunOriginal));
        VerifySingle(
            nameof(TargetWithWorkerNumber),
            nameof(PrefixWithValidOriginalMethod));
        VerifySingle(
            nameof(TargetWithWorkerNumber),
            nameof(FinalizerWithValidException),
            HarmonyPatchContractKind.Finalizer);
    }

    [Test]
    public void FieldInjection_WhenPrivateInstanceFieldTypeMatches_IsAccepted()
    {
        VerifySingle(
            RequireFieldTargetMethod(),
            RequireMethod(nameof(PrefixWithValidField)),
            HarmonyPatchContractKind.Prefix);
    }

    [Test]
    public void SharedState_WhenTypesMatch_IsAccepted()
    {
        MethodInfo targetMethod = RequireMethod(nameof(TargetWithWorkerNumber));

        HarmonyPatchContractBindingVerifier.VerifyAll(
            [
                new HarmonyPatchContractBinding(
                    targetMethod,
                    RequireMethod(nameof(PrefixWithIntegerState)),
                    HarmonyPatchContractKind.Prefix),
                new HarmonyPatchContractBinding(
                    targetMethod,
                    RequireMethod(nameof(PostfixWithIntegerState)),
                    HarmonyPatchContractKind.Postfix)
            ]);
    }

    [Test]
    public void Transpiler_WhenDocumentedParameterTypesAreUsed_IsAccepted()
    {
        VerifySingle(
            nameof(TargetWithWorkerNumber),
            nameof(ValidTranspiler),
            HarmonyPatchContractKind.Transpiler);
    }

    [Test]
    public async Task TargetMethod_WhenReflectedOnDerivedType_IsRejectedDuringPreparation()
    {
        MethodInfo targetMethod = typeof(DerivedTargetFixture)
            .GetMethod(nameof(BaseTargetFixture.InheritedMethod))!;
        MethodInfo patchMethod = RequireMethod(nameof(PrefixWithValidOriginalMethod));

        HarmonyPatchContractViolationException exception =
            (await Assert.That(() =>
                HarmonyPatchContractBindingVerifier.VerifyAll(
                    [
                        new HarmonyPatchContractBinding(
                            targetMethod,
                            patchMethod,
                            HarmonyPatchContractKind.Prefix)
                    ])).ThrowsExactly<HarmonyPatchContractViolationException>())!;

        await Assert.That(exception.Message).Contains("reflected type");
        await Assert.That(exception.Message).Contains(nameof(DerivedTargetFixture));
        await Assert.That(exception.Message).Contains(nameof(BaseTargetFixture));
    }

    [Test]
    public void TargetMethod_WhenDeclaredOnReflectedType_IsAccepted()
    {
        MethodInfo targetMethod = typeof(BaseTargetFixture)
            .GetMethod(nameof(BaseTargetFixture.InheritedMethod))!;
        MethodInfo patchMethod = RequireMethod(nameof(PrefixWithValidOriginalMethod));

        HarmonyPatchContractBindingVerifier.VerifyAll(
            [
                new HarmonyPatchContractBinding(
                    targetMethod,
                    patchMethod,
                    HarmonyPatchContractKind.Prefix)
            ]);
    }

    private static void VerifySingle(
        string targetMethodName,
        string patchMethodName,
        HarmonyPatchContractKind patchKind = HarmonyPatchContractKind.Prefix) =>
        HarmonyPatchContractBindingVerifier.VerifyAll(
            [
                new HarmonyPatchContractBinding(
                    RequireMethod(targetMethodName),
                    RequireMethod(patchMethodName),
                    patchKind)
            ]);

    private static void VerifySingle(
        MethodBase targetMethod,
        MethodInfo patchMethod,
        HarmonyPatchContractKind patchKind) =>
        HarmonyPatchContractBindingVerifier.VerifyAll(
            [
                new HarmonyPatchContractBinding(
                    targetMethod,
                    patchMethod,
                    patchKind)
            ]);

    private static MethodInfo RequireMethod(string methodName) =>
        typeof(HarmonyPatchContractBindingVerifierTests).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new TUnit.Assertions.Exceptions.AssertionException(
            "Missing binding-contract fixture method " + methodName + ".");

    private static MethodInfo RequireFieldTargetMethod() =>
        typeof(FieldTargetFixture).GetMethod(
            nameof(FieldTargetFixture.Mutate),
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new TUnit.Assertions.Exceptions.AssertionException("Missing field-target fixture method.");

    private static MethodInfo RequireStructTargetMethod() =>
        typeof(StructTargetFixture).GetMethod(
            nameof(StructTargetFixture.Mutate),
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new TUnit.Assertions.Exceptions.AssertionException("Missing struct-target fixture method.");

    private static MethodInfo RequireInstancePatchMethod() =>
        typeof(InstancePatchFixture).GetMethod(
            nameof(InstancePatchFixture.Prefix),
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new TUnit.Assertions.Exceptions.AssertionException("Missing instance patch fixture method.");

    private static MethodInfo RequireTypeMappedPatchMethod() =>
        typeof(TypeMappedPatchFixture).GetMethod(
            nameof(TypeMappedPatchFixture.Prefix),
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new TUnit.Assertions.Exceptions.AssertionException("Missing type-mapped patch fixture method.");

    private static void UpdatePickupsWithRenamedNavigator(
        NavigatorFixture worker_navigator,
        int worker)
    {
        _ = worker_navigator;
        _ = worker;
    }

    private static void UpdatePickupsPrefixWithStaleNavigatorName(
        NavigatorFixture navigator) =>
        _ = navigator;

    private static void UpdatePickupsPrefixWithOutOfRangePosition(
        NavigatorFixture __2) =>
        _ = __2;

    private static void UpdatePickupsPrefixWithVerifiedPosition(
        NavigatorFixture __0) =>
        _ = __0;

    private static void UpdatePickupsPrefixWithHarmonyArgumentIndex(
        [HarmonyArgument(0)] NavigatorFixture navigator) =>
        _ = navigator;

    private static void UpdatePickupsPrefixWithHarmonyArgumentName(
        [HarmonyArgument("worker_navigator")] NavigatorFixture navigator) =>
        _ = navigator;

    [HarmonyArgument("navigator", "worker_navigator")]
    private static void UpdatePickupsPrefixWithMethodHarmonyArgument(
        NavigatorFixture navigator) =>
        _ = navigator;

    private static void UpdatePickupsPrefixWithOutOfRangeHarmonyArgument(
        [HarmonyArgument(4)] NavigatorFixture navigator) =>
        _ = navigator;

    private static void TargetWithWorkerNumber(int worker) =>
        _ = worker;

    private static void PrefixWithIncompatibleWorkerType(string worker) =>
        _ = worker;

    private static void PrefixWithIncompatibleByRefWorkerType(
        ref string worker) =>
        _ = worker;

    private static void PrefixWithInstance(object __instance) =>
        _ = __instance;

    private static void PrefixWithStructInterfaceInstance(
        IStructTargetFixture __instance) =>
        _ = __instance;

    private static void PrefixWithBoxedStructInstance(object __instance) =>
        _ = __instance;

    private static void PostfixWithResult(int __result) =>
        _ = __result;

    private static int TargetReturningWorkerNumber(int worker) => worker;

    private static void PostfixWithIncompatibleResult(string __result) =>
        _ = __result;

    private static ResultStructFixture TargetReturningResultStruct() =>
        new ResultStructFixture();

    private static void PostfixWithResultInterface(
        IResultStructFixture __result) =>
        _ = __result;

    private static void PostfixWithBoxedResult(object __result) =>
        _ = __result;

    private static void PrefixWithMissingField(int ___missing) =>
        _ = ___missing;

    private static void PrefixWithIncompatibleFieldType(string ___counter) =>
        _ = ___counter;

    private static void PrefixWithBoxedFieldType(object ___counter) =>
        _ = ___counter;

    private static void PrefixWithInvalidArgsArray(string __args) =>
        _ = __args;

    private static void PrefixWithInvalidRunOriginal(int __runOriginal) =>
        _ = __runOriginal;

    private static void PrefixWithInvalidOriginalMethod(string __originalMethod) =>
        _ = __originalMethod;

    private static void PrefixWithException(Exception __exception) =>
        _ = __exception;

    private static void PostfixWithResultReference(object __resultRef) =>
        _ = __resultRef;

    private static int referenceResultFixture;

    private static ref int TargetReturningWorkerReference() =>
        ref referenceResultFixture;

    private static void PostfixWithValidResultReference(
        ref RefResult<int> __resultRef) =>
        _ = __resultRef;

    private static void PostfixWithByValueRefReturnResult(int __result) =>
        _ = __result;

    private static void PostfixWithByReferenceRefReturnResult(
        ref int __result) =>
        _ = __result;

    private static void PostfixWithWrongResultReference(
        ref RefResult<string> __resultRef) =>
        _ = __resultRef;

    private static void PrefixWithIntegerState(out int __state) =>
        __state = 0;

    private static void PostfixWithStringState(string __state) =>
        _ = __state;

    private static void PostfixWithIntegerState(int __state) =>
        _ = __state;

    private static void PrefixWithValidInstance(FieldTargetFixture __instance) =>
        _ = __instance;

    private static void PostfixWithValidResult(ref int __result) =>
        _ = __result;

    private static void PrefixWithValidField(int ___counter) =>
        _ = ___counter;

    private static void PrefixWithValidArgsArray(object[] __args) =>
        _ = __args;

    private static void PrefixWithValidRunOriginal(bool __runOriginal) =>
        _ = __runOriginal;

    private static void PrefixWithValidOriginalMethod(
        MethodBase __originalMethod) =>
        _ = __originalMethod;

    private static Exception? FinalizerWithValidException(
        Exception? __exception) =>
        __exception;

    private static string PrefixWithInvalidReturn() => string.Empty;

    private static int PostfixWithInvalidReturn() => 0;

    private static int ValidPassThroughPostfix(int returnedValue) =>
        returnedValue;

    private static int FinalizerWithInvalidReturn() => 0;

    private static void TranspilerWithVoidReturn(
        IEnumerable<CodeInstruction> instructions) =>
        _ = instructions;

    private static IEnumerable<CodeInstruction>
        TranspilerWithUnsupportedParameter(string instructions)
    {
        _ = instructions;
        return [];
    }

    private static IEnumerable<CodeInstruction> ValidTranspiler(
        IEnumerable<CodeInstruction> instructions,
        System.Reflection.Emit.ILGenerator generator,
        MethodBase originalMethod)
    {
        _ = generator;
        _ = originalMethod;
        return instructions;
    }

    private sealed class NavigatorFixture
    {
    }

    private sealed class FieldTargetFixture
    {
#pragma warning disable CS0169
        private int counter;
#pragma warning restore CS0169

        internal void Mutate()
        {
        }
    }

    private interface IStructTargetFixture
    {
    }

    private struct StructTargetFixture : IStructTargetFixture
    {
        internal void Mutate()
        {
        }
    }

    private interface IResultStructFixture
    {
    }

    private readonly struct ResultStructFixture : IResultStructFixture
    {
    }

    private sealed class InstancePatchFixture
    {
        internal void Prefix()
        {
        }
    }

    [HarmonyArgument("navigator", "worker_navigator")]
    private static class TypeMappedPatchFixture
    {
        internal static void Prefix(NavigatorFixture navigator) =>
            _ = navigator;
    }

    private class BaseTargetFixture
    {
        public void InheritedMethod()
        {
        }
    }

    private sealed class DerivedTargetFixture : BaseTargetFixture
    {
    }
}
