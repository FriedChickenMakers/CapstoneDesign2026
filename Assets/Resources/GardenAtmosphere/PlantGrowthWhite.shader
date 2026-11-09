Shader "Capstone/Plant Growth White"
{
    Properties { _Glow("White glow",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float _Glow;
            CBUFFER_END
            V Vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.color=a.color; return o; }
            half4 Frag(V i):SV_Target
            {
                half shimmer=.12h*sin(i.positionWS.y*19-_Time.y*7);
                return half4(lerp(i.color.rgb,half3(2.6h+shimmer,2.7h+shimmer,2.8h+shimmer),_Glow),1);
            }
            ENDHLSL
        }
    }
}
