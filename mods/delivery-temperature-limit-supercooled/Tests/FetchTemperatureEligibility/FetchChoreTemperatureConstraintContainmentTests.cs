namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
public sealed class FetchChoreTemperatureConstraintContainmentTests
{
    // ONI's FetchAreaChore selects every pickup against the root chore only and
    // stores those pickups in each coalesced candidate without a temperature
    // recheck. For two enabled, nonempty constraints, coalescing is therefore
    // safe only when every pickup temperature admitted by the root destination
    // is also admitted by the candidate destination. Missing and disabled
    // constraints retain the separately characterized "no temperature-specific
    // requirement" behavior.

    [Test]
    public async Task CanCombine_WhenCandidateIsUnconstrained_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint: null)).IsTrue().Because("A missing candidate constraint must add no temperature-specific requirement.");
        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                CreateDisabledConstraint())).IsTrue().Because("A disabled candidate constraint must behave exactly like a missing constraint.");
    }

    [Test]
    public async Task CanCombine_WhenRootIsUnconstrainedButCandidateIsConstrained_ReturnsFalse()
    {
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint: null,
                candidateConstraint)).IsFalse().Because("A missing root constraint admits pickups the constrained candidate rejects.");
        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                CreateDisabledConstraint(),
                candidateConstraint)).IsFalse().Because("A disabled root constraint must behave exactly like a missing constraint.");
    }

    [Test]
    public async Task CanCombine_WhenRootIntervalIsInsideCandidate_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint)).IsTrue().Because("Every pickup admitted by the root is admitted by the broader candidate.");
    }

    [Test]
    public async Task CanCombine_WhenCandidateIntervalIsInsideRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 350);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint)).IsFalse().Because("A pickup admitted by the broader root could violate the narrower candidate.");
    }

    [Test]
    public async Task CanCombine_WhenCandidateMinimumIsAboveRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 201, maximumExclusiveKelvin: 400);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint)).IsFalse();
    }

    [Test]
    public async Task CanCombine_WhenCandidateMaximumIsBelowRoot_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 399);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint)).IsFalse();
    }

    [Test]
    public async Task CanCombine_WhenConstraintsAreEqual_ReturnsTrue()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                candidateConstraint)).IsTrue();
    }

    [Test]
    public async Task CanCombine_WhenCandidateIsEmpty_ReturnsFalse()
    {
        DeliveryTemperatureConstraint rootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 200, maximumExclusiveKelvin: 400);
        DeliveryTemperatureConstraint emptyCandidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 350, maximumExclusiveKelvin: 300);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                rootConstraint,
                emptyCandidateConstraint)).IsFalse().Because("An empty candidate destination rejects every pickup the root may select.");
    }

    [Test]
    public async Task CanCombine_WhenRootIsEmptyAndCandidateIsNonEmpty_ReturnsFalse()
    {
        DeliveryTemperatureConstraint emptyRootConstraint =
            CreateConstraint(minimumInclusiveKelvin: 350, maximumExclusiveKelvin: 300);
        DeliveryTemperatureConstraint candidateConstraint =
            CreateConstraint(minimumInclusiveKelvin: 250, maximumExclusiveKelvin: 275);

        await Assert.That(FetchChoreTemperatureConstraintContainment.CanCombine(
                emptyRootConstraint,
                candidateConstraint)).IsFalse().Because("An empty root selects no legitimate pickup, so coalescing is rejected defensively.");
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
