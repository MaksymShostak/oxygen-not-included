using UnityEngine;
using UnityEngine.UI;
using System;
using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using TMPro;
using STRINGS;

namespace DeliveryTemperatureLimit
{
    public class TemperatureLimitWidget : KMonoBehaviour
    {
        private GameObject lowInput;
        private GameObject highInput;
        private TMPro.TMP_InputField lowField;
        private TMPro.TMP_InputField highField;

        private TemperatureLimit target;

        protected override void OnPrefabInit()
        {
            var margin = new RectOffset(4, 4, 4, 4);
            var baseLayout = gameObject.GetComponent<BoxLayoutGroup>();
            if (baseLayout != null)
                baseLayout.Params = new BoxLayoutParams()
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = margin,
                };
            PPanel panel = new PPanel("MainPanel")
            {
                Direction = PanelDirection.Horizontal,
                Margin = margin,
                Spacing = 4,
                FlexSize = Vector2.right
            };
            PTextField lowInputField = new PTextField( "lowLimit" )
            {
                    Type = PTextField.FieldType.Integer,
                    OnTextChanged = OnTextChangedLow,
                    MinWidth = 72
            };
            lowInputField.AddOnRealize((obj) => {
                lowInput = obj;
                lowField = obj.GetComponent<TMPro.TMP_InputField>();
            });
            PTextField highInputField = new PTextField( "highLimit" )
            {
                Type = PTextField.FieldType.Integer,
                OnTextChanged = OnTextChangedHigh,
                MinWidth = 72
            };
            highInputField.AddOnRealize((obj) => {
                highInput = obj;
                highField = obj.GetComponent<TMPro.TMP_InputField>();
            });
            PLabel label = new PLabel( "label" )
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.LABEL
            };
            PLabel separator = new PLabel( "separator" )
            {
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                Text = STRINGS.TEMPERATURELIMIT.RANGE_SEPARATOR
            };
            panel.AddChild( label );
            panel.AddChild( lowInputField );
            panel.AddChild( separator );
            panel.AddChild( highInputField );
            panel.AddTo( gameObject );
            base.OnPrefabInit();
            UpdateInputs();
        }

        public void SetTarget(TemperatureLimit new_target)
        {
            target = new_target;
            UpdateInputs();
        }

        private bool isUpdating = false;

        private void UpdateInputs()
        {
            if (isUpdating) return;
            isUpdating = true;
            try
            {
                UpdateInputsInternal();
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void UpdateInputsInternal()
        {
            if (target == null || lowField == null || highField == null)
                return;

            if (target.IsDisabled())
            {
                EmptyInputsInternal();
            }
            else
            {
                if (target.LowLimit == TemperatureLimit.MinValue)
                    SetInputText(lowField, -1);
                else
                    SetInputText(lowField, target.LowLimit);

                if (target.HighLimit == TemperatureLimit.MaxValue)
                    SetInputText(highField, -1);
                else
                    SetInputText(highField, target.HighLimit);
            }
            UpdateToolTip();
        }

        private void EmptyInputsInternal()
        {
            if (lowField != null && lowField.text != "") lowField.text = "";
            if (highField != null && highField.text != "") highField.text = "";
        }

        private void SetInputText(TMP_InputField field, int value)
        {
            if (field == null) return;
            if (value == -1)
            {
                if (field.text != "") field.text = "";
            }
            else
            {
                string text = GameUtil.GetFormattedTemperature(value, GameUtil.TimeSlice.None,
                    GameUtil.TemperatureInterpretation.Absolute, true, true);
                if (field.text != text)
                    field.text = text;
            }
        }

        private void OnTextChangedLow(GameObject source, string text)
        {
            if (isUpdating) return;

            isUpdating = true;
            try
            {
                int value = ParseTemperature(text);
                if (value == -1)
                {
                    target.SetLowLimit(TemperatureLimit.MinValue);
                    if (target.HighLimit == TemperatureLimit.MaxValue || target.IsDisabled())
                    {
                        target.Disable();
                    }
                }
                else
                {
                    target.SetLowLimit(value);
                    if (target.IsDisabled())
                    {
                        target.SetHighLimit(TemperatureLimit.MaxValue);
                    }
                    else if (value > target.HighLimit)
                    {
                        target.SetHighLimit(value);
                    }
                }
                UpdateInputsInternal();
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void OnTextChangedHigh(GameObject source, string text)
        {
            if (isUpdating) return;

            isUpdating = true;
            try
            {
                int value = ParseTemperature(text);
                if (value == -1)
                {
                    target.SetHighLimit(TemperatureLimit.MaxValue);
                    if (target.LowLimit == TemperatureLimit.MinValue)
                    {
                        target.Disable();
                    }
                }
                else
                {
                    target.SetHighLimit(value);
                    if (value < target.LowLimit)
                    {
                        target.SetLowLimit(value);
                    }
                }
                UpdateInputsInternal();
            }
            finally
            {
                isUpdating = false;
            }
        }

        private int ParseTemperature(string text)
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
                return -1;

            if (text.EndsWith(GameUtil.GetTemperatureUnitSuffix()))
                text = text.Remove(text.Length - GameUtil.GetTemperatureUnitSuffix().Length);

            int result;
            if (int.TryParse(text, out result))
                return (int)Math.Round(GameUtil.GetTemperatureConvertedToKelvin(result));

            return -1;
        }

        private void UpdateToolTip()
        {
            string tooltip;
            if( !target.IsDisabled())
                tooltip  = string.Format(STRINGS.TEMPERATURELIMIT.TOOLTIP_RANGE,
                    GameUtil.GetFormattedTemperature(target.LowLimit, GameUtil.TimeSlice.None,
                        GameUtil.TemperatureInterpretation.Absolute, true, true),
                    GameUtil.GetFormattedTemperature(target.HighLimit, GameUtil.TimeSlice.None,
                        GameUtil.TemperatureInterpretation.Absolute, true, true));
            else
                tooltip = STRINGS.TEMPERATURELIMIT.TOOLTIP_NOTSET;
            PUIElements.SetToolTip( lowInput, tooltip );
            PUIElements.SetToolTip( highInput, tooltip );
        }

        public bool IsAnyFieldFocused()
        {
            return (lowField != null && lowField.isFocused) || (highField != null && highField.isFocused);
        }

        protected override void OnDisable()
        {
            // This should be called whenever the widget is hidden, reset the widget
            // values if it's for construction.
            MaterialSelectionPanel_Patch.CheckResetToConstructionDefaults( target );
        }
    }
}
