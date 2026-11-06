#nullable enable

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class DeliveryTemperatureUserInterfaceContractTests
{
    [Test]
    public async Task TemperatureSection_InitializesBeforeBindingAnInactiveScreenTarget()
    {
        string managedDirectory = Environment.GetEnvironmentVariable(
            "ONI_MANAGED_ASSEMBLY_DIRECTORY")
            ?? throw new InvalidOperationException("The installed ONI assemblies are required.");
        var refresh = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                Path.Combine(managedDirectory, "Assembly-CSharp.dll"),
                "DetailsScreen", "Refresh")
            .Single().Instructions.ToList();
        int setTarget = refresh.FindIndex(instruction =>
            instruction.ResolvedOperand == "SideScreenContent.SetTarget");
        int show = refresh.FindIndex(instruction =>
            instruction.ResolvedOperand == "KScreen.Show");
        await Assert.That(setTarget >= 0 && show > setTarget).IsTrue().Because("The game binds the target before showing the inactive side screen.");

        string path = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment().ResolvePipelineBuildOrInconclusive().AssemblyPath;
        var binding = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                path, "DeliveryTemperatureLimit.TemperatureLimitSideScreen", "SetTarget")
            .Single().Instructions.Where(instruction => instruction.Operation != "nop").ToList();
        await Assert.That(binding[0].Operation).IsEqualTo("ldarg.0");
        await Assert.That(binding[1].ResolvedOperand).IsEqualTo("KMonoBehaviour.InitializeComponent").Because("Initialize through ONI's idempotent lifecycle before accessing the container or widget; Show has not run yet.");
    }

    [Test]
    public async Task TemperatureSection_InstantiatesItsOwnNativeHeaderBeforeSettingItsTitle()
    {
        var instructions = ReadSideScreenInitialization();
        int clone = instructions.FindIndex(instruction =>
            instruction.ResolvedOperand == "Util.KInstantiateUI");
        await Assert.That(clone >= 0).IsTrue().Because("GetTitle only supplies the tab's top title. The temperature section needs its own native header instance.");
        int title = instructions.FindIndex(instruction =>
            instruction.ResolvedOperand == "TMPro.TMP_Text.SetText");
        await Assert.That(title > clone).IsTrue().Because("Set the section title on the cloned header, preserving the shared tab title.");
        await Assert.That(instructions.Take(clone).Any(instruction =>
            instruction.ResolvedOperand == "DetailsScreen.GetTabOfType")).IsTrue().Because("The header must come from the game's side-screen tab, so its styling follows ONI.");
    }

    [Test]
    public async Task TemperatureSection_KeepsThePaddedEditorInASeparateContentContainer()
    {
        var instructions = ReadSideScreenInitialization();
        int container = instructions.FindIndex(instruction =>
            instruction.Operation == "stfld"
            && instruction.ResolvedOperand == "SideScreenContent.ContentContainer");
        await Assert.That(container > 0).IsTrue();
        await Assert.That(instructions[container - 1].ResolvedOperand).IsEqualTo("PeterHan.PLib.UI.PUIElements.CreateUI").Because("Editor padding must apply to a child container, leaving the native section header at full width.");
    }

    private static List<AssemblyInstructionContract> ReadSideScreenInitialization()
    {
        string path = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment().ResolvePipelineBuildOrInconclusive().AssemblyPath;
        return DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                path, "DeliveryTemperatureLimit.TemperatureLimitSideScreen", "OnPrefabInit")
            .Single().Instructions.ToList();
    }

    [Test]
    public async Task TemperatureLimitWidget_WhenCompiled_DoesNotAddLegacyInputFieldToTmpInputs()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment().ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0)
        {
            Skip.Test("Supply DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH to inspect the compiled widget.");
        }

        string assemblyPath = builds.Single().AssemblyPath;
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        TypeDefinition widget = metadata.TypeDefinitions
            .Select(metadata.GetTypeDefinition)
            .Single(type => metadata.GetString(type.Namespace) == "DeliveryTemperatureLimit"
                && metadata.GetString(type.Name) == "TemperatureLimitWidget");

        foreach (MethodDefinitionHandle handle in widget.GetMethods())
        {
            string methodName = metadata.GetString(metadata.GetMethodDefinition(handle).Name);
            foreach (var body in DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
                         assemblyPath, "DeliveryTemperatureLimit.TemperatureLimitWidget", methodName))
            {
                foreach (var instruction in body.Instructions.Where(instruction =>
                             instruction.ResolvedOperand == "method-spec:UnityEngine.GameObject.AddComponent"))
                {
                    var specification = metadata.GetMethodSpecification(
                        (MethodSpecificationHandle)MetadataTokens.EntityHandle((int)instruction.Operand!));
                    BlobReader signature = metadata.GetBlobReader(specification.Signature);
                    signature.ReadSignatureHeader();
                    await Assert.That(signature.ReadCompressedInteger()).IsEqualTo(1);
                    if (signature.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle)
                    {
                        continue;
                    }

                    EntityHandle argument = signature.ReadTypeHandle();
                    if (argument.Kind != HandleKind.TypeReference)
                    {
                        continue;
                    }

                    TypeReference component = metadata.GetTypeReference((TypeReferenceHandle)argument);
                    await Assert.That(metadata.GetString(component.Namespace) == "UnityEngine.UI"
                        && metadata.GetString(component.Name) == "InputField").IsFalse().Because($"{methodName} adds a legacy InputField to a PLib TMP input. Unity rejects the second Selectable, returning null before dummyField.enabled is assigned.");
                }
            }
        }
    }

    [Test]
    public async Task TemperatureLimitSideScreen_WhenInspected_DoesNotPollOrSwallowKeysInUpdate()
    {
        string sourceRoot = ResolveSourceRoot();
        string sideScreenSource = File.ReadAllText(
            Path.Combine(sourceRoot, "TemperatureLimitUserInterface", "TemperatureLimitSideScreen.cs"));

        await Assert.That(sideScreenSource.Contains("Input.GetKeyDown(KeyCode.W)", StringComparison.Ordinal)).IsFalse().Because("TemperatureLimitSideScreen must not poll WASD keys in Update.");
        await Assert.That(sideScreenSource.Contains("[DEBUG-KEY]", StringComparison.Ordinal)).IsFalse().Because("TemperatureLimitSideScreen must not retain diagnostic debug log probes.");
    }

    [Test]
    public async Task DeliveryTemperatureOptionsUiBridge_WhenInspected_ResolvesImplementedMethodBeforeHarmonyPatch()
    {
        string sourceRoot = ResolveSourceRoot();
        string bridgeSource = File.ReadAllText(
            Path.Combine(sourceRoot, "Options", "DeliveryTemperatureOptionsUiBridge.cs"));

        await Assert.That(bridgeSource).Contains("GetImplementedMethod").Because("DeliveryTemperatureOptionsUiBridge must resolve the implemented method before Harmony patching.");
        await Assert.That(bridgeSource).Contains("MethodBase.GetMethodFromHandle").Because("DeliveryTemperatureOptionsUiBridge must resolve methods to their declaring type via MethodBase.GetMethodFromHandle.");
        await Assert.That(bridgeSource).Contains("MethodInfo patchTarget = GetImplementedMethod(target);").Because("DeliveryTemperatureOptionsUiBridge.Patch must resolve patchTarget via GetImplementedMethod.");

        MethodInfo derivedMethod = typeof(ContractTestDerivedDialogScreenDouble).GetMethod("Deactivate")!;
        await Assert.That(derivedMethod.ReflectedType).IsNotEqualTo(derivedMethod.DeclaringType).Because("Derived class reflection must produce an inherited method where ReflectedType differs from DeclaringType.");

        MethodInfo resolvedMethod = ((MethodInfo?)MethodBase.GetMethodFromHandle(
            derivedMethod.MethodHandle,
            derivedMethod.DeclaringType!.TypeHandle))!;
        await Assert.That(resolvedMethod.ReflectedType).IsEqualTo(resolvedMethod.DeclaringType).Because("Resolved method handle on DeclaringType must produce ReflectedType == DeclaringType to satisfy Harmony.");
    }

    private class ContractTestBaseKScreenDouble
    {
        public virtual void Deactivate() { }
    }

    private class ContractTestDerivedDialogScreenDouble : ContractTestBaseKScreenDouble
    {
    }

    private static string ResolveSourceRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(
                directory,
                "mods",
                "delivery-temperature-limit-supercooled",
                "Source");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new DirectoryNotFoundException(
            "Could not resolve the delivery-temperature-limit-supercooled Source directory.");
    }
}
