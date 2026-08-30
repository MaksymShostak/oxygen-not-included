#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Carries the outcome of exactly one physical FastTrack assembly read.
    /// Success owns both values; every failure owns only a semantic diagnostic.
    /// </summary>
    internal sealed class FastTrackAssemblyFileIdentity
    {
        internal FastTrackAssemblyFileIdentity(
            FastTrackAssemblyFileIdentityReadState readState,
            Version? fileVersion,
            string? assemblySha256,
            string? failureMessage)
        {
            if (readState == FastTrackAssemblyFileIdentityReadState.NotRead)
            {
                throw new ArgumentException(
                    "NotRead is a report state and cannot be returned by the " +
                    "physical-file identity reader.",
                    nameof(readState));
            }

            if (readState == FastTrackAssemblyFileIdentityReadState.Success)
            {
                if (fileVersion == null)
                {
                    throw new ArgumentException(
                        "A successful FastTrack file identity requires a file " +
                        "version.",
                        nameof(fileVersion));
                }

                if (string.IsNullOrWhiteSpace(assemblySha256))
                {
                    throw new ArgumentException(
                        "A successful FastTrack file identity requires a SHA-256 " +
                        "digest.",
                        nameof(assemblySha256));
                }

                if (failureMessage != null)
                {
                    throw new ArgumentException(
                        "A successful FastTrack file identity cannot carry a " +
                        "failure message.",
                        nameof(failureMessage));
                }
            }
            else
            {
                if (fileVersion != null || assemblySha256 != null)
                {
                    throw new ArgumentException(
                        "A failed FastTrack file identity cannot expose partial " +
                        "file metadata.");
                }

                if (string.IsNullOrWhiteSpace(failureMessage))
                {
                    throw new ArgumentException(
                        "A failed FastTrack file identity requires a semantic " +
                        "failure message.",
                        nameof(failureMessage));
                }
            }

            ReadState = readState;
            FileVersion = fileVersion;
            AssemblySha256 = assemblySha256;
            FailureMessage = failureMessage;
        }

        internal FastTrackAssemblyFileIdentityReadState ReadState { get; }

        internal Version? FileVersion { get; }

        internal string? AssemblySha256 { get; }

        internal string? FailureMessage { get; }
    }
}
