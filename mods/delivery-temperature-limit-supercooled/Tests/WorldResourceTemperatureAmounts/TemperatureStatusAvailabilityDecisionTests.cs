using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;

[TestClass]
public sealed class TemperatureStatusAvailabilityDecisionTests
{
    [DataRow(7.0f, 20.0f, 14.0f)]
    [DataRow(7.0f, 3.0f, 10.0f)]
    [TestMethod]
    public void CalculateFetchable_WhenEligibleTotalAndRemainingAreKnown_ReturnsCharacterizedAmount(
        float eligibleTotal,
        float remaining,
        float expectedFetchable)
    {
        Assert.AreEqual(
            expectedFetchable,
            TemperatureStatusAvailabilityDecision.CalculateFetchable(
                eligibleTotal,
                remaining));
    }

    [TestMethod]
    public void ShouldTryReplacement_WhenOriginalStorageAndFetchableAreBelowMinimum_ReturnsFalse()
    {
        Assert.IsFalse(
            TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                originalStorageAmount: 2.0f,
                originalFetchableAmount: 4.0f,
                minimumRequiredAmount: 7.0f));
    }

    [TestMethod]
    public void ShouldTryReplacement_WhenOriginalAmountsMeetMinimum_ReturnsTrue()
    {
        Assert.IsTrue(
            TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                originalStorageAmount: 2.0f,
                originalFetchableAmount: 5.0f,
                minimumRequiredAmount: 7.0f));
    }

    [TestMethod]
    public void ShouldTryReplacement_WhenOriginalSumIsNaN_PreservesOriginalLessThanBranchBehavior()
    {
        Assert.IsTrue(
            TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                originalStorageAmount: float.NaN,
                originalFetchableAmount: 5.0f,
                minimumRequiredAmount: 7.0f));
    }

    [TestMethod]
    public void CalculateFetchable_WhenEligibleTotalIsZero_ReturnsZero()
    {
        Assert.AreEqual(
            0.0f,
            TemperatureStatusAvailabilityDecision.CalculateFetchable(
                eligibleTotal: 0.0f,
                remaining: 20.0f));
    }

    [TestMethod]
    public void CalculateFetchable_WhenRemainingIsNegative_PreservesMathfMinEquivalent()
    {
        Assert.AreEqual(
            4.0f,
            TemperatureStatusAvailabilityDecision.CalculateFetchable(
                eligibleTotal: 7.0f,
                remaining: -3.0f));
    }

    [TestMethod]
    public void TryCalculateReplacementFetchable_WhenTemperatureConstraintIsDisabled_PreservesOriginalFetchable()
    {
        AssertPreservesOriginalFetchable(
            TemperatureConstrainedAmountAvailability
                .TemperatureConstraintDisabled());
    }

    [TestMethod]
    public void TryCalculateReplacementFetchable_WhenInventoryIsIncomplete_PreservesOriginalFetchable()
    {
        AssertPreservesOriginalFetchable(
            TemperatureConstrainedAmountAvailability.InventoryIncomplete());
    }

    [TestMethod]
    public void TryCalculateReplacementFetchable_WhenInventoryIsComplete_AppliesCharacterizedFormula()
    {
        var fetchable = 41.0f;

        if (TemperatureStatusAvailabilityDecision
            .TryCalculateReplacementFetchable(
                TemperatureConstrainedAmountAvailability.Complete(7.0f),
                remaining: 3.0f,
                out var replacementFetchable))
        {
            fetchable = replacementFetchable;
        }

        Assert.AreEqual(10.0f, fetchable);
    }

    [TestMethod]
    public void TryCalculateReplacementFetchable_WhenCompleteEligibleTotalIsZero_ReplacesWithZero()
    {
        var fetchable = 41.0f;

        if (TemperatureStatusAvailabilityDecision
            .TryCalculateReplacementFetchable(
                TemperatureConstrainedAmountAvailability.Complete(0.0f),
                remaining: 3.0f,
                out var replacementFetchable))
        {
            fetchable = replacementFetchable;
        }

        Assert.AreEqual(0.0f, fetchable);
    }

    [TestMethod]
    public void TryCalculateReplacementFetchable_WhenAvailabilityStateIsUnknown_ThrowsRatherThanSilentlyReplacing()
    {
        var privateConstructor =
            typeof(TemperatureConstrainedAmountAvailability).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new[]
                {
                    typeof(TemperatureConstrainedAmountAvailabilityState),
                    typeof(float)
                },
                modifiers: null);
        Assert.IsNotNull(privateConstructor);
        var unknownAvailability =
            (TemperatureConstrainedAmountAvailability)privateConstructor.Invoke(
                new object[]
                {
                    (TemperatureConstrainedAmountAvailabilityState)int.MaxValue,
                    19.0f
                });

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TemperatureStatusAvailabilityDecision
                .TryCalculateReplacementFetchable(
                    unknownAvailability,
                    remaining: 3.0f,
                    out _));
    }

    private static void AssertPreservesOriginalFetchable(
        TemperatureConstrainedAmountAvailability availability)
    {
        const float originalFetchable = 41.0f;
        var fetchable = originalFetchable;

        if (TemperatureStatusAvailabilityDecision
            .TryCalculateReplacementFetchable(
                availability,
                remaining: 3.0f,
                out var replacementFetchable))
        {
            fetchable = replacementFetchable;
        }

        Assert.AreEqual(originalFetchable, fetchable);
    }
}
