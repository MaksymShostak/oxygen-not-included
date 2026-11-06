using System.Reflection;
using System.Runtime.Loader;
using DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureAssemblyContracts;
public sealed class TemperatureRangeHelpTests
{
    [Test]
    public async Task HelpPreview_StaysOpenWhileMovingFromTriggerToContent()
    {
        using var help = new HelpState();
        await help.Call("SetPointerOverTrigger", true);
        await Assert.That(help.Visible).IsTrue();
        await help.Call("SetPointerOverContent", true);
        await help.Call("SetPointerOverTrigger", false);
        await Assert.That(help.Visible).IsTrue();
        await help.Call("SetPointerOverContent", false);
        await Assert.That(help.Visible).IsFalse();
    }

    [Test]
    public async Task Activation_PinsHelpAfterPointerAndFocusLeave_SecondActivationClosesIt()
    {
        using var help = new HelpState();
        await help.Call("SetFocused", true);
        await help.Call("Activate");
        await help.Call("SetFocused", false);
        await Assert.That(help.Visible).IsTrue();
        await help.Call("SetPointerOverTrigger", true);
        await help.Call("Activate");
        await Assert.That(help.Visible).IsFalse().Because("Closing must not immediately reopen from the same hover.");
    }

    [Test]
    public async Task Escape_DismissesHelpUntilANewInteraction_ActivationCanReopenIt()
    {
        using var help = new HelpState();
        await help.Call("SetFocused", true);
        await Assert.That(help.Visible).IsTrue();
        await help.Call("Dismiss");
        await Assert.That(help.Visible).IsFalse();
        await help.Call("SetFocused", true);
        await Assert.That(help.Visible).IsFalse();
        await help.Call("Activate");
        await Assert.That(help.Visible).IsTrue();
        await help.Call("Dismiss");
        await help.Call("SetFocused", false);
        await help.Call("SetFocused", true);
        await Assert.That(help.Visible).IsTrue();
    }

    [Test]
    public async Task ChangingTargetOrClosingEditor_ResetsPinnedAndPreviewHelp()
    {
        using var help = new HelpState();
        await help.Call("SetPointerOverTrigger", true);
        await help.Call("SetFocused", true);
        await help.Call("Activate");
        await help.Call("Reset");
        await Assert.That(help.Visible).IsFalse();
        await help.Call("SetPointerOverTrigger", true);
        await Assert.That(help.Visible).IsTrue();
    }

    [Test]
    public async Task HigherModal_ClosesExistingPinnedHelpWithoutReopeningFromTheSameHover()
    {
        using var help = new HelpState();
        await help.Call("SetKeyboardLayerActive", true);
        await help.Call("SetPointerOverTrigger", true);
        await help.Call("Activate");
        await help.Call("SetKeyboardLayerActive", false);
        await Assert.That(help.Visible).IsFalse();
        await help.Call("SetKeyboardLayerActive", true);
        await help.Call("SetPointerOverTrigger", true);
        await Assert.That(help.Visible).IsFalse();
    }

    [Test]
    public async Task AlreadyBlockedKeyboardLayer_DoesNotCancelANewRoutedPointerPreview()
    {
        using var help = new HelpState();
        await help.Call("SetKeyboardLayerActive", false);
        await help.Call("SetPointerOverTrigger", true);
        await help.Call("SetKeyboardLayerActive", false);
        await Assert.That(help.Visible).IsTrue().Because("Only a change of input layer dismisses help, not a stale keyboard gate.");
    }

    [Test]
    public async Task NormalEditorRefresh_DoesNotDisplayTheRoutineRangeSummary()
    {
        var body = await Read("TemperatureLimitWidget", "UpdateInputsCore");
        await Assert.That(body.Any(i => i.ResolvedOperand ==
            "DeliveryTemperatureLimit.TemperatureLimitPresenter.GetRangeDescription")).IsFalse().Because("Normal use should show the controls without a redundant range sentence.");
    }

    [Test]
    public async Task FeedbackUpdate_ReachesInactiveTextAndRemovesEmptyFeedbackFromLayout()
    {
        var body = await Read("TemperatureLimitWidget", "ShowFeedback");
        await AssertCalls(body, "TMPro.TMP_Text.set_text");
        await AssertCalls(body, "UnityEngine.GameObject.SetActive");
        await AssertCalls(body, "UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild");
    }

    [Test]
    public async Task FeedbackLayout_MeasuresAtAvailableWidthAndProtectsWrappedHeight()
    {
        var body = await Read("TemperatureRangeTextLayout", "SetLayoutHorizontal");
        await AssertCalls(body, "TMPro.TMP_Text.GetPreferredValues");
        await AssertCalls(body, "UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild");
        await AssertCalls((await Read("TemperatureRangeTextLayout", "get_minHeight")),
            "DeliveryTemperatureLimit.TemperatureRangeTextLayout.get_preferredHeight");
    }

    [Test]
    public async Task HelpKeyboardPolling_RespectsTheGameScreenStacksModalOrdering()
    {
        var body = await Read("TemperatureRangeHelpScreen", "ScreenUpdate");
        await Assert.That(body.Any(i => i.Operation.StartsWith("brtrue", StringComparison.Ordinal))).IsTrue().Because("A higher modal must prevent background help from processing raw keyboard input.");
        await AssertCalls(body, "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.HandleKeyboard");
        await AssertCalls(body, "DeliveryTemperatureLimit.TemperatureRangeHelpState.SetKeyboardLayerActive");
        await Assert.That(DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(BuildPath(),
            "DeliveryTemperatureLimit.TemperatureRangeHelpScreen", "Update")).Count().IsEqualTo(0).Because("Raw Update bypasses KScreenManager's topLevel gate.");
    }

    [Test]
    public async Task RoutedClick_OpensHelpEvenWhenThePreviousKeyboardPollWasBlocked()
    {
        await AssertRoutedEventOpensHelp("ToggleHelp");
    }

    [Test]
    public async Task RoutedHover_OpensHelpEvenWhenThePreviousKeyboardPollWasBlocked()
    {
        await AssertRoutedEventOpensHelp("OnPointerEnter");
    }

    [Test]
    public async Task PopoverCreation_UsesTheGamesUiLayerAndTransformInitialization()
    {
        // A raw GameObject stays on Default. ONI's camera UI needs the UI layer,
        // unit scale and RectTransform supplied by the same helper as the editor.
        await AssertCalls((await Read("TemperatureRangeHelpScreen", "CreatePopover")),
            "PeterHan.PLib.UI.PUIElements.CreateUI");
    }

    [Test]
    public async Task DisabledHelp_IgnoresRoutedClickAndHover()
    {
        await AssertRoutedEventOpensHelp("OnPointerEnter", enabled: false);
        await AssertRoutedEventOpensHelp("OnSubmit", enabled: false);
    }

    private static async Task AssertRoutedEventOpensHelp(string method, bool enabled = true)
    {
        using var help = new HelpState();
        var instructions = await Read("TemperatureRangeHelpScreen", method);
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
                        await Assert.That(targetIndex >= 0).IsTrue();
                        index = targetIndex - 1;
                    }
                    break;
                case "call": case "callvirt":
                    switch (instruction.ResolvedOperand)
                    {
                        case "DeliveryTemperatureLimit.RuntimeFailureReporting.get_IsUserInterfaceEnabled":
                            stack.Push(enabled); break;
                        case "KScreen.OnPointerEnter": stack.Pop(); stack.Pop(); break;
                        case "DeliveryTemperatureLimit.TemperatureRangeHelpState.Activate":
                            await ((HelpState)stack.Pop()).Call("Activate"); break;
                        case "DeliveryTemperatureLimit.TemperatureRangeHelpState.SetPointerOverTrigger":
                            bool over = (bool)stack.Pop();
                            await ((HelpState)stack.Pop()).Call("SetPointerOverTrigger", over); break;
                        case "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.ApplyVisibility":
                            stack.Pop(); stack.Pop(); presented = help.Visible; break;
                        default: Assert.Fail("Unexpected event call: " + instruction); break;
                    }
                    break;
                case "ret":
                    await Assert.That(presented).IsEqualTo(enabled).Because("Only an enabled help control may request visible help.");
                    return;
                case "leave": case "leave.s":
                    int destination = instructions[index + 1].Offset + Convert.ToInt32(instruction.Operand);
                    index = instructions.ToList().FindIndex(item => item.Offset == destination) - 1;
                    await Assert.That(index >= 0).IsTrue();
                    stack.Clear();
                    break;
                case "nop": break;
                default: Assert.Fail("Unsupported event instruction: " + instruction); break;
            }
        }
        Assert.Fail("The routed event did not return.");
    }

    [Test]
    public async Task HelpTabNavigation_ChecksWhichEditorOwnsTheCurrentSelection()
    {
        await AssertCalls((await Read("TemperatureRangeHelpScreen", "HandleKeyboard")),
            "DeliveryTemperatureLimit.TemperatureLimitWidget.CanNavigateFromCurrentSelection");
    }

    [Test]
    public async Task ScrollingHelp_DoesNotAlsoZoomTheGame()
    {
        var body = await Read("TemperatureRangeHelpScreen", "OnKeyDown");
        await AssertCalls(body, "DeliveryTemperatureLimit.TemperatureRangeHelpState.get_IsPointerOverContent");
        await AssertCalls(body, "KButtonEvent.TryConsume");
    }

    [Test]
    public async Task OverflowingHelp_CanBeScrolledWithoutAMouse()
    {
        await AssertCalls((await Read("TemperatureRangeHelpScreen", "HandleKeyboard")),
            "DeliveryTemperatureLimit.TemperatureRangeHelpScreen.HandleHelpScroll");
        await AssertCalls((await Read("TemperatureRangeHelpScreen", "HandleHelpScroll")),
            "UnityEngine.UI.ScrollRect.set_verticalNormalizedPosition");
    }

    private static async Task AssertCalls(IReadOnlyList<AssemblyInstructionContract> body, string method) =>
        await Assert.That(body.Any(i => i.ResolvedOperand == method)).IsTrue().Because("Missing native boundary call: " + method);

    private static async Task<IReadOnlyList<AssemblyInstructionContract>> Read(string type, string method)
    {
        var bodies = DeliveryTemperatureAssemblyMetadataReader.ReadMethodBodies(BuildPath(),
            "DeliveryTemperatureLimit." + type, method);
        await Assert.That(bodies).Count().IsEqualTo(1).Because($"The compiled UI must implement {type}.{method}.");
        return bodies.Single().Instructions;
    }

    private static string BuildPath()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment()
            .ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Skip.Test("Run with DELIVERY_TEMPERATURE_LIMIT_BUILD_RESULT_PATH.");
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
            if (candidate is null) throw new TUnit.Assertions.Exceptions.AssertionException("The help interaction must support focus, pinned opening, and dismissal.");
            type = candidate;
            instance = Activator.CreateInstance(type, nonPublic: true)!;
        }

        public bool Visible => (bool)type.GetProperty("IsVisible")!.GetValue(instance)!;
        public async Task Call(string method, params object[] arguments)
        {
            MethodInfo? handler = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            if (handler is null) throw new TUnit.Assertions.Exceptions.AssertionException("Missing help interaction: " + method);
            handler.Invoke(instance, arguments);
        }
        public void Dispose() => context.Unload();
    }
}
