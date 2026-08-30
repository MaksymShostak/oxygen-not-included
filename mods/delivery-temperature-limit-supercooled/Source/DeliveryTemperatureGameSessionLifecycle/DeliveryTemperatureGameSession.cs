#nullable enable

using System;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns all mutable domain services for exactly one loaded ONI game lifetime.
    /// Static Harmony entry points capture this object through the host; all domain
    /// algorithms remain ordinary instance-based services.
    /// </summary>
    internal sealed class DeliveryTemperatureGameSession
    {
        private const int PublicationsStopped = 0;
        private const int PublicationsAccepted = 1;
        private const int OwnedStateReleaseNotStarted = 0;
        private const int OwnedStateReleaseInProgress = 1;
        private const int OwnedStateReleaseCompleted = 2;

        private readonly object temperatureLimitMutationLock = new object();
        private readonly object worldTopologyMutationLock = new object();
        private int publicationAcceptanceState = PublicationsAccepted;
        private int inFlightPublicationCount;
        private int ownedStateReleaseState = OwnedStateReleaseNotStarted;
        private long currentWorldInventoryCollectionGenerationValue;
        private FetchTemperatureEligibilitySnapshot?
            currentFetchTemperatureEligibility;

        internal DeliveryTemperatureGameSession(
            GameSessionGeneration generation,
            int gameInstanceId)
        {
            if (generation.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(generation),
                    "A game session requires a nonzero generation.");
            }

            Generation = generation;
            GameInstanceId = gameInstanceId;
            TemperatureConstraints = new TemperatureConstraintRegistry();
            TemperatureLimitComponents = new TemperatureLimitComponentIndex();
            WorldParentTopology = new WorldParentTopologyCatalog(generation);
            WorldResourceTemperatureAmounts =
                new WorldResourceTemperatureAmountCatalog();
            FetchRequestTopology = new FetchRequestTopologyTracker();
            DiagnosticLimiter = new SessionDiagnosticLimiter();
        }

        internal GameSessionGeneration Generation { get; }

        internal int GameInstanceId { get; }

        internal bool IsAcceptingPublications =>
            Volatile.Read(ref publicationAcceptanceState) ==
            PublicationsAccepted;

        internal TemperatureConstraintRegistry TemperatureConstraints { get; }

        internal TemperatureLimitComponentIndex TemperatureLimitComponents { get; }

        internal WorldParentTopologyCatalog WorldParentTopology { get; }

        internal WorldResourceTemperatureAmountCatalog
            WorldResourceTemperatureAmounts { get; }

        internal FetchRequestTopologyTracker FetchRequestTopology { get; }

        internal WorldInventoryCollectionGeneration
            CurrentWorldInventoryCollectionGeneration
        {
            get
            {
                long generationValue = Volatile.Read(
                    ref currentWorldInventoryCollectionGenerationValue);
                return generationValue == 0
                    ? default(WorldInventoryCollectionGeneration)
                    : new WorldInventoryCollectionGeneration(generationValue);
            }
        }

        internal FetchTemperatureEligibilitySnapshot?
            CurrentFetchTemperatureEligibility =>
                Volatile.Read(ref currentFetchTemperatureEligibility);

        internal SessionDiagnosticLimiter DiagnosticLimiter { get; }

        internal GameSessionTemperatureLimitRegistrationToken
            RegisterTemperatureLimit(
                int gameObjectInstanceId,
                int componentInstanceId,
                TemperatureLimit component,
                DeliveryTemperatureConstraint constraint)
        {
            if (!TryBeginPublication())
            {
                throw CreatePublicationLifecycleViolation();
            }

            try
            {
                lock (temperatureLimitMutationLock)
                {
                    var priorConstraintSnapshot =
                        TemperatureConstraints.CaptureSnapshot();
                    long? preparedInventoryCollectionGenerationValue =
                        priorConstraintSnapshot.EnabledConstraintCount == 0 &&
                        constraint.IsEnabled
                            ? GetNextWorldInventoryCollectionGenerationValue()
                            : (long?)null;
                    long priorInventoryCollectionGenerationValue =
                        Volatile.Read(
                            ref currentWorldInventoryCollectionGenerationValue);
                    TemperatureConstraintRegistrationToken
                        constraintRegistrationToken =
                            default(TemperatureConstraintRegistrationToken);
                    bool constraintRegistrationChanged = false;
                    bool componentIndexPublicationSucceeded = false;

                    try
                    {
                        constraintRegistrationToken =
                            TemperatureConstraints.Register(
                                componentInstanceId,
                                constraint,
                                out constraintRegistrationChanged);
                        ThrowIfNotAcceptingPublications();

                        if (!TemperatureLimitComponents.TryRegister(
                                gameObjectInstanceId,
                                component,
                                constraintRegistrationToken,
                                constraint))
                        {
                            throw new InvalidOperationException(
                                "The temperature-limit component index rejected a " +
                                "registration whose ownership token was already " +
                                "associated with different component state.");
                        }

                        componentIndexPublicationSucceeded = true;
                        ThrowIfNotAcceptingPublications();

                        if (constraintRegistrationChanged)
                        {
                            // The registry and component index are complete before
                            // dependent topology changes. A candidate stamped with
                            // the prior version can therefore never publish afterward.
                            FetchRequestTopology.RecordEffectiveChange();
                            PublishInventoryCollectionTransition(
                                priorConstraintSnapshot.EnabledConstraintCount,
                                TemperatureConstraints.CaptureSnapshot()
                                    .EnabledConstraintCount,
                                preparedInventoryCollectionGenerationValue);
                        }

                        return new GameSessionTemperatureLimitRegistrationToken(
                            Generation,
                            gameObjectInstanceId,
                            constraintRegistrationToken);
                    }
                    catch
                    {
                        // Cross-service mutation is intentionally sequential: no code
                        // holds either owned service's internal synchronization while
                        // invoking the other. Roll back only exact acquired ownership.
                        if (componentIndexPublicationSucceeded &&
                            constraintRegistrationChanged)
                        {
                            TemperatureLimitComponents.TryRemove(
                                gameObjectInstanceId,
                                constraintRegistrationToken);
                        }

                        if (constraintRegistrationChanged)
                        {
                            TemperatureConstraints.TryRemove(
                                constraintRegistrationToken,
                                out _);
                        }

                        Volatile.Write(
                            ref currentWorldInventoryCollectionGenerationValue,
                            priorInventoryCollectionGenerationValue);
                        throw;
                    }
                }
            }
            finally
            {
                EndPublication();
            }
        }

        internal bool TryReplaceTemperatureConstraint(
            GameSessionTemperatureLimitRegistrationToken registrationToken,
            DeliveryTemperatureConstraint constraint)
        {
            if (!registrationToken.GameSessionGeneration.Equals(Generation) ||
                !TryBeginPublication())
            {
                return false;
            }

            try
            {
                lock (temperatureLimitMutationLock)
                {
                    if (!TemperatureLimitComponents.TryGetConstraint(
                            registrationToken.GameObjectInstanceId,
                            out var priorConstraint,
                            out var observedConstraintRegistrationToken) ||
                        !observedConstraintRegistrationToken.Equals(
                            registrationToken.ConstraintRegistrationToken))
                    {
                        return false;
                    }

                    var priorConstraintSnapshot =
                        TemperatureConstraints.CaptureSnapshot();
                    long? preparedInventoryCollectionGenerationValue =
                        priorConstraintSnapshot.EnabledConstraintCount == 0 &&
                        constraint.IsEnabled
                            ? GetNextWorldInventoryCollectionGenerationValue()
                            : (long?)null;

                    if (!TemperatureConstraints.TryReplace(
                            registrationToken.ConstraintRegistrationToken,
                            constraint,
                            out var registryStateChanged))
                    {
                        return false;
                    }

                    if (!IsAcceptingPublications)
                    {
                        if (registryStateChanged)
                        {
                            RestoreConstraintRegistryEntry(
                                registrationToken.ConstraintRegistrationToken,
                                priorConstraint);
                        }

                        return false;
                    }

                    bool componentIndexStateChanged =
                        !priorConstraint.Equals(constraint);
                    if (componentIndexStateChanged &&
                        !TemperatureLimitComponents.TryReplaceConstraint(
                            registrationToken.GameObjectInstanceId,
                            registrationToken.ConstraintRegistrationToken,
                            constraint))
                    {
                        if (registryStateChanged)
                        {
                            RestoreConstraintRegistryEntry(
                                registrationToken.ConstraintRegistrationToken,
                                priorConstraint);
                        }

                        return false;
                    }

                    // The immutable registry snapshot is published before the
                    // component index entry. Generation validation prevents a
                    // candidate captured in that short ordering window from
                    // becoming the current combined fetch snapshot.
                    if (!IsAcceptingPublications)
                    {
                        if (componentIndexStateChanged)
                        {
                            RestoreComponentIndexConstraint(
                                registrationToken,
                                priorConstraint);
                        }

                        if (registryStateChanged)
                        {
                            RestoreConstraintRegistryEntry(
                                registrationToken.ConstraintRegistrationToken,
                                priorConstraint);
                        }

                        return false;
                    }

                    if (registryStateChanged)
                    {
                        FetchRequestTopology.RecordEffectiveChange();
                        PublishInventoryCollectionTransition(
                            priorConstraintSnapshot.EnabledConstraintCount,
                            TemperatureConstraints.CaptureSnapshot()
                                .EnabledConstraintCount,
                            preparedInventoryCollectionGenerationValue);
                    }

                    return true;
                }
            }
            finally
            {
                EndPublication();
            }
        }

        internal void RemoveTemperatureLimit(
            GameSessionTemperatureLimitRegistrationToken registrationToken)
        {
            if (!registrationToken.GameSessionGeneration.Equals(Generation))
            {
                return;
            }

            lock (temperatureLimitMutationLock)
            {
                if (!TemperatureLimitComponents.TryGetConstraint(
                        registrationToken.GameObjectInstanceId,
                        out _,
                        out var observedConstraintRegistrationToken) ||
                    !observedConstraintRegistrationToken.Equals(
                        registrationToken.ConstraintRegistrationToken))
                {
                    return;
                }

                var priorConstraintSnapshot =
                    TemperatureConstraints.CaptureSnapshot();

                // Remove the directly queried component entry first. Both services
                // use exact owner-conditional removal, so delayed cleanup can never
                // remove a newer registration that reused either integer identity.
                if (!TemperatureLimitComponents.TryRemove(
                        registrationToken.GameObjectInstanceId,
                        registrationToken.ConstraintRegistrationToken))
                {
                    return;
                }

                if (TemperatureConstraints.TryRemove(
                        registrationToken.ConstraintRegistrationToken,
                        out var registryStateChanged) &&
                    registryStateChanged)
                {
                    FetchRequestTopology.RecordEffectiveChange();
                    PublishInventoryCollectionTransition(
                        priorConstraintSnapshot.EnabledConstraintCount,
                        TemperatureConstraints.CaptureSnapshot()
                            .EnabledConstraintCount,
                        preparedInventoryCollectionGenerationValue: null);
                }
            }
        }

        internal bool TryPublishFetchTemperatureEligibility(
            FetchTemperatureEligibilitySnapshot candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!TryBeginPublication())
            {
                return false;
            }

            try
            {
                // Capture each independently published state exactly once. A
                // candidate is all-or-nothing: no dictionary is merged into live
                // state, and the prior immutable reference survives every mismatch.
                ActiveTemperatureConstraintSnapshot currentConstraints =
                    TemperatureConstraints.CaptureSnapshot();
                WorldParentTopologySnapshot currentWorldTopology =
                    WorldParentTopology.CaptureSnapshot();
                FetchRequestTopologyVersion currentFetchTopologyVersion =
                    FetchRequestTopology.CaptureVersion();

                if (!candidate.GameSessionGeneration.Equals(Generation) ||
                    !currentWorldTopology.GameSessionGeneration.Equals(Generation) ||
                    !candidate.ConstraintGeneration.Equals(
                        currentConstraints.Generation) ||
                    !candidate.FetchTopologyVersion.Equals(
                        currentFetchTopologyVersion) ||
                    !candidate.WorldTopologyVersion.Equals(
                        currentWorldTopology.Version) ||
                    !IsAcceptingPublications)
                {
                    return false;
                }

                Volatile.Write(
                    ref currentFetchTemperatureEligibility,
                    candidate);
                return true;
            }
            finally
            {
                EndPublication();
            }
        }

        internal WorldParentTopologyChange RegisterWorld(
            int worldId,
            int parentWorldId)
        {
            if (!TryBeginPublication())
            {
                throw CreatePublicationLifecycleViolation();
            }

            try
            {
                lock (worldTopologyMutationLock)
                {
                    WorldParentTopologyChange change =
                        WorldParentTopology.RegisterWorld(worldId, parentWorldId);
                    if (!change.HasChanged)
                    {
                        return change;
                    }

                    // Each catalog releases its own lock before returning. The fetch
                    // version advances only after both topology projections agree.
                    WorldResourceTemperatureAmounts.RegisterWorld(
                        worldId,
                        parentWorldId);
                    ThrowIfNotAcceptingPublications();
                    FetchRequestTopology.RecordEffectiveChange();
                    return change;
                }
            }
            finally
            {
                EndPublication();
            }
        }

        internal WorldParentTopologyChange RemoveWorld(int worldId)
        {
            if (!TryBeginPublication())
            {
                throw CreatePublicationLifecycleViolation();
            }

            try
            {
                lock (worldTopologyMutationLock)
                {
                    WorldParentTopologyChange change =
                        WorldParentTopology.RemoveWorld(worldId);
                    if (!change.HasChanged)
                    {
                        return change;
                    }

                    WorldResourceTemperatureAmounts.RemoveWorld(worldId);
                    ThrowIfNotAcceptingPublications();
                    FetchRequestTopology.RecordEffectiveChange();
                    return change;
                }
            }
            finally
            {
                EndPublication();
            }
        }

        internal void StopAcceptingPublications()
        {
            Interlocked.Exchange(
                ref publicationAcceptanceState,
                PublicationsStopped);
        }

        internal void ReleaseOwnedState()
        {
            StopAcceptingPublications();

            int observedReleaseState = Interlocked.CompareExchange(
                ref ownedStateReleaseState,
                OwnedStateReleaseInProgress,
                OwnedStateReleaseNotStarted);
            if (observedReleaseState != OwnedStateReleaseNotStarted)
            {
                WaitForOwnedStateReleaseCompletion();
                return;
            }

            try
            {
                // Stop makes TryBeginPublication reject newcomers. Waiting for the
                // bounded set already inside a pure-domain operation prevents later
                // world/fetch ClearForGameSession calls from racing a late publisher.
                var spinWait = new SpinWait();
                while (Volatile.Read(ref inFlightPublicationCount) != 0)
                {
                    spinWait.SpinOnce();
                }

                Volatile.Write(
                    ref currentFetchTemperatureEligibility,
                    null);
                WorldParentTopology.ClearForGameSession();
                WorldResourceTemperatureAmounts.ClearForGameSession();

                // The registry and component index have no retained external or
                // thread-static resources; detaching this session makes them
                // collectible as a unit. The topology catalog explicitly releases
                // its mutable map while its independently owned immutable snapshot
                // remains valid for already-captured readers. The combined fetch
                // snapshot is released only after every in-flight publisher exits.
            }
            finally
            {
                Volatile.Write(
                    ref ownedStateReleaseState,
                    OwnedStateReleaseCompleted);
            }
        }

        private long GetNextWorldInventoryCollectionGenerationValue()
        {
            long currentGenerationValue = Volatile.Read(
                ref currentWorldInventoryCollectionGenerationValue);
            if (currentGenerationValue == long.MaxValue)
            {
                throw CreateWorldInventoryCollectionGenerationExhaustedException();
            }

            try
            {
                long nextGenerationValue = checked(currentGenerationValue + 1L);
                if (nextGenerationValue <= 0)
                {
                    throw CreateWorldInventoryCollectionGenerationExhaustedException();
                }

                return nextGenerationValue;
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "The world-inventory collection generation is exhausted; " +
                    "collection will not start with a wrapped or reusable identity.",
                    exception);
            }
        }

        private void PublishInventoryCollectionTransition(
            int priorEnabledConstraintCount,
            int currentEnabledConstraintCount,
            long? preparedInventoryCollectionGenerationValue)
        {
            if (priorEnabledConstraintCount == 0 &&
                currentEnabledConstraintCount > 0)
            {
                if (!preparedInventoryCollectionGenerationValue.HasValue)
                {
                    throw new InvalidOperationException(
                        "A zero-to-nonzero enabled-constraint transition did not " +
                        "precompute its next inventory collection generation.");
                }

                Volatile.Write(
                    ref currentWorldInventoryCollectionGenerationValue,
                    preparedInventoryCollectionGenerationValue.Value);
                return;
            }

            if (priorEnabledConstraintCount > 0 &&
                currentEnabledConstraintCount == 0)
            {
                // The monotonic generation value remains the last issued identity;
                // the zero enabled-count snapshot is the explicit bypass state. Drop
                // all amounts/proofs while retaining registered world topology so a
                // later collection generation starts incomplete, never falsely zero.
                WorldResourceTemperatureAmounts
                    .ClearTemperatureAmountPublicationsForCollectionBypass();
            }
        }

        private static InvalidOperationException
            CreateWorldInventoryCollectionGenerationExhaustedException() =>
            new InvalidOperationException(
                "The world-inventory collection generation is exhausted; " +
                "collection will not start with a wrapped or reusable identity.");

        private bool TryBeginPublication()
        {
            if (!IsAcceptingPublications)
            {
                return false;
            }

            Interlocked.Increment(ref inFlightPublicationCount);
            if (IsAcceptingPublications)
            {
                return true;
            }

            Interlocked.Decrement(ref inFlightPublicationCount);
            return false;
        }

        private void EndPublication()
        {
            int remainingPublicationCount =
                Interlocked.Decrement(ref inFlightPublicationCount);
            if (remainingPublicationCount < 0)
            {
                throw new InvalidOperationException(
                    "The in-flight game-session publication count became negative.");
            }
        }

        private void ThrowIfNotAcceptingPublications()
        {
            if (!IsAcceptingPublications)
            {
                throw CreatePublicationLifecycleViolation();
            }
        }

        private static InvalidOperationException
            CreatePublicationLifecycleViolation() =>
            new InvalidOperationException(
                "The delivery temperature game session is not accepting " +
                "publications because shutdown has started.");

        private void RestoreConstraintRegistryEntry(
            TemperatureConstraintRegistrationToken constraintRegistrationToken,
            DeliveryTemperatureConstraint priorConstraint)
        {
            if (!TemperatureConstraints.TryReplace(
                    constraintRegistrationToken,
                    priorConstraint,
                    out _))
            {
                throw new InvalidOperationException(
                    "A rejected temperature-constraint publication could not " +
                    "restore its exact registry owner.");
            }
        }

        private void RestoreComponentIndexConstraint(
            GameSessionTemperatureLimitRegistrationToken registrationToken,
            DeliveryTemperatureConstraint priorConstraint)
        {
            if (!TemperatureLimitComponents.TryReplaceConstraint(
                    registrationToken.GameObjectInstanceId,
                    registrationToken.ConstraintRegistrationToken,
                    priorConstraint))
            {
                throw new InvalidOperationException(
                    "A rejected temperature-constraint publication could not " +
                    "restore its exact component-index owner.");
            }
        }

        private void WaitForOwnedStateReleaseCompletion()
        {
            var spinWait = new SpinWait();
            while (Volatile.Read(ref ownedStateReleaseState) !=
                   OwnedStateReleaseCompleted)
            {
                spinWait.SpinOnce();
            }
        }
    }
}
