using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;

[TestClass]
public sealed class PickupTemperatureGroupingSessionTests
{
    [TestMethod]
    public void Classify_WhenNoEnabledConstraints_ReturnsNoTemperatureDistinctionWithoutCacheGrowth()
    {
        var session = CreateSessionWithoutEnabledConstraints();
        var constraints = session.TemperatureConstraints.CaptureSnapshot();
        var worldTopology = session.WorldParentTopology.CaptureSnapshot();
        var groupingSession = new PickupTemperatureGroupingSession();
        groupingSession.Begin(
            session,
            resolvedParentWorldId: null,
            constraints,
            eligibilitySnapshot: null,
            worldTopology);

        var classification = groupingSession.Classify(
            pickupInstanceId: 101,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: false,
            temperatureKelvin: 15.0f);

        Assert.AreEqual(
            TemperatureEligibilityClassKey.NoTemperatureDistinction(),
            classification);
        Assert.IsEmpty(GetPickupClassificationDictionary(groupingSession));
    }

    [TestMethod]
    public void Classify_WhenSnapshotIsCurrent_UsesScopedPartition()
    {
        var session = CreateSessionWithEnabledConstraint();
        var iron = new Tag("Iron");
        var snapshot = BuildCurrentEligibilitySnapshot(
            session,
            builder => builder.AddTemperatureConstrainedFetchRequest(
                parentWorldId: 1,
                requestedTags: [iron],
                enabledConstraint: Constraint(10, 20)));
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 102,
            new PickupTagIdentity(17, iron),
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        Assert.AreEqual(
            TemperatureEligibilityClassificationKind
                .OptimizedPartitionInterval,
            classification.ClassificationKind);
        Assert.AreEqual(1, classification.PartitionDefinitionId);
        Assert.AreEqual(1, classification.IntervalOrdinal);
    }

    [TestMethod]
    public void Classify_WhenSnapshotIsNull_UsesExactDecisionBucket()
    {
        var session = CreateSessionWithEnabledConstraint();
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);

        var classification = groupingSession.Classify(
            pickupInstanceId: 103,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: true,
            temperatureKelvin: 15.75f);

        AssertExactDecisionBucket(classification, 15.75f);
    }

    [TestMethod]
    public void Classify_WhenSnapshotConstraintGenerationIsStale_UsesExactDecisionBucket()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        session.TemperatureConstraints.Register(
            componentInstanceId: 10401,
            Constraint(30, 40),
            out var stateChanged);
        Assert.IsTrue(stateChanged);
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 104,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        AssertExactDecisionBucket(classification, 15.0f);
    }

    [TestMethod]
    public void Classify_WhenSnapshotFetchVersionIsStale_UsesExactDecisionBucket()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        session.FetchRequestTopology.RecordEffectiveChange();
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 105,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        AssertExactDecisionBucket(classification, 15.0f);
    }

    [TestMethod]
    public void Classify_WhenSnapshotWorldVersionIsStale_UsesExactDecisionBucket()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        session.WorldParentTopology.RegisterWorld(worldId: 7, parentWorldId: 1);
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 106,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        AssertExactDecisionBucket(classification, 15.0f);
    }

    [TestMethod]
    public void Classify_WhenParentWorldIsUnresolved_UsesExactDecisionBucket()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: null,
            snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 107,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        AssertExactDecisionBucket(classification, 15.0f);
    }

    [TestMethod]
    public void Classify_WhenPrimaryElementIsMissing_UsesDedicatedMissingClass()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 108,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: Array.Empty<Tag>(),
            hasPrimaryElement: false,
            temperatureKelvin: 15.0f);

        Assert.AreEqual(
            TemperatureEligibilityClassKey.MissingPrimaryElement(),
            classification);
    }

    [TestMethod]
    public void Classify_WhenSamePickupRepeats_ReturnsCachedFullKey()
    {
        var session = CreateSessionWithEnabledConstraint();
        var iron = new Tag("Iron");
        var snapshot = BuildCurrentEligibilitySnapshot(
            session,
            builder => builder.AddTemperatureConstrainedFetchRequest(
                1,
                [iron],
                Constraint(10, 20)));
        var groupingSession = BeginGroupingSession(session, 1, snapshot);
        var tagIdentity = new PickupTagIdentity(17, iron);

        var firstClassification = groupingSession.Classify(
            pickupInstanceId: 109,
            tagIdentity,
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);
        var repeatedClassification = groupingSession.Classify(
            pickupInstanceId: 109,
            tagIdentity,
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 25.0f);

        Assert.AreEqual(firstClassification, repeatedClassification);
        Assert.AreEqual(1, firstClassification.IntervalOrdinal);
        Assert.AreEqual(1, GetPickupClassificationDictionary(groupingSession).Count);
    }

    [TestMethod]
    public void Classify_WhenSameTagIdentityRepeatsAcrossPickups_ReusesPartitionDefinition()
    {
        var session = CreateSessionWithEnabledConstraint();
        var iron = new Tag("Iron");
        var snapshot = BuildCurrentEligibilitySnapshot(
            session,
            builder => builder.AddTemperatureConstrainedFetchRequest(
                1,
                [iron],
                Constraint(10, 20)));
        var groupingSession = BeginGroupingSession(session, 1, snapshot);
        var tagIdentity = new PickupTagIdentity(17, iron);

        var inside = groupingSession.Classify(
            pickupInstanceId: 110,
            tagIdentity,
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);
        var above = groupingSession.Classify(
            pickupInstanceId: 111,
            tagIdentity,
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 25.0f);

        Assert.AreEqual(inside.PartitionDefinitionId, above.PartitionDefinitionId);
        Assert.AreEqual(1, inside.IntervalOrdinal);
        Assert.AreEqual(2, above.IntervalOrdinal);
    }

    [TestMethod]
    public void Classify_WhenApplicableTagsDiffer_DoesNotReuseWrongUnion()
    {
        var session = CreateSessionWithEnabledConstraint();
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var snapshot = BuildCurrentEligibilitySnapshot(
            session,
            builder =>
            {
                builder.AddTemperatureConstrainedFetchRequest(
                    1,
                    [iron],
                    Constraint(10, 20));
                builder.AddTemperatureConstrainedFetchRequest(
                    1,
                    [copper],
                    Constraint(30, 40));
            });
        var groupingSession = BeginGroupingSession(session, 1, snapshot);
        var sharedTagIdentity = new PickupTagIdentity(17, new Tag("Metal"));

        var ironClassification = groupingSession.Classify(
            pickupInstanceId: 112,
            sharedTagIdentity,
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);
        var copperClassification = groupingSession.Classify(
            pickupInstanceId: 113,
            sharedTagIdentity,
            applicableRequestedTags: [copper],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        Assert.AreNotEqual(
            ironClassification.PartitionDefinitionId,
            copperClassification.PartitionDefinitionId);
        Assert.AreEqual(1, ironClassification.IntervalOrdinal);
        Assert.AreEqual(0, copperClassification.IntervalOrdinal);
    }

    [TestMethod]
    public void Classify_WhenDifferentTagIdentitiesHaveEqualEndpointUnions_InternsOnePartitionDefinition()
    {
        var session = CreateSessionWithEnabledConstraint();
        var iron = new Tag("Iron");
        var copper = new Tag("Copper");
        var snapshot = BuildCurrentEligibilitySnapshot(
            session,
            builder =>
            {
                builder.AddTemperatureConstrainedFetchRequest(
                    1,
                    [iron],
                    Constraint(10, 20));
                builder.AddTemperatureConstrainedFetchRequest(
                    1,
                    [copper],
                    Constraint(10, 20));
            });
        var groupingSession = BeginGroupingSession(session, 1, snapshot);

        var ironClassification = groupingSession.Classify(
            pickupInstanceId: 114,
            new PickupTagIdentity(17, iron),
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);
        var copperClassification = groupingSession.Classify(
            pickupInstanceId: 115,
            new PickupTagIdentity(18, copper),
            applicableRequestedTags: [copper],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        Assert.AreEqual(
            ironClassification.PartitionDefinitionId,
            copperClassification.PartitionDefinitionId);
    }

    [TestMethod]
    public void Classify_WhenCurrentApplicableUnionIsEmpty_ReturnsNoTemperatureDistinction()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        var groupingSession = BeginGroupingSession(session, 1, snapshot);

        var classification = groupingSession.Classify(
            pickupInstanceId: 116,
            new PickupTagIdentity(17, new Tag("Iron")),
            applicableRequestedTags: [new Tag("Iron")],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        Assert.AreEqual(
            TemperatureEligibilityClassKey.NoTemperatureDistinction(),
            classification);
    }

    [TestMethod]
    public void Begin_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var session = CreateSessionWithEnabledConstraint();
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            groupingSession.Begin(
                session,
                resolvedParentWorldId: 1,
                session.TemperatureConstraints.CaptureSnapshot(),
                eligibilitySnapshot: null,
                session.WorldParentTopology.CaptureSnapshot()));
    }

    [TestMethod]
    public void Complete_WhenInactive_IsIdempotent()
    {
        var groupingSession = new PickupTemperatureGroupingSession();

        groupingSession.Complete();
        groupingSession.Complete();
    }

    [TestMethod]
    public void Discard_WhenExceptionOccurs_ReleasesPerCallReferences()
    {
        var session = CreateSessionWithEnabledConstraint();
        var iron = new Tag("Iron");
        var snapshot = BuildCurrentEligibilitySnapshot(
            session,
            builder => builder.AddTemperatureConstrainedFetchRequest(
                parentWorldId: 1,
                requestedTags: [iron],
                enabledConstraint: Constraint(10, 20)));
        var groupingSession = BeginGroupingSession(session, 1, snapshot);
        groupingSession.Classify(
            pickupInstanceId: 117,
            new PickupTagIdentity(17, iron),
            applicableRequestedTags: [iron],
            hasPrimaryElement: true,
            temperatureKelvin: 15.0f);

        groupingSession.Discard();

        Assert.IsNull(GetPrivateFieldValue(
            groupingSession,
            "capturedGameSession"));
        Assert.IsNull(GetPrivateFieldValue(
            groupingSession,
            "capturedActiveTemperatureConstraints"));
        Assert.IsNull(GetPrivateFieldValue(
            groupingSession,
            "capturedEligibilitySnapshot"));
        Assert.IsNull(GetPrivateFieldValue(
            groupingSession,
            "capturedWorldTopology"));
        Assert.IsEmpty(GetPickupClassificationDictionary(groupingSession));
        Assert.IsEmpty(GetPrivateDictionary(
            groupingSession,
            "firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity"));
        Assert.IsEmpty(GetPrivateDictionary(
            groupingSession,
            "temperaturePartitionDefinitionByDecisionEndpoints"));

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        groupingSession.Complete();
    }

    [TestMethod]
    public void Complete_WhenPickupCacheExceededHighWater_ReplacesDictionary()
    {
        var session = CreateSessionWithEnabledConstraint();
        var groupingSession = new PickupTemperatureGroupingSession();
        var retentionLimit =
            RetainedCollectionCapacityLimits
                .MaximumRetainedPickupClassificationCount;

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        var dictionaryAtLimit =
            GetPickupClassificationDictionary(groupingSession);
        ClassifyExactFallbackPickups(
            groupingSession,
            pickupCount: retentionLimit,
            pickupInstanceIdOffset: 0);
        groupingSession.Complete();
        Assert.AreSame(
            dictionaryAtLimit,
            GetPickupClassificationDictionary(groupingSession));

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        var dictionaryBeforeLimitExceeded =
            GetPickupClassificationDictionary(groupingSession);
        ClassifyExactFallbackPickups(
            groupingSession,
            pickupCount: retentionLimit + 1,
            pickupInstanceIdOffset: retentionLimit);
        groupingSession.Complete();
        Assert.AreNotSame(
            dictionaryBeforeLimitExceeded,
            GetPickupClassificationDictionary(groupingSession));

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        var dictionaryBeforeLargerWorkload =
            GetPickupClassificationDictionary(groupingSession);
        ClassifyExactFallbackPickups(
            groupingSession,
            pickupCount: (retentionLimit * 2) + 17,
            pickupInstanceIdOffset: retentionLimit * 3);
        groupingSession.Complete();
        Assert.AreNotSame(
            dictionaryBeforeLargerWorkload,
            GetPickupClassificationDictionary(groupingSession));
    }

    [TestMethod]
    public void Classify_WhenGroupingSessionIsInactive_ThrowsInvalidOperationException()
    {
        var groupingSession = new PickupTemperatureGroupingSession();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            groupingSession.Classify(
                pickupInstanceId: 118,
                new PickupTagIdentity(17, new Tag("Iron")),
                applicableRequestedTags: Array.Empty<Tag>(),
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f));
    }

    [TestMethod]
    public void Classify_WhenApplicableRequestedTagsIsNull_ThrowsArgumentNullException()
    {
        var session = CreateSessionWithEnabledConstraint();
        var groupingSession = BeginGroupingSession(session, 1, null);

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            groupingSession.Classify(
                pickupInstanceId: 119,
                new PickupTagIdentity(17, new Tag("Iron")),
                applicableRequestedTags: null!,
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f));
    }

    private static void ClassifyExactFallbackPickups(
        PickupTemperatureGroupingSession groupingSession,
        int pickupCount,
        int pickupInstanceIdOffset)
    {
        var tagIdentity = new PickupTagIdentity(17, new Tag("Iron"));
        for (var pickupOffset = 0;
             pickupOffset < pickupCount;
             pickupOffset++)
        {
            var classification = groupingSession.Classify(
                checked(pickupInstanceIdOffset + pickupOffset),
                tagIdentity,
                applicableRequestedTags: Array.Empty<Tag>(),
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f);
            AssertExactDecisionBucket(classification, 15.0f);
        }

        Assert.AreEqual(
            pickupCount,
            GetPickupClassificationDictionary(groupingSession).Count);
    }

    private static void AssertExactDecisionBucket(
        TemperatureEligibilityClassKey classification,
        float temperatureKelvin)
    {
        Assert.AreEqual(
            TemperatureEligibilityClassificationKind
                .ExactTemperatureDecisionBucket,
            classification.ClassificationKind);
        Assert.AreEqual(
            TemperatureDecisionBucket.FromTemperature(temperatureKelvin),
            classification.ExactTemperatureDecisionBucket);
    }

    private static DeliveryTemperatureGameSession
        CreateSessionWithoutEnabledConstraints() =>
        new DeliveryTemperatureGameSession(
            new GameSessionGeneration(1),
            gameInstanceId: 1);

    private static DeliveryTemperatureGameSession
        CreateSessionWithEnabledConstraint()
    {
        var session = CreateSessionWithoutEnabledConstraints();
        session.RegisterTemperatureLimit(
            gameObjectInstanceId: 10001,
            componentInstanceId: 10002,
            new TemperatureLimit(),
            Constraint(10, 20));
        return session;
    }

    private static FetchTemperatureEligibilitySnapshot
        BuildCurrentEligibilitySnapshot(
            DeliveryTemperatureGameSession session,
            Action<FetchTemperatureEligibilityBuilder> contributeRequests)
    {
        var builder = new FetchTemperatureEligibilityBuilder();
        builder.Begin(
            session.Generation,
            session.TemperatureConstraints.CaptureSnapshot(),
            session.FetchRequestTopology.CaptureVersion(),
            session.WorldParentTopology.CaptureSnapshot());
        contributeRequests(builder);
        return builder.Build();
    }

    private static PickupTemperatureGroupingSession BeginGroupingSession(
        DeliveryTemperatureGameSession session,
        int? resolvedParentWorldId,
        FetchTemperatureEligibilitySnapshot? eligibilitySnapshot)
    {
        var groupingSession = new PickupTemperatureGroupingSession();
        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId,
            eligibilitySnapshot);
        return groupingSession;
    }

    private static void BeginGroupingSession(
        PickupTemperatureGroupingSession groupingSession,
        DeliveryTemperatureGameSession session,
        int? resolvedParentWorldId,
        FetchTemperatureEligibilitySnapshot? eligibilitySnapshot)
    {
        groupingSession.Begin(
            session,
            resolvedParentWorldId,
            session.TemperatureConstraints.CaptureSnapshot(),
            eligibilitySnapshot,
            session.WorldParentTopology.CaptureSnapshot());
    }

    private static IDictionary GetPickupClassificationDictionary(
        PickupTemperatureGroupingSession groupingSession) =>
        GetPrivateDictionary(
            groupingSession,
            "temperatureClassesByPickupInstanceId");

    private static IDictionary GetPrivateDictionary(
        PickupTemperatureGroupingSession groupingSession,
        string fieldName) =>
        Assert.IsInstanceOfType<IDictionary>(GetPrivateFieldValue(
            groupingSession,
            fieldName));

    private static object? GetPrivateFieldValue(
        PickupTemperatureGroupingSession groupingSession,
        string fieldName)
    {
        var field = typeof(PickupTemperatureGroupingSession).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"Expected one predeclared private field named '{fieldName}'.");
        return field.GetValue(groupingSession);
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);
}
