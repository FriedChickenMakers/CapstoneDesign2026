Shader "Capstone/Garden Cloud Particle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float a=exp(-dot(p*float2(1.3,2),p*float2(1.3,2))*3);
                a+=exp(-dot((p-float2(-.35,.04))*float2(2.4,2.5),(p-float2(-.35,.04))*float2(2.4,2.5))*2)*.5;
                a+=exp(-dot((p-float2(.3,.08))*float2(2,2.8),(p-float2(.3,.08))*float2(2,2.8))*2)*.5;
                return half4(i.color.rgb,i.color.a*saturate(a)*smoothstep(1,.6,max(abs(p.x),abs(p.y))));
            }
            ENDHLSL
        }
    }
}
