using UnityEngine;
using UnityEngine.UI;
namespace BubblePics
{
    public sealed class TransitionCoverImage : Image
    {
        [SerializeField] private Vector2[] vertices;
        [SerializeField] private int[] triangles;
        [SerializeField] private int innerCount;
        [SerializeField,Range(0,1)] private float closure;
        [SerializeField] private Vector2 center=new Vector2(.5f,.47f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(vertices==null||triangles==null)return;
            Rect r=GetPixelAdjustedRect();Vector4 uv=sprite!=null?UnityEngine.Sprites.DataUtility.GetOuterUV(sprite):new Vector4(0,0,1,1);
            for(int i=0;i<vertices.Length;i++)
            {
                Vector2 source=vertices[i],position=i<innerCount?Vector2.Lerp(source,center,closure):source;
                var vertex=UIVertex.simpleVert;vertex.color=color;vertex.position=new Vector3(r.x+position.x*r.width,r.y+(1-position.y)*r.height);
                vertex.uv0=new Vector2(Mathf.Lerp(uv.x,uv.z,source.x),Mathf.Lerp(uv.y,uv.w,1-source.y));vh.AddVert(vertex);
            }
            for(int i=0;i<triangles.Length;i+=3)vh.AddTriangle(triangles[i],triangles[i+1],triangles[i+2]);
        }
    }
}
