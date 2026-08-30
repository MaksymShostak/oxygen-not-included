using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;

[TestClass]
public sealed class TemperatureConstraintRegistryTests
{
    private const int RandomizedOperationSeed = 0x51A7E;
    private const int RandomizedOperationCount = 50000;
    private const int RandomizedComponentIdentityCount = 2048;
    private const int RandomizedSnapshotAssertionInterval = 97;

    [TestMethod]
    public void Register_WhenIdentityIsNew_AddsConstraintAndIncrementsGenerationOnce()
    {
        var registry = new TemperatureConstraintRegistry();

        var registration = registry.Register(
            41,
            Constraint(10, 20),
            out var effectiveStateChanged);
        var snapshot = registry.CaptureSnapshot();

        Assert.AreEqual(41, registration.ComponentInstanceId);
        Assert.AreEqual(1L, registration.RegistrationSequence);
        Assert.IsTrue(effectiveStateChanged);
        Assert.AreEqual(new TemperatureConstraintGeneration(1), snapshot.Generation);
        Assert.AreEqual(1, snapshot.EnabledConstraintCount);
        Assert.AreEqual(1, snapshot.EnabledNonEmptyConstraintCount);
        AssertEndpoints(snapshot, 10, 20);
    }

    [TestMethod]
    public void Register_WhenConstraintIsIdentical_ReturnsExistingRegistrationWithoutGenerationChange()
    {
        var registry = new TemperatureConstraintRegistry();
        var firstRegistration = registry.Register(41, Constraint(10, 20), out _);
        var snapshotBeforeRepeat = registry.CaptureSnapshot();

        var repeatedRegistration = registry.Register(
            41,
            Constraint(10, 20),
            out var effectiveStateChanged);

        Assert.AreEqual(firstRegistration, repeatedRegistration);
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreSame(snapshotBeforeRepeat, registry.CaptureSnapshot());
    }

    [TestMethod]
    public void Register_WhenConstraintDiffers_ReplacesEntryAndUpdatesEndpointCounts()
    {
        var registry = new TemperatureConstraintRegistry();
        var replacedRegistration = registry.Register(41, Constraint(10, 20), out _);
        var sharedEndpointRegistration = registry.Register(
            42,
            Constraint(10, 30),
            out _);

        var replacementRegistration = registry.Register(
            41,
            Constraint(30, 40),
            out var effectiveStateChanged);
        var replacementSnapshot = registry.CaptureSnapshot();

        Assert.AreNotEqual(replacedRegistration, replacementRegistration);
        Assert.IsTrue(effectiveStateChanged);
        Assert.AreEqual(new TemperatureConstraintGeneration(3), replacementSnapshot.Generation);
        Assert.AreEqual(2, replacementSnapshot.EnabledConstraintCount);
        Assert.AreEqual(2, replacementSnapshot.EnabledNonEmptyConstraintCount);
        AssertEndpoints(replacementSnapshot, 10, 30, 40);

        Assert.IsFalse(registry.TryRemove(replacedRegistration, out effectiveStateChanged));
        Assert.IsFalse(effectiveStateChanged);
        Assert.IsTrue(registry.TryRemove(sharedEndpointRegistration, out effectiveStateChanged));
        Assert.IsTrue(effectiveStateChanged);
        AssertEndpoints(registry.CaptureSnapshot(), 30, 40);
    }

    [TestMethod]
    public void Register_WhenConstraintIsDisabled_DoesNotAddEndpoints()
    {
        var registry = new TemperatureConstraintRegistry();

        registry.Register(41, Constraint(400, 0), out var effectiveStateChanged);
        var snapshot = registry.CaptureSnapshot();

        Assert.IsTrue(effectiveStateChanged);
        Assert.AreEqual(0, snapshot.EnabledConstraintCount);
        Assert.AreEqual(0, snapshot.EnabledNonEmptyConstraintCount);
        Assert.IsEmpty(snapshot.SortedDecisionEndpointsKelvin);
    }

    [TestMethod]
    public void Register_WhenConstraintIsEnabledButEmpty_CountsActiveWithoutAddingEndpoints()
    {
        var registry = new TemperatureConstraintRegistry();

        registry.Register(41, Constraint(100, 100), out var effectiveStateChanged);
        var snapshot = registry.CaptureSnapshot();

        Assert.IsTrue(effectiveStateChanged);
        Assert.AreEqual(1, snapshot.EnabledConstraintCount);
        Assert.AreEqual(0, snapshot.EnabledNonEmptyConstraintCount);
        Assert.IsEmpty(snapshot.SortedDecisionEndpointsKelvin);
    }

    [TestMethod]
    public void TryReplace_WhenConstraintIsIdentical_IsNoOp()
    {
        var registry = new TemperatureConstraintRegistry();
        var registration = registry.Register(41, Constraint(10, 20), out _);
        var snapshotBeforeReplacement = registry.CaptureSnapshot();

        var registrationFound = registry.TryReplace(
            registration,
            Constraint(10, 20),
            out var effectiveStateChanged);

        Assert.IsTrue(registrationFound);
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreSame(snapshotBeforeReplacement, registry.CaptureSnapshot());
    }

    [TestMethod]
    public void TryReplace_WhenRegistrationIsUnknown_ReturnsFalse()
    {
        var registry = new TemperatureConstraintRegistry();
        var snapshotBeforeReplacement = registry.CaptureSnapshot();
        var unknownRegistration = new TemperatureConstraintRegistrationToken(41, 1);

        var registrationFound = registry.TryReplace(
            unknownRegistration,
            Constraint(10, 20),
            out var effectiveStateChanged);

        Assert.IsFalse(registrationFound);
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreSame(snapshotBeforeReplacement, registry.CaptureSnapshot());
    }

    [TestMethod]
    public void TryRemove_WhenRegistrationIsUnknown_IsIdempotent()
    {
        var registry = new TemperatureConstraintRegistry();
        var snapshotBeforeRemoval = registry.CaptureSnapshot();
        var unknownRegistration = new TemperatureConstraintRegistrationToken(41, 1);

        Assert.IsFalse(registry.TryRemove(
            unknownRegistration,
            out var effectiveStateChanged));
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreSame(snapshotBeforeRemoval, registry.CaptureSnapshot());

        Assert.IsFalse(registry.TryRemove(
            unknownRegistration,
            out effectiveStateChanged));
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreSame(snapshotBeforeRemoval, registry.CaptureSnapshot());
    }

    [TestMethod]
    public void TryRemove_WhenRegistrationTokenIsStale_DoesNotRemoveReplacement()
    {
        var registry = new TemperatureConstraintRegistry();
        var first = registry.Register(41, Constraint(10, 20), out _);
        var replacement = registry.Register(41, Constraint(30, 40), out _);

        Assert.IsFalse(registry.TryRemove(first, out var effectiveStateChanged));
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreEqual(1, registry.CaptureSnapshot().EnabledConstraintCount);
        Assert.IsTrue(registry.TryRemove(replacement, out effectiveStateChanged));
        Assert.IsTrue(effectiveStateChanged);
    }

    [TestMethod]
    public void CaptureSnapshot_WhenEndpointsHaveDuplicates_ContainsEachEndpointOnceSorted()
    {
        var registry = new TemperatureConstraintRegistry();
        registry.Register(41, Constraint(10, 20), out _);
        registry.Register(42, Constraint(10, 20), out _);
        registry.Register(43, Constraint(10, 30), out _);

        AssertEndpoints(registry.CaptureSnapshot(), 10, 20, 30);
    }

    [TestMethod]
    public void CaptureSnapshot_WhenEndpointsSpanMembershipWords_EmitsAscendingBoundaryValues()
    {
        var registry = new TemperatureConstraintRegistry();
        registry.Register(41, Constraint(0, 63), out _);
        registry.Register(42, Constraint(64, 65), out _);
        registry.Register(
            43,
            Constraint(
                OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1,
                OniStorableTemperatureBounds.MaximumTemperatureKelvin),
            out _);

        AssertEndpoints(
            registry.CaptureSnapshot(),
            0,
            63,
            64,
            65,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin);
    }

    [TestMethod]
    public void CaptureSnapshot_AfterLastReferenceRemoved_RemovesEndpoint()
    {
        var registry = new TemperatureConstraintRegistry();
        var first = registry.Register(41, Constraint(10, 20), out _);
        var second = registry.Register(42, Constraint(10, 20), out _);
        var endpointsWithTwoOwners =
            registry.CaptureSnapshot().SortedDecisionEndpointsKelvin;

        Assert.IsTrue(registry.TryRemove(first, out _));
        Assert.AreSame(
            endpointsWithTwoOwners,
            registry.CaptureSnapshot().SortedDecisionEndpointsKelvin);

        Assert.IsTrue(registry.TryRemove(second, out _));
        Assert.IsEmpty(registry.CaptureSnapshot().SortedDecisionEndpointsKelvin);
    }

    [TestMethod]
    public void CaptureSnapshot_WhenEndpointMembershipIsUnchanged_ReusesSortedEndpointReference()
    {
        var registry = new TemperatureConstraintRegistry();
        var first = registry.Register(41, Constraint(10, 20), out _);
        var firstSnapshot = registry.CaptureSnapshot();

        registry.Register(42, Constraint(10, 20), out _);
        var secondSnapshot = registry.CaptureSnapshot();

        Assert.AreNotSame(firstSnapshot, secondSnapshot);
        Assert.AreSame(
            firstSnapshot.SortedDecisionEndpointsKelvin,
            secondSnapshot.SortedDecisionEndpointsKelvin);

        Assert.IsTrue(registry.TryRemove(first, out _));
        var thirdSnapshot = registry.CaptureSnapshot();
        Assert.AreNotSame(secondSnapshot, thirdSnapshot);
        Assert.AreSame(
            secondSnapshot.SortedDecisionEndpointsKelvin,
            thirdSnapshot.SortedDecisionEndpointsKelvin);
    }

    [TestMethod]
    public void CaptureSnapshot_WhenCallerMutatesReturnedView_CannotMutateRegistryState()
    {
        var registry = new TemperatureConstraintRegistry();
        registry.Register(41, Constraint(10, 20), out _);
        var snapshot = registry.CaptureSnapshot();
        var returnedView = snapshot.SortedDecisionEndpointsKelvin;

        Assert.IsFalse(returnedView is int[]);
        if (returnedView is IList<int> listView)
        {
            Assert.ThrowsExactly<NotSupportedException>(() => listView[0] = 999);
        }

        AssertEndpoints(snapshot, 10, 20);
        Assert.AreSame(snapshot, registry.CaptureSnapshot());
        AssertEndpoints(registry.CaptureSnapshot(), 10, 20);
    }

    [TestMethod]
    public void Register_WhenRegistrationSequenceIsExhausted_ThrowsBeforeMutation()
    {
        var registry = new TemperatureConstraintRegistry();
        var survivingRegistration = registry.Register(41, Constraint(10, 20), out _);
        var snapshotBeforeFailure = registry.CaptureSnapshot();
        SetPrivateInt64Field(registry, "nextRegistrationSequence", long.MaxValue);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Register(41, Constraint(30, 40), out _));

        StringAssert.Contains(exception.Message, "registration sequence");
        Assert.AreEqual(
            long.MaxValue,
            ReadPrivateInt64Field(registry, "nextRegistrationSequence"));
        Assert.AreSame(snapshotBeforeFailure, registry.CaptureSnapshot());
        AssertEndpoints(registry.CaptureSnapshot(), 10, 20);

        SetPrivateInt64Field(
            registry,
            "nextRegistrationSequence",
            survivingRegistration.RegistrationSequence);
        Assert.IsTrue(registry.TryRemove(survivingRegistration, out var changed));
        Assert.IsTrue(changed);
    }

    [TestMethod]
    public void TryReplace_WhenGenerationIsExhausted_ThrowsBeforeMutation()
    {
        var registry = new TemperatureConstraintRegistry();
        var originalConstraint = Constraint(10, 20);
        var replacementConstraint = Constraint(30, 40);
        var registration = registry.Register(41, originalConstraint, out _);
        var snapshotBeforeFailure = registry.CaptureSnapshot();
        SetPrivateInt64Field(registry, "generation", long.MaxValue);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.TryReplace(registration, replacementConstraint, out _));

        StringAssert.Contains(exception.Message, "constraint generation");
        Assert.AreEqual(long.MaxValue, ReadPrivateInt64Field(registry, "generation"));
        Assert.AreSame(snapshotBeforeFailure, registry.CaptureSnapshot());
        AssertEndpoints(registry.CaptureSnapshot(), 10, 20);

        SetPrivateInt64Field(
            registry,
            "generation",
            snapshotBeforeFailure.Generation.Value);
        Assert.IsTrue(registry.TryReplace(
            registration,
            originalConstraint,
            out var effectiveStateChanged));
        Assert.IsFalse(effectiveStateChanged);
        Assert.AreSame(snapshotBeforeFailure, registry.CaptureSnapshot());

        Assert.IsTrue(registry.TryReplace(
            registration,
            replacementConstraint,
            out effectiveStateChanged));
        Assert.IsTrue(effectiveStateChanged);
        AssertEndpoints(registry.CaptureSnapshot(), 30, 40);
    }

    [TestMethod]
    public void EndpointReferenceCountStorage_WhenSizedForCurrentOniBound_UsesReviewedFixedMemory()
    {
        var endpointCount =
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;
        var endpointReferenceCountElementStorageBytes = endpointCount * sizeof(int);

        Assert.AreEqual(10001, endpointCount);
        Assert.AreEqual(40004, endpointReferenceCountElementStorageBytes);
    }

    [TestMethod]
    public void RegistryOperations_WhenRandomized_MatchDeterministicReferenceModel()
    {
        var registry = new TemperatureConstraintRegistry();
        var referenceEntriesByComponentInstanceId =
            new Dictionary<int, ReferenceRegistryEntry>();
        var random = new Random(RandomizedOperationSeed);
        long expectedGeneration = 0;

        for (var operationIndex = 0;
             operationIndex < RandomizedOperationCount;
             operationIndex++)
        {
            var componentInstanceId = random.Next(RandomizedComponentIdentityCount);
            switch (random.Next(3))
            {
                case 0:
                    ApplyRandomizedRegistration(
                        registry,
                        referenceEntriesByComponentInstanceId,
                        random,
                        componentInstanceId,
                        operationIndex,
                        ref expectedGeneration);
                    break;

                case 1:
                    ApplyRandomizedReplacement(
                        registry,
                        referenceEntriesByComponentInstanceId,
                        random,
                        componentInstanceId,
                        operationIndex,
                        ref expectedGeneration);
                    break;

                default:
                    ApplyRandomizedRemoval(
                        registry,
                        referenceEntriesByComponentInstanceId,
                        random,
                        componentInstanceId,
                        operationIndex,
                        ref expectedGeneration);
                    break;
            }

            if (operationIndex % RandomizedSnapshotAssertionInterval == 0)
            {
                AssertSnapshotMatchesReferenceModel(
                    registry,
                    referenceEntriesByComponentInstanceId,
                    expectedGeneration,
                    operationIndex);
            }
        }

        AssertSnapshotMatchesReferenceModel(
            registry,
            referenceEntriesByComponentInstanceId,
            expectedGeneration,
            RandomizedOperationCount);
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static void AssertEndpoints(
        ActiveTemperatureConstraintSnapshot snapshot,
        params int[] expectedEndpointsKelvin)
    {
        var observedEndpointsKelvin = CopyEndpoints(
            snapshot.SortedDecisionEndpointsKelvin);
        Assert.AreSequenceEqual(expectedEndpointsKelvin, observedEndpointsKelvin);
    }

    private static int[] CopyEndpoints(IReadOnlyList<int> endpointsKelvin)
    {
        var copiedEndpointsKelvin = new int[endpointsKelvin.Count];
        for (var endpointIndex = 0; endpointIndex < endpointsKelvin.Count; endpointIndex++)
        {
            copiedEndpointsKelvin[endpointIndex] = endpointsKelvin[endpointIndex];
        }

        return copiedEndpointsKelvin;
    }

    private static void ApplyRandomizedRegistration(
        TemperatureConstraintRegistry registry,
        IDictionary<int, ReferenceRegistryEntry> referenceEntriesByComponentInstanceId,
        Random random,
        int componentInstanceId,
        int operationIndex,
        ref long expectedGeneration)
    {
        var constraint = CreateRandomConstraint(random);
        var hadExistingEntry = referenceEntriesByComponentInstanceId.TryGetValue(
            componentInstanceId,
            out var existingEntry);
        if (hadExistingEntry && existingEntry is null)
        {
            throw new InvalidOperationException(
                "The randomized reference registry cannot contain a null entry.");
        }

        var expectedEffectiveStateChanged =
            existingEntry is null ||
            !existingEntry.Constraint.Equals(constraint);
        var observedRegistration = registry.Register(
            componentInstanceId,
            constraint,
            out var observedEffectiveStateChanged);
        var assertionContext = RandomizedAssertionContext(operationIndex);

        Assert.AreEqual(
            expectedEffectiveStateChanged,
            observedEffectiveStateChanged,
            assertionContext);
        if (expectedEffectiveStateChanged)
        {
            expectedGeneration++;
            if (existingEntry is not null)
            {
                Assert.AreNotEqual(
                    existingEntry.RegistrationToken,
                    observedRegistration,
                    assertionContext);
            }

            referenceEntriesByComponentInstanceId[componentInstanceId] =
                new ReferenceRegistryEntry(observedRegistration, constraint);
        }
        else
        {
            if (existingEntry is null)
            {
                throw new InvalidOperationException(
                    "An unchanged registration requires its existing reference entry.");
            }

            Assert.AreEqual(
                existingEntry.RegistrationToken,
                observedRegistration,
                assertionContext);
        }
    }

    private static void ApplyRandomizedReplacement(
        TemperatureConstraintRegistry registry,
        IDictionary<int, ReferenceRegistryEntry> referenceEntriesByComponentInstanceId,
        Random random,
        int componentInstanceId,
        int operationIndex,
        ref long expectedGeneration)
    {
        var constraint = CreateRandomConstraint(random);
        var assertionContext = RandomizedAssertionContext(operationIndex);
        if (referenceEntriesByComponentInstanceId.TryGetValue(
                componentInstanceId,
                out var existingEntry) &&
            random.Next(4) != 0)
        {
            var expectedEffectiveStateChanged =
                !existingEntry.Constraint.Equals(constraint);
            var registrationFound = registry.TryReplace(
                existingEntry.RegistrationToken,
                constraint,
                out var observedEffectiveStateChanged);

            Assert.IsTrue(registrationFound, assertionContext);
            Assert.AreEqual(
                expectedEffectiveStateChanged,
                observedEffectiveStateChanged,
                assertionContext);
            if (expectedEffectiveStateChanged)
            {
                expectedGeneration++;
                referenceEntriesByComponentInstanceId[componentInstanceId] =
                    new ReferenceRegistryEntry(existingEntry.RegistrationToken, constraint);
            }

            return;
        }

        var staleOrUnknownRegistration = new TemperatureConstraintRegistrationToken(
            componentInstanceId,
            long.MaxValue - operationIndex);
        Assert.IsFalse(
            registry.TryReplace(
                staleOrUnknownRegistration,
                constraint,
                out var effectiveStateChanged),
            assertionContext);
        Assert.IsFalse(effectiveStateChanged, assertionContext);
    }

    private static void ApplyRandomizedRemoval(
        TemperatureConstraintRegistry registry,
        IDictionary<int, ReferenceRegistryEntry> referenceEntriesByComponentInstanceId,
        Random random,
        int componentInstanceId,
        int operationIndex,
        ref long expectedGeneration)
    {
        var assertionContext = RandomizedAssertionContext(operationIndex);
        if (referenceEntriesByComponentInstanceId.TryGetValue(
                componentInstanceId,
                out var existingEntry) &&
            random.Next(4) != 0)
        {
            Assert.IsTrue(
                registry.TryRemove(
                    existingEntry.RegistrationToken,
                    out var knownOwnerEffectiveStateChanged),
                assertionContext);
            Assert.IsTrue(knownOwnerEffectiveStateChanged, assertionContext);
            referenceEntriesByComponentInstanceId.Remove(componentInstanceId);
            expectedGeneration++;
            return;
        }

        var staleOrUnknownRegistration = new TemperatureConstraintRegistrationToken(
            componentInstanceId,
            long.MaxValue - operationIndex);
        Assert.IsFalse(
            registry.TryRemove(
                staleOrUnknownRegistration,
                out var staleOwnerEffectiveStateChanged),
            assertionContext);
        Assert.IsFalse(staleOwnerEffectiveStateChanged, assertionContext);
    }

    private static DeliveryTemperatureConstraint CreateRandomConstraint(Random random)
    {
        var serializedLowLimit = random.Next(
            OniStorableTemperatureBounds.MinimumTemperatureKelvin - 100,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 101);
        var serializedHighLimit = random.Next(
            OniStorableTemperatureBounds.MinimumTemperatureKelvin - 100,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 101);
        return DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit,
            serializedHighLimit);
    }

    private static void AssertSnapshotMatchesReferenceModel(
        TemperatureConstraintRegistry registry,
        IReadOnlyDictionary<int, ReferenceRegistryEntry>
            referenceEntriesByComponentInstanceId,
        long expectedGeneration,
        int operationIndex)
    {
        var expectedEndpointMembership = new bool[
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1];
        var expectedEnabledConstraintCount = 0;
        var expectedEnabledNonEmptyConstraintCount = 0;

        foreach (var referenceEntry in referenceEntriesByComponentInstanceId.Values)
        {
            var constraint = referenceEntry.Constraint;
            if (!constraint.IsEnabled)
            {
                continue;
            }

            expectedEnabledConstraintCount++;
            if (constraint.IsEmpty)
            {
                continue;
            }

            expectedEnabledNonEmptyConstraintCount++;
            expectedEndpointMembership[constraint.MinimumInclusiveKelvin] = true;
            expectedEndpointMembership[constraint.MaximumExclusiveKelvin] = true;
        }

        var expectedEndpointsKelvin = new List<int>();
        for (var endpointKelvin = 0;
             endpointKelvin < expectedEndpointMembership.Length;
             endpointKelvin++)
        {
            if (expectedEndpointMembership[endpointKelvin])
            {
                expectedEndpointsKelvin.Add(endpointKelvin);
            }
        }

        var snapshot = registry.CaptureSnapshot();
        var observedEndpointsKelvin = CopyEndpoints(
            snapshot.SortedDecisionEndpointsKelvin);
        var assertionContext = RandomizedAssertionContext(operationIndex);

        Assert.AreEqual(
            new TemperatureConstraintGeneration(expectedGeneration),
            snapshot.Generation,
            assertionContext);
        Assert.AreEqual(
            expectedEnabledConstraintCount,
            snapshot.EnabledConstraintCount,
            assertionContext);
        Assert.AreEqual(
            expectedEnabledNonEmptyConstraintCount,
            snapshot.EnabledNonEmptyConstraintCount,
            assertionContext);
        Assert.AreSequenceEqual(
            expectedEndpointsKelvin.ToArray(),
            observedEndpointsKelvin,
            assertionContext);
    }

    private static string RandomizedAssertionContext(int operationIndex) =>
        $"Seed=0x{RandomizedOperationSeed:X}; operation index={operationIndex}.";

    private static long ReadPrivateInt64Field(
        TemperatureConstraintRegistry registry,
        string exactFieldName)
    {
        var field = RequirePrivateInt64Field(exactFieldName);
        return Assert.IsInstanceOfType<long>(field.GetValue(registry));
    }

    private static void SetPrivateInt64Field(
        TemperatureConstraintRegistry registry,
        string exactFieldName,
        long value)
    {
        var field = RequirePrivateInt64Field(exactFieldName);
        field.SetValue(registry, value);
    }

    private static FieldInfo RequirePrivateInt64Field(string exactFieldName)
    {
        var field = typeof(TemperatureConstraintRegistry).GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private field " +
            $"TemperatureConstraintRegistry.{exactFieldName}.");
        Assert.AreEqual(typeof(long), field.FieldType);
        return field;
    }

    private sealed class ReferenceRegistryEntry
    {
        internal ReferenceRegistryEntry(
            TemperatureConstraintRegistrationToken registrationToken,
            DeliveryTemperatureConstraint constraint)
        {
            RegistrationToken = registrationToken;
            Constraint = constraint;
        }

        internal TemperatureConstraintRegistrationToken RegistrationToken { get; }

        internal DeliveryTemperatureConstraint Constraint { get; }
    }
}
