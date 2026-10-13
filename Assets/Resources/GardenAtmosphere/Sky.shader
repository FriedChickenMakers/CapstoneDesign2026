Shader "Capstone/Garden Clock Sky"
{
    Properties { _Daylight("Daylight",Float)=1 _Twilight("Twilight",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0) _SkyAspect("Sky display aspect",Float)=1 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
            float _Daylight, _Twilight, _SkyAspect;
            float3 _SunDirection;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.direction=v.vertex.xyz;
                // The native orthographic sky pass uses a square angular frustum.
                // Fit its camera-space ray to the camera's display aspect before
                // sampling the world sky; no celestial position is screen-pinned.
                if (unity_OrthoParams.w > .5)
                {
                    float3 viewRay=mul((float3x3)UNITY_MATRIX_V,o.direction);
                    // Shared card RTs can have a different aspect from the final
                    // RawImage; the camera already carries that display aspect.
                    viewRay.x*=_SkyAspect;
                    o.direction=mul(viewRay,(float3x3)UNITY_MATRIX_V);
                }
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);
                float height=saturate(d.y*.7+.3);
                float3 day=lerp(float3(.75,.87,.92),float3(.22,.53,.82),height);
                // These are linear output values: a .1 night sky becomes much brighter
                // after display conversion. Keep the horizon subtle instead of blue daylight.
                float3 night=lerp(float3(.009,.017,.037),float3(.0015,.004,.012),height);
                float3 color=lerp(night,day,_Daylight);
                color=lerp(color,float3(.94,.53,.37),_Twilight*pow(1-height,3)*.65);
                // Native skybox directions remain fixed in the world. Looking away
                // takes the sun/moon out of frame, without camera-relative clamps.
                float sunAlignment=dot(d,normalize(_SunDirection));
                float sunVisible=smoothstep(-.04,.015,_SunDirection.y);
                float moonVisible=smoothstep(-.04,.015,-_SunDirection.y)*(1-_Daylight*.97);
                float sunDisc=smoothstep(cos(.032),cos(.028),sunAlignment)*sunVisible;
                float moonDisc=smoothstep(cos(.029),cos(.025),-sunAlignment)*moonVisible;
                // Only the luminous disc is HDR. The surrounding sky keeps its
                // existing exposure while the post-process spreads a small glow.
                color=lerp(color,float3(2.8,2.46,1.51),sunDisc*.96);
                color=lerp(color,float3(.79,.86,1),moonDisc*.94);
                color+=float3(1,.88,.54)*exp((sunAlignment-1)*650)*sunVisible*.09;
                color+=float3(.79,.86,1)*exp((-sunAlignment-1)*800)*moonVisible*.028;
                float2 uv=float2(atan2(d.x,d.z)/6.2831853+.5,asin(d.y)/3.14159265+.5)*float2(240,120);
                float2 cell=floor(uv);float seed=hash(cell);
                float2 center=float2(hash(cell+13.1),hash(cell+42.7))*.6+.2;
                float star=(1-smoothstep(.035,.11,length(frac(uv)-center)))*step(.973,seed);
                color+=star*float3(.8,.86,1)*(1-_Daylight)*smoothstep(-.3,.15,d.y);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
