using System.Reflection;
using System.Runtime.Loader;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;

[TestClass]
public sealed class TemperatureRangeHelpTests
{
    [TestMethod]
    public void HelpPreview_StaysOpenWhileMovingFromTriggerToContent()
    {
        using var help = new HelpState();
        help.Call("SetPointerOverTrigger", true);
        Assert.IsTrue(help.Visible);
        help.Call("SetPointerOverContent", true);
        help.Call("SetPointerOverTrigger", false);
        Assert.IsTrue(help.Visible);
        help.Call("SetPointerOverContent", false);
        Assert.IsFalse(help.Visible);
    }

    [TestMethod]
    public void Activation_PinsHelpAfterPointerAndFocusLeave_SecondActivationClosesIt()
    {
        using var help = new HelpState();
        help.Call("SetFocused", true);
        help.Call("Activate");
        help.Call("SetFocused", false);
        Assert.IsTrue(help.Visible);
        help.Call("SetPointerOverTrigger", true);
        help.Call("Activate");
        Assert.IsFalse(help.Visible, "Closing must not immediately reopen from the same hover.");
    }

    [TestMethod]
    public void Escape_DismissesHelpUntilANewInteraction_ActivationCanReopenIt()
    {
        using var help = new HelpState();
        help.Call("SetFocused", true);
        Assert.IsTrue(help.Visible);
        help.Call("Dismiss");
        Assert.IsFalse(help.Visible);
        help.Call("SetFocused", true);
        Assert.IsFalse(help.Visible);
        help.Call("Activate");
        Assert.IsTrue(help.Visible);
        help.Call("Dismiss");
        help.Call("SetFocused", false);
        help.Call("SetFocused", true);
        Assert.IsTrue(help.Visible);
    }

    [TestMethod]
    public void ChangingTargetOrClosingEditor_ResetsPinnedAndPreviewHelp()
    {
        using var help = new HelpState();
        help.Call("SetPointerOverTrigger", true);
        help.Call("SetFocused", true);
        help.Call("Activate");
        help.Call("Reset");
        Assert.IsFalse(help.Visible);
        help.Call("SetPointerOverTrigger", true);
        Assert.IsTrue(help.Visible);
    }

    [TestMethod]
    public void HigherModal_ClosesExistingPinnedHelpWithoutReopeningFromTheSameHover()
    {
        using var help = new HelpState();
        help.Call("SetKeyboardLayerActive", true);
        help.Call("SetPointerOverTrigger", true);
        help.Call("Activate");
        help.Call("SetKeyboardLayerActive", false);
        Assert.IsFalse(help.Visible);
        help.Call("SetKeyboardLayerActive", true);
        help.Call("SetPointerOverTrigger", true);
        Assert.IsFalse(help.Visible);
    }

    [TestMethod]
    public void AlreadyBlockedKeyboardLayer_DoesNotCancelANewRoutedPointerPreview()
    {
        using var help = new HelpState();
        help.Call("SetKeyboardLayerActive", false);
        help.Call("SetPointerOverTrigger", true);
        help.Call("SetKeyboardLayerActive", false);
        Assert.IsTrue(help.Visible, "Only a change of input layer dismisses help, not a stale keyboard gate.");
    }

    [TestMethod]
    public void NormalEditorRefresh_DoesNotDisplayTheRoutineRangeSummary()
    {
        var body = Read("TemperatureLimitWidget", "UpdateInputsCore");
        Assert.IsFalse(body.Any(i => i.ResolvedOperand ==
            "DeliveryTemperatureLimit.TemperatureLimitPresenter.GetRangeDescription"),
            "Normal use should show the controls without a redundant range sentence.");
    }

    [TestMethod]
    public void FeedbackUpdate_ReachesInactiveTextAndRemovesEmptyFeedbackFromLayout()
    {
        var body = Read("TemperatureLimitWidget", "ShowFeedback");
        AssertCalls(body, "TMPro.TMP_Text.set_text");
        AssertCalls(body, "UnityEngine.GameObject.SetActive");
        AssertCalls(body, "UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild");
    }

    [TestMethod]
    public void FeedbackLayout_MeasuresAtAvailableWidthAndProtectsWrappedHeight()
    {
        var body = Read("TemperatureRangeTextLayout", "SetLayoutHorizontal");
        AssertCalls(body, "TMPro.TMP_Text.GetPreferredValues");
        AssertCalls(body, "UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild");
        AssertCalls(Read("TemperatureRangeTextLayout", "get_minHeight"),
            "DeliveryTemperatureLimit.TemperatureRangeTextLayout.get_preferredHeight");
    }

    [TestMethod]
    public void HelpKeyboardPolling_RespectsTheGameScreenStacksModalOrdering()
    {
        var body = Read("TemperatureRangeHelpScreen", "ScreenUpdate");
        Assert.IsTrue(body.Any(i => i.Operation.StartsWith("brtrue", StringComparison.Ordinal)),
            "A higher modal must prevent background help from processing raw keyboard input.");
        AssertCalls(body, "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.HandleKeyboard");
        AssertCalls(body, "DeliveryTemperatureLimit.TemperatureRangeHelpState.SetKeyboardLayerActive");
        Assert.HasCount(0, DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(BuildPath(),
            "DeliveryTemperatureLimit.TemperatureRangeHelpScreen", "Update"),
            "Raw Update bypasses KScreenManager's topLevel gate.");
    }

    [TestMethod]
    public void RoutedClick_OpensHelpEvenWhenThePreviousKeyboardPollWasBlocked()
    {
        AssertRoutedEventOpensHelp("ToggleHelp");
    }

    [TestMethod]
    public void RoutedHover_OpensHelpEvenWhenThePreviousKeyboardPollWasBlocked()
    {
        AssertRoutedEventOpensHelp("OnPointerEnter");
    }

    [TestMethod]
    public void PopoverCreation_UsesTheGamesUiLayerAndTransformInitialization()
    {
        // A raw GameObject stays on Default. ONI's camera UI needs the UI layer,
        // unit scale and RectTransform supplied by the same helper as the editor.
        AssertCalls(Read("TemperatureRangeHelpScreen", "CreatePopover"),
            "PeterHan.PLib.UI.PUIElements.CreateUI");
    }

    private static void AssertRoutedEventOpensHelp(string method)
    {
        using var help = new HelpState();
        var instructions = Read("TemperatureRangeHelpScreen", method);
        var stack = new Stack<object>();
        bool presented = false;
        // Execute the compiled event branch and real interaction state. Only the
        // native pointer base call and renderer are boundaries; this is not a
        // claim that a headless test can verify Unity's displayed pixels.
        for (int index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            switch (instruction.Operation)
            {
                case "ldarg.0": case "ldarg.1": stack.Push(new object()); break;
                case "ldc.i4.0": stack.Push(false); break;
                case "ldc.i4.1": stack.Push(true); break;
                case "ldfld":
                    stack.Pop();
                    if (instruction.ResolvedOperand == "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.acceptsInput")
                        stack.Push(false); // Last screen-stack poll, not this routed event.
                    else if (instruction.ResolvedOperand == "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.state")
                        stack.Push(help);
                    else Assert.Fail("Unexpected event field: " + instruction);
                    break;
                case "brfalse": case "brfalse.s": case "brtrue": case "brtrue.s":
                    bool condition = (bool)stack.Pop();
                    if (condition == instruction.Operation.StartsWith("brtrue", StringComparison.Ordinal))
                    {
                        int target = instructions[index + 1].Offset + Convert.ToInt32(instruction.Operand);
                        int targetIndex = instructions.ToList().FindIndex(item => item.Offset == target);
                        Assert.IsTrue(targetIndex >= 0);
                        index = targetIndex - 1;
                    }
                    break;
                case "call": case "callvirt":
                    switch (instruction.ResolvedOperand)
                    {
                        case "KScreen.OnPointerEnter": stack.Pop(); stack.Pop(); break;
                        case "DeliveryTemperatureLimit.TemperatureRangeHelpState.Activate":
                            ((HelpState)stack.Pop()).Call("Activate"); break;
                        case "DeliveryTemperatureLimit.TemperatureRangeHelpState.SetPointerOverTrigger":
                            bool over = (bool)stack.Pop();
                            ((HelpState)stack.Pop()).Call("SetPointerOverTrigger", over); break;
                        case "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.ApplyVisibility":
                            stack.Pop(); stack.Pop(); presented = help.Visible; break;
                        default: Assert.Fail("Unexpected event call: " + instruction); break;
                    }
                    break;
                case "ret":
                    Assert.IsTrue(presented, "A pointer event delivered to the help control must request visible help.");
                    return;
                case "nop": break;
                default: Assert.Fail("Unsupported event instruction: " + instruction); break;
            }
        }
        Assert.Fail("The routed event did not return.");
    }

    [TestMethod]
    public void HelpTabNavigation_ChecksWhichEditorOwnsTheCurrentSelection()
    {
        AssertCalls(Read("TemperatureRangeHelpScreen", "HandleKeyboard"),
            "DeliveryTemperatureLimit.TemperatureLimitWidget.CanNavigateFromCurrentSelection");
    }

    [TestMethod]
    public void ScrollingHelp_DoesNotAlsoZoomTheGame()
    {
        var body = Read("TemperatureRangeHelpScreen", "OnKeyDown");
        AssertCalls(body, "DeliveryTemperatureLimit.TemperatureRangeHelpState.get_IsPointerOverContent");
        AssertCalls(body, "KButtonEvent.TryConsume");
    }

    [TestMethod]
    public void OverflowingHelp_CanBeScrolledWithoutAMouse()
    {
        AssertCalls(Read("TemperatureRangeHelpScreen", "HandleKeyboard"),
            "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.HandleHelpScroll");
        AssertCalls(Read("TemperatureRangeHelpScreen", "HandleHelpScroll"),
            "UnityEngine.UI.ScrollRect.set_verticalNormalizedPosition");
    }

    private static void AssertCalls(IReadOnlyList<AssemblyInstructionContract> body, string method) =>
        Assert.IsTrue(body.Any(i => i.ResolvedOperand == method), "Missing native boundary call: " + method);

    private static IReadOnlyList<AssemblyInstructionContract> Read(string type, string method)
    {
        var bodies = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(BuildPath(),
            "DeliveryTemperatureLimit." + type, method);
        Assert.HasCount(1, bodies, $"The compiled UI must implement {type}.{method}.");
        return bodies.Single().Instructions;
    }

    private static string BuildPath()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Assert.Inconclusive("Run with DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH.");
        return builds.Single().AssemblyPath;
    }

    // Execute the framework-independent interaction state from the exact built
    // artifact. Unity behaviours remain native-boundary contracts, not fake UI.
    private sealed class HelpState : IDisposable
    {
        private readonly AssemblyLoadContext context = new("TemperatureRangeHelp", isCollectible: true);
        private readonly Type type;
        private readonly object instance;

        public HelpState()
        {
            Type? candidate = context.LoadFromAssemblyPath(BuildPath())
                .GetType("DeliveryTemperatureLimit.TemperatureRangeHelpState");
            Assert.IsNotNull(candidate, "The help interaction must support focus, pinned opening, and dismissal.");
            type = candidate;
            instance = Activator.CreateInstance(type, nonPublic: true)!;
        }

        public bool Visible => (bool)type.GetProperty("IsVisible")!.GetValue(instance)!;
        public void Call(string method, params object[] arguments)
        {
            MethodInfo? handler = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(handler, "Missing help interaction: " + method);
            handler.Invoke(instance, arguments);
        }
        public void Dispose() => context.Unload();
    }
}
