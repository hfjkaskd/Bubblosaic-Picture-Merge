Shader "BubblePics/SplashStripes"
{
    // Port of splash_progress_stripes.gdshader — green capsule with scrolling stripes.
    Properties
    {
        _MainTex ("Tex", 2D) = "white" {}
        _BarPx ("Bar Px", Vector) = (270, 40, 0, 0)
        _CornerRadius ("Corner Radius", Float) = 19
        _StripePeriod ("Stripe Period", Float) = 40
        _StripeSlope ("Stripe Slope", Float) = 0.7
        _ScrollSpeed ("Scroll Speed", Float) = 56
        _StripeSoftness ("Stripe Softness", Float) = 0.12
        _ColorDark ("Dark", Color) = (0.063, 0.769, 0.122, 1)
        _ColorLight ("Light", Color) = (0.153, 0.835, 0.220, 1)
        _Gloss ("Gloss", Float) = 0.06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BarPx;
            float _CornerRadius, _StripePeriod, _StripeSlope, _ScrollSpeed, _StripeSoftness, _Gloss;
            fixed4 _ColorDark, _ColorLight;

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
                float2 px = float2(i.uv.x * _BarPx.x, (1.0 - i.uv.y) * _BarPx.y);
                // capsule sdf
                float r = min(_CornerRadius, _BarPx.y * 0.5);
                float2 half_ = _BarPx.xy * 0.5;
                float2 d2 = abs(px - half_) - (half_ - r);
                float dist = length(max(d2, 0.0)) - r;
                float aa = fwidth(dist);
                float mask = 1.0 - smoothstep(0.0, aa * 1.5, dist);
                if (mask <= 0.001) discard;

                float diag = px.x + px.y * _StripeSlope + _Time.y * _ScrollSpeed;
                float phase = frac(diag / _StripePeriod);
                float stripe = smoothstep(0.5 - _StripeSoftness, 0.5 + _StripeSoftness, abs(phase * 2.0 - 1.0));
                fixed3 col = lerp(_ColorLight.rgb, _ColorDark.rgb, stripe);
                float gloss = _Gloss * smoothstep(0.6, 0.0, i.uv.y * -1.0 + 1.0);
                col += gloss;
                return fixed4(col, mask * i.color.a);
            }
            ENDCG
        }
    }
}
