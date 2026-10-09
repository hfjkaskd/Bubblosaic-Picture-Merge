using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Fits baked artwork without distortion over a separate full-screen background.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class BackdropEdgeExtension : BaseMeshEffect
    {
        [SerializeField, Min(.01f)] float contentAspect = 1080f / 2340;
        [SerializeField] Image sourceImage;

        protected override void OnEnable()
        {
            if (sourceImage == null) sourceImage = GetComponent<Image>();
            base.OnEnable();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || sourceImage == null || sourceImage.sprite == null) return;
            Rect rect = sourceImage.GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float width = Mathf.Min(rect.width, rect.height * contentAspect);
            float height = width / contentAspect;
            Rect inner = new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height);
            Sprite sprite = sourceImage.overrideSprite != null ? sourceImage.overrideSprite : sourceImage.sprite;
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            // Sample pixel centres to avoid the transparent packing gutter at atlas edges.
            float du = .5f / sprite.texture.width, dv = .5f / sprite.texture.height;
            uv.x += du; uv.y += dv; uv.z -= du; uv.w -= dv;
            vertices.Clear();
            Color32 color = sourceImage.color;
            float blendX = Mathf.Max(0, rect.width - width);
            float blendY = Mathf.Max(0, rect.height - height);
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                float x0 = Boundary(col, rect.xMin, inner.xMin, inner.xMax, rect.xMax);
                float x1 = Boundary(col + 1, rect.xMin, inner.xMin, inner.xMax, rect.xMax);
                float y0 = Boundary(row, rect.yMin, inner.yMin, inner.yMax, rect.yMax);
                float y1 = Boundary(row + 1, rect.yMin, inner.yMin, inner.yMax, rect.yMax);
                if (x1 <= x0 || y1 <= y0) continue;
                float u0 = Mathf.Lerp(uv.x, uv.z, (x0-inner.xMin)/width), u1 = Mathf.Lerp(uv.x, uv.z, (x1-inner.xMin)/width);
                float v0 = Mathf.Lerp(uv.y, uv.w, (y0-inner.yMin)/height), v1 = Mathf.Lerp(uv.y, uv.w, (y1-inner.yMin)/height);
                int first = vertices.currentVertCount;
                vertices.AddVert(new Vector3(x0, y0), Tint(color,col,row,blendX,blendY), new Vector2(u0, v0));
                vertices.AddVert(new Vector3(x0, y1), Tint(color,col,row+1,blendX,blendY), new Vector2(u0, v1));
                vertices.AddVert(new Vector3(x1, y1), Tint(color,col+1,row+1,blendX,blendY), new Vector2(u1, v1));
                vertices.AddVert(new Vector3(x1, y0), Tint(color,col+1,row,blendX,blendY), new Vector2(u1, v0));
                vertices.AddTriangle(first, first + 1, first + 2);
                vertices.AddTriangle(first + 2, first + 3, first);
            }
        }

        static Color32 Tint(Color32 color, int x, int y, float blendX, float blendY)
        {
            if ((blendX > 0 && (x == 0 || x == 3)) || (blendY > 0 && (y == 0 || y == 3))) color.a = 0;
            return color;
        }

        static float Boundary(int index, float outerMin, float innerMin, float innerMax, float outerMax)
        {
            switch (index) { case 0: return outerMin; case 1: return innerMin; case 2: return innerMax; default: return outerMax; }
        }
    }
}
