#if BIZZA_REAL_WITHDRAW
using UnityEngine;
using UnityEngine.UI;

// UI mesh: no extra texture or downloaded resource is needed.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DailyMissionLoadingSpinner : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments = 48;
        Rect rect = GetPixelAdjustedRect();
        float outer = Mathf.Min(rect.width, rect.height) * .5f;
        float inner = outer * .72f;
        for (int i = 0; i <= segments; i++)
        {
            float progress = (float)i / segments;
            float angle = progress * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Color tint = color;
            tint.a *= Mathf.Lerp(.12f, 1f, progress);
            mesh.AddVert(rect.center + direction * inner, tint, Vector2.zero);
            mesh.AddVert(rect.center + direction * outer, tint, Vector2.zero);
            if (i == 0) continue;
            int vertex = i * 2;
            mesh.AddTriangle(vertex - 2, vertex - 1, vertex);
            mesh.AddTriangle(vertex - 1, vertex + 1, vertex);
        }
    }
}
#endif
