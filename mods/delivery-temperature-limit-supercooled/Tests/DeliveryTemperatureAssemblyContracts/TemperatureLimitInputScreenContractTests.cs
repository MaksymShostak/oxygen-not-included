using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class TemperatureLimitInputScreenContractTests
{
    private const string ScreenType = "DeliveryTemperatureLimit.TemperatureLimitInputScreen";

    [Test]
    public async Task BothTemperatureFields_RegisterAnInputScreenWithTheGame()
    {
        string path = BuildPath();
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        TypeDefinitionHandle screen = metadata.TypeDefinitions.SingleOrDefault(handle =>
            metadata.GetString(metadata.GetTypeDefinition(handle).Name) == "TemperatureLimitInputScreen");
        await Assert.That(screen.IsNil).IsFalse().Because("Each TMP field needs an active input owner; the parent side screen does not receive keyboard events.");
        TypeDefinition definition = metadata.GetTypeDefinition(screen);
        await Assert.That(metadata.GetString(metadata.GetTypeReference(
            (TypeReferenceHandle)definition.BaseType).Name)).IsEqualTo("KScreen");

        var constructor = Body(path, ".ctor");
        int activation = constructor.Instructions.ToList().FindIndex(instruction =>
            instruction.ResolvedOperand == "KScreen.activateOnSpawn");
        await Assert.That(activation > 0).IsTrue().Because("The input owner must register on spawn.");
        await Assert.That(constructor.Instructions[activation - 1].Operation).IsEqualTo("ldc.i4.1");

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
        await Assert.That(attachedInputs).IsEqualTo(2).Because("Both lower and upper fields need a registered input owner.");
    }

    [Test]
    public async Task InputOwner_WiresFocusAndEndEditAndUnsubscribesOnCleanup()
    {
        string path = BuildPath();
        string spawn = Body(path, "OnSpawn").FormatInstructions();
        await Assert.That(spawn).Contains("KScreen.OnSpawn");
        await Assert.That(spawn).Contains("TMPro.TMP_InputField.onFocus");
        await Assert.That(spawn).Contains("TemperatureLimitInputScreen.OnInputFocus");
        await Assert.That(spawn).Contains("TemperatureLimitInputScreen.OnInputEndEdit");
        await Assert.That(spawn).Contains("AddListener");
        string cleanup = Body(path, "OnCleanUp").FormatInstructions();
        await Assert.That(cleanup).Contains("System.Delegate.Remove");
        await Assert.That(cleanup).Contains("RemoveListener");
        await Assert.That(cleanup).Contains("TemperatureLimitInputScreen.ReleaseInputCapture");
    }

    [Test]
    public async Task InputOwner_DisablingOrRefocusingCancelsPendingRelease()
    {
        string path = BuildPath();
        await Assert.That(Body(path, "OnDisable").FormatInstructions()).Contains("TemperatureLimitInputScreen.ReleaseInputCapture");
        await Assert.That(Body(path, "OnInputFocus").FormatInstructions()).Contains("TemperatureLimitInputScreen.CancelPendingRelease");
        await Assert.That(Body(path, "ReleaseInputCapture").FormatInstructions()).Contains("TemperatureLimitInputScreen.CancelPendingRelease");
        await Assert.That(Body(path, "CancelPendingRelease").FormatInstructions()).Contains("UnityEngine.MonoBehaviour.StopCoroutine");
        await AssertEditingAssignment(Body(path, "OnInputFocus"), "ldc.i4.1");
        await AssertEditingAssignment(Body(path, "ReleaseInputCapture"), "ldc.i4.0");
    }

    [Test]
    public async Task InputOwner_EndEditDefersReleaseUntilEndOfFrame()
    {
        string path = BuildPath();
        string endEdit = Body(path, "OnInputEndEdit").FormatInstructions();
        await Assert.That(endEdit).Contains("UnityEngine.MonoBehaviour.StartCoroutine");
        await Assert.That(endEdit).Contains("TemperatureLimitInputScreen.ReleaseAfterEditFrame");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        string iteratorName = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Select(type => metadata.GetString(type.Name))
            .Single(name => name.StartsWith("<ReleaseAfterEditFrame>", StringComparison.Ordinal));
        var moveNext = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
            path, ScreenType + "+" + iteratorName, "MoveNext").Single();
        await Assert.That(moveNext.FormatInstructions()).Contains("UnityEngine.WaitForEndOfFrame..ctor");
        await AssertEditingAssignment(moveNext, "ldc.i4.0");
    }

    [Test]
    public async Task InputOwner_KeyDownAndKeyUpConsumeOnlyDuringEditing()
    {
        string path = BuildPath();
        foreach (string method in new[] { "OnKeyDown", "OnKeyUp" })
        {
            var instructions = Body(path, method).Instructions;
            // Execute the actual compiled branch with managed substitutes for the
            // two native boundary properties. No TMP or screen routing is simulated.
            await Assert.That((await EvaluateCapture(instructions, editing: true))).IsTrue().Because(method);
            await Assert.That((await EvaluateCapture(instructions, editing: false))).IsFalse().Because(method);
            await Assert.That((await EvaluateCapture(instructions, editing: true, enabled: false))).IsFalse().Because(method);
        }
    }

    private static async Task<bool> EvaluateCapture(IReadOnlyList<AssemblyInstructionContract> instructions, bool editing, bool enabled = true)
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
                    if (instruction.ResolvedOperand == "DeliveryTemperatureLimit.RuntimeFailureReporting.get_IsUserInterfaceEnabled")
                        stack.Push(enabled);
                    else if (instruction.ResolvedOperand == "KScreen.get_isEditing")
                    {
                        stack.Pop(); stack.Push(editing);
                    }
                    else if (instruction.ResolvedOperand == "KInputEvent.set_Consumed")
                    {
                        consumed = (bool)stack.Pop(); stack.Pop();
                    }
                    else Assert.Fail("Unexpected native call in key capture: " + instruction);
                    break;
                case "brfalse": case "brfalse.s": case "brtrue": case "brtrue.s":
                    bool condition = (bool)stack.Pop();
                    if (condition == instruction.Operation.StartsWith("brtrue", StringComparison.Ordinal))
                    {
                        int target = instructions[index + 1].Offset + Convert.ToInt32(instruction.Operand);
                        int targetIndex = instructions.ToList().FindIndex(item => item.Offset == target);
                        await Assert.That(targetIndex >= 0).IsTrue().Because("Invalid branch target in capture method.");
                        index = targetIndex - 1;
                    }
                    break;
                case "ret": return consumed;
                case "leave": case "leave.s":
                    int destination = instructions[index + 1].Offset + Convert.ToInt32(instruction.Operand);
                    index = instructions.ToList().FindIndex(item => item.Offset == destination) - 1;
                    await Assert.That(index >= 0).IsTrue();
                    stack.Clear();
                    break;
                case "nop": break;
                default: Assert.Fail("Unsupported capture instruction: " + instruction); break;
            }
        }
        Assert.Fail("Capture method did not return.");
        return false;
    }

    private static AssemblyMethodBodyContract Body(string path, string method) =>
        DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(path, ScreenType, method).Single();

    private static async Task AssertEditingAssignment(AssemblyMethodBodyContract body, string expectedValue)
    {
        int setter = body.Instructions.ToList().FindIndex(instruction =>
            instruction.ResolvedOperand == "KScreen.set_isEditing");
        await Assert.That(setter > 0).IsTrue().Because(body.FormatInstructions());
        await Assert.That(body.Instructions[setter - 1].Operation).IsEqualTo(expectedValue).Because(body.MethodName);
    }

    private static string BuildPath()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Skip.Test("Supply DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH.");
        return builds.Single().AssemblyPath;
    }
}
