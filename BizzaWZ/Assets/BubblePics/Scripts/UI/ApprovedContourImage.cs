using UnityEngine;
using UnityEngine.UI;
namespace BubblePics
{
    // The artist-authored silhouette is triangulated offline; runtime only emits its small UI mesh.
    public sealed class ApprovedContourImage : Image
    {
        [SerializeField] Vector2[] contour;
        [SerializeField] int[] triangles;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if(contour==null||triangles==null||contour.Length<3){base.OnPopulateMesh(vh);return;}
            vh.Clear();Rect r=GetPixelAdjustedRect();Vector4 uv=sprite!=null?UnityEngine.Sprites.DataUtility.GetOuterUV(sprite):new Vector4(0,0,1,1);
            for(int i=0;i<contour.Length;i++){Vector2 p=contour[i];var v=UIVertex.simpleVert;v.color=color;v.position=new Vector3(r.x+p.x*r.width,r.y+(1-p.y)*r.height);v.uv0=new Vector2(Mathf.Lerp(uv.x,uv.z,p.x),Mathf.Lerp(uv.y,uv.w,1-p.y));vh.AddVert(v);}
            for(int i=0;i+2<triangles.Length;i+=3)vh.AddTriangle(triangles[i],triangles[i+1],triangles[i+2]);
        }
    }
}
