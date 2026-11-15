Shader "TowerDefence/BattlefieldFoam"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.world=TransformObjectToWorld(i.positionOS.xyz);o.pos=TransformWorldToHClip(o.world);o.uv=i.uv;return o; }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float edge=i.uv.y;
                float ripple=0.5+0.5*sin(edge*22-i.uv.x*0.7+sin(i.uv.x*3)*0.8-_Time.y*1.2);
                float alpha=(1-smoothstep(0.65,1,edge))*lerp(0.12,0.9,smoothstep(0.5,0.8,ripple));
                alpha=max(alpha,(1-smoothstep(0.03,0.23,edge))*0.85);
                return half4(lerp(half3(0.13,0.67,0.8),half3(0.78,0.96,0.94),ripple),alpha);
            }
            ENDHLSL
        }
    }
}
