namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class ClearableDestinationSweepEligibilityTests
{
    [TestMethod]
    public void AllowsClearing_WhenOriginalDestinationIsAbsent_ReturnsFalse()
    {
        var input = CreateInput(originalHasDestination: false);

        Assert.IsFalse(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void AllowsClearing_WhenNoTemperatureConstraintIsEnabled_PreservesOriginalDestinationResult()
    {
        var input = CreateInput(enabledTemperatureConstraintCount: 0);

        Assert.IsTrue(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void AllowsClearing_WhenPickupHasNoPrimaryElement_ReturnsFalse()
    {
        var input = CreateInput(hasPrimaryElement: false);

        Assert.IsFalse(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void AllowsClearing_WhenParentWorldCannotBeResolved_ReturnsFalse()
    {
        var input = CreateInput(isParentWorldResolved: false);

        Assert.IsFalse(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void AllowsClearing_WhenEligibilitySnapshotIsStale_ReturnsFalse()
    {
        var input = CreateInput(isEligibilitySnapshotCurrent: false);

        Assert.IsFalse(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void AllowsClearing_WhenCurrentEligibilityAllowsPickup_ReturnsTrue()
    {
        var input = CreateInput(currentEligibilityAllowsPickup: true);

        Assert.IsTrue(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void AllowsClearing_WhenCurrentEligibilityRejectsPickup_ReturnsFalse()
    {
        var input = CreateInput(currentEligibilityAllowsPickup: false);

        Assert.IsFalse(
            ClearableDestinationSweepEligibility.AllowsClearing(input));
    }

    [TestMethod]
    public void Constructor_WhenEnabledConstraintCountIsNegative_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ClearableDestinationSweepEligibilityInput(
                originalHasDestination: true,
                enabledTemperatureConstraintCount: -1,
                hasPrimaryElement: true,
                isParentWorldResolved: true,
                isEligibilitySnapshotCurrent: true,
                currentEligibilityAllowsPickup: true));
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
