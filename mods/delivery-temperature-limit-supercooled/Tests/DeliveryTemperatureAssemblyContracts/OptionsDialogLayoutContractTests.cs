using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

/// <summary>
/// Guards the Unity layout boundary in the compiled mod. These checks do not
/// replace rendered acceptance at different UI scales and translation lengths.
/// </summary>
[TestClass]
public sealed class OptionsDialogLayoutContractTests
{
    [TestMethod]
    public void RefreshingDisclosures_DoesNotResizeOrRepositionTheDialogShell()
    {
        const string owner = "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.";
        var pending = new Queue<string>();
        var visited = new HashSet<string>();
        pending.Enqueue("Refresh");
        while (pending.TryDequeue(out string? method))
        {
            if (!visited.Add(method)) continue;
            foreach (var instruction in Read("DeliveryTemperatureOptionsDialog", method))
            {
                Assert.AreNotEqual("UnityEngine.RectTransform.SetSizeWithCurrentAnchors",
                    instruction.ResolvedOperand, "Expanding content must scroll inside the existing shell.");
                if (instruction.ResolvedOperand?.StartsWith(owner, StringComparison.Ordinal) == true &&
                    instruction.Operation == "call")
                    pending.Enqueue(instruction.ResolvedOperand.Substring(owner.Length));
            }
        }
    }

    [TestMethod]
    public void ReportingNavigation_PreservesTheExistingOptionsDraft()
    {
        foreach (string method in new[] { "ShowReport", "BackToOptions" })
        {
            var body = Read("DeliveryTemperatureOptionsDialog", method);
            AssertCalls(body, "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.Refresh",
                "Navigation changes the visible view in the existing dialog.");
            Assert.IsFalse(body.Any(i => i.ResolvedOperand is
                "DeliveryTemperatureLimit.OptionsEditSession..ctor" or
                "DeliveryTemperatureLimit.OptionsEditSession.Save" or
                "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.Build" or
                "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.Close"),
                "Returning from a report must not rebuild, save, or discard the settings session.");
        }
        AssertCalls(Read("DeliveryTemperatureOptionsDialog", "RequestClose"),
            "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.BackToOptions",
            "Escape and the title close control must return from reporting before closing Options.");
    }

    [TestMethod]
    public void HiddenSections_AreRemovedFromTheActiveLayoutHierarchy()
    {
        var body = Read("DeliveryTemperatureOptionsDialog", "SetVisible");
        AssertCalls(body, "UnityEngine.GameObject.SetActive",
            "A collapsed section must leave PLib's active-child layout, not only become invisible.");
        Assert.IsFalse(body.Any(i => i.ResolvedOperand == "UnityEngine.Transform.set_localScale"),
            "A hidden panel must not remain an active child of the layout hierarchy.");
    }

    [TestMethod]
    public void WrappedText_ProtectsItsMeasuredHeightFromCompression()
    {
        AssertCalls(Read("DeliveryTemperatureOptionsDialog", "SizeText"),
            "UnityEngine.UI.LayoutElement.set_minHeight",
            "Preferred height alone allows expanded scroll content to compress labels and buttons.");
    }

    [TestMethod]
    public void TextGeometry_HasOneLayoutControllerOwner()
    {
        var body = Read("DeliveryTemperatureOptionsDialog", "SizeText");
        AssertCalls(body, "UnityEngine.Object.DestroyImmediate",
            "PLib directly invokes child ILayoutControllers even when the Behaviour is disabled.");
        Assert.IsFalse(body.Any(i => i.ResolvedOperand == "UnityEngine.Behaviour.set_enabled"),
            "Disabling the old layout leaves a competing controller attached to the text wrapper.");
    }

    [TestMethod]
    public void TextGeometry_SetsTheTextAlignmentWhenReplacingPlibLayout()
    {
        AssertCalls(Read("DeliveryTemperatureOptionsDialog", "SizeText"),
            "TMPro.TMP_Text.set_alignment",
            "PLabel.TextAlignment controls its wrapper; replacing that layout must also align the text itself.");
    }

    [TestMethod]
    public void DisabledTemperatureFields_HaveAVisibleDisabledState()
    {
        var body = Read("DeliveryTemperatureOptionsDialog", "SetInputEnabled");
        AssertCalls(body, "UnityEngine.UI.Selectable.set_interactable",
            "A disabled field must reject editing.");
        AssertCalls(body, "UnityEngine.CanvasGroup.set_alpha",
            "The white native input must visibly communicate that editing is unavailable.");
    }

    [TestMethod]
    public void StatusUpdates_CanPopulateTextBeforeTheHiddenLabelIsShown()
    {
        var body = Read("DeliveryTemperatureOptionsDialog", "SetLabel");
        AssertCalls(body, "TMPro.TMP_Text.set_text",
            "Status updates must reach inactive text children; PLib.SetText only searches active children.");
        Assert.IsFalse(body.Any(i => i.ResolvedOperand == "PeterHan.PLib.UI.PUIElements.SetText"),
            "A first validation error or report message must not be lost while its label is hidden.");
    }

    [TestMethod]
    public void ReportSuccess_UsesAFileNameInsteadOfAnUnboundedDirectoryPath()
    {
        AssertCalls(Read("SupportReportPlayerPresenter", "PresentSuccess"),
            "System.IO.Path.GetFileName",
            "The report status must not fill the panel with the user's full filesystem path.");
    }

    private static IReadOnlyList<AssemblyInstructionContract> Read(string type, string method)
    {
        var builds = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment().ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0)
            Assert.Inconclusive("Supply DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH for compiled UI contracts.");
        var bodies = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
            builds.Single().AssemblyPath, "DeliveryTemperatureLimit." + type, method);
        Assert.HasCount(1, bodies, $"The compiled mod must implement {type}.{method}.");
        return bodies.Single().Instructions;
    }

    private static void AssertCalls(IReadOnlyList<AssemblyInstructionContract> body,
        string member, string message) =>
        Assert.IsTrue(body.Any(i => i.ResolvedOperand == member), message);
}
