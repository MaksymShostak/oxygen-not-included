namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;

[TestClass]
public sealed class TemperatureLimitComponentIndexTests
{
    [TestMethod]
    public void TryRegister_WhenEntryIsNew_PublishesComponentAndConstraint()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        var constraint = Constraint(10, 20);

        Assert.IsTrue(index.TryRegister(
            gameObjectInstanceId: 77,
            component,
            registration,
            constraint));

        Assert.IsTrue(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedComponentRegistration));
        Assert.AreSame(component, observedComponent);
        Assert.AreEqual(registration, observedComponentRegistration);

        Assert.IsTrue(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedConstraintRegistration));
        Assert.AreEqual(constraint, observedConstraint);
        Assert.AreEqual(registration, observedConstraintRegistration);
    }

    [TestMethod]
    public void TryRegister_WhenSameOwnerAndStateRepeats_IsIdempotent()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        var constraint = Constraint(10, 20);

        Assert.IsTrue(index.TryRegister(77, component, registration, constraint));
        Assert.IsTrue(index.TryRegister(77, component, registration, constraint));

        Assert.IsTrue(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration));
        Assert.AreSame(component, observedComponent);
        Assert.AreEqual(registration, observedRegistration);
        Assert.IsTrue(index.TryRemove(77, registration));
        Assert.IsFalse(index.TryRemove(77, registration));
    }

    [TestMethod]
    public void TryRegister_WhenOwnerReusesTokenWithDifferentState_ReturnsFalse()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        var originalConstraint = Constraint(10, 20);
        Assert.IsTrue(index.TryRegister(
            77,
            component,
            registration,
            originalConstraint));

        Assert.IsFalse(index.TryRegister(
            77,
            component,
            registration,
            Constraint(30, 40)));

        Assert.IsTrue(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedRegistration));
        Assert.AreEqual(originalConstraint, observedConstraint);
        Assert.AreEqual(registration, observedRegistration);
    }

    [TestMethod]
    public void TryRegister_WhenDifferentOwnerUsesSameGameObjectId_ReplacesAtomically()
    {
        var index = new TemperatureLimitComponentIndex();
        var oldRegistration = Registration(componentId: 1, sequence: 10);
        var newRegistration = Registration(componentId: 2, sequence: 11);
        var oldComponent = new TemperatureLimit();
        var newComponent = new TemperatureLimit();

        Assert.IsTrue(index.TryRegister(
            77,
            oldComponent,
            oldRegistration,
            Constraint(10, 20)));
        Assert.IsTrue(index.TryRegister(
            77,
            newComponent,
            newRegistration,
            Constraint(30, 40)));

        Assert.IsTrue(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration));
        Assert.AreSame(newComponent, observedComponent);
        Assert.AreEqual(newRegistration, observedRegistration);
        Assert.IsTrue(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedConstraintRegistration));
        Assert.AreEqual(Constraint(30, 40), observedConstraint);
        Assert.AreEqual(newRegistration, observedConstraintRegistration);
    }

    [TestMethod]
    public void TryReplaceConstraint_WhenTokenMatches_ChangesOnlyConstraint()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        Assert.IsTrue(index.TryRegister(
            77,
            component,
            registration,
            Constraint(10, 20)));

        Assert.IsTrue(index.TryReplaceConstraint(
            77,
            registration,
            Constraint(30, 40)));

        Assert.IsTrue(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration));
        Assert.AreSame(component, observedComponent);
        Assert.AreEqual(registration, observedRegistration);
        Assert.IsTrue(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedConstraintRegistration));
        Assert.AreEqual(Constraint(30, 40), observedConstraint);
        Assert.AreEqual(registration, observedConstraintRegistration);
    }

    [TestMethod]
    public void TryReplaceConstraint_WhenTokenIsStale_LeavesEntryUnchanged()
    {
        var index = new TemperatureLimitComponentIndex();
        var currentComponent = new TemperatureLimit();
        var currentRegistration = Registration(componentId: 2, sequence: 11);
        var staleRegistration = Registration(componentId: 1, sequence: 10);
        var currentConstraint = Constraint(30, 40);
        Assert.IsTrue(index.TryRegister(
            77,
            currentComponent,
            currentRegistration,
            currentConstraint));

        Assert.IsFalse(index.TryReplaceConstraint(
            77,
            staleRegistration,
            Constraint(50, 60)));

        Assert.IsTrue(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration));
        Assert.AreSame(currentComponent, observedComponent);
        Assert.AreEqual(currentRegistration, observedRegistration);
        Assert.IsTrue(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out _));
        Assert.AreEqual(currentConstraint, observedConstraint);
    }

    [TestMethod]
    public void TryGetConstraint_WhenGameObjectIsUnknown_ReturnsFalse()
    {
        var index = new TemperatureLimitComponentIndex();

        Assert.IsFalse(index.TryGetConstraint(
            77,
            out var observedConstraint,
            out var observedRegistration));
        Assert.AreEqual(default(DeliveryTemperatureConstraint), observedConstraint);
        Assert.AreEqual(default(TemperatureConstraintRegistrationToken), observedRegistration);
    }

    [TestMethod]
    public void TryGetRegisteredComponent_WhenGameObjectIsUnknown_ReturnsFalse()
    {
        var index = new TemperatureLimitComponentIndex();

        Assert.IsFalse(index.TryGetRegisteredComponent(
            77,
            out var observedComponent,
            out var observedRegistration));
        Assert.IsNull(observedComponent);
        Assert.AreEqual(default(TemperatureConstraintRegistrationToken), observedRegistration);
    }

    [TestMethod]
    public void TryRemove_WhenCalledTwice_IsIdempotent()
    {
        var index = new TemperatureLimitComponentIndex();
        var component = new TemperatureLimit();
        var registration = Registration(componentId: 1, sequence: 10);
        Assert.IsTrue(index.TryRegister(
            77,
            component,
            registration,
            Constraint(10, 20)));

        Assert.IsTrue(index.TryRemove(77, registration));
        Assert.IsFalse(index.TryRemove(77, registration));
        Assert.IsFalse(index.TryGetRegisteredComponent(77, out _, out _));
    }

    [TestMethod]
    public void TryRemove_WhenGameObjectIdWasReused_DoesNotRemoveNewOwner()
    {
        var index = new TemperatureLimitComponentIndex();
        var oldRegistration = Registration(componentId: 1, sequence: 10);
        var newRegistration = Registration(componentId: 2, sequence: 11);
        var oldComponent = new TemperatureLimit();
        var newComponent = new TemperatureLimit();

        Assert.IsTrue(index.TryRegister(
            77,
            oldComponent,
            oldRegistration,
            Constraint(10, 20)));
        Assert.IsTrue(index.TryRegister(
            77,
            newComponent,
            newRegistration,
            Constraint(30, 40)));

        Assert.IsFalse(index.TryRemove(77, oldRegistration));
        Assert.IsTrue(index.TryGetRegisteredComponent(
            77,
            out var component,
            out var survivingRegistration));
        Assert.AreSame(newComponent, component);
        Assert.AreEqual(newRegistration, survivingRegistration);
    }

    [TestMethod]
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

        Assert.IsTrue(index.TryRegister(
            77,
            oldComponent,
            oldRegistration,
            oldConstraint));

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

        Assert.AreEqual(0, failedReplacementCount);
        Assert.AreEqual(0, missingEntryCount);
        Assert.AreEqual(0, invalidPairCount);
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
