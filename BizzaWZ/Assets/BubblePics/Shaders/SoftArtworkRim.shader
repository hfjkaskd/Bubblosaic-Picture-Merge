Shader "BubblePics/UI/SoftArtworkRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RimTint ("Bright rim tint", Color) = (0.55,0.85,1,1)
        _RimStrength ("Bright rim reduction", Range(0,1)) = 0.6
        _RimReachPx ("Edge search in atlas pixels", Float) = 12
        _HueShift ("Button hue shift", Range(0,1)) = 0
        _Saturation ("Button saturation", Range(0,2)) = 1
        _Brightness ("Button brightness", Range(0,2)) = 1
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
            float4 _MainTex_TexelSize, _ClipRect;
            fixed4 _Color, _RimTint;
            float _RimStrength, _RimReachPx, _HueShift, _Saturation, _Brightness;

            float3 Recolor(float3 rgb)
            {
                float high = max(rgb.r, max(rgb.g, rgb.b));
                float low = min(rgb.r, min(rgb.g, rgb.b));
                float range = high - low;
                float hue = 0;
                if (range > 0.0001)
                {
                    if (high == rgb.r) hue = (rgb.g - rgb.b) / range;
                    else if (high == rgb.g) hue = (rgb.b - rgb.r) / range + 2;
                    else hue = (rgb.r - rgb.g) / range + 4;
                    hue = frac(hue / 6 + _HueShift);
                }
                float saturation = saturate(range / max(high, 0.0001) * _Saturation);
                float3 spectrum = saturate(abs(frac(hue + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
                return saturate(high * _Brightness * lerp(1, spectrum, saturation));
            }

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float2 reach = _MainTex_TexelSize.xy * _RimReachPx;
                float nearAlpha = min(
                    min(tex2D(_MainTex, i.uv + float2(reach.x, 0)).a,
                        tex2D(_MainTex, i.uv - float2(reach.x, 0)).a),
                    min(tex2D(_MainTex, i.uv + float2(0, reach.y)).a,
                        tex2D(_MainTex, i.uv - float2(0, reach.y)).a));
                float brightestNeutral = min(tex.r, min(tex.g, tex.b));
                float saturation = max(tex.r, max(tex.g, tex.b)) - brightestNeutral;
                float white = smoothstep(0.72, 0.94, brightestNeutral)
                    * (1 - smoothstep(0.14, 0.40, saturation));
                float edge = saturate((1 - nearAlpha) * 2);
                tex.rgb = lerp(tex.rgb, tex.rgb * _RimTint.rgb, white * edge * _RimStrength);
                if (abs(_HueShift) + abs(_Saturation - 1) + abs(_Brightness - 1) > 0.0001)
                    tex.rgb = Recolor(tex.rgb);
                fixed4 col = tex * i.color;
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
