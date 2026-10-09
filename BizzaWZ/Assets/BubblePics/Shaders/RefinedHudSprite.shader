Shader "BubblePics/UI/RefinedHudSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AtlasSize ("Atlas pixel size", Vector) = (2036,516,0,0)
        _SpriteRect ("Sprite pixel rect", Vector) = (0,0,285,145)
        _Radius ("Authored corner radius", Float) = 42
        _RimWidth ("Visible rim in source pixels", Float) = 3
        _RimFeather ("Rim to fill blend in source pixels", Float) = 4
        _RimDark ("Rim shadow", Color) = (0.02,0.57,0.8,1)
        _RimLight ("Rim highlight", Color) = (0.35,0.88,1,1)
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _AtlasSize, _SpriteRect, _ClipRect;
            fixed4 _Color, _RimDark, _RimLight;
            float _Radius, _RimWidth, _RimFeather;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float roundedDistance(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Work in the sprite's authored pixels; Image UVs point into the shared atlas.
                float2 spriteUv = (i.uv * _AtlasSize.xy - _SpriteRect.xy) / _SpriteRect.zw;
                float2 p = (spriteUv - 0.5) * _SpriteRect.zw;
                float outside = roundedDistance(p, _SpriteRect.zw * 0.5, _Radius);
                float inward = -outside;

                fixed4 authored = tex2D(_MainTex, i.uv);
                float fillY = clamp(spriteUv.y, 0.18, 0.82);
                float2 fillUv = (_SpriteRect.xy + _SpriteRect.zw * float2(0.5, fillY)) / _AtlasSize.xy;
                fixed3 fill = tex2D(_MainTex, fillUv).rgb;

                float luma = dot(authored.rgb, fixed3(0.25, 0.62, 0.13));
                float gloss = smoothstep(0.15, 0.96, luma);
                fixed3 rim = lerp(_RimDark.rgb, _RimLight.rgb, gloss);
                rim = lerp(rim, _RimLight.rgb, smoothstep(0.65, 1.0, spriteUv.y) * 0.12);
                float fillBlend = smoothstep(_RimWidth, _RimWidth + _RimFeather, inward);
                fixed4 col = fixed4(lerp(rim, fill, fillBlend), authored.a) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
