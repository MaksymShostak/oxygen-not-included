using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FastTrackCompatibility;

[TestClass]
public sealed class FastTrackPickupGroupingKeyAllocatorTests
{
    [TestMethod]
    public void GetOrAllocate_WhenOriginalHashMatchesButTemperatureClassDiffers_ReturnsDifferentIntegers()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        allocator.Begin(temperatureGroupingIsActive: true);

        var first = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1));
        var second = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 2));

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void GetOrAllocate_WhenCompositeRepeats_ReusesInteger()
    {
        var allocator = BeginActiveAllocator();
        var classification =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1);

        var first = allocator.GetOrAllocate(123, classification);
        var repeated = allocator.GetOrAllocate(123, classification);

        Assert.AreEqual(first, repeated);
        Assert.AreEqual(1, GetAllocatedGroupingKeyDictionary(allocator).Count);
    }

    [TestMethod]
    public void GetOrAllocate_WhenOriginalHashesDiffer_ReturnsDifferentIntegers()
    {
        var allocator = BeginActiveAllocator();
        var classification =
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1);

        var first = allocator.GetOrAllocate(123, classification);
        var second = allocator.GetOrAllocate(456, classification);

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void GetOrAllocate_WhenDefinitionIdsDiffer_ReturnsDifferentIntegers()
    {
        var allocator = BeginActiveAllocator();

        var first = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(7, 1));
        var second = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.OptimizedPartitionInterval(8, 1));

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void GetOrAllocate_WhenMissingPrimaryElementClassIsUsed_AllocatesNormally()
    {
        var allocator = BeginActiveAllocator();

        var missingPrimaryElementKey = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.MissingPrimaryElement());
        var exactTemperatureKey = allocator.GetOrAllocate(
            123,
            TemperatureEligibilityClassKey.ExactDecisionBucket(
                TemperatureDecisionBucket.FromIntegerKelvin(0)));

        Assert.AreEqual(0, missingPrimaryElementKey);
        Assert.AreEqual(1, exactTemperatureKey);
        Assert.AreNotEqual(missingPrimaryElementKey, exactTemperatureKey);
    }

    [TestMethod]
    public void GetOrAllocate_WhenGroupingIsInactive_ReturnsOriginalHashWithoutRetainingEntry()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        allocator.Begin(temperatureGroupingIsActive: false);

        var allocatedKey = allocator.GetOrAllocate(
            originalTagBitsHash: -123456789,
            TemperatureEligibilityClassKey.MissingPrimaryElement());

        Assert.AreEqual(-123456789, allocatedKey);
        Assert.IsEmpty(GetAllocatedGroupingKeyDictionary(allocator));
    }

    [TestMethod]
    public void GetOrAllocate_WhenEveryOriginalHashCollides_StillAllocatesUniqueIntegers()
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
            Assert.IsTrue(
                allocatedKeys.Add(allocatedKey),
                $"Classification {classificationIndex} reused integer " +
                $"{allocatedKey}.");
        }

        Assert.HasCount(1000, allocatedKeys);
    }

    [TestMethod]
    public void GetOrAllocate_WhenIntegerSpaceIsExhausted_ThrowsWithoutWraparound()
    {
        var allocator = BeginActiveAllocator();
        var nextAllocationField = RequirePrivateInstanceField(
            "nextAllocatedGroupingKey",
            typeof(int));
        nextAllocationField.SetValue(allocator, int.MaxValue);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            allocator.GetOrAllocate(
                123,
                TemperatureEligibilityClassKey.MissingPrimaryElement()));

        StringAssert.Contains(exception.Message, "exhausted");
        Assert.AreEqual(int.MaxValue, nextAllocationField.GetValue(allocator));
        Assert.IsEmpty(GetAllocatedGroupingKeyDictionary(allocator));
    }

    [TestMethod]
    public void Begin_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var allocator = BeginActiveAllocator();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            allocator.Begin(temperatureGroupingIsActive: true));
    }

    [TestMethod]
    public void Discard_WhenCalledAfterFailure_ClearsActiveState()
    {
        var allocator = BeginActiveAllocator();
        var nextAllocationField = RequirePrivateInstanceField(
            "nextAllocatedGroupingKey",
            typeof(int));
        nextAllocationField.SetValue(allocator, int.MaxValue);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            allocator.GetOrAllocate(
                123,
                TemperatureEligibilityClassKey.MissingPrimaryElement()));

        allocator.Discard();

        Assert.IsEmpty(GetAllocatedGroupingKeyDictionary(allocator));
        Assert.AreEqual(0, nextAllocationField.GetValue(allocator));
        allocator.Begin(temperatureGroupingIsActive: true);
        Assert.AreEqual(
            0,
            allocator.GetOrAllocate(
                456,
                TemperatureEligibilityClassKey.MissingPrimaryElement()));
    }

    [TestMethod]
    public void Complete_WhenEntryCountExceededHighWater_ReplacesDictionary()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        var retentionLimit =
            RetainedCollectionCapacityLimits
                .MaximumRetainedFastTrackGroupingKeyCount;

        allocator.Begin(temperatureGroupingIsActive: true);
        var dictionaryAtLimit = GetAllocatedGroupingKeyDictionary(allocator);
        AllocateAndVerifyDistinctComposites(allocator, retentionLimit);
        allocator.Complete();
        Assert.AreSame(
            dictionaryAtLimit,
            GetAllocatedGroupingKeyDictionary(allocator));

        allocator.Begin(temperatureGroupingIsActive: true);
        var dictionaryBeforeLimitExceeded =
            GetAllocatedGroupingKeyDictionary(allocator);
        AllocateAndVerifyDistinctComposites(allocator, retentionLimit + 1);
        allocator.Complete();
        Assert.AreNotSame(
            dictionaryBeforeLimitExceeded,
            GetAllocatedGroupingKeyDictionary(allocator));

        allocator.Begin(temperatureGroupingIsActive: true);
        var dictionaryBeforeLargerWorkload =
            GetAllocatedGroupingKeyDictionary(allocator);
        AllocateAndVerifyDistinctComposites(
            allocator,
            (retentionLimit * 2) + 17);
        allocator.Complete();
        Assert.AreNotSame(
            dictionaryBeforeLargerWorkload,
            GetAllocatedGroupingKeyDictionary(allocator));
    }

    [TestMethod]
    public void GetOrAllocate_WhenOneHundredThousandCollisionHeavyInputsRepeat_PreservesCompositeBijection()
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
                Assert.AreEqual(expectedGroupingKey, allocatedGroupingKey);
            }
            else
            {
                expectedGroupingKeyByComposite.Add(
                    composite,
                    allocatedGroupingKey);
                Assert.IsFalse(
                    compositeByAllocatedGroupingKey.ContainsKey(
                        allocatedGroupingKey),
                    $"Distinct composite input {inputIndex} reused allocated " +
                    $"integer {allocatedGroupingKey}.");
                compositeByAllocatedGroupingKey.Add(
                    allocatedGroupingKey,
                    composite);
            }

            Assert.AreEqual(
                composite,
                compositeByAllocatedGroupingKey[allocatedGroupingKey]);
        }

        Assert.HasCount(
            reusedOriginalHashCount * reusedTemperatureClassCount,
            expectedGroupingKeyByComposite);
        Assert.AreEqual(
            expectedGroupingKeyByComposite.Count,
            GetAllocatedGroupingKeyDictionary(allocator).Count);
    }

    [TestMethod]
    public void GetOrAllocate_WhenAllocatorHasNotBegun_ThrowsInvalidOperationException()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            allocator.GetOrAllocate(
                123,
                TemperatureEligibilityClassKey.MissingPrimaryElement()));
    }

    private static void AllocateAndVerifyDistinctComposites(
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
            Assert.AreEqual(compositeIndex, allocatedGroupingKey);
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
            Assert.AreEqual(compositeIndex, repeatedGroupingKey);
        }

        Assert.AreEqual(
            compositeCount,
            GetAllocatedGroupingKeyDictionary(allocator).Count);
    }

    private static FastTrackPickupGroupingKeyAllocator BeginActiveAllocator()
    {
        var allocator = new FastTrackPickupGroupingKeyAllocator();
        allocator.Begin(temperatureGroupingIsActive: true);
        return allocator;
    }

    private static IDictionary GetAllocatedGroupingKeyDictionary(
        FastTrackPickupGroupingKeyAllocator allocator) =>
        Assert.IsInstanceOfType<IDictionary>(
            RequirePrivateInstanceField(
                    "allocatedGroupingKeyByPickupGroupingIdentity",
                    typeof(Dictionary<,>))
                .GetValue(allocator));

    private static FieldInfo RequirePrivateInstanceField(
        string fieldName,
        Type expectedFieldType)
    {
        var field = typeof(FastTrackPickupGroupingKeyAllocator).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"Expected one predeclared private field named '{fieldName}'.");
        if (expectedFieldType.IsGenericTypeDefinition)
        {
            Assert.IsTrue(field.FieldType.IsGenericType);
            Assert.AreEqual(
                expectedFieldType,
                field.FieldType.GetGenericTypeDefinition());
        }
        else
        {
            Assert.AreEqual(expectedFieldType, field.FieldType);
        }

        return field;
    }
}
