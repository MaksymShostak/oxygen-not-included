namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;

[TestClass]
public sealed class RuntimeFailureStateTests
{
    [TestMethod]
    public void RuntimeFailure_StopsSessionAdmissionAndCannotRestartWithinTheProcess()
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
            Assert.IsNotNull(ensure.Invoke(null, [1234]));
            Assert.AreEqual(true, capture.Invoke(null, [null]));
            Assert.AreEqual(true, disable.Invoke(null, null));
            Assert.AreEqual(false, capture.Invoke(null, [null]));
            Assert.AreEqual(false, disable.Invoke(null, null));
            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => ensure.Invoke(null, [1234]));
            Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
            var session = host.GetMethod("DetachGameSession", flags)!.Invoke(null, [1234]);
            Assert.IsNotNull(session);
            host.GetMethod("CompleteShutdown", flags)!.Invoke(null, [session]);
        }
        finally { context.Unload(); }
    }

    [TestMethod]
    public void Failure_IsRecordedPermanentlyAndQueuesOnlyOneWarning()
    {
        var state = new RuntimeFailureState();
        Assert.IsFalse(state.HasFailed);
        Assert.IsFalse(state.HasPendingWarning);
        Assert.IsFalse(state.TryTakeWarning());
        Assert.IsTrue(state.TryRecordFailure());
        Assert.IsTrue(state.HasFailed);
        Assert.IsTrue(state.HasPendingWarning);
        Assert.IsFalse(state.TryRecordFailure());
        Assert.IsTrue(state.TryTakeWarning());
        Assert.IsFalse(state.TryTakeWarning());
        Assert.IsFalse(state.HasPendingWarning);
        Assert.IsTrue(state.HasFailed);
    }

    [TestMethod]
    public void ConcurrentFailures_ProduceOneFailureTransitionAndOneWarning()
    {
        var state = new RuntimeFailureState();
        int transitions = 0;
        Parallel.For(0, 64, _ =>
        {
            if (state.TryRecordFailure()) Interlocked.Increment(ref transitions);
        });
        Assert.AreEqual(1, transitions);
        int warnings = 0;
        Parallel.For(0, 64, _ =>
        {
            if (state.TryTakeWarning()) Interlocked.Increment(ref warnings);
        });
        Assert.AreEqual(1, warnings);
        Assert.IsTrue(state.HasFailed);
    }
}
