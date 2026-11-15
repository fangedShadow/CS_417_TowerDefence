Shader "TowerDefence/Navy/Solar Blast"
{
    Properties
    {
        [HDR]_Color("Blast Color",Color)=(.05,.8,1,1)
        _BlastRadius("Blast Radius",Float)=3
        _ExpansionSpeed("Expansion Speed",Float)=5
        _Duration("Duration",Float)=1.2
        _Emission("Emission",Range(0,6))=2.3
        _Opacity("Opacity",Range(0,1))=1
        _Age("Effect Age",Float)=0
        _ScrollSpeed("Radial UV Scroll",Vector)=(1.3,-1.7,0,0)
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
            CBUFFER_START(UnityPerMaterial) half4 _Color;float4 _ScrollSpeed;float _BlastRadius,_ExpansionSpeed,_Duration,_Emission,_Opacity,_Age;CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);i.positionOS.xz*=(_BlastRadius*2);o.pos=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 p=i.uv*2-1;float radius=length(p)*_BlastRadius;
                float angle=atan2(p.y,p.x);float front=min(_BlastRadius,max(.04,_Age*_ExpansionSpeed));
                float wave=1-smoothstep(.055,.28,abs(radius-front));
                float core=saturate(1-radius/max(front,.04))*.23;
                float flow=.6+.4*sin(angle*11+radius*8+_Time.y*_ScrollSpeed.y*6+sin(angle*5+_Time.y*_ScrollSpeed.x)*1.5);
                float fade=1-smoothstep(.12,max(.13,_Duration),_Age);
                half a=saturate((wave*flow+core)*fade*_Opacity);clip(a-.003);
                return half4(lerp(_Color.rgb,half3(.85,1,1),wave*.55)*_Emission,a);
            }
            ENDHLSL
        }
    }
}
