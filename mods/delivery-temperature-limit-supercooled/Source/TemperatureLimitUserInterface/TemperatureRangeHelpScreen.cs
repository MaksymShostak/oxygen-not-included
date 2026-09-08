#nullable enable

using PeterHan.PLib.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Owns a nonmodal help popover and its input. Klei ToolTip only implements
    /// pointer hover and clears on click, so it cannot own this interaction.
    /// </summary>
    internal sealed class TemperatureRangeHelpScreen : KScreen, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private readonly TemperatureRangeHelpState state = new TemperatureRangeHelpState();
        private TemperatureLimitWidget owner = null!;
        private GameObject? popover;
        private RectTransform? viewport;
        private RectTransform? content;
        private RectTransform? canvasRect;
        private TMP_Text? helpText;
        private TemperatureRangeTextLayout? textLayout;
        private ScrollRect? helpScroll;
        private Graphic? focusRing;
        private ColorStyleSetting? buttonColors;
        private string description = "";
        private float hideAt = -1;
        private int activationFrame = -1;
        private int tabFrame = -1;
        private int escapeFrame = -1;
        private int scrollFrame = -1;
        private bool captureEscapeRelease;
        private bool captureActivationRelease;
        private bool captureTabRelease;
        private float lastSortKey;
        private readonly Vector3[] triggerCorners = new Vector3[4];

        public TemperatureRangeHelpScreen() { activateOnSpawn = true; }

        internal bool IsHelpVisible => state.IsVisible;
        internal bool HandledEscapeThisFrame => escapeFrame == Time.frameCount;

        // Run before temperature-field capture, but consume only the help and
        // navigation keys actually handled below. Camera controls remain free.
        public override float GetSortKey() => IsHelpVisible || (owner != null && owner.IsAnyFieldFocused())
            ? KScreen.EDITING_SCREEN_SORT_KEY + 1 : 0;

        internal void Initialize(TemperatureLimitWidget widget)
        {
            owner = widget;
            displayName = STRINGS.DELIVERY_TEMPERATURE_LIMIT.SIDESCREEN.SECTION_RANGE;
            // Keep the native KButton's sounds and hit testing, with no filled
            // action-button background. This style belongs to this widget only.
            buttonColors = ScriptableObject.CreateInstance<ColorStyleSetting>();
            buttonColors.Init(Color.clear);
            KImage background = GetComponent<KImage>();
            background.colorStyleSetting = buttonColors;
            background.ApplyColorStyleSetting();

            foreach (Behaviour component in GetComponents<Behaviour>())
                if (component is ILayoutController || component is ILayoutElement)
                    Object.DestroyImmediate(component);
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 32;
            layout.minHeight = layout.preferredHeight = 32;
            layout.flexibleWidth = layout.flexibleHeight = 0;

            TMP_Text label = GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 14;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            Color ink = new Color(0.30f, 0.31f, 0.37f);
            label.color = ink;

            Graphic circle = CreateCircle("HelpCircle", 18, ink);
            focusRing = CreateCircle("HelpFocusRing", 28, PUITuning.Colors.ButtonPinkStyle.inactiveColor);
            focusRing.enabled = false;
            var selectable = gameObject.AddComponent<Selectable>();
            selectable.targetGraphic = circle;
            selectable.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.pressedColor = new Color(0.35f, 0.35f, 0.35f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.1f;
            selectable.colors = colors;
        }

        private Graphic CreateCircle(string name, float size, Color color)
        {
            GameObject child = PUIElements.CreateUI(gameObject, name);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var circle = child.AddComponent<TemperatureRangeHelpCircle>();
            circle.color = color;
            circle.raycastTarget = false;
            return circle;
        }

        internal void SetDescription(string text)
        {
            description = text;
            if (helpText != null) helpText.text = text;
        }

        internal void ResetHelp()
        {
            state.Reset();
            hideAt = -1;
            if (popover != null) popover.SetActive(false);
            if (focusRing != null) focusRing.enabled = false;
        }

        internal void ToggleHelp()
        {
            // Pointer/submit events have already been routed to this control.
            // ScreenUpdate's topLevel argument gates raw keyboard polling only;
            // it must not veto a later event using an earlier frame's value.
            state.Activate();
            ApplyVisibility(immediate: true);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            state.SetPointerOverTrigger(true);
            ApplyVisibility();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            state.SetPointerOverTrigger(false);
            ApplyVisibility();
        }

        internal void SetPointerOverContent(bool over)
        {
            state.SetPointerOverContent(over);
            ApplyVisibility();
        }

        public void OnSelect(BaseEventData eventData)
        {
            state.SetFocused(true);
            if (focusRing != null) focusRing.enabled = true;
            ApplyVisibility();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            state.SetFocused(false);
            if (focusRing != null) focusRing.enabled = false;
            ApplyVisibility();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            ActivateOnce();
            eventData.Use();
        }

        private bool IsFocused => UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == gameObject;

        private void ActivateOnce()
        {
            if (activationFrame == Time.frameCount) return;
            activationFrame = Time.frameCount;
            if (IsFocused) ToggleHelp();
            else owner.ActivateFocusedButton();
        }

        private void DismissHelp()
        {
            state.Dismiss();
            ApplyVisibility(immediate: true);
        }

        private bool HandleKeyboard(bool escape)
        {
            if (escape && (IsHelpVisible || HandledEscapeThisFrame))
            {
                escapeFrame = Time.frameCount;
                captureEscapeRelease = true;
                DismissHelp();
                return true;
            }
            if (HandleHelpScroll()) return true;
            if (Input.GetKeyDown(KeyCode.Tab) && owner != null && owner.CanNavigateFromCurrentSelection())
            {
                captureTabRelease = true;
                if (tabFrame != Time.frameCount)
                {
                    tabFrame = Time.frameCount;
                    owner.MoveKeyboardFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                }
                return true;
            }
            if ((IsFocused || (owner != null && owner.IsActionButtonFocused())) &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                captureActivationRelease = true;
                ActivateOnce();
                return true;
            }
            return false;
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (e.Consumed) return;
            if (HandleKeyboard(e.IsAction(global::Action.Escape))) e.Consumed = true;
            if (!e.Consumed && IsHelpVisible && state.IsPointerOverContent)
            {
                if (!e.TryConsume(global::Action.ZoomIn)) e.TryConsume(global::Action.ZoomOut);
            }
        }

        private bool HandleHelpScroll()
        {
            if (!IsFocused || !IsHelpVisible || helpScroll == null || content == null || viewport == null)
                return false;
            bool up = Input.GetKeyDown(KeyCode.PageUp);
            bool down = Input.GetKeyDown(KeyCode.PageDown);
            bool home = Input.GetKeyDown(KeyCode.Home);
            bool end = Input.GetKeyDown(KeyCode.End);
            if (!up && !down && !home && !end) return false;
            if (scrollFrame != Time.frameCount)
            {
                scrollFrame = Time.frameCount;
                float page = viewport.rect.height / Mathf.Max(1, content.rect.height - viewport.rect.height);
                helpScroll.verticalNormalizedPosition = home ? 1 : end ? 0 :
                    Mathf.Clamp01(helpScroll.verticalNormalizedPosition + (up ? page : -page));
            }
            return true;
        }

        public override void OnKeyUp(KButtonEvent e)
        {
            if (captureEscapeRelease && e.IsAction(global::Action.Escape))
            {
                e.Consumed = true;
                captureEscapeRelease = false;
            }
            if (captureTabRelease && Input.GetKeyUp(KeyCode.Tab))
            {
                e.Consumed = true;
                captureTabRelease = false;
            }
            if (captureActivationRelease && (Input.GetKeyUp(KeyCode.Return) ||
                Input.GetKeyUp(KeyCode.KeypadEnter) || Input.GetKeyUp(KeyCode.Space)))
            {
                e.Consumed = true;
                captureActivationRelease = false;
            }
        }

        public override void ScreenUpdate(bool topLevel)
        {
            state.SetKeyboardLayerActive(topLevel);
            if (!topLevel)
            {
                ApplyVisibility(immediate: true);
                return;
            }
            if (owner == null) return;
            // Unity focus/submit and ONI key dispatch can run in the same frame.
            // Frame guards prevent double activation, including unbound UI keys.
            HandleKeyboard(Input.GetKeyDown(KeyCode.Escape));
            if (popover != null && popover.activeSelf && Input.GetMouseButtonDown(0))
            {
                Canvas root = GetComponentInParent<Canvas>().rootCanvas;
                Camera? camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, Input.mousePosition, camera) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, camera))
                    DismissHelp();
            }
        }

        private void LateUpdate()
        {
            ApplyVisibility();
            float sortKey = GetSortKey();
            if (sortKey != lastSortKey && IsActive())
            {
                lastSortKey = sortKey;
                KScreenManager.Instance.RefreshStack();
            }
        }

        private void ApplyVisibility(bool immediate = false)
        {
            if (state.IsVisible)
            {
                hideAt = -1;
                if (popover == null) CreatePopover();
                popover!.SetActive(true);
                PositionPopover();
            }
            else if (popover != null && popover.activeSelf)
            {
                // Allow crossing the small pointer gap without flicker. This is
                // an exit grace period, never a timeout while reading the help.
                if (hideAt < 0) hideAt = Time.unscaledTime + 0.15f;
                if (immediate || Time.unscaledTime >= hideAt) popover.SetActive(false);
            }
        }

        private void CreatePopover()
        {
            Canvas root = GetComponentInParent<Canvas>().rootCanvas;
            canvasRect = (RectTransform)root.transform;
            popover = PUIElements.CreateUI(root.gameObject, "TemperatureRangeHelpPopover");
            viewport = (RectTransform)popover.transform;
            viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.pivot = new Vector2(0, 1);
            Canvas overlay = popover.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = root.sortingLayerID;
            overlay.sortingOrder = GetComponentInParent<Canvas>().sortingOrder + 1;
            overlay.worldCamera = root.worldCamera;
            popover.AddComponent<GraphicRaycaster>();
            popover.AddComponent<Image>().color = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
            popover.AddComponent<RectMask2D>();
            popover.AddComponent<TemperatureRangeHelpPointer>().Owner = this;
            GameObject body = new PLabel("TemperatureRangeHelpText")
            {
                Text = description,
                TextStyle = PUITuning.Fonts.UILightStyle,
                DynamicSize = true
            }.Build();
            content = (RectTransform)body.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            helpText = body.GetComponentInChildren<TMP_Text>(true);
            textLayout = TemperatureRangeTextLayout.Attach(body, 8);
            ScrollRect scroll = popover.AddComponent<ScrollRect>();
            helpScroll = scroll;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
        }

        private void PositionPopover()
        {
            if (viewport == null || content == null || canvasRect == null || textLayout == null) return;
            Rect available = canvasRect.rect;
            float width = Mathf.Min(320, Mathf.Max(1, available.width - 16));
            viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            content.sizeDelta = new Vector2(0, content.sizeDelta.y);
            textLayout.SetLayoutHorizontal();

            ((RectTransform)transform).GetWorldCorners(triggerCorners);
            Vector3 left = canvasRect.InverseTransformPoint(triggerCorners[1]);
            Vector3 right = canvasRect.InverseTransformPoint(triggerCorners[2]);
            Vector3 bottom = canvasRect.InverseTransformPoint(triggerCorners[0]);
            float roomAbove = Mathf.Max(1, available.yMax - left.y - 16);
            float roomBelow = Mathf.Max(1, bottom.y - available.yMin - 16);
            bool above = roomAbove >= textLayout.preferredHeight || roomAbove >= roomBelow;
            float height = Mathf.Min(textLayout.preferredHeight, above ? roomAbove : roomBelow);
            viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textLayout.preferredHeight);
            // Anchor directly to the control so crossing to the help requires
            // only the small exit grace period. Prefer above to leave fields visible.
            float x = Mathf.Clamp((left.x + right.x - width) * 0.5f,
                available.xMin + 8, available.xMax - width - 8);
            float y = above ? left.y + height + 8 : bottom.y - 8;
            viewport.localPosition = new Vector3(x, y, 0);
        }

        protected override void OnDisable()
        {
            ResetHelp();
            captureEscapeRelease = captureActivationRelease = captureTabRelease = false;
            base.OnDisable();
        }

        protected override void OnCleanUp()
        {
            if (popover != null) Object.Destroy(popover);
            if (buttonColors != null) Object.Destroy(buttonColors);
            base.OnCleanUp();
        }
    }

    internal sealed class TemperatureRangeHelpPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal TemperatureRangeHelpScreen Owner = null!;
        public void OnPointerEnter(PointerEventData eventData) => Owner.SetPointerOverContent(true);
        public void OnPointerExit(PointerEventData eventData) => Owner.SetPointerOverContent(false);
    }
}
