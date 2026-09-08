#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Lets the parent own width and measures wrapped text at that width. The
    /// resulting height is a minimum, so PLib cannot compress validation lines.
    /// </summary>
    internal sealed class TemperatureRangeTextLayout : MonoBehaviour, ILayoutElement, ILayoutController
    {
        private TMP_Text text = null!;
        private float padding;
        private float height;

        internal static TemperatureRangeTextLayout Attach(GameObject wrapper, float padding = 0)
        {
            // PLib invokes attached layout controllers even when disabled.
            foreach (Behaviour component in wrapper.GetComponents<Behaviour>())
                if (component is ILayoutController || component is ILayoutElement)
                    Object.DestroyImmediate(component);

            var layout = wrapper.AddComponent<TemperatureRangeTextLayout>();
            layout.text = wrapper.GetComponentInChildren<TMP_Text>(true);
            layout.padding = padding;
#pragma warning disable CS0618
            layout.text.enableWordWrapping = true;
#pragma warning restore CS0618
            layout.text.enableAutoSizing = false;
            layout.text.alignment = TextAlignmentOptions.TopLeft;
            layout.text.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = layout.text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return layout;
        }

        public float minWidth => 0;
        public float preferredWidth => 0;
        public float flexibleWidth => 1;
        public float minHeight => preferredHeight;
        public float preferredHeight => height;
        public float flexibleHeight => 0;
        public int layoutPriority => 10;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() => SetLayoutHorizontal();
        public void SetLayoutVertical() { }

        public void SetLayoutHorizontal()
        {
            float width = ((RectTransform)transform).rect.width;
            if (text == null || width <= 2 * padding) return;
            float measured = text.GetPreferredValues(text.text, width - 2 * padding,
                float.PositiveInfinity).y + 2 * padding;
            if (Mathf.Approximately(height, measured)) return;
            height = measured;
            // During a layout pass the subsequent vertical calculation sees the
            // new height. Outside a pass, request the parent layout explicitly.
            if (!CanvasUpdateRegistry.IsRebuildingLayout())
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }
    }
}
