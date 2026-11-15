Shader "TowerDefence/Navy/Vertex Outline"
{
    Properties { [HDR]_OutlineColor("Outline Color",Color)=(.05,.9,1,1) _Thickness("World Thickness",Float)=.045 _Opacity("Opacity",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)half4 _OutlineColor;float _Thickness,_Opacity;CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 p=TransformObjectToWorld(i.positionOS.xyz);p+=normalize(TransformObjectToWorldNormal(i.normalOS))*_Thickness;o.pos=TransformWorldToHClip(p);return o;}
            half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(_OutlineColor.rgb,_OutlineColor.a*_Opacity);}
            ENDHLSL
        }
    }
}
