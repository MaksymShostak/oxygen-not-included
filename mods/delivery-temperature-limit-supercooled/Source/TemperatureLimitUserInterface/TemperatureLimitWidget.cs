#nullable enable

using PeterHan.PLib.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Edits one <see cref="TemperatureLimit"/> using the player's active ONI
    /// display unit while keeping Kelvin as the component's canonical storage.
    /// </summary>
    internal sealed class TemperatureLimitWidget : KMonoBehaviour
    {
        private GameObject? lowInput;
        private GameObject? highInput;
        private TMP_InputField? lowField;
        private TMP_InputField? highField;
        private TemperatureLimit? target;
        private bool isUpdatingInputs;

        internal void SetTarget(TemperatureLimit? newTarget)
        {
            target = newTarget;
            UpdateInputs();
        }

        internal bool IsAnyFieldFocused() =>
            (lowField != null && lowField.isFocused) ||
            (highField != null && highField.isFocused);

        protected override void OnPrefabInit()
        {
            var margin = new RectOffset(4, 4, 4, 4);
            BoxLayoutGroup? baseLayout =
                gameObject.GetComponent<BoxLayoutGroup>();
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
                Direction = PanelDirection.Horizontal,
                Margin = margin,
                Spacing = 4,
                FlexSize = Vector2.right
            };
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
            });
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
            });
            var label = new PLabel("label")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.LABEL
            };
            var separator = new PLabel("separator")
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.RANGE_SEPARATOR
            };

            panel.AddChild(label);
            panel.AddChild(lowInputField);
            panel.AddChild(separator);
            panel.AddChild(highInputField);
            panel.AddTo(gameObject);
            base.OnPrefabInit();
            UpdateInputs();
        }

        protected override void OnDisable()
        {
            ConstructionMaterialTemperatureLimit
                .ResetConstructionMaterialTemperatureLimitToDefaultsIfOwned(
                    target);
            base.OnDisable();
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

            if (target.IsDisabled())
            {
                SetInputText(lowField, null);
                SetInputText(highField, null);
            }
            else
            {
                SetInputText(
                    lowField,
                    target.LowLimit == TemperatureLimit.MinValue
                        ? (int?)null
                        : target.LowLimit);
                SetInputText(
                    highField,
                    target.HighLimit == TemperatureLimit.MaxValue
                        ? (int?)null
                        : target.HighLimit);
            }

            UpdateTooltip(target);
        }

        private static void SetInputText(
            TMP_InputField field,
            int? temperatureKelvin)
        {
            string displayedText = temperatureKelvin.HasValue
                ? GameUtil.GetFormattedTemperature(
                    temperatureKelvin.Value,
                    GameUtil.TimeSlice.None,
                    GameUtil.TemperatureInterpretation.Absolute,
                    true,
                    true)
                : string.Empty;
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

            isUpdatingInputs = true;
            try
            {
                int? parsedTemperatureKelvin =
                    TryParseDisplayedTemperatureKelvin(text);
                if (!parsedTemperatureKelvin.HasValue)
                {
                    target.SetLowLimit(TemperatureLimit.MinValue);
                    if (target.HighLimit == TemperatureLimit.MaxValue ||
                        target.IsDisabled())
                    {
                        target.Disable();
                    }
                }
                else
                {
                    int temperatureKelvin = parsedTemperatureKelvin.Value;
                    target.SetLowLimit(temperatureKelvin);
                    if (target.IsDisabled())
                    {
                        target.SetHighLimit(TemperatureLimit.MaxValue);
                    }
                    else if (temperatureKelvin > target.HighLimit)
                    {
                        target.SetHighLimit(temperatureKelvin);
                    }
                }

                UpdateInputsCore();
            }
            finally
            {
                isUpdatingInputs = false;
            }
        }

        private void OnHighInputChanged(GameObject source, string text)
        {
            _ = source;
            if (isUpdatingInputs || target == null)
            {
                return;
            }

            isUpdatingInputs = true;
            try
            {
                int? parsedTemperatureKelvin =
                    TryParseDisplayedTemperatureKelvin(text);
                if (!parsedTemperatureKelvin.HasValue)
                {
                    target.SetHighLimit(TemperatureLimit.MaxValue);
                    if (target.LowLimit == TemperatureLimit.MinValue)
                    {
                        target.Disable();
                    }
                }
                else
                {
                    int temperatureKelvin = parsedTemperatureKelvin.Value;
                    target.SetHighLimit(temperatureKelvin);
                    if (temperatureKelvin < target.LowLimit)
                    {
                        target.SetLowLimit(temperatureKelvin);
                    }
                }

                UpdateInputsCore();
            }
            finally
            {
                isUpdatingInputs = false;
            }
        }

        private static int? TryParseDisplayedTemperatureKelvin(string text)
        {
            string normalizedText = text.Trim();
            if (normalizedText.Length == 0)
            {
                return null;
            }

            string unitSuffix = GameUtil.GetTemperatureUnitSuffix();
            if (normalizedText.EndsWith(
                    unitSuffix,
                    StringComparison.Ordinal))
            {
                normalizedText = normalizedText.Remove(
                    normalizedText.Length - unitSuffix.Length);
            }

            return int.TryParse(normalizedText, out int displayedTemperature)
                ? (int?)Math.Round(
                    GameUtil.GetTemperatureConvertedToKelvin(
                        displayedTemperature))
                : null;
        }

        private void UpdateTooltip(TemperatureLimit currentTarget)
        {
            string tooltip = currentTarget.IsDisabled()
                ? STRINGS.TEMPERATURELIMIT.TOOLTIP_NOTSET.ToString()
                : string.Format(
                    STRINGS.TEMPERATURELIMIT.TOOLTIP_RANGE,
                    GameUtil.GetFormattedTemperature(
                        currentTarget.LowLimit,
                        GameUtil.TimeSlice.None,
                        GameUtil.TemperatureInterpretation.Absolute,
                        true,
                        true),
                    GameUtil.GetFormattedTemperature(
                        currentTarget.HighLimit,
                        GameUtil.TimeSlice.None,
                        GameUtil.TemperatureInterpretation.Absolute,
                        true,
                        true));
            PUIElements.SetToolTip(lowInput, tooltip);
            PUIElements.SetToolTip(highInput, tooltip);
        }
    }
}
