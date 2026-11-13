Shader "Capstone/Garden Cloud Particle"
{
    Properties
    {
        _Noise("Mist density volume",3D)="white"{}
        _Daylight("Daylight",Float)=1
        _Twilight("Twilight",Float)=0
        _LightDirection("World light direction",Vector)=(0,1,0,0)
        _IslandOrigin("Island origin",Vector)=(0,0,0,0)
        _CloudSeconds("Wind time",Float)=0
        _GardenLamp0("First lamp position and range",Vector)=(0,0,0,0)
        _GardenLamp1("Second lamp position and range",Vector)=(0,0,0,0)
        _GardenLampColor("Warm lamp radiance",Color)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Preserve opaque sky alpha when this RT is composed by a UI RawImage.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler3D _Noise;
            CBUFFER_START(UnityPerMaterial)
            float _Daylight, _Twilight, _CloudSeconds;
            float3 _LightDirection, _IslandOrigin;
            float4 _GardenLamp0, _GardenLamp1;
            float3 _GardenLampColor;
            CBUFFER_END
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float3 world:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            float LampScatter(float3 world, float4 lamp)
            {
                float distanceSquared = dot(world-lamp.xyz,world-lamp.xyz);
                float falloff = saturate(1-distanceSquared/max(lamp.w*lamp.w,.0001));
                // Bounded, local single-scattering approximation. The same actual
                // point light positions/ranges illuminate meshes in URP.
                return step(.01,lamp.w)*falloff*falloff/(1+distanceSquared);
            }
            half4 frag(v2f i):SV_Target
            {
                float3 local=i.world-_IslandOrigin;
                float3 wind=float3(_CloudSeconds*.0007,-_CloudSeconds*.00031,_CloudSeconds*.00049);
                float3 p=local*float3(.050,.071,.054)+wind+i.color.rgb*.19;
                // Three volume samples, without ray marching or a shaped cloud outline.
                float n0=tex3D(_Noise,p).r;
                float n1=tex3D(_Noise,p*2.37+float3(.31,.67,.13)-wind*.6).r;
                float n2=tex3D(_Noise,p*5.11+float3(.72,.19,.53)+wind*.4).r;
                float density=saturate((n0*.58+n1*.29+n2*.13-.30)*2.6);
                density*=.65+.35*density;
                float2 edge=abs(i.uv-.5)*2;
                // This only feathers the particle boundary. The 3D field supplies wisps.
                float feather=(1-smoothstep(.25,1,edge.x))*(1-smoothstep(.20,1,edge.y));
                // Keep the upper central garden clear while letting mist drift below it.
                float clearCentre=(1-smoothstep(1.25,2.4,length(local.xz)))*smoothstep(-1.15,-.15,local.y);
                float elevation=saturate(local.y*.12+.55);
                float shade=saturate(.4+n0*.33+_LightDirection.y*.10+elevation*.12);
                float3 night=lerp(float3(.019,.029,.048),float3(.071,.093,.13),shade);
                float3 day=lerp(float3(.78,.84,.88),float3(.97,.985,1),shade);
                day=lerp(day,float3(.73,.54,.49),_Twilight*.6);
                float lightOpacity=lerp(.65,1,_Daylight);
                float scattering=LampScatter(i.world,_GardenLamp0)+LampScatter(i.world,_GardenLamp1);
                float3 mistColor=lerp(night,day,_Daylight)+_GardenLampColor*scattering*.85;
                return half4(mistColor,density*feather*i.color.a*lightOpacity*(1-clearCentre*.98));
            }
            ENDHLSL
        }
    }
}
