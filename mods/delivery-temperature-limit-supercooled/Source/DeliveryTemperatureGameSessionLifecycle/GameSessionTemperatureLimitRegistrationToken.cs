#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Complete ownership identity for one session-scoped temperature-limit
    /// registration. All three identities are required because Unity instance IDs
    /// and registry sequences may legitimately be reused by a later game session.
    /// </summary>
    internal readonly struct GameSessionTemperatureLimitRegistrationToken :
        IEquatable<GameSessionTemperatureLimitRegistrationToken>
    {
        internal GameSessionTemperatureLimitRegistrationToken(
            GameSessionGeneration gameSessionGeneration,
            int gameObjectInstanceId,
            TemperatureConstraintRegistrationToken constraintRegistrationToken)
        {
            if (gameSessionGeneration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameSessionGeneration),
                    "A temperature-limit registration requires a nonzero " +
                    "game-session generation.");
            }

            GameSessionGeneration = gameSessionGeneration;
            GameObjectInstanceId = gameObjectInstanceId;
            ConstraintRegistrationToken = constraintRegistrationToken;
        }

        internal GameSessionGeneration GameSessionGeneration { get; }

        internal int GameObjectInstanceId { get; }

        internal TemperatureConstraintRegistrationToken
            ConstraintRegistrationToken { get; }

        public bool Equals(
            GameSessionTemperatureLimitRegistrationToken other) =>
            GameSessionGeneration.Equals(other.GameSessionGeneration) &&
            GameObjectInstanceId == other.GameObjectInstanceId &&
            ConstraintRegistrationToken.Equals(
                other.ConstraintRegistrationToken);

        public override bool Equals(object? obj) =>
            obj is GameSessionTemperatureLimitRegistrationToken other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = GameSessionGeneration.GetHashCode();
                hashCode = (hashCode * 397) ^ GameObjectInstanceId;
                hashCode =
                    (hashCode * 397) ^
                    ConstraintRegistrationToken.GetHashCode();
                return hashCode;
            }
        }
    }
}
