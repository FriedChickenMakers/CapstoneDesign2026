Shader "Capstone/Plant Memo Paper"
{
    Properties { _BaseMap("Paper texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=position.positionCS; output.positionWS=position.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv; output.color=input.color; output.fog=ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 normal=NormalizeNormalPerPixel(input.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                // Thin paper scatters sunlight from its back as well as its front.
                // This fill still follows the actual sun/moon color and intensity;
                // it does not make the notes glow independently after dark.
                half paperDiffuse=.45h+.55h*abs(dot(normal,light.direction));
                half3 lighting=SampleSH(normal)+light.color*paperDiffuse*light.distanceAttenuation*light.shadowAttenuation;
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*input.color.rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    albedo=LinearToSRGB(albedo);
                #endif
                return half4(MixFog(albedo*lighting,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 DepthVert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFrag(float4 positionCS:SV_POSITION):SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }
}
