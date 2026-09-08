#nullable enable

using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SideScreenStrings = STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Displays and edits temperature limits using the player's active ONI
    /// display unit while keeping Kelvin as the canonical simulation storage.
    /// Provides explicit bounds naming, unit labels, contextual help and validation,
    /// draft-based editing, and an explicit Clear action.
    /// </summary>
    internal sealed class TemperatureLimitWidget : KMonoBehaviour
    {
        private GameObject? lowInput;
        private GameObject? highInput;
        private TMP_InputField? lowField;
        private TMP_InputField? highField;
        private GameObject? lowUnitLabel;
        private GameObject? highUnitLabel;
        private GameObject? clearButton;
        private GameObject? statusLabel;
        private TMP_Text? statusText;
        private TemperatureRangeTextLayout? statusLayout;
        private TemperatureRangeHelpScreen? helpScreen;
        private Selectable? clearSelectable;
        private Outline? clearFocusOutline;

        private TemperatureLimit? target;
        private string? lowDraft;
        private string? highDraft;
        private bool isUpdatingInputs;

        internal void SetTarget(TemperatureLimit? newTarget)
        {
            helpScreen?.ResetHelp();
            target = newTarget;
            lowDraft = null;
            highDraft = null;
            UpdateInputs();
        }

        internal bool IsAnyFieldFocused() =>
            (lowField != null && lowField.isFocused) ||
            (highField != null && highField.isFocused);

        protected override void OnPrefabInit()
        {
            var margin = new RectOffset(4, 4, 4, 4);
            BoxLayoutGroup? baseLayout = gameObject.GetComponent<BoxLayoutGroup>();
            if (baseLayout != null)
            {
                baseLayout.Params = new BoxLayoutParams
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = margin
                };
            }

            var panel = new PPanel("MainPanel")
            {
                Direction = PanelDirection.Vertical,
                Margin = margin,
                Spacing = 4,
                FlexSize = Vector2.right
            };

            var headerPanel = new PPanel("HeaderRow")
            {
                Direction = PanelDirection.Horizontal,
                Spacing = 4,
                FlexSize = Vector2.right
            };
            var headerLabel = new PLabel("HeaderLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = SideScreenStrings.SECTION_RANGE
            };
            var headerSpacer = new PSpacer
            {
                FlexSize = Vector2.right
            };
            var helpButton = new PButton("TemperatureRangeHelp")
            {
                Text = "?",
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                OnClick = _ => helpScreen?.ToggleHelp()
            };
            helpButton.AddOnRealize(realizedHelp =>
            {
                helpScreen = realizedHelp.AddComponent<TemperatureRangeHelpScreen>();
                helpScreen.Initialize(this);
            });
            var clearBtn = new PButton("ClearButton")
            {
                Text = SideScreenStrings.BUTTON_CLEAR,
                ToolTip = SideScreenStrings.TOOLTIPS.CLEAR,
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Color = PUITuning.Colors.ButtonBlueStyle,
                Margin = new RectOffset(6, 6, 2, 2),
                OnClick = OnClearClicked
            };
            clearBtn.AddOnRealize(realizedButton =>
            {
                clearButton = realizedButton;
                clearSelectable = realizedButton.AddComponent<Selectable>();
                clearSelectable.transition = Selectable.Transition.None;
                clearSelectable.targetGraphic = realizedButton.GetComponent<Graphic>();
                clearFocusOutline = realizedButton.AddComponent<Outline>();
                clearFocusOutline.effectColor = Color.white;
                clearFocusOutline.effectDistance = new Vector2(2, -2);
                clearFocusOutline.enabled = false;
            });
            headerPanel.AddChild(headerLabel);
            headerPanel.AddChild(helpButton);
            headerPanel.AddChild(headerSpacer);
            headerPanel.AddChild(clearBtn);

            var boundsGrid = new PGridPanel("BoundsGrid");
            boundsGrid.AddRow(new GridRowSpec());
            boundsGrid.AddRow(new GridRowSpec());
            boundsGrid.AddColumn(new GridColumnSpec());
            boundsGrid.AddColumn(new GridColumnSpec(72f, 0f));
            boundsGrid.AddColumn(new GridColumnSpec());

            var labelMargin = new RectOffset(0, 6, 2, 2);
            var inputMargin = new RectOffset(0, 0, 2, 2);
            var unitMargin = new RectOffset(6, 0, 2, 2);

            var lowLabel = new PLabel("LowLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = SideScreenStrings.LOWER_BOUND
            };
            boundsGrid.AddChild(
                lowLabel,
                new GridComponentSpec(0, 0)
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = labelMargin
                });

            var lowInputField = new PTextField("lowLimit")
            {
                Type = PTextField.FieldType.Integer,
                MinWidth = 72
            };
            lowInputField.AddOnRealize(realizedInput =>
            {
                lowInput = realizedInput;
                var plibInputScreen = realizedInput.GetComponent<KScreen>();
                if (plibInputScreen != null)
                {
                    UnityEngine.Object.DestroyImmediate(plibInputScreen);
                }

                // PLib already supplies the input's Selectable. Adding a legacy
                // InputField here is rejected by Unity and returns null.
                realizedInput.AddComponent<TemperatureLimitInputScreen>();
                lowField = realizedInput.GetComponent<TMP_InputField>();
                if (lowField != null)
                {
                    lowField.onValueChanged.AddListener(text => OnLowInputChanged(realizedInput, text));
                    lowField.onEndEdit.AddListener(OnLowInputEndEdit);
                }
            });
            boundsGrid.AddChild(
                lowInputField,
                new GridComponentSpec(0, 1)
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = inputMargin
                });

            var lowUnit = new PLabel("LowUnit")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = TemperatureLimitPresenter.GetCurrentUnitSuffix()
            };
            lowUnit.AddOnRealize(realizedUnit =>
            {
                lowUnitLabel = realizedUnit;
            });
            boundsGrid.AddChild(
                lowUnit,
                new GridComponentSpec(0, 2)
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = unitMargin
                });

            var highLabel = new PLabel("HighLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = SideScreenStrings.UPPER_BOUND
            };
            boundsGrid.AddChild(
                highLabel,
                new GridComponentSpec(1, 0)
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = labelMargin
                });

            var highInputField = new PTextField("highLimit")
            {
                Type = PTextField.FieldType.Integer,
                MinWidth = 72
            };
            highInputField.AddOnRealize(realizedInput =>
            {
                highInput = realizedInput;
                var plibInputScreen = realizedInput.GetComponent<KScreen>();
                if (plibInputScreen != null)
                {
                    UnityEngine.Object.DestroyImmediate(plibInputScreen);
                }

                // Keep the PLib TMP input as this object's only Selectable.
                realizedInput.AddComponent<TemperatureLimitInputScreen>();
                highField = realizedInput.GetComponent<TMP_InputField>();
                if (highField != null)
                {
                    highField.onValueChanged.AddListener(text => OnHighInputChanged(realizedInput, text));
                    highField.onEndEdit.AddListener(OnHighInputEndEdit);
                }
            });
            boundsGrid.AddChild(
                highInputField,
                new GridComponentSpec(1, 1)
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = inputMargin
                });

            var highUnit = new PLabel("HighUnit")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = TemperatureLimitPresenter.GetCurrentUnitSuffix()
            };
            highUnit.AddOnRealize(realizedUnit =>
            {
                highUnitLabel = realizedUnit;
            });
            boundsGrid.AddChild(
                highUnit,
                new GridComponentSpec(1, 2)
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = unitMargin
                });

            var status = new PLabel("StatusLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                // Build the text child even though normal feedback is empty.
                Text = " ",
                DynamicSize = true,
                TextAlignment = TextAnchor.UpperLeft,
                FlexSize = Vector2.right
            };
            status.AddOnRealize(realizedStatus =>
            {
                statusLabel = realizedStatus;
                statusText = realizedStatus.GetComponentInChildren<TMP_Text>(true);
                statusLayout = TemperatureRangeTextLayout.Attach(realizedStatus);
            });

            panel.AddChild(headerPanel);
            panel.AddChild(boundsGrid);
            panel.AddChild(status);
            panel.AddTo(gameObject);
            ShowFeedback(null);

            base.OnPrefabInit();
            UpdateInputs();
        }

        protected override void OnDisable()
        {
            helpScreen?.ResetHelp();
            lowDraft = null;
            highDraft = null;
            if (lowField != null && lowField.isFocused)
            {
                lowField.DeactivateInputField();
            }

            if (highField != null && highField.isFocused)
            {
                highField.DeactivateInputField();
            }

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == lowInput ||
                 UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == highInput))
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            ConstructionMaterialTemperatureLimit
                .ResetConstructionMaterialTemperatureLimitToDefaultsIfOwned(
                    target);
            base.OnDisable();
        }

        private void Update()
        {
            if (clearFocusOutline != null) clearFocusOutline.enabled = IsActionButtonFocused();
            if (IsAnyFieldFocused() && Input.GetKeyDown(KeyCode.Escape) &&
                !(helpScreen?.IsHelpVisible ?? false) && !(helpScreen?.HandledEscapeThisFrame ?? false))
            {
                RevertDrafts();
            }
        }

        private void UpdateInputs()
        {
            if (isUpdatingInputs)
            {
                return;
            }

            isUpdatingInputs = true;
            try
            {
                UpdateInputsCore();
            }
            finally
            {
                isUpdatingInputs = false;
            }
        }

        private void UpdateInputsCore()
        {
            if (target == null || lowField == null || highField == null)
            {
                return;
            }

            TemperatureBounds bounds = ReadBounds(target);

            SetInputText(lowField, bounds.LowerKelvin);
            SetInputText(highField, bounds.UpperKelvin);

            string unitSuffix = TemperatureLimitPresenter.GetCurrentUnitSuffix();
            if (lowUnitLabel != null)
            {
                PUIElements.SetText(lowUnitLabel, unitSuffix);
            }

            if (highUnitLabel != null)
            {
                PUIElements.SetText(highUnitLabel, unitSuffix);
            }

            if (clearButton != null)
            {
                PButton.SetButtonEnabled(clearButton, !bounds.IsUnbounded);
                if (clearSelectable != null) clearSelectable.interactable = !bounds.IsUnbounded;
            }

            ShowFeedback(bounds.IsEqualBounds ? SideScreenStrings.VALIDATION.EMPTY_INTERVAL.ToString() : null);
            UpdateHelp(bounds);

            UpdateTooltips();
            ConfigureNavigation();
        }

        private static void SetInputText(
            TMP_InputField field,
            int? temperatureKelvin)
        {
            string displayedText = TemperatureLimitPresenter.FormatInputText(
                temperatureKelvin);
            if (!string.Equals(
                    field.text,
                    displayedText,
                    StringComparison.Ordinal))
            {
                field.text = displayedText;
            }
        }

        private void OnLowInputChanged(GameObject source, string text)
        {
            _ = source;
            if (isUpdatingInputs || target == null)
            {
                return;
            }

            lowDraft = text;
        }

        private void OnHighInputChanged(GameObject source, string text)
        {
            _ = source;
            if (isUpdatingInputs || target == null)
            {
                return;
            }

            highDraft = text;
        }

        private void OnLowInputEndEdit(string text)
        {
            lowDraft = text;
            CommitDrafts();
        }

        private void OnHighInputEndEdit(string text)
        {
            highDraft = text;
            CommitDrafts();
        }

        private void CommitDrafts()
        {
            if (isUpdatingInputs || target == null ||
                (helpScreen?.HandledEscapeThisFrame ?? false) ||
                ((helpScreen?.IsHelpVisible ?? false) && Input.GetKeyDown(KeyCode.Escape)))
            {
                return;
            }

            TemperatureBounds currentBounds = ReadBounds(target);
            string effectiveLowText = lowDraft ??
                TemperatureLimitPresenter.FormatInputText(currentBounds.LowerKelvin);
            string effectiveHighText = highDraft ??
                TemperatureLimitPresenter.FormatInputText(currentBounds.UpperKelvin);

            TemperatureValidationResult validation =
                TemperatureLimitPresenter.ValidateAndParse(
                    effectiveLowText,
                    effectiveHighText);

            if (!validation.IsValid)
            {
                ShowFeedback(validation.Message);
                return;
            }

            TemperatureBounds targetBounds = validation.Bounds;
            if (!currentBounds.Equals(targetBounds))
            {
                WriteBounds(target, targetBounds);
            }

            lowDraft = null;
            highDraft = null;
            UpdateInputs();

            if (validation.Severity == TemperatureValidationSeverity.Warning)
            {
                ShowFeedback(validation.Message);
            }
        }

        private void ShowFeedback(string? message)
        {
            if (statusLabel == null || statusText == null) return;
            // PLib.SetText only searches active children, losing the first error
            // when this optional feedback row is inactive.
            statusText.text = message ?? string.Empty;
            statusLabel.SetActive(!string.IsNullOrEmpty(message));
            statusLayout?.SetLayoutHorizontal();
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private void UpdateHelp(TemperatureBounds bounds)
        {
            // These are complete, existing localized messages, not English
            // fragments. The effective range is available on request as well.
            helpScreen?.SetDescription(string.Join("\n\n", new[]
            {
                TemperatureLimitPresenter.GetRangeDescription(bounds),
                SideScreenStrings.TOOLTIPS.STATUS.ToString(),
                SideScreenStrings.TOOLTIPS.LOWER_BOUND.ToString(),
                SideScreenStrings.TOOLTIPS.UPPER_BOUND.ToString(),
                SideScreenStrings.TOOLTIPS.CLEAR.ToString()
            }));
        }

        internal bool IsActionButtonFocused() => clearSelectable != null && clearSelectable.interactable &&
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == clearButton;

        internal void ActivateFocusedButton()
        {
            if (IsActionButtonFocused()) OnClearClicked(clearButton!);
        }

        internal bool CanNavigateFromCurrentSelection()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null) return false;
            GameObject? selected = events.currentSelectedGameObject;
            return selected == null || selected == helpScreen?.gameObject ||
                selected == clearButton || selected == lowInput || selected == highInput;
        }

        internal void MoveKeyboardFocus(bool backwards)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null || helpScreen == null || lowField == null || highField == null) return;
            var controls = new List<GameObject> { helpScreen.gameObject };
            if (clearSelectable != null && clearSelectable.interactable) controls.Add(clearButton!);
            controls.Add(lowField.gameObject);
            controls.Add(highField.gameObject);
            int index = controls.IndexOf(events.currentSelectedGameObject);
            int next = index < 0 ? (backwards ? controls.Count - 1 : 0) :
                (index + (backwards ? controls.Count - 1 : 1)) % controls.Count;
            GameObject selected = controls[next];
            events.SetSelectedGameObject(selected);
            TMP_InputField? input = selected.GetComponent<TMP_InputField>();
            if (input != null) input.ActivateInputField();
        }

        private void RevertDrafts()
        {
            lowDraft = null;
            highDraft = null;
            if (lowField != null && lowField.isFocused)
            {
                lowField.DeactivateInputField();
            }

            if (highField != null && highField.isFocused)
            {
                highField.DeactivateInputField();
            }

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            UpdateInputs();
        }

        private void OnClearClicked(GameObject source)
        {
            _ = source;
            if (target == null)
            {
                return;
            }

            TemperatureBounds current = ReadBounds(target);
            if (current.IsUnbounded)
            {
                return;
            }

            lowDraft = null;
            highDraft = null;
            target.Disable();
            UpdateInputs();
        }

        private static TemperatureBounds ReadBounds(TemperatureLimit limit)
        {
            if (limit.IsDisabled())
            {
                return TemperatureBounds.Unbounded;
            }

            int? lower = limit.LowLimit <= TemperatureLimit.MinValue
                ? (int?)null
                : limit.LowLimit;
            int? upper = limit.HighLimit >= TemperatureLimit.MaxValue
                ? (int?)null
                : limit.HighLimit;
            return new TemperatureBounds(lower, upper);
        }

        private static void WriteBounds(
            TemperatureLimit limit,
            TemperatureBounds bounds)
        {
            if (bounds.IsUnbounded)
            {
                limit.Disable();
                return;
            }

            int low = bounds.LowerKelvin ?? TemperatureLimit.MinValue;
            int high = bounds.UpperKelvin ?? TemperatureLimit.MaxValue;
            limit.SetLowLimit(low);
            limit.SetHighLimit(high);
        }

        private void UpdateTooltips()
        {
            PUIElements.SetToolTip(
                lowInput,
                SideScreenStrings.TOOLTIPS.LOWER_BOUND.ToString());
            PUIElements.SetToolTip(
                highInput,
                SideScreenStrings.TOOLTIPS.UPPER_BOUND.ToString());
        }

        private void ConfigureNavigation()
        {
            if (lowField == null || highField == null)
            {
                return;
            }

            Navigation navLow = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = highField
            };
            lowField.navigation = navLow;

            Navigation navHigh = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = lowField,
                selectOnDown = clearSelectable
            };
            highField.navigation = navHigh;

            if (clearSelectable != null)
            {
                Navigation navClear = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = highField
                };
                clearSelectable.navigation = navClear;
            }
        }
    }
}
