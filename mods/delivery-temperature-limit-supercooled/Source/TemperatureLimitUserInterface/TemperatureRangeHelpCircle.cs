#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace DeliveryTemperatureLimit
{
    /// <summary>A scalable outline, independent of the font's optional symbol glyphs.</summary>
    internal sealed class TemperatureRangeHelpCircle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = Mathf.Max(0, outer - 1.25f);
            const int segments = 64;
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = segment * (2 * Mathf.PI / segments);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.AddVert(center + direction * outer, color, Vector2.zero);
                vertices.AddVert(center + direction * inner, color, Vector2.zero);
                if (segment == 0) continue;
                int index = segment * 2;
                vertices.AddTriangle(index - 2, index, index - 1);
                vertices.AddTriangle(index, index + 1, index - 1);
            }
        }
    }
}
