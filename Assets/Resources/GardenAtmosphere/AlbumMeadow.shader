Shader "Capstone/Album Meadow"
{
    Properties
    {
        _AmbientTint("Sky bounce",Color)=(.4,.5,.4,1)
        _LightTint("Sun or moon",Color)=(.6,.6,.5,1)
        _LightDirection("Light direction",Vector)=(0,1,0,0)
        _EnvironmentOpacity("Ground visibility",Range(0,1))=1
        [HideInInspector] _ZWrite("Write depth",Float)=1
    }
    SubShader
    {
        // Transparent queue also keeps the fading scenery out of the photo's
        // opaque memo-picking snapshot. Plant and memo occlusion stay unchanged.
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientTint, _LightTint;
                float4 _LightDirection;
                float _EnvironmentOpacity, _ZWrite;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // Thin grass receives diffuse light from both sides; broad sky
                // bounce comes from the same clock values as the garden. This
                // isolated preview does not rely on a later global SH update.
                half diffuse=.45h+.55h*abs(dot(normalize(input.normalWS),normalize(_LightDirection.xyz)));
                half3 color=input.color.rgb*(_AmbientTint.rgb+_LightTint.rgb*diffuse);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    color=LinearToSRGB(color);
                #endif
                // A single angle-driven alpha covers the ENTIRE field. Neither
                // camera direction, world position nor vertex alpha cuts a half
                // out of the geometry. Transparent surfaces also stop writing depth.
                return half4(color,_EnvironmentOpacity);
            }
            ENDHLSL
        }
    }
}
