#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    internal static class SupportReportJsonFileWriter
    {
        private static readonly JsonSerializerSettings SerializationSettings =
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

        internal static string Write(
            SupportReportDocument document,
            string reportFileName)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string validatedFileName = ValidateFileName(reportFileName);
            string reportDirectory = Path.GetFullPath(Path.Combine(
                Application.persistentDataPath,
                "DeliveryTemperatureLimit",
                "support-reports"));
            Directory.CreateDirectory(reportDirectory);
            string finalPath = Path.Combine(
                reportDirectory,
                validatedFileName);
            if (File.Exists(finalPath))
            {
                throw new IOException(
                    "A support report with the generated identity already exists.");
            }

            string temporaryPath = Path.Combine(
                reportDirectory,
                validatedFileName + ".tmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                SupportJsonReportSerialization serialization =
                    SupportJsonReportSizeLimiter.SerializeWithinLimit(
                        document,
                        SupportReportLimits.MaximumReportBytes,
                        SerializeDocument);
                string json = serialization.Json;
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(false),
                    bufferSize: 64 * 1024,
                    leaveOpen: true))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                long reportByteCount = new FileInfo(temporaryPath).Length;
                if (reportByteCount >= SupportReportLimits.MaximumReportBytes)
                {
                    throw new InvalidOperationException(
                        "The generated support report reached the 12 MiB safety limit.");
                }

                File.Move(temporaryPath, finalPath);
                return finalPath;
            }
            catch
            {
                DeleteOwnedTemporaryFile(temporaryPath, reportDirectory);
                throw;
            }
        }

        private static string SerializeDocument(
            SupportReportDocument document) =>
            JsonConvert.SerializeObject(
                document,
                Formatting.Indented,
                SerializationSettings);

        private static string ValidateFileName(string reportFileName)
        {
            string validated = SupportReportCollections.RequireNonBlank(
                reportFileName,
                nameof(reportFileName));
            if (Path.IsPathRooted(validated) ||
                !string.Equals(
                    Path.GetFileName(validated),
                    validated,
                    StringComparison.Ordinal) ||
                !validated.EndsWith(".json", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The support report filename must be one local JSON filename.",
                    nameof(reportFileName));
            }

            return validated;
        }

        private static void DeleteOwnedTemporaryFile(
            string temporaryPath,
            string reportDirectory)
        {
            try
            {
                string? parent = Path.GetDirectoryName(
                    Path.GetFullPath(temporaryPath));
                if (parent == null ||
                    !string.Equals(
                        parent,
                        Path.GetFullPath(reportDirectory),
                        Path.DirectorySeparatorChar == '\\'
                            ? StringComparison.OrdinalIgnoreCase
                            : StringComparison.Ordinal) ||
                    !Path.GetFileName(temporaryPath).Contains(
                        ".tmp-",
                        StringComparison.Ordinal) ||
                    !File.Exists(temporaryPath))
                {
                    return;
                }

                File.Delete(temporaryPath);
            }
            catch (Exception)
            {
                // The original generation failure is more useful to the player.
            }
        }
    }
}
