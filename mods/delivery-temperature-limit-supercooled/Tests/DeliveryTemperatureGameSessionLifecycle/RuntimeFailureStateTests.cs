namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;
public sealed class RuntimeFailureStateTests
{
    [Test]
    public async Task RuntimeFailure_StopsSessionAdmissionAndCannotRestartWithinTheProcess()
    {
        // Load the real linked production host with isolated static state. No
        // production reset hook may reactivate a runtime that has failed.
        var context = new System.Runtime.Loader.AssemblyLoadContext("failure-containment", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(RuntimeFailureStateTests).Assembly.Location);
            var host = assembly.GetType("DeliveryTemperatureLimit.DeliveryTemperatureGameSessionHost", throwOnError: true)!;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var ensure = host.GetMethod("EnsureGameSession", flags)!;
            var disable = host.GetMethod("TryDisableRuntime", flags)!;
            var capture = host.GetMethod("TryCaptureCurrent", flags)!;
            Assert.NotNull(ensure.Invoke(null, [1234]));
            await Assert.That((bool)capture.Invoke(null, [null])!).IsTrue();
            await Assert.That((bool)disable.Invoke(null, null)!).IsTrue();
            await Assert.That((bool)capture.Invoke(null, [null])!).IsFalse();
            await Assert.That((bool)disable.Invoke(null, null)!).IsFalse();
            var exception = (await Assert.That(() => ensure.Invoke(null, [1234])).Throws<System.Reflection.TargetInvocationException>())!;
            await Assert.That(exception.InnerException).IsAssignableTo<InvalidOperationException>();
            var session = host.GetMethod("DetachGameSession", flags)!.Invoke(null, [1234]);
            Assert.NotNull(session);
            host.GetMethod("CompleteShutdown", flags)!.Invoke(null, [session]);
        }
        finally { context.Unload(); }
    }

    [Test]
    public async Task Failure_IsRecordedPermanentlyAndQueuesOnlyOneWarning()
    {
        var state = new RuntimeFailureState();
        await Assert.That(state.HasFailed).IsFalse();
        await Assert.That(state.HasPendingWarning).IsFalse();
        await Assert.That(state.TryTakeWarning()).IsFalse();
        await Assert.That(state.TryRecordFailure()).IsTrue();
        await Assert.That(state.HasFailed).IsTrue();
        await Assert.That(state.HasPendingWarning).IsTrue();
        await Assert.That(state.TryRecordFailure()).IsFalse();
        await Assert.That(state.TryTakeWarning()).IsTrue();
        await Assert.That(state.TryTakeWarning()).IsFalse();
        await Assert.That(state.HasPendingWarning).IsFalse();
        await Assert.That(state.HasFailed).IsTrue();
    }

    [Test]
    public async Task ConcurrentFailures_ProduceOneFailureTransitionAndOneWarning()
    {
        var state = new RuntimeFailureState();
        int transitions = 0;
        Parallel.For(0, 64, _ =>
        {
            if (state.TryRecordFailure()) Interlocked.Increment(ref transitions);
        });
        await Assert.That(transitions).IsEqualTo(1);
        int warnings = 0;
        Parallel.For(0, 64, _ =>
        {
            if (state.TryTakeWarning()) Interlocked.Increment(ref warnings);
        });
        await Assert.That(warnings).IsEqualTo(1);
        await Assert.That(state.HasFailed).IsTrue();
    }
}
