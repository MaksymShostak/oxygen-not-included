namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;

[TestClass]
public sealed class SessionDiagnosticLimiterTests
{
    [TestMethod]
    public void ShouldEmit_WhenDiagnosticKeyOccursForFirstTime_ReturnsTrue()
    {
        var limiter = new SessionDiagnosticLimiter();

        Assert.IsTrue(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
    }

    [TestMethod]
    public void ShouldEmit_WhenDiagnosticKeyRepeats_ReturnsFalse()
    {
        var limiter = new SessionDiagnosticLimiter();

        Assert.IsTrue(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
        Assert.IsFalse(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
    }

    [TestMethod]
    public void ShouldEmit_WhenDifferentDiagnosticKeyOccurs_ReturnsTrue()
    {
        var limiter = new SessionDiagnosticLimiter();
        Assert.IsTrue(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));

        Assert.IsTrue(limiter.ShouldEmit("DTL_WORLD_UNRESOLVED"));
    }

    [TestMethod]
    public void NewSessionLimiter_WhenDiagnosticKeyOccurredInPriorSession_ReturnsTrue()
    {
        var priorSessionLimiter = new SessionDiagnosticLimiter();
        var newSessionLimiter = new SessionDiagnosticLimiter();
        Assert.IsTrue(priorSessionLimiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
        Assert.IsFalse(priorSessionLimiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));

        Assert.IsTrue(newSessionLimiter.ShouldEmit("DTL_FASTTRACK_ANCHOR"));
    }

    [TestMethod]
    public async Task ShouldEmit_WhenSameDiagnosticKeyArrivesConcurrently_PermitsExactlyOneEmission()
    {
        const int concurrentCallerCount = 64;
        var limiter = new SessionDiagnosticLimiter();
        using var startSignal = new ManualResetEventSlim(initialState: false);
        var permittedEmissionCount = 0;
        var callers = new Task[concurrentCallerCount];

        for (var callerIndex = 0;
             callerIndex < callers.Length;
             callerIndex++)
        {
            callers[callerIndex] = Task.Run(() =>
            {
                startSignal.Wait();
                if (limiter.ShouldEmit("DTL_WORLD_UNRESOLVED"))
                {
                    Interlocked.Increment(ref permittedEmissionCount);
                }
            });
        }

        startSignal.Set();
        await Task.WhenAll(callers);

        Assert.AreEqual(1, permittedEmissionCount);
    }

    [TestMethod]
    public void ShouldEmit_WhenDiagnosticKeyIsNull_ThrowsArgumentNullException()
    {
        var limiter = new SessionDiagnosticLimiter();

        Assert.ThrowsExactly<ArgumentNullException>(() => limiter.ShouldEmit(null!));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void ShouldEmit_WhenDiagnosticKeyIsNotMeaningful_ThrowsArgumentException(
        string diagnosticKey)
    {
        var limiter = new SessionDiagnosticLimiter();

        Assert.ThrowsExactly<ArgumentException>(() => limiter.ShouldEmit(diagnosticKey));
    }
}
