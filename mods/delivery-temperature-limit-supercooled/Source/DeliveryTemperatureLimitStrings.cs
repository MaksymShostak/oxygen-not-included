#nullable enable

namespace STRINGS
{
    /// <summary>
    /// Stable Klei localization path consumed by the user interface and external
    /// translation resources. This precise type is the contract; no parallel
    /// facade or renamed localization tree is introduced.
    /// </summary>
    public class TEMPERATURELIMIT
    {
        public static LocString LABEL = "Temperatures:";

        public static LocString RANGE_SEPARATOR = "-";

        public static LocString TOOLTIP_RANGE =
            "Only objects in the temperature range from <b>{0}</b> to <b>{1}</b> may be delivered";

        public static LocString TOOLTIP_NOTSET =
            "No limit on the temperature range of objects that may be delivered";

        public static LocString SIDESCREEN_TITLE = "Delivery Limit";
    }
}
