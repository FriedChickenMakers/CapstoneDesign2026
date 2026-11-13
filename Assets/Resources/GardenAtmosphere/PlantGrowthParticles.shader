Shader "Capstone/Plant Growth Particles"
{
    Properties { _Round("Round particle",Float)=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float _Round;
            CBUFFER_END
            V Vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.uv=a.uv; o.color=a.color; return o; }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float alpha=_Round>.5?saturate(1-dot(p,p)):saturate(1-abs(p.y));
                return half4(i.color.rgb*1.8h,i.color.a*alpha*alpha);
            }
            ENDHLSL
        }
    }
}
