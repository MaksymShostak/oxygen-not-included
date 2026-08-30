#nullable enable

using KSerialization;
using System;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Stores and publishes the delivery-temperature constraint for one ONI
    /// destination. Mutable lookup and constraint state belong to the current
    /// game session; this component owns only its serialized player settings and
    /// exact registration token.
    /// </summary>
    public class TemperatureLimit : KMonoBehaviour
    {
        [Serialize]
        [SerializeField] // Unity prefab cloning and Klei save loading must preserve the same private identity.
        private int lowLimit = OniStorableTemperatureBounds.MinimumTemperatureKelvin;

        [Serialize]
        [SerializeField] // Zero remains the serialized disabled representation for existing saves.
        private int highLimit = 0;

        // This token is deliberately session-stamped. A delayed Unity cleanup
        // callback therefore cannot remove a newer component that reused either
        // the GameObject or registry integer identity in a later loaded game.
        private GameSessionTemperatureLimitRegistrationToken? registrationToken;

        private static readonly EventSystem.IntraObjectHandler<TemperatureLimit>
            CopySettingsHandler =
                new EventSystem.IntraObjectHandler<TemperatureLimit>(
                    (component, data) => component.CopySettings(
                        Get(data as GameObject)));

        public const int MinValue = 0;

        public const int MaxValue = OniStorableTemperatureBounds.MaximumTemperatureKelvin;

        public int LowLimit => lowLimit;

        public int HighLimit => highLimit;

        public bool IsDisabled() => highLimit <= 0;

        public static TemperatureLimit? Get(GameObject? gameObject)
        {
            // Unity's overloaded equality treats a destroyed native object as
            // null. Calling GetInstanceID before this check can cross into an
            // invalid native object even when the managed reference is non-null.
            if (gameObject == null ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var gameSession))
            {
                return null;
            }

            int gameObjectInstanceId = gameObject.GetInstanceID();
            if (!gameSession.TemperatureLimitComponents
                    .TryGetRegisteredComponent(
                        gameObjectInstanceId,
                        out var component,
                        out var constraintRegistrationToken))
            {
                return null;
            }

            // The component and ownership token came from one immutable index
            // entry. If Unity destroyed the component without delivering cleanup,
            // exact-owner removal is safe even if a replacement wins immediately
            // after this read; the stale token cannot remove that replacement.
            if (component == null)
            {
                gameSession.RemoveTemperatureLimit(
                    new GameSessionTemperatureLimitRegistrationToken(
                        gameSession.Generation,
                        gameObjectInstanceId,
                        constraintRegistrationToken));
                return null;
            }

            return component;
        }

        public void CopySettings(TemperatureLimit? source)
        {
            if (source == null)
            {
                return;
            }

            ApplySerializedLimits(source.lowLimit, source.highLimit);
        }

        public void SetLowLimit(int value) =>
            ApplySerializedLimits(value, highLimit);

        public void SetHighLimit(int value) =>
            ApplySerializedLimits(lowLimit, value);

        public void Disable() => ApplySerializedLimits(lowLimit, 0);

        public bool AllowedByTemperature(float temperature) =>
            CreateCanonicalConstraint().Allows(temperature);

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.CopySettings, CopySettingsHandler);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            DeliveryTemperatureConstraint canonicalConstraint =
                CreateCanonicalConstraint();
            lowLimit = canonicalConstraint.MinimumInclusiveKelvin;
            highLimit = canonicalConstraint.MaximumExclusiveKelvin;

            if (!DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var gameSession))
            {
                // Runtime authority may have rejected this loaded game. The
                // installed component remains inert and retains no global state.
                registrationToken = null;
                return;
            }

            registrationToken = gameSession.RegisterTemperatureLimit(
                gameObject.GetInstanceID(),
                GetInstanceID(),
                this,
                canonicalConstraint);
        }

        protected override void OnCleanUp()
        {
            GameSessionTemperatureLimitRegistrationToken? ownedRegistration =
                registrationToken;
            registrationToken = null;

            if (ownedRegistration.HasValue &&
                DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var gameSession) &&
                ownedRegistration.Value.GameSessionGeneration.Equals(
                    gameSession.Generation))
            {
                gameSession.RemoveTemperatureLimit(ownedRegistration.Value);
            }

            base.OnCleanUp();
        }

        private void ApplySerializedLimits(
            int candidateLowLimit,
            int candidateHighLimit)
        {
            DeliveryTemperatureConstraint canonicalConstraint =
                DeliveryTemperatureConstraint.FromSerializedLimits(
                    candidateLowLimit,
                    candidateHighLimit);
            if (lowLimit == canonicalConstraint.MinimumInclusiveKelvin &&
                highLimit == canonicalConstraint.MaximumExclusiveKelvin)
            {
                // Idempotent UI/copy callbacks must not advance registry,
                // fetch-topology, or inventory collection generations.
                return;
            }

            lowLimit = canonicalConstraint.MinimumInclusiveKelvin;
            highLimit = canonicalConstraint.MaximumExclusiveKelvin;
            PublishConstraintReplacement(canonicalConstraint);
        }

        private DeliveryTemperatureConstraint CreateCanonicalConstraint() =>
            DeliveryTemperatureConstraint.FromSerializedLimits(
                lowLimit,
                highLimit);

        private void PublishConstraintReplacement(
            DeliveryTemperatureConstraint canonicalConstraint)
        {
            if (!registrationToken.HasValue ||
                !DeliveryTemperatureGameSessionHost.TryCaptureCurrent(
                    out var gameSession) ||
                !registrationToken.Value.GameSessionGeneration.Equals(
                    gameSession.Generation))
            {
                return;
            }

            // A false return means shutdown or exact ownership replacement won.
            // The serialized player choice remains valid, while the stale token
            // has no authority to mutate a later session.
            _ = gameSession.TryReplaceTemperatureConstraint(
                registrationToken.Value,
                canonicalConstraint);
        }
    }
}
