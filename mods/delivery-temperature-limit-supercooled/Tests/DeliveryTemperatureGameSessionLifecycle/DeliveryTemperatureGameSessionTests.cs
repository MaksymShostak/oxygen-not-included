using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;
[NotInParallel]
public sealed class DeliveryTemperatureGameSessionTests
{
    private const int LifecycleScheduleSeed = 0x5E5510;
    private const int LifecycleScheduleOperationCount = 10000;
    private const int ScheduleGameObjectIdentityCount = 32;

    private readonly HashSet<int> gameInstanceIdsForCleanup = new();

    [After(HookType.Test)]
    public void CompleteAnyTrackedCurrentGameSession()
    {
        // Tests exercise the same two-phase lifecycle used by the eventual Harmony
        // prefix/finalizer pair. No production reset hook or test-only branch exists.
        foreach (var gameInstanceId in gameInstanceIdsForCleanup)
        {
            var detachedSession =
                DeliveryTemperatureGameSessionHost.DetachGameSession(gameInstanceId);
            DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        }
    }

    [Test]
    public async Task StopCurrentSessionPublications_MakesTheSessionUnavailableWithoutReleasingInFlightState()
    {
        var session = EnsureTrackedGameSession(5199);
        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out var captured)).IsTrue();
        await Assert.That(captured).IsSameReferenceAs(session);

        DeliveryTemperatureGameSessionHost.StopCurrentSessionPublications();

        await Assert.That(session.IsAcceptingPublications).IsFalse();
        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _)).IsFalse();
        // Failure containment stops new work immediately. The ordinary teardown
        // still owns detachment and release after workers have unwound.
        await Assert.That(DeliveryTemperatureGameSessionHost.DetachGameSession(5199)).IsSameReferenceAs(session);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(session);
    }

    [Test]
    public async Task EnsureGameSession_WhenGameIdentityChanges_DetachesAndInvalidatesOldSession()
    {
        var oldSession = EnsureTrackedGameSession(5101);

        var newSession = EnsureTrackedGameSession(5102);

        await Assert.That(newSession).IsNotSameReferenceAs(oldSession);
        await Assert.That(oldSession.IsAcceptingPublications).IsFalse();
        await Assert.That(newSession.IsAcceptingPublications).IsTrue();
        await Assert.That(newSession.Generation).IsNotEqualTo(oldSession.Generation);
    }

    [Test]
    public async Task EnsureGameSession_WhenIdentityMatches_ReturnsSameSession()
    {
        var firstCapture = EnsureTrackedGameSession(5201);

        var repeatedCapture = EnsureTrackedGameSession(5201);

        await Assert.That(repeatedCapture).IsSameReferenceAs(firstCapture);
        await Assert.That(repeatedCapture.Generation).IsEqualTo(firstCapture.Generation);
        await Assert.That(repeatedCapture.IsAcceptingPublications).IsTrue();
    }

    [Test]
    public async Task EnsureGameSession_WhenSameIdentityIsEnsuredConcurrently_PublishesOneSession()
    {
        const int concurrentCallerCount = 32;
        const int gameInstanceId = 5251;
        gameInstanceIdsForCleanup.Add(gameInstanceId);
        using var startSignal = new ManualResetEventSlim(initialState: false);
        var callers = new Task<DeliveryTemperatureGameSession>[
            concurrentCallerCount];

        for (var callerIndex = 0;
             callerIndex < callers.Length;
             callerIndex++)
        {
            callers[callerIndex] = Task.Run(() =>
            {
                startSignal.Wait();
                return DeliveryTemperatureGameSessionHost.EnsureGameSession(
                    gameInstanceId);
            });
        }

        startSignal.Set();
        var observedSessions = await Task.WhenAll(callers);
        var publishedSession = observedSessions[0];

        foreach (var observedSession in observedSessions)
        {
            await Assert.That(observedSession).IsSameReferenceAs(publishedSession);
        }

        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var capturedSession)).IsTrue();
        await Assert.That(capturedSession).IsSameReferenceAs(publishedSession);
        await Assert.That(publishedSession.IsAcceptingPublications).IsTrue();
    }

    [Test]
    public async Task TryCaptureCurrent_WhenNoSession_ReturnsFalse()
    {
        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var capturedSession)).IsFalse();
        await Assert.That(capturedSession).IsNull();
    }

    [Test]
    public async Task DetachGameSession_WhenIdentityMatches_StopsAndReturnsSession()
    {
        var session = EnsureTrackedGameSession(5301);

        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5301);

        await Assert.That(detachedSession).IsSameReferenceAs(session);
        await Assert.That(session.IsAcceptingPublications).IsFalse();
        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _)).IsFalse();
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
    }

    [Test]
    public async Task DetachGameSession_WhenIdentityDiffers_DoesNotDetachCurrentSession()
    {
        var session = EnsureTrackedGameSession(5401);

        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5402);

        await Assert.That(detachedSession).IsNull();
        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var capturedSession)).IsTrue();
        await Assert.That(capturedSession).IsSameReferenceAs(session);
        await Assert.That(session.IsAcceptingPublications).IsTrue();
    }

    [Test]
    public async Task CompleteShutdown_WhenCalledTwice_IsIdempotent()
    {
        var session = EnsureTrackedGameSession(5501);
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5501);
        await Assert.That(detachedSession).IsSameReferenceAs(session);

        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);

        await Assert.That(session.IsAcceptingPublications).IsFalse();
        await Assert.That(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 55011,
                componentInstanceId: 55012,
                new TemperatureLimit(),
                Constraint(10, 20))).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task EnsureGameSession_WhenCreated_ConstructsWorldParentTopologyForSameGeneration()
    {
        var session = EnsureTrackedGameSession(5551);

        var topologySnapshot = session.WorldParentTopology.CaptureSnapshot();

        await Assert.That(topologySnapshot.GameSessionGeneration).IsEqualTo(session.Generation);
        await Assert.That(topologySnapshot.Version.Value).IsEqualTo(0L);
        await Assert.That(topologySnapshot.TryResolveParentWorld(0, out _)).IsFalse();
    }

    [Test]
    public async Task EnsureGameSession_WhenCreated_ConstructsOwnedWorldResourceTemperatureAmountCatalog()
    {
        var session = EnsureTrackedGameSession(55511);
        var generation = new WorldInventoryCollectionGeneration(1);

        session.WorldResourceTemperatureAmounts.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);

        await Assert.That(session.WorldResourceTemperatureAmounts
                .GetWorldResourceTagCoverageRequirementState(7, generation)).IsEqualTo(WorldResourceTagCoverageRequirementState.CoverageRequired);
    }

    [Test]
    public async Task CompleteShutdown_WhenTopologySnapshotWasCaptured_ClearsOwnedMappingsWithoutMutatingSnapshot()
    {
        var session = EnsureTrackedGameSession(5552);
        session.WorldParentTopology.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);
        var capturedSnapshot = session.WorldParentTopology.CaptureSnapshot();
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5552);

        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);

        await Assert.That(capturedSnapshot.TryResolveParentWorld(
            7,
            out var capturedParentWorldId)).IsTrue();
        await Assert.That(capturedParentWorldId).IsEqualTo(1);
        await Assert.That(session.WorldParentTopology.CaptureSnapshot()).IsSameReferenceAs(capturedSnapshot);
        var ownedMappingField = typeof(WorldParentTopologyCatalog).GetField(
            "parentWorldIdsByWorldId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (ownedMappingField is null) throw new TUnit.Assertions.Exceptions.AssertionException("Shutdown structure requires the exact private owned mapping field " +
            "WorldParentTopologyCatalog.parentWorldIdsByWorldId.");
        var ownedMappings = (await Assert.That(ownedMappingField.GetValue(session.WorldParentTopology)).IsAssignableTo<IDictionary<int, int>>())!;
        await Assert.That(ownedMappings.Count).IsEqualTo(0);
        var exception = (await Assert.That(() =>
            session.WorldParentTopology.RegisterWorld(
                worldId: 8,
                parentWorldId: 1)).ThrowsExactly<InvalidOperationException>())!;
        await Assert.That(exception.Message).Contains("no longer accepts publications");
    }

    [Test]
    public async Task CompleteShutdown_WhenWorldResourceAmountsWerePublished_ClearsCatalogOnceAndRejectsLatePublication()
    {
        var session = EnsureTrackedGameSession(55521);
        var generation = new WorldInventoryCollectionGeneration(1);
        session.WorldResourceTemperatureAmounts.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(generation);
        builder.BeginResourceTag(new Tag("Iron"));
        builder.AddTemperatureAmount(300.0f, 10.0f);
        builder.CompleteResourceTag();
        var publication = builder.Build();
        await Assert.That(session.WorldResourceTemperatureAmounts
                .PublishCompleteWorldResourceAmounts(7, publication)).IsTrue();
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(55521);

        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);

        await Assert.That(session.WorldResourceTemperatureAmounts
                .PublishCompleteWorldResourceAmounts(7, publication)).IsFalse();
        var catalogDictionaryFields = typeof(WorldResourceTemperatureAmountCatalog)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => typeof(IDictionary).IsAssignableFrom(field.FieldType))
            .ToArray();
        await Assert.That(catalogDictionaryFields).IsNotEmpty();
        foreach (var catalogDictionaryField in catalogDictionaryFields)
        {
            var retainedState = (await Assert.That(catalogDictionaryField.GetValue(
                    session.WorldResourceTemperatureAmounts)).IsAssignableTo<IDictionary>())!;
            await Assert.That(((System.Collections.IEnumerable)retainedState).Cast<object?>()).IsEmpty().Because($"Shutdown retained catalog state in " +
                $"{catalogDictionaryField.Name}.");
        }
    }

    [Test]
    public async Task EnsureGameSession_WhenCreated_ConstructsFetchServicesInBypassState()
    {
        var session = EnsureTrackedGameSession(55522);

        Assert.NotNull(session.FetchRequestTopology);
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(0L);
        await Assert.That(session.CurrentWorldInventoryCollectionGeneration.Value).IsEqualTo(0L);
        await Assert.That(session.CurrentFetchTemperatureEligibility).IsNull();
    }

    [Test]
    public async Task TryPublishFetchTemperatureEligibility_WhenEveryVersionMatches_PublishesWholeCandidate()
    {
        var session = EnsureTrackedGameSession(55523);
        var candidate = BuildCurrentFetchTemperatureEligibilityCandidate(session);

        var published = session.TryPublishFetchTemperatureEligibility(candidate);

        await Assert.That(published).IsTrue();
        await Assert.That(session.CurrentFetchTemperatureEligibility).IsSameReferenceAs(candidate);
    }

    [Test]
    public async Task TryPublishFetchTemperatureEligibility_WhenGameSessionWasDetached_RejectsCandidateAndPreservesPublication()
    {
        var oldSession = EnsureTrackedGameSession(55524);
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(oldSession);
        var currentSession = EnsureTrackedGameSession(55525);
        var priorPublication =
            BuildCurrentFetchTemperatureEligibilityCandidate(currentSession);
        await Assert.That(currentSession.TryPublishFetchTemperatureEligibility(priorPublication)).IsTrue();

        var published =
            currentSession.TryPublishFetchTemperatureEligibility(staleCandidate);

        await Assert.That(published).IsFalse();
        await Assert.That(currentSession.CurrentFetchTemperatureEligibility).IsSameReferenceAs(priorPublication);
    }

    [Test]
    public async Task TryPublishFetchTemperatureEligibility_WhenConstraintGenerationChanged_RejectsCandidateAndPreservesPublication()
    {
        var session = EnsureTrackedGameSession(55526);
        var priorPublication = BuildCurrentFetchTemperatureEligibilityCandidate(session);
        await Assert.That(session.TryPublishFetchTemperatureEligibility(priorPublication)).IsTrue();
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(session);
        session.TemperatureConstraints.Register(
            componentInstanceId: 555261,
            Constraint(10, 20),
            out var effectiveStateChanged);
        await Assert.That(effectiveStateChanged).IsTrue();

        var published =
            session.TryPublishFetchTemperatureEligibility(staleCandidate);

        await Assert.That(published).IsFalse();
        await Assert.That(session.CurrentFetchTemperatureEligibility).IsSameReferenceAs(priorPublication);
    }

    [Test]
    public async Task TryPublishFetchTemperatureEligibility_WhenFetchTopologyVersionChanged_RejectsCandidateAndPreservesPublication()
    {
        var session = EnsureTrackedGameSession(55527);
        var priorPublication = BuildCurrentFetchTemperatureEligibilityCandidate(session);
        await Assert.That(session.TryPublishFetchTemperatureEligibility(priorPublication)).IsTrue();
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(session);
        session.FetchRequestTopology.RecordEffectiveChange();

        var published =
            session.TryPublishFetchTemperatureEligibility(staleCandidate);

        await Assert.That(published).IsFalse();
        await Assert.That(session.CurrentFetchTemperatureEligibility).IsSameReferenceAs(priorPublication);
    }

    [Test]
    public async Task TryPublishFetchTemperatureEligibility_WhenWorldTopologyVersionChanged_RejectsCandidateAndPreservesPublication()
    {
        var session = EnsureTrackedGameSession(55528);
        var priorPublication = BuildCurrentFetchTemperatureEligibilityCandidate(session);
        await Assert.That(session.TryPublishFetchTemperatureEligibility(priorPublication)).IsTrue();
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(session);
        session.WorldParentTopology.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);

        var published =
            session.TryPublishFetchTemperatureEligibility(staleCandidate);

        await Assert.That(published).IsFalse();
        await Assert.That(session.CurrentFetchTemperatureEligibility).IsSameReferenceAs(priorPublication);
    }

    [Test]
    public async Task RegisterTemperatureLimit_WhenEffectiveConstraintChanges_RecordsOneFetchTopologyChange()
    {
        var session = EnsureTrackedGameSession(55529);
        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 555291,
            componentInstanceId: 555292,
            new TemperatureLimit(),
            Constraint(10, 20));
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(1L);

        await Assert.That(session.TryReplaceTemperatureConstraint(
            registration,
            Constraint(10, 20))).IsTrue();
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(1L);

        await Assert.That(session.TryReplaceTemperatureConstraint(
            registration,
            Constraint(30, 40))).IsTrue();
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(2L);

        session.RemoveTemperatureLimit(registration);
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(3L);
    }

    [Test]
    public async Task RegisterWorld_WhenNew_UpdatesTopologyInventoryAndFetchVersionOnce()
    {
        var session = EnsureTrackedGameSession(55530);
        var arbitraryPositiveCollectionGeneration =
            new WorldInventoryCollectionGeneration(1);

        var change = session.RegisterWorld(worldId: 7, parentWorldId: 1);

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(1L);
        await Assert.That(session.WorldParentTopology.CaptureSnapshot()
            .TryResolveParentWorld(7, out var parentWorldId)).IsTrue();
        await Assert.That(parentWorldId).IsEqualTo(1);
        await Assert.That(session.WorldResourceTemperatureAmounts
                .GetWorldResourceTagCoverageRequirementState(
                    7,
                    arbitraryPositiveCollectionGeneration)).IsEqualTo(WorldResourceTagCoverageRequirementState.CoverageRequired);
    }

    [Test]
    public async Task RegisterWorld_WhenIdentical_DoesNotAdvanceFetchVersion()
    {
        var session = EnsureTrackedGameSession(555301);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var topologyBeforeRepeatedRegistration =
            session.WorldParentTopology.CaptureSnapshot();
        var fetchTopologyVersionBeforeRepeatedRegistration =
            session.FetchRequestTopology.CaptureVersion();

        var repeatedChange = session.RegisterWorld(worldId: 7, parentWorldId: 1);

        await Assert.That(repeatedChange.HasChanged).IsFalse();
        await Assert.That(session.WorldParentTopology.CaptureSnapshot()).IsSameReferenceAs(topologyBeforeRepeatedRegistration);
        await Assert.That(session.FetchRequestTopology.CaptureVersion()).IsEqualTo(fetchTopologyVersionBeforeRepeatedRegistration);
    }

    [Test]
    public async Task RegisterWorld_WhenReparented_InvalidatesBothInventoryParentsAndAdvancesFetchVersionOnce()
    {
        var session = EnsureTrackedGameSession(555302);
        var collectionGeneration = new WorldInventoryCollectionGeneration(1);
        var iron = new Tag("Iron");
        var constraint = Constraint(0, 1000);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        session.RegisterWorld(worldId: 8, parentWorldId: 1);
        await PublishCompleteWorldResourceAmount(
            session,
            worldId: 7,
            collectionGeneration,
            iron,
            amount: 10.0f);
        await PublishCompleteWorldResourceAmount(
            session,
            worldId: 8,
            collectionGeneration,
            iron,
            amount: 20.0f);
        await Assert.That((await RequireCompleteTemperatureConstrainedAmount(
                session,
                parentWorldId: 1,
                iron,
                constraint,
                collectionGeneration))).IsEqualTo(30.0f);
        var fetchTopologyVersionBeforeReparenting =
            session.FetchRequestTopology.CaptureVersion();

        var change = session.RegisterWorld(worldId: 7, parentWorldId: 2);

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(change.PreviousParentWorldId).IsEqualTo(1);
        await Assert.That(change.CurrentParentWorldId).IsEqualTo(2);
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(fetchTopologyVersionBeforeReparenting.Value + 1L);
        await Assert.That((await RequireCompleteTemperatureConstrainedAmount(
                session,
                parentWorldId: 1,
                iron,
                constraint,
                collectionGeneration))).IsEqualTo(20.0f);
        await Assert.That((await RequireCompleteTemperatureConstrainedAmount(
                session,
                parentWorldId: 2,
                iron,
                constraint,
                collectionGeneration))).IsEqualTo(10.0f);
    }

    [Test]
    public async Task RemoveWorld_WhenKnown_RemovesTopologyAndInventoryBeforeAdvancingFetchVersion()
    {
        var session = EnsureTrackedGameSession(55531);
        var arbitraryPositiveCollectionGeneration =
            new WorldInventoryCollectionGeneration(1);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);

        var change = session.RemoveWorld(worldId: 7);

        await Assert.That(change.HasChanged).IsTrue();
        await Assert.That(session.FetchRequestTopology.CaptureVersion().Value).IsEqualTo(2L);
        await Assert.That(session.WorldParentTopology.CaptureSnapshot()
            .TryResolveParentWorld(7, out _)).IsFalse();
        await Assert.That(session.WorldResourceTemperatureAmounts
                .GetWorldResourceTagCoverageRequirementState(
                    7,
                    arbitraryPositiveCollectionGeneration)).IsEqualTo(WorldResourceTagCoverageRequirementState
                .UnknownWorldOrCollectionGeneration);
    }

    [Test]
    public async Task RemoveWorld_WhenUnknown_IsIdempotent()
    {
        var session = EnsureTrackedGameSession(555311);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var topologyBeforeUnknownRemoval =
            session.WorldParentTopology.CaptureSnapshot();
        var fetchTopologyVersionBeforeUnknownRemoval =
            session.FetchRequestTopology.CaptureVersion();

        var change = session.RemoveWorld(worldId: 999);

        await Assert.That(change.HasChanged).IsFalse();
        await Assert.That(session.WorldParentTopology.CaptureSnapshot()).IsSameReferenceAs(topologyBeforeUnknownRemoval);
        await Assert.That(session.FetchRequestTopology.CaptureVersion()).IsEqualTo(fetchTopologyVersionBeforeUnknownRemoval);
    }

    [Test]
    public async Task StopAcceptingPublications_WhenLateWorldCallbackArrives_RejectsMutation()
    {
        var session = EnsureTrackedGameSession(555312);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var topologyAtShutdownBoundary =
            session.WorldParentTopology.CaptureSnapshot();
        var fetchTopologyVersionAtShutdownBoundary =
            session.FetchRequestTopology.CaptureVersion();
        session.StopAcceptingPublications();

        await Assert.That(() =>
            session.RegisterWorld(worldId: 8, parentWorldId: 1)).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() =>
            session.RemoveWorld(worldId: 7)).ThrowsExactly<InvalidOperationException>();
        await Assert.That(session.WorldParentTopology.CaptureSnapshot()).IsSameReferenceAs(topologyAtShutdownBoundary);
        await Assert.That(session.FetchRequestTopology.CaptureVersion()).IsEqualTo(fetchTopologyVersionAtShutdownBoundary);
    }

    [Test]
    public async Task WorldLifecycleOperations_WhenInspected_DoNotRetainCrossCatalogMutationLock()
    {
        var crossCatalogMutationLock = typeof(DeliveryTemperatureGameSession)
            .GetField(
                "worldTopologyMutationLock",
                BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(crossCatalogMutationLock).IsNull().Because("World topology and amount-catalog mutations must release each " +
            "owned catalog lock before entering the next catalog; a session-level " +
            "outer lock would nest those synchronization domains.");
    }

    [Test]
    public async Task TemperatureLimitLifecycle_WhenEnabledCountCrossesZero_AdvancesGenerationAndClearsAmountsWithoutLosingWorlds()
    {
        var session = EnsureTrackedGameSession(55532);
        var iron = new Tag("Iron");
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var ownedCatalog = session.WorldResourceTemperatureAmounts;
        var firstRegistration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 555321,
            componentInstanceId: 555322,
            new TemperatureLimit(),
            Constraint(10, 20));
        var firstCollectionGeneration =
            session.CurrentWorldInventoryCollectionGeneration;
        await Assert.That(firstCollectionGeneration.Value).IsEqualTo(1L);

        var completeWorldBuilder =
            new CompleteWorldResourceTemperatureAmountsBuilder();
        completeWorldBuilder.BeginWorld(firstCollectionGeneration);
        completeWorldBuilder.BeginResourceTag(iron);
        completeWorldBuilder.AddTemperatureAmount(15.0f, 10.0f);
        completeWorldBuilder.CompleteResourceTag();
        await Assert.That(ownedCatalog.PublishCompleteWorldResourceAmounts(
            worldId: 7,
            completeWorldBuilder.Build())).IsTrue();
        var completeAvailability =
            ownedCatalog.GetTemperatureConstrainedAmountAvailability(
                parentWorldId: 1,
                resourceTag: iron,
                Constraint(10, 20),
                firstCollectionGeneration);
        await Assert.That(completeAvailability.TryGetCompleteAvailableAmount(
                out var completeAmount)).IsTrue();
        await Assert.That(completeAmount).IsEqualTo(10.0f);

        await Assert.That(session.TryReplaceTemperatureConstraint(
            firstRegistration,
            Constraint(30, 40))).IsTrue();
        await Assert.That(session.CurrentWorldInventoryCollectionGeneration).IsEqualTo(firstCollectionGeneration);
        session.RemoveTemperatureLimit(firstRegistration);

        await Assert.That(session.WorldResourceTemperatureAmounts).IsSameReferenceAs(ownedCatalog);
        await Assert.That(session.CurrentWorldInventoryCollectionGeneration).IsEqualTo(firstCollectionGeneration);
        await Assert.That(ownedCatalog.GetWorldResourceTagCoverageRequirementState(
                7,
                firstCollectionGeneration)).IsEqualTo(WorldResourceTagCoverageRequirementState.CoverageRequired);

        session.RegisterTemperatureLimit(
            gameObjectInstanceId: 555323,
            componentInstanceId: 555324,
            new TemperatureLimit(),
            Constraint(50, 60));

        await Assert.That(session.CurrentWorldInventoryCollectionGeneration.Value).IsEqualTo(firstCollectionGeneration.Value + 1);
        await Assert.That(ownedCatalog.GetWorldResourceTagCoverageRequirementState(
                7,
                session.CurrentWorldInventoryCollectionGeneration)).IsEqualTo(WorldResourceTagCoverageRequirementState.CoverageRequired);
    }

    [Test]
    public async Task RegisterTemperatureLimit_WhenInventoryCollectionGenerationIsExhausted_ThrowsWithoutStartingCollection()
    {
        var session = EnsureTrackedGameSession(55533);
        var generationField = await RequirePrivateInstanceField(
            typeof(DeliveryTemperatureGameSession),
            "currentWorldInventoryCollectionGenerationValue",
            typeof(long));
        generationField.SetValue(session, long.MaxValue);
        var priorConstraintSnapshot =
            session.TemperatureConstraints.CaptureSnapshot();
        var priorCatalog = session.WorldResourceTemperatureAmounts;
        var priorFetchTopologyVersion =
            session.FetchRequestTopology.CaptureVersion();

        var exception = (await Assert.That(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 555331,
                componentInstanceId: 555332,
                new TemperatureLimit(),
                Constraint(10, 20))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("exhausted");
        await Assert.That(session.TemperatureConstraints.CaptureSnapshot()).IsSameReferenceAs(priorConstraintSnapshot);
        await Assert.That(session.WorldResourceTemperatureAmounts).IsSameReferenceAs(priorCatalog);
        await Assert.That(session.FetchRequestTopology.CaptureVersion()).IsEqualTo(priorFetchTopologyVersion);
        await Assert.That(session.CurrentWorldInventoryCollectionGeneration.Value).IsEqualTo(long.MaxValue);
        await Assert.That(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(555331, out _, out _)).IsFalse();
    }

    [Test]
    public async Task OldSession_WhenNewSessionExists_RejectsTemperatureLimitRegistration()
    {
        var oldSession = EnsureTrackedGameSession(5601);
        var newSession = EnsureTrackedGameSession(5602);

        var exception = (await Assert.That(() =>
            oldSession.RegisterTemperatureLimit(
                gameObjectInstanceId: 56011,
                componentInstanceId: 56012,
                new TemperatureLimit(),
                Constraint(10, 20))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("not accepting publications");
        await Assert.That(newSession.IsAcceptingPublications).IsTrue();
        await Assert.That(newSession.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount).IsEqualTo(0);
    }

    [Test]
    public async Task EnsureGameSession_WhenIdentityChanges_CreatesFreshSessionDiagnosticLimiter()
    {
        var oldSession = EnsureTrackedGameSession(5651);
        await Assert.That(oldSession.DiagnosticLimiter.ShouldEmit("DTL_WORLD_UNRESOLVED")).IsTrue();
        await Assert.That(oldSession.DiagnosticLimiter.ShouldEmit("DTL_WORLD_UNRESOLVED")).IsFalse();

        var newSession = EnsureTrackedGameSession(5652);

        await Assert.That(newSession.DiagnosticLimiter).IsNotSameReferenceAs(oldSession.DiagnosticLimiter);
        await Assert.That(newSession.DiagnosticLimiter.ShouldEmit("DTL_WORLD_UNRESOLVED")).IsTrue();
    }

    [Test]
    public async Task RegisterTemperatureLimit_WhenSessionIsStopping_ThrowsLifecycleViolation()
    {
        var session = EnsureTrackedGameSession(5701);
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5701);

        var exception = (await Assert.That(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 57011,
                componentInstanceId: 57012,
                new TemperatureLimit(),
                Constraint(10, 20))).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("not accepting publications");
        await Assert.That(session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount).IsEqualTo(0);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
    }

    [Test]
    public async Task RemoveTemperatureLimit_WhenRegistrationBelongsToOldSession_DoesNotTouchCurrentSession()
    {
        const int reusedGameObjectInstanceId = 58011;
        const int reusedComponentInstanceId = 58012;
        var oldSession = EnsureTrackedGameSession(5801);
        var oldRegistration = oldSession.RegisterTemperatureLimit(
            reusedGameObjectInstanceId,
            reusedComponentInstanceId,
            new TemperatureLimit(),
            Constraint(10, 20));
        var currentSession = EnsureTrackedGameSession(5802);
        var currentComponent = new TemperatureLimit();
        var currentRegistration = currentSession.RegisterTemperatureLimit(
            reusedGameObjectInstanceId,
            reusedComponentInstanceId,
            currentComponent,
            Constraint(10, 20));
        var snapshotBeforeStaleRemoval =
            currentSession.TemperatureConstraints.CaptureSnapshot();

        // Registry sequence values restart inside each session, so the outer session
        // generation is what makes this otherwise identical ownership token stale.
        await Assert.That(currentRegistration.ConstraintRegistrationToken).IsEqualTo(oldRegistration.ConstraintRegistrationToken);
        await Assert.That(currentRegistration.GameSessionGeneration).IsNotEqualTo(oldRegistration.GameSessionGeneration);
        currentSession.RemoveTemperatureLimit(oldRegistration);

        await Assert.That(currentSession.TemperatureConstraints.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeStaleRemoval);
        await Assert.That(currentSession.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                reusedGameObjectInstanceId,
                out var retainedComponent,
                out var retainedConstraintRegistration)).IsTrue();
        await Assert.That(retainedComponent).IsSameReferenceAs(currentComponent);
        await Assert.That(retainedConstraintRegistration).IsEqualTo(currentRegistration.ConstraintRegistrationToken);
    }

    [Test]
    public async Task TryReplaceTemperatureConstraint_WhenNormalizedConstraintIsIdentical_DoesNotAdvanceGeneration()
    {
        var session = EnsureTrackedGameSession(5901);
        var normalizedFullRangeConstraint = Constraint(-100, 20000);
        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 59011,
            componentInstanceId: 59012,
            new TemperatureLimit(),
            normalizedFullRangeConstraint);
        var snapshotBeforeReplacement =
            session.TemperatureConstraints.CaptureSnapshot();

        var registrationFound = session.TryReplaceTemperatureConstraint(
            registration,
            Constraint(
                OniStorableTemperatureBounds.MinimumTemperatureKelvin,
                OniStorableTemperatureBounds.MaximumTemperatureKelvin));

        await Assert.That(registrationFound).IsTrue();
        await Assert.That(session.TemperatureConstraints.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeReplacement);
        await Assert.That(session.TemperatureConstraints.CaptureSnapshot().Generation).IsEqualTo(snapshotBeforeReplacement.Generation);
    }

    [Test]
    public async Task TryReplaceTemperatureConstraint_WhenRegistrationMatches_UpdatesBothOwnedServices()
    {
        var session = EnsureTrackedGameSession(5951);
        var component = new TemperatureLimit();
        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 59511,
            componentInstanceId: 59512,
            component,
            Constraint(10, 20));
        var generationBeforeReplacement =
            session.TemperatureConstraints.CaptureSnapshot().Generation;
        var replacementConstraint = Constraint(30, 40);

        var registrationFound = session.TryReplaceTemperatureConstraint(
            registration,
            replacementConstraint);

        await Assert.That(registrationFound).IsTrue();
        await Assert.That(session.TemperatureConstraints
                .CaptureSnapshot()
                .Generation
                .Value).IsEqualTo(generationBeforeReplacement.Value + 1);
        await Assert.That(session.TemperatureLimitComponents.TryGetConstraint(
            59511,
            out var observedConstraint,
            out var observedConstraintRegistration)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(replacementConstraint);
        await Assert.That(observedConstraintRegistration).IsEqualTo(registration.ConstraintRegistrationToken);
        await Assert.That(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                59511,
                out var observedComponent,
                out _)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(component);
    }

    [Test]
    public async Task RegisterTemperatureLimit_WhenSessionIsActive_PublishesCompositeOwnershipToBothServices()
    {
        var session = EnsureTrackedGameSession(6001);
        var component = new TemperatureLimit();
        var constraint = Constraint(20, 40);

        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 60011,
            componentInstanceId: 60012,
            component,
            constraint);

        await Assert.That(registration.GameSessionGeneration).IsEqualTo(session.Generation);
        await Assert.That(registration.GameObjectInstanceId).IsEqualTo(60011);
        await Assert.That(registration.ConstraintRegistrationToken.ComponentInstanceId).IsEqualTo(60012);
        await Assert.That(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                60011,
                out var observedComponent,
                out var observedConstraintRegistration)).IsTrue();
        await Assert.That(observedComponent).IsSameReferenceAs(component);
        await Assert.That(observedConstraintRegistration).IsEqualTo(registration.ConstraintRegistrationToken);
        await Assert.That(session.TemperatureLimitComponents.TryGetConstraint(
            60011,
            out var observedConstraint,
            out _)).IsTrue();
        await Assert.That(observedConstraint).IsEqualTo(constraint);
        await Assert.That(session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount).IsEqualTo(1);
    }

    [Test]
    public async Task RegisterTemperatureLimit_WhenComponentIndexRejectsPublication_RollsBackConstraintRegistration()
    {
        const int gameObjectInstanceId = 61011;
        const int componentInstanceId = 61012;
        var session = EnsureTrackedGameSession(6101);
        var constraint = Constraint(20, 40);
        var predictedFirstConstraintRegistration =
            new TemperatureConstraintRegistrationToken(
                componentInstanceId,
                registrationSequence: 1);
        var retainedConflictingComponent = new TemperatureLimit();
        await Assert.That(session.TemperatureLimitComponents.TryRegister(
            gameObjectInstanceId,
            retainedConflictingComponent,
            predictedFirstConstraintRegistration,
            constraint)).IsTrue();

        var exception = (await Assert.That(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId,
                componentInstanceId,
                new TemperatureLimit(),
                constraint)).ThrowsExactly<InvalidOperationException>())!;

        await Assert.That(exception.Message).Contains("component index");
        await Assert.That(session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount).IsEqualTo(0);
        await Assert.That(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                gameObjectInstanceId,
                out var retainedComponent,
                out var retainedRegistration)).IsTrue();
        await Assert.That(retainedComponent).IsSameReferenceAs(retainedConflictingComponent);
        await Assert.That(retainedRegistration).IsEqualTo(predictedFirstConstraintRegistration);
    }

    [Test]
    public async Task RemoveTemperatureLimit_WhenRegistrationMatches_RemovesBothOwnedEntriesIdempotently()
    {
        var session = EnsureTrackedGameSession(6201);
        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 62011,
            componentInstanceId: 62012,
            new TemperatureLimit(),
            Constraint(20, 40));

        session.RemoveTemperatureLimit(registration);
        session.RemoveTemperatureLimit(registration);

        await Assert.That(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(62011, out _, out _)).IsFalse();
        await Assert.That(session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount).IsEqualTo(0);
    }

    [Test]
    public async Task TryReplaceTemperatureConstraint_WhenRegistrationIsStale_LeavesCurrentRegistrationUnchanged()
    {
        const int reusedGameObjectInstanceId = 63011;
        const int reusedComponentInstanceId = 63012;
        var oldSession = EnsureTrackedGameSession(6301);
        var staleRegistration = oldSession.RegisterTemperatureLimit(
            reusedGameObjectInstanceId,
            reusedComponentInstanceId,
            new TemperatureLimit(),
            Constraint(10, 20));
        var currentSession = EnsureTrackedGameSession(6302);
        var currentConstraint = Constraint(30, 40);
        currentSession.RegisterTemperatureLimit(
            reusedGameObjectInstanceId,
            reusedComponentInstanceId,
            new TemperatureLimit(),
            currentConstraint);
        var snapshotBeforeStaleReplacement =
            currentSession.TemperatureConstraints.CaptureSnapshot();

        await Assert.That(currentSession.TryReplaceTemperatureConstraint(
            staleRegistration,
            Constraint(50, 60))).IsFalse();

        await Assert.That(currentSession.TemperatureConstraints.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeStaleReplacement);
        await Assert.That(currentSession.TemperatureLimitComponents.TryGetConstraint(
            reusedGameObjectInstanceId,
            out var retainedConstraint,
            out _)).IsTrue();
        await Assert.That(retainedConstraint).IsEqualTo(currentConstraint);
    }

    [Test]
    [NotInParallel]
    public async Task EnsureGameSession_WhenGenerationIsExhausted_ThrowsBeforePublication()
    {
        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _)).IsFalse();
        var generationSourceField = await RequirePrivateStaticField(
            typeof(DeliveryTemperatureGameSessionHost),
            "lastIssuedGameSessionGeneration",
            typeof(long));
        var generationBeforeTest =
            (await Assert.That(generationSourceField.GetValue(null)).IsAssignableTo<long>())!;
        generationSourceField.SetValue(null, long.MaxValue);

        try
        {
            var exception = (await Assert.That(() =>
                EnsureTrackedGameSession(6401)).ThrowsExactly<InvalidOperationException>())!;

            await Assert.That(exception.Message).Contains("game-session generation");
            await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _)).IsFalse();
            await Assert.That(((await Assert.That(generationSourceField.GetValue(null)).IsAssignableTo<long>())!)).IsEqualTo(long.MaxValue);
        }
        finally
        {
            // This focused nonparallel test restores exactly the static value it
            // changed; it does not reset any production state created elsewhere.
            generationSourceField.SetValue(null, generationBeforeTest);
        }
    }

    [Test]
    public async Task RetainedCollectionCapacityLimits_WhenInspected_ExposeReviewedResourcePolicies()
    {
        var expectedConstantValuesByExactFieldName =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["MaximumRetainedPickupClassificationCount"] = 16384,
                ["MaximumRetainedFastTrackGroupingKeyCount"] = 8192,
                ["MaximumRetainedFetchEligibilityEntryCount"] = 4096,
                ["MaximumRetainedWorldResourceTagCount"] = 4096,
            };
        var observedConstantValuesByExactFieldName =
            typeof(RetainedCollectionCapacityLimits)
                .GetFields(
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(field => field.IsLiteral && field.FieldType == typeof(int))
                .ToDictionary(
                    field => field.Name,
                    field => (int)(field.GetRawConstantValue()
                        ?? throw new TUnit.Assertions.Exceptions.AssertionException(
                            $"The integer constant {field.Name} has no value.")),
                    StringComparer.Ordinal);

        await Assert.That(observedConstantValuesByExactFieldName).IsEquivalentTo(expectedConstantValuesByExactFieldName, TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    public async Task LifecycleProductionTypes_WhenInspected_ContainOnlyNamedMutableStaticState()
    {
        var lifecycleProductionTypes = new[]
        {
            typeof(GameSessionGeneration),
            typeof(GameSessionTemperatureLimitRegistrationToken),
            typeof(RetainedCollectionCapacityLimits),
            typeof(SessionDiagnosticLimiter),
            typeof(DeliveryTemperatureGameSession),
            typeof(DeliveryTemperatureGameSessionHost),
        };
        var observedMutableStaticFieldNames = lifecycleProductionTypes
            .SelectMany(type => type.GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic))
            .Where(field => !field.IsLiteral && !field.IsInitOnly)
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}")
            .OrderBy(fieldName => fieldName, StringComparer.Ordinal)
            .ToArray();
        var expectedMutableStaticFieldNames = new[]
        {
            "DeliveryTemperatureGameSessionHost.currentGameSession",
            "DeliveryTemperatureGameSessionHost.lastIssuedGameSessionGeneration",
        };

        await Assert.That(observedMutableStaticFieldNames).IsEquivalentTo(expectedMutableStaticFieldNames, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task LifecycleOperations_WhenDeterministicallyScheduled_RejectStaleGenerationMutation()
    {
        var random = new Random(LifecycleScheduleSeed);
        DeliveryTemperatureGameSession? expectedCurrentSession = null;
        var currentRegistrationsByGameObjectInstanceId =
            new Dictionary<int, ScheduledRegistration>();
        var staleRegistrations =
            new List<GameSessionTemperatureLimitRegistrationToken>();
        var explicitlyDetachedSessions =
            new List<DeliveryTemperatureGameSession>();

        for (var operationIndex = 0;
             operationIndex < LifecycleScheduleOperationCount;
             operationIndex++)
        {
            switch (random.Next(6))
            {
                case 0:
                    expectedCurrentSession = await EnsureScheduledGameSession(
                        random,expectedCurrentSession,                        currentRegistrationsByGameObjectInstanceId,                        staleRegistrations);
                    break;

                case 1:
                    await AssertScheduledCapture(
                        expectedCurrentSession,
                        operationIndex);
                    break;

                case 2:
                    expectedCurrentSession = await DetachScheduledGameSession(
                        random,expectedCurrentSession,                        currentRegistrationsByGameObjectInstanceId,                        staleRegistrations,                        explicitlyDetachedSessions,                        operationIndex);
                    break;

                case 3:
                    CompleteScheduledShutdown(
                        explicitlyDetachedSessions,
                        random);
                    break;

                case 4:
                    expectedCurrentSession = await RegisterScheduledTemperatureLimit(
                        random,expectedCurrentSession,                        currentRegistrationsByGameObjectInstanceId,                        staleRegistrations);
                    break;

                default:
                    await RemoveScheduledTemperatureLimit(
                        random,
                        expectedCurrentSession,
                        currentRegistrationsByGameObjectInstanceId,
                        staleRegistrations,
                        operationIndex);
                    break;
            }

            await AssertScheduledCurrentState(
                expectedCurrentSession,
                currentRegistrationsByGameObjectInstanceId,
                operationIndex);
        }

        foreach (var detachedSession in explicitlyDetachedSessions)
        {
            DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        }
    }

    private DeliveryTemperatureGameSession EnsureTrackedGameSession(
        int gameInstanceId)
    {
        gameInstanceIdsForCleanup.Add(gameInstanceId);
        return DeliveryTemperatureGameSessionHost.EnsureGameSession(gameInstanceId);
    }

    private async Task<DeliveryTemperatureGameSession?> EnsureScheduledGameSession(
        Random random,DeliveryTemperatureGameSession? expectedCurrentSession,        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,        ICollection<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations)
    {
        var gameInstanceId = 6500 + random.Next(4);
        var priorSession = expectedCurrentSession;
        var ensuredSession = EnsureTrackedGameSession(gameInstanceId);

        if (priorSession is not null &&
            priorSession.GameInstanceId != gameInstanceId)
        {
            MoveCurrentRegistrationsToStale(
                currentRegistrationsByGameObjectInstanceId,
                staleRegistrations);
            await Assert.That(priorSession.IsAcceptingPublications).IsFalse();
        }

        expectedCurrentSession = ensuredSession;
        return expectedCurrentSession;
    }

    private static async Task AssertScheduledCapture(
        DeliveryTemperatureGameSession? expectedCurrentSession,
        int operationIndex)
    {
        var captured = DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
            out var observedSession);
        var assertionContext = LifecycleScheduleAssertionContext(operationIndex);

        await Assert.That(captured).IsEqualTo(expectedCurrentSession is not null).Because(assertionContext);
        if (expectedCurrentSession is null)
        {
            await Assert.That(observedSession).IsNull().Because(assertionContext);
        }
        else
        {
            await Assert.That(observedSession).IsSameReferenceAs(expectedCurrentSession).Because(assertionContext);
        }
    }

    private static async Task<DeliveryTemperatureGameSession?> DetachScheduledGameSession(
        Random random,DeliveryTemperatureGameSession? expectedCurrentSession,        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,        ICollection<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations,        ICollection<DeliveryTemperatureGameSession> explicitlyDetachedSessions,        int operationIndex)
    {
        var detachMatchingIdentity =
            expectedCurrentSession is not null && random.Next(2) == 0;
        var requestedGameInstanceId = detachMatchingIdentity
            ? expectedCurrentSession!.GameInstanceId
            : 6600 + random.Next(4);
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(
                requestedGameInstanceId);
        var assertionContext = LifecycleScheduleAssertionContext(operationIndex);

        if (!detachMatchingIdentity)
        {
            await Assert.That(detachedSession).IsNull().Because(assertionContext);
            return expectedCurrentSession;
        }

        await Assert.That(detachedSession).IsSameReferenceAs(expectedCurrentSession).Because(assertionContext);
        await Assert.That(detachedSession!.IsAcceptingPublications).IsFalse().Because(assertionContext);
        explicitlyDetachedSessions.Add(detachedSession);
        MoveCurrentRegistrationsToStale(
            currentRegistrationsByGameObjectInstanceId,
            staleRegistrations);
        expectedCurrentSession = null;
        return expectedCurrentSession;
    }

    private static void CompleteScheduledShutdown(
        IList<DeliveryTemperatureGameSession> explicitlyDetachedSessions,
        Random random)
    {
        if (explicitlyDetachedSessions.Count == 0)
        {
            DeliveryTemperatureGameSessionHost.CompleteShutdown(null);
            return;
        }

        var detachedSessionIndex = random.Next(explicitlyDetachedSessions.Count);
        var detachedSession = explicitlyDetachedSessions[detachedSessionIndex];
        explicitlyDetachedSessions.RemoveAt(detachedSessionIndex);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
    }

    private async Task<DeliveryTemperatureGameSession?> RegisterScheduledTemperatureLimit(
        Random random,DeliveryTemperatureGameSession? expectedCurrentSession,        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,        ICollection<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations)
    {
        if (expectedCurrentSession is null)
        {
            expectedCurrentSession = await EnsureScheduledGameSession(
                random,expectedCurrentSession,                currentRegistrationsByGameObjectInstanceId,                staleRegistrations);
        }

        var availableIdentityOffset = FindAvailableScheduleIdentityOffset(
            random,
            currentRegistrationsByGameObjectInstanceId);
        if (availableIdentityOffset < 0)
        {
            return expectedCurrentSession;
        }

        var gameObjectInstanceId = 67000 + availableIdentityOffset;
        var componentInstanceId = 68000 + availableIdentityOffset;
        var component = new TemperatureLimit();
        var constraint = Constraint(
            10 + availableIdentityOffset,
            100 + availableIdentityOffset);
        var registration = expectedCurrentSession!.RegisterTemperatureLimit(
            gameObjectInstanceId,
            componentInstanceId,
            component,
            constraint);

        currentRegistrationsByGameObjectInstanceId.Add(
            gameObjectInstanceId,
            new ScheduledRegistration(
                registration,
                component,
                constraint));
        return expectedCurrentSession;
    }

    private static async Task RemoveScheduledTemperatureLimit(
        Random random,
        DeliveryTemperatureGameSession? expectedCurrentSession,
        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        IReadOnlyList<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations,
        int operationIndex)
    {
        if (expectedCurrentSession is null)
        {
            return;
        }

        var removeStaleRegistration =
            staleRegistrations.Count > 0 && random.Next(2) == 0;
        if (removeStaleRegistration)
        {
            var staleRegistration =
                staleRegistrations[random.Next(staleRegistrations.Count)];
            var snapshotBeforeStaleRemoval =
                expectedCurrentSession.TemperatureConstraints.CaptureSnapshot();

            expectedCurrentSession.RemoveTemperatureLimit(staleRegistration);

            await Assert.That(expectedCurrentSession.TemperatureConstraints.CaptureSnapshot()).IsSameReferenceAs(snapshotBeforeStaleRemoval).Because(LifecycleScheduleAssertionContext(operationIndex));
            return;
        }

        if (currentRegistrationsByGameObjectInstanceId.Count == 0)
        {
            return;
        }

        var scheduledRegistration =
            currentRegistrationsByGameObjectInstanceId.Values
                .ElementAt(random.Next(
                    currentRegistrationsByGameObjectInstanceId.Count));
        expectedCurrentSession.RemoveTemperatureLimit(
            scheduledRegistration.RegistrationToken);
        currentRegistrationsByGameObjectInstanceId.Remove(
            scheduledRegistration.RegistrationToken.GameObjectInstanceId);
    }

    private static async Task AssertScheduledCurrentState(
        DeliveryTemperatureGameSession? expectedCurrentSession,
        IReadOnlyDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        int operationIndex)
    {
        var assertionContext = LifecycleScheduleAssertionContext(operationIndex);
        if (expectedCurrentSession is null)
        {
            await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _)).IsFalse().Because(assertionContext);
            await Assert.That(currentRegistrationsByGameObjectInstanceId.Count).IsEqualTo(0).Because(assertionContext);
            return;
        }

        await Assert.That(DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var observedCurrentSession)).IsTrue().Because(assertionContext);
        await Assert.That(observedCurrentSession).IsSameReferenceAs(expectedCurrentSession).Because(assertionContext);
        await Assert.That(expectedCurrentSession.IsAcceptingPublications).IsTrue().Because(assertionContext);
        await Assert.That(expectedCurrentSession.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount).IsEqualTo(currentRegistrationsByGameObjectInstanceId.Count).Because(assertionContext);

        foreach (var scheduledRegistration in
                 currentRegistrationsByGameObjectInstanceId.Values)
        {
            await Assert.That(scheduledRegistration.RegistrationToken.GameSessionGeneration).IsEqualTo(expectedCurrentSession.Generation).Because(assertionContext);
            await Assert.That(expectedCurrentSession.TemperatureLimitComponents
                .TryGetRegisteredComponent(
                    scheduledRegistration.RegistrationToken.GameObjectInstanceId,
                    out var observedComponent,
                    out var observedConstraintRegistration)).IsTrue().Because(assertionContext);
            await Assert.That(observedComponent).IsSameReferenceAs(scheduledRegistration.Component).Because(assertionContext);
            await Assert.That(observedConstraintRegistration).IsEqualTo(scheduledRegistration.RegistrationToken
                    .ConstraintRegistrationToken).Because(assertionContext);
            await Assert.That(expectedCurrentSession.TemperatureLimitComponents
                .TryGetConstraint(
                    scheduledRegistration.RegistrationToken.GameObjectInstanceId,
                    out var observedConstraint,
                    out _)).IsTrue().Because(assertionContext);
            await Assert.That(observedConstraint).IsEqualTo(scheduledRegistration.Constraint).Because(assertionContext);
        }
    }

    private static int FindAvailableScheduleIdentityOffset(
        Random random,
        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId)
    {
        var initialOffset = random.Next(ScheduleGameObjectIdentityCount);
        for (var offsetAttempt = 0;
             offsetAttempt < ScheduleGameObjectIdentityCount;
             offsetAttempt++)
        {
            var candidateOffset =
                (initialOffset + offsetAttempt) %
                ScheduleGameObjectIdentityCount;
            if (!currentRegistrationsByGameObjectInstanceId.ContainsKey(
                    67000 + candidateOffset))
            {
                return candidateOffset;
            }
        }

        return -1;
    }

    private static void MoveCurrentRegistrationsToStale(
        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        ICollection<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations)
    {
        foreach (var scheduledRegistration in
                 currentRegistrationsByGameObjectInstanceId.Values)
        {
            staleRegistrations.Add(scheduledRegistration.RegistrationToken);
        }

        currentRegistrationsByGameObjectInstanceId.Clear();
    }

    private static DeliveryTemperatureConstraint Constraint(
        int minimumInclusiveKelvin,
        int maximumExclusiveKelvin) =>
        DeliveryTemperatureConstraint.FromSerializedLimits(
            minimumInclusiveKelvin,
            maximumExclusiveKelvin);

    private static async Task PublishCompleteWorldResourceAmount(
        DeliveryTemperatureGameSession session,
        int worldId,
        WorldInventoryCollectionGeneration collectionGeneration,
        Tag resourceTag,
        float amount)
    {
        var builder = new CompleteWorldResourceTemperatureAmountsBuilder();
        builder.BeginWorld(collectionGeneration);
        builder.BeginResourceTag(resourceTag);
        builder.AddTemperatureAmount(temperatureKelvin: 300.0f, amount);
        builder.CompleteResourceTag();
        await Assert.That(session.WorldResourceTemperatureAmounts
                .PublishCompleteWorldResourceAmounts(
                    worldId,
                    builder.Build())).IsTrue();
    }

    private static async Task<float> RequireCompleteTemperatureConstrainedAmount(
        DeliveryTemperatureGameSession session,
        int parentWorldId,
        Tag resourceTag,
        DeliveryTemperatureConstraint constraint,
        WorldInventoryCollectionGeneration collectionGeneration)
    {
        var availability = session.WorldResourceTemperatureAmounts
            .GetTemperatureConstrainedAmountAvailability(
                parentWorldId,
                resourceTag,
                constraint,
                collectionGeneration);
        await Assert.That(availability.TryGetCompleteAvailableAmount(out var amount)).IsTrue().Because($"Expected a complete aggregate for parent world " +
            $"{parentWorldId} and resource tag {resourceTag}.");
        return amount;
    }

    private static FetchTemperatureEligibilitySnapshot
        BuildCurrentFetchTemperatureEligibilityCandidate(
            DeliveryTemperatureGameSession session)
    {
        var builder = new FetchTemperatureEligibilityBuilder();
        builder.Begin(
            session.Generation,
            session.TemperatureConstraints.CaptureSnapshot(),
            session.FetchRequestTopology.CaptureVersion(),
            session.WorldParentTopology.CaptureSnapshot());
        return builder.Build();
    }

    private static string LifecycleScheduleAssertionContext(
        int operationIndex) =>
        $"Seed=0x{LifecycleScheduleSeed:X}; operation index={operationIndex}.";

    private static async Task<FieldInfo> RequirePrivateStaticField(
        Type declaringType,
        string exactFieldName,
        Type exactFieldType)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private static " +
            $"field {declaringType.Name}.{exactFieldName}.");
        await Assert.That(field.FieldType).IsEqualTo(exactFieldType);
        return field;
    }

    private static async Task<FieldInfo> RequirePrivateInstanceField(
        Type declaringType,
        string exactFieldName,
        Type exactFieldType)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null) throw new TUnit.Assertions.Exceptions.AssertionException($"The representation contract requires the exact private instance " +
            $"field {declaringType.Name}.{exactFieldName}.");
        await Assert.That(field.FieldType).IsEqualTo(exactFieldType);
        return field;
    }

    private sealed class ScheduledRegistration
    {
        internal ScheduledRegistration(
            GameSessionTemperatureLimitRegistrationToken registrationToken,
            TemperatureLimit component,
            DeliveryTemperatureConstraint constraint)
        {
            RegistrationToken = registrationToken;
            Component = component;
            Constraint = constraint;
        }

        internal GameSessionTemperatureLimitRegistrationToken RegistrationToken { get; }

        internal TemperatureLimit Component { get; }

        internal DeliveryTemperatureConstraint Constraint { get; }
    }
}
