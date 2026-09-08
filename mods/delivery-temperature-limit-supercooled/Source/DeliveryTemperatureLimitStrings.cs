#nullable enable

namespace STRINGS
{
    /// <summary>
    /// Stable Klei localization hierarchy consumed by the user interface, external
    /// translation catalogs, and runtime localization loaders. All user-facing strings
    /// are organized into symmetrical, domain-specific nested classes.
    /// </summary>
    public static class DELIVERY_TEMPERATURE_LIMIT
    {
        public static class SIDESCREEN
        {
            public static LocString TITLE = "Delivery Temperature Limit";
            public static LocString SECTION_RANGE = "Temperature range";
            public static LocString LOWER_BOUND = "At least";
            public static LocString UPPER_BOUND = "Below";
            public static LocString BUTTON_CLEAR = "Clear";

            public static class STATUS
            {
                public static LocString DISABLED = "No temperature limit";
                public static LocString LOWER_BOUND_ONLY = "Allows deliveries at or above {0}";
                public static LocString UPPER_BOUND_ONLY = "Allows deliveries below {0}";
                public static LocString INTERVAL = "Allows deliveries at or above {0} and below {1}";
            }

            public static class VALIDATION
            {
                public static LocString EMPTY_INTERVAL = "<color=#F0B310>\u25B2</color> No deliveries can match this range";
                public static LocString BOUNDS_REVERSED = "<color=#F44A47>\u25B2</color> \"At least\" must not be above \"Below\"";
                public static LocString INVALID_NUMBER = "<color=#F44A47>\u25B2</color> Enter a whole-number temperature";
                public static LocString OUT_OF_RANGE = "<color=#F44A47>\u25B2</color> Temperature must be between {0} and {1}";
            }

            public static class TOOLTIPS
            {
                public static LocString LOWER_BOUND =
                    "Minimum temperature for delivered materials. This value is included. Leave blank for no minimum.";
                public static LocString UPPER_BOUND =
                    "Upper temperature boundary for delivered materials. Materials at exactly this boundary are excluded. Leave blank for no upper limit.";
                public static LocString CLEAR = "Remove both temperature limits.";
                public static LocString STATUS =
                    "Delivery Temperature Limit checks a material's temperature when choosing resources for delivery.";
            }
        }

        public static class OPTIONS
        {
            public static LocString DIALOG_TITLE = "Delivery Temperature Limit - Options";
            public static LocString DIALOG_INTRO = "Set individual buildings' delivery ranges in their details panels. These settings are shared across your colonies.";
            public static LocString RESTART_NOTICE = "Changes take effect after restarting Oxygen Not Included.";
            public static LocString PENDING_RESTART_NOTICE = "Saved changes are waiting for a restart. This session still uses its startup settings.";

            public static LocString SECTION_CONSTRUCTION = "Construction";
            public static LocString CHECKBOX_LIMIT_CONSTRUCTION = "Limit construction material temperatures";
            public static LocString LABEL_DEFAULT_CONSTRUCTION_RANGE = "Default range for new construction";
            public static LocString TOOLTIP_DEFAULT_CONSTRUCTION_RANGE = "Existing construction sites are not updated. Limits are rounded to whole Kelvin values; the lower bound is included and the upper bound is excluded.";
            public static LocString HINT_CONSTRUCTION_DISABLED = "Enable construction limits to edit this default range.";

            public static LocString SECTION_RESOURCE_WARNINGS = "Resource warnings";
            public static LocString CHECKBOX_WARN_ON_BLOCKED = "Warn when temperature limits block delivery";
            public static LocString TOOLTIP_WARN_ON_BLOCKED = "Includes temperature in the Lacks Resources check. Does not change delivery filtering.";
            public static LocString NOTICE_RESOURCE_WARNINGS_UNAVAILABLE = "Temperature-aware resource warnings are unavailable in this session. Your preference is retained.";

            public static LocString VALIDATION_INTEGER_REQUIRED = "Enter a whole-number temperature in the unit shown.";
            public static LocString VALIDATION_SUPPORTED_RANGE = "The rounded temperature must be within the supported range: {0} to {1} {2}.";
            public static LocString VALIDATION_EMPTY_INTERVAL = "The 'At least' value must be lower than 'Below' after rounding to whole Kelvin values. Choose a nonempty construction range.";
            public static LocString VALIDATION_INVALID_PENDING = "Correct or revert the range before disabling construction limits. Invalid values will not be saved.";
            public static LocString BUTTON_REVERT_RANGE = "Revert temperature edits";

            public static LocString BANNER_LEGACY_UNIT = "Unspecified unit in previous settings: pre-interpreted as {0}. Select a unit if different:";
            public static LocString BUTTON_INTERPRET_CELSIUS = "Interpret as \u00B0C";
            public static LocString BUTTON_INTERPRET_FAHRENHEIT = "Interpret as \u00B0F";
            public static LocString BUTTON_INTERPRET_KELVIN = "Interpret as K";

            public static LocString BUTTON_EXPAND_HELP = "Show help and diagnostics";
            public static LocString BUTTON_COLLAPSE_HELP = "Hide help and diagnostics";
            public static LocString LABEL_INSTALLED_VERSION = "Installed assembly file version: {0}";
            public static LocString BUTTON_OPEN_HOMEPAGE = "Open mod page";
            public static LocString BUTTON_OPEN_CONFIG_FOLDER = "Open configuration folder";
            public static LocString TOOLTIP_CONFIG_FOLDER = "This action does not save your draft. Close and reopen Options after editing the file externally.";
            public static LocString TOOLTIP_SUPPORT_REPORT = "Create a diagnostic file on this computer. You choose whether to share it.";
            public static LocString CHECKBOX_INCLUDE_PLAYER_LOG = "Include Player.log in the next report";
            public static LocString TOOLTIP_INCLUDE_PLAYER_LOG = "May contain personal information. Known paths are redacted where possible; review the report before sharing.";
            public static LocString BUTTON_CREATE_REPORT = "Create local report";
            public static LocString STATUS_CREATING_REPORT = "Creating a local report\u2026";
            public static LocString STATUS_REPORT_CREATED = "Local report created. Nothing was uploaded or copied to the clipboard. Review before sharing:";
            public static LocString STATUS_REPORT_FAILED = "The report could not be created. No report was uploaded. Any previous successful report remains available. Failure details are in Player.log.";
            public static LocString BUTTON_OPEN_LAST_REPORT_FOLDER = "Open last report folder";
            public static LocString BUTTON_COPY_REPORT_SUMMARY = "Copy last report summary";
            public static LocString STATUS_SUMMARY_COPIED = "The clipboard now contains the report summary. Review it before sharing.";
            public static LocString BUTTON_OPEN_ISSUE_FORM = "Open GitHub issue form";
            public static LocString TOOLTIP_ISSUE_FORM = "Opens the issue template without diagnostic data in the URL. You choose what to paste or attach.";
            public static LocString STATUS_NO_REPORT = "Create a local report first. Reports from earlier sessions are not automatically selected.";
            public static LocString STATUS_ACTION_FAILED = "The action could not be completed. Details are in Player.log.";

            public static LocString BUTTON_RESTORE_DEFAULTS = "Restore defaults";
            public static LocString TOOLTIP_RESTORE_DEFAULTS = "Replaces the draft only. Cancel still leaves the saved file unchanged.";
            public static LocString BUTTON_CANCEL = "Cancel";
            public static LocString BUTTON_SAVE = "Save changes";
            public static LocString STATUS_CHANGES_SAVED = "Changes saved. The running session has not been changed.";
            public static LocString BUTTON_DONE = "Done";
            public static LocString BUTTON_RESTART_NOW = "Restart now";
            public static LocString BUTTON_RESTART_LATER = "Later";
            public static LocString WARNING_RUNNING_COLONY = "Save your colony and return to the main menu before restarting. Saving options does not save colony progress.";
            public static LocString DIALOG_DISCARD_TITLE = "Discard unsaved changes?";
            public static LocString BUTTON_CONFIRM_DISCARD = "Discard changes";
            public static LocString BUTTON_CANCEL_DISCARD = "Keep editing";
            public static LocString ERROR_SAVE_FAILED = "Options were not saved. Your draft is still here. Check file access and disk space; after an external edit, close and reopen Options. Details are in Player.log.";
            public static LocString ERROR_LOAD_FAILED = "Options could not be read safely. The configuration has not been changed. Check the file, then reopen Options. Details are in Player.log.";
            public static LocString ERROR_UI_FAILED = "The options screen could not be opened safely. No settings were changed. See Player.log for details.";
        }
    }
}
