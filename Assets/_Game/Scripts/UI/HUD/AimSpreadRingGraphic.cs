using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// UI ring mesh inscribed in its RectTransform, with a constant stroke in pixels whatever the ring size
    /// (a scaled ring sprite would thicken as it grows). Edges fade out over <c>_feather</c> px for anti-aliasing.
    /// Uses the default UI material — no texture.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class AimSpreadRingGraphic : MaskableGraphic
    {
        [Tooltip("Stroke width in pixels (solid part).")]
        [SerializeField, Min(0.5f)] private float _thickness = 2f;
        [Tooltip("Soft edge in pixels on each side of the stroke.")]
        [SerializeField, Min(0f)] private float _feather = 1f;
        [SerializeField, Range(12, 128)] private int _segments = 64;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (outer <= 0f) return;

            Vector2 centre = rect.center;
            float halfStroke = _thickness * 0.5f;
            float mid = Mathf.Max(halfStroke, outer - halfStroke - _feather);
            // 4 radii per spoke: feathered inner edge → solid inner → solid outer → feathered outer edge.
            float r0 = Mathf.Max(0f, mid - halfStroke - _feather);
            float r1 = Mathf.Max(0f, mid - halfStroke);
            float r2 = mid + halfStroke;
            float r3 = mid + halfStroke + _feather;

            Color32 solid = color;
            Color32 clear = color;
            clear.a = 0;

            for (int i = 0; i < _segments; i++)
            {
                float angle = i * Mathf.PI * 2f / _segments;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(centre + dir * r0, clear, Vector2.zero);
                vh.AddVert(centre + dir * r1, solid, Vector2.zero);
                vh.AddVert(centre + dir * r2, solid, Vector2.zero);
                vh.AddVert(centre + dir * r3, clear, Vector2.zero);
            }

            for (int i = 0; i < _segments; i++)
            {
                int a = i * 4;
                int b = ((i + 1) % _segments) * 4;
                for (int band = 0; band < 3; band++)
                {
                    vh.AddTriangle(a + band, b + band, b + band + 1);
                    vh.AddTriangle(a + band, b + band + 1, a + band + 1);
                }
            }
        }
    }
}
