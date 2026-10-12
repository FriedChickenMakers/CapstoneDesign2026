Shader "Capstone/Garden Cloud Particle"
{
    Properties
    {
        _Noise("Cloud density",2D)="gray"{}
        _Daylight("Daylight",Float)=1
        _Twilight("Twilight",Float)=0
        _LightDirection("Light in camera space",Vector)=(0,1,1,0)
        _CloudSeconds("Wind time",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Preserve opaque sky alpha in the garden RT. Blending alpha by SrcAlpha
        // would make UI RawImage composite the bright canvas through night clouds.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Noise;
            CBUFFER_START(UnityPerMaterial)
            float _Daylight, _Twilight, _CloudSeconds;
            float3 _LightDirection;
            CBUFFER_END
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            void lobe(float2 p,float2 center,float radius,inout float distance,inout float2 dome)
            {
                float2 q=(p-center)/radius;
                float next=(length(q)-1)*radius;
                // A smooth union keeps lighting continuous between neighbouring lobes;
                // choosing one nearest sphere creates visible straight shading seams.
                float blend=saturate(.5+.5*(next-distance)/.23);
                distance=lerp(next,distance,blend)-.23*blend*(1-blend);
                dome=lerp(q,dome,blend);
            }
            half4 frag(v2f i):SV_Target
            {
                // RGB carries independent shape seeds, not a per-particle light color.
                float seed=i.color.r, variant=i.color.g, thin=step(.75,i.color.b);
                float2 p=(i.uv-.5)*float2(3.3,2.5);
                float mirror=lerp(-1,1,step(.5,seed));
                p.x*=mirror;
                float2 drift=float2(_CloudSeconds*.00027,_CloudSeconds*.00011);
                float2 origin=float2(seed*7.31,variant*5.17);
                // Exactly three texture samples. No ray marching or fragment noise loops.
                float n0=tex2D(_Noise,p*.053+origin+drift).r;
                float n1=tex2D(_Noise,p*.137+origin.yx-drift*.63).r;
                float n2=tex2D(_Noise,p*.379+origin*.73+drift*.41).r;
                float d=10;
                float2 dome=0;
                lobe(p,float2(-1.01,-.06),.32+seed*.10,d,dome);
                lobe(p,float2(-.65,.12+variant*.19),.46+seed*.05,d,dome);
                lobe(p,float2(-.16,.31+seed*.12),.48+variant*.09,d,dome);
                lobe(p,float2(.46,.19-variant*.12),.46+seed*.08,d,dome);
                lobe(p,float2(.98,-.07),.32+variant*.08,d,dome);
                lobe(p,float2(.04,-.12),.64,d,dome);
                float3 normal=normalize(float3(dome.x*mirror,dome.y,sqrt(saturate(1-dot(dome,dome)))));
                // Scalloped tops and a flatter, uneven base resemble fair-weather cumulus.
                // The distant layer has stronger erosion and lower opacity for broken wisps.
                d=max(d,-.39-p.y+(n0-.5)*.12);
                d+=(n0-.5)*lerp(.22,.49,thin)+(n1-.5)*.11+(n2-.5)*.035;
                float coverage=1-smoothstep(-.035,.055,d);
                float interior=saturate(-d*3.5);
                float density=lerp(.63,.96,interior)*lerp(1,.55+n1*.45,thin);
                float shade=saturate(.42+dot(normal,normalize(_LightDirection))*.23+p.y*.22+(n0-.5)*.26+(n1-.5)*.09);
                float3 night=lerp(float3(.008,.014,.027),float3(.033,.048,.071),shade);
                float3 daylight=lerp(float3(.43,.53,.64),float3(.94,.96,.95),shade);
                float3 sunset=lerp(float3(.30,.22,.29),float3(.99,.62,.35),shade);
                daylight=lerp(daylight,sunset,_Twilight*.82);
                float3 color=lerp(night,daylight,_Daylight);
                // A narrow feather belongs at the edge, never a whole Gaussian ellipse.
                return half4(color,coverage*density*i.color.a);
            }
            ENDHLSL
        }
    }
}
