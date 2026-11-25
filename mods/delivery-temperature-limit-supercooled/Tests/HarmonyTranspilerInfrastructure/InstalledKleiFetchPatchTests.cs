using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
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
    public async Task SweepSelectsTheFirstEligibleNativeCandidateForEachPickup()
    {
        using var game = new InstalledGame();
        var code = game.Rewrite("KleiDirectDeliveryEligibilityPatches", "ResolveClearableManager",
            method => method.Name.EndsWith("EvaluateFetchRangeTranspiler", StringComparison.Ordinal));
        var pair = CompileSweepPair(code);
        int visited = 0;
        string Pick(int kelvin, bool limits, bool exact, bool tag, bool nativeAcceptsHot)
        {
            visited = 0;
            foreach (var (name, low, high, native) in new[] { ("hot", 400, 501, nativeAcceptsHot), ("cold", 100, 201, true) })
                if (pair(exact ? 1 : 0, tag, !limits || low <= kelvin && kelvin < high, native, () => visited++))
                    return name;
            return "";
        }
        await Assert.That(Pick(150, true, true, true, true)).IsEqualTo("cold");
        await Assert.That(visited).IsEqualTo(1);
        await Assert.That(Pick(450, true, false, false, true)).IsEqualTo("hot");
        await Assert.That(Pick(450, true, true, true, false)).IsEqualTo("");
        await Assert.That(Pick(300, true, true, true, true)).IsEqualTo("");
        await Assert.That(visited).IsEqualTo(0);
        await Assert.That(Pick(150, true, true, false, true)).IsEqualTo("");
        await Assert.That(Pick(150, false, true, true, false)).IsEqualTo("cold");
        await Assert.That(visited).IsEqualTo(2);
        int success = code.FindIndex(op => op.Code == OpCodes.Ret) - 1;
        code[success] = code[success] with { Code = OpCodes.Ldc_I4_0 };
        await Assert.That(CompileSweepPair(code)(1, true, true, true, () => { })).IsFalse();
    }

    private delegate bool SweepPair(int criteria, bool tag, bool temperatureAllowed, bool preconditions, Action run);
    private static SweepPair CompileSweepPair(List<Instruction> code)
    {
        // Execute the emitted branch instructions unchanged. The fixture supplies the
        // Unity-dependent predicate results and counts calls at Klei's native precondition boundary.
        var method = new DynamicMethod("SweepCandidateControlFlow", typeof(bool),
            [typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(Action)]);
        var il = method.GetILGenerator();
        int first = code.FindIndex(op => op.Code == OpCodes.Bne_Un_S || op.Code == OpCodes.Bne_Un);
        int failed = code.FindIndex(op => op.Member == "IsSuccess") + 1;
        int next = code.FindIndex(op => op.Labels.Contains((Label)code[failed].Operand!));
        var labels = Enumerable.Range(0, next + 1).Select(_ => il.DefineLabel()).ToArray();
        for (int i = first; i <= next; i++)
        {
            var op = code[i];
            il.MarkLabel(labels[i]);
            if (i == first) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_1); }
            if (op.Member == "HasTag") il.Emit(OpCodes.Ldarg_1);
            else if (op.Member == "IsPickupAllowedForFetchChore") il.Emit(OpCodes.Ldarg_2);
            else if (op.Member == "IsSuccess") il.Emit(OpCodes.Ldarg_3);
            else if (op.Member == "RunPreconditions")
            {
                il.Emit(OpCodes.Ldarg_S, (byte)4);
                il.Emit(OpCodes.Callvirt, typeof(Action).GetMethod(nameof(Action.Invoke))!);
            }
            else if (op.Operand is Label target && op.Code.FlowControl == FlowControl.Cond_Branch)
                il.Emit(op.Code, labels[code.FindIndex(item => item.Labels.Contains(target))]);
            else if (op.Code == OpCodes.Ret)
            {
                if (code[i - 1].Code != OpCodes.Ldc_I4_0 && code[i - 1].Code != OpCodes.Ldc_I4_1)
                    throw new InvalidDataException("Unexpected native sweep result");
                il.Emit(code[i - 1].Code);
                il.Emit(OpCodes.Ret);
            }
            else if (i == next) { il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret); }
        }
        return method.CreateDelegate<SweepPair>();
    }

    [Test]
    [Arguments("missing")]
    [Arguments("duplicate")]
    [Arguments("redirected")]
    [Arguments("interior")]
    [Arguments("loop-exit")]
    [Arguments("false-exhaustion")]
    public async Task SweepPreflightRejectsAnUnprovedCandidateGate(string mutation)
    {
        using var game = new InstalledGame();
        var failure = await Assert.That(() => game.Rewrite("KleiDirectDeliveryEligibilityPatches",
            "ResolveClearableManager", method => method.Name.EndsWith("EvaluateFetchRangeTranspiler", StringComparison.Ordinal),
            (il, generator) =>
            {
                var items = il.Cast<object>().ToList();
                var type = items[0].GetType();
                var operand = type.GetField("operand")!;
                int set = items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "Set");
                int hasTag = items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "HasTag");
                if (mutation == "missing") il.RemoveAt(set);
                else if (mutation == "duplicate") il.Add(items[set]);
                else if (mutation == "redirected") operand.SetValue(items[hasTag + 1], operand.GetValue(items[hasTag - 5]));
                else if (mutation == "interior")
                {
                    var label = generator.DefineLabel();
                    ((List<Label>)type.GetField("labels")!.GetValue(items[set - 6])!).Add(label);
                    il.Add(Activator.CreateInstance(type, OpCodes.Br, label));
                }
                else if (mutation == "false-exhaustion") type.GetField("opcode")!.SetValue(items[^2], OpCodes.Ldc_I4_1);
                else
                {
                    var rangeCount = items.Single(item => (operand.GetValue(item) as FieldInfo)?.Name == "count");
                    type.GetField("opcode")!.SetValue(rangeCount, OpCodes.Pop);
                }
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
        var deadDestination = TraceChore(code, true, true, 1, true, true, destinationExists: false);
        await Assert.That(deadDestination).IsEqualTo((false, false, false, 0));
        var multiple = TraceChore(code, true, false, 2, true, true);
        await Assert.That(multiple.Recorded && multiple.NativeWrite && multiple.Probes == 1).IsTrue();
    }

    [Test]
    public async Task ReachableChoresPublishPerWorldIntervalsAndPreserveInstalledNativeTagContents()
    {
        using var game = new InstalledGame();
        List<Instruction> Read(bool patch) => game.Rewrite("KleiAuthoritativeFetchTemperatureEligibilityPatches",
            "ResolveGlobalChoreProviderUpdateStorageFetchableBits", m => m.Name == "UpdateStorageFetchableBitsTranspiler",
            applyTranspiler: patch);
        var installed = Read(false);
        var patched = Read(true);
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var salt = new Tag("Salt");
        var entries = new (int World, int Hash, Tag[] Tags, int Min, int Max, bool Reachable, bool Food)[]
        {
            (1, 10, [iron], 10, 20, true, false), (1, 10, [iron], 40, 50, true, false),
            (1, 10, [copper, salt], 0, 0, true, true), (1, 10, [iron], 80, 90, false, false),
            (2, 10, [iron], 70, 80, true, true), (2, 20, [copper, salt], 5, 15, true, false)
        };
        var builder = new FetchTemperatureEligibilityBuilder();
        var session = new GameSessionGeneration(1);
        void Begin() => builder.Begin(session, new ActiveTemperatureConstraintSnapshot(new TemperatureConstraintGeneration(2),
                4, 4, Array.AsReadOnly(new[] { 5, 10, 15, 20, 40, 50, 70, 80 })), new FetchRequestTopologyVersion(3),
            new WorldParentTopologySnapshot(session, new WorldParentTopologyVersion(4), new Dictionary<int, int>()));
        Begin();
        HashSet<Tag> Traverse(List<Instruction> code, bool active)
        {
            var seen = new HashSet<int>();
            var native = new HashSet<Tag>();
            int worldHook = code.FindIndex(op => op.Member == "BeginParentWorldFetchMapSection");
            int world = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (active && entry.World != world)
                {
                    // Follow the active branch with the actual sorted-world getter result on the stack.
                    if (worldHook < 4 || code[worldHook - 4].Member != "get_Item" ||
                        !code[worldHook - 3].Code.Name!.StartsWith("ldloc", StringComparison.Ordinal) ||
                        !code[worldHook - 2].Code.Name!.StartsWith("brfalse", StringComparison.Ordinal) ||
                        code[worldHook - 1].Code != OpCodes.Dup)
                        throw new InvalidDataException("The parent-world hook lost the installed getter result");
                    world = entry.World;
                }
                var result = TraceChore(code, active, seen.Contains(entry.Hash), entry.Tags.Length, entry.Reachable,
                    storage: true, food: entry.Food, choreId: 100 + i,
                    record: id =>
                    {
                        var chore = entries[id - 100];
                        if (chore.Max == 0) builder.AddUnconstrainedFetchRequest(world, chore.Tags);
                        else builder.AddTemperatureConstrainedFetchRequest(world, chore.Tags,
                            DeliveryTemperatureConstraint.FromSerializedLimits(chore.Min, chore.Max));
                    },
                    writeTags: single => native.UnionWith(single ? entry.Tags.Take(1) : entry.Tags));
                if (result.HashInserted) seen.Add(entry.Hash);
            }
            return native;
        }
        var stockTags = Traverse(installed, false);
        var actual = Traverse(patched, true);
        await Assert.That(actual.SetEquals(stockTags) && stockTags.SetEquals([iron, copper, salt])).IsTrue();
        var snapshot = builder.Build();
        await Assert.That(snapshot.TryGetStorageEligibility(1, iron, out var one)).IsTrue();
        await Assert.That(one.Intervals.ToArray()).IsEquivalentTo(new[] { new AllowedTemperatureInterval(10, 20),
            new AllowedTemperatureInterval(40, 50) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(snapshot.TryGetStorageEligibility(2, iron, out var two)).IsTrue();
        await Assert.That(two.Intervals.Single()).IsEqualTo(new AllowedTemperatureInterval(70, 80));
        foreach (var tag in new[] { copper, salt })
        {
            await Assert.That(snapshot.TryGetStorageEligibility(1, tag, out var unbounded) && unbounded.AllowsEveryTemperature).IsTrue();
            await Assert.That(snapshot.TryGetStorageEligibility(2, tag, out var constrained) &&
                constrained.Intervals.Single().Equals(new AllowedTemperatureInterval(5, 15))).IsTrue();
        }
        Begin();
        builder.AddUnconstrainedFetchRequest(2, [iron]);
        builder.Discard();
        Begin();
        await Assert.That(builder.Build().TryGetStorageEligibility(2, iron, out _)).IsFalse();
    }

    [Test]
    public async Task InstalledFetchFinalizerDiscardsInterruptedAndDetachedBuildsAndRecovers()
    {
        using var game = new InstalledModAssemblyContext();
        const BindingFlags members = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags properties = BindingFlags.Instance | BindingFlags.NonPublic;
        Type Mod(string name) => game.Candidate.GetType("DeliveryTemperatureLimit." + name, true)!;
        var host = Mod("DeliveryTemperatureGameSessionHost");
        var hooks = Mod("KleiAuthoritativeFetchTemperatureEligibilityPatches");
        object? Call(Type type, string name, params object?[] args) => type.GetMethod(name, members)!.Invoke(null, args);
        object Start(int id)
        {
            var session = Call(host, "EnsureGameSession", id)!;
            var registry = session.GetType().GetProperty("TemperatureConstraints", properties)!.GetValue(session)!;
            var limit = Call(Mod("DeliveryTemperatureConstraint"), "FromSerializedLimits", 10, 20);
            registry.GetType().GetMethod("Register", properties)!.Invoke(registry, [42, limit, false]);
            return session;
        }
        object Begin()
        {
            object?[] args = [null];
            Call(hooks, "UpdateStorageFetchableBitsPrefix", args);
            return args[0]!;
        }
        object? Published(object session) => session.GetType().GetProperty("CurrentFetchTemperatureEligibility", properties)!.GetValue(session);
        void Complete(object state)
        {
            Call(hooks, "UpdateStorageFetchableBitsPostfix", state);
            Call(hooks, "UpdateStorageFetchableBitsFinalizer", null, state);
        }
        var first = Start(1);
        Complete(Begin());
        var initial = Published(first);
        var interrupted = Begin();
        var error = new InvalidOperationException("simulated native interruption");
        var returned = Call(hooks, "UpdateStorageFetchableBitsFinalizer", error, interrupted);
        var afterInterruption = Published(first);
        var stale = Begin();
        var detached = Call(host, "DetachGameSession", 1);
        var second = Start(2);
        Complete(stale);
        var afterStale = Published(second);
        Call(host, "CompleteShutdown", detached);
        Complete(Begin());
        var afterRecovery = Published(second);
        Call(host, "CompleteShutdown", Call(host, "DetachGameSession", 2));

        await Assert.That(initial is not null && ReferenceEquals(initial, afterInterruption)).IsTrue();
        await Assert.That(ReferenceEquals(error, returned)).IsTrue();
        await Assert.That(afterStale).IsNull();
        await Assert.That(afterRecovery is not null && !ReferenceEquals(initial, afterRecovery)).IsTrue();
    }

    [Test]
    [Arguments("seen")]
    [Arguments("reach")]
    [Arguments("native-write")]
    [Arguments("probe-cell")]
    [Arguments("offset-cell")]
    [Arguments("food-type")]
    [Arguments("bypass-recording")]
    public async Task FetchPreflightRejectsBrokenNativeDestinationPaths(string mutation)
    {
        using var game = new InstalledGame();
        var failure = await Assert.That(() => game.Rewrite("KleiAuthoritativeFetchTemperatureEligibilityPatches",
            "ResolveGlobalChoreProviderUpdateStorageFetchableBits", method => method.Name == "UpdateStorageFetchableBitsTranspiler",
            (il, generator) =>
            {
                var items = il.Cast<object>().ToList();
                var type = items[0].GetType();
                var opcode = type.GetField("opcode")!;
                var operand = type.GetField("operand")!;
                int Call(string name) => items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == name);
                if (mutation is "probe-cell" or "offset-cell")
                {
                    int load = Call("GetOffsets") - (mutation == "probe-cell" ? 4 : 1);
                    var loopIndex = items[items.FindLastIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "Add") + 2];
                    opcode.SetValue(items[load], opcode.GetValue(loopIndex));
                    operand.SetValue(items[load], operand.GetValue(loopIndex));
                    return;
                }
                if (mutation == "food-type")
                {
                    opcode.SetValue(items[Call("Contains") + 8], OpCodes.Ldloc_0);
                    return;
                }
                if (mutation == "bypass-recording")
                {
                    var label = generator.DefineLabel();
                    ((List<Label>)type.GetField("labels")!.GetValue(items[Call("UnionWith") - 4])!).Add(label);
                    int entry = Call("Contains") - 3;
                    il.Insert(entry, Activator.CreateInstance(type, OpCodes.Ldc_I4_0, null));
                    il.Insert(entry + 1, Activator.CreateInstance(type, OpCodes.Brtrue, label));
                    return;
                }
                int site = mutation switch
                {
                    "seen" => items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "Contains") + 1,
                    "reach" => items.FindIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "IsReachable") + 1,
                    _ => items.FindLastIndex(item => (operand.GetValue(item) as MethodInfo)?.Name == "Add")
                };
                opcode.SetValue(items[site], OpCodes.Nop);
            })).ThrowsExactly<TargetInvocationException>();
        await Assert.That(failure!.InnerException!.GetType().Name).IsEqualTo("HarmonyPatchContractViolationException");
    }

    private static (bool Recorded, bool NativeWrite, bool HashInserted, int Probes) TraceChore(
        List<Instruction> code, bool active, bool seen, int tagCount, bool reachable, bool storage, bool destinationExists = true,
        bool food = false, int choreId = 80, Action<int>? record = null, Action<bool>? writeTags = null)
    {
        // Follow the emitted control flow for one iteration. Substitute measured fixture
        // inputs only at game boundaries. The rejection label denotes the next chore.
        int first = code.FindIndex(op => op.Member == "Contains") + 1;
        var reachBranch = code[code.FindIndex(op => op.Member == "IsReachable") + 1];
        int end = code.FindIndex(op => op.Labels.Contains((Label)reachBranch.Operand!));
        int activeStore = code.FindIndex(op => op.Member == "IsAuthoritativeFetchEligibilityBuildActive") + 1;
        var locals = new Dictionary<int, int> { [0] = 1, [1] = 2, [code[first - 3].LocalIndex] = choreId };
        if (activeStore > 0) locals[code[activeStore].LocalIndex] = active ? 1 : 0;
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
            else if (instruction == OpCodes.Ldfld)
            {
                int instance = stack.Pop();
                if (op.Member is "tags" or "tagsFirst" or "tagsHash" && instance != choreId)
                    throw new InvalidDataException("The native tag write read a different chore");
                stack.Push(op.Member switch { "tags" => -1, "tagsFirst" => -2, _ => 0 });
            }
            else if (op.Operand is MethodInfo call)
            {
                int argument = stack.Count == 0 ? 0 : stack.Peek();
                foreach (var _ in call.GetParameters()) stack.Pop();
                if (!call.IsStatic) stack.Pop();
                int value = 0;
                switch (call.Name)
                {
                    case "get_choreType": value = food ? 2 : storage ? 1 : 9; break;
                    case "op_Implicit": value = destinationExists ? 1 : 0; break;
                    case "IsReachable": probes++; value = reachable ? 1 : 0; break;
                    case "get_Count": value = tagCount; break;
                    case "RecordSelectedFetchChore": records++; record?.Invoke(argument); break;
                    case "UnionWith" when argument == -1: writes++; writeTags?.Invoke(false); break;
                    case "Add" when call.DeclaringType!.GetGenericArguments()[0] == typeof(int): hashInserts++; break;
                    case "Add" when call.DeclaringType!.GetGenericArguments()[0].Name == "Tag" && argument == -2: writes++; writeTags?.Invoke(true); break;
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

    private sealed class InstalledGame : IDisposable
    {
        private readonly InstalledModAssemblyContext assemblies = new();

        internal List<Instruction> Rewrite(string adapter, string resolverPrefix, Func<MethodInfo, bool> match, Action<IList, ILGenerator>? mutate = null, bool applyTranspiler = true)
        {
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly;
            var members = assemblies.Candidate.GetType("DeliveryTemperatureLimit." + adapter, true)!.GetMethods(declared);
            var resolver = members.Single(m => m.Name.StartsWith(resolverPrefix, StringComparison.Ordinal) && m.Name.EndsWith("Target", StringComparison.Ordinal));
            var original = (MethodInfo)resolver.Invoke(null, null)!;
            var reader = assemblies.LoadFromAssemblyName(new("0Harmony")).GetType("HarmonyLib.PatchProcessor", true)!
                .GetMethods().Single(m => m.Name == "GetOriginalInstructions" && m.GetParameters()[1].ParameterType == typeof(ILGenerator));
            var generator = new DynamicMethod("ReadInstalledChore", typeof(void), Type.EmptyTypes).GetILGenerator();
            var il = reader.Invoke(null, [original, generator]);
            mutate?.Invoke((IList)il!, generator);
            var patch = members.Single(match);
            var result = applyTranspiler ? (IEnumerable)patch.Invoke(null,
                [il, patch.GetParameters()[1].ParameterType == typeof(ILGenerator) ? generator : original])! : (IEnumerable)il!;
            return result.Cast<object>().Select(instruction =>
            {
                var type = instruction.GetType();
                return new Instruction((OpCode)type.GetField("opcode")!.GetValue(instruction)!,
                    type.GetField("operand")!.GetValue(instruction),
                    (List<Label>)type.GetField("labels")!.GetValue(instruction)!);
            }).ToList();
        }

        public void Dispose() => assemblies.Dispose();
    }
}
