#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PeterHan.PLib.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DeliveryTemperatureLimit
{
    /// <summary>Snapshot reads and checked, same-directory atomic replacement.</summary>
    internal sealed class DeliveryTemperatureOptionsStore
    {
        private static readonly object WriteLock = new object();
        private const int MaximumFileBytes = 65536;
        internal string Path { get; }

        internal DeliveryTemperatureOptionsStore(string? path = null)
        {
            Path = path ?? POptions.GetConfigFilePath(typeof(DeliveryTemperatureLimitOptions));
        }

        internal sealed class Snapshot
        {
            internal Snapshot(byte[]? bytes, JObject document)
            {
                Bytes = bytes;
                Document = document;
            }
            internal byte[]? Bytes { get; }
            internal JObject Document { get; }
            internal bool IsLegacy => Bytes != null && Document["SchemaVersion"] == null;
            internal bool IsDefaultLegacy => IsLegacy &&
                Document["MinConstructionTemperature"]?.Type == JTokenType.Integer &&
                (int)Document["MinConstructionTemperature"]! == -50 &&
                Document["MaxConstructionTemperature"]?.Type == JTokenType.Integer &&
                (int)Document["MaxConstructionTemperature"]! == 45;
        }

        internal Snapshot Read()
        {
            byte[]? bytes = ReadBytes();
            if (bytes == null)
                return new Snapshot(null, JObject.FromObject(new DeliveryTemperatureLimitOptions()));
            using var stream = new MemoryStream(bytes, false);
            using var text = new StreamReader(stream, new UTF8Encoding(false, true), true);
            using var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
            JObject document = JObject.Load(reader);
            while (reader.Read())
                if (reader.TokenType != JsonToken.Comment)
                    throw new InvalidDataException("Unexpected data after the configuration object.");
            var snapshot = new Snapshot(bytes, document);
            // Validate the schema now. Legacy unit interpretation is deferred.
            if (snapshot.IsLegacy)
            {
                if (document["TemperatureUnit"] != null)
                    throw new InvalidDataException("TemperatureUnit requires SchemaVersion.");
            }
            else if (Integer(document, "SchemaVersion", 1) != 1 ||
                (string?)document["TemperatureUnit"] != "kelvin")
                throw new InvalidDataException("Unsupported configuration schema or unit.");
            return snapshot;
        }

        internal DeliveryTemperatureLimitOptions Values(Snapshot snapshot, string legacyUnit)
        {
            JObject doc = snapshot.Document;
            var result = new DeliveryTemperatureLimitOptions
            {
                CheckTemperatureForStatusItems = Boolean(doc, "CheckTemperatureForStatusItems", true),
                UnderConstructionLimit = Boolean(doc, "UnderConstructionLimit", false)
            };
            if (snapshot.IsDefaultLegacy)
            {
                result.MinConstructionTemperature = 223;
                result.MaxConstructionTemperature = 318;
                return result;
            }
            int lowDefault = snapshot.IsLegacy ?
                int.Parse(OptionsTemperatureUnits.Format(223, legacyUnit)) : 223;
            int highDefault = snapshot.IsLegacy ?
                int.Parse(OptionsTemperatureUnits.Format(318, legacyUnit)) : 318;
            int low = Integer(doc, "MinConstructionTemperature", lowDefault);
            int high = Integer(doc, "MaxConstructionTemperature", highDefault);
            result.MinConstructionTemperature = snapshot.IsLegacy ? ConvertLegacy(low, legacyUnit) : low;
            result.MaxConstructionTemperature = snapshot.IsLegacy ? ConvertLegacy(high, legacyUnit) : high;
            return result;
        }

        internal Snapshot Save(Snapshot expected, DeliveryTemperatureLimitOptions values)
        {
            if (values.SchemaVersion != 1 || values.TemperatureUnit != "kelvin")
                throw new InvalidDataException("Unsupported configuration schema or unit.");
            if (values.UnderConstructionLimit && (values.MinConstructionTemperature < 0 ||
                values.MaxConstructionTemperature > OniStorableTemperatureBounds.MaximumTemperatureKelvin ||
                values.MinConstructionTemperature >= values.MaxConstructionTemperature))
                throw new InvalidDataException("Construction defaults must be a nonempty supported range.");
            JObject doc = (JObject)expected.Document.DeepClone();
            // Preserve unknown keys; no type-name deserialization is used.
            foreach (JProperty property in JObject.FromObject(values).Properties())
                doc[property.Name] = property.Value.DeepClone();
            byte[] bytes = new UTF8Encoding(false).GetBytes(doc.ToString(Formatting.Indented) + "\n");
            if (bytes.Length > MaximumFileBytes)
                throw new InvalidDataException("Configuration exceeds 64 KiB.");
            string directory = System.IO.Path.GetDirectoryName(Path) ??
                throw new InvalidOperationException("Configuration path has no directory.");
            string temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            lock (WriteLock)
            {
                RequireUnchanged(expected);
                Directory.CreateDirectory(directory);
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    RequireUnchanged(expected);
                    if (expected.Bytes == null)
                        File.Move(temporary, Path);
                    else
                        File.Replace(temporary, Path, null);
                    return new Snapshot(bytes, doc);
                }
                finally
                {
                    // A cleanup failure must not turn a successful commit into a
                    // reported failure. Never delete the destination as a fallback.
                    try { File.Delete(temporary); }
                    catch (Exception ex) { Log("Temporary configuration cleanup failed.", ex); }
                }
            }
        }

        internal static DeliveryTemperatureLimitOptions LoadRuntimeSnapshot()
        {
            try
            {
                var store = new DeliveryTemperatureOptionsStore();
                Snapshot snapshot = store.Read();
                // An old file cannot reveal its original unit. Keep the current
                // interpretation for this process; confirmation is required on save.
                var values = store.Values(snapshot, OptionsTemperatureUnits.Current);
                if (snapshot.IsLegacy && !snapshot.IsDefaultLegacy && values.UnderConstructionLimit)
                    Log("Legacy temperature units are unconfirmed; open Options before migrating.", null);
                return values;
            }
            catch (Exception ex)
            {
                Log("Configuration could not be loaded; this process uses defaults. The file was not changed.", ex);
                return new DeliveryTemperatureLimitOptions();
            }
        }

        private void RequireUnchanged(Snapshot expected)
        {
            byte[]? actual = ReadBytes();
            if (expected.Bytes == null ? actual != null :
                actual == null || !expected.Bytes.SequenceEqual(actual))
                throw new IOException("Configuration changed outside this editor. Close and reopen Options.");
        }

        private byte[]? ReadBytes()
        {
            try
            {
                using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > MaximumFileBytes)
                    throw new InvalidDataException("Configuration exceeds 64 KiB.");
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                if (buffer.Length > MaximumFileBytes)
                    throw new InvalidDataException("Configuration exceeds 64 KiB.");
                return buffer.ToArray();
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private static int Integer(JObject doc, string key, int fallback)
        {
            JToken? token = doc[key];
            if (token == null) return fallback;
            if (token.Type != JTokenType.Integer)
                throw new InvalidDataException(key + " must be an integer.");
            return checked((int)token);
        }

        private static bool Boolean(JObject doc, string key, bool fallback)
        {
            JToken? token = doc[key];
            if (token == null) return fallback;
            if (token.Type != JTokenType.Boolean)
                throw new InvalidDataException(key + " must be a boolean.");
            return (bool)token;
        }

        private static int ConvertLegacy(int value, string unit)
        {
            double rounded = Math.Round(OptionsTemperatureUnits.ToKelvin(value, unit));
            if (double.IsNaN(rounded) || double.IsInfinity(rounded) ||
                rounded < int.MinValue || rounded > int.MaxValue)
                throw new InvalidDataException("Legacy temperature conversion overflowed.");
            return (int)rounded;
        }

        internal static void Log(string message, Exception? exception) =>
            DeliveryTemperatureSupportReporter.Record("DTL-OPTIONS", SupportDiagnosticSeverity.Warning,
                message, exception);
    }
}
