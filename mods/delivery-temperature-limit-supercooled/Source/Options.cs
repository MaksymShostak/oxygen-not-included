using PeterHan.PLib.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    [JsonObject(MemberSerialization.OptIn)]
    [ModInfo("https://github.com/MaksymShostak/oxygen-not-included/tree/HEAD/mods/delivery-temperature-limit-supercooled")]
    [ConfigFile(SharedConfigLocation: true)]
    [RestartRequired]
    public sealed class Options : SingletonOptions< Options >, IOptions
    {
        [Option("Include Temperature in \"Lacks Resources\" Warning", "If enabled, the yellow \"Lacks Resources\" warning will appear if all available materials in the colony are blocked by your temperature limits. Disabling this saves CPU performance in large colonies, but buildings may sit empty without showing a warning.")]
        [JsonProperty]
        public bool CheckTemperatureForStatusItems { get; set; }

        [Option("Apply Limits to Construction Materials", "When enabled, temperature limits will also apply to materials delivered to build new structures. This prevents duplicants from using hot materials (like igneous rock near volcanoes) to build in cold areas.")]
        [JsonProperty]
        public bool UnderConstructionLimit { get; set; }

        [Option("Default Max Construction Temperature", "The default maximum temperature allowed for materials used when placing new building blueprints.")]
        [JsonProperty]
        public int MaxConstructionTemperature { get; set; }

        [Option("Default Min Construction Temperature", "The default minimum temperature allowed for materials used when placing new building blueprints.")]
        [JsonProperty]
        public int MinConstructionTemperature { get; set; }

        public Options()
        {
            CheckTemperatureForStatusItems = true;
            UnderConstructionLimit = false;
            MaxConstructionTemperature = (int) Math.Round( GameUtil.GetTemperatureConvertedFromKelvin(
                45 + 273.15f, GameUtil.temperatureUnit ));
            MinConstructionTemperature = (int) Math.Round( GameUtil.GetTemperatureConvertedFromKelvin(
                -50 + 273.15f, GameUtil.temperatureUnit ));
        }

        public override string ToString()
        {
            return $"DeliveryTemperatureLimit.Options[checktemperatureforstatusitems={CheckTemperatureForStatusItems},"
                + $"DeliveryTemperatureLimit.Options[underconstructionlimit={UnderConstructionLimit},"
                + $"maxconstructiontemperature={MaxConstructionTemperature},"
                + $"minconstructiontemperature={MinConstructionTemperature}]";
        }

        public void OnOptionsChanged()
        {
            // 'this' is the Options instance used by the options dialog, so set up
            // the actual instance used by the mod. MemberwiseClone() is enough to copy non-reference data.
            Instance = (Options) this.MemberwiseClone();
        }

        public IEnumerable<IOptionsEntry> CreateOptions()
        {
            return null;
        }
    }
}
