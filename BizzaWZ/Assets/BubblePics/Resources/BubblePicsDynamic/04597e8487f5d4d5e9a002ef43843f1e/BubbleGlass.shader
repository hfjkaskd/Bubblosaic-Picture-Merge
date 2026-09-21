Shader "BubblePics/BubbleGlass"
{
    // Port of bubble.gdshader (glass ball + selected ring + dissolve).
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _BaseTex ("Base Tex", 2D) = "white" {}
        _BaseStrength ("Base Strength", Range(0,2)) = 1.0
        _Tint ("Tint", Color) = (1,1,1,1)
        _BodyAlpha ("Body Alpha", Range(0,1)) = 1.0
        _RimWidth ("Rim Width", Range(0,0.5)) = 0.10
        _RimIntensity ("Rim Intensity", Range(0,4)) = 0.0
        _SpecularOffset ("Specular Offset", Vector) = (-0.28,-0.32,0,0)
        _SpecularRadius ("Specular Radius", Range(0,0.6)) = 0.18
        _SpecularIntensity ("Specular Intensity", Range(0,2)) = 0.0
        _EdgeSoftness ("Edge Softness", Range(0,0.1)) = 0.02
        _Selected ("Selected", Range(0,1)) = 0.0
        _SelectedColor ("Selected Color", Color) = (0.6,0.95,1,1)
        _DissolveNoise ("Dissolve Noise", 2D) = "white" {}
        _DissolveProgress ("Dissolve Progress", Range(0,1.5)) = 0.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0,0.5)) = 0.06
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (0.6,0.95,1,1)
        _DissolveDirection ("Dissolve Direction", Vector) = (0,0,0,0)
        _DissolveNoiseStrength ("Dissolve Noise Strength", Range(0,1)) = 0.30
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseTex;
            float _BaseStrength;
            fixed4 _Tint;
            float _BodyAlpha;
            float _RimWidth;
            float _RimIntensity;
            float4 _SpecularOffset;
            float _SpecularRadius;
            float _SpecularIntensity;
            float _EdgeSoftness;
            float _Selected;
            fixed4 _SelectedColor;
            sampler2D _DissolveNoise;
            float _DissolveProgress;
            float _DissolveEdgeWidth;
            fixed4 _DissolveEdgeColor;
            float4 _DissolveDirection;
            float _DissolveNoiseStrength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // convert to Godot uv orientation (v down) for offsets
                float2 uvG = float2(i.uv.x, 1.0 - i.uv.y);
                float modA = i.color.a;
                float2 p = uvG * 2.0 - 1.0;
                float r = length(p);

                float disc = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, r);
                if (disc <= 0.001) discard;

                fixed4 sampled = tex2D(_BaseTex, i.uv);
                float3 body = sampled.rgb * _Tint.rgb * _BaseStrength;

                float rimPos = 1.0 - _RimWidth * 0.5;
                float rim = smoothstep(rimPos - _RimWidth, rimPos, r) - smoothstep(rimPos, rimPos + _RimWidth * 0.5, r);
                rim = saturate(rim) * _RimIntensity;
                float3 rimCol = lerp(_Tint.rgb, float3(1,1,1), 0.85);

                float specD = length(p - _SpecularOffset.xy);
                float spec = smoothstep(_SpecularRadius, _SpecularRadius * 0.35, specD) * _SpecularIntensity;
                float spec2 = smoothstep(0.045, 0.0, length(p - float2(0.30, 0.25))) * 0.8 * _SpecularIntensity;

                float selRing = smoothstep(0.78, 0.97, r) * (1.0 - smoothstep(0.97, 1.0, r));
                float selStrength = _Selected * selRing;

                float3 col = body + rimCol * rim + float3(1,1,1) * (spec + spec2) + _SelectedColor.rgb * selStrength * 1.4;
                float a = sampled.a * _BodyAlpha * disc + rim * 0.5 + spec * 0.6 + spec2 * 0.6 + selStrength * 0.9;
                a = saturate(a) * disc;

                if (_DissolveProgress > 0.0)
                {
                    float n = tex2D(_DissolveNoise, i.uv).r;
                    float phase;
                    if (length(_DissolveDirection.xy) < 0.001) phase = r;
                    else phase = saturate(dot(uvG, normalize(_DissolveDirection.xy)));
                    float threshold = phase + (n - 0.5) * _DissolveNoiseStrength;
                    if (threshold < _DissolveProgress) discard;
                    float edgeT = smoothstep(threshold - _DissolveEdgeWidth, threshold, _DissolveProgress);
                    col = lerp(col, _DissolveEdgeColor.rgb, edgeT);
                    a = saturate(a + edgeT * 0.9) * disc;
                }

                return fixed4(col, a * modA);
            }
            ENDCG
        }
    }
}
