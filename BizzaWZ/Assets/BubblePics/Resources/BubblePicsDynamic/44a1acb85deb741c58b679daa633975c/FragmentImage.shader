Shader "BubblePics/FragmentImage"
{
    // Port of fragment_image.gdshader: picture_round inner corners + seam feather.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CornerRadius ("Corner Radius", Range(0,0.5)) = 0.0
        _CornerMask ("Corner Mask", Float) = 15
        _EdgeMask ("Edge Mask", Float) = 15
        _UvRegion ("UV Region", Vector) = (0,0,1,1)
        _FillCornerRadius ("Fill Corner Radius", Range(0,0.5)) = 0.0
        _FillCornerMask ("Fill Corner Mask", Float) = 0
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

            sampler2D _MainTex;
            float _CornerRadius;
            float _CornerMask;
            float _EdgeMask;
            float4 _UvRegion;
            float _FillCornerRadius;
            float _FillCornerMask;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float photoAlphaWithSeam(float2 uv, float cornerRadius, int cornerMask, int edgeMask, float fillRadius, int fillMask)
            {
                int idx;
                if (uv.y < 0.5) idx = (uv.x < 0.5) ? 0 : 1;
                else idx = (uv.x < 0.5) ? 2 : 3;
                bool fillThis = ((fillMask >> idx) & 1) == 1;
                bool roundThis = fillThis || (((cornerMask >> idx) & 1) == 1);
                float cr = fillThis ? fillRadius : cornerRadius;
                float2 p = abs(uv - 0.5);
                float2 q = p - (0.5 - cr);
                const float FAR = 10.0;
                float dTop = ((edgeMask & 1) != 0) ? uv.y : FAR;
                float dRight = ((edgeMask & 2) != 0) ? (1.0 - uv.x) : FAR;
                float dBottom = ((edgeMask & 4) != 0) ? (1.0 - uv.y) : FAR;
                float dLeft = ((edgeMask & 8) != 0) ? uv.x : FAR;
                float dEdge = min(min(dTop, dRight), min(dBottom, dLeft));
                float dist;
                if (roundThis && q.x > 0.0 && q.y > 0.0) dist = cr - length(q);
                else dist = dEdge;
                float aa = fwidth(dist);
                return smoothstep(0.0, aa * 1.5, dist);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Note: sprite uv.y in Unity increases upward; Godot's UV.y increases
                // downward. Flip local v so masks (bit0=TL etc.) match Godot.
                float2 localUv = (i.uv - _UvRegion.xy) / _UvRegion.zw;
                localUv.y = 1.0 - localUv.y;
                fixed4 c = tex2D(_MainTex, i.uv);
                float a = photoAlphaWithSeam(localUv, _CornerRadius, (int)_CornerMask, (int)_EdgeMask, _FillCornerRadius, (int)_FillCornerMask);
                return fixed4(c.rgb * i.color.rgb, c.a * a * i.color.a);
            }
            ENDCG
        }
    }
}
