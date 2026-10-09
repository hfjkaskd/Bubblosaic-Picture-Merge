Shader "BubblePics/UI/NewPlayerReferencePlate"
{
    Properties
    {
        [PerRendererData] _MainTex("Source artwork",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _TextureSize("Texture dimensions",Vector)=(852,1846,0,0)
        _Erase0("Content slot 0: left top width height",Vector)=(0,0,0,0)
        _Erase1("Content slot 1",Vector)=(0,0,0,0)
        _Erase2("Content slot 2",Vector)=(0,0,0,0)
        _Erase3("Content slot 3",Vector)=(0,0,0,0)
        _Erase4("Content slot 4",Vector)=(0,0,0,0)
        _Erase5("Content slot 5",Vector)=(0,0,0,0)
        _SamplePoint("Clean pixel: x y enabled",Vector)=(0,0,0,0)
        _SampleX("Clean source column; negative uses row",Float)=-1
        _SampleY("Clean source row; negative interpolates edges",Float)=-1
        _VisibleRect("Visible rounded source bounds",Vector)=(0,0,0,0)
        _Radius("Corner radius",Float)=0
        _Feather("Edge feather",Float)=1
        _EraseRadius("Content-slot corner radius",Float)=0
        _EraseFeather("Content-slot edge blend",Float)=0.5
        _InkOnly("Remove light matte behind blue ink",Float)=0
        _MirrorPatch("Reconstruct circular edge behind badge",Vector)=(0,0,0,0)
        _MirrorY("Source circle center Y",Float)=0
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
            sampler2D _MainTex;fixed4 _Color;
            float4 _SamplePoint;
            float _InkOnly,_MirrorY;float4 _MirrorPatch;
            float4 _TextureSize,_Erase0,_Erase1,_Erase2,_Erase3,_Erase4,_Erase5,_ClipRect;
            float _SampleX,_SampleY,_Radius,_Feather,_EraseRadius,_EraseFeather;float4 _VisibleRect;
            v2f vert(appdata v) {v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex;o.color=v.color*_Color;o.uv=v.uv;return o;}
            float inside(float2 p,float4 r)
            {
                if(_EraseRadius<=0)
                {
                    float2 edge=min(p-r.xy,r.xy+r.zw-p);
                    return smoothstep(-_EraseFeather,_EraseFeather,min(edge.x,edge.y));
                }
                float radius=min(_EraseRadius,min(r.z,r.w)*.5);
                float2 q=abs(p-r.xy-r.zw*.5)-r.zw*.5+radius;
                float distance=length(max(q,0))+min(max(q.x,q.y),0)-radius;
                return (1-smoothstep(-_EraseFeather,_EraseFeather,distance))*step(.1,min(r.z,r.w));
            }
            fixed4 clearSlot(fixed4 color,float2 p,float2 uv,float4 slot)
            {
                if(slot.z<=0 || slot.w<=0)return color;
                float mask=inside(p,slot);
                if(mask>0)
                {
                    fixed4 fill;
                    if(_SamplePoint.z>0)fill=tex2D(_MainTex,float2(_SamplePoint.x/_TextureSize.x,1-_SamplePoint.y/_TextureSize.y));
                    else if(_SampleX>=0)fill=tex2D(_MainTex,float2(_SampleX/_TextureSize.x,uv.y));
                    else if(_SampleY>=0)fill=tex2D(_MainTex,float2(uv.x,1-_SampleY/_TextureSize.y));
                    else
                    {
                        // Interpolate the clean edges of this slot, preserving the
                        // panel's local lighting instead of adding a flat rectangle.
                        fixed4 top=tex2D(_MainTex,float2(uv.x,1-(slot.y-1)/_TextureSize.y));
                        fixed4 bottom=tex2D(_MainTex,float2(uv.x,1-(slot.y+slot.w+1)/_TextureSize.y));
                        fill=lerp(top,bottom,saturate((p.y-slot.y)/max(slot.w,1)));
                    }
                    color=lerp(color,fill,mask);
                }
                return color;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=float2(i.uv.x,1-i.uv.y)*_TextureSize.xy;
                fixed4 col=tex2D(_MainTex,i.uv);
                // Remove all baked control/content slots. Their separate prefab children
                // supply native state, amount, logo, captions and standard Buttons.
                col=clearSlot(col,p,i.uv,_Erase0);col=clearSlot(col,p,i.uv,_Erase1);
                col=clearSlot(col,p,i.uv,_Erase2);col=clearSlot(col,p,i.uv,_Erase3);
                col=clearSlot(col,p,i.uv,_Erase4);col=clearSlot(col,p,i.uv,_Erase5);
                if(_MirrorPatch.z>0&&p.x>=_MirrorPatch.x&&p.x<=_MirrorPatch.x+_MirrorPatch.z&&p.y>=_MirrorPatch.y&&p.y<=_MirrorPatch.y+_MirrorPatch.w)
                    col=tex2D(_MainTex,float2(i.uv.x,1-(2*_MirrorY-p.y)/_TextureSize.y));
                if(_InkOnly>0)col.a*=smoothstep(.08,.19,col.b-col.r);
                if(_VisibleRect.z>0){float2 q=abs(p-_VisibleRect.xy-_VisibleRect.zw*.5)-_VisibleRect.zw*.5+_Radius;float d=length(max(q,0))+min(max(q.x,q.y),0)-_Radius;col.a*=1-smoothstep(-_Feather,0,d);}
                col*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
