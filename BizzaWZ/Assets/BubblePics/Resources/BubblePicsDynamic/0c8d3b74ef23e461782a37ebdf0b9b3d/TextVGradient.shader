Shader "BubblePics/TextVGradient"
{
    // Port of text_vgradient.gdshader: vertical gradient tint for UI text.
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _GradTop ("Grad Top", Color) = (1,0.965,0.62,1)
        _GradBottom ("Grad Bottom", Color) = (1,0.902,0.404,1)
        _RectH ("Rect Height", Float) = 70
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
            fixed4 _GradTop;
            fixed4 _GradBottom;
            float _RectH;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float2 localY : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float localY : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.localY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float t = saturate(0.5 - i.localY / max(_RectH, 0.001));
                fixed4 grad = lerp(_GradTop, _GradBottom, t);
                // white text pixels take gradient; outline (colored) stays
                float whiteness = step(0.9, min(min(i.color.r, i.color.g), i.color.b));
                fixed3 rgb = lerp(i.color.rgb, grad.rgb, whiteness);
                return fixed4(rgb, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
