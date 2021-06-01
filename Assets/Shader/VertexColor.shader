// ref https://github.com/keijiro/Pcx

Shader "PointCloud/VertexColor"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _PointSize("Point Size", Float) = 0.0008
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile _ UNITY_COLORSPACE_GAMMA

            #include "Disk.cginc"

            ENDCG
        }
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #define PCX_SHADOW_CASTER 1

            #include "Disk.cginc"

            ENDCG
        }
    }
}