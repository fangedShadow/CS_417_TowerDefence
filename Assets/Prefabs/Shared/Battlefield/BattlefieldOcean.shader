Shader "TowerDefence/BattlefieldOcean"
{
    Properties { _BaseColor("Water Color", Color) = (0.02,0.32,0.55,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                output.world=world; output.positionCS=TransformWorldToHClip(world); return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.world.xz;
                float phase=p.y*2.8+p.x*0.55+sin(p.x*1.35-p.y*.45)*1.15+sin(p.x*3.2+p.y*.7)*.24+_Time.y*.9;
                float a=sin(phase);
                float b=sin(p.x*.45-p.y*1.4+sin(p.x*.8+p.y*.2)*.9-_Time.y*.65);
                float swell=sin(p.x*.19+p.y*.37+_Time.y*.25);
                float wave=a*.43+b*.29+swell*.28;
                float crest=(1-smoothstep(.04,.23,abs(a)))*smoothstep(-.5,.8,b);
                half3 deep=SRGBToLinear(half3(.012,.18,.31)), bright=SRGBToLinear(half3(.018,.36,.56));
                half3 color=lerp(deep,bright,saturate(.53+wave*.55));
                color=lerp(color,SRGBToLinear(half3(.16,.57,.73)),crest*.75);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
