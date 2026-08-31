using System.Reflection;
using HarmonyLib;

namespace DeliveryTemperatureLimit.Tests.HarmonyTranspilerInfrastructure;

[TestClass]
public sealed class HarmonyPatchContractBindingVerifierTests
{
    [TestMethod]
    public void NamedTargetArgument_WhenMetadataNameChanges_IsRejectedDuringPreparation()
    {
        MethodInfo targetMethod = RequireMethod(
            nameof(UpdatePickupsWithRenamedNavigator));
        MethodInfo patchMethod = RequireMethod(
            nameof(UpdatePickupsPrefixWithStaleNavigatorName));

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                HarmonyPatchContractBindingVerifier.VerifyAll(
                    [
                        new HarmonyPatchContractBinding(
                            targetMethod,
                            patchMethod,
                            HarmonyPatchContractKind.Prefix)
                    ]));

        StringAssert.Contains(exception.Message, "navigator");
        StringAssert.Contains(
            exception.Message,
            nameof(UpdatePickupsWithRenamedNavigator));
        StringAssert.Contains(
            exception.Message,
            nameof(UpdatePickupsPrefixWithStaleNavigatorName));
    }

    [TestMethod]
    public void PositionalTargetArgument_WhenIndexIsOutsideTarget_IsRejectedDuringPreparation()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(UpdatePickupsWithRenamedNavigator),
                    nameof(UpdatePickupsPrefixWithOutOfRangePosition)));

        StringAssert.Contains(exception.Message, "__2");
        StringAssert.Contains(exception.Message, "index 2");
    }

    [TestMethod]
    public void HarmonyArgumentIndex_WhenTargetMetadataNameDiffers_BindsByPosition()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithHarmonyArgumentIndex));
    }

    [TestMethod]
    public void HarmonyArgumentName_WhenPatchParameterIsRenamed_BindsOriginalName()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithHarmonyArgumentName));
    }

    [TestMethod]
    public void HarmonyArgumentMethodMapping_WhenPatchParameterIsRenamed_BindsOriginalName()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithMethodHarmonyArgument));
    }

    [TestMethod]
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

    [TestMethod]
    public void HarmonyArgumentIndex_WhenIndexIsOutsideTarget_IsRejectedWithIndexDiagnostic()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(UpdatePickupsWithRenamedNavigator),
                    nameof(UpdatePickupsPrefixWithOutOfRangeHarmonyArgument)));

        StringAssert.Contains(exception.Message, "index 4");
    }

    [TestMethod]
    public void PositionalTargetArgument_WhenMetadataNameChanges_BindsVerifiedPosition()
    {
        VerifySingle(
            nameof(UpdatePickupsWithRenamedNavigator),
            nameof(UpdatePickupsPrefixWithVerifiedPosition));
    }

    [TestMethod]
    public void NamedTargetArgument_WhenPatchTypeCannotReceiveTargetType_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithIncompatibleWorkerType)));

        StringAssert.Contains(exception.Message, "worker");
        StringAssert.Contains(exception.Message, "System.Int32");
        StringAssert.Contains(exception.Message, "System.String");
    }

    [TestMethod]
    public void NamedTargetArgument_WhenPatchByRefTypeDiffers_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithIncompatibleByRefWorkerType)));

        StringAssert.Contains(exception.Message, "worker");
        StringAssert.Contains(exception.Message, "by-reference");
    }

    [TestMethod]
    public void InstanceInjection_WhenTargetIsStatic_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInstance)));

        StringAssert.Contains(exception.Message, "__instance");
        StringAssert.Contains(exception.Message, "static target");
    }

    [TestMethod]
    public void InstanceInjection_WhenStructWouldNeedInterfaceBoxing_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    RequireStructTargetMethod(),
                    RequireMethod(nameof(PrefixWithStructInterfaceInstance)),
                    HarmonyPatchContractKind.Prefix));

        StringAssert.Contains(exception.Message, "__instance");
        StringAssert.Contains(exception.Message, nameof(StructTargetFixture));
    }

    [TestMethod]
    public void InstanceInjection_WhenStructIsReceivedAsObject_IsAccepted()
    {
        VerifySingle(
            RequireStructTargetMethod(),
            RequireMethod(nameof(PrefixWithBoxedStructInstance)),
            HarmonyPatchContractKind.Prefix);
    }

    [TestMethod]
    public void ResultInjection_WhenTargetReturnsVoid_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PostfixWithResult),
                    HarmonyPatchContractKind.Postfix));

        StringAssert.Contains(exception.Message, "__result");
        StringAssert.Contains(exception.Message, "void target");
    }

    [TestMethod]
    public void ResultInjection_WhenPatchTypeCannotReceiveReturnType_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerNumber),
                    nameof(PostfixWithIncompatibleResult),
                    HarmonyPatchContractKind.Postfix));

        StringAssert.Contains(exception.Message, "__result");
        StringAssert.Contains(exception.Message, "System.Int32");
        StringAssert.Contains(exception.Message, "System.String");
    }

    [TestMethod]
    public void ResultInjection_WhenValueTypeWouldNeedInterfaceBoxing_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetReturningResultStruct),
                    nameof(PostfixWithResultInterface),
                    HarmonyPatchContractKind.Postfix));

        StringAssert.Contains(exception.Message, "__result");
        StringAssert.Contains(exception.Message, nameof(ResultStructFixture));
    }

    [TestMethod]
    public void ResultInjection_WhenValueTypeIsReceivedAsObject_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningResultStruct),
            nameof(PostfixWithBoxedResult),
            HarmonyPatchContractKind.Postfix);
    }

    [TestMethod]
    public void FieldInjection_WhenFieldDoesNotExist_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    RequireFieldTargetMethod(),
                    RequireMethod(nameof(PrefixWithMissingField)),
                    HarmonyPatchContractKind.Prefix));

        StringAssert.Contains(exception.Message, "___missing");
        StringAssert.Contains(exception.Message, "field 'missing'");
    }

    [TestMethod]
    public void FieldInjection_WhenPatchTypeCannotReceiveFieldType_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    RequireFieldTargetMethod(),
                    RequireMethod(nameof(PrefixWithIncompatibleFieldType)),
                    HarmonyPatchContractKind.Prefix));

        StringAssert.Contains(exception.Message, "___counter");
        StringAssert.Contains(exception.Message, "System.Int32");
        StringAssert.Contains(exception.Message, "System.String");
    }

    [TestMethod]
    public void FieldInjection_WhenValueTypeWouldNeedBoxing_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    RequireFieldTargetMethod(),
                    RequireMethod(nameof(PrefixWithBoxedFieldType)),
                    HarmonyPatchContractKind.Prefix));

        StringAssert.Contains(exception.Message, "___counter");
        StringAssert.Contains(exception.Message, "System.Int32");
        StringAssert.Contains(exception.Message, "System.Object");
    }

    [TestMethod]
    public void ArgsInjection_WhenPatchTypeIsNotObjectArray_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInvalidArgsArray)));

        StringAssert.Contains(exception.Message, "__args");
        StringAssert.Contains(exception.Message, "System.Object[]");
    }

    [TestMethod]
    public void RunOriginalInjection_WhenPatchTypeIsNotBoolean_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInvalidRunOriginal)));

        StringAssert.Contains(exception.Message, "__runOriginal");
        StringAssert.Contains(exception.Message, "System.Boolean");
    }

    [TestMethod]
    public void OriginalMethodInjection_WhenPatchTypeCannotReceiveMethodBase_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithInvalidOriginalMethod)));

        StringAssert.Contains(exception.Message, "__originalMethod");
        StringAssert.Contains(exception.Message, "System.Reflection.MethodBase");
    }

    [TestMethod]
    public void ExceptionInjection_WhenPatchIsNotFinalizer_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(PrefixWithException)));

        StringAssert.Contains(exception.Message, "__exception");
        StringAssert.Contains(exception.Message, "Finalizer");
    }

    [TestMethod]
    public void ResultReferenceInjection_WhenTargetDoesNotReturnByReference_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerNumber),
                    nameof(PostfixWithResultReference),
                    HarmonyPatchContractKind.Postfix));

        StringAssert.Contains(exception.Message, "__resultRef");
        StringAssert.Contains(exception.Message, "by-reference return");
    }

    [TestMethod]
    public void ResultReferenceInjection_WhenReferenceTypesMatch_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningWorkerReference),
            nameof(PostfixWithValidResultReference),
            HarmonyPatchContractKind.Postfix);
    }

    [TestMethod]
    public void ResultInjection_WhenRefReturnIsRequestedByValue_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerReference),
                    nameof(PostfixWithByValueRefReturnResult),
                    HarmonyPatchContractKind.Postfix));

        StringAssert.Contains(exception.Message, "__result");
        StringAssert.Contains(exception.Message, "by-reference return");
    }

    [TestMethod]
    public void ResultInjection_WhenRefReturnIsRequestedByReference_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningWorkerReference),
            nameof(PostfixWithByReferenceRefReturnResult),
            HarmonyPatchContractKind.Postfix);
    }

    [TestMethod]
    public void ResultReferenceInjection_WhenReferenceElementTypeDiffers_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerReference),
                    nameof(PostfixWithWrongResultReference),
                    HarmonyPatchContractKind.Postfix));

        StringAssert.Contains(exception.Message, "__resultRef");
        StringAssert.Contains(exception.Message, "System.Int32");
    }

    [TestMethod]
    public void SharedState_WhenPatchTypesDiffer_IsRejectedBeforeMutation()
    {
        MethodInfo targetMethod = RequireMethod(nameof(TargetWithWorkerNumber));

        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
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
                    ]));

        StringAssert.Contains(exception.Message, "__state");
        StringAssert.Contains(exception.Message, "System.Int32");
        StringAssert.Contains(exception.Message, "System.String");
    }

    [TestMethod]
    public void PatchMethod_WhenItIsNotStatic_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    RequireMethod(nameof(TargetWithWorkerNumber)),
                    RequireInstancePatchMethod(),
                    HarmonyPatchContractKind.Prefix));

        StringAssert.Contains(exception.Message, "must be static");
    }

    [TestMethod]
    [DataRow(nameof(PrefixWithInvalidReturn), 0)]
    [DataRow(nameof(PostfixWithInvalidReturn), 1)]
    [DataRow(nameof(FinalizerWithInvalidReturn), 3)]
    public void PatchReturn_WhenKindContractIsInvalid_IsRejected(
        string patchMethodName,
        int patchKindValue)
    {
        var patchKind = (HarmonyPatchContractKind)patchKindValue;
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetReturningWorkerNumber),
                    patchMethodName,
                    patchKind));

        StringAssert.Contains(exception.Message, "return type");
    }

    [TestMethod]
    public void PassThroughPostfix_WhenFirstParameterHasArbitraryName_IsAccepted()
    {
        VerifySingle(
            nameof(TargetReturningWorkerNumber),
            nameof(ValidPassThroughPostfix),
            HarmonyPatchContractKind.Postfix);
    }

    [TestMethod]
    public void Transpiler_WhenReturnTypeIsNotInstructionSequence_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(TranspilerWithVoidReturn),
                    HarmonyPatchContractKind.Transpiler));

        StringAssert.Contains(exception.Message, "Transpiler");
        StringAssert.Contains(exception.Message, "IEnumerable");
    }

    [TestMethod]
    public void Transpiler_WhenParameterTypeIsUnsupported_IsRejected()
    {
        HarmonyPatchContractViolationException exception =
            Assert.ThrowsExactly<HarmonyPatchContractViolationException>(() =>
                VerifySingle(
                    nameof(TargetWithWorkerNumber),
                    nameof(TranspilerWithUnsupportedParameter),
                    HarmonyPatchContractKind.Transpiler));

        StringAssert.Contains(exception.Message, "instructions");
        StringAssert.Contains(exception.Message, "unsupported transpiler parameter");
    }

    [TestMethod]
    public void VerifyAll_WhenSourceListChanges_ReturnsImmutableVerifiedSnapshot()
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

        Assert.AreEqual(1, verifiedBindings.Count);
        Assert.AreSame(binding, verifiedBindings[0]);
    }

    [TestMethod]
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

    [TestMethod]
    public void FieldInjection_WhenPrivateInstanceFieldTypeMatches_IsAccepted()
    {
        VerifySingle(
            RequireFieldTargetMethod(),
            RequireMethod(nameof(PrefixWithValidField)),
            HarmonyPatchContractKind.Prefix);
    }

    [TestMethod]
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

    [TestMethod]
    public void Transpiler_WhenDocumentedParameterTypesAreUsed_IsAccepted()
    {
        VerifySingle(
            nameof(TargetWithWorkerNumber),
            nameof(ValidTranspiler),
            HarmonyPatchContractKind.Transpiler);
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
        throw new AssertFailedException(
            "Missing binding-contract fixture method " + methodName + ".");

    private static MethodInfo RequireFieldTargetMethod() =>
        typeof(FieldTargetFixture).GetMethod(
            nameof(FieldTargetFixture.Mutate),
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new AssertFailedException("Missing field-target fixture method.");

    private static MethodInfo RequireStructTargetMethod() =>
        typeof(StructTargetFixture).GetMethod(
            nameof(StructTargetFixture.Mutate),
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new AssertFailedException("Missing struct-target fixture method.");

    private static MethodInfo RequireInstancePatchMethod() =>
        typeof(InstancePatchFixture).GetMethod(
            nameof(InstancePatchFixture.Prefix),
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new AssertFailedException("Missing instance patch fixture method.");

    private static MethodInfo RequireTypeMappedPatchMethod() =>
        typeof(TypeMappedPatchFixture).GetMethod(
            nameof(TypeMappedPatchFixture.Prefix),
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new AssertFailedException("Missing type-mapped patch fixture method.");

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
}
