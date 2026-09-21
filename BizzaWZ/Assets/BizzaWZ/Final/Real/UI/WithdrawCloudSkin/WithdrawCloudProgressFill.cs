using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Uses Image.fillAmount to shorten a horizontal sliced Image while retaining
/// the sprite's two end caps. Progress values and tweening remain on the Image.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class WithdrawCloudProgressFill : BaseMeshEffect
{
    [SerializeField] private Image sourceImage;

    protected override void OnEnable()
    {
        if (sourceImage == null) sourceImage = GetComponent<Image>();
        base.OnEnable();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || sourceImage == null || sourceImage.gameObject != gameObject ||
            sourceImage.type != Image.Type.Sliced || vertices.currentVertCount == 0)
            return;

        // UGUI Image.fillAmount calls SetVerticesDirty for every Image.Type,
        // including Sliced, so no polling, animation callback or Update is needed.
        float amount = sourceImage.fillAmount;
        if (amount <= 0f || float.IsNaN(amount))
        {
            vertices.Clear();
            return;
        }
        if (amount >= 1f) return;

        Rect rect = sourceImage.GetPixelAdjustedRect();
        float width = rect.width;
        if (width <= 0f)
        {
            vertices.Clear();
            return;
        }

        UIVertex vertex = default;
        float meshMinX = float.PositiveInfinity;
        float meshMaxX = float.NegativeInfinity;
        int count = vertices.currentVertCount;
        for (int i = 0; i < count; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            meshMinX = Mathf.Min(meshMinX, vertex.position.x);
            meshMaxX = Mathf.Max(meshMaxX, vertex.position.x);
        }
        if (meshMaxX <= meshMinX) return;

        // Reproduce Image.GetAdjustedBorders on the horizontal axis. The full
        // pixel-adjusted rectangle defines progress, not the mesh bounds: packed
        // sprite padding can inset the outer mesh vertices from the rectangle.
        Sprite sprite = sourceImage.overrideSprite;
        if (sprite == null) sprite = sourceImage.sprite;
        float left = 0f;
        float right = 0f;
        if (sprite != null)
        {
            float pixelsPerUnit = sourceImage.pixelsPerUnit * sourceImage.pixelsPerUnitMultiplier;
            if (pixelsPerUnit > 0f)
            {
                left = sprite.border.x / pixelsPerUnit;
                right = sprite.border.z / pixelsPerUnit;
                float originalWidth = sourceImage.rectTransform.rect.width;
                if (originalWidth != 0f)
                {
                    float pixelAdjustment = width / originalWidth;
                    left *= pixelAdjustment;
                    right *= pixelAdjustment;
                }
                float borders = left + right;
                if (borders > width)
                {
                    float fit = width / borders;
                    left *= fit;
                    right *= fit;
                }
            }
        }

        float filledWidth = width * amount;
        float combinedBorders = left + right;
        float capScale = combinedBorders > filledWidth ? filledWidth / combinedBorders : 1f;
        float filledLeft = left * capScale;
        float filledRight = right * capScale;
        float sourceMiddle = Mathf.Max(0f, width - combinedBorders);
        float filledMiddle = Mathf.Max(0f, filledWidth - filledLeft - filledRight);
        bool fromRight = sourceImage.fillMethod == Image.FillMethod.Horizontal &&
            sourceImage.fillOrigin == (int)Image.OriginHorizontal.Right;
        float destinationLeft = fromRight ? rect.xMax - filledWidth : rect.xMin;
        float sourceRightStart = width - right;

        for (int i = 0; i < count; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            Vector3 position = vertex.position;
            float x = position.x - rect.xMin;
            float mapped;
            if (sourceMiddle <= 0f)
            {
                // The original Image has already shrunk its borders together;
                // both caps now scale proportionally and there is no middle.
                mapped = x * amount;
            }
            else if (left > 0f && x <= left)
            {
                mapped = x * capScale;
            }
            else if (right > 0f && x >= sourceRightStart)
            {
                mapped = filledWidth - (width - x) * capScale;
            }
            else
            {
                mapped = filledLeft + (x - left) * filledMiddle / sourceMiddle;
            }
            // Existing outer padding follows its cap. Preserve UVs, indices,
            // height, tint and the RectTransform, including all child labels.
            position.x = destinationLeft + mapped;
            vertex.position = position;
            vertices.SetUIVertex(vertex, i);
        }
    }
}
