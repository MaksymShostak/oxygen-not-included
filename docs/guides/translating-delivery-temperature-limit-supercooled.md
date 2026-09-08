# Translator Handoff Guide: Delivery Temperature Limit (Supercooled)

This document is the authoritative localization reference and translation brief for the Oxygen Not Included mod **Delivery Temperature Limit (Supercooled)**. It provides context, UI locations, format placeholder rules, and semantic definitions for all **80 active strings**.

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

### Domain 2: Mod Options and bug reports (`OPTIONS`)

The main screen shows colony-wide settings and a restart notice. Construction range fields appear only while construction limits are enabled. Toggling the feature off and back on preserves typed values. Saving while disabled keeps valid edits; incomplete or invalid typing leaves the previously stored bounds unchanged.

Labels, inputs, buttons, and expanded explanations use the same body text size. Headings use one larger size. Text wraps instead of shrinking. Keep labels brief, especially checkbox captions and action buttons. Single-line buttons share a common height; wrapped translations may grow. The layout reserves room for a scrollbar and keyboard focus outlines. Related controls are grouped with smaller gaps than the spacing between sections.

Supplementary explanations live behind adjacent `?` buttons. Hover shows the native tooltip; clicking or activating the button with the keyboard expands persistent text. Clicking again collapses it. Restart notices, validation errors, and the short GitHub disclosure remain visible when relevant.

#### Settings and range terminology

- `LABEL_SETTINGS_SCOPE`: settings shared by all colonies.
- `DIALOG_INTRO`: on-demand help explaining that individual building ranges belong in building details.
- `LABEL_DEFAULT_CONSTRUCTION_RANGE`: defaults for new construction, not existing sites.
- `TOOLTIP_DEFAULT_CONSTRUCTION_RANGE`: existing sites are unchanged; stored bounds use whole kelvin; the lower bound is inclusive and the upper bound exclusive.
- `TOOLTIP_WARN_ON_BLOCKED`: changes the resource warning check, not delivery filtering.
- `VALIDATION_SUPPORTED_RANGE`: preserve `{0}`, `{1}`, and `{2}` for minimum, maximum, and temperature unit.
- `VALIDATION_EMPTY_INTERVAL`: use the exact translated lower/upper input labels; minimum must be strictly less than maximum after rounding.
- `BUTTON_REVERT_RANGE`: revert temperature edits, distinct from restoring all defaults.
- `BANNER_LEGACY_UNIT`: preserve `{0}`, the assumed unit. The interpretation buttons choose what old numbers mean.

The obsolete disabled-range hint, disabled-range validation warning, and installed assembly version label have been removed. The loaded version remains in diagnostic reports.

#### Reporting flow

1. `BUTTON_REPORT_BUG` opens a dedicated report view, titled `DIALOG_REPORT_TITLE`. `BUTTON_BACK_TO_OPTIONS` returns to settings without saving or discarding the draft. Escape and the title close control also return to Options first. The existing dialog stays in place, and longer content scrolls inside it.
2. `CHECKBOX_INCLUDE_PLAYER_LOG` means **Include game log (optional)**. This choice lasts for the open dialog, including repeat attempts and failures. A newly opened dialog starts unchecked.
3. `NOTICE_ISSUE_FORM` explains that continuing sends a diagnostic summary to GitHub to prefill the form. The player reviews and submits it in the browser. The report file itself stays local.
4. `BUTTON_OPEN_ISSUE_FORM` means **Continue to GitHub**. It creates a fresh local report and opens the existing bug form with that report's summary. If creation fails, it does not open a form using an older report.
5. `STATUS_LOG_INCLUDED`, `STATUS_LOG_NOT_INCLUDED`, and `STATUS_LOG_UNAVAILABLE` describe what was actually collected, behind the `SECTION_REPORT_DETAILS` disclosure alongside the local filename. Including the log in the local file does not attach it to GitHub. The generated summary explicitly explains manual attachment and never claims that an attachment already exists.
6. `STATUS_ISSUE_FORM_OPENED` gives the next step: finish the bug form in the browser; review and attach the saved file to share the full report. Preserve the newline between these instructions. The report-folder action is available below this feedback.

Translate “game log” as a player-facing term for the game's diagnostic log, not a saved colony or gameplay history. `TOOLTIP_INCLUDE_PLAYER_LOG` explains the local log excerpt, possible personal information, best-effort removal of known paths, and review before attaching.

`SECTION_ADVANCED_TROUBLESHOOTING` is a stable disclosure heading with a chevron, not an action label that changes between “show” and “hide”. Its whole row toggles the advanced tools: configuration folder, local-only report generation, opening the last report folder, and copying its summary. Enter and Space also toggle focused disclosure headers. Creating a local report does not open a browser; copying is a separate action. Keep those actions distinct in translation. `SECTION_SUPPORT` groups the report and mod-page actions in Options.

#### Saving and closing

- `BUTTON_SAVE` means **Save settings**. It saves Options, not colony progress. The settings footer is absent from the report view, where continuing to GitHub is the primary action.
- `BUTTON_RESTORE_DEFAULTS` changes the draft; Cancel still preserves the saved file.
- `BUTTON_CANCEL_DISCARD` means **Keep editing**; `BUTTON_CONFIRM_DISCARD` discards the draft.
- Restart and running-colony notices must keep their distinction between saved settings and the current running session.
- Load/save error messages must not imply that an unsuccessful operation changed the saved configuration.

The POT is the complete source catalog. Keep every catalog context synchronized with its declared `LocString` and preserve placeholders.

---

## 3. Recommended Translator Workflow

### Active Language Catalogs
The repository currently maintains 18 translation catalogs in `mods/delivery-temperature-limit-supercooled/translations/`:
- Czech (`cs.po`)
- German (`de.po`)
- Greek (`el.po`)
- Spanish (`es.po`)
- French (`fr.po`)
- Hungarian (`hu.po`)
- Italian (`it.po`)
- Japanese (`ja.po`)
- Korean (`ko.po`)
- Polish (`pl.po`)
- Portuguese (`pt.po`)
- Brazilian Portuguese (`pt_BR.po`)
- Thai (`th.po`)
- Turkish (`tr.po`)
- Ukrainian (`uk.po`)
- Vietnamese (`vi.po`)
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
