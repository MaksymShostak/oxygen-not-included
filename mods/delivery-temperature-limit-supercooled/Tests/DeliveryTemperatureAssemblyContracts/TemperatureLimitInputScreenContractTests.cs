using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class TemperatureLimitInputScreenContractTests
{
    private const string ScreenType = "DeliveryTemperatureLimit.TemperatureLimitInputScreen";

    [TestMethod]
    public void BothTemperatureFields_RegisterAnInputScreenWithTheGame()
    {
        string path = BuildPath();
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        TypeDefinitionHandle screen = metadata.TypeDefinitions.SingleOrDefault(handle =>
            metadata.GetString(metadata.GetTypeDefinition(handle).Name) == "TemperatureLimitInputScreen");
        Assert.IsFalse(screen.IsNil,
            "Each TMP field needs an active input owner; the parent side screen does not receive keyboard events.");
        TypeDefinition definition = metadata.GetTypeDefinition(screen);
        Assert.AreEqual("KScreen", metadata.GetString(metadata.GetTypeReference(
            (TypeReferenceHandle)definition.BaseType).Name));

        var constructor = Body(path, ".ctor");
        int activation = constructor.Instructions.ToList().FindIndex(instruction =>
            instruction.ResolvedOperand == "KScreen.activateOnSpawn");
        Assert.IsTrue(activation > 0, "The input owner must register on spawn.");
        Assert.AreEqual("ldc.i4.1", constructor.Instructions[activation - 1].Operation);

        var widget = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
            metadata.GetString(type.Name) == "TemperatureLimitWidget");
        int attachedInputs = 0;
        foreach (MethodDefinitionHandle method in widget.GetMethods())
        {
            foreach (var body in DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                         path, "DeliveryTemperatureLimit.TemperatureLimitWidget",
                         metadata.GetString(metadata.GetMethodDefinition(method).Name)))
            {
                foreach (var instruction in body.Instructions.Where(instruction =>
                             instruction.ResolvedOperand == "method-spec:UnityEngine.GameObject.AddComponent"))
                {
                    var spec = metadata.GetMethodSpecification(
                        (MethodSpecificationHandle)MetadataTokens.EntityHandle((int)instruction.Operand!));
                    BlobReader signature = metadata.GetBlobReader(spec.Signature);
                    signature.ReadSignatureHeader();
                    if (signature.ReadCompressedInteger() != 1 ||
                        signature.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) continue;
                    if (signature.ReadTypeHandle() == screen) attachedInputs++;
                }
            }
        }
        Assert.AreEqual(2, attachedInputs, "Both lower and upper fields need a registered input owner.");
    }

    [TestMethod]
    public void InputOwner_WiresFocusAndEndEditAndUnsubscribesOnCleanup()
    {
        string path = BuildPath();
        string spawn = Body(path, "OnSpawn").FormatInstructions();
        StringAssert.Contains(spawn, "KScreen.OnSpawn");
        StringAssert.Contains(spawn, "TMPro.TMP_InputField.onFocus");
        StringAssert.Contains(spawn, "TemperatureLimitInputScreen.OnInputFocus");
        StringAssert.Contains(spawn, "TemperatureLimitInputScreen.OnInputEndEdit");
        StringAssert.Contains(spawn, "AddListener");
        string cleanup = Body(path, "OnCleanUp").FormatInstructions();
        StringAssert.Contains(cleanup, "System.Delegate.Remove");
        StringAssert.Contains(cleanup, "RemoveListener");
        StringAssert.Contains(cleanup, "TemperatureLimitInputScreen.ReleaseInputCapture");
    }

    [TestMethod]
    public void InputOwner_DisablingOrRefocusingCancelsPendingRelease()
    {
        string path = BuildPath();
        StringAssert.Contains(Body(path, "OnDisable").FormatInstructions(),
            "TemperatureLimitInputScreen.ReleaseInputCapture");
        StringAssert.Contains(Body(path, "OnInputFocus").FormatInstructions(),
            "TemperatureLimitInputScreen.CancelPendingRelease");
        StringAssert.Contains(Body(path, "ReleaseInputCapture").FormatInstructions(),
            "TemperatureLimitInputScreen.CancelPendingRelease");
        StringAssert.Contains(Body(path, "CancelPendingRelease").FormatInstructions(),
            "UnityEngine.MonoBehaviour.StopCoroutine");
        AssertEditingAssignment(Body(path, "OnInputFocus"), "ldc.i4.1");
        AssertEditingAssignment(Body(path, "ReleaseInputCapture"), "ldc.i4.0");
    }

    [TestMethod]
    public void InputOwner_EndEditDefersReleaseUntilEndOfFrame()
    {
        string path = BuildPath();
        string endEdit = Body(path, "OnInputEndEdit").FormatInstructions();
        StringAssert.Contains(endEdit, "UnityEngine.MonoBehaviour.StartCoroutine");
        StringAssert.Contains(endEdit, "TemperatureLimitInputScreen.ReleaseAfterEditFrame");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        string iteratorName = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Select(type => metadata.GetString(type.Name))
            .Single(name => name.StartsWith("<ReleaseAfterEditFrame>", StringComparison.Ordinal));
        var moveNext = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
            path, ScreenType + "+" + iteratorName, "MoveNext").Single();
        StringAssert.Contains(moveNext.FormatInstructions(), "UnityEngine.WaitForEndOfFrame..ctor");
        AssertEditingAssignment(moveNext, "ldc.i4.0");
    }

    [TestMethod]
    public void InputOwner_KeyDownAndKeyUpConsumeOnlyDuringEditing()
    {
        string path = BuildPath();
        foreach (string method in new[] { "OnKeyDown", "OnKeyUp" })
        {
            var instructions = Body(path, method).Instructions;
            // Execute the actual compiled branch with managed substitutes for the
            // two native boundary properties. No TMP or screen routing is simulated.
            Assert.IsTrue(EvaluateCapture(instructions, editing: true), method);
            Assert.IsFalse(EvaluateCapture(instructions, editing: false), method);
        }
    }

    private static bool EvaluateCapture(IReadOnlyList<AssemblyInstructionContract> instructions, bool editing)
    {
        var stack = new Stack<object>();
        bool consumed = false;
        for (int index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            switch (instruction.Operation)
            {
                case "ldarg.0": case "ldarg.1": stack.Push(new object()); break;
                case "ldc.i4.1": stack.Push(true); break;
                case "call": case "callvirt":
                    if (instruction.ResolvedOperand == "KScreen.get_isEditing")
                    {
                        stack.Pop(); stack.Push(editing);
                    }
                    else if (instruction.ResolvedOperand == "KInputEvent.set_Consumed")
                    {
                        consumed = (bool)stack.Pop(); stack.Pop();
                    }
                    else Assert.Fail("Unexpected native call in key capture: " + instruction);
                    break;
                case "brfalse": case "brfalse.s":
                    if (!(bool)stack.Pop())
                    {
                        int target = instructions[index + 1].Offset + Convert.ToInt32(instruction.Operand);
                        int targetIndex = instructions.ToList().FindIndex(item => item.Offset == target);
                        Assert.IsTrue(targetIndex >= 0, "Invalid branch target in capture method.");
                        index = targetIndex - 1;
                    }
                    break;
                case "ret": return consumed;
                case "nop": break;
                default: Assert.Fail("Unsupported capture instruction: " + instruction); break;
            }
        }
        Assert.Fail("Capture method did not return.");
        return false;
    }

    private static AssemblyMethodBodyContract Body(string path, string method) =>
        DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(path, ScreenType, method).Single();

    private static void AssertEditingAssignment(AssemblyMethodBodyContract body, string expectedValue)
    {
        int setter = body.Instructions.ToList().FindIndex(instruction =>
            instruction.ResolvedOperand == "KScreen.set_isEditing");
        Assert.IsTrue(setter > 0, body.FormatInstructions());
        Assert.AreEqual(expectedValue, body.Instructions[setter - 1].Operation, body.MethodName);
    }

    private static string BuildPath()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Assert.Inconclusive("Supply DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH.");
        return builds.Single().AssemblyPath;
    }
}
