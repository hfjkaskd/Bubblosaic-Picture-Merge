Shader "BubblePics/StickerImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Source Image", 2D) = "white" {}
        _MaskTex ("Sticker Mask", 2D) = "white" {}
        _UvRegion ("Source UV Region", Vector) = (0,0,1,1)
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _UvRegion;
            fixed4 _Color;

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

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 localUv =
                    (input.uv - _UvRegion.xy) / _UvRegion.zw;
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                fixed maskAlpha = tex2D(
                    _MaskTex,
                    saturate(localUv)).a;
                color.a *= maskAlpha;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
