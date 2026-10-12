Shader "Capstone/Garden Clock Sky"
{
    Properties { _Daylight("Daylight",Float)=1 _Twilight("Twilight",Float)=0 _SunDirection("Sun direction",Vector)=(0,1,0,0) _ClockHour("Local clock hour",Float)=12 _CelestialUv("Celestial screen position",Vector)=(.5,.9,0,0) _ViewAspect("View aspect",Float)=1 }
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
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; float4 screen:TEXCOORD1; };
            float _Daylight, _Twilight, _ClockHour, _ViewAspect;
            float2 _CelestialUv;
            float3 _SunDirection;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;o.screen=ComputeScreenPos(o.vertex);return o; }
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
                // Art-directed clock arc keeps the celestial body visible above the island
                // in a downward-looking garden camera. The same clock drives world lighting.
                float isDay=step(6,_ClockHour)*(1-step(18,_ClockHour));
                float2 screenUv=i.screen.xy/i.screen.w;
                float2 clockOffset=(screenUv-_CelestialUv)*float2(_ViewAspect,1);
                float distanceClock=length(clockOffset);
                float disc=1-smoothstep(.029,.032,distanceClock);
                float glow=exp(-distanceClock*distanceClock*240)*lerp(.035,.16,isDay);
                float3 celestialColor=lerp(float3(.79,.86,1),float3(1,.88,.54),isDay);
                color=lerp(color,celestialColor,disc*.94);
                color+=celestialColor*glow;
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
