#nullable enable

using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns editing and closing; PDialog supplies only the native modal shell.
    /// Content buttons never use PDialog's automatic close-and-save flow.
    /// </summary>
    internal sealed class DeliveryTemperatureOptionsDialog
    {
        private readonly DeliveryTemperatureOptionsStore store = new DeliveryTemperatureOptionsStore();
        private readonly List<System.Action<object>> closeCallbacks = new List<System.Action<object>>();
        private readonly GameObject? previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        private readonly List<System.Action> refreshControls = new List<System.Action>();
        private OptionsEditSession? session;
        private KScreen? screen;
        private OptionsDialogLifetime? lifetime;
        private OptionsFocusNavigation focus = new OptionsFocusNavigation();
        private GameObject? lowInput, highInput, messageLabel, supportLabel;
        private GameObject? settingsPanel, helpPanel, discardPanel, footerPanel, legacyPanel, savedPanel;
        private GameObject? keepEditingButton;
        private bool helpOpen, discardOpen, includeLog, reporting, saved, repairRange, updatingInputs;
        private bool handlingKeyboard, closeAfterKeyRelease, closeScheduled, ended;
        private int lastKeyFrame = -1;
        private float contentWidth;
        private string message = "";

        internal void AddCloseCallback(System.Action<object>? callback)
        {
            if (callback != null) closeCallbacks.Add(callback);
        }

        internal void Show()
        {
            if (session == null)
            {
                try
                {
                    session = new OptionsEditSession(store);
                    repairRange = !RangeIsValid;
                }
                catch (Exception ex)
                {
                    DeliveryTemperatureOptionsStore.Log("Options could not be read.", ex);
                    message = Text.ERROR_LOAD_FAILED;
                }
            }
            Build();
        }

        private void Build()
        {
            refreshControls.Clear();
            focus = new OptionsFocusNavigation();
            Vector2 maximum = GetAvailableSize();
            contentWidth = maximum.x - 56;
            var dialog = new PDialog("DeliveryTemperatureOptions")
            {
                Title = Text.DIALOG_TITLE,
                Size = new Vector2(Math.Min(520, maximum.x), Math.Min(360, maximum.y)),
                MaxSize = maximum, SortKey = 150,
                DialogBackColor = PUITuning.Colors.OptionsBackground,
                RoundToNearestEven = true
            };
            PPanel body = dialog.Body;
            body.Direction = PanelDirection.Vertical;
            body.Margin = new RectOffset(12, 12, 12, 12);
            body.Spacing = 10;
            var contents = Column("SettingsContents");
            contents.AddChild(Label(Text.DIALOG_INTRO));
            contents.AddChild(Label(Text.RESTART_NOTICE, obj => refreshControls.Add(() =>
                SetLabel(obj, session?.RestartPending == true ? Text.PENDING_RESTART_NOTICE : Text.RESTART_NOTICE))));
            if (session != null) AddSettings(contents, session);
            contents.AddChild(Button("HelpToggle", helpOpen ? Text.BUTTON_COLLAPSE_HELP : Text.BUTTON_EXPAND_HELP,
                () => { helpOpen = !helpOpen; Refresh(); }, enabled: () => !discardOpen));
            var help = Column("HelpAndDiagnostics");
            help.AddOnRealize(obj => { helpPanel = obj; SetVisible(obj, helpOpen); });
            AddHelp(help);
            contents.AddChild(help);
            var scroll = new PScrollPane
            {
                Child = contents, ScrollHorizontal = false, ScrollVertical = true,
                AlwaysShowHorizontal = false, AlwaysShowVertical = false,
                FlexSize = Vector2.one, TrackSize = 12
            };
            body.AddChild(scroll);
            body.AddChild(Label(message, obj => messageLabel = obj));
            AddFooter(body);
            GameObject? root = null;
            try
            {
                root = dialog.Build();
                screen = root.GetComponent<KScreen>() ??
                    throw new InvalidOperationException("PLib did not create a modal KScreen.");
                DeliveryTemperatureOptionsUiBridge.Attach(screen, this);
                lifetime = root.AddComponent<OptionsDialogLifetime>();
                lifetime.PollInput = HandleKeyRelease;
                KScreen ownedScreen = screen;
                lifetime.Disposed = () =>
                {
                    DeliveryTemperatureOptionsUiBridge.Detach(ownedScreen);
                    Finish();
                };
                Refresh();
                screen.Activate();
                if (session != null && repairRange) Validate(false);
                focus.Move(false);
            }
            catch
            {
                if (screen != null) DeliveryTemperatureOptionsUiBridge.Detach(screen);
                if (root != null) UnityEngine.Object.Destroy(root);
                screen = null;
                throw;
            }
        }

                private void AddSettings(PPanel parent, OptionsEditSession edit)
        {
            var legacy = Column("LegacyUnitConfirmation");
            legacy.AddOnRealize(obj => { legacyPanel = obj; SetVisible(obj, edit.NeedsDisambiguation); });
            refreshControls.Add(() => { if (legacyPanel != null && session != null) SetVisible(legacyPanel, session.NeedsDisambiguation); });
            legacy.AddChild(Label(string.Format(Text.BANNER_LEGACY_UNIT, OptionsTemperatureUnits.Symbol(edit.DisplayUnit))));
            var legacyRow = new PPanel("LegacyButtons") { Direction = PanelDirection.Horizontal, Spacing = 8, FlexSize = Vector2.right };
            foreach (string unit in new[] { "celsius", "fahrenheit", "kelvin" })
            {
                string label = unit == "celsius" ? Text.BUTTON_INTERPRET_CELSIUS :
                    unit == "fahrenheit" ? Text.BUTTON_INTERPRET_FAHRENHEIT : Text.BUTTON_INTERPRET_KELVIN;
                legacyRow.AddChild(Button("Interpret" + unit, label, () =>
                {
                    try
                    {
                        edit.ConfirmLegacyUnit(unit);
                        ResetInputText();
                        Validate(false);
                        Refresh();
                        focus.Move(false);
                    }
                    catch (Exception ex) { Fail(Text.ERROR_LOAD_FAILED, ex); }
                }));
            }
            legacy.AddChild(legacyRow);
            parent.AddChild(legacy);
            var settings = Column("GameplaySettings");
            settings.AddOnRealize(obj => settingsPanel = obj);
            settings.AddChild(Heading(Text.SECTION_CONSTRUCTION));
            settings.AddChild(Check("Construction", Text.CHECKBOX_LIMIT_CONSTRUCTION, () => edit.LimitConstruction, () =>
            {
                if (edit.LimitConstruction && Validate(false) != ConstructionRangeError.None)
                {
                    message = Text.VALIDATION_INVALID_PENDING;
                    Refresh();
                    return;
                }
                edit.LimitConstruction = !edit.LimitConstruction;
                Refresh();
            }, () => CanEdit));
            var range = Column("ConstructionDefaults");
            range.Margin = new RectOffset(16, 0, 0, 8);
            range.AddChild(Label(Text.LABEL_DEFAULT_CONSTRUCTION_RANGE));
            AddInput(range, "Lower", STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.LOWER_BOUND,
                true, obj => lowInput = obj);
            AddInput(range, "Upper", STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.UPPER_BOUND,
                false, obj => highInput = obj);
            range.AddChild(Label(Text.TOOLTIP_DEFAULT_CONSTRUCTION_RANGE));
            var revert = Button("RevertRange", Text.BUTTON_REVERT_RANGE, () =>
            {
                edit.Range.Revert(); ResetInputText(); Validate(false);
            }, enabled: () => CanEdit && edit.Range.IsDirty);
            revert.AddOnRealize(obj =>
            {
                SetVisible(obj, edit.Range.IsDirty);
                refreshControls.Add(() => SetVisible(obj, edit.Range.IsDirty));
            });
            range.AddChild(revert);
            range.AddChild(Label(Text.HINT_CONSTRUCTION_DISABLED, obj => refreshControls.Add(() =>
                SetVisible(obj, !edit.LimitConstruction && !repairRange))));
            settings.AddChild(range);
            settings.AddChild(Heading(Text.SECTION_RESOURCE_WARNINGS));
            settings.AddChild(Check("Warnings", Text.CHECKBOX_WARN_ON_BLOCKED, () => edit.CheckWarnings,
                () => { edit.CheckWarnings = !edit.CheckWarnings; Refresh(); }, () => CanEdit));
            settings.AddChild(Label(Text.TOOLTIP_WARN_ON_BLOCKED));
            SupportRuntimeSnapshot runtime = DeliveryTemperatureRuntimePatchInstaller.CaptureSupportReportSnapshot();
            if (runtime.StatusCompatibilityDiagnostic != null)
                settings.AddChild(Label(Text.NOTICE_RESOURCE_WARNINGS_UNAVAILABLE));
            parent.AddChild(settings);
        }

        private void AddInput(PPanel parent, string name, string label, bool lower,
            System.Action<GameObject> realized)
        {
            var row = new PPanel(name + "Row") { Direction = PanelDirection.Horizontal,
                Spacing = 8, FlexSize = Vector2.right };
            row.AddChild(Label(label, null, contentWidth - 160));
            var input = new PTextField(name) { Type = PTextField.FieldType.Integer,
                MinWidth = 96, OnTextChanged = (_, text) =>
                {
                    if (session == null || updatingInputs) return;
                    if (lower) session.Range.LowerText = text; else session.Range.UpperText = text;
                    session.DismissDisambiguation();
                    Refresh();
                } };
            input.AddOnRealize(obj =>
            {
                realized(obj);
                TMP_InputField field = obj.GetComponent<TMP_InputField>() ??
                    throw new InvalidOperationException("PLib did not create a text field.");
                field.characterLimit = 12;
                updatingInputs = true;
                try { field.text = lower ? session!.Range.LowerText : session!.Range.UpperText; }
                finally { updatingInputs = false; }
                field.onEndEdit.AddListener(_ => { if (!updatingInputs) Validate(false); });
                refreshControls.Add(() => field.interactable = CanEditRange);
                focus.Add(obj, () => field.ActivateInputField(), () => CanEditRange);
            });
            row.AddChild(input);
            row.AddChild(Label(OptionsTemperatureUnits.Symbol(session!.DisplayUnit), null, 32));
            parent.AddChild(row);
        }

        private void AddHelp(PPanel parent)
        {
            string version = typeof(DeliveryTemperatureLimitMod).Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "?";
            parent.AddChild(Label(string.Format(Text.LABEL_INSTALLED_VERSION, version)));
            parent.AddChild(Button("Homepage", Text.BUTTON_OPEN_HOMEPAGE, () => External(() => Application.OpenURL(
                "https://steamcommunity.com/sharedfiles/filedetails/?id=3759075866"))));
            parent.AddChild(Button("ConfigurationFolder", Text.BUTTON_OPEN_CONFIG_FOLDER, () => External(() =>
            {
                string directory = Path.GetDirectoryName(store.Path) ??
                    throw new InvalidOperationException("Configuration directory is unavailable.");
                Directory.CreateDirectory(directory);
                Application.OpenURL(new Uri(directory).AbsoluteUri);
            })));
            parent.AddChild(Label(Text.TOOLTIP_CONFIG_FOLDER));
            parent.AddChild(Label(Text.TOOLTIP_SUPPORT_REPORT));
            parent.AddChild(Check("IncludeLog", Text.CHECKBOX_INCLUDE_PLAYER_LOG, () => includeLog,
                () => { includeLog = !includeLog; Refresh(); }, () => !reporting));
            parent.AddChild(Label(Text.TOOLTIP_INCLUDE_PLAYER_LOG));
            parent.AddChild(Button("CreateReport", Text.BUTTON_CREATE_REPORT, CreateReport,
                enabled: () => !reporting));
            parent.AddChild(Label(SupportReportPlayerPresenter.Message, obj => supportLabel = obj));
            parent.AddChild(Button("ReportFolder", Text.BUTTON_OPEN_LAST_REPORT_FOLDER,
                () => SupportAction(SupportReportPlayerPresenter.OpenLastReportFolder),
                enabled: () => SupportReportPlayerPresenter.HasReport && !reporting));
            parent.AddChild(Button("CopySummary", Text.BUTTON_COPY_REPORT_SUMMARY,
                () => SupportAction(SupportReportPlayerPresenter.CopyLastReportSummary),
                enabled: () => SupportReportPlayerPresenter.HasReport && !reporting));
            parent.AddChild(Button("Issue", Text.BUTTON_OPEN_ISSUE_FORM,
                () => SupportAction(SupportReportPlayerPresenter.OpenIssueForm), Text.TOOLTIP_ISSUE_FORM));
        }

        private void AddFooter(PPanel body)
        {
            var confirm = Column("DiscardConfirmation");
            confirm.AddOnRealize(obj => { discardPanel = obj; SetVisible(obj, discardOpen); });
            confirm.AddChild(Label(Text.DIALOG_DISCARD_TITLE));
            var keep = Button("KeepEditing", Text.BUTTON_CANCEL_DISCARD,
                () => { discardOpen = false; Refresh(); focus.Move(false); });
            keep.AddOnRealize(obj => keepEditingButton = obj);
            confirm.AddChild(keep);
            confirm.AddChild(Button("Discard", Text.BUTTON_CONFIRM_DISCARD, Close));
            body.AddChild(confirm);
            var footer = Column("Actions");
            footer.AddOnRealize(obj => { footerPanel = obj; SetVisible(obj, !saved); });
            if (session != null)
                footer.AddChild(Button("Defaults", Text.BUTTON_RESTORE_DEFAULTS, () =>
                {
                    session.RestoreDefaults(); ResetInputText(); message = ""; Refresh();
                }, Text.TOOLTIP_RESTORE_DEFAULTS));
            var actions = new PPanel("SaveAndCancel") { Direction = PanelDirection.Horizontal,
                Spacing = 12, FlexSize = Vector2.right, Alignment = TextAnchor.MiddleRight };
            actions.AddChild(Button("Cancel", Text.BUTTON_CANCEL, RequestClose));
            if (session != null) actions.AddChild(Button("Save", Text.BUTTON_SAVE, Save, primary: true));
            footer.AddChild(actions);
            body.AddChild(footer);
            var result = Column("SavedActions");
            result.AddOnRealize(obj => { savedPanel = obj; SetVisible(obj, saved); });
            result.AddChild(Label(Text.STATUS_CHANGES_SAVED));
            result.AddChild(Label(Text.WARNING_RUNNING_COLONY, obj => refreshControls.Add(() =>
                SetVisible(obj, session?.RestartPending == true && Game.Instance != null))));
            var restart = Button("Restart", Text.BUTTON_RESTART_NOW, () => External(() =>
            {
                if (Game.Instance != null) { message = Text.WARNING_RUNNING_COLONY; Refresh(); return; }
                PGameUtils.SaveMods();
                App.instance.Restart();
            }));
            restart.AddOnRealize(obj => refreshControls.Add(() =>
                SetVisible(obj, session?.RestartPending == true && Game.Instance == null)));
            result.AddChild(restart);
            result.AddChild(Button("Later", Text.BUTTON_RESTART_LATER, Close));
            body.AddChild(result);
        }

        private bool CanEdit => session != null &&
            !saved && !discardOpen && !reporting && !closeAfterKeyRelease;
        private bool RangeIsValid => session != null &&
            session.Range.Validate(out _, out _) == ConstructionRangeError.None;
        private bool CanEditRange => CanEdit && (session!.LimitConstruction || repairRange);

        private void ResetInputText()
        {
            if (session == null) return;
            updatingInputs = true;
            try
            {
                TMP_InputField? low = lowInput?.GetComponent<TMP_InputField>();
                TMP_InputField? high = highInput?.GetComponent<TMP_InputField>();
                if (low != null) low.text = session.Range.LowerText;
                if (high != null) high.text = session.Range.UpperText;
                repairRange = !RangeIsValid;
            }
            finally { updatingInputs = false; }
        }

        private ConstructionRangeError Validate(bool focusError)
        {
            if (session == null) return ConstructionRangeError.None;
            ConstructionRangeError error = session.Range.Validate(out _, out _);
            message = error == ConstructionRangeError.None ? "" :
                error == ConstructionRangeError.LowerNumber || error == ConstructionRangeError.UpperNumber ?
                Text.VALIDATION_INTEGER_REQUIRED.ToString() : error == ConstructionRangeError.EmptyRange ? Text.VALIDATION_EMPTY_INTERVAL.ToString() :
                string.Format(Text.VALIDATION_SUPPORTED_RANGE.ToString(),
                    OptionsTemperatureUnits.Format(0, session.DisplayUnit),
                    OptionsTemperatureUnits.Format(OniStorableTemperatureBounds.MaximumTemperatureKelvin,
                        session.DisplayUnit), OptionsTemperatureUnits.Symbol(session.DisplayUnit));
            Refresh();
            if (focusError && error != ConstructionRangeError.None)
                focus.Focus(error == ConstructionRangeError.UpperNumber ||
                    error == ConstructionRangeError.UpperRange ? highInput : lowInput);
            return error;
        }

        private void Save()
        {
            if (session == null || reporting) return;
            
            if (Validate(true) != ConstructionRangeError.None) return;
            if (!session.IsDirty) { Close(); return; }
            bool written;
            try
            {
                if (session.Save(out written) != ConstructionRangeError.None) return;
            }
            catch (Exception ex) { Fail(Text.ERROR_SAVE_FAILED, ex); return; }
            // A presentation error after this point must not be called a save failure.
            if (!written) { Close(); return; }
            saved = true;
            message = "";
            ResetInputText();
            Refresh();
            focus.Move(false);
        }

        internal void RequestClose()
        {
            if (ended || reporting) return;
            if (session?.IsDirty != true) { Close(); return; }
            discardOpen = true;
            Refresh();
            focus.Focus(keepEditingButton);
        }

        private void Close()
        {
            if (handlingKeyboard || Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return) ||
                Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space))
            {
                closeAfterKeyRelease = true;
                return;
            }
            DestroyShell();
            Finish();
        }

        private void DestroyShell()
        {
            KScreen? previous = screen;
            screen = null;
            if (lifetime != null) { lifetime.Disposed = null; lifetime.PollInput = null; }
            if (previous == null) return;
            DeliveryTemperatureOptionsUiBridge.Detach(previous);
            previous.Deactivate();
        }

        internal void AbortOpening() { DestroyShell(); Finish(); }

        private void Finish()
        {
            if (ended) return;
            ended = true;
            DeliveryTemperatureOptionsUiBridge.Released(this);
            if (previousSelection != null && previousSelection.activeInHierarchy)
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(previousSelection);
            object value = session?.SavedCopy() ?? DeliveryTemperatureLimitOptions.Instance.Copy();
            foreach (var callback in closeCallbacks)
            {
                try { callback(value); }
                catch (Exception ex) { DeliveryTemperatureOptionsStore.Log("Options close callback failed.", ex); }
            }
            closeCallbacks.Clear();
        }

        internal void HandleKeyboard(bool escapeAction)
        {
            bool escape = escapeAction || Input.GetKeyDown(KeyCode.Escape);
            bool tab = Input.GetKeyDown(KeyCode.Tab);
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool space = Input.GetKeyDown(KeyCode.Space) && !focus.IsTextFocused;
            if ((!escape && !tab && !enter && !space) || lastKeyFrame == Time.frameCount ||
                reporting || closeAfterKeyRelease || ended) return;
            lastKeyFrame = Time.frameCount;
            handlingKeyboard = true;
            try
            {
                if (escape)
                {
                    if (discardOpen) { discardOpen = false; Refresh(); }
                    else RequestClose();
                }
                else if (tab)
                    focus.Move(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                else if (enter || space) focus.Activate();
            }
            finally { handlingKeyboard = false; }
        }

        internal void HandleKeyRelease()
        {
            if (!closeAfterKeyRelease || closeScheduled || lifetime == null ||
                Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return) ||
                Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space)) return;
            closeScheduled = true;
            lifetime.StartCoroutine(CloseAfterInputFrame());
        }

        private IEnumerator CloseAfterInputFrame()
        {
            // Keep the modal registered through the release frame so Escape/Enter
            // cannot activate the screen underneath it.
            yield return new WaitForEndOfFrame();
            closeScheduled = false;
            closeAfterKeyRelease = false;
            Close();
        }

        private void CreateReport()
        {
            if (reporting || lifetime == null) return;
            reporting = true;
            SetLabel(supportLabel, Text.STATUS_CREATING_REPORT);
            Refresh();
            lifetime.StartCoroutine(CreateReportAfterRepaint(includeLog));
        }

        private IEnumerator CreateReportAfterRepaint(bool withLog)
        {
            // Keep Unity snapshot capture on the main thread, but paint feedback
            // before bounded file/log work. This is not an asynchronous upload.
            yield return null;
            try
            {
                if (withLog) DeliveryTemperatureSupportReporter.CreateExtendedReport();
                else DeliveryTemperatureSupportReporter.CreateStandardReport();
                includeLog = false;
                SetLabel(supportLabel, SupportReportPlayerPresenter.Message);
            }
            finally { reporting = false; Refresh(); }
        }

        private void SupportAction(System.Action action)
        {
            action();
            SetLabel(supportLabel, SupportReportPlayerPresenter.Message);
            Refresh();
        }

        private void External(System.Action action)
        {
            try { action(); }
            catch (Exception ex) { Fail(Text.STATUS_ACTION_FAILED, ex); }
        }

        private void Fail(string text, Exception exception)
        {
            DeliveryTemperatureOptionsStore.Log(text, exception);
            message = text;
            Refresh();
        }

        private void Refresh()
        {
            SetVisible(helpPanel, helpOpen && !discardOpen);
            SetVisible(legacyPanel, session?.NeedsDisambiguation == true && !discardOpen);
            SetVisible(settingsPanel, !discardOpen);
            SetVisible(footerPanel, !saved && !discardOpen);
            SetVisible(savedPanel, saved && !discardOpen);
            SetVisible(discardPanel, discardOpen);
            SetLabel(messageLabel, message);
            foreach (System.Action update in refreshControls) update();
            if (screen != null && screen.transform is RectTransform rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        private static PPanel Column(string name) => new PPanel(name)
        {
            Direction = PanelDirection.Vertical, Alignment = TextAnchor.UpperLeft,
            Spacing = 8, FlexSize = Vector2.right
        };

        private PLabel Heading(string text) => Label(text, obj =>
        {
            TMP_Text? heading = obj.GetComponentInChildren<TMP_Text>();
            if (heading != null) heading.fontStyle |= FontStyles.Bold;
            SizeLabel(obj, contentWidth);
        });

        private PLabel Label(string text, System.Action<GameObject>? realized = null, float? width = null)
        {
            // PLib does not create a text component for an empty initial string.
            var label = new PLabel { Text = string.IsNullOrEmpty(text) ? " " : text,
                TextStyle = PUITuning.Fonts.UILightStyle, DynamicSize = true,
                TextAlignment = TextAnchor.UpperLeft, FlexSize = Vector2.right };
            label.AddOnRealize(obj => { SizeLabel(obj, width ?? contentWidth); realized?.Invoke(obj); });
            return label;
        }

        private PButton Button(string name, string label, System.Action action, string tooltip = "",
            bool primary = false, Func<bool>? enabled = null)
        {
            Func<bool> available = () => !reporting && !closeAfterKeyRelease && (enabled?.Invoke() ?? true);
            System.Action invoke = () => { if (available()) action(); };
            var button = new PButton(name) { Text = label, ToolTip = tooltip,
                Color = primary ? PUITuning.Colors.ButtonPinkStyle : PUITuning.Colors.ButtonBlueStyle,
                TextStyle = PUITuning.Fonts.UILightStyle, DynamicSize = true,
                Margin = new RectOffset(10, 10, 6, 6),
                OnClick = _ => invoke() };
            button.AddOnRealize(obj =>
            {
                float width = name == "Save" || name == "Cancel" ? (contentWidth - 12) / 2 : contentWidth;
                SizeButton(obj, width);
                refreshControls.Add(() => PButton.SetButtonEnabled(obj, available()));
                focus.Add(obj, invoke, available);
                if (name == "HelpToggle" || name == "Later") refreshControls.Add(() =>
                {
                    PUIElements.SetText(obj, name == "HelpToggle" ?
                        (helpOpen ? Text.BUTTON_COLLAPSE_HELP : Text.BUTTON_EXPAND_HELP) :
                        (session?.RestartPending == true ? Text.BUTTON_RESTART_LATER : Text.BUTTON_DONE));
                    SizeButton(obj, width);
                });
            });
            return button;
        }

        private PPanel Check(string name, string label, Func<bool> value, System.Action toggle, Func<bool> enabled)
        {
            var row = new PPanel(name + "Row") { Direction = PanelDirection.Horizontal,
                Spacing = 10, FlexSize = Vector2.right };
            GameObject? checkObject = null;
            System.Action invoke = () => { if (enabled()) { toggle(); Refresh(); } };
            row.AddChild(Label(label, obj =>
            {
                TMP_Text? text = obj.GetComponentInChildren<TMP_Text>();
                if (text != null) text.raycastTarget = true;
                var trigger = obj.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(_ => { focus.Focus(checkObject); invoke(); });
                trigger.triggers.Add(entry);
            }, contentWidth - 48));
            var check = new PCheckBox { OnChecked = (_, state) => invoke() }.SetKleiBlueStyle();
            check.AddOnRealize(obj =>
            {
                checkObject = obj;
                CanvasGroup group = obj.AddComponent<CanvasGroup>();
                refreshControls.Add(() =>
                {
                    group.interactable = enabled();
                    group.blocksRaycasts = enabled();
                    group.alpha = enabled() ? 1 : 0.55f;
                    PCheckBox.SetCheckState(obj, value() ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
                });
                focus.Add(obj, invoke, enabled);
            });
            row.AddChild(check);
            return row;
        }

        private void SetLabel(GameObject? obj, string text)
        {
            if (obj == null) return;
            PUIElements.SetText(obj, text);
            SizeLabel(obj, contentWidth);
        }

        private static void SizeLabel(GameObject obj, float width) => SizeText(obj, width, 0, 0, true);

        private static void SizeButton(GameObject obj, float maximum)
        {
            TMP_Text? text = obj.GetComponentInChildren<TMP_Text>();
            if (text != null) SizeText(obj, Math.Min(maximum,
                text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x + 20),
                10, 6, false);
        }

        private static void SizeText(GameObject obj, float width, float horizontalPadding,
            float verticalPadding, bool flexible)
        {
            TMP_Text? text = obj.GetComponentInChildren<TMP_Text>();
            if (text == null) return;
            // This wrapper owns text geometry. Disable PLib's intrinsic-width
            // layout controller so a long translation cannot expand the dialog.
            foreach (Behaviour component in obj.GetComponents<Behaviour>())
                if (component is ILayoutController) component.enabled = false;
            #pragma warning disable CS0618
            text.enableWordWrapping = true;
#pragma warning restore CS0618
            text.richText = false;
            text.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            LayoutElement layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
            layout.layoutPriority = 10;
            layout.minWidth = 0;
            layout.preferredWidth = width;
            layout.flexibleWidth = flexible ? 1 : 0;
            layout.preferredHeight = text.GetPreferredValues(text.text,
                Math.Max(1, width - 2 * horizontalPadding), float.PositiveInfinity).y + 2 * verticalPadding;
        }

        private static void SetVisible(GameObject? obj, bool visible)
        {
            if (obj == null) return;
            obj.transform.localScale = visible ? Vector3.one : Vector3.zero;
            CanvasGroup group = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (obj.transform is RectTransform rect) LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        private static Vector2 GetAvailableSize()
        {
            GameObject parent = PDialog.GetParentObject() ??
                throw new InvalidOperationException("No ONI dialog parent is available.");
            Canvas? canvas = parent.GetComponentInParent<Canvas>();
            RectTransform? rect = canvas != null ? canvas.rootCanvas.transform as RectTransform :
                parent.transform as RectTransform;
            float width = rect != null ? rect.rect.width : Screen.width;
            float height = rect != null ? rect.rect.height : Screen.height;
            if (width < 360 || height < 300)
                throw new InvalidOperationException("The available UI viewport is too small for this editor.");
            return new Vector2(Math.Min(680, width - 48), Math.Min(720, height - 48));
        }
    }
}