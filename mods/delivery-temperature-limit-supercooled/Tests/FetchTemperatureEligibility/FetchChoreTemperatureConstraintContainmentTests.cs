namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class FetchChoreTemperatureConstraintContainmentTests
{
    // For two enabled, nonempty constraints, coalescing is safe only when every
    // pickup temperature admitted by the candidate destination is also admitted
    // by the root destination. Missing and disabled constraints retain the
    // separately characterized "no temperature-specific requirement" behavior.

    [TestMethod]
    public void CanCombine_WhenCandidateIsUnconstrained_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        Assert.IsTrue(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint: null),
            "A missing candidate constraint must add no temperature-specific requirement.");
        Assert.IsTrue(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                CreateDisabledConstraint()),
            "A disabled candidate constraint must behave exactly like a missing constraint.");
    }

    [TestMethod]
    public void CanCombine_WhenRootIsUnconstrainedButCandidateIsConstrained_ReturnsFalse()
    {
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint: null,
                candidateConstraint),
            "A missing root constraint cannot prove containment of a constrained candidate.");
        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                CreateDisabledConstraint(),
                candidateConstraint),
            "A disabled root constraint must behave exactly like a missing constraint.");
    }

    [TestMethod]
    public void CanCombine_WhenCandidateIntervalIsInsideRoot_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        Assert.IsTrue(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint));
    }

    [TestMethod]
    public void CanCombine_WhenCandidateMinimumIsBelowRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 199, maximumExclusiveKelvin: 350);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint));
    }

    [TestMethod]
    public void CanCombine_WhenCandidateMaximumIsAboveRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 401);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint));
    }

    [TestMethod]
    public void CanCombine_WhenConstraintsAreEqual_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);

        Assert.IsTrue(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint));
    }

    [TestMethod]
    public void CanCombine_WhenCandidateIsEmpty_ReturnsTrueBecauseItAdmitsNoAdditionalPickup()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint emptyCandidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 350, maximumExclusiveKelvin: 300);

        Assert.IsTrue(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                emptyCandidateConstraint));
    }

    [TestMethod]
    public void CanCombine_WhenRootIsEmptyAndCandidateIsNonEmpty_ReturnsFalse()
    {
        DeliveryTemperatureConstraint emptyRootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 350, maximumExclusiveKelvin: 300);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 275);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                emptyRootConstraint,
                candidateConstraint));
    }

    private static DeliveryTemperatureConstraint CreateDisabledConstraint() =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            serializedLowLimit: 0,
            serializedHighLimit: 0);

    private static DeliveryTemperatureConstraint CreateConstraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);
}
