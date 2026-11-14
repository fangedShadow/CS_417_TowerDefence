Shader "TowerDefence/BattlefieldTerrain"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.52,0.66,0.09,1)
        _LightColor("Sunlit Variation", Color) = (0.72,0.77,0.17,1)
        _DarkColor("Shaded Variation", Color) = (0.33,0.49,0.05,1)
        _SoilColor("Earth / Crevices", Color) = (0.65,0.46,0.23,1)
        _Surface("Surface: Grass 0, Sand 1, Stone 2, Vertex Color 3", Float) = 0
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor, _LightColor, _DarkColor, _SoilColor;
        float _Surface;
    CBUFFER_END
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS); o.color=v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.world.xz;
                float broad=noise(p*0.42), detail=noise(p*2.4), grain=noise(p*9.5);
                half3 color=lerp(_DarkColor.rgb,_LightColor.rgb,broad*0.7+detail*0.3);
                if (_Surface<0.5)
                {
                    float dry=noise(p*1.05+noise(p*0.2)*2.1);
                    float soil=smoothstep(0.58,0.76,dry)*(0.4+0.6*smoothstep(0.36,0.64,broad));
                    color=lerp(color,_SoilColor.rgb,soil*0.85);
                    color*=0.91+grain*0.17;
                }
                else if (_Surface<1.5)
                {
                    color=lerp(_BaseColor.rgb,_LightColor.rgb,detail*0.5+broad*0.35);
                    float2 cell=frac(p*2.1+float2(noise(p*0.6),noise(p*0.6+8))*0.35);
                    float seam=smoothstep(0.017,0.055,min(min(cell.x,1-cell.x),min(cell.y,1-cell.y)));
                    color*=lerp(0.95,1.02,seam)*(0.92+grain*0.14);
                }
                else if (_Surface<2.5)
                {
                    float band=0.5+0.5*sin(i.world.y*5.6+noise(p*1.7)*2.0);
                    color=lerp(_DarkColor.rgb,_LightColor.rgb,detail*0.5+broad*0.25+band*0.25);
                    color*=0.9+grain*0.16;
                }
                else color=SRGBToLinear(i.color.rgb)*_BaseColor.rgb;
                half3 n=normalize(i.normal);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(dot(n,sun.direction));
                half3 ambient=max(SampleSH(n),half3(0.19,0.21,0.23));
                half shadow=lerp(0.50,1,sun.shadowAttenuation);
                half3 lit=color*(ambient+sun.color*(0.16+diffuse*0.78)*shadow);
                return half4(lit,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 shadowVert(A v):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p=TransformObjectToWorld(v.positionOS.xyz),n=TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 dir=normalize(_LightPosition-p);
                #else
                float3 dir=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,dir));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 shadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
