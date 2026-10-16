Shader "Hidden/Water/VolumeBackDepth"
{
    Properties
    {
        _Swell_Height("Swell height", Float) = .012
        _Swell_Length("Swell length", Float) = .8
        _Swell_Speed("Swell speed", Float) = .85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Cull Front
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "../Includes/WaterSystem.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Swell_Height, _Swell_Length, _Swell_Speed;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float eyeDepth:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                float3 position,normal;
                WaterVolumeSwell_float(input.positionOS,input.normalOS,0,_Time.y,_Swell_Height,_Swell_Length,_Swell_Speed,position,normal);
                float3 world=TransformObjectToWorld(position);
                Varyings o; o.positionCS=TransformWorldToHClip(world);o.eyeDepth=-TransformWorldToView(world).z;return o;
            }
            float Frag(Varyings input):SV_Target { return input.eyeDepth; }
            ENDHLSL
        }
    }
}
