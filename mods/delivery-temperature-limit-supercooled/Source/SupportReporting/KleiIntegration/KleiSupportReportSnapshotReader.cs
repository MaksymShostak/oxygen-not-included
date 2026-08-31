#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    internal sealed class KleiCurrentModSupportSnapshot
    {
        internal KleiCurrentModSupportSnapshot(
            SupportReportFact staticId,
            SupportReportFact title,
            SupportReportFact packageVersion,
            SupportReportFact assemblyVersion,
            IEnumerable<string> warnings)
        {
            StaticId = staticId;
            Title = title;
            PackageVersion = packageVersion;
            AssemblyVersion = assemblyVersion;
            Warnings = SupportReportCollections.CopyStrings(
                warnings,
                nameof(warnings));
        }

        internal SupportReportFact StaticId { get; }

        internal SupportReportFact Title { get; }

        internal SupportReportFact PackageVersion { get; }

        internal SupportReportFact AssemblyVersion { get; }

        internal IReadOnlyList<string> Warnings { get; }
    }

    internal sealed class KleiLoadedModsSupportSnapshot
    {
        internal KleiLoadedModsSupportSnapshot(
            IEnumerable<SupportActiveModSnapshot> activeMods,
            int omittedActiveModCount,
            bool wasPublished,
            IEnumerable<string> warnings)
        {
            ActiveMods = SupportReportCollections.CopyObjects(
                activeMods,
                nameof(activeMods));
            if (omittedActiveModCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(omittedActiveModCount));
            }

            OmittedActiveModCount = omittedActiveModCount;
            WasPublished = wasPublished;
            Warnings = SupportReportCollections.CopyStrings(
                warnings,
                nameof(warnings));
        }

        internal IReadOnlyList<SupportActiveModSnapshot> ActiveMods { get; }

        internal int OmittedActiveModCount { get; }

        internal bool WasPublished { get; }

        internal IReadOnlyList<string> Warnings { get; }

        internal static KleiLoadedModsSupportSnapshot Unpublished() =>
            new KleiLoadedModsSupportSnapshot(
                Array.Empty<SupportActiveModSnapshot>(),
                0,
                wasPublished: false,
                new[]
                {
                    "The active loaded-mod list was not published before report generation."
                });
    }

    internal static class KleiSupportReportSnapshotReader
    {
        internal static KleiCurrentModSupportSnapshot CaptureCurrentMod(
            KMod.Mod currentMod,
            Assembly assembly)
        {
            if (currentMod == null)
            {
                throw new ArgumentNullException(nameof(currentMod));
            }

            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var warnings = new List<string>();
            SupportReportFact staticId = CaptureFact(
                () => currentMod.staticID,
                "Temperature Limit static ID",
                warnings);
            SupportReportFact title = CaptureFact(
                () => currentMod.title,
                "Temperature Limit title",
                warnings);
            SupportReportFact packageVersion = CaptureFact(
                () => currentMod.packagedModInfo?.version,
                "Temperature Limit packaged version",
                warnings);
            SupportReportFact assemblyVersion = CaptureFact(
                () => assembly.GetName().Version?.ToString(),
                "Temperature Limit assembly version",
                warnings);
            return new KleiCurrentModSupportSnapshot(
                staticId,
                title,
                packageVersion,
                assemblyVersion,
                warnings);
        }

        internal static KleiLoadedModsSupportSnapshot CaptureLoadedMods(
            IReadOnlyList<KMod.Mod> loadedMods)
        {
            if (loadedMods == null)
            {
                throw new ArgumentNullException(nameof(loadedMods));
            }

            var activeMods = new List<SupportActiveModSnapshot>();
            var warnings = new List<string>();
            int omittedActiveModCount = 0;
            for (int loadOrderIndex = 0;
                 loadOrderIndex < loadedMods.Count;
                 loadOrderIndex++)
            {
                KMod.Mod? loadedMod = loadedMods[loadOrderIndex];
                if (loadedMod == null)
                {
                    warnings.Add(
                        "One loaded-mod entry was null and could not be captured.");
                    continue;
                }

                bool isActive;
                try
                {
                    isActive = loadedMod.IsActive();
                }
                catch (Exception)
                {
                    warnings.Add(
                        "One loaded mod did not expose a readable active state.");
                    continue;
                }

                if (!isActive)
                {
                    continue;
                }

                if (activeMods.Count >=
                    SupportReportLimits.MaximumActiveMods)
                {
                    omittedActiveModCount++;
                    continue;
                }

                try
                {
                    activeMods.Add(CreateActiveModSnapshot(
                        loadedMod,
                        loadOrderIndex));
                }
                catch (Exception)
                {
                    omittedActiveModCount++;
                    warnings.Add(
                        "One active mod could not be sanitized and was omitted.");
                }
            }

            return new KleiLoadedModsSupportSnapshot(
                activeMods,
                omittedActiveModCount,
                wasPublished: true,
                warnings);
        }

        internal static SupportReportDocument CreateDocument(
            SupportReportKind reportKind,
            Guid reportId,
            DateTimeOffset generatedAtUtc,
            KleiCurrentModSupportSnapshot currentMod,
            KleiLoadedModsSupportSnapshot loadedMods,
            SupportRuntimeSnapshot runtime,
            IReadOnlyList<SupportDiagnosticSnapshot> diagnostics,
            int omittedDistinctDiagnosticCount)
        {
            if (reportId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A support report requires a non-empty report ID.",
                    nameof(reportId));
            }

            if (currentMod == null)
            {
                throw new ArgumentNullException(nameof(currentMod));
            }

            if (loadedMods == null)
            {
                throw new ArgumentNullException(nameof(loadedMods));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var warnings = new List<string>();
            Append(warnings, currentMod.Warnings);
            Append(warnings, loadedMods.Warnings);
            SupportReportGameSnapshot game = CaptureGameSnapshot(warnings);
            SupportPathRedactor pathRedactor = CreateKnownPathRedactor(
                warnings);
            IReadOnlyList<string> diagnosticRedactions;
            IReadOnlyList<SupportDiagnosticSnapshot> sanitizedDiagnostics =
                SanitizeDiagnostics(
                    diagnostics,
                    pathRedactor,
                    out diagnosticRedactions);
            SupportPlayerLogSnapshot? playerLog = CreatePlayerLogSnapshot(
                reportKind,
                pathRedactor,
                warnings);
            DeliveryTemperatureLimitOptions options =
                DeliveryTemperatureLimitOptions.Instance;
            var temperatureLimit =
                new SupportReportTemperatureLimitSnapshot(
                    currentMod.StaticId,
                    currentMod.Title,
                    currentMod.PackageVersion,
                    currentMod.AssemblyVersion,
                    options.CheckTemperatureForStatusItems,
                    options.UnderConstructionLimit,
                    GameUtil.temperatureUnit.ToString(),
                    options.MaxConstructionTemperature,
                    options.MinConstructionTemperature);
            SupportGenerationSnapshot generation = CreateGenerationSnapshot(
                game,
                runtime,
                loadedMods,
                playerLog,
                warnings);
            SupportPrivacySnapshot privacy = CreatePrivacySnapshot(
                playerLog,
                diagnosticRedactions);

            return new SupportReportDocument(
                reportId.ToString("N"),
                generatedAtUtc,
                reportKind,
                game,
                temperatureLimit,
                runtime,
                loadedMods.ActiveMods,
                loadedMods.OmittedActiveModCount,
                sanitizedDiagnostics,
                omittedDistinctDiagnosticCount,
                playerLog,
                generation,
                privacy);
        }

        private static SupportActiveModSnapshot CreateActiveModSnapshot(
            KMod.Mod loadedMod,
            int loadOrderIndex)
        {
            string title = string.IsNullOrWhiteSpace(loadedMod.title)
                ? "<untitled active mod>"
                : loadedMod.title;
            SupportReportFact staticId = string.IsNullOrWhiteSpace(
                    loadedMod.staticID)
                ? SupportReportFact.Unavailable(
                    "The active mod did not publish a static ID.")
                : SupportReportFact.Available(loadedMod.staticID);
            string? packagedVersion = loadedMod.packagedModInfo?.version;
            SupportReportFact declaredVersion = string.IsNullOrWhiteSpace(
                    packagedVersion)
                ? SupportReportFact.Unavailable(
                    "The active mod did not publish a declared version.")
                : SupportReportFact.Available(packagedVersion);
            var loadedAssemblies = new List<string>();
            if (loadedMod.loaded_mod_data?.dlls != null)
            {
                foreach (Assembly loadedAssembly in
                         loadedMod.loaded_mod_data.dlls)
                {
                    if (loadedAssembly == null)
                    {
                        continue;
                    }

                    AssemblyName name = loadedAssembly.GetName();
                    string simpleName = string.IsNullOrWhiteSpace(name.Name)
                        ? "<unnamed assembly>"
                        : name.Name;
                    loadedAssemblies.Add(
                        simpleName + " " +
                        (name.Version?.ToString() ?? "version-unavailable"));
                }
            }

            loadedAssemblies.Sort(StringComparer.Ordinal);
            string sourceKind = loadedMod.IsDev
                ? "dev"
                : loadedMod.IsLocal
                    ? "local"
                    : "platform";
            return new SupportActiveModSnapshot(
                loadOrderIndex,
                title,
                staticId,
                declaredVersion,
                loadedAssemblies,
                sourceKind);
        }

        private static SupportReportGameSnapshot CaptureGameSnapshot(
            ICollection<string> warnings)
        {
            SupportReportFact build = CaptureFact(
                () => KleiVersion.ChangeList.ToString(
                    CultureInfo.InvariantCulture),
                "ONI build",
                warnings);
            SupportReportFact branch = CaptureFact(
                () => KleiVersion.BuildBranch,
                "ONI branch",
                warnings);
            SupportReportFact gameVersion = CaptureFact(
                () => Application.version,
                "ONI application version",
                warnings);
            SupportReportFact unityVersion = CaptureFact(
                () => Application.unityVersion,
                "Unity version",
                warnings);
            SupportReportFact platform = CaptureFact(
                () => Application.platform.ToString(),
                "runtime platform",
                warnings);
            SupportReportFact architecture = CaptureFact(
                () => Environment.Is64BitProcess ? "x64" : "x86",
                "process architecture",
                warnings);
            SupportReportFact locale = CaptureFact(
                () => CultureInfo.CurrentCulture.Name,
                "process locale",
                warnings);
            SupportActiveDlcSnapshot activeDlcs =
                SupportActiveDlcCapture.Capture(
                    () => DlcManager.GetActiveDLCIds(),
                    warnings);
            return new SupportReportGameSnapshot(
                build,
                branch,
                gameVersion,
                unityVersion,
                platform,
                architecture,
                locale,
                activeDlcs);
        }

        private static SupportReportFact CaptureFact(
            Func<string?> reader,
            string factName,
            ICollection<string> warnings)
        {
            try
            {
                string? value = reader();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return SupportReportFact.Available(value);
                }
            }
            catch (Exception)
            {
            }

            string reason = factName + " was unavailable.";
            warnings.Add(reason);
            return SupportReportFact.Unavailable(reason);
        }

        private static SupportPathRedactor CreateKnownPathRedactor(
            ICollection<string> warnings)
        {
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var rules = new List<SupportPathRedactionRule>();
            try
            {
                AddPathRule(
                    rules,
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    "<USER_PROFILE>",
                    comparison);
            }
            catch (Exception)
            {
                warnings.Add(
                    "The user-profile prefix was unavailable for path redaction.");
            }

            try
            {
                AddPathRule(
                    rules,
                    Application.persistentDataPath,
                    "<ONI_DATA>",
                    comparison);
            }
            catch (Exception)
            {
                warnings.Add(
                    "The ONI data prefix was unavailable for path redaction.");
            }

            try
            {
                AddPathRule(
                    rules,
                    Path.GetDirectoryName(Application.dataPath),
                    "<ONI_INSTALLATION>",
                    comparison);
            }
            catch (Exception)
            {
                warnings.Add(
                    "The ONI installation prefix was unavailable for path redaction.");
            }

            return new SupportPathRedactor(rules, comparison);
        }

        private static void AddPathRule(
            ICollection<SupportPathRedactionRule> rules,
            string? pathPrefix,
            string placeholder,
            StringComparison comparison)
        {
            if (string.IsNullOrWhiteSpace(pathPrefix))
            {
                return;
            }

            foreach (SupportPathRedactionRule existing in rules)
            {
                if (string.Equals(
                        existing.PathPrefix,
                        pathPrefix,
                        comparison))
                {
                    return;
                }
            }

            rules.Add(new SupportPathRedactionRule(
                pathPrefix,
                placeholder));
        }

        private static IReadOnlyList<SupportDiagnosticSnapshot>
            SanitizeDiagnostics(
                IReadOnlyList<SupportDiagnosticSnapshot> diagnostics,
                SupportPathRedactor redactor,
                out IReadOnlyList<string> appliedPlaceholders)
        {
            var sanitized = new List<SupportDiagnosticSnapshot>(
                diagnostics.Count);
            var usedPlaceholders = new List<string>();
            for (int index = 0; index < diagnostics.Count; index++)
            {
                SupportDiagnosticSnapshot diagnostic = diagnostics[index];
                RedactedSupportText message = redactor.Redact(
                    diagnostic.Message);
                RedactedSupportText? exceptionMessage =
                    diagnostic.ExceptionMessage == null
                        ? null
                        : redactor.Redact(diagnostic.ExceptionMessage);
                AppendDistinct(
                    usedPlaceholders,
                    message.AppliedPlaceholders);
                if (exceptionMessage != null)
                {
                    AppendDistinct(
                        usedPlaceholders,
                        exceptionMessage.AppliedPlaceholders);
                }

                sanitized.Add(new SupportDiagnosticSnapshot(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.FirstOccurredAtUtc,
                    diagnostic.LastOccurredAtUtc,
                    diagnostic.RepeatCount,
                    message.Content,
                    diagnostic.ExceptionType,
                    exceptionMessage?.Content));
            }

            appliedPlaceholders =
                new ReadOnlyCollection<string>(usedPlaceholders);
            return new ReadOnlyCollection<SupportDiagnosticSnapshot>(
                sanitized);
        }

        private static SupportPlayerLogSnapshot? CreatePlayerLogSnapshot(
            SupportReportKind reportKind,
            SupportPathRedactor redactor,
            ICollection<string> warnings)
        {
            if (reportKind != SupportReportKind.ExtendedPlayerLog)
            {
                return null;
            }

            try
            {
                string consoleLogPath = Application.consoleLogPath;
                if (string.IsNullOrWhiteSpace(consoleLogPath) ||
                    !File.Exists(consoleLogPath))
                {
                    warnings.Add(
                        "The current Player.log was not available at Unity's console-log location.");
                    return SupportPlayerLogSnapshot.Unavailable(
                        "unity-console-log-path",
                        "The current Player.log was unavailable.");
                }

                using (var stream = new FileStream(
                    consoleLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    return new SupportLogExcerptBuilder().Create(
                        stream,
                        "unity-console-log-path",
                        redactor);
                }
            }
            catch (Exception)
            {
                warnings.Add(
                    "The current Player.log could not be read; the extended report remains usable without it.");
                return SupportPlayerLogSnapshot.Unavailable(
                    "unity-console-log-path",
                    "The current Player.log could not be read.");
            }
        }

        private static SupportGenerationSnapshot CreateGenerationSnapshot(
            SupportReportGameSnapshot game,
            SupportRuntimeSnapshot runtime,
            KleiLoadedModsSupportSnapshot loadedMods,
            SupportPlayerLogSnapshot? playerLog,
            IEnumerable<string> warnings)
        {
            var included = new List<string>();
            var unavailable = new List<string>();
            AddFactState(included, unavailable, "game.build", game.Build);
            AddFactState(included, unavailable, "game.branch", game.Branch);
            AddFactState(
                included,
                unavailable,
                "game.version",
                game.GameVersion);
            AddFactState(
                included,
                unavailable,
                "game.unityVersion",
                game.UnityVersion);
            AddFactState(
                included,
                unavailable,
                "game.platform",
                game.Platform);
            AddFactState(
                included,
                unavailable,
                "game.architecture",
                game.Architecture);
            AddFactState(included, unavailable, "game.locale", game.Locale);
            if (string.Equals(
                    game.ActiveDlcs.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal))
            {
                included.Add("game.activeDlcs");
            }
            else
            {
                unavailable.Add("game.activeDlcs");
            }

            included.Add("temperatureLimit.identityAndSettings");
            if (string.Equals(
                    runtime.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal))
            {
                included.Add("runtime.patchPlanAndFastTrack");
            }
            else
            {
                unavailable.Add("runtime.patchPlanAndFastTrack");
            }

            if (loadedMods.WasPublished)
            {
                included.Add("activeMods");
            }
            else
            {
                unavailable.Add("activeMods");
            }

            included.Add("diagnostics");
            if (playerLog != null)
            {
                if (string.Equals(
                        playerLog.State,
                        SupportReportLimits.AvailableState,
                        StringComparison.Ordinal))
                {
                    included.Add("playerLog");
                }
                else
                {
                    unavailable.Add("playerLog");
                }
            }

            return new SupportGenerationSnapshot(
                included,
                unavailable,
                warnings,
                issueSummaryWasShortened: false);
        }

        private static SupportPrivacySnapshot CreatePrivacySnapshot(
            SupportPlayerLogSnapshot? playerLog,
            IReadOnlyList<string> diagnosticRedactions)
        {
            var included = new List<string>
            {
                "ONI, Unity, platform, DLC, and Temperature Limit versions",
                "Temperature Limit settings and verified runtime selection",
                "active mod identities, versions, source kinds, and load order",
                "bounded Temperature Limit diagnostics"
            };
            var redacted = new List<string>();
            AppendDistinct(redacted, diagnosticRedactions);
            var potentiallySensitive = new List<string>();
            if (playerLog != null &&
                string.Equals(
                    playerLog.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal))
            {
                included.Add("bounded recent Player.log text");
                AppendDistinct(redacted, playerLog.RedactedPlaceholders);
                potentiallySensitive.Add(
                    "Player.log can contain arbitrary text emitted by ONI and other mods; automatic redaction is best effort.");
            }

            return new SupportPrivacySnapshot(
                included,
                new[]
                {
                    "absolute paths as report fields",
                    "user and account names",
                    "Steam user IDs and network information",
                    "environment variables",
                    "save files, save metadata, screenshots, and crash dumps",
                    "other mods' configuration contents"
                },
                redacted,
                potentiallySensitive);
        }

        private static void AddFactState(
            ICollection<string> included,
            ICollection<string> unavailable,
            string factName,
            SupportReportFact fact)
        {
            if (string.Equals(
                    fact.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal))
            {
                included.Add(factName);
            }
            else
            {
                unavailable.Add(factName);
            }
        }

        private static void Append<T>(
            ICollection<T> destination,
            IEnumerable<T> source)
        {
            foreach (T item in source)
            {
                destination.Add(item);
            }
        }

        private static void AppendDistinct(
            ICollection<string> destination,
            IEnumerable<string> source)
        {
            foreach (string item in source)
            {
                bool present = false;
                foreach (string existing in destination)
                {
                    if (string.Equals(
                            existing,
                            item,
                            StringComparison.Ordinal))
                    {
                        present = true;
                        break;
                    }
                }

                if (!present)
                {
                    destination.Add(item);
                }
            }
        }
    }
}
