namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;
public sealed class ThreadConfinedSessionSlotTests
{
    [Before(HookType.Test)]
    public void ClearCurrentTestThreadState()
    {
        ThreadConfinedSessionSlot<SlotTestContext>.DiscardAll();
    }

    [After(HookType.Test)]
    public void ReleaseCurrentTestThreadState()
    {
        ThreadConfinedSessionSlot<SlotTestContext>.DiscardAll();
    }

    [Test]
    public async Task Enter_WhenEmpty_SetsCurrent()
    {
        var context = new SlotTestContext("outer");

        var token = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            new GameSessionGeneration(1),
            context);

        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current)).IsTrue();
        await Assert.That(current).IsSameReferenceAs(context);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(token);
    }

    [Test]
    public async Task Enter_WhenNested_SavesPreviousAndSetsNested()
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

        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current)).IsTrue();
        await Assert.That(current).IsSameReferenceAs(nested);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(nestedToken);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken);
    }

    [Test]
    public async Task Exit_WhenNested_RestoresPrevious()
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

        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current)).IsTrue();
        await Assert.That(current).IsSameReferenceAs(outer);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken);
        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _)).IsFalse();
    }

    [Test]
    public async Task Exit_WhenTokenIsStale_ThrowsLifecycleViolation()
    {
        var generation = new GameSessionGeneration(1);
        var outerToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            new SlotTestContext("outer"));
        var nested = new SlotTestContext("nested");
        var nestedToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            nested);

        var exception = (await Assert.That(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("stale or out of order");
        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current)).IsTrue();
        await Assert.That(current).IsSameReferenceAs(nested);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(nestedToken);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken);
    }

    [Test]
    public async Task DiscardAll_AfterException_ClearsReferences()
    {
        var generation = new GameSessionGeneration(1);
        var outerToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            new SlotTestContext("outer"));
        ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            generation,
            new SlotTestContext("nested"));

        ThreadConfinedSessionSlot<SlotTestContext>.DiscardAll();

        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _)).IsFalse();
        await Assert.That(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Exit(outerToken)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Enter_WhenGameSessionGenerationChanges_DiscardsOldThreadStateBeforeUse()
    {
        var oldToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            new GameSessionGeneration(1),
            new SlotTestContext("old game"));
        var currentGameContext = new SlotTestContext("current game");

        var currentToken = ThreadConfinedSessionSlot<SlotTestContext>.Enter(
            new GameSessionGeneration(2),
            currentGameContext);

        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(
                out var current)).IsTrue();
        await Assert.That(current).IsSameReferenceAs(currentGameContext);
        ThreadConfinedSessionSlot<SlotTestContext>.Exit(currentToken);
        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _)).IsFalse();
        await Assert.That(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Exit(oldToken)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Enter_WhenGameSessionGenerationIsDefault_ThrowsWithoutPublishingContext()
    {
        await Assert.That(() =>
            ThreadConfinedSessionSlot<SlotTestContext>.Enter(
                default(GameSessionGeneration),
                new SlotTestContext("invalid"))).ThrowsExactly<ArgumentOutOfRangeException>();

        await Assert.That(ThreadConfinedSessionSlot<SlotTestContext>.TryGetCurrent(out _)).IsFalse();
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
