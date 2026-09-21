Shader "BubblePics/WaterRipple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CausticsTex ("Caustics", 2D) = "gray" {}
        _ScaleU ("Scale", Float) = 3.5
        _Aspect ("Aspect Correction", Float) = 2.22
        _HStretch ("Horizontal Stretch", Float) = 1.6
        _ScrollA ("Scroll A", Vector) = (0.1, 0.06, 0, 0)
        _ScrollB ("Scroll B", Vector) = (-0.09, 0.075, 0, 0)
        _RippleFreq ("Ripple Frequency", Float) = 3.0
        _RippleAmp ("Ripple Amplitude", Float) = 0.06
        _Intensity ("Intensity", Float) = 0.05
        _Tint ("Tint", Color) = (0.85, 0.95, 1, 1)
        _BandHeight ("Top Band Height", Float) = 0.167
        _BandFade ("Top Band Fade", Float) = 0.067
        _Persp ("Perspective Strength", Float) = 5.0
        _PerspCurve ("Perspective Curve", Float) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _CausticsTex;
            float _ScaleU;
            float _Aspect;
            float _HStretch;
            float4 _ScrollA;
            float4 _ScrollB;
            float _RippleFreq;
            float _RippleAmp;
            float _Intensity;
            fixed4 _Tint;
            float _BandHeight;
            float _BandFade;
            float _Persp;
            float _PerspCurve;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Unity sprite UVs run bottom-up; Godot's CanvasItem UV used
                // by the source effect runs down from the top of the screen.
                float2 screenUv = float2(input.uv.x, 1.0 - input.uv.y);
                float bandEnd = max(_BandHeight + _BandFade, 0.001);
                clip(bandEnd - screenUv.y);

                float depth = saturate(screenUv.y / bandEnd);
                float perspectiveT = pow(depth, _PerspCurve);
                float zoom = lerp(1.0, 1.0 + _Persp, perspectiveT);
                float2 uv = float2(
                    (screenUv.x - 0.5) * zoom / _HStretch + 0.5,
                    screenUv.y * _Aspect * zoom) * _ScaleU;
                float2 ripple = float2(
                    sin(screenUv.y * _RippleFreq + _Time.y * 0.8),
                    cos(screenUv.x * _RippleFreq + _Time.y * 0.6)) *
                    _RippleAmp;
                float c1 = tex2D(_CausticsTex,
                    uv + _ScrollA.xy * _Time.y + ripple).r;
                float c2 = tex2D(_CausticsTex,
                    uv * 0.7 + _ScrollB.xy * _Time.y + ripple).r;
                float caustic = c1 * c2 * 1.5 + (c1 + c2) * 0.2;
                float mask = 1.0 - smoothstep(
                    _BandHeight,
                    _BandHeight + _BandFade,
                    screenUv.y);
                return fixed4(
                    _Tint.rgb,
                    caustic * _Intensity * mask * input.color.a);
            }
            ENDCG
        }
    }
}
