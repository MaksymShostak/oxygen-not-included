namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;

[TestClass]
public sealed class ThreadConfinedSessionSlotTests
{
    [TestInitialize]
    public void ClearCurrentTestThreadState()
    {
        ThreadConfinedSessionSlot<SlotTestContext>.DiscardAll();
    }

    [TestCleanup]
    public void ReleaseCurrentTestThreadState()
    {
        ThreadConfinedSessionSlot<SlotTestContext>.DiscardAll();
    }

    [TestMethod]
    public void Enter_WhenEmpty_SetsCurrent()
    {
        var context = new SlotTestContext("outer");

        var token = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            new GameSessionGeneration(1),
            context);

        Assert.IsTrue(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current));
        Assert.AreSame(context, current);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(token);
    }

    [TestMethod]
    public void Enter_WhenNested_SavesPreviousAndSetsNested()
    {
        var outer = new SlotTestContext("outer");
        var nested = new SlotTestContext("nested");
        var generation = new GameSessionGeneration(1);
        var outerToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            outer);

        var nestedToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            nested);

        Assert.IsTrue(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current));
        Assert.AreSame(nested, current);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(nestedToken);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken);
    }

    [TestMethod]
    public void Exit_WhenNested_RestoresPrevious()
    {
        var outer = new SlotTestContext("outer");
        var nested = new SlotTestContext("nested");
        var generation = new GameSessionGeneration(1);
        var outerToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            outer);
        var nestedToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            nested);

        ThreadConfinedSessionSlot<SlotTestContext>.Exit(nestedToken);

        Assert.IsTrue(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current));
        Assert.AreSame(outer, current);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken);
        Assert.IsFalse(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _));
    }

    [TestMethod]
    public void Exit_WhenTokenIsStale_ThrowsLifecycleViolation()
    {
        var generation = new GameSessionGeneration(1);
        var outerToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            new SlotTestContext("outer"));
        var nested = new SlotTestContext("nested");
        var nestedToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            nested);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken));

        StringAssert.Contains(exception.Message, "stale or out of order");
        Assert.IsTrue(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current));
        Assert.AreSame(nested, current);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(nestedToken);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken);
    }

    [TestMethod]
    public void DiscardAll_AfterException_ClearsReferences()
    {
        var generation = new GameSessionGeneration(1);
        var outerToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            new SlotTestContext("outer"));
        ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            new SlotTestContext("nested"));

        ThreadConfinedSessionSlot<SlotTestContext>.DiscardAll();

        Assert.IsFalse(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken));
    }

    [TestMethod]
    public void Enter_WhenGameSessionGenerationChanges_DiscardsOldThreadStateBeforeUse()
    {
        var oldToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            new GameSessionGeneration(1),
            new SlotTestContext("old game"));
        var currentGameContext = new SlotTestContext("current game");

        var currentToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            new GameSessionGeneration(2),
            currentGameContext);

        Assert.IsTrue(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current));
        Assert.AreSame(currentGameContext, current);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(currentToken);
        Assert.IsFalse(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Exit(oldToken));
    }

    [TestMethod]
    public void Enter_WhenGameSessionGenerationIsDefault_ThrowsWithoutPublishingContext()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Enter(
                default(GameSessionGeneration),
                new SlotTestContext("invalid")));

        Assert.IsFalse(
            ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _));
    }

    private sealed class SlotTestContext
    {
        internal SlotTestContext(string semanticName)
        {
            SemanticName = semanticName;
        }

        internal string SemanticName { get; }
    }
}
