namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;
public sealed class SessionDiagnosticLimiterTests
{
    [Test]
    public async Task ShouldEmit_WhenDiagnosticKeyOccursForFirstTime_ReturnsTrue()
    {
        var limiter = new SessionDiagnosticLimiter();

        await Assert.That(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsTrue();
    }

    [Test]
    public async Task ShouldEmit_WhenDiagnosticKeyRepeats_ReturnsFalse()
    {
        var limiter = new SessionDiagnosticLimiter();

        await Assert.That(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsTrue();
        await Assert.That(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsFalse();
    }

    [Test]
    public async Task ShouldEmit_WhenDifferentDiagnosticKeyOccurs_ReturnsTrue()
    {
        var limiter = new SessionDiagnosticLimiter();
        await Assert.That(limiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsTrue();

        await Assert.That(limiter.ShouldEmit("DTL_WORLD_UNRESOLVED")).IsTrue();
    }

    [Test]
    public async Task NewSessionLimiter_WhenDiagnosticKeyOccurredInPriorSession_ReturnsTrue()
    {
        var priorSessionLimiter = new SessionDiagnosticLimiter();
        var newSessionLimiter = new SessionDiagnosticLimiter();
        await Assert.That(priorSessionLimiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsTrue();
        await Assert.That(priorSessionLimiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsFalse();

        await Assert.That(newSessionLimiter.ShouldEmit("DTL_FASTTRACK_ANCHOR")).IsTrue();
    }

    [Test]
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

        await Assert.That(permittedEmissionCount).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldEmit_WhenDiagnosticKeyIsNull_ThrowsArgumentNullException()
    {
        var limiter = new SessionDiagnosticLimiter();

        await Assert.That(() => limiter.ShouldEmit(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ShouldEmit_WhenDiagnosticKeyIsNotMeaningful_ThrowsArgumentException(
        string diagnosticKey)
    {
        var limiter = new SessionDiagnosticLimiter();

        await Assert.That(() => limiter.ShouldEmit(diagnosticKey)).ThrowsExactly<ArgumentException>();
    }
}
