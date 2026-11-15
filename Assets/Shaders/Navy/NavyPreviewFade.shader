Shader "TowerDefence/Navy/Preview Fade"
{
    Properties { _Opacity("Opacity",Range(0,1))=.3 _Color("Fade Color",Color)=(.3,.8,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)half4 _Color;float _Opacity;CBUFFER_END
            struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=TransformObjectToHClip(i.positionOS.xyz);return o;}
            half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(_Color.rgb,_Opacity*_Color.a);}
            ENDHLSL
        }
    }
}
