#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Monotonic nonzero identity of one game-session composition root.
    /// It prevents work captured for a prior save or main-menu lifetime from being
    /// accepted by services owned by a newer game session.
    /// </summary>
    internal readonly struct GameSessionGeneration :
        IEquatable<GameSessionGeneration>
    {
        internal GameSessionGeneration(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A game-session generation must be nonzero and positive.");
            }

            Value = value;
        }

        internal long Value { get; }

        public bool Equals(GameSessionGeneration other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is GameSessionGeneration other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();
    }
}
