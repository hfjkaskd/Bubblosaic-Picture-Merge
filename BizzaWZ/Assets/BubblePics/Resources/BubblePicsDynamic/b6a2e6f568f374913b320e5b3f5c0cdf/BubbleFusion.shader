Shader "BubblePics/BubbleFusion"
{
    // Port of bubble_fusion.gdshader (metaball glass blob, up to 4 circles).
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _BaseTex ("Base Tex", 2D) = "white" {}
        _QuadPx ("Quad Px", Vector) = (256,256,0,0)
        _SmoothK ("Smooth K", Range(0,1024)) = 50
        _MergeT ("Merge T", Range(0,1)) = 0
        _RimWidthPx ("Rim Width Px", Range(0,64)) = 9
        _RimIntensity ("Rim Intensity", Range(0,4)) = 0.6
        _RimEndIntensity ("Rim End Intensity", Range(0,4)) = 0.6
        _WobbleAxis ("Wobble Axis", Vector) = (1,0,0,0)
        _WobbleE ("Wobble E", Float) = 0
        _CircleCount ("Circle Count", Float) = 2
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
            float4 _Circles[4]; // xy=center px (godot y-down local), z=radius px
            float _CircleCount;
            float4 _QuadPx;
            float _SmoothK;
            float _MergeT;
            float _RimWidthPx;
            float _RimIntensity;
            float _RimEndIntensity;
            float4 _WobbleAxis;
            float _WobbleE;

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

            float sminf(float a, float b, float k)
            {
                if (k <= 0.0001) return min(a, b);
                float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                int n = clamp((int)_CircleCount, 1, 4);
                float2 uvG = float2(i.uv.x, 1.0 - i.uv.y); // godot v-down
                float2 p = uvG * _QuadPx.xy;

                if (abs(_WobbleE) > 0.0005)
                {
                    float2 pivot = float2(0, 0);
                    for (int ii = 0; ii < 4; ii++) if (ii < n) pivot += _Circles[ii].xy;
                    pivot /= (float)n;
                    float2 nrm = normalize(_WobbleAxis.xy + float2(1e-5, 0));
                    float2 rel = p - pivot;
                    float al = dot(rel, nrm);
                    float2 alV = al * nrm;
                    float2 peV = rel - alV;
                    float along = 1.0 + _WobbleE;
                    float perp = 1.0 - 0.5 * _WobbleE;
                    p = pivot + alV / along + peV / perp;
                }

                float k = _SmoothK * (1.0 - _MergeT);
                float d = length(p - _Circles[0].xy) - _Circles[0].z;
                float rmax = _Circles[0].z;
                for (int i2 = 1; i2 < 4; i2++)
                {
                    if (i2 < n)
                    {
                        float di = length(p - _Circles[i2].xy) - _Circles[i2].z;
                        d = sminf(d, di, k);
                        rmax = max(rmax, _Circles[i2].z);
                    }
                }

                float aa = fwidth(d) + 0.0001;
                float edgeSoft = max(aa, 0.02 * rmax);
                float disc = 1.0 - smoothstep(-edgeSoft, 0.0, d);
                if (disc <= 0.001) discard;

                float kk = max(_SmoothK, aa);
                float diMin = length(p - _Circles[0].xy) - _Circles[0].z;
                for (int i3 = 1; i3 < 4; i3++)
                    if (i3 < n) diMin = min(diMin, length(p - _Circles[i3].xy) - _Circles[i3].z);
                float2 dir = float2(0, 0);
                float reach = 0.0;
                float wsum = 0.0;
                for (int i4 = 0; i4 < 4; i4++)
                {
                    if (i4 < n)
                    {
                        float di = length(p - _Circles[i4].xy) - _Circles[i4].z;
                        float w = exp(clamp((diMin - di) / kk, -20.0, 0.0));
                        float2 dirI = normalize(p - _Circles[i4].xy + float2(1e-4, 1e-4));
                        dir += dirI * w;
                        reach += _Circles[i4].z * w;
                        wsum += w;
                    }
                }
                reach = max(reach / max(wsum, 1e-4), aa);
                dir = normalize(dir + float2(1e-4, 1e-4));
                float texR = 1.0 - clamp(-d / reach, 0.0, 1.0);
                float2 uvS = float2(0.5, 0.5) + dir * texR * 0.5;
                // dir computed in godot v-down space; flip v to sample Unity texture
                uvS.y = 1.0 - uvS.y;
                fixed4 glass = tex2D(_BaseTex, clamp(uvS, 0.0, 1.0));

                float rim = (1.0 - smoothstep(0.0, _RimWidthPx, -d)) * smoothstep(0.0, aa * 2.0, -d);
                float rimAmt = lerp(_RimIntensity, _RimEndIntensity, smoothstep(0.6, 1.0, _MergeT));
                rim = saturate(rim) * rimAmt;

                float3 col = glass.rgb + float3(1, 1, 1) * rim;
                float a = max(glass.a, rim * 0.5);
                a = saturate(a) * disc;
                return fixed4(col, a * i.color.a);
            }
            ENDCG
        }
    }
}
