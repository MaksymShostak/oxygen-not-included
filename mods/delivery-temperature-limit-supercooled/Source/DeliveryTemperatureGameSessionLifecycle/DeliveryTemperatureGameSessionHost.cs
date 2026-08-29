#nullable enable

using System;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Lock-free publication boundary for the one currently loaded game session.
    /// It stores only the integer Game identity, never a Unity object, so the pure
    /// lifecycle can be linked into deterministic tests without game dependencies.
    /// </summary>
    internal static class DeliveryTemperatureGameSessionHost
    {
        private static DeliveryTemperatureGameSession? currentGameSession;
        private static long lastIssuedGameSessionGeneration;

        internal static DeliveryTemperatureGameSession EnsureGameSession(
            int gameInstanceId)
        {
            while (true)
            {
                var observedSession = Volatile.Read(ref currentGameSession);
                if (observedSession != null &&
                    observedSession.GameInstanceId == gameInstanceId &&
                    observedSession.IsAcceptingPublications)
                {
                    return observedSession;
                }

                // Allocate before disturbing an existing session. Exhausting the
                // monotonic identity source therefore cannot invalidate a still-
                // usable current session or publish a zero/wrapped generation.
                var candidateSession = new DeliveryTemperatureGameSession(
                    AllocateGameSessionGeneration(),
                    gameInstanceId);

                if (observedSession != null)
                {
                    // Stop first: a worker holding this object can thereafter mutate
                    // only an inactive session whose publication methods reject it.
                    // The replacement is never published while the old session still
                    // accepts work.
                    observedSession.StopAcceptingPublications();
                    if (!ReferenceEquals(
                            Interlocked.CompareExchange(
                                ref currentGameSession,
                                null,
                                observedSession),
                            observedSession))
                    {
                        DiscardUnpublishedSession(candidateSession);
                        continue;
                    }

                    CompleteShutdown(observedSession);
                }

                var competingSession = Interlocked.CompareExchange(
                    ref currentGameSession,
                    candidateSession,
                    null);
                if (competingSession is null)
                {
                    return candidateSession;
                }

                // A concurrent ensure won the publication race. This candidate was
                // never externally visible, but it still follows the real two-phase
                // lifecycle before the caller observes the winner on the next loop.
                DiscardUnpublishedSession(candidateSession);
            }
        }

        internal static bool TryCaptureCurrent(
            out DeliveryTemperatureGameSession session)
        {
            var observedSession = Volatile.Read(ref currentGameSession);
            if (observedSession != null &&
                observedSession.IsAcceptingPublications)
            {
                session = observedSession;
                return true;
            }

            session = null!;
            return false;
        }

        internal static DeliveryTemperatureGameSession? DetachGameSession(
            int gameInstanceId)
        {
            var observedSession = Volatile.Read(ref currentGameSession);
            if (observedSession is null ||
                observedSession.GameInstanceId != gameInstanceId)
            {
                return null;
            }

            observedSession.StopAcceptingPublications();
            return ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref currentGameSession,
                        null,
                        observedSession),
                    observedSession)
                ? observedSession
                : null;
        }

        internal static void CompleteShutdown(
            DeliveryTemperatureGameSession? detachedSession)
        {
            detachedSession?.ReleaseOwnedState();
        }

        private static GameSessionGeneration AllocateGameSessionGeneration()
        {
            while (true)
            {
                long observedGeneration = Volatile.Read(
                    ref lastIssuedGameSessionGeneration);
                if (observedGeneration == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "The game-session generation source is exhausted; it will " +
                        "not wrap or publish a reusable ownership identity.");
                }

                long candidateGeneration;
                try
                {
                    candidateGeneration = checked(observedGeneration + 1);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        "The game-session generation source is exhausted; it will " +
                        "not wrap or publish a reusable ownership identity.",
                        exception);
                }

                if (candidateGeneration <= 0)
                {
                    throw new InvalidOperationException(
                        "The game-session generation source produced a nonpositive " +
                        "identity and cannot safely publish a session.");
                }

                if (Interlocked.CompareExchange(
                        ref lastIssuedGameSessionGeneration,
                        candidateGeneration,
                        observedGeneration) == observedGeneration)
                {
                    return new GameSessionGeneration(candidateGeneration);
                }
            }
        }

        private static void DiscardUnpublishedSession(
            DeliveryTemperatureGameSession unpublishedSession)
        {
            unpublishedSession.StopAcceptingPublications();
            unpublishedSession.ReleaseOwnedState();
        }
    }
}
