using System.Collections;
using System.Reflection;

namespace DeliveryTemperatureLimit.Tests.DeliveryTemperatureGameSessionLifecycle;

[TestClass]
[DoNotParallelize]
public sealed class DeliveryTemperatureGameSessionTests
{
    private const int LifecycleScheduleSeed = 0x5E5510;
    private const int LifecycleScheduleOperationCount = 10000;
    private const int ScheduleGameObjectIdentityCount = 32;

    private readonly HashSet<int> gameInstanceIdsForCleanup = new();

    [TestCleanup]
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

    [TestMethod]
    public void EnsureGameSession_WhenGameIdentityChanges_DetachesAndInvalidatesOldSession()
    {
        var oldSession = EnsureTrackedGameSession(5101);

        var newSession = EnsureTrackedGameSession(5102);

        Assert.AreNotSame(oldSession, newSession);
        Assert.IsFalse(oldSession.IsAcceptingPublications);
        Assert.IsTrue(newSession.IsAcceptingPublications);
        Assert.AreNotEqual(oldSession.Generation, newSession.Generation);
    }

    [TestMethod]
    public void EnsureGameSession_WhenIdentityMatches_ReturnsSameSession()
    {
        var firstCapture = EnsureTrackedGameSession(5201);

        var repeatedCapture = EnsureTrackedGameSession(5201);

        Assert.AreSame(firstCapture, repeatedCapture);
        Assert.AreEqual(firstCapture.Generation, repeatedCapture.Generation);
        Assert.IsTrue(repeatedCapture.IsAcceptingPublications);
    }

    [TestMethod]
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
            Assert.AreSame(publishedSession, observedSession);
        }

        Assert.IsTrue(
            DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var capturedSession));
        Assert.AreSame(publishedSession, capturedSession);
        Assert.IsTrue(publishedSession.IsAcceptingPublications);
    }

    [TestMethod]
    public void TryCaptureCurrent_WhenNoSession_ReturnsFalse()
    {
        Assert.IsFalse(
            DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var capturedSession));
        Assert.IsNull(capturedSession);
    }

    [TestMethod]
    public void DetachGameSession_WhenIdentityMatches_StopsAndReturnsSession()
    {
        var session = EnsureTrackedGameSession(5301);

        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5301);

        Assert.AreSame(session, detachedSession);
        Assert.IsFalse(session.IsAcceptingPublications);
        Assert.IsFalse(
            DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _));
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
    }

    [TestMethod]
    public void DetachGameSession_WhenIdentityDiffers_DoesNotDetachCurrentSession()
    {
        var session = EnsureTrackedGameSession(5401);

        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5402);

        Assert.IsNull(detachedSession);
        Assert.IsTrue(
            DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var capturedSession));
        Assert.AreSame(session, capturedSession);
        Assert.IsTrue(session.IsAcceptingPublications);
    }

    [TestMethod]
    public void CompleteShutdown_WhenCalledTwice_IsIdempotent()
    {
        var session = EnsureTrackedGameSession(5501);
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5501);
        Assert.AreSame(session, detachedSession);

        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);

        Assert.IsFalse(session.IsAcceptingPublications);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 55011,
                componentInstanceId: 55012,
                new TemperatureLimit(),
                Constraint(10, 20)));
    }

    [TestMethod]
    public void EnsureGameSession_WhenCreated_ConstructsWorldParentTopologyForSameGeneration()
    {
        var session = EnsureTrackedGameSession(5551);

        var topologySnapshot = session.WorldParentTopology.CaptureSnapshot();

        Assert.AreEqual(
            session.Generation,
            topologySnapshot.GameSessionGeneration);
        Assert.AreEqual(0L, topologySnapshot.Version.Value);
        Assert.IsFalse(topologySnapshot.TryResolveParentWorld(0, out _));
    }

    [TestMethod]
    public void EnsureGameSession_WhenCreated_ConstructsOwnedWorldResourceTemperatureAmountCatalog()
    {
        var session = EnsureTrackedGameSession(55511);
        var generation = new WorldInventoryCollectionGeneration(1);

        session.WorldResourceTemperatureAmounts.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);

        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageRequired,
            session.WorldResourceTemperatureAmounts
                .GetWorldResourceTagCoverageRequirementState(7, generation));
    }

    [TestMethod]
    public void CompleteShutdown_WhenTopologySnapshotWasCaptured_ClearsOwnedMappingsWithoutMutatingSnapshot()
    {
        var session = EnsureTrackedGameSession(5552);
        session.WorldParentTopology.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);
        var capturedSnapshot = session.WorldParentTopology.CaptureSnapshot();
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5552);

        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);

        Assert.IsTrue(capturedSnapshot.TryResolveParentWorld(
            7,
            out var capturedParentWorldId));
        Assert.AreEqual(1, capturedParentWorldId);
        Assert.AreSame(
            capturedSnapshot,
            session.WorldParentTopology.CaptureSnapshot());
        var ownedMappingField = typeof(WorldParentTopologyCatalog).GetField(
            "parentWorldIdsByWorldId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            ownedMappingField,
            "Shutdown structure requires the exact private owned mapping field " +
            "WorldParentTopologyCatalog.parentWorldIdsByWorldId.");
        var ownedMappings = Assert.IsInstanceOfType<IDictionary<int, int>>(
            ownedMappingField.GetValue(session.WorldParentTopology));
        Assert.AreEqual(0, ownedMappings.Count);
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.WorldParentTopology.RegisterWorld(
                worldId: 8,
                parentWorldId: 1));
        StringAssert.Contains(exception.Message, "no longer accepts publications");
    }

    [TestMethod]
    public void CompleteShutdown_WhenWorldResourceAmountsWerePublished_ClearsCatalogOnceAndRejectsLatePublication()
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
        Assert.IsTrue(
            session.WorldResourceTemperatureAmounts
                .PublishCompleteWorldResourceAmounts(7, publication));
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(55521);

        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);

        Assert.IsFalse(
            session.WorldResourceTemperatureAmounts
                .PublishCompleteWorldResourceAmounts(7, publication));
        var catalogDictionaryFields = typeof(WorldResourceTemperatureAmountCatalog)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => typeof(IDictionary).IsAssignableFrom(field.FieldType))
            .ToArray();
        Assert.IsNotEmpty(catalogDictionaryFields);
        foreach (var catalogDictionaryField in catalogDictionaryFields)
        {
            var retainedState = Assert.IsInstanceOfType<IDictionary>(
                catalogDictionaryField.GetValue(
                    session.WorldResourceTemperatureAmounts));
            Assert.IsEmpty(
                retainedState,
                $"Shutdown retained catalog state in " +
                $"{catalogDictionaryField.Name}.");
        }
    }

    [TestMethod]
    public void EnsureGameSession_WhenCreated_ConstructsFetchServicesInBypassState()
    {
        var session = EnsureTrackedGameSession(55522);

        Assert.IsNotNull(session.FetchRequestTopology);
        Assert.AreEqual(0L, session.FetchRequestTopology.CaptureVersion().Value);
        Assert.AreEqual(
            0L,
            session.CurrentWorldInventoryCollectionGeneration.Value);
        Assert.IsNull(session.CurrentFetchTemperatureEligibility);
    }

    [TestMethod]
    public void TryPublishFetchTemperatureEligibility_WhenEveryVersionMatches_PublishesWholeCandidate()
    {
        var session = EnsureTrackedGameSession(55523);
        var candidate = BuildCurrentFetchTemperatureEligibilityCandidate(session);

        var published = session.TryPublishFetchTemperatureEligibility(candidate);

        Assert.IsTrue(published);
        Assert.AreSame(candidate, session.CurrentFetchTemperatureEligibility);
    }

    [TestMethod]
    public void TryPublishFetchTemperatureEligibility_WhenGameSessionWasDetached_RejectsCandidateAndPreservesPublication()
    {
        var oldSession = EnsureTrackedGameSession(55524);
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(oldSession);
        var currentSession = EnsureTrackedGameSession(55525);
        var priorPublication =
            BuildCurrentFetchTemperatureEligibilityCandidate(currentSession);
        Assert.IsTrue(
            currentSession.TryPublishFetchTemperatureEligibility(priorPublication));

        var published =
            currentSession.TryPublishFetchTemperatureEligibility(staleCandidate);

        Assert.IsFalse(published);
        Assert.AreSame(
            priorPublication,
            currentSession.CurrentFetchTemperatureEligibility);
    }

    [TestMethod]
    public void TryPublishFetchTemperatureEligibility_WhenConstraintGenerationChanged_RejectsCandidateAndPreservesPublication()
    {
        var session = EnsureTrackedGameSession(55526);
        var priorPublication = BuildCurrentFetchTemperatureEligibilityCandidate(session);
        Assert.IsTrue(
            session.TryPublishFetchTemperatureEligibility(priorPublication));
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(session);
        session.TemperatureConstraints.Register(
            componentInstanceId: 555261,
            Constraint(10, 20),
            out var effectiveStateChanged);
        Assert.IsTrue(effectiveStateChanged);

        var published =
            session.TryPublishFetchTemperatureEligibility(staleCandidate);

        Assert.IsFalse(published);
        Assert.AreSame(
            priorPublication,
            session.CurrentFetchTemperatureEligibility);
    }

    [TestMethod]
    public void TryPublishFetchTemperatureEligibility_WhenFetchTopologyVersionChanged_RejectsCandidateAndPreservesPublication()
    {
        var session = EnsureTrackedGameSession(55527);
        var priorPublication = BuildCurrentFetchTemperatureEligibilityCandidate(session);
        Assert.IsTrue(
            session.TryPublishFetchTemperatureEligibility(priorPublication));
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(session);
        session.FetchRequestTopology.RecordEffectiveChange();

        var published =
            session.TryPublishFetchTemperatureEligibility(staleCandidate);

        Assert.IsFalse(published);
        Assert.AreSame(
            priorPublication,
            session.CurrentFetchTemperatureEligibility);
    }

    [TestMethod]
    public void TryPublishFetchTemperatureEligibility_WhenWorldTopologyVersionChanged_RejectsCandidateAndPreservesPublication()
    {
        var session = EnsureTrackedGameSession(55528);
        var priorPublication = BuildCurrentFetchTemperatureEligibilityCandidate(session);
        Assert.IsTrue(
            session.TryPublishFetchTemperatureEligibility(priorPublication));
        var staleCandidate =
            BuildCurrentFetchTemperatureEligibilityCandidate(session);
        session.WorldParentTopology.RegisterWorld(
            worldId: 7,
            parentWorldId: 1);

        var published =
            session.TryPublishFetchTemperatureEligibility(staleCandidate);

        Assert.IsFalse(published);
        Assert.AreSame(
            priorPublication,
            session.CurrentFetchTemperatureEligibility);
    }

    [TestMethod]
    public void RegisterTemperatureLimit_WhenEffectiveConstraintChanges_RecordsOneFetchTopologyChange()
    {
        var session = EnsureTrackedGameSession(55529);
        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 555291,
            componentInstanceId: 555292,
            new TemperatureLimit(),
            Constraint(10, 20));
        Assert.AreEqual(1L, session.FetchRequestTopology.CaptureVersion().Value);

        Assert.IsTrue(session.TryReplaceTemperatureConstraint(
            registration,
            Constraint(10, 20)));
        Assert.AreEqual(1L, session.FetchRequestTopology.CaptureVersion().Value);

        Assert.IsTrue(session.TryReplaceTemperatureConstraint(
            registration,
            Constraint(30, 40)));
        Assert.AreEqual(2L, session.FetchRequestTopology.CaptureVersion().Value);

        session.RemoveTemperatureLimit(registration);
        Assert.AreEqual(3L, session.FetchRequestTopology.CaptureVersion().Value);
    }

    [TestMethod]
    public void RegisterWorld_WhenNew_UpdatesTopologyInventoryAndFetchVersionOnce()
    {
        var session = EnsureTrackedGameSession(55530);
        var arbitraryPositiveCollectionGeneration =
            new WorldInventoryCollectionGeneration(1);

        var change = session.RegisterWorld(worldId: 7, parentWorldId: 1);

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(1L, session.FetchRequestTopology.CaptureVersion().Value);
        Assert.IsTrue(session.WorldParentTopology.CaptureSnapshot()
            .TryResolveParentWorld(7, out var parentWorldId));
        Assert.AreEqual(1, parentWorldId);
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageRequired,
            session.WorldResourceTemperatureAmounts
                .GetWorldResourceTagCoverageRequirementState(
                    7,
                    arbitraryPositiveCollectionGeneration));
    }

    [TestMethod]
    public void RegisterWorld_WhenIdentical_DoesNotAdvanceFetchVersion()
    {
        var session = EnsureTrackedGameSession(555301);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var topologyBeforeRepeatedRegistration =
            session.WorldParentTopology.CaptureSnapshot();
        var fetchTopologyVersionBeforeRepeatedRegistration =
            session.FetchRequestTopology.CaptureVersion();

        var repeatedChange = session.RegisterWorld(worldId: 7, parentWorldId: 1);

        Assert.IsFalse(repeatedChange.HasChanged);
        Assert.AreSame(
            topologyBeforeRepeatedRegistration,
            session.WorldParentTopology.CaptureSnapshot());
        Assert.AreEqual(
            fetchTopologyVersionBeforeRepeatedRegistration,
            session.FetchRequestTopology.CaptureVersion());
    }

    [TestMethod]
    public void RegisterWorld_WhenReparented_InvalidatesBothInventoryParentsAndAdvancesFetchVersionOnce()
    {
        var session = EnsureTrackedGameSession(555302);
        var collectionGeneration = new WorldInventoryCollectionGeneration(1);
        var iron = new Tag("Iron");
        var constraint = Constraint(0, 1000);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        session.RegisterWorld(worldId: 8, parentWorldId: 1);
        PublishCompleteWorldResourceAmount(
            session,
            worldId: 7,
            collectionGeneration,
            iron,
            amount: 10.0f);
        PublishCompleteWorldResourceAmount(
            session,
            worldId: 8,
            collectionGeneration,
            iron,
            amount: 20.0f);
        Assert.AreEqual(
            30.0f,
            RequireCompleteTemperatureConstrainedAmount(
                session,
                parentWorldId: 1,
                iron,
                constraint,
                collectionGeneration));
        var fetchTopologyVersionBeforeReparenting =
            session.FetchRequestTopology.CaptureVersion();

        var change = session.RegisterWorld(worldId: 7, parentWorldId: 2);

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(1, change.PreviousParentWorldId);
        Assert.AreEqual(2, change.CurrentParentWorldId);
        Assert.AreEqual(
            fetchTopologyVersionBeforeReparenting.Value + 1L,
            session.FetchRequestTopology.CaptureVersion().Value);
        Assert.AreEqual(
            20.0f,
            RequireCompleteTemperatureConstrainedAmount(
                session,
                parentWorldId: 1,
                iron,
                constraint,
                collectionGeneration));
        Assert.AreEqual(
            10.0f,
            RequireCompleteTemperatureConstrainedAmount(
                session,
                parentWorldId: 2,
                iron,
                constraint,
                collectionGeneration));
    }

    [TestMethod]
    public void RemoveWorld_WhenKnown_RemovesTopologyAndInventoryBeforeAdvancingFetchVersion()
    {
        var session = EnsureTrackedGameSession(55531);
        var arbitraryPositiveCollectionGeneration =
            new WorldInventoryCollectionGeneration(1);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);

        var change = session.RemoveWorld(worldId: 7);

        Assert.IsTrue(change.HasChanged);
        Assert.AreEqual(2L, session.FetchRequestTopology.CaptureVersion().Value);
        Assert.IsFalse(session.WorldParentTopology.CaptureSnapshot()
            .TryResolveParentWorld(7, out _));
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState
                .UnknownWorldOrCollectionGeneration,
            session.WorldResourceTemperatureAmounts
                .GetWorldResourceTagCoverageRequirementState(
                    7,
                    arbitraryPositiveCollectionGeneration));
    }

    [TestMethod]
    public void RemoveWorld_WhenUnknown_IsIdempotent()
    {
        var session = EnsureTrackedGameSession(555311);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var topologyBeforeUnknownRemoval =
            session.WorldParentTopology.CaptureSnapshot();
        var fetchTopologyVersionBeforeUnknownRemoval =
            session.FetchRequestTopology.CaptureVersion();

        var change = session.RemoveWorld(worldId: 999);

        Assert.IsFalse(change.HasChanged);
        Assert.AreSame(
            topologyBeforeUnknownRemoval,
            session.WorldParentTopology.CaptureSnapshot());
        Assert.AreEqual(
            fetchTopologyVersionBeforeUnknownRemoval,
            session.FetchRequestTopology.CaptureVersion());
    }

    [TestMethod]
    public void StopAcceptingPublications_WhenLateWorldCallbackArrives_RejectsMutation()
    {
        var session = EnsureTrackedGameSession(555312);
        session.RegisterWorld(worldId: 7, parentWorldId: 1);
        var topologyAtShutdownBoundary =
            session.WorldParentTopology.CaptureSnapshot();
        var fetchTopologyVersionAtShutdownBoundary =
            session.FetchRequestTopology.CaptureVersion();
        session.StopAcceptingPublications();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.RegisterWorld(worldId: 8, parentWorldId: 1));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.RemoveWorld(worldId: 7));
        Assert.AreSame(
            topologyAtShutdownBoundary,
            session.WorldParentTopology.CaptureSnapshot());
        Assert.AreEqual(
            fetchTopologyVersionAtShutdownBoundary,
            session.FetchRequestTopology.CaptureVersion());
    }

    [TestMethod]
    public void WorldLifecycleOperations_WhenInspected_DoNotRetainCrossCatalogMutationLock()
    {
        var crossCatalogMutationLock = typeof(DeliveryTemperatureGameSession)
            .GetField(
                "worldTopologyMutationLock",
                BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNull(
            crossCatalogMutationLock,
            "World topology and amount-catalog mutations must release each " +
            "owned catalog lock before entering the next catalog; a session-level " +
            "outer lock would nest those synchronization domains.");
    }

    [TestMethod]
    public void TemperatureLimitLifecycle_WhenEnabledCountCrossesZero_AdvancesGenerationAndClearsAmountsWithoutLosingWorlds()
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
        Assert.AreEqual(1L, firstCollectionGeneration.Value);

        var completeWorldBuilder =
            new CompleteWorldResourceTemperatureAmountsBuilder();
        completeWorldBuilder.BeginWorld(firstCollectionGeneration);
        completeWorldBuilder.BeginResourceTag(iron);
        completeWorldBuilder.AddTemperatureAmount(15.0f, 10.0f);
        completeWorldBuilder.CompleteResourceTag();
        Assert.IsTrue(ownedCatalog.PublishCompleteWorldResourceAmounts(
            worldId: 7,
            completeWorldBuilder.Build()));
        var completeAvailability =
            ownedCatalog.GetTemperatureConstrainedAmountAvailability(
                parentWorldId: 1,
                resourceTag: iron,
                Constraint(10, 20),
                firstCollectionGeneration);
        Assert.IsTrue(
            completeAvailability.TryGetCompleteAvailableAmount(
                out var completeAmount));
        Assert.AreEqual(10.0f, completeAmount);

        Assert.IsTrue(session.TryReplaceTemperatureConstraint(
            firstRegistration,
            Constraint(30, 40)));
        Assert.AreEqual(
            firstCollectionGeneration,
            session.CurrentWorldInventoryCollectionGeneration);
        session.RemoveTemperatureLimit(firstRegistration);

        Assert.AreSame(ownedCatalog, session.WorldResourceTemperatureAmounts);
        Assert.AreEqual(
            firstCollectionGeneration,
            session.CurrentWorldInventoryCollectionGeneration);
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageRequired,
            ownedCatalog.GetWorldResourceTagCoverageRequirementState(
                7,
                firstCollectionGeneration));

        session.RegisterTemperatureLimit(
            gameObjectInstanceId: 555323,
            componentInstanceId: 555324,
            new TemperatureLimit(),
            Constraint(50, 60));

        Assert.AreEqual(
            firstCollectionGeneration.Value + 1,
            session.CurrentWorldInventoryCollectionGeneration.Value);
        Assert.AreEqual(
            WorldResourceTagCoverageRequirementState.CoverageRequired,
            ownedCatalog.GetWorldResourceTagCoverageRequirementState(
                7,
                session.CurrentWorldInventoryCollectionGeneration));
    }

    [TestMethod]
    public void RegisterTemperatureLimit_WhenInventoryCollectionGenerationIsExhausted_ThrowsWithoutStartingCollection()
    {
        var session = EnsureTrackedGameSession(55533);
        var generationField = RequirePrivateInstanceField(
            typeof(DeliveryTemperatureGameSession),
            "currentWorldInventoryCollectionGenerationValue",
            typeof(long));
        generationField.SetValue(session, long.MaxValue);
        var priorConstraintSnapshot =
            session.TemperatureConstraints.CaptureSnapshot();
        var priorCatalog = session.WorldResourceTemperatureAmounts;
        var priorFetchTopologyVersion =
            session.FetchRequestTopology.CaptureVersion();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 555331,
                componentInstanceId: 555332,
                new TemperatureLimit(),
                Constraint(10, 20)));

        StringAssert.Contains(exception.Message, "exhausted");
        Assert.AreSame(
            priorConstraintSnapshot,
            session.TemperatureConstraints.CaptureSnapshot());
        Assert.AreSame(priorCatalog, session.WorldResourceTemperatureAmounts);
        Assert.AreEqual(
            priorFetchTopologyVersion,
            session.FetchRequestTopology.CaptureVersion());
        Assert.AreEqual(
            long.MaxValue,
            session.CurrentWorldInventoryCollectionGeneration.Value);
        Assert.IsFalse(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(555331, out _, out _));
    }

    [TestMethod]
    public void OldSession_WhenNewSessionExists_RejectsTemperatureLimitRegistration()
    {
        var oldSession = EnsureTrackedGameSession(5601);
        var newSession = EnsureTrackedGameSession(5602);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            oldSession.RegisterTemperatureLimit(
                gameObjectInstanceId: 56011,
                componentInstanceId: 56012,
                new TemperatureLimit(),
                Constraint(10, 20)));

        StringAssert.Contains(exception.Message, "not accepting publications");
        Assert.IsTrue(newSession.IsAcceptingPublications);
        Assert.AreEqual(
            0,
            newSession.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount);
    }

    [TestMethod]
    public void EnsureGameSession_WhenIdentityChanges_CreatesFreshSessionDiagnosticLimiter()
    {
        var oldSession = EnsureTrackedGameSession(5651);
        Assert.IsTrue(oldSession.DiagnosticLimiter.ShouldEmit("DTL_WORLD_UNRESOLVED"));
        Assert.IsFalse(oldSession.DiagnosticLimiter.ShouldEmit("DTL_WORLD_UNRESOLVED"));

        var newSession = EnsureTrackedGameSession(5652);

        Assert.AreNotSame(
            oldSession.DiagnosticLimiter,
            newSession.DiagnosticLimiter);
        Assert.IsTrue(newSession.DiagnosticLimiter.ShouldEmit("DTL_WORLD_UNRESOLVED"));
    }

    [TestMethod]
    public void RegisterTemperatureLimit_WhenSessionIsStopping_ThrowsLifecycleViolation()
    {
        var session = EnsureTrackedGameSession(5701);
        var detachedSession =
            DeliveryTemperatureGameSessionHost.DetachGameSession(5701);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId: 57011,
                componentInstanceId: 57012,
                new TemperatureLimit(),
                Constraint(10, 20)));

        StringAssert.Contains(exception.Message, "not accepting publications");
        Assert.AreEqual(
            0,
            session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount);
        DeliveryTemperatureGameSessionHost.CompleteShutdown(detachedSession);
    }

    [TestMethod]
    public void RemoveTemperatureLimit_WhenRegistrationBelongsToOldSession_DoesNotTouchCurrentSession()
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
        Assert.AreEqual(
            oldRegistration.ConstraintRegistrationToken,
            currentRegistration.ConstraintRegistrationToken);
        Assert.AreNotEqual(
            oldRegistration.GameSessionGeneration,
            currentRegistration.GameSessionGeneration);
        currentSession.RemoveTemperatureLimit(oldRegistration);

        Assert.AreSame(
            snapshotBeforeStaleRemoval,
            currentSession.TemperatureConstraints.CaptureSnapshot());
        Assert.IsTrue(currentSession.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                reusedGameObjectInstanceId,
                out var retainedComponent,
                out var retainedConstraintRegistration));
        Assert.AreSame(currentComponent, retainedComponent);
        Assert.AreEqual(
            currentRegistration.ConstraintRegistrationToken,
            retainedConstraintRegistration);
    }

    [TestMethod]
    public void TryReplaceTemperatureConstraint_WhenNormalizedConstraintIsIdentical_DoesNotAdvanceGeneration()
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

        Assert.IsTrue(registrationFound);
        Assert.AreSame(
            snapshotBeforeReplacement,
            session.TemperatureConstraints.CaptureSnapshot());
        Assert.AreEqual(
            snapshotBeforeReplacement.Generation,
            session.TemperatureConstraints.CaptureSnapshot().Generation);
    }

    [TestMethod]
    public void TryReplaceTemperatureConstraint_WhenRegistrationMatches_UpdatesBothOwnedServices()
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

        Assert.IsTrue(registrationFound);
        Assert.AreEqual(
            generationBeforeReplacement.Value + 1,
            session.TemperatureConstraints
                .CaptureSnapshot()
                .Generation
                .Value);
        Assert.IsTrue(session.TemperatureLimitComponents.TryGetConstraint(
            59511,
            out var observedConstraint,
            out var observedConstraintRegistration));
        Assert.AreEqual(replacementConstraint, observedConstraint);
        Assert.AreEqual(
            registration.ConstraintRegistrationToken,
            observedConstraintRegistration);
        Assert.IsTrue(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                59511,
                out var observedComponent,
                out _));
        Assert.AreSame(component, observedComponent);
    }

    [TestMethod]
    public void RegisterTemperatureLimit_WhenSessionIsActive_PublishesCompositeOwnershipToBothServices()
    {
        var session = EnsureTrackedGameSession(6001);
        var component = new TemperatureLimit();
        var constraint = Constraint(20, 40);

        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 60011,
            componentInstanceId: 60012,
            component,
            constraint);

        Assert.AreEqual(session.Generation, registration.GameSessionGeneration);
        Assert.AreEqual(60011, registration.GameObjectInstanceId);
        Assert.AreEqual(
            60012,
            registration.ConstraintRegistrationToken.ComponentInstanceId);
        Assert.IsTrue(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                60011,
                out var observedComponent,
                out var observedConstraintRegistration));
        Assert.AreSame(component, observedComponent);
        Assert.AreEqual(
            registration.ConstraintRegistrationToken,
            observedConstraintRegistration);
        Assert.IsTrue(session.TemperatureLimitComponents.TryGetConstraint(
            60011,
            out var observedConstraint,
            out _));
        Assert.AreEqual(constraint, observedConstraint);
        Assert.AreEqual(
            1,
            session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount);
    }

    [TestMethod]
    public void RegisterTemperatureLimit_WhenComponentIndexRejectsPublication_RollsBackConstraintRegistration()
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
        Assert.IsTrue(session.TemperatureLimitComponents.TryRegister(
            gameObjectInstanceId,
            retainedConflictingComponent,
            predictedFirstConstraintRegistration,
            constraint));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.RegisterTemperatureLimit(
                gameObjectInstanceId,
                componentInstanceId,
                new TemperatureLimit(),
                constraint));

        StringAssert.Contains(exception.Message, "component index");
        Assert.AreEqual(
            0,
            session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount);
        Assert.IsTrue(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(
                gameObjectInstanceId,
                out var retainedComponent,
                out var retainedRegistration));
        Assert.AreSame(retainedConflictingComponent, retainedComponent);
        Assert.AreEqual(
            predictedFirstConstraintRegistration,
            retainedRegistration);
    }

    [TestMethod]
    public void RemoveTemperatureLimit_WhenRegistrationMatches_RemovesBothOwnedEntriesIdempotently()
    {
        var session = EnsureTrackedGameSession(6201);
        var registration = session.RegisterTemperatureLimit(
            gameObjectInstanceId: 62011,
            componentInstanceId: 62012,
            new TemperatureLimit(),
            Constraint(20, 40));

        session.RemoveTemperatureLimit(registration);
        session.RemoveTemperatureLimit(registration);

        Assert.IsFalse(session.TemperatureLimitComponents
            .TryGetRegisteredComponent(62011, out _, out _));
        Assert.AreEqual(
            0,
            session.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount);
    }

    [TestMethod]
    public void TryReplaceTemperatureConstraint_WhenRegistrationIsStale_LeavesCurrentRegistrationUnchanged()
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

        Assert.IsFalse(currentSession.TryReplaceTemperatureConstraint(
            staleRegistration,
            Constraint(50, 60)));

        Assert.AreSame(
            snapshotBeforeStaleReplacement,
            currentSession.TemperatureConstraints.CaptureSnapshot());
        Assert.IsTrue(currentSession.TemperatureLimitComponents.TryGetConstraint(
            reusedGameObjectInstanceId,
            out var retainedConstraint,
            out _));
        Assert.AreEqual(currentConstraint, retainedConstraint);
    }

    [TestMethod]
    [DoNotParallelize]
    public void EnsureGameSession_WhenGenerationIsExhausted_ThrowsBeforePublication()
    {
        Assert.IsFalse(
            DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _));
        var generationSourceField = RequirePrivateStaticField(
            typeof(DeliveryTemperatureGameSessionHost),
            "lastIssuedGameSessionGeneration",
            typeof(long));
        var generationBeforeTest =
            Assert.IsInstanceOfType<long>(generationSourceField.GetValue(null));
        generationSourceField.SetValue(null, long.MaxValue);

        try
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                EnsureTrackedGameSession(6401));

            StringAssert.Contains(exception.Message, "game-session generation");
            Assert.IsFalse(
                DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _));
            Assert.AreEqual(
                long.MaxValue,
                Assert.IsInstanceOfType<long>(
                    generationSourceField.GetValue(null)));
        }
        finally
        {
            // This focused nonparallel test restores exactly the static value it
            // changed; it does not reset any production state created elsewhere.
            generationSourceField.SetValue(null, generationBeforeTest);
        }
    }

    [TestMethod]
    public void RetainedCollectionCapacityLimits_WhenInspected_ExposeReviewedResourcePolicies()
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
                    field => Assert.IsInstanceOfType<int>(
                        field.GetRawConstantValue()),
                    StringComparer.Ordinal);

        CollectionAssert.AreEquivalent(
            expectedConstantValuesByExactFieldName,
            observedConstantValuesByExactFieldName);
    }

    [TestMethod]
    public void LifecycleProductionTypes_WhenInspected_ContainOnlyNamedMutableStaticState()
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

        Assert.AreSequenceEqual(
            expectedMutableStaticFieldNames,
            observedMutableStaticFieldNames);
    }

    [TestMethod]
    public void LifecycleOperations_WhenDeterministicallyScheduled_RejectStaleGenerationMutation()
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
                    EnsureScheduledGameSession(
                        random,
                        ref expectedCurrentSession,
                        currentRegistrationsByGameObjectInstanceId,
                        staleRegistrations);
                    break;

                case 1:
                    AssertScheduledCapture(
                        expectedCurrentSession,
                        operationIndex);
                    break;

                case 2:
                    DetachScheduledGameSession(
                        random,
                        ref expectedCurrentSession,
                        currentRegistrationsByGameObjectInstanceId,
                        staleRegistrations,
                        explicitlyDetachedSessions,
                        operationIndex);
                    break;

                case 3:
                    CompleteScheduledShutdown(
                        explicitlyDetachedSessions,
                        random);
                    break;

                case 4:
                    RegisterScheduledTemperatureLimit(
                        random,
                        ref expectedCurrentSession,
                        currentRegistrationsByGameObjectInstanceId,
                        staleRegistrations);
                    break;

                default:
                    RemoveScheduledTemperatureLimit(
                        random,
                        expectedCurrentSession,
                        currentRegistrationsByGameObjectInstanceId,
                        staleRegistrations,
                        operationIndex);
                    break;
            }

            AssertScheduledCurrentState(
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

    private void EnsureScheduledGameSession(
        Random random,
        ref DeliveryTemperatureGameSession? expectedCurrentSession,
        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        ICollection<GameSessionTemperatureLimitRegistrationToken>
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
            Assert.IsFalse(priorSession.IsAcceptingPublications);
        }

        expectedCurrentSession = ensuredSession;
    }

    private static void AssertScheduledCapture(
        DeliveryTemperatureGameSession? expectedCurrentSession,
        int operationIndex)
    {
        var captured = DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
            out var observedSession);
        var assertionContext = LifecycleScheduleAssertionContext(operationIndex);

        Assert.AreEqual(
            expectedCurrentSession is not null,
            captured,
            assertionContext);
        if (expectedCurrentSession is null)
        {
            Assert.IsNull(observedSession, assertionContext);
        }
        else
        {
            Assert.AreSame(
                expectedCurrentSession,
                observedSession,
                assertionContext);
        }
    }

    private static void DetachScheduledGameSession(
        Random random,
        ref DeliveryTemperatureGameSession? expectedCurrentSession,
        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        ICollection<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations,
        ICollection<DeliveryTemperatureGameSession> explicitlyDetachedSessions,
        int operationIndex)
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
            Assert.IsNull(detachedSession, assertionContext);
            return;
        }

        Assert.AreSame(
            expectedCurrentSession,
            detachedSession,
            assertionContext);
        Assert.IsFalse(
            detachedSession!.IsAcceptingPublications,
            assertionContext);
        explicitlyDetachedSessions.Add(detachedSession);
        MoveCurrentRegistrationsToStale(
            currentRegistrationsByGameObjectInstanceId,
            staleRegistrations);
        expectedCurrentSession = null;
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

    private void RegisterScheduledTemperatureLimit(
        Random random,
        ref DeliveryTemperatureGameSession? expectedCurrentSession,
        IDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        ICollection<GameSessionTemperatureLimitRegistrationToken>
            staleRegistrations)
    {
        if (expectedCurrentSession is null)
        {
            EnsureScheduledGameSession(
                random,
                ref expectedCurrentSession,
                currentRegistrationsByGameObjectInstanceId,
                staleRegistrations);
        }

        var availableIdentityOffset = FindAvailableScheduleIdentityOffset(
            random,
            currentRegistrationsByGameObjectInstanceId);
        if (availableIdentityOffset < 0)
        {
            return;
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
    }

    private static void RemoveScheduledTemperatureLimit(
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

            Assert.AreSame(
                snapshotBeforeStaleRemoval,
                expectedCurrentSession.TemperatureConstraints.CaptureSnapshot(),
                LifecycleScheduleAssertionContext(operationIndex));
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

    private static void AssertScheduledCurrentState(
        DeliveryTemperatureGameSession? expectedCurrentSession,
        IReadOnlyDictionary<int, ScheduledRegistration>
            currentRegistrationsByGameObjectInstanceId,
        int operationIndex)
    {
        var assertionContext = LifecycleScheduleAssertionContext(operationIndex);
        if (expectedCurrentSession is null)
        {
            Assert.IsFalse(
                DeliveryTemperatureGameSessionHost.TryCaptureCurrent(out _),
                assertionContext);
            Assert.AreEqual(
                0,
                currentRegistrationsByGameObjectInstanceId.Count,
                assertionContext);
            return;
        }

        Assert.IsTrue(
            DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                out var observedCurrentSession),
            assertionContext);
        Assert.AreSame(
            expectedCurrentSession,
            observedCurrentSession,
            assertionContext);
        Assert.IsTrue(
            expectedCurrentSession.IsAcceptingPublications,
            assertionContext);
        Assert.AreEqual(
            currentRegistrationsByGameObjectInstanceId.Count,
            expectedCurrentSession.TemperatureConstraints
                .CaptureSnapshot()
                .EnabledConstraintCount,
            assertionContext);

        foreach (var scheduledRegistration in
                 currentRegistrationsByGameObjectInstanceId.Values)
        {
            Assert.AreEqual(
                expectedCurrentSession.Generation,
                scheduledRegistration.RegistrationToken.GameSessionGeneration,
                assertionContext);
            Assert.IsTrue(expectedCurrentSession.TemperatureLimitComponents
                .TryGetRegisteredComponent(
                    scheduledRegistration.RegistrationToken.GameObjectInstanceId,
                    out var observedComponent,
                    out var observedConstraintRegistration),
                assertionContext);
            Assert.AreSame(
                scheduledRegistration.Component,
                observedComponent,
                assertionContext);
            Assert.AreEqual(
                scheduledRegistration.RegistrationToken
                    .ConstraintRegistrationToken,
                observedConstraintRegistration,
                assertionContext);
            Assert.IsTrue(expectedCurrentSession.TemperatureLimitComponents
                .TryGetConstraint(
                    scheduledRegistration.RegistrationToken.GameObjectInstanceId,
                    out var observedConstraint,
                    out _),
                assertionContext);
            Assert.AreEqual(
                scheduledRegistration.Constraint,
                observedConstraint,
                assertionContext);
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

    private static void PublishCompleteWorldResourceAmount(
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
        Assert.IsTrue(
            session.WorldResourceTemperatureAmounts
                .PublishCompleteWorldResourceAmounts(
                    worldId,
                    builder.Build()));
    }

    private static float RequireCompleteTemperatureConstrainedAmount(
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
        Assert.IsTrue(
            availability.TryGetCompleteAvailableAmount(out var amount),
            $"Expected a complete aggregate for parent world " +
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

    private static FieldInfo RequirePrivateStaticField(
        Type declaringType,
        string exactFieldName,
        Type exactFieldType)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private static " +
            $"field {declaringType.Name}.{exactFieldName}.");
        Assert.AreEqual(exactFieldType, field.FieldType);
        return field;
    }

    private static FieldInfo RequirePrivateInstanceField(
        Type declaringType,
        string exactFieldName,
        Type exactFieldType)
    {
        var field = declaringType.GetField(
            exactFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            field,
            $"The representation contract requires the exact private instance " +
            $"field {declaringType.Name}.{exactFieldName}.");
        Assert.AreEqual(exactFieldType, field.FieldType);
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
