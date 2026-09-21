Shader "BubblePics/SpineLite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        _DstBlend ("Dst Blend", Float) = 10  // OneMinusSrcAlpha
        [HideInInspector] _Pma ("Premultiplied Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed _Pma;

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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb *= lerp(1.0, i.color.a, _Pma);
                return c * i.color;
            }
            ENDCG
        }
    }
}
