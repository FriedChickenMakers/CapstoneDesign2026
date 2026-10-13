Shader "MindfulGarden/ProceduralPlantVertexColor"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="SimpleLit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 fogAndVertexLight : TEXCOORD2;
                half4 color : COLOR;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD3;
                #endif
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=position.positionCS;
                output.positionWS=position.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                output.fogAndVertexLight.x=ComputeFogFactor(position.positionCS.z);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.fogAndVertexLight.yzw=VertexLighting(position.positionWS,output.normalWS);
                #endif
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord=GetShadowCoord(position);
                #endif
                return output;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;
                // Leaves from older recipes can be single sided; their undersides
                // face the camera but still receive the same real garden lights.
                lighting.normalWS=NormalizeNormalPerPixel(input.normalWS)*IS_FRONT_VFACE(facing,1.0h,-1.0h);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.vertexLighting=input.fogAndVertexLight.yzw;
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    lighting.shadowCoord=input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #endif

                SurfaceData surface=(SurfaceData)0;
                surface.albedo=input.color.rgb;
                // Recipes store linear vertex colors. URP expects gamma albedo
                // only when the whole project uses gamma lighting.
                #if defined(UNITY_COLORSPACE_GAMMA)
                    surface.albedo=LinearToSRGB(surface.albedo);
                #endif
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                // Matte, non-emissive plants share the scene's HDR exposure and
                // bloom. No fixed light floor keeps plants bright at midnight.
                half4 color=UniversalFragmentBlinnPhong(lighting,surface);
                color.rgb=MixFog(color.rgb,input.fogAndVertexLight.x);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }
}
