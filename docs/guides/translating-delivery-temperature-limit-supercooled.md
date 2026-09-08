# Translator Handoff Guide: Delivery Temperature Limit (Supercooled)

This document is the authoritative localization reference and translation brief for the Oxygen Not Included mod **Delivery Temperature Limit (Supercooled)**. It provides context, UI locations, format placeholder rules, and semantic definitions for all **74 active strings**.

---

## 1. General Translation Guidelines

### Target Audience & Tone
- **Game Tone**: Follows Klei's official *Oxygen Not Included* tone: functional, concise, semi-industrial, and player-focused.
- **Terminology Consistency**: Use official Klei translations for game concepts where applicable:
  - *Deliveries / Delivery*: The logistics errand where duplicants carry materials to storage or buildings.
  - *Colony*: Player's save game / asteroid base.
  - *Storage / Details Panel*: The side screen inspector panel when clicking buildings.
  - *Construction / Blueprints*: Planned buildings awaiting materials and building errands.
  - *Units*: Formatted temperatures use $^\circ	ext{C}$, $^\circ	ext{F}$, or $	ext{K}$ depending on player settings.

### Formatting & Tag Rules
- **Placeholders (`{0}`, `{1}`)**:
  - Never translate, omit, or alter the bracket syntax `{0}` or `{1}`.
  - If a sentence requires reordering for grammatical naturalness in the target language, you may swap `{0}` and `{1}` (e.g., `{1} ... {0}`).
- **Color & Style Markup**:
  - `<color=#F0B310>▲</color>`: Yellow warning hazard triangle. Preserve the exact tag and color code.
  - `<color=#F44A47>▲</color>`: Red error hazard triangle. Preserve the exact tag and color code.
  - Note: `▲` is the character `▲` (U+25B2 BLACK UP-POINTING TRIANGLE).
- **Special Characters**:
  - `…`: Ellipsis (`…`)
  - `°`: Degree sign (`°`)
  - `\n`: Hard newline. Preserve newlines where indicated.
- **Mathematical / Interval Conventions**:
  - **Lower Bound ("At least")**: Inclusive ($T \ge 	ext{min}$). Material exactly at this temperature is accepted.
  - **Upper Bound ("Below")**: Exclusive ($T < 	ext{max}$). Material exactly at this temperature is excluded (this prevents materials right at boiling/freezing points from causing phase change disasters).

---

## 2. String Catalog & Semantic Definitions

### Domain 1: In-Game Building Details Side-Screen (`SIDESCREEN`)
*UI Location: Inspector details panel displayed on the right side of the screen when a building with deliverable storage is selected.*

| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TITLE` | `Delivery Temperature Limit` | Native side screen section header | None | Identifies the temperature restriction on delivered materials and separates these controls from adjacent sections. Preserve both delivery and temperature in a concise title. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.SECTION_RANGE` | `Temperature range` | Section header | None | Group title directly above the minimum and maximum input fields. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.LOWER_BOUND` | `At least` | Text input label | None | Label for the minimum allowed temperature. Denotes an **inclusive** lower limit ($T \ge x$). |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.UPPER_BOUND` | `Below` | Text input label | None | Label for the maximum allowed temperature. Denotes an **exclusive** upper boundary ($T < x$). |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.BUTTON_CLEAR` | `Clear` | Action button | None | Header button that clears both limits, disabling filtering. Action verb, keep very short (1 word). |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.STATUS.DISABLED` | `No temperature limit` | Inline status message | None | Displayed when both input fields are blank. Informs player that duplicants may deliver materials at any temperature. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.STATUS.LOWER_BOUND_ONLY` | `Allows deliveries at or above {0}` | Inline status message | `{0}` = Formatted temperature with unit (e.g. `20 °C`) | Displayed when only a minimum limit is set. Confirms only materials at or warmer than `{0}` are permitted. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.STATUS.UPPER_BOUND_ONLY` | `Allows deliveries below {0}` | Inline status message | `{0}` = Formatted temperature with unit (e.g. `0 °C`) | Displayed when only a maximum limit is set. Confirms only materials strictly colder than `{0}` are permitted. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.STATUS.INTERVAL` | `Allows deliveries at or above {0} and below {1}` | Inline status message | `{0}` = Lower bound (inclusive); `{1}` = Upper bound (exclusive) | Displayed when both limits are set. Confirms filtering accepts only temperatures in the $[{0}, {1})$ interval. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.VALIDATION.EMPTY_INTERVAL` | `<color=#F0B310>▲</color> No deliveries can match this range.` | Inline warning label | `<color=#F0B310>▲</color>` = Warning icon | Displayed when lower bound equals upper bound ($x$ to $x$). Since upper is exclusive, no temperature can satisfy $x \le T < x$. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.VALIDATION.BOUNDS_REVERSED` | `<color=#F44A47>▲</color> "At least" must not be above "Below".` | Inline error label | `<color=#F44A47>▲</color>` = Error icon; escaped quotes `"` | Displayed when typed lower bound exceeds upper bound ($x > y$). References the exact input labels "At least" and "Below". |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.VALIDATION.INVALID_NUMBER` | `<color=#F44A47>▲</color> Enter a whole-number temperature.` | Inline error label | `<color=#F44A47>▲</color>` = Error icon | Displayed when typed text contains decimals, invalid signs, or non-numeric characters. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.VALIDATION.OUT_OF_RANGE` | `<color=#F44A47>▲</color> Temperature must be between {0} and {1}.` | Inline error label | `{0}` = Min storable temp ($0	ext{ K}$ in current unit); `{1}` = Max storable temp ($10{,}000	ext{ K}$) | Displayed when value exceeds the game engine's physical simulation bounds ($0	ext{ K}$ to $10{,}000	ext{ K}$). |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TOOLTIPS.LOWER_BOUND` | `Minimum temperature for delivered materials. This value is included. Leave blank for no minimum.` | Field hover tooltip | None | Tooltip for the "At least" input field. Explains mathematical inclusivity and clearing behavior. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TOOLTIPS.UPPER_BOUND` | `Upper temperature boundary for delivered materials. Materials at exactly this boundary are excluded. Leave blank for no upper limit.` | Field hover tooltip | None | Tooltip for the "Below" input field. Explains mathematical exclusivity and clearing behavior. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TOOLTIPS.CLEAR` | `Remove both temperature limits.` | Button hover tooltip | None | Tooltip explaining the effect of the "Clear" button. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.TOOLTIPS.STATUS` | `Delivery Temperature Limit checks a material's temperature when choosing resources for delivery.` | Status label hover tooltip | None | Explanatory tooltip clarifying that filtering happens during errand resource allocation. |

---

### Domain 2: Mod Options & Global Settings Dialog (`OPTIONS`)
*UI Location: Main Menu / In-Game Pause Menu $	o$ Options $	o$ Mods $	o$ Delivery Temperature Limit $	o$ Options.*

#### A. Dialog Header & Shell
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.DIALOG_TITLE` | `Delivery Temperature Limit - Options` | Modal window title | None | Title banner of the main mod configuration dialog. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.DIALOG_INTRO` | `Set individual buildings' delivery ranges in their details panels. These settings are shared across your colonies.` | Introductory paragraph | None | Informs players that day-to-day filtering is configured per-building in the colony, while options here are global preferences. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.RESTART_NOTICE` | `Changes take effect after restarting Oxygen Not Included.` | Static footer note | None | Reminds the player that changed configuration parameters require an application restart to be initialized. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.PENDING_RESTART_NOTICE` | `Saved changes are waiting for a restart. This session still uses its startup settings.` | Status banner | None | Informational banner alerting the player that newly saved settings have not taken effect in the active session yet. |

#### B. Construction Settings Section
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.SECTION_CONSTRUCTION` | `Construction` | Section header | None | Section grouping default temperature limits applied to new construction blueprints. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.CHECKBOX_LIMIT_CONSTRUCTION` | `Limit construction material temperatures` | Checkbox toggle label | None | Toggles whether newly placed building blueprints automatically receive delivery temperature constraints. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.LABEL_DEFAULT_CONSTRUCTION_RANGE` | `Default range for new construction` | Field label | None | Label for default temperature limits assigned to newly placed construction blueprints. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_DEFAULT_CONSTRUCTION_RANGE` | `Existing construction sites are not updated. Limits are rounded to whole Kelvin values; the lower bound is included and the upper bound is excluded.` | Hover tooltip | None | Detailed tooltip explaining that changing this default does not retroactively alter existing construction sites. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.HINT_CONSTRUCTION_DISABLED` | `Enable construction limits to edit this default range.` | Inline hint label | None | Explanatory note displayed when construction limits are disabled, explaining why range inputs are greyed out. |

#### C. Resource Warnings Section
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.SECTION_RESOURCE_WARNINGS` | `Resource warnings` | Section header | None | Section grouping alerts and notifications regarding filtered materials. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.CHECKBOX_WARN_ON_BLOCKED` | `Warn when temperature limits block delivery` | Checkbox toggle label | None | Toggles whether buildings whose deliveries are blocked solely by temperature limits raise a "Lacks Resources" notification. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_WARN_ON_BLOCKED` | `Includes temperature in the Lacks Resources check. Does not change delivery filtering.` | Hover tooltip | None | Tooltip clarifying that this toggle only affects notification logic, not actual duplicant errand choices. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.NOTICE_RESOURCE_WARNINGS_UNAVAILABLE` | `Temperature-aware resource warnings are unavailable in this session. Your preference is retained.` | Inline notice | None | Informational note shown if compatibility patches could not be safely initialized during game startup. |

#### D. Validation & Range Editing
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.VALIDATION_INTEGER_REQUIRED` | `Enter a whole-number temperature in the unit shown.` | Inline error text | None | Displayed when draft input contains non-integer characters or unsupported symbols. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.VALIDATION_SUPPORTED_RANGE` | `The rounded temperature must be within the supported range: {0} to {1} {2}.` | Inline error text | `{0}` = Min formatted temp; `{1}` = Max formatted temp; `{2}` = Active unit symbol ($^\circ	ext{C}, ^\circ	ext{F}, 	ext{K}$) | Displayed when entered values exceed valid simulation limits. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.VALIDATION_EMPTY_INTERVAL` | `The 'At least' value must be lower than 'Below' after rounding to whole Kelvin values. Choose a nonempty construction range.` | Inline error text | Single quotes around labels | Displayed when minimum $\ge$ maximum after conversion to Kelvin. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.VALIDATION_INVALID_PENDING` | `Correct or revert the range before disabling construction limits. Invalid values will not be saved.` | Inline error text | None | Warns the player that an unparseable input field cannot be saved even if unchecking the feature. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_REVERT_RANGE` | `Revert temperature edits` | Action button | None | Discards unsaved typing in the default construction range boxes and restores last-saved numbers. |

#### E. Legacy Unit Disambiguation
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BANNER_LEGACY_UNIT` | `Unspecified unit in previous settings: pre-interpreted as {0}. Select a unit if different:` | Migration banner | `{0}` = Formatted active unit symbol ($^\circ	ext{C}, ^\circ	ext{F}, 	ext{K}$) | Appears once when upgrading from a pre-v2.0 version where numbers had no recorded unit. Informs player of default assumption. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_INTERPRET_CELSIUS` | `Interpret as °C` | Action button | `°` = Degree sign (`°`) | Button to confirm that legacy un-annotated numbers were entered in Celsius. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_INTERPRET_FAHRENHEIT` | `Interpret as °F` | Action button | `°` = Degree sign (`°`) | Button to confirm that legacy un-annotated numbers were entered in Fahrenheit. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_INTERPRET_KELVIN` | `Interpret as K` | Action button | None | Button to confirm that legacy un-annotated numbers were entered in Kelvin. |

#### F. Help & Local Diagnostic Reporting
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_EXPAND_HELP` | `Show help and diagnostics` | Accordion expand button | None | Expands the collapsible troubleshooting and diagnostic tools section. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_COLLAPSE_HELP` | `Hide help and diagnostics` | Accordion collapse button | None | Collapses the troubleshooting and diagnostic tools section. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.LABEL_INSTALLED_VERSION` | `Installed assembly file version: {0}` | Informational label | `{0}` = Mod version number string (e.g. `2026.9.5.0`) | Displays the loaded mod binary version to assist in bug reports. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_OPEN_HOMEPAGE` | `Open mod page` | Link button | None | Opens the mod's Steam Workshop / GitHub repository page in the player's web browser. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_OPEN_CONFIG_FOLDER` | `Open configuration folder` | Action button | None | Opens the local operating system directory where `config.json` is stored. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_CONFIG_FOLDER` | `This action does not save your draft. Close and reopen Options after editing the file externally.` | Hover tooltip | None | Cautions the player that modifying the config file externally requires reopening the UI to reload. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_SUPPORT_REPORT` | `Create a diagnostic file on this computer. You choose whether to share it.` | Hover tooltip | None | Privacy notice explaining that diagnostic generation is 100% offline and local. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.CHECKBOX_INCLUDE_PLAYER_LOG` | `Include Player.log in the next report` | Checkbox toggle label | Monospace/code reference `Player.log` | Opt-in toggle to bundle Unity game engine log into diagnostic package. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_INCLUDE_PLAYER_LOG` | `May contain personal information. Known paths are redacted where possible; review the report before sharing.` | Hover tooltip | None | Privacy guidance clarifying that logs may contain usernames, which the mod attempts to sanitize. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_CREATE_REPORT` | `Create local report` | Action button | None | Initiates generation of a local JSON support report on the computer. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_CREATING_REPORT` | `Creating a local report…` | Activity status | `…` = Ellipsis (`…`) | Shown temporarily while the report file is being gathered and written. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_REPORT_CREATED` | `Local report created. Nothing was uploaded or copied to the clipboard. Review before sharing:` | Success message | Precedes file path | Explicit privacy confirmation that report was created purely on disk without network activity. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_REPORT_FAILED` | `The report could not be created. No report was uploaded. Any previous successful report remains available. Failure details are in Player.log.` | Failure message | Monospace/code reference `Player.log` | Displayed if filesystem error prevents writing the diagnostic file. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_OPEN_LAST_REPORT_FOLDER` | `Open last report folder` | Action button | None | Opens the OS file explorer focused on the directory containing generated reports. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_COPY_REPORT_SUMMARY` | `Copy last report summary` | Action button | None | Copies a brief, sanitized markdown diagnostic summary to the player's clipboard. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_SUMMARY_COPIED` | `The clipboard now contains the report summary. Review it before sharing.` | Feedback label | None | Confirms summary was copied and encourages player to review for sensitive data. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_OPEN_ISSUE_FORM` | `Open GitHub issue form` | Link button | None | Opens the GitHub bug tracker in browser. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_ISSUE_FORM` | `Opens the issue template without diagnostic data in the URL. You choose what to paste or attach.` | Hover tooltip | None | Privacy note stating no logs or identifiers are transmitted via URL parameters. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_NO_REPORT` | `Create a local report first. Reports from earlier sessions are not automatically selected.` | Feedback label | None | Explains that "Open folder" / "Copy summary" require generating a report in the current session. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_ACTION_FAILED` | `The action could not be completed. Details are in Player.log.` | Feedback label | Monospace/code reference `Player.log` | Generic message when an OS clipboard, URL open, or folder open operation throws an exception. |

#### G. Modal Footer Actions & Colony Safety
| Key (`msgctxt`) | English Source (`msgid`) | UI Type & Constraints | Placeholders & Markup | Precise Definition & Translator Guidance |
|---|---|---|---|---|
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_RESTORE_DEFAULTS` | `Restore defaults` | Action button | None | Resets all settings in the editor to original factory defaults. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.TOOLTIP_RESTORE_DEFAULTS` | `Replaces the draft only. Cancel still leaves the saved file unchanged.` | Hover tooltip | None | Clarifies that clicking "Restore defaults" affects the active draft only until explicitly saved. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_CANCEL` | `Cancel` | Footer action button | None | Closes the options window and abandons all uncommitted changes. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_SAVE` | `Save changes` | Footer action button | None | Writes validated options to disk. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.STATUS_CHANGES_SAVED` | `Changes saved. The running session has not been changed.` | Footer status label | None | Confirms successful disk write and reminds player running session needs a restart. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_DONE` | `Done` | Footer action button | None | Closes the options dialog when no restart is pending. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_RESTART_NOW` | `Restart now` | Footer action button | None | Immediately restarts the game to reload options. Only visible from Main Menu. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_RESTART_LATER` | `Later` | Footer action button | None | Closes options without restarting immediately. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.WARNING_RUNNING_COLONY` | `Save your colony and return to the main menu before restarting. Saving options does not save colony progress.` | Safety alert | None | Displayed when options are opened mid-game to prevent players from losing unsaved colony progress. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.DIALOG_DISCARD_TITLE` | `Discard unsaved changes?` | Modal prompt title | None | Confirmation prompt when clicking Cancel, Close, or pressing Escape with unsaved edits. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_CONFIRM_DISCARD` | `Discard changes` | Dialog action button | None | Confirms discarding unsaved draft edits and closes options. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_CANCEL_DISCARD` | `Keep editing` | Dialog action button | None | Cancels the discard prompt and returns focus to editing the draft. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.ERROR_SAVE_FAILED` | `Options were not saved. Your draft is still here. Check file access and disk space; after an external edit, close and reopen Options. Details are in Player.log.` | Error dialog message | Monospace `Player.log` | Displayed if saving the options JSON fails due to filesystem permissions or disk full. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.ERROR_LOAD_FAILED` | `Options could not be read safely. The configuration has not been changed. Check the file, then reopen Options. Details are in Player.log.` | Error dialog message | Monospace `Player.log` | Displayed if reading the config JSON fails due to syntax error or corruption. |
| `STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.ERROR_UI_FAILED` | `The options screen could not be opened safely. No settings were changed. See Player.log for details.` | Fallback error message | Monospace `Player.log` | Emergency fallback dialog message if the custom options screen fails to initialize. |

---

## 3. Recommended Translator Workflow

### Active Language Catalogs
The repository currently maintains 9 community translation catalogs in [`mods/delivery-temperature-limit-supercooled/translations/`](file:///c:/Users/maksy/GitHub/oxygen-not-included/mods/delivery-temperature-limit-supercooled/translations):
- German (`de.po`)
- Spanish (`es.po`)
- French (`fr.po`)
- Korean (`ko.po`)
- Portuguese (`pt.po`)
- Brazilian Portuguese (`pt_BR.po`)
- Ukrainian (`uk.po`)
- Simplified Chinese (`zh.po`)
- Traditional Chinese (`zh_tw.po`)

### Contributing Workflow
1. **Working with CAT Tools (Poedit, Crowdin, Weblate)**:
   - Load `mods/delivery-temperature-limit-supercooled/translations/delivery_temperature_limit.pot` directly into your translation tool.
   - The `msgctxt` field contains the unique key; the `msgid` field contains the English source string.
   - Reference the tables above for precise context, UI location, and mechanical intent.
2. **Testing Translations In-Game**:
   - Save the translated `.po` file into `mods/delivery-temperature-limit-supercooled/translations/<language_code>.po` (e.g., `de.po`, `zh.po`, `uk.po`).
   - Run `oni-mod-pipeline build` and `oni-mod-pipeline install --target dev` to test layout and text expansion in the live game.
