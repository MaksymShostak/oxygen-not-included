using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.WorldResourceTemperatureAmounts;
public sealed class TemperatureStatusAvailabilityDecisionTests
{
    [Arguments(7.0f, 20.0f, 14.0f)]
    [Arguments(7.0f, 3.0f, 10.0f)]
    [Test]
    public async Task CalculateFetchable_WhenEligibleTotalAndRemainingAreKnown_ReturnsCharacterizedAmount(
        float eligibleTotal,
        float remaining,
        float expectedFetchable)
    {
        await Assert.That(TemperatureStatusAvailabilityDecision.CalculateFetchable(
                eligibleTotal,
                remaining)).IsEqualTo(expectedFetchable);
    }

    [Test]
    public async Task ShouldTryReplacement_WhenOriginalStorageAndFetchableAreBelowMinimum_ReturnsFalse()
    {
        await Assert.That(TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                originalStorageAmount: 2.0f,
                originalFetchableAmount: 4.0f,
                minimumRequiredAmount: 7.0f)).IsFalse();
    }

    [Test]
    public async Task ShouldTryReplacement_WhenOriginalAmountsMeetMinimum_ReturnsTrue()
    {
        await Assert.That(TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                originalStorageAmount: 2.0f,
                originalFetchableAmount: 5.0f,
                minimumRequiredAmount: 7.0f)).IsTrue();
    }

    [Test]
    public async Task ShouldTryReplacement_WhenOriginalSumIsNaN_PreservesOriginalLessThanBranchBehavior()
    {
        await Assert.That(TemperatureStatusAvailabilityDecision.ShouldTryReplacement(
                originalStorageAmount: float.NaN,
                originalFetchableAmount: 5.0f,
                minimumRequiredAmount: 7.0f)).IsTrue();
    }

    [Test]
    public async Task CalculateFetchable_WhenEligibleTotalIsZero_ReturnsZero()
    {
        await Assert.That(TemperatureStatusAvailabilityDecision.CalculateFetchable(
                eligibleTotal: 0.0f,
                remaining: 20.0f)).IsEqualTo(0.0f);
    }

    [Test]
    public async Task CalculateFetchable_WhenRemainingIsNegative_PreservesMathfMinEquivalent()
    {
        await Assert.That(TemperatureStatusAvailabilityDecision.CalculateFetchable(
                eligibleTotal: 7.0f,
                remaining: -3.0f)).IsEqualTo(4.0f);
    }

    [Test]
    public async Task TryCalculateReplacementFetchable_WhenTemperatureConstraintIsDisabled_PreservesOriginalFetchable()
    {
        await AssertPreservesOriginalFetchable(
            TemperatureConstrainedAmountAvailability
                .TemperatureConstraintDisabled());
    }

    [Test]
    public async Task TryCalculateReplacementFetchable_WhenInventoryIsIncomplete_PreservesOriginalFetchable()
    {
        await AssertPreservesOriginalFetchable(
            TemperatureConstrainedAmountAvailability.InventoryIncomplete());
    }

    [Test]
    public async Task TryCalculateReplacementFetchable_WhenInventoryIsComplete_AppliesCharacterizedFormula()
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

        await Assert.That(fetchable).IsEqualTo(10.0f);
    }

    [Test]
    public async Task TryCalculateReplacementFetchable_WhenCompleteEligibleTotalIsZero_ReplacesWithZero()
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

        await Assert.That(fetchable).IsEqualTo(0.0f);
    }

    [Test]
    public async Task TryCalculateReplacementFetchable_WhenAvailabilityStateIsUnknown_ThrowsRatherThanSilentlyReplacing()
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
        Assert.NotNull(privateConstructor);
        var unknownAvailability =
            (TemperatureConstrainedAmountAvailability)privateConstructor.Invoke(
                new object[]
                {
                    (TemperatureConstrainedAmountAvailabilityState)int.MaxValue,
                    19.0f
                });

        await Assert.That(() =>
            TemperatureStatusAvailabilityDecision
                .TryCalculateReplacementFetchable(
                    unknownAvailability,
                    remaining: 3.0f,
                    out _)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    private static async Task AssertPreservesOriginalFetchable(
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

        await Assert.That(fetchable).IsEqualTo(originalFetchable);
    }
}
