#nullable enable

using PeterHan.PLib.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Displays and edits temperature limits using the player's active ONI
    /// display unit while keeping Kelvin as the canonical simulation storage.
    /// Provides explicit bounds naming, unit labels, persistent status feedback,
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

        private TemperatureLimit? target;
        private string? lowDraft;
        private string? highDraft;
        private bool isUpdatingInputs;

        internal void SetTarget(TemperatureLimit? newTarget)
        {
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
                Text = STRINGS.TEMPERATURELIMIT.TEMPERATURE_RANGE
            };
            var headerSpacer = new PSpacer
            {
                FlexSize = Vector2.right
            };
            var clearBtn = new PButton("ClearButton")
            {
                Text = STRINGS.TEMPERATURELIMIT.CLEAR,
                ToolTip = STRINGS.TEMPERATURELIMIT.TOOLTIP_CLEAR,
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Color = PUITuning.Colors.ButtonBlueStyle,
                Margin = new RectOffset(6, 6, 2, 2),
                OnClick = OnClearClicked
            };
            clearBtn.AddOnRealize(realizedButton =>
            {
                clearButton = realizedButton;
            });
            headerPanel.AddChild(headerLabel);
            headerPanel.AddChild(headerSpacer);
            headerPanel.AddChild(clearBtn);

            var boundsGrid = new PGridPanel("BoundsGrid");
            boundsGrid.AddRow(new GridRowSpec());
            boundsGrid.AddRow(new GridRowSpec());
            boundsGrid.AddColumn(new GridColumnSpec());
            boundsGrid.AddColumn(new GridColumnSpec(72f, 0f));
            boundsGrid.AddColumn(new GridColumnSpec());

            var lowLabel = new PLabel("LowLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.LOW_BOUND_LABEL
            };
            boundsGrid.AddChild(
                lowLabel,
                new GridComponentSpec(0, 0) { Alignment = TextAnchor.MiddleLeft });

            var lowInputField = new PTextField("lowLimit")
            {
                Type = PTextField.FieldType.Integer,
                OnTextChanged = OnLowInputChanged,
                MinWidth = 72
            };
            lowInputField.AddOnRealize(realizedInput =>
            {
                lowInput = realizedInput;
                lowField = realizedInput.GetComponent<TMP_InputField>();
                if (lowField != null)
                {
                    lowField.onEndEdit.AddListener(OnLowInputEndEdit);
                }
            });
            boundsGrid.AddChild(
                lowInputField,
                new GridComponentSpec(0, 1) { Alignment = TextAnchor.MiddleLeft });

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
                new GridComponentSpec(0, 2) { Alignment = TextAnchor.MiddleLeft });

            var highLabel = new PLabel("HighLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.HIGH_BOUND_LABEL
            };
            boundsGrid.AddChild(
                highLabel,
                new GridComponentSpec(1, 0) { Alignment = TextAnchor.MiddleLeft });

            var highInputField = new PTextField("highLimit")
            {
                Type = PTextField.FieldType.Integer,
                OnTextChanged = OnHighInputChanged,
                MinWidth = 72
            };
            highInputField.AddOnRealize(realizedInput =>
            {
                highInput = realizedInput;
                highField = realizedInput.GetComponent<TMP_InputField>();
                if (highField != null)
                {
                    highField.onEndEdit.AddListener(OnHighInputEndEdit);
                }
            });
            boundsGrid.AddChild(
                highInputField,
                new GridComponentSpec(1, 1) { Alignment = TextAnchor.MiddleLeft });

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
                new GridComponentSpec(1, 2) { Alignment = TextAnchor.MiddleLeft });

            var status = new PLabel("StatusLabel")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.STATUS_DISABLED,
                ToolTip = STRINGS.TEMPERATURELIMIT.TOOLTIP_STATUS
            };
            status.AddOnRealize(realizedStatus =>
            {
                statusLabel = realizedStatus;
            });

            panel.AddChild(headerPanel);
            panel.AddChild(boundsGrid);
            panel.AddChild(status);
            panel.AddTo(gameObject);

            base.OnPrefabInit();
            UpdateInputs();
        }

        protected override void OnDisable()
        {
            lowDraft = null;
            highDraft = null;
            ConstructionMaterialTemperatureLimit
                .ResetConstructionMaterialTemperatureLimitToDefaultsIfOwned(
                    target);
            base.OnDisable();
        }

        private void Update()
        {
            if (IsAnyFieldFocused() && Input.GetKeyDown(KeyCode.Escape))
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
            }

            if (statusLabel != null)
            {
                string statusText = bounds.IsEqualBounds
                    ? STRINGS.TEMPERATURELIMIT.WARNING_EMPTY.ToString()
                    : TemperatureLimitPresenter.GetRangeDescription(bounds);
                PUIElements.SetText(statusLabel, statusText);
            }

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
            if (isUpdatingInputs || target == null)
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
                if (statusLabel != null)
                {
                    PUIElements.SetText(statusLabel, validation.Message);
                }

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

            if (validation.Severity == TemperatureValidationSeverity.Warning &&
                statusLabel != null)
            {
                PUIElements.SetText(statusLabel, validation.Message);
            }
        }

        private void RevertDrafts()
        {
            lowDraft = null;
            highDraft = null;
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
                STRINGS.TEMPERATURELIMIT.TOOLTIP_LOW.ToString());
            PUIElements.SetToolTip(
                highInput,
                STRINGS.TEMPERATURELIMIT.TOOLTIP_HIGH.ToString());
            PUIElements.SetToolTip(
                statusLabel,
                STRINGS.TEMPERATURELIMIT.TOOLTIP_STATUS.ToString());
        }

        private void ConfigureNavigation()
        {
            if (lowField == null || highField == null)
            {
                return;
            }

            Selectable? clearSelectable = clearButton != null
                ? clearButton.GetComponent<Selectable>()
                : null;

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
