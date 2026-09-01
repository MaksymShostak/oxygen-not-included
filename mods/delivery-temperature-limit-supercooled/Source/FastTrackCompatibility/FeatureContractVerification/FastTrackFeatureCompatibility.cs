#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Identifies why an active FastTrack replacement could not be verified.
    /// Inactive replacements deliberately carry no failure because the Klei
    /// implementation remains authoritative in that case.
    /// </summary>
    internal enum FastTrackFeatureCompatibilityFailureCode
    {
        AssemblyFileIdentityUnavailable,
        UnsupportedAssemblyBuild,
        WorldInventoryContractViolation,
        PickupGroupingContractViolation,
        DirectDeliveryEligibilityContractViolation
    }

    /// <summary>
    /// Carries the immutable result for exactly one FastTrack feature. A ready
    /// result owns all reflected members its adapter may consume; later gameplay
    /// code must not repeat reflection or reinterpret an incomplete result.
    /// </summary>
    internal sealed class FastTrackFeatureCompatibility
    {
        private static readonly IReadOnlyDictionary<
                FastTrackVerifiedMember,
                MemberInfo>
            NoVerifiedMembers =
                new ReadOnlyDictionary<FastTrackVerifiedMember, MemberInfo>(
                    new Dictionary<FastTrackVerifiedMember, MemberInfo>());

        private FastTrackFeatureCompatibility(
            FastTrackFeature feature,
            FastTrackFeatureCompatibilityState state,
            IReadOnlyDictionary<FastTrackVerifiedMember, MemberInfo>
                verifiedMembers,
            FastTrackFeatureCompatibilityFailureCode? failureCode,
            string? failureMessage)
        {
            Feature = feature;
            State = state;
            VerifiedMembers = verifiedMembers;
            FailureCode = failureCode;
            FailureMessage = failureMessage;
        }

        internal FastTrackFeature Feature { get; }

        internal FastTrackFeatureCompatibilityState State { get; }

        internal IReadOnlyDictionary<FastTrackVerifiedMember, MemberInfo>
            VerifiedMembers { get; }

        internal FastTrackFeatureCompatibilityFailureCode? FailureCode { get; }

        internal string? FailureMessage { get; }

        internal MemberInfo GetVerifiedMember(
            FastTrackVerifiedMember verifiedMember)
        {
            if (!Enum.IsDefined(
                    typeof(FastTrackVerifiedMember),
                    verifiedMember))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verifiedMember),
                    verifiedMember,
                    "Unknown FastTrack verified-member role.");
            }

            if (State != FastTrackFeatureCompatibilityState.Ready)
            {
                throw new InvalidOperationException(
                    "Reflected FastTrack members are available only for a " +
                    "feature whose compatibility state is Ready.");
            }

            MemberInfo? member;
            if (!VerifiedMembers.TryGetValue(verifiedMember, out member) ||
                member == null)
            {
                throw new ArgumentException(
                    "The requested reflected member does not belong to the " +
                    Feature +
                    " FastTrack feature contract.",
                    nameof(verifiedMember));
            }

            return member;
        }

        internal static FastTrackFeatureCompatibility ModNotLoaded(
            FastTrackFeature feature) =>
            CreateNonCompatibleState(
                feature,
                FastTrackFeatureCompatibilityState.ModNotLoaded);

        internal static FastTrackFeatureCompatibility ReplacementInactive(
            FastTrackFeature feature) =>
            CreateNonCompatibleState(
                feature,
                FastTrackFeatureCompatibilityState.ReplacementInactive);

        internal static FastTrackFeatureCompatibility Ready(
            FastTrackFeature feature,
            IDictionary<FastTrackVerifiedMember, MemberInfo> verifiedMembers)
        {
            if (verifiedMembers == null)
            {
                throw new ArgumentNullException(nameof(verifiedMembers));
            }

            if (verifiedMembers.Count == 0)
            {
                throw new ArgumentException(
                    "A ready FastTrack feature must expose its verified " +
                    "reflected members.",
                    nameof(verifiedMembers));
            }

            var copiedMembers =
                new Dictionary<FastTrackVerifiedMember, MemberInfo>(
                    verifiedMembers.Count);
            foreach (KeyValuePair<FastTrackVerifiedMember, MemberInfo> member in
                     verifiedMembers)
            {
                if (member.Value == null)
                {
                    throw new ArgumentException(
                        "A verified FastTrack member cannot be null.",
                        nameof(verifiedMembers));
                }

                copiedMembers.Add(member.Key, member.Value);
            }

            return new FastTrackFeatureCompatibility(
                feature,
                FastTrackFeatureCompatibilityState.Ready,
                new ReadOnlyDictionary<FastTrackVerifiedMember, MemberInfo>(
                    copiedMembers),
                null,
                null);
        }

        internal static FastTrackFeatureCompatibility Incompatible(
            FastTrackFeature feature,
            FastTrackFeatureCompatibilityFailureCode failureCode,
            string failureMessage)
        {
            if (string.IsNullOrWhiteSpace(failureMessage))
            {
                throw new ArgumentException(
                    "An incompatible FastTrack feature requires a semantic " +
                    "failure message.",
                    nameof(failureMessage));
            }

            return new FastTrackFeatureCompatibility(
                feature,
                FastTrackFeatureCompatibilityState.Incompatible,
                NoVerifiedMembers,
                failureCode,
                failureMessage);
        }

        private static FastTrackFeatureCompatibility CreateNonCompatibleState(
            FastTrackFeature feature,
            FastTrackFeatureCompatibilityState state) =>
            new FastTrackFeatureCompatibility(
                feature,
                state,
                NoVerifiedMembers,
                null,
                null);
    }
}
