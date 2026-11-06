using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.TemperatureConstraints;
public sealed class TemperatureConstraintRegistryTests
{
    private const int RandomizedOperationSeed = 0x51A7E;
    private const int RandomizedOperationCount = 50000;
    private const int RandomizedComponentIdentityCount = 2048;
    private const int RandomizedSnapshotAssertionInterval = 97;

    [Test]
    public async Task Register_WhenIdentityIsNew_AddsConstraintAndIncrementsGenerationOnce()
    {
        var registry = new TemperatureConstraintRegistry();

        var registration = registry.Register(
            41,
            Constraint(10, 20),
            out var effectiveStateChanged);
        var snapshot = registry.CaptureSnapshot();

        await Assert.That(registration.ComponentInstanceId).IsEqualTo(41);
        await Assert.That(registration.RegistrationSequence).IsEqualTo(1L);
        await Assert.That(effectiveStateChanged).IsTrue();
        await Assert.That(snapshot.Generation).IsEqualTo(new TemperatureConstraintGeneration(1));
        await Assert.That(snapshot.EnabledConstraintCount).IsEqualTo(1);
        await Assert.That(snapshot.EnabledNonEmptyConstraintCount).IsEqualTo(1);
        await AssertEndpoints(snapshot, 10, 20);
    }

    [Test]
    public async Task Register_WhenConstraintIsIdentical_ReturnsExistingRegistrationWithoutGenerationChange()
    {
        var registry = new TemperatureConstraintRegistry();
        var firstRegistration = registry.Register(41, Constraint(10, 20), out _);
        var snapshotBeforeRepeat = registry.CaptureSnapshot();

        var repeatedRegistration = registry.Register(
            41,
            Constraint(10, 20),
            out var effectiveStateChanged);

        await Assert.That(repeatedRegistration).IsEqualTo(firstRegistration);
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeRepeat);
    }

    [Test]
    public async Task Register_WhenConstraintDiffers_ReplacesEntryAndUpdatesEndpointCounts()
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

        await Assert.That(replacementRegistration).IsNotEqualTo(replacedRegistration);
        await Assert.That(effectiveStateChanged).IsTrue();
        await Assert.That(replacementSnapshot.Generation).IsEqualTo(new TemperatureConstraintGeneration(3));
        await Assert.That(replacementSnapshot.EnabledConstraintCount).IsEqualTo(2);
        await Assert.That(replacementSnapshot.EnabledNonEmptyConstraintCount).IsEqualTo(2);
        await AssertEndpoints(replacementSnapshot, 10, 30, 40);

        await Assert.That(registry.TryRemove(replacedRegistration, out effectiveStateChanged)).IsFalse();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.TryRemove(sharedEndpointRegistration, out effectiveStateChanged)).IsTrue();
        await Assert.That(effectiveStateChanged).IsTrue();
        await AssertEndpoints(registry.CaptureSnapshot(), 30, 40);
    }

    [Test]
    public async Task Register_WhenConstraintIsDisabled_DoesNotAddEndpoints()
    {
        var registry = new TemperatureConstraintRegistry();

        registry.Register(41, Constraint(400, 0), out var effectiveStateChanged);
        var snapshot = registry.CaptureSnapshot();

        await Assert.That(effectiveStateChanged).IsTrue();
        await Assert.That(snapshot.EnabledConstraintCount).IsEqualTo(0);
        await Assert.That(snapshot.EnabledNonEmptyConstraintCount).IsEqualTo(0);
        await Assert.That(snapshot.SortedDecisionEndpointsKelvin).IsEmpty();
    }

    [Test]
    public async Task Register_WhenConstraintIsEnabledButEmpty_CountsActiveWithoutAddingEndpoints()
    {
        var registry = new TemperatureConstraintRegistry();

        registry.Register(41, Constraint(100, 100), out var effectiveStateChanged);
        var snapshot = registry.CaptureSnapshot();

        await Assert.That(effectiveStateChanged).IsTrue();
        await Assert.That(snapshot.EnabledConstraintCount).IsEqualTo(1);
        await Assert.That(snapshot.EnabledNonEmptyConstraintCount).IsEqualTo(0);
        await Assert.That(snapshot.SortedDecisionEndpointsKelvin).IsEmpty();
    }

    [Test]
    public async Task TryReplace_WhenConstraintIsIdentical_IsNoOp()
    {
        var registry = new TemperatureConstraintRegistry();
        var registration = registry.Register(41, Constraint(10, 20), out _);
        var snapshotBeforeReplacement = registry.CaptureSnapshot();

        var registrationFound = registry.TryReplace(
            registration,
            Constraint(10, 20),
            out var effectiveStateChanged);

        await Assert.That(registrationFound).IsTrue();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeReplacement);
    }

    [Test]
    public async Task TryReplace_WhenRegistrationIsUnknown_ReturnsFalse()
    {
        var registry = new TemperatureConstraintRegistry();
        var snapshotBeforeReplacement = registry.CaptureSnapshot();
        var unknownRegistration = new TemperatureConstraintRegistrationToken(41, 1);

        var registrationFound = registry.TryReplace(
            unknownRegistration,
            Constraint(10, 20),
            out var effectiveStateChanged);

        await Assert.That(registrationFound).IsFalse();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeReplacement);
    }

    [Test]
    public async Task TryRemove_WhenRegistrationIsUnknown_IsIdempotent()
    {
        var registry = new TemperatureConstraintRegistry();
        var snapshotBeforeRemoval = registry.CaptureSnapshot();
        var unknownRegistration = new TemperatureConstraintRegistrationToken(41, 1);

        await Assert.That(registry.TryRemove(
            unknownRegistration,
            out var effectiveStateChanged)).IsFalse();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeRemoval);

        await Assert.That(registry.TryRemove(
            unknownRegistration,
            out effectiveStateChanged)).IsFalse();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeRemoval);
    }

    [Test]
    public async Task TryRemove_WhenRegistrationTokenIsStale_DoesNotRemoveReplacement()
    {
        var registry = new TemperatureConstraintRegistry();
        var first = registry.Register(41, Constraint(10, 20), out _);
        var replacement = registry.Register(41, Constraint(30, 40), out _);

        await Assert.That(registry.TryRemove(first, out var effectiveStateChanged)).IsFalse();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot().EnabledConstraintCount).IsEqualTo(1);
        await Assert.That(registry.TryRemove(replacement, out effectiveStateChanged)).IsTrue();
        await Assert.That(effectiveStateChanged).IsTrue();
    }

    [Test]
    public async Task CaptureSnapshot_WhenEndpointsHaveDuplicates_ContainsEachEndpointOnceSorted()
    {
        var registry = new TemperatureConstraintRegistry();
        registry.Register(41, Constraint(10, 20), out _);
        registry.Register(42, Constraint(10, 20), out _);
        registry.Register(43, Constraint(10, 30), out _);

        await AssertEndpoints(registry.CaptureSnapshot(), 10, 20, 30);
    }

    [Test]
    public async Task CaptureSnapshot_WhenEndpointsSpanMembershipWords_EmitsAscendingBoundaryValues()
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

        await AssertEndpoints(
            registry.CaptureSnapshot(),
            0,
            63,
            64,
            65,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin - 1,
            OniStorableTemperatureBounds.MaximumTemperatureKelvin);
    }

    [Test]
    public async Task CaptureSnapshot_AfterLastReferenceRemoved_RemovesEndpoint()
    {
        var registry = new TemperatureConstraintRegistry();
        var first = registry.Register(41, Constraint(10, 20), out _);
        var second = registry.Register(42, Constraint(10, 20), out _);
        var endpointsWithTwoOwners =
            registry.CaptureSnapshot().SortedDecisionEndpointsKelvin;

        await Assert.That(registry.TryRemove(first, out _)).IsTrue();
        await Assert.That(registry.CaptureSnapshot().SortedDecisionEndpointsKelvin).IsSameReferenceAs(endpointsWithTwoOwners);

        await Assert.That(registry.TryRemove(second, out _)).IsTrue();
        await Assert.That(registry.CaptureSnapshot().SortedDecisionEndpointsKelvin).IsEmpty();
    }

    [Test]
    public async Task CaptureSnapshot_WhenEndpointMembershipIsUnchanged_ReusesSortedEndpointReference()
    {
        var registry = new TemperatureConstraintRegistry();
        var first = registry.Register(41, Constraint(10, 20), out _);
        var firstSnapshot = registry.CaptureSnapshot();

        registry.Register(42, Constraint(10, 20), out _);
        var secondSnapshot = registry.CaptureSnapshot();

        await Assert.That(secondSnapshot).IsNotSameReferenceAs(firstSnapshot);
        await Assert.That(secondSnapshot.SortedDecisionEndpointsKelvin).IsSameReferenceAs(firstSnapshot.SortedDecisionEndpointsKelvin);

        await Assert.That(registry.TryRemove(first, out _)).IsTrue();
        var thirdSnapshot = registry.CaptureSnapshot();
        await Assert.That(thirdSnapshot).IsNotSameReferenceAs(secondSnapshot);
        await Assert.That(thirdSnapshot.SortedDecisionEndpointsKelvin).IsSameReferenceAs(secondSnapshot.SortedDecisionEndpointsKelvin);
    }

    [Test]
    public async Task CaptureSnapshot_WhenCallerMutatesReturnedView_CannotMutateRegistryState()
    {
        var registry = new TemperatureConstraintRegistry();
        registry.Register(41, Constraint(10, 20), out _);
        var snapshot = registry.CaptureSnapshot();
        var returnedView = snapshot.SortedDecisionEndpointsKelvin;

        await Assert.That(returnedView is int[]).IsFalse();
        if (returnedView is IList<int> listView)
        {
            await Assert.That(() => listView[0] = 999).ThrowsExactly<NotSupportedException>();
        }

        await AssertEndpoints(snapshot, 10, 20);
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshot);
        await AssertEndpoints(registry.CaptureSnapshot(), 10, 20);
    }

    [Test]
    public async Task Register_WhenRegistrationSequenceIsExhausted_ThrowsBeforeMutation()
    {
        var registry = new TemperatureConstraintRegistry();
        var survivingRegistration = registry.Register(41, Constraint(10, 20), out _);
        var snapshotBeforeFailure = registry.CaptureSnapshot();
        await SetPrivateInt64Field(registry, "nextRegistrationSequence", long.MaxValue);

        var exception = (await Assert.That(() =>
            registry.Register(41, Constraint(30, 40), out _)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("registration sequence");
        await Assert.That((await ReadPrivateInt64Field(registry, "nextRegistrationSequence"))).IsEqualTo(long.MaxValue);
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeFailure);
        await AssertEndpoints(registry.CaptureSnapshot(), 10, 20);

        await SetPrivateInt64Field(
            registry,
            "nextRegistrationSequence",
            survivingRegistration.RegistrationSequence);
        await Assert.That(registry.TryRemove(survivingRegistration, out var changed)).IsTrue();
        await Assert.That(changed).IsTrue();
    }

    [Test]
    public async Task TryReplace_WhenGenerationIsExhausted_ThrowsBeforeMutation()
    {
        var registry = new TemperatureConstraintRegistry();
        var originalConstraint = Constraint(10, 20);
        var replacementConstraint = Constraint(30, 40);
        var registration = registry.Register(41, originalConstraint, out _);
        var snapshotBeforeFailure = registry.CaptureSnapshot();
        await SetPrivateInt64Field(registry, "generation", long.MaxValue);

        var exception = (await Assert.That(() =>
            registry.TryReplace(registration, replacementConstraint, out _)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("constraint generation");
        await Assert.That((await ReadPrivateInt64Field(registry, "generation"))).IsEqualTo(long.MaxValue);
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeFailure);
        await AssertEndpoints(registry.CaptureSnapshot(), 10, 20);

        await SetPrivateInt64Field(
            registry,
            "generation",
            snapshotBeforeFailure.Generation.Value);
        await Assert.That(registry.TryReplace(
            registration,
            originalConstraint,
            out var effectiveStateChanged)).IsTrue();
        await Assert.That(effectiveStateChanged).IsFalse();
        await Assert.That(registry.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeFailure);

        await Assert.That(registry.TryReplace(
            registration,
            replacementConstraint,
            out effectiveStateChanged)).IsTrue();
        await Assert.That(effectiveStateChanged).IsTrue();
        await AssertEndpoints(registry.CaptureSnapshot(), 30, 40);
    }

    [Test]
    public async Task EndpointReferenceCountStorage_WhenSizedForCurrentOniBound_UsesReviewedFixedMemory()
    {
        var endpointCount =
            OniStorableTemperatureBounds.MaximumTemperatureKelvin + 1;
        var endpointReferenceCountElementStorageBytes = endpointCount * sizeof(int);

        await Assert.That(endpointCount).IsEqualTo(10001);
        await Assert.That(endpointReferenceCountElementStorageBytes).IsEqualTo(40004);
    }

    [Test]
    public async Task RegistryOperations_WhenRandomized_MatchDeterministicReferenceModel()
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
                    expectedGeneration = await ApplyRandomizedRegistration(
                        registry,                        referenceEntriesByComponentInstanceId,                        random,                        componentInstanceId,                        operationIndex,expectedGeneration);
                    break;

                case 1:
                    expectedGeneration = await ApplyRandomizedReplacement(
                        registry,                        referenceEntriesByComponentInstanceId,                        random,                        componentInstanceId,                        operationIndex,expectedGeneration);
                    break;

                default:
                    expectedGeneration = await ApplyRandomizedRemoval(
                        registry,                        referenceEntriesByComponentInstanceId,                        random,                        componentInstanceId,                        operationIndex,expectedGeneration);
                    break;
            }

            if (operationIndex % RandomizedSnapshotAssertionInterval == 0)
            {
                await AssertSnapshotMatchesReferenceModel(
                    registry,
                    referenceEntriesByComponentInstanceId,
                    expectedGeneration,
                    operationIndex);
            }
        }

        await AssertSnapshotMatchesReferenceModel(
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

    private static async Task AssertEndpoints(
        ActiveTemperatureConstraintSnapshot snapshot,
        params int[] expectedEndpointsKelvin)
    {
        var observedEndpointsKelvin = CopyEndpoints(
            snapshot.SortedDecisionEndpointsKelvin);
        await Assert.That(observedEndpointsKelvin).IsEquivalentTo(expectedEndpointsKelvin, TUnit.Assertions.Enums.CollectionOrdering.Matching);
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

    private static async Task<long> ApplyRandomizedRegistration(
        TemperatureConstraintRegistry registry,        IDictionary<int, ReferenceRegistryEntry> referenceEntriesByComponentInstanceId,        Random random,        int componentInstanceId,        int operationIndex,long expectedGeneration)
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

        await Assert.That(observedEffectiveStateChanged).IsEqualTo(expectedEffectiveStateChanged).Because(assertionContext);
        if (expectedEffectiveStateChanged)
        {
            expectedGeneration++;
            if (existingEntry is not null)
            {
                await Assert.That(observedRegistration).IsNotEqualTo(existingEntry.RegistrationToken).Because(assertionContext);
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

            await Assert.That(observedRegistration).IsEqualTo(existingEntry.RegistrationToken).Because(assertionContext);
        }
        return expectedGeneration;
    }

    private static async Task<long> ApplyRandomizedReplacement(
        TemperatureConstraintRegistry registry,        IDictionary<int, ReferenceRegistryEntry> referenceEntriesByComponentInstanceId,        Random random,        int componentInstanceId,        int operationIndex,long expectedGeneration)
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

            await Assert.That(registrationFound).IsTrue().Because(assertionContext);
            await Assert.That(observedEffectiveStateChanged).IsEqualTo(expectedEffectiveStateChanged).Because(assertionContext);
            if (expectedEffectiveStateChanged)
            {
                expectedGeneration++;
                referenceEntriesByComponentInstanceId[componentInstanceId] =
                    new ReferenceRegistryEntry(existingEntry.RegistrationToken, constraint);
            }

            return expectedGeneration;
        }

        var staleOrUnknownRegistration = new TemperatureConstraintRegistrationToken(
            componentInstanceId,
            long.MaxValue - operationIndex);
        await Assert.That(registry.TryReplace(
                staleOrUnknownRegistration,
                constraint,
                out var effectiveStateChanged)).IsFalse().Because(assertionContext);
        await Assert.That(effectiveStateChanged).IsFalse().Because(assertionContext);
        return expectedGeneration;
    }

    private static async Task<long> ApplyRandomizedRemoval(
        TemperatureConstraintRegistry registry,        IDictionary<int, ReferenceRegistryEntry> referenceEntriesByComponentInstanceId,        Random random,        int componentInstanceId,        int operationIndex,long expectedGeneration)
    {
        var assertionContext = RandomizedAssertionContext(operationIndex);
        if (referenceEntriesByComponentInstanceId.TryGetValue(
                componentInstanceId,
                out var existingEntry) &&
            random.Next(4) != 0)
        {
            await Assert.That(registry.TryRemove(
                    existingEntry.RegistrationToken,
                    out var knownOwnerEffectiveStateChanged)).IsTrue().Because(assertionContext);
            await Assert.That(knownOwnerEffectiveStateChanged).IsTrue().Because(assertionContext);
            referenceEntriesByComponentInstanceId.Remove(componentInstanceId);
            expectedGeneration++;
            return expectedGeneration;
        }

        var staleOrUnknownRegistration = new TemperatureConstraintRegistrationToken(
            componentInstanceId,
            long.MaxValue - operationIndex);
        await Assert.That(registry.TryRemove(
                staleOrUnknownRegistration,
                out var staleOwnerEffectiveStateChanged)).IsFalse().Because(assertionContext);
        await Assert.That(staleOwnerEffectiveStateChanged).IsFalse().Because(assertionContext);
        return expectedGeneration;
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

    private static async Task AssertSnapshotMatchesReferenceModel(
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

        await Assert.That(snapshot.Generation).IsEqualTo(new TemperatureConstraintGeneration(expectedGeneration)).Because(assertionContext);
        await Assert.That(snapshot.EnabledConstraintCount).IsEqualTo(expectedEnabledConstraintCount).Because(assertionContext);
        await Assert.That(snapshot.EnabledNonEmptyConstraintCount).IsEqualTo(expectedEnabledNonEmptyConstraintCount).Because(assertionContext);
        await Assert.That(observedEndpointsKelvin).IsEquivalentTo(expectedEndpointsKelvin.ToArray(), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because(assertionContext);
    }

    private static string RandomizedAssertionContext(int operationIndex) =>
        $"Seed=0x{RandomizedOperationSeed:X}; operation index={operationIndex}.";

    private static async Task<long> ReadPrivateInt64Field(
        TemperatureConstraintRegistry registry,
        string exactFieldName)
    {
        var field = await RequirePrivateInt64Field(exactFieldName);
        return ((await Assert.That(field.GetValue(registry)).IsAssignableTo<long>())!);
    }

    private static async Task SetPrivateInt64Field(
        TemperatureConstraintRegistry registry,
        string exactFieldName,
        long value)
    {
        var field = await RequirePrivateInt64Field(exactFieldName);
        field.SetValue(registry, value);
    }

    private static async Task<FieldInfo> RequirePrivateInt64Field(string exactFieldName)
    {
        var field = typeof(TemperatureConstraintRegistry).GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private field " +
            $"TemperatureConstraintRegistry.{exactFieldName}.");
        await Assert.That(field.FieldType).IsEqualTo(typeof(long));
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
