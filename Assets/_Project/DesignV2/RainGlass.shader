Shader "RainyApartment/WindowRain"
{
    Properties { _Tint("玻璃颜色", Color)=(.32,.53,.66,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=float2(input.world.x+input.world.z,input.world.y)*float2(21,5);
                float2 cell=floor(uv);float rnd=Hash(cell);float2 f=frac(uv);
                float2 center=float2(.2+.6*rnd,.12+.6*Hash(cell+7));
                float2 d=(f-center)*float2(1,2.6);
                float drop=(1-smoothstep(.038,.083,length(d)))*step(.38,rnd);
                float trail=(1-smoothstep(.012,.035,abs(f.x-center.x)))*smoothstep(center.y,center.y+.5,f.y)*.32*step(.7,rnd);
                float drift=frac(input.world.y*.23+_Time.y*.13+Hash(float2(cell.x,0)));
                float streak=(1-smoothstep(.008,.025,abs(f.x-center.x)))*(1-smoothstep(.0,.2,abs(drift-.5)))*step(.78,rnd);
                return half4(_Tint.rgb+drop*.2,.025+drop*.24+trail*.13+streak*.15);
            }
            ENDHLSL
        }
    }
}
