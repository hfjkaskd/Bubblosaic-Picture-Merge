Shader "Hidden/BubblePics/ReleaseBake/TutorialFocusHalo"
{
 Properties { _DimCorners("Shade outside rounded focus",Float)=0 _CornerRadius("Focus radius",Float)=.5 _Color("Glow",Color)=(1,.72,.1,1) _Width("Ring thickness",Float)=.012 _GlowWidth("Glow falloff",Float)=.05 _Radius("Corner radius",Float)=.34 _StencilComp("Stencil Comparison",Float)=8 _Stencil("Stencil ID",Float)=0 _StencilOp("Stencil Operation",Float)=0 _StencilWriteMask("Stencil Write Mask",Float)=255 _StencilReadMask("Stencil Read Mask",Float)=255 _ColorMask("Color Mask",Float)=15 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"} Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]} Cull Off Lighting Off ZWrite Off ZTest Always Blend One Zero ColorMask [_ColorMask]
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _DimCorners,_CornerRadius;
 struct A{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};struct V{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};fixed4 _Color;float _Width,_GlowWidth,_Radius;
 V vert(A a){V v;v.vertex=UnityObjectToClipPos(a.vertex);v.uv=a.uv;v.color=a.color;return v;}
 fixed4 frag(V v):SV_Target{if(_DimCorners>0){float2 p=abs(v.uv-.5)-.5+_CornerRadius;float edge=length(max(p,0))+min(max(p.x,p.y),0)-_CornerRadius;return fixed4(_Color.rgb,smoothstep(-.007,.005,edge)*_Color.a)*v.color;}float2 q=abs(v.uv-.5)-.38+_Radius;float d=abs(length(max(q,0))+min(max(q.x,q.y),0)-_Radius);float core=1-smoothstep(_Width*.4,_Width*1.6,d);float glow=exp(-d*d/max(_GlowWidth*_GlowWidth,.0001))*.65;return fixed4(lerp(_Color.rgb,float3(1,1,.86),core),max(core,glow)*_Color.a)*v.color;}
 ENDCG }
 }
}
