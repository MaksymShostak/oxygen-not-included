#nullable enable

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class DeliveryTemperatureUserInterfaceContractTests
{
    [TestMethod]
    public void TemperatureLimitWidget_WhenCompiled_DoesNotAddLegacyInputFieldToTmpInputs()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment().ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0)
        {
            Assert.Inconclusive("Supply DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH to inspect the compiled widget.");
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
                    Assert.AreEqual(1, signature.ReadCompressedInteger());
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
                    Assert.IsFalse(
                        metadata.GetString(component.Namespace) == "UnityEngine.UI"
                        && metadata.GetString(component.Name) == "InputField",
                        $"{methodName} adds a legacy InputField to a PLib TMP input. Unity rejects the second Selectable, returning null before dummyField.enabled is assigned.");
                }
            }
        }
    }

    [TestMethod]
    public void TemperatureLimitSideScreen_WhenInspected_DoesNotPollOrSwallowKeysInUpdate()
    {
        string sourceRoot = ResolveSourceRoot();
        string sideScreenSource = File.ReadAllText(
            Path.Combine(sourceRoot, "TemperatureLimitUserInterface", "TemperatureLimitSideScreen.cs"));

        Assert.IsFalse(
            sideScreenSource.Contains("Input.GetKeyDown(KeyCode.W)", StringComparison.Ordinal),
            "TemperatureLimitSideScreen must not poll WASD keys in Update.");
        Assert.IsFalse(
            sideScreenSource.Contains("[DEBUG-KEY]", StringComparison.Ordinal),
            "TemperatureLimitSideScreen must not retain diagnostic debug log probes.");
    }

    [TestMethod]
    public void DeliveryTemperatureOptionsUiBridge_WhenInspected_ResolvesImplementedMethodBeforeHarmonyPatch()
    {
        string sourceRoot = ResolveSourceRoot();
        string bridgeSource = File.ReadAllText(
            Path.Combine(sourceRoot, "Options", "DeliveryTemperatureOptionsUiBridge.cs"));

        StringAssert.Contains(
            bridgeSource,
            "GetImplementedMethod",
            "DeliveryTemperatureOptionsUiBridge must resolve the implemented method before Harmony patching.");
        StringAssert.Contains(
            bridgeSource,
            "MethodBase.GetMethodFromHandle",
            "DeliveryTemperatureOptionsUiBridge must resolve methods to their declaring type via MethodBase.GetMethodFromHandle.");
        StringAssert.Contains(
            bridgeSource,
            "MethodInfo patchTarget = GetImplementedMethod(target);",
            "DeliveryTemperatureOptionsUiBridge.Patch must resolve patchTarget via GetImplementedMethod.");

        MethodInfo derivedMethod = typeof(ContractTestDerivedDialogScreenDouble).GetMethod("Deactivate")!;
        Assert.AreNotEqual(
            derivedMethod.DeclaringType,
            derivedMethod.ReflectedType,
            "Derived class reflection must produce an inherited method where ReflectedType differs from DeclaringType.");

        MethodInfo resolvedMethod = ((MethodInfo?)MethodBase.GetMethodFromHandle(
            derivedMethod.MethodHandle,
            derivedMethod.DeclaringType!.TypeHandle))!;
        Assert.AreEqual(
            resolvedMethod.DeclaringType,
            resolvedMethod.ReflectedType,
            "Resolved method handle on DeclaringType must produce ReflectedType == DeclaringType to satisfy Harmony.");
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
