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
            public static LocString LABEL_SETTINGS_SCOPE = "Settings for all colonies";
            public static LocString DIALOG_INTRO = "Set individual buildings' delivery ranges in their details panels. These settings are shared across your colonies.";
            public static LocString RESTART_NOTICE = "Changes take effect after restarting Oxygen Not Included.";
            public static LocString PENDING_RESTART_NOTICE = "Saved changes are waiting for a restart. This session still uses its startup settings.";

            public static LocString SECTION_CONSTRUCTION = "Construction";
            public static LocString CHECKBOX_LIMIT_CONSTRUCTION = "Limit construction material temperatures";
            public static LocString LABEL_DEFAULT_CONSTRUCTION_RANGE = "Default range for new construction";
            public static LocString TOOLTIP_DEFAULT_CONSTRUCTION_RANGE = "Existing construction sites are not updated. Limits are rounded to whole Kelvin values; the lower bound is included and the upper bound is excluded.";

            public static LocString SECTION_RESOURCE_WARNINGS = "Resource warnings";
            public static LocString CHECKBOX_WARN_ON_BLOCKED = "Warn when temperature limits block delivery";
            public static LocString TOOLTIP_WARN_ON_BLOCKED = "Includes temperature in the Lacks Resources check. Does not change delivery filtering.";
            public static LocString NOTICE_RESOURCE_WARNINGS_UNAVAILABLE = "Temperature-aware resource warnings are unavailable in this session. Your preference is retained.";

            public static LocString VALIDATION_INTEGER_REQUIRED = "Enter a whole-number temperature in the unit shown.";
            public static LocString VALIDATION_SUPPORTED_RANGE = "The rounded temperature must be within the supported range: {0} to {1} {2}.";
            public static LocString VALIDATION_EMPTY_INTERVAL = "The 'At least' value must be lower than 'Below' after rounding to whole Kelvin values. Choose a nonempty construction range.";
            public static LocString BUTTON_REVERT_RANGE = "Revert temperature edits";

            public static LocString BANNER_LEGACY_UNIT = "Unspecified unit in previous settings: pre-interpreted as {0}. Select a unit if different:";
            public static LocString BUTTON_INTERPRET_CELSIUS = "Interpret as \u00B0C";
            public static LocString BUTTON_INTERPRET_FAHRENHEIT = "Interpret as \u00B0F";
            public static LocString BUTTON_INTERPRET_KELVIN = "Interpret as K";

            public static LocString BUTTON_REPORT_BUG = "Report a bug\u2026";
            public static LocString DIALOG_REPORT_TITLE = "Delivery Temperature Limit - Report a bug";
            public static LocString BUTTON_BACK_TO_OPTIONS = "Back to Options";
            public static LocString SECTION_SUPPORT = "Help and support";
            public static LocString SECTION_REPORT_DETAILS = "Report details";
            public static LocString SECTION_ADVANCED_TROUBLESHOOTING = "Advanced troubleshooting";
            public static LocString BUTTON_OPEN_HOMEPAGE = "Open mod page";
            public static LocString BUTTON_OPEN_CONFIG_FOLDER = "Open configuration folder";
            public static LocString TOOLTIP_CONFIG_FOLDER = "This action does not save your draft. Close and reopen Options after editing the file externally.";
            public static LocString TOOLTIP_SUPPORT_REPORT = "Create a diagnostic file on this computer. You choose whether to share it.";
            public static LocString CHECKBOX_INCLUDE_PLAYER_LOG = "Include game log (optional)";
            public static LocString TOOLTIP_INCLUDE_PLAYER_LOG = "Adds recent game log entries to the local report. Logs may contain personal information; known paths are removed where possible. Review the file before attaching it on GitHub.";
            public static LocString BUTTON_CREATE_REPORT = "Create local report";
            public static LocString STATUS_CREATING_REPORT = "Creating a local report\u2026";
            public static LocString STATUS_REPORT_CREATED = "Report saved on this computer.";
            public static LocString STATUS_REPORT_FAILED = "Could not create the report. Try again. Details are in the game log.";
            public static LocString STATUS_LOG_INCLUDED = "Game log excerpt saved in this local report.";
            public static LocString STATUS_LOG_NOT_INCLUDED = "Game log not included.";
            public static LocString STATUS_LOG_UNAVAILABLE = "The game log could not be read. The report contains game and mod details only.";
            public static LocString BUTTON_OPEN_LAST_REPORT_FOLDER = "Open report folder";
            public static LocString BUTTON_COPY_REPORT_SUMMARY = "Copy last report summary";
            public static LocString STATUS_SUMMARY_COPIED = "Report summary copied.";
            public static LocString BUTTON_OPEN_ISSUE_FORM = "Continue to GitHub";
            public static LocString NOTICE_ISSUE_FORM = "Send game and mod details to GitHub to fill in the bug form. You can review them before posting. The report file stays on this computer.";
            public static LocString STATUS_ISSUE_FORM_OPENED = "Finish your bug report in your browser.\nTo share the full report, review the saved file and attach it to the form.";
            public static LocString STATUS_NO_REPORT = "Create a local report first. Reports from earlier sessions are not automatically selected.";
            public static LocString STATUS_ACTION_FAILED = "The action could not be completed. Details are in Player.log.";

            public static LocString BUTTON_RESTORE_DEFAULTS = "Restore defaults";
            public static LocString TOOLTIP_RESTORE_DEFAULTS = "Replaces the draft only. Cancel still leaves the saved file unchanged.";
            public static LocString BUTTON_CANCEL = "Cancel";
            public static LocString BUTTON_SAVE = "Save settings";
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
