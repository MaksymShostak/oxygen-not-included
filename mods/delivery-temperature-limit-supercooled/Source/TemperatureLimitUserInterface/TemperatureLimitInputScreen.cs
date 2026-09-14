#nullable enable

using System;

using System.Collections;
using TMPro;
using UnityEngine;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns ONI keyboard capture for one temperature field. Unity retains text
    /// editing; this screen prevents the same keys from reaching game shortcuts.
    /// </summary>
    internal sealed class TemperatureLimitInputScreen : KScreen
    {
        private TMP_InputField? inputField;
        private Coroutine? pendingRelease;

        public TemperatureLimitInputScreen()
        {
            activateOnSpawn = true;
        }

        protected override void OnSpawn()
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
            {
                base.OnSpawn();
                inputField = GetComponent<TMP_InputField>();
                if (inputField == null)
                    throw new InvalidOperationException("Temperature input field is unavailable.");
                inputField.onFocus += OnInputFocus;
                inputField.onEndEdit.AddListener(OnInputEndEdit);
            }
            catch (Exception exception)
            {
                isEditing = false;
                RuntimeFailureReporting.DisableUserInterface(nameof(OnSpawn), exception);
            }
        }

        private void OnInputFocus()
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
            {
                CancelPendingRelease();
                // TMP invokes onFocus before isFocused becomes true. Use the focus
                // notification so the screen stack is ordered before key dispatch.
                isEditing = true;
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnInputFocus), exception);
            }
        }

        private void OnInputEndEdit(string text)
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
            {
                CancelPendingRelease();
                if (isActiveAndEnabled && gameObject.activeInHierarchy)
                {
                    pendingRelease = StartCoroutine(ReleaseAfterEditFrame());
                }
                else
                {
                    ReleaseInputCapture();
                }
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnInputEndEdit), exception);
            }
        }

        private IEnumerator ReleaseAfterEditFrame()
        {
            // Enter/Escape must not also activate a game action in this frame.
            yield return new WaitForEndOfFrame();
            try
            {
                pendingRelease = null;
                isEditing = false;
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(ReleaseAfterEditFrame), exception);
            }
        }

        private void CancelPendingRelease()
        {
            if (pendingRelease != null)
            {
                Coroutine release = pendingRelease;
                pendingRelease = null;
                StopCoroutine(release);
            }
        }

        private void ReleaseInputCapture()
        {
            isEditing = false;
            CancelPendingRelease();
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
            {
                if (isEditing)
                {
                    e.Consumed = true;
                }
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnKeyDown), exception);
            }
        }

        public override void OnKeyUp(KButtonEvent e)
        {
            if (!RuntimeFailureReporting.IsUserInterfaceEnabled) return;
            try
            {
                if (isEditing)
                {
                    e.Consumed = true;
                }
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnKeyUp), exception);
            }
        }

        protected override void OnDisable()
        {
            try
            {
                try { ReleaseInputCapture(); }
                finally { base.OnDisable(); }
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnDisable), exception);
            }
        }

        protected override void OnCleanUp()
        {
            try
            {
                try
                {
                    ReleaseInputCapture();
                    if (inputField != null)
                    {
                        inputField.onFocus -= OnInputFocus;
                        inputField.onEndEdit.RemoveListener(OnInputEndEdit);
                    }
                }
                finally { base.OnCleanUp(); }
            }
            catch (Exception exception)
            {
                RuntimeFailureReporting.DisableUserInterface(nameof(OnCleanUp), exception);
            }
        }
    }
}
