namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class FetchChoreTemperatureConstraintContainmentTests
{
    // ONI's FetchAreaChore selects every pickup against the root chore only and
    // stores those pickups in each coalesced candidate without a temperature
    // recheck. For two enabled, nonempty constraints, coalescing is therefore
    // safe only when every pickup temperature admitted by the root destination
    // is also admitted by the candidate destination. Missing and disabled
    // constraints retain the separately characterized "no temperature-specific
    // requirement" behavior.

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
            "A missing root constraint admits pickups the constrained candidate rejects.");
        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                CreateDisabledConstraint(),
                candidateConstraint),
            "A disabled root constraint must behave exactly like a missing constraint.");
    }

    [TestMethod]
    public void CanCombine_WhenRootIntervalIsInsideCandidate_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);

        Assert.IsTrue(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint),
            "Every pickup admitted by the root is admitted by the broader candidate.");
    }

    [TestMethod]
    public void CanCombine_WhenCandidateIntervalIsInsideRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint),
            "A pickup admitted by the broader root could violate the narrower candidate.");
    }

    [TestMethod]
    public void CanCombine_WhenCandidateMinimumIsAboveRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 201, maximumExclusiveKelvin: 400);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint));
    }

    [TestMethod]
    public void CanCombine_WhenCandidateMaximumIsBelowRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 399);

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
    public void CanCombine_WhenCandidateIsEmpty_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint emptyCandidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 350, maximumExclusiveKelvin: 300);

        Assert.IsFalse(
            FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                emptyCandidateConstraint),
            "An empty candidate destination rejects every pickup the root may select.");
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
                candidateConstraint),
            "An empty root selects no legitimate pickup, so coalescing is rejected defensively.");
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
