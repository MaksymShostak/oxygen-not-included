#nullable enable

using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private const float BodyFontSize = 18;
        private const float HeadingFontSize = 20;
        private readonly SupportReportSession reportSession = new SupportReportSession();
        private readonly DeliveryTemperatureOptionsStore store = new DeliveryTemperatureOptionsStore();
        private readonly List<System.Action<object>> closeCallbacks = new List<System.Action<object>>();
        private readonly GameObject? previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        private readonly List<System.Action> refreshControls = new List<System.Action>();
        private OptionsEditSession? session;
        private KScreen? screen;
        private OptionsDialogLifetime? lifetime;
        private OptionsFocusNavigation focus = new OptionsFocusNavigation();
        private GameObject? lowInput, highInput, messageLabel, supportLabel, reportDetailsLabel;
        private GameObject? contentsPanel, settingsPanel, reportPanel, rangePanel, discardPanel, footerPanel, legacyPanel, savedPanel, reportFooter;
        private GameObject? keepEditingButton, cancelButton, reportButton, includeLogControl;
        private ScrollRect? contentScroll;
        private float settingsScrollPosition = 1;
        private bool reportOpen, reportPrepared, discardOpen, reporting, saved, updatingInputs, refreshing;
        private bool handlingKeyboard, closeAfterKeyRelease, closeScheduled, ended;
        private int lastKeyFrame = -1;
        private float contentWidth;
        private string message = "";
        private string reportMessage = "";

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
            // Reserve both the native dialog/body margins and a scrollbar gutter.
            // Only the scrollable body yields space; footer controls keep their measured size.
            contentWidth = maximum.x - 64;
            var dialog = new PDialog("DeliveryTemperatureOptions")
            {
                Title = Text.DIALOG_TITLE,
                Size = new Vector2(maximum.x, 0),
                MaxSize = maximum, SortKey = 150,
                DialogBackColor = PUITuning.Colors.OptionsBackground,
                RoundToNearestEven = true
            };
            PPanel body = dialog.Body;
            body.Direction = PanelDirection.Vertical;
            body.Alignment = TextAnchor.UpperLeft;
            body.Margin = new RectOffset(12, 12, 12, 12);
            body.Spacing = 10;
            var contents = Column("Contents");
            contents.Margin = new RectOffset(3, 17, 3, 8);
            contents.AddOnRealize(obj => contentsPanel = obj);
            var settings = Column("SettingsView");
            settings.Spacing = 24;
            settings.AddOnRealize(obj => settingsPanel = obj);
            var introduction = Column("SettingsIntroduction");
            introduction.AddChild(HelpLabel("ScopeHelp", Text.LABEL_SETTINGS_SCOPE, Text.DIALOG_INTRO));
            introduction.AddChild(Description(Text.RESTART_NOTICE, obj => refreshControls.Add(() =>
                SetLabel(obj, session?.RestartPending == true ? Text.PENDING_RESTART_NOTICE : Text.RESTART_NOTICE))));
            settings.AddChild(introduction);
            if (session != null) AddSettings(settings, session);
            AddSupport(settings);
            contents.AddChild(settings);
            AddReportView(contents);
            var scroll = new PScrollPane
            {
                Child = contents, ScrollHorizontal = false, ScrollVertical = true,
                AlwaysShowHorizontal = false, AlwaysShowVertical = false,
                FlexSize = Vector2.one, TrackSize = 12
            };
            scroll.AddOnRealize(obj => contentScroll = obj.GetComponent<ScrollRect>());
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
                if (session?.LimitConstruction == true && !RangeIsValid) Validate(false);
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
            legacy.AddOnRealize(obj => { legacyPanel = obj; SetVisible(obj, edit.NeedsDisambiguation && edit.LimitConstruction); });
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
                }, maximumWidth: (contentWidth - 16) / 3));
            }
            legacy.AddChild(legacyRow);
            parent.AddChild(legacy);
            var construction = Column("ConstructionSettings");
            construction.AddChild(Heading(Text.SECTION_CONSTRUCTION));
            construction.AddChild(Check("Construction", Text.CHECKBOX_LIMIT_CONSTRUCTION, () => edit.LimitConstruction, () =>
            {
                edit.LimitConstruction = !edit.LimitConstruction;
                Validate(false);
            }, () => CanEdit));
            var range = Column("ConstructionDefaults");
            range.AddOnRealize(obj => { rangePanel = obj; SetVisible(obj, edit.LimitConstruction); });
            range.Margin = new RectOffset(0, 0, 8, 0);
            range.AddChild(HelpLabel("RangeHelp", Text.LABEL_DEFAULT_CONSTRUCTION_RANGE,
                Text.TOOLTIP_DEFAULT_CONSTRUCTION_RANGE));
            var temperatures = new PPanel("TemperatureRange") { Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.UpperLeft, Spacing = 16, FlexSize = Vector2.right };
            AddInput(temperatures, "Lower", STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.LOWER_BOUND,
                true, obj => lowInput = obj);
            AddInput(temperatures, "Upper", STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.UPPER_BOUND,
                false, obj => highInput = obj);
            range.AddChild(temperatures);
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
            construction.AddChild(range);
            parent.AddChild(construction);
            var warnings = Column("ResourceWarnings");
            warnings.AddChild(Heading(Text.SECTION_RESOURCE_WARNINGS));
            warnings.AddChild(Check("Warnings", Text.CHECKBOX_WARN_ON_BLOCKED, () => edit.CheckWarnings,
                () => { edit.CheckWarnings = !edit.CheckWarnings; Refresh(); }, () => CanEdit,
                Text.TOOLTIP_WARN_ON_BLOCKED));
            SupportRuntimeSnapshot runtime = DeliveryTemperatureRuntimePatchInstaller.CaptureSupportReportSnapshot();
            if (runtime.StatusCompatibilityDiagnostic != null)
                warnings.AddChild(Label(Text.NOTICE_RESOURCE_WARNINGS_UNAVAILABLE));
            parent.AddChild(warnings);
        }

        private void AddInput(PPanel parent, string name, string label, bool lower,
            System.Action<GameObject> realized)
        {
            float width = (contentWidth - 16) / 2;
            var fieldGroup = Column(name + "Field");
            fieldGroup.AddChild(Label(label, null, width));
            var row = new PPanel(name + "Row") { Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleLeft, Spacing = 8, FlexSize = Vector2.right };
            var input = new PTextField(name) { Type = PTextField.FieldType.Integer,
                MinWidth = (int)Math.Min(80, width - 40), FlexSize = Vector2.right, OnTextChanged = (_, text) =>
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
                field.pointSize = BodyFontSize;
                field.textComponent.enableAutoSizing = false;
                updatingInputs = true;
                try { field.text = lower ? session!.Range.LowerText : session!.Range.UpperText; }
                finally { updatingInputs = false; }
                field.onEndEdit.AddListener(_ => { if (!updatingInputs) Validate(false); });
                var layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
                layout.layoutPriority = 10;
                layout.minHeight = layout.preferredHeight = 36;
                layout.preferredWidth = width - 40;
                refreshControls.Add(() => SetInputEnabled(field, CanEditRange));
                focus.Add(obj, () => field.ActivateInputField(), () => CanEditRange);
            });
            row.AddChild(input);
            row.AddChild(Label(OptionsTemperatureUnits.Symbol(session!.DisplayUnit), null, 32, false));
            fieldGroup.AddChild(row);
            parent.AddChild(fieldGroup);
        }

        private static void SetInputEnabled(TMP_InputField field, bool enabled)
        {
            field.interactable = enabled;
            CanvasGroup group = field.GetComponent<CanvasGroup>() ?? field.gameObject.AddComponent<CanvasGroup>();
            group.alpha = enabled ? 1 : 0.45f;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        private void AddSupport(PPanel parent)
        {
            float actionWidth = (contentWidth - 12) / 2;
            var support = Column("Support");
            support.AddChild(Heading(Text.SECTION_SUPPORT));
            var links = ActionRow("SupportLinks");
            links.Alignment = TextAnchor.MiddleLeft;
            var report = Button("ReportBug", Text.BUTTON_REPORT_BUG, ShowReport, maximumWidth: actionWidth);
            report.AddOnRealize(obj => reportButton = obj);
            links.AddChild(report);
            links.AddChild(Button("Homepage", Text.BUTTON_OPEN_HOMEPAGE, () => External(() => Application.OpenURL(
                "https://steamcommunity.com/sharedfiles/filedetails/?id=3759075866")), maximumWidth: actionWidth));
            support.AddChild(links);
            parent.AddChild(support);
        }

        private void AddReportView(PPanel parent)
        {
            float actionWidth = (contentWidth - 12) / 2;
            var reports = Column("ReportView");
            reports.Spacing = 24;
            reports.AddOnRealize(obj => { reportPanel = obj; SetVisible(obj, reportOpen); });
            reports.AddChild(Description(Text.NOTICE_ISSUE_FORM));
            var prepare = Column("PrepareReport");
            prepare.Spacing = 12;
            prepare.AddChild(Check("IncludeLog", Text.CHECKBOX_INCLUDE_PLAYER_LOG, () => reportSession.IncludeGameLog,
                () => { reportSession.IncludeGameLog = !reportSession.IncludeGameLog; Refresh(); }, () => !reporting,
                Text.TOOLTIP_INCLUDE_PLAYER_LOG));
            prepare.AddChild(Button("ContinueToGitHub", Text.BUTTON_OPEN_ISSUE_FORM,
                () => CreateReport(true), primary: true));
            reports.AddChild(prepare);
            var result = Column("ReportResult");
            result.Spacing = 12;
            result.AddOnRealize(obj => refreshControls.Add(() =>
                SetVisible(obj, reportPrepared || !string.IsNullOrEmpty(reportMessage))));
            result.AddChild(Description(reportMessage, obj => supportLabel = obj));
            var attachment = Button("AttachReport", Text.BUTTON_OPEN_LAST_REPORT_FOLDER,
                () => SupportAction(SupportReportPlayerPresenter.OpenLastReportFolder),
                enabled: () => SupportReportPlayerPresenter.HasReport);
            attachment.AddOnRealize(obj => refreshControls.Add(() => SetVisible(obj, reportPrepared)));
            result.AddChild(attachment);
            var details = Column("ReportDetails");
            details.AddChild(Description(" ", obj => reportDetailsLabel = obj));
            var detailDisclosure = Disclosure("ReportDetails", Text.SECTION_REPORT_DETAILS, details);
            detailDisclosure.AddOnRealize(obj => refreshControls.Add(() => SetVisible(obj, reportPrepared)));
            result.AddChild(detailDisclosure);
            reports.AddChild(result);
            var advanced = Column("AdvancedTroubleshooting");
            advanced.Spacing = 12;
            advanced.AddChild(Button("ConfigurationFolder", Text.BUTTON_OPEN_CONFIG_FOLDER, () => External(() =>
            {
                string directory = Path.GetDirectoryName(store.Path) ??
                    throw new InvalidOperationException("Configuration directory is unavailable.");
                Directory.CreateDirectory(directory);
                Application.OpenURL(new Uri(directory).AbsoluteUri);
            }), Text.TOOLTIP_CONFIG_FOLDER, maximumWidth: actionWidth));
            advanced.AddChild(Button("CreateReport", Text.BUTTON_CREATE_REPORT, () => CreateReport(false),
                Text.TOOLTIP_SUPPORT_REPORT));
            var reportActions = ActionRow("ReportActions");
            reportActions.Alignment = TextAnchor.MiddleLeft;
            reportActions.AddChild(Button("ReportFolder", Text.BUTTON_OPEN_LAST_REPORT_FOLDER,
                () => SupportAction(SupportReportPlayerPresenter.OpenLastReportFolder),
                enabled: () => SupportReportPlayerPresenter.HasReport && !reporting, maximumWidth: actionWidth));
            reportActions.AddChild(Button("CopySummary", Text.BUTTON_COPY_REPORT_SUMMARY,
                () => SupportAction(SupportReportPlayerPresenter.CopyLastReportSummary),
                enabled: () => SupportReportPlayerPresenter.HasReport && !reporting, maximumWidth: actionWidth));
            advanced.AddChild(reportActions);
            reports.AddChild(Disclosure("AdvancedTroubleshooting", Text.SECTION_ADVANCED_TROUBLESHOOTING, advanced));
            parent.AddChild(reports);
        }

        private void ShowReport()
        {
            if (reporting || reportOpen) return;
            settingsScrollPosition = contentScroll?.verticalNormalizedPosition ?? 1;
            reportOpen = true;
            Refresh();
            if (contentScroll != null)
            {
                contentScroll.StopMovement();
                contentScroll.verticalNormalizedPosition = 1;
            }
            focus.Focus(includeLogControl);
        }

        private void BackToOptions()
        {
            if (reporting || !reportOpen) return;
            reportOpen = false;
            Refresh();
            if (contentScroll != null)
            {
                contentScroll.StopMovement();
                contentScroll.verticalNormalizedPosition = settingsScrollPosition;
            }
            focus.Focus(reportButton);
        }

        private void AddFooter(PPanel body)
        {
            var back = ActionRow("ReportNavigation");
            back.Alignment = TextAnchor.MiddleLeft;
            back.Margin = new RectOffset(0, 0, 14, 0);
            back.AddChild(Button("BackToOptions", Text.BUTTON_BACK_TO_OPTIONS, BackToOptions));
            back.AddOnRealize(obj => { reportFooter = obj; SetVisible(obj, reportOpen); });
            body.AddChild(back);
            var confirm = Column("DiscardConfirmation");
            confirm.Margin = new RectOffset(12, 12, 12, 12);
            confirm.BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
            confirm.AddOnRealize(obj => { discardPanel = obj; SetVisible(obj, discardOpen); });
            confirm.AddChild(Label(Text.DIALOG_DISCARD_TITLE, null, contentWidth - 24));
            var confirmActions = ActionRow("DiscardActions");
            var keep = Button("KeepEditing", Text.BUTTON_CANCEL_DISCARD,
                KeepEditing, maximumWidth: (contentWidth - 36) / 2);
            keep.AddOnRealize(obj => keepEditingButton = obj);
            confirmActions.AddChild(keep);
            confirmActions.AddChild(Button("Discard", Text.BUTTON_CONFIRM_DISCARD, Close,
                maximumWidth: (contentWidth - 36) / 2));
            confirm.AddChild(confirmActions);
            body.AddChild(confirm);
            var footer = Column("Actions");
            footer.Margin = new RectOffset(0, 0, 14, 0);
            footer.AddOnRealize(obj => { footerPanel = obj; SetVisible(obj, !saved); });
            if (session != null)
                footer.AddChild(Button("Defaults", Text.BUTTON_RESTORE_DEFAULTS, () =>
                {
                    session.RestoreDefaults(); ResetInputText(); message = ""; Refresh();
                }, Text.TOOLTIP_RESTORE_DEFAULTS));
            var actions = ActionRow("SaveAndCancel");
            var cancel = Button("Cancel", Text.BUTTON_CANCEL, RequestClose,
                maximumWidth: (contentWidth - 12) / 2);
            cancel.AddOnRealize(obj => cancelButton = obj);
            actions.AddChild(cancel);
            if (session != null) actions.AddChild(Button("Save", Text.BUTTON_SAVE, Save, primary: true,
                maximumWidth: (contentWidth - 12) / 2));
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

        private static PPanel ActionRow(string name) => new PPanel(name)
        {
            Direction = PanelDirection.Horizontal, Spacing = 12,
            FlexSize = Vector2.right, Alignment = TextAnchor.MiddleRight
        };

        private void KeepEditing()
        {
            discardOpen = false;
            Refresh();
            focus.Focus(cancelButton);
        }

        private bool CanEdit => session != null &&
            !saved && !discardOpen && !reportOpen && !reporting && !closeAfterKeyRelease;
        private bool RangeIsValid => session != null &&
            session.Range.Validate(out _, out _) == ConstructionRangeError.None;
        private bool CanEditRange => CanEdit && session!.LimitConstruction;

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
            }
            finally { updatingInputs = false; }
        }

        private ConstructionRangeError Validate(bool focusError)
        {
            if (session == null) return ConstructionRangeError.None;
            ConstructionRangeError error = session.Range.ResolveForSave(session.LimitConstruction, out _, out _);
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
            if (reportOpen) { BackToOptions(); return; }
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
                    if (discardOpen) KeepEditing();
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

        private void CreateReport(bool openIssueForm)
        {
            if (reporting || lifetime == null) return;
            reporting = true;
            reportMessage = Text.STATUS_CREATING_REPORT;
            Refresh();
            lifetime.StartCoroutine(CreateReportAfterRepaint(openIssueForm));
        }

        private IEnumerator CreateReportAfterRepaint(bool openIssueForm)
        {
            // Keep Unity snapshot capture on the main thread, but paint feedback
            // before bounded file/log work. This is not an asynchronous upload.
            yield return null;
            try
            {
                bool created = reportSession.Create(openIssueForm, kind => kind == SupportReportKind.ExtendedPlayerLog ?
                    DeliveryTemperatureSupportReporter.CreateExtendedReport() :
                    DeliveryTemperatureSupportReporter.CreateStandardReport(),
                    SupportReportPlayerPresenter.OpenIssueForm);
                reportPrepared |= created;
                reportMessage = SupportReportPlayerPresenter.Message;
            }
            catch (Exception ex)
            {
                SupportReportPlayerPresenter.PresentFailure(Text.STATUS_REPORT_FAILED, ex);
                reportMessage = SupportReportPlayerPresenter.Message;
            }
            finally { reporting = false; Refresh(); }
        }

        private void SupportAction(System.Action action)
        {
            action();
            reportMessage = SupportReportPlayerPresenter.Message;
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
            if (reportOpen) reportMessage = text;
            else message = text;
            Refresh();
        }

        private void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                SetVisible(settingsPanel, !reportOpen);
                SetVisible(reportPanel, reportOpen);
                SetVisible(rangePanel, session?.LimitConstruction == true);
                SetVisible(legacyPanel, session?.NeedsDisambiguation == true && session.LimitConstruction);
                // Keep the form visible as context while the footer asks for confirmation.
                if (contentsPanel != null)
                {
                    CanvasGroup group = contentsPanel.GetComponent<CanvasGroup>() ??
                        contentsPanel.AddComponent<CanvasGroup>();
                    group.alpha = discardOpen ? 0.45f : 1;
                    group.interactable = !discardOpen;
                    group.blocksRaycasts = !discardOpen;
                }
                SetVisible(footerPanel, !reportOpen && !saved && !discardOpen);
                SetVisible(savedPanel, !reportOpen && saved && !discardOpen);
                SetVisible(reportFooter, reportOpen);
                SetVisible(discardPanel, discardOpen);
                SetLabel(messageLabel, message);
                SetVisible(messageLabel, !reportOpen && !string.IsNullOrEmpty(message) && !discardOpen);
                SetLabel(supportLabel, reportMessage);
                SetVisible(supportLabel, !string.IsNullOrEmpty(reportMessage));
                SetLabel(reportDetailsLabel, SupportReportPlayerPresenter.ReportDetails);
                Transform? title = screen?.transform.Find("Title");
                if (title != null) PUIElements.SetText(title.gameObject,
                    reportOpen ? Text.DIALOG_REPORT_TITLE : Text.DIALOG_TITLE);
                foreach (System.Action update in refreshControls) update();
                RefreshLayout();
            }
            finally { refreshing = false; }
        }

        private void RefreshLayout()
        {
            if (screen == null || !(screen.transform is RectTransform rect)) return;
            // The shell keeps the size chosen when it opened. Disclosures and view
            // navigation only change the content inside its scrolling viewport.
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private static PPanel Column(string name) => new PPanel(name)
        {
            Direction = PanelDirection.Vertical, Alignment = TextAnchor.UpperLeft,
            Spacing = 8, FlexSize = Vector2.right
        };

        private PLabel Heading(string text) => Label(text, obj =>
        {
            TMP_Text? heading = obj.GetComponentInChildren<TMP_Text>();
            if (heading != null)
            {
                heading.fontStyle |= FontStyles.Bold;
                heading.fontSize = HeadingFontSize;
            }
            SizeLabel(obj, contentWidth);
        });

        private PLabel Description(string text, System.Action<GameObject>? realized = null) => Label(text, obj =>
        {
            TMP_Text? description = obj.GetComponentInChildren<TMP_Text>();
            if (description != null)
            {
                description.color = new Color(0.88f, 0.89f, 0.92f);
            }
            SizeLabel(obj, contentWidth);
            realized?.Invoke(obj);
        });

        private PLabel Label(string text, System.Action<GameObject>? realized = null, float? width = null,
            bool flexible = true)
        {
            // PLib does not create a text component for an empty initial string.
            var label = new PLabel { Text = string.IsNullOrEmpty(text) ? " " : text,
                TextStyle = PUITuning.Fonts.UILightStyle, DynamicSize = true,
                TextAlignment = TextAnchor.UpperLeft, FlexSize = flexible ? Vector2.right : Vector2.zero };
            label.AddOnRealize(obj =>
            {
                SetBodyFont(obj);
                SizeText(obj, width ?? contentWidth, 0, 0, flexible);
                realized?.Invoke(obj);
            });
            return label;
        }

        private PButton Button(string name, string label, System.Action action, string tooltip = "",
            bool primary = false, Func<bool>? enabled = null, float? maximumWidth = null)
        {
            Func<bool> available = () => !reporting && !closeAfterKeyRelease &&
                (!discardOpen || name == "KeepEditing" || name == "Discard") && (enabled?.Invoke() ?? true);
            System.Action invoke = () => { if (available()) action(); };
            var button = new PButton(name) { Text = label, ToolTip = tooltip,
                Color = primary ? PUITuning.Colors.ButtonPinkStyle : PUITuning.Colors.ButtonBlueStyle,
                TextStyle = PUITuning.Fonts.UILightStyle, DynamicSize = true,
                Margin = new RectOffset(10, 10, 6, 6),
                OnClick = _ => invoke() };
            button.AddOnRealize(obj =>
            {
                SetBodyFont(obj);
                float width = maximumWidth ?? contentWidth;
                SizeButton(obj, width);
                refreshControls.Add(() => PButton.SetButtonEnabled(obj, available()));
                focus.Add(obj, invoke, available);
                if (name == "Later") refreshControls.Add(() =>
                {
                    TMP_Text? text = obj.GetComponentInChildren<TMP_Text>(true);
                    if (text != null) text.text =
                        (session?.RestartPending == true ? Text.BUTTON_RESTART_LATER : Text.BUTTON_DONE);
                    SizeButton(obj, width);
                });
            });
            return button;
        }

        private PPanel HelpLabel(string name, string label, string help)
        {
            var row = new PPanel(name + "Row") { Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleLeft, Spacing = 10, FlexSize = Vector2.right };
            row.AddChild(Label(label, obj => SizeCompactLabel(obj, contentWidth - 42),
                contentWidth - 42, false));
            return WithHelp(name, row, help);
        }

        private PPanel Disclosure(string name, string label, PPanel content)
        {
            bool expanded = false;
            GameObject? region = null;
            var section = Column(name + "Disclosure");
            section.Spacing = 12;
            var header = Button(name + "Header", label, () =>
            {
                expanded = !expanded;
                SetVisible(region, expanded);
                Refresh();
            });
            header.Sprite = PUITuning.Images.Arrow;
            header.SpriteSize = new Vector2(16, 16);
            header.FlexSize = Vector2.right;
            header.AddOnRealize(obj =>
            {
                // Own both children after replacing PLib's intrinsic text layout.
                SizeText(obj, contentWidth - 28, 12, 6, true);
                LayoutElement layout = obj.GetComponent<LayoutElement>();
                layout.preferredWidth = contentWidth;
                TMP_Text text = obj.GetComponentInChildren<TMP_Text>(true);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.rectTransform.offsetMin = new Vector2(40, 6);
                text.rectTransform.offsetMax = new Vector2(-12, -6);
                foreach (Image icon in obj.GetComponentsInChildren<Image>(true))
                {
                    if (icon.sprite != PUITuning.Images.Arrow) continue;
                    RectTransform rect = icon.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(16, 16);
                    rect.anchoredPosition = new Vector2(18, 0);
                    refreshControls.Add(() => rect.localRotation =
                        Quaternion.Euler(0, 0, expanded ? -90 : 0));
                }
            });
            section.AddChild(header);
            content.AddOnRealize(obj => { region = obj; SetVisible(obj, false); });
            section.AddChild(content);
            return section;
        }

        private PPanel WithHelp(string name, PPanel row, string help)
        {
            bool expanded = false;
            GameObject? detail = null;
            var section = Column(name + "Help");
            row.AddChild(Button(name + "HelpButton", "?", () =>
            {
                expanded = !expanded;
                SetVisible(detail, expanded);
                Refresh();
            }, help, maximumWidth: 32));
            section.AddChild(row);
            section.AddChild(Description(help, obj => { detail = obj; SetVisible(obj, false); }));
            return section;
        }

        private PPanel Check(string name, string label, Func<bool> value, System.Action toggle,
            Func<bool> enabled, string help = "")
        {
            var row = new PPanel(name + "Row") { Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleLeft, Spacing = 10, FlexSize = Vector2.right };
            GameObject? checkObject = null;
            Func<bool> available = () => !discardOpen && !reporting && !closeAfterKeyRelease && enabled();
            System.Action invoke = () => { if (available()) { toggle(); Refresh(); } };
            var caption = Label(label, obj =>
            {
                if (help.Length > 0) SizeCompactLabel(obj, contentWidth - 82);
                TMP_Text? text = obj.GetComponentInChildren<TMP_Text>();
                if (text != null) text.raycastTarget = true;
                var trigger = obj.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(_ => { focus.Focus(checkObject); invoke(); });
                trigger.triggers.Add(entry);
            }, contentWidth - 40 - (help.Length > 0 ? 42 : 0), help.Length == 0);
            // Reserve space around the 24-unit box for the keyboard focus ring.
            var check = new PCheckBox { CheckSize = new Vector2(20, 20),
                Margin = new RectOffset(3, 3, 3, 3),
                OnChecked = (_, state) => invoke() }.SetKleiBlueStyle();
            check.AddOnRealize(obj =>
            {
                checkObject = obj;
                if (name == "IncludeLog") includeLogControl = obj;
                CanvasGroup group = obj.AddComponent<CanvasGroup>();
                refreshControls.Add(() =>
                {
                    group.interactable = available();
                    group.blocksRaycasts = available();
                    group.alpha = available() ? 1 : 0.55f;
                    PCheckBox.SetCheckState(obj, value() ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
                });
                focus.Add(obj, invoke, available);
            });
            row.AddChild(check);
            row.AddChild(caption);
            return help.Length > 0 ? WithHelp(name, row, help) : row;
        }

        private static void SetBodyFont(GameObject obj)
        {
            TMP_Text? text = obj.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;
            text.enableAutoSizing = false;
            text.fontSize = BodyFontSize;
        }

        private void SetLabel(GameObject? obj, string text)
        {
            if (obj == null) return;
            TMP_Text? label = obj.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.text = text;
            SizeLabel(obj, contentWidth);
        }

        private static void SizeLabel(GameObject obj, float width) => SizeText(obj, width, 0, 0, true);

        private static void SizeCompactLabel(GameObject obj, float maximum)
        {
            TMP_Text? text = obj.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;
            float natural = (float)Math.Ceiling(text.GetPreferredValues(text.text,
                float.PositiveInfinity, float.PositiveInfinity).x) + 2;
            SizeText(obj, Math.Min(maximum, natural), 0, 0, false);
        }

        private static void SizeButton(GameObject obj, float maximum)
        {
            TMP_Text? text = obj.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;
            // Round up and allow for TMP's fractional glyph advances so an exact
            // preferred width cannot accidentally wrap a single-line caption.
            float natural = (float)Math.Ceiling(text.GetPreferredValues(text.text,
                float.PositiveInfinity, float.PositiveInfinity).x) + 22;
            SizeText(obj, Math.Min(maximum, natural), 10, 6, false);
            if (natural <= maximum)
            {
#pragma warning disable CS0618
                text.enableWordWrapping = false;
#pragma warning restore CS0618
                LayoutElement layout = obj.GetComponent<LayoutElement>();
                layout.minHeight = layout.preferredHeight = 36;
            }
        }

        private static void SizeText(GameObject obj, float width, float horizontalPadding,
            float verticalPadding, bool flexible)
        {
            TMP_Text? text = obj.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;
            // PLib directly calls ILayoutController even on disabled Behaviours.
            // Remove the intrinsic-width controller before owning this wrapper's geometry.
            foreach (Behaviour component in obj.GetComponents<Behaviour>())
                if (component is ILayoutController) UnityEngine.Object.DestroyImmediate(component);
#pragma warning disable CS0618
            text.enableWordWrapping = true;
#pragma warning restore CS0618
            text.richText = false;
            text.alignment = horizontalPadding > 0 ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            text.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            LayoutElement layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
            layout.layoutPriority = 10;
            layout.minWidth = flexible ? 0 : width;
            layout.preferredWidth = width;
            layout.flexibleWidth = flexible ? 1 : 0;
            layout.preferredHeight = text.GetPreferredValues(text.text,
                Math.Max(1, width - 2 * horizontalPadding), float.PositiveInfinity).y + 2 * verticalPadding;
            if (horizontalPadding > 0) layout.preferredHeight = Math.Max(36, layout.preferredHeight);
            layout.minHeight = layout.preferredHeight;
            layout.flexibleHeight = 0;
        }

        private static void SetVisible(GameObject? obj, bool visible)
        {
            if (obj == null) return;
            if (obj.activeSelf == visible) return;
            obj.SetActive(visible);
            if (obj.transform.parent is RectTransform rect) LayoutRebuilder.MarkLayoutForRebuild(rect);
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
