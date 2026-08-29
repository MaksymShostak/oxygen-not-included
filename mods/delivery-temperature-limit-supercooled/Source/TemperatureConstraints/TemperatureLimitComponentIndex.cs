#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Maps a game-object instance identity to the complete immutable registration
    /// needed by direct delivery checks. The index calls no Unity API, so ordinary
    /// Klei and optional FastTrack readers share the same thread-safe lookup.
    /// </summary>
    internal sealed class TemperatureLimitComponentIndex
    {
        private readonly ConcurrentDictionary<
            int,
            TemperatureLimitComponentIndexEntry> entriesByGameObjectInstanceId =
                new ConcurrentDictionary<int, TemperatureLimitComponentIndexEntry>();

        internal bool TryRegister(
            int gameObjectInstanceId,
            TemperatureLimit component,
            TemperatureConstraintRegistrationToken registrationToken,
            DeliveryTemperatureConstraint constraint)
        {
            if (component is null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            TemperatureLimitComponentIndexEntry? candidateEntry = null;

            while (true)
            {
                if (!entriesByGameObjectInstanceId.TryGetValue(
                        gameObjectInstanceId,
                        out var observedEntry))
                {
                    candidateEntry ??= new TemperatureLimitComponentIndexEntry(
                        component,
                        registrationToken,
                        constraint);
                    if (entriesByGameObjectInstanceId.TryAdd(
                            gameObjectInstanceId,
                            candidateEntry))
                    {
                        return true;
                    }

                    // Another publisher won the missing-entry race. Re-read its
                    // complete immutable entry before deciding ownership.
                    continue;
                }

                if (observedEntry.RegistrationToken.Equals(registrationToken))
                {
                    // Repeating the exact publication is an allocation-free no-op.
                    // Reusing one token for different state is rejected: constraint
                    // changes use TryReplaceConstraint, while a new owner must have
                    // a new token.
                    return ReferenceEquals(observedEntry.Component, component) &&
                        observedEntry.Constraint.Equals(constraint);
                }

                candidateEntry ??= new TemperatureLimitComponentIndexEntry(
                    component,
                    registrationToken,
                    constraint);
                if (entriesByGameObjectInstanceId.TryUpdate(
                        gameObjectInstanceId,
                        candidateEntry,
                        observedEntry))
                {
                    return true;
                }

                // The comparison entry changed. Loop so a replacement is never
                // based on independently read component, token, or constraint state.
            }
        }

        internal bool TryReplaceConstraint(
            int gameObjectInstanceId,
            TemperatureConstraintRegistrationToken registrationToken,
            DeliveryTemperatureConstraint constraint)
        {
            while (entriesByGameObjectInstanceId.TryGetValue(
                gameObjectInstanceId,
                out var observedEntry))
            {
                if (!observedEntry.RegistrationToken.Equals(registrationToken))
                {
                    return false;
                }

                if (observedEntry.Constraint.Equals(constraint))
                {
                    return true;
                }

                var replacementEntry = new TemperatureLimitComponentIndexEntry(
                    observedEntry.Component,
                    registrationToken,
                    constraint);
                if (entriesByGameObjectInstanceId.TryUpdate(
                        gameObjectInstanceId,
                        replacementEntry,
                        observedEntry))
                {
                    return true;
                }

                // A concurrent publication changed the entry. Re-read it and reject
                // immediately if this token no longer owns the game-object identity.
            }

            return false;
        }

        internal bool TryRemove(
            int gameObjectInstanceId,
            TemperatureConstraintRegistrationToken registrationToken)
        {
            while (entriesByGameObjectInstanceId.TryGetValue(
                gameObjectInstanceId,
                out var observedEntry))
            {
                if (!observedEntry.RegistrationToken.Equals(registrationToken))
                {
                    return false;
                }

                var expectedPair = new KeyValuePair<
                    int,
                    TemperatureLimitComponentIndexEntry>(
                        gameObjectInstanceId,
                        observedEntry);

                // .NET Standard 2.1 has no public ConcurrentDictionary overload that
                // conditionally removes both key and value. Its ICollection pair
                // removal is atomic and compares this exact immutable entry. A stale
                // owner check followed by key-only TryRemove could delete a newer
                // replacement that wins between those two operations.
                var conditionalEntries =
                    (ICollection<KeyValuePair<
                        int,
                        TemperatureLimitComponentIndexEntry>>)
                    entriesByGameObjectInstanceId;
                if (conditionalEntries.Remove(expectedPair))
                {
                    return true;
                }

                // A concurrent update won. Re-read so this token removes only a
                // still-owned replacement and never a different registration.
            }

            return false;
        }

        internal bool TryGetRegisteredComponent(
            int gameObjectInstanceId,
            out TemperatureLimit component,
            out TemperatureConstraintRegistrationToken registrationToken)
        {
            if (entriesByGameObjectInstanceId.TryGetValue(
                    gameObjectInstanceId,
                    out var observedEntry))
            {
                // Copy both values from one captured immutable entry. Two dictionary
                // reads could pair an old component with a newer ownership token.
                component = observedEntry.Component;
                registrationToken = observedEntry.RegistrationToken;
                return true;
            }

            component = null!;
            registrationToken = default(TemperatureConstraintRegistrationToken);
            return false;
        }

        internal bool TryGetConstraint(
            int gameObjectInstanceId,
            out DeliveryTemperatureConstraint constraint,
            out TemperatureConstraintRegistrationToken registrationToken)
        {
            if (entriesByGameObjectInstanceId.TryGetValue(
                    gameObjectInstanceId,
                    out var observedEntry))
            {
                constraint = observedEntry.Constraint;
                registrationToken = observedEntry.RegistrationToken;
                return true;
            }

            constraint = default(DeliveryTemperatureConstraint);
            registrationToken = default(TemperatureConstraintRegistrationToken);
            return false;
        }

        /// <summary>
        /// Whole immutable dictionary value. Its semantic name is deliberately
        /// explicit because component, ownership, and constraint must move together.
        /// </summary>
        private sealed class TemperatureLimitComponentIndexEntry
        {
            internal TemperatureLimitComponentIndexEntry(
                TemperatureLimit component,
                TemperatureConstraintRegistrationToken registrationToken,
                DeliveryTemperatureConstraint constraint)
            {
                Component = component;
                RegistrationToken = registrationToken;
                Constraint = constraint;
            }

            internal TemperatureLimit Component { get; }

            internal TemperatureConstraintRegistrationToken RegistrationToken { get; }

            internal DeliveryTemperatureConstraint Constraint { get; }
        }
    }
}
