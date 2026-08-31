#nullable enable

using Newtonsoft.Json;
using PeterHan.PLib.Options;
using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Player-configurable behavior persisted by PLib in this mod's shared
    /// configuration file. Property identities remain stable save/config keys.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [ModInfo("https://github.com/MaksymShostak/oxygen-not-included/tree/HEAD/mods/delivery-temperature-limit-supercooled")]
    [ConfigFile(SharedConfigLocation: true)]
    [RestartRequired]
    public sealed class DeliveryTemperatureLimitOptions
    {
        private static readonly Lazy<DeliveryTemperatureLimitOptions>
            LoadedOptions = new Lazy<DeliveryTemperatureLimitOptions>(
                LoadPersistedOptions);

        /// <summary>
        /// Returns the one immutable-for-this-process option snapshot. The class
        /// is restart-required, so runtime patch selection must never change
        /// after the loaded-mod topology has been verified.
        /// </summary>
        internal static DeliveryTemperatureLimitOptions Instance =>
            LoadedOptions.Value;

        [Option(
            "Include Temperature in \"Lacks Resources\" Warning",
            "If enabled, the yellow \"Lacks Resources\" warning will appear if all available materials in the colony are blocked by your temperature limits. Disabling this saves CPU performance in large colonies, but buildings may sit empty without showing a warning.")]
        [JsonProperty]
        public bool CheckTemperatureForStatusItems { get; set; }

        [Option(
            "Apply Limits to Construction Materials",
            "When enabled, temperature limits will also apply to materials delivered to build new structures. This prevents duplicants from using hot materials (like igneous rock near volcanoes) to build in cold areas.")]
        [JsonProperty]
        public bool UnderConstructionLimit { get; set; }

        [Option(
            "Default Max Construction Temperature",
            "The default maximum temperature allowed for materials used when placing new building blueprints.")]
        [JsonProperty]
        public int MaxConstructionTemperature { get; set; }

        [Option(
            "Default Min Construction Temperature",
            "The default minimum temperature allowed for materials used when placing new building blueprints.")]
        [JsonProperty]
        public int MinConstructionTemperature { get; set; }

        [Option(
            "Create Support Report",
            "Creates a local diagnostic report, copies a summary, and opens the GitHub bug form. Player.log is not read.",
            "Support")]
        [JsonIgnore]
        public System.Action<object> CreateSupportReport =>
            _ => DeliveryTemperatureSupportReporter.CreateStandardReport();

        [Option(
            "Create Extended Support Report",
            "Creates the same local report and includes a bounded, best-effort-redacted copy of the current Player.log. Review it before uploading.",
            "Support")]
        [JsonIgnore]
        public System.Action<object> CreateExtendedSupportReport =>
            _ => DeliveryTemperatureSupportReporter.CreateExtendedReport();

        public DeliveryTemperatureLimitOptions()
        {
            CheckTemperatureForStatusItems = true;
            UnderConstructionLimit = false;
            MaxConstructionTemperature = (int)Math.Round(
                GameUtil.GetTemperatureConvertedFromKelvin(
                    45 + 273.15f,
                    GameUtil.temperatureUnit));
            MinConstructionTemperature = (int)Math.Round(
                GameUtil.GetTemperatureConvertedFromKelvin(
                    -50 + 273.15f,
                    GameUtil.temperatureUnit));
        }

        public override string ToString() =>
            "DeliveryTemperatureLimit.DeliveryTemperatureLimitOptions[" +
            "checkTemperatureForStatusItems=" +
            CheckTemperatureForStatusItems +
            ", underConstructionLimit=" +
            UnderConstructionLimit +
            ", maxConstructionTemperature=" +
            MaxConstructionTemperature +
            ", minConstructionTemperature=" +
            MinConstructionTemperature +
            "]";

        private static DeliveryTemperatureLimitOptions LoadPersistedOptions() =>
            POptions.ReadSettings<DeliveryTemperatureLimitOptions>() ??
            new DeliveryTemperatureLimitOptions();
    }
}
