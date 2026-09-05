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

        public static LocString TEMPERATURE_RANGE = "Temperature range";

        public static LocString LOW_BOUND_LABEL = "At least";

        public static LocString HIGH_BOUND_LABEL = "Below";

        public static LocString CLEAR = "Clear";

        public static LocString STATUS_DISABLED = "No temperature limit";

        public static LocString STATUS_LOW_ONLY = "Allows deliveries at or above {0}";

        public static LocString STATUS_HIGH_ONLY = "Allows deliveries below {0}";

        public static LocString STATUS_RANGE = "Allows deliveries: \u2265 {0} and < {1}";

        public static LocString WARNING_EMPTY = "\u26a0 No deliveries can match this range.";

        public static LocString ERROR_REVERSED = "\u26a0 \"At least\" must not be above \"Below\".";

        public static LocString ERROR_NUMBER = "\u26a0 Enter a whole-number temperature.";

        public static LocString ERROR_RANGE = "\u26a0 Temperature must be between {0} and {1}.";

        public static LocString TOOLTIP_LOW =
            "Minimum temperature for delivered materials. This value is included. Leave blank for no minimum.";

        public static LocString TOOLTIP_HIGH =
            "Upper temperature boundary for delivered materials. Materials at exactly this boundary are excluded. Leave blank for no upper limit.";

        public static LocString TOOLTIP_CLEAR = "Remove both temperature limits.";

        public static LocString TOOLTIP_STATUS =
            "Delivery Temperature Limit checks a material's temperature when choosing resources for delivery.";
    }
}
