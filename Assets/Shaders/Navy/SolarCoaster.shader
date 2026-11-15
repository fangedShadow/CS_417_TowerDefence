Shader "TowerDefence/Navy/Solar Coaster"
{
    Properties
    {
        [HDR]_Color("Energy Color",Color)=(.04,.8,1,1)
        _Emission("Emission",Range(0,6))=2
        _Opacity("Opacity",Range(0,1))=1
        _ScrollSpeed("Scrolling UV Speed",Vector)=(2,-.65,0,0)
        _FlowScale("Flow Frequency",Float)=5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color;float4 _ScrollSpeed;float _Emission,_Opacity,_FlowScale;CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 flow=i.uv+_Time.y*_ScrollSpeed.xy;
                float bands=.5+.5*sin(flow.x*_FlowScale*6.283+sin(flow.y*12.0)*1.8);
                float veins=pow(saturate(bands),4);
                float edge=smoothstep(0,.16,i.uv.y)*smoothstep(1,.80,i.uv.y);
                float tips=smoothstep(0,.07,i.uv.x)*smoothstep(1,.93,i.uv.x);
                half alpha=saturate(edge*tips*(.25+veins*.75)*_Opacity*_Color.a);clip(alpha-.003);
                return half4(lerp(_Color.rgb,half3(.9,1,1),veins*.72)*_Emission,alpha);
            }
            ENDHLSL
        }
    }
}
