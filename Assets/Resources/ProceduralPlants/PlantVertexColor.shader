Shader "MindfulGarden/ProceduralPlantVertexColor"
{
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Stable soft art lighting, including leaf undersides. Inputs are linear RGB.
                half diffuse=abs(dot(normalize(input.normalWS),normalize(half3(-.4,.85,-.3))));
                half3 color=input.color.rgb*(.60+.40*diffuse);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    color=LinearToSRGB(color);
                #endif
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
