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

        private int publicationAcceptanceState = PublicationsAccepted;
        private int inFlightPublicationCount;
        private int ownedStateReleaseState = OwnedStateReleaseNotStarted;

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

            TemperatureConstraintRegistrationToken constraintRegistrationToken =
                default(TemperatureConstraintRegistrationToken);
            bool constraintRegistrationChanged = false;
            bool componentIndexPublicationSucceeded = false;

            try
            {
                constraintRegistrationToken = TemperatureConstraints.Register(
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
                        "registration whose ownership token was already associated " +
                        "with different component state.");
                }

                componentIndexPublicationSucceeded = true;
                ThrowIfNotAcceptingPublications();

                return new GameSessionTemperatureLimitRegistrationToken(
                    Generation,
                    gameObjectInstanceId,
                    constraintRegistrationToken);
            }
            catch
            {
                // Cross-service mutation is intentionally sequential: no code holds
                // the registry lock and component-index synchronization at once. If
                // the second publication fails, remove only the exact ownership
                // acquired by this transaction before preserving the original error.
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

                throw;
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
                if (!TemperatureLimitComponents.TryGetConstraint(
                        registrationToken.GameObjectInstanceId,
                        out var priorConstraint,
                        out var observedConstraintRegistrationToken) ||
                    !observedConstraintRegistrationToken.Equals(
                        registrationToken.ConstraintRegistrationToken))
                {
                    return false;
                }

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

                // The immutable registry snapshot is published before the component
                // index entry, leaving a deliberately short observable ordering
                // window without ever holding both services' synchronization. The
                // fetch snapshot built in later tasks captures and validates the
                // registry generation, so mixed-generation work cannot publish.
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

                return true;
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

            if (!TemperatureLimitComponents.TryGetConstraint(
                    registrationToken.GameObjectInstanceId,
                    out _,
                    out var observedConstraintRegistrationToken) ||
                !observedConstraintRegistrationToken.Equals(
                    registrationToken.ConstraintRegistrationToken))
            {
                return;
            }

            // Remove the directly queried component entry first. Both services use
            // exact owner-conditional removal, so a delayed cleanup can never remove
            // a newer registration that reused either integer instance identity.
            if (!TemperatureLimitComponents.TryRemove(
                    registrationToken.GameObjectInstanceId,
                    registrationToken.ConstraintRegistrationToken))
            {
                return;
            }

            TemperatureConstraints.TryRemove(
                registrationToken.ConstraintRegistrationToken,
                out _);
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

                WorldParentTopology.ClearForGameSession();

                // The registry and component index have no retained external or
                // thread-static resources; detaching this session makes them
                // collectible as a unit. The topology catalog explicitly releases
                // its mutable map while its independently owned immutable snapshot
                // remains valid for already-captured readers. Later completed
                // session services add their clear calls at this same one-time point.
            }
            finally
            {
                Volatile.Write(
                    ref ownedStateReleaseState,
                    OwnedStateReleaseCompleted);
            }
        }

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
