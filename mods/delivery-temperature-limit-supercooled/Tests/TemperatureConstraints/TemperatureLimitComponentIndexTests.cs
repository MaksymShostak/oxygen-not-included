namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;
public sealed class TemperatureLimitComponentIndexTests
{
    [Test]
    public async Task TryRegister_WhenEntryIsNew_PublishesComponentAndConstraint()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        var constraint = Constraint(10, 20);

        await Assert.That(index.TryRegister(
            gameObjectInstanceId: 77,
            component,
            registration,
            constraint)).IsTrue();

        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedComponentRegistration)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(component);
        await Assert.That(observedComponentRegistration).IsEqualTo(registration);

        await Assert.That(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedConstraintRegistration)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(constraint);
        await Assert.That(observedConstraintRegistration).IsEqualTo(registration);
    }

    [Test]
    public async Task TryRegister_WhenSameOwnerAndStateRepeats_IsIdempotent()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        var constraint = Constraint(10, 20);

        await Assert.That(index.TryRegister(77, component, registration, constraint)).IsTrue();
        await Assert.That(index.TryRegister(77, component, registration, constraint)).IsTrue();

        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(component);
        await Assert.That(observedRegistration).IsEqualTo(registration);
        await Assert.That(index.TryRemove(77, registration)).IsTrue();
        await Assert.That(index.TryRemove(77, registration)).IsFalse();
    }

    [Test]
    public async Task TryRegister_WhenOwnerReusesTokenWithDifferentState_ReturnsFalse()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        var originalConstraint = Constraint(10, 20);
        await Assert.That(index.TryRegister(
            77,
            component,
            registration,
            originalConstraint)).IsTrue();

        await Assert.That(index.TryRegister(
            77,
            component,
            registration,
            Constraint(30, 40))).IsFalse();

        await Assert.That(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedRegistration)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(originalConstraint);
        await Assert.That(observedRegistration).IsEqualTo(registration);
    }

    [Test]
    public async Task TryRegister_WhenDifferentOwnerUsesSameGameObjectId_ReplacesAtomically()
    {
        var index = new TemperatureLimitComponentIndex();
        var oldRegistration = Registration(componentId: 1, sequence: 10);
        var newRegistration = Registration(componentId: 2, sequence: 11);
        var oldComponent = new TemperatureLimit();
        var newComponent = new TemperatureLimit();

        await Assert.That(index.TryRegister(
            77,
            oldComponent,
            oldRegistration,
            Constraint(10, 20))).IsTrue();
        await Assert.That(index.TryRegister(
            77,
            newComponent,
            newRegistration,
            Constraint(30, 40))).IsTrue();

        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(newComponent);
        await Assert.That(observedRegistration).IsEqualTo(newRegistration);
        await Assert.That(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedConstraintRegistration)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(Constraint(30, 40));
        await Assert.That(observedConstraintRegistration).IsEqualTo(newRegistration);
    }

    [Test]
    public async Task TryReplaceConstraint_WhenTokenMatches_ChangesOnlyConstraint()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        await Assert.That(index.TryRegister(
            77,
            component,
            registration,
            Constraint(10, 20))).IsTrue();

        await Assert.That(index.TryReplaceConstraint(
            77,
            registration,
            Constraint(30, 40))).IsTrue();

        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(component);
        await Assert.That(observedRegistration).IsEqualTo(registration);
        await Assert.That(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedConstraintRegistration)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(Constraint(30, 40));
        await Assert.That(observedConstraintRegistration).IsEqualTo(registration);
    }

    [Test]
    public async Task TryReplaceConstraint_WhenTokenIsStale_LeavesEntryUnchanged()
    {
        var index = new TemperatureLimitComponentIndex();
        var currentComponent = new TemperatureLimit();
        var currentRegistration = Registration(componentId: 2, sequence: 11);
        var staleRegistration = Registration(componentId: 1, sequence: 10);
        var currentConstraint = Constraint(30, 40);
        await Assert.That(index.TryRegister(
            77,
            currentComponent,
            currentRegistration,
            currentConstraint)).IsTrue();

        await Assert.That(index.TryReplaceConstraint(
            77,
            staleRegistration,
            Constraint(50, 60))).IsFalse();

        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(currentComponent);
        await Assert.That(observedRegistration).IsEqualTo(currentRegistration);
        await Assert.That(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out _)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(currentConstraint);
    }

    [Test]
    public async Task TryGetConstraint_WhenGameObjectIsUnknown_ReturnsFalse()
    {
        var index = new TemperatureLimitComponentIndex();

        await Assert.That(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedRegistration)).IsFalse();
        await Assert.That(observedConstraint).IsEqualTo(default(DeliveryTemperatureConstraint));
        await Assert.That(observedRegistration).IsEqualTo(default(TemperatureConstraintRegistrationToken));
    }

    [Test]
    public async Task TryGetRegisteredComponent_WhenGameObjectIsUnknown_ReturnsFalse()
    {
        var index = new TemperatureLimitComponentIndex();

        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration)).IsFalse();
        await Assert.That(observedComponent).IsNull();
        await Assert.That(observedRegistration).IsEqualTo(default(TemperatureConstraintRegistrationToken));
    }

    [Test]
    public async Task TryRemove_WhenCalledTwice_IsIdempotent()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        await Assert.That(index.TryRegister(
            77,
            component,
            registration,
            Constraint(10, 20))).IsTrue();

        await Assert.That(index.TryRemove(77, registration)).IsTrue();
        await Assert.That(index.TryRemove(77, registration)).IsFalse();
        await Assert.That(index.TryGetRegisteredComponent(77, out _, out _)).IsFalse();
    }

    [Test]
    public async Task TryRemove_WhenGameObjectIdWasReused_DoesNotRemoveNewOwner()
    {
        var index = new TemperatureLimitComponentIndex();
        var oldRegistration = Registration(componentId: 1, sequence: 10);
        var newRegistration = Registration(componentId: 2, sequence: 11);
        var oldComponent = new TemperatureLimit();
        var newComponent = new TemperatureLimit();

        await Assert.That(index.TryRegister(
            77,
            oldComponent,
            oldRegistration,
            Constraint(10, 20))).IsTrue();
        await Assert.That(index.TryRegister(
            77,
            newComponent,
            newRegistration,
            Constraint(30, 40))).IsTrue();

        await Assert.That(index.TryRemove(77, oldRegistration)).IsFalse();
        await Assert.That(index.TryGetRegisteredComponent(
            77,
            out var component,
            out var survivingRegistration)).IsTrue();
        await Assert.That(component).IsSameReferenceAs(newComponent);
        await Assert.That(survivingRegistration).IsEqualTo(newRegistration);
    }

    [Test]
    public async Task ConcurrentReaders_WhenEntryIsReplaced_ObserveOnlyWholeOldOrWholeNewEntry()
    {
        const int writerIterationCount = 20000;
        const int readerIterationCount = 50000;
        const int readerCount = 4;
        var index = new TemperatureLimitComponentIndex();
        var oldRegistration = Registration(componentId: 1, sequence: 10);
        var newRegistration = Registration(componentId: 2, sequence: 11);
        var oldComponent = new TemperatureLimit();
        var newComponent = new TemperatureLimit();
        var oldConstraint = Constraint(10, 20);
        var newConstraint = Constraint(30, 40);
        using var startSignal = new ManualResetEventSlim(initialState: false);
        var invalidPairCount = 0;
        var missingEntryCount = 0;
        var failedReplacementCount = 0;

        await Assert.That(index.TryRegister(
            77,
            oldComponent,
            oldRegistration,
            oldConstraint)).IsTrue();

        var writer = Task.Run(() =>
        {
            startSignal.Wait();
            for (var iteration = 0; iteration < writerIterationCount; iteration++)
            {
                var publishNewEntry = (iteration & 1) == 0;
                if (!index.TryRegister(
                        77,
                        publishNewEntry ? newComponent : oldComponent,
                        publishNewEntry ? newRegistration : oldRegistration,
                        publishNewEntry ? newConstraint : oldConstraint))
                {
                    Interlocked.Increment(ref failedReplacementCount);
                }

                if ((iteration & 63) == 0)
                {
                    Thread.Yield();
                }
            }
        });

        var readers = new Task[readerCount];
        for (var readerIndex = 0; readerIndex < readers.Length; readerIndex++)
        {
            readers[readerIndex] = Task.Run(() =>
            {
                startSignal.Wait();
                for (var iteration = 0; iteration < readerIterationCount; iteration++)
                {
                    if (!index.TryGetRegisteredComponent(
                            77,
                            out var observedComponent,
                            out var observedRegistration))
                    {
                        Interlocked.Increment(ref missingEntryCount);
                        continue;
                    }

                    var observedWholeOldEntry =
                        ReferenceEquals(observedComponent, oldComponent) &&
                        observedRegistration.Equals(oldRegistration);
                    var observedWholeNewEntry =
                        ReferenceEquals(observedComponent, newComponent) &&
                        observedRegistration.Equals(newRegistration);
                    if (!observedWholeOldEntry && !observedWholeNewEntry)
                    {
                        Interlocked.Increment(ref invalidPairCount);
                    }

                    if ((iteration & 255) == 0)
                    {
                        Thread.Yield();
                    }
                }
            });
        }

        startSignal.Set();
        await writer;
        await Task.WhenAll(readers);

        await Assert.That(failedReplacementCount).IsEqualTo(0);
        await Assert.That(missingEntryCount).IsEqualTo(0);
        await Assert.That(invalidPairCount).IsEqualTo(0);
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static TemperatureConstraintRegistrationToken Registration(
        int componentId,
        long sequence) =>
        new TemperatureConstraintRegistrationToken(componentId, sequence);
}
