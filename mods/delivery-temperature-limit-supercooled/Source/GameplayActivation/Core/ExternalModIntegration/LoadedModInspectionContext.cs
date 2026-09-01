#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Immutable copy of one Klei loaded-mod entry. Assembly association remains
    /// attached to the same static-ID entry so a co-resident mod cannot satisfy
    /// another declaration's identity contract.
    /// </summary>
    internal sealed class LoadedModCandidate
    {
        internal LoadedModCandidate(
            bool isActive,
            string staticId,
            IEnumerable<Assembly> loadedAssemblies)
        {
            IsActive = isActive;
            StaticId = ExternalModIntegrationModelValidation
                .RequireExactBoundedText(
                    staticId,
                    nameof(staticId),
                    256,
                    "A loaded mod static ID");
            LoadedAssemblies = CopyAssemblies(loadedAssemblies);
        }

        internal bool IsActive { get; }

        internal string StaticId { get; }

        internal IReadOnlyList<Assembly> LoadedAssemblies { get; }

        private static IReadOnlyList<Assembly> CopyAssemblies(
            IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var copied = new List<Assembly>();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly == null)
                {
                    throw new ArgumentException(
                        "A loaded-mod assembly reference cannot be null.",
                        nameof(assemblies));
                }

                copied.Add(assembly);
            }

            return new ReadOnlyCollection<Assembly>(copied);
        }
    }

    /// <summary>
    /// Exact result of matching one declaration against authoritative copied
    /// loaded-mod entries. Runtime object references remain preparation-scoped.
    /// </summary>
    internal sealed class DeclaredLoadedModMatch
    {
        private DeclaredLoadedModMatch(
            DeclaredModMatchState matchState,
            LoadedModCandidate? matchedCandidate,
            Assembly? matchedAssembly,
            string? diagnosticCode,
            string? diagnosticMessage)
        {
            MatchState = matchState;
            MatchedCandidate = matchedCandidate;
            MatchedAssembly = matchedAssembly;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
        }

        internal DeclaredModMatchState MatchState { get; }

        internal LoadedModCandidate? MatchedCandidate { get; }

        internal Assembly? MatchedAssembly { get; }

        internal string? DiagnosticCode { get; }

        internal string? DiagnosticMessage { get; }

        internal bool IsInspectable =>
            MatchState == DeclaredModMatchState.Matched &&
            MatchedCandidate != null &&
            MatchedAssembly != null;

        internal static DeclaredLoadedModMatch NotMatched() =>
            new DeclaredLoadedModMatch(
                DeclaredModMatchState.NotMatched,
                null,
                null,
                null,
                null);

        internal static DeclaredLoadedModMatch Ambiguous(
            string diagnosticMessage) =>
            new DeclaredLoadedModMatch(
                DeclaredModMatchState.Ambiguous,
                null,
                null,
                "declared-integration-identity-ambiguous",
                diagnosticMessage);

        internal static DeclaredLoadedModMatch MatchedWithoutAssembly(
            LoadedModCandidate matchedCandidate) =>
            new DeclaredLoadedModMatch(
                DeclaredModMatchState.Matched,
                matchedCandidate,
                null,
                "declared-integration-assembly-missing",
                "The exact active mod entry did not supply one assembly with " +
                "the declaration's accepted simple name.");

        internal static DeclaredLoadedModMatch InspectionUnavailable(
            LoadedModCandidate? matchedCandidate) =>
            new DeclaredLoadedModMatch(
                DeclaredModMatchState.InspectionUnavailable,
                matchedCandidate,
                null,
                "declared-integration-identity-inspection-unavailable",
                "The exact loaded-mod identity could not be inspected safely.");

        internal static DeclaredLoadedModMatch Matched(
            LoadedModCandidate matchedCandidate,
            Assembly matchedAssembly) =>
            new DeclaredLoadedModMatch(
                DeclaredModMatchState.Matched,
                matchedCandidate,
                matchedAssembly,
                null,
                null);
    }

    /// <summary>
    /// Short-lived authoritative topology facts used only during declared
    /// integration preparation. It is never retained in diagnostics.
    /// </summary>
    internal sealed class LoadedModInspectionContext
    {
        internal LoadedModInspectionContext(
            IEnumerable<LoadedModCandidate> loadedModCandidates,
            IEnumerable<ActiveHarmonyPrefixDescriptor> activeHarmonyPrefixes)
        {
            LoadedModCandidates = CopyCandidates(loadedModCandidates);
            ActiveHarmonyPrefixes = CopyPrefixes(activeHarmonyPrefixes);
        }

        internal IReadOnlyList<LoadedModCandidate> LoadedModCandidates { get; }

        internal IReadOnlyList<ActiveHarmonyPrefixDescriptor>
            ActiveHarmonyPrefixes { get; }

        internal DeclaredLoadedModMatch Match(
            DeclaredModIntegrationDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var matchingCandidates = new List<LoadedModCandidate>();
            for (int candidateIndex = 0;
                 candidateIndex < LoadedModCandidates.Count;
                 candidateIndex++)
            {
                LoadedModCandidate candidate =
                    LoadedModCandidates[candidateIndex];
                if (candidate.IsActive &&
                    ContainsExact(
                        descriptor.AcceptedStaticIds,
                        candidate.StaticId))
                {
                    matchingCandidates.Add(candidate);
                }
            }

            if (matchingCandidates.Count == 0)
            {
                return DeclaredLoadedModMatch.NotMatched();
            }

            if (matchingCandidates.Count != 1)
            {
                return DeclaredLoadedModMatch.Ambiguous(
                    "More than one active loaded-mod entry has an accepted exact " +
                    "static ID for this declaration.");
            }

            LoadedModCandidate matchedCandidate = matchingCandidates[0];
            var matchingAssemblies = new List<Assembly>();
            try
            {
                for (int assemblyIndex = 0;
                     assemblyIndex < matchedCandidate.LoadedAssemblies.Count;
                     assemblyIndex++)
                {
                    Assembly assembly =
                        matchedCandidate.LoadedAssemblies[assemblyIndex];
                    string? simpleName = assembly.GetName().Name;
                    if (simpleName != null &&
                        ContainsExact(
                            descriptor.AcceptedAssemblySimpleNames,
                            simpleName))
                    {
                        matchingAssemblies.Add(assembly);
                    }
                }
            }
            catch (Exception)
            {
                return DeclaredLoadedModMatch.InspectionUnavailable(
                    matchedCandidate);
            }

            if (matchingAssemblies.Count == 0)
            {
                return DeclaredLoadedModMatch.MatchedWithoutAssembly(
                    matchedCandidate);
            }

            if (matchingAssemblies.Count != 1)
            {
                return DeclaredLoadedModMatch.Ambiguous(
                    "The exact active loaded-mod entry supplied more than one " +
                    "assembly with an accepted simple name.");
            }

            return DeclaredLoadedModMatch.Matched(
                matchedCandidate,
                matchingAssemblies[0]);
        }

        private static bool ContainsExact(
            IReadOnlyList<string> acceptedValues,
            string candidate)
        {
            for (int index = 0; index < acceptedValues.Count; index++)
            {
                if (string.Equals(
                        acceptedValues[index],
                        candidate,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<LoadedModCandidate> CopyCandidates(
            IEnumerable<LoadedModCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var copied = new List<LoadedModCandidate>();
            foreach (LoadedModCandidate candidate in candidates)
            {
                if (candidate == null)
                {
                    throw new ArgumentException(
                        "A loaded-mod inspection candidate cannot be null.",
                        nameof(candidates));
                }

                copied.Add(candidate);
            }

            return new ReadOnlyCollection<LoadedModCandidate>(copied);
        }

        private static IReadOnlyList<ActiveHarmonyPrefixDescriptor> CopyPrefixes(
            IEnumerable<ActiveHarmonyPrefixDescriptor> prefixes)
        {
            if (prefixes == null)
            {
                throw new ArgumentNullException(nameof(prefixes));
            }

            var copied = new List<ActiveHarmonyPrefixDescriptor>();
            foreach (ActiveHarmonyPrefixDescriptor prefix in prefixes)
            {
                if (prefix == null)
                {
                    throw new ArgumentException(
                        "An active Harmony prefix descriptor cannot be null.",
                        nameof(prefixes));
                }

                copied.Add(prefix);
            }

            return new ReadOnlyCollection<ActiveHarmonyPrefixDescriptor>(copied);
        }
    }
}
