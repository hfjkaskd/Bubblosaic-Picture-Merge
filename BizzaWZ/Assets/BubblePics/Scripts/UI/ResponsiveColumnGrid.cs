using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Retains prefab-authored rows and spacing while fitting fixed columns to the available width.</summary>
    [AddComponentMenu("Layout/Responsive Column Grid")]
    public sealed class ResponsiveColumnGrid : GridLayoutGroup
    {
        public override void SetLayoutHorizontal()
        {
            if (constraint == Constraint.FixedColumnCount)
            {
                int columns = Mathf.Max(1, constraintCount);
                float width = Mathf.Max(0, (rectTransform.rect.width - padding.horizontal - spacing.x * (columns - 1)) / columns);
                if (!Mathf.Approximately(cellSize.x, width)) cellSize = new Vector2(width, cellSize.y);
            }
            base.SetLayoutHorizontal();
        }
    }
}
