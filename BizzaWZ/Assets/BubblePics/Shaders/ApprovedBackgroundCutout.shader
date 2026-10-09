Shader "BubblePics/UI/ApprovedBackgroundCutout"
{
    Properties
    {
        [PerRendererData] _MainTex("Background source",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        _SourceSize("Source dimensions",Vector)=(852,1846,0,0)
        _Cutout("Live panel rectangle",Vector)=(0,20.5,799,1187)
        _Radius("Panel radius",Float)=64
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
            struct appdata{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
            sampler2D _MainTex;fixed4 _Color;float4 _SourceSize,_Cutout,_ClipRect;float _Radius;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex;o.color=v.color*_Color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*_SourceSize.xy;
                float2 q=abs(p-_Cutout.xy)-_Cutout.zw*.5+_Radius;
                float d=length(max(q,0))+min(max(q.x,q.y),0)-_Radius;
                fixed4 col=tex2D(_MainTex,i.uv)*i.color;
                // Only exterior background pixels are shown. Every control comes from the live prefab.
                col.a*=smoothstep(-.5,.5,d);
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
