#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Reads the physical identity of a loaded FastTrack assembly exactly once.
    /// This is deliberately the only production type that touches
    /// <see cref="Assembly.Location"/>, file-version metadata, or assembly bytes.
    /// </summary>
    internal sealed class FastTrackAssemblyFileIdentityReader :
        IFastTrackAssemblyFileIdentityReader
    {
        public FastTrackAssemblyFileIdentity Read(Assembly fastTrackAssembly)
        {
            if (fastTrackAssembly == null)
            {
                throw new ArgumentNullException(nameof(fastTrackAssembly));
            }

            if (fastTrackAssembly.IsDynamic)
            {
                return Failure(
                    FastTrackAssemblyFileIdentityReadState.DynamicAssembly,
                    "The loaded FastTrack assembly is dynamic and therefore " +
                    "has no verifiable physical file.");
            }

            string assemblyPath;
            try
            {
                assemblyPath = fastTrackAssembly.Location;
            }
            catch (NotSupportedException exception)
            {
                return ReadFailure(exception);
            }
            catch (SecurityException exception)
            {
                return ReadFailure(exception);
            }

            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                return Failure(
                    FastTrackAssemblyFileIdentityReadState.LocationUnavailable,
                    "The loaded FastTrack assembly does not expose a physical " +
                    "file location.");
            }

            if (!File.Exists(assemblyPath))
            {
                return Failure(
                    FastTrackAssemblyFileIdentityReadState.AssemblyFileMissing,
                    "The physical file for the loaded FastTrack assembly no " +
                    "longer exists.");
            }

            try
            {
                FileVersionInfo versionInfo =
                    FileVersionInfo.GetVersionInfo(assemblyPath);
                var fileVersion = new Version(
                    versionInfo.FileMajorPart,
                    versionInfo.FileMinorPart,
                    versionInfo.FileBuildPart,
                    versionInfo.FilePrivatePart);
                string digest;
                using (var stream = new FileStream(
                           assemblyPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read))
                using (SHA256 algorithm = SHA256.Create())
                {
                    digest = ToUppercaseHexadecimal(
                        algorithm.ComputeHash(stream));
                }

                return new FastTrackAssemblyFileIdentity(
                    FastTrackAssemblyFileIdentityReadState.Success,
                    fileVersion,
                    digest,
                    null);
            }
            catch (UnauthorizedAccessException exception)
            {
                return ReadFailure(exception);
            }
            catch (IOException exception)
            {
                return ReadFailure(exception);
            }
            catch (NotSupportedException exception)
            {
                return ReadFailure(exception);
            }
            catch (SecurityException exception)
            {
                return ReadFailure(exception);
            }
            catch (CryptographicException exception)
            {
                return ReadFailure(exception);
            }
            catch (Win32Exception exception)
            {
                return ReadFailure(exception);
            }
            catch (ArgumentException exception)
            {
                return ReadFailure(exception);
            }
        }

        private static FastTrackAssemblyFileIdentity ReadFailure(
            Exception exception) =>
            Failure(
                FastTrackAssemblyFileIdentityReadState.ReadFailed,
                "The physical FastTrack assembly identity could not be read " +
                "(" +
                exception.GetType().Name +
                "): " +
                exception.Message);

        private static FastTrackAssemblyFileIdentity Failure(
            FastTrackAssemblyFileIdentityReadState readState,
            string message) =>
            new FastTrackAssemblyFileIdentity(
                readState,
                null,
                null,
                message);

        private static string ToUppercaseHexadecimal(byte[] digest)
        {
            // Convert explicitly rather than depending on a newer runtime-only
            // hexadecimal helper; the game-loaded project targets netstandard2.1.
            var builder = new StringBuilder(digest.Length * 2);
            for (var byteIndex = 0; byteIndex < digest.Length; byteIndex++)
            {
                builder.Append(digest[byteIndex].ToString("X2"));
            }

            return builder.ToString();
        }
    }
}
