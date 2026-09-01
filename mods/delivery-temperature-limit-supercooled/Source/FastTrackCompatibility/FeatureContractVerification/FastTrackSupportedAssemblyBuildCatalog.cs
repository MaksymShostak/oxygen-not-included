#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable closed-world set of exact FastTrack assembly builds admitted
    /// for runtime compatibility inspection.
    /// </summary>
    internal sealed class FastTrackSupportedAssemblyBuildCatalog
    {
        private static readonly FastTrackSupportedAssemblyBuildCatalog
            DeclaredCatalog = new FastTrackSupportedAssemblyBuildCatalog(
                new[]
                {
                    new FastTrackAssemblyBuildIdentity(
                        new Version(0, 18, 4, 0),
                        "D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD"),
                    new FastTrackAssemblyBuildIdentity(
                        new Version(0, 18, 5, 0),
                        "CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B")
                });

        private readonly HashSet<FastTrackAssemblyBuildIdentity> buildSet;

        internal FastTrackSupportedAssemblyBuildCatalog(
            IEnumerable<FastTrackAssemblyBuildIdentity> builds)
        {
            if (builds == null)
            {
                throw new ArgumentNullException(nameof(builds));
            }

            var copiedBuilds = new List<FastTrackAssemblyBuildIdentity>();
            buildSet = new HashSet<FastTrackAssemblyBuildIdentity>();
            foreach (FastTrackAssemblyBuildIdentity? build in builds)
            {
                if (build == null)
                {
                    throw new ArgumentException(
                        "A supported FastTrack assembly build catalog cannot " +
                        "contain null.",
                        nameof(builds));
                }

                if (!buildSet.Add(build))
                {
                    throw new ArgumentException(
                        "A supported FastTrack assembly build catalog cannot " +
                        "repeat an exact build identity.",
                        nameof(builds));
                }

                copiedBuilds.Add(build);
            }

            copiedBuilds.Sort(CompareBuildIdentities);
            Builds = new ReadOnlyCollection<FastTrackAssemblyBuildIdentity>(
                copiedBuilds);
        }

        internal static FastTrackSupportedAssemblyBuildCatalog Declared =>
            DeclaredCatalog;

        internal IReadOnlyList<FastTrackAssemblyBuildIdentity> Builds { get; }

        internal bool Contains(
            Version fileVersion,
            string assemblySha256)
        {
            if (fileVersion == null)
            {
                return false;
            }

            string normalizedAssemblySha256;
            if (!FastTrackAssemblyBuildIdentity.TryNormalizeAssemblySha256(
                    assemblySha256,
                    out normalizedAssemblySha256))
            {
                return false;
            }

            return buildSet.Contains(new FastTrackAssemblyBuildIdentity(
                fileVersion,
                normalizedAssemblySha256));
        }

        private static int CompareBuildIdentities(
            FastTrackAssemblyBuildIdentity left,
            FastTrackAssemblyBuildIdentity right)
        {
            int fileVersionComparison =
                left.FileVersion.CompareTo(right.FileVersion);
            return fileVersionComparison != 0
                ? fileVersionComparison
                : StringComparer.Ordinal.Compare(
                    left.AssemblySha256,
                    right.AssemblySha256);
        }
    }
}
