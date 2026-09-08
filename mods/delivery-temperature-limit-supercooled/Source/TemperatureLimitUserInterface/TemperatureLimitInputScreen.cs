#nullable enable

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
            base.OnSpawn();
            inputField = GetComponent<TMP_InputField>();
            inputField.onFocus += OnInputFocus;
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        private void OnInputFocus()
        {
            CancelPendingRelease();
            // TMP invokes onFocus before isFocused becomes true. Use the focus
            // notification so the screen stack is ordered before key dispatch.
            isEditing = true;
        }

        private void OnInputEndEdit(string text)
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

        private IEnumerator ReleaseAfterEditFrame()
        {
            // Enter/Escape must not also activate a game action in this frame.
            yield return new WaitForEndOfFrame();
            pendingRelease = null;
            isEditing = false;
        }

        private void CancelPendingRelease()
        {
            if (pendingRelease != null)
            {
                StopCoroutine(pendingRelease);
                pendingRelease = null;
            }
        }

        private void ReleaseInputCapture()
        {
            CancelPendingRelease();
            isEditing = false;
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (isEditing)
            {
                e.Consumed = true;
            }
        }

        public override void OnKeyUp(KButtonEvent e)
        {
            if (isEditing)
            {
                e.Consumed = true;
            }
        }

        protected override void OnDisable()
        {
            ReleaseInputCapture();
            base.OnDisable();
        }

        protected override void OnCleanUp()
        {
            if (inputField != null)
            {
                inputField.onFocus -= OnInputFocus;
                inputField.onEndEdit.RemoveListener(OnInputEndEdit);
            }
            ReleaseInputCapture();
            base.OnCleanUp();
        }
    }
}
