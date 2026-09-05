#nullable enable

using Newtonsoft.Json;
using PeterHan.PLib.Options;
using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Versioned shared configuration. Version 1 stores whole Kelvin values under
    /// the existing temperature property names; display units are presentation only.
    /// Untagged files are interpreted explicitly by DeliveryTemperatureOptionsStore.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [ModInfo("https://github.com/MaksymShostak/oxygen-not-included/tree/HEAD/mods/delivery-temperature-limit-supercooled")]
    [ConfigFile(SharedConfigLocation: true)]
    public sealed class DeliveryTemperatureLimitOptions
    {
        private static readonly Lazy<DeliveryTemperatureLimitOptions> LoadedOptions =
            new Lazy<DeliveryTemperatureLimitOptions>(
                DeliveryTemperatureOptionsStore.LoadRuntimeSnapshot);

        // Never replace or mutate this process-lifetime snapshot after saving.
        internal static DeliveryTemperatureLimitOptions Instance => LoadedOptions.Value;

        [JsonProperty]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty]
        public string TemperatureUnit { get; set; } = "kelvin";

        [JsonProperty]
        public bool CheckTemperatureForStatusItems { get; set; } = true;

        [JsonProperty]
        public bool UnderConstructionLimit { get; set; }

        [JsonProperty]
        public int MaxConstructionTemperature { get; set; } = 318;

        [JsonProperty]
        public int MinConstructionTemperature { get; set; } = 223;

        internal DeliveryTemperatureLimitOptions Copy() =>
            (DeliveryTemperatureLimitOptions)MemberwiseClone();

        internal bool HasSameValues(DeliveryTemperatureLimitOptions other) =>
            CheckTemperatureForStatusItems == other.CheckTemperatureForStatusItems &&
            UnderConstructionLimit == other.UnderConstructionLimit &&
            MinConstructionTemperature == other.MinConstructionTemperature &&
            MaxConstructionTemperature == other.MaxConstructionTemperature;

        public override string ToString() =>
            "DeliveryTemperatureLimit.Options[unit=kelvin, checkTemperatureForStatusItems=" +
            CheckTemperatureForStatusItems + ", underConstructionLimit=" +
            UnderConstructionLimit + ", minimumInclusive=" + MinConstructionTemperature +
            ", maximumExclusive=" + MaxConstructionTemperature + "]";
    }
}
