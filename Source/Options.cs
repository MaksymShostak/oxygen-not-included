using PeterHan.PLib.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DeliveryTemperatureLimit
{
    [JsonObject(MemberSerialization.OptIn)]
    [ModInfo("https://github.com/Hadden-Industries/oni-deliverytemperaturelimit")]
    [ConfigFile(SharedConfigLocation: true)]
    [RestartRequired]
    public sealed class Options : SingletonOptions< Options >, IOptions
    {
        [Option("Check Temperature For Status Items", "Status items such as 'Building lacks resources' will be shown if resources are available but not allowed by temperature limits.")]
        [JsonProperty]
        public bool CheckTemperatureForStatusItems { get; set; }

        [Option("Under Construction Limit", "Limit also deliveries to buildings under construction.")]
        [JsonProperty]
        public bool UnderConstructionLimit { get; set; }

        [Option("Max Construction Temperature", "Maximum temperature of resources for building a building.")]
        [JsonProperty]
        public int MaxConstructionTemperature { get; set; }

        [Option("Min Construction Temperature", "Minimum temperature of resources for building a building.")]
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
