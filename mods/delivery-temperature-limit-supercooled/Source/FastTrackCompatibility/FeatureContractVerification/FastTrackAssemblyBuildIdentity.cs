#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Identifies one exact FastTrack assembly build by its file version and
    /// complete content digest.
    /// </summary>
    internal sealed class FastTrackAssemblyBuildIdentity :
        IEquatable<FastTrackAssemblyBuildIdentity>
    {
        private const int Sha256HexadecimalLength = 64;

        internal FastTrackAssemblyBuildIdentity(
            Version fileVersion,
            string assemblySha256)
        {
            FileVersion = fileVersion ??
                throw new ArgumentNullException(nameof(fileVersion));
            if (assemblySha256 == null)
            {
                throw new ArgumentNullException(nameof(assemblySha256));
            }

            string normalizedAssemblySha256;
            if (!TryNormalizeAssemblySha256(
                    assemblySha256,
                    out normalizedAssemblySha256))
            {
                throw new ArgumentException(
                    "A FastTrack assembly SHA-256 must contain exactly 64 " +
                    "ASCII hexadecimal characters.",
                    nameof(assemblySha256));
            }

            AssemblySha256 = normalizedAssemblySha256;
        }

        internal Version FileVersion { get; }

        internal string AssemblySha256 { get; }

        internal static bool TryNormalizeAssemblySha256(
            string? assemblySha256,
            out string normalizedAssemblySha256)
        {
            normalizedAssemblySha256 = string.Empty;
            if (assemblySha256 == null ||
                assemblySha256.Length != Sha256HexadecimalLength)
            {
                return false;
            }

            for (int index = 0; index < assemblySha256.Length; index++)
            {
                char value = assemblySha256[index];
                bool isDecimalDigit = value >= '0' && value <= '9';
                bool isUppercaseHexadecimal = value >= 'A' && value <= 'F';
                bool isLowercaseHexadecimal = value >= 'a' && value <= 'f';
                if (!isDecimalDigit &&
                    !isUppercaseHexadecimal &&
                    !isLowercaseHexadecimal)
                {
                    return false;
                }
            }

            normalizedAssemblySha256 = assemblySha256.ToUpperInvariant();
            return true;
        }

        public bool Equals(FastTrackAssemblyBuildIdentity? other) =>
            other != null &&
            FileVersion.Equals(other.FileVersion) &&
            StringComparer.Ordinal.Equals(
                AssemblySha256,
                other.AssemblySha256);

        public override bool Equals(object? obj) =>
            Equals(obj as FastTrackAssemblyBuildIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                return (FileVersion.GetHashCode() * 397) ^
                    StringComparer.Ordinal.GetHashCode(AssemblySha256);
            }
        }
    }
}
