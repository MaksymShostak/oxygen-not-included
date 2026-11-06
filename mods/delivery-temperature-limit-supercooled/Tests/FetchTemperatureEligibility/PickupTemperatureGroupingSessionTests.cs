using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.FetchTemperatureEligibility;
public sealed class PickupTemperatureGroupingSessionTests
{
    [Test]
    public async Task Classify_WhenNoEnabledConstraints_ReturnsNoTemperatureDistinctionWithoutCacheGrowth()
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

        await Assert.That(classification).IsEqualTo(TemperatureEligibilityClassKey.NoTemperatureDistinction());
        await Assert.That(((System.Collections.IEnumerable)(await GetPickupClassificationDictionary(groupingSession))).Cast<object?>()).IsEmpty();
    }

    [Test]
    public async Task Classify_WhenSnapshotIsCurrent_UsesScopedPartition()
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

        await Assert.That(classification.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind
                .OptimizedPartitionInterval);
        await Assert.That(classification.PartitionDefinitionId).IsEqualTo(1);
        await Assert.That(classification.IntervalOrdinal).IsEqualTo(1);
    }

    [Test]
    public async Task Classify_WhenSnapshotIsNull_UsesExactDecisionBucket()
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

        await AssertExactDecisionBucket(classification, 15.75f);
    }

    [Test]
    public async Task Classify_WhenSnapshotConstraintGenerationIsStale_UsesExactDecisionBucket()
    {
        var session = CreateSessionWithEnabledConstraint();
        var snapshot = BuildCurrentEligibilitySnapshot(session, _ => { });
        session.TemperatureConstraints.Register(
            componentInstanceId: 10401,
            Constraint(30, 40),
            out var stateChanged);
        await Assert.That(stateChanged).IsTrue();
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

        await AssertExactDecisionBucket(classification, 15.0f);
    }

    [Test]
    public async Task Classify_WhenSnapshotFetchVersionIsStale_UsesExactDecisionBucket()
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

        await AssertExactDecisionBucket(classification, 15.0f);
    }

    [Test]
    public async Task Classify_WhenSnapshotWorldVersionIsStale_UsesExactDecisionBucket()
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

        await AssertExactDecisionBucket(classification, 15.0f);
    }

    [Test]
    public async Task Classify_WhenParentWorldIsUnresolved_UsesExactDecisionBucket()
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

        await AssertExactDecisionBucket(classification, 15.0f);
    }

    [Test]
    public async Task Classify_WhenPrimaryElementIsMissing_UsesDedicatedMissingClass()
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

        await Assert.That(classification).IsEqualTo(TemperatureEligibilityClassKey.MissingPrimaryElement());
    }

    [Test]
    public async Task Classify_WhenSamePickupRepeats_ReturnsCachedFullKey()
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

        await Assert.That(repeatedClassification).IsEqualTo(firstClassification);
        await Assert.That(firstClassification.IntervalOrdinal).IsEqualTo(1);
        await Assert.That((await GetPickupClassificationDictionary(groupingSession)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Classify_WhenSameTagIdentityRepeatsAcrossPickups_ReusesPartitionDefinition()
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

        await Assert.That(above.PartitionDefinitionId).IsEqualTo(inside.PartitionDefinitionId);
        await Assert.That(inside.IntervalOrdinal).IsEqualTo(1);
        await Assert.That(above.IntervalOrdinal).IsEqualTo(2);
    }

    [Test]
    public async Task ClassifyUsingApplicableRequestedTagResolver_WhenNoConstraintsAreEnabled_BypassesResolverAndEveryTemperatureCache()
    {
        var session = CreateSessionWithoutEnabledConstraints();
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: null,
            eligibilitySnapshot: null);
        var resolverWasInvoked = false;
        PickupTemperatureGroupingSession
            .ApplicableRequestedTagResolver<Tag> resolver =
                (_, _) =>
                {
                    resolverWasInvoked = true;
                    return Array.Empty<Tag>();
                };

        var classification = groupingSession
            .ClassifyUsingApplicableRequestedTagResolver(
                pickupInstanceId: 122,
                new PickupTagIdentity(17, new Tag("Iron")),
                new Tag("Iron"),
                resolver,
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f);

        await Assert.That(classification).IsEqualTo(TemperatureEligibilityClassKey.NoTemperatureDistinction());
        await Assert.That(resolverWasInvoked).IsFalse();
        await Assert.That(((System.Collections.IEnumerable)(await GetPickupClassificationDictionary(groupingSession))).Cast<object?>()).IsEmpty();
        await Assert.That(((System.Collections.IEnumerable)(await GetPrivateDictionary(
            groupingSession,
            "applicableRequestedTagsByPickupTagIdentity"))).Cast<object?>()).IsEmpty();
    }

    [Test]
    public async Task ClassifyUsingApplicableRequestedTagResolver_WhenTenThousandPickupsShareCompleteTagIdentity_ResolvesApplicableTagsOnce()
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
        var applicableTagResolverInvocationCount = 0;
        PickupTemperatureGroupingSession
            .ApplicableRequestedTagResolver<Tag> resolver =
                (prefabTag, requestedTags) =>
                {
                    applicableTagResolverInvocationCount++;
                    return requestedTags
                        .Where(requestedTag => requestedTag.Equals(prefabTag))
                        .ToArray();
                };

        for (var pickupInstanceId = 1;
             pickupInstanceId <= 10_000;
             pickupInstanceId++)
        {
            var classification = groupingSession
                .ClassifyUsingApplicableRequestedTagResolver(
                    pickupInstanceId,
                    tagIdentity,
                    iron,
                    resolver,
                    hasPrimaryElement: true,
                    temperatureKelvin: 15.0f);

            await Assert.That(classification.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind
                    .OptimizedPartitionInterval);
        }

        await Assert.That(applicableTagResolverInvocationCount).IsEqualTo(1);
    }

    [Test]
    public async Task ClassifyUsingApplicableRequestedTagResolver_WhenOriginalHashesMatchButPrefabTagsDiffer_ResolvesEachCompleteIdentitySeparately()
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
        var applicableTagResolverInvocationCount = 0;
        PickupTemperatureGroupingSession
            .ApplicableRequestedTagResolver<Tag> resolver =
                (prefabTag, requestedTags) =>
                {
                    applicableTagResolverInvocationCount++;
                    return requestedTags
                        .Where(requestedTag => requestedTag.Equals(prefabTag))
                        .ToArray();
                };

        var ironClassification = groupingSession
            .ClassifyUsingApplicableRequestedTagResolver(
                pickupInstanceId: 120,
                new PickupTagIdentity(17, iron),
                iron,
                resolver,
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f);
        var copperClassification = groupingSession
            .ClassifyUsingApplicableRequestedTagResolver(
                pickupInstanceId: 121,
                new PickupTagIdentity(17, copper),
                copper,
                resolver,
                hasPrimaryElement: true,
                temperatureKelvin: 35.0f);

        await Assert.That(applicableTagResolverInvocationCount).IsEqualTo(2);
        await Assert.That(copperClassification.PartitionDefinitionId).IsNotEqualTo(ironClassification.PartitionDefinitionId);
    }

    [Test]
    public async Task Classify_WhenApplicableTagsDiffer_DoesNotReuseWrongUnion()
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

        await Assert.That(copperClassification.PartitionDefinitionId).IsNotEqualTo(ironClassification.PartitionDefinitionId);
        await Assert.That(ironClassification.IntervalOrdinal).IsEqualTo(1);
        await Assert.That(copperClassification.IntervalOrdinal).IsEqualTo(0);
    }

    [Test]
    public async Task Classify_WhenDifferentTagIdentitiesHaveEqualEndpointUnions_InternsOnePartitionDefinition()
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

        await Assert.That(copperClassification.PartitionDefinitionId).IsEqualTo(ironClassification.PartitionDefinitionId);
    }

    [Test]
    public async Task Classify_WhenCurrentApplicableUnionIsEmpty_ReturnsNoTemperatureDistinction()
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

        await Assert.That(classification).IsEqualTo(TemperatureEligibilityClassKey.NoTemperatureDistinction());
    }

    [Test]
    public async Task Begin_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var session = CreateSessionWithEnabledConstraint();
        var groupingSession = BeginGroupingSession(
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);

        await Assert.That(() =>
            groupingSession.Begin(
                session,
                resolvedParentWorldId: 1,
                session.TemperatureConstraints.CaptureSnapshot(),
                eligibilitySnapshot: null,
                session.WorldParentTopology.CaptureSnapshot())).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public void Complete_WhenInactive_IsIdempotent()
    {
        var groupingSession = new PickupTemperatureGroupingSession();

        groupingSession.Complete();
        groupingSession.Complete();
    }

    [Test]
    public async Task Discard_WhenExceptionOccurs_ReleasesPerCallReferences()
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

        await Assert.That((await GetPrivateFieldValue(
            groupingSession,
            "capturedGameSession"))).IsNull();
        await Assert.That((await GetPrivateFieldValue(
            groupingSession,
            "capturedActiveTemperatureConstraints"))).IsNull();
        await Assert.That((await GetPrivateFieldValue(
            groupingSession,
            "capturedEligibilitySnapshot"))).IsNull();
        await Assert.That((await GetPrivateFieldValue(
            groupingSession,
            "capturedWorldTopology"))).IsNull();
        await Assert.That((await GetPrivateFieldValue(
            groupingSession,
            "capturedRequestedTagsForResolvedParentWorld"))).IsNull();
        await Assert.That(((System.Collections.IEnumerable)(await GetPickupClassificationDictionary(groupingSession))).Cast<object?>()).IsEmpty();
        await Assert.That(((System.Collections.IEnumerable)(await GetPrivateDictionary(
            groupingSession,
            "applicableRequestedTagsByPickupTagIdentity"))).Cast<object?>()).IsEmpty();
        await Assert.That(((System.Collections.IEnumerable)(await GetPrivateDictionary(
            groupingSession,
            "firstApplicableRequestedTagPartitionResolutionByPickupTagIdentity"))).Cast<object?>()).IsEmpty();
        await Assert.That(((System.Collections.IEnumerable)(await GetPrivateDictionary(
            groupingSession,
            "temperaturePartitionDefinitionByDecisionEndpoints"))).Cast<object?>()).IsEmpty();

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        groupingSession.Complete();
    }

    [Test]
    public async Task Complete_WhenPickupCacheExceededHighWater_ReplacesDictionary()
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
            await GetPickupClassificationDictionary(groupingSession);
        await ClassifyExactFallbackPickups(
            groupingSession,
            pickupCount: retentionLimit,
            pickupInstanceIdOffset: 0);
        groupingSession.Complete();
        await Assert.That((await GetPickupClassificationDictionary(groupingSession))).IsSameReferenceAs(dictionaryAtLimit);

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        var dictionaryBeforeLimitExceeded =
            await GetPickupClassificationDictionary(groupingSession);
        await ClassifyExactFallbackPickups(
            groupingSession,
            pickupCount: retentionLimit + 1,
            pickupInstanceIdOffset: retentionLimit);
        groupingSession.Complete();
        await Assert.That((await GetPickupClassificationDictionary(groupingSession))).IsNotSameReferenceAs(dictionaryBeforeLimitExceeded);

        BeginGroupingSession(
            groupingSession,
            session,
            resolvedParentWorldId: 1,
            eligibilitySnapshot: null);
        var dictionaryBeforeLargerWorkload =
            await GetPickupClassificationDictionary(groupingSession);
        await ClassifyExactFallbackPickups(
            groupingSession,
            pickupCount: (retentionLimit * 2) + 17,
            pickupInstanceIdOffset: retentionLimit * 3);
        groupingSession.Complete();
        await Assert.That((await GetPickupClassificationDictionary(groupingSession))).IsNotSameReferenceAs(dictionaryBeforeLargerWorkload);
    }

    [Test]
    public async Task Classify_WhenGroupingSessionIsInactive_ThrowsInvalidOperationException()
    {
        var groupingSession = new PickupTemperatureGroupingSession();

        await Assert.That(() =>
            groupingSession.Classify(
                pickupInstanceId: 118,
                new PickupTagIdentity(17, new Tag("Iron")),
                applicableRequestedTags: Array.Empty<Tag>(),
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Classify_WhenApplicableRequestedTagsIsNull_ThrowsArgumentNullException()
    {
        var session = CreateSessionWithEnabledConstraint();
        var groupingSession = BeginGroupingSession(session, 1, null);

        await Assert.That(() =>
            groupingSession.Classify(
                pickupInstanceId: 119,
                new PickupTagIdentity(17, new Tag("Iron")),
                applicableRequestedTags: null!,
                hasPrimaryElement: true,
                temperatureKelvin: 15.0f)).ThrowsExactly<ArgumentNullException>();
    }

    private static async Task ClassifyExactFallbackPickups(
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
            await AssertExactDecisionBucket(classification, 15.0f);
        }

        await Assert.That((await GetPickupClassificationDictionary(groupingSession)).Count).IsEqualTo(pickupCount);
    }

    private static async Task AssertExactDecisionBucket(
        TemperatureEligibilityClassKey classification,
        float temperatureKelvin)
    {
        await Assert.That(classification.ClassificationKind).IsEqualTo(TemperatureEligibilityClassificationKind
                .ExactTemperatureDecisionBucket);
        await Assert.That(classification.ExactTemperatureDecisionBucket).IsEqualTo(TemperatureDecisionBucket.FromTemperature(temperatureKelvin));
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

    private static async Task<IDictionary> GetPickupClassificationDictionary(
        PickupTemperatureGroupingSession groupingSession) =>
        await GetPrivateDictionary(
            groupingSession,
            "temperatureClassesByPickupInstanceId");

    private static async Task<IDictionary> GetPrivateDictionary(
        PickupTemperatureGroupingSession groupingSession,
        string fieldName) =>
        (await Assert.That((await GetPrivateFieldValue(
            groupingSession,
            fieldName))).IsAssignableTo<IDictionary>())!;

    private static async Task<object?> GetPrivateFieldValue(
        PickupTemperatureGroupingSession groupingSession,
        string fieldName)
    {
        var field = typeof(PickupTemperatureGroupingSession).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"Expected one predeclared private field named '{fieldName}'.");
        return field.GetValue(groupingSession);
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);
}
