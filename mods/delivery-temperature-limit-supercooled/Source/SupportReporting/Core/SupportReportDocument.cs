#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeliveryTemperatureLimit
{
    internal sealed class SupportReportFact
    {
        private SupportReportFact(
            string state,
            string? value,
            string? reason)
        {
            State = state;
            Value = value;
            Reason = reason;
        }

        public string State { get; }

        public string? Value { get; }

        public string? Reason { get; }

        internal static SupportReportFact Available(string value) =>
            new SupportReportFact(
                SupportReportLimits.AvailableState,
                value ?? throw new ArgumentNullException(nameof(value)),
                null);

        internal static SupportReportFact Unavailable(string reason) =>
            new SupportReportFact(
                SupportReportLimits.UnavailableState,
                null,
                SupportReportCollections.RequireNonBlank(
                    reason,
                    nameof(reason)));
    }

    internal sealed class SupportReportDocument
    {
        private readonly SupportReportKind supportReportKind;

        internal SupportReportDocument(
            string reportId,
            DateTimeOffset generatedAtUtc,
            SupportReportKind reportKind,
            SupportReportGameSnapshot game,
            SupportReportTemperatureLimitSnapshot temperatureLimit,
            SupportRuntimeSnapshot runtime,
            IEnumerable<SupportActiveModSnapshot> activeMods,
            int omittedActiveModCount,
            IEnumerable<SupportDiagnosticSnapshot> diagnostics,
            int omittedDistinctDiagnosticCount,
            SupportPlayerLogSnapshot? playerLog,
            SupportGenerationSnapshot generation,
            SupportPrivacySnapshot privacy)
        {
            if (generatedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The report timestamp must use the UTC offset.",
                    nameof(generatedAtUtc));
            }

            if (!Enum.IsDefined(typeof(SupportReportKind), reportKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reportKind),
                    reportKind,
                    "Unknown support report kind.");
            }

            if (omittedActiveModCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(omittedActiveModCount));
            }

            if (omittedDistinctDiagnosticCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(omittedDistinctDiagnosticCount));
            }

            bool extended =
                reportKind == SupportReportKind.ExtendedPlayerLog;
            if (extended != (playerLog != null))
            {
                throw new ArgumentException(
                    extended
                        ? "An extended support report requires a player log snapshot."
                        : "A standard support report cannot contain a player log snapshot.",
                    nameof(playerLog));
            }

            SchemaVersion = SupportReportLimits.SchemaVersion;
            ReportId = SupportReportCollections.RequireNonBlank(
                reportId,
                nameof(reportId));
            GeneratedAtUtc = generatedAtUtc;
            supportReportKind = reportKind;
            ReportKind = extended ? "extended-player-log" : "standard";
            Game = game ?? throw new ArgumentNullException(nameof(game));
            TemperatureLimit = temperatureLimit ??
                throw new ArgumentNullException(nameof(temperatureLimit));
            Runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            ActiveMods = SupportReportCollections.CopyObjects(
                activeMods,
                nameof(activeMods));
            OmittedActiveModCount = omittedActiveModCount;
            Diagnostics = SupportReportCollections.CopyObjects(
                diagnostics,
                nameof(diagnostics));
            OmittedDistinctDiagnosticCount =
                omittedDistinctDiagnosticCount;
            PlayerLog = playerLog;
            Generation = generation ??
                throw new ArgumentNullException(nameof(generation));
            Privacy = privacy ??
                throw new ArgumentNullException(nameof(privacy));
        }

        public int SchemaVersion { get; }

        public string ReportId { get; }

        public DateTimeOffset GeneratedAtUtc { get; }

        public string ReportKind { get; }

        public SupportReportGameSnapshot Game { get; }

        public SupportReportTemperatureLimitSnapshot TemperatureLimit { get; }

        public SupportRuntimeSnapshot Runtime { get; }

        public IReadOnlyList<SupportActiveModSnapshot> ActiveMods { get; }

        public int OmittedActiveModCount { get; }

        public IReadOnlyList<SupportDiagnosticSnapshot> Diagnostics { get; }

        public int OmittedDistinctDiagnosticCount { get; }

        public SupportPlayerLogSnapshot? PlayerLog { get; }

        public SupportGenerationSnapshot Generation { get; }

        public SupportPrivacySnapshot Privacy { get; }

        internal SupportReportDocument WithIssueSummaryWasShortened() =>
            new SupportReportDocument(
                ReportId,
                GeneratedAtUtc,
                supportReportKind,
                Game,
                TemperatureLimit,
                Runtime,
                ActiveMods,
                OmittedActiveModCount,
                Diagnostics,
                OmittedDistinctDiagnosticCount,
                PlayerLog,
                new SupportGenerationSnapshot(
                    Generation.IncludedFacts,
                    Generation.UnavailableFacts,
                    Generation.Warnings,
                    issueSummaryWasShortened: true),
                Privacy);

        internal SupportReportDocument WithFurtherShortenedPlayerLog(
            string content,
            string warning)
        {
            if (PlayerLog == null ||
                !string.Equals(
                    PlayerLog.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal) ||
                PlayerLog.Content == null)
            {
                throw new InvalidOperationException(
                    "Only an available Player.log can be shortened.");
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            string validatedWarning =
                SupportReportCollections.RequireNonBlank(
                    warning,
                    nameof(warning));
            var warnings = new List<string>(Generation.Warnings);
            if (!warnings.Contains(validatedWarning))
            {
                warnings.Add(validatedWarning);
            }

            SupportPlayerLogSnapshot shortenedPlayerLog =
                SupportPlayerLogSnapshot.Available(
                    PlayerLog.SourceState,
                    PlayerLog.OriginalByteCount,
                    PlayerLog.IncludedRawByteCount,
                    truncated: true,
                    PlayerLog.RedactedPlaceholders,
                    content);

            return new SupportReportDocument(
                ReportId,
                GeneratedAtUtc,
                supportReportKind,
                Game,
                TemperatureLimit,
                Runtime,
                ActiveMods,
                OmittedActiveModCount,
                Diagnostics,
                OmittedDistinctDiagnosticCount,
                shortenedPlayerLog,
                new SupportGenerationSnapshot(
                    Generation.IncludedFacts,
                    Generation.UnavailableFacts,
                    warnings,
                    Generation.IssueSummaryWasShortened),
                Privacy);
        }
    }

    internal sealed class SupportReportGameSnapshot
    {
        internal SupportReportGameSnapshot(
            SupportReportFact build,
            SupportReportFact branch,
            SupportReportFact gameVersion,
            SupportReportFact unityVersion,
            SupportReportFact platform,
            SupportReportFact architecture,
            SupportReportFact locale,
            SupportActiveDlcSnapshot activeDlcs)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Branch = branch ?? throw new ArgumentNullException(nameof(branch));
            GameVersion = gameVersion ??
                throw new ArgumentNullException(nameof(gameVersion));
            UnityVersion = unityVersion ??
                throw new ArgumentNullException(nameof(unityVersion));
            Platform = platform ??
                throw new ArgumentNullException(nameof(platform));
            Architecture = architecture ??
                throw new ArgumentNullException(nameof(architecture));
            Locale = locale ?? throw new ArgumentNullException(nameof(locale));
            ActiveDlcs = activeDlcs ??
                throw new ArgumentNullException(nameof(activeDlcs));
        }

        public SupportReportFact Build { get; }

        public SupportReportFact Branch { get; }

        public SupportReportFact GameVersion { get; }

        public SupportReportFact UnityVersion { get; }

        public SupportReportFact Platform { get; }

        public SupportReportFact Architecture { get; }

        public SupportReportFact Locale { get; }

        public SupportActiveDlcSnapshot ActiveDlcs { get; }
    }

    internal sealed class SupportReportTemperatureLimitSnapshot
    {
        internal SupportReportTemperatureLimitSnapshot(
            SupportReportFact staticId,
            SupportReportFact title,
            SupportReportFact packageVersion,
            SupportReportFact assemblyVersion,
            bool checkTemperatureForStatusItems,
            bool underConstructionLimit,
            string temperatureUnit,
            int maxConstructionTemperature,
            int minConstructionTemperature)
        {
            StaticId = staticId ??
                throw new ArgumentNullException(nameof(staticId));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            PackageVersion = packageVersion ??
                throw new ArgumentNullException(nameof(packageVersion));
            AssemblyVersion = assemblyVersion ??
                throw new ArgumentNullException(nameof(assemblyVersion));
            CheckTemperatureForStatusItems = checkTemperatureForStatusItems;
            UnderConstructionLimit = underConstructionLimit;
            TemperatureUnit = SupportReportCollections.RequireNonBlank(
                temperatureUnit,
                nameof(temperatureUnit));
            MaxConstructionTemperature = maxConstructionTemperature;
            MinConstructionTemperature = minConstructionTemperature;
        }

        public SupportReportFact StaticId { get; }

        public SupportReportFact Title { get; }

        public SupportReportFact PackageVersion { get; }

        public SupportReportFact AssemblyVersion { get; }

        public bool CheckTemperatureForStatusItems { get; }

        public bool UnderConstructionLimit { get; }

        public string TemperatureUnit { get; }

        public int MaxConstructionTemperature { get; }

        public int MinConstructionTemperature { get; }
    }

    internal sealed class SupportRuntimeSnapshot
    {
        private SupportRuntimeSnapshot(
            string state,
            string installationState,
            string? unavailableReason,
            IEnumerable<string> selectedPatchGroups,
            string? statusCompatibilityDiagnostic,
            SupportFastTrackSnapshot? fastTrack)
        {
            State = state;
            InstallationState = SupportReportCollections.RequireNonBlank(
                installationState,
                nameof(installationState));
            UnavailableReason = unavailableReason;
            SelectedPatchGroups = SupportReportCollections.CopyStrings(
                selectedPatchGroups,
                nameof(selectedPatchGroups));
            StatusCompatibilityDiagnostic =
                statusCompatibilityDiagnostic;
            FastTrack = fastTrack;
        }

        public string State { get; }

        public string InstallationState { get; }

        public string? UnavailableReason { get; }

        public IReadOnlyList<string> SelectedPatchGroups { get; }

        public string? StatusCompatibilityDiagnostic { get; }

        public SupportFastTrackSnapshot? FastTrack { get; }

        internal static SupportRuntimeSnapshot Available(
            string installationState,
            IEnumerable<string> selectedPatchGroups,
            string? statusCompatibilityDiagnostic,
            SupportFastTrackSnapshot fastTrack) =>
            new SupportRuntimeSnapshot(
                SupportReportLimits.AvailableState,
                installationState,
                null,
                selectedPatchGroups,
                statusCompatibilityDiagnostic,
                fastTrack ?? throw new ArgumentNullException(nameof(fastTrack)));

        internal static SupportRuntimeSnapshot Unavailable(
            string installationState,
            string reason) =>
            new SupportRuntimeSnapshot(
                SupportReportLimits.UnavailableState,
                installationState,
                SupportReportCollections.RequireNonBlank(
                    reason,
                    nameof(reason)),
                Array.Empty<string>(),
                null,
                null);
    }

    internal sealed class SupportFastTrackSnapshot
    {
        internal SupportFastTrackSnapshot(
            string state,
            SupportReportFact assemblyIdentity,
            SupportReportFact assemblyVersion,
            SupportReportFact fileVersion,
            SupportReportFact assemblySha256,
            IEnumerable<SupportFastTrackFeatureSnapshot> features)
        {
            State = SupportReportCollections.RequireNonBlank(
                state,
                nameof(state));
            AssemblyIdentity = assemblyIdentity ??
                throw new ArgumentNullException(nameof(assemblyIdentity));
            AssemblyVersion = assemblyVersion ??
                throw new ArgumentNullException(nameof(assemblyVersion));
            FileVersion = fileVersion ??
                throw new ArgumentNullException(nameof(fileVersion));
            AssemblySha256 = assemblySha256 ??
                throw new ArgumentNullException(nameof(assemblySha256));
            Features = SupportReportCollections.CopyObjects(
                features,
                nameof(features));
        }

        public string State { get; }

        public SupportReportFact AssemblyIdentity { get; }

        public SupportReportFact AssemblyVersion { get; }

        public SupportReportFact FileVersion { get; }

        public SupportReportFact AssemblySha256 { get; }

        public IReadOnlyList<SupportFastTrackFeatureSnapshot> Features { get; }
    }

    internal sealed class SupportFastTrackFeatureSnapshot
    {
        internal SupportFastTrackFeatureSnapshot(
            string feature,
            string state,
            string? failureCode,
            string? failureMessage)
        {
            Feature = SupportReportCollections.RequireNonBlank(
                feature,
                nameof(feature));
            State = SupportReportCollections.RequireNonBlank(
                state,
                nameof(state));
            FailureCode = failureCode;
            FailureMessage = failureMessage;
        }

        public string Feature { get; }

        public string State { get; }

        public string? FailureCode { get; }

        public string? FailureMessage { get; }
    }

    internal sealed class SupportActiveModSnapshot
    {
        internal SupportActiveModSnapshot(
            int loadOrderIndex,
            string title,
            SupportReportFact staticId,
            SupportReportFact declaredVersion,
            IEnumerable<string> loadedAssemblies,
            string sourceKind)
        {
            if (loadOrderIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(loadOrderIndex));
            }

            LoadOrderIndex = loadOrderIndex;
            Title = SupportReportCollections.RequireNonBlank(
                title,
                nameof(title));
            StaticId = staticId ??
                throw new ArgumentNullException(nameof(staticId));
            DeclaredVersion = declaredVersion ??
                throw new ArgumentNullException(nameof(declaredVersion));
            LoadedAssemblies = SupportReportCollections.CopyStrings(
                loadedAssemblies,
                nameof(loadedAssemblies));
            SourceKind = SupportReportCollections.RequireNonBlank(
                sourceKind,
                nameof(sourceKind));
        }

        public int LoadOrderIndex { get; }

        public string Title { get; }

        public SupportReportFact StaticId { get; }

        public SupportReportFact DeclaredVersion { get; }

        public IReadOnlyList<string> LoadedAssemblies { get; }

        public string SourceKind { get; }
    }

    internal sealed class SupportDiagnosticSnapshot
    {
        internal SupportDiagnosticSnapshot(
            string code,
            string severity,
            DateTimeOffset firstOccurredAtUtc,
            DateTimeOffset lastOccurredAtUtc,
            int repeatCount,
            string message,
            string? exceptionType,
            string? exceptionMessage)
        {
            if (firstOccurredAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The first diagnostic timestamp must use the UTC offset.",
                    nameof(firstOccurredAtUtc));
            }

            if (lastOccurredAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The last diagnostic timestamp must use the UTC offset.",
                    nameof(lastOccurredAtUtc));
            }

            if (lastOccurredAtUtc < firstOccurredAtUtc)
            {
                throw new ArgumentException(
                    "The last diagnostic timestamp cannot precede the first.",
                    nameof(lastOccurredAtUtc));
            }

            if (repeatCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repeatCount));
            }

            Code = SupportReportCollections.RequireNonBlank(
                code,
                nameof(code));
            Severity = SupportReportCollections.RequireNonBlank(
                severity,
                nameof(severity));
            FirstOccurredAtUtc = firstOccurredAtUtc;
            LastOccurredAtUtc = lastOccurredAtUtc;
            RepeatCount = repeatCount;
            Message = message ??
                throw new ArgumentNullException(nameof(message));
            ExceptionType = exceptionType;
            ExceptionMessage = exceptionMessage;
        }

        public string Code { get; }

        public string Severity { get; }

        public DateTimeOffset FirstOccurredAtUtc { get; }

        public DateTimeOffset LastOccurredAtUtc { get; }

        public int RepeatCount { get; }

        public string Message { get; }

        public string? ExceptionType { get; }

        public string? ExceptionMessage { get; }
    }

    internal sealed class SupportPlayerLogSnapshot
    {
        private SupportPlayerLogSnapshot(
            string state,
            string sourceState,
            string? unavailableReason,
            long originalByteCount,
            int includedRawByteCount,
            bool truncated,
            IEnumerable<string> redactedPlaceholders,
            string? content)
        {
            State = state;
            SourceState = SupportReportCollections.RequireNonBlank(
                sourceState,
                nameof(sourceState));
            UnavailableReason = unavailableReason;
            OriginalByteCount = originalByteCount;
            IncludedRawByteCount = includedRawByteCount;
            Truncated = truncated;
            RedactedPlaceholders = SupportReportCollections.CopyStrings(
                redactedPlaceholders,
                nameof(redactedPlaceholders));
            Content = content;
        }

        public string State { get; }

        public string SourceState { get; }

        public string? UnavailableReason { get; }

        public long OriginalByteCount { get; }

        public int IncludedRawByteCount { get; }

        public bool Truncated { get; }

        public IReadOnlyList<string> RedactedPlaceholders { get; }

        public string? Content { get; }

        internal static SupportPlayerLogSnapshot Available(
            string sourceState,
            long originalByteCount,
            int includedRawByteCount,
            bool truncated,
            IEnumerable<string> redactedPlaceholders,
            string content)
        {
            if (originalByteCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(originalByteCount));
            }

            if (includedRawByteCount < 0 ||
                includedRawByteCount > originalByteCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(includedRawByteCount));
            }

            return new SupportPlayerLogSnapshot(
                SupportReportLimits.AvailableState,
                sourceState,
                null,
                originalByteCount,
                includedRawByteCount,
                truncated,
                redactedPlaceholders,
                content ?? throw new ArgumentNullException(nameof(content)));
        }

        internal static SupportPlayerLogSnapshot Unavailable(
            string sourceState,
            string reason) =>
            new SupportPlayerLogSnapshot(
                SupportReportLimits.UnavailableState,
                sourceState,
                SupportReportCollections.RequireNonBlank(
                    reason,
                    nameof(reason)),
                0,
                0,
                false,
                Array.Empty<string>(),
                null);
    }

    internal sealed class SupportGenerationSnapshot
    {
        internal SupportGenerationSnapshot(
            IEnumerable<string> includedFacts,
            IEnumerable<string> unavailableFacts,
            IEnumerable<string> warnings,
            bool issueSummaryWasShortened)
        {
            IncludedFacts = SupportReportCollections.CopyStrings(
                includedFacts,
                nameof(includedFacts));
            UnavailableFacts = SupportReportCollections.CopyStrings(
                unavailableFacts,
                nameof(unavailableFacts));
            Warnings = SupportReportCollections.CopyStrings(
                warnings,
                nameof(warnings));
            IssueSummaryWasShortened = issueSummaryWasShortened;
        }

        public IReadOnlyList<string> IncludedFacts { get; }

        public IReadOnlyList<string> UnavailableFacts { get; }

        public IReadOnlyList<string> Warnings { get; }

        public bool IssueSummaryWasShortened { get; }
    }

    internal sealed class SupportPrivacySnapshot
    {
        internal SupportPrivacySnapshot(
            IEnumerable<string> included,
            IEnumerable<string> excluded,
            IEnumerable<string> redacted,
            IEnumerable<string> potentiallySensitive)
        {
            Included = SupportReportCollections.CopyStrings(
                included,
                nameof(included));
            Excluded = SupportReportCollections.CopyStrings(
                excluded,
                nameof(excluded));
            Redacted = SupportReportCollections.CopyStrings(
                redacted,
                nameof(redacted));
            PotentiallySensitive = SupportReportCollections.CopyStrings(
                potentiallySensitive,
                nameof(potentiallySensitive));
        }

        public IReadOnlyList<string> Included { get; }

        public IReadOnlyList<string> Excluded { get; }

        public IReadOnlyList<string> Redacted { get; }

        public IReadOnlyList<string> PotentiallySensitive { get; }
    }

    internal static class SupportReportCollections
    {
        internal static IReadOnlyList<T> CopyObjects<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (T item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "A report collection cannot contain null values.",
                        parameterName);
                }

                copy.Add(item);
            }

            return new ReadOnlyCollection<T>(copy);
        }

        internal static IReadOnlyList<string> CopyStrings(
            IEnumerable<string> source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<string>();
            foreach (string item in source)
            {
                copy.Add(RequireNonBlank(item, parameterName));
            }

            return new ReadOnlyCollection<string>(copy);
        }

        internal static string RequireNonBlank(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A report value cannot be blank.",
                    parameterName);
            }

            return value;
        }
    }
}
