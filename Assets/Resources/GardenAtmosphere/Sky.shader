Shader "Capstone/Garden Clock Sky"
{
    Properties
    {
        _Daylight("Daylight",Float)=1
        _Twilight("Twilight",Float)=0
        _SunDirection("Sun direction",Vector)=(0,1,0,0)
        _SkyAspect("Sky display aspect",Float)=1
        _GalaxyIntensity("Galaxy intensity",Range(0,1))=1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
            float _Daylight, _Twilight, _SkyAspect, _GalaxyIntensity;
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
            float skyHash(float3 p)
            {
                p=frac(p*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float skyNoise(float3 p)
            {
                float3 cell=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                float4 lower=float4(skyHash(cell),skyHash(cell+float3(1,0,0)),
                    skyHash(cell+float3(0,1,0)),skyHash(cell+float3(1,1,0)));
                float4 upper=float4(skyHash(cell+float3(0,0,1)),skyHash(cell+float3(1,0,1)),
                    skyHash(cell+float3(0,1,1)),skyHash(cell+float3(1,1,1)));
                float2 slices=lerp(float2(lower.x,upper.x),float2(lower.y,upper.y),f.x);
                slices=lerp(slices,lerp(float2(lower.z,upper.z),float2(lower.w,upper.w),f.x),f.y);
                return lerp(slices.x,slices.y,f.z);
            }
            float3 galaxy(float3 direction, out float starDensity)
            {
                // Sample the unit world sphere rather than longitude/latitude or
                // camera coordinates: the atmosphere has no seam and stays in place
                // when the camera orbits. Three noise scales keep this mobile-friendly.
                float broad=skyNoise(direction*3.8+float3(8.7,2.4,5.1));
                float medium=skyNoise(direction*11.7+float3(3.1,9.2,6.4));
                float fine=skyNoise(direction*31.1+float3(7.3,5.8,2.6));
                float latitude=dot(direction,normalize(float3(.65,.50,.57)));
                float warpedLatitude=latitude+(broad-.5)*.20;
                float halo=exp2(-warpedLatitude*warpedLatitude*22);
                float core=exp2(-warpedLatitude*warpedLatitude*115);
                float cloud=smoothstep(.22,.78,broad*.38+medium*.42+fine*.20);
                float dustOffset=(warpedLatitude+(medium-.5)*.10)*26;
                float dust=exp2(-dustOffset*dustOffset)*(.40+fine*.60);
                float density=(halo*.22+core*cloud*.92)*(1-dust*.82);
                // A broad transition through cyan and blue into violet. Tint the
                // existing gas density so the purple stays a soft part of the
                // world-space atmosphere, without another noise pass or overlay.
                float hue=smoothstep(-.32,.48,dot(direction,normalize(float3(.35,-.85,.40)))
                    +(broad-.5)*.35);
                float3 gas=lerp(float3(.024,.071,.043),float3(.035,.045,.150),hue);
                gas=lerp(gas,float3(.098,.038,.185),smoothstep(.30,.92,hue)*.62);
                starDensity=halo*cloud;
                return gas*density;
            }
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);
                float height=saturate(d.y*.7+.3);
                // The garden camera looks below the horizon, so that hemisphere
                // also needs blue sky. Existing mist supplies the white lower clouds.
                // Slightly clearer blue at the horizon while the lower
                // hemisphere stays softer behind the island and its mist.
                float3 day=lerp(float3(.17,.45,.77),float3(.11,.47,.88),smoothstep(-.85,.12,d.y));
                day=lerp(day,float3(.060,.30,.75),smoothstep(.12,1,d.y));
                // These are linear output values: a .1 night sky becomes much brighter
                // after display conversion. Keep the horizon subtle instead of blue daylight.
                float3 night=lerp(float3(.009,.017,.037),float3(.0015,.004,.012),height);
                float3 color=lerp(night,day,_Daylight);
                color=lerp(color,float3(.94,.53,.37),_Twilight*pow(1-height,3)*.65);
                float galaxyStars=0;
                float nightVisibility=(1-smoothstep(.03,.30,_Daylight))*_GalaxyIntensity;
                // Uniform across the sky draw: daytime avoids all galaxy noise work.
                if (nightVisibility>.0001)
                {
                    float3 galacticLight=galaxy(d,galaxyStars);
                    float atmosphere=smoothstep(-.78,-.06,d.y);
                    color+=galacticLight*nightVisibility*atmosphere;
                    galaxyStars*=nightVisibility*atmosphere;
                }
                // Native skybox directions remain fixed in the world. Looking away
                // takes the sun/moon out of frame, without camera-relative clamps.
                float sunAlignment=dot(d,normalize(_SunDirection));
                float sunVisible=smoothstep(-.04,.015,_SunDirection.y);
                float moonVisible=smoothstep(-.04,.015,-_SunDirection.y)*(1-_Daylight*.97);
                float sunDisc=smoothstep(cos(.032),cos(.028),sunAlignment)*sunVisible;
                float moonDisc=smoothstep(cos(.029),cos(.025),-sunAlignment)*moonVisible;
                // Only the luminous disc is HDR. The surrounding sky keeps its
                // existing exposure while the post-process spreads a small glow.
                color=lerp(color,float3(2.88,2.54,1.51),sunDisc*.96);
                color=lerp(color,float3(.79,.86,1),moonDisc*.94);
                color+=float3(1,.88,.54)*exp((sunAlignment-1)*650)*sunVisible*.09;
                color+=float3(.79,.86,1)*exp((-sunAlignment-1)*800)*moonVisible*.028;
                float2 uv=float2(atan2(d.x,d.z)/6.2831853+.5,asin(d.y)/3.14159265+.5)*float2(240,120);
                float2 cell=floor(uv);float seed=hash(cell);
                float2 center=float2(hash(cell+13.1),hash(cell+42.7))*.6+.2;
                float star=(1-smoothstep(.035,.11,length(frac(uv)-center)))*step(.973-galaxyStars*.034,seed);
                color+=star*float3(.8,.86,1)*(1-_Daylight)*smoothstep(-.3,.15,d.y);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
