namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
public sealed class ClearableDestinationSweepEligibilityTests
{
    [Test]
    public async Task AllowsClearing_WhenOriginalDestinationIsAbsent_ReturnsFalse()
    {
        var input = CreateInput(originalHasDestination: false);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsFalse();
    }

    [Test]
    public async Task AllowsClearing_WhenNoTemperatureConstraintIsEnabled_PreservesOriginalDestinationResult()
    {
        var input = CreateInput(enabledTemperatureConstraintCount: 0);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsTrue();
    }

    [Test]
    public async Task AllowsClearing_WhenPickupHasNoPrimaryElement_ReturnsFalse()
    {
        var input = CreateInput(hasPrimaryElement: false);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsFalse();
    }

    [Test]
    public async Task AllowsClearing_WhenParentWorldCannotBeResolved_ReturnsFalse()
    {
        var input = CreateInput(isParentWorldResolved: false);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsFalse();
    }

    [Test]
    public async Task AllowsClearing_WhenEligibilitySnapshotIsStale_ReturnsFalse()
    {
        var input = CreateInput(isEligibilitySnapshotCurrent: false);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsFalse();
    }

    [Test]
    public async Task AllowsClearing_WhenCurrentEligibilityAllowsPickup_ReturnsTrue()
    {
        var input = CreateInput(currentEligibilityAllowsPickup: true);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsTrue();
    }

    [Test]
    public async Task AllowsClearing_WhenCurrentEligibilityRejectsPickup_ReturnsFalse()
    {
        var input = CreateInput(currentEligibilityAllowsPickup: false);

        await Assert.That(ClearableDestinationSweepEligibility.AllowsClearing(input)).IsFalse();
    }

    [Test]
    public async Task Constructor_WhenEnabledConstraintCountIsNegative_Throws()
    {
        await Assert.That(() =>
            new ClearableDestinationSweepEligibilityInput(
                originalHasDestination: true,
                enabledTemperatureConstraintCount: -1,
                hasPrimaryElement: true,
                isParentWorldResolved: true,
                isEligibilitySnapshotCurrent: true,
                currentEligibilityAllowsPickup: true)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    private static ClearableDestinationSweepEligibilityInput CreateInput(
        bool originalHasDestination = true,
        int enabledTemperatureConstraintCount = 1,
        bool hasPrimaryElement = true,
        bool isParentWorldResolved = true,
        bool isEligibilitySnapshotCurrent = true,
        bool currentEligibilityAllowsPickup = true) =>
        new ClearableDestinationSweepEligibilityInput(
            originalHasDestination,
            enabledTemperatureConstraintCount,
            hasPrimaryElement,
            isParentWorldResolved,
            isEligibilitySnapshotCurrent,
            currentEligibilityAllowsPickup);
}
