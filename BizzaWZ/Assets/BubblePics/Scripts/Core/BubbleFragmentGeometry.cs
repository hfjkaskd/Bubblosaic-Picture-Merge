using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Port of bubble_fragment_geometry.gd. Rect in texture pixels with
    /// TOP-LEFT origin (y down), matching the source image layout.</summary>
    public static class BubbleFragmentGeometry
    {
        public static Rect RegionForPath(int texW, int texH, List<int> path)
        {
            var rect = new Rect(0, 0, texW, texH);
            foreach (int q in path)
            {
                float hw = rect.width * 0.5f;
                float hh = rect.height * 0.5f;
                float ox = rect.x + ((q == 1 || q == 3) ? hw : 0f);
                float oy = rect.y + ((q == 2 || q == 3) ? hh : 0f);
                rect = new Rect(ox, oy, hw, hh);
            }
            return rect;
        }
    }
}
