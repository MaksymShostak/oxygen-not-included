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
        // Dependency selection is independent of runtime admission. Tests measure the physical DLL.
        internal static FastTrackAssemblyBuildIdentity SelectedBuild { get; } = new FastTrackAssemblyBuildIdentity(
            new Version(0, 18, 6, 0), "58DBBB3E347B3E9EDBAAE7FB9BF706C6AB304CE6D9216A8BFFBC4624B4138E1A");

        // The released beta has not passed paired U60 acceptance with distinguishable settings.
        private static readonly FastTrackSupportedAssemblyBuildCatalog DeclaredCatalog =
            new FastTrackSupportedAssemblyBuildCatalog(Array.Empty<FastTrackAssemblyBuildIdentity>());

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
