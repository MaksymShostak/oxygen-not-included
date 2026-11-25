using System.Reflection;
using System.Reflection.Emit;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

internal static class CopySettingsEventContract
{
    private const string Component = "DeliveryTemperatureLimit.TemperatureLimit";

    internal static async Task Verify(string path)
    {
        using var assemblies = new InstalledModAssemblyContext(path);
        var module = assemblies.Candidate.ManifestModule;
        var init = Body(path, "OnPrefabInit");
        int index = Array.FindIndex(init, op => op.ResolvedOperand == "KMonoBehaviour.Subscribe");
        await Assert.That(index >= 4).IsTrue();
        var subscribe = module.ResolveMethod((int)init[index].Operand!)!;
        await Assert.That(subscribe.GetParameters().Select(p => p.ParameterType).SequenceEqual(
            new[] { typeof(int), typeof(Action<object, object>), typeof(object) })).IsTrue();
        var game = assemblies.LoadFromAssemblyName(new("Assembly-CSharp"));
        var copyHash = game.GetType("GameHashes", true)!.GetField("CopySettings")!.GetRawConstantValue();
        await Assert.That(init[index - 4].Operation).IsEqualTo("ldarg.0");
        await Assert.That(init[index - 3].Operand).IsEqualTo(copyHash);
        await Assert.That(init[index - 2].ResolvedOperand).IsEqualTo(Component + ".CopySettingsHandler");
        await Assert.That(init[index - 1].Operation).IsEqualTo("ldarg.0");
        var binding = Body(path, ".cctor");
        int handler = Array.FindIndex(binding, op => op.ResolvedOperand == Component + ".CopySettingsHandler");
        await Assert.That(handler >= 2 && binding[handler - 2].Operation == "ldftn").IsTrue();
        string callback = binding[handler - 2].ResolvedOperand![(Component.Length + 1)..];
        var copy = Project(path, "CopySettings", [typeof(Bin), typeof(Bin)], null);
        var dispatch = Project(path, callback, [typeof(object), typeof(object)], copy).CreateDelegate<Action<object, object>>();

        var source = new Bin(100, 250);
        var receiver = new Bin(400, 500);
        dispatch(receiver, source.Owner);
        await Assert.That((receiver.Low, receiver.High, source.Low, source.High)).IsEqualTo((100, 250, 100, 250));
        dispatch(receiver, source.Owner);
        await Assert.That(receiver.Changes).IsEqualTo(1);
        var disabled = new Bin(0, 0);
        dispatch(receiver, disabled.Owner);
        await Assert.That((receiver.Low, receiver.High)).IsEqualTo((0, 0));
        source.Owner.Current = null;
        dispatch(receiver, source.Owner);
        await Assert.That(receiver.Changes).IsEqualTo(2);
        var newer = new Bin(600, 700);
        receiver.Owner.Current = newer;
        dispatch(receiver, newer.Owner);
        await Assert.That((receiver.Low, receiver.High, newer.Low, newer.High)).IsEqualTo((0, 0, 600, 700));
    }

    private static AssemblyInstructionContract[] Body(string path, string method) =>
        DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(path, Component, method).Single().Instructions.ToArray();

    // Preserve the compiled callback and CopySettings control flow, arguments and field loads.
    // Only Unity object lifetime, session lookup and the separately tested normalized setter use stand-ins.
    private static DynamicMethod Project(string path, string method, Type[] arguments, DynamicMethod? copy)
    {
        var result = new DynamicMethod(method, typeof(void), arguments, typeof(CopySettingsEventContract), true);
        var il = result.GetILGenerator();
        il.DeclareLocal(typeof(Bin));
        var code = Body(path, method);
        var labels = code.ToDictionary(op => op.Offset, _ => il.DefineLabel());
        var opcodes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(op => op.Name!);
        for (int i = 0; i < code.Length; i++)
        {
            var instruction = code[i];
            il.MarkLabel(labels[instruction.Offset]);
            var op = opcodes[instruction.Operation];
            if (op.OperandType == OperandType.InlineNone) il.Emit(op);
            else if (op.OperandType is OperandType.ShortInlineBrTarget or OperandType.InlineBrTarget)
                il.Emit(op, labels[code[i + 1].Offset + Convert.ToInt32(instruction.Operand)]);
            else if (op == OpCodes.Isinst)
                il.Emit(op, instruction.ResolvedOperand switch
                {
                    Component => typeof(Bin), "UnityEngine.GameObject" => typeof(Obj),
                    _ => throw new InvalidDataException(instruction.ToString())
                });
            else if (op == OpCodes.Ldfld)
                il.Emit(op, typeof(Bin).GetField(instruction.ResolvedOperand switch
                {
                    Component + ".lowLimit" => nameof(Bin.Low), Component + ".highLimit" => nameof(Bin.High),
                    _ => throw new InvalidDataException(instruction.ToString())
                })!);
            else if (op == OpCodes.Call || op == OpCodes.Callvirt)
                il.Emit(OpCodes.Call, instruction.ResolvedOperand switch
                {
                    "UnityEngine.Object.op_Equality" => typeof(Bin).GetMethod(nameof(Bin.Equal))!,
                    "UnityEngine.Object.op_Inequality" => typeof(Bin).GetMethod(nameof(Bin.Unequal))!,
                    "UnityEngine.Component.get_gameObject" => typeof(Bin).GetProperty(nameof(Bin.Owner))!.GetMethod!,
                    Component + ".Get" => typeof(Obj).GetMethod(nameof(Obj.Lookup))!,
                    Component + ".CopySettings" => copy!,
                    Component + ".ApplySerializedLimits" => typeof(Bin).GetMethod(nameof(Bin.Apply))!,
                    _ => throw new InvalidDataException(instruction.ToString())
                });
            else throw new InvalidDataException(instruction.ToString());
        }
        return result;
    }

    private sealed class Obj
    {
        public Bin? Current;
        public static Bin? Lookup(Obj? obj) => obj?.Current;
    }

    private sealed class Bin
    {
        public int Low, High, Changes;
        public Obj Owner { get; } = new();
        public Bin(int low, int high) { Owner.Current = this; Low = low; High = high; }
        public static bool Equal(Bin? a, Bin? b) => ReferenceEquals(a, b);
        public static bool Unequal(Bin? a, Bin? b) => !Equal(a, b);
        public void Apply(int low, int high)
        {
            if ((low, high) == (Low, High)) return;
            Low = low;
            High = high;
            Changes++;
        }
    }
}
