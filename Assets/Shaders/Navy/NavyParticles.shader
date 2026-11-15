Shader "TowerDefence/Navy/Particles"
{
    Properties { [HDR]_Tint("Tint",Color)=(1,1,1,1) _Opacity("Opacity",Range(0,1))=1 _Shape("Disc 0 / Ring 1 / Star 2 / Coin 3",Float)=0 [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Destination Blend",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha [_DstBlend] ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Tint; half _Opacity,_Shape; CBUFFER_END
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 p=i.uv*2-1;float r=length(p);
                half a=_Shape<.5?smoothstep(1,.03,r*r):_Shape<1.5?1-smoothstep(.07,.20,abs(r-.7)):
                    _Shape<2.5?saturate(1-r)*saturate(.25/(abs(p.x*p.y)+.025)):_Shape<3.5?1-smoothstep(.68,.82,max(abs(p.x),abs(p.y))):
                    _Shape<4.5?(1-smoothstep(.04,.11,abs(r-.78)))*.35+pow(saturate(1-abs(atan2(p.y,p.x))/.45),3)*saturate(1-r)*.8:
                    1-smoothstep(.003,.015,abs(r-.9));
                half4 c=i.color*_Tint;c.a*=a*_Opacity;clip(c.a-.005);return c;
            }
            ENDHLSL
        }
    }
}
