#nullable enable

namespace DeliveryTemperatureLimit
{
    /// <summary>Interaction state shared by pointer, keyboard, and touch help access.</summary>
    internal sealed class TemperatureRangeHelpState
    {
        private bool overTrigger;
        private bool overContent;
        private bool focused;
        private bool pinned;
        private bool dismissed;
        private bool keyboardLayerActive;

        public bool IsVisible => !dismissed && (pinned || overTrigger || overContent || focused);
        public bool IsPointerOverContent => overContent;

        public void SetKeyboardLayerActive(bool value)
        {
            // A newly opened modal dismisses help. A stale keyboard-stack flag
            // must not cancel a pointer event already routed to this control.
            if (keyboardLayerActive && !value) Dismiss();
            keyboardLayerActive = value;
        }

        public void SetPointerOverTrigger(bool value)
        {
            if (value && !overTrigger) dismissed = false;
            overTrigger = value;
        }

        public void SetPointerOverContent(bool value) => overContent = value;

        public void SetFocused(bool value)
        {
            if (value && !focused) dismissed = false;
            focused = value;
        }

        public void Activate()
        {
            if (pinned) Dismiss();
            else
            {
                pinned = true;
                dismissed = false;
            }
        }

        public void Dismiss()
        {
            pinned = false;
            dismissed = true;
        }

        public void Reset()
        {
            overTrigger = overContent = focused = pinned = dismissed = false;
            keyboardLayerActive = false;
        }
    }
}
