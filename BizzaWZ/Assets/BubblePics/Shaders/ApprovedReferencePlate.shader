Shader "BubblePics/UI/ApprovedReferencePlate"
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
        _SampleX("Clean source column; negative uses row",Float)=-1
        _SampleY("Clean source row",Float)=1334
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
            float4 _TextureSize,_Erase0,_Erase1,_Erase2,_Erase3,_Erase4,_Erase5,_ClipRect;
            float _SampleX,_SampleY;
            v2f vert(appdata v) {v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex;o.color=v.color*_Color;o.uv=v.uv;return o;}
            float inside(float2 p,float4 r)
            {
                float2 q=abs(p-r.xy-r.zw*.5)-r.zw*.5;
                return (1-smoothstep(-.5,.5,max(q.x,q.y)))*step(.1,min(r.z,r.w));
            }
            fixed4 clearSlot(fixed4 color,float2 p,float2 uv,float4 slot)
            {
                float mask=inside(p,slot);
                if(mask>0)
                {
                    fixed4 fill;
                    if(_SampleX>=0)fill=tex2D(_MainTex,float2(_SampleX/_TextureSize.x,uv.y));
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
