using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    // Four prefab-authored panels leave the real target visible and clickable.
    [DisallowMultipleComponent]
    public sealed class TutorialFocusBackdrop : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform focus;
        [SerializeField] private Image top;
        [SerializeField] private Image bottom;
        [SerializeField] private Image left;
        [SerializeField] private Image right;
        [SerializeField] private Image corners;
        private readonly Vector3[] worldCorners = new Vector3[4];
        private Rect previous;
        private Vector2 previousViewport;
        private Color previousColor;
        private bool dirty=true;
        private void OnEnable(){dirty=true;}
        private void LateUpdate()
        {
            RefreshLayout();
        }
        public void RefreshLayout()
        {
            if(viewport==null||focus==null||top==null)return;
            focus.GetWorldCorners(worldCorners);
            Vector3 lo=viewport.InverseTransformPoint(worldCorners[0]),hi=viewport.InverseTransformPoint(worldCorners[2]);
            Rect v=viewport.rect;
            Rect hole=Rect.MinMaxRect(Mathf.Clamp(lo.x,v.xMin,v.xMax),Mathf.Clamp(lo.y,v.yMin,v.yMax),Mathf.Clamp(hi.x,v.xMin,v.xMax),Mathf.Clamp(hi.y,v.yMin,v.yMax));
            if(!dirty&&hole==previous&&v.size==previousViewport&&top.color==previousColor)return;
            dirty=false;previous=hole;previousViewport=v.size;previousColor=top.color;
            if(corners!=null)corners.color=previousColor;
            Place(top,Rect.MinMaxRect(v.xMin,hole.yMax,v.xMax,v.yMax));
            Place(bottom,Rect.MinMaxRect(v.xMin,v.yMin,v.xMax,hole.yMin));
            Place(left,Rect.MinMaxRect(v.xMin,hole.yMin,hole.xMin,hole.yMax));
            Place(right,Rect.MinMaxRect(hole.xMax,hole.yMin,v.xMax,hole.yMax));
        }
        private void Place(Image image,Rect area)
        {
            image.color=previousColor;
            image.rectTransform.anchoredPosition=area.center;
            image.rectTransform.sizeDelta=area.size;
        }
    }
}
