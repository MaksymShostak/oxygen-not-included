using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;
public sealed class FastTrackPickupGroupingKeyAllocatorTests
{
    [Test]
    public async Task GetOrAllocate_WhenOriginalHashMatchesAndTemperatureClassesDiffer_ReturnsDifferentKeys()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        allocator.Begin(temperatureGroupingIsActive: true);
        var firstCandidate = new FastTrackFetchableCandidateModel(123);
        var secondCandidate = new FastTrackFetchableCandidateModel(123);

        var first = await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            firstCandidate,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1));
        var second = await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            secondCandidate,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2));

        await Assert.That(second).IsNotEqualTo(first);
        await Assert.That(firstCandidate.TagBitsHash).IsEqualTo(123);
        await Assert.That(secondCandidate.TagBitsHash).IsEqualTo(123);
    }

    [Test]
    public async Task GetOrAllocate_WhenCompositeIdentityRepeats_ReturnsSameKey()
    {
        var allocator = BeginActiveAllocator();
        var candidate = new FastTrackFetchableCandidateModel(123);
        var classification =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1);

        var first = await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            candidate,
            classification);
        var repeated = await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            candidate,
            classification);

        await Assert.That(repeated).IsEqualTo(first);
        await Assert.That(candidate.TagBitsHash).IsEqualTo(123);
        await Assert.That((await GetAllocatedGroupingKeyDictionary(allocator)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetOrAllocate_WhenOriginalHashesDifferAndTemperatureClassMatches_ReturnsDifferentKeys()
    {
        var allocator = BeginActiveAllocator();
        var firstCandidate = new FastTrackFetchableCandidateModel(123);
        var secondCandidate = new FastTrackFetchableCandidateModel(456);
        var classification =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1);

        var first = await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            firstCandidate,
            classification);
        var second = await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            secondCandidate,
            classification);

        await Assert.That(second).IsNotEqualTo(first);
        await Assert.That(firstCandidate.TagBitsHash).IsEqualTo(123);
        await Assert.That(secondCandidate.TagBitsHash).IsEqualTo(456);
    }

    [Test]
    public async Task GetOrAllocate_WhenDefinitionIdsDiffer_ReturnsDifferentIntegers()
    {
        var allocator = BeginActiveAllocator();

        var first = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1));
        var second = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(8, 1));

        await Assert.That(second).IsNotEqualTo(first);
    }

    [Test]
    public async Task GetOrAllocate_WhenPrimaryElementIsMissing_UsesDedicatedNonTemperatureClass()
    {
        var allocator = BeginActiveAllocator();
        var missingPrimaryElementCandidate =
            new FastTrackFetchableCandidateModel(123);
        var exactTemperatureCandidate =
            new FastTrackFetchableCandidateModel(123);

        var missingPrimaryElementKey =
            await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            missingPrimaryElementCandidate,
            TemperatureEligibilityClassKey.MissingPrimaryElement());
        var exactTemperatureKey =
            await ConstructPickupTagKeyWithoutMutatingFetchable(
            allocator,
            exactTemperatureCandidate,
            TemperatureEligibilityClassKey.ExactDecisionBucket(
                TemperatureDecisionBucket.FromIntegerKelvin(0)));

        await Assert.That(missingPrimaryElementKey).IsEqualTo(0);
        await Assert.That(exactTemperatureKey).IsEqualTo(1);
        await Assert.That(exactTemperatureKey).IsNotEqualTo(missingPrimaryElementKey);
        await Assert.That(missingPrimaryElementCandidate.TagBitsHash).IsEqualTo(123);
        await Assert.That(exactTemperatureCandidate.TagBitsHash).IsEqualTo(123);
    }

    [Test]
    public async Task GetOrAllocate_WhenGroupingIsInactive_ReturnsOriginalHashWithoutRetainingEntry()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        allocator.Begin(temperatureGroupingIsActive: false);

        var allocatedKey = allocator.GetOrAllocate(
            originalTagBitsHash: -123456789,
            TemperatureEligibilityClassKey.MissingPrimaryElement());

        await Assert.That(allocatedKey).IsEqualTo(-123456789);
        await Assert.That(((System.Collections.IEnumerable)(await GetAllocatedGroupingKeyDictionary(allocator))).Cast<object?>()).IsEmpty();
    }

    [Test]
    public async Task GetOrAllocate_WhenEveryOriginalHashCollides_StillAllocatesUniqueIntegers()
    {
        var allocator = BeginActiveAllocator();
        var allocatedKeys = new HashSet<int>();

        for (var classificationIndex = 0;
             classificationIndex < 1000;
             classificationIndex++)
        {
            var allocatedKey = allocator.GetOrAllocate(
                originalTagBitsHash: 123,
                TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                    partitionDefinitionId: classificationIndex + 1,
                    intervalOrdinal: classificationIndex % 5));
            await Assert.That(allocatedKeys.Add(allocatedKey)).IsTrue().Because($"Classification {classificationIndex} reused integer " +
                $"{allocatedKey}.");
        }

        await Assert.That(allocatedKeys).Count().IsEqualTo(1000);
    }

    [Test]
    public async Task GetOrAllocate_WhenIntegerSpaceIsExhausted_ThrowsWithoutWraparound()
    {
        var allocator = BeginActiveAllocator();
        var nextAllocationField = await RequirePrivateInstanceField(
            "nextAllocatedGroupingKey",
            typeof(int));
        nextAllocationField.SetValue(allocator, int.MaxValue);

        var exception = (await Assert.That(() =>
            allocator.GetOrAllocate(
                123,
                TemperatureEligibilityClassKey.MissingPrimaryElement())).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("exhausted");
        await Assert.That(nextAllocationField.GetValue(allocator)).IsEqualTo(int.MaxValue);
        await Assert.That(((System.Collections.IEnumerable)(await GetAllocatedGroupingKeyDictionary(allocator))).Cast<object?>()).IsEmpty();
    }

    [Test]
    public async Task Begin_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var allocator = BeginActiveAllocator();

        await Assert.That(() =>
            allocator.Begin(temperatureGroupingIsActive: true)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Discard_WhenCalledAfterFailure_ClearsActiveState()
    {
        var allocator = BeginActiveAllocator();
        var nextAllocationField = await RequirePrivateInstanceField(
            "nextAllocatedGroupingKey",
            typeof(int));
        nextAllocationField.SetValue(allocator, int.MaxValue);
        await Assert.That(() =>
            allocator.GetOrAllocate(
                123,
                TemperatureEligibilityClassKey.MissingPrimaryElement())).ThrowsExactly<InvalidOperationException>();

        allocator.Discard();

        await Assert.That(((System.Collections.IEnumerable)(await GetAllocatedGroupingKeyDictionary(allocator))).Cast<object?>()).IsEmpty();
        await Assert.That(nextAllocationField.GetValue(allocator)).IsEqualTo(0);
        allocator.Begin(temperatureGroupingIsActive: true);
        await Assert.That(allocator.GetOrAllocate(
                456,
                TemperatureEligibilityClassKey.MissingPrimaryElement())).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_WhenEntryCountExceededHighWater_ReplacesDictionary()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        var retentionLimit =
            RetainedCollectionCapacityLimits
                .MaximumRetainedFastTrackGroupingKeyCount;

        allocator.Begin(temperatureGroupingIsActive: true);
        var dictionaryAtLimit = await GetAllocatedGroupingKeyDictionary(allocator);
        await AllocateAndVerifyDistinctComposites(allocator, retentionLimit);
        allocator.Complete();
        await Assert.That((await GetAllocatedGroupingKeyDictionary(allocator))).IsSameReferenceAs(dictionaryAtLimit);

        allocator.Begin(temperatureGroupingIsActive: true);
        var dictionaryBeforeLimitExceeded =
            await GetAllocatedGroupingKeyDictionary(allocator);
        await AllocateAndVerifyDistinctComposites(allocator, retentionLimit + 1);
        allocator.Complete();
        await Assert.That((await GetAllocatedGroupingKeyDictionary(allocator))).IsNotSameReferenceAs(dictionaryBeforeLimitExceeded);

        allocator.Begin(temperatureGroupingIsActive: true);
        var dictionaryBeforeLargerWorkload =
            await GetAllocatedGroupingKeyDictionary(allocator);
        await AllocateAndVerifyDistinctComposites(
            allocator,
            (retentionLimit * 2) + 17);
        allocator.Complete();
        await Assert.That((await GetAllocatedGroupingKeyDictionary(allocator))).IsNotSameReferenceAs(dictionaryBeforeLargerWorkload);
    }

    [Test]
    public async Task GetOrAllocate_WhenOneHundredThousandCollisionHeavyInputsRepeat_PreservesCompositeBijection()
    {
        const int inputCount = 100000;
        const int reusedOriginalHashCount = 16;
        const int reusedTemperatureClassCount = 257;
        var allocator = BeginActiveAllocator();
        var expectedGroupingKeyByComposite =
            new Dictionary<(int, TemperatureEligibilityClassKey), int>();
        var compositeByAllocatedGroupingKey =
            new Dictionary<int, (int, TemperatureEligibilityClassKey)>();

        for (var inputIndex = 0; inputIndex < inputCount; inputIndex++)
        {
            var originalTagBitsHash = inputIndex % reusedOriginalHashCount;
            var temperatureClassIndex =
                inputIndex % reusedTemperatureClassCount;
            var temperatureClass =
                TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                    partitionDefinitionId: temperatureClassIndex + 1,
                    intervalOrdinal: temperatureClassIndex % 7);
            var composite = (originalTagBitsHash, temperatureClass);
            var allocatedGroupingKey = allocator.GetOrAllocate(
                originalTagBitsHash,
                temperatureClass);

            if (expectedGroupingKeyByComposite.TryGetValue(
                    composite,
                    out var expectedGroupingKey))
            {
                await Assert.That(allocatedGroupingKey).IsEqualTo(expectedGroupingKey);
            }
            else
            {
                expectedGroupingKeyByComposite.Add(
                    composite,
                    allocatedGroupingKey);
                await Assert.That(compositeByAllocatedGroupingKey.ContainsKey(
                        allocatedGroupingKey)).IsFalse().Because($"Distinct composite input {inputIndex} reused allocated " +
                    $"integer {allocatedGroupingKey}.");
                compositeByAllocatedGroupingKey.Add(
                    allocatedGroupingKey,
                    composite);
            }

            await Assert.That(compositeByAllocatedGroupingKey[allocatedGroupingKey]).IsEqualTo(composite);
        }

        await Assert.That(expectedGroupingKeyByComposite).Count().IsEqualTo(reusedOriginalHashCount * reusedTemperatureClassCount);
        await Assert.That((await GetAllocatedGroupingKeyDictionary(allocator)).Count).IsEqualTo(expectedGroupingKeyByComposite.Count);
    }

    [Test]
    public async Task GetOrAllocate_WhenAllocatorHasNotBegun_ThrowsInvalidOperationException()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();

        await Assert.That(() =>
            allocator.GetOrAllocate(
                123,
                TemperatureEligibilityClassKey.MissingPrimaryElement())).ThrowsExactly<InvalidOperationException>();
    }

    private static async Task AllocateAndVerifyDistinctComposites(
        FastTrackPickupGroupingKeyAllocator allocator,
        int compositeCount)
    {
        for (var compositeIndex = 0;
             compositeIndex < compositeCount;
             compositeIndex++)
        {
            var allocatedGroupingKey = allocator.GetOrAllocate(
                originalTagBitsHash: compositeIndex % 17,
                TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                    partitionDefinitionId: compositeIndex + 1,
                    intervalOrdinal: compositeIndex % 3));
            await Assert.That(allocatedGroupingKey).IsEqualTo(compositeIndex);
        }

        for (var compositeIndex = 0;
             compositeIndex < compositeCount;
             compositeIndex++)
        {
            var repeatedGroupingKey = allocator.GetOrAllocate(
                originalTagBitsHash: compositeIndex % 17,
                TemperatureEligibilityClassKey.OptimizedPartitionInterval(
                    partitionDefinitionId: compositeIndex + 1,
                    intervalOrdinal: compositeIndex % 3));
            await Assert.That(repeatedGroupingKey).IsEqualTo(compositeIndex);
        }

        await Assert.That((await GetAllocatedGroupingKeyDictionary(allocator)).Count).IsEqualTo(compositeCount);
    }

    private static FastTrackPickupGroupingKeyAllocator BeginActiveAllocator()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        allocator.Begin(temperatureGroupingIsActive: true);
        return allocator;
    }

    private static async Task<int> ConstructPickupTagKeyWithoutMutatingFetchable(
        FastTrackPickupGroupingKeyAllocator allocator,
        FastTrackFetchableCandidateModel fetchableCandidate,
        TemperatureEligibilityClassKey temperatureEligibilityClass)
    {
        int originalTagBitsHash = fetchableCandidate.TagBitsHash;
        int constructorGroupingKey = allocator.GetOrAllocate(
            originalTagBitsHash,
            temperatureEligibilityClass);
        await Assert.That(fetchableCandidate.TagBitsHash).IsEqualTo(originalTagBitsHash).Because("Only the private PickupTagKey constructor argument may receive " +
            "the allocated key; FastTrack's fetchable tagBitsHash must remain " +
            "unchanged.");
        return constructorGroupingKey;
    }

    private static async Task<IDictionary> GetAllocatedGroupingKeyDictionary(
        FastTrackPickupGroupingKeyAllocator allocator) =>
        (await Assert.That((await RequirePrivateInstanceField(
                    "allocatedGroupingKeysByCompositeIdentity",
                    typeof(Dictionary<,>)))
                .GetValue(allocator)).IsAssignableTo<IDictionary>())!;

    private static async Task<FieldInfo> RequirePrivateInstanceField(
        string fieldName,
        Type expectedFieldType)
    {
        var field = typeof(FastTrackPickupGroupingKeyAllocator).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"Expected one predeclared private field named '{fieldName}'.");
        if (expectedFieldType.IsGenericTypeDefinition)
        {
            await Assert.That(field.FieldType.IsGenericType).IsTrue();
            await Assert.That(field.FieldType.GetGenericTypeDefinition()).IsEqualTo(expectedFieldType);
        }
        else
        {
            await Assert.That(field.FieldType).IsEqualTo(expectedFieldType);
        }

        return field;
    }

    /// <summary>
    /// Mutable test representation of the one FastTrack fetchable field that the
    /// adapter must read but never rewrite.
    /// </summary>
    private sealed class FastTrackFetchableCandidateModel
    {
        internal FastTrackFetchableCandidateModel(int tagBitsHash)
        {
            TagBitsHash = tagBitsHash;
        }

        internal int TagBitsHash { get; set; }
    }
}
