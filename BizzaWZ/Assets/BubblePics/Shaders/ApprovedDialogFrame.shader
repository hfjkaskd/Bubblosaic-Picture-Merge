Shader "BubblePics/UI/ApprovedDialogFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SourceRect ("Atlas UV rectangle", Vector) = (0,0,1,1)
        _SourceSize ("Source pixel size", Vector) = (100,100,0,0)
        _OuterRect ("Outer rounded rectangle", Vector) = (0,0,100,100)
        _TopArc ("Top edge curvature in source pixels", Float) = 0
        _TopInset ("Top edge inset in source pixels", Float) = 0
        _OuterRadius ("Outer radius", Float) = 20
        _CapEllipse ("Additional silhouette ellipse", Vector) = (0,0,0,0)
        _MaskMode ("Mask: 0 none, 1 rounded, 2 polygon", Float) = 1
        _EraseOn ("Replace dynamic content area", Float) = 1
        _EraseRect ("Dynamic content rectangle", Vector) = (0,0,80,80)
        _EraseRadius ("Dynamic content radius", Float) = 10
        _EraseFeather ("Dynamic content edge feather", Float) = 1
        _FillSampleU ("Use unobstructed source column, negative disables", Float) = -1
        _FillTop ("Interior top", Color) = (1,1,1,1)
        _FillMiddle ("Interior middle", Color) = (1,1,1,1)
        _FillBottom ("Interior bottom", Color) = (1,1,1,1)
        _P0 ("Polygon 0", Vector) = (0,0,0,0)
        _P1 ("Polygon 1", Vector) = (0,0,0,0)
        _P2 ("Polygon 2", Vector) = (0,0,0,0)
        _P3 ("Polygon 3", Vector) = (0,0,0,0)
        _P4 ("Polygon 4", Vector) = (0,0,0,0)
        _P5 ("Polygon 5", Vector) = (0,0,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd, _FillTop, _FillMiddle, _FillBottom;
            float4 _SourceRect,_SourceSize,_OuterRect,_CapEllipse,_EraseRect,_ClipRect;
            float4 _P0,_P1,_P2,_P3,_P4,_P5;
            float _TopArc,_TopInset;
            float _OuterRadius,_MaskMode,_EraseOn,_EraseRadius,_EraseFeather,_FillSampleU;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;
            }
            float roundedDistance(float2 p,float4 bounds,float radius)
            {
                float2 q=abs(p-bounds.xy)-bounds.zw*.5+radius;
                return length(max(q,0))+min(max(q.x,q.y),0)-radius;
            }
            float edgeDistance(float2 p,float2 a,float2 b)
            {
                float2 edge=b-a;
                return (edge.x*(p.y-a.y)-edge.y*(p.x-a.x))/max(length(edge),.001);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 localUV=(i.uv-_SourceRect.xy)/_SourceRect.zw;
                float2 p=(localUV-.5)*_SourceSize.xy;
                fixed4 col=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                if(_EraseOn>.5)
                {
                    float feather=max(_EraseFeather,.01)*.5;
                    float inside=1-smoothstep(-feather,feather,roundedDistance(p,_EraseRect,_EraseRadius));
                    float t=saturate((p.y-_EraseRect.y)/_EraseRect.w+.5);
                    fixed4 fill=t<.5?lerp(_FillBottom,_FillMiddle,t*2):lerp(_FillMiddle,_FillTop,(t-.5)*2);
                    if(_FillSampleU>=0)
                        fill=tex2D(_MainTex,float2(_SourceRect.x+_FillSampleU*_SourceRect.z,i.uv.y));
                    col=lerp(col,fill,inside);
                }
                if(_MaskMode>.5)
                {
                    float distance=roundedDistance(p,_OuterRect,_OuterRadius);
                    if(_MaskMode>1.5)
                    {
                        distance=max(edgeDistance(p,_P0.xy,_P1.xy),edgeDistance(p,_P1.xy,_P2.xy));
                        distance=max(distance,edgeDistance(p,_P2.xy,_P3.xy));
                        distance=max(distance,edgeDistance(p,_P3.xy,_P4.xy));
                        distance=max(distance,edgeDistance(p,_P4.xy,_P5.xy));
                        distance=max(distance,edgeDistance(p,_P5.xy,_P0.xy));
                    }
                    else if(_CapEllipse.z>0 && _CapEllipse.w>0)
                    {
                        float ellipse=(length((p-_CapEllipse.xy)/_CapEllipse.zw)-1)*min(_CapEllipse.z,_CapEllipse.w);
                        distance=min(distance,ellipse);
                    }
                    col.a*=1-smoothstep(-.5,.5,distance);
                }
                if(_TopArc>0) { float edge=_SourceSize.y*.5-_TopInset-_TopArc*pow(abs(p.x)/(_SourceSize.x*.5),2); col.a*=1-smoothstep(-.5,.5,p.y-edge); }
                col*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a-.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
