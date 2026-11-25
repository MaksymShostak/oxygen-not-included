using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.HarmonyTranspilerInfrastructure;

/// <summary>Read each installed method with the shipped Harmony and apply the exact merged candidate's transpiler.</summary>
public sealed class InstalledKleiFetchPatchTests
{
    [Test]
    public async Task SweepChecksBothTagPathsBeforeSettingTheNativeContext()
    {
        using var game = new InstalledGame();
        var code = game.Rewrite("KleiDirectDeliveryEligibilityPatches", "ResolveClearableManager",
            method => method.Name.StartsWith("ClearableManager", StringComparison.Ordinal) && method.Name.EndsWith("Transpiler", StringComparison.Ordinal));
        int gate = code.FindIndex(op => op.Member == "IsPickupAllowedForFetchChore");
        int tag = code.FindIndex(op => op.Member == "HasTag");
        int set = code.FindIndex(op => op.Member == "Set");
        await Assert.That(gate > tag && gate < set).IsTrue();
        int tagAccepted = code.FindIndex(op => op.Code == OpCodes.Bne_Un_S || op.Code == OpCodes.Bne_Un);
        int failure = code.FindIndex(op => op.Member == "IsSuccess") + 1;
        await Assert.That(code[gate + 1].Code).IsEqualTo(OpCodes.Brfalse);
        await Assert.That(code[gate + 1].Operand).IsEqualTo(code[tag + 1].Operand);

        int join = code.FindIndex(op => op.Labels.Contains((Label)code[tagAccepted].Operand!));
        await Assert.That(join).IsEqualTo(tag + 2);
        await Assert.That(join < gate).IsTrue();
        int next = code.FindIndex(op => op.Labels.Contains((Label)code[gate + 1].Operand!));
        await Assert.That(code[next].Labels.Contains((Label)code[failure].Operand!)).IsTrue();
        await Assert.That(code[next + 1].Code == OpCodes.Ldc_I4_1 && code[next + 2].Code == OpCodes.Add).IsTrue();
    }

    [Test]
    [Arguments("missing")]
    [Arguments("duplicate")]
    [Arguments("redirected")]
    public async Task SweepPreflightRejectsAnUnprovedCandidateGate(string mutation)
    {
        using var game = new InstalledGame();
        var failure = await Assert.That(() => game.Rewrite("KleiDirectDeliveryEligibilityPatches",
            "ResolveClearableManager", method => method.Name.EndsWith("EvaluateFetchRangeTranspiler", StringComparison.Ordinal),
            il =>
            {
                var items = il.Cast<object>().ToList();
                var type = items[0].GetType();
                var operand = type.GetField("operand")!;
                int set = items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "Set");
                int hasTag = items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "HasTag");
                if (mutation == "missing") il.RemoveAt(set);
                else if (mutation == "duplicate") il.Add(items[set]);
                else operand.SetValue(items[hasTag + 1], operand.GetValue(items[hasTag - 5]));
            })).ThrowsExactly<TargetInvocationException>();
        await Assert.That(failure!.InnerException!.GetType().Name).IsEqualTo("HarmonyPatchContractViolationException");
    }

    [Test]
    public async Task EachReachableDestinationIsRecordedEvenWhenNativeTagWritesAreDeduplicated()
    {
        using var game = new InstalledGame();
        var code = game.Rewrite("KleiAuthoritativeFetchTemperatureEligibilityPatches", "ResolveGlobalChoreProviderUpdateStorageFetchableBits",
            method => method.Name == "UpdateStorageFetchableBitsTranspiler");
        // A and B share a single tag but have disjoint ranges. C collides with their hash
        // despite having two different tags. The native first-hash policy writes only A.
        var inputs = new[] { ("A", 1, true, true), ("B", 1, true, true), ("C", 2, true, true),
            ("unreachable", 1, false, true), ("wrong-type", 1, true, false) };
        foreach (bool active in new[] { false, true })
        {
            var recorded = new List<string>();
            var nativeWrites = new List<string>();
            int probes = 0;
            bool seen = false;
            foreach (var (name, tagCount, reachable, storage) in inputs)
            {
                var result = TraceChore(code, active, seen, tagCount, reachable, storage);
                if (result.Recorded) recorded.Add(name);
                if (result.NativeWrite) nativeWrites.Add(name);
                seen |= result.HashInserted;
                probes += result.Probes;
            }
            await Assert.That(string.Join(",", nativeWrites)).IsEqualTo("A");
            await Assert.That(string.Join(",", recorded)).IsEqualTo(active ? "A,B,C" : "");
            await Assert.That(probes).IsEqualTo(active ? 4 : 1);
        }
        var multiple = TraceChore(code, true, false, 2, true, true);
        await Assert.That(multiple.Recorded && multiple.NativeWrite && multiple.Probes == 1).IsTrue();
    }

    private static (bool Recorded, bool NativeWrite, bool HashInserted, int Probes) TraceChore(
        List<Instruction> code, bool active, bool seen, int tagCount, bool reachable, bool storage)
    {
        // Follow the emitted control flow for one iteration. Substitute measured fixture
        // inputs only at game boundaries. The rejection label denotes the next chore.
        int first = code.FindIndex(op => op.Member == "Contains") + 1;
        var reachBranch = code[code.FindIndex(op => op.Member == "IsReachable") + 1];
        int end = code.FindIndex(op => op.Labels.Contains((Label)reachBranch.Operand!));
        int activeStore = code.FindIndex(op => op.Member == "IsAuthoritativeFetchEligibilityBuildActive") + 1;
        var locals = new Dictionary<int, int> { [0] = 1, [1] = 2, [code[activeStore].LocalIndex] = active ? 1 : 0 };
        var stack = new Stack<int>();
        stack.Push(seen ? 1 : 0);
        int records = 0, writes = 0, hashInserts = 0, probes = 0;
        for (int i = first; i < end; i++)
        {
            var op = code[i];
            var instruction = op.Code;
            if (instruction == OpCodes.Dup) stack.Push(stack.Peek());
            else if (instruction == OpCodes.Pop) stack.Pop();
            else if (instruction == OpCodes.Ldarg_0) stack.Push(0);
            else if (instruction == OpCodes.Ldc_I4_1) stack.Push(1);
            else if (instruction.Name!.StartsWith("ldloc", StringComparison.Ordinal)) stack.Push(locals.GetValueOrDefault(op.LocalIndex));
            else if (instruction.Name.StartsWith("stloc", StringComparison.Ordinal)) locals[op.LocalIndex] = stack.Pop();
            else if (instruction == OpCodes.Ldfld) { stack.Pop(); stack.Push(0); }
            else if (op.Operand is MethodInfo call)
            {
                foreach (var _ in call.GetParameters()) stack.Pop();
                if (!call.IsStatic) stack.Pop();
                int value = 0;
                switch (call.Name)
                {
                    case "get_choreType": value = storage ? 1 : 9; break;
                    case "op_Implicit": value = 1; break;
                    case "IsReachable": probes++; value = reachable ? 1 : 0; break;
                    case "get_Count": value = tagCount; break;
                    case "RecordSelectedFetchChore": records++; break;
                    case "UnionWith": writes++; break;
                    case "Add" when call.DeclaringType!.GetGenericArguments()[0] == typeof(int): hashInserts++; break;
                    case "Add" when call.DeclaringType!.GetGenericArguments()[0].Name == "Tag": writes++; break;
                }
                if (call.ReturnType != typeof(void)) stack.Push(value);
            }
            else if (op.Operand is Label label)
            {
                bool take = instruction.Name.Split('.')[0] switch
                {
                    "br" => true, "brtrue" => stack.Pop() != 0, "brfalse" => stack.Pop() == 0,
                    "beq" => stack.Pop() == stack.Pop(), "bne" => stack.Pop() != stack.Pop(),
                    _ => throw new InvalidDataException(instruction.Name)
                };
                if (take)
                {
                    int target = code.FindIndex(item => item.Labels.Contains(label));
                    if (target <= i || target > end) throw new InvalidDataException("Unexpected iteration branch");
                    i = target - 1;
                }
            }
            else throw new InvalidDataException("Unexpected installed instruction " + instruction.Name);
        }
        if (stack.Count != 0 || records > 1 || writes > 1 || hashInserts > 1) throw new InvalidDataException("Unbalanced or duplicated chore operation");
        return (records == 1, writes == 1, hashInserts == 1, probes);
    }

    private sealed record Instruction(OpCode Code, object? Operand, List<Label> Labels)
    {
        internal string? Member => (Operand as MemberInfo)?.Name;
        internal int LocalIndex => Code.Name!.Last() switch
        {
            '0' => 0, '1' => 1, '2' => 2, '3' => 3,
            _ => Operand is LocalVariableInfo local ? local.LocalIndex : Convert.ToInt32(Operand)
        };
    }

    private sealed class InstalledGame : AssemblyLoadContext, IDisposable
    {
        private readonly string managed = Environment.GetEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY")!;
        private readonly Assembly candidate;
        internal InstalledGame() : base("InstalledKleiFetchPatches", isCollectible: true)
        {
            var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment().ProbeExactPipelineBuildDataRows();
            if (builds.Count == 0) Skip.Test("The installed adapter contract needs the pipeline build result.");
            candidate = LoadFromAssemblyPath(builds.Single().AssemblyPath);
        }

        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name is null or "mscorlib" or "netstandard" or "System" || name.Name.StartsWith("System.", StringComparison.Ordinal))
                return null;
            var path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }

        internal List<Instruction> Rewrite(string adapter, string resolverPrefix, Func<MethodInfo, bool> match, Action<IList>? mutate = null)
        {
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly;
            var members = candidate.GetType("DeliveryTemperatureLimit." + adapter, true)!.GetMethods(declared);
            var resolver = members.Single(m => m.Name.StartsWith(resolverPrefix, StringComparison.Ordinal) && m.Name.EndsWith("Target", StringComparison.Ordinal));
            var original = (MethodInfo)resolver.Invoke(null, null)!;
            var reader = LoadFromAssemblyName(new("0Harmony")).GetType("HarmonyLib.PatchProcessor", true)!
                .GetMethods().Single(m => m.Name == "GetOriginalInstructions" && m.GetParameters()[1].ParameterType == typeof(ILGenerator));
            var generator = new DynamicMethod("ReadInstalledChore", typeof(void), Type.EmptyTypes).GetILGenerator();
            var il = reader.Invoke(null, [original, generator]);
            mutate?.Invoke((IList)il!);
            var patch = members.Single(match);
            var result = (IEnumerable)patch.Invoke(null, [il, patch.GetParameters()[1].ParameterType == typeof(ILGenerator) ? generator : original])!;
            return result.Cast<object>().Select(instruction =>
            {
                var type = instruction.GetType();
                return new Instruction((OpCode)type.GetField("opcode")!.GetValue(instruction)!,
                    type.GetField("operand")!.GetValue(instruction),
                    (List<Label>)type.GetField("labels")!.GetValue(instruction)!);
            }).ToList();
        }

        public void Dispose() => Unload();
    }
}
