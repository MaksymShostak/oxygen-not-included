using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class OptionsDialogLayoutContractTests
{
    [Test]
    public async Task RefreshingDisclosures_DoesNotResizeOrRepositionTheDialogShell()
    {
        const string owner = "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.";
        var pending = new Queue<string>();
        var visited = new HashSet<string>();
        pending.Enqueue("Refresh");
        while (pending.TryDequeue(out string? method))
        {
            if (!visited.Add(method)) continue;
            foreach (var instruction in (await Read("DeliveryTemperatureOptionsDialog", method)))
            {
                await Assert.That(instruction.ResolvedOperand).IsNotEqualTo("UnityEngine.RectTransform.SetSizeWithCurrentAnchors").Because("Expanding content must scroll inside the existing shell.");
                if (instruction.ResolvedOperand?.StartsWith(owner, StringComparison.Ordinal) == true &&
                    instruction.Operation == "call")
                    pending.Enqueue(instruction.ResolvedOperand.Substring(owner.Length));
            }
        }
    }

    [Test]
    public async Task ReportingNavigation_PreservesTheExistingOptionsDraft()
    {
        foreach (string method in new[] { "ShowReport", "BackToOptions" })
        {
            var body = await Read("DeliveryTemperatureOptionsDialog", method);
            await AssertCalls(body, "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.Refresh",
                "Navigation changes the visible view in the existing dialog.");
            await Assert.That(body.Any(i => i.ResolvedOperand is
                "DeliveryTemperatureLimit.OptionsEditSession..ctor" or
                "DeliveryTemperatureLimit.OptionsEditSession.Save" or
                "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.Build" or
                "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.Close")).IsFalse().Because("Returning from a report must not rebuild, save, or discard the settings session.");
        }
        await AssertCalls((await Read("DeliveryTemperatureOptionsDialog", "RequestClose")),
            "DeliveryTemperatureLimit.DeliveryTemperatureOptionsDialog.BackToOptions",
            "Escape and the title close control must return from reporting before closing Options.");
    }

    [Test]
    public async Task HiddenSections_AreRemovedFromTheActiveLayoutHierarchy()
    {
        var body = await Read("DeliveryTemperatureOptionsDialog", "SetVisible");
        await AssertCalls(body, "UnityEngine.GameObject.SetActive",
            "A collapsed section must leave PLib's active-child layout, not only become invisible.");
        await Assert.That(body.Any(i => i.ResolvedOperand == "UnityEngine.Transform.set_localScale")).IsFalse().Because("A hidden panel must not remain an active child of the layout hierarchy.");
    }

    [Test]
    public async Task WrappedText_ProtectsItsMeasuredHeightFromCompression()
    {
        await AssertCalls((await Read("DeliveryTemperatureOptionsDialog", "SizeText")),
            "UnityEngine.UI.LayoutElement.set_minHeight",
            "Preferred height alone allows expanded scroll content to compress labels and buttons.");
    }

    [Test]
    public async Task TextGeometry_HasOneLayoutControllerOwner()
    {
        var body = await Read("DeliveryTemperatureOptionsDialog", "SizeText");
        await AssertCalls(body, "UnityEngine.Object.DestroyImmediate",
            "PLib directly invokes child ILayoutControllers even when the Behaviour is disabled.");
        await Assert.That(body.Any(i => i.ResolvedOperand == "UnityEngine.Behaviour.set_enabled")).IsFalse().Because("Disabling the old layout leaves a competing controller attached to the text wrapper.");
    }

    [Test]
    public async Task TextGeometry_SetsTheTextAlignmentWhenReplacingPlibLayout()
    {
        await AssertCalls((await Read("DeliveryTemperatureOptionsDialog", "SizeText")),
            "TMPro.TMP_Text.set_alignment",
            "PLabel.TextAlignment controls its wrapper; replacing that layout must also align the text itself.");
    }

    [Test]
    public async Task DisabledTemperatureFields_HaveAVisibleDisabledState()
    {
        var body = await Read("DeliveryTemperatureOptionsDialog", "SetInputEnabled");
        await AssertCalls(body, "UnityEngine.UI.Selectable.set_interactable",
            "A disabled field must reject editing.");
        await AssertCalls(body, "UnityEngine.CanvasGroup.set_alpha",
            "The white native input must visibly communicate that editing is unavailable.");
    }

    [Test]
    public async Task StatusUpdates_CanPopulateTextBeforeTheHiddenLabelIsShown()
    {
        var body = await Read("DeliveryTemperatureOptionsDialog", "SetLabel");
        await AssertCalls(body, "TMPro.TMP_Text.set_text",
            "Status updates must reach inactive text children; PLib.SetText only searches active children.");
        await Assert.That(body.Any(i => i.ResolvedOperand == "PeterHan.PLib.UI.PUIElements.SetText")).IsFalse().Because("A first validation error or report message must not be lost while its label is hidden.");
    }

    [Test]
    public async Task ReportSuccess_UsesAFileNameInsteadOfAnUnboundedDirectoryPath()
    {
        await AssertCalls((await Read("SupportReportPlayerPresenter", "PresentSuccess")),
            "System.IO.Path.GetFileName",
            "The report status must not fill the panel with the user's full filesystem path.");
    }

    private static async Task<IReadOnlyList<AssemblyInstructionContract>> Read(string type, string method)
    {
        var builds = PipelineProvenanceBoundAssemblyLocator
            .CreateForCurrentPipelineEnvironment().ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0)
            Skip.Test("Supply DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH for compiled UI contracts.");
        var bodies = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(
            builds.Single().AssemblyPath, "DeliveryTemperatureLimit." + type, method);
        await Assert.That(bodies).Count().IsEqualTo(1).Because($"The compiled mod must implement {type}.{method}.");
        return bodies.Single().Instructions;
    }

    private static async Task AssertCalls(IReadOnlyList<AssemblyInstructionContract> body,
        string member, string message) =>
        await Assert.That(body.Any(i => i.ResolvedOperand == member)).IsTrue().Because(message);
}
