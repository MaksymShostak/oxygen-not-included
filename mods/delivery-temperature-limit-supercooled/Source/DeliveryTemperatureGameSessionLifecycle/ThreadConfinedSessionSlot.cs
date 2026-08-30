#nullable enable

using System;
using System.Threading;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Stores a LIFO context stack independently on each physical execution
    /// thread for one closed context type.
    /// </summary>
    /// <remarks>
    /// ONI and FastTrack may perform pickup work on worker threads. Thread-static
    /// state follows that execution model directly; <see cref="System.Threading.AsyncLocal{T}"/>
    /// would incorrectly flow a context across asynchronous continuations. The
    /// opaque token retains only scalar identity, never a context or prior frame.
    /// </remarks>
    internal static class ThreadConfinedSessionSlot<T>
        where T : class
    {
        private const int InitialFrameCapacity = 4;

        private static long lastIssuedTokenIdentity;

        [ThreadStatic]
        private static SessionFrame[]? currentThreadFrames;

        [ThreadStatic]
        private static int currentThreadFrameCount;

        internal static SessionScopeToken Enter(
            GameSessionGeneration gameSessionGeneration,
            T context)
        {
            if (gameSessionGeneration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameSessionGeneration),
                    "A thread-confined context requires a nonzero game-session " +
                    "generation.");
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            long tokenIdentity = AllocateTokenIdentity();
            bool generationHasChanged =
                currentThreadFrameCount > 0 &&
                !currentThreadFrames![currentThreadFrameCount - 1]
                    .GameSessionGeneration.Equals(
                    gameSessionGeneration);
            int requiredFrameCount = generationHasChanged
                ? 1
                : checked(currentThreadFrameCount + 1);
            EnsureFrameCapacity(requiredFrameCount);

            if (generationHasChanged)
            {
                // A worker can be reused by a later loaded game. Never nest new
                // work under a frame owned by the old game merely because an
                // earlier exceptional path failed to unwind it.
                ClearUsedFrames();
            }

            var candidateFrame = new SessionFrame(
                tokenIdentity,
                gameSessionGeneration,
                context);

            // Publish one fully initialized value-type frame. The reusable array
            // avoids allocating a linked frame object on every pickup update; only
            // previously unseen nesting depth can grow storage.
            currentThreadFrames![currentThreadFrameCount] = candidateFrame;
            currentThreadFrameCount++;
            return new SessionScopeToken(
                tokenIdentity,
                gameSessionGeneration);
        }

        internal static bool TryGetCurrent(out T context)
        {
            if (currentThreadFrameCount > 0)
            {
                context = currentThreadFrames![currentThreadFrameCount - 1]
                    .Context!;
                return true;
            }

            context = null!;
            return false;
        }

        internal static void Exit(SessionScopeToken token)
        {
            if (currentThreadFrameCount == 0 ||
                token.TokenIdentity <= 0 ||
                currentThreadFrames![currentThreadFrameCount - 1]
                    .TokenIdentity != token.TokenIdentity ||
                !currentThreadFrames[currentThreadFrameCount - 1]
                    .GameSessionGeneration.Equals(
                    token.GameSessionGeneration))
            {
                throw new InvalidOperationException(
                    "The thread-confined session token is stale or out of order; " +
                    "only the current LIFO scope may exit.");
            }

            // Clear the exact reference-bearing slot before publishing the smaller
            // depth. The reusable backing array therefore never pins a completed
            // context merely because it once held a deeper nested update.
            int completedFrameIndex = currentThreadFrameCount - 1;
            currentThreadFrames[completedFrameIndex] =
                default(SessionFrame);
            currentThreadFrameCount = completedFrameIndex;
        }

        internal static void DiscardAll()
        {
            // Clear every used value while retaining a small reusable frame array.
            // Token identities are deliberately not reused afterward.
            ClearUsedFrames();
        }

        private static void EnsureFrameCapacity(int requiredFrameCount)
        {
            SessionFrame[]? frames = currentThreadFrames;
            if (frames != null && frames.Length >= requiredFrameCount)
            {
                return;
            }

            int candidateCapacity = frames == null
                ? InitialFrameCapacity
                : checked(frames.Length * 2);
            while (candidateCapacity < requiredFrameCount)
            {
                candidateCapacity = checked(candidateCapacity * 2);
            }

            var replacementFrames = new SessionFrame[candidateCapacity];
            if (frames != null && currentThreadFrameCount > 0)
            {
                Array.Copy(
                    frames,
                    replacementFrames,
                    currentThreadFrameCount);
            }

            currentThreadFrames = replacementFrames;
        }

        private static void ClearUsedFrames()
        {
            if (currentThreadFrames != null &&
                currentThreadFrameCount > 0)
            {
                Array.Clear(
                    currentThreadFrames,
                    index: 0,
                    length: currentThreadFrameCount);
            }

            currentThreadFrameCount = 0;
        }

        private static long AllocateTokenIdentity()
        {
            while (true)
            {
                long observedIdentity = Volatile.Read(
                    ref lastIssuedTokenIdentity);
                if (observedIdentity == long.MaxValue)
                {
                    throw CreateTokenIdentityExhaustedException();
                }

                long candidateIdentity;
                try
                {
                    candidateIdentity = checked(observedIdentity + 1L);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        "The thread-confined session token identity source is " +
                        "exhausted and will not wrap or reuse an identity.",
                        exception);
                }

                if (candidateIdentity <= 0)
                {
                    throw CreateTokenIdentityExhaustedException();
                }

                if (Interlocked.CompareExchange(
                        ref lastIssuedTokenIdentity,
                        candidateIdentity,
                        observedIdentity) == observedIdentity)
                {
                    return candidateIdentity;
                }
            }
        }

        private static InvalidOperationException
            CreateTokenIdentityExhaustedException() =>
            new InvalidOperationException(
                "The thread-confined session token identity source is exhausted " +
                "and will not wrap or reuse an identity.");

        /// <summary>
        /// Scalar proof that a caller owns the currently entered LIFO frame.
        /// </summary>
        internal readonly struct SessionScopeToken
        {
            internal SessionScopeToken(
                long tokenIdentity,
                GameSessionGeneration gameSessionGeneration)
            {
                TokenIdentity = tokenIdentity;
                GameSessionGeneration = gameSessionGeneration;
            }

            internal long TokenIdentity { get; }

            internal GameSessionGeneration GameSessionGeneration { get; }
        }

        /// <summary>
        /// One value-type entry in the reusable thread-local LIFO buffer.
        /// </summary>
        private readonly struct SessionFrame
        {
            internal SessionFrame(
                long tokenIdentity,
                GameSessionGeneration gameSessionGeneration,
                T context)
            {
                TokenIdentity = tokenIdentity;
                GameSessionGeneration = gameSessionGeneration;
                Context = context;
            }

            internal long TokenIdentity { get; }

            internal GameSessionGeneration GameSessionGeneration { get; }

            internal T? Context { get; }
        }
    }
}
